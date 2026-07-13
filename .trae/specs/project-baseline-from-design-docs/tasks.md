# Tasks

> 基于 27 份设计文档提炼的项目基线任务清单
> 任务按依赖关系排序，标注 `[依赖 X]` 表示前置任务，`[可并行]` 表示可与同级任务并行

---

## 阶段 0：工程基础设施（前置）

- [ ] **T0-1**: 修正 `ExporterSettings.json` 三处路径为 Windows 绝对路径（非 macOS 默认） `[无依赖]`
- [ ] **T0-2**: 服务端 `Server/*.csproj` 锁定 NuGet 版本（Fantasy-Net 2025.2.1402 / NLog 5.5.1），启用 `RestorePackagesWithLockFile`，提交 `packages.lock.json` `[无依赖]`
- [ ] **T0-3**: 创建 6+1 asmdef 骨架（`NoitaCA.Core` / `NoitaCA.Lockstep` / `NoitaCA.Simulation` / `NoitaCA.Renderer` / `NoitaCA.Gameplay` / `NoitaCA.Editor` + `Hotfix.asmdef`）+ 引用配置 `[无依赖]`
- [ ] **T0-4**: CI 静态校验 asmdef 依赖图（AOT 不反向依赖 Hotfix / Hotfix 不触确定性类型） `[依赖 T0-3]`
- [ ] **T0-5**: 锁定 Burst 1.8.29，CI 比对 `BurstVersion` 字段 `[无依赖]`
- [ ] **T0-6**: `.gitignore` 排除 `HybridCLRData/`，禁 force add `[无依赖]`

## 阶段 1：确定性基础与协议

- [ ] **T1-1**: 实现 `Fix64`（Q31.32，±2^15 限制，乘法用 double 中间结果）+ `Fix64Vec2` + `Fix64Math`（Cos/Sin/Atan2，Burst 兼容） `[依赖 T0-3]`
- [ ] **T1-2**: 实现 `Xorshift128Plus`（SplitMix64 链式初始化）+ `MurmurHash3`（全 9 字段，short `& 0xFFFF`） `[依赖 T0-3]`
- [ ] **T1-3**: 在 `OuterMessage.proto` 追加全部消息（按双序列化分工标注），删除 `InputPayload.SpellId`，`ProtocolVersion` bump 1→2，跑 `ProtocolExportTool` 生成双端代码 `[依赖 T0-1, T0-3]`
- [ ] **T1-4**: 迁移 `PixelData`（byte/short 替代 bool，9 字段含 `ushort Color` RGB565）+ `MaterialDatabase` + `PixelReactionUtility` 到 Core `[依赖 T1-1]`
- [ ] **T1-5**: 实现 `Palette.cs` 12 色 RGB565 LUT + `IsValidRgb565`（Burst 兼容）+ `PaletteIndex` 枚举 `[依赖 T1-4]`
- [ ] **T1-6**: 单元测试：Fix64 精度 ≤ 2.3e-10 / Xorshift128Plus 同种子 1000 次一致 / MurmurHash3 全字段覆盖 `[依赖 T1-1, T1-2]`

## 阶段 2：DOTS ECS Simulation（Phase 2）

- [ ] **T2-1**: 创建 `PixelGridAdapter` 桥接 + 修改现有 MonoBehaviour 调用点走 Adapter `[依赖 T1-4]`
- [ ] **T2-2**: 迁移 `PixelSimulationSystem` / `MovementSystem` + `MovementJob` / `InteractionSystem` + `InteractionJob` + 双缓冲 NativeArray（CurrentPixels 只读 + NextPixels 可写） `[依赖 T2-1]`
- [ ] **T2-3**: 迁移 `PixelWorldFactory`（5 参数签名含 heightMap）+ dirty chunk 追踪 `[依赖 T2-2]`
- [ ] **T2-4**: 实现 12 材料 + 9 类化学反应 + 温度场 + 相变 + 确定性迭代（偶数正向/奇数反向）+ 像素冲突 (x,y) 字典序 `[依赖 T2-2]`
- [ ] **T2-5**: Burst 配置 `FloatMode.Strict` + `FloatPrecision.Standard` + `CompileSynchronously=true` + chunk 按 `Entity.Index` 显式排序 `[依赖 T2-2]`
- [ ] **T2-6**: 验证 256×256 跑 1000 帧新旧系统像素级匹配 + MovementJob 加速比 ≥ 2×(Win) / ≥ 1.5×(Android) `[依赖 T2-2]`

