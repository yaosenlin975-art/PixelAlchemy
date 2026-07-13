# NoitaCA 美术管线与 VFX 规范（Art Pipeline & VFX Spec）

> **文档版本**：v1.0
> **创建日期**：2026-07-07
> **状态**：草稿 / 待评审（MVP 落地基线）
> **归属学科**：技术美术（TA）
> **Owner**：柯桥（TA）；协同：苏立（美术）；资产加载：YooAsset
> **上游锁定文档**：`架构决策记录.md` v1.0（§3/§7 确定性红线）、`美术风格指南.md` v1.0（§2/§3/§5）、`协议与序列化规范.md` v1.0（§3/§6.1/§8）、`游戏设计文档.md` v1.0（§6/§7）
> **技术栈**：Unity `6000.3.19f1` + HybridCLR `8.12.0` + DOTS ECS `1.4.x`（Burst Job）+ Fantasy.Unity `2025.2.1402` + YooAsset `2.3.19` + URP 2D `17.4.0`
> **强约束声明**：本规范所有内容必须服从 `美术风格指南.md §2` 的**确定性渲染硬约束**——像素颜色可编码进 `PixelData.Color`（`ushort` 打包 **RGB565**，16 位）；**无 bloom、无随机抖动（dither）/ 屏幕随机噪声、禁用依赖客户端时间的随机**。渲染与音频是**纯表现层**，绝不进入 `InputPayload`（对齐 `ADR §7` 与 `协议与序列化规范.md §2/§8`）。

---

## 0. 范围与引用速查

| 条目 | 本规范定义 | 权威上游 |
|------|-----------|---------|
| 像素色域 | RGB565（R5 G6 B5），16 位 | `美术风格指南.md §2.1` |
| 12 材料色表 | §1.2 调色板白名单 | `美术风格指南.md §3`（含 `0x514A`=10,5,10 已修正） |
| 像素→Color32 展开 | Burst Job 整数展开 | `美术风格指南.md §3` 末段 + `玩法重构方案.md §4.3.2 RenderBridgeSystem` |
| Telegraph 帧时机 | §3.1 | `美术风格指南.md §5` + 主架构 `§4.5.5`（TargetFrame−90）+ `G2C_SceneEvent`（主架构 `§6.3`） |
| 确定性红线 | §7 | `ADR §3/§7`、`协议与序列化规范.md §2/§8`、主架构 `§8.2/§8.3` |
| 像素字体缺失降级 | §4 | `ADR §8` 行动项 `#5`（owner：柯桥协调苏立，经 YooAsset） |
| 资源版本对齐 | §1.3/§4 | 主架构 `§4.5.3`（影响玩法资源全房一致） |

> 本规范是美术资产与 VFX 落地的**唯一管线事实源**。与 `美术风格指南.md` 冲突时以本文对"管线/导出/工具"的细化为准，调色板仍以 `美术风格指南.md §3` 为唯一取色源。

---

## 1. 像素资产管线（Pixel Asset Pipeline）

### 1.1 像素密度与尺寸约定（Pixel Density & Sizing）

| 维度 | 约定 | 理由 / 引用 |
|------|------|------------|
| **世界↔纹理** | 1 个模拟像素 = 1 个纹理 texel（1:1 烘焙） | `PixelData` 逐像素网格（`玩法重构方案.md §4.1.1`），`RenderBridgeSystem` 直接写 `NativeArray<Color32>`（`§4.3.2`） |
| **屏幕缩放** | 整数倍缩放（2× / 3× / 4×，按设备档位），**禁止分数缩放** | 分数缩放引入重采样伪影 + 非确定性插值；整数 nearest 保证跨端一致 |
| **默认竞技场** | 256×256 像素（GDD §8 默认），最大 1024×1024 | `ArenaConfig.Width/Height ∈ [64,1024]`（`GDD §8`） |
| **材质像素** | 1×1（逐像素由 CA 落沙写入，无 sprite） | 材料即颜色，见 `美术风格指南.md §4` |
| **玩家小人** | 8（宽）× 12（高）像素，4–8 色 | `美术风格指南.md §6`（简洁像素小人，颜色区分房主/排名） |
| **法术图标 / 道具图标** | 16×16（最小），推荐 24×24 | `美术风格指南.md §6`；进 YooAsset（§1.3） |
| **生物** | 16×16 圆润块，眼睛提示 AI 状态 | `美术风格指南.md §6` |
| **UI 图标 / 表情** | 16×16 扁平，1–2 色 | `美术风格指南.md §6/§7` |
| **图块（装饰 chunk）** | 16×16 对齐 grid（仅地形装饰，非材质） | 与 CA 16×16 chunk 对齐（`玩法重构方案.md §4.2.5` chunk 排序） |

