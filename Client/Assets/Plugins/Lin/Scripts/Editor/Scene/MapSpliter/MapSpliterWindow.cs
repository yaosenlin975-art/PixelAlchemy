/*
┌────────────────────────────┐
│　Description：大地图分拆器
│　Remark：
└────────────────────────────┘
*/
using Lin.Editor.BuildTool;
using Lin.Editor.Helper;
using Lin.Editor.Scene.Spliter.Tree;
using Lin.Runtime;
using Lin.Runtime.Helper;
using Lin.Runtime.Map;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset.Editor;
using static Lin.Runtime.Map.MapLoader;
using Object = UnityEngine.Object;

namespace Lin.Editor.Scene.Spliter
{
    [Serializable]
    struct SpliterSettings
    {
        [FolderPath]
        [LabelText("输出路径")]
        public string outputDir;

        [LabelText("复制到输出路径")]
        [Tooltip("外部预制体复制到输出路径")]
        public bool copyIntersectedObjects;

        [EnumToggleButtons]
        [LabelText("分拆方式")]
        public ESlpiterType sliterType;


        private const string TERRAIN_2_MESH = "Terrain转Mesh";
        [BoxGroup(TERRAIN_2_MESH)]
        [LabelText(TERRAIN_2_MESH)]
        public bool terrain2Mesh;

        [Range(50, 500)]
        [LabelText("顶点数")]
        [BoxGroup(TERRAIN_2_MESH)]
        [ShowIf(nameof(terrain2Mesh))]
        public int vertexCount;

        [LabelText("处理坑洞")]
        [BoxGroup(TERRAIN_2_MESH)]
        [ShowIf(nameof(terrain2Mesh))]
        public bool exportHoles;

        [LabelText("纹理大小")]
        [BoxGroup(TERRAIN_2_MESH)]
        [ShowIf(nameof(terrain2Mesh))]
        public EResolution resolution;
        public enum EResolution
        {
            [LabelText("64")]
            _64 = 64,

            [LabelText("128")]
            _128 = 128,

            [LabelText("256")]
            _256 = 256,

            [LabelText("512")]
            _512 = 512,

            [LabelText("1024")]
            _1024 = 1024,

            [LabelText("2048")]
            _2048 = 2048,

            [LabelText("4096")]
            _4096 = 4096,

            [LabelText("8192")]
            _8192 = 8192
        }

        public static SpliterSettings GetDefault()
        {
            var globalSettings = GlobalConfig_SO.GetInstance();
            return new SpliterSettings
            {
                sliterType = ESlpiterType.QUADTREE,
                copyIntersectedObjects = false,
                exportHoles = true,
                outputDir = $"{globalSettings.prefabDirectory}/Map Split",
                resolution = EResolution._512,
                terrain2Mesh = true,
                vertexCount = 100
            };
        }
    }

    class MapSpliterWindow : OdinEditorWindow
    {
        private static readonly string[] ignoreTags = new string[] { };

        [Range(1, 7), LabelText("分拆深度"), OnValueChanged(nameof(Refresh))] 
        public int targetDepth;
        private const string NODE_DEPTH_KEY = nameof(NODE_DEPTH_KEY);

        [HideLabel]
        public SpliterSettings settings;
        private const string MAP_SPLITER_SETTINGS_KEY = nameof(MAP_SPLITER_SETTINGS_KEY);

        private Bounds mapBounds;

        [SerializeField, LabelText("场景搜索")]
        private string searchKeyWords;
        private List<SceneInfos> sceneList;
        private Vector2 sceneListScrollPos;

        [MenuItem("Lin/大地图分拆")]
        private static void Init()
        {
            var window = GetWindow<MapSpliterWindow>("地图分拆");
            window.minSize = new Vector2(370, 400);
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            settings = PrefsHelper.Get(MAP_SPLITER_SETTINGS_KEY, SpliterSettings.GetDefault());
            targetDepth = PrefsHelper.Get(NODE_DEPTH_KEY, 3);
            sceneList = SceneUtilities.GetAllSceneInfos();

            CalculateMapBounds(default, default);
            EditorSceneManager.activeSceneChangedInEditMode += CalculateMapBounds;
            SceneView.duringSceneGui += DrawMapBounds;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PrefsHelper.Set(NODE_DEPTH_KEY, targetDepth);
            PrefsHelper.Set(MAP_SPLITER_SETTINGS_KEY, settings);
            EditorSceneManager.activeSceneChangedInEditMode -= CalculateMapBounds;
            SceneView.duringSceneGui -= DrawMapBounds;
        }

