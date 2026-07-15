# NoitaCA DOTS 重构设计文档

## 1. 概述

将 NoitaCA 落沙元胞自动机从 MonoBehaviour + 托管数组重构为 DOTS ECS 多线程架构，引入 ZLinq 零分配查询、ZString 零分配字符串、UniTask 异步编排。

**目标 Unity**：6000.3.19f1 | **目标后端**：IL2CPP（全平台）

---

## 2. DOTS 包配置

### 需添加的包（manifest.json）

| 包名 | 版本 | 用途 |
|---|---|---|
| `com.unity.feature.ecs` | 1.0.0 | ECS 元包（entities + entities.graphics + physics） |
| `com.unity.entities` | 1.4.7 | ECS 核心框架 |
| `com.unity.entities.graphics` | 1.4.20 | ECS→URP 渲染桥接 |
| `com.unity.physics` | 1.4.6 | DOTS 物理（可选，预留） |
| `com.unity.burst` | 1.8.29 | Burst 编译器（Jobs 加速） |
| `com.unity.collections` | 2.6.7 | NativeArray/NativeList 容器 |
| `com.unity.mathematics` | 1.3.2 | math.float2/int2 数学库 |
| `com.unity.serialization` | 3.1.3 | 二进制/JSON 序列化 |

### 程序集定义（新建）

采用 HybridCLR 热更新架构，整体收敛为 **AOT + Hotfix 两个程序集**，命名空间与程序集一一对应：

```
Assets/Scripts/
├── AOT/
│   └── AOT.asmdef        # 命名空间 AOT  —— 确定性 DOTS 核心 + 共享数据 + 渲染桥接
└── Hotfix/
    └── Hotfix.asmdef     # 命名空间 Hotfix —— 可热更的玩法/内容/UI/元数据
```

- **AOT 只保留一个程序集**：不按 Core / Simulation / Renderer / Gameplay 过度拆分子程序集。
  AOT 承载所有确定性、需 Burst 编译、不可热更的 DOTS 核心：像素组件、网格元数据、
  模拟配置、Movement / Interaction / Creature / Spell / RenderBridge 等 ISystem、MaterialDatabase。
- **Hotfix 即单个 Hotfix 程序集**：承载可热更新的玩法与内容（法术/材料反应定义表、生物/装备配置、
  玩家输入映射、UI 流程、元数据与埋点），直接作为 Hotfix.dll 由 HybridCLR 运行时加载。
- **依赖方向单向**：`Hotfix` 引用 `AOT`；`AOT` 严禁反向引用 `Hotfix`，否则破坏热更新隔离。
- 编辑器工具（StressTest 覆盖层等）保留为独立 Editor 程序集，不进 AOT / Hotfix 主链路。

---

## 2.1 命名空间与程序集示例

### 2.1.1 AOT.asmdef

