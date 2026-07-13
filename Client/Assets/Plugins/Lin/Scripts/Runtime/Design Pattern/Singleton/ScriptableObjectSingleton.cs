/*
┌────────────────────────────┐
│　Description: SO单例
└────────────────────────────┘
*/
using Lin.Runtime.Attribute;
using Lin.Runtime.Helper;
using System.Reflection;
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Lin.Runtime.DesignPattern.Singleton
{
    public abstract class ScriptableObjectSingleton<T> : ScriptableObject where T : ScriptableObjectSingleton<T>
    {
        private static T instance;

        public static T GetInstance()
        {
            if (instance != null)
                return instance;

            var att = typeof(T).GetCustomAttribute<AssetPathAttribute>();
            if (att == null)
                throw new NullReferenceException($"请为 {typeof(T).FullName} 添加 AssetPathAttribute");

#if UNITY_EDITOR
            try
            {
                instance = att.Load<T>();
            }
            catch (System.Exception)
            {
                instance = CreateInstance<T>();
                var assetPath = att.LoaderType != AssetPathAttribute.ELoaderType.Resources ? att.AssetPath : $"Assets/Resources/{att.AssetPath}.asset";
                IOHelper.InsureExist(System.IO.Path.GetDirectoryName(assetPath), false);
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                instance.Debug("Object不存在, 已自动创建");
            }
#else
            instance = att.Load<T>();
#endif
            return instance;
        }
    }
}
