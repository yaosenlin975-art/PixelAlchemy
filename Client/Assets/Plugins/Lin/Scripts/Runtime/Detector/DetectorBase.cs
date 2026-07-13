/*
┌──────────────┐                                   
│　类名: Detector
│　功能说明: 检测器基类
└──────────────┘
*/
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lin.Runtime.Detector
{
#if UNITY_2D
    public delegate void DetectAction(IEnumerable<KeyValuePair<Collider2D, Vector2>> detectList);
#else
    public delegate void DetectAction(IEnumerable<KeyValuePair<Collider, Vector2>> detectList);
#endif

    public abstract class DetectorBase : MonoBehaviour
    {
        #region - Fields -

        [Tooltip("无视自身(包括子父体)")]
        public bool ignoreSelf;

        [Tooltip("可检测的层级")]
        public LayerMask touchableLayer;

        [Tooltip("击中物体的最大数量")]
        public int maxObjectCount = 20;

        [Tooltip("单个物体单次检测的最大被检查次数")]
        public int maxHitCount = 1;

        public float detectInterval = 0.2f;

        [Tooltip("用于无GC检测")]
#if UNITY_2D
        protected RaycastHit2D[] noAlloc;
        
        [Tooltip("单次检测到的对象, 检测到的点")]
        protected Dictionary<Collider2D, Vector2> hits;

        [Tooltip("每个物体的本次检测的检测间隔、击中次数")]
        private Dictionary<Collider2D, HitRecord> objectHitCount;
#else
        protected RaycastHit[] noAlloc;

        [Tooltip("单次检测到的对象")]
        protected Dictionary<Collider, Vector2> hits;

        [Tooltip("每个物体的本次检测的检测间隔、击中次数")]
        Dictionary<Collider, HitRecord> objectHitCount;
#endif

        /// <summary> 当击中物体时 </summary>
        protected HashSet<DetectAction> onHitted; 

        #endregion

        #region - Debug -

#if EDITOR_DEBUG
        [SerializeField, Tooltip("DrawGizmos持续时间")] protected float dgDuration;
#endif

        #endregion

        #region - Life Cycle -

        protected virtual void Awake()
        {
            onHitted = new HashSet<DetectAction>();

#if UNITY_2D
            hits = new Dictionary<Collider2D, Vector2>(maxObjectCount);
            noAlloc = new RaycastHit2D[maxObjectCount];
            objectHitCount = new Dictionary<Collider2D, HitRecord>(maxObjectCount);
#else
            hits = new Dictionary<Collider, Vector2>(maxObjectCount);
            noAlloc = new RaycastHit[maxObjectCount];
            objectHitCount = new Dictionary<Collider, HitRecord>(maxObjectCount);
#endif
        }

        private void OnDisable()
        {
            hits.Clear();
            objectHitCount.Clear();
            Array.Clear(noAlloc, 0, noAlloc.Length);
        }

        protected virtual void FixedUpdate()
        {
            int count = Detect();
            if (count > 0)
            {
                foreach (var action in onHitted)
                    action(hits);

                hits.Clear();
            }
        }

#if EDITOR_DEBUG
        protected abstract void OnDrawGizmos();
#endif

#endregion

        #region - Methods -

        /// <summary> 检测 </summary>
        /// <returns>击中物体的数量</returns>
        protected abstract int Detect();

        protected void PushToList(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var info = noAlloc[i];

                var collider = info.collider;
                if (!collider)
                    continue;

                if (ignoreSelf && IsPart(collider.transform))
                    continue;

                if (objectHitCount.TryGetValue(collider, out HitRecord objectInfo))
                {
                    if (objectInfo.hitCount >= maxHitCount)    //本次攻击已完成对该物体的所有击中
                        continue;

                    if (Time.time - objectInfo.hitTime < detectInterval)  //距离上次击中时间过短
                        continue;

                    objectInfo.hitTime = Time.time;
                    objectInfo.hitCount += 1;
                    objectHitCount[collider] = objectInfo;
                }
                else
                    objectHitCount.Add(collider, new HitRecord() { hitCount = 1, hitTime = 0 });

                hits.Add(collider, info.point);
            }
        }

        public void AddOnHittedListener(DetectAction listener) => onHitted.Add(listener);

        public void RemoveOnHittedListener(DetectAction listener) => onHitted.Remove(listener);

        private bool IsPart(Transform target)
        {
            if (target == transform)
                return true;

            if (target.parent is null)
                return false;

            return IsPart(target.parent);
        }

        #endregion
    }

    [Serializable]
    struct HitRecord
    {
        public int hitCount;
        public float hitTime;
    }
}
