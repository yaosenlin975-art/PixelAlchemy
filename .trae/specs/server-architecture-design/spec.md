# Server Architecture Design Spec

> 来源：综合 `Docs/多人联机帧同步对战设计.md` / `Docs/帧同步Netcode设计.md` / `Docs/协议与序列化规范.md` / `Docs/构建与部署.md` / `Docs/数据分析埋点规格.md` / `Docs/架构决策记录.md` / `Docs/项目搭建与贡献指南.md`
> 部署目标：上线运营，<10K DAU
> 技术栈：.NET 8 + Fantasy-Net 2025.2.1402 + MongoDB（不引入 Redis）+ NLog 5.5.1
> 模式：2-8 人帧同步像素落沙对战（默认 4 人乱斗），服务器中继方案 B（不跑像素模拟）

---

## Why

NoitaCA 项目 27 份设计文档已收口（ADR D1–D6 拍板、协议 wire 定稿 ProtocolVersion=2、`InputPayload` 8B），客户端侧需求已由 `project-baseline-from-design-docs/spec.md` 落实为 12 能力域 SHALL 级需求。但服务端架构（Fantasy-Net 7-Scene 拓扑 / MongoDB 8 集合持久化 / 帧中继流水线 / 跨进程编排 / 热更隔离）尚无可验证的需求基线，直接进入实施将导致：① Fantasy 框架能力被冗余组件替代（如自建 Redis Session/跨进程 RPC）；② 协议层 wire 与服务端业务逻辑边界模糊；③ 反作弊/热更/重连等横切关注点缺乏服务端集成视角的验收点。本 spec 把服务端架构固化为 12 能力域 SHALL 级需求，作为 `Server/` 目录实施的单一事实源。

---

## What Changes

- **新增**：将 Fantasy-Net 8 项内置能力（Session/Roaming/SphereEvent/Addressable/MongoDB/多协议/进程编排/ECS 源生成器）映射为 7-Scene 拓扑的 SHALL 级需求，禁止引入 Redis/MQ 等冗余组件
- **新增**：把 8 个 MongoDB 集合（accounts/players/match_history/replays/bans/analytics_events/room_configs/mmr_seasons）的索引设计与 TTL 策略固化为可验证检查点
- **新增**：把帧中继流水线（30Hz 聚合 → ValidateInput 6 项 → FrameBatch 广播 → 状态哈希多数派裁决 → 慢客户端三档追赶 → 30s 重连）固化为服务端集成视角的 WHEN/THEN 场景
- **新增**：把 G2C_MatchSettle 精简 wire（仅 `MmrDelta` 服务端权威）的服务端汇编流程 + match_history 落库 + MMR 原子更新固化为可验证需求
- **新增**：把 Hotfix.dll RSA 签名 + Battle 状态禁 LoadFromStream + 版本对齐四项校验的服务端执行点固化为可验证需求
- **新增**：把混合派生埋点模型（服务端权威子集 E1/E2/E6/E7/E8 + 客户端上报经状态哈希校验 E3/E4/E5）的服务端 ingestion 流程固化为可验证需求
- **不变**：不修改任何 27 份设计文档的协议 wire、ADR 决策、约束；本 spec 为服务端架构的需求化镜像
- **不变**：不重复定义 `project-baseline-from-design-docs/spec.md` 已覆盖的客户端需求（DOTS/输入/UI/美术/关卡等），仅在服务端集成视角下做必要的引用与衔接
- **BREAKING**：本 spec 中标注"引用 baseline 能力域 X"的 Requirement 不再展开客户端侧 Scenario，仅补服务端侧 Scenario；如需完整场景集，需同时查阅 baseline spec

---

## Impact

- **Affected specs**: `project-baseline-from-design-docs/spec.md`（本 spec 为其服务端架构补集，引用其能力域 4/7/12 不重复展开）
- **Affected code**:
  - 服务端：`Server/Main`（入口+启动+NLog）/ `Server/Entity`（数据实体+`Generate/NetworkProtocol`）/ `Server/Hotfix`（玩法逻辑+协议处理器+反作弊规则参数+场景事件时间线）
  - 协议：`Tools/NetworkProtocol/Outer/OuterMessage.proto`（已定稿，ProtocolVersion=2）
  - 配置：`Tools/ProtocolExportTool/ExporterSettings.json`（Windows 绝对路径）/ `Server/Main/NLog.config` / Fantasy `MachineConfig` / `ProcessConfig` / `SceneConfig` / `WorldConfig`
  - CI：`dotnet restore --locked-mode`（Fantasy-Net 2025.2.1402 / NLog 5.5.1 锁定）
- **Affected docs**: 全部 27 份 `Docs/*.md`（本 spec 为其服务端架构需求化镜像，不替代原文）
- **Affected checklist/tasks**: `.trae/specs/server-architecture-design/checklist.md` 与 `tasks.md`（已存在，本 spec 为其上游需求基线）

---

## ADDED Requirements

### 能力域 1：进程拓扑与 Scene 编排

### Requirement: 7-Scene 拓扑划分（基于 Fantasy SceneConfig）
The system SHALL 通过 Fantasy `SceneConfig` 声明 7 个 SceneType：Gate / Login / Match / Game / Realm / Replay / Analytics，各 Scene 职责单一、协议归属明确，禁止跨职责混用（引用 `Docs/多人联机帧同步对战设计.md §5.1` Fantasy 三层归属）。

| SceneType | 职责 | 协议 | 进程拓扑 |
|---|---|---|---|
| Gate | 客户端唯一入口，鉴权后转发到内部 Scene | Outer KCP | 1 个进程 |
| Login | 账号注册/登录/Token 签发/设备指纹上报 | Outer KCP | 1 个进程 |
| Match | 匹配队列（Quick/Custom）+ 房间管理 + 观战列表 | Inner | 1 个进程 |
| Game (Battle) | 房间实体、帧中继、ValidateInput、状态哈希裁决、结算、场景事件注入 | Inner | N 个进程（水平扩展） |
| Realm | 排行榜查询、MMR 季节管理、玩家档案查询 | Inner | 1 个进程 |
| Replay | 录像存储与回放分发 | Inner | 1 个进程 |
| Analytics | 埋点 ingestion、事件归档 | Inner | 1 个进程 |

#### Scenario: Gate 仅接收外网 KCP
- **WHEN** 客户端发起首连
- **THEN** 仅 Gate Scene 监听外网 KCP 端口；Login/Match/Game/Realm/Replay/Analytics 均 Inner 协议，不暴露外网

#### Scenario: Game Scene 水平扩展
- **WHEN** 并发对局数增长
- **THEN** Game Scene 可水平扩展为 N 个进程实例，其他 6 个 Scene 各保持 1 个进程（按 `<10K DAU` 容量规划）

#### Scenario: 开发态单进程合并启动
- **WHEN** 服务端以 `--m Develop` 启动
- **THEN** 7 个 Scene 全部注册到同一进程，`SceneConfig` 仍声明完整拓扑，便于本地调试

#### Scenario: 进程拓扑非硬编码
- **WHEN** 切换开发/生产环境
- **THEN** Scene/Process 拓扑通过 `SceneConfig`/`ProcessConfig`/`WorldConfig` 声明，不在 `Main` 入口硬编码

---

### Requirement: Fantasy 框架能力复用（禁止引入冗余组件）
The system SHALL 复用 Fantasy-Net 8 项内置能力满足跨进程通信/会话/数据库/分布式寻址/热更需求，禁止引入 Redis / 自建跨进程 RPC / 自建 Pub/Sub 中间件 / 自建 Session 表 / 自建实体定位表。

