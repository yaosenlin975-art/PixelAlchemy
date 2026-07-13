# Tasks - 服务器架构设计

> NoitaCA 服务器架构实施任务清单（Fantasy-Net + MongoDB，<10K DAU）
> 任务编号 `S{阶段}-{序号}`（S=Server），与项目基线 `T{phase}-{n}` 区分
> 标注 `[依赖 X]` 表示前置任务；`[可并行]` 表示可与同级任务并行；`[Fantasy-Net]` 表示依赖框架特性
> 协议层已就绪（`OuterMessage.proto` 定义完毕，ProtocolVersion=2，InputPayload 8B），本清单聚焦服务端架构实现

---

## 阶段 0：工程基础设施（前置）✅ 已完成

- [x] **S0-1**: 搭建 Fantasy 三层项目结构（Entity / Hotfix / Main） `[无依赖]` `[Fantasy-Net: asmdef + 源生成器]`
  - [x] 创建 `Server/Entity/Entity.csproj`（数据实体 + Generate，AOT 不热更）
  - [x] 创建 `Server/Hotfix/Hotfix.csproj`（玩法逻辑 + 协议处理器，热更层）
  - [x] 创建 `Server/Main/Main.csproj`（入口 + 配置，引用 Entity + Hotfix）
  - [x] 验证：`dotnet build` 三层编译通过；Main 引用 Entity+Hotfix；Hotfix 不反向引用 Main
  - [x] 验证：AssemblyHelper.Initialize() 可加载 Hotfix 程序集

- [x] **S0-2**: 配置 SceneConfig / ProcessConfig / WorldConfig（7 Scene + 进程拓扑） `[依赖 S0-1]` `[Fantasy-Net: 进程编排]`
  - [x] 定义 7 个 SceneType：Gate / Login / Match / Game / Realm / Replay / Analytics
  - [x] ProcessConfig：Gate/Login/Match/Realm/Replay/Analytics 各 1 进程；Game N 进程（水平扩展）
  - [x] WorldConfig：开发环境单进程合并启动（`--m Develop`）；生产环境多进程分离
  - [x] 验证：`--m Develop` 启动后 7 个 Scene 均注册成功，日志输出 Scene 创建信息

- [x] **S0-3**: MongoDB 连接配置 + 8 集合初始化 `[依赖 S0-1, S0-2]` `[Fantasy-Net: WorldConfig MongoDB]`
  - [x] WorldConfig 配置 MongoDB 连接串（DB: `noitaca`，mongodb://localhost:27017/）
  - [x] 初始化 8 集合：accounts / players / match_history / replays / bans / analytics_events / room_configs / mmr_seasons
  - [x] accounts: 唯一索引 `DeviceId` + `AccountId`；players: 唯一索引 `PlayerId` + `AccountId` + `Mmr`(desc)
  - [x] match_history: 索引 `MatchId`(unique) + `SettleAt`(desc) + `Players.PlayerId`(multikey)
  - [x] replays: TTL 索引 `ExpireAt`（ExpireAfter=TimeSpan.Zero，30天逻辑在 Hotfix 写入时设置）
  - [x] bans: 索引 `PlayerId` + TTL 索引 `ExpireAt`
  - [x] analytics_events: 索引 `PlayerId` + `MatchId` + `EventType` + `Timestamp`(desc)
  - [x] room_configs: 索引 `RoomId`(unique) + `Status` + `CreatedAt`
  - [x] mmr_seasons: 唯一索引 `SeasonId`
  - [x] 验证：服务启动后 MongoDB 连接成功；8 集合 + 索引均可查询

- [x] **S0-4**: KCP 协议配置（全 KCP） `[依赖 S0-2]` `[Fantasy-Net: 多协议支持]`
  - [x] Gate Scene 外网监听配置 KCP 协议（outerPort=20000）
  - [x] 内部 Scene 间通信走 TCP/Roaming（不暴露外网）
  - [x] KCP 参数：Fantasy-Net 2026 已硬编码极速参数（nodelay=1, interval=5ms, resend=2, nc=1），与 spec 高度一致
  - [x] 验证：Gate Scene KCP 监听正常（UDP 20000）

- [x] **S0-5**: NLog 日志系统适配 Scene 维度 `[依赖 S0-2]` `[可并行 S0-3/S0-4/S0-6]`
  - [x] NLog.config 按 `sceneName.appId` 分文件，7 Scene 日志分离正确
  - [x] Hotfix 层统一日志入口（SceneLogExtensions，禁 `Console.WriteLine` / `using NLog`）
  - [x] 热路径日志降级到 Trace
  - [x] 按天滚动 + maxArchiveFiles=210（30天 × 7 Scene）
  - [x] 验证：7 Scene 各自日志文件独立

