// 职责：Q31.32 定点数，范围 ±32768，Burst 兼容，乘法用 double 中间结果保证确定性。
// Responsibility: Q31.32 fixed-point number, range ±32768, Burst-compatible; uses double intermediates for deterministic multiplication.
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace AOT
{
    [BurstCompile]
    public readonly struct Fix64 : System.IEquatable<Fix64>, System.IComparable<Fix64>
    {
        public const long One = 1L << 32;
        public const long Half = One >> 1;
        public const double MaxValue = 32768.0;

        private readonly long _rawValue;

        public Fix64(long rawValue)
        {
            _rawValue = rawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long RawValue
        {
            get { return _rawValue; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64 FromRaw(long raw)
        {
            return new Fix64(raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64 FromFloat(float value)
        {
            return new Fix64((long)(value * One));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ToFloat()
        {
            return (float)_rawValue / One;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64 operator +(Fix64 a, Fix64 b)
        {
            return new Fix64(a._rawValue + b._rawValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64 operator -(Fix64 a, Fix64 b)
        {
            return new Fix64(a._rawValue - b._rawValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64 operator *(Fix64 a, Fix64 b)
        {
            long result = (long)((double)a._rawValue * (double)b._rawValue / (double)One);
            return new Fix64(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64 operator /(Fix64 a, Fix64 b)
        {
            double result = (double)a._rawValue / (double)b._rawValue;
            return new Fix64((long)(result * (double)One));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64 operator -(Fix64 a)
        {
            return new Fix64(-a._rawValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Fix64 other)
        {
            return _rawValue == other._rawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(Fix64 other)
        {
            if (_rawValue < other._rawValue)
                return -1;
            if (_rawValue > other._rawValue)
                return 1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is Fix64 other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return _rawValue.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Fix64 a, Fix64 b)
        {
            return a._rawValue == b._rawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Fix64 a, Fix64 b)
        {
            return a._rawValue != b._rawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(Fix64 a, Fix64 b)
        {
            return a._rawValue < b._rawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(Fix64 a, Fix64 b)
        {
            return a._rawValue > b._rawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(Fix64 a, Fix64 b)
        {
            return a._rawValue <= b._rawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(Fix64 a, Fix64 b)
        {
            return a._rawValue >= b._rawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ((float)_rawValue / One).ToString();
        }
    }
}
