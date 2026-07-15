# NoitaCA 帧同步 Netcode 细化设计文档

> **文档版本**：v1.0
> **创建日期**：2026-07-06
> **定位**：`Docs/多人联机帧同步对战设计.md` v1.0 的 netcode 细化补充
> **状态**：待评审
> **代码示例约定**：本文档所有 C# 代码块均为**伪代码，仅作设计参考**，不可直接编译

---

## 1. 概述

### 1.1 文档定位

本文档是已有 [多人联机帧同步对战设计.md](./多人联机帧同步对战设计.md) 的**深化补充**，专注 netcode 实现细节。已有文档定义了总体架构、协议骨架、四大系统；本文档回答"如何实现"。

### 1.2 与已有文档的关系

```
已有文档（v1.0）                  本文档（深化）
├── §3 总体架构                 →  引用，不重复
├── §4 客户端设计               →  §2/§3/§4 深化回滚与预测
├── §5 服务端设计               →  §5/§6 深化重连与配套
├── §6 协议设计（基础消息）     →  §7 追加重连/时钟/观战消息
├── §8 确定性与多线程           →  §2 引用周期调和，深化快照单元
├── §9 反作弊                   →  引用，不重复
└── §11 实施路线图              →  §11 细化优先级
```

| 本文档章节 | 类型 | 已有文档对应 |
|-----------|------|-------------|
| §2 分层回滚模型 | **新增** | 已有 §4.2.3 仅提及"周期调和"，未展开 |
| §3 用户输入预测 | **新增** | 已有文档无 |
| §4 帧回滚机制 | **新增** | 已有 §11 Phase 3 仅列"snapshot + rewind"待办 |
| §5 断线重连处理 | **深化** | 已有 §1.2 仅"30s 重连"目标 |
| §6 配套机制 | **新增** | 已有文档无 |
| §7 协议设计补充 | **追加** | 已有 §6 基础消息保留 |
| §8 数据结构总览 | **新增** | 已有文档无 |

### 1.3 设计目标

| 指标 | 目标值 | 验收方法 |
|------|--------|----------|
| tick rate | 30 Hz 逻辑 / 60 Hz 渲染 | 帧率统计 |
| 玩家数 | 2-8 人（可配置，默认 4） | 对局实测（N=2/4/8 三档） |
| 延迟容忍 | ping < 100ms 无 desync | 4 人局域网测试（N=8 最坏情况验证） |
| 抖动吸收 | ping 200ms ± 50ms 抖动可吸收 | 限速模拟测试 |
| 重连窗口 | 30 秒 | 断线后计时重连 |
| 状态量 | 玩家层 2.5KB/帧，环境层 1MB/帧 | 内存采样 |
| 回滚开销 | < 5ms/帧（7 帧回滚） | profiler 采样 |

### 1.4 关键决策（用户已确认）

| 决策项 | 选定方案 | 依据 |
|--------|---------|------|
| 断线期间对局行为 | 继续对局 + 重连快照恢复 | 用户决策 |
| 玩家层回滚 | 轻量级 GGPO 风格回滚（玩家层 N 玩家×64B+法术/生物/道具 ≈ 1.5KB~5KB/帧，N=RoomConfig.PlayerCount ∈ [2,8]） | 用户决策 |
| 环境层回滚 | 不回滚，周期调和替代（1MB/帧成本不可承受） | 用户决策 |
| 配套机制范围 | 时钟同步 + Jitter Buffer + Catch-up + 观战延迟全包 | 用户决策 |

---

## 2. 分层回滚模型（核心）

### 2.1 分层总览

```
┌─────────────────────────────────────────────────────────────┐
│  静态层（地形基底）                                           │
│  ├── 同步：对局开始全量下发 + 场景事件增量                    │
│  └── 回滚：不回滚（只读）                                     │
├─────────────────────────────────────────────────────────────┤
│  环境层（CA 流动像素：水/火/烟/沙）                           │
│  ├── 同步：周期调和（每 6 帧 = 200ms 服务端权威快照）         │
│  ├── 回滚：不回滚（状态量 1MB/帧，重模拟成本不可承受）         │
│  └── 失配修正：哈希失配时下发像素数据，客户端覆盖             │
├─────────────────────────────────────────────────────────────┤
│  玩家层（角色/法术/生物/道具）                                 │
│  ├── 同步：每帧输入锁步（30Hz）                               │
│  ├── 回滚：GGPO 风格回滚（INPUT_DELAY=2, MAX_ROLLBACK=7）    │
│  └── 状态量：N 玩家 × 64B（N=2..8，128B~512B）+ 法术/生物/道具 ≈ 1.5KB~5KB/帧  │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 环境层周期调和机制（5 Hz）

#### 2.2.1 快照单元设计

快照单元与 [PixelGrid.cs](../Client/Assets/Scripts/AOT/Simulation/PixelGrid.cs) 的 `changedChunks` 对齐，使用 **Per-Region 16×16 chunk**（与 `ChunkSize = 16` 一致）。

> 注：`PixelGrid.changedChunks` 当前为 `private bool[,]` 无公共访问器，无法直接读取 dirty 状态。需在 PixelGrid DOTS 重构时暴露 `IsChunkDirty(cx, cy)` 公共 API，供客户端哈希计算时仅遍历 dirty chunk。
>
> ⚠ **B3 阻塞依赖（确定性 CI 卡点）**：`IsChunkDirty(cx, cy)`（连同 §4.1.2 的 `ForEachCellInChunk` / `GetCell`）是 B3 门禁（万帧三平台哈希一致 + GC=0）的**前置阻塞项**。PixelGrid DOTS 重构未暴露这些公共读取 API 前，环境层 `MurmurHash3` 无法在真实像素数据上计算，B3 真机校验无法闭环。→ **须优先推进 PixelGrid DOTS 重构的「公共读取 API」子项**（详见《测试QA计划.md》B3 门禁）。

`RegionSnapshot` 结构统一定义见 [§4.1.2](#412-环境层区域快照)（含 `ChunkX` / `ChunkY` / `MurmurHash3` / `Frame` / `PixelBytes` 字段），本节不重复。

#### 2.2.2 哈希比对与多数派裁决

每 6 帧（200ms = 5Hz）执行（服务端不持有 PixelData，不跑像素模拟，详见 v1.0 §5.3）：
1. 各客户端本地计算 dirty chunk 的 `MurmurHash3`，上报 `C2G_RegionHashReport`（仅 dirty chunk 哈希）
2. 服务端比对各客户端上报的哈希，按多数派裁决（4 玩家取 ≥2 票一致的哈希为权威）
3. 服务端将权威哈希广播给所有客户端（`G2C_RegionHashBroadcast`）
4. 客户端比对本地哈希与权威哈希，失配 chunk 上报 `C2G_RegionHashMismatch`
5. 服务端从权威客户端（多数派一方）获取该 chunk 的像素数据（NativeArray<PixelData>），序列化为 PixelBytes 下发（4KB/chunk，RLE 压缩后约 1KB）

#### 2.2.3 客户端覆盖策略

```csharp
// 伪代码，仅作设计参考
public void ApplyRegionSnapshot(RegionSnapshot snapshot)
{
    // 1. 保存当前像素用于视觉插值
    NativeArray<PixelData> oldPixels = CaptureChunk(snapshot.ChunkX, snapshot.ChunkY);
    
    // 2. 用服务端权威数据覆盖
    // WriteChunk 内部用 MemoryPack 将 byte[] 反序列化为 NativeArray<PixelData>
    WriteChunk(snapshot.ChunkX, snapshot.ChunkY, snapshot.PixelBytes);
    
    // 3. 视觉插值平滑 100ms（避免像素突变）
    //    通过 PixelWorldRenderer 的插值缓冲区实现
    RenderBridge.SetInterpolation(snapshot.ChunkX, snapshot.ChunkY, 
                                   oldPixels, durationMs: 100);
}
```

#### 2.2.4 带宽估算

| 场景 | chunk 数 | 单 chunk 体积 | 总带宽/秒 |
|------|---------|--------------|-----------|
| 哈希一致（仅摘要） | 256 chunk × 8B | 8B | 256×8×5 = 10 KB/s |
| 5% chunk 失配 | 12 chunk × 1KB(RLE) | 1KB | 12×1×5 = 60 KB/s |
| 30% chunk 失配（剧烈战斗） | 76 chunk × 1KB | 1KB | 76×1×5 = 380 KB/s |

> **结论**：常规 10KB/s，剧烈战斗峰值 380KB/s（30% chunk 失配）。注：经 RLE 进一步压缩后可降至约 200KB/s。失配率 > 50% 触发全量快照重置。

### 2.3 玩家层 GGPO 风格回滚

#### 2.3.1 回滚范围

| 数据类型 | 是否回滚 | 单实例体积 | 数量 | 总量 |
|---------|---------|-----------|------|------|
| PlayerData | ✅ | 64B | 4 | 256B |
| SpellEntity | ✅ | 48B | ≤8 | 384B |
| CreatureData | ✅ | 56B | ≤16 | 896B |
| ItemEntity | ✅ | 32B | ≤32 | 1KB |
| PixelData（环境层） | ❌ | 16B | 65536 | 1MB（不回滚） |

**玩家层单帧状态量 ≈ 2.5KB**，7 帧快照 = 17.5KB，memcpy 可承受。

#### 2.3.2 回滚参数

| 参数 | 值 | 依据 |
|------|-----|------|
| `INPUT_DELAY` | 2 帧（66ms） | GGPO 推荐值，平衡延迟与回滚频率 |
| `MAX_ROLLBACK` | 7 帧（233ms） | 覆盖 200ms 抖动 + 1 帧处理余量 |
| `SNAPSHOT_BUFFER` | 8 | MAX_ROLLBACK + 1（当前帧） |
| `SNAPSHOT_INTERVAL` | 1 帧（每帧存） | 玩家层状态小，全量快照成本可忽略 |

#### 2.3.3 回滚核心数据结构

```csharp
// 伪代码，仅作设计参考
// 玩家层全量快照
public sealed class PlayerLayerSnapshot
{
    public long Frame;                    // 快照对应帧号
    public PlayerData[] Players;          // N 玩家（RoomConfig.PlayerCount）
    public List<SpellEntity> Spells;     // 活跃法术
    public List<CreatureData> Creatures;  // 活跃生物
    public List<ItemEntity> Items;        // 地面道具
    
