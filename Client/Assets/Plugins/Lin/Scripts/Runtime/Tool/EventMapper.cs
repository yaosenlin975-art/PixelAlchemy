/*
┌────────────────────────────┐
│　Description: GameObject事件器
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: EventMapper
└──────────────┘
*/
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Lin.Runtime.Tool
{
    public class EventMapper : MonoBehaviour
    {
        private Dictionary<Type, HashSet<Delegate>> eventMap;

        private void Awake()
        {
            eventMap = new Dictionary<Type, HashSet<Delegate>>();
        }

        private void OnDestroy()
        {
            foreach (var map in eventMap.Values)
            {
                map.Clear();
                HashSetPool<Delegate>.Release(map);
            }
            eventMap.Clear();
        }

        public void Register<T>(Action<T> action) where T : struct
        {
            var type = typeof(T);
            if (!eventMap.TryGetValue(type, out var map))
            {
                map = HashSetPool<Delegate>.Get();
                eventMap.Add(type, map);
            }

            map.Add(action);
        }

        public void Deregister<T>(Action<T> action) where T : struct
        {
            var type = typeof(T);
            if (!eventMap.TryGetValue(type, out var map))
                return;

            map.Remove(action);
        }

        public void Dispatch<T>(T args) where T : struct
        {
            var type = args.GetType();
            if (!eventMap.TryGetValue(type, out var map))
                return;

            foreach (var action in map)
                (action as Action<T>)(args);
        }
    }
}
