/*
┌────────────────────────────┐
│　Description：地图上的物品加载, 根据配置保证不重复加载
│　Remark：
└────────────────────────────┘
*/
using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using System;
using UnityEngine;

namespace Lin.Runtime.Map
{
    public class SceneObject : IDisposable, IComparable
    {
        public string prefabPath { get; }
        private Vector3 position;
        private Vector3 euler;
        private Vector3 scale;

        public GameObject gameObject { get; private set; }

        public Bounds bounds { get; private set; }
        public EState state { get; private set; }

        public SceneObject(string[] configs)
        {
            prefabPath = MapLoader.Instance.GetPrefabPath(int.Parse(configs[0]));
            position = GetVector3(configs, 1);
            euler = GetVector3(configs, 4);
            scale = GetVector3(configs, 7);
            bounds = new Bounds(GetVector3(configs, 10), GetVector3(configs, 13));
        }

        public SceneObject(GameObject target)
        {
            bounds = target.CalculateObjectBounds();
            state = EState.Loaded;
        }

        internal void Refresh()
        {
            if (state == EState.Unload)
            {
                MapLoader.Instance.Wait2Load(this);
                state = EState.Wait2Load;
            }
        }

        public async UniTask Load()
        {
            var mapLoader = MapLoader.Instance;

            switch (state)
            {
                case EState.Loading:
                case EState.Loaded:
                    return;

                case EState.ShouldUnload:
                    //TODO:卸载
                    break;

                default:
                    break;
            }

            try
            {
                if (ShouldCancelLoad())
                {
                    mapLoader.OnLoadFinish(this);
                    state = EState.Unload;
                    return;
                }

                state = EState.Loading;
                var handler = mapLoader.GetHandle<GameObject>(prefabPath, CalculatePriority());
                if (!handler.IsDone)
                    await handler;

                var asyncOperation = handler.InstantiateAsync(position, Quaternion.Euler(euler), mapLoader.transform);
                await asyncOperation;
                gameObject = asyncOperation.Result;
                gameObject.transform.localScale = scale;
                gameObject.transform.SetParent(mapLoader.transform, true);
                gameObject.SetActive(true);

                if (gameObject.transform.childCount > 0)
                    StaticBatchingUtility.Combine(gameObject);

                mapLoader.OnLoadFinish(this);
                state = EState.Loaded;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
                mapLoader.OnLoadFinish(this);
                state = EState.Loaded;
            }
        }

        private bool ShouldCancelLoad() => GetSqrMagnitudeWithTargetPoint() >= MapLoader.Instance.cancelLoadDistanceSqr;

        private float GetSqrMagnitudeWithTargetPoint()
        {
            var targetPos = MapLoader.Instance.TargetPosition;
            var cloestPoint = bounds.ClosestPoint(targetPos);
            return (cloestPoint - targetPos).sqrMagnitude;
        }

        private Vector3 GetVector3(string[] array, int startIndex)
        {
            Vector3 result = Vector3.zero;
            result.x = float.Parse(array[startIndex]);
            result.y = float.Parse(array[startIndex + 1]);
            result.z = float.Parse(array[startIndex + 2]);
            return result;
        }

        public void Dispose()
        {
            gameObject = null;
        }

        public enum EState
        {
            Unload,
            Wait2Load,
            Loading,
            Loaded,
            ShouldUnload
        }

        public uint CalculatePriority()
        {
            return (uint)Mathf.RoundToInt(bounds.size.x + bounds.size.z + bounds.size.y + bounds.size.x * bounds.size.y * bounds.size.z);
        }

        public int CompareTo(object obj)
        {
            if (obj is not SceneObject sceneObject)
                return -1;

            //越近  优先级越高
            return -(int)Mathf.Sign(GetSqrMagnitudeWithTargetPoint() - sceneObject.GetSqrMagnitudeWithTargetPoint());
        }
    }
}