| 需求 | Fantasy 内置能力 | 禁止引入 |
|---|---|---|
| 客户端会话 | 内置 Session | Redis Session 表 |
| 跨进程通信 | Roaming 路由（Gate 自动转发到内部 Scene） | 自建跨进程 RPC |
| 跨服事件广播 | `SphereEvent.PublishToRemoteSubscribers` | 自建 Pub/Sub 中间件 |
| 分布式对象寻址 | Addressable + RouteId + AddressableId | 自建实体定位表 |
| 数据库 | WorldConfig 配置的 MongoDB | Redis 缓存层 |
| 多协议 | TCP/KCP/WebSocket/HTTP 一键切换 | 自建协议适配器 |
| 进程编排 | MachineConfig/ProcessConfig/SceneConfig/WorldConfig | 自建编排脚本 |
| ECS 实体管理 | Entity-Component-System + 源生成器零反射 | 自建实体框架 |

#### Scenario: Roaming 替代自建跨进程通信
- **WHEN** Gate 收到客户端请求需路由到 Login/Match/Game
- **THEN** 通过 Fantasy Roaming 路由透明转发，不出现自建跨进程通信通道代码

#### Scenario: 内置 Session 替代 Redis Session
- **WHEN** 客户端建立连接
- **THEN** Gate 使用 Fantasy 内置 Session 管理连接生命周期，SessionId→PlayerId 映射驻留内存，不查 Redis

#### Scenario: Addressable 替代自建实体定位
- **WHEN** Match Scene 需定位某 GameRoom Entity
- **THEN** 通过 Fantasy Addressable + RouteId 寻址，不出现自建实体定位表

#### Scenario: SphereEvent 替代自建 Pub/Sub
- **WHEN** 对局结束需通知 Realm（刷新排行）/ Replay（落盘）/ Analytics（埋点）
- **THEN** 通过 `SphereEvent.PublishToRemoteSubscribers` 异步广播，不引入 Redis Pub/Sub 或外部 MQ

#### Scenario: WorldConfig MongoDB 替代硬编码连接
- **WHEN** 服务端启动
- **THEN** MongoDB 连接串通过 `WorldConfig` 配置注入，Hotfix 业务代码不直接硬编码连接串

---

### 能力域 2：网络协议与连接管理

### Requirement: 全 KCP 协议栈
The system SHALL 对所有客户端-服务端通信使用 KCP 协议（外网），内部 Scene 间通信走 Fantasy Roaming（不暴露外网）；KCP 参数针对 30Hz 帧同步优化（引用 `Docs/多人联机帧同步对战设计.md §3.1` 数据流）。

#### Scenario: 客户端 → Gate KCP
- **WHEN** 客户端连接服务端
- **THEN** 建立 KCP 连接到 Gate Scene 外网端口，`C2G_Input`/`C2G_StateHash`/`C2G_ReconnectRequest` 等所有消息均走 KCP

#### Scenario: Gate → 内部 Scene Roaming
- **WHEN** Gate 收到客户端请求需转发到内部 Scene
- **THEN** 通过 Fantasy Roaming 路由到目标 Scene（Login/Match/Game），响应原路经 Gate 回传客户端

#### Scenario: KCP 参数优化
- **WHEN** 配置 KCP
- **THEN** `nodelay=true`, `interval=10ms`, `resend=2`, `nc=true`，针对 30Hz 帧同步低延迟场景调优

#### Scenario: 协议无 TCP/WebSocket 混用
- **WHEN** 审计服务端网络配置
- **THEN** 全 Scene 网络协议统一 KCP，无 TCP/WebSocket 混用（HTTP 仅用于健康检查端点，非对局通信）

---

### Requirement: 30 秒断线重连窗口（引用 `Docs/帧同步Netcode设计.md §5`）
The system SHALL 为断线玩家保留 GameRoom 席位 30 秒，期间持续保存输入队列（最近 30 帧）、玩家层快照（每 60 帧一个，保留 180 帧）、frame_batch 缓冲（最多 900 帧）；30 秒未重连判负，房间继续 N-1 人对局。

#### Scenario: 30s 内重连成功
- **WHEN** 玩家断线后在 30s 内发起 `C2G_ReconnectRequest{RoomId, PlayerId, LastConfirmedFrame}`
- **THEN** 服务端验证房间存在 + 玩家未被判负 → 下发最新全量快照（分 64 片每片 16KB，`G2C_SnapshotChunk`）→ 客户端 `C2G_SnapshotAck{Complete=true}` → 服务端补发 N+1 到 N+840 增量 `G2C_FrameBatch` → 客户端 Replay 追上 `CurrentServerFrame` → 恢复输入同步

#### Scenario: 30s 超时判负
- **WHEN** 30 秒未重连
- **THEN** 判负，房间继续 N-1 人对局，席位释放

#### Scenario: 断线瞬间 Reconnecting 宽限（非战斗无敌帧）
- **WHEN** 玩家断线瞬间
- **THEN** 角色进入 `Reconnecting` 宽限 `DisconnectedGraceFrames≈90` 帧（3s@30Hz）免秒，属网络层容错（与 ADR D3 无 i-frames 不冲突）；该计数器作为玩家层状态量写入每 60 帧 `MurmurHash3` 状态哈希，保证三端哈希一致

#### Scenario: 5s 未重连冻结
- **WHEN** 玩家断线 5s 未重连
- **THEN** 角色进入 `Reconnecting`/`Frozen` 状态：模拟冻结、退出战斗判定、掉落装备到地面（不引入 HP 字段，与 ADR D4 一致）

#### Scenario: 快照校验失败兜底
- **WHEN** 重连快照分片丢失或校验失败
- **THEN** 客户端发 `C2G_SnapshotAck{Complete=false}`，服务端重发缺失分片；最多重试 3 次，仍失败则判负

---

### Requirement: 慢客户端三档追赶（引用 `Docs/帧同步Netcode设计.md §6.3`）
The system SHALL 服务端 `SlowClientDetector` 检测客户端输入延迟帧数，按三档处理：警告（15 帧）、踢出（30 帧）；客户端侧 `CatchUpController` 按 lag 范围决定追赶步数（lag≤1/2-5/6-10/>10）。

> **冲突标注**：本 spec 以 `Docs/帧同步Netcode设计.md §6.3` 为权威源。`.trae/specs/server-architecture-design/checklist.md 类别5`（≤3/4-32/>32）与 `tasks.md S4-6`（5-15/15-30/>30）的三档阈值口径与权威源不一致，实施前需统一以 §6.3 为准。

#### Scenario: 客户端侧三档追赶（引用 §6.3.2）
- **WHEN** 客户端检测到 `lag = serverFrame - localFrame`
- **THEN** `lag≤1` 跑 1 步；`2≤lag≤5` 跑 lag 步；`6≤lag≤10` 跑 5 步；`lag>10` 请求全量快照重置（走重连流程）

#### Scenario: 服务端警告（15 帧）
- **WHEN** 服务端检测玩家 `lag > 15` 帧
- **THEN** 下发 `G2C_NetworkWarning{LagFrames, Level=Warning}`，客户端 UI 提示网络差

#### Scenario: 服务端踢出（30 帧）
- **WHEN** 服务端检测玩家 `lag > 30` 帧（1 秒）
- **THEN** 踢出该玩家（判负 + 房间继续），不硬扛积压

---

### 能力域 3：账号与认证

### Requirement: DeviceId + Token 认证（引用 `Docs/数据分析埋点规格.md §7.3` + `Docs/多人联机帧同步对战设计.md §6.6`）
The system SHALL 以 `DeviceId`（设备级稳定、重装不变）+ `Token`（含 AccountId + 过期时间）为认证方式；`DeviceId`/`InstallId` 在客户端经 SHA-256 哈希后通过 `C2G_DeviceReport`（ProtoBuf 低频一次性）上报，不传明文，不关联账号，不进 30Hz 输入流，不 bump `ProtocolVersion`。

