# NoitaCA 多人联机帧同步对战设计文档

> **文档版本**：v1.0
> **创建日期**：2026-07-06
> **方案**：B（中继 + 事件注入）
> **状态**：待评审

---

## 1. 概述

将现有 NoitaCA 像素落沙元胞自动机改造为 **4 人乱斗帧同步对战游戏**。采用 **纯输入锁步 + 服务器中继 + 场景事件注入** 架构，集成聊天、房间、匹配、场景四大系统。

### 1.1 核心决策

| 决策项 | 选定方案 | 依据 |
|--------|---------|------|
| 同步模型 | 纯输入锁步（全确定性） | 用户决策 |
| 对战规模 | 4 人乱斗 | 用户决策 |
| 服务器权威 | 中继 + 事件注入（方案 B） | 用户决策 |
| 多线程 | 保留 DOTS 多线程，显式排序控制差异，周期调和 | 用户调整 |
| 世界规模 | 可配置竞技场，掉出边界即死亡（百战天虫规则） | 用户调整 |
| 程序集边界 | 模拟代码在 AOT，Hotfix 只放协议处理 | qq362946 建议 |
| 代码热更 | HybridCLR，对局中锁定版本，仅非对局状态热更 | Ante + qq362946 |
| 资源热更 | YooAsset，对局前版本对齐校验，影响玩法的资源必须全房一致 | 用户要求 |

### 1.2 设计目标

- **核心循环**：找匹配 → 加载竞技场 → 战斗 → 结算 → 找匹配，每步 <3s
- **匹配等待**：<30s
- **tick rate**：30 Hz 逻辑帧（输入同步），60 Hz 渲染帧（插值）
- **世界规模**：默认 256×256 像素，可在 `[64, 1024]` 区间配置
- **断线重连**：支持 30s 内重连续局
- **热更隔离**：对局 Battle 状态下绝对不应用 Hotfix.dll 热更，避免 desync

---

## 2. 项目背景与现状

### 2.1 现有技术栈

- **客户端**：Unity 6 (6000.3.19f1) + HybridCLR 8.12.0 + DOTS ECS 1.4.x + URP 2D 17.4.0 + Fantasy.Unity 2025.2.1402 + YooAsset 2.3.19 + UniTask 2.5.11 + ZLinq 1.5.6 + ZString 2.6.0
- **服务端**：.NET 8 + Fantasy-Net (Main/Entity/Hotfix 三层) + NLog
- **协议**：protobuf + MemoryPack 双序列化

### 2.2 现有目录结构

```
Noita/
├── Client/Unity/                    # Unity 客户端（待迁移到 Client/）
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── Simulation/          # 像素模拟核心
│   │   │   ├── Abilities/           # 法术系统
│   │   │   ├── Creatures/           # 生物系统
│   │   │   ├── Equipment/           # 装备系统
│   │   │   ├── Demo/                # 压测
│   │   │   └── Hotfix.asmdef        # 热更程序集
│   │   └── Docs/DOTS_REFACTORING_DESIGN.md
│   ├── HybridCLRData/
│   └── ProjectSettings/
├── Server/
│   ├── Main/                        # 服务器入口
│   ├── Entity/                      # 实体层
│   └── Hotfix/                      # 热更层
├── Tools/
│   ├── NetworkProtocol/             # .proto 协议定义
│   └── ProtocolExportTool/          # 协议导出工具
│       └── ExporterSettings.json    # 路径配置（当前为 macOS 默认值，需修正）
└── Docs/
    └── Noita-Dev-Agent.md           # 项目配置
```

### 2.3 已识别问题

1. `ExporterSettings.json` 三条路径均为 macOS 默认值 `/Users/fantasy/...`，需改为 Windows 路径
2. `fantasy_quickstart.md` 写 Unity 2022.3.62 LTS，实际为 6000.3.19f1
3. Fantasy-Net / NLog 包版本用 `*`，需锁定
4. 客户端路径含 `Client/Unity/` 多余层级，需扁平化到 `Client/`

---

## 3. 总体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        客户端 ×4                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  HybridCLR AOT 程序集                                       │  │
│  │  ┌────────────────────┐  ┌────────────────────────────┐  │   │
│  │  │ NoitaCA.Lockstep   │  │ NoitaCA.Simulation (DOTS)  │  │   │
│  │  │ (帧同步调度+输入队列)│  │ (Burst+IJobChunk+定点数)   │  │   │
│  │  └─────────┬──────────┘  └─────────┬──────────────────┘  │   │
│  │            │                       │                       │  │
│  │  ┌─────────▼─────────────────────▼──────────────────┐    │  │
│  │  │     确定性快照缓冲 + 状态哈希 (每60帧比对)         │    │  │
│  │  └─────────────────────────────────────────────────┘    │  │
│  └────────────────────────┬──────────────────────────────┘   │
│                           │                                     │
│  ┌────────────────────────▼──────────────────────────────┐   │
│  │  Hotfix.asmdef (热更)                                    │  │
│  │  协议 handler / UI 逻辑 / 房间管理 / 匹配请求            │  │
│  └────────────────────────┬──────────────────────────────┘   │
└───────────────────────────┼──────────────────────────────────┘
                            │ Fantasy Session
                            │ (MemoryPack 输入 + ProtoBuf 事件)