**尺寸纪律**：
- 所有 sprite 尺寸必须为 **偶数** 且对齐 2 的幂或 16 对齐（便于合批 / 图集）。
- 禁止非整数旋转/位移美术；旋转类表现（如玩家朝向）以**预渲染 4/8 向帧**或整数帧翻转实现，禁用运行期浮点旋转（破坏确定性）。
- 导出分辨率即最终像素分辨率，**不**在引擎内二次缩放（缩放只在相机整数倍）。

### 1.2 调色板纪律（Palette Discipline / RGB565 强制）

#### 1.2.1 RGB565 编码规则

- 格式：**R5 G6 B5**（共 16 bit，`ushort`）。精度：R/B 各 32 级（0–31），G 64 级（0–63）。
- 所有像素颜色必须是下表 **12 个 RGB565 常量之一**（取自 `美术风格指南.md §3`，已含 `0x514A`=10,5,10 修正）。**美术资产不得引入板外色**（`美术风格指南.md §8` checklist）。

| 索引 | 语义别名 | 材质 | RGB565(hex) | R,G,B(0–31/0–63/0–31) | 用途约束 |
|------|---------|------|------|------|------|
| 0 | `Sand` | 沙 | `0xFF90` | 31,52,16 | 暖黄沙 |
| 1 | `Water` | 水 | `0x029F` | 0,20,31 | 深蓝 |
| 2 | `Stone` | 石 | `0xA294` | 20,20,20 | 中性灰（UI 中性色） |
| 3 | `Wood` | 木 | `0xE648` | 28,18,8 | 棕褐 |
| 4 | `Fire` | 火 | `0xF900` | 31,8,0 | **危险/警示语义**（边界红光 + AoE Telegraph） |
| 5 | `Smoke` | 烟 | `0x9264` | 18,18,18 | 浅灰 |
| 6 | `Steam` | 蒸汽 | `0xB3DF` | 22,30,31 | 近白青 |
| 7 | `Ice` | 冰 | `0x73FF` | 14,31,31 | **安全语义**（UI 安全色） |
| 8 | `Lava` | 熔岩 | `0xF9E0` | 31,15,0 | 炽橙 |
| 9 | `Acid` | 酸 | `0x7BE0` | 15,31,0 | **拾取/道具 Telegraph 语义** |
| 10 | `Poison` | 毒 | `0x63E0` | 12,31,0 | 毒绿 |
| 11 | `Ash` | 灰烬 | `0x514A` | 10,5,10 | **禁用/降级语义**（UI 灰） |

> **白名单 = 上述 12 个值**。UI / Telegraph / 状态色均为对 12 色之一的**语义别名**（如 `UI_Danger = Fire(4)`、`Telegraph_Loot = Acid(9)`、`UI_Safe = Ice(7)`、`UI_Disabled = Ash(11)`），不新增任何板外 hex。

#### 1.2.2 索引约束（Palette Index）

```csharp
// NoitaCA.Renderer/Palette.cs  (AOT, blittable)
// 索引以 美术风格指南.md §3 为唯一取色源；语义别名仅映射，不新增色值
public enum PaletteIndex : byte
{
    Sand   = 0,  Water = 1,  Stone = 2,  Wood = 3,
    Fire   = 4,  Smoke = 5,  Steam = 6,  Ice  = 7,
    Lava   = 8,  Acid  = 9,  Poison = 10, Ash = 11,
    // 语义别名（值复用上述 12 色，不扩展色域）
    UI_Danger     = Fire,    // 0xF900 边界红光 + AoE/Hazard Telegraph
    Telegraph_Hazard = Fire, // 0xF900 Bomb/Lava/Poison/Blink 落点
    Telegraph_Loot   = Acid, // 0x7BE0 道具刷新 Telegraph
    UI_Safe       = Ice,     // 0x73FF 安全
    UI_Disabled   = Ash,     // 0x514A 禁用
    UI_Neutral    = Stone,   // 0xA294 中性
}
```

- 任何美术资产导出的每个唯一颜色，其 RGB565 值必须 ∈ `{0xFF90,0x029F,0xA294,0xE648,0xF900,0x9264,0xB3DF,0x73FF,0xF9E0,0x7BE0,0x63E0,0x514A}`。
- “明暗变体”仅允许材质 §4 定义的**至多 1 个预定义变体**，且变体必须在导出时即固化为确定 RGB565 值（非运行时随机/噪声）。

#### 1.2.3 违规检查手段（Violation Detection，三道关卡）

