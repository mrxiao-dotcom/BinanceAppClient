using System.Collections.Generic;
using System.Threading.Tasks;
using BinanceApps.Core.Models;

namespace BinanceApps.Core.Interfaces
{
    /// <summary>
    /// 量比异动选股服务接口
    /// </summary>
    public interface IVolumeRatioService
    {
        /// <summary>
        /// 执行量比异动选股
        /// </summary>
        Task<List<VolumeRatioResult>> SearchVolumeRatioAsync(VolumeRatioFilter filter);

        /// <summary>
        /// 获取合约的26小时均线数据
        /// </summary>
        Task<decimal?> Get26HourMaAsync(string symbol);

        /// <summary>
        /// 计算量比
        /// </summary>
        decimal? CalculateVolumeRatio(string symbol, decimal price24H, decimal circulatingSupply);
    }
}
