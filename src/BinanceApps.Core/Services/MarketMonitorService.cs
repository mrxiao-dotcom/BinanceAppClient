using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BinanceApps.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 市场监控服务
    /// </summary>
    public class MarketMonitorService
    {
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly NotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private Timer? _monitorTimer;
        private DateTime _lastPushDate = DateTime.MinValue;
        private int _soundAlertCount = 0;
        private DateTime _lastSoundAlert = DateTime.MinValue;
        private CancellationTokenSource? _cancellationTokenSource;

        public MarketMonitorService(
            IBinanceSimulatedApiClient apiClient, 
            NotificationService notificationService,
            IConfiguration configuration)
        {
            _apiClient = apiClient;
            _notificationService = notificationService;
            _configuration = configuration;
        }

        /// <summary>
        /// 启动市场监控
        /// </summary>
        public void StartMonitoring()
        {
            try
            {
                var enabled = bool.Parse(_configuration["MarketMonitor:Enabled"] ?? "false");
                if (!enabled)
                {
                    Console.WriteLine("📊 市场监控未启用");
                    return;
                }

                var intervalMinutes = int.Parse(_configuration["MarketMonitor:CheckIntervalMinutes"] ?? "30");
                
                Console.WriteLine($"📊 启动市场监控服务，检查间隔: {intervalMinutes}分钟");
                
                _cancellationTokenSource = new CancellationTokenSource();
                
                // 启动监控定时器
                _monitorTimer = new Timer(async _ => await CheckMarketVolumeAsync(), null, 
                    TimeSpan.Zero, TimeSpan.FromMinutes(intervalMinutes));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动市场监控服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止市场监控
        /// </summary>
        public void StopMonitoring()
        {
            try
            {
                Console.WriteLine("📊 停止市场监控服务");
                
                _monitorTimer?.Dispose();
                _monitorTimer = null;
                
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 停止市场监控服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查市场成交额
        /// </summary>
        private async Task CheckMarketVolumeAsync()
        {
            try
            {
                if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                    return;

                Console.WriteLine($"📊 {DateTime.Now:HH:mm:ss} 开始检查市场成交额...");

                // 获取24H数据
                var allTickers = await _apiClient.GetAllTicksAsync();
                if (allTickers == null || !allTickers.Any())
                {
                    Console.WriteLine("⚠️ 无法获取24H数据");
                    return;
                }

                // 过滤可交易的永续合约
                var tradablePerpetuals = allTickers.Where(t => 
                    t.Symbol.EndsWith("USDT") && 
                    t.Count > 0 && // 有交易活动
                    t.QuoteVolume > 0 // 有成交额
                ).ToList();

                // 计算总成交额
                var totalVolume = tradablePerpetuals.Sum(t => t.QuoteVolume);
                var totalVolumeBillion = totalVolume / 1_000_000_000; // 转换为亿

                Console.WriteLine($"📊 当前24H总成交额: {totalVolumeBillion:F2}亿USDT (来自{tradablePerpetuals.Count}个合约)");

                // 检查是否超过阈值
                var threshold = decimal.Parse(_configuration["MarketMonitor:VolumeThresholdBillion"] ?? "100");
                
                if (totalVolumeBillion >= threshold)
                {
                    await HandleVolumeThresholdExceeded(totalVolumeBillion, threshold);
                }
                else
                {
                    // 重置声音提醒计数
                    _soundAlertCount = 0;
                    Console.WriteLine($"💚 市场成交额正常 ({totalVolumeBillion:F2}亿 < {threshold}亿)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 检查市场成交额失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理成交额超过阈值的情况
        /// </summary>
        private async Task HandleVolumeThresholdExceeded(decimal totalVolume, decimal threshold)
        {
            try
            {
                Console.WriteLine($"🚨 市场成交额超过阈值！当前: {totalVolume:F2}亿USDT, 阈值: {threshold}亿USDT");

                var now = DateTime.Now;
                var soundAlertInterval = int.Parse(_configuration["MarketMonitor:SoundAlertIntervalMinutes"] ?? "1");
                var maxSoundAlerts = int.Parse(_configuration["MarketMonitor:SoundAlertCount"] ?? "3");

                // 处理声音提醒 (1分钟1次，连续3次)
                if (_soundAlertCount < maxSoundAlerts && 
                    (now - _lastSoundAlert).TotalMinutes >= soundAlertInterval)
                {
                    _soundAlertCount++;
                    _lastSoundAlert = now;
                    
                    Console.WriteLine($"🔔 发送声音提醒 ({_soundAlertCount}/{maxSoundAlerts})");
                    
                    // 发送声音提醒
                    if (bool.Parse(_configuration["NotificationSettings:SoundAlert"] ?? "false"))
                    {
                        await _notificationService.SendNotificationAsync(
                            "市场过热提醒", 
                            $"24H成交额达到{totalVolume:F2}亿USDT，超过{threshold}亿阈值", 
                            "warning");
                    }
                }

                // 处理微信推送 (当天只推送1次)
                var today = now.Date;
                if (_lastPushDate.Date != today)
                {
                    _lastPushDate = now;
                    
                    Console.WriteLine("📱 发送微信推送通知");
                    
                    // 发送推送通知
                    if (bool.Parse(_configuration["NotificationSettings:PushNotification"] ?? "false"))
                    {
                        await SendPushNotificationAsync(totalVolume, threshold);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 处理成交额超过阈值失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送推送通知到所有配置的Token
        /// </summary>
        private async Task SendPushNotificationAsync(decimal totalVolume, decimal threshold)
        {
            try
            {
                var tokensSection = _configuration.GetSection("NotificationSettings:PushTokens");
                if (tokensSection == null || !tokensSection.GetChildren().Any())
                {
                    Console.WriteLine("⚠️ 未配置推送Token");
                    return;
                }

                var tokens = tokensSection.GetChildren().Select(x => x.Value).Where(x => !string.IsNullOrEmpty(x)).ToList();
                if (!tokens.Any())
                {
                    Console.WriteLine("⚠️ 推送Token列表为空");
                    return;
                }

                var title = _configuration["NotificationSettings:PushTitle"] ?? "BinanceApps提醒";
                var content = $"""
                🚨 市场过热警告！

                📊 当前24H成交额：{totalVolume:F2}亿USDT
                ⚠️ 设定阈值：{threshold}亿USDT
                📈 超出比例：{((totalVolume - threshold) / threshold * 100):F1}%

                ⏰ 检测时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}

                请注意市场风险，谨慎操作！
                """;

                foreach (var token in tokens)
                {
                    if (string.IsNullOrEmpty(token))
                        continue;
                        
                    try
                    {
                        await SendSinglePushNotificationAsync(token, title, content);
                        Console.WriteLine($"✅ 推送通知发送成功: {token[..Math.Min(8, token.Length)]}...");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ 推送通知发送失败 ({token[..Math.Min(8, token.Length)]}...): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 发送推送通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送单个推送通知
        /// </summary>
        private async Task SendSinglePushNotificationAsync(string token, string title, string content)
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // 使用虾推啥API格式
            var url = $"https://wx.xtuis.cn/{token}.send";
            var parameters = $"text={Uri.EscapeDataString(title)}&desp={Uri.EscapeDataString(content)}";
            
            var response = await httpClient.GetAsync($"{url}?{parameters}");
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"推送请求失败: {response.StatusCode}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // 检查响应内容是否包含错误信息
            if (responseContent.Contains("error") || responseContent.Contains("失败"))
            {
                throw new Exception($"推送失败: {responseContent}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
        }
    }
} 