┌───────────────────────────▼──────────────────────────────────┐
│                       Fantasy-Net 服务器                        │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Main 层 (AOT)                                            │  │
│  │  网络底层 / Session 生命周期 / NLog                       │  │
│  └────────────────────────┬───────────────────────────────┘  │
│  ┌────────────────────────▼───────────────────────────────┐  │
│  │  Entity 层 (AOT)                                          │  │
│  │  MatchSession / GameRoom / Player 实体                    │  │
│  │  状态哈希存储 / 输入验证 / 场景事件调度器                  │  │
│  └────────────────────────┬───────────────────────────────┘  │
│  ┌────────────────────────▼───────────────────────────────┐  │
│  │  Hotfix 层 (可热更)                                       │  │
│  │  匹配算法 / 房间规则 / 场景事件时间线 / 反作弊规则         │  │
│  └────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────┘
```

### 3.1 数据流

1. **输入流**：客户端 → `C2G_Input`（MemoryPack）→ 服务器中继 → `G2C_FrameBatch`（MemoryPack，含所有玩家输入）→ 所有客户端
2. **事件流**：服务器场景调度器 → `G2C_SceneEvent`（ProtoBuf RouteMessage，帧延迟解密）→ 所有客户端
3. **状态校验**：客户端每 60 帧计算状态哈希 → `C2G_StateHash` → 服务器比对 → 不匹配标记
4. **控制流**：匹配/房间/聊天走标准 Fantasy 消息

---

## 4. 客户端设计

### 4.1 程序集划分（重构后）

```
Assets/Scripts/
├── NoitaCA.Core.asmdef              # DOTS 组件、共享数据（AOT）
├── NoitaCA.Simulation.asmdef        # ISystem 作业（AOT，确定性模拟）
├── NoitaCA.Lockstep.asmdef          # 帧同步调度、输入队列、状态哈希（AOT）
├── NoitaCA.Renderer.asmdef          # MonoBehaviour 渲染桥接（AOT）
├── NoitaCA.Gameplay.asmdef          # 玩家/法术/生物/装备（AOT）
├── NoitaCA.Editor.asmdef            # 编辑器工具（AOT）
└── Hotfix.asmdef                    # 协议 handler、UI、房间管理（热更）
```

**关键约束**（来自 qq362946 + Newman）：
- ✅ `NoitaCA.Lockstep` / `NoitaCA.Simulation` 必须 AOT，**绝不在 Hotfix**
- ✅ Hotfix 只放：协议消息 handler、UI 事件、匹配/房间请求转发
- ✅ Hotfix 可调参数：`MatchConfig`、`RoomConfig`（数据对象，非模拟逻辑）
- ❌ Hotfix 禁止：访问 DOTS `EntityManager`、写 `IComponentData`、跑 `IJobChunk`

### 4.2 DOTS 确定性策略

**多线程保留 + 显式排序控制差异 + 周期调和**（用户决策调整）

#### 4.2.1 数学确定性

- **定点数库**：选用 `Fix64`（Q31.32 格式），覆盖 ±2.1e9 范围，精度 2.3e-10
- **替换范围**：
  - `Unity.Mathematics.float` → `Fix64`（位置、速度、温度）
  - `Unity.Mathematics.float2` → `Fix64Vec2`
  - `Math.Sqrt` / `Math.Sin` 等 transcendental → 定点近似（查表 + 牛顿迭代）
- **Burst 配置**：
  ```csharp
  [BurstCompile(
      CompileSynchronously = true,
      FloatMode = FloatMode.Strict,
      FloatPrecision = FloatPrecision.Standard)]
  struct PixelSimulationJob : IJobChunk { ... }
  ```

#### 4.2.2 多线程迭代顺序确定性

- **chunk 排序**：`EntityQuery.ToComponentDataArrayAsync` 后按 `Entity.Index` 升序排序，再分批
- **批处理**：`IJobChunk` 按 chunk index 顺序调度，每批 N chunks（N 可配，默认 64）
- **冲突解决规则**：多像素竞争同一格时，按 `(x, y)` 字典序升序处理，先到先得
- **迭代方向**：偶数帧从左上到右下，奇数帧从右下到左上（消除方向偏置）

#### 4.2.3 周期调和（10 Hz）

- 每 6 逻辑帧（200ms）做一次环境层状态调和
- 服务器广播"权威环境快照"（压缩的关键 chunk 状态）
- 客户端用快照覆盖本地环境状态（玩家层不受影响）
- 视觉差异通过插值平滑过渡，避免突变

### 4.3 HybridCLR 边界

| 代码类型 | 程序集 | 热更 | 说明 |
|---------|--------|------|------|
| 协议消息 handler | Hotfix.asmdef | ✅ | 仅处理消息分发，不含模拟 |
| UI 逻辑 | Hotfix.asmdef | ✅ | 聊天、房间、匹配界面 |
| 匹配/房间请求 | Hotfix.asmdef | ✅ | 转发到服务器 |
| 可调参数对象 | Hotfix.asmdef | ✅ | `MatchConfig` 等 |
| 帧同步调度 | NoitaCA.Lockstep.asmdef | ❌ | AOT，避免热更 desync |
| DOTS ISystem | NoitaCA.Simulation.asmdef | ❌ | AOT，Burst 编译 |
| DOTS 组件 | NoitaCA.Core.asmdef | ❌ | AOT |
| 渲染桥接 | NoitaCA.Renderer.asmdef | ❌ | AOT |

**Hotfix.dll 签名校验**（来自 Newman）：
- 客户端构建时签名 Hotfix.dll，公钥内置在 AOT
- 服务器连接时挑战 `SHA256(Hotfix.dll)`，客户端用私钥签名回应
- 不匹配拒绝连接（防止注入修改版 Hotfix）

### 4.4 世界规模与边界

**可配置竞技场**（来自 Purho + 用户调整）：

```csharp
// NoitaCA.Core/ArenaConfig.cs
public struct ArenaConfig
{
    public int Width;       // [64, 1024]，默认 256
    public int Height;      // [64, 1024]，默认 256
    public int Seed;        // 服务器下发，确定性生成地形
    public BoundaryMode Mode; // Kill | Bounce | Wrap
}