- [x] **S0-6**: RouteType 注册（7 Scene 路由 + 协议路由） `[依赖 S0-1]` `[Fantasy-Net: Roaming 路由]` `[可并行 S0-3/S0-4/S0-5]`
  - [x] RouteType.Config 追加：GateRouteType(10002) / LoginRouteType(10003) / MatchRouteType(10004) / GameRouteType(10005) / RealmRouteType(10006) / ReplayRouteType(10007) / AnalyticsRouteType(10008)
  - [x] 现有 ChatRouteType=10001 保留
  - [x] 跑 ProtocolExportTool 重新生成 RouteType.cs（禁手改 Generate）
  - [x] 验证：生成的 RouteType.cs 包含全部 8 个路由；RoamingTypes 枚举返回完整列表

- [x] **S0-7**: NuGet 版本锁定 `[无依赖]` `[可并行]`
  - [x] `Server/*.csproj` 所有 PackageReference 写死版本：Fantasy-Net 2026.0.1023 / NLog 5.5.1（MongoDB.Driver 3.9.0 传递依赖）
  - [x] 启用 `RestorePackagesWithLockFile`，提交 `packages.lock.json`
  - [x] 禁止 `*` 版本范围
  - [x] 验证：`dotnet restore --locked-mode` 通过；CI 锁定还原无漂移

### 阶段 0 检查点 ✅ 全部通过
- [x] 三层项目编译通过，asmdef 边界清晰（0 警告 0 错误，Main→Entity+Hotfix，Hotfix→Entity，无反向）
- [x] 7 Scene 可启动，MongoDB 8 集合 + 索引就绪（启动日志确认连接成功 + 8 集合创建）
- [x] KCP 连接通畅，日志按 Scene 分离（Gate UDP 20000 监听，7 Scene 日志文件独立）
- [x] RouteType 生成完整，无手改 Generate（8 个路由 + RoamingTypes 暴露 + 工具生成）

---

## 阶段 1：Gate Scene（客户端唯一入口）

- [ ] **S1-1**: Gate Scene 注册 + Session 管理 `[依赖 S0-2, S0-4, S0-6]` `[Fantasy-Net: Session 管理 + Roaming]`
  - [ ] Gate Scene 启动监听 KCP 外网端口
  - [ ] 客户端连接建立 Session，分配 SessionId
  - [ ] Session 维护 `(SessionId → PlayerId/AccountId)` 映射
  - [ ] 验证：客户端连接后获得 SessionId；断线后 Session 清理；Session 可查询关联玩家

- [ ] **S1-2**: Token 校验中间件 `[依赖 S1-1, S2-3]`
  - [ ] Gate 收到请求后校验 Header 中 Token（除 Login 路由外）
  - [ ] Token 校验失败返回 `G2C_AuthFailed`（错误码区分过期/无效/缺失）
  - [ ] Token 校验通过后将 PlayerId 注入 Session 上下文
  - [ ] 验证：无 Token / 过期 Token / 伪造 Token 均被拒绝；合法 Token 通过

- [ ] **S1-3**: Gate → Login 路由 `[依赖 S1-1, S0-6]` `[Fantasy-Net: Roaming 跨进程]`
  - [ ] 注册/登录请求通过 Roaming 转发到 Login Scene
  - [ ] Login Scene 处理完成后结果通过 Roaming 回传 Gate
  - [ ] 验证：注册/登录请求正确路由到 Login；响应原路返回客户端

- [ ] **S1-4**: Gate → Match 路由 `[依赖 S1-2, S0-6]` `[Fantasy-Net: Roaming]`
  - [ ] 匹配/房间请求经 Token 校验后路由到 Match Scene
  - [ ] 验证：未校验 Token 的请求被拦截；校验通过后路由到 Match

- [ ] **S1-5**: Gate → Game 路由 `[依赖 S1-2, S0-6]` `[Fantasy-Net: Roaming + Addressable]`
  - [ ] 对局内消息（C2G_Input 等）经 Token 校验后路由到对应 Game Scene
  - [ ] 通过 Addressable + RouteId 寻址到具体 GameRoom Entity
  - [ ] 验证：C2G_Input 正确路由到目标 GameRoom；路由失败返回错误码

---

## 阶段 2：Login Scene（账号与 Token）

- [ ] **S2-1**: 账号注册（DeviceId） `[依赖 S1-3, S0-3]` `[可并行 S2-4]`
  - [ ] 接收 DeviceId + InstallId（来自 C2G_DeviceReport 通道）
  - [ ] accounts 集合插入新账号（`DeviceId` 唯一索引去重）
  - [ ] 同步初始化 players 文档（`PlayerId` 生成 + 默认 MMR=1000）
  - [ ] 验证：首次注册成功；重复 DeviceId 注册返回已存在；players 文档 MMR 初始 1000

