/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/


using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lin.Runtime.Helper
{
    public static class LocalTimeUtil
    {
        private static readonly DateTime UnixEpoch =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 将服务器 UTC 时间戳转换为指定时区的本地时间（自动处理DST）
        /// </summary>
        /// <param name="timestamp">Unix时间戳（秒或毫秒）</param>
        /// <param name="isMilliseconds">是否为毫秒级时间戳</param>
        /// <param name="timeZoneId">时区ID（null = 当前系统时区）</param>
        public static DateTime TimestampToLocal(long timestamp, bool isMilliseconds = false, string timeZoneId = null)
        {
            double seconds = isMilliseconds ? timestamp / 1000.0 : timestamp;
            DateTime utcTime = UnixEpoch.AddSeconds(seconds);

            try
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                // Editor / PC 完整支持 TimeZoneInfo
                TimeZoneInfo zone = string.IsNullOrEmpty(timeZoneId)
                    ? TimeZoneInfo.Local
                    : SafeFindTimeZone(timeZoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime, zone);

#elif UNITY_ANDROID || UNITY_IOS
            // 移动端 ToLocalTime() 更稳定（系统自动处理DST）
            if (string.IsNullOrEmpty(timeZoneId))
                return utcTime.ToLocalTime();

            // 如果指定时区，尝试用 TimeZoneInfo
            var zone = SafeFindTimeZone(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, zone);

#else
            // 其他平台（WebGL / Console）
            return utcTime.ToLocalTime();
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalTimeUtil] 时区转换失败，使用 ToLocalTime() 兜底。原因: {ex.Message}");
                return utcTime.ToLocalTime();
            }
        }

        /// <summary>
        /// 返回带DST标签的字符串（适合UI显示）
        /// </summary>
        public static string TimestampToLocalString(long timestamp, bool isMilliseconds = false, string timeZoneId = null)
        {
            DateTime localTime = TimestampToLocal(timestamp, isMilliseconds, timeZoneId);
            string zoneName = "Unknown";
            string dstMark = "";

            try
            {
                TimeZoneInfo zone = string.IsNullOrEmpty(timeZoneId)
                    ? TimeZoneInfo.Local
                    : SafeFindTimeZone(timeZoneId);

                bool isDst = zone.IsDaylightSavingTime(localTime);
                dstMark = isDst ? "（夏令时）" : "（冬令时）";
                zoneName = zone.DisplayName;
            }
            catch { }

            return $"{localTime:yyyy-MM-dd HH:mm:ss} {dstMark} [{zoneName}]";
        }

        /// <summary>
        /// 安全获取时区信息（兼容不同平台）
        /// </summary>
        private static TimeZoneInfo SafeFindTimeZone(string timeZoneId)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
                // 兼容不同平台命名差异（例如 Android 用 IANA，Windows 用标准名）
                if (Alias.TryGetValue(timeZoneId, out string alt))
                    return TimeZoneInfo.FindSystemTimeZoneById(alt);

                Debug.LogWarning($"[LocalTimeUtil] 未找到时区ID: {timeZoneId}，使用系统默认。");
                return TimeZoneInfo.Local;
            }
        }

        /// <summary>
        /// 常见时区别名映射（Windows ↔ IANA）
        /// </summary>
        private static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
    {
        { "Asia/Shanghai", "China Standard Time" },
        { "Asia/Tokyo", "Tokyo Standard Time" },
        { "America/New_York", "Eastern Standard Time" },
        { "Europe/London", "GMT Standard Time" },
        { "Europe/Paris", "Romance Standard Time" },
        { "Australia/Sydney", "AUS Eastern Standard Time" },
        { "UTC", "UTC" },
        { "Etc/UTC", "UTC" }
    };
    }
}