    public PlayerLayerSnapshot Clone()
    {
        // 全量深拷贝（状态小，memcpy 高效）
        return new PlayerLayerSnapshot
        {
            Frame = Frame,
            Players = (PlayerData[])Players.Clone(),
            Spells = new List<SpellEntity>(Spells),
            Creatures = new List<CreatureData>(Creatures),
            Items = new List<ItemEntity>(Items)
        };
    }
}
```

> ⚠ **GC 风险（与 B3 GC=0 门禁冲突）**：上例为设计参考伪代码，使用 `class` + `List<T>` + `Clone()` 深拷贝，每帧回滚会触发托管堆分配（`List` 扩容 / 数组 `new`），与 B3「万帧 GC=0」门禁直接冲突。真实实现须改为 `struct PlayerLayerSnapshot` + `NativeArray<T>`（或 `UnsafeList<T>`）+ Burst 编译的零分配方案，`Clone()` 改为 `NativeArray.Copy` / `memcpy`，热路径禁止任何 `new` / `List`。

---

## 3. 用户输入预测（核心）

### 3.1 本地预测机制

| 角色 | 预测策略 | 延迟 |
|------|---------|------|
| 本地玩家 | 本地输入立即应用 | 0（零延迟响应） |
| 远端玩家 | 用上一帧输入乐观填充 | 1 帧预测误差可接受 |
| 服务端 | 等待所有玩家输入再中继 FrameBatch | INPUT_DELAY=2 帧 |

### 3.2 输入延迟缓冲实现

```csharp
// 伪代码，仅作设计参考
// 输入环形缓冲（每玩家一个）
public sealed class InputRingBuffer<T>
{
    private readonly T[] _buffer;
    private readonly long[] _frameTags;   // 每个槽对应的帧号（参考 §4.2 SnapshotRingBuffer）
    private readonly int _capacity;
    
    public InputRingBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new T[capacity];
        _frameTags = new long[capacity];
        for (int i = 0; i < capacity; i++) _frameTags[i] = -1;   // 初始化为 -1（未写入）
    }
    
    // 写入第 frame 帧的输入
    public void Write(long frame, T input)
    {
        int idx = (int)(frame % _capacity);
        _buffer[idx] = input;
        _frameTags[idx] = frame;
    }
    
    // 读取第 frame 帧的输入，不存在返回 default
    public T Read(long frame)
    {
        int idx = (int)(frame % _capacity);
        if (_frameTags[idx] != frame) return default;
        return _buffer[idx];
    }
    
    public bool HasInput(long frame)
    {
        // 用于检测远端输入是否已到达（用 frameTags 验证，避免合法 default 输入被误判为"无输入"）
        int idx = (int)(frame % _capacity);
        return _frameTags[idx] == frame;
    }
}
```

### 3.3 客户端 TickFrame 流程

```csharp
// 伪代码，仅作设计参考
public sealed class ClientLockstepScheduler
{
    private const int INPUT_DELAY = 2;
    private const int MAX_ROLLBACK = 7;
    
    private readonly InputRingBuffer<InputPayload> _localInputs;
    private readonly InputRingBuffer<InputPayload>[] _remoteInputs; // N 玩家（RoomConfig.PlayerCount）
    private readonly SnapshotRingBuffer<PlayerLayerSnapshot> _snapshots;
    private readonly RollbackContext _rollback;
    
    public long LocalFrame;     // 客户端已渲染帧
    public long ConfirmedFrame; // 服务端已确认帧（最新 G2C_FrameBatch.Frame）
    
    // 每个 tick 调用一次（30Hz）
    public void TickFrame()
    {
        // 1. 收集本地输入（立即应用，但要存入 INPUT_DELAY 后的帧槽）
        InputPayload localInput = InputController.Capture();
        long targetFrame = LocalFrame + INPUT_DELAY;
        _localInputs.Write(targetFrame, localInput);
        
        // 2. 远端玩家：用上一帧输入乐观预测
        for (int i = 0; i < 4; i++)
        {
            if (i == LocalPlayerId) continue;
            if (!_remoteInputs[i].HasInput(targetFrame))
            {
                InputPayload predicted = _remoteInputs[i].Read(targetFrame - 1);
                _remoteInputs[i].Write(targetFrame, predicted);
            }
        }
        
        // 3. 保存回滚快照（在应用输入前）
        _snapshots.Save(LocalFrame, CapturePlayerLayerSnapshot());
        
        // 4. 应用所有玩家输入并推进模拟
        ApplyInputsAndStep(LocalFrame);
        LocalFrame++;
    }
    
