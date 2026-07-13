/*
┌────────────────────────────┐
│　Description：Chunk刷新方式
│　Remark：
└────────────────────────────┘
*/
using UnityEngine;
using System;

namespace Lin.Runtime.Map
{
    interface IChunkNode : IDisposable
    {
        /// <summary>
        /// 根据角色所在区域进行刷新
        /// </summary>
        /// <param name="playerBounds"></param>
        void OnUpdate(Bounds playerBounds);
        /// <summary>
        /// 根据摄像机视角进行刷新
        /// </summary>
        /// <param name="viewPlanes"></param>
        /// <param name="maxViewDistance"></param>
        /// <param name="cameraPos"></param>
        void OnUpdate(in Plane[] viewPlanes, float maxViewDistance, Vector3 cameraPos);
        void OnDrawGizmos();
    }
}