## 阶段 3：Renderer（Phase 3）

- [ ] **T3-1**: 创建 `RenderBridgeSystem`（Burst Job 内 RGB565→Color32 整数展开，无浮点） `[依赖 T1-5, T2-2]`
- [ ] **T3-2**: URP 2D Renderer 去后处理（零 Post-processing Volume） `[无依赖]`
- [ ] **T3-3**: 验证 `SetPixels32` 耗时 < 2ms(Win) / < 5ms(Android)，热路径 GC=0 `[依赖 T3-1]`
- [ ] **T3-4**: 实现 `Tools/PaletteValidator`（Python+PIL）+ `PixelSprite.meta` 模板 + CI 拦截板外色 `[依赖 T1-5]`

## 阶段 4：Gameplay 玩法（Phase 4）

- [ ] **T4-1**: 迁移 `PlayerData` / `PlayerController` / `PlayerMovementJob` + `ArenaConfig` 边界判定（Kill/Bounce/Wrap） `[依赖 T3-1]`
- [ ] **T4-2**: 实现击退冲量模型 + K 表 + 单步 clamp `MaxSpeed×1.5` + 自身爆炸 ×0.5 `[依赖 T4-1]`
- [ ] **T4-3**: 迁移 `SpellData` / `SpellController` / `SpellMovementJob` + 12 法术数值表 + `RECOIL_TABLE`（Bolt120→Fire390，StoneWall/Lava/Shield/Blink=0） `[依赖 T4-2]`
- [ ] **T4-4**: 实现后坐力施加：查表 → `MAX_RECOIL_SPEED`(405) clamp → `Fix64Math.Cos/Sin` 反冲方向 → 叠加到 `VelX/VelY`，护栏 T=135px `[依赖 T4-3]`
- [ ] **T4-5**: 实现 Mana 双闸（上限 100 / 回复 15/s）+ 每法术独立冷却 + 法术循环切换 `SelectedSpellSlot`(0-3) `[依赖 T4-3]`
- [ ] **T4-6**: 实现 Dash（`ActionFlags.Dash` bit9 边沿触发，`ApplyDash` 纯位移无 i-frames，Distance 34px / CD 1.8s） `[依赖 T4-1]`
- [ ] **T4-7**: 迁移 `CreatureData` / `CreatureAISystem` / `CreatureAIJob` + 状态机（Idle/Wander/Chase/Attack/Flee）+ `NativeQueue<PixelWriteRequest>` 队列 `[依赖 T4-3]`
- [ ] **T4-8**: 实现 Shield 挡伤不抗推（阻挡材料/命中伤害，不阻止击退） `[依赖 T4-3]` `[P1 优先级]`
- [ ] **T4-9**: 迁移 Equipment + 拆分 `InputController`（玩法 vs 调试） `[依赖 T4-7]`

## 阶段 5：输入采集与跨平台

- [ ] **T5-1**: 在 Core 定义 `IPlayerInputProvider` + `DeterministicInput`(blittable struct) + `InputActionFlags` 枚举 `[依赖 T0-3]`
- [ ] **T5-2**: 实现 `KeyboardMouseInputProvider`（PC 键鼠）+ `EdgeBuffer` `[依赖 T5-1]`
- [ ] **T5-3**: 实现 `TouchScreenInputProvider`（移动触屏，双手横屏，≥2 并发触控点，摇杆死区 12%） `[依赖 T5-1]`
- [ ] **T5-4**: Core 共享 `NormalizeMove()`（PC/移动同函数）+ `Fix64Math.Atan2` 量化（`AimAngle = clamp(round(angle/2π*628), 0, 628)`） `[依赖 T5-1]`
- [ ] **T5-5**: 在 Lockstep 实现 `ClientLockstepScheduler.TickFrame()`（30Hz tick 边界调用 `Capture()`）+ `InputRingBuffer` + `DeterministicInput`↔`InputPayload` 序列化适配 `[依赖 T5-2, T5-3, T1-3]`
- [ ] **T5-6**: 在 Hotfix 实现 `C2G_InputHandler`（仅网络收发，不含模拟逻辑） `[依赖 T5-5]`
- [ ] **T5-7**: 扩展 `PlayerData` 快照字段（`ActionFlags/AimAngle/MoveX/Y/SelectedSpellSlot`）保证回滚重放 `[依赖 T5-5]`
- [ ] **T5-8**: 本地持久化 `InputRemapProfile`（PC）+ `TouchLayoutProfile`（移动），不进确定性流 `[依赖 T5-2, T5-3]`