    // 收到服务端帧批次时调用
    public void OnFrameBatch(G2C_FrameBatch batch)
    {
        // 1. 检测预测失配
        if (batch.Frame <= ConfirmedFrame) return; // 旧包丢弃
        
        long mismatchFrame = DetectMismatch(batch);
        if (mismatchFrame < 0)
        {
            // 无失配，更新确认帧
            ConfirmedFrame = batch.Frame;
            return;
        }
        
        // 2. 失配触发回滚
        _rollback.RollbackTo(mismatchFrame, _snapshots);
        
        // 3. 用服务端权威输入覆盖（按 PlayerId 索引，跳过本地玩家；batch 仅含单帧输入，使用 batch.Frame）
        foreach (var pi in batch.Inputs)
        {
            if (pi.PlayerId == LocalPlayerId) continue;
            _remoteInputs[pi.PlayerId].Write(batch.Frame, pi.Payload);
        }
        
        // 4. 重模拟到当前帧
        _rollback.ReplayForward(mismatchFrame, LocalFrame, this);
        
        ConfirmedFrame = batch.Frame;
    }
    
    // 检测预测失配，返回失配帧号，无失配返回 -1
    private long DetectMismatch(G2C_FrameBatch batch)
    {
        // 遍历 4 个玩家槽位（而非 batch.Inputs，后者可能不含全部玩家）
        for (long f = ConfirmedFrame + 1; f <= batch.Frame; f++)
        {
            for (int pid = 0; pid < 4; pid++)
            {
                if (pid == LocalPlayerId) continue;
                // 取服务端该玩家 f 帧输入；无记录时用上一帧输入兜底（保持预测连续性）
                InputPayload serverInput = LookupServerInput(batch, f, pid);
                InputPayload predicted = _remoteInputs[pid].Read(f);
                if (!predicted.Equals(serverInput))
                {
                    return f; // 首个失配帧
                }
            }
        }
        return -1;
    }
    
    // 服务端输入查找：有则返回，无则用该玩家上一帧输入兜底
    // batch 仅含单帧输入（batch.Frame），frame != batch.Frame 时直接走兜底
    private InputPayload LookupServerInput(G2C_FrameBatch batch, long frame, int pid)
    {
        if (frame != batch.Frame)
            return _remoteInputs[pid].Read(frame - 1); // 该帧无服务端输入，用上一帧填充预测
        foreach (var pi in batch.Inputs)
        {
            if (pi.PlayerId == pid) return pi.Payload;
        }
        return _remoteInputs[pid].Read(frame - 1); // batch 中无该玩家输入，用上一帧填充预测
    }
}
```

### 3.4 预测失配处理

| 失配类型 | 处理策略 |
|---------|---------|
| 玩家层失配（输入不一致） | 回滚到失配帧 + 重模拟到当前帧（GGPO 流程） |
| 环境层失配（哈希不一致） | 不回滚，等周期调和（200ms 内服务端下发权威数据） |
| 状态哈希失配（0.5Hz 比对） | 上报服务端，多数派裁决，少数派标记 |

---

## 4. 帧回滚机制（核心）

### 4.1 状态快照设计

#### 4.1.1 玩家层全量快照

见 [§2.3.3](#233-回滚核心数据结构) 的 `PlayerLayerSnapshot`。每帧保存（状态量 2.5KB，成本可忽略）。

#### 4.1.2 环境层区域快照

```csharp
// 伪代码，仅作设计参考
// PixelData 类型由 玩法重构方案.md §4.1.1 定义为 struct PixelData : IComponentData（9 字段），
// 本文档所有 NativeArray<PixelData> 参数均引用此定义，不再使用本文件内的类型别名。
// 环境层区域快照单元（统一定义，§2.2.1 引用此处）
public readonly struct RegionSnapshot : IEquatable<RegionSnapshot>
{
    public readonly int ChunkX;          // chunk 列号
    public readonly int ChunkY;          // chunk 行号
    public readonly uint MurmurHash3;    // 16×16=256 像素 × 16B = 4KB 的摘要
    public readonly long Frame;          // 快照对应帧号
    public readonly byte[] PixelBytes;   // 仅哈希失配时填充（懒加载，4KB，RLE 压缩后约 1KB），重命名以避免与 struct PixelData 同名混淆
}

// 环境层快照缓冲：每 60 帧（2 秒）保存一次
public sealed class EnvironmentSnapshotBuffer
{
    private readonly RegionSnapshot[,] _lastSnapshot;  // 最新快照
    private long _lastSnapshotFrame;
    
    public void SaveSnapshot(long frame, PixelGrid grid)
    {
        // 仅在 frame % 60 == 0 时保存
        if (frame % 60 != 0) return;
        
        for (int cx = 0; cx < grid.ChunkColumns; cx++)
        for (int cy = 0; cy < grid.ChunkRows; cy++)
        {
            if (!grid.IsChunkDirty(cx, cy)) continue;  // 仅 dirty chunk；依赖 §2.2.1 待暴露的 IsChunkDirty API（B3 阻塞依赖，详见 §2.2.1）
            _lastSnapshot[cx, cy] = new RegionSnapshot
            {
                ChunkX = cx, ChunkY = cy,
                // PixelGrid 无 GetChunkPixels：聚合 chunk 内像素后哈希
                // 待 PixelGrid DOTS 重构后改用 NativeArray<PixelData> 直接访问
                MurmurHash3 = ComputeChunkHash(grid, cx, cy),
                Frame = frame
            };
        }
        _lastSnapshotFrame = frame;
    }
    
    // 用待 PixelGrid DOTS 重构后新增的 API（ForEachCellInChunk + GetCell，与 §2.2.1 IsChunkDirty 同批新增）聚合 chunk 内像素并计算哈希
    // ⚠ B3 阻塞依赖：以上 API 未暴露前本函数仅作接口占位，无法接入真实像素数据；B3 真机校验不可达（详见 §2.2.1）。
    private static uint ComputeChunkHash(PixelGrid grid, int cx, int cy)
    {
        // ⚠ GC 注意：NativeArray<T> + Allocator.Temp 本身不入托管堆，但本例 lambda 闭包（(x,y)=>…）在 Burst 禁用时会产生分配；
        //    真实实现须改为 [BurstCompile] IJob 或 Burst 内联聚合，禁止托管闭包，确保 B3 GC=0。
        var pixels = new NativeArray<PixelData>(grid.ChunkSize * grid.ChunkSize, Allocator.Temp);
        grid.ForEachCellInChunk(cx, cy, (x, y) =>
        {
            int i = (y % grid.ChunkSize) * grid.ChunkSize + (x % grid.ChunkSize);
            pixels[i] = grid.GetCell(x, y);
        });
        uint hash = MurmurHash3.Compute(pixels);
        pixels.Dispose();
        return hash;
    }
}
```

### 4.2 环形缓冲实现

```csharp
// 伪代码，仅作设计参考
// 通用快照环形缓冲
public sealed class SnapshotRingBuffer<T> where T : class
{
    private readonly T[] _buffer;
    private readonly long[] _frameTags;   // 每个槽对应的帧号
    private readonly int _capacity;
    
    public SnapshotRingBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new T[capacity];
        _frameTags = new long[capacity];
        for (int i = 0; i < capacity; i++) _frameTags[i] = -1;
    }
    
    public void Save(long frame, T snapshot)
    {
        int idx = (int)(frame % _capacity);
        _buffer[idx] = snapshot;
        _frameTags[idx] = frame;
    }
    
    // 读取指定帧快照，不存在返回 null
    public T Load(long frame)
    {
        int idx = (int)(frame % _capacity);
        if (_frameTags[idx] != frame) return null;
        return _buffer[idx];
    }
    
    public bool HasSnapshot(long frame)
    {
        int idx = (int)(frame % _capacity);
        return _frameTags[idx] == frame;
    }
    
    public void ClearBefore(long frame)
    {
        // 清理过期快照（frame 之前的槽）
        for (int i = 0; i < _capacity; i++)
        {
            if (_frameTags[i] < frame && _frameTags[i] >= 0)
            {
                _buffer[i] = null;
                _frameTags[i] = -1;
            }
        }
    }
}
```

### 4.3 多帧回滚处理

```csharp
// 伪代码，仅作设计参考
public sealed class RollbackContext
{
    private const int MAX_ROLLBACK = 7;
    private readonly SnapshotRingBuffer<PlayerLayerSnapshot> _snapshots;
    