public enum BoundaryMode
{
    Kill,       // 掉出边界即死亡（百战天虫规则，默认）
    Bounce,     // 边界反弹
    Wrap        // 环绕
}
```

**死亡判定**：
- 玩家像素中心 y > Height + 16 或 x ∉ [-16, Width+16] → 触发 `PlayerDeath` 事件
- 死亡玩家观战剩余对局，可发送快捷表情

### 4.5 热更与资源管理（HybridCLR + YooAsset 协同）

多人对战下热更的核心矛盾：**对局中不能热更（会 desync），但全局热更能力必须保留**。本节明确 HybridCLR 代码热更与 YooAsset 资源热更的协同边界。

#### 4.5.1 双轨热更模型

```
┌─────────────────────────────────────────────────────────┐
│  代码热更 (HybridCLR)                                    │
│  ├── 范围: Hotfix.asmdef 内的所有代码                     │
│  ├── 时机: 仅在"非对局"状态（大厅/房间/结算）             │
│  ├── 分发: YooAsset 下发 Hotfix.dll + 签名               │
│  └── 校验: 服务器挑战 SHA256(Hotfix.dll) + 资源版本号     │
├─────────────────────────────────────────────────────────┤
│  资源热更 (YooAsset)                                     │
│  ├── 范围: UI prefab / 特效 / 音效 / 场景预制体 / 配置表  │
│  ├── 时机: 启动时 + 大厅空闲时（对局中不下载）            │
│  ├── 分发: YooAsset CDN + 版本清单                        │
│  └── 校验: 服务器连接时上报资源版本，不匹配拒绝进对局     │
└─────────────────────────────────────────────────────────┘
```

#### 4.5.2 Hotfix.dll 热更流程（HybridCLR）

**热更内容范围**（仅以下代码可热更）：
- 协议消息 handler（新增/修改消息处理逻辑）
- UI 逻辑（界面调整、新增界面）
- 匹配/房间请求转发逻辑
- 反作弊规则参数（阈值、冷却数值）
- 场景事件时间线配置（道具刷新表、Telegraph 帧数）
- `MatchConfig` / `RoomConfig` 等数据对象

**热更时机约束**（来自 Ante + qq362946）：
- ✅ 大厅状态：可热更
- ✅ 房间 Lobby 状态：可热更（房主可强制等待所有人热更完成）
- ✅ 结算状态：可热更
- ❌ **对局 Battle 状态：绝对禁止热更**
  - 对局中 `AssemblyLoadContext.LoadFromStream` 替换 Hotfix.dll 会导致：
    1. 已注册的协议 handler 引用旧 dll，新 handler 不生效
    2. 静态字段值丢失（MMR 计算中间态、匹配队列）
    3. 4 个客户端热更时序不一致 → desync
- ❌ 对局加载阶段：禁止热更（从 Lobby → Battle 的过渡期）

**热更流程**：
```
1. 客户端启动 → 检查 YooAsset 版本清单
2. 发现 Hotfix.dll 新版本 → 下载到本地缓存
3. 校验签名（公钥验签）
4. 进入大厅 → 提示"有更新，是否应用"
5. 用户确认 → 退出当前 Scene → AssemblyLoadContext.Unload → LoadFromStream(新 dll)
6. 重新进入大厅，服务器挑战校验新 dll 哈希
7. 校验通过 → 可进入匹配/房间
```

**对局中热更策略**：
- 服务器在对局开始时锁定 `HotfixVersion`
- 对局中即使客户端拉到新版本，也**不应用**，等对局结束
- 对局结束后强制走热更流程，再进下一局

#### 4.5.3 YooAsset 资源热更

**资源分类与热更策略**：

| 资源类型 | 热更 | 时机 | 对局影响 |
|---------|------|------|---------|
| UI prefab | ✅ | 大厅 | 无（对局中不加载大厅 UI） |
| 特效（法术/爆炸） | ✅ | 大厅 | **需版本对齐**（不同版本特效不同 → 视觉 desync） |
| 音效 | ✅ | 大厅 | 无（不影响逻辑） |
| 场景预制体（竞技场地形） | ✅ | 大厅 | **需版本对齐**（地形不同 → 玩法 desync） |
| 道具 prefab | ✅ | 大厅 | **需版本对齐** |
| 配置表（道具表/法术表） | ✅ | 大厅 | **必须版本对齐** |
| Telegraph 光柱特效 | ✅ | 大厅 | 需版本对齐 |
| Texture / Material | ✅ | 大厅 | 无（纯视觉） |

**关键约束**：影响**逻辑或玩法表现**的资源（特效、地形、道具、配置表）必须所有客户端版本一致，否则进对局前服务器拒绝。

#### 4.5.4 版本对齐机制

```csharp
// 客户端连接服务器时上报版本
public struct ClientVersionReport
{
    public string HotfixHash;          // SHA256(Hotfix.dll)
    public string AssetManifestHash;   // YooAsset 版本清单哈希
    public uint ProtocolVersion;       // 协议版本号
    public uint BurstVersion;          // Burst 编译器版本
}
```

**服务器校验规则**：
1. `HotfixHash` 必须在服务器白名单内（允许多版本并存，灰度发布）
2. `AssetManifestHash` 必须与房间内其他玩家一致（进房间时检查）
3. `ProtocolVersion` 必须匹配当前服务器版本
4. `BurstVersion` 必须匹配（跨版本 Burst 可能破坏确定性）

**对局开始前最终校验**（`Battle` 状态进入前）：
```
房主点击"开始" →
  服务器遍历房间内 4 个玩家 →
    比对 HotfixHash（必须全一致或全在白名单兼容组）→
    比对 AssetManifestHash（必须全一致）→
    比对 BurstVersion（必须全一致）→
  任一不匹配 → 拒绝开始，提示"玩家 X 资源版本不一致"
