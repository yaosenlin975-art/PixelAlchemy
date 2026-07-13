/*
┌────────────────────────────┐
│　Description：开关控制SceneView跟随GameView
│　Remark：
└────────────────────────────┘
*/
using Lin.Editor.Scene.Spliter;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lin.Editor.Scene
{
    public static class SceneUtilities
    {
        public static List<SceneInfos> GetAllSceneInfos()
        {
            List<SceneInfos> scenes = new List<SceneInfos>();
            string[] guids = AssetDatabase.FindAssets("t:Scene", new string[] { "Assets" });
            string currentScene = SceneManager.GetActiveScene().path;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.Equals(currentScene))
                    scenes.Insert(0, new SceneInfos(path, true));
                else
                    scenes.Add(new SceneInfos(path));
            }

            return scenes;
        }

        public static Bounds CalculateMapBounds()
        {
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

            Vector3 mapMinBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mapMaxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    // 更新边界值
                    mapMinBounds = Vector3.Min(mapMinBounds, renderer.bounds.min);
                    mapMaxBounds = Vector3.Max(mapMaxBounds, renderer.bounds.max);
                }
            }

            return new Bounds((mapMinBounds + mapMaxBounds) / 2f, mapMaxBounds - mapMinBounds);
        }
    }
}