## 阶段 6：帧同步网络层

- [ ] **T6-1**: 实现 `InputRingBuffer<T>` / `SnapshotRingBuffer<T>` / `RingBuffer<T>` + `PlayerLayerSnapshot` 深拷贝 + `RollbackContext.RollbackTo` / `ReplayForward` `[依赖 T1-2, T5-5]`
- [ ] **T6-2**: 服务端 `GameRoom` 实体 + `PlayerSlot × N` + 输入聚合为 `G2C_FrameBatch` + `LockstepScheduler` 帧推进 `[依赖 T1-3, T6-1]`
- [ ] **T6-3**: `AntiCheatSystem.ValidateInput`（频率/范围/模长/AimAngle/保留位/位移 6 项校验） `[依赖 T6-2]`
- [ ] **T6-4**: 状态哈希多数派裁决（`⌊N/2⌋+1` 通用公式）+ 连续 3 次少数派踢出 + 全不一致标记异常 `[依赖 T6-2]`
- [ ] **T6-5**: Hotfix.dll RSA 签名校验（构建机签名 + 客户端验签 + 服务器白名单 + 防重放） `[依赖 T0-3]`
- [ ] **T6-6**: 场景事件帧延迟解密（AES-128-CTR + 密钥派生 `frame = TargetFrame - 3`） `[依赖 T6-2]`
- [ ] **T6-7**: 断线重连（`C2G_ReconnectRequest` / `G2C_SnapshotChunk` / 30s 窗口 + 无敌 3s + 快照分片 64×16KB） `[依赖 T6-2]`
- [ ] **T6-8**: 时钟同步（NTP 四时间戳 + EMA `EMA_ALPHA=0.1`，对局开始 3 次 / 对局中每 10s / 重连后 3 次） `[依赖 T6-2]`
- [ ] **T6-9**: `JitterBuffer<T>`（2 帧缓冲，动态调整 2-4 帧） `[依赖 T6-1]`
- [ ] **T6-10**: Catch-up + `SlowClientDetector`（客户端三档追赶 / 服务端 15 帧警告 / 30 帧踢出） `[依赖 T6-2]`
- [ ] **T6-11**: 观战模式预留（`SpectatorBroadcaster` + 450 帧 RingBuffer，默认 10s 延迟 / 死亡玩家 1s） `[依赖 T6-2]`
- [ ] **T6-12**: 版本对齐校验（`HotfixHash` / `AssetManifestHash` / `ProtocolVersion` / `BurstVersion` 四项，对局前强制） `[依赖 T6-5]`
- [ ] **T6-13**: 热更时机约束（Battle 状态禁 `LoadFromStream`，对局结束强制热更） `[依赖 T6-5]`

## 阶段 7：关卡与内容生成

- [ ] **T7-1**: 服务端 `WorldBuilder.BuildWorldAsync` 接收 `ArenaConfig` 生成静态地形（悬浮岛 + 四周虚空） `[依赖 T2-3]`
- [ ] **T7-2**: 出生点纯函数（`spawnRng = Xorshift128Plus(Seed^0x5PAA4E72)`，环半径 R clamp，`θ = phase + i*(360/N)`，间距 ≥ Width/4） `[依赖 T7-1]`
- [ ] **T7-3**: 出生台强制平整（16×4 Stone + 顶部 2px Sand，100% 安全落地） `[依赖 T7-2]`
- [ ] **T7-4**: 落地 P0 默认 `Cross_Quad(256)` + 变体 `Twin_Mesa(256)` `[依赖 T7-1, T7-3]`
- [ ] **T7-5**: 材料布局反应对就近（木上压水 / 岩旁熔岩 / 酸蚀非石带） `[依赖 T7-4]`
- [ ] **T7-6**: 服务端 `SceneScheduler`（`ItemSpawnTable` 权重 + `itemRng` 预滚整局时间线 + Telegraph 90 帧） `[依赖 T7-1, T6-6]`
- [ ] **T7-7**: MVP 生物最小集（1 Slime + 1 Firebat，Seed 确定性生成） `[依赖 T4-7, T7-4]`
- [ ] **T7-8**: 生物击退力度对齐 K 表（敌对 90/30px / 中立 45/15px）+ 击杀归属"最后接触玩家" `[依赖 T4-7]`

