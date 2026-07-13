/*
┌────────────────────────────┐
│　Description：服务器命令管理
│　Remark：用于编译和运行ServerBase项目
└────────────────────────────┘
*/

using Lin.Runtime.Helper;
using Cysharp.Text;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace Lin.Editor.Command
{
    // 在编辑器加载时初始化，保证事件挂接生效
    [InitializeOnLoad]
    public static class ServerCommands
    {
        private const string title = "Server";

        // 运行中的服务器进程引用
        private static volatile Process serverProcess;

        // 运行时设置的服务器进程名（用于查杀同名进程）；默认取 csproj 文件名
        private static string serverProcessName;

        // ServerBase 项目根路径与 csproj 路径
        private static readonly string projectPath = Path.Combine(Application.dataPath, "../ToolProjects/ServerBase");
        private static readonly string csprojPath = Path.Combine(projectPath, "ServerBase.csproj");

        // 输出根路径 (Debug 配置)
        private static readonly string debugOutputPath = Path.Combine(projectPath, "bin/Debug");

        private static void Debug(object message) => Log.Debug(title, message);

        private static void Error(object message) => Log.Error(title, message);

        // 静态构造：编辑器域加载时挂接播放模式状态变化事件
        static ServerCommands()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Run()
        {
            Task.Run(() =>
            {
                try
                {
                    // 在编译之前，确保不存在同名已运行进程，避免端口/文件占用
                    KillSameNameProcesses(ResolveProcessName());

                    // 编译项目，若失败则不继续运行
                    if (!CompileProject())
                        return;

                    // 运行服务器
                    RunServer();
                }
                catch (Exception ex)
                {
                    Error(ZString.Format("运行服务器时发生错误: {0}", ex.Message));
                }
            });
        }

        public static void Stop()
        {
            try
            {
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    // 优先尝试温和的退出方式；若无窗口则直接 Kill
                    try
                    {
                        if (!serverProcess.CloseMainWindow())
                            serverProcess.Kill();
                    }
                    catch
                    {
                        serverProcess.Kill();
                    }

                    serverProcess.Dispose();
                    serverProcess = null;
                    Debug("ServerBase.exe 已停止运行");
                }
                else Debug("ServerBase.exe 未在运行");
            }
            catch (Exception ex)
            {
                Error(ZString.Format("停止服务器时发生错误: {0}", ex.Message));
            }
        }

        private static bool CompileProject()
        {
            try
            {
                Debug("开始编译 ServerBase 项目...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = ZString.Format("build \"{0}\" --configuration Debug", csprojPath),
                    WorkingDirectory = projectPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    // 编译超时控制：超过 5 秒视为失败，避免卡住启动流程
                    var exited = process.WaitForExit(5000);
                    if (!exited)
                    {
                        try
                        {
                            // 尝试终止编译进程，避免遗留子进程
                            process.Kill();
                        }
                        catch { }

                        Error("编译超时(>5s)，取消启动");
                        return false;
                    }
                    
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    if (process.ExitCode == 0)
                    {
                        Debug("编译成功");
                        if (!string.IsNullOrEmpty(output)) 
                            Debug(ZString.Format("编译输出: {0}", output));
                        return true;
                    }
                    else
                    {
                        Error(ZString.Format("编译失败，退出代码: {0}", process.ExitCode));
                        if (!string.IsNullOrEmpty(error)) 
                            Error(ZString.Format("编译错误: {0}", error));
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Error(ZString.Format("编译过程中发生错误: {0}", ex.Message));
                return false;
            }
        }

        private static void RunServer()
        {
            try
            {
                // 若已有运行中的进程，避免重复启动
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    Debug("ServerBase.exe 已在运行");
                    return;
                }

                // 根据最新生成的 TFM 解析 exe 路径（如 net8.0/net7.0）
                var exePath = ResolveExePath();
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    Error(ZString.Format("可执行文件不存在或未生成: {0}", exePath ?? "(未解析)"));
                    return;
                }

                Debug("启动ServerBase.exe...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                serverProcess = Process.Start(startInfo);
                
                // 异步读取输出
                serverProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) 
                        Debug(e.Data);
                };
                
                serverProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) 
                        Error(e.Data);
                };

                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();

                Debug("ServerBase.exe 已启动，日志将显示在控制台中");
            }
            catch (Exception ex)
            {
                Error(ZString.Format("启动服务器时发生错误: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 解析最新生成的可执行文件路径。
        /// 规则：在 Debug 输出目录下选择版本号最高的 netX.Y 目录，组合出 ServerBase.exe。
        /// </summary>
        private static string ResolveExePath()
        {
            try
            {
                if (!Directory.Exists(debugOutputPath)) return null;

                // 寻找所有以 net 开头的 TFM 目录（如 net8.0/net7.0）
                var tfmDirs = Directory.GetDirectories(debugOutputPath, "net*");
                if (tfmDirs == null || tfmDirs.Length == 0) return null;

                string bestDir = null;
                Version bestVersion = null;

                for (int i = 0; i < tfmDirs.Length; i++)
                {
                    var name = Path.GetFileName(tfmDirs[i]);
                    if (string.IsNullOrEmpty(name)) continue;

                    // 去掉前缀 net 并尝试解析出版本
                    var verText = name.StartsWith("net") ? name.Substring(3) : name;
                    if (!Version.TryParse(verText, out var ver)) continue;

                    if (bestVersion == null || ver > bestVersion)
                    {
                        bestVersion = ver;
                        bestDir = tfmDirs[i];
                    }
                }

                if (string.IsNullOrEmpty(bestDir)) return null;
                var exePath = Path.Combine(bestDir, "ServerBase.exe");
                return exePath;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 设置运行时服务器进程名；若传入空则回退到 csproj 文件名。
        /// 用法：在调用 Run() 前根据需求设置特定名称。
        /// </summary>
        public static void SetServerProcessName(string name)
        {
            serverProcessName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(csprojPath) : name;
            Debug(ZString.Format("服务器进程名已设置: {0}", serverProcessName));
        }

        /// <summary>
        /// 解析用于查杀的进程名；优先使用运行时设置，其次使用 csproj 文件名。
        /// </summary>
        private static string ResolveProcessName()
        {
            if (!string.IsNullOrEmpty(serverProcessName)) return serverProcessName;
            return Path.GetFileNameWithoutExtension(csprojPath);
        }

        /// <summary>
        /// 查找并结束所有同名进程，避免编译/运行阶段被占用。
        /// 仅在编译前调用，确保环境干净。
        /// </summary>
        private static void KillSameNameProcesses(string name)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                if (processes == null || processes.Length == 0)
                {
                    Debug(ZString.Format("未发现已运行的进程: {0}", name));
                    return;
                }

                for (int i = 0; i < processes.Length; i++)
                {
                    try
                    {
                        var p = processes[i];
                        if (!p.HasExited) p.Kill();
                        p.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Error(ZString.Format("结束进程失败 {0}: {1}", name, ex.Message));
                    }
                }

                Debug(ZString.Format("已结束同名进程: {0} (数量: {1})", name, processes.Length));
            }
            catch (Exception ex)
            {
                Error(ZString.Format("查询/结束进程时发生错误: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 监听编辑器播放模式状态变化；当退出播放模式时自动停止服务器进程。
        /// 用法：无需手工调用，编辑器域加载后自动生效。
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 退出播放模式时，杀死服务器进程，避免残留占用端口
            if (state == PlayModeStateChange.ExitingPlayMode)
                Stop();
        }
    }
}
