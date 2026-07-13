# NoitaCA 项目基线规格 (Project Baseline Spec)

> 来源：综合 `Docs/` 下 27 份设计文档（2026-07-07 收口版）
> 技术栈：Unity 6000.3.19f1 + DOTS ECS + HybridCLR + YooAsset | 服务端 Fantasy-Net + net8
> 模式：2-8 人帧同步像素落沙对战

---

## Why

NoitaCA 项目已完成 27 份设计文档的编写与跨文档一致性收口（ADR D1–D6 钉死、协议 wire 落地）。但设计文档以叙述/表格为主，缺乏可验证的 SHALL 级需求与 WHEN/THEN 场景。本 spec 将设计文档转换为 spec-driven-development 格式（ADDED Requirements + Scenarios），建立项目唯一的**可验证需求基线**，用于指导后续编码、验收与回归测试。

---

## What Changes

- **新增**：将 27 份设计文档提炼为 12 个能力域的 SHALL 级需求（含成功/失败/边界场景）
- **新增**：建立 ADR D1–D6 决策点的需求化表达（边界死亡/缩圈/Dash/HP/AimPower/后坐力）
- **新增**：将跨文档一致性约束（确定性红线、协议零变更、反作弊公式）固化为可验证检查点
- **新增**：将 6 阶段 DOTS 重构路线图转化为有序任务清单
- **不变**：不修改任何设计文档内容；本 spec 是设计文档的需求化镜像

---

## Impact

- **Affected specs**: 本 spec 为项目基线，无前置 spec
- **Affected code**: 
  - 客户端：`NoitaCA.Core` / `NoitaCA.Lockstep` / `NoitaCA.Simulation` / `NoitaCA.Renderer` / `NoitaCA.Gameplay` / `NoitaCA.Editor` / `Hotfix`
  - 服务端：`Server/Main` / `Server/Entity`（含 `Generate/NetworkProtocol`）
  - 协议：`Tools/NetworkProtocol/Outer/OuterMessage.proto`
  - 构建：`ProtocolExportTool` / `ExporterSettings.json` / CI 流水线
- **Affected docs**: 全部 27 份 `Docs/*.md`（本 spec 为其需求化镜像，不替代原文）

---

## ADDED Requirements

### 能力域 1：核心玩法与游戏模式

### Requirement: 房间规模与游戏模式
The system SHALL 支持 2-8 人可配置房间（`RoomConfig.PlayerCount`，默认 4），提供自建房(Custom) / 快速匹配(Quick Match) / 排位赛(Ranked) 三种模式。

#### Scenario: 房主设置合法人数
- **WHEN** 房主在 Custom 模式下设置 `PlayerCount=6`
- **THEN** 服务器夹取 `Math.Clamp(6, 2, 8)`=6 存入 `RoomConfigComponent`，创建 6 个 `PlayerSlot`

#### Scenario: 非法人数被夹取
- **WHEN** 房主设置 `PlayerCount=10` 或 `PlayerCount=1`
- **THEN** 服务器强制夹取为 8 或 2

#### Scenario: 排位赛强制标准配置
- **WHEN** 玩家进入 Ranked
- **THEN** 强制标准配置（Cross_Quad 256 / Kill 边界），禁用对手文本聊天，胜者 +N MMR（K=32, BaseMmr=1000）

#### Scenario: 反作弊派生用法
- **WHEN** `AntiCheatSystem.CompareHashes` 计算多数派阈值
- **THEN** 阈值 = `⌊RoomConfig.PlayerCount / 2⌋ + 1`

---

### Requirement: 核心循环与节奏
The system SHALL 保证单局（4 人乱斗）从匹配到结算 < 8 分钟，每步流程（匹配/加载/结算）< 3s。

#### Scenario: 单局时长达标
- **WHEN** 4 人标准局结束
- **THEN** 总时长 < 8 min，首杀 30-60s，中期击杀间隔 20-40s，残局 1v1 边界博弈 30-90s

#### Scenario: 加载超时
- **WHEN** 加载竞技场耗时 ≥ 3s
- **THEN** 视为性能不达标，触发回归告警（不阻断对局）

---

### 能力域 2：边界死亡与击退机制（ADR D1）

### Requirement: 出界死亡规则（D1）
The system SHALL 以 Kill（出界即死）为全模式默认边界模式；玩家像素中心越过竞技场边界（`cy > Height + 16` 或 `cx ∉ [-16, Width + 16]`）即触发 `PlayerDeath`。

#### Scenario: 出界致死
- **WHEN** 玩家中心越过边界
- **THEN** 立即触发 `PlayerDeath`，玩家进入观战（1 秒延迟）

#### Scenario: 多人同帧出界
- **WHEN** 多名玩家同一帧出界
- **THEN** 按出界顺序定名次（1=冠军，N=末位）

#### Scenario: 最后存活者胜利
- **WHEN** 仅剩 1 名玩家未出界
- **THEN** 该玩家获胜，进入结算

#### Scenario: Bounce 变体（非默认）
- **WHEN** 房主配置 `BoundaryMode = Bounce`（仅自建房）
- **THEN** 边界反弹不致死，速度按弹性系数反射

#### Scenario: 边界余量精度
- **WHEN** 玩家中心恰位于 `Height + 16`
- **THEN** 不触发死亡（严格 > 越界）

---

### Requirement: 击退冲量推人模型
The system SHALL 通过击退冲量 K（px/s）实现"把对手挤出边界"，按 `v *= 0.90/step` 衰减，近似总位移 `≈ K / 3.0` px。设计铁律：仅在边缘 15-55px 内被命中才会被推出界。

#### Scenario: 轻弹边缘命中
- **WHEN** 玩家在距边界 ≤ 15px 处被基础 Bolt（K=45）命中
- **THEN** 累计位移 ≈ 15px，触发出界致死

#### Scenario: 轻弹中段命中不可推
- **WHEN** 玩家在距边界 > 55px 处被 Bolt 命中
- **THEN** 产生击退但不致死

#### Scenario: 击退堆叠 exploit 防护
- **WHEN** 单步内多源击退叠加
- **THEN** 单步击退速度 clamp 至 `MaxSpeed × 1.5 = 16px/步`

#### Scenario: 自身爆炸减半
- **WHEN** 玩家被自己施放的爆炸波及
- **THEN** 击退量 ×0.5

