using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 时间戳修复工具类
    /// 确保生成的时间戳符合币安API要求的纯数字格式
    /// </summary>
    public static class TimestampFixer
    {
        /// <summary>
        /// 生成安全的时间戳字符串，确保只包含数字
        /// </summary>
        /// <returns>纯数字格式的时间戳字符串</returns>
        public static string GenerateSafeTimestamp()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timestampStr = timestamp.ToString(CultureInfo.InvariantCulture);
            
            // 验证时间戳格式，确保只包含数字
            var isValid = Regex.IsMatch(timestampStr, @"^[0-9]{1,20}$");
            if (!isValid)
            {
                Console.WriteLine($"⚠️ 时间戳格式异常: '{timestampStr}'，尝试修复...");
                // 如果格式异常，强制转换为纯数字格式
                timestampStr = Regex.Replace(timestampStr, @"[^0-9]", "");
                Console.WriteLine($"✅ 修复后的时间戳: '{timestampStr}'");
            }
            
            return timestampStr;
        }
        
        /// <summary>
        /// 验证时间戳格式是否有效
        /// </summary>
        /// <param name="timestampStr">时间戳字符串</param>
        /// <returns>是否有效</returns>
        public static bool IsValidTimestamp(string timestampStr)
        {
            return Regex.IsMatch(timestampStr, @"^[0-9]{1,20}$");
        }
    }
} 