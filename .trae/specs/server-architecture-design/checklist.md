# Checklist

> NoitaCA 服务器架构设计验证检查点清单
> 部署目标：上线运营 <10K DAU | 技术栈：Fantasy-Net + MongoDB（不引入 Redis）
> 7 Scene：Gate / Login / Match / Game / Realm / Replay / Analytics | 网络：全 KCP | 认证：DeviceId + Token

---

## 类别 1：工程基础设施

- [ ] Fantasy 三层目录结构齐全：`Server/Entity`（数据实体+Generate）、`Server/Hotfix`（玩法逻辑）、`Server/Main`（入口+启动）均存在，Hotfix 与 Main 编译时引用 Entity（csproj），Main 运行时通过 `AssemblyHelper.LoadHotfixAssembly` 加载 Hotfix.dll，无 Entity 反向依赖 Hotfix
- [ ] `Server/Main/Program.cs` 调用 `AssemblyHelper.Initialize()` 触发 Entity 与 Hotfix 程序集强制加载（`EnsureLoaded()`）并启动 `Fantasy.Platform.Net.Entry.Start(logger)`
- [ ] `Server/Entity/AssemblyHelper.cs` 的 `LoadHotfixAssembly()` 使用独立 `AssemblyLoadContext`（可卸载），热更时先 `Unload()` 再 `GC.Collect()` 后重载
- [ ] SceneConfig 已声明 7 个 SceneType（Gate/Login/Match/Game/Realm/Replay/Analytics）并配置各自 Scene 的 Process 归属
- [ ] ProcessConfig 配置多进程拓扑（Gate 独立进程可水平扩展，Game 按房间数动态扩缩），每个 Process 绑定独立 KCP 端口
- [ ] WorldConfig 已配置 MongoDB 连接串（非硬编码于 Hotfix 代码），连接池参数（maxPoolSize/minPoolSize）可调
- [ ] MongoDB 连接在 Main 启动阶段建立，失败时 `Environment.Exit(1)` 终止而非吞异常
- [ ] 8 个集合初始化脚本/索引声明齐全：`accounts` / `players` / `match_history` / `replays` / `bans` / `analytics_events` / `room_configs` / `mmr_seasons`
- [ ] 集合索引就绪：`accounts.device_id`（唯一）、`players.user_id`（唯一）、`match_history.match_id`（唯一+TTL）、`replays.expire_at`（TTL 30天）、`analytics_events.event_time`（TTL 90天）、`mmr_seasons.season_id`
- [ ] 全 Scene 网络协议统一 KCP（无 TCP/WebSocket 混用），KCP 参数（nodelay/interval/resend/nc）在配置中集中声明
- [ ] NLog 配置（`Server/Main/NLog.config`）已包含 Info/Warn/Error 三级 target，Error 落文件 + 控制台，按天滚动且保留 30 天
- [ ] Hotfix 层所有入口方法使用 NLog 记录进入/退出日志，Main/Entity 层不直接写业务日志

## 类别 2：Gate Scene

- [ ] Gate Scene 已注册到 SceneConfig 并能接收 KCP 连接（绑定对外端口，客户端首连目标）
- [ ] Gate 使用 Fantasy 内置 Session 机制管理客户端连接，未自建 Redis Session 表
- [ ] 客户端首次握手时 Gate 校验 `DeviceId + Token`，Token 无效/过期返回 `G2C_AuthFailed` 并断开
- [ ] Gate 根据 RouteType 将消息路由至 Login（注册/登录）、Match（匹配/房间）、Game（对局内帧）三类目标 Scene
- [ ] Gate 转发消息使用 Fantasy Roaming 路由（非自建跨进程通信通道）
- [ ] Gate 维护 SessionId → 玩家 Identity 映射，路由时附加目标 Scene 寻址（Addressable），不自建实体定位表
- [ ] 客户端断线时 Gate 释放 Session 资源并通知 Game Scene 触发 30s 重连窗口逻辑

## 类别 3：Login Scene