        [OnInspectorGUI]
        private void ListScenes()
        {
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Label("场景列表");
            if (GUILayout.Button("复原", GUILayout.Width(100)))
                RecoveryBackUp();
            if (GUILayout.Button("刷新", GUILayout.Width(100)))
                Refresh();
            GUILayout.EndHorizontal();

            bool isEmpty = string.IsNullOrEmpty(searchKeyWords);
            sceneListScrollPos = GUILayout.BeginScrollView(sceneListScrollPos, GUILayout.Height(200));
            for (int i = 0; i < sceneList.Count; i++)
            {
                var scene = sceneList[i];
                if (!isEmpty && !scene.name.Contains(searchKeyWords))
                    continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(scene.name, GUILayout.Width(200)))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(scene.path));
                    EditorSceneManager.OpenScene(scene.path);
                }
                scene.select = EditorGUILayout.Toggle(scene.select);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void RecoveryBackUp()
        {
            for (int i = 0; i < sceneList.Count; i++)
            {
                var scene = sceneList[i];
                if (scene.name.EndsWith("备份"))
                {
                    var originalScenePath = scene.path.Replace("_备份", string.Empty);
                    AssetDatabase.CopyAsset(scene.path, originalScenePath);
                }
            }
            AssetDatabase.Refresh();
        }

        private void Refresh()
        {
            CalculateMapBounds(default, default);
        }

        [Button, ButtonGroup]
        private void All()
        {
            for (int i = 0; i < sceneList.Count; i++)
                sceneList[i].select = true;
        }

        [Button, ButtonGroup]
        private void None()
        {
            for (int i = 1; i < sceneList.Count; i++)
                sceneList[i].select = false;
        }

        [Button("分拆")]
        private void Split()
        {
            //ObjectInScene, PrefabPath
            Dictionary<GameObject, string> pathMap = new Dictionary<GameObject, string>();  
            HashSet<string> paths = new HashSet<string>();
            StringBuilder configBuilder = new StringBuilder();

            for (int i = 0; i < sceneList.Count; i++)
            {
                var scene = sceneList[i];
                if (!scene.select)
                    continue;

                if (!SceneManager.GetActiveScene().path.Equals(scene.path))
                    EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);

                //备份场景
                var currentScene = SceneManager.GetActiveScene();
                var originalScenePath = scene.path.Replace(scene.name, $"{scene.name}_备份");
                EditorSceneManager.SaveScene(currentScene, originalScenePath, false);
                EditorSceneManager.OpenScene(scene.path);
                currentScene = SceneManager.GetActiveScene();

                //提出Terrain中的树, 删除Terrain
                Terrain2Mesh(scene);

                //计算边界
                MeshRenderer[] targets = FindObjectsOfType<MeshRenderer>();
                TreeNodeBaseEditor spliter;
                Bounds rootBounds;
                switch (settings.sliterType)
                {
                    case ESlpiterType.QUADTREE:
                        rootBounds = QuadtreeNode.CalculateRootBounds(mapBounds);
                        spliter = new QuadtreeNode(rootBounds, 0, targetDepth);
                        break;

                    case ESlpiterType.OCTREE:
                    default:
                        rootBounds = OctreeNode.CalculateRootBounds(mapBounds);
                        spliter = new OctreeNode(rootBounds, 0, targetDepth);
                        break;
                }

                string dir = $"{settings.outputDir}/{scene.name.RemoveSymbols()}";
                IOHelper.InsureExist(dir, false, true);
                spliter.Split(targets, ignoreTags);
                spliter.WriteConfig(dir, pathMap, paths, settings.copyIntersectedObjects);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                //写入分割配置（地图大小，深度，地址表）
                var loader = FindObjectOfType<MapLoader>() ?? new GameObject("MapLoader").AddComponent<MapLoader>();
                loader.mapBounds = rootBounds;
                loader.sourceDirectory = dir;
                loader.slpiterType = settings.sliterType;

                //块列表
                SaveChunkList(dir, configBuilder);

                //地址表
                SavePathList(paths, configBuilder, dir);

                //YooAsset 写入资源收集器
                Write2YooCollector(currentScene, paths, dir);

                //删除场景内的物体
                foreach (var pair in pathMap)
                    DestroyImmediate(pair.Key);

                //清理场景中无用GameObject
                var gameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var gameObject in gameObjects)
                {
                    if (gameObject.CompareTag("Untagged") && gameObject.layer == 0 && gameObject.transform.childCount == 0)
                    {
                        var cmps = gameObject.GetComponentsInChildren<Component>();
                        if (cmps.Length == 1)
                            DestroyImmediate(gameObject);
                    }
                }

