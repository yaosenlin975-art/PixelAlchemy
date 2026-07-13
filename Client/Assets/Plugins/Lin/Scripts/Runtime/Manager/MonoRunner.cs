/*
┌────────────────────────────┐
│　Description: Mono Update, 为非Mono对象提供生命周期回调, 为多线程行为转移到主线程上
│　Remark: 
└────────────────────────────┘
*/
using Lin.Runtime.DesignPattern.Singleton;
using System.Collections.Generic;
using System;
using System.Collections.Concurrent;
using ZLinq;

namespace Lin.Runtime.Manager
{
    public class MonoRunner : MonoSingleton<MonoRunner>
    {
        private Dictionary<EUpdateType, HashSet<Action>> actions;
        private ConcurrentQueue<Action> mainThreadActions;
        [UnityEngine.SerializeField]
        private int maxMainThreadActionsPerTick = 256;

        public bool isPaused = false;

        protected override void Init()
        {
            base.Init();

            actions = new Dictionary<EUpdateType, HashSet<Action>>()
            {
                {EUpdateType.FixedUpdate, new HashSet<Action>() },
                {EUpdateType.LateUpdate, new HashSet<Action>() },
                {EUpdateType.Update, new HashSet<Action>() },
                {EUpdateType.GUI, new HashSet<Action>() }
            };
            mainThreadActions = new ConcurrentQueue<Action>();
        }

        private void Update() => Run(EUpdateType.Update);

        private void FixedUpdate() => Run(EUpdateType.FixedUpdate);

        private void LateUpdate() => Run(EUpdateType.LateUpdate);

        private void OnGUI() => Run(EUpdateType.GUI);

        private void Run(EUpdateType type)
        {
            if (isPaused)
                return;

            int processed = 0;
            while (processed < maxMainThreadActionsPerTick && mainThreadActions.TryDequeue(out var action))
            {
                action();
                processed++;
            }

            foreach (var action in actions[type].AsValueEnumerable())
                action();
        }

        public void Add2MainThread(Action action)
        {
            if (action is null)
                throw new ArgumentNullException();

            mainThreadActions.Enqueue(action);
        }

        public void AddListener(EUpdateType updateType, Action action) => actions[updateType].Add(action);

        public void RemoveListener(EUpdateType updateType, Action action) => actions[updateType].Remove(action);

        public enum EUpdateType
        {
            Update,
            FixedUpdate,
            LateUpdate,
            GUI
        }
    }
}
