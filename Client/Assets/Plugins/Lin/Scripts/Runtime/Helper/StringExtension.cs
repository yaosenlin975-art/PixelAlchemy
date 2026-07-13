using Cysharp.Text;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
#if !GAME_SERVER
using System;
using System.Linq;
#endif

namespace Lin.Runtime.Helper
{
    public static class StringExtension
    {
        public static string PrettyAddress(this IPEndPoint endPoint)
        {
            if (endPoint == null) return "";

            // Map to IPv4 if "IsIPv4MappedToIPv6" for readability
            // "::ffff:127.0.0.1" -> "127.0.0.1"
            return
                endPoint.Address.IsIPv4MappedToIPv6
                ? endPoint.Address.MapToIPv4().ToString()
                : endPoint.Address.ToString();
        }

        public static byte[] ToBytes(this string self) => System.Text.Encoding.Default.GetBytes(self);

        public static T ToEnum<T>(this string str)
            where T : struct, Enum
        {
            if (Enum.TryParse<T>(str, true, out var result))
                return result;

            throw new ArgumentException(ZString.Format("Cannot convert '{0}' to enum {1}", str, typeof(T).Name));
        }

        public static string Truncate(this string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return str.Length <= maxLength ? str : str.Substring(0, maxLength);
        }

        public static string ToTitleCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
        }

        public static bool IsNullOrEmpty(this string str) => string.IsNullOrEmpty(str);

        public static bool IsNullOrWhiteSpace(this string str) => string.IsNullOrWhiteSpace(str);

        public static string Reverse(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            
            using var sb = ZString.CreateStringBuilder();
            for (int i = str.Length - 1; i >= 0; i--)
            {
                sb.Append(str[i]);
            }
            return sb.ToString();
        }

        public static string RemoveWhitespace(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            
            using var sb = ZString.CreateStringBuilder();
            foreach (char c in str)
            {
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string ToCamelCase(this string str)
        {
            if (string.IsNullOrEmpty(str) || !char.IsUpper(str[0]))
                return str;

            using var sb = ZString.CreateStringBuilder();
            sb.Append(char.ToLower(str[0]));
            if (str.Length > 1)
                sb.Append(str.AsSpan(1));
            return sb.ToString();
        }

        public static string SplitCamelCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return Regex.Replace(str, "([a-z])([A-Z])", "$1 $2");
        }

        public static string LastCharacters(this string str, int charactersCount)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return str.Length <= charactersCount
                ? str
                : str.Substring(str.Length - charactersCount);
        }

        public static string FirstCharacters(this string str, int charactersCount)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            return str.Length <= charactersCount ? str : str.Substring(0, charactersCount);
        }

        public unsafe static string FirstCharacterUpper(this string self)
        {
            if (string.IsNullOrEmpty(self))
                return self;

            fixed(char* ptr = self)
            {
                if (*ptr < 'a' || *ptr > 'z')
                    return self;

                *ptr = (char)(*ptr - 'a' + 'A');
            }
            return self;
        }

        public unsafe static string FirstCharacterLower(this string self)
        {
            if (string.IsNullOrEmpty(self))
                return self;


            fixed (char* ptr = self)
            {
                if (*ptr < 'A' || *ptr > 'Z')
                    return self;

                *ptr = (char)(*ptr - 'A' + 'a');
            }
            return self;
        }

        /// <summary>
        /// 去除除了数字和字母外的所有字符
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string RemoveSymbols(this string self) => Regex.Replace(self, "[^a-zA-Z0-9]", string.Empty);

        /// <summary>
        /// FNV-1a哈希算法
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static long GetStableHashCode64(this string self)
        {
            unchecked
            {
                const long p = 1099511628211;
                long hash = (long)14695981039346656037;
                for (int i = 0; i < self.Length; i++)
                    hash = (hash ^ self[i]) * p;
                return hash;
            }
        }