                //覆盖地图
                EditorSceneManager.SaveScene(currentScene);
            }
            AssetDatabase.Refresh();
        }

        private void CalculateMapBounds(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene __)
        {
            mapBounds = SceneUtilities.CalculateMapBounds();
            if (float.IsNegativeInfinity(mapBounds.size.x))
                mapBounds = new Bounds();
        }

        private void DrawMapBounds(SceneView obj)
        {
            var size = mapBounds.size;
            float maxSize = Mathf.Max(size.x, size.y, size.z);

            Handles.color = Color.blue;
            Handles.DrawWireCube(mapBounds.center, mapBounds.size);
            Handles.Label(mapBounds.min, $"地图大小 {mapBounds.size}", new GUIStyle() { normal = new GUIStyleState() { textColor = Color.blue } });

            var chunkBounds = new Bounds();
            Bounds rootBounds;
            float chunkSize = maxSize / Mathf.Pow(2, targetDepth);
            switch (settings.sliterType)
            {
                case ESlpiterType.QUADTREE:
                    rootBounds = QuadtreeNode.CalculateRootBounds(mapBounds);

                    chunkBounds.center = rootBounds.min + new Vector3(chunkSize, rootBounds.size.y, chunkSize) / 2f;
                    chunkBounds.size = new Vector3(chunkSize, mapBounds.size.y, chunkSize);
                    break;

                case ESlpiterType.OCTREE:
                default:
                    rootBounds = OctreeNode.CalculateRootBounds(mapBounds);

                    chunkBounds.center = rootBounds.min + Vector3.one * chunkSize / 2f;
                    chunkBounds.size = Vector3.one * chunkSize;
                    break;
            }
            //分块
            Handles.color = Color.red;
            Handles.DrawWireCube(chunkBounds.center, chunkBounds.size);
            Handles.Label(chunkBounds.min + chunkSize * Vector3.up, $"分块大小 {chunkBounds.size}", new GUIStyle() { normal = new GUIStyleState() { textColor = Color.red } });
            //根节点
            Handles.color = Color.green;
            Handles.DrawWireCube(rootBounds.center, rootBounds.size);
            Handles.Label(rootBounds.min + Vector3.up * mapBounds.size.y, $"根节点大小 {rootBounds.size}", new GUIStyle() { normal = new GUIStyleState() { textColor = Color.green } });
        }

        /// <summary>
        /// 将当前地图有物体的块记录
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="configBuilder"></param>
        private void SaveChunkList(string dir, StringBuilder configBuilder)
        {
            configBuilder.Clear();
            var chunkConfigs = Directory.GetFiles(dir, "*-*.txt");
            foreach (var config in chunkConfigs)
            {
                if (configBuilder.Length > 0)
                    configBuilder.AppendLine();
                configBuilder.Append(Path.GetFileNameWithoutExtension(config));
            }
            File.WriteAllText($"{dir}/{CHUNKS_INDEX_FILE_NAME}", configBuilder.ToString());
        }

        private void SavePathList(IEnumerable<string> paths, StringBuilder configBuilder, string dir)
        {
            //地址表
            configBuilder.Clear();
            foreach (var path in paths)
            {
                if (configBuilder.Length > 0)
                    configBuilder.AppendLine();
                configBuilder.Append(path);
            }
            File.WriteAllText($"{dir}/{PREFAB_PATHS_FILE_NAME}", configBuilder.ToString());
        }

        /// <summary>
        /// 写入YooAsset资源收集器
        /// </summary>
        /// <param name="currentScene"></param>
        /// <param name="paths"></param>
        private void Write2YooCollector(UnityEngine.SceneManagement.Scene currentScene, IEnumerable<string> paths, string dir)
        {
            //找到场景所在包
            AssetBundleCollectorSetting settings = YooAssetHelper.LoadAssetBundleCollectorSetting();
            var isExisted = settings.FindPackage(currentScene.path, out var package);
            if (!isExisted)
            {
                Debug.LogWarning($"未将场景 {currentScene.name} 放入资源收集器中，现已新建对应收集器");
                package = new AssetBundleCollectorPackage()
                {
                    PackageName = currentScene.name.Replace(" ", string.Empty),
                    PackageDesc = "Generated by code.",
                    AutoCollectShaders = true,
                    Groups = new List<AssetBundleCollectorGroup>
                    {
                        new AssetBundleCollectorGroup()
                        {
                            GroupName = "Scene",
                            GroupDesc = "Generated by code.",
                            ActiveRuleName = "EnableGroup",
                            Collectors = new List<AssetBundleCollector>()
                            {
                                new AssetBundleCollector()
                                {
                                    CollectPath = currentScene.path,
                                    CollectorGUID = AssetDatabase.AssetPathToGUID(currentScene.path),
                                    CollectorType = ECollectorType.MainAssetCollector,
                                    AddressRuleName = "AddressByFileName",
                                    PackRuleName = "PackDirectory",
                                    FilterRuleName = "CollectAll"
                                }
                            }
                        }
                    }
                };
                settings.Packages.Add(package);
            }

            var group = package.InsureGroupExist("MapSpliter");
            group.Collectors.Clear();

            //把输出文件夹整个加入收集器中
            Add2Collector(dir);

            //预制体写入资源收集
            foreach (var assetPath in paths)
            {
                if (assetPath.StartsWith("../"))
                    continue;

                Add2Collector(assetPath);
            }

            UnityEditor.EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.Refresh();

            void Add2Collector(string assetPath)
            {
                assetPath = assetPath.Replace("..", dir);
                if (!assetPath.StartsWith("Assets") || package.Contains(assetPath))
                    return;

                group.Collectors.Add(new AssetBundleCollector()
                {
                    CollectPath = assetPath,
                    CollectorGUID = AssetDatabase.AssetPathToGUID(assetPath),
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = "AddressByFileName",
                    PackRuleName = "PackDirectory",
                    FilterRuleName = "CollectAll"
                });
            }
        }

        #region - Terrain 2 Mesh -

        private void Terrain2Mesh(SceneInfos sceneInfos)
        {
            if (!settings.terrain2Mesh)
                return;

            GameObject root = GameObject.Find("Terrain 2 Mesh") ?? new GameObject("Terrain 2 Mesh");
            foreach (var terrain in FindObjectsOfType<Terrain>(true))
            {
                var data = terrain.terrainData;
                if (data == null)
                    continue;

                var parent = new GameObject($"{terrain.name}_Meshes");
                parent.transform.SetParent(root.transform);
                parent.transform.CopyFrom(terrain.transform);

                //获取预制体
                CopyPrefabsFromTerrain(data, parent.transform);

                //生成Mesh
                //data.Translate2Meshes($"{Path.GetDirectoryName(sceneInfos.path)}/Terrain2Meshes", parent.transform, settings.vertexCount, (int)settings.resolution, targetDepth, settings.exportHoles);

                parent.SetActive(terrain.gameObject.activeInHierarchy);
            }
        }

        private void CopyPrefabsFromTerrain(TerrainData data, Transform parent)
        {
            foreach (var treeInstance in data.treeInstances)
            {
                var treePrototypes = data.treePrototypes[treeInstance.prototypeIndex];
                var treePos = Vector3.Scale(treeInstance.position, data.size) + parent.position;
                var tree = PrefabUtility.InstantiatePrefab(treePrototypes.prefab, parent) as GameObject;
                tree.transform.position = treePos;
                tree.transform.rotation = Quaternion.Euler(0, Mathf.Rad2Deg * treeInstance.rotation, 0);
                tree.transform.localScale = new Vector3(treeInstance.widthScale, treeInstance.heightScale, treeInstance.widthScale);
                GameObjectUtility.SetStaticEditorFlags(tree, StaticEditorFlags.OccludeeStatic);
            }
        }

        #endregion
    }
}