```

#### 4.5.5 YooAsset 与场景系统协同

**场景事件的资源加载**（来自 §7.4 场景系统）：

```
服务器下发 G2C_SceneEvent{ItemSpawn, ItemId=42, TelegraphFrames=90}
  ↓
客户端收到（TargetFrame - 90 帧时解密）
  ↓
LockstepScheduler 在 TargetFrame - 90 调度:
  1. 通过 YooAsset 加载 ItemId=42 的 prefab（异步，UniTask）
  2. 在 TargetFrame - 30 时实例化 Telegraph 光柱
  3. 在 TargetFrame 时实例化道具本体
  4. 若资源未加载完成 → 回退到默认占位 prefab + 上报错误
```

**资源预加载策略**：
- 对局加载阶段（Lobby → Battle 过渡，约 3 秒）：预加载本局可能用到的所有道具 prefab（按 ItemSpawnTable）
- Telegraph 期间（3 秒）：足够 YooAsset 异步加载完成
- 兜底：资源未加载完成时用纯色方块占位，不影响逻辑（道具拾取判定走服务器，不依赖本地 prefab）

#### 4.5.6 热更版本灰度发布

```
版本号格式: <ProtocolVersion>.<HotfixVersion>.<AssetVersion>
例: 1.0.3

灰度策略:
  1. 新版本 Hotfix.dll + 资源包上传 YooAsset CDN
  2. 服务器白名单加入新版本哈希，标记为"灰度"
  3. 匹配时优先同版本匹配（白名单组内匹配）
  4. 灰度比例从 5% → 20% → 50% → 100%
  5. 全量后旧版本移出白名单，强制热更
```

---

## 5. 服务端设计

### 5.1 Fantasy 三层归属

| 层 | 职责 | 程序集 | 热更 |
|----|------|--------|------|
| **Main** | 网络底层、Session 生命周期、NLog | Main.csproj | ❌ |
| **Entity** | `MatchSession` / `GameRoom` / `Player` 实体、状态哈希存储、输入验证 | Entity.csproj | ❌ |
| **Hotfix** | 匹配算法、房间规则、场景事件时间线、反作弊规则 | Hotfix.csproj | ✅ |

### 5.2 房间实体模型

```
MatchSession (根实体)
├── MatchmakerComponent        # 匹配器
├── GameRoom ×N (子实体)
│   ├── RoomConfigComponent    # 房间配置
│   ├── PlayerSlot ×4          # 玩家槽位
│   │   ├── PlayerInputQueue   # 输入队列（最近 30 帧）
│   │   ├── PlayerStateHash    # 状态哈希历史
│   │   └── PlayerConnection   # 连接状态
│   ├── SceneScheduler         # 场景事件调度器
│   │   ├── EventTimeline       # 已调度的场景事件
│   │   └── ItemSpawnTable      # 道具刷新表
│   └── FrameClock             # 房间帧时钟
└── ChatChannel                # 大厅聊天
```

### 5.3 中继逻辑

**服务器不跑像素模拟**，只做：

1. **输入聚合**：每 33ms（30Hz）收集所有玩家 `C2G_Input`，打包为 `G2C_FrameBatch`
2. **输入验证**（来自 Newman）：
   - 移动速度合理性（不超过 `MaxSpeed * dt * 1.5`）
   - 冷却检查（法术、道具使用）
   - 范围检查（拾取、攻击距离）
3. **事件注入**：`SceneScheduler` 按时间线生成 `G2C_SceneEvent`，附带目标帧号
4. **状态哈希比对**：每 60 帧收集客户端哈希，多数派一致为权威，少数派标记

### 5.4 状态哈希校验

```csharp
// Entity/Components/PlayerStateHashComponent.cs
public class PlayerStateHashComponent : EntityComponent
{
    // 哈希历史，环形缓冲
    public CircularBuffer<(long Frame, Hash128 Hash)> History = new(60);
    public int MismatchCount;
    public DateTime LastMismatchTime;
}
```

**规则**：
- 客户端每 60 帧（2 秒）发送 `C2G_StateHash{frame, hash}`
- 服务器比对 4 个客户端哈希
- 多数派一致 → 标记少数派为"待审查"
- 连续 3 次少数派 → 踢出房间
- 全部不一致 → 标记对局"异常"，录像送人工分析

---

## 6. 协议设计

### 6.1 消息类型一览

| 消息 | 方向 | 序列化 | 频率 | 用途 |
|------|------|--------|------|------|
| `C2G_Input` | C→S | MemoryPack | 30 Hz | 玩家输入 |
| `G2C_FrameBatch` | S→C | MemoryPack | 30 Hz | 帧批次（所有玩家输入） |
| `G2C_SceneEvent` | S→C | ProtoBuf + Route | 事件驱动 | 场景事件（道具生成/消失） |
| `C2G_StateHash` | C→S | MemoryPack | 0.5 Hz | 状态哈希上报 |
| `G2C_HashMismatch` | S→C | ProtoBuf | 异常时 | 哈希不匹配通知 |
| `C2G_ChatMsg` | C→S | ProtoBuf | 用户驱动 | 聊天消息 |
| `G2C_ChatMsg` | S→C | ProtoBuf | 用户驱动 | 聊天广播 |
| `C2G_MatchRequest` | C→S | ProtoBuf | 一次性 | 匹配请求 |
| `G2C_MatchResult` | S→C | ProtoBuf | 一次性 | 匹配结果 |
| `C2G_RoomCreate` | C→S | ProtoBuf | 一次性 | 创建房间 |
| `C2G_RoomJoin` | C→S | ProtoBuf | 一次性 | 加入房间 |
| `G2C_RoomState` | S→C | ProtoBuf | 状态变更 | 房间状态推送 |

### 6.2 输入协议（MemoryPack）

```protobuf
// Outer/OuterMessage.proto 追加
// Protocol MemoryPack

