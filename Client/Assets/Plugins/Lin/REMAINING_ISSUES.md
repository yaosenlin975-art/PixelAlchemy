# Lin 插件剩余问题清单

> 生成时间: 2026-06-03
> 状态: 已完成主题 1-5，剩余问题按优先级排列

---

## 一、Critical 级别 (必须修复)

### 1.1 日志文件路径映射错误 ✅ 已修复

**文件:** `Runtime/Helper/Log.cs` L73-88

**问题:** `LogType.Error` 和 `LogType.Exception` 的路径变量映射反了：
```csharp
case LogType.Error:      // ← 普通错误
    return GetFilePath(ref exceptionPath);  // ← 但写的是 exceptionPath!
case LogType.Exception:  // ← 异常
    return GetFilePath(ref errorPath);      // ← 但写的是 errorPath!
```

**影响:** 错误日志和异常日志会写到对方的文件中，导致排查问题时看错文件。

**修复方案:**
```csharp
case LogType.Error:      return GetFilePath(ref errorPath);
case LogType.Exception:  return GetFilePath(ref exceptionPath);
```

---

### 1.2 ServerCommands.serverProcess 静态字段跨线程竞争 ✅ 已修复

**文件:** `Editor/Command/ServerCommands.cs` L26, L48-68, L71-98

**问题:**
- `serverProcess` 在 `Task.Run` 内部（线程池线程）赋值 (L191)
- `Stop()` 在主线程读取 `serverProcess` (L73-98)
- 典型的竞态条件：`Stop()` 可能读到 null 或未完全初始化的对象

**修复方案:** 给 `serverProcess` 添加 `volatile` 关键字，或使用 `lock` 保护读写。

---

### 1.3 HtmlToUGUIBaker 跨线程字段访问 ✅ 已修复

**文件:** `Editor/UI/HtmlToUGUIBaker.cs` L68-69

**问题:**
- `bakeTcpListener` 在 `RunBakeServer`（后台线程）中初始化
- `StopAutoBakeServer`（主线程）中访问
- `bakeListenerThread` 同理

**修复方案:** 两个字段都加 `volatile` 修饰符。

---

## 二、Warning 级别 (建议修复)

### 2.1 async void 反模式 (剩余) ✅ 已修复

> 注意: Unity 消息函数 (Start/OnEnable 等) 可保留 async void，无需修改。

| 文件 | 行号 | 方法签名 | 风险 |
|------|------|----------|------|
| `Runtime/IngameConsole/NetworkCommands.cs` | 14 | `public static async void Ping(string ip)` | Console 命令，无法 await |
| `Runtime/Notice/DingTalkNoticer.cs` | 31 | `public override async void Message(string message)` | 继承基类约束 |
| `Runtime/Notice/WeixinWorkNoticer.cs` | 22 | `public async override void Message(string message)` | 继承基类约束 |
| `Runtime/Notice/WeixinWorkNoticer.cs` | 43 | `public async void File(string filePath)` | 调用方不 await |
| `Runtime/Tool/IntervalLooper.cs` | 63 | `private async void RunAsync()` | 内部 fire-and-forget |
| `Runtime/Manager/SoundManager.cs` | 153 | `private async void Play(...)` | 内部 fire-and-forget |
| `Runtime/UI/PanelBase.cs` | 246 | `async void OnComplete()` | 事件回调 |
| `Runtime/Resource/Updater/ResourcesUpdater.cs` | 58 | `public override async void BeginDownload()` | 继承基类约束 |
| `Runtime/Resource/Updater/AndroidUpdater.cs` | 38 | `public override async void BeginDownload()` | 继承基类约束 |
| `Runtime/Helper/TransformExtensions.cs` | 1715 | `public static async void Square(...)` | 扩展方法 fire-and-forget |