- [ ] **S2-2**: 账号登录（DeviceId + Token） `[依赖 S2-3]` `[可并行 S2-1/S2-4]`
  - [ ] 客户端携带 DeviceId + Token 请求登录
  - [ ] 校验 Token 有效性（签名 + 过期时间）
  - [ ] Token 失效则触发重新签发流程
  - [ ] 验证：合法 Token 登录成功；过期 Token 触发重签；账号不存在返回注册提示

- [ ] **S2-3**: Token 签发 `[依赖 S1-3, S0-3]`
  - [ ] 登录成功后签发 Token（JWT 或 HMAC 签名，含 PlayerId + 过期时间戳）
  - [ ] Token 有效期默认 7 天，可配置
  - [ ] Token 存储到 accounts 集合（或内存缓存，不引入 Redis）
  - [ ] 验证：签发的 Token 可被 Gate 校验中间件验证；过期 Token 被拒绝

- [ ] **S2-4**: C2G_DeviceReport 处理 `[依赖 S1-3, S0-3]` `[可并行 S2-1/S2-2]`
  - [ ] 处理 `C2G_DeviceReport`（DeviceId 哈希 64 字符 + InstallId 哈希 64 字符）
  - [ ] 连接/重连时一次性上报，不进 30Hz 流
  - [ ] DeviceId 绑定到 Session（稳定匿名 ID，跨对局追踪用）
  - [ ] 验证：DeviceReport 上报后 Session 绑定稳定 ID；重连重新上报不冲突

- [ ] **S2-5**: accounts 集合 CRUD 封装 `[依赖 S2-1, S0-3]`
  - [ ] Entity 层封装 AccountData 实体（`DeviceId` / `PlayerId` / `Token` / `CreatedAt`）
  - [ ] Hotfix 层提供查询/更新接口（禁直接操作 MongoDB）
  - [ ] 验证：CRUD 接口可用；并发注册无数据竞争（依赖唯一索引）

---

## 阶段 3：Match Scene（匹配与房间）

- [ ] **S3-1**: Quick 匹配队列 `[依赖 S1-4, S2-2]` `[可并行 S3-2/S3-3]`
  - [ ] Quick 模式匹配队列（按 MMR 分段 ±200 范围）
  - [ ] 队列超时（默认 30s）扩大 MMR 搜索范围
  - [ ] 凑齐 2-8 人（默认 4 人）触发创建 Game Entity
  - [ ] 验证：MMR 相近玩家优先匹配；超时扩范围；人数达标触发 Game 创建

- [ ] **S3-2**: Custom 房间创建 `[依赖 S1-4]` `[可并行 S3-1/S3-3]`
  - [ ] 房主创建房间（`RoomConfig`: PlayerCount 2-8 / ArenaConfig / BoundaryMode）
  - [ ] 生成邀请码（6 位短码）
  - [ ] 房间状态：Waiting / Playing / Settled
  - [ ] 验证：房间创建成功；邀请码唯一；PlayerCount 被 `Math.Clamp(2,8)` 夹取

- [ ] **S3-3**: 房间列表查询 + 邀请码加入 `[依赖 S3-2]` `[可并行 S3-1]`
  - [ ] 查询可加入的 Custom 房间列表（Waiting 状态 + 未满员）
  - [ ] 邀请码加入房间（校验码有效 + 房间未满 + 未开始）
  - [ ] 验证：列表只返回可加入房间；无效邀请码拒绝；满员拒绝；已开始拒绝

- [ ] **S3-4**: MMR 匹配算法 `[依赖 S3-1, S0-3]`
  - [ ] ELO 变体 K=32 / BaseMmr=1000
  - [ ] 匹配分段：|MMR_a - MMR_b| ≤ 200（优先），超时逐步扩大
  - [ ] 验证：MMR 差距大的玩家不优先匹配；超时后放宽范围

- [ ] **S3-5**: 凑齐人创建 Game Entity `[依赖 S3-1/S3-2, S0-6]` `[Fantasy-Net: ECS 实体 + Addressable]`
  - [ ] Match Scene 通过 Roaming 通知 Game Scene 创建 GameRoom Entity
  - [ ] 生成 RoomId + 分配 PlayerId × N + 确定性 Seed
  - [ ] 下发 `G2C_MatchFound` 给全房玩家（含 RoomId / PlayerId / Seed / ArenaConfig）
  - [ ] 验证：GameRoom Entity 创建成功；N 个 PlayerSlot 就绪；客户端收到 MatchFound

- [ ] **S3-6**: room_configs 集合归档 `[依赖 S3-2, S0-3]`
  - [ ] 房间配置（PlayerCount / ArenaConfig / BoundaryMode / MatchMode）落 room_configs
  - [ ] 索引 RoomId 支持回查
  - [ ] 验证：房间配置可落库可查询；对局结束后配置保留用于录像/分析

