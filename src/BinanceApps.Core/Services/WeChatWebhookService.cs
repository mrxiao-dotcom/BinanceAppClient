using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 企业微信Webhook推送服务
    /// </summary>
    public class WeChatWebhookService
    {
        private readonly string _webhookUrl;
        private readonly HttpClient _httpClient;
        private readonly ILogger? _logger;

        public WeChatWebhookService(string webhookUrl, ILogger? logger = null)
        {
            _webhookUrl = webhookUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _logger = logger;
        }

        /// <summary>
        /// 发送文本消息
        /// </summary>
        public async Task<bool> SendTextMessageAsync(string content, bool mentionAll = false)
        {
            try
            {
                // 根据企业微信官方文档，text消息需要mentioned_list才会有通知
                var payload = new
                {
                    msgtype = "text",
                    text = new
                    {
                        content = content,
                        mentioned_list = mentionAll ? new[] { "@all" } : Array.Empty<string>()
                    }
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
                { 
                    WriteIndented = false,  // 不格式化，避免不必要的换行
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📤 正在推送到企业微信...");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📝 发送的JSON: {json}");
                
                var response = await _httpClient.PostAsync(_webhookUrl, httpContent);
                var responseText = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📨 企业微信响应: {responseText}");

                // 检查响应内容中的errcode
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<JsonDocument>(responseText);
                        if (result != null && result.RootElement.TryGetProperty("errcode", out var errcode))
                        {
                            var errorCode = errcode.GetInt32();
                            if (errorCode == 0)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 企业微信推送成功");
                                return true;
                            }
                            else
                            {
                                var errmsg = result.RootElement.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 企业微信推送失败: errcode={errorCode}, errmsg={errmsg}");
                                _logger?.LogWarning($"企业微信推送失败: errcode={errorCode}, errmsg={errmsg}");
                                return false;
                            }
                        }
                        else
                        {
                            // 如果没有errcode字段，认为成功
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 企业微信推送成功（无errcode）");
                            return true;
                        }
                    }
                    catch
                    {
                        // 如果无法解析JSON，认为成功
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 企业微信推送成功（响应解析失败）");
                        return true;
                    }
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 企业微信推送失败: HTTP {response.StatusCode}, {responseText}");
                    _logger?.LogWarning($"企业微信推送失败: {responseText}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 企业微信推送异常: {ex.Message}");
                _logger?.LogError(ex, "企业微信推送异常");
                return false;
            }
        }

        /// <summary>
        /// 发送Markdown消息
        /// </summary>
        public async Task<bool> SendMarkdownMessageAsync(string content)
        {
            try
            {
                var payload = new
                {
                    msgtype = "markdown",
                    markdown = new
                    {
                        content = content
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📤 正在推送到企业微信...");
                var response = await _httpClient.PostAsync(_webhookUrl, httpContent);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 企业微信推送成功");
                    return true;
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 企业微信推送失败: {responseText}");
                    _logger?.LogWarning($"企业微信推送失败: {responseText}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 企业微信推送异常: {ex.Message}");
                _logger?.LogError(ex, "企业微信推送异常");
                return false;
            }
        }

        /// <summary>
        /// 发送预警通知
        /// </summary>
        public async Task<bool> SendAlertAsync(string symbol, string direction, decimal price, decimal ema, decimal distancePercent)
        {
            try
            {
                var directionEmoji = direction == "多头" ? "📈" : "📉";
                var distanceSign = distancePercent >= 0 ? "+" : "";
                
                // 构建消息内容（简化格式，确保能正常显示）
                var message = $"⚠️ 价格预警\n" +
                             $"合约：{symbol}\n" +
                             $"方向：{direction} {directionEmoji}\n" +
                             $"价格：{price:F8}\n" +
                             $"EMA：{ema:F8}\n" +
                             $"距离：{distanceSign}{distancePercent:F2}%\n" +
                             $"时间：{DateTime.Now:HH:mm:ss}";

                // @all 确保有通知提醒
                return await SendTextMessageAsync(message, mentionAll: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 构建预警消息失败: {ex.Message}");
                _logger?.LogError(ex, "构建预警消息失败");
                return false;
            }
        }

        /// <summary>
        /// 发送测试消息
        /// </summary>
        public async Task<bool> SendTestMessageAsync()
        {
            var testMessage = $"🧪 测试消息\n" +
                             $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                             $"如果收到此消息，webhook配置正确！";
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🧪 发送测试消息...");
            // 测试消息也@all，确保能收到
            return await SendTextMessageAsync(testMessage, mentionAll: true);
        }
    }
}