message C2G_Input // IMessage
{
    uint64 Frame;          // 客户端帧号
    InputPayload Payload;  // 输入载荷
}

message InputPayload
{
    int16 MoveX;           // 定点数 ×100，[-100, 100]
    int16 MoveY;           // 定点数 ×100
    uint16 ActionFlags;    // 位标志：Jump/Attack/Spell1/Spell2/Pickup/Drop
    int16 AimAngle;        // 定点数 ×100，[0, 628] 表示 [0, 2π]
    uint32 SpellId;        // 法术 ID（0 = 无）
}

message G2C_FrameBatch // IMessage
{
    uint64 Frame;                    // 服务器帧号
    repeated PlayerInput Inputs;     // 所有玩家输入
}

message PlayerInput
{
    uint32 PlayerId;
    InputPayload Payload;
}
```

**为什么 MemoryPack**：输入消息每秒 30 × 4 = 120 条，体积小频率高，MemoryPack 比 ProtoBuf 快 5-10 倍。

### 6.3 场景事件协议（ProtoBuf + RouteMessage）

```protobuf
// Outer/OuterMessage.proto 追加
// Protocol ProtoBuf

message G2C_SceneEvent // IRouteMessage
{
    uint64 TargetFrame;     // 事件生效帧
    SceneEventType Type;
    oneof Payload
    {
        ItemSpawnEvent ItemSpawn;
        ItemDespawnEvent ItemDespawn;
        TerrainEvent Terrain;
        HazardEvent Hazard;
    }
    bytes EncryptedPayload; // 帧延迟加密载荷（见 6.5）
}

enum SceneEventType
{
    ItemSpawn = 0;
    ItemDespawn = 1;
    TerrainChange = 2;
    HazardTrigger = 3;
}

message ItemSpawnEvent
{
    uint32 ItemId;
    int32 PosX;     // 定点数 ×100
    int32 PosY;
    uint32 TelegraphFrames; // 预告帧数（默认 90 = 3s @30Hz）
}

message ItemDespawnEvent
{
    uint32 ItemUid;
}
```

**为什么 RouteMessage**：场景事件定向投递到房间实体，Fantasy 的 RouteMessage 支持实体寻址。

### 6.4 聊天/房间/匹配协议

```protobuf
// Outer/OuterMessage.proto 追加

message C2G_ChatMsg
{
    ChatChannel Channel;   // Lobby/Room/QuickChat
    string Text;           // 文本（Lobby/Room）
    uint32 QuickChatId;    // 快捷短语 ID（QuickChat）
}

message G2C_ChatMsg
{
    uint32 SenderId;
    string SenderName;
    ChatChannel Channel;
    string Text;
    uint32 QuickChatId;
}

enum ChatChannel
{
    Lobby = 0;
    Room = 1;
    QuickChat = 2;
}

message C2G_MatchRequest
{
    MatchMode Mode;        // QuickMatch/Ranked/Custom
    uint32 DesiredPlayers; // 2/4
}

message G2C_MatchResult
{
    uint64 RoomId;
    uint64 ServerSessionId;
    repeated uint32 PlayerIds;
    ArenaConfig ArenaConfig;
}

enum MatchMode
{
    QuickMatch = 0;
    Ranked = 1;
    Custom = 2;
}
```

### 6.5 场景事件帧延迟解密

**威胁**（来自 Newman）：客户端预读未来帧事件获取不公平预判。

**方案**：
- 服务器生成 `G2C_SceneEvent` 时，`TargetFrame` 为未来帧（如 `currentFrame + 90`）
- `EncryptedPayload` 用 `AES-128-CTR` 加密，密钥派生自 `frame = TargetFrame - 3`
- 客户端在 `TargetFrame - 3` 帧时才派生解密密钥
- 解密后才能读取 `ItemSpawnEvent.PosX/PosY`，但 `TelegraphFrames` 已开始播放光柱
- 玩家看到光柱 → 3 秒后道具落地 → 公平

```csharp
// 密钥派生
byte[] DeriveKey(ulong frame, byte[] roomSecret)
{
    using var hmac = new HMACSHA256(roomSecret);
    var frameBytes = BitConverter.GetBytes(frame);
    return hmac.ComputeHash(frameBytes)[..16];
}
```

---

## 7. 四大系统设计

### 7.1 聊天系统

**分层设计**（来自 Cagan）：

| 频道 | 范围 | 类型 | 排位赛 |
|------|------|------|--------|
| Lobby | 大厅所有玩家 | 全文本 | N/A |
| Room | 房间内 4 人 | 全文本 | ✅ |
| QuickChat | 对局内 4 人 | 预设短语 + 表情 | ✅（仅快捷） |

**快捷短语表**（`QuickChatId`）：
- 0: GG
- 1: Nice!
- 2: Help!
- 3: Run!
- 4: Attack!
- 5: Defend!
- 6-15: 表情动画

**过滤**：
- 文本聊天走服务器关键词过滤（敏感词库）
- 排位赛禁对手文本（仅快捷短语）
- 举报系统：玩家可举报聊天记录，写入 `ReportEntity`

### 7.2 房间系统

**三种房间模式**：

#### 7.2.1 自建房（Custom Room）

```
玩家点击"创建房间"
  → 服务器生成 6 字符房间码（如 "A3K9X2"）
  → 房间进入 Lobby 状态
  → 房主可配置：地图大小/边界模式/道具刷新率/对局时长
  → 其他玩家输入房间码加入
  → 房主点击"开始"→ 进入 Battle 状态
```

#### 7.2.2 快速匹配（Quick Match）

```
玩家点击"快速匹配"
  → 服务器 Matchmaker 收集请求
  → 凑齐 4 人 → 生成房间 → 直接进入 Battle 状态
  → 默认配置：256×256 / Kill 边界 / 标准刷新率 / 5 分钟
