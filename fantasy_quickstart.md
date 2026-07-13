# Unity 客户端快速开始

本指南将帮助你快速在 Unity 项目中集成 Fantasy Framework,并实现与服务器的网络通信。

## 前置要求

在开始之前,请确保:

- ✅ Unity 版本 **6000.3.19f1** 或更高
- ✅ 已了解 C# 和 Unity 基础开发知识
- ✅ (可选) 已搭建 Fantasy 服务器并启动

> **💡 提示：** 如果你还没有服务器，可以使用 Fantasy CLI 快速创建一个：
> ```bash
> # 安装 Fantasy CLI
> dotnet tool install -g Fantasy.Cli
>
> # 创建服务器项目（包含协议定义工具）
> fantasy init -n MyGameServer
> ```
>
> **⚠️ macOS/Linux 用户注意：** 如果安装后无法使用 `fantasy` 命令，请查看 [Fantasy CLI 文档](../../Fantasy.Packages/Fantasy.Cil/README.md) 配置 PATH。
>
> 详见 [服务器端快速开始](01-QuickStart-Server.md) 文档。

---

## 安装 Fantasy.Unity

Fantasy.Unity 支持两种安装方式,推荐使用 **OpenUPM** 方式安装。

### 方式一: 通过 OpenUPM 安装 (推荐)

OpenUPM 是 Unity 包管理器的第三方注册表服务,可以轻松管理和更新包版本。

#### 选项 A: 使用 Package Manager UI 安装

这是最直观的安装方式,适合不熟悉 JSON 配置的用户:

1. **打开 Project Settings**
   - 在 Unity 菜单栏选择 `Edit` → `Project Settings`

2. **配置 Package Manager**
   - 在左侧面板选择 `Package Manager`
   - 点击 `Scoped Registries` 区域的 `+` 按钮添加新的注册表

3. **添加 OpenUPM 注册表**

   填写以下信息:

   | 字段 | 值 |
   |------|-----|
   | **Name** | `package.openupm.com` |
   | **URL** | `https://package.openupm.com` |
   | **Scope(s)** | `com.fantasy.unity` |

4. **保存设置**
   - 点击 `Save` 或 `Apply` 按钮

5. **安装 Fantasy.Unity 包**
   - 打开 Package Manager: `Window` → `Package Manager`
   - 点击左上角的 `+` 按钮
   - 选择 `Add package by name...` 或 `Add package from git URL...`
   - 在 **Name** 字段输入: `com.fantasy.unity`
   - 在 **Version** 字段输入版本号 (例如 `2025.2.1402`)
     - 💡 **提示**: 可以指定特定版本号,也可以留空使用最新版本
     - ✅ **建议**: 使用最新版本以获得最新功能和 Bug 修复
   - 点击 `Add` 按钮

6. **等待导入完成**
   - Unity 会自动下载并导入 Fantasy.Unity 包及其依赖项
   - 导入完成后,在 Package Manager 中可以看到 `Fantasy.Unity` 包

---

#### 选项 B: 通过 manifest.json 安装

这是更快捷的安装方式,适合熟悉 Unity 包管理的用户:

1. **定位 manifest.json 文件**

   在你的 Unity 项目根目录下找到:
   ```
   YourProject/
   └── Packages/
       └── manifest.json
   ```

