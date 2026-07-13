// ================================================================================
// Fantasy.Net 服务器应用程序入口
// ================================================================================
// 本文件是 Fantasy.Net 分布式游戏服务器的主入口点
//
// 初始化流程：
//   1. 注册全局未处理异常钩子（AppDomain + TaskScheduler），确保崩溃可被记录
//   2. 强制加载引用程序集，触发 ModuleInitializer 执行
//   3. 配置日志基础设施（NLog）
//   4. 启动 Fantasy.Net 框架
// ================================================================================

using System.Threading.Tasks;
using Fantasy;
using NLog;

// 注册全局未处理异常钩子,在主流程启动之前安装,确保任何后续异常都可被记录
// Register global unhandled-exception hooks before main flow starts, so any
// subsequent crash is recorded before the process exits.
RegisterGlobalExceptionHandlers();

try
{
    // 初始化引用的程序集，确保 ModuleInitializer 执行
    // .NET 采用延迟加载机制 - 仅当类型被引用时才加载程序集
    // 通过访问 AssemblyMarker 强制加载程序集并调用 ModuleInitializer
    // 注意：Native AOT 不存在延迟加载问题，所有程序集在编译时打包
    AssemblyHelper.Initialize();
    // 配置 NLog 日志基础设施
    var logger = new Fantasy.NLog("Server");
    // 使用 NLog 日志系统启动 Fantasy.Net 框架
    await Fantasy.Platform.Net.Entry.Start(logger);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"服务器初始化过程中发生致命错误：{ex}");
    Environment.Exit(1);
}

// 职责：注册 AppDomain 与 TaskScheduler 的未处理异常钩子,使用 NLog 记录。
// Responsibility: Register AppDomain and TaskScheduler unhandled-exception hooks,
//                log via NLog. Does not alter the existing try/catch + Exit flow.
static void RegisterGlobalExceptionHandlers()
{
    var logger = LogManager.GetLogger("Server");

    // AppDomain 级别的未捕获异常（含非异步线程、未观察异常的目标线程）
    // AppDomain-level unhandled exceptions (non-async threads, finalizer, etc.)
    AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    {
        var ex = args.ExceptionObject as Exception;
        if (ex != null)
        {
            logger.Fatal(ex, "[AppDomain.UnhandledException] IsTerminating={IsTerminating}", args.IsTerminating);
        }
        else
        {
            logger.Fatal("[AppDomain.UnhandledException] Non-Exception object thrown. IsTerminating={IsTerminating}", args.IsTerminating);
        }
    };

    // Task 中未观察到的异常（async/await 链路中未捕获的错误）
    // Unobserved task exceptions (errors swallowed in async/await chains)
    TaskScheduler.UnobservedTaskException += (_, args) =>
    {
        logger.Error(args.Exception, "[TaskScheduler.UnobservedTaskException]");
        // 标记为已观察,避免进程被默认终止策略结束
        // Mark as observed to keep the existing try/catch + Environment.Exit(1) flow authoritative.
        args.SetObserved();
    };
}