1. **导出期（DCC 插件，Aseprite/Photoshop 脚本）**：导出时强制 Quantize→12 色调色板；非板内色被吸附（snap）到最近板内值并打 warning 日志。艺术家在 DCC 内即可看到越界像素高亮。
2. **CI / 资产校验（TA 提供 `Tools/PaletteValidator`，Python + PIL）**：读取仓库内所有 `*.png` 资产，解码像素，提取唯一色，逐色比对白名单；输出 `violations.json`（含 文件/坐标/越界 hex），CI 失败阻断合并。脚本放置于 `Tools/PaletteValidator/`，TA 维护。
3. **运行时（开发版 assert，Burst 安全）**：`RenderBridgeSystem` 展开前对 `PixelData.Color` 做 `IsValidRgb565(color)` 校验（查 12 值 LUT，O(1)）；开发构建下越界即 `Debug.LogError`+上下文，发布构建静默吸附到最近板内值（保不崩，但需 CI 兜住）。

```csharp
// 12 值 LUT（编译期常量，Burst 兼容）
static readonly ushort[] PALETTE = {0xFF90,0x029F,0xA294,0xE648,0xF900,0x9264,
                                    0xB3DF,0x73FF,0xF9E0,0x7BE0,0x63E0,0x514A};
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsValidRgb565(ushort c)
{
    for (int i = 0; i < 12; i++) if (PALETTE[i] == c) return true;
    return false;
}
```

### 1.3 导出格式（Export Format：PNG 索引色 + .meta）

- **源文件**：DCC 内以 **8-bit 索引色（Indexed Color）PNG** 产出，调色板严格为 §1.2.1 的 12 色（顺序固定，索引 0–11）。
- **.meta 强制字段**（Unity 导入元数据，TA 提供模板 `Tools/PaletteValidator/PixelSprite.meta.template`，美术导入时套用）：

| 字段 | 值 | 说明 |
|------|----|------|
| `textureType` | `Sprite` | 像素精灵 |
| `spritePixelsPerUnit` | `1` | 1 像素 = 1 单位（对齐 1:1 世界像素） |
| `filterMode` | `Point` | **禁止 Bilinear/Trilinear**（保硬边、确定性） |
| `mipmapEnabled` | `false` | 像素艺术不做 mip |
| `sRGBTexture` | `false` | 已是最终 RGB565，不做色彩管理重映射 |
| `alphaSource` | `None` 或 `FromInput`（图标用） | 材质像素无 alpha；图标可用 1-bit alpha |
| `compression` | `None`（或 `BC7` 仅限非像素 UI 图集） | 像素资产零压缩保真 |
| `maxTextureSize` | 等于原生尺寸 | 禁止引擎降采样 |
| `wrapMode` | `Clamp` | 防边缘采样伪影 |
| `allowedInAtlases` | `true`（仅图标/UI） | 材质像素不入图集（逐像素写入） |

- **资产组织**：材质像素**不入图集**（由 CA 逐像素写 `PixelData.Color`）；仅**图标/UI/玩家/生物 sprite** 进图集（YooAsset 分组）。
- **YooAsset 分组**：
  - `Art_PixelSprites`（玩家/生物/图标/UI，影响可读性的 UI 一致性资源）→ 版本对齐（主架构 `§4.5.3`）。
  - 材质调色板常量（`Palette.cs`）为代码常量，随 Hotfix.dll 走 HybridCLR，不走 YooAsset 资源流。
- **热更版本对齐**：图标/字体等“影响可读性”的资产纳入 YooAsset 版本清单（`AssetManifestHash`），对局前校验全房一致（主架构 `§4.5.3`）；加载失败兜底见 `§4.3` 与 `§1.3` 占位 prefab（主架构 `§4.5.5`/ checklist `§1174`）。

---

## 2. 着色器 / 渲染约束清单（Shader & Render Constraints）

### 2.1 禁用项（破坏确定性 / 越界色域，一律禁止）

| 禁用 | 原因 | 引用 |
|------|------|------|
| Bloom / 泛光 | 依赖浮点 HDR 与跨平台发散的阈值 | `美术风格指南.md §2.2` |
| 后处理随机抖色 / dither 噪声 | 每客户端随机不同 → desync | `美术风格指南.md §2.2`、ADR 确定性红线 |
| 每帧基于 `Time.deltaTime` / `Time.time` 的色相/亮度偏移 | 非确定性（架构 `§8.2` 禁用 `Time.deltaTime`） | `美术风格指南.md §2.2`、主架构 `§8.2` |
| 屏幕空间随机噪声（film grain / scanline jitter） | 客户端时间相关随机 → desync | ADR §7 确定性红线 |
| 浮点后处理调色（tonemap / LUT 浮点采样） | 跨平台浮点发散 | `美术风格指南.md §2.2` |
| 超出 16 位的 HDR / 32 位色 | 无法打包进 `ushort Color` | `美术风格指南.md §2.2` |
| 运行期浮点旋转/缩放像素 sprite | 重采样伪影 + 非确定 | 见 §1.1 |