#### Scenario: 首次注册
- **WHEN** 客户端首次连接上报 `C2G_DeviceReport{DeviceId=hash, InstallId=hash}`
- **THEN** Login Scene 查询 `accounts` 集合（`DeviceId` 唯一索引去重），不存在则创建账号 + 同步初始化 `players` 文档（生成 `PlayerId` + 默认 MMR=1000）

#### Scenario: 重复 DeviceId 注册
- **WHEN** 同一 `DeviceId` 再次上报
- **THEN** `accounts.device_id` 唯一索引拒绝重复插入，返回已存在账号；不创建新 `players` 文档

#### Scenario: Token 签发
- **WHEN** 登录成功
- **THEN** Login Scene 签发 Token（含 AccountId + 过期时间戳，默认 7 天有效），Token 校验使用 Fantasy 内置机制，不引入外部鉴权组件

#### Scenario: Token 过期
- **WHEN** 客户端携带过期 Token 请求
- **THEN** Gate 返回 `G2C_AuthFailed`（错误码区分过期/无效/缺失），触发客户端重新登录流程

#### Scenario: Token 伪造
- **WHEN** 客户端携带伪造 Token（签名不匹配）
- **THEN** Gate 校验签名失败，拒绝请求并断开连接

#### Scenario: C2G_DeviceReport 不进 30Hz 流
- **WHEN** 客户端连接/重连
- **THEN** `C2G_DeviceReport` 仅一次性上报，不进入 30Hz `C2G_Input` 流，不参与状态哈希

#### Scenario: DeviceId 哈希格式
- **WHEN** 客户端采集 `DeviceId`/`InstallId`
- **THEN** 上报前 `SHA-256` 哈希（输入=canonical UUID 字符串，小写去连字符；输出=小写 hex 完整 64 字符，不截断），wire 类型为 `string`

---

### Requirement: Gate Token 校验中间件
The system SHALL Gate Scene 收到除 Login 路由外的所有请求时强制校验 Token，校验通过后将 PlayerId 注入 Session 上下文用于后续路由寻址。

#### Scenario: 无 Token 请求被拒
- **WHEN** 客户端未携带 Token 请求 Match/Game 路由
- **THEN** Gate 拦截，返回 `G2C_AuthFailed{Reason=Missing}`

#### Scenario: Token 校验通过后注入 Session
- **WHEN** Token 校验通过
- **THEN** PlayerId 注入 Session 上下文，后续 Gate → Match/Game 路由时附加目标 Scene 寻址（Addressable），不自建实体定位表

---

### 能力域 4：匹配服务

### Requirement: Quick 匹配（引用 `Docs/多人联机帧同步对战设计.md §7.2.2` + `§7.3`）
The system SHALL Quick 匹配按 MMR 分桶（±50/±100/±200/±500）匹配，等待 10s 扩大桶范围，等待 30s 提示"是否接受 AI 填充"；凑齐 `RoomConfig.PlayerCount`（默认 4，[2,8]）人后创建 Game Entity，默认配置 256×256 / Kill 边界 / 标准刷新率 / 5 分钟。

#### Scenario: MMR 相近优先匹配
- **WHEN** 多名玩家在 ±50 桶内
- **THEN** 先到先匹配，凑齐 `PlayerCount` 人触发创建 Game Entity

#### Scenario: 超时扩大桶范围
- **WHEN** 玩家等待 10s 未匹配到
- **THEN** 桶范围扩大到 ±100，再 10s 到 ±200，再 10s 到 ±500

#### Scenario: AI 填充提示
- **WHEN** 玩家等待 30s 未匹配到
- **THEN** 提示"是否接受 AI 填充"，玩家可选择继续等待或接受 AI

#### Scenario: 匹配成功创建 GameRoom
- **WHEN** 凑齐 `PlayerCount` 人
- **THEN** Match Scene 通过 Roaming 通知 Game Scene 创建 GameRoom Entity，生成 RoomId + 分配 PlayerId × N + 确定性 Seed，下发 `G2C_MatchResult{RoomId, ServerSessionId, PlayerIds, ArenaConfig}` 给全房玩家

---

### Requirement: Custom 自建房（引用 `Docs/多人联机帧同步对战设计.md §7.2.1`）
The system SHALL Custom 模式房主创建房间生成 6 字符邀请码，其他玩家输入邀请码加入；房主点击"开始"前强制校验全房版本对齐（HotfixHash/AssetManifestHash/ProtocolVersion/BurstVersion 四项一致）。

#### Scenario: 房主创建房间
- **WHEN** 玩家点击"创建房间"
- **THEN** 服务器生成 6 字符邀请码（如 "A3K9X2"），房间进入 Lobby 状态；房主可配置 `PlayerCount`（[2,8]）/`ArenaConfig`/`BoundaryMode`/道具刷新率/对局时长

#### Scenario: PlayerCount 强制夹取
- **WHEN** 房主设置 `PlayerCount=10` 或 `PlayerCount=1`
- **THEN** 服务器强制 `Math.Clamp(playerCount, 2, 8)` 夹取为 8 或 2

#### Scenario: 邀请码加入
- **WHEN** 玩家输入邀请码
- **THEN** 校验码有效 + 房间未满 + 未开始，加入房间；任一不满足拒绝

#### Scenario: 房主开始前版本对齐校验
- **WHEN** 房主点击"开始"
- **THEN** 服务器遍历房间内 N 个玩家，比对 `HotfixHash`（全一致或在白名单兼容组）+ `AssetManifestHash`（全一致）+ `ProtocolVersion=2`（全一致）+ `BurstVersion=1.8.29`（全一致），任一不匹配拒绝开始并提示"玩家 X 资源版本不一致"

---

### Requirement: 房间生命周期状态机（引用 `Docs/多人联机帧同步对战设计.md §7.2`）
The system SHALL 房间状态机为 `Lobby → Battle → Result → Closed`，状态转换由服务端权威驱动。

#### Scenario: Lobby → Battle
- **WHEN** 房主点击"开始"且版本对齐校验通过
- **THEN** 房间进入 Battle 状态，GameRoom Entity 创建，帧中继开始

#### Scenario: Battle → Result
- **WHEN** 对局结束（仅剩 1 名玩家或超时）
- **THEN** 房间进入 Result 状态，下发 `G2C_MatchSettle`，5s 后回到 Lobby 或 Closed

#### Scenario: 所有人离开 → Closed
- **WHEN** 房间内所有玩家离开
- **THEN** 房间进入 Closed 状态，GameRoom Entity 销毁，资源释放（NativeArray/RingBuffer）

---

### Requirement: room_configs 集合归档
The system SHALL 房间配置（`PlayerCount`/`ArenaConfig`/`BoundaryMode`/`MatchMode`/邀请码/房主/状态/创建时间）运行时驻留内存，对局结束后归档落库到 `room_configs` 集合，索引 `RoomId`（唯一）+ `Status` + `CreatedAt` 支持回查。

#### Scenario: 房间配置可落库可查询
- **WHEN** 对局结束
- **THEN** 房间配置归档到 `room_configs`，可按 `RoomId` 回查用于录像/分析

---

### 能力域 5：帧中继服务

### Requirement: 30Hz 输入聚合（引用 `Docs/多人联机帧同步对战设计.md §5.3`）
The system SHALL Game Scene 每 33ms（30Hz）聚合所有玩家 `C2G_Input`（`InputPayload` 8B / ProtocolVersion=2）打包为 `G2C_FrameBatch`（MemoryPack 30Hz）；服务器不跑像素模拟，只做输入中继 + 输入验证 + 场景事件注入 + 状态哈希比对。

#### Scenario: 30Hz tick 精度
- **WHEN** 对局进行中
- **THEN** 每 33ms 聚合一次，tick 精度 ±1ms，无积压

#### Scenario: 缺失玩家补空输入
- **WHEN** 某玩家本帧未上报 `C2G_Input`
- **THEN** 服务端补空输入（`MoveX=0, MoveY=0, ActionFlags=0, AimAngle=0`）填充到 FrameBatch，不阻塞帧推进

