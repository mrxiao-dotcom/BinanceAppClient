using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 合约发行量数据模型
    /// </summary>
    public class ContractSupplyData
    {
        /// <summary>
        /// 合约代码 (如 BTCUSDT)
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 基础资产代码 (如 BTC)
        /// </summary>
        public string BaseAsset { get; set; } = string.Empty;
        
        /// <summary>
        /// 流通供应量
        /// </summary>
        public decimal CirculatingSupply { get; set; }
        
        /// <summary>
        /// 总供应量
        /// </summary>
        public decimal TotalSupply { get; set; }
        
        /// <summary>
        /// 最大供应量
        /// </summary>
        public decimal MaxSupply { get; set; }
        
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// 数据来源
        /// </summary>
        public string DataSource { get; set; } = string.Empty;
        
        /// <summary>
        /// 是否有效数据
        /// </summary>
        public bool IsValid => CirculatingSupply > 0 && !string.IsNullOrEmpty(Symbol);
    }
    
    /// <summary>
    /// 市值数据模型
    /// </summary>
    public class MarketCapData
    {
        /// <summary>
        /// 合约代码
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 基础资产代码
        /// </summary>
        public string BaseAsset { get; set; } = string.Empty;
        
        /// <summary>
        /// 当前价格
        /// </summary>
        public decimal CurrentPrice { get; set; }
        
        /// <summary>
        /// 流通供应量
        /// </summary>
        public decimal CirculatingSupply { get; set; }
        
        /// <summary>
        /// 市值 (价格 × 流通供应量)
        /// </summary>
        public decimal MarketCap { get; set; }
        
        /// <summary>
        /// 完全稀释市值 (价格 × 最大供应量)
        /// </summary>
        public decimal FullyDilutedCap { get; set; }
        
        /// <summary>
        /// 市值排名
        /// </summary>
        public int MarketCapRank { get; set; }
        
        /// <summary>
        /// 计算时间
        /// </summary>
        public DateTime CalculatedAt { get; set; }
        
        /// <summary>
        /// 格式化市值显示
        /// </summary>
        public string FormattedMarketCap 
        { 
            get 
            {
                if (MarketCap >= 1_000_000_000)
                    return $"{MarketCap / 1_000_000_000:F2}B";
                else if (MarketCap >= 1_000_000)
                    return $"{MarketCap / 1_000_000:F2}M";
                else if (MarketCap >= 1_000)
                    return $"{MarketCap / 1_000:F2}K";
                else
                    return MarketCap.ToString("F2");
            }
        }
    }
    
    /// <summary>
    /// 发行量数据文件结构
    /// </summary>
    public class SupplyDataFile
    {
        /// <summary>
        /// 文件最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// 数据版本
        /// </summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// 合约发行量数据列表
        /// </summary>
        public List<ContractSupplyData> Contracts { get; set; } = new List<ContractSupplyData>();
        
        /// <summary>
        /// 数据来源说明
        /// </summary>
        public Dictionary<string, string> DataSources { get; set; } = new Dictionary<string, string>();
    }
} 