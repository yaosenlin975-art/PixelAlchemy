using UnityEngine;

namespace IngameDebugConsole.Commands
{
    public class IOCommands
    {
        [ConsoleMethod("PDPath", "打印 Application.persistentDataPath"), UnityEngine.Scripting.Preserve]
        public static void PersistentDataPath()
        {
            Debug.Log(Application.persistentDataPath);
        }
    }
}