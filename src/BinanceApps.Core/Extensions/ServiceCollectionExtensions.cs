using Microsoft.Extensions.DependencyInjection;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Services;

namespace BinanceApps.Core.Extensions
{
    /// <summary>
    /// 服务集合扩展方法
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加币安应用核心服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddBinanceAppsCore(this IServiceCollection services)
        {
            // 注册核心服务
            services.AddSingleton<IBinanceApiClient, BinanceApiClient>();
            return services;
        }

        /// <summary>
        /// 添加币安应用核心服务（包含模拟服务）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="useSimulated">是否使用模拟服务</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddBinanceAppsCore(this IServiceCollection services, bool useSimulated)
        {
            if (useSimulated)
            {
                // 注册模拟服务
                services.AddSingleton<SimulatedDataManager>();
                services.AddSingleton<IBinanceApiClient, BinanceSimulatedApiClient>();
                services.AddSingleton<IBinanceSimulatedApiClient, BinanceSimulatedApiClient>();
            }
            else
            {
                // 注册真实服务
                services.AddSingleton<IBinanceApiClient, BinanceApiClient>();
            }
            
            return services;
        }
    }
} 