---

### Requirement: 缩圈策略（D2 - MVP 不做）
The system SHALL 在 v1/MVP 阶段不实现缩圈机制；为未来 ≥512 大型地图 / 8 人 BR 预留 `ShrinkEnabled` + `ShrinkRate` 配置位。

#### Scenario: MVP 默认无缩圈
- **WHEN** 4 人 256px 乱斗进行中
- **THEN** 不触发任何缩圈逻辑，靠边界博弈收口

---

### Requirement: Dash 冲刺（D3 - MVP 无 i-frames 纯位移）
The system SHALL 提供 Dash 能力：Distance 34px、Duration 0.16s（≈5 帧）、Cooldown 1.8s、无资源消耗、可被固体阻挡、**MVP 无 i-frames**（纯位移，不免疫任何效果含击退）。

#### Scenario: Dash 脱离险境
- **WHEN** 玩家按下 Dash 且 CD 已过，瞄准方向朝场内
- **THEN** 快速突进 34px 脱离边界危险区

#### Scenario: Dash 被固体阻挡
- **WHEN** Dash 路径上存在 Stone/Wood/Ice
- **THEN** Dash 命中即停，不穿墙

#### Scenario: Dash 期间仍可被击退（无 i-frames）
- **WHEN** 玩家在 Dash 期间被法术/爆炸命中
- **THEN** 仍受击退冲量，可被推出界（Dash ≠ 保命）

#### Scenario: Dash CD 内重复触发
- **WHEN** 玩家在 1.8s CD 内再次触发 Dash
- **THEN** 服务端 `ValidateInput` 确定性拒绝

---

### Requirement: HP 模型与免推铁律（D4）
The system SHALL 在 v1 默认采用纯出界致死模式，无 HP、无血条 HUD；no-HP 模式下禁止任何"免疫击退"效果。

#### Scenario: v1 默认无 HP
- **WHEN** 玩家被 Fire/Poison/Lava/Acid 命中
- **THEN** 不造成 HP 伤害，仅生成材料触发反应 + 提供击退 K

#### Scenario: 免推铁律
- **WHEN** 任何机制（Shield / Blink / Dash i-frames）尝试给予免推效果
- **THEN** 系统拒绝（§9.7 铁律），保持 Kill 招牌机制张力

#### Scenario: HP 模式可选（Mode B）
- **WHEN** 房主配置 Mode B
- **THEN** BaseHP=100，双死因并存，出界仍即死，TTK 20-40s

---

### 能力域 3：法术系统（ADR D5/D6）

### Requirement: 法术类型与优先级
The system SHALL 将法术分为 5 类（攻击/元素/环境改造/位移/防御），12 个法术标记 P0 或 P1 优先级。

#### Scenario: MVP(P0) 法术集合
- **WHEN** 开发 MVP 阶段
- **THEN** 实现 6 个 P0 法术：Bolt/Heavy/Bomb/Fire/Water/StoneWall + 移动层 Dash

#### Scenario: Ice 法术延后 P1
- **WHEN** 规划 Ice 冰锥开发
- **THEN** Ice 不在 MVP 内，需 P1 阶段；P1 包含 Ice/Poison/Acid/Lava/Shield/Blink

---

### Requirement: Shield 挡伤不抗推
The system SHALL 实现 Shield 护盾泡为"自身环绕挡伤泡"，阻挡材料/命中伤害，但 SHALL NOT 免疫击退。

#### Scenario: Shield 阻挡材料伤害
- **WHEN** Shield 激活期间（半径 16px、持 90 帧），火/毒/酸等材料伤害作用玩家
- **THEN** 材料伤害与命中伤害被阻挡

#### Scenario: Shield 不免疫击退
- **WHEN** Shield 激活期间被击退
- **THEN** Shield 不阻止击退速度施加（对应 D4 无 HP 设计）

---

### Requirement: 法术槽循环切换（D5）
The system SHALL 使用 4 法术槽循环切换模型：`SelectedSpellSlot`(0–3) 由 `SelectNext/SelectPrev` 边沿确定性自增/自减（循环），作为 `PlayerData` 字段进入快照、可回滚。

#### Scenario: 循环切换成功
- **WHEN** 当前 `SelectedSpellSlot=3` 时收到 `SelectNext` 边沿
- **THEN** 推导得到 `SelectedSpellSlot=0`（循环回绕）

#### Scenario: 切换免费
- **WHEN** 玩家按 SelectNext/Prev 切换法术
- **THEN** 不消耗 Mana、不消耗回合、不受冷却闸限制

---

### Requirement: AimPower 固定力度（D5）
The system SHALL 在 MVP 阶段使用固定力度模型，`InputPayload` 中 SHALL NOT 包含 `AimPower` 字段（uint8 仅预留，延后 P1）。

#### Scenario: 固定力度施法
- **WHEN** 玩家触发 `CastSpell`(Attack 边沿)
- **THEN** 初速为法术常量，方向=`AimAngle`（int16 [0,628]，×100 表示 [0,2π)）

#### Scenario: AimPower 字段不存在
- **WHEN** 客户端组装 InputPayload
- **THEN** InputPayload 保持 8B 结构，无 AimPower 字段

---

### Requirement: 法术后坐力（D6 - 速度冲量模型）
The system SHALL 在施法者成功施法发射类法术时，沿 `AimAngle` 反向施加一次性速度冲量到 `PlayerData.VelX/VelY`；非发射类法术(StoneWall/Lava/Shield/Blink)后坐力 = 0。

#### Scenario: 发射类法术后坐力差异化
- **WHEN** 施法者施法 Bolt/Water/Ice/Poison/Acid/Heavy/Bomb/Fire
- **THEN** 分别获得 120/160/220/240/260/320/360/390 px/s 反向速度

#### Scenario: 非发射类法术无后坐力
- **WHEN** 施法者施法 StoneWall/Lava/Shield/Blink
- **THEN** RecoilSpeed=0，无后坐力产生

#### Scenario: 后坐力确定性
- **WHEN** 后坐力产生
- **THEN** = f(SelectedSpellSlot 派生法术, AimAngle)，纯模拟派生，不进 InputPayload、不 bump ProtocolVersion

