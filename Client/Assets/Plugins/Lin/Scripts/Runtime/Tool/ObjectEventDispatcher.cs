/*
┌────────────────────────────┐
│　Description: 游戏物体的事件调度
│　Remark: 具体事件支持  需要在Hotfix里定义（EObjectEventID,  Class Expand）
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: ObjectEventDispatcher
└──────────────┘
*/
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lin.Runtime.Tool
{
    [DisallowMultipleComponent]
    public class ObjectEventDispatcher : MonoBehaviour
    {
        private Dictionary<int, Delegate> delegatesMap = new Dictionary<int, Delegate>();

        private void OnDestroy()
        {
            delegatesMap.Clear();
            delegatesMap = null;
        }

        public void Register(int eventID, Action callback)=> RegisterInternal(eventID, callback);

        public void Register<T>(int eventID, T callback) where T : Delegate => RegisterInternal(eventID, callback);

        private void RegisterInternal(int eventID, Delegate callback)
        {
            if (delegatesMap.TryGetValue(eventID, out var callbacks))
            {
                callbacks = Delegate.Combine(callbacks, callback);
                delegatesMap[eventID] = callbacks;
                return;
            }

            delegatesMap.Add(eventID, callback);
        }

        public void Deregister(int eventID, Action callback) => DeregisterInternal(eventID, callback);

        public void Deregister<T>(int eventID, T callback) where T : Delegate => DeregisterInternal(eventID, callback);

        private void DeregisterInternal(int eventID, Delegate callback)
        {
            if (!delegatesMap.TryGetValue(eventID, out var callbacks))
                return;

            callbacks = Delegate.Remove(callbacks, callback);
            delegatesMap[eventID] = callbacks;
        }

        public Action Get(int eventID) => Get<Action>(eventID);

        public T Get<T>(int eventID) where T : Delegate
        {
            delegatesMap.TryGetValue(eventID, out var callbacks);
            return callbacks as T;
        }
    }
}
