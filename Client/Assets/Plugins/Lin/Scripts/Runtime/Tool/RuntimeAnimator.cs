/*
┌────────────────────────────┐
│　Description: 累积器
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: Accumulator
└──────────────┘
*/

using Cysharp.Threading.Tasks;
using Lin.Runtime.Resource;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Lin.Runtime.Tool
{
    [RequireComponent(typeof(Animator)), DisallowMultipleComponent]
    public class RuntimeAnimator : MonoBehaviour
    {
        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();

            RuntimeAnimatorController rac = animator.runtimeAnimatorController;
            AnimatorOverrideController aoc = new AnimatorOverrideController(rac);
            animator.runtimeAnimatorController = aoc;
            Resources.UnloadUnusedAssets();
        }

        public async UniTaskVoid ReplaceClip(string stateName, string clipPath)
        {
            var aoc = animator.runtimeAnimatorController as AnimatorOverrideController;
            aoc[stateName] = await ResLoader.LoadAssetAsync<AnimationClip>(clipPath);
            animator.CrossFade(stateName, 0.1f);
            //animator.Play(stateName);
        }

        public void Play(string name) => ReplaceClip("Attack1", name).Forget();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(RuntimeAnimator))]
    class RuntimeAnimatorEditor : Editor
    {
        private List<string> clips;

        private void OnEnable()
        {
            clips = Directory.GetFiles(Application.dataPath, "*.fbx", SearchOption.AllDirectories).Where(c =>
            {
                string path = "Assets" + c.Replace(Application.dataPath, string.Empty).Replace("\\", "/");
                ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
                return modelImporter.clipAnimations.Length > 0;
            }).Select(c => "Assets" + c.Replace(Application.dataPath, string.Empty).Replace("\\", "/")).ToList();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            foreach (var clip in clips)
            {
                if (GUILayout.Button(Path.GetFileNameWithoutExtension(clip)))
                {
                    (target as RuntimeAnimator).Play(clip);
                }
            }
        }
    }

#endif
}