    public RollbackContext(SnapshotRingBuffer<PlayerLayerSnapshot> snapshots)
    {
        _snapshots = snapshots;
    }
    
    // 回滚到目标帧
    public void RollbackTo(long targetFrame, SnapshotRingBuffer<PlayerLayerSnapshot> buffer)
    {
        PlayerLayerSnapshot snapshot = buffer.Load(targetFrame);
        if (snapshot == null)
        {
            // 快照丢失（超过 MAX_ROLLBACK 窗口）
            // 冻结模拟 + 请求服务端全量快照
            RequestFullSnapshot();
            return;
        }
        
        // 恢复玩家层状态
        RestorePlayerLayer(snapshot);
    }
    
    // 从 from+1 到 to 重模拟
    public void ReplayForward(long from, long to, ClientLockstepScheduler scheduler)
    {
        for (long f = from + 1; f <= to; f++)
        {
            // 应用该帧的输入（已用服务端权威输入覆盖）
            scheduler.ApplyInputsAndStep(f);
        }
    }
    
    private void RequestFullSnapshot()
    {
        // 触发 C2G_ReconnectRequest 流程（见 §5）
        Network.Send(new C2G_ReconnectRequest { Reason = ReconnectReason.RollbackOverflow });
    }
}
```

### 4.4 回滚成本估算

| 操作 | 单帧成本 | 7 帧回滚总成本 |
|------|---------|---------------|
| Load 快照（memcpy 2.5KB） | 0.01ms | 0.07ms |
| ApplyInputsAndStep（玩家层模拟） | 0.5ms | 3.5ms |
| 环境层 CA 模拟（不回滚，跳过） | 0 | 0 |
| **总计** | 0.51ms | 3.57ms |

> **结论**：7 帧回滚 3.57ms，加上正常 tick 15ms（环境层 CA），单帧总耗时 < 20ms，30Hz 预算 33ms 内可承受。

### 4.5 回滚边界约束

| 约束 | 策略 |
|------|------|
| 最大回滚深度 | `MAX_ROLLBACK = 7`，超过则冻结模拟 + 请求全量快照 |
| 快照丢失 | 冻结模拟 + 请求服务端全量快照（与 §4.3 RollbackTo 一致，不走"回退+重新预测"） |
| 回滚期间渲染 | 渲染层使用插值缓冲，回滚在逻辑层完成后再提交渲染 |
| 并发回滚 | 同一 tick 内只允许一次回滚，避免级联 |

---

## 5. 断线重连处理（核心）

### 5.1 重连窗口设计依据

**30 秒窗口**依据：
- 宽带抖动恢复 < 5 秒
- WiFi 切换 < 10 秒
- 用户网络重连 < 20 秒
- 留 10 秒缓冲给快照传输 + 重模拟

### 5.2 对局继续机制

#### 5.2.1 断线玩家槽位处理

| 时间窗 | 槽位状态 | 行为 |
|--------|---------|------|
| 断线瞬间 | 角色进入 `Reconnecting` 宽限 | 原地不动；`DisconnectedGraceFrames`≈90 帧（3s@30Hz）免秒，属网络层容错，非战斗无敌帧（与 ADR D3 不冲突） |
| 5 秒未重连 | 角色进入 `Reconnecting`/`Frozen` 状态 | 模拟冻结、退出战斗判定、掉落装备到地面（不引入 HP 字段，与 ADR D4 一致） |
| 30 秒未重连 | 判负 | 房间继续 3 人对局 |

> ⚠ **确定性约束（须与 simulation-engineer 协同）**：「断线无敌 3 秒」不是客户端视觉豁免，必须由**确定性模拟状态**驱动——新增 `DisconnectedGraceFrames` 计数器（断线后按帧递减，归零即解除；3 秒 ≈ 90 帧 @30Hz），作为玩家层状态量写入每 60 帧 `MurmurHash3` 状态哈希。否则断线端（暂停模拟/豁免）与在线端哈希分叉，触发误判失配、B3 校验失败。递减逻辑、帧率基准、跨平台定点实现须与模拟层一并定义，避免浮点计时漂移。**断线处理裁定（ADR D4 铁律：MVP 无 HP、纯出界即死）**：「5 秒未重连」与「断线无敌 3s」一律用生命周期状态位表达——玩家层 `PlayerData` 增加 `Alive` / `Reconnecting` / `Spectating`（及 `Frozen` 表达模拟冻结）状态位，**不引入任何 HP/Health 字段**；「5 秒未重连」改为进入 `Reconnecting`/`Frozen` 状态（模拟冻结、退出战斗判定、装备掉地），「断线无敌 3s」改为 `Reconnecting` 宽限（网络层容错，非战斗 i-frames，与 D3 不冲突）。`DisconnectedGraceFrames` 计数器照常作为玩家层状态量写入每 60 帧 `MurmurHash3` 状态哈希，保证三端哈希一致。状态哈希（§8.3 环境层 PixelData 9 字段 + 玩家层 `PlayerLayerSnapshot`）天然不含 HP 字段，与 ADR D4 一致——已全文检索确认本设计文档无 HP/Health 字段定义。

#### 5.2.2 服务端持续保存

服务器为断线玩家持续保存：
- 输入队列（最近 30 帧）
- 玩家层快照（每 60 帧一个，保留 180 帧 = 6 秒）
- frame_batch 缓冲（断线后的所有帧批次，最多 900 帧 = 30 秒）

### 5.3 快照策略（方案 C：全量 + 增量）

| 项 | 值 | 说明 |
|----|-----|------|
| 全量快照周期 | 60 帧（2 秒） | 平衡存储成本与重连速度 |
| 快照保留时长 | 900 帧（30 秒） | 与 30 秒重连窗口对齐；900/60=15 个快照共约 15MB（每快照 1MB，含环境层）。注：此处为全量快照保留，区别于 §5.2.2 的玩家层快照（2.5KB）保留 180 帧 |
| 30 秒重连窗口实现 | 最新全量快照 + 后续 frame_batch 增量 | 全量快照 ≤ 2 秒前，增量 ≤ 28 秒（840 帧） |

### 5.4 重连协议设计

重连协议消息定义（含字段编号）详见 [§7.2](#72-完整-proto-定义)，本节不重复。

### 5.5 重连时序图

```
客户端                              服务端
  │                                  │
  │ 检测断线（ping > 5s 或 socket 错误）
  │ ──────────────────────────────► │ (检测到玩家断线，开始保存 frame_batch)
  │                                  │
  │ 进入重连流程                      │
  │ 重连倒计时 30s 开始               │ 房间内玩家继续对局（断线玩家进入 `Reconnecting` 宽限，网络层容错，非战斗无敌帧）
  │                                  │
  │ 重新连接 Fantasy Session          │
  │ ─── C2G_ReconnectRequest ─────► │
  │   {RoomId, PlayerId,             │
  │    LastConfirmedFrame}           │
  │                                  │ 验证房间存在 + 玩家未被判负
  │                                  │ 加载最新全量快照（frame N-60 到 N）
  │                                  │ 计算分片数 = ceil(1MB / 16KB) = 64
  │ ◄── G2C_ReconnectResponse ──── │
  │   {Success, LatestFullSnapshotFrame=N,
  │    TotalSnapshotChunks=64,       │
  │    CurrentServerFrame=N+840}     │
  │                                  │
  │ ◄── G2C_SnapshotChunk #0 ────── │ (16KB)
  │ ◄── G2C_SnapshotChunk #1 ────── │ (16KB)
  │           ...                    │
  │ ◄── G2C_SnapshotChunk #63 ───── │ (16KB)
  │                                  │
  │ 组装快照（64×16KB = 1MB）         │
  │ 加载到本地 ECS World             │
  │ ─── C2G_SnapshotAck ─────────► │ {Complete=true, AssembledFrame=N}
  │                                  │
  │                                  │ 发送 frame_batch 增量（N+1 到 N+840）
  │ ◄── G2C_FrameBatch (N+1) ────── │
  │ ◄── G2C_FrameBatch (N+2) ────── │
  │           ...                    │
  │ ◄── G2C_FrameBatch (N+840) ──── │
  │                                  │
  │ Replay 增量帧（840 帧 < 1秒）     │
  │ 追上 CurrentServerFrame          │
  │                                  │
  │ 加入对局，恢复输入同步            │
  │ ─── C2G_Input (N+841) ────────► │
  │                                  │ 玩家正式回归（取消 `Reconnecting` 宽限）
  │                                  │
