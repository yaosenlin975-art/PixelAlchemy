/*
┌────────────────────────────┐
│　Description: 可入背包的物体
│　Remark: 
└────────────────────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Interface
{
    public interface IInventoryItem
    {
        /// <summary> 物品唯一标识。 </summary>
        public int id { get; }
        /// <summary> 物品名称。 </summary>
        public string name { get; }
        /// <summary> 物品描述。 </summary>
        public string description { get; }
    }
}