# Checklist

> NoitaCA 项目基线 spec 验证检查点清单
> 来源：综合 27 份设计文档的验证检查点

---

## 工程基础设施

- [ ] `ExporterSettings.json` 三处路径为 Windows 绝对路径（非 macOS 默认 `/Users/fantasy/...`）
- [ ] `Server/*.csproj` 所有 `PackageReference` 写死版本（Fantasy-Net 2025.2.1402 / NLog 5.5.1），无 `*`
- [ ] `packages.lock.json` 已提交，CI `dotnet restore --locked-mode` 通过
- [ ] 6+1 asmdef 编译通过（NoitaCA.Core/Lockstep/Simulation/Renderer/Gameplay/Editor + Hotfix），零警告
- [ ] CI asmdef 依赖图扫描通过（AOT 不反向依赖 Hotfix / Hotfix 不触确定性类型）
- [ ] BurstVersion 锁定 1.8.29，CI 比对通过
- [ ] `HybridCLRData/` 未被 git 跟踪（`.gitignore` 已排除）

## 确定性基础

- [ ] Fix64 加减乘除精度 ≤ 2.3e-10
- [ ] Fix64 乘法用 double 中间结果避免 64 位溢出（|raw| ≤ 2^47）
- [ ] Xorshift128Plus 同种子同平台 1000 次结果一致（SplitMix64 链式初始化）
- [ ] MurmurHash3 覆盖 PixelData 全 9 字段，short 字段 `& 0xFFFF` 防符号扩展
- [ ] `Fix64Math.Cos/Sin/Atan2` Burst 兼容，禁用 `Math.Cos/Sin/Atan2`

## 协议与序列化

- [ ] `InputPayload` wire 体积 = 8 字节（MoveX:int16 + MoveY:int16 + ActionFlags:uint16 + AimAngle:int16）
- [ ] `InputPayload.SpellId` 已删除，`ProtocolVersion` = 2
- [ ] 旧版本客户端（ProtocolVersion=1）被拒绝进对局，不 desync、不崩溃
- [ ] `ActionFlags` bit2/3 置位被 `ValidateInput` 拒绝；bit10-15 置位被拒绝
- [ ] 高频消息（C2G_Input/G2C_FrameBatch/G2C_SnapshotChunk/C2G_StateHash）使用 MemoryPack
- [ ] 低频消息（G2C_SceneEvent/C2G_ReconnectRequest/G2C_MatchSettle）使用 protobuf
- [ ] 子消息（InputPayload）显式标注 `// Protocol MemoryPack`
- [ ] `ProtocolExportTool` 生成到 `Server/Entity/Generate/NetworkProtocol/` 与 `Client/Hotfix/Generate/NetworkProtocol/`
- [ ] Generate 目录未手改

## DOTS ECS Simulation

- [ ] 256×256 默认场景跑 1000 帧，新旧系统像素级匹配
- [ ] MovementJob 加速比 ≥ 2×(i7-12700H) / ≥ 1.5×(Android 中端)
- [ ] Burst 配置 `FloatMode.Strict` + `FloatPrecision.Standard` + `CompileSynchronously=true`
- [ ] chunk 按 `Entity.Index` 显式排序；偶数帧正向/奇数帧反向迭代
- [ ] 双缓冲 NativeArray（CurrentPixels 只读 + NextPixels 可写）Job 完成后 Swap
- [ ] 12 材料 + 9 类化学反应全部实现且确定性一致
- [ ] 像素冲突按 (x,y) 字典序升序先到先得

## 核心玩法（ADR D1-D6）