```

### 5.6 重连失败处理

| 失败场景 | 处理 |
|---------|------|
| 30 秒超时 | 判负，房间继续 3 人对局 |
| 快照分片丢失 | 客户端发 `C2G_SnapshotAck{Complete=false}`，服务端重发缺失分片 |
| 快照校验失败（哈希不一致） | 客户端重新请求，最多 3 次，仍失败则判负 |
| 版本不一致 | 直接拒绝，提示玩家更新 |

> 版本上报消息 `C2G_ClientVersionReport` 定义详见 [v1.0 设计文档 §6.6](./多人联机帧同步对战设计.md)。

---

## 6. 配套机制

### 6.1 时钟同步（NTP 风格）

#### 6.1.1 算法

NTP 四时间戳：
- `t1`：客户端发送请求时刻（客户端时钟）
- `t2`：服务端接收请求时刻（服务端时钟）
- `t3`：服务端发送响应时刻（服务端时钟）
- `t4`：客户端接收响应时刻（客户端时钟）

```
RTT = (t4 - t1) - (t3 - t2)
ClockOffset = ((t2 - t1) + (t3 - t4)) / 2
ServerTime = LocalTime + ClockOffset
```

#### 6.1.2 EMA 平滑

```csharp
// 伪代码，仅作设计参考
public sealed class ClockSync
{
    private const float EMA_ALPHA = 0.1f;  // 平滑系数
    private long _offsetTicks;             // 时钟偏移（ticks）
    private long _rttTicks;                // 平滑 RTT
    
    public void OnClockSyncResponse(C2G_ClockSyncRequest req, G2C_ClockSyncResponse resp)
    {
        long t1 = req.ClientSendTicks;
        long t4 = DateTime.UtcNow.Ticks;
        long t2 = resp.ServerReceiveTicks;
        long t3 = resp.ServerSendTicks;
        
        long rtt = (t4 - t1) - (t3 - t2);
        long offset = ((t2 - t1) + (t3 - t4)) / 2;
        
        // EMA 平滑（避免单次抖动影响）
        _rttTicks = (long)(_rttTicks * (1 - EMA_ALPHA) + rtt * EMA_ALPHA);
        _offsetTicks = (long)(_offsetTicks * (1 - EMA_ALPHA) + offset * EMA_ALPHA);
    }
    
    public long GetServerTimeTicks()
    {
        return DateTime.UtcNow.Ticks + _offsetTicks;
    }
    
    public long GetRttMs()
    {
        return _rttTicks / TimeSpan.TicksPerMillisecond;
    }
}
```

#### 6.1.3 同步频率

| 时机 | 次数 | 说明 |
|------|------|------|
| 对局开始 | 连续 3 次 | 校准初始偏移 |
| 对局中 | 每 10 秒 1 次 | 修正漂移 |
| 重连后 | 连续 3 次 | 重新校准 |

### 6.2 Jitter Buffer

#### 6.2.1 参数

```
buffer_size_frames = ceil(max_jitter_ms / tick_ms) = ceil(50 / 33.33) = 2 帧
```

#### 6.2.2 实现

```csharp
// 伪代码，仅作设计参考
public sealed class JitterBuffer<T> where T : class
{
    private readonly T[] _buffer;
    private readonly int _capacity;
    private int _count;
    private long _nextExpectedFrame;
    
    public JitterBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new T[capacity];
    }
    
    // 写入包（可能乱序到达）
    public void Push(long frame, T packet)
    {
        int idx = (int)(frame % _capacity);
        _buffer[idx] = packet;
    }
    
    // 按帧序顺序弹出，缺帧返回 default
    public bool TryPop(long expectedFrame, out T packet)
    {
        int idx = (int)(expectedFrame % _capacity);
        if (_buffer[idx] == null)
        {
            packet = default;
            return false;  // 缺帧，调用方做预测填充
        }
        packet = _buffer[idx];
        _buffer[idx] = null;
        return true;
    }
    
    // 缺帧处理：用上一帧输入预测填充 + 收到真包时回滚
    public T HandleMissing(long frame, T lastReceived)
    {
        // 预测填充：返回上一帧的输入
        // 真包到达时由 RollbackContext 处理（见 §3.3 OnFrameBatch）
        return lastReceived;
    }
}
```

#### 6.2.3 乱序包重排

包按 `frame` 字段写入环形槽，`TryPop` 严格按 `expectedFrame` 顺序读取。乱序到达的包自动归位。

### 6.3 慢速客户端 Catch-up

#### 6.3.1 落后检测

```csharp
// 伪代码，仅作设计参考
public sealed class CatchUpController
{
    private const int MAX_CATCHUP_STEPS = 5;       // 单 tick 最多追 5 帧
    private const int LAG_THRESHOLD_FULL_RESET = 10; // 超过则请求全量快照
    
    public int ComputeCatchUpSteps(long serverFrame, long localFrame)
    {
        // ComputeCatchUpSteps 在 lag ≤ 10 范围内单调非递减（1,2,3,4,5,5,5,5,5,5）
        // lag > 10 时返回 -1 是异常分支，不参与步数比较
        long lag = serverFrame - localFrame;
        
        if (lag <= 1) return 1;                    // 正常 1 步
        if (lag <= 5)  return (int)lag;            // 每 tick 跑 lag 步追上
        if (lag <= 10) return MAX_CATCHUP_STEPS;   // 每 tick 跑 5 步
        return -1;                                  // 触发全量快照重置
    }
}
```

#### 6.3.2 三档处理

| 落后帧数 | 处理 | 说明 |
|---------|------|------|
| `lag <= 1` | 正常 1 步 | 无需追赶 |
| `2 <= lag <= 5` | 每 tick 跑 `lag` 步 | 1-2 秒内追上 |
| `6 <= lag <= 10` | 每 tick 跑 5 步 | 2-3 秒追上 |
| `lag > 10` | 请求全量快照重置 | 走重连流程（§5） |

#### 6.3.3 服务端慢速检测

```csharp
// 伪代码，仅作设计参考（服务端 Entity 层）
public sealed class SlowClientDetector
{
    private const int KICK_LAG_FRAMES = 30;       // 落后 30 帧（1 秒）踢出
    private const int WARN_LAG_FRAMES = 15;      // 落后 15 帧警告
    