### 2.2 允许项

- 固定的 16 位调色板（§1.2）。
- Telegraph / 动效：**确定性程序化动画**，基于**整数帧计数**（非浮点），见 `美术风格指南.md §2.3` 与本文 §3。
- 周期调和 100ms 视觉插值（主架构 `§8.3` / `§4.2.3`）：使用**确定性线性插值**（以逻辑帧为基准的固定窗口），非 `Time`。
- 阶梯式透明度（每 N 帧一档），确定性。
- URP 2D Renderer 仅保留：Sprite Render（Point 采样）、无后处理栈（或仅确定性 2D 排序）。**禁止**添加 Post-processing Volume（Bloom/Vignette/ColorLUT）。

### 2.3 Burst Job 内 RGB565→Color32 边界（强制）

像素颜色在模拟态是 `PixelData.Color`（`ushort` RGB565）。渲染由 `RenderBridgeSystem`（`[UpdateInGroup(PresentationSystemGroup)]`，`玩法重构方案.md §4.3.2`）在 **Burst Job** 内展开为 `Color32`，再于主线程唯一同步点 `PixelWorldRenderer.Update` 经 `Texture2D.SetPixels32` 上传（`玩法重构方案.md §4.3.1`）。

**展开为整数运算（禁止浮点）**，标准 5→8 / 6→8 位扩展：

```csharp
// NoitaCA.Renderer/RenderBridgeSystem.cs  (Burst Job 内)
// 引用 美术风格指南.md §3 材料色表：Color 为 RGB565 ushort
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Color32 Rgb565ToColor32(ushort c)
{
    // 整数移位展开，无浮点；Burst FloatMode.Strict 下完全一致
    int r5 = (c >> 11) & 0x1F;          // 5 bit
    int g6 = (c >> 5)  & 0x3F;          // 6 bit
    int b5 =  c        & 0x1F;          // 5 bit
    byte r = (byte)((r5 << 3) | (r5 >> 2));   // 0..31 -> 0..255
    byte g = (byte)((g6 << 2) | (g6 >> 4));   // 0..63 -> 0..255
    byte b = (byte)((b5 << 3) | (b5 >> 2));
    return new Color32(r, g, b, 255);
}
```

- **边界纪律**：RGB565→Color32 是唯一“16 位→8 位”展开点，且**只在 `RenderBridgeSystem` 的 Burst Job 内**发生；模拟态其它地方不得出现 `Color32`/`Color`（避免色域泄漏）。
- 展开前先过 `IsValidRgb565`（§1.2.3）；非法值开发版报错、发布版吸附。
- 256×256 展开 = 65536 次整数运算，Burst 下预算 **< 1ms/帧**（主线程仅 1 次 `SetPixels32`）。

### 2.4 性能预算（Pixel Art 基线）

| 指标 | 预算（MVP） | 备注 |
|------|------------|------|
| 像素世界 DrawCall | **1**（单张 `Texture2D`） | 256×256 = 256KB `NativeArray<Color32>`（`GAMEPLAY §4.3.1`） |
| VFX 叠加 DrawCall | ≤ 8（Telegraph/命中/死亡） | 确定性精灵，不入像素网格 |
| 像素展开耗时 | < 1 ms/帧（Burst） | `RenderBridgeSystem` |
| 图标/UI 图集 | ≤ 2 张 1024² | YooAsset 分组加载 |
| 字体图集 | ≤ 1 张 1024²（P0） | 见 §4 |
| 后处理 | 0（无 Bloom/无 LUT） | 保确定性 |

---

## 3. VFX 规范（Visual Effects）

> **铁律**：所有 VFX 由**确定性逻辑事件**驱动，分两类来源，二者均不进入 `InputPayload`：
> 1. **服务器授权事件**：`G2C_SceneEvent`（ProtoBuf Route，主架构 `§6.3`；帧延迟解密 `§6.5`）→ `TargetFrame` 全客户端已知 → 确定。
> 2. **确定性模拟事件**：模拟在 Burst Job 内写入 `VfxTrigger` 队列（携带 `type + frame + 像素坐标 + 参数`），因模拟处于锁步，队列内容与顺序全客户端一致 → 确定。
>
> VFX **绝不**读取客户端 `Time`、绝不使用客户端随机；其帧时机全部来自**逻辑帧整数计数**（30Hz）。

### 3.1 Telegraph 落点光圈（固定色 + 形状 + 帧对齐）