- [ ] D1 边界：出界判定 `cy > Height+16` 或 `cx ∉ [-16, Width+16]` 触发 `PlayerDeath`
- [ ] D1 边界：Bounce/Wrap 仅自建房可选，Kill 为默认
- [ ] D2 缩圈：MVP 无缩圈逻辑触发；`ShrinkEnabled`/`ShrinkRate` 字段预留
- [ ] D3 Dash：Distance=34px / Duration=0.16s / CD=1.8s / 可被固体阻挡 / 期间可被击退 / 无 i-frames
- [ ] D4 HP：MVP 无血条 HUD；环境危害非主要死因
- [ ] D4 §9.7 铁律：no-HP 模式无任何"免疫击退"效果（Shield/Blink/Dash 均不免疫击退）
- [ ] D5 AimPower：MVP 固定力度，InputPayload 不含 AimPower 字段
- [ ] D5 法术切换：`SelectedSpellSlot` 循环 0-3，由 SelectNext/Prev 推导，不写入 InputPayload
- [ ] D6 后坐力：发射类按 RecoilSpeed 施加反向速度冲量（Bolt120→Fire390）
- [ ] D6 后坐力：StoneWall/Lava/Shield/Blink 后坐力=0
- [ ] D6 后坐力：护栏 T=135px，开阔地不误杀，贴边≤135px 可自爆
- [ ] D6 后坐力：InputPayload 8B 不变、ProtocolVersion=2 不 bump、状态哈希零新增字段
- [ ] 击退单步 clamp ≤ 16px（MaxSpeed×dt×1.5）
- [ ] 自身爆炸击退 ×0.5
- [ ] Mana 双闸：不足/CD 未到均拒绝施法（无投射物 + 无后坐力）

## 帧同步网络

- [ ] 30Hz 逻辑帧 + 60Hz 渲染帧插值，`dt=1/30` 固定步长，禁 `Time.deltaTime`
- [ ] 服务器不跑像素模拟（方案 B 中继 + 事件注入）
- [ ] `RoomConfig.PlayerCount` 在 [2,8] 可配置，默认 4；`PlayerCount=10` 夹取为 8
- [ ] 出生点基于 `RoomConfig.PlayerCount` + `ArenaConfig.Seed` 确定性生成，间距 ≥ Width/4
- [ ] 确定性红线：PauseMenu/QuickChat/结算按钮不进 InputPayload、不进状态哈希
- [ ] 分层回滚：静态层只读 / 环境层周期调和(每6帧) / 玩家层 GGPO(INPUT_DELAY=2, MAX_ROLLBACK=7, SNAPSHOT_BUFFER=8)
- [ ] 玩家层 7 帧回滚总成本 < 4ms
- [ ] 回滚重放全部动作（含 SelectNext/Prev/UseConsumable/Dash）重建 SelectedSpellSlot
- [ ] 断线重连：30s 内重连成功 + 无敌 3s + 快照分片 64×16KB
- [ ] 断线重连：30s 超时判负，房间继续 N-1 人
- [ ] 回滚溢出(>7帧)走重连流程

## 反作弊

- [ ] ValidateInput：帧频率 / MoveX/Y 范围 / 模长 ≤100 / AimAngle [0,628] / 保留位禁用 / 位移 ≤MaxSpeed×1.5
- [ ] 首帧通过（LastInputFrame=-1）
- [ ] 同帧重复拒绝；斜向加速(100,100)拒绝；瞬移拒绝
- [ ] 状态哈希多数派：N=2 阈值2全票 / N=4 阈值3 / N=8 阈值5
- [ ] 连续 3 次少数派踢出；全不一致标记异常（不误踢）
- [ ] Hotfix.dll 篡改 → 验签失败 → 连接拒绝
- [ ] 旧版本哈希不在白名单拒绝
- [ ] 防重放：同 (RoomId,PlayerId,H) 每会话仅一次
- [ ] 场景事件帧延迟解密：`TargetFrame-3` 前无法解密
- [ ] 版本对齐：HotfixHash/AssetManifestHash/ProtocolVersion/BurstVersion 四项全一致才准入对局

## 关卡与内容