    public void OnFrameTick(GameRoom room)
    {
        foreach (var player in room.Players)
        {
            long lag = room.FrameClock - player.LastInputFrame;
            if (lag > KICK_LAG_FRAMES)
            {
                // 踢出：判负 + 房间继续
                room.KickPlayer(player, "Slow client (lag > 1s)");
            }
            else if (lag > WARN_LAG_FRAMES)
            {
                // 警告：UI 提示玩家网络差
                room.SendTo(player, new G2C_NetworkWarning { LagFrames = lag });
            }
        }
    }
}
```

### 6.4 观战延迟

#### 6.4.1 参数

| 观战类型 | 延迟 | 帧数 | 说明 |
|---------|------|------|------|
| 默认观战 | 10 秒 | 300 帧 | 防止信息泄露影响对局 |
| 死亡玩家观战 | 1 秒 | 30 帧 | 降低体验损失（已死亡，无信息优势） |

#### 6.4.2 实现

```csharp
// 伪代码，仅作设计参考（服务端 Entity 层）
public sealed class SpectatorBroadcaster
{
    private const int DEFAULT_DELAY_FRAMES = 300;  // 10 秒
    private const int DEAD_PLAYER_DELAY_FRAMES = 30; // 1 秒
    
    private readonly RingBuffer<G2C_FrameBatch> _batchBuffer;
    
    public SpectatorBroadcaster()
    {
        _batchBuffer = new RingBuffer<G2C_FrameBatch>(450); // 15 秒容量
    }
    
    // 房间 tick 推进时调用
    public void OnRoomTick(G2C_FrameBatch batch)
    {
        _batchBuffer.Push(batch.Frame, batch);
    }
    
    // 给观战者发送延迟帧
    public void BroadcastToSpectator(Spectator spectator, long currentFrame)
    {
        int delay = spectator.IsDeadPlayer 
            ? DEAD_PLAYER_DELAY_FRAMES 
            : DEFAULT_DELAY_FRAMES;
        
        long targetFrame = currentFrame - delay;
        if (_batchBuffer.TryGet(targetFrame, out var batch))
        {
            spectator.Session.Send(batch);
        }
    }
}

// 简易环形缓冲
public sealed class RingBuffer<T>
{
    private readonly T[] _buffer;
    private readonly long[] _frameTags;
    private readonly int _capacity;
    
    public RingBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new T[capacity];
        _frameTags = new long[capacity];
        for (int i = 0; i < capacity; i++) _frameTags[i] = -1;   // 初始化为 -1（未写入，参考 §4.2）
    }
    
    public void Push(long frame, T item)
    {
        int idx = (int)(frame % _capacity);
        _buffer[idx] = item;
        _frameTags[idx] = frame;
    }
    
    public bool TryGet(long frame, out T item)
    {
        int idx = (int)(frame % _capacity);
        if (_frameTags[idx] != frame)
        {
            item = default;
            return false;
        }
        item = _buffer[idx];
        return true;
    }
}
```

---

## 7. 协议设计补充

### 7.1 追加消息总览

在已有 [OuterMessage.proto](../Tools/NetworkProtocol/Outer/OuterMessage.proto) 基础上追加：

| 消息 | 方向 | 序列化 | 频率 | 用途 |
|------|------|--------|------|------|
| `C2G_ReconnectRequest` | C→S | ProtoBuf | 一次性 | 重连请求 |
| `G2C_ReconnectResponse` | S→C | ProtoBuf | 一次性 | 重连响应 |
| `C2G_ReconnectAbort` | C→S | ProtoBuf | 一次性 | 客户端取消重连 |
| `G2C_ReconnectProgress` | S→C | ProtoBuf | 重连期间 | 重连进度通知（广播其他玩家） |
| `G2C_SnapshotChunk` | S→C | MemoryPack | 流式（64 片） | 快照分片 |
| `C2G_SnapshotAck` | C→S | ProtoBuf | 1-3 次 | 接收确认 |
| `C2G_ClockSyncRequest` | C→S | MemoryPack | 每 10s | 时钟同步请求 |
| `G2C_ClockSyncResponse` | S→C | MemoryPack | 每 10s | 时钟同步响应 |
| `G2C_SpectatorBatch` | S→C | MemoryPack | 30Hz | 观战帧批次（延迟版） |
| `G2C_NetworkWarning` | S→C | ProtoBuf | 触发时 | 慢速客户端警告 |

### 7.2 完整 proto 定义

```protobuf
// 伪代码，仅作设计参考
// 追加到 Outer/OuterMessage.proto
// 保留字段号，与 OuterMessage.proto 现有风格一致（不使用 optional）
// 序列化协议：在每个 message 上方用 // Protocol ProtoBuf 或 // Protocol MemoryPack 标注

// ===== 重连协议 =====

// Protocol ProtoBuf
message C2G_ReconnectRequest // IRequest, G2C_ReconnectResponse
{
    uint64 RoomId = 1;
    uint32 PlayerId = 2;
    uint64 LastConfirmedFrame = 3;
    ReconnectReason Reason = 4;
}

// Protocol ProtoBuf
message G2C_ReconnectResponse // IResponse
{
    bool Success = 1;
    ReconnectFailReason FailReason = 2;
    uint64 LatestFullSnapshotFrame = 3;
    uint32 TotalSnapshotChunks = 4;
    uint64 CurrentServerFrame = 5;
    uint32 RemainingReconnectSeconds = 6;
    uint64 RoomId = 7;                  // 回显房间 ID（供客户端校验）
    uint32 PlayerId = 8;                // 回显玩家槽位（供客户端校验）
}

// Protocol MemoryPack
message G2C_SnapshotChunk // IMessage
{
    uint32 ChunkIndex = 1;
    uint32 TotalChunks = 2;
    bytes Payload = 3;        // ≤ 16KB，MemoryPack 序列化的快照分片
    bool IsCompressed = 4;   // Zstd 压缩标志
}

// Protocol ProtoBuf
message C2G_SnapshotAck // IMessage
{
    uint32 ReceivedChunks = 1;
    bool Complete = 2;
    uint64 AssembledFrame = 3;
}

// Protocol ProtoBuf
message C2G_ReconnectAbort // IMessage
{
    uint64 RoomId = 1;
    uint32 PlayerId = 2;
    ReconnectAbortReason Reason = 3;
}

// Protocol ProtoBuf
message G2C_ReconnectProgress // IMessage
{
    uint32 ReconnectingPlayerId = 1;  // 正在重连的玩家槽位
    ReconnectStage Stage = 2;        // 重连阶段
    uint32 RemainingSeconds = 3;     // 剩余重连时间
}

enum ReconnectReason
{
    NetworkDrop = 0;
    AppBackground = 1;
    RollbackOverflow = 2;
}

enum ReconnectFailReason
{
    RoomClosed = 0;
    Timeout = 1;
    PlayerKicked = 2;
    VersionMismatch = 3;
}

enum ReconnectAbortReason
{
    UserCancel = 0;
    GiveUp = 1;
}

enum ReconnectStage
{
    RequestReceived = 0;
    SnapshotStreaming = 1;
    Replaying = 2;
    Rejoined = 3;
}

// ===== 时钟同步协议 =====

// Protocol MemoryPack
message C2G_ClockSyncRequest // IRequest, G2C_ClockSyncResponse
{
    int64 ClientSendTicks = 1;  // 客户端发送时刻（UTC ticks）
}

// Protocol MemoryPack
message G2C_ClockSyncResponse // IResponse
{
    int64 ServerReceiveTicks = 1;  // 服务端接收时刻
    int64 ServerSendTicks = 2;    // 服务端发送时刻
}