Telegraph 是“可读性混乱”的基石（`GDD §2` 体验支柱）。所有 Telegraph **形状固定（垂直光柱 + 落点圆环），颜色取自白名单语义色，动画由整数帧驱动（阶梯式，每 15 帧一档）**，禁用 `Time.deltaTime`（`美术风格指南.md §5`）。

#### 3.1.1 道具刷新 Telegraph（ItemSpawn，源自 `G2C_SceneEvent`）

- 触发：`G2C_SceneEvent{Type=ItemSpawn, ItemSpawn{ItemId,PosX,PosY,TelegraphFrames=90}}`（主架构 `§6.3` / `§4.5.5`）。
- 帧时机（以 `TargetFrame` 为锚，`ITEM_TELEGRAPH_LEAD = 90` 帧 = 3s@30Hz）：

| 阶段 | 帧区间（相对 TargetFrame） | 表现 |
|------|---------------------------|------|
| 解密/加载 | `−90`（收到即解密，`§6.5` 帧延迟） | YooAsset 异步加载 prefab（UniTask，`§4.5.5`） |
| 光柱渐亮 | `[−90, −30)`（60 帧） | 垂直光柱，亮度 0→满，**每 15 帧一档**（4 档阶梯） |
| 落点圆环 | `[−30, 0)`（30 帧） | 落点圆环脉动（每 15 帧明暗交替） |
| 道具落地 | `= 0`（TargetFrame） | 实例化道具本体，光柱/圆环消失 |

- 颜色：`Telegraph_Loot = Acid(0x7BE0)`（绿，拾取语义，`美术风格指南.md §5` 建议）。
- 形状：固定 1×N 垂直光柱（N = 落点至屏幕顶整数像素）+ 固定半径圆环（半径 = 道具影响半径，整数像素）。

#### 3.1.2 AoE / 落点 Telegraph（Bomb / Lava / Poison / Blink，源自确定性模拟事件）

由施法确定性推导：法术在 `Attack` + `SelectedSpellSlot` + `AimAngle`（均来自 `InputPayload`）下施放，弹道/落点由 `Fix64` 物理确定性计算（`GAMEPLAY §4.2.5` 数学约束）→ 落点像素 + 影响帧全客户端一致 → 模拟写入 `VfxTrigger{Telegraph_Aoe, F_impact, pos, radius, color}`。

| 法术 | 归属（ADR §7） | 颜色（语义） | 预告提前量 | 表现 |
|------|---------------|------------|-----------|------|
| **Bomb** 炸弹 | P0 | `Telegraph_Hazard = Fire(0xF900)` | `lead = 24` 帧 | 落点圆环 + 短光柱，命中帧写爆炸像素 |
| **Lava** 熔岩喷发 | P1 | `Telegraph_Hazard = Fire(0xF900)` | `lead = 30` 帧 | 落点圆环脉动，触发写熔岩像素 |
| **Poison** 毒弹 | P1 | `Telegraph_Hazard = Fire(0xF900)` | `lead = 20` 帧 | 落点圆环，触发写毒像素 |
| **Blink** 闪现 | P1 | `Telegraph_Hazard = Fire(0xF900)` | `lead = 12` 帧 | 目标格圆环（自指向，短预告），触发瞬移 |

> 注：Bomb 为 P0 必做；Lava/Poison/Blink 为 ADR §7 的 P1 法术，其 Telegraph 列入 **P1 清单（§6）**。AoE 半径/落点全部为确定性整数像素，无客户端随机。
> 边界红光警示（GDD §4.2 “边界红光警示”）亦用 `UI_Danger = Fire(0xF900)`，与 Hazard Telegraph 同色但**位置在边界**且**常驻**（非落点圆环），靠位置+形态区分，不引新色。

### 3.2 命中 / 击退 / 出界死亡 表现层特效

全部为**纯表现层**（不影响模拟哈希），绑定确定性模拟事件，整数帧驱动，无 client-time 随机。

| 特效 | 触发确定性事件 | 帧时机 | 表现（固定色+形状） |
|------|---------------|--------|-------------------|
| **命中 HitImpact** | 弹道/范围写入像素成功（模拟判定命中） | `F_hit` 起 8 帧 | 目标点固定星芒（1–2 px 亮点，板内色），阶梯透明度 |
| **击退 Knockback** | `PlayerMovementJob` 击退模型生效（ADR §1 边缘 15–55px 推人） | `F_knock` 起 10 帧 | 沿击退向量方向的确定性拖影（方向来自 `Fix64` 向量，非随机），用 `UI_Danger` |
| **出界死亡 PlayerOut** | `PlayerMovementJob` 边界判定 `PlayerDeath`（GDD §4.2 / ADR §1） | `F_death` 起 30 帧 | 死亡迸发：以 `PlayerId` 为固定种子的**确定性图案**（环形+放射，整数帧步进）；位置取死亡瞬间像素坐标（模拟已知） |
| **Dash 拖影** | `ActionFlags.Dash` 边沿（ADR §3，无 i-frames） | `F_dash` 起 5 帧（≈0.16s） | 沿 Dash 方向的 3 帧残影（复用玩家色），确定性 |

