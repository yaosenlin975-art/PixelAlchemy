/*
┌────────────────────────────┐
│　Description：根据视锥刷新
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：ViewUpdater
└──────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Map
{
    class ViewPlanesUpdater : AreaUpdaterBase
    {
        private readonly Camera camera;
        private Plane[] viewPlanes;

        private float radius;

        public ViewPlanesUpdater(IChunkNode root, Camera camera) : base(root)
        {
            this.camera = camera;
            viewPlanes = new Plane[6];
        }

        public override Vector3 GetTargetPosition() => camera.transform.position;

        public override void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(GetTargetPosition(), radius);
        }

        public override void OnUpdate(float maxViewDistance)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, viewPlanes);
            root.OnUpdate(viewPlanes, maxViewDistance * maxViewDistance, GetTargetPosition());
            radius = maxViewDistance;
        }
    }
}