**可保留 async void (Unity 消息/局部函数):**
- `Initializer.cs` L42: `private async void Start()` — Unity MonoBehaviour 消息
- `Initializer.cs` L120: `async void OnResourceUpdateFinish(...)` — 局部函数

**建议修复策略:**
1. 对于有基类约束的 (DingTalkNoticer/WeixinWorkNoticer/ResourcesUpdater/AndroidUpdater) — 考虑将基类 `NoticerBase.Message` 和 `UpdaterBase.BeginDownload` 改为返回 `UniTask`
2. 对于独立方法 (Ping/File/Square/RunAsync/Play/OnComplete) — 改为 `UniTask` 方法，调用方用 `.Forget()` 处理 fire-and-forget

---

### 2.2 WebGL GetAwaiter().GetResult() 阻塞主线程 ✅ 已标注注释

| 文件 | 行号 | 说明 |
|------|------|------|
| `Runtime/Tool/LazyAsset.cs` | 60 | `GetPrefab()` WebGL 分支 |
| `Runtime/Tool/LazyAsset.cs` | 78 | `Instantiate()` WebGL 分支 |
| `Runtime/Design Pattern/Singleton/ScriptableObjectSingleton.cs` | 46 | `GetInstance()` WebGL 分支 |
| `Runtime/Design Pattern/Singleton/MonoSingleton.cs` | 45 | `GetInstance()` WebGL 分支 |
| `Runtime/Attribute/AssetPathAttribute.cs` | 52 | `Load<T>()` 中 `GetAwaiter().GetResult()` |

**说明:**
- 在 WebGL 平台，UniTask 不依赖 `SynchronizationContext`，所以 `GetAwaiter().GetResult()` **不会死锁**
- 但会阻塞主线程直到异步操作完成，可能造成帧卡顿
- 当前标记为 **Warning** (非 Critical)，因为 WebGL 本身是单线程，且 YooAsset 设置了 `WebGLForceSyncLoadAsset = true`

**建议:** 保持现状，但应在文档中标注此行为，提醒开发者在 WebGL 平台避免在 Update 中调用这些同步方法。

---

### 2.3 File.Create().Close() 未用 using 保护

**文件:** `Runtime/Helper/Log.cs` L103

```csharp
File.Create(path).Close();  // 应该用 using 包裹
```

**修复:** 改为 `using (File.Create(path)) { }` (与 Theme 3 一致)

---

## 三、Info 级别 (技术债)

### 3.1 TODO/FIXME 标记清单 ✅ 已审计 (均为功能规划标记，保留)

| 文件 | 行号 | 内容 |
|------|------|------|
| `Editor/Asset/RepeatedAssetChecker/AssetDetail.cs` | 48 | `TODO: 找出依赖这个资源的资源, 替换成其他现有的资源` |
| `Runtime/Resource/Info/Infos.cs` | 57 | `TODO:DLC` |
| `Editor/Sprite/SpriteSpliter/SpriteSpliterWindow.cs` | 34 | `TODO:添加常见锚点` |
| `Editor/Sprite/SpriteMenuItems.cs` | 341 | `TODO:动画是否直接覆盖` |
| `Editor/Helper/LinkHelper.cs` | 27 | `TODO: 合并时检测程序集是否存在, 不存在则直接从link中删除并打印` |
| `Runtime/Helper/AnimatorExtensions.cs` | 3 | `TODO: Improve Set Animator` (文件头注释) |
| `Editor/FrameworkSynchronizer/AssetsTreeWindow/AssetsTreeWindow.cs` | 11 | `TODO: 点击窗口中的文件/文件夹时...` |
| `Runtime/Tool/PriorityQueue.cs` | 894, 939 | `TODO: Update if this changes in the future` (.NET issue tracker) |

**EditorTools.cs** L184/187 的 `throw new System.NotImplementedException("TODO:运行逻辑")` 是代码生成模板的一部分，属于设计意图，无需修改。

---

### 3.2 缺失的 XML 注释 ✅ 已补充