#### Scenario: 后坐力不进状态哈希新增字段
- **WHEN** 每 60 帧比对状态哈希
- **THEN** 后坐力仅修改 `PlayerData.VelX/VelY` 与位置（已被 MurmurHash3 覆盖），零新增哈希字段

#### Scenario: 自爆护栏
- **WHEN** 施法者在距边界 > 135px 的开阔地施放任意法术
- **THEN** 不因后坐力触界（防误杀）

#### Scenario: 贴边自爆保留
- **WHEN** 施法者在距界 ≤ 135px 贴边朝场内发射
- **THEN** 允许触发自爆出界致死（高风险战术，D1 一致）

---

### Requirement: 魔力与冷却双闸
The system SHALL 使用共享 Mana（上限 100、回复 15/s）+ 每法术独立冷却（帧@30Hz）双闸。

#### Scenario: Mana 不足时施法
- **WHEN** 玩家当前 Mana < 法术消耗
- **THEN** 施法被拒绝，无投射物生成、无后坐力产生

#### Scenario: 法术 CD 未到
- **WHEN** 玩家在法术 CD 内重复施放同一法术
- **THEN** 施法被拒绝

---

### Requirement: 像素物质元胞自动机
The system SHALL 实现 12 种像素材料（Sand/Water/Stone/Wood/Fire/Smoke/Steam/Ice/Lava/Acid/Poison/Ash）的元胞自动机模拟，包含密度排序、温度场、相变与 9 类化学反应。

#### Scenario: 粉末重力堆积
- **WHEN** Sand 像素上方无支撑
- **THEN** 受重力下落、横向扩散

#### Scenario: 水+火→蒸汽
- **WHEN** Water 与 Fire 相邻
- **THEN** 反应生成 Steam

#### Scenario: 熔岩+水→石+蒸汽
- **WHEN** Lava 与 Water 相邻
- **THEN** 瞬间生成 Stone + Steam（造墙/封路）

#### Scenario: 确定性迭代方向
- **WHEN** CA 模拟运行
- **THEN** 偶数帧正向、奇数帧反向迭代消除方向偏置

#### Scenario: 像素冲突解决
- **WHEN** 多像素竞争同一格
- **THEN** 按 (x, y) 字典序升序先到先得

---

### 能力域 4：帧同步网络架构

### Requirement: 帧同步架构与帧率基线
The system SHALL 以 30 Hz 逻辑帧（输入锁步同步）+ 60 Hz 渲染帧（插值）运行对局；逻辑帧固定 `dt = 1/30`，禁止使用 `Time.deltaTime`；服务器不跑像素模拟（方案 B）。

#### Scenario: 正常 30Hz 对局
- **WHEN** N 人对局进行且 ping < 100ms
- **THEN** 服务器每 33ms 聚合所有玩家 `C2G_Input`，打包为 `G2C_FrameBatch` 下发

---

### Requirement: InputPayload 8 字节定稿（ProtocolVersion=2）
The system SHALL 定义 `InputPayload` 为 8 字节结构（`MoveX:int16` + `MoveY:int16` + `ActionFlags:uint16` + `AimAngle:int16`），删除原 `SpellId` 字段；`ProtocolVersion` = 2。

#### Scenario: 正常输入上报
- **WHEN** 玩家在 30Hz tick 边界产生输入
- **THEN** wire 体积 8B，`|MoveX|≤100 ∧ |MoveY|≤100 ∧ √(MoveX²+MoveY²)≤100`，`AimAngle∈[0,628]`

#### Scenario: 旧版本客户端被拒绝
- **WHEN** 客户端上报 `ProtocolVersion=1`（仍含 `SpellId`）
- **THEN** 服务器拒绝其进对局

---

### Requirement: ActionFlags 位分配与保留位禁用
The system SHALL 将 `InputActionFlags` 定义为 16 bit：bit0 Jump、bit1 Attack、bit2/3 Spell1/Spell2 **保留位（禁用，服务器拒绝）**、bit4 Pickup、bit5 Drop、bit6 SelectNext、bit7 SelectPrev、bit8 UseConsumable、bit9 Dash、bit10-15 保留位强制 0。

#### Scenario: 非法置位被拒
- **WHEN** 客户端上报 `ActionFlags` 中 bit2/3 或 bit10-15 任一为 1
- **THEN** `ValidateInput` 返回 false，服务器拒绝该帧输入

---

### Requirement: 确定性红线（不进流不进哈希）
The system SHALL 保证 `PauseMenu`、`QuickChat`、`结算按钮` 等 UI 交互不进入 `InputPayload`、不参与状态哈希。

#### Scenario: 暂停不污染模拟
- **WHEN** 玩家打开暂停菜单
- **THEN** 暂停为本地 UI 行为，不产生 `InputPayload` 字段

#### Scenario: 快捷聊天走独立通道
- **WHEN** 玩家发送 QuickChat
- **THEN** 走 `C2G_ChatMsg{Channel=QuickChat}` protobuf 通道，不进入 30Hz 输入流

---

### Requirement: 分层回滚模型
The system SHALL 采用三层混合回滚：静态层只读不回滚；环境层周期调和（每 6 帧 = 200ms 服务端权威快照）不实时回滚；玩家层 GGPO 回滚（`INPUT_DELAY=2`, `MAX_ROLLBACK=7`, `SNAPSHOT_BUFFER=8`）。

#### Scenario: 玩家层失配触发回滚
- **WHEN** `G2C_FrameBatch` 检测到远端输入预测失配
- **THEN** 回滚到失配帧 + 用服务端权威输入覆盖 + 重模拟到当前帧，7 帧回滚总成本 < 4ms

#### Scenario: 环境层失配不回滚
- **WHEN** 环境层 chunk 哈希失配
- **THEN** 不回滚，等周期调和（200ms 内服务端下发权威像素数据覆盖）

---

### Requirement: 断线重连（30s 窗口 + 无敌 3s）
The system SHALL 支持 30 秒重连窗口：断线瞬间玩家角色无敌 3 秒（网络容错，防被秒）；30 秒未重连判负。

#### Scenario: 30s 内重连成功
- **WHEN** 玩家断线后在 30s 内发起 `C2G_ReconnectRequest`
- **THEN** 服务端下发最新全量快照（分 64 片每片 16KB），客户端 replay 追上当前帧

#### Scenario: 30s 超时判负
- **WHEN** 30 秒未重连
- **THEN** 判负，房间继续 N-1 人对局