---

## 阶段 4：Game Scene（帧中继核心）

- [ ] **S4-1**: GameRoom Entity 生命周期 `[依赖 S3-5, S0-3]` `[Fantasy-Net: ECS 实体 + Addressable]`
  - [ ] GameRoom Entity 创建：RoomId / PlayerSlot × N / ArenaConfig / Seed / LockstepScheduler
  - [ ] 状态机：Loading → Battle → Settling → Destroyed
  - [ ] 所有玩家加载完成（版本对齐校验通过）后进入 Battle
  - [ ] 销毁时清理所有子 Entity + 释放资源
  - [ ] 验证：生命周期状态切换正确；Destroy 后无残留 Entity；Addressable 可寻址

- [ ] **S4-2**: C2G_Input 接收与聚合（30Hz） `[依赖 S4-1]` `[Fantasy-Net: Session 消息分发]`
  - [ ] 每 33ms tick 聚合所有玩家 C2G_Input（InputPayload 8B / ProtocolVersion=2）
  - [ ] 缺失输入补空输入（`MoveX=0, MoveY=0, ActionFlags=0, AimAngle=0`）
  - [ ] 验证：30Hz tick 精度 ±1ms；缺失玩家补空输入不阻塞帧推进

- [ ] **S4-3**: ValidateInput 6 项校验 `[依赖 S4-2]`
  - [ ] 1. 帧频率：`dt = frame - LastInputFrame`，首帧 `LastInputFrame=-1` → `dt≥1` 通过；`dt=0` 或 `dt>1` 拒绝
  - [ ] 2. MoveX/MoveY 范围：`|MoveX|≤100 ∧ |MoveY|≤100`
  - [ ] 3. 模长：`√(MoveX²+MoveY²)≤100`（斜向 100,100 ≈ 141 拒绝）
  - [ ] 4. AimAngle 范围：`[0, 628]`
  - [ ] 5. ActionFlags 保留位：bit2/bit3/bit10-15 任一为 1 拒绝
  - [ ] 6. 位移幅度：单帧位移 ≤ `MaxSpeed/30 * 1.5`（容忍 1.5 倍）
  - [ ] 验证：6 项非法输入均被拒绝；合法输入通过；拒绝时不污染帧批次

- [ ] **S4-4**: G2C_FrameBatch 广播 `[依赖 S4-2, S4-3]` `[Fantasy-Net: Session 广播]`
  - [ ] 聚合后的 PlayerInput[] 打包为 G2C_FrameBatch（MemoryPack 30Hz）
  - [ ] 广播给房间内所有在线玩家
  - [ ] 验证：所有玩家收到相同 FrameBatch；wire 体积与 N 成线性；30Hz 无积压

- [ ] **S4-5**: 状态哈希多数派裁决 `[依赖 S4-1]` `[Fantasy-Net: Roaming 收集]`
  - [ ] 客户端每 60 帧上报 `C2G_StateHash`（MurmurHash3 全 9 字段）
  - [ ] 服务器收集 N 个客户端哈希，阈值 = `⌊N/2⌋+1`
  - [ ] 多数派一致的哈希为权威；少数派 `MismatchCount++`
  - [ ] 连续 3 次少数派踢出；全不一致标记 `FlagMatchAnomaly` 送人工分析
  - [ ] 验证：N=2 阈值=2；N=4 阈值=3；少数派连续 3 次踢出；全不一致触发异常标记

- [ ] **S4-6**: 慢客户端三档追赶 `[依赖 S4-2]`
  - [ ] 服务端检测客户端输入延迟帧数
  - [ ] 档位 1（延迟 5-15 帧）：客户端 2× 加速追赶
  - [ ] 档位 2（延迟 15-30 帧）：客户端 4× 加速追赶 + 服务端警告
  - [ ] 档位 3（延迟 > 30 帧）：踢出
  - [ ] 验证：三档触发条件正确；追赶消息下发；超时踢出

- [ ] **S4-7**: 30s 断线重连窗口 `[依赖 S4-1, S4-5]` `[Fantasy-Net: Session 重连]`
  - [ ] 断线瞬间玩家角色无敌 3s（网络容错）
  - [ ] 30s 内 `C2G_ReconnectRequest` 可重连：下发全量快照（分 64 片每片 16KB）
  - [ ] 30s 超时判负，房间继续 N-1 人对局
  - [ ] 验证：30s 内重连成功恢复；超时判负；无敌期内不被击杀；快照分片完整重组