```

#### 7.2.3 排位赛（Ranked）

```
玩家点击"排位赛"
  → 服务器按 MMR 匹配（ELO 算法）
  → 凑齐 4 人相近 MMR → 生成房间
  → 强制配置：256×256 / Kill 边界 / 标准刷新率 / 5 分钟 / 禁对手文本
  → 结算后更新 MMR
```

**房间状态机**：

```
Lobby ──(房主开始)──► Battle ──(结束/超时)──► Result ──(5s)──► Lobby
                          │
                          └──(所有人离开)──► Closed
```

### 7.3 匹配系统

**MMR 算法**（ELO 变体）：

```csharp
// Hotfix/Matchmaking/MmrCalculator.cs
public static class MmrCalculator
{
    public const int K = 32;
    public const int BaseMmr = 1000;

    public static int UpdateMmr(int currentMmr, int[] opponentMmrs, int rank)
    {
        // rank: 1=冠军, 4=末位
        var expected = 0.0;
        foreach (var opp in opponentMmrs)
        {
            expected += 1.0 / (1.0 + Math.Pow(10, (opp - currentMmr) / 400.0));
        }
        expected /= opponentMmrs.Length;

        var actual = (4 - rank) / 3.0; // 1→1.0, 4→0.0
        return currentMmr + (int)(K * (actual - expected));
    }
}
```

**匹配队列**：
- 按 MMR 分桶：±50, ±100, ±200, ±500
- 等待 10s 扩大桶范围
- 等待 30s 提示"是否接受 AI 填充"

### 7.4 场景系统

**职责**：控制道具生成与消失，由服务器 `SceneScheduler` 调度。

#### 7.4.1 道具刷新表

```csharp
// Hotfix/Scene/ItemSpawnTable.cs
public class ItemSpawnTable
{
    public SpawnEntry[] Entries;

    public struct SpawnEntry
    {
        public uint ItemId;
        public int Weight;              // 刷新权重
        public uint MinFrame;           // 最早刷新帧
        public uint MaxFrame;           // 最晚刷新帧
        public uint CooldownFrames;     // 同道具冷却
    }
}
```

#### 7.4.2 刷新调度

```
对局开始 (Frame=0)
  → SceneScheduler 加载 ItemSpawnTable
  → 按权重随机选择道具
  → 计算 TargetFrame = currentFrame + TelegraphFrames(90)
  → 生成 G2C_SceneEvent{TargetFrame, ItemSpawnEvent}
  → 加入 EventTimeline
  → 服务器在 TargetFrame 前 3 帧广播（帧延迟解密）

道具消失:
  → 玩家拾取 → C2G_Input 触发 Pickup 动作
  → 服务器验证 → 广播 G2C_SceneEvent{ItemDespawn}
  → 或道具超时（5 分钟未拾取）→ 自动消失
```

#### 7.4.3 Telegraph 动画

- 服务器决定生成道具时，立即广播加密的 `G2C_SceneEvent`
- 客户端在 `TargetFrame - 90`（即 3 秒前）开始播放光柱 telegraph
- 3 秒后道具落地，可拾取
- 玩家可看到光柱争夺点位（来自 Cagan）

#### 7.4.4 道具类型

| 类型 | 效果 | 刷新权重 |
|------|------|---------|
| 武器 | 增加攻击力/范围 | 30 |
| 法术卷轴 | 学习临时法术 | 20 |
| 治疗药水 | 恢复生命 | 25 |
| 护盾 | 临时无敌 3s | 10 |
| 速度增益 | 移动速度 +50% 持续 5s | 10 |
| 炸弹 | 投掷爆炸物 | 5 |

---

## 8. 确定性与多线程

### 8.1 多线程策略（用户决策：保留 + 控制 + 调和）

```
每逻辑帧 (33ms):
├── 阶段 1: 输入收集 (1ms)
│   └── 单线程，从输入队列读取
├── 阶段 2: 玩家层模拟 (5ms)
│   ├── IJobChunk 并行处理玩家实体
│   └── chunk 排序: 按 Entity.Index 升序
├── 阶段 3: 环境层 CA 模拟 (15ms)
│   ├── IJobChunk 并行处理像素 chunk
│   ├── chunk 排序: 按 (chunkX, chunkY) 字典序
│   └── 冲突解决: 多像素竞争同一格 → (x,y) 字典序升序
├── 阶段 4: 状态快照 (3ms)
│   └── 单线程，计算关键状态哈希
└── 阶段 5: 调和检查 (每 6 帧, 2ms)
    └── 单线程，比对服务器快照