#### Scenario: 无敌期内被攻击
- **WHEN** 玩家断线后 3 秒内被攻击
- **THEN** 无敌生效（网络容错窗口，非战斗无敌帧）

---

### Requirement: 赛后结算下行（G2C_MatchSettle 精简 wire）
The system SHALL 在 `MatchEnded` 后下发 `G2C_MatchSettle`（protobuf，不进 30Hz）；仅 `MmrDelta` 为服务端权威，其余确定性量客户端从本地 sim 派生。

#### Scenario: 正常结算
- **WHEN** 对局结束
- **THEN** 服务端下发 `G2C_MatchSettle`，客户端展示结算界面，更新 MMR

#### Scenario: 回放隐藏
- **WHEN** MVP 阶段结算界面
- **THEN** `ReplayAvailable` 恒 false，隐藏"查看回放"按钮（P4+ 启用时追加字段，不破坏 wire）

---

### Requirement: 出生点确定性生成
The system SHALL 基于 `RoomConfig.PlayerCount` + `ArenaConfig.Seed` 确定性生成 N 个出生点，最小间距 ≥ `Width / 4`，使用 `Xorshift128Plus`。

#### Scenario: 多端出生点一致
- **WHEN** 对局开始时各客户端独立生成出生点
- **THEN** 基于 `ArenaConfig.Seed` 与 `PlayerCount`，所有客户端生成相同位置序列

#### Scenario: 间距约束
- **WHEN** N=4，Width=256
- **THEN** 4 个出生点任意两点间距 ≥ 64

---

### 能力域 5：确定性保证

### Requirement: 确定性基础（Fix64 + Xorshift128Plus + MurmurHash3）
The system SHALL 全部浮点数学替换为 `Fix64`（Q31.32，范围 ±2^15 限制）；PRNG 用 `Xorshift128Plus`（SplitMix64 链式初始化）；状态哈希用 `MurmurHash3`（遍历全 9 字段，short 字段 `& 0xFFFF` 防符号扩展）。

#### Scenario: 三平台哈希一致
- **WHEN** 相同输入在 Win/Mac/Android 跑 10000 帧
- **THEN** 状态哈希完全一致

#### Scenario: 多线程 = 单线程
- **WHEN** 同输入下多线程模式 vs 单线程模式
- **THEN** 状态哈希一致

#### Scenario: Burst 配置
- **WHEN** Burst 编译
- **THEN** `FloatMode.Strict` + `FloatPrecision.Standard` + `CompileSynchronously=true`

---

### Requirement: 确定性 CI 强制门禁
The system SHALL 在 CI 中落地确定性卡点：Win/Mac/Android 三平台各跑 10000 帧，每 60 帧 dump 全量 MurmurHash3 状态哈希，CI 自动逐行 diff。

#### Scenario: 全采样点一致
- **WHEN** 三平台 10000 帧（≈167 个采样点）hash 序列全一致
- **THEN** B3 通过，放行合并

#### Scenario: 任一采样点分歧
- **WHEN** 任一采样点不一致
- **THEN** 阻断合并（不可豁免），输出五项定位信息（F0/F1/模块/字段/平台）

#### Scenario: 热路径零 GC
- **WHEN** Profiler GC Alloc 扫描热路径
- **THEN** 0 字节/帧

#### Scenario: Burst 覆盖率 100%
- **WHEN** Burst Inspector 检查玩法 Job
- **THEN** 覆盖率 100%，`BurstVersion` 锁定 1.8.29

---

### 能力域 6：DOTS ECS 架构与重构

### Requirement: 程序集边界隔离
The system SHALL 通过 asmdef 硬性隔离 AOT 层与 Hotfix 层：AOT 层包含 `NoitaCA.Core` / `NoitaCA.Lockstep` / `NoitaCA.Simulation` / `NoitaCA.Renderer` / `NoitaCA.Gameplay` / `NoitaCA.Editor`；Hotfix 层包含 `Hotfix.asmdef`。

#### Scenario: AOT 反向依赖被禁止
- **WHEN** AOT 程序集尝试 `using` Hotfix
- **THEN** CI 静态校验阻断

#### Scenario: Hotfix 禁触确定性类型
- **WHEN** Hotfix 引用 `NoitaCA.Simulation` 内部确定性类型
- **THEN** CI 静态校验阻断

#### Scenario: 确定性逻辑驻留 AOT
- **WHEN** 进入 `InputPayload` 或参与锁步状态哈希的逻辑
- **THEN** 全部在 AOT，绝不在 Hotfix

---

### Requirement: 热更时机约束（Battle 禁热更）
The system SHALL 仅在大厅 / Lobby / 结算状态允许热更；对局 Battle 状态与 Lobby→Battle 加载过渡期绝对禁止热更（禁 `LoadFromStream`）。

#### Scenario: 对局 Battle 禁热更
- **WHEN** 对局进行中，客户端拉到新 Hotfix 版本
- **THEN** 服务器已锁定 `HotfixVersion`，新版本不应用，等对局结束

#### Scenario: 对局结束后强制热更
- **WHEN** 对局结束
- **THEN** 强制走热更流程，再进下一局

---

### Requirement: DOTS ECS 6 阶段重构
The system SHALL 将 MonoBehaviour 玩法代码全量重构为 5 程序集 + Bootstrap 4 MB，6 阶段实施，每阶段保证编译通过。

#### Scenario: Phase 1 基础骨架
- **WHEN** Phase 1 完成
- **THEN** 5 asmdef 编译通过、Fix64 精度 ≤ 2.3e-10、Xorshift128Plus 同种子 1000 次一致

#### Scenario: Phase 6 清理验收
- **WHEN** Phase 6 完成
- **THEN** PixelGridAdapter 删除、10000 帧三平台哈希一致、编译零警告、热路径 GC=0

#### Scenario: 单帧逻辑耗时达标
- **WHEN** 256×256 默认场景运行
- **THEN** 单帧逻辑耗时 < 16ms（i7-12700H）/ < 25ms（Android 中端机）

---

### Requirement: 双缓冲像素写入
The system SHALL 在 MovementJob/InteractionJob/SpellMovementJob 中采用双缓冲 NativeArray（CurrentPixels 只读 + NextPixels 可写），Job 完成后主线程 Swap。