#### Scenario: G2C_FrameBatch 广播
- **WHEN** 聚合完成
- **THEN** `G2C_FrameBatch{Frame, Inputs[]}` MemoryPack 序列化广播给房间内所有在线玩家，wire 体积与 N 成线性

---

### Requirement: ValidateInput 6 项校验（引用 `Docs/协议与序列化规范.md §8` + baseline 能力域 7）
The system SHALL 服务端 `AntiCheatSystem.ValidateInput`（Entity 层 AOT）对每个 `C2G_Input` 执行 6 项校验，任一失败拒绝该帧输入并计入 desync 报告，拒绝时不污染 FrameBatch。

> 校验 6 项详见 baseline spec 能力域 7「反作弊 - 输入验证」，本 spec 不重复展开客户端侧 Scenario，仅补服务端集成视角。

#### Scenario: 首帧通过
- **WHEN** 玩家发送首帧输入（`LastInputFrame` 初始化为 -1）
- **THEN** `dt = frame - (-1) = frame + 1 ≥ 1`，通过频率检查

#### Scenario: 拒绝不污染 FrameBatch
- **WHEN** ValidateInput 任一项失败
- **THEN** 该玩家本帧输入被丢弃，FrameBatch 中该玩家位置补空输入（不阻塞其他玩家帧推进），拒绝事件含玩家 id / 帧号 / 失败项编号可追溯审计

#### Scenario: 反作弊规则参数可热更
- **WHEN** 需调整反作弊阈值/冷却数值
- **THEN** `AntiCheatConfig`（阈值/冷却数值）位于 Hotfix 层可热更，`AntiCheatSystem` 执行器位于 Entity 层 AOT 不可热更（引用 `Docs/构建与部署.md §3.1`）

---

### Requirement: 状态哈希多数派裁决（引用 `Docs/多人联机帧同步对战设计.md §5.4` + `§9.2` + baseline 能力域 7）
The system SHALL 服务端每 60 帧收集客户端 `C2G_StateHash`，多数派阈值 = `⌊RoomConfig.PlayerCount / 2⌋ + 1`；少数派 `MismatchCount++`，连续 3 次踢出；全部不一致标记 `FlagMatchAnomaly` 录像送人工分析。

> 客户端侧状态哈希字段（MurmurHash3 全 9 字段）详见 baseline spec 能力域 5，本 spec 不重复。

#### Scenario: 多数派达成
- **WHEN** N=4，3 客户端哈希一致，1 客户端不同
- **THEN** maxCount=3 ≥ threshold=3，多数派哈希为权威；少数派 `MismatchCount++`

#### Scenario: 连续 3 次少数派踢出
- **WHEN** 玩家连续 3 次处于少数派
- **THEN** `KickPlayer(player, "Determinism mismatch")`，房间继续 N-1 人对局

#### Scenario: 全部不一致送人工
- **WHEN** 所有客户端哈希两两不同（maxCount < threshold）
- **THEN** `FlagMatchAnomaly(frame, "No clear majority")`，录像送人工分析

---

### Requirement: 场景事件帧延迟解密 AES-128-CTR（引用 `Docs/多人联机帧同步对战设计.md §6.5` + baseline 能力域 7）
The system SHALL Game Scene `SceneScheduler` 按 `ItemSpawnTable` 权重确定性推导刷新时间线，`G2C_SceneEvent` 的 `EncryptedPayload` 用 AES-128-CTR 加密，密钥派生自 `frame = TargetFrame - 3`。

#### Scenario: SceneScheduler 预滚时间线
- **WHEN** 对局开始（Frame=0）
- **THEN** `SceneScheduler` 加载 `ItemSpawnTable`，按权重随机选择道具（武器30/法术卷轴20/治疗药水25/护盾10/速度增益10/炸弹5），`TargetFrame = currentFrame + TelegraphFrames(90)`，加入 `EventTimeline`

#### Scenario: 帧延迟解密防预读
- **WHEN** 服务器生成 `G2C_SceneEvent{TargetFrame=currentFrame+90, EncryptedPayload}`
- **THEN** 客户端在 `TargetFrame - 3` 帧时才派生解密密钥（HMACSHA256(roomSecret, frameBytes)[..16]），此前无法读取 `ItemSpawnEvent.PosX/PosY`

#### Scenario: Telegraph 公平预告
- **WHEN** 服务器决定在 TargetFrame 刷新道具
- **THEN** 全客户端在 `TargetFrame - 90`（3s 前）收到事件并播放光柱 telegraph，3s 后道具落地可拾取

---

### Requirement: 观战 RingBuffer + G2C_SpectatorBatch（引用 `Docs/帧同步Netcode设计.md §6.4`）
The system SHALL Game Scene 维护观战 RingBuffer（默认 450 帧 = 15s 容量），观战者订阅后接收 `G2C_SpectatorBatch`（MemoryPack 30Hz）；默认观战延迟 10s（300 帧），死亡玩家观战延迟 1s（30 帧）。

#### Scenario: 默认观战 10s 延迟
- **WHEN** 观战者订阅房间
- **THEN** 接收 `currentFrame - 300` 帧的 `G2C_SpectatorBatch`，防止信息泄露影响对局

#### Scenario: 死亡玩家 1s 延迟
- **WHEN** 玩家死亡后观战剩余对局
- **THEN** 接收 `currentFrame - 30` 帧的 `G2C_SpectatorBatch`，降低体验损失

#### Scenario: G2C_SpectatorBatch.DelayedEvents 预留
- **WHEN** 当前 MVP 观战模式
- **THEN** `G2C_SpectatorBatch.DelayedEvents`（field 3）在 proto 中已注释，启用观战模式（后 MVP）时再补 `RouteType.Config` 注册，不破坏现有 wire

---

### 能力域 6：结算服务

### Requirement: G2C_MatchSettle 精简 wire（引用 `Docs/协议与序列化规范.md §6.1`）
The system SHALL 对局状态机进入 `MatchEnded` 后服务端下发 `G2C_MatchSettle`（ProtoBuf 低频一次性，不进 30Hz，不 bump `ProtocolVersion`）；仅 `MmrDelta`（int32，ELO 变体 K=32）为服务端权威计算，其余 `Rank`/`Kills`/`Deaths`/`MaterialContribution` 为确定性模拟量客户端从本地 sim 派生。

#### Scenario: 仅 MmrDelta 服务端权威
- **WHEN** 对局结束
- **THEN** `G2C_MatchSettle{MatchId, TotalFrames, Players[PlayerId, Rank, Kills, Deaths, MaterialContribution, MmrDelta], YourselfRank}` 下发，其中 `MmrDelta` 由服务端 ELO K=32 算法计算，其余字段客户端已可从本地 sim 推导

#### Scenario: 客户端派生字段
- **WHEN** 客户端收到 `G2C_MatchSettle`
- **THEN** `IsWinner`/`IsDraw` 由 `Rank` + `Players[]` 推导（多人同 `Rank=1` 即平局）；`MatchDurationMs = TotalFrames / 30 * 1000`；`DisplayName` 按 `PlayerId` 从房间名册查得

#### Scenario: 回放可用性恒 false（MVP）
- **WHEN** MVP 阶段结算界面
- **THEN** `ReplayAvailable` 恒 false，隐藏"查看回放"按钮（P4+ 启用时作为前向兼容追加字段，不破坏现有 wire）

---

### Requirement: match_history 落库
The system SHALL 对局结束写入 `match_history` 集合（`MatchId` / `Arena` / `Players[]` / `Winner` / `SettleAt` / `TotalFrames` / `Seed` / `ArenaConfig`），复合索引 `(PlayerId, MatchId)` + `SettleAt` 降序支持玩家历史查询。

#### Scenario: 每局结束落库
- **WHEN** 对局结束 + `G2C_MatchSettle` 下发完成
- **THEN** 写入一条 `match_history` 文档，`MatchId` 唯一索引去重