> **死亡迸发确定性说明**：禁用客户端 `Random`；图案由 `Xorshift128Plus(seed = PlayerId)`（与模拟同源 PRNG，`GAMEPLAY §4.2.5`）在**表现层**生成粒子初值——因 `PlayerId` 全客户端一致，图案一致。若担心表现层 PRNG 与模拟耦合，可改为**纯固定图案**（无需 PRNG）。MVP 采用固定图案（最简、零风险）。

### 3.3 触发事件枚举与帧时机（VfxTrigger）

```csharp
// NoitaCA.Renderer/VfxTrigger.cs  (AOT, blittable; 仅表现层，绝不进 InputPayload)
public enum VfxType : byte
{
    None            = 0,
    TelegraphItem   = 1,  // 源自 G2C_SceneEvent.ItemSpawn（TargetFrame 已知）
    TelegraphAoe    = 2,  // Bomb/Lava/Poison/Blink 落点（模拟推导 F_impact）
    HitImpact       = 3,  // 命中
    Knockback       = 4,  // 击退
    PlayerOut       = 5,  // 出界死亡
    DashTrail       = 6,  // Dash 拖影
}

// 模拟在 Burst Job 写入；表现系统（PresentationSystemGroup）消费
public struct VfxTrigger
{
    public VfxType  Type;
    public int      TriggerFrame;   // 30Hz 逻辑帧（确定性锚点）
    public int      PosX;           // 像素坐标（Fix64×100 取整）
    public int      PosY;
    public ushort   Color;          // RGB565（必须 ∈ 白名单）
    public byte     Param;          // 半径/方向等（整数）
}
```

**事件来源矩阵**（明确“确定性来自哪”）：

| VfxType | 来源 | 确定性锚点 | 是否进 InputPayload |
|---------|------|-----------|--------------------|
| `TelegraphItem` | 服务器 `G2C_SceneEvent` | `TargetFrame`（全客户端一致） | 否（网络事件，非输入） |
| `TelegraphAoe` | 模拟施法（InputPayload 派生） | `F_impact`（锁步推导） | 否（由 InputPayload 推算，但事件本身不入流） |
| `HitImpact`/`Knockback`/`PlayerOut`/`DashTrail` | 模拟状态变化 | 逻辑帧（锁步） | 否 |

> 与 `协议与序列化规范.md §2` 对齐：渲染/音频为纯表现，凡进 `InputPayload` 的字段（MoveX/Y、ActionFlags、AimAngle）才参与状态哈希；VfxTrigger 不入流、不污染哈希、不导致 desync（`§2` 红线）。

---

## 4. 像素字体资产规范 + YooAsset 归属（ADR §8 行动项 #5）

> **行动项**：`ADR §8 #5` —— 像素字体资产（中文点阵 + 等宽像素数字）owner 指派：**柯桥(TA) 协调 苏立(美术) 经 YooAsset 提供**。本文固化其规格与归属。

### 4.1 点阵尺寸（Glyph Metrics）

| 字符类 | 点阵 | 行高/字宽（渲染 px） | 备注 |
|--------|------|--------------------|------|
| ASCII 可打印（0x20–0x7E，95 字） | **8×8** 基准，渲染 8×8（或 2× 整数放大） | 8×8 / 等宽 8 | 等宽，数字 0–9 严格等宽 |
| 数字 0–9（HUD/计时） | **8×8** 等宽，专用 `DigitSet` | 8×8 / 字宽恒 8 | 计分/倒计时无抖动 |
| CJK 常用汉字（P0 子集） | **16×16** | 16×16 | 清晰可读下限 |
| CJK 扩展（P1） | 16×16（同尺寸，扩字符集） | 16×16 | — |
| 标点 / 全角符号 | 16×16（与 CJK 同格） | 16×16 | 全角对齐 |

- **等宽约束**：ASCII 与数字必须等宽（HUD 数字不跳动）；CJK 用 16×16 定宽格。
- **渲染缩放**：随相机整数倍（2×/3×/4×），禁止分数缩放（对齐 §1.1）。
- **颜色**：字形为 1-bit 掩码（on/off），渲染色取白名单语义色（`UI_Neutral=Stone` 默认文本，`UI_Danger=Fire` 警告文本，`UI_Safe=Ice` 安全文本）。**字形本身不带色，仅掩码 + 调色板着色** → 不引入板外色。

