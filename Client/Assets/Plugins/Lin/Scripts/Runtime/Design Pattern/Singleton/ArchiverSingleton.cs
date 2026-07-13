/*
┌────────────────────────────┐
│　Description: 存档单例
└────────────────────────────┘
*/
using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using System;

namespace Lin.Runtime.DesignPattern.Singleton
{
    [Serializable]
    public abstract class ArchiverSingleton<T> where T : ArchiverSingleton<T>, new()
    {
        private static T instance;

        private static string key;
        private static string Key
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                    key = $"ArchiverSingleton_{typeof(T).Name}";

                return key; 
            }
        }

        public static T GetInstance()
        {
            if (instance is null)
            {
                instance = new();
                instance.Load();
            }

            return instance;
        }

        protected virtual void Load()
        {
            var temp = PrefsHelper.Get<T>(Key);
            if (temp != null)
                instance = temp;
        }

        public virtual void Save() => PrefsHelper.Set(Key, this as T);

        public virtual void Delete() => PrefsHelper.DeleteKey<T>(Key);
    }
}
