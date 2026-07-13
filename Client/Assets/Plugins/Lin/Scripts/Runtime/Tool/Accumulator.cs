/*
┌────────────────────────────┐
│　Description: 累积器
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: Accumulator
└──────────────┘
*/

using Lin.Runtime.Interface;
using Lin.Runtime.Manager;
using System;
using UnityEngine;

namespace Lin.Runtime.Tool
{
    public class Accumulator : IPoolObject
    {
        private float triggerInterval;
        private Action onFinishAccumulation;
        private Action trigger;

        public float targetAccumulation { get; private set; }
        private float accumulateInterval;
        public float accumulateTime { get; private set; }
        public int currentTriggerTimes { get; private set; }

        /// <param name="targetAccumulation">目标存在时间</param>
        /// <param name="triggerTimes">存在时间内触发的次数</param>
        /// <param name="trigger">触发事件</param>
        /// <param name="onFinishAccumulation">完全完成计时</param>
        public void Init(float targetAccumulation, float triggerInterval, Action trigger = null, Action onFinishAccumulation = null)
        {
            if (triggerInterval <= 0)
                throw new Exception($"{nameof(triggerInterval)} <= 0");

            accumulateInterval = 0;
            this.targetAccumulation = targetAccumulation;
            this.triggerInterval = triggerInterval;
            this.onFinishAccumulation = onFinishAccumulation;
            this.trigger = trigger;

            MonoRunner.GetInstance().AddListener(MonoRunner.EUpdateType.Update, Accumulate);
        }

        private void Accumulate()
        {
            accumulateInterval += Time.deltaTime;

            while (accumulateInterval >= triggerInterval)
            {
                accumulateInterval -= triggerInterval;
                trigger?.Invoke();
                currentTriggerTimes++;

                accumulateTime += triggerInterval;
                if (accumulateTime >= targetAccumulation)
                {
                    MonoRunner.GetInstance().RemoveListener(MonoRunner.EUpdateType.Update, Accumulate);
                    onFinishAccumulation?.Invoke();
                    Factory<Accumulator>.Release(this);
                    break;
                }
            }
        }

        public static Accumulator Get() => Factory<Accumulator>.Get();

        public void AddTargetAccumulationTime(float addition) => targetAccumulation += addition;

        public void Restart()
        {
            accumulateInterval = 0;
            accumulateTime = 0;
            currentTriggerTimes = 0;
        }

        public void OnGet()
        {
            onFinishAccumulation = null;
            trigger = null;
        }

        public void OnRelease()
        {
            onFinishAccumulation = null;
            trigger = null;
        }
    }
}