#### Scenario: 按 PlayerId 查历史
- **WHEN** 客户端查询玩家历史对局
- **THEN** 通过 `Players.PlayerId` 索引查询，按 `SettleAt` 降序返回分页结果

---

### Requirement: MMR 原子更新（引用 `Docs/多人联机帧同步对战设计.md §7.3`）
The system SHALL 按 `MmrDelta` 更新 `players` 集合的 `Mmr`/`Wins`/`Losses`/`Season` 字段，更新原子（`findAndModify` 或事务），并发结算不脏写；ELO 变体 `K=32` / `BaseMmr=1000`，名次分 `S_i = (N - Rank_i)/(N-1) ∈ [0,1]`。

#### Scenario: MMR 更新正确
- **WHEN** 对局结束 `MmrDelta` 计算完成
- **THEN** `players.Mmr += MmrDelta`，`Wins`/`Losses` 按 `IsWinner`/`IsDraw` 累加，更新原子

#### Scenario: 并发结算不脏写
- **WHEN** 同一玩家多场对局并发结算
- **THEN** `findAndModify` 或事务保证 MMR 更新原子，无脏写

#### Scenario: 结算后跨服通知
- **WHEN** MMR 更新完成
- **THEN** 通过 `SphereEvent.PublishToRemoteSubscribers` 异步通知 Realm（刷新排行榜缓存）+ Replay（落盘录像）+ Analytics（埋点），不阻塞帧中继主循环

---

### 能力域 7：反作弊汇总

### Requirement: desync 报告归档
The system SHALL 状态哈希分歧时归档 desync 报告（`MatchId` / 分歧帧 / 各客户端哈希 / `InputRingBuffer` 快照），供后续离线 replay 定位（从分歧帧 F-300 离线重放 + 字段级 diff）。

#### Scenario: desync 报告完整归档
- **WHEN** 状态哈希多数派裁决检测到分歧
- **THEN** 归档 desync 报告到 `analytics_events` 子集合或独立字段，含 `MatchId` / 分歧帧 / 各客户端哈希 / `InputRingBuffer` 快照

#### Scenario: 离线 replay 定位
- **WHEN** 需定位 desync 根因
- **THEN** 从分歧帧 F-300 离线重放，字段级 diff 定位 F0/F1/模块/字段/平台五项信息

#### Scenario: desync 累计阈值触发风控
- **WHEN** 单玩家累计 desync 次数超阈值
- **THEN** 触发可疑标记，写入 `bans` 集合或风控队列

---

### Requirement: 反作弊规则归属（Hotfix vs Entity）
The system SHALL 反作弊执行器 `AntiCheatSystem`（含 `ValidateInput`/`CompareHashes`）位于 Entity 层 AOT 不可热更；反作弊规则参数 `AntiCheatConfig`（阈值/冷却数值）位于 Hotfix 层可热更（引用 `Docs/构建与部署.md §3.1`）。

#### Scenario: AntiCheatSystem 不可热更
- **WHEN** 尝试在 Hotfix 层修改 `ValidateInput` 逻辑
- **THEN** CI 静态校验阻断（Hotfix 禁触 `NoitaCA.Simulation` 内部确定性类型规则同理适用服务端 Entity 层反作弊执行器）

#### Scenario: AntiCheatConfig 可热更
- **WHEN** 需调整反作弊阈值
- **THEN** `AntiCheatConfig`（Hotfix 层数据对象）可热更，不影响进行中对局（对局中锁定版本）

---

### 能力域 8：排行榜与档案

### Requirement: 排行榜查询分页（引用 `Docs/项目搭建与贡献指南.md` Realm 职责）
The system SHALL Realm Scene 分页查询 `players` 集合按 `Mmr` 降序（`pageSize` 默认 50），返回排名 + `PlayerId` + `DisplayName` + `Mmr`；Top-N 缓存于内存定时刷新（非每次查库）。

#### Scenario: 分页 + MMR 降序
- **WHEN** 客户端请求排行榜第 1 页
- **THEN** 返回 Top 50 玩家按 `Mmr` 降序，性能 < 100ms（索引覆盖）

#### Scenario: Top-N 内存缓存
- **WHEN** 排行榜被频繁查询
- **THEN** Top-N（如 Top 100）缓存于 Realm Scene 内存，定时刷新（如每 60s），不每次查库

#### Scenario: Realm 通过 Roaming 接收查询
- **WHEN** Gate/Match/Game 需查询排行榜
- **THEN** 通过 Fantasy Roaming 路由到 Realm Scene，不自建跨进程 RPC

---

### Requirement: 玩家档案查询
The system SHALL Realm Scene 查询玩家档案返回 `players` + `mmr_seasons` 聚合结果（MMR / 胜负场 / 最近对局），不含敏感字段（`token`/`device_id`）。

#### Scenario: 档案数据完整
- **WHEN** 客户端查询玩家档案
- **THEN** 返回 MMR / 胜负场 / 最近对局（关联 `match_history`，按 `SettleAt` 降序）

#### Scenario: 敏感字段不外泄
- **WHEN** 档案查询结果序列化
- **THEN** `token`/`device_id` 字段不包含在响应中

---

### Requirement: 赛季管理（mmr_seasons 集合）
The system SHALL `mmr_seasons` 集合支持赛季切换（旧赛季归档、新赛季重置初始 MMR），赛季结束时 MMR 快照存档；新赛季 MMR 软重置（按上赛季 MMR × 0.75 + 250）。

#### Scenario: 赛季切换软重置
- **WHEN** 新赛季开始
- **THEN** 玩家 MMR 软重置为 `上赛季 MMR × 0.75 + 250`，旧赛季 MMR 快照存 `mmr_seasons`

#### Scenario: 历史赛季可查
- **WHEN** 查询历史赛季
- **THEN** `mmr_seasons` 按 `SeasonId`（唯一索引）返回归档数据

---

### Requirement: 封禁校验（bans 集合）
The system SHALL 登录/匹配/进对局前查询 `bans` 集合，命中封禁记录拒绝并返回剩余时长；`bans` 集合 TTL 索引 `ExpireAt` 自动过期解封。

#### Scenario: 封禁玩家被拒
- **WHEN** 封禁玩家尝试登录/匹配
- **THEN** 命中 `bans` 集合记录，拒绝并返回剩余时长

#### Scenario: 过期自动解封
- **WHEN** 封禁记录 `ExpireAt` 到期
- **THEN** TTL 索引自动删除文档，玩家可正常登录/匹配

#### Scenario: 封禁记录可查
- **WHEN** 查询封禁历史
- **THEN** `bans` 集合按 `PlayerId` 索引返回封禁原因/封禁人/过期时间

---

### 能力域 9：录像服务

### Requirement: 录像存储（replays 集合）
The system SHALL Replay Scene 对局结束后存储录像（`MatchId` / `InputSequence` / `Seed` / `ArenaConfig` / `PlayerIds` / `Snapshot` / `Size` / `ExpireAt`），录像格式为 `InputRingBuffer` 序列化（仅输入流 + Seed，不存全量状态），存储体积 < 1MB/局。

#### Scenario: 录像可落库
- **WHEN** 对局结束
- **THEN** 通过 `SphereEvent` 异步接收对局结束事件，写入 `replays` 集合，关联 `MatchId`

#### Scenario: 录像体积合理
- **WHEN** 5 分钟 4 人对局录像
- **THEN** 存储体积 < 1MB（仅输入流 + Seed，不存全量状态）

#### Scenario: 按 MatchId 查询
- **WHEN** 客户端请求回放
- **THEN** 通过 `MatchId` 唯一索引查询 `replays` 集合

---

### Requirement: 30 天 TTL 索引
The system SHALL `replays` 集合 TTL 索引 `ExpireAt` 字段，30 天后自动删除文档；精彩对局可标记 `Preserved=true` 豁免 TTL。