- [ ] **S4-8**: 观战 RingBuffer + G2C_SpectatorBatch `[依赖 S4-4]`
  - [ ] 观战 RingBuffer 默认 450 帧（10s 延迟）；死亡玩家 1s 延迟
  - [ ] 观战者订阅后收到 G2C_SpectatorBatch（延迟帧批次）
  - [ ] G2C_SpectatorBatch.DelayedEvents 预留（schema 已注释，启用观战时补 RouteType）
  - [ ] 验证：观战延迟 ≥ 10s；死亡玩家切观战 1s 延迟；RingBuffer 不溢出

- [ ] **S4-9**: 场景事件帧延迟解密 AES-128-CTR `[依赖 S4-1]`
  - [ ] G2C_SceneEvent 的 EncryptedPayload 用 AES-128-CTR 加密
  - [ ] 密钥派生：`frame = TargetFrame - 3`（客户端在 TargetFrame-3 帧才能解密）
  - [ ] Telegraph：目标帧前 90 帧（3s）下发，落地时全客户端同步
  - [ ] 验证：TargetFrame-3 前无法解密；TargetFrame-3 后正确解密；3s 后道具/事件落地

- [ ] **S4-10**: G2C_SceneEvent 注入（SceneScheduler） `[依赖 S4-9]`
  - [ ] SceneScheduler 按 ItemSpawnTable 权重确定性推导刷新时间线
  - [ ] 武器(30)/法术卷轴(20)/治疗药水(25)/护盾(10)/速度增益(10)/炸弹(5) 按权重
  - [ ] 预滚整局时间线（`itemRng = Xorshift128Plus(Seed^派生)`）
  - [ ] 验证：同 Seed 多次生成时间线一致；权重分布符合预期；Telegraph 90 帧提前

---

## 阶段 5：结算服务

- [ ] **S5-1**: G2C_MatchSettle 精简 wire（仅 MmrDelta 权威） `[依赖 S4-1]`
  - [ ] 结算下发 G2C_MatchSettle（ProtoBuf 低频一次性）
  - [ ] 仅 `MmrDelta` 服务端权威计算（ELO K=32 Base=1000）
  - [ ] Rank/Kills/Deaths/MaterialContribution 为确定性量，客户端本地派生
  - [ ] IsWinner/IsDraw 由 Rank + Players[] 推导；MatchDurationMs = TotalFrames/30*1000
  - [ ] DisplayName 按 PlayerId 查房间名册
  - [ ] 验证：wire 仅含服务端权威字段；客户端可派生所有展示字段；MmrDelta 计算正确

- [ ] **S5-2**: match_history 落库 `[依赖 S5-1, S0-3]`
  - [ ] 对局结束写入 match_history 集合（MatchId / PlayerIds / Rank / MmrDelta / Seed / ArenaConfig / Duration / Timestamp）
  - [ ] 复合索引 `(PlayerId, MatchId)` 支持玩家历史查询
  - [ ] 验证：每局结束落库一条；可按 PlayerId 查历史；可按 MatchId 查详情

- [ ] **S5-3**: 客户端派生字段对齐 `[依赖 S5-1]`
  - [ ] 校验客户端派生的 Rank/Kills/Deaths/MaterialContribution 与服务端记录一致
  - [ ] 不一致时以服务端记录为准，标记异常
  - [ ] 验证：派生字段与服务端记录一致；不一致触发告警

- [ ] **S5-4**: MMR 更新（players 集合） `[依赖 S5-1, S0-3]`
  - [ ] 按 MmrDelta 更新 players 集合的 MMR 字段
  - [ ] 更新玩家胜负场数（Wins/Losses/Draws）
  - [ ] 验证：MMR 更新后值正确；胜负计数累加；并发更新无竞争

---

## 阶段 6：Realm Scene（档案与排行）

- [ ] **S6-1**: 排行榜查询（分页 + Mmr desc） `[依赖 S5-4, S0-3]` `[可并行 S6-2/S6-4]`
  - [ ] 分页查询 players 集合按 MMR 降序（pageSize 默认 50）
  - [ ] 返回排名 + PlayerId + DisplayName + MMR
  - [ ] 验证：分页正确；MMR 降序；性能 < 100ms（索引覆盖）

- [ ] **S6-2**: 玩家档案查询 `[依赖 S0-3]` `[可并行 S6-1/S6-4]`
  - [ ] 查询玩家信息：MMR / 胜负场 / 最近对局（关联 match_history）
  - [ ] 验证：档案数据完整；最近对局按时间倒序；空档案返回默认值

- [ ] **S6-3**: 赛季管理（mmr_seasons） `[依赖 S5-4, S0-3]`
  - [ ] 赛季创建 / 结束 / 归档（SeasonId + StartAt + EndAt）
  - [ ] 赛季结束时 MMR 快照存 mmr_seasons
  - [ ] 新赛季 MMR 软重置（按上赛季 MMR × 0.75 + 250）
  - [ ] 验证：赛季切换 MMR 软重置正确；历史赛季可查；归档数据完整

