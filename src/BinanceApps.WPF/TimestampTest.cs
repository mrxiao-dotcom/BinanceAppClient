using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BinanceApps.WPF
{
    public static class TimestampTest
    {
        public static void TestTimestampFormatting()
        {
            Console.WriteLine("=== 时间戳格式化测试 ===");
            
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // 测试默认格式化
            var defaultStr = timestamp.ToString();
            var invariantStr = timestamp.ToString(CultureInfo.InvariantCulture);
            
            Console.WriteLine($"原始时间戳: {timestamp}");
            Console.WriteLine($"默认ToString(): '{defaultStr}'");
            Console.WriteLine($"不变文化ToString(): '{invariantStr}'");
            
            // 验证格式
            var regex = new Regex(@"^[0-9]{1,20}$");
            var isDefaultValid = regex.IsMatch(defaultStr);
            var isInvariantValid = regex.IsMatch(invariantStr);
            
            Console.WriteLine($"默认格式是否有效: {(isDefaultValid ? "✅" : "❌")}");
            Console.WriteLine($"不变文化格式是否有效: {(isInvariantValid ? "✅" : "❌")}");
            Console.WriteLine($"当前区域设置: {CultureInfo.CurrentCulture.Name}");
            
            if (!isDefaultValid)
            {
                Console.WriteLine("⚠️ 默认格式化包含非数字字符，这就是API错误的原因！");
            }
            
            Console.WriteLine("=== 测试完成 ===");
            
            // 仅在控制台显示结果，不弹出窗口
            Console.WriteLine($"✅ 时间戳测试完成 - 默认格式: {(isDefaultValid ? "有效" : "无效")}, 不变文化: {(isInvariantValid ? "有效" : "无效")}");
        }
    }
} 