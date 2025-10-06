using Microsoft.Extensions.DependencyInjection;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Services;

namespace BinanceApps.Core.Extensions
{
    /// <summary>
    /// 模拟服务集合扩展方法
    /// </summary>
    public static class SimulatedServiceCollectionExtensions
    {
        /// <summary>
        /// 添加币安应用模拟服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddBinanceAppsSimulated(this IServiceCollection services)
        {
            // 注册模拟数据管理器
            services.AddSingleton<SimulatedDataManager>();
            
            // 注册模拟API客户端
            services.AddSingleton<IBinanceSimulatedApiClient, BinanceSimulatedApiClient>();
            
            return services;
        }

        /// <summary>
        /// 添加币安应用模拟服务（替换真实服务）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddBinanceAppsSimulatedOnly(this IServiceCollection services)
        {
            // 注册模拟数据管理器
            services.AddSingleton<SimulatedDataManager>();
            
            // 注册模拟API客户端作为基础API客户端
            services.AddSingleton<IBinanceApiClient, BinanceSimulatedApiClient>();
            services.AddSingleton<IBinanceSimulatedApiClient, BinanceSimulatedApiClient>();
            
            return services;
        }
    }
} 