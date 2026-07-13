/*
┌────────────────────────────┐
│　Description：地图加载
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：MapLoader
└──────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Map
{
    abstract class AreaUpdaterBase
    {
        protected readonly IChunkNode root;

        public abstract Vector3 GetTargetPosition();

        public AreaUpdaterBase(IChunkNode root)
        {
            this.root = root;
        }

        public abstract void OnUpdate(float maxViewDistance);

        public abstract void OnDrawGizmos();
    }
}