#### Scenario: 并行像素写入安全
- **WHEN** 多 chunk 并行 Job 写入像素
- **THEN** 写 NextPixels 不写 CurrentPixels，避免跨 chunk 写邻居异常

---

### 能力域 7：反作弊与安全

### Requirement: 反作弊 - 输入验证
The system SHALL 在服务器 `AntiCheatSystem.ValidateInput` 中校验：帧频率、`MoveX`/`MoveY` 范围与模长、`AimAngle` 范围、`ActionFlags` 保留位禁用、位移幅度（容忍 1.5 倍）。

#### Scenario: 首帧通过
- **WHEN** 玩家发送首帧输入（`LastInputFrame` 初始化为 -1）
- **THEN** `dt = frame + 1 ≥ 1`，通过频率检查

#### Scenario: 斜向加速被拒
- **WHEN** 输入 `MoveX=100, MoveY=100`（模长 ≈ 141）
- **THEN** 模长检查 > 100，拒绝

#### Scenario: 瞬移被拒
- **WHEN** 玩家单帧位移 > `MaxSpeed / 30 * 1.5`
- **THEN** 位移幅度检查失败，拒绝

---

### Requirement: 反作弊 - 状态哈希多数派裁决
The system SHALL 每 60 帧由客户端上报 `C2G_StateHash`，服务器比对；多数派阈值 = `⌊RoomConfig.PlayerCount / 2⌋ + 1`；少数派连续 3 次踢出。

#### Scenario: 2 人对局双方一致
- **WHEN** N=2，两客户端哈希一致
- **THEN** 阈值=2，达成多数派

#### Scenario: 4 人对局 3 票多数派
- **WHEN** N=4，3 客户端哈希一致，1 客户端不同
- **THEN** maxCount=3 ≥ threshold=3，少数派 `MismatchCount++`，连续 3 次踢出

#### Scenario: 全部不一致
- **WHEN** 所有客户端哈希两两不同
- **THEN** 标记对局"异常"，录像送人工分析

---

### Requirement: 反作弊 - Hotfix.dll 数字签名
The system SHALL 采用非对称数字签名（RSA-2048 或 ECDSA-P256）：构建机签名 `SHA256(Hotfix.dll)`，客户端内置公钥验签，服务器验签 + 白名单 + 防重放。

#### Scenario: 篡改被拒
- **WHEN** 客户端 Hotfix.dll 被修改，签名验证失败
- **THEN** 服务器拒绝连接

#### Scenario: 防重放
- **WHEN** 同一 `(RoomId, PlayerId, H)` 在同一会话内重复上报
- **THEN** 第二次被拒绝

---

### Requirement: 反作弊 - 场景事件帧延迟解密
The system SHALL 对 `G2C_SceneEvent` 的 `EncryptedPayload` 用 AES-128-CTR 加密，密钥派生自 `frame = TargetFrame - 3`。

#### Scenario: 正常解密
- **WHEN** 服务器生成 `TargetFrame = currentFrame + 90` 的道具生成事件
- **THEN** 客户端在 `TargetFrame - 3` 帧时才派生解密密钥，3 秒后道具落地

#### Scenario: 预读失败
- **WHEN** 客户端试图在 `TargetFrame - 3` 之前读取
- **THEN** 无法派生密钥，解密失败

---

### Requirement: 版本对齐校验
The system SHALL 在对局开始前校验全房玩家 `HotfixHash` / `AssetManifestHash` / `ProtocolVersion` / `BurstVersion` 四项一致性。

#### Scenario: 资源版本不一致
- **WHEN** 房间内 N 人 `AssetManifestHash` 不一致
- **THEN** 服务器拒绝开始，提示"玩家 X 资源版本不一致"

#### Scenario: Burst 版本不一致
- **WHEN** 跨版本 Burst 可能破坏确定性
- **THEN** `BurstVersion` 不一致时拒绝开始对局

---

### 能力域 8：关卡与内容生成

### Requirement: 竞技场确定性生成
The system SHALL 以 `ArenaConfig.Seed` 为唯一熵源，使用 `Xorshift128Plus` 派生分流（terrainRng/spawnRng/itemRng/creatureRng），绝不使用 `System.Random` / `UnityEngine.Random`。

#### Scenario: 同 Seed 三平台一致
- **WHEN** 同一 `(Seed, ArenaConfig, N)` 在三端生成
- **THEN** 状态哈希一致

#### Scenario: 客户端本地生成被拒绝
- **WHEN** 客户端尝试本地计算地形或刷新位置
- **THEN** 拒绝——仅服务器依据 Seed 生成，客户端消费

---

### Requirement: 竞技场参数与地形
The system SHALL 使用 `Width/Height ∈[64,1024]`（默认 256×256）、`BoundaryMode=Kill`（默认）、`MaxSpeed=320 px/s`；地形采用"悬浮岛 + 四周虚空"形态。

#### Scenario: 4 人默认档
- **WHEN** `RoomConfig.PlayerCount=4`
- **THEN** 推荐尺寸 256×256

#### Scenario: 跌出即死（D1 原生满足）
- **WHEN** 玩家跌出岛缘
- **THEN** 越过边界判定阈值触发 `PlayerDeath`，无额外致死陷阱

---

### Requirement: 道具刷新与 Telegraph
The system SHALL 由服务器 `SceneScheduler` 按 `ItemSpawnTable` 权重确定性推导刷新，目标帧前 90 帧（3s）下发明文加密事件。

#### Scenario: Telegraph 公平预告
- **WHEN** 服务器决定在 TargetFrame 刷新道具
- **THEN** 全客户端在 `TargetFrame - 90` 收到事件并播放光柱，3s 后道具落地

#### Scenario: 道具权重分布
- **WHEN** 刷新道具
- **THEN** 武器(30)/法术卷轴(20)/治疗药水(25)/护盾(10)/速度增益(10)/炸弹(5) 按权重随机

#### Scenario: 治疗药水 no-HP 兜底
- **WHEN** no-HP 模式下拾取治疗药水
- **THEN** 改为净化瓶：清除 debuff + 1s 免环境控制（不免疫击退）

---

### Requirement: 生物 AI 确定性
The system SHALL 保证生物 AI 在帧同步下完全确定性，状态机 `Idle→Wander→Chase→Attack/Flee`，走玩家层 GGPO 回滚。

