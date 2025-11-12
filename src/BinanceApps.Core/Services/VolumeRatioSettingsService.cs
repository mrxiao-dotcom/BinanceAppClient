using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 量比异动选股参数设置服务
    /// </summary>
    public class VolumeRatioSettingsService
    {
        private readonly string _settingsFilePath;
        private readonly ILogger<VolumeRatioSettingsService>? _logger;

        public VolumeRatioSettingsService(ILogger<VolumeRatioSettingsService>? logger = null)
        {
            _logger = logger;
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "VolumeRatioSettings.json");
            
            // 确保设置目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        }

        /// <summary>
        /// 保存筛选参数
        /// </summary>
        public async Task SaveFilterAsync(VolumeRatioFilter filter)
        {
            try
            {
                var settings = new VolumeRatioSettings
                {
                    Filter = filter,
                    LastUpdated = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                await File.WriteAllTextAsync(_settingsFilePath, json);
                _logger?.LogInformation("量比异动选股参数已保存");
                Console.WriteLine("✅ 量比异动选股参数已保存");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存量比异动选股参数失败");
                Console.WriteLine($"❌ 保存量比异动选股参数失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载筛选参数
        /// </summary>
        public async Task<VolumeRatioFilter?> LoadFilterAsync()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _logger?.LogInformation("量比异动选股参数文件不存在，使用默认参数");
                    return GetDefaultFilter();
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<VolumeRatioSettings>(json);

                if (settings?.Filter != null)
                {
                    _logger?.LogInformation("量比异动选股参数已加载");
                    Console.WriteLine("✅ 量比异动选股参数已加载");
                    return settings.Filter;
                }

                return GetDefaultFilter();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载量比异动选股参数失败");
                Console.WriteLine($"❌ 加载量比异动选股参数失败: {ex.Message}");
                return GetDefaultFilter();
            }
        }

        /// <summary>
        /// 获取默认筛选参数
        /// </summary>
        private VolumeRatioFilter GetDefaultFilter()
        {
            return new VolumeRatioFilter
            {
                MinMarketCap = 0,
                MaxMarketCap = 1000000000,
                MinVolumeRatio = 0.1m,
                MaxVolumeRatio = 10,
                Min24HVolume = 1000000,
                Max24HVolume = 1000000000,
                MaDistancePercent = 3.0m,
                IsLong = true
            };
        }
    }

    /// <summary>
    /// 量比异动选股设置
    /// </summary>
    public class VolumeRatioSettings
    {
        /// <summary>
        /// 筛选条件
        /// </summary>
        public VolumeRatioFilter Filter { get; set; } = new VolumeRatioFilter();

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; }
    }
}