```

### 8.2 确定性保证清单

| 项 | 措施 |
|----|------|
| 浮点数学 | 全部替换为 `Fix64` 定点数 |
| Burst 优化 | `FloatMode.Strict` + `FloatPrecision.Standard` + `CompileSynchronously = true` |
| chunk 迭代顺序 | `EntityQuery.ToComponentDataArray` + 按 `Entity.Index` 显式排序 |
| 像素冲突解决 | (x, y) 字典序升序，先到先得 |
| 迭代方向 | 偶数帧正向，奇数帧反向（消除偏置） |
| 随机数 | 服务器下发种子，使用确定性 PRNG（如 xorshift64） |
| 物理 | 不用 Unity Physics（非确定），全部自定义 2D 像素碰撞 |
| 时间步长 | 固定 `dt = 1/30`，不用 `Time.deltaTime` |

### 8.3 周期调和（10 Hz）

- 每 6 逻辑帧（200ms）服务器广播"权威环境快照"
- 快照内容：关键 chunk 的像素摘要（每 16×16 区域取代表性像素 + 计数）
- 压缩：RLE + ZLinq 零分配编码
- 客户端用快照覆盖本地环境状态，玩家层不受影响
- 视觉插值平滑过渡 100ms

---

## 9. 反作弊

### 9.1 威胁模型

| 威胁 | 来源 | 缓解 |
|------|------|------|
| 输入注入 | Newman | 服务器输入验证（范围/速度/冷却） |
| 客户端模拟篡改 | Newman | Hotfix.dll 签名校验 |
| 确定性分歧攻击 | Newman | 状态哈希 60 帧比对 + 多数派裁决 |
| 未来事件预读 | Newman | 场景事件帧延迟解密 |
| 透视外挂 | Newman | 服务器不下发不可见信息（玩家位置全发，但只在加载后） |
| 自动脚本 | Newman | 输入频率统计 + 行为模式分析 |

### 9.2 MVP 反作弊实现

```csharp
// Entity/Systems/AntiCheatSystem.cs
public class AntiCheatSystem : EntitySystem
{
    // 输入验证
    public bool ValidateInput(uint playerId, InputPayload input, long frame)
    {
        var player = GetPlayer(playerId);
        var lastPos = player.Position;
        var maxMove = ArenaConfig.MaxSpeed * (Fix64)1 / 30 * (Fix64)1.5; // 1.5 倍容忍

        if (Math.Abs(input.MoveX) > 100) return false;
        if (Math.Abs(input.MoveY) > 100) return false;

        var dt = frame - player.LastInputFrame;
        if (dt < 5) return false; // 输入过频

        return true;
    }

    // 状态哈希比对
    public void CompareHashes(long frame)
    {
        var hashes = CollectHashes(frame);
        var majority = hashes.GroupBy(h => h.Hash)
            .OrderByDescending(g => g.Count())
            .First();

        foreach (var minority in hashes.Where(h => h.Hash != majority.Hash))
        {
            minority.Player.MismatchCount++;
            if (minority.Player.MismatchCount >= 3)
            {
                KickPlayer(minority.Player, "Determinism mismatch");
            }
        }
    }
}
```

### 9.3 Hotfix.dll 签名校验

```
客户端构建时:
  1. 编译 Hotfix.dll
  2. 用项目私钥签名 Hotfix.dll → 生成 signature
  3. signature 内置到 AOT 程序集

客户端连接服务器时:
  1. 服务器发送随机挑战 nonce
  2. 客户端用 Hotfix.dll + nonce 计算 HMAC
  3. 服务器验证 HMAC（公钥预共享）
  4. 不匹配 → 拒绝连接

热更场景:
  1. 新版本 Hotfix.dll 生成新签名
  2. 服务器同步更新白名单
  3. 客户端拉取新 Hotfix.dll + 新签名