#### Scenario: 三平台哈希一致
- **WHEN** 同一 `ArenaConfig.Seed` 与相同输入序列在三端推进
- **THEN** 三端生物位置/速度/AI 状态/PRNG 状态逐帧一致

#### Scenario: MVP 无血量驱动
- **WHEN** 设计者试图以 HP 阈值驱动 `Flee`
- **THEN** 拒绝（MVP 决策表基于距离/材料状态，无 HP）

#### Scenario: 生物走玩家层回滚
- **WHEN** 玩家层回滚到第 K 帧
- **THEN** 生物状态同步恢复到第 K 帧快照

---

### Requirement: MVP 生物最小集
The system SHALL 每竞技场按 Seed 确定性生成 1 中立（Slime）+ 1 敌对（Firebat）最小集。

#### Scenario: MVP 配置
- **WHEN** P0 默认 `Cross_Quad(256)` 加载
- **THEN** 场上恰好 1 Slime（外围瓣）+ 1 Firebat（中央高台）

#### Scenario: 生物不直接致死（D4 一致）
- **WHEN** 玩家被生物攻击
- **THEN** 仅产生击退/控制，致死只由出界判定

---

### 能力域 9：UI/输入/跨平台

### Requirement: 12 动作词汇表契约
The system SHALL 在 PC 与移动端之间保持同一套 12 个动作语义不变，平台仅改变"如何产生动作值"。

#### Scenario: 同一意图跨平台等价
- **WHEN** PC 玩家按 `Q` 键与移动端玩家点按"切法术▶"按钮
- **THEN** 两端 `Capture()` 产出的 `ActionFlags.SelectNext` 位均被置位，字节级相同

#### Scenario: 动作数守恒失败
- **WHEN** 任何采集层尝试新增第 13 个动作
- **THEN** 编译期/审查期被拒

---

### Requirement: PC 键鼠采集通道
The system SHALL 提供 `KeyboardMouseInputProvider` 实现 `IPlayerInputProvider`，按默认键位表采集 12 动作，键位 100% 可重映射。

#### Scenario: 斜向归一化
- **WHEN** 玩家同时按 `W+D`
- **THEN** `MoveX=71, MoveY=71`（模长=100，归一化）

#### Scenario: 滚轮切法术防惯性连切
- **WHEN** 玩家滚动鼠标滚轮
- **THEN** 每次"刻度变化"算一次边沿

---

### Requirement: 移动端触屏采集通道
The system SHALL 提供 `TouchScreenInputProvider` 实现 `IPlayerInputProvider`，默认双手横屏模式，支持 ≥2 并发触控点。

#### Scenario: 多点触控并行
- **WHEN** 左拇指拖摇杆同时右拇指点施法按钮
- **THEN** 两触控点独立维护，互不干扰

#### Scenario: 摇杆死区
- **WHEN** 摇杆偏移 < 12%
- **THEN** `MoveX=MoveY=0`，防误触抖动

---

### Requirement: 30Hz tick 边界结算
The system SHALL 不在事件/渲染帧即时产出 `InputPayload`，由 `ClientLockstepScheduler` 在 30Hz 逻辑 tick 边界统一调用 `Capture()` 结算。

#### Scenario: PC 60Hz 渲染不影响 30Hz 采样
- **WHEN** PC 渲染帧率波动 55–65Hz
- **THEN** 输入在 30Hz tick 边界结算，渲染帧只更新缓存

#### Scenario: 边沿长按不连发
- **WHEN** 玩家长按施法键
- **THEN** 仅按下瞬间置位 `ActionFlags.Attack`，长按不连发

---

### Requirement: AimAngle 量化确定性
The system SHALL 通过 `Fix64Math.Atan2`（Burst 兼容）计算 `AimAngle`，量化公式 `AimAngle = clamp(round(angle / (2π) * 628), 0, 628)`，禁止 `Math.Atan2`。

#### Scenario: PC 与移动同量化函数
- **WHEN** PC 鼠标与移动触点产生同一世界向量
- **THEN** 两端共用同一 `Fix64Math.Atan2` 量化函数，输出整数值相等

#### Scenario: 浮点禁用
- **WHEN** 采集层使用 `Math.Atan2` 或 `UnityEngine.Mathf`
- **THEN** 破坏确定性，红线拒绝

---

### Requirement: HUD 元素与无 HP 血条
The system SHALL 在 MVP HUD 不绘制 HP 血条（D4），4 个法术槽由 `SelectedSpellSlot` 推导高亮，HUD 仅做表现。

#### Scenario: MVP HUD 无血条
- **WHEN** 默认 MVP 模式
- **THEN** HUD 不画血条；显示击杀数、边界警示、当前法术槽高亮、道具

#### Scenario: HUD 确定性违规
- **WHEN** HUD 动画使用 `Time.deltaTime` 或随机抖色
- **THEN** 破坏帧同步确定性，CI 红线触发拒绝

---

### Requirement: 屏幕自适应与无障碍
The system SHALL 锁定横屏（Landscape），UI 以 `Screen.safeArea` 内缩，移动端可点控件 ≥ 44pt，主操作 ≥ 64pt；提供色盲模式（RGB565 板内）、键位重映射、高对比、减少动效。

#### Scenario: 横屏锁定
- **WHEN** 设备处于竖屏启动
- **THEN** 强制旋转为横屏

#### Scenario: 色盲安全色在 16 位板内
- **WHEN** 启用色盲安全配色
- **THEN** 替代色仍可编码进 `ushort`，不引入 RGB565 板外色

---

### 能力域 10：美术/音频/叙事

### Requirement: RGB565 16 位色域强制
The system SHALL 将所有像素颜色编码为 16-bit RGB565（`ushort`），仅允许 12 值白名单，禁止任何板外色。

#### Scenario: 板外色检测
- **WHEN** 美术资产含 hex `0xA012`（非白名单）
- **THEN** CI `PaletteValidator` 输出 `violations.json` 阻断合并

#### Scenario: 渲染禁用项
- **WHEN** 配置 URP 2D Renderer
- **THEN** 无任何 Post-processing Volume（Bloom/Vignette/ColorLUT 全禁），禁 `Time.deltaTime` 色偏移

---

### Requirement: VFX 确定性驱动
The system SHALL 驱动所有 VFX 来自服务器 `G2C_SceneEvent` 或确定性模拟事件 `VfxTrigger`；VFX SHALL NOT 进入 `InputPayload`、不污染状态哈希。

