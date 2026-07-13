/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：AuthoringSceneSystemGroup
└──────────────┘
*/

using System.Linq;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZLinq;

namespace Lin.Runtime.Manager
{
    public abstract partial class AuthoringMultipleScenesSystemGroup : ComponentSystemGroup
    {
        private bool initialized;
        protected override void OnCreate()
        {
            base.OnCreate();
            initialized = false;
        }

        protected override void OnUpdate()
        {
            if (!initialized)
            {
                var currentScene = SceneManager.GetActiveScene();
                if (currentScene.isLoaded)
                {
                    var enable = false;
                    if (AuthoringSceneName.AsValueEnumerable().Contains(currentScene.name))
                        enable = true;
                    else
                        foreach (var scene in Object.FindObjectsByType<SubScene>(FindObjectsSortMode.None).AsValueEnumerable())
                        {
                            if (AuthoringSceneName.AsValueEnumerable().Contains(scene.name))
                            {
                                enable = true;
                                break;
                            }
                        }

                    Enabled = enable;
                    initialized = true;
                }
            }

            base.OnUpdate();
        }

        protected abstract string[] AuthoringSceneName { get; }
    }
}