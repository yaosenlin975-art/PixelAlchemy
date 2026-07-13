/*
┌────────────────────────────┐
│　Description：摄像机控制, 分布类
│　Remark：
└────────────────────────────┘
*/
using Lin.Runtime.Attribute;
using Lin.Runtime.DesignPattern.Singleton;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Lin.Runtime.Manager
{
    public partial class CameraController : MonoSingleton<CameraController>
    {
        [ReadOnly]
        [SerializeField]
        [GetInChild("Main Camera")]
        private Camera mainCamera;

        public Camera MainCamera => mainCamera;

        [ReadOnly]
        [SerializeField]
        [GetInChild("UI Camera")]
        private Camera uiCamera;

        public Camera UICamera => uiCamera; 

        [ReadOnly]
        [SerializeField]
        [GetInChild("Object Camera")]
        private Camera objectCamera;

        public Camera ObjectCamera => objectCamera;

        public void OnFullScreenPanelStateChanged(bool hasFullScreenPanels) => ObjectCamera.enabled = !hasFullScreenPanels;
    }
}