#### Scenario: TelegraphAoe 由模拟推导
- **WHEN** Bomb 施法
- **THEN** 弹道/落点由 Fix64 物理确定性计算，模拟写入 `VfxTrigger`，全客户端一致

#### Scenario: 客户端随机 VFX
- **WHEN** VFX 用 `UnityEngine.Random` 决定粒子初值
- **THEN** 违反确定性红线，校验拒绝

---

### Requirement: 音频确定性红线
The system SHALL 保证音频不进入 `InputPayload`、不参与状态哈希；音频触发来自确定性 `SimAudioEvent`，回滚时丢弃并重建。

#### Scenario: 音频不入输入流
- **WHEN** 计算锁步哈希
- **THEN** 音频事件缓冲不计入状态哈希

#### Scenario: 回滚重建
- **WHEN** `RollbackTo(mismatch)`
- **THEN** `audioBuffer[mismatch..current]` 整体丢弃；`ReplayForward` 重新产生事件

#### Scenario: 禁用客户端随机决定发声
- **WHEN** 用 `Random.Range` 选「播火球音还是水流音」
- **THEN** 禁止（语义音必须由 `SwitchParam` 决定）；cosmetic pitch±5% 允许

---

### Requirement: 像素字体
The system SHALL 提供像素字体资产：ASCII 8×8 等宽、CJK P0 子集 16×16，字形为 1-bit mask + palette tinted。

#### Scenario: HUD 数字等宽
- **WHEN** 渲染计分/倒计时数字
- **THEN** 字宽恒 8，无跳动

#### Scenario: CJK 缺失降级
- **WHEN** 字符不在已烘焙 P0 字表
- **THEN** 渲染 `□` 占位（`0x514A`），不崩溃

---

### Requirement: 世界观纯包装
The system SHALL 将所有叙事内容视为 Presentation-only skin，不绑定任何机制，严格服从 ADR 五点裁决。

#### Scenario: 叙事不改机制
- **WHEN** 应用流派旗帜/叙事名
- **THEN** 零机制绑定，仅可选展示文本

#### Scenario: 出界即死唯一死因
- **WHEN** 玩家越过边界
- **THEN** 坠渊者叙事称谓，无 HP/血量/第二死因叙事

---

### 能力域 11：结算系统

### Requirement: 结算触发与排名派生
The system SHALL 在 `MatchEnded` 时弹出结算界面，排名由客户端从本地 sim 派生，`Players[]` 长度 = `playerCount`，所有玩家 `Take(playerCount)` 入榜。

#### Scenario: 最后存活者诞生
- **WHEN** Kill 模式下仅 1 名玩家未出界
- **THEN** 该玩家胜，按出局顺序定名次

#### Scenario: 排名 tie-break 全序
- **WHEN** 两玩家 `EliminationFrame` 相同
- **THEN** 按 sim 内 `PlayerDeath` 事件确定性顺序 → `SurvivalMs` → `SlotIndex` 逐级裁决

#### Scenario: 结算显示所有玩家
- **WHEN** 8 人对局结束
- **THEN** 排名榜 8 行可滚动，本地玩家行高亮

---

### Requirement: 结算界面规范
The system SHALL 结算界面使用 RGB565 状态色（胜=`0x73FF` 冰青、负=`0xF900` 火橙、平=`0xA294` 中性灰），整数帧驱动动画，横屏锁定。

#### Scenario: 揭晓动画整数帧驱动
- **WHEN** 结算界面进入揭晓阶段
- **THEN** 横幅按整数帧线性插值滑入，禁用 `Time.deltaTime` 与随机抖色

#### Scenario: 渐变被拒绝
- **WHEN** 实现尝试在胜负色之间做色相 tween
- **THEN** 拒绝，改用离散状态切换

#### Scenario: 再来一局模式差异
- **WHEN** 排位赛玩家试图即时重开
- **THEN** 按钮替换为"返回大厅 → 重新匹配"（防 MMR 滥用）

---

### 能力域 12：工程构建与 CI/CD

### Requirement: HybridCLR/YooAsset 协同
The system SHALL 通过 HybridCLR 导出 Hotfix.dll，用 RSA 私钥签名，Hash + 签名写入 YooAsset 资源包元数据。

#### Scenario: 构建签名链路
- **WHEN** CI 在 IL2CPP 全量构建后
- **THEN** 产出 `Hotfix.dll` → 计算 `H=SHA256(Hotfix.dll)` → 私钥签名 → 产物附带三元组写入 YooAsset 元数据

#### Scenario: 客户端启动验签
- **WHEN** 客户端启动
- **THEN** AOT 内置 pubKey 验本地 Hotfix.dll

---

### Requirement: 协议版本策略
The system SHALL 因删除 `InputPayload.SpellId`（12B→8B）bump ProtocolVersion 1→2；删除/改类型/改字段号必须 bump 且旧客户端拒绝。

#### Scenario: 删除字段 bump
- **WHEN** 改 `OuterMessage.proto` 删除 `InputPayload.SpellId`
- **THEN** ProtocolVersion 自增到 2，重新生成双端代码（禁手改 Generate）

#### Scenario: 保留位禁用不 bump
- **WHEN** bit2/3、bit10-15 保留位禁用
- **THEN** 不 bump，但 `ValidateInput` 必须拒绝置位

---

### Requirement: NuGet 版本锁定
The system SHALL 在服务端 `Server/*.csproj` 中所有 `PackageReference` 写死具体版本（Fantasy-Net 2025.2.1402 / NLog 5.5.1），启用 `RestorePackagesWithLockFile`，CI 以 `--locked-mode` 还原。

#### Scenario: 锁定还原通过
- **WHEN** CI 运行 `dotnet restore --locked-mode`
- **THEN** 所有版本与 `packages.lock.json` 一致，还原通过

#### Scenario: 版本漂移
- **WHEN** 出现 `*` 或锁定文件漂移
- **THEN** 还原失败，阻断合并

---

### Requirement: 灰度发布策略
The system SHALL 按 5%→20%→50%→100% 推进灰度，每升一档前确认上一档监控指标平稳。

