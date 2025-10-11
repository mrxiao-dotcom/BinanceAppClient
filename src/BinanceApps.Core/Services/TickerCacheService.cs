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
    /// Ticker数据缓存服务
    /// 用于缓存市场Ticker数据，减少API调用频率和流量消耗
    /// </summary>
    public class TickerCacheService
    {
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly ILogger<TickerCacheService> _logger;
        private readonly IConfiguration _configuration;
        
        private List<PriceStatistics>? _cachedTickers;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);
        
        private int _cacheExpirySeconds = 30; // 默认30秒

        public TickerCacheService(
            IBinanceSimulatedApiClient apiClient,
            ILogger<TickerCacheService> logger,
            IConfiguration configuration)
        {
            _apiClient = apiClient;
            _logger = logger;
            _configuration = configuration;
            
            // 从配置文件读取缓存有效期
            if (int.TryParse(_configuration["TickerCache:ExpirySeconds"], out int expirySeconds) && expirySeconds > 0)
            {
                _cacheExpirySeconds = expirySeconds;
            }
            
            _logger.LogInformation($"TickerCacheService 初始化完成，缓存有效期：{_cacheExpirySeconds}秒");
        }

        /// <summary>
        /// 获取所有Ticker数据（带缓存）
        /// </summary>
        public async Task<List<PriceStatistics>> GetAllTickersAsync()
        {
            var now = DateTime.Now;
            var cacheAge = (now - _lastUpdateTime).TotalSeconds;

            // 如果缓存有效，直接返回
            if (_cachedTickers != null && cacheAge < _cacheExpirySeconds)
            {
                _logger.LogDebug($"使用缓存的Ticker数据，缓存年龄：{cacheAge:F1}秒");
                return _cachedTickers;
            }

            // 使用锁避免并发更新
            await _updateLock.WaitAsync();
            try
            {
                // 双重检查，可能其他线程已经更新了
                now = DateTime.Now;
                cacheAge = (now - _lastUpdateTime).TotalSeconds;
                
                if (_cachedTickers != null && cacheAge < _cacheExpirySeconds)
                {
                    _logger.LogDebug($"其他线程已更新缓存，使用缓存数据");
                    return _cachedTickers;
                }

                // 从API获取最新数据
                _logger.LogInformation($"缓存已过期（{cacheAge:F1}秒），从API获取最新Ticker数据...");
                var tickers = await _apiClient.GetAllTicksAsync();
                
                if (tickers != null && tickers.Count > 0)
                {
                    _cachedTickers = tickers;
                    _lastUpdateTime = now;
                    _logger.LogInformation($"成功更新Ticker缓存，共{tickers.Count}个合约");
                }
                else
                {
                    _logger.LogWarning("获取Ticker数据失败或数据为空，使用旧缓存");
                }

                return _cachedTickers ?? new List<PriceStatistics>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新Ticker缓存时发生异常");
                // 如果有旧缓存，返回旧缓存
                return _cachedTickers ?? new List<PriceStatistics>();
            }
            finally
            {
                _updateLock.Release();
            }
        }

        /// <summary>
        /// 获取指定合约的Ticker数据
        /// </summary>
        public async Task<PriceStatistics?> GetTickerAsync(string symbol)
        {
            var tickers = await GetAllTickersAsync();
            return tickers.FirstOrDefault(t => t.Symbol == symbol);
        }

        /// <summary>
        /// 强制刷新缓存
        /// </summary>
        public async Task<List<PriceStatistics>> ForceRefreshAsync()
        {
            await _updateLock.WaitAsync();
            try
            {
                _logger.LogInformation("强制刷新Ticker缓存...");
                var tickers = await _apiClient.GetAllTicksAsync();
                
                if (tickers != null && tickers.Count > 0)
                {
                    _cachedTickers = tickers;
                    _lastUpdateTime = DateTime.Now;
                    _logger.LogInformation($"强制刷新成功，共{tickers.Count}个合约");
                }
                
                return _cachedTickers ?? new List<PriceStatistics>();
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
            _cachedTickers = null;
            _lastUpdateTime = DateTime.MinValue;
            _logger.LogInformation("Ticker缓存已清除");
        }

        /// <summary>
        /// 获取缓存状态信息
        /// </summary>
        public (bool IsCached, double AgeSeconds, int Count) GetCacheStatus()
        {
            var age = (DateTime.Now - _lastUpdateTime).TotalSeconds;
            return (
                IsCached: _cachedTickers != null,
                AgeSeconds: age,
                Count: _cachedTickers?.Count ?? 0
            );
        }
    }
}