// ===== 观战协议 =====

// Protocol MemoryPack
message G2C_SpectatorBatch // IMessage
{
    uint64 Frame = 1;                 // 帧号（已减去延迟）
    repeated PlayerInput Inputs = 2;          // 所有玩家输入
    // repeated G2C_SceneEvent DelayedEvents = 3; // 该帧触发的场景事件 —— 对齐《协议与序列化规范.md》§6：v1.0 观战仅转发 Inputs，场景事件回放延后至「观战后」MVP 再补，暂不纳入 wire
    uint32 SpectatorId = 4;          // 观战者 ID
}

// ===== 网络警告（慢速客户端） =====

// Protocol ProtoBuf
message G2C_NetworkWarning // IMessage
{
    int64 LagFrames = 1;      // 落后帧数
    NetworkWarnLevel Level = 2;
}

enum NetworkWarnLevel
{
    Info = 0;       // lag <= 5
    Warning = 1;    // 6 <= lag <= 15
    Critical = 2;    // 16 <= lag <= 30
}
```

### 7.3 序列化选择依据

| 消息类型 | 序列化 | 理由 |
|---------|--------|------|
| `G2C_SnapshotChunk` | MemoryPack | 大数据高频，MemoryPack 比 ProtoBuf 快 5-10 倍 |
| `C2G_ClockSyncRequest/Response` | MemoryPack | 高频小包，需极速 |
| `G2C_SpectatorBatch` | MemoryPack | 30Hz 高频 |
| `C2G_ReconnectRequest/Response` | ProtoBuf | 低频一次性，可读性优先 |
| `C2G_SnapshotAck` | ProtoBuf | 低频小包 |

### 7.4 消息频率与带宽表

| 消息 | 频率 | 单包体积 | 4 玩家带宽/秒 |
|------|------|---------|--------------|
| `C2G_Input` | 30 Hz | 16B wire（InputPayload 8B + uint64 Frame 8B 包络） | 4×30×16 = 1.9 KB/s |
| `G2C_FrameBatch` | 30 Hz | 56B（4 玩家 × 14B：InputPayload 8B + 6B 帧元数据） | 4×30×56 = 6.7 KB/s |
| `C2G_StateHash` | 0.5 Hz | 24B | 4×0.5×24 = 48 B/s |
| `G2C_SnapshotChunk` | 重连时 64 片 | 16KB | 1MB 一次性 |
| `C2G_ClockSyncRequest` | 0.1 Hz | 8B | 4×0.1×8 = 3.2 B/s |
| `G2C_SpectatorBatch` | 30 Hz（仅观战） | 80B | 1×30×80 = 2.4 KB/s |

> **总带宽**：常规对局 ≈ 10 KB/s，剧烈战斗峰值 ≈ 380 KB/s（30% chunk 失配，RLE 压缩后约 200 KB/s，含环境层失配下发）。注：`C2G_Input` / `G2C_FrameBatch` 体积以《协议与序列化规范.md》§13 为准（InputPayload 8B 定长、ProtocolVersion=2）；本表原为含 SpellId 的 12B / 60B 旧口径，已校正。

---

## 8. 数据结构总览

### 8.1 通用环形缓冲

| 类型 | 用途 | 容量 |
|------|------|------|
| `InputRingBuffer<T>` | 每玩家输入缓冲 | 10 帧（覆盖回滚 7 帧 + 当前 + 输入延迟 2 帧 = 10 帧） |
| `SnapshotRingBuffer<T>` | 玩家层快照缓冲 | 8 帧 |
| `RingBuffer<G2C_FrameBatch>` | 观战帧缓冲 | 450 帧（15 秒） |

### 8.2 Xorshift128Plus 确定性 PRNG

> 注：v1.0 §8.2 确定性保证清单已声明使用 Xorshift128Plus 作为确定性 PRNG，本文档提供实现细节。

> **注**：种子扩展已在 [玩法重构方案.md §4.1.4](./玩法重构方案.md) 修正为链式初始化（`_s1 = SplitMix64(_s0)`），实现时应使用修正版。本文档保留原版仅作历史参考与设计思路记录。

```csharp
// 伪代码，仅作设计参考
// Burst 兼容的确定性 PRNG，服务端下发种子
public struct Xorshift128Plus
{
    private ulong _s0;
    private ulong _s1;
    
