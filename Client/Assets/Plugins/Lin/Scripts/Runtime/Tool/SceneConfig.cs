/*
┌────────────────────────────┐
│　Description: 场景自身特殊设置
│　Remark: 
└────────────────────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Tool
{
    public abstract class SceneConfig : MonoBehaviour
    {
        private void Awake() => OnEnter();

        private void Reset()
        {
            name = "SceneConfig";
        }

        private void OnDestroy()
        {
            OnExit();
        }

        protected abstract void OnEnter();

        protected abstract void OnExit();
    }
}
