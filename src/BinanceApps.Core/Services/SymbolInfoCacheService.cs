using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 合约信息缓存服务
    /// 用于缓存合约信息数据，减少API调用频率
    /// </summary>
    public class SymbolInfoCacheService
    {
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly ILogger<SymbolInfoCacheService> _logger;
        private readonly IConfiguration _configuration;
        
        private List<SymbolInfo>? _cachedSymbolInfos;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);
        
        private int _cacheExpirySeconds = 300; // 默认5分钟，合约信息变化不频繁

        public SymbolInfoCacheService(
            IBinanceSimulatedApiClient apiClient,
            ILogger<SymbolInfoCacheService> logger,
            IConfiguration configuration)
        {
            _apiClient = apiClient;
            _logger = logger;
            _configuration = configuration;
            
            // 从配置文件读取缓存有效期
            if (int.TryParse(_configuration["SymbolInfoCache:ExpirySeconds"], out int expirySeconds) && expirySeconds > 0)
            {
                _cacheExpirySeconds = expirySeconds;
            }
            
            _logger.LogInformation($"SymbolInfoCacheService 初始化完成，缓存有效期：{_cacheExpirySeconds}秒");
        }

        /// <summary>
        /// 获取所有合约信息（带缓存）
        /// </summary>
        public async Task<List<SymbolInfo>> GetAllSymbolsInfoAsync()
        {
            var now = DateTime.Now;
            var cacheAge = (now - _lastUpdateTime).TotalSeconds;

            // 如果缓存有效，直接返回
            if (_cachedSymbolInfos != null && cacheAge < _cacheExpirySeconds)
            {
                _logger.LogDebug($"使用缓存的合约信息数据，缓存年龄：{cacheAge:F1}秒");
                return _cachedSymbolInfos;
            }

            // 使用锁避免并发更新
            await _updateLock.WaitAsync();
            try
            {
                // 双重检查
                now = DateTime.Now;
                cacheAge = (now - _lastUpdateTime).TotalSeconds;
                
                if (_cachedSymbolInfos != null && cacheAge < _cacheExpirySeconds)
                {
                    _logger.LogDebug($"其他线程已更新缓存，使用缓存数据");
                    return _cachedSymbolInfos;
                }

                // 从API获取最新数据
                _logger.LogInformation($"缓存已过期（{cacheAge:F1}秒），从API获取最新合约信息...");
                var symbolInfos = await _apiClient.GetAllSymbolsInfoAsync();
                
                if (symbolInfos != null && symbolInfos.Count > 0)
                {
                    _cachedSymbolInfos = symbolInfos;
                    _lastUpdateTime = now;
                    _logger.LogInformation($"成功更新合约信息缓存，共{symbolInfos.Count}个合约");
                }
                else
                {
                    _logger.LogWarning("获取合约信息失败或数据为空，使用旧缓存");
                }

                return _cachedSymbolInfos ?? new List<SymbolInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新合约信息缓存时发生异常");
                return _cachedSymbolInfos ?? new List<SymbolInfo>();
            }
            finally
            {
                _updateLock.Release();
            }
        }

        /// <summary>
        /// 获取指定合约的信息
        /// </summary>
        public async Task<SymbolInfo?> GetSymbolInfoAsync(string symbol)
        {
            var symbolInfos = await GetAllSymbolsInfoAsync();
            return symbolInfos.FirstOrDefault(s => s.Symbol == symbol);
        }

        /// <summary>
        /// 强制刷新缓存
        /// </summary>
        public async Task<List<SymbolInfo>> ForceRefreshAsync()
        {
            await _updateLock.WaitAsync();
            try
            {
                _logger.LogInformation("强制刷新合约信息缓存...");
                var symbolInfos = await _apiClient.GetAllSymbolsInfoAsync();
                
                if (symbolInfos != null && symbolInfos.Count > 0)
                {
                    _cachedSymbolInfos = symbolInfos;
                    _lastUpdateTime = DateTime.Now;
                    _logger.LogInformation($"强制刷新成功，共{symbolInfos.Count}个合约");
                }
                
                return _cachedSymbolInfos ?? new List<SymbolInfo>();
            }
            finally
            {
                _updateLock.Release();
            }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _cachedSymbolInfos = null;
            _lastUpdateTime = DateTime.MinValue;
            _logger.LogInformation("合约信息缓存已清除");
        }

        /// <summary>
        /// 获取缓存状态信息
        /// </summary>
        public (bool IsCached, double AgeSeconds, int Count) GetCacheStatus()
        {
            var age = (DateTime.Now - _lastUpdateTime).TotalSeconds;
            return (
                IsCached: _cachedSymbolInfos != null,
                AgeSeconds: age,
                Count: _cachedSymbolInfos?.Count ?? 0
            );
        }
    }
}

