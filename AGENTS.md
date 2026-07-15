# PixelAlchemy — 全局 AI Agent 指令

> 此文件供所有 AI 编码助手（Claude Code、Cursor、WindSurf 等）在进入项目时自动读取。
> 请严格遵守以下规范，除非有明确理由偏离并已与开发者确认。

---

## 1. 项目概要

| 项目 | 值 |
|------|-----|
| 原名 | NoitaCA — 像素落沙对战游戏 |
| 技术栈 | Unity 6000.3.19f1 + DOTS ECS + HybridCLR + YooAsset |
| 服务端 | Fantasy-Net + .NET 8 |
| 模式 | 2-8 人帧同步像素落沙对战 |
| 关键原则 | **确定性优先** — 所有影响世界状态的操作必须收敛为 30Hz InputPayload |

设计文档统一存放于根目录 `Docs/`，规格文档位于 `.trae/specs/`。**约定：所有设计 / 规格 / 计划类 `.md` 仅允许放在根 `Docs/`（及 `.trae/specs/`），`Client/Assets/Docs/` 不再存放任何 `.md` 文档**（2026-07-15 已将 `DOTS重构设计文档.md` 从 `Client/Assets/Docs/` 迁入根 `Docs/`）。

---

## 2. C# 编码规范

### 2.1 命名规范

| 类别 | 规则 | 示例 |
|------|------|------|
| 类 / 结构体 / 枚举 / 方法 | 大驼峰 PascalCase | `PlayerController`, `Fix64Vec2` |
| 字段 / 属性 | 小驼峰 camelCase | `playerHealth`, `movementSpeed` |
| 字段 vs 属性命名冲突 | 属性用大驼峰，兼容现有写法 | — |
| const 常量 | 全大写 + 下划线分隔 | `OBJECT_CAMERA_VISIBLE_LAYER` |
| private 修饰符 | **不可省略** | `private int health;` |
| 禁止前缀 | 禁止 `m_`、`_xxx` 等私有前缀 | ❌ `m_health` → ✅ `health` |

**例外**：反射/序列化的外部字段、数字开头的特殊命名、第三方示例代码可保留原风格，但新代码不得扩散此风格。

### 2.2 格式化排版

- 缩进：**4 空格**，大括号换行（Allman 风格）
- 单行 `if`/`for`/`while`：可省略大括号，但**判断与执行必须分两行**
  ```csharp
  // ✅ 正确
  if (condition)
      DoSomething();

  // ❌ 错误：不能写成一行
  if (condition) DoSomething();
  ```
- 允许用局部函数收敛小逻辑

### 2.3 using 排序

按模块分组排序，组间空行分隔：
```
项目/插件引用 → 第三方库 → System → Unity
```

### 2.4 注释规范

- 每个文件必须添加文件头注释（说明文件职责）
- 复杂逻辑必须加注释，**解释逻辑原因和实现方式**，而非重复命名
- 对外工具/框架 API 建议补充 XML 文档注释（`/// <summary>`）

### 2.5 编码禁令与约束

- **全局禁止 lambda 表达式和匿名委托**（如 `() => {}`、`x => x.Name == y`）
  - 统一改写为方法内的本地函数（local function）
- **禁止过度工程**：够用即可，拒绝臃肿，只写最少必要代码
- **无关改动**：不要捎带与当前任务无关的修改
- 新代码必须逐条对照本规范；review 时重点检查 lambda 禁令

---

## 3. Git 约定

- 分支命名：`feature/xxx`、`fix/xxx`、`refactor/xxx`
- 提交信息：中文或英文均可，需清晰描述变更内容
- 目标分支：开发工作合并至 `dev`，`main` 为发布分支

---

## 4. 工作方式

- 写代码前先理解现有设计和架构，避免重复造轮子
- 修改已有代码时遵循「接触即修复」原则：碰到的代码如果违反本规范，同步修正
- 若发现潜在问题或更好的方案，先与开发者讨论而非直接大改
- 所有 AI agent 须在此文件头部标注身份，例如：
  ```
  > Agent: Reasonix / Claude Code 4.0 / 插件: none
  ```
