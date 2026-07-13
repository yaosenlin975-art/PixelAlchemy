using Lin.Editor.BuildTool;
using Lin.Editor.Helper;
using Lin.Runtime.Helper;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.Scene
{
    public class SceneSwitcher : EditorWindow
    {
        //Shift Z前最后一个场景
        public const string LAST_SCENE_KEY = nameof(LAST_SCENE_KEY);

        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;
        private VisualTreeAsset sceneButtonPrefab;

        private HashSet<string> addedScenes = new HashSet<string>();

        private string[] allScenes;

        [MenuItem("Lin/Scene Switcher #s")]
        public static void ShowExample()
        {
            SceneSwitcher wnd = GetWindow<SceneSwitcher>();
            wnd.titleContent = new GUIContent("场景切换");
            wnd.minSize = wnd.maxSize = new Vector2(600, 518);
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            sceneButtonPrefab = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/Scene/SceneSwitcher/SceneButton.uxml");
            root.Q<Button>("CreateToolScene").clicked += OnCreateToolSceneBtnClick;
            root.Q<Button>("RefreshButton").clicked += OnRefreshBtnClick;
            root.Q<TextField>("SearchField").RegisterValueChangedCallback(SearchScenes);

            OnRefreshBtnClick();
        }

        private void OnRefreshBtnClick()
        {
            allScenes = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);
            if (allScenes.Any())
            {
                for (int i = 0; i < allScenes.Length; i++)
                    allScenes[i] = allScenes[i].Replace("\\", "/");

                Array.Sort(allScenes);
            }

            InitBuildSettingScenes();
            InitYooAssetScenes();
            InitToolScenes();
            InitOtherScenes();
            InitLastScene();
            //刷新窗口大小
        }

        private void InitLastScene()
        {
            var lastSceneContainer = rootVisualElement.Q("LastScene");
            while (lastSceneContainer.childCount > 0)
                lastSceneContainer.RemoveAt(0);

            var lastScene = PrefsHelper.Get(LAST_SCENE_KEY, string.Empty);
            if (!string.IsNullOrEmpty(lastScene))
            {
                var sceneBtn = CreateSceneButton(lastScene);
                lastSceneContainer.Add(sceneBtn);
            }
        }

        private void SearchScenes(ChangeEvent<string> evt)
        {
            if (evt.newValue.IsNullOrWhitespace())
                return;

            var lower = evt.newValue.ToLower();
            AddSceneButtons(allScenes.Where(s => Path.GetFileNameWithoutExtension(s).ToLower().Contains(lower)), "SearchListView");
        }

        private void OnCreateToolSceneBtnClick()
        {
            // 获取可用的资源名
            string basePath = "Assets/Scenes/Tool/";
            string sceneName = "ToolScene";
            string fileName = $"{basePath}{sceneName}.unity";

            if (File.Exists(fileName))
            {
                int index = 1;

                while (File.Exists($"{basePath}{sceneName}{index}.unity"))
                    index++;

                fileName = $"{basePath}{sceneName}{index}.unity";
            }

            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), fileName);

            // 刷新并重新加载工具场景列表
            AssetDatabase.Refresh();
            InitToolScenes();
        }

        private void InitBuildSettingScenes()
        {
            var scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
            Array.Sort(scenes);
            addedScenes.AddRange(scenes);
            AddSceneButtons(scenes, "BuildSettingsListView");
        }

        private void InitYooAssetScenes()
        {
            var settings = YooAssetHelper.LoadAssetBundleCollectorSetting();
            var scenes = new List<string>();

            // 遍历所有包的配置
            foreach (var package in settings.Packages)
            {
                // 遍历包内所有收集器组
                foreach (var group in package.Groups)
                {
                    // 遍历组内所有收集器
                    foreach (var collector in group.Collectors)
                    {
                        var collectPath = collector.CollectPath;

                        if (Directory.Exists(collectPath))
                        {
                            // 获取收集器目录下所有场景文件
                            var sceneFiles = Directory.GetFiles(collectPath, "*.unity", SearchOption.AllDirectories);
                            scenes.AddRange(sceneFiles.Select(path => path.Replace("\\", "/")));
                        }
                        else if (collectPath.EndsWith(".unity"))
                            scenes.Add(collectPath);
                    }
                }
            }

            if (scenes.Any())
            {
                scenes.Sort();
                addedScenes.AddRange(scenes);
                AddSceneButtons(scenes, "YooAssetListView");
            }
        }

        private void InitToolScenes()
        {
            IOHelper.InsureExist("Assets/Scenes/Tool", false, false);
            var sceneFiles = Directory.GetFiles("Assets/Scenes/Tool", "*.unity", SearchOption.AllDirectories);
            if (sceneFiles.Any())
            {
                for (int i = 0; i < sceneFiles.Length; i++)
                    sceneFiles[i] = sceneFiles[i].Replace("\\", "/");

                Array.Sort(sceneFiles);
                addedScenes.AddRange(sceneFiles);
                AddSceneButtons(sceneFiles, "ToolListView");
            }
        }

        private void InitOtherScenes() => AddSceneButtons(allScenes.Where(s => !addedScenes.Contains(s)), "OtherListView");

        private void AddSceneButtons(IEnumerable<string> scenes, string listViewName)
        {
            var content = rootVisualElement.Q<ListView>(listViewName).Q("unity-content-container");
            while (content.childCount > 0)
                content.RemoveAt(0);

            foreach (var scene in scenes)
            {
                var button = CreateSceneButton(scene);
                content.Add(button);
            }
        }

        private VisualElement CreateSceneButton(string scenePath)
        {
            var button = sceneButtonPrefab.Instantiate().Query().AtIndex(1);
            button.Q<Button>("SwitchButton").clicked += () => EditorSceneManager.OpenScene(scenePath);
            button.Q<Button>("SelectButton").clicked += () => {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath);
                EditorGUIUtility.PingObject(sceneAsset);
                Selection.activeObject = sceneAsset;
            };
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            button.Q<Label>("SceneName").text = Path.GetFileNameWithoutExtension(scenePath);
            button.Q<Label>("ScenePath").text = scenePath.Replace("Assets/", string.Empty).Replace($"/{sceneName}.unity", string.Empty);
            return button;
        }
    }
}