#### Scenario: 灰度推进
- **WHEN** 新 Hotfix.dll + 资源包上传 YooAsset CDN
- **THEN** 匹配优先同版本，灰度比例 5%→20%→50%→100%

#### Scenario: 灰度回滚
- **WHEN** 灰度桶内 desync 率/崩溃率越阈值
- **THEN** 回退白名单到上一稳定 `HotfixHash` + `AssetManifestHash`，历史包仍在 CDN

#### Scenario: AOT 层不可热更回滚
- **WHEN** 回滚涉及 ProtocolVersion / BurstVersion 变更
- **THEN** 无法热更回滚，必须发新客户端版本

---

### Requirement: 监控告警
The system SHALL 监控 8 项核心指标（desync_rate / hash_mismatch_count / version_reject_rate / hotfix_sig_fail_rate 等），分 P0–P3 四级告警。

#### Scenario: P0 致命告警
- **WHEN** `desync_rate` > 0 持续
- **THEN** 立即电话/IM 通知 on-call，评估灰度回滚

---

### Requirement: 双语注释与编码约定
The system SHALL 对所有类型与公开方法提供成对中文+英文注释（`// 职责：` + `// Responsibility:`）；值类型用 `struct`，不可继承类用 `sealed`，热路径方法加 `[MethodImpl(MethodImplOptions.AggressiveInlining)]`。

#### Scenario: 双语注释成对
- **WHEN** 编写 public API
- **THEN** 提供成对中文+英文注释

#### Scenario: 禁用 IEnumerator 协程
- **WHEN** 代码使用 `IEnumerator` 协程
- **THEN** 禁止，统一 `UniTask`

#### Scenario: 热路径禁 LINQ/$ 字符串
- **WHEN** 热路径使用 LINQ 或 `$"..."`
- **THEN** 禁止，用 `ZLinq.AsRef()` / `ZString`

---

### Requirement: 数据埋点混合派生模型
The system SHALL 采用混合派生模型：服务端权威子集（E1/E2/E6/E7/E8）由输入流推导；E3/E4/E5 由客户端带外上报，服务端用状态哈希多数派裁决校验后采纳。

#### Scenario: 服务端权威子集正常派生
- **WHEN** 玩家 Attack 位触发且 CD/Mana 通过
- **THEN** E2 spell_cast 由服务端从 Attack 边沿 + 镜像 SelectedSpellSlot 派生

#### Scenario: 客户端上报与多数派哈希分歧
- **WHEN** E3/E4/E5 客户端上报值与状态哈希多数派不一致
- **THEN** 服务端拒收 + 告警（verify-don't-trust）

#### Scenario: 红线：所有事件不进 InputPayload
- **WHEN** 任意分析事件尝试进入 InputPayload
- **THEN** 红线禁止

---

### Requirement: last-contact 玩家击杀归因
The system SHALL 出界触发 E5 boundary_kill 时，按 `last_player_contact_id` 是否存在且 `(death_frame - last_player_contact_frame) <= ATTR_WINDOW`（默认 15s=450 帧）决定 credited_player。

#### Scenario: 玩家推挤出界归信用
- **WHEN** 玩家 A 用重弹把 B 推到边缘，B 自行走出界，仍在 ATTR_WINDOW 内
- **THEN** 信用归 A

#### Scenario: 生物推挤出界仍归玩家
- **WHEN** 生物把 B 推出界，B 最近一次玩家接触是 5s 前的 A
- **THEN** 生物不覆盖 last_player_contact → 信用归 A

#### Scenario: 自灭
- **WHEN** B 全程无玩家接触，自己走/掉出界
- **THEN** 自灭（SelfElim），不计入任何人 Kills

---

## MODIFIED Requirements

无（本 spec 为项目基线，无前置 spec 可修改）。

---

## REMOVED Requirements

无（本 spec 为项目基线，无前置 spec 可移除）。

---

## 附录：ADR 决策点索引

| ADR | 决策点 | 内容 | 影响域 |
|-----|--------|------|--------|
| **D1** | 边界死亡 | Kill（出界即死）为全模式默认 | 关卡形态、胜负条件、HUD |
| **D2** | 缩圈 | MVP 不做缩圈 | 关卡/网络负荷 |
| **D3** | Dash | 保留，MVP 无 i-frames 纯位移 | GDD §6.1、InputPayload 位、移动手感 |
| **D4** | HP 死亡 | MVP 纯出界致死，无 HP | HUD 血条、结算、免推铁律 §9.7 |
| **D5** | AimPower + 法术切换 | MVP 固定力度 + 循环切换 | InputPayload 协议、施法数值 |
| **D6** | 法术后坐力 | 速度冲量模型 + 允许自爆 + StoneWall/Lava=0 | 法术手感、协议零变更 |
| **A1** | GDD 缺 Dash | 采纳谋远段落整合进 GDD §6.1 | GDD 一致性 |
| **A2** | Unity 版本 | 统一 6000.3.19f1 | 构建一致性 |
| **A3** | NuGet 版本 | Fantasy-Net 2025.2.1402 / NLog 5.5.1 锁定 | 构建确定性 |
| **A4** | SpellId 孤儿字段 | 删除（InputPayload 12B→8B，ProtocolVersion 1→2） | 协议破坏性变更 |
| **A5** | Spell1/Spell2 位 | 保留为 reserved，v1 禁用 | ActionFlags 位分配 |
| **A6** | AimAngle 量化 | [0,2π)×100 → int16 [0,628] | 确定性 |

---

## 附录：跨文档一致性铁律

1. **确定性红线**：凡进入 `InputPayload` 的字段都参与锁步状态哈希；暂停/快捷聊天/结算按钮不进流不进哈希
2. **协议零变更**：后坐力作为模拟派生量，不进 InputPayload（8B 不变）、不 bump ProtocolVersion
3. **免推铁律（§9.7）**：no-HP 模式下禁止任何"免疫击退"效果
4. **反作弊公式**：多数派阈值 = `⌊RoomConfig.PlayerCount / 2⌋ + 1`
5. **出生点确定性**：基于 `RoomConfig.PlayerCount` + `ArenaConfig.Seed`
6. **结算显示**：`Players[]` 长度 = `playerCount`，`Take(playerCount)` 入榜
7. **RGB565 合规**：UI/VFX 颜色全从 12 值白名单取色
8. **整数帧动画**：禁用 `Time.deltaTime` 与随机抖色
