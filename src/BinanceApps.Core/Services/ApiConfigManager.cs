using System;
using System.IO;
using System.Text.Json;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// API配置管理器
    /// 统一处理API Key的读取、验证和缓存
    /// </summary>
    public class ApiConfigManager
    {
        private string? _cachedApiKey;
        private string? _cachedSecretKey;
        private bool? _cachedIsTestnet;
        private DateTime _lastConfigRead = DateTime.MinValue;
        private readonly TimeSpan _configCacheTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 获取当前有效的API配置
        /// </summary>
        /// <returns>API配置信息</returns>
        public ApiConfig GetCurrentConfig()
        {
            // 如果缓存过期或未缓存，重新读取配置
            if (DateTime.Now - _lastConfigRead > _configCacheTimeout || _cachedApiKey == null)
            {
                RefreshConfig();
            }

            return new ApiConfig
            {
                ApiKey = _cachedApiKey ?? "",
                SecretKey = _cachedSecretKey ?? "",
                IsTestnet = _cachedIsTestnet ?? false,
                IsValid = IsValidConfig(_cachedApiKey, _cachedSecretKey)
            };
        }

        /// <summary>
        /// 强制刷新配置缓存
        /// </summary>
        public void RefreshConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"❌ 配置文件不存在: {configPath}");
                    return;
                }

                var jsonContent = File.ReadAllText(configPath);
                var configDoc = JsonDocument.Parse(jsonContent);

                if (configDoc.RootElement.TryGetProperty("BinanceApi", out var binanceApi))
                {
                    _cachedApiKey = binanceApi.TryGetProperty("ApiKey", out var apiKeyElement) ? apiKeyElement.GetString() ?? "" : "";
                    _cachedSecretKey = binanceApi.TryGetProperty("SecretKey", out var secretKeyElement) ? secretKeyElement.GetString() ?? "" : "";
                    _cachedIsTestnet = binanceApi.TryGetProperty("IsTestnet", out var isTestnetElement) ? isTestnetElement.GetBoolean() : false;
                }
                else
                {
                    _cachedApiKey = "";
                    _cachedSecretKey = "";
                    _cachedIsTestnet = false;
                }

                _lastConfigRead = DateTime.Now;

                Console.WriteLine($"🔄 API配置已刷新 - API Key有效: {IsValidApiKey(_cachedApiKey)}, Secret Key有效: {IsValidSecretKey(_cachedSecretKey)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 刷新API配置失败: {ex.Message}");
                // 保持现有缓存
            }
        }

        /// <summary>
        /// 验证API配置是否有效
        /// </summary>
        /// <param name="apiKey">API Key</param>
        /// <param name="secretKey">Secret Key</param>
        /// <returns>是否有效</returns>
        public static bool IsValidConfig(string? apiKey, string? secretKey)
        {
            return IsValidApiKey(apiKey) && IsValidSecretKey(secretKey);
        }

        /// <summary>
        /// 验证API Key是否有效
        /// </summary>
        /// <param name="apiKey">API Key</param>
        /// <returns>是否有效</returns>
        public static bool IsValidApiKey(string? apiKey)
        {
            return !string.IsNullOrEmpty(apiKey) && 
                   !apiKey.Contains("YOUR_") && 
                   !apiKey.Contains("INVALID_") &&
                   apiKey.Length >= 20;
        }

        /// <summary>
        /// 验证Secret Key是否有效
        /// </summary>
        /// <param name="secretKey">Secret Key</param>
        /// <returns>是否有效</returns>
        public static bool IsValidSecretKey(string? secretKey)
        {
            return !string.IsNullOrEmpty(secretKey) && 
                   !secretKey.Contains("YOUR_") && 
                   !secretKey.Contains("INVALID_") &&
                   secretKey.Length >= 20;
        }

        /// <summary>
        /// 清除配置缓存
        /// </summary>
        public void ClearCache()
        {
            _cachedApiKey = null;
            _cachedSecretKey = null;
            _cachedIsTestnet = null;
            _lastConfigRead = DateTime.MinValue;
            Console.WriteLine("🗑️ API配置缓存已清除");
        }
    }

    /// <summary>
    /// API配置信息
    /// </summary>
    public class ApiConfig
    {
        public string ApiKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
        public bool IsTestnet { get; set; }
        public bool IsValid { get; set; }
    }
} 