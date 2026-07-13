/*
┌────────────────────────────┐
│　Description: 移动
│　Remark: 
└────────────────────────────┘
*/

using UnityEngine;

namespace Lin.Runtime.Interface
{
    public interface IMove
    {
        /// <summary> 移动速度。 </summary>
        float moveSpeed { get; }
        /// <summary> 按指定方向移动。 </summary>
        void Move(Vector2 dir);

        /// <summary> 跳跃力度。 </summary>
        float jumpForce { get; }
        /// <summary> 跳跃。 </summary>
        void Jump();
    }
}
