/*
┌────────────────────────────┐
│　Description: UI组件辅助类
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: UIHelper
└──────────────┘
*/
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Lin.Runtime.Helper
{
    public static class EventTriggerExtension
    {
        public static void AddEvent(this EventTrigger self, EventTriggerType eventID, UnityAction<BaseEventData> callBack)
        {
            var entry = self.GetEventEntry(eventID);
            if (entry is null)
            {
                entry = new EventTrigger.Entry() { eventID = eventID, callback = new EventTrigger.TriggerEvent() };
                self.triggers.Add(entry);
            }
            entry.callback.AddListener(callBack);
        }

        public static bool RemoveEvent(this EventTrigger self, EventTriggerType eventID, UnityAction<BaseEventData> callBack)
        {
            var toRemove = self.GetEventEntry(eventID);
            if (toRemove is null)
                return false;

            toRemove.callback.RemoveListener(callBack);
            return true;
        }

        private static EventTrigger.Entry GetEventEntry(this EventTrigger self, EventTriggerType eventID) => self.triggers.Find(self => self.eventID == eventID);
    }
}