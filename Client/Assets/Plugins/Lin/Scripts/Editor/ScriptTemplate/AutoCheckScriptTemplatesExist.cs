/*
┌────────────────────────────┐
│　Description: 自动检测脚本模板是否存在, 不存在则复制
│　Author: 花球i
│　Remark: 
└────────────────────────────┘
*/

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.ScriptTemplate
{
    public static class AutoCheckScriptTemplatesExist
{
    [UnityEditor.Callbacks.DidReloadScripts]
    static void OnScriptReloaded()
    {
        string directoryPath = Application.dataPath + "/ScriptTemplates";
        if (Directory.Exists(directoryPath))
            return;

        CopyTemplates();
    }

    [MenuItem("Lin/脚本模板")]
    static void CopyTemplates()
    {
        string directoryPath = Application.dataPath + "/ScriptTemplates";
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        //Assets/Plugins/Lin/Scripts/Editor/ScriptTemplate/ScriptTemplates
        string[] scriptTempaltes = Directory.GetFiles("Assets/Plugins/Lin/Scripts/Editor/ScriptTemplate/ScriptTemplates", "*.txt");
        foreach (var st in scriptTempaltes)
        {
            FileInfo fi = new FileInfo(st);
            string destination = $"{directoryPath}/{fi.Name}";
            if (File.Exists(destination))
                continue;

            File.Copy(st, destination);
        }

        if (EditorUtility.DisplayDialog("脚本模板", "脚本复制完成, 重启编辑器后生效, 是否立即重启编辑器？", "立即重启", "稍后自行重启"))
            EditorApplication.OpenProject(Application.dataPath.Replace("Assets", string.Empty));
    }
}
}
