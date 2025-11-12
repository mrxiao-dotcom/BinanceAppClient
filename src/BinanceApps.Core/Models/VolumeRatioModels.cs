using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 量比异动筛选条件
    /// </summary>
    public class VolumeRatioFilter
    {
        /// <summary>
        /// 总市值最小值
        /// </summary>
        public decimal? MinMarketCap { get; set; }

        /// <summary>
        /// 总市值最大值
        /// </summary>
        public decimal? MaxMarketCap { get; set; }

        /// <summary>
        /// 量比最小值
        /// </summary>
        public decimal? MinVolumeRatio { get; set; }

        /// <summary>
        /// 量比最大值
        /// </summary>
        public decimal? MaxVolumeRatio { get; set; }

        /// <summary>
        /// 24H成交额最小值
        /// </summary>
        public decimal? Min24HVolume { get; set; }

        /// <summary>
        /// 24H成交额最大值
        /// </summary>
        public decimal? Max24HVolume { get; set; }

        /// <summary>
        /// 小时均线距离百分比
        /// </summary>
        public decimal MaDistancePercent { get; set; } = 3.0m;

        /// <summary>
        /// 多空选项：true=多头(均线上方)，false=空头(均线下方)
        /// </summary>
        public bool IsLong { get; set; } = true;

        /// <summary>
        /// 均线周期（默认26）
        /// </summary>
        public int MaPeriod { get; set; } = 26;

        /// <summary>
        /// 同侧K线数量（默认10）
        /// </summary>
        public int SameSideCount { get; set; } = 10;
    }

    /// <summary>
    /// 量比异动选股结果
    /// </summary>
    public class VolumeRatioResult
    {
        /// <summary>
        /// 合约名
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 24H涨幅
        /// </summary>
        public decimal PriceChangePercent { get; set; }

        /// <summary>
        /// 24H成交额
        /// </summary>
        public decimal Volume24H { get; set; }

        /// <summary>
        /// 流通市值
        /// </summary>
        public decimal CirculatingMarketCap { get; set; }

        /// <summary>
        /// 总市值
        /// </summary>
        public decimal TotalMarketCap { get; set; }

        /// <summary>
        /// 流通比例
        /// </summary>
        public decimal CirculatingRatio { get; set; }

        /// <summary>
        /// 量比
        /// </summary>
        public decimal VolumeRatio { get; set; }

        /// <summary>
        /// 26小时均线距离百分比
        /// </summary>
        public decimal MaDistancePercent { get; set; }

        /// <summary>
        /// 最新价格
        /// </summary>
        public decimal LastPrice { get; set; }

        /// <summary>
        /// 26小时均线价格
        /// </summary>
        public decimal Ma26Price { get; set; }

        /// <summary>
        /// 流通量
        /// </summary>
        public decimal CirculatingSupply { get; set; }

        /// <summary>
        /// 总供应量
        /// </summary>
        public decimal TotalSupply { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 同侧K线数收（收盘价）
        /// </summary>
        public int SameSideCloseCount { get; set; }

        /// <summary>
        /// 同侧K线数最（最高/最低价）
        /// </summary>
        public int SameSideExtremeCount { get; set; }
    }

}
