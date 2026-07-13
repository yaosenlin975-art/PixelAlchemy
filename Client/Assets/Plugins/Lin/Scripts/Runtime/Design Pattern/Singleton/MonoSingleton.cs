/*
┌────────────────────────────┐
│　Description: Mono单例
└────────────────────────────┘
*/
using Cysharp.Text;
using Lin.Runtime.Attribute;
using System.Reflection;
using UnityEngine;

namespace Lin.Runtime.DesignPattern.Singleton
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T instance;

        public static T GetInstance()
        {
            if (instance != null)
                return instance;

            instance = FindAnyObjectByType<T>();
            if (instance == null)
            {
                var assetPath = typeof(T).GetCustomAttribute<AssetPathAttribute>();
                if (assetPath != null)
                    instance = assetPath.Load<GameObject>(true).GetComponent<T>();
            }

            if (instance == null)
                instance = new GameObject().AddComponent<T>();

            instance.name = ZString.Concat('[', typeof(T).Name, ']');
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        protected virtual void Init() { }

        private void Awake()
        {
            var ins = GetInstance();
            if (ins != this)
            {
                Destroy(gameObject);
                return;
            }

            Init();
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public void Destroy()
        {
            if (instance == this)
                Destroy(gameObject);
        }

        public static bool HasInstance() => instance;
    }
}