#### Scenario: 过期录像自动删除
- **WHEN** 录像 `ExpireAt` 到期
- **THEN** TTL 索引自动删除文档

#### Scenario: Preserved 豁免
- **WHEN** 录像标记 `Preserved=true`
- **THEN** 不被 TTL 删除，永久保留

---

### Requirement: 回放分发
The system SHALL Replay Scene 按需流式下发回放分片（`G2C_ReplayChunk`），客户端本地重放从 Seed + InputSequence 重建模拟，重放结果与原对局状态哈希一致。

#### Scenario: 分片下发
- **WHEN** 客户端请求回放
- **THEN** Replay Scene 分片下发 `InputSequence`，避免一次性大包

#### Scenario: 本地重放一致
- **WHEN** 客户端本地重放
- **THEN** 从 Seed + InputSequence 重建模拟，重放结果与原对局状态哈希一致（确定性回归测试）

---

### 能力域 10：埋点 ingestion

### Requirement: 混合派生模型（引用 `Docs/数据分析埋点规格.md §0.1`）
The system SHALL Analytics Scene 采用混合派生模型：服务端权威子集（E1/E2/E6/E7/E8 + 场景 spawn/despawn）从输入流 + 场景时间线 + 镜像/冷却追踪器推导；客户端上报事件（E3/E4/E5/`MaterialContribution`）依赖位置/碰撞/CA 像素，服务端用每 60 帧状态哈希多数派裁决校验后采纳（verify-don't-trust）。

#### Scenario: 服务端权威子集派生
- **WHEN** 玩家 Attack 位触发且 CD/Mana 通过
- **THEN** E2 `spell_cast` 由服务端从 Attack 边沿 + 镜像 `SelectedSpellSlot` + 权威 `SpellIds` + 冷却时间线派生

#### Scenario: 客户端上报经哈希校验
- **WHEN** E3/E4/E5/`MaterialContribution` 客户端上报值到达服务端
- **THEN** 服务端用每 60 帧状态哈希多数派裁决校验，死亡/状态变化与多数派哈希一致才采纳

#### Scenario: 客户端上报与多数派分歧
- **WHEN** E3/E4/E5 客户端上报值与状态哈希多数派不一致
- **THEN** 服务端拒收 + 告警（verify-don't-trust）

#### Scenario: 红线 - 所有事件不进 InputPayload
- **WHEN** 任意分析事件尝试进入 `InputPayload`
- **THEN** 红线禁止（对齐 `Docs/协议与序列化规范.md §8` 确定性红线）

---

### Requirement: 事件归档（analytics_events 集合）
The system SHALL Analytics Scene 接收两类事件写入 `analytics_events` 集合（`EventId` / `PlayerId` / `MatchId` / `EventType` / `Payload` / `Timestamp`），索引 `PlayerId` + `MatchId` + `EventType` + `Timestamp` 降序支持查询。

#### Scenario: 事件落库可查
- **WHEN** 事件 ingestion 完成
- **THEN** 写入 `analytics_events` 集合，可按 `PlayerId`/`MatchId`/`EventType`/时间范围查询

#### Scenario: ingestion 与 Game 解耦
- **WHEN** 对局帧中继进行中
- **THEN** Analytics 通过 `SphereEvent` 异步接收对局结束事件，不阻塞帧中继主循环

---

### Requirement: last-contact 玩家击杀归因（引用 `Docs/数据分析埋点规格.md §3`）
The system SHALL 出界触发 E5 `boundary_kill` 时，按 `last_player_contact_id` 是否存在且 `(death_frame - last_player_contact_frame) <= ATTR_WINDOW`（默认 15s = 450 帧）决定 `credited_player`；生物/环境/反应不覆盖 `last_player_contact_id`。

#### Scenario: 玩家推挤出界归信用
- **WHEN** 玩家 A 用重弹把 B 推到边缘，B 自行走出界，仍在 ATTR_WINDOW 内
- **THEN** 信用归 A（last-contact 即 A）

#### Scenario: 生物推挤出界仍归玩家
- **WHEN** 生物把 B 推出界，B 最近一次玩家接触是 5s 前的 A
- **THEN** 生物不覆盖 `last_player_contact` → 信用归 A（符合 GDD Q3「生物致死按最后接触玩家」）

#### Scenario: 自灭
- **WHEN** B 全程无玩家接触，自己走/掉出界
- **THEN** `last_player_contact_id` 空或超窗口 → 自灭（SelfElim），不计入任何人 Kills

#### Scenario: 同帧多人出界
- **WHEN** 多名玩家同一帧出界
- **THEN** 按 `death_frame` 同帧内以 `player_id` 确定性子排序定名次（与结算 `Rank` 一致）；归因各自独立计算

---

### Requirement: 下发前 sanity gate（引用 `Docs/数据分析埋点规格.md §2.4`）
The system SHALL 服务端在汇编 `G2C_MatchSettle` / 落盘 E1–E9 之前强制跑三项校验：字段对齐校验（`sum(kills) == count(boundary_kill where last_contact_player_id != null)` 且 `sum(deaths) == count(boundary_kill)`）+ 帧连续性 + 客户端上报经状态哈希多数派校验。

#### Scenario: 字段对齐校验
- **WHEN** 服务端汇编 `G2C_MatchSettle`
- **THEN** `sum(kills) == count(boundary_kill where credited_player != null)` 且 `sum(deaths) == count(boundary_kill)`（D∈{0,1}），不一致告警（归因 bug/丢帧）

#### Scenario: 帧连续性校验
- **WHEN** 服务端落盘 E1–E9
- **THEN** `match_settle.total_frames` 应 ≈ E5 最大 `death_frame` 的场次，缺口告警（服务器掉帧/日志丢失）

---

### 能力域 11：热更隔离

### Requirement: Battle 状态禁 LoadFromStream（引用 `Docs/多人联机帧同步对战设计.md §4.5.2` + `Docs/构建与部署.md §3.7`）
The system SHALL 对局 Battle 状态与 Lobby→Battle 加载过渡期绝对禁止 `AssemblyLoadContext.LoadFromStream` 替换 Hotfix.dll；服务器在对局开始时锁定 `HotfixVersion`，对局中即使客户端拉到新版本也不应用，等对局结束。

#### Scenario: Battle 中拒绝热更
- **WHEN** 对局进行中，客户端拉到新 Hotfix 版本
- **THEN** 服务器已锁定 `HotfixVersion`，新版本不应用，等对局结束

#### Scenario: 对局结束强制热更
- **WHEN** 对局结束
- **THEN** 强制走热更流程，再进下一局

#### Scenario: 热更流程
- **WHEN** 热更触发
- **THEN** 上传新 Hotfix.dll → 校验 RSA 签名 → 等待无活跃对局 → `Unload()` 旧 ALC → `LoadFromStream` 新程序集 → `EnsureLoaded()`

#### Scenario: 热更不影响进行中对局
- **WHEN** 热更前后有进行中对局
- **THEN** GameRoom 状态哈希一致（对局中锁定版本保证）

---

### Requirement: Hotfix.dll RSA 数字签名（引用 `Docs/多人联机帧同步对战设计.md §9.3` + baseline 能力域 7）
The system SHALL 采用非对称数字签名（RSA-2048 或 ECDSA-P256）：构建机签名 `SHA256(Hotfix.dll)`，客户端内置公钥验签，服务器验签 + 白名单 + 防重放。

> 客户端侧签名校验详见 baseline spec 能力域 7，本 spec 不重复，仅补服务端集成视角。

#### Scenario: 服务器验签 + 白名单
- **WHEN** 客户端上报 `(HotfixHash H, signature)`
- **THEN** 服务器用预共享公钥验签 signature 对 H 有效，且 H 在白名单内（允许多版本兼容组灰度）

