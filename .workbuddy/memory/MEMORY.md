# Noita 项目长期记忆

## 文档约定
- 项目文档（设计/规格/计划类 .md）统一使用**中文文件名**，英文原名仅作历史参考。涉及改名时必须同步更新全项目交叉引用，避免断链。
- 改名安全流程：① 先全项目内容替换旧文件名令牌（\b 单词边界 + 忽略大小写）→ ② 再重命名文件。校验用脚本扫描残留令牌 + 解析 markdown 相对链接。
- 设计文档统一存放于根 `Docs/`；`Client/Assets/Docs/` 不再存放任何 .md（已于 2026-07-15 将 `DOTS重构设计文档.md` 迁入根 `Docs/`）。

## 环境
- Unity 6000.3.19f1 / IL2CPP；架构正从 MonoBehaviour 重构为 DOTS ECS。
- 大仓库（4 万+ 文件），全量 Grep 易超时，定向扫描 .md 或限定目录更稳。

## 程序集与命名空间约定（HybridCLR）
- 仅 **两个程序集**：`AOT`（命名空间 `AOT`，单程序集，不按 Core/Simulation/Renderer/Gameplay 细分）+ `Hotfix`（命名空间 `Hotfix`，单程序集）。
- 归属：确定性、需 Burst、不可热更的 DOTS 核心（组件/ISystem/共享数据/渲染桥接）→ AOT；可热更的定义表/配置/UI/元数据 → Hotfix。依赖单向：Hotfix 引用 AOT，AOT 禁反向引用。
- 见 `Docs/DOTS重构设计文档.md` §2 / §2.1。

### ⚠️ 待拍板：设计文档基础模型冲突（2026-07-15 审查发现）
- `DOTS重构设计文档.md` 已改为 AOT+Hotfix（2 程序集，ns AOT/Hotfix）。
- 但其余 7+ 文档（多人联机帧同步对战设计 / 帧同步Netcode设计 / 玩法重构方案 / 跨平台输入架构 / 协议与序列化规范 / 构建与部署 / 项目搭建与贡献指南 / 开发智能体配置）仍用旧 `NoitaCA.*`（Core/Simulation/Renderer/Gameplay/Editor/Lockstep，6–7 程序集，ns NoitaCA）+ Hotfix 模型。两套互斥。
- 需主理人拍板「唯一事实源」。推荐采纳 AOT+Hotfix 并回填其余文档（用户已明确指令 AOT 单程序集）。回填前需确认代码侧是否已合并为单 AOT asmdef（当前代码实际在 `AOT/NoitaCA/` 下含多 asmdef）。
- 其他未闭环项：F4 Fix64 范围(±2^15 vs ±2.1e9)、F6 确定性常量热更落点(ADR 补 O3 裁定)、F3 PixelGrid 过时引用、F7 老审查 P1-5/7/8。（F5 DOTS §4.1 `float`→定点、F12 §6 ZLinq lambda→本地函数，已于 2026-07-15 修复。）完整见 `Docs/evo-forge/设计文档体系审查报告.md`。
