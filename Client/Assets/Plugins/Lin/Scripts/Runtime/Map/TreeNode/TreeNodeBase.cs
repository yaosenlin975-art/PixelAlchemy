/*
┌────────────────────────────┐
│　Description：检测是否加载
│　Remark：
└────────────────────────────┘
*/
using System.Collections.Generic;
using UnityEngine;
using Lin.Runtime.Resource;
using UnityEngine.Pool;

namespace Lin.Runtime.Map
{
    abstract class TreeNodeBase<T> : DesignPattern.TreeNode.TreeNodeBase<T>, IChunkNode where T : TreeNodeBase<T>
    {
        private List<SceneObject> inChunk;
        protected bool viewable;
        protected Vector3 targetPos { get; private set; }

        public TreeNodeBase(Bounds bounds) : base(bounds, 0)
        {
            var mapLoader = MapLoader.Instance;
            var chunksIndexTextAsset = ResLoader.LoadTextAsset(mapLoader.GetFilePath(MapLoader.CHUNKS_INDEX_FILE_NAME));
            string[] chunksIndex = chunksIndexTextAsset.text.Split("\r\n");
            foreach (var index in chunksIndex)
            {
                string[] details = index.Split('-');
                //获取到配置文件中最底部的节点
                var node = this;
                for (int i = 1; i < details.Length; i++)
                    node = node[int.Parse(details[i])];

                node.Load(index);
            }
        }

        protected TreeNodeBase(Bounds bounds, int depth) : base(bounds, depth) { }

        private new T this[int index]
        {
            get
            {
                if (children is null)
                    children = CreateChildren();

                if (children[index] is null)
                    children[index] = CreateChild(index);

                return children[index];
            }
            set
            {
                if (children is null)
                    children = CreateChildren();

                children[index] = value;
            }
        }

        protected abstract T[] CreateChildren();

        public void OnUpdate(in Plane[] viewPlanes, float maxViewDistance, Vector3 cameraPos)
        {
            //先检测视锥
            bool viewable = GeometryUtility.TestPlanesAABB(viewPlanes, bounds);

            targetPos = cameraPos;

            //再检测距离
            if (viewable)
            {
                var closestPoint = bounds.ClosestPoint(cameraPos);
                var dir = cameraPos - closestPoint;
                viewable = dir.sqrMagnitude <= maxViewDistance;
            }

            if (viewable)
            {
                if (children is not null)
                    for (int i = 0; i < children.Length; i++)
                        children[i]?.OnUpdate(viewPlanes, maxViewDistance, cameraPos);
                else
                    Show();
            }

            this.viewable = viewable;
        }

        public void OnUpdate(Bounds playerBounds)
        {
            bool viewable = bounds.Intersects(playerBounds);
            targetPos = playerBounds.center;

            if (viewable)
            {
                if (children is not null)
                    for (int i = 0; i < children.Length; i++)
                        children[i]?.OnUpdate(playerBounds);
                else
                    Show();
            }

            this.viewable = viewable;
        }

        public void OnDrawGizmos()
        {
            if (children is not null)
                foreach (var child in children)
                    child?.OnDrawGizmos();
            else
            {
                Gizmos.color = viewable ? Color.green : Color.red;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        private void Show()
        {
            if (inChunk is not null)
            {
                foreach (var item in inChunk)
                    item.Refresh();
            }
        }

        private void Load(string index)
        {
            var mapLoader = MapLoader.Instance;
            var configs = ResLoader.LoadTextAsset(MapLoader.Instance.GetFilePath($"{index}.txt")).text.Split("\r\n");

            inChunk = ListPool<SceneObject>.Get();
            if (inChunk.Capacity < configs.Length)
                inChunk.Capacity = configs.Length + 1;

            for (int i = 0; i < configs.Length; i++)
            {
                var prefabConfig = configs[i];
                SceneObject so = mapLoader.GetSceneObject(prefabConfig);
                inChunk.Add(so);
            }
        }

        protected override void Disposing()
        {
            if (inChunk is not null)
                ListPool<SceneObject>.Release(inChunk);
        }
    }
}