部分公共类/方法缺少 `<summary>` XML 注释，建议优先补充：

| 文件 | 类型 |
|------|------|
| `Runtime/Inventory/InventorySlot.cs` | 公共类缺少说明 |
| `Runtime/Interface/IShow.cs` | 接口缺少说明 |
| `Runtime/Interface/IScrollableData.cs` | 接口缺少说明 |
| `Runtime/Interface/IInspectorGUI.cs` | 接口缺少说明 |
| `Runtime/Interface/IInventoryItem.cs` | 接口缺少说明 |
| `Runtime/Interface/IMove.cs` | 接口缺少说明 |
| `Runtime/Interface/IUpdate.cs` | 接口缺少说明 |
| `Runtime/Interface/IPoolObject.cs` | 接口缺少说明 |
| `Runtime/Interface/IInitialize.cs` | 接口缺少说明 |
| `Runtime/Interface/IFixedUpdate.cs` | 接口缺少说明 |
| `Runtime/Interface/IVersion.cs` | 接口缺少说明 |
| `Runtime/Interface/ILateUpdate.cs` | 接口缺少说明 |
| `Runtime/Interface/IEvent.cs` | 接口缺少说明 |
| `Runtime/Interface/IDrawGizmos.cs` | 接口缺少说明 |
| `Runtime/Inventory/IStackable.cs` | 接口缺少说明 |
| `Runtime/Inventory/IInventoryTagged.cs` | 接口缺少说明 |
| `Runtime/Inventory/InventoryExtensions.cs` | 公共类缺少说明 |

---

### 3.3 IntervalLooper 空 catch 吞异常 ✅ 已修复

**文件:** `Runtime/Tool/IntervalLooper.cs` L96

```csharp
catch (Exception) { }  // 吞掉所有异常，包括业务逻辑异常
```

**建议:** 至少添加一条 `Debug.LogWarning` 或在注释中说明吞异常的原因。

---

## 四、已完成的主题回顾

| 主题 | 状态 | 修复内容 |
|------|------|----------|
| **主题 1**: 命名空间与目录卫生 | ✅ 完成 | GUIManager/GlobalConfigDrawer/AutoCheckScriptTemplatesExist 命名空间修复 |
| **主题 2**: 命名规范 | ✅ 完成 | isSuccessed → isSucceeded (BuildTool/Event.cs/UpdatePanel) |
| **主题 3**: using/IDisposable 资源释放 | ✅ 完成 | IOHelper.IsOccupied/NetworkCommands/TextAssetEditor |
| **主题 4**: async/await 同步包装 | ✅ 已完成 | NoticerBase/UpdaterBase 改为 UniTask, 独立方法改为 UniTask + .Forget() |
| **主题 5**: ScriptableObjectSingleton 访问权限 | ✅ 完成 | 审计后确认不存在 OnEnable 调用 GetInstance 的 Critical 问题 |
| **主题 6**: 线程安全 | ✅ 已完成 | ServerCommands/HtmlToUGUIBaker volatile 已添加 |
| **主题 7**: 文档与日志 | ✅ 已完成 | Log 路径错误已修复，IntervalLooper catch 已补充日志 |

---

## 五、修复优先级建议

1. **P0 (立即修复):** 1.1 Log 路径映射错误 — 只需改两行，影响线上日志排查
2. **P1 (尽快修复):** 1.2 ServerCommands 竞态条件、1.3 HtmlToUGUIBaker 跨线程 — 加 `volatile` 即可
3. ✅ **P2 (已完成):** 2.1 async void 改造 — NoticerBase/UpdaterBase 改 UniTask，调用方加 .Forget()
4. **P3 (有空再做):** 2.2 WebGL GetAwaiter 文档标注、2.3 Log.cs using 修复
5. **P4 (技术债):** 3.1 TODO 清理、3.2 XML 注释补充、3.3 IntervalLooper catch
