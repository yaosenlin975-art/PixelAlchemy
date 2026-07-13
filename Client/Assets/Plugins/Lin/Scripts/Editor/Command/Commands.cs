/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
*/
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using Lin.Runtime.Helper;
using Lin.Editor.Attribute;
using Lin.Editor.Scene;

namespace Lin.Editor.Command
{
    public static class Commands
    {
        [MenuItem("Lin/Commands/Jump To Main Scene #z")]
        [ToolbarButton(Toolbar.Element.EAlign.Middle, Toolbar.Element.EVisibleMode.Editor, "Assets/Plugins/Lin/Arts/Sprites/运行.png", "以Boot运行游戏\nShift+Z")]
        public static void JumpToMainScene()
        {
            if (!EditorApplication.isPlaying)
            {
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != EditorConst.MAIN_SCENE_PATH)
                {
                    PrefsHelper.Set(SceneSwitcher.LAST_SCENE_KEY, currentScene.path);
                    EditorSceneManager.OpenScene(EditorConst.MAIN_SCENE_PATH);
                }

                Debug.Log($"Jump to {EditorConst.MAIN_SCENE_PATH} and run.");
                EditorApplication.isPlaying = true;
            }
            else
                Debug.Log("Game already run.");
        }
    }
}