- [ ] 账号注册接口接收 `DeviceId`，写入 `accounts` 集合，`device_id` 唯一索引防重复注册
- [ ] 账号登录校验 `DeviceId` 存在性后签发 `Token`（含过期时间），Token 写入 `accounts.token` 与 `accounts.token_expire`
- [ ] `C2G_DeviceReport` 消息处理在 Login Scene 完成，设备指纹（型号/系统/版本）写入 `players` 集合
- [ ] `accounts` 集合 CRUD 全部在 Hotfix 层实现，Entity 层仅提供数据结构，Main 层不写业务逻辑
- [ ] 首次登录自动创建 `players` 档案记录（user_id 关联 account_id，初始 MMR 写入 `mmr_seasons` 当赛季）
- [ ] Login 注册 `RouteType`（≥1001）并通过 `RoamingTypes` 暴露给框架，供 Gate 路由
- [ ] Token 签发/校验使用 Fantasy 内置机制，未自建 Token 服务或引入外部鉴权组件

## 类别 4：Match Scene

- [ ] Quick 匹配队列按 MMR 区间分桶（±200），同桶内先到先匹配，超时（如 15s）放宽区间
- [ ] Custom 房间创建支持邀请码（6 位），邀请码写入 `room_configs` 集合并设过期时间
- [ ] MMR 匹配算法读取 `mmr_seasons` 当赛季 MMR，匹配成功后锁定参战玩家 MMR 快照用于结算
- [ ] 凑齐 2-8 人后 Match Scene 创建 GameRoom Entity（分配 Game Scene 进程），并将房间配置归档到 `room_configs`
- [ ] Match Scene 通过 Fantasy Addressable 寻址定位可用 Game Scene 进程，未自建进程选择器
- [ ] 观战列表在 Match 维护，对局开始后通过 SphereEvent 通知 Game Scene 订阅观战玩家
- [ ] 匹配取消/超时释放队列占位，避免内存泄漏

## 类别 5：Game Scene（帧中继）

- [ ] GameRoom Entity 生命周期完整：创建（Match 通知）→ 运行（帧中继）→ 结算（G2C_MatchSettle）→ 销毁（释放 NativeArray/缓冲）
- [ ] `C2G_Input` 按 30Hz 聚合，每 33ms 合并本帧所有玩家输入为一个 FrameBatch
- [ ] `ValidateInput` 6 项校验全部实现且任一失败拒绝该输入并计入 desync 报告：
  - [ ] 1. MoveX 范围合法（int16 有界）
  - [ ] 2. MoveY 范围合法（int16 有界）
  - [ ] 3. ActionFlags bit2/bit3 置位拒绝（未定义位）
  - [ ] 4. ActionFlags bit10-15 置位拒绝（保留位）
  - [ ] 5. AimAngle 范围合法（[0,360)° 量化后 int16 有界）
  - [ ] 6. 帧序号连续且输入频率 ≤ 30Hz（防加速作弊）
- [ ] `G2C_FrameBatch` 广播聚合后的帧给房间内所有活跃玩家，使用 MemoryPack 序列化
- [ ] 状态哈希多数派裁决：每 N 帧收集客户端 `C2G_StateHash`，采纳 `⌊N/2⌋+1` 多数派哈希，少数派记 desync
- [ ] 慢客户端三档追赶策略实现：落后 ≤3 帧→补发增量；落后 4-32 帧→关键帧快照；落后 >32 帧→踢出并允许 30s 重连
- [ ] 30s 断线重连窗口：断线后保留 GameRoom 席位 30s，`C2G_ReconnectRequest` 命中后恢复席位并补发关键帧
- [ ] 观战 RingBuffer 实现：观战玩家只接收最近 N 帧（只读不参与输入），`G2C_SpectatorBatch` 协议预留并广播
- [ ] 场景事件（缩圈/宝箱/危险区）使用 AES-128-CTR 加密，密钥在 `frame = TargetFrame - 3` 时才下发解密（延迟解密防前瞻作弊）
- [ ] Game Scene 内确定性基础：Fix64 Q31.32 + Xorshift128Plus + MurmurHash3，与客户端同种子同结果
- [ ] GameRoom 销毁时释放所有 NativeArray 与 RingBuffer，无托管堆泄漏

## 类别 6：结算服务

- [ ] `G2C_MatchSettle` wire 仅含服务端权威字段 `MmrDelta`（其余 MMR 新值/段位/排名均客户端派生）
- [ ] `match_history` 落库字段：match_id / 战局时长 / 参战玩家 id 列表 / 名次 / MmrDelta / 状态哈希摘要 / replay_id
- [ ] 客户端派生字段（段位/排名/胜率）与服务端可重算结果对齐，verify-don't-trust 模型校验通过
- [ ] MMR 更新写入 `mmr_seasons` 当赛季记录，更新原子（findAndModify 或事务），并发结算不脏写
- [ ] 结算后触发 SphereEvent 通知 Realm（刷新排行榜缓存）与 Replay（落盘录像）、Analytics（埋点）