#### Scenario: 防重放
- **WHEN** 同一 `(RoomId, PlayerId, H)` 在同一连接会话内重复上报
- **THEN** 第二次被拒绝；socket close 时清除旧标记，重连视为新会话可重新上报

#### Scenario: 验签失败拒绝连接
- **WHEN** 验签失败或 H 不在白名单
- **THEN** 拒绝连接（防注入修改版 Hotfix）

---

### Requirement: 版本对齐四项校验（引用 `Docs/多人联机帧同步对战设计.md §4.5.4` + baseline 能力域 12）
The system SHALL 对局开始前校验全房玩家 `HotfixHash` / `AssetManifestHash` / `ProtocolVersion=2` / `BurstVersion=1.8.29` 四项一致性。

#### Scenario: 房主点击开始前校验
- **WHEN** 房主点击"开始"
- **THEN** 服务器遍历房间内 N 个玩家，比对四项一致性，任一不匹配拒绝开始并提示"玩家 X 资源版本不一致"

#### Scenario: BurstVersion 不一致拒绝
- **WHEN** 跨版本 Burst 可能破坏确定性
- **THEN** `BurstVersion` 不一致时拒绝开始对局

#### Scenario: ProtocolVersion 不一致拒绝
- **WHEN** 客户端上报 `ProtocolVersion=1`（仍含 SpellId）
- **THEN** 服务器拒绝其进对局，返回版本不匹配错误（不 desync/崩溃）

---

### 能力域 12：数据库与持久化

### Requirement: MongoDB 8 集合（DB: noitaca）
The system SHALL 通过 Fantasy `WorldConfig` 配置 MongoDB 连接串（DB: `noitaca`），初始化 8 个集合：`accounts` / `players` / `match_history` / `replays` / `bans` / `analytics_events` / `room_configs` / `mmr_seasons`。

| 集合名 | 主要字段 | 索引 | 用途 |
|---|---|---|---|
| accounts | AccountId / Platform / DeviceId / CreateTime | AccountId(unique) / DeviceId | 账号 |
| players | PlayerId / AccountId / Name / Mmr / Wins / Losses / Season | PlayerId(unique) / AccountId / Mmr(desc) | 玩家档案 + 排行榜 |
| match_history | MatchId / Arena / Players[] / Winner / SettleAt / TotalFrames | MatchId(unique) / SettleAt(desc) / Players.PlayerId | 对局记录 |
| replays | MatchId / Frames[] / Snapshot / Size / ExpireAt | MatchId(unique) / ExpireAt(TTL) | 录像存储（30天TTL） |
| bans | PlayerId / Reason / ExpireAt / BannedBy | PlayerId / ExpireAt(TTL) | 封禁记录 |
| analytics_events | EventId / PlayerId / MatchId / EventType / Payload / Timestamp | PlayerId / MatchId / EventType / Timestamp(desc) | 埋点数据 |
| room_configs | RoomId / HostPlayerId / PlayerCount / Arena / Status / CreatedAt | RoomId(unique) / Status / CreatedAt | 自建房配置（运行时内存，归档落库） |
| mmr_seasons | SeasonId / StartAt / EndAt / BaseMmr | SeasonId(unique) | 赛季元数据 |

#### Scenario: 集合齐全
- **WHEN** 服务启动后查询 MongoDB
- **THEN** 8 个集合均存在，可查询

#### Scenario: 连接失败终止
- **WHEN** MongoDB 连接失败
- **THEN** Main 启动阶段 `Environment.Exit(1)` 终止而非吞异常

#### Scenario: 连接池可调
- **WHEN** 配置 MongoDB 连接
- **THEN** 连接池参数（`maxPoolSize`/`minPoolSize`）可通过 `WorldConfig` 调整，不硬编码于 Hotfix 代码

---

### Requirement: 索引设计
The system SHALL 为 8 个集合建立完整索引：唯一索引（防重复）+ TTL 索引（自动过期）+ 复合索引（支持查询模式）。

#### Scenario: accounts.device_id 唯一索引
- **WHEN** 重复 DeviceId 注册
- **THEN** `accounts.device_id` 唯一索引拒绝重复插入

#### Scenario: players 索引齐全
- **WHEN** 查询玩家档案 / 排行榜
- **THEN** `players.PlayerId`（唯一）/ `players.AccountId` / `players.Mmr`（降序）索引覆盖查询

#### Scenario: replays.expire_at TTL 索引
- **WHEN** 录像 `ExpireAt` 到期
- **THEN** TTL 索引自动删除文档（30 天）

#### Scenario: bans.expire_at TTL 索引
- **WHEN** 封禁记录 `ExpireAt` 到期
- **THEN** TTL 索引自动解封

#### Scenario: match_history 复合索引
- **WHEN** 查询玩家历史对局
- **THEN** `(Players.PlayerId, MatchId)` 复合索引 + `SettleAt` 降序索引支持分页查询

#### Scenario: analytics_events 时间索引
- **WHEN** 按时间范围查询埋点
- **THEN** `Timestamp` 降序 + `PlayerId` + `MatchId` + `EventType` 索引覆盖查询

---

### Requirement: Fantasy 三层归属（引用 `Docs/多人联机帧同步对战设计.md §5.1` + `Docs/项目搭建与贡献指南.md §5`）
The system SHALL 服务端三层职责清晰：Main（网络底层/Session/NLog，AOT 不可热更）/ Entity（MatchSession/GameRoom/Player 实体 + 状态哈希存储 + 输入验证 + `AntiCheatSystem` 执行器 AOT 不可热更）/ Hotfix（匹配算法/房间规则/场景事件时间线/`AntiCheatConfig` 参数，可热更）。

#### Scenario: Hotfix 不反向引用 Main
- **WHEN** 编译三层项目
- **THEN** Main 引用 Entity + Hotfix；Hotfix 引用 Entity；Hotfix 不反向引用 Main

#### Scenario: Entity 层不写业务逻辑
- **WHEN** 编写数据实体
- **THEN** Entity 层仅提供数据结构（`AccountData`/`PlayerData`/`MatchHistoryData` 等），CRUD 在 Hotfix 层封装

#### Scenario: Main 层仅入口启动
- **WHEN** 编写 Main 层代码
- **THEN** Main 仅含入口启动 + NLog 配置 + AssemblyHelper.Initialize()，不写业务逻辑

#### Scenario: Hotfix 热更使用 Fantasy ALC
- **WHEN** 热更触发
- **THEN** 使用 Fantasy 内置 `AssemblyLoadContext` 机制（`AssemblyHelper.LoadHotfixAssembly`），未自建热更框架

---

## MODIFIED Requirements

无（本 spec 为新建，无前置 server-architecture-design spec 可修改；与 `project-baseline-from-design-docs/spec.md` 的衔接通过引用实现，不修改 baseline 任何 Requirement）。

---

## REMOVED Requirements

无。

---

## 附录：ADR 决策点索引（D1–D6）

> 与 `Docs/架构决策记录.md` + `project-baseline-from-design-docs/spec.md` 附录保持一致，本 spec 仅做服务端集成视角的索引，不重复 ADR 内容。