- [ ] 同 Seed 在三平台生成地形/出生/刷新时间线，状态哈希一致
- [ ] 客户端不本地计算地形/刷新位置，仅消费服务器结果
- [ ] 出生台 100% 安全（无液体/虚空/埋压）
- [ ] 岛缘虚空区使"跌出/推出"成为唯一死法（D1 原生满足，无额外致死陷阱）
- [ ] 道具 Telegraph 90 帧（3s）可见，所有人公平
- [ ] 首道具 TargetFrame=150，间隔 8–14s，4 人档同屏 ≤3
- [ ] 道具 5min 超时消失
- [ ] no-HP 模式道具池无"免疫击退"道具
- [ ] 生物 AI 状态机 Idle/Wander/Chase/Attack/Flee 全链路可触发
- [ ] 生物走玩家层 GGPO 回滚，不进环境层
- [ ] MVP 1 Slime + 1 Firebat，P0 不刷新不重生
- [ ] 击退力度对齐 K 表（敌对 90/30 / 中立 45/15）
- [ ] 致死只由出界判定，生物不直接致死

## UI/输入/跨平台

- [ ] 12 动作语义在 PC 与移动端不增减、不拆分
- [ ] PC↔移动同意图 `DeterministicInput` 字节级相同
- [ ] `AimAngle` 用 `Fix64Math.Atan2` 量化 [0,628]，禁 `Math.Atan2`/`UnityEngine.Mathf`
- [ ] `MoveX/Y` 归一化模长 ≤100，PC/移动同一 `NormalizeMove()` 函数
- [ ] 采集层在 30Hz tick 边界 `Capture()`，不在渲染/事件帧即时产出
- [ ] 持续输入取 tick 边界当前值，边沿取"本 tick 内是否按下"，长按不连发
- [ ] 采集层为 MonoBehaviour（主线程），不在 Burst Job 内读 Input System/Touch
- [ ] `DeterministicInput` 全整数字段，无浮点
- [ ] MVP HUD 不画 HP 血条（D4）
- [ ] HUD 4 法术槽高亮当前选中槽，不携带 SpellId
- [ ] HUD 动画用整数帧驱动，禁 `Time.deltaTime` 与随机抖色
- [ ] 横屏锁定；`Screen.safeArea` 内缩
- [ ] 移动端可点控件 ≥ 44pt，主操作 ≥ 64pt
- [ ] 色盲安全色在 RGB565 16 位板内
- [ ] 键位重映射数据本地存储，不进确定性流，跨端不共享

## 美术/音频/VFX

- [ ] `IsValidRgb565` 对 12 白名单值返回 true，对板外 hex 返回 false
- [ ] CI `PaletteValidator` 拦下任意含板外色的 PNG，输出 `violations.json`
- [ ] `RenderBridgeSystem` Burst 展开 RGB565→Color32 无浮点
- [ ] URP 渲染栈零 Post-processing Volume（Bloom/Vignette/ColorLUT 全禁）
- [ ] 全部 VFX 由 `VfxTrigger`/`G2C_SceneEvent` 驱动，不进 InputPayload、不污染状态哈希
- [ ] Telegraph 全客户端同帧/同形/同色（90帧道具/Bomb 24帧等）
- [ ] 回滚后 `audioBuffer` 丢弃 + `ReplayForward` 重建，音频与权威模拟逐帧一致
- [ ] 客户端 `Random` 决定发声/类别的代码路径为零
- [ ] 移动端构建 voice≤24 / RAM≤16MB / 采样率 32kHz / DSP CPU≤6%
- [ ] P0 CJK 字表 700–1000 字固化；缺失字符降级为 `□`(`0x514A`)，不崩溃
- [ ] YooAsset `AssetManifestHash` 校验全房一致
- [ ] 叙事文本零机制绑定（流派名不出现于数值/匹配代码）
- [ ] 唯一死因为出界坠渊，无 HP/血量/第二死因叙事文本
- [ ] 死亡迸发用固定图案（MVP）或 `Xorshift128Plus(seed=PlayerId)`，禁 `UnityEngine.Random`

## 结算

