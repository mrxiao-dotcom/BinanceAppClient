using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BinanceApps.WPF
{
    public static class TimestampDebugTest
    {
        public static void RunDetailedTimestampTest()
        {
            Console.WriteLine("=== 详细时间戳格式化调试测试 ===");
            
            // 测试1: 基本时间戳生成
            var now = DateTimeOffset.UtcNow;
            var timestamp1 = now.ToUnixTimeMilliseconds();
            Console.WriteLine($"📅 当前UTC时间: {now:yyyy-MM-dd HH:mm:ss.fff} UTC");
            Console.WriteLine($"🔢 Unix时间戳(毫秒): {timestamp1}");
            
            // 测试2: 不同文化设置下的字符串转换
            Console.WriteLine("\n=== 不同文化设置测试 ===");
            
            var cultures = new[] { 
                CultureInfo.InvariantCulture,
                CultureInfo.CurrentCulture,
                new CultureInfo("en-US"),
                new CultureInfo("zh-CN")
            };
            
            foreach (var culture in cultures)
            {
                var timestampStr = timestamp1.ToString(culture);
                var isValid = Regex.IsMatch(timestampStr, @"^[0-9]{1,20}$");
                Console.WriteLine($"🌍 {culture.Name,-12}: '{timestampStr}' - {(isValid ? "✅ 有效" : "❌ 无效")}");
                
                if (!isValid)
                {
                    Console.WriteLine($"   ⚠️ 包含非数字字符，详细分析:");
                    for (int i = 0; i < timestampStr.Length; i++)
                    {
                        char c = timestampStr[i];
                        if (!char.IsDigit(c))
                        {
                            Console.WriteLine($"      位置 {i}: '{c}' (ASCII: {(int)c})");
                        }
                    }
                }
            }
            
            // 测试3: 强制使用InvariantCulture
            Console.WriteLine("\n=== 强制InvariantCulture测试 ===");
            var safeTimestamp = timestamp1.ToString(CultureInfo.InvariantCulture);
            var regex = new Regex(@"^[0-9]{1,20}$");
            var isValidSafe = regex.IsMatch(safeTimestamp);
            
            Console.WriteLine($"🔒 强制InvariantCulture: '{safeTimestamp}'");
            Console.WriteLine($"📏 长度: {safeTimestamp.Length}");
            Console.WriteLine($"✅ 格式验证: {(isValidSafe ? "通过" : "失败")}");
            
            // 测试4: 字符逐个检查
            Console.WriteLine("\n=== 字符逐个检查 ===");
            bool allDigits = true;
            for (int i = 0; i < safeTimestamp.Length; i++)
            {
                char c = safeTimestamp[i];
                bool isDigit = char.IsDigit(c);
                if (!isDigit)
                {
                    Console.WriteLine($"❌ 位置 {i}: '{c}' 不是数字 (ASCII: {(int)c})");
                    allDigits = false;
                }
            }
            if (allDigits)
            {
                Console.WriteLine("✅ 所有字符都是数字");
            }
            
            // 测试5: 币安API格式要求测试
            Console.WriteLine("\n=== 币安API格式要求测试 ===");
            var binanceRegex = new Regex(@"^[0-9]{1,20}$");
            var binanceValid = binanceRegex.IsMatch(safeTimestamp);
            Console.WriteLine($"🏦 币安API格式: {(binanceValid ? "✅ 符合" : "❌ 不符合")}");
            Console.WriteLine($"📊 时间戳范围: 1-20位数字");
            Console.WriteLine($"📊 实际长度: {safeTimestamp.Length}位");
            
            // 测试6: 生成多个时间戳测试一致性
            Console.WriteLine("\n=== 连续生成测试 ===");
            for (int i = 0; i < 3; i++)
            {
                var testTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var testStr = testTimestamp.ToString(CultureInfo.InvariantCulture);
                var testValid = binanceRegex.IsMatch(testStr);
                Console.WriteLine($"🔄 测试{i+1}: {testStr} - {(testValid ? "✅" : "❌")}");
                System.Threading.Thread.Sleep(10); // 短暂延迟
            }
            
            Console.WriteLine("=== 时间戳调试测试完成 ===\n");
        }
    }
} 