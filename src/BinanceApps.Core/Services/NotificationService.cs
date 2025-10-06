using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Media;
using Microsoft.Extensions.Configuration;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 通知服务，处理声音提醒和推送通知
    /// </summary>
    public class NotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        
        public NotificationService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        /// <summary>
        /// 发送通知
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="content">通知内容</param>
        /// <param name="type">通知类型</param>
        public async Task SendNotificationAsync(string title, string content, string type = "info")
        {
            try
            {
                // 发送声音提醒
                if (bool.Parse(_configuration["NotificationSettings:SoundAlert"] ?? "false"))
                {
                    PlaySoundAlert(type);
                }

                // 发送推送通知
                if (bool.Parse(_configuration["NotificationSettings:PushNotification"] ?? "false"))
                {
                    await SendPushNotificationAsync(title, content, type);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 发送通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放声音提醒
        /// </summary>
        /// <param name="type">通知类型</param>
        private void PlaySoundAlert(string type)
        {
            try
            {
                // 检查是否为Windows平台
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    // 使用Console.Beep播放系统提醒音
                    switch (type.ToLower())
                    {
                        case "error":
                        case "warning":
                            Console.Beep(800, 500); // 高频短音
                            break;
                        case "success":
                            Console.Beep(600, 300); // 中频短音
                            break;
                        default:
                            Console.Beep(400, 200); // 低频短音
                            break;
                    }
                }
                else
                {
                    Console.WriteLine($"🔔 声音提醒: {type}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 播放声音提醒失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送推送通知
        /// </summary>
        /// <param name="title">推送标题</param>
        /// <param name="content">推送内容</param>
        /// <param name="type">推送类型</param>
        private async Task SendPushNotificationAsync(string title, string content, string type)
        {
            try
            {
                var token = _configuration["NotificationSettings:PushToken"];
                var pushTitle = _configuration["NotificationSettings:PushTitle"] ?? "BinanceApps提醒";
                
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("⚠️ 推送Token未配置，跳过推送通知");
                    return;
                }

                var payload = new
                {
                    pushkey = token,
                    text = $"{pushTitle} - {title}",
                    desp = content,
                    type = type
                };

                var jsonContent = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var pushUrl = _configuration["NotificationSettings:PushUrl"] ?? "https://wx.xtuis.cn";
                var response = await _httpClient.PostAsync(pushUrl, httpContent);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ 推送通知发送成功");
                }
                else
                {
                    Console.WriteLine($"❌ 推送通知发送失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 发送推送通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 