```json
{
    "name": "AOT",
    "rootNamespace": "AOT",
    "references": [
        "Unity.Entities",
        "Unity.Entities.Graphics",
        "Unity.Burst",
        "Unity.Collections",
        "Unity.Mathematics",
        "Unity.Serialization",
        "Unity.Physics"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": true,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### 2.1.2 Hotfix.asmdef

```json
{
    "name": "Hotfix",
    "rootNamespace": "Hotfix",
    "references": [
        "AOT",
        "Unity.Entities",
        "Unity.Mathematics",
        "YooAsset"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": true,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> Hotfix.asmdef 须登记到 HybridCLR 的 `hotfixAssembly` 列表，并由构建流程产出 `Hotfix.dll`；
> AOT.asmdef 随主包 IL2CPP 编译进二进制，不参与热更。

### 2.1.3 命名空间示例

AOT 核心组件（`namespace AOT;`）：

```csharp
// AOT/Core/Components/PixelData.cs
namespace AOT;

public struct PixelData : IComponentData
{
    public byte MaterialType;
    public byte Flags;
    // ... 其余字段同 §4.1
}
```

Hotfix 可热更定义表（`namespace Hotfix;`）：

```csharp
// Hotfix/Gameplay/Definitions/SpellDefinitions.cs
namespace Hotfix;

// 法术 / 材料反应定义表：可热更，不进入确定性热路径
public static class SpellDefinitions
{
    // 数据驱动的法术参数与材料反应规则在此配置
}
```

**约定**：AOT 目录下所有文件统一 `namespace AOT;`，Hotfix 目录下所有文件统一 `namespace Hotfix;`，
与各自 .asmdef 的 `rootNamespace` 保持一致，避免命名空间与程序集错位。

---

## 3. 架构总览

```
┌─────────────────────────────────────────────────────┐
│                  MonoBehaviour 层                     │
│  PixelWorldBootstrap  InputController  SpellCtrl    │
│  PlayerController     CreatureAI       Equipment    │
│  UniTask 异步加载     ZString 日志     ZLinq 查询   │
└──────────────────────┬──────────────────────────────┘
                       │ GetEntityQuery / EntityManager
┌──────────────────────▼──────────────────────────────┐
│                   DOTS ECS 层                        │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────┐ │
│  │ PixelData   │  │ MovementISys │  │InteractISys│ │
│  │ (chunk)     │→ │ (IJobChunk)  │→ │(IJobChunk) │ │
│  └─────────────┘  └──────────────┘  └────────────┘ │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────┐ │
│  │ DirtyChunk  │  │ CreatureISys │  │ SpellISys  │ │
│  │ Tracker     │  │ (IJobChunk)  │  │(IJobChunk) │ │
│  └─────────────┘  └──────────────┘  └────────────┘ │
└──────────────────────┬──────────────────────────────┘
                       │ EntitiesGraphicsSystem
┌──────────────────────▼──────────────────────────────┐
│                  渲染桥接层                           │
│  PixelWorldRenderer（读取 chunk → Texture2D）        │
│  SpriteRenderer 显示  Camera 跟随                    │
└─────────────────────────────────────────────────────┘
```

---

## 4. 数据模型 — DOTS 组件

### 4.1 核心像素组件

```csharp
// AOT/Core/Components/PixelData.cs
namespace AOT;

public struct PixelData : IComponentData
{
    public byte MaterialType;      // MaterialType 枚举（12 种材质）
    public short Temperature;      // 温度（定点：°C × 100，确定性，禁用 float）
    public short Lifetime;         // 生命周期（定点：秒 × 100，确定性，禁用 float）
    public byte Density;           // 气体/液体/粉末排序用
    public byte FallingFrames;     // 重力累积帧数
    public ushort Color;           // 打包 RGB565，提升缓存效率
    public byte Flags;             // 位标志：Bit0=生物, Bit1=法术, Bit2=存活, Bit3=已更新
    public short VelocityX;        // 定点数 ×100
    public short VelocityY;
}
```

**为什么用短整型定点而非 float？** 落沙模拟必须确定性（帧同步 30Hz），所有世界状态字段禁用 `float`。温度/生命周期用 `short` 存「实际值 × 100」定点，速度 `VelocityX/Y` 同理（见下方字段）。原 `struct Pixel` 占 48+ 字节，12 种材质类型用 1 字节、密度 1 字节、颜色 RGB565 用 2 字节、温度/寿命用 `short`，压缩到 **14 字节/像素**，IJobChunk 缓存命中率提升约 3 倍。

### 4.2 网格元数据组件

```csharp
public struct GridSize : IComponentData
{
    public int Width;
    public int Height;
}

public struct ChunkDirtyFlag : IComponentData
{
    public byte IsDirty;          // 1 = 需要重新模拟
    public byte SleepFrames;      // 倒计时，归零后重新检查
}

// 共享组件，用于 chunk 空间分组
public struct ChunkPosition : ISharedComponentData
{
    public int ChunkX;
    public int ChunkY;
}
```

### 4.3 模拟配置组件

```csharp
public struct SimulationConfig : IComponentData
{
    public int SimulationMode;    // 0=全扫描, 1=活跃像素, 2=分块
    public int ProcessingBudget;  // 每步最大处理像素数
    public short AmbientTemperature; // 环境温度（定点：°C × 100，确定性，禁用 float）
}
```

---

## 5. 系统设计

### 5.1 移动系统（ISystem + IJobChunk）

**替代**：`MovementSystem.cs`（MonoBehaviour，约 200 行）

```csharp
// AOT/Simulation/MovementSystem.cs
namespace AOT;

[BurstCompile]
public partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 首轮单线程，后续按行带并行
        var job = new MovementJob
        {
            PixelLookup = SystemAPI.GetComponentLookup<PixelData>(true),
            GridSize = gridEntity.Get<GridSize>(),
        };
        
        // IJobChunk 按 64 像素 chunk 并行处理
        state.Dependency = job.ScheduleByChunk(
            SystemAPI.GetChunkIterator<PixelData>(), state.Dependency);
    }
}

[BurstCompile]
public struct MovementJob : IJobChunk
{
    public ComponentDataFromEntity<PixelData> PixelLookup;
    
    public void Execute(ArchetypeChunk chunk, int chunkIndex, ...)
    {
        // 粉末：重力 + 横向扩散（随机扫描方向）
        // 液体：水平搜索 + 位移置换
        // 气体：基于密度上升
        // 使用 NativeArray 直接访问 chunk 数据（零拷贝）
    }
}
```

**线程策略**：
- 按行带划分：每个作业处理一个垂直条带
- 通过 `ScheduleByChunk` 使用 4-8 个工作线程
- 随机扫描方向使用 Burst 兼容 RNG（不使用 System.Random）

### 5.2 交互系统（ISystem + IJobChunk）

**替代**：`InteractionSystem.cs`（约 150 行）

```csharp
// AOT/Simulation/InteractionSystem.cs
namespace AOT;

[BurstCompile]
public partial struct InteractionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var job = new InteractionJob
        {
            PixelLookup = SystemAPI.GetComponentLookup<PixelData>(true),
            MaterialDefs = materialDefinitions, // NativeArray<MaterialDefinition>
            AmbientTemp = config.AmbientTemperature,
        };
        state.Dependency = job.ScheduleByChunk(
            SystemAPI.GetChunkIterator<PixelData>(), state.Dependency);
    }
}
```

**与 MonoBehaviour 版本的关键差异**：
- `System.Random` → `Unity.Mathematics.Random`（Burst 兼容）
- `PixelGrid.GetNeighbors()` → 直接 NativeArray 索引
- 温度/生命周期变更写入输出 NativeArray（双缓冲）

### 5.3 生物 AI 系统（ISystem + IJobChunk）

**替代**：`PixelCreature.cs` MonoBehaviour AI 逻辑

```csharp
// AOT/Simulation/CreatureAISystem.cs
namespace AOT;

[BurstCompile]
public partial struct CreatureAISystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 并行：每个生物独立 job chunk
        // 游走/追击/跳跃逻辑在工作线程执行
        // 网格写入批量处理，避免竞争条件
    }
}
```

### 5.4 法术系统（ISystem）

**替代**：`SpellController.cs` MonoBehaviour 施法逻辑

```csharp
// AOT/Simulation/SpellSystem.cs
namespace AOT;

[BurstCompile]
public partial struct SpellSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 弹道移动：IJobChunk 并行
        // 光束/锥形：单线程（短暂执行）
        // 范围效果：并行网格写入
    }
}
```

### 5.5 渲染桥接系统（ISystem，模拟后运行）

**替代**：`PixelWorldRenderer.cs` 的部分逻辑

```csharp
// AOT/Renderer/RenderBridgeSystem.cs
namespace AOT;

public partial struct RenderBridgeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 作业：读取 PixelData chunk → 写入 NativeArray<Color32>
        // 主线程：NativeArray → Texture2D.SetPixels32()
        // 这是唯一的主线程同步点
    }
}
```

---

## 6. ZLinq 集成

### 6.1 使用场景

> **AGENTS.md 全局禁令**：禁止 lambda 表达式与匿名委托。ZLinq 的谓词/选择器一律用本地函数（方法组）传入，下表仅展示方法组形式，本地函数定义略。

| 场景 | ZLinq 用法（本地函数，禁 lambda） |
|---|---|
| 查询活跃像素 | `pixelData.Where(IsAlive).Select(GetPosition)` |
| 生物注册表查找 | `creatures.Where(HpPositive).OrderBy(ByDistanceToPlayer)` |
| 法术目标选择 | `enemies.InCircle(center, radius).MinBy(ByDistance)` |
| 装备背包 | `inventory.Where(IsEquipped).Select(GetAbility)` |
| 调试统计 | `stats.Where(IsChunkMode).Average(GetSimMs)` |

### 6.2 零分配模式

```csharp
// 之前（每帧分配 GC，且使用 lambda 违反 AGENTS.md 禁令）
var active = pixels.Where(p => p.IsActive).ToList();

// 之后（零分配；按 AGENTS.md 全局禁用 lambda，谓词用本地函数替代）
bool IsActive(PixelData pixel) => pixel.IsAlive();
foreach (var pixel in pixels.AsRef().Where(IsActive))
{
    ProcessPixel(pixel);
}
```

---

## 7. ZString 集成

### 7.1 调试日志

```csharp
// 之前
Debug.Log($"Simulation step {step} took {ms}ms, active={activeCount}");

// 之后（零分配）
Debug.Log(ZString.Format("Simulation step {0} took {1:F2}ms, active={2}", step, ms, activeCount));
```

### 7.2 IMGUI 覆盖层（StressTestDebugOverlay、StressTestPerformancePanel）

```csharp
// 之前
GUI.Label(new Rect(10, 10, 300, 20), $"FPS: {fps:F1} | Pixels: {totalPixels}");

// 之后
using var sb = ZString.CreateStringBuilder();
sb.AppendFormat("FPS: {0:F1} | Pixels: {1}", fps, totalPixels);
GUI.Label(rect, sb.ToString());
```

### 7.3 生物/装备命名

```csharp
// 运行时生物命名
creature.DisplayName = ZString.Format("爬行者 Lv.{0}", level);
```

---

## 8. UniTask 集成

### 8.1 异步世界生成

```csharp
// 之前（协程）
IEnumerator GenerateWorld()
{
    yield return null;
    // ... 区块生成
}

// 之后
async UniTaskVoid GenerateWorldAsync(CancellationToken ct)
{
    await TaskUtility.SwitchToThreadPool();
    // ... 工作线程并行区块生成
    await TaskUtility.SwitchToMainThread();
    // ... 应用到 ECS
}
```

### 8.2 资源加载

```csharp
// YooAsset 异步加载
var handle = YooAssets.LoadAssetAsync<TextAsset>("MaterialDatabase");
await handle.ToUniTask(cancellationToken: ct);
```

### 8.3 法术施放流程

```csharp
// 法术施放异步管线
async UniTask CastSpellAsync(SpellElement element, CancellationToken ct)
{
    await PlayCastAnimation(element, ct);
    await WaitForProjectileImpact(ct);
    await ApplyAreaEffect(impactPoint, ct);
}
```

---

## 9. 重构阶段

### 阶段一：基础搭建（第 1-3 天）
- [ ] 添加 DOTS 包到 manifest.json
- [ ] 创建程序集定义（AOT.asmdef + Hotfix.asmdef，见 §2 / §2.1）
- [ ] 定义 PixelData、GridSize、SimulationConfig 组件
- [ ] 创建 WorldChunkEntity 工厂（PixelGrid → ECS 实体转换）
- [ ] 保留现有 MonoBehaviour 渲染作为回退方案

### 阶段二：核心模拟（第 4-7 天）
- [ ] 实现 MovementSystem（ISystem + IJobChunk）
- [ ] 实现 InteractionSystem（ISystem + IJobChunk）
- [ ] PixelData 双缓冲（current/next NativeArray）
- [ ] 脏区块追踪迁移到 ECS 共享组件
- [ ] 验证：同时运行新旧系统，对比输出结果

### 阶段三：渲染桥接（第 8-9 天）
- [ ] RenderBridgeSystem：ECS chunk → Texture2D
- [ ] PixelWorldRenderer 改为从 ECS 读取（替代 PixelGrid）
- [ ] 渲染作业添加 Burst 编译

### 阶段四：玩法系统（第 10-13 天）
- [ ] PlayerController 转换：MonoBehaviour 外壳 + ECS 查询
- [ ] SpellController + PixelAbility → SpellSystem
- [ ] PixelCreature → CreatureAISystem
- [ ] PixelEquipmentPickup → EquipmentSystem
- [ ] UniTask 异步加载流程

### 阶段五：打磨 + ZLinq/ZString（第 14-15 天）
- [ ] 所有 Debug.Log 替换为 ZString
- [ ] 热路径中 List/Where/Select 替换为 ZLinq
- [ ] StressTest 覆盖层使用 ZString StringBuilder
- [ ] 性能基准测试：新旧对比

### 阶段六：清理（第 16 天）
- [ ] 移除过时的 MonoBehaviour 存根（SimulationSystem、PixelCell）
- [ ] 移除 WorldGrid.cs、PixelCell.cs
- [ ] 更新文档

---

## 10. 风险评估

| 风险 | 应对措施 |
|---|---|
| entities.graphics 与 URP 版本冲突 | 先测试导入；可能需要 entities.graphics 1.4.8+ 或手动程序集引用 |
| IJobChunk 像素写入竞争条件 | 双缓冲 + 每 chunk 原子写入模式 |
| Burst 无法使用托管类型 | 所有热路径数据放 NativeArray；MonoBehaviour 外壳调用 Unity API |
| 调试困难 | 保留 MonoBehaviour 调试覆盖层；用 ZString 高效日志 |
| IL2CPP 构建时间 | Burst 编译增加时间；通过增量构建缓解 |

---

## 11. 文件迁移映射

| 旧文件（MonoBehaviour） | 新文件（DOTS） | 程序集 | 阶段 |
|---|---|---|---|
| `Simulation/Pixel.cs` | `AOT/Core/Components/PixelData.cs` | AOT | 1 |
| `Simulation/PixelGrid.cs` | `AOT/Core/WorldChunkEntity.cs` | AOT | 1 |
| `Simulation/PixelSimulation.cs` | `AOT/Simulation/SimulationTickSystem.cs` | AOT | 2 |
| `Simulation/MovementSystem.cs` | `AOT/Simulation/MovementSystem.cs`（ISystem） | AOT | 2 |
| `Simulation/InteractionSystem.cs` | `AOT/Simulation/InteractionSystem.cs`（ISystem） | AOT | 2 |
| `Simulation/MaterialDatabase.cs` | `AOT/Core/MaterialDatabase.cs`（NativeArray） | AOT | 1 |
| `PixelWorldRenderer.cs` | `AOT/Renderer/RenderBridgeSystem.cs` | AOT | 3 |
| `PlayerController.cs` | `AOT/Gameplay/PlayerSystem.cs`（ISystem）+ MonoBehaviour 外壳 | AOT | 4 |
| `SpellController.cs` | `AOT/Gameplay/SpellSystem.cs`（ISystem） | AOT | 4 |
| `Creatures/PixelCreature.cs` | `AOT/Gameplay/CreatureAISystem.cs`（ISystem） | AOT | 4 |
| `Equipment/*.cs` | `AOT/Gameplay/EquipmentSystem.cs`（ISystem） | AOT | 4 |
| （新增）法术 / 材料反应定义表 | `Hotfix/Gameplay/Definitions/SpellDefinitions.cs` | Hotfix | 4 |
| （新增）生物 / 装备配置表 | `Hotfix/Gameplay/Definitions/ContentTables.cs` | Hotfix | 4 |
| `Demo/StressTestDebugOverlay.cs` | `Editor/StressTestOverlay.cs`（ZString） | Editor | 5 |
| `Demo/StressTestPerformancePanel.cs` | `Editor/StressTestPanel.cs`（ZString） | Editor | 5 |

> **程序集归属原则**：所有确定性、需 Burst 编译、不可热更的 DOTS 核心（组件 / ISystem / 共享数据 / 渲染桥接）一律放 **AOT**；法术 / 材料反应 / 生物 / 装备等**可热更的定义表与配置**放 **Hotfix**；纯编辑器工具放独立 Editor 程序集。

---

## 12. 验收标准

- [ ] MovementSystem + InteractionSystem 在 IJobChunk 中运行（4+ 线程）
- [ ] PixelWorldRenderer 从 ECS 读取（热路径无托管 PixelGrid）
- [ ] Debug.Log 零分配（全部使用 ZString）
- [ ] ZLinq 用于生物/装备查询（零 LINQ 分配）
- [ ] UniTask 用于所有异步流程（无协程）
- [ ] 所有系统 Burst 编译
- [ ] IL2CPP 构建成功
- [ ] 视觉输出与原版一致（像素级匹配）
- [ ] 8 核机器模拟吞吐量提升 2 倍以上
