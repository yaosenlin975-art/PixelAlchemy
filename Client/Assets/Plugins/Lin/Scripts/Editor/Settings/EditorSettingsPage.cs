using Lin.Editor.Asset;
using Lin.Editor.Config;
using Lin.Editor.Helper;
using Lin.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace Lin.Editor.Settings
{
    static class EditorSettingsPage
    {
        private static EditorSettings_SO settings;
        private static SerializedObject settingsObject;

        // Excel 
        private static SerializedProperty jsonOutput;
        private static SerializedProperty cSharpOutput;
        private static SerializedProperty prefabsOutput;

        // CDN
        private static GlobalConfigDrawer globalConfigDrawer;

        // Asset Summary
        private static SerializedProperty descriptionFilters;
        private static SerializedProperty assetSummaryTitleSize;
        private static SerializedProperty assetSummaryEditWay;

        private static SerializedProperty sceneObjectDescriptionTitleSize;
        private static SerializedProperty sceneObjectDescriptionEditWay;

        private static SerializedProperty scriptDscColor;
        private static SerializedProperty scriptDscBold;
        private static SerializedProperty scriptDscItalic;

        // SceneView
        private static SerializedProperty drawRendererInfos_SV;
        private static SerializedProperty infosColor_SV;

        // TODO 过滤
        private static SerializedProperty todoIgnoreFolders;
        private static SerializedProperty todoIgnoreAssemblies;

        [SettingsProvider]
        private static SettingsProvider InProjectSettings()
        {
            return new SettingsProvider("Project/Lin Settings", SettingsScope.Project)
            {
                activateHandler = OnSettingsActive,
                guiHandler = OnSettingsGUI,
            };
        }

        [MenuItem("Lin/Settings", priority = 100)]
        private static void OpenSettingsPage() => SettingsService.OpenProjectSettings("Project/Lin Settings");

        private static void OnSettingsActive(string searchContext, VisualElement rootElement)
        {

            if (globalConfigDrawer is null)
            {
                globalConfigDrawer = new GlobalConfigDrawer();
            }

            if (settings is null)
            {
                settings = EditorSettings_SO.GetInstance();
                settingsObject = new SerializedObject(settings);
                jsonOutput = globalConfigDrawer.configDirectory;
                cSharpOutput = settingsObject.FindProperty(nameof(settings.cSharpOutput));

                assetSummaryTitleSize = settingsObject.FindProperty(nameof(settings.assetSummaryTitleSize));
                assetSummaryEditWay = settingsObject.FindProperty(nameof(settings.assetSummaryEditWay));

                sceneObjectDescriptionTitleSize = settingsObject.FindProperty(nameof(settings.sceneObjectDescriptionTitleSize));
                sceneObjectDescriptionEditWay = settingsObject.FindProperty(nameof(settings.sceneObjectDescriptionEditWay));

                scriptDscColor = settingsObject.FindProperty(nameof(settings.scriptDscColor));
                scriptDscBold = settingsObject.FindProperty(nameof(settings.scriptDscBold));
                scriptDscItalic = settingsObject.FindProperty(nameof(settings.scriptDscItalic));
                descriptionFilters = settingsObject.FindProperty(nameof(settings.descriptionFilters));
                prefabsOutput = globalConfigDrawer.prefabDirectory;

                drawRendererInfos_SV = settingsObject.FindProperty(nameof(settings.drawRendererInfos_SV));
                infosColor_SV = settingsObject.FindProperty(nameof(settings.infosColor_SV));

                todoIgnoreFolders = settingsObject.FindProperty(nameof(settings.todoIgnoreFolders));
                todoIgnoreAssemblies = settingsObject.FindProperty(nameof(settings.todoIgnoreAssemblies));
            }
        }

        private static void OnSettingsGUI(string searchContext)
        {
            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            Title("Common", 0);
            EditorGUILayout.PropertyField(prefabsOutput, new GUIContent("预制体输出路径"));

            Title("C#");
            EditorGUILayout.PropertyField(cSharpOutput, new GUIContent("脚本输出路径"));

            Title("Excel");
            EditorGUILayout.PropertyField(jsonOutput, new GUIContent("Json输出路径"));

            Title("Asset Summary");
            EditorGUILayout.PropertyField(assetSummaryTitleSize, new GUIContent("注释字体大小"));
            EditorGUILayout.PropertyField(assetSummaryEditWay, new GUIContent("点击进入修改"));

            Title("Scene Object Description");
            EditorGUILayout.PropertyField(sceneObjectDescriptionTitleSize, new GUIContent("注释字体大小"));
            EditorGUILayout.PropertyField(sceneObjectDescriptionEditWay, new GUIContent("点击进入修改"));

            Title("Script Descriptions");
            EditorGUILayout.PropertyField(scriptDscColor, new GUIContent("颜色"));
            EditorGUILayout.PropertyField(scriptDscBold, new GUIContent("粗体"));
            EditorGUILayout.PropertyField(scriptDscItalic, new GUIContent("斜体"));
            EditorGUILayout.PropertyField(descriptionFilters, new GUIContent("描述标识"));

            Title("SceneView");
            EditorGUILayout.PropertyField(drawRendererInfos_SV, new GUIContent("展示模型信息"));
            EditorGUILayout.PropertyField(infosColor_SV, new GUIContent("绘制颜色"));

            Title("Todo Filter");
            EditorGUILayout.PropertyField(todoIgnoreFolders, new GUIContent("忽略的文件夹"), true);
            EditorGUILayout.PropertyField(todoIgnoreAssemblies, new GUIContent("忽略的程序集"), true);

            if (settingsObject.ApplyModifiedProperties())
                AssetSummaryDrawer.Refresh();

            Title("Resources");
            globalConfigDrawer.Draw();

            void Title(string message, float space = 10)
            {
                GUILayout.Space(space);
                GUILayout.Label(message, title);
            }
        }
    }
}
