/*
┌────────────────────────────┐
│　Description: 信息存储辅助
│　Remark: 会将所有信息都序列化成字符串
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: PrefsHelper
└──────────────┘
*/
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using System.Text;
using UnityEngine.Scripting;

namespace Lin.Runtime.Helper
{
    [Preserve]
    public static class PrefsHelper
    {
        private static Dictionary<Type, object> archivesMap = new Dictionary<Type, object>();
         
        private static PrefsArchive<T> GetArchive<T>()
        {
            var type = typeof(T);
            PrefsArchive<T> archive;

            if (!archivesMap.ContainsKey(type))
            {
                archive = PrefsArchive<T>.Load();
                archivesMap.Add(type, archive);
            }
            else
                archive = archivesMap[type] as PrefsArchive<T>;

            return archive; 
        }

        public static void DeleteKey<T>(string key)
        {
            var archive = GetArchive<T>();
            archive.RemoveAndSave(key);
        }

        public static T Get<T>(string key)
        {
            CheckType<T>();
            var archive = GetArchive<T>();
            return archive.Get(key);
        }

        public static T Get<T>(string key, T defaultValue)
        {
            CheckType<T>();
            var archive = GetArchive<T>();
            return archive.Get(key, defaultValue);
        }

        public static T Get<T>(string key, Func<T> createFunc)
        {
            CheckType<T>();
            var archive = GetArchive<T>();
            return archive.Get(key, createFunc);
        }

        public static void Set<T>(string key, T value)
        {
            CheckType<T>();
            var archive = GetArchive<T>();
            archive.Set(key, value);
        }

        /// <summary>
        /// 获取所有已被访问过的存档类型（仅暴露已加载的 Type 列表，不触发任何 I/O）
        /// </summary>
        public static IEnumerable<Type> GetAllArchiveTypes()
        {
            return archivesMap.Keys;
        }

        /// <summary>
        /// 获取指定类型下的所有 key
        /// </summary>
        public static IEnumerable<string> GetAllKeys<T>()
        {
            CheckType<T>();
            return GetArchive<T>().Keys;
        }

        /// <summary>
        /// 判断指定 key 是否存在
        /// </summary>
        public static bool ContainsKey<T>(string key)
        {
            CheckType<T>();
            return GetArchive<T>().ContainsKey(key);
        }

        /// <summary>
        /// 清除指定类型下的所有 key
        /// </summary>
        public static void Clear<T>()
        {
            CheckType<T>();
            var archive = GetArchive<T>();
            foreach (var key in archive.Keys.ToArray())
                archive.RemoveAndSave(key);
        }

        private static void CheckType<T>()
        {
            var type = typeof(T);
            // 如果是值类型但不是struct（即内置值类型如int、enum等），直接通过
            if (type.IsValueType && !type.IsLayoutSequential)
                return;
            
            // 其他类型需要检查是否可序列化
            if (!type.Attributes.HasFlag(System.Reflection.TypeAttributes.Serializable))
                throw new Exception($"{type.FullName} has not 'Serializable'.");
        }

        [Serializable]
        class PrefsArchive<T> : Dictionary<string, T>
        {
            private string filePath;
            private object locker;
            private const byte OFFSET = 7;

            public static PrefsArchive<T> Load()
            {
                string filePath = GetPath();
                PrefsArchive<T> result;
#if UNITY_WEBGL
                string key = typeof(T).FullName;
                if (PlayerPrefs.HasKey(key))
                {
                    string json = PlayerPrefs.GetString(key);
                    result = JsonConvert.DeserializeObject<PrefsArchive<T>>(json);
                }
                else
                    result = new PrefsArchive<T>();
#else
                if (File.Exists(filePath))
                {
                    var bytes = File.ReadAllBytes(filePath);
                    Translate(bytes, filePath);
                    string json = Encoding.Default.GetString(bytes);
                    result = JsonConvert.DeserializeObject<PrefsArchive<T>>(json);
                }
                else
                    result = new PrefsArchive<T>();
#endif
                result.filePath = filePath;
                result.locker = new object();
                 
                return result;
            }

#if !UNITY_WEBGL
            //简易加密，解密
            private static void Translate(byte[] bytes, string path)
            {
                Type type = typeof(T);
                byte flags = (byte)(type.FullName.Length % byte.MaxValue);
                byte offset = (byte)((flags + path.Length) % byte.MaxValue);
                offset = offset == 0 ? OFFSET : offset;
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] ^= offset;
            }
#endif
            private static void Save(PrefsArchive<T> archive)
            { 
#if UNITY_WEBGL
                string json = JsonConvert.SerializeObject(archive);
                PlayerPrefs.SetString(typeof(T).FullName, json);
#else
                string json = JsonConvert.SerializeObject(archive);
                string dir = Path.GetDirectoryName(archive.filePath);
                IOHelper.InsureExist(dir, false);
                var bytes = Encoding.Default.GetBytes(json); 
                Translate(bytes, archive.filePath);
                File.WriteAllBytes(archive.filePath, bytes);
#endif
            }

            private static string GetPath()
            {
                var fileName = typeof(T).Name.GetStableHashCode64().ToString("x");
#if UNITY_EDITOR
                var dir = "EditorPrefs";
                IOHelper.InsureExist(dir, false);
                return Path.Combine(dir, fileName);
#else
                StringBuilder stringBuilder = new StringBuilder(Application.persistentDataPath);
                stringBuilder.Append("/Temps/");
                stringBuilder.Append(fileName);
                return stringBuilder.ToString();
#endif
            }

            public void Set(string key, T value)
            {
                lock (locker)
                {
                    if (!ContainsKey(key))
                        Add(key, default);

                    this[key] = value;
                    Save(this);
                }
            }

            // 基类 Dictionary.Remove 只改内存不落盘, 删除后下次域重载会被 Load 回来
            // (历史上导致 Generator Step7 清完 Info, 下一次编译又重新跑整个生成流程)
            public void RemoveAndSave(string key)
            {
                lock (locker)
                {
                    if (Remove(key))
                        Save(this);
                }
            }

            public T Get(string key, T defaultValue = default)
            {
                lock (locker)
                {
                    if (TryGetValue(key, out T result))
                        return result;

                    return defaultValue;
                }
            }

            public T Get(string key, Func<T> createFunc)
            {
                lock (locker)
                {
                    if (TryGetValue(key, out T result))
                        return result;

                    return createFunc();
                }
            }
        }
    }
}