#if !GAME_SERVER
        #region - 数字转字符串 - 

        //伤害 治疗会用到
        //长度, 字符串
        private static UnityEngine.Pool.ObjectPool<string> numberStringPool = new UnityEngine.Pool.ObjectPool<string>(createFunc: () => new string('\0', 24));

        public static void RecycleNumberString(string str)
        {
            if (str == null || str.Length < 24)
                return;

            try
            {
                numberStringPool.Release(str);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 只能主线程调用, 负责会出现资源争夺, 正常也只有治疗 伤害 等主线程UI上的数值显示会用到, 记得回收
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public unsafe static string GetNumberString(this uint self)
        {
            if (self == 0)
                return "0";

            // 计算数字位数
            int length = (int)Math.Floor(Math.Log10(self)) + 1;

            var numberString = numberStringPool.Get();
            fixed (char* ptr = numberString)
            {
                char* current = ptr;

                // 从最高位开始填充字符
                for (int i = length - 1; i >= 0; i--)
                {
                    int power = (int)Math.Pow(10, i);
                    int digit = (int)(self / (uint)power) - (int)(self / (uint)(power * 10)) * 10;
                    *current++ = (char)(digit + '0');
                }

                *current = '\0';
            }

            return numberString;
        }

        /// <summary>
        /// 只能主线程调用, 负责会出现资源争夺, 正常也只有治疗 伤害 等主线程UI上的数值显示会用到, 记得回收
        /// </summary>
        public unsafe static string GetNumberString(this short self) => GetNumberString((long)self);

        /// <summary>
        /// 只能主线程调用, 负责会出现资源争夺, 正常也只有治疗 伤害 等主线程UI上的数值显示会用到, 记得回收
        /// </summary>
        public unsafe static string GetNumberString(this int self) => GetNumberString((long)self);

        /// <summary>
        /// 只能主线程调用, 负责会出现资源争夺, 正常也只有治疗 伤害 等主线程UI上的数值显示会用到, 记得回收
        /// </summary>
        public unsafe static string GetNumberString(this ushort self) => GetNumberString((uint)self);

        /// <summary>
        /// 只能主线程调用, 负责会出现资源争夺, 正常也只有治疗 伤害 等主线程UI上的数值显示会用到, 记得回收
        /// </summary>
        public unsafe static string GetNumberString(this long self)
        {
            // 长整型实现类似int，但需要考虑更大数值范围
            if (self == 0)
                return "0";

            bool isNegative = self < 0;
            self = Math.Abs(self);

            // 计算数字位数
            int length = (int)Math.Floor(Math.Log10(self)) + 1;
            if (isNegative)
                length++;

            var numberString = numberStringPool.Get();
            fixed (char* ptr = numberString)
            {
                char* current = ptr;
                if (isNegative) *current++ = '-';

                // 从最高位开始填充字符
                for (int i = length - (isNegative ? 2 : 1); i >= 0; i--)
                {
                    long power = (long)Math.Pow(10, i);
                    int digit = (int)(self / power) - (int)(self / (power * 10)) * 10;
                    *current++ = (char)(digit + '0');
                }

                *current = '\0';
            }

            return numberString;
        }

        /// <summary>
        /// 只能主线程调用, 负责会出现资源争夺, 正常也只有治疗 伤害 等主线程UI上的数值显示会用到, 记得回收
        /// </summary>
        public unsafe static string GetNumberString(this ulong self)
        {
            if (self == 0)
                return "0";

            // 计算数字位数
            int length = (int)Math.Floor(Math.Log10(self)) + 1;

            var numberString = numberStringPool.Get();
            fixed (char* ptr = numberString)
            {
                char* current = ptr;

                // 从最高位开始填充字符
                for (int i = length - 1; i >= 0; i--)
                {
                    ulong power = (ulong)Math.Pow(10, i);
                    int digit = (int)(self / power) - (int)(self / (power * 10)) * 10;
                    *current++ = (char)(digit + '0');
                }

                *current = '\0';
            }

            return numberString;
        }

        /// <summary>
        /// 只能主线程调用, 负责会出现资源争夺, 正常也只有治疗 伤害 等主线程UI上的数值显示会用到, 记得回收
        /// </summary>
        /// <param name="self"></param>
        /// <param name="decimalPoint">保留小数位数</param>
        /// <returns></returns>
        public unsafe static string GetNumberString(this float self, int decimalPoint = 2)
        {
            if (float.IsNaN(self))
                return "NaN";

            if (float.IsInfinity(self))
                return self > 0 ? "∞" : "-∞";

            bool isNegative = self < 0;
            self = Math.Abs(self);

            // 处理整数部分
            int integerPart = (int)self;
            float fractionalPart = self - integerPart;

            // 计算整数部分位数
            int integerLength = integerPart == 0 ? 1 : (int)Math.Floor(Math.Log10(integerPart)) + 1;
            if (isNegative) integerLength++;

            // 总长度 = 整数部分 + 小数点 + 小数位数
            int totalLength = integerLength + 1 + decimalPoint;

            var numberString = numberStringPool.Get();
            fixed (char* ptr = numberString)
            {
                char* current = ptr;

                // 处理符号
                if (isNegative) *current++ = '-';

                // 处理整数部分
                for (int i = integerLength - (isNegative ? 2 : 1); i >= 0; i--)
                {
                    int power = (int)Math.Pow(10, i);
                    int digit = (integerPart / power) - (integerPart / (power * 10)) * 10;
                    *current++ = (char)(digit + '0');
                }

                // 小数点
                if (decimalPoint > 0)
                {
                    *current++ = '.';

                    // 处理小数部分
                    for (int i = 0; i < decimalPoint; i++)
                    {
                        fractionalPart *= 10;
                        int digit = (int)fractionalPart - ((int)(fractionalPart / 10)) * 10;
                        *current++ = (char)(digit + '0');
                    }
                }

                *current = '\0';
            }

            return numberString;
        }

        #endregion
#endif
    }
}