- [ ] wire 仅精简 `SettlePlayer`（协议 §6.1），富字段客户端本地派生
- [ ] 仅 `MmrDelta` 服务端权威下发
- [ ] `Players[]` 长度 = playerCount，所有玩家入榜
- [ ] 排名算法确定性全序，tie-break 逐级生效
- [ ] 揭晓动画整数帧驱动，无 `Time.deltaTime`/随机抖色/渐变
- [ ] RGB565 合规，胜负平三色（`0x73FF`/`0xF900`/`0xA294`）色盲可辨 + 符号双重编码
- [ ] 横屏锁定 + 竖屏门遮罩
- [ ] 移动端 safe-area 内缩，主按钮 ≥64pt，其余 ≥44pt
- [ ] 按钮元操作不进 InputPayload
- [ ] 模式差异（Custom 同房间/Quick 重匹配/Ranked 禁即时）正确
- [ ] 回放按钮默认隐藏（`ReplayAvailable=false`）不阻塞 MVP
- [ ] 战绩卡各端一致，排位无对手文本

## CI/CD 确定性门禁

- [ ] Win/Mac/Android 三平台 10000 帧（167 个采样点）状态哈希全一致
- [ ] 多线程 = 单线程哈希一致；奇偶帧迭代一致
- [ ] 热路径 GC = 0 字节/帧（Profiler GC Alloc）
- [ ] Burst 覆盖率 100% 玩法 Job；BurstVersion 锁定 1.8.29 一致
- [ ] 单帧逻辑 < 16ms(Win) / < 25ms(Android 中端)；渲染 < 16.6ms
- [ ] 单元测试全绿：Fix64 / Xorshift128Plus / MurmurHash3 / 材料反应表 / ArenaConfig / NormalizeMove / ValidateInput
- [ ] desync 对局内探测：正常 4 人局 5min 0 误报
- [ ] 协议版本兼容：旧 v1 拒绝、新 v2 放行
- [ ] IL2CPP 三平台编译零警告成功
- [ ] 内存预算：Win ≤1.5GB / Android 中端 ≤1.0GB / 低端 ≤700MB
- [ ] GameBootstrap 加载 < 3s(Win) / < 6s(Android)

## 数据埋点

- [ ] 所有 E1–E9 事件不进 InputPayload、不参与状态哈希（红线）
- [ ] match_settle 与 G2C_MatchSettle 字段 1:1 对齐
- [ ] 埋点校验门：sum(kills)==count(有信用 E5) 且 sum(deaths)==count(E5) 且 D∈{0,1}
- [ ] 客户端上报经状态哈希多数派校验后采纳（verify-don't-trust）
- [ ] 归因：last-contact 玩家归因正确（生物/环境不抢信用）
- [ ] Dash：iframes_active=false + knockback_during_dash 验证 Dash≠免推
- [ ] DeviceId/InstallId：SHA-256 hex 64 字符、不传明文、DeviceId 重装不变

## 编码规范

- [ ] 双语注释成对（`// 职责：` + `// Responsibility:`）覆盖所有新增类型/方法
- [ ] 值类型 struct/readonly struct，DOTS IComponentData 必须 struct
- [ ] 不可继承类 sealed
- [ ] 热路径方法 `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- [ ] 禁用 IEnumerator 协程，统一 UniTask
- [ ] 热路径禁 LINQ/$ 字符串，用 ZLinq.AsRef() / ZString
- [ ] Burst Job 内禁字符串/托管类型/ZLinq/ZString/UniTask，仅 NativeArray + blittable struct
- [ ] 禁用 py/ps1 脚本修改含中文的 .cs 文件

## 试玩回归

- [ ] 单局 4 人 < 8 min，首杀 30-60s，残局 1v1 30-90s
- [ ] PushOutRate/SelfElimRate/生物归因占比监控达标
- [ ] GDD §7 漂移已修正（护盾条目对齐"挡伤不免疫击退"）
- [ ] `bodyWidth=60` [TUNING] 假设值已复核确认

## 跨平台一致性

- [ ] PC↔移动同意图 `DeterministicInput` 字节级相同
- [ ] 三平台（Win/Mac/Android）10000 帧状态哈希一致
- [ ] 4 人对局（2 PC + 2 移动）5 分钟 ping 100ms，0 desync 误报
- [ ] 机型矩阵：中端达标 / 低端降级可达 / iOS 单独构建验证
