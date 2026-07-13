/*
┌────────────────────────────┐
│　Description: 事件调度 改
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: EventHelper
└──────────────┘
*/

using System.Collections.Generic;
using System;
using ZLinq;

namespace Lin.Runtime.Helper
{
    public static class EventHelper
    {
        public static void Register<T>(Action<T> handler) where T : struct => EventMap<T>.Register(handler);

        public static void Deregister<T>(Action<T> handler) where T : struct => EventMap<T>.Deregister(handler);

        public static void Dispatch<T>(this T args) where T : struct => EventMap<T>.Dispatch(args);

        private static class EventMap<T> where T : struct
        {
            private static HashSet<Action<T>> handlers = new HashSet<Action<T>>();
            // 派发期间收集到的待处理增删,执行前后统一应用,避免 foreach 迭代中修改 handlers
            private static HashSet<Action<T>> pendingAdds = new HashSet<Action<T>>();
            private static HashSet<Action<T>> pendingRemoves = new HashSet<Action<T>>();

            public static void Register(Action<T> handler)
            {
                // 后调用的 Register 覆盖先调用的 Deregister(同一 handler)
                pendingRemoves.Remove(handler);
                pendingAdds.Add(handler);
            }

            public static void Deregister(Action<T> handler)
            {
                // 后调用的 Deregister 覆盖先调用的 Register(同一 handler)
                pendingAdds.Remove(handler);
                pendingRemoves.Add(handler);
            }

            public static void Dispatch(T args)
            {
                // 执行前: 把上次派发累积的待处理增删先应用到 handlers
                ApplyPending();

                // 执行: 此时 handlers 在整个迭代过程中不会被修改
                foreach (var handler in handlers)
                    handler(args);

                // 执行后: 把本次派发期间产生的 Register/Deregister 也应用,避免跨周期累积
                ApplyPending();
            }

            private static void ApplyPending()
            {
                if (pendingRemoves.Count > 0)
                {
                    foreach (var h in pendingRemoves.AsValueEnumerable()) 
                        handlers.Remove(h);

                    pendingRemoves.Clear();
                }

                if (pendingAdds.Count > 0)
                {
                    foreach (var h in pendingAdds.AsValueEnumerable()) 
                        handlers.Add(h);
                
                    pendingAdds.Clear();
                }
            }
        }
    }
}