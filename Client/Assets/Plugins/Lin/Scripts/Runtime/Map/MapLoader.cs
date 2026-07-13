/*
┌────────────────────────────┐
│　Description：被分拆的地图的加载器
│　Remark：
└────────────────────────────┘
*/
using Cysharp.Threading.Tasks;
using Lin.Runtime.Resource;
using Sirenix.OdinInspector;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;

namespace Lin.Runtime.Map
{
    public class MapLoader : MonoBehaviour
    {
        public EState state { get; private set; } = EState.UNSTART;

        [ReadOnly]
        [LabelText("分拆方式")]
        public ESlpiterType slpiterType;

        [DisableInPlayMode]
        [LabelText("加载方式")]
        public ELoadType loadType;

        private IChunkNode root;

        private AreaUpdaterBase areaUpdater;

        public Vector3 TargetPosition => areaUpdater.GetTargetPosition();

        /// <summary> 预制体地址记录文件 </summary>
        public const string PREFAB_PATHS_FILE_NAME = "Paths.txt";

        public const string UNSTATIC_MODLES_CONFIG_NAME = "Unstatic.txt";

        /// <summary> 记录该地图有那些块 </summary>
        public const string CHUNKS_INDEX_FILE_NAME = "ChunkList.txt";

        /// <summary> 资源所在位置 </summary>
        public string sourceDirectory;

        /// <summary> 当前地图包围盒的大小 </summary>
        public Bounds mapBounds;

        /// <summary> 可视距离 </summary>
        private float viewDistance;
        public float ViewDistanceSqr
        {
            get => viewDistance * viewDistance;
            set
            {
                viewDistance = value;
                cancelLoadDistanceSqr = Mathf.Pow(viewDistance + 30, 2);
                shouldUnloadDistanceSqr = Mathf.Pow(3 * viewDistance, 2);
                Debug.Log($"{nameof(viewDistance)}: {viewDistance}\t{nameof(cancelLoadDistanceSqr)}: {Mathf.Sqrt(cancelLoadDistanceSqr)}\t{nameof(shouldUnloadDistanceSqr)}: {Mathf.Sqrt(shouldUnloadDistanceSqr)}");
            }
        }

        public float cancelLoadDistanceSqr { get; private set; }
        public float shouldUnloadDistanceSqr { get; private set; }

        private string[] prefabPaths;

        private static MapLoader instance;
        public static MapLoader Instance => instance;

        //避免重复加载相交于多chunk的物体
        private ConcurrentDictionary<string, AssetHandle> handlers;
        private Dictionary<string, SceneObject> loadedObjects;
        private ConcurrentQueue<SceneObject> toLoads;
        private List<SceneObject> loadings;
        public int loadingsCount => toLoads.Count + loadings.Count;
        public int maxLoadingCount = 7;
        public bool asyncLoad = true;
        private CancellationTokenSource onDestory;
        public CancellationToken OnDestoryToken => onDestory.Token;

        public const int TARGET_MEMORY_SIZE = 16 * 1024;

        private void Awake()
        {
            instance = this;
            loadedObjects = new Dictionary<string, SceneObject>();
            toLoads = new ConcurrentQueue<SceneObject>();
            loadings = new List<SceneObject>();
            handlers = new ConcurrentDictionary<string, AssetHandle>();

            var prefabTextAsset = ResLoader.LoadTextAsset(GetFilePath(PREFAB_PATHS_FILE_NAME));
            prefabPaths = prefabTextAsset.text.Split("\r\n");

            root = slpiterType == ESlpiterType.QUADTREE ? new QuadtreeNode(mapBounds) : new OctreeNode(mapBounds);

            onDestory = new CancellationTokenSource();

            float currentMemorySize = SystemInfo.systemMemorySize;
            float percent = currentMemorySize / TARGET_MEMORY_SIZE;
            ViewDistanceSqr = Mathf.Min(30 + 170 * percent, 170 * 2);   //30视野为固定值，170为浮动值

            maxLoadingCount = Mathf.RoundToInt(Mathf.Min(5 + 25 * percent, 25 * 3));    //5为固定值，25为浮动值
            Debug.Log($"{nameof(maxLoadingCount)}: {maxLoadingCount}");
        }

        private void Start()
        {
            areaUpdater = 
                loadType == ELoadType.VIEW ? 
                new ViewPlanesUpdater(root, Camera.main) : 
                new BoundsUpdater(root, Camera.main.transform);
        }

        private void Update()
        {
            areaUpdater.OnUpdate(viewDistance);

            while (toLoads.Count > 0 && loadings.Count < maxLoadingCount)
            {
                toLoads.TryDequeue(out var target);
                lock (loadings)
                    loadings.Add(target);

                target.Load().GetAwaiter();
                state = EState.LOADING;
            }
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                root.OnDrawGizmos();
                areaUpdater.OnDrawGizmos();
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(mapBounds.center, mapBounds.size);
            }
        }

        private void OnDestroy()
        {
            instance = null;

            foreach (var so in loadedObjects.Values)
                so.Dispose();

            loadedObjects.Clear();
            loadedObjects = null;

            root.Dispose();
            root = null;

            onDestory.Cancel();
            onDestory.Dispose();
            onDestory = null;

            foreach (var pair in handlers)
                pair.Value.Dispose();
            handlers.Clear();
            handlers = null;
        }

        public AssetHandle GetHandle<T>(string path, uint priority) where T: Object
        {
            return handlers.GetOrAdd(path, p =>
            {
                var package = ResLoader.CheckLocationValid(path);
                if (package is null)
                {
                    Debug.LogError($"无法找到 {path}");
                    return null;
                }
                var handler = package.GetAsyncHandle<T>(p, priority);
                return handler;
            });
        }

        public string GetFilePath(string fileName) => $"{sourceDirectory}/{fileName}";

        public string GetPrefabPath(int index)
        {
            string path = prefabPaths[index].Replace("..", sourceDirectory);
            Debug.Log($"{index} {path}");
            return path;
        }

        public SceneObject GetSceneObject(string key)
        {
            lock (loadedObjects)
            {
                if (!loadedObjects.ContainsKey(key))
                {
                    string[] configs = key.Split(',');
                    loadedObjects[key] = new SceneObject(configs);
                }
            }

            return loadedObjects[key];
        }

        public void Wait2Load(SceneObject target) => toLoads.Enqueue(target);

        public void OnLoadFinish(SceneObject target)
        {
            lock (loadings)
            {
                loadings.Remove(target);
                Debug.Log($"加载剩余：{loadingsCount}");
            }
        }

        public async UniTask WaitUntilAllLoaded()
        {
            await UniTask.WaitUntil(() => state != EState.UNSTART, PlayerLoopTiming.FixedUpdate);
            await UniTask.WaitUntil(() => loadingsCount == 0, PlayerLoopTiming.FixedUpdate);
        }

        public enum EState
        {
            UNSTART,
            LOADING,
            WAIT_TO_LOAD,
        }

        public enum ESlpiterType
        {
            [LabelText("四叉")]
            QUADTREE,

            [LabelText("八叉")]
            OCTREE
        }

        public enum ELoadType
        {
            [LabelText("视锥")]
            VIEW,

            [LabelText("摄像机位置")]
            POSITION,
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(MapLoader))]
    class MapLoaderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.Space(10);
            GUILayout.Label($"视野范围：{(target as MapLoader).ViewDistanceSqr}");
        }
    }
#endif
}