```

---

## 10. 目录迁移与 Fantasy 路径修正

### 10.1 目录迁移：Client/Unity → Client

**操作**：将 `d:\Unity\Projects\Noita\Client\Unity\*` 全部上移到 `d:\Unity\Projects\Noita\Client\`

**迁移后结构**：
```
Noita/
├── Client/                          # 原 Client/Unity/ 内容
│   ├── Assets/
│   ├── HybridCLRData/
│   ├── ProjectSettings/
│   ├── Packages/
│   ├── .codegraph/
│   ├── .cursor/
│   ├── .gitignore
│   └── .vsconfig
├── Server/
├── Tools/
└── Docs/
```

**受影响文件**：
- `Tools/ProtocolExportTool/ExporterSettings.json` — 客户端生成路径
- `Docs/Noita-Dev-Agent.md` — 项目结构描述
- `fantasy_quickstart.md` — 路径引用（如有）

### 10.2 Fantasy 路径修正

**`ExporterSettings.json` 修正**：

```json
{
    "Export": {
        "NetworkProtocolDirectory": {
            "Value": "d:/Unity/Projects/Noita/Tools/NetworkProtocol",
            "Comment": "ProtoBuf文件所在的文件夹位置"
        },
        "NetworkProtocolServerDirectory": {
            "Value": "d:/Unity/Projects/Noita/Server/Entity/Generate/NetworkProtocol",
            "Comment": "ProtoBuf生成到服务端的文件夹位置"
        },
        "NetworkProtocolClientDirectory": {
            "Value": "d:/Unity/Projects/Noita/Client/Assets/Scripts/Hotfix/Generate/NetworkProtocol",
            "Comment": "ProtoBuf生成到客户端的文件夹位置"
        }
    }
}
```

**注意**：
- 使用正斜杠 `/`（Fantasy 导出工具跨平台兼容，Unity 也接受）
- 服务端生成目录 `Server/Entity/Generate/NetworkProtocol/` 需在 Entity.csproj 中加入 `<Compile Include="Generate/NetworkProtocol/**/*.cs" />`
- 客户端生成目录 `Client/Assets/Scripts/Hotfix/Generate/NetworkProtocol/` 由 Hotfix.asmdef 自动包含（asmdef 按 folder scope）

### 10.3 文档同步修正

`Docs/Noita-Dev-Agent.md` 第 24-32 行的项目结构需更新：
- `Client/Unity/` → `Client/`
- 第 122 行 `Client/Unity/Assets/Scripts/Hotfix/Generate/NetworkProtocol/` → `Client/Assets/Scripts/Hotfix/Generate/NetworkProtocol/`
- 第 201 行陷阱表中的 macOS 路径警告保留

---

## 11. 实施路线图

### Phase 1：基础设施（2 周）

- [ ] 目录迁移：`Client/Unity/*` → `Client/*`
- [ ] `ExporterSettings.json` 路径修正
- [ ] 文档同步更新
- [ ] Entity.csproj 加入 Generate 目录编译项
- [ ] 验证协议导出工具能正常生成到新路径
- [ ] 创建 `NoitaCA.Lockstep.asmdef`、`NoitaCA.Core.asmdef` 等新程序集骨架

### Phase 2：确定性基础（3 周）

- [ ] 引入 `Fix64` 定点数库（建议 `FixPoint.NET` 或自定义）
- [ ] 改造 `PixelData` 组件为定点数
- [ ] 改造 `PixelSimulation` IJobChunk：`FloatMode.Strict` + 显式 chunk 排序
- [ ] 写确定性回归测试：相同输入在 Win/Mac/Android 跑 1000 帧比对哈希
- [ ] Burst 版本锁定 + CI 跑确定性测试

### Phase 3：网络层（2 周）

- [ ] 设计 `C2G_Input` / `G2C_FrameBatch` / `G2C_SceneEvent` proto
- [ ] 服务端 `GameRoom` 实体 + 输入中继逻辑
- [ ] 客户端 `LockstepScheduler`（输入队列 + 帧推进）
- [ ] 状态哈希上报与比对
- [ ] 断线重连（snapshot + rewind）

### Phase 4：四大系统（4 周）

- [ ] 房间系统（自建房/快速匹配/排位）
- [ ] 匹配系统（MMR ELO）
- [ ] 聊天系统（Lobby/Room/QuickChat）
- [ ] 场景系统（SceneScheduler + Telegraph + 帧延迟解密）

### Phase 5：反作弊与签名（2 周）

- [ ] Hotfix.dll 签名校验
- [ ] 输入验证规则
- [ ] 状态哈希多数派裁决
- [ ] 场景事件帧延迟解密

### Phase 6：对局玩法（3 周）

- [ ] 256×256 竞技场地形生成
- [ ] 边界死亡规则
- [ ] 4 人出生点
- [ ] 道具刷新表
- [ ] 结算画面

### Phase 7：热更与资源管理（2 周）

- [ ] HybridCLR 对局中热更锁定（HotfixVersion 锁定 + Battle 状态拒绝 LoadFromStream）
- [ ] YooAsset 资源版本清单上报（`ClientVersionReport` 协议）
- [ ] 服务器版本白名单 + 灰度发布机制
- [ ] 对局开始前版本对齐校验（HotfixHash + AssetManifestHash + BurstVersion）
- [ ] 场景事件资源预加载（对局加载阶段预加载道具 prefab）
- [ ] YooAsset 加载失败兜底（占位 prefab + 错误上报）
- [ ] 热更流程 UI（"有更新，是否应用"提示）

**总工期估算**：18 周（约 4.5 个月），单人全职。

---

## 12. 验收清单

### 12.1 确定性验收
- [ ] 相同输入下，Win/Mac/Android 三平台跑 10000 帧状态哈希一致
- [ ] Burst 版本升级后回归测试通过
- [ ] 多线程与单线程模式状态哈希一致（验证排序正确）

### 12.2 网络验收
- [ ] 4 人 30Hz 对局，ping < 100ms 下无 desync
- [ ] ping 200ms 下 jitter buffer 生效，无卡顿
- [ ] 断线 30s 内重连成功，状态恢复
- [ ] 状态哈希 60 帧比对，0 误报

### 12.3 玩法验收
- [ ] 4 人乱斗从匹配到结算 < 8 分钟
- [ ] 道具 Telegraph 3 秒可见
- [ ] 边界死亡规则正确触发
- [ ] 排位赛 MMR 更新正确

### 12.4 安全验收
- [ ] Hotfix.dll 篡改 → 连接被拒
- [ ] 输入注入（超出速度/范围）→ 被服务器拒绝
- [ ] 状态哈希分歧 3 次 → 踢出房间
- [ ] 场景事件预读 → 解密失败（客户端无法提前获取明文）

### 12.5 热更与资源验收
- [ ] 对局 Battle 状态下尝试 `LoadFromStream` → 被客户端运行时拦截
- [ ] 房间内 4 人 `AssetManifestHash` 不一致 → 服务器拒绝开始对局
- [ ] `HotfixHash` 不在白名单 → 客户端被踢出匹配队列
- [ ] 新版本 Hotfix.dll 灰度发布：5% 用户拿到新版本，匹配优先同版本
- [ ] 对局加载阶段预加载道具 prefab 完成（< 3s）
- [ ] YooAsset 加载失败 → 占位 prefab 显示，对局不中断
- [ ] Burst 版本不一致 → 对局开始前拒绝（防 desync）

---

## 13. 风险登记

| 风险 | 概率 | 影响 | 缓解 |
|------|------|------|------|
| Burst 跨版本确定性变化 | 中 | 高（已发布版本 desync） | 锁定 Burst 版本 + CI 回归测试 + 对局前 BurstVersion 校验 |
| HybridCLR 升级破坏签名机制 | 中 | 中 | 升级前安全审查 + 兼容性测试 |
| 定点数精度不足导致玩法异常 | 低 | 中 | 早期原型验证 + 精度可调 |
| 4 人带宽过高 | 低 | 低 | MemoryPack 压缩 + 输入差分编码 |
| 玩家扩展到 8+ 人 | 中 | 高 | 重新评估拓扑（AOI 分片） |
| 灰度发布期间匹配池分裂 | 中 | 中 | 灰度比例动态调整 + 跨版本兼容组 |
| YooAsset CDN 故障 | 低 | 高 | 本地缓存兜底 + 重试机制 + 对局前资源完整性校验 |
| 对局中客户端拉到新版本未应用 | 中 | 低 | 设计预期行为，对局结束才热更（已锁定） |

---

## 14. 引用

- Glenn Fiedler, *Gaffer on Games: Deterministic Lockstep*
- Unity Technologies, *Burst Compiler Documentation: FloatMode and FloatPrecision*
- qq362946, *Fantasy.Unity Documentation*
- Petri Purho, *Noita GDC Talk: Exploring the Tech Behind Noita*
- Marty Cagan, *Inspired: How to Create Tech Products Customers Love*
- Garry Newman, *Facepunch Blog: Dealing with Cheaters in Rust*

---

**文档结束**
