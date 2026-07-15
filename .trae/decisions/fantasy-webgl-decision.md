# FANTASY_WEBGL 决策

## 结论
**不启用 FANTASY_WEBGL 编译符号**。

## 理由
1. 当前 Noita 项目主目标平台为 Standalone（PC）+ Android（移动端），无 WebGL 需求
2. WebGL 平台仅支持 WebSocket 协议，KCP 不支持；引入会增加协议适配复杂度
3. 当前 Fantasy-Unity 2025.2.1402 已在 [Client/ProjectSettings/ProjectSettings.asset](file:///d:/Unity/Projects/Noita/Client/ProjectSettings/ProjectSettings.asset) 注入 FANTASY_UNITY 但**不**注入 FANTASY_WEBGL
4. 后续若需 WebGL 构建，按 [fantasy_quickstart.md WebGL 平台额外配置] 节补装

## 复审
- 触发条件：决定支持 WebGL 平台时
- 操作：在 ProjectSettings.asset 两处 scriptingDefineSymbols 加 `FANTASY_WEBGL`