| ADR | 决策点 | 内容 | 服务端集成影响 |
|-----|--------|------|----------------|
| **D1** | 边界死亡 | Kill（出界即死）为全模式默认 | Game Scene `PlayerDeath` 事件触发条件、`boundary_kill`（E5）埋点来源 |
| **D2** | 缩圈 | MVP 不做缩圈 | Game Scene 无缩圈调度逻辑；为未来 ≥512 大型地图预留 `ShrinkEnabled`+`ShrinkRate` |
| **D3** | Dash | 保留 Dash（bit9），MVP 无 i-frames 纯位移 | `ValidateInput` 接受 Dash 位；`dash_used`（E8）埋点 `iframes_active=false` |
| **D4** | HP 死亡 | MVP 纯出界即死，不引入 HP | `match_history`/`G2C_MatchSettle`/埋点零 HP 字段；`Deaths ∈ {0,1}`；免推铁律 §9.7 |
| **D5** | AimPower + 法术切换 | MVP 固定力度 + 循环切换 | `InputPayload` 8B 不含 AimPower；`SelectedSpellSlot` 由 SelectNext/Prev 推导，进状态哈希 |
| **D6** | 法术后坐力 | 速度冲量模型 + 允许自爆 + StoneWall/Lava=0 | 后坐力纯模拟派生不进 InputPayload、不 bump ProtocolVersion；仅修改 VelX/VelY 被 MurmurHash3 覆盖 |
| **A1** | GDD 缺 Dash | 采纳谋远段落整合进 GDD §6.1 | 无服务端影响 |
| **A2** | Unity 版本 | 统一 6000.3.19f1 | 服务端无 Unity 依赖（.NET 8） |
| **A3** | NuGet 版本 | Fantasy-Net 2025.2.1402 / NLog 5.5.1 锁定 | `Server/*.csproj` 全部锁定，CI `--locked-mode` 还原 |
| **A4** | SpellId 孤儿字段 | 删除（InputPayload 12B→8B，ProtocolVersion 1→2） | 服务端期望 `ProtocolVersion=2` 常量；旧版本客户端拒绝 |
| **A5** | Spell1/Spell2 位 | 保留为 reserved，v1 禁用 | `ValidateInput` 拒绝 bit2/3 + bit10-15 置位 |
| **A6** | AimAngle 量化 | [0,2π)×100 → int16 [0,628] | `ValidateInput` 校验 `AimAngle ∈ [0,628]` |

---

## 附录：跨文档一致性铁律（服务端视角）

> 与 `project-baseline-from-design-docs/spec.md` 附录保持一致，本 spec 仅补服务端集成视角。

1. **服务器不跑像素模拟**：Game Scene 只做输入聚合/ValidateInput/场景事件注入/状态哈希比对，不做 CA 模拟（引用 `Docs/多人联机帧同步对战设计.md §5.3`）
2. **协议零变更约束**：后坐力作为模拟派生量不进 InputPayload（8B 不变）、不 bump ProtocolVersion（引用 ADR D6）
3. **多数派裁决公式**：阈值 = `⌊RoomConfig.PlayerCount / 2⌋ + 1`（引用 `Docs/多人联机帧同步对战设计.md §9.2`）
4. **混合派生模型红线**：所有分析事件绝不进入 `InputPayload`、绝不参与状态哈希（引用 `Docs/数据分析埋点规格.md §0.1`）
5. **免推铁律**：no-HP 模式下禁止任何"免疫击退"效果（引用 ADR D4 + `Docs/数值平衡设计.md §9.7`）
6. **断线 Reconnecting 宽限 ≠ 战斗无敌帧**：`DisconnectedGraceFrames≈90` 帧（3s）属网络层容错，与 ADR D3 无 i-frames 不冲突（引用 `Docs/帧同步Netcode设计.md §5.2.1`）
7. **Hotfix 边界**：协议 handler 在 Hotfix；模拟逻辑在 AOT；Hotfix 禁触确定性类型；AOT 不反向依赖 Hotfix（引用 `Docs/构建与部署.md §3.1`）
8. **Generate 目录禁手改**：协议变更走 `ProtocolExportTool` 重新生成，逻辑在 Hotfix（引用 `Docs/项目搭建与贡献指南.md §4`）
9. **ExporterSettings.json 路径**：必须为 Windows 绝对路径（非 macOS 默认 `/Users/fantasy/...`），正斜杠兼容
10. **NuGet 版本锁定**：禁止 `*` 版本范围，启用 `RestorePackagesWithLockFile`，CI `--locked-mode` 还原（引用 ADR A3）

---

## 附录：Fantasy-Net 框架能力映射表

| 服务端需求 | Fantasy 内置能力 | 实施位置 | 禁止引入 |
|---|---|---|---|
| 客户端会话管理 | 内置 Session | Gate Scene | Redis Session 表 |
| Gate → Login/Match/Game 路由 | Roaming 跨进程路由 | Gate Scene | 自建跨进程 RPC |
| Match → Game 创建 GameRoom | ECS 实体 + Addressable + RouteId | Match Scene → Game Scene | 自建实体定位表 |
| 对局结束通知 Realm/Replay/Analytics | `SphereEvent.PublishToRemoteSubscribers` | Game Scene → 三个 Inner Scene | 自建 Pub/Sub / MQ |
| MongoDB 连接 | WorldConfig 配置 | Main 启动 | 硬编码连接串 / Redis 缓存层 |
| KCP 协议 | 多协议一键切换 | Gate Scene 外网 | 自建协议适配器 |
| 进程编排 | MachineConfig/ProcessConfig/SceneConfig/WorldConfig | Main 启动 | 硬编码拓扑 |
| 实体管理零反射 | ECS + 源生成器 | Entity 层 | 自建实体框架 |
| Hotfix 热更 | 内置 AssemblyLoadContext（AssemblyHelper.LoadHotfixAssembly） | Main 启动 + 热更流程 | 自建热更框架 |
| RouteType 路由注册 | `RoamingTypes` 枚举暴露 | RouteType.cs（Generate） | 绕过框架路由层 |

---

## 附录：与现有设计文档冲突点

> 实施前需与设计文档主理人（程驭）+ 服务端（云澈）仲裁统一。

| # | 冲突点 | 权威源（建议） | 冲突来源 | 影响范围 |
|---|---|---|---|---|
| 1 | 慢客户端三档追赶阈值 | `Docs/帧同步Netcode设计.md §6.3`（客户端 lag≤1/2-5/6-10/>10；服务端 warn=15/kick=30） | `checklist.md 类别5`（≤3/4-32/>32）+ `tasks.md S4-6`（5-15/15-30/>30）口径不一 | Game Scene `SlowClientDetector` 实现；S4-6 任务验收标准 |
| 2 | "断线无敌 3s" 语义 | `Docs/帧同步Netcode设计.md §5.2.1`（Reconnecting 宽限，非战斗无敌帧） | `project-baseline-from-design-docs/spec.md 能力域4` 使用"无敌"措辞（虽注明"网络容错窗口，非战斗无敌帧"） | 断线玩家状态机实现；与 ADR D3 无 i-frames 一致性表述 |
| 3 | NLog 版本号 | `Docs/架构决策记录.md §6 A3`（NLog 5.5.1） | `Docs/项目搭建与贡献指南.md §7`（NLog 5.3.x） | `Server/*.csproj` NuGet 锁定值 |
| 4 | MatchMode 枚举 | `Docs/多人联机帧同步对战设计.md §6.4`（QuickMatch/Ranked/Custom 三种） | 任务书"已确认决策汇总"表中"匹配模式 Quick + Custom（不做 Ranked）" | Match Scene 队列实现；`C2G_MatchRequest.Mode` 枚举值；排位 MMR 是否在 MVP 启用 |

> 冲突 4 说明：任务书明确"不做 Ranked"，但 `Docs/多人联机帧同步对战设计.md §7.2.3` 与 `§7.3`（MMR 算法）仍定义 Ranked 模式；`Docs/数据分析埋点规格.md §1.4` 亦按排位 MMR 设计看板。需主理人拍板 MVP 是否启用 Ranked：① 若不启用，Match Scene 仅 Quick + Custom 两种队列，`G2C_MatchSettle.MmrDelta` 仍可计算但不更新排位榜；② 若启用，需补 Ranked 匹配池 + 排位禁对手文本 + 强制标准配置。

---

**文档结束** — 本 spec 为 NoitaCA 服务器架构需求化镜像，与 27 份 `Docs/*.md` + `project-baseline-from-design-docs/spec.md` 交叉引用，不替代原文。所有服务端实施以本 spec 为可验证需求基线，配合 `checklist.md` + `tasks.md` 落地验收。
