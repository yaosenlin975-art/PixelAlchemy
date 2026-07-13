/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using Lin.Runtime.DesignPattern.Singleton;
using Lin.Runtime.Helper;
using Lin.Runtime.Tool;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lin.Editor.Hierarchy.SceneObject
{
    public class SceneObjectDescriptionsMap : Singleton<SceneObjectDescriptionsMap>
    {
        private Dictionary<int, Description> descriptionMap;

        public SceneObjectDescriptionsMap()
        {
            descriptionMap = new Dictionary<int, Description>();

            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene arg0, UnityEngine.SceneManagement.Scene arg1) => descriptionMap.Clear();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="description"></param>
        public void SetDescription(int instanceId, string title, string description, Color titleColor)
        {
            var desCmp = GetOrAdd(instanceId);
            {
                desCmp.title = title;
                desCmp.description = description;
                desCmp.titleColor = titleColor;
                desCmp.EditorSave(false);
            }
        }

        public (string title, string description, Color color)GetDescription(int instanceId)
        {
            var desCmp = Get(instanceId);
            if (desCmp != null)
                return desCmp.Get();

            return (string.Empty, string.Empty, Color.white);
        }

        public void RemoveDescription(int instanceId)
        {
            var des = Get(instanceId);
            if (des != null)
                des.Destroy();
            descriptionMap.Remove(instanceId);
        }

        private Description GetOrAdd(int instanceId)
        {
            var result = Get(instanceId);
            if (result == null)
            {
                GameObject target = UnityEditor.EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                result = target.GetOrAddComponent<Description>();
                descriptionMap.AddOrUpdate(instanceId, result);
            }
            return result;
        }

        private Description Get(int instanceId)
        {
            if (!descriptionMap.TryGetValue(instanceId, out var result))
            {
                GameObject target = UnityEditor.EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (target == null)
                    return null;

                result = target.GetComponent<Description>();
                descriptionMap.Add(instanceId, result);
            }

            return result;
        }
    }
}