### 4.2 字符集（Charset）

| 集合 | 范围 | P0 / P1 | 用途 |
|------|------|---------|------|
| ASCII 可打印 | 0x20–0x7E（95） | **P0 全量** | UI 标签、数字、英文 |
| 数字等宽集 | 0–9 | **P0** | 计时/计分/MMR |
| CJK 核心子集（P0） | 策划提供《UI/常用名 高频字表》（目标 700–1000 字，覆盖 GDD/UI 文案 + 常见玩家名） | **P0** | 中文 UI + 玩家名 |
| CJK 扩展（P1） | GB2312 一级字（3755）动态增补 | **P1** | 全量中文（聊天/长文本） |
| 标点/全角 | 常用 30–50 个 | **P0** | 中文排版 |

> P0 CJK 子集由**策划（谋远）+ 产品（许清楚）**提供高频字表，TA 与美术据此烘焙；超出子集的字符走 §4.3 降级。**所有客户端字表在构建期固化**，故降级行为确定（同码 → 同降级）。

### 4.3 缺失降级策略（Missing Glyph Fallback，确定性）

- **第一级**：字符在已烘焙字表内 → 正常显示。
- **第二级（缺失 CJK）**：渲染固定占位字形 `□`（以 `UI_Disabled=Ash(0x514A)` 绘制，16×16 方框），**绝不**因缺失而崩溃或引板外色。
- **第三级（极端）**：若整字体资产 YooAsset 加载失败 → 主架构 `§4.5.5`/ checklist `§1174` 的**占位 prefab** 机制兜底：文本区显示固定 `□` 占位 + 错误上报，**对局不中断**。
- **确定性保证**：降级是“字符码 → 固定占位”的纯查表逻辑（无随机、无 `Time`），全客户端一致；且字表经 YooAsset 版本对齐（§4.4），正常局所有客户端字表相同，降级仅在异常客户端发生（仅影响该端可读性，不影响模拟/公平）。

### 4.4 YooAsset 归属与版本对齐

| 项 | 约定 |
|----|------|
| 资产形态 | 字体图集纹理（RGB565 掩码/调色板）+ 字符宽度/偏移元数据（JSON/二进制） |
| 打包分组 | `Art_PixelFont`（YooAsset 资源包） |
| 加载方式 | 对局前经 YooAsset 加载（`AssetManifestHash` 校验，主架构 `§4.5.3`），UniTask 异步 |
| 版本对齐 | 与图标同属“影响可读性资源”，纳入版本清单全房一致校验；不匹配→拒绝进对局（主架构 `§4.5.3`/ `§4.5.6` 灰度） |
| Owner 链 | 柯桥(TA) 定规格/工具 → 苏立(美术) 产出点阵 → YooAsset 打包 → 客户端加载 |
| 热更 | 字体变更随 YooAsset 资源热更（对局外生效，避免 desync，主架构 `§4.5`） |

---

## 5. 与上游文档的引用关系（Cross-Reference）

| 本规范章节 | 引用文档 | 关系 |
|-----------|---------|------|
| §1.2 调色板 | `美术风格指南.md §3`（12 材料 RGB565，含 `0x514A`=10,5,10 修正） | 唯一取色源，本文细化索引/校验 |
| §1.1/§2.3 | `美术风格指南.md §2`（确定性渲染硬约束）、§4（材料表现）、§5（Telegraph 风格）、§8（checklist） | 落实为管线/导出/工具 |
| §2.3 | `美术风格指南.md §3` 末段 + `玩法重构方案.md §4.3.2 RenderBridgeSystem` | Burst Job RGB565→Color32 边界 |
| §3.1.1 | `协议与序列化规范.md §3/§6.1`（双序列化）、主架构 `§6.3 G2C_SceneEvent` / `§6.5` 帧延迟解密 / `§4.5.5` | Telegraph 由服务器事件驱动 |
| §3 | `协议与序列化规范.md §2/§8`（渲染/音频纯表现、不进 InputPayload、ValidateInput） | VfxTrigger 不污染哈希 |
| §3.1.2/§3.2 | `ADR §1`（边界死亡/击退模型）、`§3`（Dash 无 i-frames）、`§7`（法术 P0/P1 归属） | Telegraph/AoE/死亡特效绑定确定性逻辑 |
| §3 帧时机 | `游戏设计文档.md §6`（玩家能力/法术）、`§7`（道具刷新 Telegraph 3s）、`§4.2`（边界死亡） | 帧时机对齐玩法定义 |
| §4 | `ADR §8 #5`（像素字体 owner 指派） | 收口未结行动项 |
| §1.3/§4.4 | 主架构 `§4.5.3`（资源版本对齐）、`§4.5.5`（YooAsset 与场景系统）、`构建与部署.md §4`（清单 Hash） | 资产归属与热更边界 |
| §2/§7 | `ADR §3/§7`（确定性红线）、主架构 `§8.2`（确定性清单：Fix64/禁用 Time.deltaTime）、`§8.3`（周期调和插值） | 渲染约束的权威依据 |