## 类别 7：Realm Scene

- [ ] 排行榜查询分页实现（limit/skip 或 cursor），Top-N 缓存于内存定时刷新（非每次查库）
- [ ] 玩家档案查询返回 `players` + `mmr_seasons` 聚合结果（不含敏感字段 token/device_id）
- [ ] 赛季管理：`mmr_seasons` 支持赛季切换（旧赛季归档、新赛季重置初始 MMR）
- [ ] 封禁校验：登录/匹配/进对局前查询 `bans` 集合，命中封禁记录拒绝并返回剩余时长
- [ ] Realm 通过 Roaming 路由接收 Gate/Match/Game 的查询请求，未自建跨进程 RPC

## 类别 8：Replay Scene

- [ ] 录像存储：帧序列 + 初始种子 + 房间配置写入 `replays` 集合，关联 match_id
- [ ] `replays.expire_at` 字段建立 TTL 索引，30 天后自动删除文档
- [ ] 回放分发按需流式下发（分片 G2C_ReplayChunk），避免一次性大包
- [ ] 回放验证可重放：同种子+输入序列重放结果与对局状态哈希一致（确定性回归测试）

## 类别 9：Analytics Scene

- [ ] 埋点 ingestion 接口接收客户端 `C2G_AnalyticsEvent`，写入 `analytics_events` 集合
- [ ] `analytics_events` 按 `event_time` TTL 索引自动归档（90 天）
- [ ] 混合派生模型 verify-don't-trust：服务端可重算的关键指标（伤害/击杀/名次）与客户端上报值比对，偏差记 desync
- [ ] desync 报告归档到独立子集合/字段，供反作弊汇总分析
- [ ] Analytics 与 Game 解耦：通过 SphereEvent 异步接收对局结束事件，不阻塞帧中继主循环

## 类别 10：反作弊

- [ ] `ValidateInput` 6 项校验全部生效（见类别 5），任一失败拒绝输入并上报
- [ ] 状态哈希多数派裁决（`⌊N/2⌋+1`）实现，少数派客户端记 desync 并累计告警阈值
- [ ] AES-128-CTR 场景事件延迟解密：`frame = TargetFrame - 3` 才下发密钥，客户端无法前瞻未来事件
- [ ] desync 报告汇总：单玩家累计 desync 超阈值触发可疑标记，写入 `bans` 或风控队列
- [ ] 反作弊逻辑全部在 Hotfix 层，Entity 层不包含判定逻辑（便于热更反作弊规则）
- [ ] ValidateInput 拒绝事件含玩家 id / 帧号 / 失败项编号，可追溯审计

## 类别 11：热更与安全

- [ ] `Hotfix.dll` 经 RSA 签名，`LoadHotfixAssembly()` 加载前校验签名，签名不符拒绝加载并告警
- [ ] Battle（对局进行中）状态禁用 `LoadFromStream` 热更，待对局结束（GameRoom 销毁）后才允许重载 Hotfix
- [ ] 热更流程：上传新 Hotfix.dll → 校验 RSA 签名 → 等待无活跃对局 → `Unload()` 旧 ALC → `LoadFromStream` 新程序集 → `EnsureLoaded()`
- [ ] `ProtocolExportTool` 的 `ExporterSettings.json` 路径为 Windows 绝对路径（非 macOS 默认 `/Users/fantasy/...`）
- [ ] 协议导出生成到 `Server/Entity/Generate/NetworkProtocol/` 与 `Client/Unity/Assets/Scripts/Hotfix/Generate/NetworkProtocol/`，Generate 目录未手改
- [ ] Hotfix 热更不影响进行中对局：重载前后 GameRoom 状态哈希一致

## 类别 12：协议一致性