- [ ] **S6-4**: bans 集合查询与封禁校验 `[依赖 S0-3]` `[可并行 S6-1/S6-2]`
  - [ ] 登录/匹配前校验 PlayerId / DeviceId 是否在 bans 集合
  - [ ] 封禁类型：临时封禁（过期自动解封）/ 永久封禁
  - [ ] 封禁原因 + 操作人记录
  - [ ] 验证：封禁玩家被拒绝登录/匹配；过期封禁自动解除；封禁记录可查

---

## 阶段 7：Replay Scene（录像存储与回放）

- [ ] **S7-1**: 录像存储（replays 集合） `[依赖 S5-2, S0-3]`
  - [ ] 对局结束后存储录像（MatchId / InputSequence / Seed / ArenaConfig / PlayerIds / Timestamp）
  - [ ] 录像格式：InputRingBuffer 序列化（仅输入流 + Seed，不存全量状态）
  - [ ] 验证：录像可落库；存储体积合理（< 1MB/局）；可按 MatchId 查询

- [ ] **S7-2**: 30 天 TTL 索引 `[依赖 S7-1, S0-3]`
  - [ ] replays 集合 TTL 索引 `ExpireAt` 字段，30 天自动删除
  - [ ] 精彩对局可标记 `Preserved=true` 豁免 TTL
  - [ ] 验证：过期录像自动删除；Preserved 录像不删除；TTL 索引生效

- [ ] **S7-3**: 回放分发协议 `[依赖 S7-1, S1-5]`
  - [ ] 客户端请求回放 → Replay Scene 分片下发 InputSequence
  - [ ] 客户端本地重放（从 Seed + InputSequence 重建模拟）
  - [ ] 验证：回放数据完整传输；客户端可本地重放；重放结果与原对局一致

---

## 阶段 8：Analytics Scene（埋点与数据）

- [ ] **S8-1**: 埋点 ingestion `[依赖 S0-3]` `[可并行 S8-2]`
  - [ ] 接收客户端带外上报（E3/E4/E5/MaterialContribution，不进 InputPayload）
  - [ ] 接收服务端输入派生事件（E1/E2/E6/E7/E8，从输入流派生）
  - [ ] 验证：两类事件均能接收；ingestion 吞吐满足 < 10K DAU

- [ ] **S8-2**: analytics_events 落库 `[依赖 S0-3]` `[可并行 S8-1]`
  - [ ] 事件写入 analytics_events 集合（EventType / PlayerId / MatchId / Frame / Payload / CreatedAt）
  - [ ] 时间索引 `CreatedAt` + `PlayerId` 支持查询
  - [ ] 验证：事件落库可查；按 PlayerId/MatchId/时间范围查询正确

- [ ] **S8-3**: 混合派生模型 verify-don't-trust `[依赖 S8-1, S4-5]`
  - [ ] 客户端上报值（E3/E4/E5）与状态哈希多数派校验
  - [ ] 不一致 → 拒收 + 告警（verify-don't-trust）
  - [ ] 服务端权威子集（E1/E2/E6/E7/E8）从输入流确定性推导
  - [ ] 验证：不一致上报被拒收+告警；服务端派生事件与输入流一致

- [ ] **S8-4**: desync 报告归档 `[依赖 S4-5, S0-3]`
  - [ ] 状态哈希不一致时归档 desync 报告（MatchId / 分歧帧 / 各客户端哈希 / InputRingBuffer 快照）
  - [ ] 供后续离线 replay 定位（从分歧帧 F-300 离线重放 + 字段级 diff）
  - [ ] 验证：desync 报告完整归档；可按 MatchId 查询；包含足够信息用于离线定位

---

## 阶段 9：热更与安全

- [ ] **S9-1**: Hotfix.dll RSA 签名 `[依赖 S0-1]` `[Fantasy-Net: 热更]`
  - [ ] 构建机签名 `SHA256(Hotfix.dll)` → RSA-2048 私钥签名
  - [ ] 服务器维护签名白名单 + 防重放（同一 (RoomId, PlayerId, H) 不重复上报）
  - [ ] 客户端启动 AOT 内置公钥验签本地 Hotfix.dll
  - [ ] 对局开始前校验全房玩家 HotfixHash 一致
  - [ ] 验证：篡改 Hotfix.dll 被拒；防重放生效；Hash 不一致拒绝开始

- [ ] **S9-2**: Battle 状态禁 LoadFromStream `[依赖 S4-1, S9-1]`
  - [ ] 对局 Battle 状态 + Lobby→Battle 加载过渡期绝对禁止 `LoadFromStream`
  - [ ] 服务器锁定 `HotfixVersion`，新版本不应用，等对局结束
  - [ ] 对局结束强制走热更流程，再进下一局
  - [ ] 验证：Battle 中拉到新版本不应用；对局结束后强制热更

