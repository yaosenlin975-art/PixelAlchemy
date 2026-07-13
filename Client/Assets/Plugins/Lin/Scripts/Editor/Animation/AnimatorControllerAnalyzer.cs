/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：NewClass
└──────────────┘
*/
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Lin.Editor.Animation
{
    public class AnimatorControllerAnalyzer
    {
        [MenuItem("Lin/动画/分析动画控制器")]
        public static void AnalyzeAnimatorController()
        {
            // 获取选中的动画控制器
            var controller = Selection.activeObject as AnimatorController;
            if (controller == null)
            {
                Debug.LogError("请选择一个 AnimatorController");
                return;
            }

            // 遍历所有层级
            foreach (var layer in controller.layers)
            {
                Debug.Log($"层级: {layer.name}");

                // 获取状态机
                var stateMachine = layer.stateMachine;

                // 分析所有状态
                foreach (var state in stateMachine.states)
                {
                    var animatorState = state.state;

                    // 1. 获取状态类型
                    var motion = animatorState.motion;
                    string stateType = motion != null ? motion.GetType().Name : "Empty";

                    // 2. 获取状态标签
                    string stateTag = string.IsNullOrEmpty(animatorState.tag) ? "无标签" : animatorState.tag;

                    // 获取动画片段路径
                    string clipPath = "无动画片段";
                    if (motion is AnimationClip clip)
                    {
                        clipPath = AssetDatabase.GetAssetPath(clip);
                    }

                    Debug.Log($"状态: {animatorState.name}, 类型: {stateType}, 标签: {stateTag}, 动画路径: {clipPath}");

                    // 3. 分析混合树
                    if (motion is BlendTree blendTree)
                    {
                        AnalyzeBlendTree(blendTree);
                    }
                }
            }
        }

        private static void AnalyzeBlendTree(BlendTree blendTree, string indent = "  ")
        {
            Debug.Log($"{indent}混合树: {blendTree.name}");
            Debug.Log($"{indent}混合类型: {blendTree.blendType}");
            Debug.Log($"{indent}混合参数: {blendTree.blendParameter}");

            if (blendTree.blendType == BlendTreeType.Simple1D || blendTree.blendType == BlendTreeType.Direct)
            {
                var children = blendTree.children;
                foreach (var child in children)
                {
                    if (child.motion is BlendTree childTree)
                    {
                        AnalyzeBlendTree(childTree, indent + "  ");
                    }
                    else
                    {
                        string clipPath = child.motion is AnimationClip clip ? AssetDatabase.GetAssetPath(clip) : "无动画片段";
                        Debug.Log($"{indent}子动画: {child.motion?.name ?? "空"}, 阈值: {child.threshold}, 路径: {clipPath}");
                    }
                }
            }
            else if (blendTree.blendType == BlendTreeType.SimpleDirectional2D || blendTree.blendType == BlendTreeType.FreeformDirectional2D || blendTree.blendType == BlendTreeType.FreeformCartesian2D)
            {
                Debug.Log($"{indent}第二混合参数: {blendTree.blendParameterY}");

                var children = blendTree.children;
                foreach (var child in children)
                {
                    string clipPath = child.motion is AnimationClip clip ? AssetDatabase.GetAssetPath(clip) : "无动画片段";
                    Debug.Log($"{indent}子动画: {child.motion?.name ?? "空"}, 位置: ({child.position.x}, {child.position.y}), 路径: {clipPath}");
                }
            }
        }
    }
}