- [ ] `InputPayload` wire 体积 = 8 字节（MoveX:int16 + MoveY:int16 + ActionFlags:uint16 + AimAngle:int16）
- [ ] `InputPayload.SpellId` 已删除，`ProtocolVersion` = 2
- [ ] 旧版本客户端（ProtocolVersion=1）被拒绝进对局，返回版本不匹配错误而非 desync/崩溃
- [ ] `G2C_MatchSettle` wire 精简：仅 `MmrDelta` 为服务端权威，段位/排名/胜率客户端派生
- [ ] `G2C_SpectatorBatch` 协议预留（观战专用，与 `G2C_FrameBatch` 区分）
- [ ] 高频消息（C2G_Input/G2C_FrameBatch/G2C_SpectatorBatch/C2G_StateHash）使用 MemoryPack
- [ ] 低频消息（G2C_SceneEvent/C2G_ReconnectRequest/G2C_MatchSettle）使用 protobuf，protobuf 使用 `optional` + 保留字段号
- [ ] 子消息（InputPayload）显式标注 `// Protocol MemoryPack`
- [ ] `RouteType.cs` 中 7 Scene 相关 RouteType 均 ≥ 1001，并通过 `RoamingTypes` 暴露

## 类别 13：CI/CD 与部署

- [ ] 服务器构建脚本（`dotnet publish` Release 配置）输出独立部署目录（含 Hotfix.dll + pdb + NLog.config + appsettings）
- [ ] Docker 化：Dockerfile 多阶段构建，镜像不含源码与 pdb（生产）或保留 pdb（调试）
- [ ] Scene 水平扩展：Gate/Game 进程可多实例部署，通过 ProcessConfig 注册到集群
- [ ] 健康检查端点：HTTP /health 返回 Scene 状态 + MongoDB 连接状态 + 在线对局数
- [ ] CI 流水线：`dotnet restore --locked-mode` + 构建 + 单元测试 + 协议导出校验
- [ ] 部署回滚策略：Hotfix.dll 可一键回滚上一版本（保留前一版本备份 + RSA 签名校验）

## 类别 14：集成测试

- [ ] 端到端流程：登录（DeviceId+Token）→ Quick 匹配 → 进对局帧中继 → 结算（G2C_MatchSettle）→ 排行榜刷新，全链路无报错
- [ ] 断线重连测试：对局中客户端断线 25s 内重连成功恢复席位，>30s 重连失败席位释放
- [ ] 慢客户端三档追赶测试：人为制造 3/20/40 帧延迟，分别触发增量补发/快照/踢出三档策略
- [ ] 反作弊验证：构造非法 ActionFlags（bit2/3/bit10-15 置位）+ 超频输入 + 篡改状态哈希，均被拒绝并记 desync
- [ ] 压测（<10K DAU）：模拟 1000 并发对局，帧中继 P99 延迟 ≤ 50ms，MongoDB 连接池不耗尽
- [ ] 多数派裁决测试：4 人对局中 1 人篡改状态哈希，多数派（3 人）哈希被采纳，篡改者记 desync
- [ ] 确定性回归：同种子+同输入序列，服务端重放与客户端录像状态哈希一致
- [ ] 热更隔离测试：对局进行中触发热更被拒绝，对局结束后热更成功且无状态丢失

## 类别 15：Fantasy 框架合规

- [ ] 跨 Scene 通信使用 Fantasy Roaming 路由（Gate→Login/Match/Game 等），未自建跨进程通信通道
- [ ] 实体定位使用 Fantasy Addressable 寻址（Match 定位 GameRoom Entity），未自建实体定位表
- [ ] 跨服事件使用 Fantasy SphereEvent（结算通知 Realm/Replay/Analytics），未自建 Pub/Sub 中间件
- [ ] 客户端会话使用 Fantasy 内置 Session 机制，未自建 Redis Session 表
- [ ] 未引入 Redis 等冗余组件（缓存走内存/框架内置，会话走内置 Session，排行榜走内存缓存）
- [ ] MongoDB 连接通过 WorldConfig 配置（非硬编码于 Hotfix 业务代码）
- [ ] Scene/Process 拓扑通过 SceneConfig/ProcessConfig 声明（非硬编码于 Main 入口）
- [ ] Hotfix 热更使用 Fantasy 内置 AssemblyLoadContext 机制（AssemblyHelper.LoadHotfixAssembly），未自建热更框架
- [ ] 协议路由 RouteType 注册到框架 `RoamingTypes`，未绕过框架路由层
- [ ] 所有玩法逻辑位于 Hotfix 层，Entity 层仅数据结构，Main 层仅入口启动，符合三层职责划分