## 阶段 8：UI/HUD/结算

- [ ] **T8-1**: HUD 实现（无 HP 血条 MVP / 4 法术槽高亮 / 边界警示 / 状态色 `0xF900`/`0x73FF`/`0x514A` / 整数帧动画） `[依赖 T1-5, T5-5]`
- [ ] **T8-2**: 屏幕自适应（横屏锁定 / `Screen.safeArea` 内缩 / ≥44pt / ≥64pt 热区） `[依赖 T8-1]`
- [ ] **T8-3**: 无障碍（色盲模式 16 位板内 / 控件放大 / 键位重映射 / 高对比 / 减少动效保留 Telegraph） `[依赖 T8-1]`
- [ ] **T8-4**: 实现 `MatchEnded` 触发判定（最后存活 / 全员出界 / 超时 / 房主终止） `[依赖 T6-2]`
- [ ] **T8-5**: 排名算法客户端本地派生（Survivors/Eliminated 分组 + tie-break 全序） `[依赖 T8-4]`
- [ ] **T8-6**: 组装 `PlayerResult`（精简 `SettlePlayer` + 本地 sim/名册派生富字段，`Take(playerCount)` 入榜） `[依赖 T8-5, T1-3]`
- [ ] **T8-7**: 结算界面（RGB565 状态色 / 整数帧揭晓动画 / 横屏锁定 / 竖屏门遮罩 / 移动 safe-area） `[依赖 T8-6]`
- [ ] **T8-8**: 结算按钮元操作（不进 InputPayload）+ 模式差异（Custom 同房间 / Quick 重匹配 / Ranked 禁即时） `[依赖 T8-7]`
- [ ] **T8-9**: 挂机自动返回倒计时（默认 30s，手动优先） `[依赖 T8-8]`
- [ ] **T8-10**: 查看回放条件显示（`ReplayAvailable==true` 当前默认隐藏）+ 分享战绩卡本地合成 `[依赖 T8-7]`

## 阶段 9：VFX/音频/叙事

- [ ] **T9-1**: `VfxTrigger` 枚举 + 模拟 Burst Job 写入队列 + 表现层消费（不进 InputPayload / 不污染哈希） `[依赖 T3-1]`
- [ ] **T9-2**: 道具刷新 Telegraph（光柱+圆环，90 帧，`G2C_SceneEvent` 驱动）+ Bomb AoE Telegraph `[依赖 T9-1, T6-6]`
- [ ] **T9-3**: 命中/击退/出界死亡/Dash 拖影 VFX（固定图案 MVP，整数帧驱动） `[依赖 T9-1]`
- [ ] **T9-4**: 像素字体 P0 资产（ASCII 8×8 + 数字等宽 + CJK 700–1000 字 16×16）+ YooAsset 打包 + 缺失降级 `□` `[依赖 T1-5]`
- [ ] **T9-5**: FMOD Studio 集成 + `SimAudioEvent` 结构 + `audioBuffer` 独立缓冲 + `EnqueueAudio`/`CommitAudio` 接入 `ApplyInputsAndStep`/`ReplayForward` `[依赖 T6-1]`
- [ ] **T9-6**: 回滚时 `audioBuffer[mismatch..current]` 丢弃 + `ReplayForward` 重建 `[依赖 T9-5]`
- [ ] **T9-7**: SFX 清单绑定 `SimAudioEventType` 枚举（12 类型 + 优先级 CRIT>HIGH>MED>LOW）+ `CombatIntensity` RTPC `[依赖 T9-5]`
- [ ] **T9-8**: 移动端音频性能预算（voice≤24 / RAM≤16MB / 32kHz / DSP CPU≤6%） `[依赖 T9-5]`
- [ ] **T9-9**: 世界观叙事字符串集中提取（6 流派 / 12 材料 12 法术叙事名 / 结算文案 / 身份称谓），纯包装零机制绑定 `[无依赖]`
- [ ] **T9-10**: 结算叙事状态色（胜冰青 / 负火橙 / 平灰）+ 横幅文案 `[依赖 T8-7, T9-9]`