- [ ] **S9-3**: 版本对齐校验（四项） `[依赖 S9-1, S4-1]`
  - [ ] 对局开始前校验：HotfixHash / AssetManifestHash / ProtocolVersion=2 / BurstVersion
  - [ ] 任一不一致拒绝开始，提示"玩家 X 版本不一致"
  - [ ] 验证：四项一致可开始；任一不一致拒绝；错误提示定位到具体玩家+项

- [ ] **S9-4**: ProtocolExportTool 配置确认 `[依赖 S0-1]` `[可并行]`
  - [ ] 确认 `ExporterSettings.json` 路径为 Windows 绝对路径（非 macOS 默认）
  - [ ] 服务端生成目录：`Server/.../Entity/Generate/NetworkProtocol/`
  - [ ] 客户端生成目录：`Client/Unity/Assets/Scripts/Hotfix/Generate/NetworkProtocol/`
  - [ ] 禁手改 Generate 目录（已在 .gitignore 或 CI 校验）
  - [ ] 验证：跑 ProtocolExportTool 双端 Generate 正确生成；路径无 macOS 残留

---

## 阶段 10：CI/CD 与部署

- [ ] **S10-1**: 服务器构建脚本 `[依赖 S0-1, S0-7]`
  - [ ] `dotnet publish` 三层项目（Entity / Hotfix / Main）
  - [ ] 构建产物：Main.dll + Entity.dll + Hotfix.dll + 配置文件 + NLog.config
  - [ ] 构建后自动签名 Hotfix.dll（RSA）
  - [ ] 验证：构建脚本一键产出可部署包；Hotfix.dll 签名正确

- [ ] **S10-2**: Docker 化部署 `[依赖 S10-1]`
  - [ ] 编写 Dockerfile（.NET 8 runtime + MongoDB 客户端）
  - [ ] docker-compose 编排：Gate / Login / Match / Game × N / Realm / Replay / Analytics
  - [ ] 挂载配置 + 日志卷
  - [ ] 验证：docker-compose up 启动全部 Scene；容器间 Roaming 通信正常；日志卷持久化

- [ ] **S10-3**: Scene 水平扩展配置 `[依赖 S10-2, S0-2]` `[Fantasy-Net: 分布式寻址]`
  - [ ] Game Scene 水平扩展：N 个进程实例，Match 按负载分配 GameRoom
  - [ ] Addressable + RouteId 跨进程寻址 GameRoom
  - [ ] 验证：多 Game 进程实例负载均衡；跨进程寻址正确；进程宕机 GameRoom 迁移

- [ ] **S10-4**: 健康检查 `[依赖 S10-2]`
  - [ ] HTTP 健康检查端点（`/health` 返回 Scene 状态 + MongoDB 连接状态）
  - [ ] Docker healthcheck 配置
  - [ ] 告警：Scene 不可用 / MongoDB 断连 / 帧堆积
  - [ ] 验证：健康端点返回正确状态；异常触发告警；healthcheck 自动重启

---

## 阶段 11：集成测试与验证

- [ ] **S11-1**: 端到端流程测试（登录→匹配→对战→结算→排行榜） `[依赖 阶段 1-6 全部]`
  - [ ] 客户端登录 → Quick 匹配 → 4 人对局 30Hz 帧同步 → 结算 → 排行榜更新
  - [ ] 验证全链路无阻塞；MMR 更新正确；match_history 落库
  - [ ] 验证：全流程 < 8 分钟；每步（匹配/加载/结算）< 3s

- [ ] **S11-2**: 断线重连测试 `[依赖 S4-7]`
  - [ ] 对局中断线 → 30s 内重连 → 快照恢复 → 继续对局
  - [ ] 30s 超时判负测试
  - [ ] 无敌 3s 期被攻击测试
  - [ ] 验证：重连成功恢复；超时判负；无敌期生效

- [ ] **S11-3**: 反作弊验证测试 `[依赖 S4-3, S4-5, S9-1]`
  - [ ] ValidateInput 6 项非法输入均被拒绝
  - [ ] 状态哈希少数派连续 3 次踢出
  - [ ] Hotfix.dll 篡改被拒绝
  - [ ] 防重放：重复上报被拒
  - [ ] 验证：全部反作弊机制生效；异常输入/状态被正确处理

- [ ] **S11-4**: 压测（<10K DAU） `[依赖 S10-3]`
  - [ ] 无头 bot 模拟 N×M 房间并发对局
  - [ ] 测定容量上限：同时在线房间数 / 并发连接数 / MongoDB QPS
  - [ ] 限流阈值确定
  - [ ] 验证：10K DAU 下服务稳定；帧延迟 < 100ms；MongoDB 无瓶颈；限流生效

