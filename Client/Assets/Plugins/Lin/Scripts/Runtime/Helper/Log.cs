/*
┌────────────────────────────┐
│　Description: 打印辅助
│　Remark: 
└────────────────────────────┘
*/

using UnityEngine;
using System.IO;
using UnityEngine.Scripting;
using Lin.Runtime.Notice;
using Lin.Runtime.Resource;
using Cysharp.Text;

namespace Lin.Runtime.Helper
{
    public static class Log
    {
        private static string logPath, errorPath, exceptionPath;

        private static string logDirectory;
        private static string GetLogDirectory()
        {
            if (string.IsNullOrEmpty(logDirectory))
                logDirectory =
#if UNITY_EDITOR
            "Logs"
#elif UNITY_STANDALONE_WIN
            $"{Application.streamingAssetsPath}/Logs"
#elif UNITY_ANDROID
            $"{Application.persistentDataPath}/Logs"
#else
            //IOS不知道放哪
            string.Empty;
#endif
            ;

            return logDirectory;
        }

        #region ----------- 自动抓取错误 -----------

        [RuntimeInitializeOnLoadMethod, Preserve]
        private static void AutoStart()
        {
            Application.logMessageReceived += CatchLog;
            Debug(nameof(Log), "LogCatcher started.");
        }

        private static void CatchLog(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Assert:
                case LogType.Warning:
                    return;

#if !UNITY_EDITOR && TEST
                case LogType.Exception:
                    var noticer = GlobalConfig_SO.GetInstance().GetNoticer();
                    noticer.Message($"Game: {Application.productName}\nVersion: {Application.version}\n{condition}\n{stackTrace}").Forget();
                    break;
#endif

                default:
                    break;
            }

            string path = GetWriterPath(type);
            WriteLine(condition, path, stackTrace);
        }

        private static string GetWriterPath(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    return GetFilePath(ref errorPath);

                case LogType.Exception:
                    return GetFilePath(ref exceptionPath);

                case LogType.Log:
                    return GetFilePath(ref logPath);

                default:
                    return string.Empty;
            }

            string GetFilePath(ref string path)
            {
                string directory = GetLogDirectory();
                if (path is null)
                {
                    string name = $"{type} {System.DateTime.Now.ToString("yy.MM.dd")}.txt";
                    path = $"{directory}/{name}";
                }

                IOHelper.InsureExist(directory, false);

                if (!File.Exists(path))
                {
                    File.WriteAllLines(path, new string[]{
                        "┌────────────────────────────┐",
                       $"│　CreateDate: {System.DateTime.Now.ToString("yy.MM.dd")}",
                       $"│　Remark: To record the {type} operations", 
                       $"│　Platform: {Application.platform}",
                       $"│　OS: {SystemInfo.operatingSystem}",
                       $"│　CPU: {SystemInfo.processorType}",
                       $"│　GPU: {SystemInfo.graphicsDeviceName}",
                       $"│　Memory: {SystemInfo.systemMemorySize}MB",
                       $"│　Unity Version: {Application.unityVersion}",
                       $"│　Game Version: {Application.version}",
                       $"│　Res Version: {ResLoader.version}",
                        "└────────────────────────────┘",
                        string.Empty
                    });
                }
                return path;
            }
        }

        private static void WriteLine(object line, string path, string stackTrace)
        {
            using var writer = File.AppendText(path);
            writer.WriteLine($"{System.DateTime.Now.ToString("T")} {line}\n{stackTrace}".Replace("<b>", string.Empty).Replace("</b>", string.Empty));
        }

#if UNITY_EDITOR

        [UnityEditor.MenuItem("Lin/Logger/打开日志文件夹")]
        private static void OpenFolder()
        {
            string directory = GetLogDirectory();
            IOHelper.InsureExist(directory, false);
            UnityEditor.EditorUtility.RevealInFinder(directory);
        }

        [UnityEditor.MenuItem("Lin/Logger/清理所有日志")]
        private static void ClearLoggers()
        {
            var result = Directory.GetFiles(GetLogDirectory(), "*.txt", SearchOption.TopDirectoryOnly);
            foreach (var path in result)
                File.Delete(path);

            UnityEngine.Debug.Log("已删除所有logger文件。");
        }

#endif

        #endregion

        #region - 打印拓展 -

        [HideInCallstack]
        public static void Debug(this Object target, object message) => UnityEngine.Debug.Log(ZString.Format("<b>[{0} {1}]</b> {2}", target.name, target.GetType().Name, message), target);

        [HideInCallstack]
        public static void Debug(this object target, object message, Object context = null) => UnityEngine.Debug.Log(ZString.Format("<b>[{0}]</b> {1}", target, message), context);

        [HideInCallstack]
        public static void Debug(string title, object message, Object context = null) => UnityEngine.Debug.Log(ZString.Format("<b>[{0}]</b> {1}", title, message), context);


        [HideInCallstack]
        public static void Warning(this Object target, object message) => UnityEngine.Debug.LogWarning(ZString.Format("<b>[{0} {1}]</b> {2}", target.name, target.GetType().Name, message), target);

        [HideInCallstack]
        public static void Warning(this object target, object message, Object context = null) => UnityEngine.Debug.LogWarning(ZString.Format("<b>[{0}]</b> {1}", target, message), context);

        [HideInCallstack]
        public static void Warning(string title, object message, Object context = null) => UnityEngine.Debug.LogWarning(ZString.Format("<b>[{0}]</b> {1}", title, message), context);


        [HideInCallstack]
        public static void Error(this Object target, object message) => UnityEngine.Debug.LogError(ZString.Format("<b>[{0} {1}]</b> {2}", target.name, target.GetType().Name, message), target);

        [HideInCallstack]
        public static void Error(this object target, object message, Object context = null) => UnityEngine.Debug.LogError(ZString.Format("<b>[{0}]</b> {1}", target, message), context);

        [HideInCallstack]
        public static void Error(string title, object message, Object context = null) => UnityEngine.Debug.LogError(ZString.Format("<b>[{0}]</b> {1}", title, message), context);

        #endregion
    }
}