## 阶段 10：Bootstrap 拆分与清理（Phase 5-6）

- [ ] **T10-1**: 拆 `PixelWorldBootstrap` → 4 MB（WorldBuilder / CameraRig / ArtDirector / GameBootstrap）+ UniTask 异步编排 `[依赖 T4-9]`
- [ ] **T10-2**: ZString/ZLinq 替换 StressTest 工具 + 协程全改 UniTask `[依赖 T10-1]`
- [ ] **T10-3**: 删除 `PixelGridAdapter` / `SimulationSystem` / `WorldGrid` / `PixelCell` + 全量回归测试 `[依赖 T10-2]`
- [ ] **T10-4**: 验证 4 MB 单文件 < 500 行 / 全局无 IEnumerator / 热路径无 `$"..."` / StressTest GC=0 / GameBootstrap 加载 < 3s(Win) / < 6s(Android) `[依赖 T10-1]`

## 阶段 11：CI/CD 与发布

- [ ] **T11-1**: 实现 `DeterminismRunner`（固定 Seed + 脚本化 InputRingBuffer + dt=1/30，三平台各跑 10000 帧，每 60 帧 dump MurmurHash3） `[依赖 T1-2, T2-2]`
- [ ] **T11-2**: 落地 B3 CI 卡点（IL2CPP Win/Mac/Android → DeterminismRunner → 哈希序列 diff → 热路径 GC=0 → BurstVersion 比对），失败输出五项定位 `[依赖 T11-1]`
- [ ] **T11-3**: 失败定位工具链（帧级二分 bisect + 模块级哈希 H_player/H_env/H_prng/H_input + 平台对拍 + 字段级 diff replay） `[依赖 T11-1]`
- [ ] **T11-4**: desync 对局内探测（客户端每 60 帧发 `C2G_StateHash` + 服务端多数派裁决 + 连续 3 次踢出 + 全不一致 FlagMatchAnomaly） `[依赖 T6-4]`
- [ ] **T11-5**: desync 回放定位（`G2C_HashMismatch` 标记分歧帧 F → 本地 InputRingBuffer + Seed 从 F-300 离线 replay → 字段级 diff） `[依赖 T11-4]`
- [ ] **T11-6**: 构建矩阵（每 PR 跑 PC+Android+服务端锁定还原+确定性回归；发版前补 Mac） `[依赖 T11-2]`
- [ ] **T11-7**: 灰度发布 5%→100%（白名单 + 匹配优先同版本 + 每档监控平稳） `[依赖 T6-12]`
- [ ] **T11-8**: 监控告警（8 项核心指标看板 + P0–P3 分级告警 + 灰度联动） `[依赖 T6-12]`
- [ ] **T11-9**: 热更回滚预案（CDN 保留最近 N 个历史版本包 + HotfixHash/AssetManifestHash 可回指） `[依赖 T6-5, T11-7]`
- [ ] **T11-10**: 压测（无头 bot 模拟 N×M 房间 + 测定容量上限 + 限流阈值） `[依赖 T11-2]`
- [ ] **T11-11**: 双语注释与编码约定 lint（CI 校验成对注释 / struct/sealed / AggressiveInlining / UniTask / ZLinq/ZString / Burst Job blittable） `[依赖 T0-3]`

## 阶段 12：数据埋点