2. **编辑 manifest.json**

   使用文本编辑器打开 `manifest.json`,将以下内容合并到文件中:

   ```json
   {
       "scopedRegistries": [
           {
               "name": "package.openupm.com",
               "url": "https://package.openupm.com",
               "scopes": [
                   "com.fantasy.unity"
               ]
           }
       ],
       "dependencies": {
           "com.fantasy.unity": "2025.2.1402"
       }
   }
   ```

   **版本说明:**
   - 💡 可以指定特定版本号 (例如 `"2025.2.1402"`)
   - ✅ **建议使用最新版本** - 删除版本号让 Unity 自动获取最新版,或访问 [OpenUPM](https://openupm.com/packages/com.fantasy.unity/) 查看最新版本号

   **完整示例:**

   假设你的原始 `manifest.json` 内容为:
   ```json
   {
       "dependencies": {
           "com.unity.collab-proxy": "2.0.0",
           "com.unity.ide.rider": "3.0.18"
       }
   }
   ```

   合并后应该是:
   ```json
   {
       "scopedRegistries": [
           {
               "name": "package.openupm.com",
               "url": "https://package.openupm.com",
               "scopes": [
                   "com.fantasy.unity"
               ]
           }
       ],
       "dependencies": {
           "com.unity.collab-proxy": "2.0.0",
           "com.unity.ide.rider": "3.0.18",
           "com.fantasy.unity": "2025.2.1402"  // 可以指定版本号,或删除引号中的版本号使用最新版
       }
   }
   ```

   > 💡 **版本号提示**: `"2025.2.1402"` 可以改为其他版本号,或删除版本号部分改为 `"com.fantasy.unity": ""` 让 Unity 自动使用最新版

3. **保存并返回 Unity**
   - 保存 `manifest.json` 文件
   - 返回 Unity 编辑器
   - Unity 会自动检测文件变化并开始下载安装包

---

### 方式二: 手动安装本地源码

如果你需要修改框架源码或调试框架内部逻辑,可以使用本地源码安装:

1. **克隆或下载 Fantasy 源码**

   ```bash
   # 使用 Git 克隆
   git clone https://github.com/qq362946/Fantasy.git

   # 或者下载 ZIP 并解压
   ```

2. **复制 Unity 包到项目**

   将 Fantasy 源码中的 Unity 包复制到你的项目:

   ```
   Fantasy/
   └── Fantasy.Packages/
       └── Fantasy.Unity/       # 这个目录就是 Unity 包
   ```

   复制到:

   ```
   YourProject/
   └── Packages/
       └── com.fantasy.unity/   # 将 Fantasy.Packages/Fantasy.Unity 复制到这里
   ```

3. **编辑 manifest.json**

   打开 `Packages/manifest.json`,添加本地包引用:

   ```json
   {
       "dependencies": {
           "com.fantasy.unity": "file:com.fantasy.unity"
       }
   }
   ```

4. **返回 Unity**
   - Unity 会自动识别本地包
   - 在 Package Manager 中可以看到 `Fantasy.Unity (local)` 包

**本地安装的优点:**
- ✅ 可以修改框架源码
- ✅ 便于调试和追踪问题
- ✅ 不依赖网络连接

**本地安装的缺点:**
- ⚠️ 需要手动更新版本
- ⚠️ 占用更多磁盘空间
- ⚠️ 需要自行维护源码

---

## 配置 Fantasy 环境

安装包完成后，需要配置 Fantasy 编译符号才能正常使用。

### 安装 FANTASY_UNITY 编译符号

1. **打开 Fantasy Settings**
   - 在 Unity 菜单栏选择 `Fantasy` → `Fantasy Settings`
   - 会打开 Fantasy 设置面板

2. **安装编译符号**
   - 在设置面板中找到 **Scripting Define Symbols** 区域
   - 检查 `FANTASY_UNITY` 的状态:
     - ✅ 如果显示 **"已安装"** 或 **"Installed"**，则无需操作
     - ⚠️ 如果显示 **"未安装"** 或 **"Not Installed"**，点击 **"安装"** 或 **"Install"** 按钮

3. **等待编译完成**
   - Unity 会自动添加 `FANTASY_UNITY` 编译符号并重新编译
   - 编译完成后，Fantasy 框架即可正常使用

**为什么需要这一步？**

- `FANTASY_UNITY` 是 Fantasy 框架的编译符号
- 它会激活 Unity 平台相关的代码和 Source Generator
- 没有这个符号，框架的核心功能将无法使用

---

### WebGL 平台额外配置

如果你的项目需要构建到 **WebGL 平台**，还需要额外安装 `FANTASY_WEBGL` 编译符号。

1. **打开 Fantasy Settings**
   - 在 Unity 菜单栏选择 `Fantasy` → `Fantasy Settings`
   - 会打开 Fantasy 设置面板

2. **安装 WebGL 编译符号**
   - 在设置面板中找到 **Scripting Define Symbols** 区域
   - 检查 `FANTASY_WEBGL` 的状态:
     - ✅ 如果显示 **"已安装"** 或 **"Installed"**，则无需操作
     - ⚠️ 如果显示 **"未安装"** 或 **"Not Installed"**，点击 **"安装"** 或 **"Install"** 按钮

3. **等待编译完成**
   - Unity 会自动添加 `FANTASY_WEBGL` 编译符号并重新编译
   - 编译完成后，项目即可构建到 WebGL 平台

**为什么 WebGL 需要额外配置？**

- `FANTASY_WEBGL` 是 WebGL 平台的专用编译符号
- 它会激活 WebGL 平台特定的网络代码（WebSocket）
- WebGL 平台有浏览器安全限制，需要特殊处理
- 只在需要构建 WebGL 时才安装此符号

**⚠️ 重要提示：**

- 如果**不需要构建 WebGL**，不要安装此符号
- WebGL 平台只支持 **WebSocket** 协议，不支持 KCP 和 TCP
- WebGL 构建需要服务器支持 WebSocket 连接

---

## 验证安装

配置完成后，验证 Fantasy.Unity 是否正确安装:

1. **检查 Package Manager**
   - 打开 `Window` → `Package Manager`
   - 在左上角选择 `Packages: In Project`
   - 确认列表中有 `Fantasy.Unity` 包

2. **检查编译符号**
   - 打开 `Fantasy` → `Fantasy Settings`
   - 确认 `FANTASY_UNITY` 显示为 **"已安装"**
   - （如果需要 WebGL 构建）确认 `FANTASY_WEBGL` 显示为 **"已安装"**

3. **检查命名空间**

   创建一个测试脚本:

   ```csharp
   using Fantasy;
   using Fantasy.Async;
   using Fantasy.Network;
   using UnityEngine;

   public class FantasyTest : MonoBehaviour
   {
       void Start()
       {
           Debug.Log("Fantasy.Unity 安装成功!");
       }
   }
   ```

   如果没有命名空间错误，说明安装成功。

---

## 示例项目

Fantasy 仓库中提供了完整的 Unity 客户端示例项目:

```
Fantasy/
└── Examples/
    └── Client/
        └── Unity/
            └── Assets/
                └── Scripts/
                    └── Examples/
                        ├── ConnectToServer/      # 连接服务器示例
                        ├── NormalMessage/        # 普通消息示例
                        ├── RouteMessage/         # 路由消息示例
                        ├── Addressable/          # Addressable 示例
                        ├── EventSystem/          # 事件系统示例
                        └── ...                   # 更多示例
```

**推荐学习顺序:**
1. `ConnectToServer/` - 学习如何连接服务器
2. `NormalMessage/` - 学习消息发送和接收
3. `RouteMessage/` - 学习路由消息
4. `EventSystem/` - 学习事件系统

---

## 常见问题

### Q1: 如何配合服务器使用网络协议？

**推荐流程：**

1. **使用 Fantasy CLI 创建服务器项目**（包含协议工具）
   ```bash
   fantasy init -n MyGameServer
   ```

2. **在服务器项目中定义协议**
   - 编辑 `Tools/NetworkProtocol/*.proto` 文件
   - 运行协议导出工具生成代码

3. **将生成的协议代码复制到 Unity**
   - 服务器和客户端使用相同的协议定义
   - 确保 OpCode 和消息结构一致

详见 [协议定义指南](../03-Advanced/06-Protocol.md)（规划中）

### Q2: 安装后找不到 Fantasy 命名空间?

**解决方法:**

1. **重新导入包**
    - `Assets` → `Reimport All`
    - 等待编译完成

2. **检查程序集引用**
    - 如果使用了 Assembly Definition (asmdef),确保引用了 `Fantasy.Unity`
    - 在 asmdef 文件的 `Assembly Definition References` 中添加 `Fantasy.Unity`

3. **重启 Unity 和 IDE**
    - 关闭 Unity 和 Visual Studio/Rider
    - 重新打开项目

---

## 下一步

恭喜! 你已经完成了 Fantasy.Unity 的安装和Unity 客户端的基础使用。接下来可以:

1. 🌐 阅读 [Unity 客户端编写启动代码](../02-Unity/01-WritingStartupCode-Unity.md) 学习如何在Unity客户端编写启动代码
2. 🔧 阅读 [协议定义指南](11-Protocol.md) 学习如何定义自己的消息协议 (待完善)
3. 🎯 阅读 [网络消息处理](10-Message.md) 深入了解消息系统 (待完善)
4. 📖 阅读 [ECS 系统详解](06-ECS.md) 学习客户端实体组件系统 (待完善)
5. 🎮 阅读 [事件系统](22-Event.md) 学习客户端事件机制 (待完善)
6. 📚 查看 `Examples/Client/Unity` 目录下的完整示例

## 获取帮助

- **GitHub**: https://github.com/qq362946/Fantasy
- **文档**: https://www.code-fantasy.com/
- **Issues**: https://github.com/qq362946/Fantasy/issues
- **OpenUPM**: https://openupm.com/packages/com.fantasy.unity/

---
# 快速开始 - 服务器端

本指南将帮助你快速创建一个 Fantasy Framework 服务器项目。

## 前提条件

- **.NET SDK**: .NET 8.0 或 .NET 9.0
- **IDE**: Visual Studio 2022、Rider 或 VS Code

检查你的 .NET 版本：

```bash
dotnet --version
```

> **📌 版本说明：**
> - Fantasy Framework 当前主版本为 **2.x**
> - 本文档基于 2.0.0 版本编写，但建议使用最新稳定版本
> - 框架支持 .NET 8.0 和 .NET 9.0
> - 查看最新版本和更新日志：[NuGet](https://www.nuget.org/packages/Fantasy-Net) | [GitHub Releases](https://github.com/qq362946/Fantasy/releases)

---

## 🎯 使用 Fantasy CLI 脚手架（强烈推荐）

Fantasy CLI 是官方提供的脚手架工具，可以**一键生成完整的项目结构**，包括配置文件、工具和示例代码，是最快速、最简单的入门方式。

### 安装 Fantasy CLI

将 Fantasy CLI 安装为全局 .NET 工具：

```bash
dotnet tool install -g Fantasy.Cli
```

更新到最新版本：

```bash
dotnet tool update -g Fantasy.Cli
```

> **⚠️ macOS/Linux 用户注意：**
>
> 如果安装后无法直接使用 `fantasy` 命令，需要配置 PATH 环境变量。
>
> **详细配置步骤请查看：** [Fantasy CLI 完整文档](../../Fantasy.Packages/Fantasy.Cil/README.md)（查看"安装"章节）

### 创建项目

**方式一：交互模式（推荐）**

```bash
fantasy init
```

工具会引导你完成以下配置：
- 项目名称
- 目标框架 (.NET 8.0 或 9.0)
- 是否添加协议导出工具
- 是否添加网络协议定义
- 是否添加 NLog 日志组件

**方式二：快速创建**

```bash
fantasy init -n MyGameServer
```

直接使用项目名创建，其他选项使用默认值。

### 生成的项目结构

```
MyGameServer/
├── Server/
│   ├── Main/                   # 服务器入口点
│   ├── Entity/                 # 游戏实体
│   │   └── Fantasy.config      # 主配置文件（已自动生成）
│   ├── Hotfix/                 # 热重载逻辑
│   └── Server.sln
├── Config/                     # 配置目录
├── Tools/                      # 工具目录
│   ├── NetworkProtocol/        # 协议定义
│   └── ProtocolExportTool/     # 协议导出工具
```

### 构建和运行

```bash
cd MyGameServer

# 构建服务器
dotnet build Server/Server.sln

# 运行服务器
dotnet run --project Server/Main/Main.csproj
```

### 添加更多组件或工具

> **⚠️ 重要限制警告：**
>
> **`fantasy add` 命令目前仅支持向新创建的目录中添加组件，不能直接附加到已有项目中！**
>
> **这意味着：**
> - ❌ **不能**在已经创建的项目中运行 `fantasy add` 来添加组件
> - ❌ **不能**在已经存在代码的项目目录中使用此命令
> - ✅ **只能**在新创建的空目录中使用 `fantasy add`
>
> **如果你已经创建了项目并想添加更多组件，有以下两种方法：**
>
> 1. **在新目录中生成组件，然后手动复制**
>    ```bash
>    # 在临时目录中生成组件
>    mkdir temp && cd temp
>    fantasy add -t networkprotocol
>    # 然后手动复制生成的文件到你的项目
>    ```
>
> 2. **使用手动方式添加（推荐）**
>    - 直接下载或复制需要的组件到项目中
>    - 参考下方的[手动集成到现有项目](#手动集成到现有项目)章节

**在空目录中使用 `fantasy add` 的命令：**

```bash
# 交互式选择组件
fantasy add

# 添加特定组件
fantasy add -t protocolexporttool  # 协议导出工具
fantasy add -t networkprotocol     # 网络协议定义
fantasy add -t nlog                # NLog 日志
fantasy add -t fantasynet          # Fantasy.Net 框架
fantasy add -t fantasyunity        # Fantasy.Unity 客户端
fantasy add -t all                 # 添加所有组件
```

### 可用组件

| 组件 | 描述 |
|------|------|
| **Fantasy.Net** | 核心框架库（包含运行时和源代码生成器） |
| **Fantasy.Unity** | Unity 客户端框架（Unity 项目专用） |
| **ProtocolExportTool** | 协议导出工具（从 .proto 文件生成代码） |
| **NetworkProtocol** | 网络协议定义文件和模板 |
| **NLog** | NLog 日志组件配置 |

### 配置语言

Fantasy CLI 支持中文和英文界面。设置环境变量可跳过语言选择：

**Windows (PowerShell)：**
```powershell
$env:FANTASY_CLI_LANG = "Chinese"  # 或 "English"
```

**Linux/macOS：**
```bash
export FANTASY_CLI_LANG=Chinese  # 或 English
```

**✅ 使用 Fantasy CLI 创建项目后，可以直接跳到 [下一步：编写启动代码](#下一步编写启动代码) 章节。**

---

## 其他安装方式

如果你不想使用脚手架工具，或者需要将 Fantasy 集成到现有项目中，可以使用以下方式：

## 推荐的项目结构

虽然不强制，但建议使用分层结构：

```
YourSolution/
├── YourSolution.sln
├── Server/                   # 入口项目（Console 应用）
│   ├── Program.cs           # 启动代码
│   └── Server.csproj        # 引用 → Server.Entity和Server.Hotfix
│
├── Server.Entity/            # 实体项目（Class Library）
│   ├── Fantasy.config       # 配置文件
│   ├── Components.cs        # 实体、组件定义
│   ├── Generate             # 生成固定代码，比如网络协议等不需要热重载的数据
│   └── Server.Entity.csproj # 引用 → Fantasy（直接引用）
│
└── Server.Hotfix/            # 热更新项目（可选）
    ├── MessageHandlers.cs   # 消息处理器
    └── Server.Hotfix.csproj # 引用 → Server.Entity
```

**项目引用链：**

```
Server (入口)
  └─引用→ Server.Entity
              ├─引用→ Fantasy ⭐ (只有这里直接引用 Fantasy)
              └─被引用← Server.Hotfix
```

**分层说明：**

| 项目 | 职责 | 引用关系 | 是否需要引用 Fantasy |
|------|------|----------|---------------------|
| **Server** | 服务器启动入口，包含 `Program.cs` | 引用 `Server.Entity` | ❌ 不需要（通过 Entity 传递） |
| **Server.Entity** | 包含实体、组件、数据定义等 `Fantasy.config`| **直接引用 Fantasy** | ✅ **需要** |
| **Server.Hotfix** | 热更新逻辑：消息处理器、事件处理器等 | 引用 `Server.Entity` | ❌ 不需要（通过 Entity 传递） |

**🔑 关键理解：**
- **只有 `Server.Entity` 需要直接引用 Fantasy 框架**
- 其他项目通过引用 `Server.Entity` 就能自动获得 Fantasy 的功能（引用传递）
- 这种设计减少了重复配置，便于维护

## 手动集成到现有项目

### 方式一：NuGet 包引用（推荐）✨

**适用场景：** 大多数项目，快速上手

**在你的项目中**添加 NuGet 包 ：

```bash
# 添加最新版本
dotnet add package Fantasy-Net

# 或指定版本号
dotnet add package Fantasy-Net --version 2025.2.1401
```

或直接编辑 `Server.Entity.csproj` 文件：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <!-- 使用最新版本（推荐） -->
        <PackageReference Include="Fantasy-Net" Version="*" />

        <!-- 或指定具体版本 -->
        <!-- <PackageReference Include="Fantasy-Net" Version="2.0.0" /> -->
    </ItemGroup>
</Project>
```

> **💡 提示：**
> - 建议使用最新稳定版本，使用 `dotnet add package Fantasy-Net` 会自动安装最新版本
> - 查看所有可用版本：https://www.nuget.org/packages/Fantasy-Net
> - 生产环境建议锁定具体版本号以保证稳定性

**✅ 完成！NuGet 包会自动配置所有必要的编译选项和 Source Generator，无需手动配置。**

**🎯 其他项目不需要直接引用：**
- `Server` 项目：引用 `Server.Entity`和`Server.Hotfix` 即可
- `Server.Hotfix` 项目：引用 `Server.Entity` 即可
- 它们会通过项目引用自动获得 Fantasy 的功能

完成此步骤后，直接跳到 **[步骤 2：创建配置文件](#步骤-2创建配置文件)**。

---

### 方式二：源码引用

**适用场景：** 需要自定义框架或深度开发

#### 2.1 Clone 项目源码

```bash
git clone https://github.com/qq362946/Fantasy.git
```

#### 2.2 添加项目引用

**只在你的项目**的 `.csproj` 中添加引用：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <!-- 引用核心框架 -->
        <ProjectReference Include="path/to/Fantasy/Fantasy.Packages/Fantasy.Net/Fantasy.Net.csproj" />

        <!-- 引用 Source Generator（必须！） -->
        <ProjectReference Include="path/to/Fantasy/Fantasy.Packages/Fantasy.SourceGenerator/Fantasy.SourceGenerator.csproj"
                          OutputItemType="Analyzer"
                          ReferenceOutputAssembly="false" />
    </ItemGroup>
</Project>
```

#### 2.3 配置项目属性

**源码引用时必须在项目中进行以下配置：**

编辑 `.csproj` 文件，添加必要的编译配置：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
    </PropertyGroup>

    <!-- ==================== 必需配置 ==================== -->

    <!-- Debug 配置 -->
    <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
        <!-- FANTASY_NET 宏：激活 Source Generator 代码生成 -->
        <DefineConstants>TRACE;FANTASY_NET</DefineConstants>
        <!-- AllowUnsafeBlocks：允许 unsafe 代码 -->
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    </PropertyGroup>

    <!-- Release 配置 -->
    <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
        <DefineConstants>TRACE;FANTASY_NET</DefineConstants>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    </PropertyGroup>

    <!-- 项目引用 -->
    <ItemGroup>
        <ProjectReference Include="path/to/Fantasy/Fantasy.Packages/Fantasy.Net/Fantasy.Net.csproj" />
        <ProjectReference Include="path/to/Fantasy/Fantasy.Packages/Fantasy.SourceGenerator/Fantasy.SourceGenerator.csproj"
                          OutputItemType="Analyzer"
                          ReferenceOutputAssembly="false" />
    </ItemGroup>
</Project>
```
**重要说明：**
- 项目中只要使用了Fantasy相关的逻辑就必须要添加`Fantasy.SourceGenerator`的引用
- `Fantasy.SourceGenerator`会自动生成框架所需要的注册代码
- 如果不添加`Fantasy.SourceGenerator`代码会无法注册的框架中
```xml
<!-- 项目添加Fantasy.SourceGenerator -->
<ItemGroup>
    <ProjectReference Include="path/to/Fantasy/Fantasy.Packages/Fantasy.SourceGenerator/Fantasy.SourceGenerator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
</ItemGroup>
```

**配置说明：**

| 配置项 | 用途 | 影响 |
|--------|------|------|
| `FANTASY_NET` | 激活 Source Generator 进行编译时代码生成 | 缺少此宏会导致框架无法生成注册代码，运行时出错 |
| `AllowUnsafeBlocks` | 允许使用 unsafe 代码 | Fantasy 使用 unsafe 代码优化性能，缺少会导致编译错误 |

---

### 步骤 2：创建配置文件

**⚠️ 重要：配置文件放在引用`Fantasy.net`项目根目录就可以，不需要非要放在入口项目！**

#### 方式一：NuGet 包（自动创建）

当你添加 NuGet 包后，`Fantasy.config` 和 `Fantasy.xsd` 会**自动**在项目根目录下创建。

你只需要根据实际需求修改配置内容即可。

#### 方式二：源码引用（手动复制）

源码中的配置文件位置：
- `Fantasy.config` 位于：`Fantasy/Fantasy.Packages/Fantasy.Net/Fantasy.config`
- `Fantasy.xsd` 位于：`Fantasy/Fantasy.Packages/Fantasy.Net/Fantasy.xsd`

将这两个文件复制到你引用了 Fantasy 的项目根目录（例如 `Server.Entity/`）即可。

#### 配置文件内容

无论使用哪种方式，`Fantasy.config` 的内容示例如下：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<fantasy xmlns="http://fantasy.net/config"
         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         xsi:schemaLocation="http://fantasy.net/config Fantasy.xsd">

    <!-- 网络配置 -->
    <network inner="TCP" maxMessageSize="1048560" />

    <!-- 会话配置 -->
    <session idleTimeout="8000" idleInterval="5000" />

    <server>
        <!-- 机器配置 -->
        <machines>
            <machine id="1" outerIP="127.0.0.1" outerBindIP="127.0.0.1" innerBindIP="127.0.0.1" />
        </machines>

        <!-- 进程配置 -->
        <processes>
            <process id="1" machineId="1" startupGroup="0" />
        </processes>

        <!-- 世界配置 -->
        <worlds>
            <world id="1" worldName="MainWorld">
                <!-- 数据库配置(可选) -->
                <database dbType="MongoDB" dbName="game" dbConnection="mongodb://localhost:27017/" />
            </world>
        </worlds>

        <!-- 场景配置 -->
        <scenes>
            <!-- Gate 场景：处理客户端连接 -->
            <scene id="1001" processConfigId="1" worldConfigId="1"
                   sceneRuntimeMode="MultiThread" sceneTypeString="Gate"
                   networkProtocol="KCP" outerPort="20000" innerPort="11001" />
        </scenes>
    </server>
</fantasy>
```

**配置要点：**

以下是配置文件中最重要的几个参数：

| 配置项 | 说明 | 示例值 |
|--------|------|--------|
| `<machine>` | 定义服务器的IP地址<br>• `outerIP`: 客户端连接的IP<br>• `innerBindIP`: 服务器间通信的IP | 本地开发都用 `127.0.0.1`<br>生产环境使用实际IP |
| `<process>` | 定义进程运行在哪台机器上<br>• `machineId`: 引用机器ID<br>• `startupGroup`: 启动顺序 | 相同分组的进程同时启动 |
| `<world>` | 定义游戏世界和数据库<br>• 可配置多个数据库（主库、从库等）<br>• `dbConnection` 为空则不连接 | 开发环境可不配置数据库 |
| `<scene>` | **核心配置**，定义业务场景<br>• `outerPort`: 客户端连接端口<br>• `innerPort`: 服务器间通信端口<br>• `networkProtocol`: 网络协议 | Gate场景使用 KCP 协议<br>Map场景不对外监听 |

**💡 快速理解：**
- 本地开发：所有 IP 都用 `127.0.0.1`，配置一个 Gate 场景即可
- 生产环境：配置实际IP地址，根据业务需求配置多个场景
- 数据库可选：开发环境可以不连接数据库（`dbConnection=""`）

> **📖 详细说明：** 完整的配置参数说明请查看 [Fantasy.config 配置文件详解](../01-Server/01-ServerConfiguration.md)

---

#### 📌 为什么配置文件要放在引用 Fantasy 的项目？

**原因：**
1. **代码生成依赖**：框架会根据 `Fantasy.config` 生成注册代码（通过 Source Generator）
2. **引用链传递**：生成的代码在配置文件所在的项目中，其他项目通过引用该项目自动获得这些代码
3. **避免依赖问题**：如果放在没有被其他项目引用的项目中，生成的代码无法被其他项目使用

**示例：**
- ✅ 放在 `Server.Entity`（被 Server 和 Hotfix 引用）→ 所有项目都能使用生成的代码
- ❌ 放在 `Server` 入口项目（Hotfix 不引用 Server）→ Hotfix 无法使用生成的代码

---

#### ⚠️ 重要：配置文件必须复制到输出目录

**无论使用 NuGet 包还是源码引用，都必须在引用 Fantasy 的项目（如 `Server.Entity`）的 `.csproj` 中包含以下配置：**

```xml
<ItemGroup>
    <!-- 将配置文件复制到输出目录 -->
    <None Update="Fantasy.config">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="Fantasy.xsd">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>

    <!-- 重要：将配置文件添加为 AdditionalFiles，使 Source Generator 能够读取 -->
    <AdditionalFiles Include="Fantasy.config" />
</ItemGroup>
```

**配置说明：**

| 配置项 | 作用 | 缺少会导致 |
|--------|------|-----------|
| `<None Update>` | 确保配置文件在编译时复制到输出目录（`bin/Debug` 或 `bin/Release`），使运行时能够读取 | ❌ 运行时找不到配置文件，程序无法启动 |
| `<AdditionalFiles Include>` | 使 Source Generator 在编译时能够读取配置文件并生成相应代码（数据库名称常量、场景类型枚举等） | ❌ 无法生成数据库相关的代码，导致编译错误或运行时异常 |

**不同方式的处理：**

- **NuGet 包方式**：**必须手动添加**上述配置到 `.csproj` 文件中，否则程序无法正常运行。
- **源码引用方式**：**必须手动添加**上述配置到 `.csproj` 文件中，否则程序无法正常运行。

---

## 下一步：编写启动代码

完成框架集成和配置文件创建后，下一步是编写服务器启动代码。

请继续阅读 **[编写启动代码](../01-Server/02-WritingStartupCode.md)**，学习：
- 如何编写 `Program.cs` 启动代码
- `AssemblyHelper` 的作用和原理
- 程序集加载机制详解
- 热重载支持
- 常见问题和最佳实践

---

## 常见问题

### Q1: 如何卸载 Fantasy CLI?

**使用以下命令卸载：**
```bash
dotnet tool uninstall -g Fantasy.Cli
```

### Q2: 找不到 Fantasy 命名空间

**原因：**
- 未安装 NuGet 包或未正确引用源码
- NuGet 包版本不兼容（需要 2.x 版本）
- 未定义 `FANTASY_NET` 宏（仅源码引用）

**解决：**
```bash
# 检查已安装的包版本
dotnet list package

# 清理并重新安装
dotnet clean
dotnet restore
dotnet build

# 如果需要，更新到最新版本
dotnet add package Fantasy-Net
```

### Q3: Source Generator 没有生成代码

**使用 NuGet 包：**
- NuGet 包会自动配置 Source Generator，通常不会出现这个问题
- 如果出现问题，尝试：`dotnet clean && dotnet build`

**使用源码引用时检查清单：**
- [ ] 是否定义了 `FANTASY_NET` 宏
- [ ] 是否设置了 `AllowUnsafeBlocks=true`
- [ ] 是否添加了 `Fantasy.SourceGenerator.csproj` 引用
- [ ] 是否成功编译（Source Generator 在编译时工作）

**调试方法：**
```bash
# 清理并重新生成
dotnet clean
dotnet build -v detailed

# 查看生成的代码
ls obj/Debug/net8.0/generated/Fantasy.SourceGenerator/
```

### Q4: 端口被占用

**错误信息：**
```
System.Net.Sockets.SocketException: Address already in use
```

**解决：**
- 修改 `Fantasy.config` 中的 `outerPort` 端口号
- 或关闭占用端口的程序

### Q5: 配置文件未找到

**错误信息：**
```
Could not find Fantasy.config
```

**原因：**
- 配置文件位置错误（应该在引用了 Fantasy 的项目根目录）
- 配置文件未复制到输出目录

**解决：**

1. **检查配置文件位置**
   ```bash
   # 配置文件应该在引用了 Fantasy 的项目根目录（如 Server.Entity）
   ls Server.Entity/Fantasy.config
   ```

2. **源码引用时：确保在项目的 `.csproj` 中配置了文件复制**
   ```xml
   <ItemGroup>
       <None Update="Fantasy.config">
           <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
       </None>
       <None Update="Fantasy.xsd">
           <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
       </None>
       <!-- 重要：将配置文件添加为 AdditionalFiles，使 Source Generator 能够读取 -->
       <AdditionalFiles Include="Fantasy.config" />
   </ItemGroup>
   ```

3. **NuGet 包方式时**
   - NuGet 包会自动创建配置文件并配置复制，通常不会出现此问题
   - 如果出现，尝试清理后重新构建：`dotnet clean && dotnet build`

### Q6: 生成的代码无法在其他项目中使用

**症状：**
- 其他项目中无法使用框架生成的代码
- 提示找不到类型或命名空间

**原因：**
配置文件放在了错误的位置，导致代码生成在没有被其他项目引用的项目中

**解决：**
1. 确保 `Fantasy.config` 在直接引用了 Fantasy 的项目根目录（如 `Server.Entity`）
2. 确保需要使用生成代码的项目正确引用了该项目
3. 检查项目引用链是否正确
4. 重新构建解决方案：`dotnet clean && dotnet build`

### Q7: 运行时出现 "Command line format error!" 错误

**错误信息：**
```
Command line format error!
```

**原因：**
服务器启动时缺少必需的命令行参数。Fantasy Framework 需要通过命令行参数指定运行模式（`RuntimeMode`）。

**解决方案：**

**方法 1: 配置 launchSettings.json（开发环境推荐）**

在项目的 `Properties/launchSettings.json` 文件中添加命令行参数：

```json
{
  "profiles": {
    "Develop": {
      "commandName": "Project",
      "commandLineArgs": "--m Develop"
    }
  }
}
```

**方法 2: IDE 配置**

在 IDE 的启动配置中添加命令行参数 `--m Develop`

**方法 3: 命令行启动**

使用命令行启动时手动指定参数：
```bash
dotnet YourServer.dll --m Develop
```

> **📖 详细说明：** 关于命令行参数的完整配置说明，请查看 [命令行参数配置文档](../01-Server/03-CommandLineArguments.md)

## 下一步

完成 Fantasy Framework 的安装和配置后，建议按照以下顺序学习：

### 📖 推荐学习路径

1. **配置文件详解** 📋
   - [Fantasy.config 配置文件详解](../01-Server/01-ServerConfiguration.md)
   - 深入了解网络配置、场景配置、数据库配置等

2. **编写启动代码** 💻
   - [编写启动代码](../01-Server/02-WritingStartupCode.md)
   - 学习 AssemblyHelper、程序集加载、启动流程

3. **命令行参数配置** ⚙️
    - [命令行参数配置](../01-Server/03-CommandLineArguments.md)
    - 配置开发环境和生产环境的启动参数

4. **场景初始化** 🎬
    - [OnCreateScene 事件使用指南](../01-Server/04-OnCreateScene.md)
    - 学习如何在场景启动时初始化逻辑

5. **配置系统使用** 🔧
   - [配置系统使用指南](../01-Server/05-ConfigUsage.md)
   - 学习如何在代码中读取和使用配置


### 🎯 其他资源

- 📱 [Unity 客户端快速开始](02-QuickStart-Unity.md) - 创建 Unity 客户端
- 📚 查看 `Examples/Server` 目录下的完整示例
- 📖 返回 [文档首页](../README.md) 查看完整文档结构

## 获取帮助

- **GitHub**: https://github.com/qq362946/Fantasy
- **文档**: https://www.code-fantasy.com/
- **Issues**: https://github.com/qq362946/Fantasy/issues

---vv