/*
┌────────────────────────────┐
│　Description: 属性计算器
│　Remark: 
└────────────────────────────┘
*/
using System;
using ZLinq;

namespace Lin.Runtime.Tool
{
    public class FloatProperty
    {
        public delegate void FloatPropertyDelegate(float value);

        public event FloatPropertyDelegate onValueChanged;

        private float minValue;
        private float maxValue;
        public float baseValue { get; private set; }
        public float currentValue { get; private set; }

        private readonly System.Collections.Generic.Dictionary<int, float> additiveMap = new System.Collections.Generic.Dictionary<int, float>(32);
        private readonly System.Collections.Generic.Dictionary<int, float> percentageMap = new System.Collections.Generic.Dictionary<int, float>(32);

        public System.Collections.Generic.IReadOnlyDictionary<int, float> GetAdditives() => additiveMap;
        public System.Collections.Generic.IReadOnlyDictionary<int, float> GetPercentages() => percentageMap;

        public FloatProperty(float baseValue = 0, float minValue = float.NegativeInfinity, float maxValue = float.PositiveInfinity)
        {
            this.baseValue = baseValue;
            this.minValue = minValue;
            this.maxValue = maxValue;

            Recalculate(true);
        }

        public void SetRange(float minValue, float maxValue)
        {
            this.minValue = minValue;
            this.maxValue = maxValue;

            Recalculate();
        }

        public void SetBase(float value)
        {
            baseValue = value;
            Recalculate();
        }

        public void SetAdditive(int id, float value)
        {
            additiveMap[id] = value;
            Recalculate();
        }

        public void RemoveAdditive(int id)
        {
            if (additiveMap.Remove(id)) Recalculate();
        }

        public void SetPercentage(int id, float value)
        {
            percentageMap[id] = value;
            Recalculate();
        }

        public void RemovePercentage(int id)
        {
            if (percentageMap.Remove(id)) 
                Recalculate();
        }

        public float GetAdditiveTotal()
        {
            float sum = 0f;
            foreach (var v in additiveMap.Values) sum += v;
            return sum;
        }

        public float GetPercentageTotal()
        {
            float sum = 0f;
            foreach (var v in percentageMap.Values) sum += v;
            return sum;
        }

        public BonusInfo GetBonusInfo()
        {
            var add = GetAdditiveTotal();
            var pct = GetPercentageTotal();
            return new BonusInfo(baseValue, add, pct, currentValue);
        }

        private void Recalculate(bool resetCurrent = false) 
        { 
            var oldValue = currentValue;

            float add = additiveMap.Values.AsValueEnumerable().Sum();
            float pct = percentageMap.Values.AsValueEnumerable().Sum();

            float final = (baseValue + add) * (1f + pct);

            if (final < minValue) 
                final = minValue;

            if (final > maxValue) 
                final = maxValue;

            if (resetCurrent) 
                currentValue = final;
            else
            {
                if (currentValue < minValue)
                    currentValue = minValue;

                if (currentValue > final)
                    currentValue = final;
            }

            if (oldValue != currentValue)
                onValueChanged?.Invoke(currentValue);
        }

        public void SetValue(float value)
        {
            currentValue = value;
            Recalculate();
        }

        public void AddValue(float delta)
        {
            currentValue += delta;
            Recalculate();
        }

        public void ReduceValue(float delta)
        {
            currentValue -= delta;
            Recalculate();
        }

        public readonly struct BonusInfo
        {
            public readonly float baseValue;
            public readonly float additiveTotal;
            public readonly float percentageTotal;
            public readonly float finalValue;

            public BonusInfo(float baseValue, float additiveTotal, float percentageTotal, float finalValue)
            {
                this.baseValue = baseValue;
                this.additiveTotal = additiveTotal;
                this.percentageTotal = percentageTotal;
                this.finalValue = finalValue;
            }
        }
    }
}