---

## 6. MVP 落地清单（P0 必做 / P1 后做）

### P0（MVP 必做，可落地）

| # | 交付物 | 责任 | 验收 |
|---|--------|------|------|
| P0-1 | 12 色调色板白名单 + `Palette.cs`（§1.2）+ `IsValidRgb565` | TA | 12 值 LUT 入 Burst Job；CI 通过 |
| P0-2 | `Tools/PaletteValidator`（Python+PIL）违规检查 + `PixelSprite.meta` 模板（§1.2.3/§1.3） | TA | CI 拦下任意板外色 PNG |
| P0-3 | `RenderBridgeSystem` RGB565→Color32 整数展开（§2.3） | TA 校验 / 引擎 | Burst 下 <1ms；无浮点 |
| P0-4 | URP 2D Renderer 去后处理（无 Bloom/LUT/dither）（§2.1/§2.4） | TA | 渲染栈零 Post-processing Volume |
| P0-5 | 道具刷新 Telegraph（光柱+落点圆环，90 帧，`G2C_SceneEvent` 驱动）（§3.1.1） | TA+客户端 | 全客户端同帧同形同色 |
| P0-6 | **Bomb** AoE Telegraph + 落点圆环（§3.1.2，P0 法术） | TA | 落点由弹道确定性推导 |
| P0-7 | 命中/击退/出界死亡/Dash 拖影 VFX（§3.2）+ `VfxTrigger` 枚举（§3.3） | TA | 全为确定性事件驱动，无 client-time |
| P0-8 | 像素字体 P0（ASCII 全量 + 数字等宽 + CJK 核心子集 700–1000）+ YooAsset 打包 + 缺失降级（§4） | TA 协调 苏立 | 字表固化；异常降级确定 |
| P0-9 | 像素资产尺寸纪律落地（玩家 8×12 / 图标 16×16 / 整数缩放）（§1.1） | 苏立(美术)+TA | DCC 模板 + 校验通过 |

### P1（MVP 后）

| # | 交付物 | 责任 |
|---|--------|------|
| P1-1 | Lava / Poison / Blink Telegraph（§3.1.2，ADR §7 P1 法术） | TA |
| P1-2 | CJK 扩展字表（GB2312 一级动态增补）+ 运行时字形注入（§4.2） | TA+苏立 |
| P1-3 | 音频钩子：确定性 `VfxTrigger` → SFX（纯表现、不进 InputPayload） | TA+音频(协同) |
| P1-4 | Unity 调色板 LUT 编辑器工具（美术自助取色，防越界） | TA |
| P1-5 | 相机缩放档位 + 大地图(1024) LOD/图集优化（§1.1/§2.4） | TA |
| P1-6 | Telegraph 多形态（非圆形落点：矩形/线形 AoE）扩展形状库 | TA+苏立 |

---

## 7. 确定性红线自检（对齐 ADR §3 / §7）

- [x] 所有像素颜色 ∈ 12 色白名单（RGB565），无板外色（§1.2）。
- [x] 无 Bloom / 无 dither / 无屏幕随机噪声 / 无 `Time.deltaTime` 色偏移（§2.1）。
- [x] RGB565→Color32 仅在 `RenderBridgeSystem` Burst Job 内整数展开（§2.3）。
- [x] Telegraph/VFX 由整数帧驱动，阶梯式，非浮点（§3）。
- [x] 渲染/音频纯表现层；VfxTrigger 不进 `InputPayload`、不污染状态哈希（§3.3 / ADR §7）。
- [x] 无依赖客户端时间的随机噪声；死亡迸发用固定图案或同源 `Xorshift128Plus(seed=PlayerId)`（§3.2）。
- [x] 图标/字体经 YooAsset 版本对齐，影响可读性资源全房一致（§1.3/§4.4）。
- [x] 调色板常量随 Hotfix.dll（HybridCLR），不与像素资产混流（§1.3）。

---

**文档结束** — 本规范为 NoitaCA 像素资产管线与 VFX 的 MVP 落地基线，所有条款服从 `美术风格指南.md` 确定性渲染硬约束与 `ADR §3/§7` 确定性红线。