    public Xorshift128Plus(ulong seed)
    {
        // SplitMix64 初始化（避免全 0 种子）
        _s0 = SplitMix64(seed);
        _s1 = SplitMix64(seed + 0x9E3779B97F4A7C15UL);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Next()
    {
        ulong x = _s0;
        ulong y = _s1;
        _s0 = y;
        x ^= x << 23;
        _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
        return _s1 + y;
    }
    
    private static ulong SplitMix64(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
```

### 8.3 MurmurHash3 状态哈希

> **注**：状态哈希字段已在 [玩法重构方案.md §4.1.5](./玩法重构方案.md) 修正为遍历全 9 字段（MaterialType/Temperature/Density/Flags/FallingFrames/Color/VelocityX/VelocityY/Lifetime），并对 4 个 short 字段添加 `& 0xFFFF` 位掩码防止符号扩展污染。实现时应使用修正版。本文档保留原版仅作设计思路记录。原版仅哈希 4 字段（MaterialType/Temperature/Density/Flags），遗漏 5 字段（VelocityX/VelocityY/Lifetime/Color/FallingFrames），导致状态哈希假阳性通过。

修正版 CombinePixels 关键步骤（完整实现见 GAMEPLAY §4.1.5）：

```csharp
uint h = Seed;
h ^= ... // 全 9 字段异或（含 & 0xFFFF 掩码保护 short 字段）
h *= 0xcc9e2d51;                 // c1
h = (h << 15) | (h >> 17);       // rotl 15
h *= 0x1b873593;                 // c2 (Round 4 修复: 原 §4.1.5 漏此三步)
h = (h << 13) | (h >> 19);       // rotl 13 (Round 4 修复)
h = h * 5 + 0xe6546b64;          // 最终 mix (Round 4 修复)
```

> 以下为原版（仅作设计思路记录，实现时勿直接复制）：

```csharp
// 伪代码，仅作设计参考
// Burst 兼容，用于环境层 chunk 哈希
public static class MurmurHash3
{
    private const uint Seed = 0x9747b28c;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(NativeArray<PixelData> data)
    {
        uint h = Seed;
        // 每 4 像素（64B）作为一块处理
        for (int i = 0; i < data.Length; i += 4)
        {
            uint k = CombinePixels(data, i);
            k *= 0xcc9e2d51;
            k = (k << 15) | (k >> 17);
            k *= 0x1b873593;
            h ^= k;
            h = (h << 13) | (h >> 19);
            h = h * 5 + 0xe6546b64;
        }
        h ^= (uint)data.Length;
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return h;
    }
    
    private static uint CombinePixels(NativeArray<PixelData> data, int start)
    {
        // 将 4 个 PixelData（64B）压缩为 uint 摘要
        uint hash = 0;
        for (int i = 0; i < 4 && start + i < data.Length; i++)
        {
            var p = data[start + i];
            hash ^= (uint)(p.MaterialType << 24 | p.Temperature & 0xFF << 16 | p.Density << 8 | p.Flags);
        }
        return hash;
    }
}
```

### 8.4 Fix64 定点数

引用已有 [多人联机帧同步对战设计.md §4.2.1](./多人联机帧同步对战设计.md)，Q31.32 格式，覆盖 ±2.1e9，精度 2.3e-10。

> **注**：Fix64 范围限制与乘法实现已在 [玩法重构方案.md §4.1.3](./玩法重构方案.md) 修正（范围限制 ±2^15 + 乘法使用 double 中间结果避免 64 位溢出）。实现时应使用修正版。

修正版 Fix64 范围与乘法（完整实现见 GAMEPLAY §4.1.3）：

```csharp
public const double MaxValue = 32768.0;  // ±2^15 范围限制
public static Fix64 operator *(Fix64 a, Fix64 b)
{
    // double 中间结果避免 64 位溢出（精度损失但跨平台一致）
    return new Fix64((long)((double)a._rawValue * (double)b._rawValue / (double)One));
}
```

---

## 9. 与已有设计文档的集成关系

### 9.1 章节映射表

| 本文档章节 | 已有文档对应章节 | 关系 | 实施动作 |
|-----------|-----------------|------|---------|
| §2 分层回滚模型 | 已有 §4.2.3 周期调和 | **深化** | 替换 §4.2.3 内容，引用本文 §2 |
| §3 用户输入预测 | 已有无 | **新增** | 在已有 §4 客户端设计后追加 |
| §4 帧回滚机制 | 已有 §11 Phase 3 | **深化** | Phase 3 待办项引用本文 §4 |
| §5 断线重连处理 | 已有 §1.2 设计目标 | **深化** | 已有目标保留，实现引用本文 §5 |
| §6 配套机制 | 已有无 | **新增** | 在已有 §8 后追加 |
| §7 协议设计补充 | 已有 §6 协议设计 | **追加** | 已有消息保留，追加新消息 |
| §8 数据结构总览 | 已有无 | **新增** | 实现时引用 |

### 9.2 需要修改已有文档的部分

1. **已有 §4.2.3 周期调和**：替换为本文 §2.2 的详细描述
2. **已有 §6.1 消息类型一览**：追加本文 §7.1 的新消息
3. **已有 §11 Phase 3 网络层**：待办项"断线重连（snapshot + rewind）"展开为本文 §5 的子任务

### 9.3 不修改的部分

- 已有 §3 总体架构（本文引用）
- 已有 §9 反作弊（本文不重复）
- 已有 §4.5 热更与资源管理（独立主题）

---

## 10. 风险与对策

| 风险 | 概率 | 影响 | 对策 |
|------|------|------|------|
| 玩家层回滚 CPU 开销超预算 | 低 | 高（掉帧） | 限制 MAX_ROLLBACK=7，profiler 监控，必要时降到 5 |
| 环境层周期调和带宽峰值 | 中 | 中（网络拥塞） | RLE 压缩 + 失配率 > 50% 触发全量快照重置 |
| 重连快照传输中断 | 中 | 高（重连失败） | 分片重传机制 + 3 次重试 + 判负兜底 |
| 时钟漂移导致 desync | 低 | 高（判定错误） | EMA 平滑 + 每 10s 校准 + 漂移 > 100ms 强制重连 |
| Jitter Buffer 不足导致卡顿 | 中 | 低（体验下降） | 动态调整 buffer_size（2-4 帧）基于 RTT 方差 |
| 回滚溢出（> MAX_ROLLBACK） | 低 | 高（冻结对局） | 触发全量快照重置（走重连流程） |
| 远端玩家预测失配频繁 | 中 | 低（视觉跳变） | 优化预测算法（输入趋势外推），降低回滚频率 |
| 观战延迟导致死亡玩家体验差 | 低 | 低 | 死亡玩家 1 秒延迟，活人 10 秒延迟 |
| 服务端慢速踢出误判 | 中 | 中（误踢） | 阈值 30 帧（1 秒）+ 警告前置 15 帧 |
| 重连后状态与对局不一致 | 低 | 高（desync） | 重连后强制状态哈希比对一次 |

---

## 11. 实施优先级

### P0：玩家层快照 + 回滚基础（必须）

- [ ] 实现 `InputRingBuffer<T>` / `SnapshotRingBuffer<T>` 通用环形缓冲
- [ ] 实现 `PlayerLayerSnapshot` 数据结构 + 深拷贝
- [ ] 实现 `RollbackContext.RollbackTo` / `ReplayForward`
- [ ] 实现 `ClientLockstepScheduler.TickFrame` / `OnFrameBatch`
- [ ] 单机回滚测试：模拟 4 玩家输入失配，验证回滚正确性

**验收**：单机模拟 1000 帧，注入随机输入失配，状态哈希与服务端一致。

### P1：断线重连协议 + 快照策略（必须）

- [ ] 实现 `C2G_ReconnectRequest` / `G2C_ReconnectResponse` proto
- [ ] 实现 `G2C_SnapshotChunk` 分片流发送 + `C2G_SnapshotAck` 确认
- [ ] 实现服务端快照保存（每 60 帧全量，保留 180 帧）
- [ ] 实现客户端快照组装 + Replay 增量帧
- [ ] 实现 30 秒重连窗口 + 判负兜底
- [ ] 实现断线玩家槽位策略（`Reconnecting` 宽限 3s → `Reconnecting`/`Frozen` 状态退出战斗 → 判负，不引入 HP 字段，与 ADR D4 一致）

**验收**：4 人对局（默认人数）中主动断开 1 人，30 秒内重连成功，状态恢复一致。另测 2 人对局断线重连。

### P2：时钟同步 + Jitter Buffer（重要）

- [ ] 实现 `ClockSync` NTP 算法 + EMA 平滑
- [ ] 实现 `JitterBuffer<T>` 乱序包重排
- [ ] 实现 `C2G_ClockSyncRequest/Response` proto
- [ ] 对局开始连续 3 次校准 + 对局中每 10s 修正

**验收**：4 人 ping 100ms 抖动 50ms，对局 5 分钟无 desync。另测 8 人满员场景（验证带宽与回滚开销）。

### P3：Catch-up + 观战延迟（增强）

- [ ] 实现 `CatchUpController` 三档处理
- [ ] 实现服务端 `SlowClientDetector` + 踢出策略
- [ ] 实现 `SpectatorBroadcaster` + `RingBuffer<G2C_FrameBatch>`
- [ ] 实现死亡玩家 1s 延迟 / 默认 10s 延迟
- [ ] 实现 `G2C_SpectatorBatch` proto

**验收**：观战者 10 秒延迟无信息泄露，死亡玩家 1 秒延迟观战体验良好。

---

## 12. 引用

- **GGPO**: Tony Cannon, *GGPO Network SDK Documentation* — 回滚参数（INPUT_DELAY=2, MAX_ROLLBACK=7）参考
- **Gaffer on Games**: Glenn Fiedler, *Deterministic Lockstep* / *Snapshot Interpolation* — 时钟同步与插值参考
- **Photon Quantum**: Exit Games, *Quantum Deterministic Network Engine* — 周期调和与状态哈希思路
- **NTP**: David L. Mills, *Network Time Protocol Version 4* — 四时间戳算法
- **MurmurHash3**: Appleby, *MurmurHash3 32-bit* — Burst 兼容状态哈希
- **Xorshift128Plus**: Vigna, *Further scramblings of Marsaglia's xorshift generators* — 确定性 PRNG
- 项目文档:
  - [多人联机帧同步对战设计.md](./多人联机帧同步对战设计.md) — 总体架构
  - [DOTS重构设计文档.md](./DOTS重构设计文档.md) — DOTS 重构
  - [PixelGrid.cs](../Client/Assets/Scripts/AOT/Simulation/PixelGrid.cs) — changedChunks 机制
  - [OuterMessage.proto](../Tools/NetworkProtocol/Outer/OuterMessage.proto) — 协议定义

---

**文档结束**