---

# Task Dependencies

## 依赖链（关键路径）

- **S0-1**（项目结构）→ **S0-2**（Scene 配置）→ **S0-3**（MongoDB）/ **S0-4**（KCP）/ **S0-6**（RouteType）→ 阶段 1-8 全部
- **S1-1**（Gate Session）→ **S1-2**（Token 中间件）→ **S1-3/S1-4/S1-5**（路由）
- **S2-3**（Token 签发）→ **S1-2**（Token 校验）→ **S2-2**（登录）
- **S3-5**（创建 Game Entity）→ **S4-1**（GameRoom 生命周期）→ **S4-2 ~ S4-10**（帧中继全部）
- **S4-1** → **S5-1**（结算）→ **S5-2**（match_history）→ **S5-4**（MMR 更新）→ **S6-1**（排行榜）/ **S6-3**（赛季）
- **S4-5**（状态哈希）→ **S8-3**（verify-don't-trust）/ **S8-4**（desync 归档）
- **S5-2** → **S7-1**（录像存储）→ **S7-2**（TTL）/ **S7-3**（回放分发）
- **S9-1**（RSA 签名）→ **S9-2**（Battle 禁热更）/ **S9-3**（版本对齐）
- **S10-1** → **S10-2** → **S10-3** / **S10-4**
- **阶段 11** 依赖阶段 1-10 全部完成

## 可并行任务组

- **阶段 0**：S0-3 / S0-4 / S0-5 / S0-6 可并行（均依赖 S0-1+S0-2）；S0-7 独立可并行
- **阶段 1**：S1-3 / S1-4 / S1-5 可并行（均依赖 S1-2 Token 校验）
- **阶段 2**：S2-1 / S2-4 可并行；S2-1 / S2-2 / S2-4 三者可并行
- **阶段 3**：S3-1（Quick）/ S3-2（Custom）/ S3-3（列表）可并行
- **阶段 4**：S4-3 / S4-4 依赖 S4-2 可并行实现；S4-8 / S4-9 / S4-10 依赖 S4-1 可并行
- **阶段 6**：S6-1 / S6-2 / S6-4 可并行
- **阶段 7**：S7-2 / S7-3 依赖 S7-1 可并行
- **阶段 8**：S8-1 / S8-2 可并行
- **阶段 9**：S9-4 独立可并行
- **阶段 10**：S10-3 / S10-4 依赖 S10-2 可并行

## Fantasy-Net 框架特性依赖

| 任务 | Fantasy 特性 |
|------|-------------|
| S0-1 | asmdef + 源生成器 |
| S0-2 | 进程编排（MachineConfig / ProcessConfig / SceneConfig / WorldConfig） |
| S0-3 | WorldConfig MongoDB 集成 |
| S0-4 | 多协议（KCP） |
| S0-6 / S1-3~S1-5 | Roaming 跨进程路由 |
| S1-1 / S4-7 | Session 管理 + 重连 |
| S3-5 / S4-1 | ECS 实体 + Addressable + RouteId 分布式寻址 |
| S4-4 / S4-5 | Session 广播 + Roaming 收集 |
| S9-1 | 热更（Hotfix 程序集） |
| S10-3 | 分布式对象寻址（Addressable + RouteId） |

---

# 验收标准对照

| 验收项 | 覆盖任务 |
|--------|----------|
| 7 个 Scene 全实现 | S1-1 ~ S1-5（Gate）/ S2-1 ~ S2-5（Login）/ S3-1 ~ S3-6（Match）/ S4-1 ~ S4-10（Game）/ S5-1 ~ S5-4（结算）/ S6-1 ~ S6-4（Realm）/ S7-1 ~ S7-3（Replay）/ S8-1 ~ S8-4（Analytics） |
| 8 个 MongoDB 集合初始化 | S0-3（accounts / players / match_history / replays / bans / analytics_events / room_configs / mmr_seasons） |
| 协议对齐 | S4-2（C2G_Input 30Hz）/ S4-3（InputPayload 8B + ProtocolVersion=2）/ S5-1（G2C_MatchSettle 精简 wire）/ S2-4（C2G_DeviceReport） |
| 反作弊 6 项 + 多数派裁决 | S4-3（ValidateInput 6 项）/ S4-5（⌊N/2⌋+1 多数派） |
| 热更隔离 + RSA 签名 | S9-1（RSA 签名）/ S9-2（Battle 禁 LoadFromStream）/ S9-3（版本对齐） |
| CI/CD 与部署 | S10-1（构建）/ S10-2（Docker）/ S10-3（水平扩展）/ S10-4（健康检查） |
| 集成测试与验证 | S11-1（端到端）/ S11-2（断线重连）/ S11-3（反作弊）/ S11-4（压测） |