- [ ] **T12-1**: 服务端输入派生追踪器（E1/E2/E6/E7/E8 + 场景事件从输入流派生） `[依赖 T6-2]`
- [ ] **T12-2**: 客户端带外上报通道（E3/E4/E5/MaterialContribution 上报，不进 InputPayload） `[依赖 T6-3]`
- [ ] **T12-3**: 服务端校验门（字段对齐 + 帧连续 + 状态哈希多数派校验 verify-don't-trust） `[依赖 T12-1, T12-2, T11-4]`
- [ ] **T12-4**: E9 match_settle 与 `G2C_MatchSettle` 1:1 字段对齐 + 服务端汇编 + 补 MmrDelta（ELO K=32 Base=1000） `[依赖 T12-3]`
- [ ] **T12-5**: last-contact 玩家归因（客户端确定性计算 `last_player_contact_id` + ATTR_WINDOW 默认 450 帧 + source_type 不覆盖规则） `[依赖 T12-2]`
- [ ] **T12-6**: E8 dash_used 携带 `iframes_active=false` + `knockback_during_dash` 验证 Dash≠免推 `[依赖 T12-2, T6-1]`
- [ ] **T12-7**: `C2G_DeviceReport` 协议落地（DeviceId 设备稳定 + InstallId 安装级，SHA-256 hex 64 字符，连接/重连一次性上报） `[依赖 T1-3]`
- [ ] **T12-8**: 服务端 Session/PlayerSlot 绑定稳定 ID + 留存/行为/MMR 看板字段预留 `[依赖 T12-7]`

## 阶段 13：跨平台一致性验证

- [ ] **T13-1**: 跨平台一致性校验脚本（PC↔移动字节比对 + 三平台 MurmurHash3 + 10000 帧回归） `[依赖 T5-5, T11-1]`
- [ ] **T13-2**: 4 人对局端到端 desync 测试（2 PC + 2 移动 5 分钟 ping 100ms，0 误报） `[依赖 T13-1, T6-2]`
- [ ] **T13-3**: 机型矩阵真机云测（旗舰/中端基准/低端降级/iOS） `[依赖 T11-2]`

## 阶段 14：试玩回归与数值验收

- [ ] **T14-1**: 试玩回归（以"被推挤出界占比 / 首杀时间 / 单局时长"三指标验收 K 表与 Mana） `[依赖 T10-3, T4-5, T2-4]`
- [ ] **T14-2**: PushOutRate/SelfElimRate/生物归因占比监控（数值 §13 风险） `[依赖 T12-5]`
- [ ] **T14-3**: GDD §7 漂移修正（护盾条目改为"挡伤不免疫击退、仅 HP 模式生效"） `[无依赖]`
- [ ] **T14-4**: `bodyWidth=60` [TUNING] 假设值复核（与 §2 PlayerBox 4×6px 量纲差异） `[依赖 T4-4]`

---

# Task Dependencies

- **T0 阶段**（全部 [可并行]）：工程基础设施前置
- **T1 阶段** 依赖 T0-3（asmdef）；T1-3 依赖 T0-1（路径修正）
- **T2 阶段** 依赖 T1-4（PixelData）；T2-2 依赖 T2-1（Adapter）
- **T3 阶段** 依赖 T1-5（Palette）+ T2-2（Simulation）
- **T4 阶段** 依赖 T3-1（Renderer）；T4-3/T4-4 依赖 T4-2（击退模型）
- **T5 阶段** 依赖 T0-3；T5-5 依赖 T1-3（协议）+ T5-2/T5-3（采集层）
- **T6 阶段** 依赖 T1-3（协议）+ T6-1（回滚）；T6-2 依赖 T6-1
- **T7 阶段** 依赖 T2-3（PixelWorldFactory）+ T6-6（场景事件）
- **T8 阶段** 依赖 T6-2（网络层）+ T1-5（Palette）
- **T9 阶段** 依赖 T3-1（Renderer）+ T6-1（回滚）；T9-9 [可并行]
- **T10 阶段** 依赖 T4-9（Gameplay 完成）
- **T11 阶段** 依赖 T1-2（哈希）+ T2-2（Simulation）+ T6-4（反作弊）
- **T12 阶段** 依赖 T6-2（网络层）+ T6-3（ValidateInput）
- **T13 阶段** 依赖 T5-5（输入）+ T11-1（DeterminismRunner）
- **T14 阶段** 依赖 T10-3（清理验收）+ T4-5（Mana）+ T2-4（材料反应）

## 可并行任务组

- T0-1 ~ T0-6（工程基础设施）全部可并行
- T1-1 与 T1-2（Fix64 与 PRNG/Hash）可并行
- T5-2 与 T5-3（PC 采集与移动采集）可并行
- T9-9（叙事字符串）可与其他所有任务并行
- T14-3（GDD 漂移修正）可独立并行
