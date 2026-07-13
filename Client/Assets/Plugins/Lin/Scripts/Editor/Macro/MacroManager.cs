/*
┌────────────────────────────┐
│　Description: 宏管理窗口
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: MacroManager
└──────────────┘
*/
#if HybridCLR
using HybridCLR.Editor.Settings;
#endif
using Lin.Editor.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using static UnityEditor.PlayerSettings;

namespace Lin.Editor.Macro
{
    class MacroManager : EditorWindow
    {
        static readonly string[] IntrinsicMacros = new string[]
        {
            "TEST",
            "UNITY_2D"
        };

        List<Macro> macros;
        HashSet<string> toRemove;
        string toAdd;
        BuildTargetGroup selectedBuildTargetGroup;

        [MenuItem("Lin/Macros")]
        static void Init()
        {
            var window = GetWindow<MacroManager>("宏管理器");
            window.minSize = window.maxSize = Vector2.one * 500;
            window.Show();
        }

        private void OnEnable()
        {
            selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            toRemove = new HashSet<string>();

            ChangeTarget();
        }

        private void OnGUI()
        {
            bool shouldSave = false;

            GUILayout.BeginHorizontal();
            selectedBuildTargetGroup = (BuildTargetGroup)EditorGUILayout.EnumPopup("目标平台", selectedBuildTargetGroup);
            if (GUILayout.Button("切换"))
                ChangeTarget();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("宏", GUILayout.Width(50));
            toAdd = EditorGUILayout.TextField(toAdd);
            if (GUILayout.Button("添加") && !string.IsNullOrEmpty(toAdd))
            {
                macros.Add(new Macro() { enable = true, name = toAdd });
                toAdd = string.Empty;
                shouldSave = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("已开启");
            int activeCount = 0;
            for (int i = 0; i < macros.Count; i++)
                activeCount += ShowButton(i, true);

            if (activeCount < macros.Count)
            {
                GUILayout.Space(10);
                GUILayout.Label("未开启");
                for (int i = 0; i < macros.Count; i++)
                    ShowButton(i, false);
            }

            while (toRemove.Count > 0)
            {
                var name = toRemove.First();
                int index = macros.FindIndex(macro => macro.name == name);
                macros.RemoveAt(index);
                toRemove.Remove(name);
            }

            if (shouldSave)
                Save();

            int ShowButton(int index, bool targetActive)
            {
                var macro = macros[index];
                if (macro.enable ^ targetActive)
                    return 0;

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(macro.name, GUILayout.Width(295));
                if (GUILayout.Button(targetActive ? "关闭" : "开启"))
                {
                    macro.enable = !targetActive;
                    macros[index] = macro;
                    shouldSave = true;
                }
                else if (GUILayout.Button("移除"))
                {
                    toRemove.Add(macro.name);
                    shouldSave = true;
                }
                GUILayout.EndHorizontal();
                return 1;
            }
        }

        private void ChangeTarget()
        {
            string macroStr = GetScriptingDefineSymbolsForGroup(selectedBuildTargetGroup);
            var macroStrArray = macroStr.Split(';');
            toRemove.Clear();

            Load();

            foreach (var child in macroStrArray)
            {
                int index = macros.FindIndex(m => m.name.Equals(child));
                if (index == -1)
                    macros.Add(new Macro() { name = child, enable = true });
                else
                {
                    var macro = macros[index];
                    macro.enable = true;
                    macros[index] = macro;
                }
            }

            Save();
        }

        #region - Json -

        private string filePath => Path.Combine(Path.GetDirectoryName(Application.dataPath), $"{selectedBuildTargetGroup}-Macros.json");

        void Save()
        {
            macros.RemoveAll(m => string.IsNullOrEmpty(m.name));
            IntrinsicMacro();

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < macros.Count; i++)
            {
                var macro = macros[i];
                if (!macro.enable)
                    continue;

                builder.Append(macro.name);
                if (i != macros.Count - 1)
                    builder.Append(';');
            }
            SetScriptingDefineSymbolsForGroup(selectedBuildTargetGroup, builder.ToString());

            string json = JsonConvert.SerializeObject(macros);
            File.WriteAllText(filePath, json);
            AssetDatabase.Refresh();
        }

        void Load()
        {
            if (!File.Exists(filePath))
            {
                macros = new List<Macro>();
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                macros = JsonConvert.DeserializeObject<List<Macro>>(json);
            }
            catch (Exception)
            {
                macros = new List<Macro>();
            }
            IntrinsicMacro();
        }

        void IntrinsicMacro()
        {
            foreach (var macroName in IntrinsicMacros)
            {
                int index = macros.FindIndex(m => m.name.Equals(macroName));
                if (index != -1)
                    continue;

                macros.Add(new Macro() { name = macroName, enable = false });
            }
        }

        #endregion

        [Serializable]
        struct Macro
        {
            public string name;
            public bool enable;
        }

        [DidReloadScripts]
        private static async void CheckPackages()
        {
            bool importedHybrid = await PluginHelper.Check("com.code-philosophy.hybridclr");
            SetMacro("HybridCLR", importedHybrid);
#if HybridCLR
            try
            {
                SetMacro("HybridCLR", HybridCLRSettings.Instance.enable);
            }
            catch (Exception)
            {
                SetMacro("HybridCLR", false);
            }
#endif

            bool importedLuban = await PluginHelper.Check("com.code-philosophy.luban");
            SetMacro("Luban", importedLuban);

            bool importedYooAsset = await PluginHelper.Check("com.tuyoogame.yooasset");
            SetMacro("YooAsset", importedYooAsset);

            var type = Type.GetType("Cysharp.Threading.Tasks.UniTask,UniTask");
            SetMacro("UniTask", type != null);
        }

        private static void SetMacro(string macro, bool enable)
        {
            string macroStr = GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            bool hasMacro = macroStr.Contains(macro);
            if (enable && !hasMacro)
                macroStr = $"{macroStr};{macro}";
            else if (!enable && hasMacro)
                macroStr = macroStr.Replace($";{macroStr}", string.Empty).Replace($"{macroStr};", string.Empty);

            SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, macroStr);
        }
    }
}
