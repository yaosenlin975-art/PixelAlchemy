/*
┌────────────────────────────┐
│　Description：地图加载
│　Remark：
└────────────────────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Map
{
    class BoundsUpdater : AreaUpdaterBase
    {
        private readonly Transform target;

        private Bounds bounds;

        public BoundsUpdater(IChunkNode root, Transform target) : base(root) 
        {
            this.target = target;
        }

        public override Vector3 GetTargetPosition() => target.position;

        public override void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        public override void OnUpdate(float maxViewDistance)
        {
            bounds = new Bounds();
            bounds.size = maxViewDistance * Vector3.one;
            bounds.center = GetTargetPosition();
            root.OnUpdate(bounds);
        }
    }
}
