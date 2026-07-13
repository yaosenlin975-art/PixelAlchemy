/*
┌────────────────────────────┐
│　Description：开关控制SceneView跟随GameView
│　Remark：
└────────────────────────────┘
*/
using Lin.Editor.Attribute;
using Lin.Runtime.Helper;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Scene
{
    [InitializeOnLoad]
    static class FollowGameCamera
    {
        private const string FOLLOW_GAME_VIEW_KEY = nameof(FOLLOW_GAME_VIEW_KEY);
        private static bool followGameView;

        static FollowGameCamera()
        {
            followGameView = PrefsHelper.Get(FOLLOW_GAME_VIEW_KEY, false);
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying || !followGameView)
                return;

            Camera camera = Camera.current;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.cameraSettings.nearClip = camera.nearClipPlane;
                sceneView.cameraSettings.fieldOfView = camera.fieldOfView;
                sceneView.pivot = camera.transform.position +
                    camera.transform.forward * sceneView.cameraDistance;
                sceneView.rotation = camera.transform.rotation;
            }
        }

        [ToolbarToggle(Toolbar.Element.EAlign.Middle, Toolbar.Element.EVisibleMode.Runtime, "Camera Icon", "SceneView 跟随 GameView", nameof(FollowGameView))]
        private static void FollowGameView(bool isOn)
        {
            PrefsHelper.Set(FOLLOW_GAME_VIEW_KEY, isOn);
            followGameView = isOn;
        }
    }
}
