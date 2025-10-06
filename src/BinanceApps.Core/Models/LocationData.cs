namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 位置数据模型
    /// </summary>
    public class LocationData
    {
        /// <summary>
        /// 合约代码
        /// </summary>
        public string Symbol { get; set; } = "";
        
        /// <summary>
        /// 当前价格
        /// </summary>
        public decimal CurrentPrice { get; set; }
        
        /// <summary>
        /// 位置比例 (close-lowest)/(highest-lowest)
        /// </summary>
        public decimal LocationRatio { get; set; }
        
        /// <summary>
        /// 最高价格
        /// </summary>
        public decimal HighestPrice { get; set; }
        
        /// <summary>
        /// 最低价格
        /// </summary>
        public decimal LowestPrice { get; set; }
        
        /// <summary>
        /// 价格区间 (highest - lowest)
        /// </summary>
        public decimal PriceRange { get; set; }
        
        /// <summary>
        /// 状态描述
        /// </summary>
        public string Status { get; set; } = "";
    }
} 