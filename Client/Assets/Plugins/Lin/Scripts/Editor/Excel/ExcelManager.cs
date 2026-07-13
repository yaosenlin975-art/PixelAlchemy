/*
┌────────────────────────────┐
│　Description: Excel管理器
│　Remark: 
└────────────────────────────┘
*/
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.IO;
using System.Linq;
using System.Threading;
using Lin.Editor.Helper;
using Luban.Editor;

namespace Lin.Editor.Excel
{
    public class ExcelManager : OdinEditorWindow
    {
        [MenuItem("Lin/Excels #e")]
        public static void Init()
        {
            var window = GetWindow<ExcelManager>("Excel管理器");
            window.browserButtons = window.browserButtons ?? new BrowserButtons();
            window.browserButtons.RefreshBrowser();
            window.minSize = new Vector2(500, 280);
            window.Show();
        }

        static string DirectoryPath => Application.dataPath.Replace("/Assets", default);

        #region ----------------- Browser -----------------

        [SerializeField, LabelText("Excel列表"), TabGroup("浏览")]
        [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true, ShowItemCount = false, DraggableItems = false)]
        private ExcelPath[] paths;
        [SerializeField, HideLabel, TabGroup("浏览")]
        private BrowserButtons browserButtons;

        [System.Serializable]
        private class ExcelPath
        {
            public ExcelPath(string path)
            {
                this.path = path.Replace("/", "\\");
                fileName = Path.GetFileNameWithoutExtension(path);
                pathDisplay = path.Substring(path.IndexOf('\\') + 1);
                pathDisplay = pathDisplay.Remove(pathDisplay.IndexOf(fileName) - 1);
            }

            [HideInInspector] public string path;

            private string fileName;
            private string pathDisplay;
            private void Open() => new Thread(() => System.Diagnostics.Process.Start("explorer.exe", path)).Start();
            private void Explorer() => new Thread(() => System.Diagnostics.Process.Start("explorer.exe", path.Substring(0, path.LastIndexOf('\\')))).Start();

            [OnInspectorGUI]
            private void OnInspectorGUI()
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                
                // 创建加粗和大字号的样式
                var boldLargeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 16,
                };
                
                // 创建斜体样式
                var italicStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Italic,
                    fontSize = 10
                };
                
                GUILayout.Label(fileName, boldLargeStyle);
                GUILayout.Label(pathDisplay, italicStyle);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUILayout.Width(100));
                if (GUILayout.Button("打开"))
                    Open();
                if (GUILayout.Button("路径"))
                    Explorer();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        [System.Serializable]
        private class BrowserButtons
        {
            [Button("刷新"), ButtonGroup]
            public void RefreshBrowser()
            {
                var window = GetWindow<ExcelManager>();
                string dirPath = DirectoryPath;
                string[] paths = Directory.GetFiles(dirPath, "*.xlsx", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(dirPath, "*.xls", SearchOption.AllDirectories))
                    .ToArray();

                window.paths = new ExcelPath[paths.Length];
                List<ExcelPath> excelPaths = new List<ExcelPath>();
                for (int i = 0; i < paths.Length; i++)
                {
                    if (paths[i].Contains("~$"))
                        continue;
                    excelPaths.Add(new ExcelPath(paths[i]));
                }
                window.paths = excelPaths.ToArray();
            }

            // 使用名为 "Luban_Client" 的配置运行导出
            // 若未找到对应配置，会在控制台输出错误信息
            [Button("All → Json"), ButtonGroup]
            private void TranslateClient2Json()
            {
                try
                {
                    var config = PathHelper.FindObject<LubanExportConfig>("LubanExportConfig");
                    config.RunCommand();
                }
                catch(Exception e)
                {
                    Debug.LogError("未找到名为 Luban_Client 的配置或执行失败");
                    Debug.LogException(e);
                }
            }
        }

        #endregion
    }
}