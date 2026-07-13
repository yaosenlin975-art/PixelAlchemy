/*
┌────────────────────────────┐
│　Description: GlobalConfig 自定义 Inspector 绘制
│　Remark: 从 GlobalConfig_SO.cs 拆出，独立命名空间
└────────────────────────────┘
*/
using Lin.Runtime;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Config
{
    public class GlobalConfigDrawer
    {
        private GlobalConfig_SO globalConfig;
        public SerializedObject globalConfigObject { get; }
        public SerializedProperty isEditorMode { get; }
#if !UNITY_WEBGL
        public SerializedProperty isStandaloneMode { get; }
#endif
        public SerializedProperty cdnConfigs { get; }
        public SerializedProperty currentEnvironmentId { get; }
        public SerializedProperty runServer { get; }
        public SerializedProperty prefabDirectory { get; }
        public SerializedProperty configDirectory { get; }

        public GlobalConfigDrawer()
        {
            globalConfig = GlobalConfig_SO.GetInstance();
            globalConfigObject = new SerializedObject(globalConfig);
            isEditorMode = globalConfigObject.FindProperty(nameof(globalConfig.isEditorMode));
#if !UNITY_WEBGL
            isStandaloneMode = globalConfigObject.FindProperty(nameof(globalConfig.isStandaloneMode));
#endif
            cdnConfigs = globalConfigObject.FindProperty(nameof(globalConfig.cdnConfigs));
            currentEnvironmentId = globalConfigObject.FindProperty(nameof(globalConfig.currentEnvironmentId));
            runServer = globalConfigObject.FindProperty(nameof(globalConfig.runServer));

            prefabDirectory = globalConfigObject.FindProperty(nameof(prefabDirectory));
            configDirectory = globalConfigObject.FindProperty(nameof(configDirectory));
        }

        public void Draw()
        {
            EditorGUILayout.PropertyField(isEditorMode, new GUIContent("编辑器加载"));

#if !UNITY_WEBGL
            EditorGUILayout.PropertyField(isStandaloneMode, new GUIContent("StreamingAssets加载"));
            if (!isStandaloneMode.boolValue)
            {
                DrawEnvironmentPopup();
                EditorGUILayout.PropertyField(cdnConfigs, new GUIContent("CDN环境配置"), true);
            }
#else
            DrawEnvironmentPopup();
            EditorGUILayout.PropertyField(cdnConfigs, new GUIContent("CDN环境配置"), true);
#endif
            EditorGUILayout.PropertyField(runServer, new GUIContent("运行服务器"));

            globalConfigObject.ApplyModifiedProperties();
            globalConfig.OnInspectorGUI();
        }

        /// <summary>
        /// 以环境名(展示用)+ ID(存储用)的形式手写当前环境 Popup,
        /// 兼容 EditorGUILayout.PropertyField 不会触发 Odin [ValueDropdown] 的问题
        /// </summary>
        private void DrawEnvironmentPopup()
        {
            var configs = globalConfig.cdnConfigs;
            var labels = new System.Collections.Generic.List<string>();
            var ids = new System.Collections.Generic.List<string>();

            if (configs != null)
            {
                foreach (var c in configs)
                {
                    if (c == null)
                        continue;
                    labels.Add(string.IsNullOrEmpty(c.name) ? c.id : c.name);
                    ids.Add(c.id);
                }
            }

            if (labels.Count == 0)
            {
                EditorGUILayout.HelpBox("暂未配置任何 CDN 环境,请先在下方列表添加条目。", MessageType.Warning);
                return;
            }

            int currentIndex = ids.IndexOf(currentEnvironmentId.stringValue);
            if (currentIndex < 0)
                currentIndex = 0; // 找不到时兜底为第一条

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(new GUIContent("当前环境"), currentIndex, labels.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                currentEnvironmentId.stringValue = ids[selected];
                globalConfig.SwitchEnvironment(ids[selected]);
            }
        }
    }
}
