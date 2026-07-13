/*
┌────────────────────────────┐
│　Description: 菜单拓展
│　Author: 花球i
└────────────────────────────┘
*/
using Cysharp.Text;
using Lin.Runtime.Helper;
using System;
using UnityEditor;
using UnityEngine;
using static Lin.Runtime.Helper.ModelHelper;

namespace Lin.Editor.Helper
{
    public static class ModelHelper
    {
        [MenuItem("Lin/人物模型/强制 T-Pose")]
        private static void EnforceTPose() => Selection.activeGameObject.GetComponent<Animator>().EnforcePose();

        [MenuItem("Lin/人物模型/强制 A-Pose")]
        private static void EnforceAPose() => Selection.activeGameObject.GetComponent<Animator>().EnforcePose(PoseType.APose);

        [MenuItem("Lin/人物模型/强制 I-Pose")]
        private static void EnforceIPose() => Selection.activeGameObject.GetComponent<Animator>().EnforcePose(PoseType.IPose);

        [MenuItem("Lin/人物模型/强制 T-Pose", validate = true),
            MenuItem("Lin/人物模型/强制 A-Pose", validate = true),
            MenuItem("Lin/人物模型/强制 I-Pose", validate = true),
            MenuItem("Lin/人物模型/当前姿势值", validate = true)]
        private static bool EnforceTPoseValidation()
        {
            var root = Selection.activeObject as GameObject;

            if (!root)
                return false;

            var animator = root.GetComponent<Animator>();

            return animator && animator.avatar && animator.avatar.isValid && animator.avatar.isHuman;
        }

        [MenuItem("Lin/人物模型/当前姿势值")]
        private static void ShowMuscle()
        {
            Animator animator = Selection.activeGameObject.GetComponent<Animator>();

            //Pose
            HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform);
            HumanPose humanPose = new HumanPose();
            handler.GetHumanPose(ref humanPose);
            float[] muscle = humanPose.muscles;

            string result = ZString.Format("{0}当前Pose值为: \n", animator.name);

            for (int i = 0; i < muscle.Length; i++)
                result = ZString.Format("{0}\t{1}", muscle[i], result);

            Debug.Log(ZString.Format("{0}\n{1}", result, DateTime.Now));
        }
    }
}