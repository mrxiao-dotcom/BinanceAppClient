using System;
using System.Collections.Generic;
using System.Linq;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 自定义板块组合
    /// </summary>
    public class CustomPortfolio
    {
        /// <summary>
        /// 组合ID（GUID）
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// 组合名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 组合说明
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// 分组名称（如：经典组、次新组、黑马组）
        /// </summary>
        public string GroupName { get; set; } = string.Empty;
        
        /// <summary>
        /// 组合内合约列表
        /// </summary>
        public List<PortfolioSymbol> Symbols { get; set; } = new List<PortfolioSymbol>();
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModifiedTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 获取成分数量
        /// </summary>
        public int SymbolCount => Symbols?.Count ?? 0;
        
        /// <summary>
        /// 显示文本
        /// </summary>
        public string DisplayText => $"{Name} ({SymbolCount}个)";
    }
    
    /// <summary>
    /// 组合内的合约信息
    /// </summary>
    public class PortfolioSymbol
    {
        /// <summary>
        /// 合约代码（如：BTCUSDT）
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 合约备注
        /// </summary>
        public string Remark { get; set; } = string.Empty;
        
        /// <summary>
        /// 添加时间
        /// </summary>
        public DateTime AddedTime { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// 组合运行时数据（包含实时行情）
    /// </summary>
    public class PortfolioRuntimeData
    {
        /// <summary>
        /// 组合信息
        /// </summary>
        public CustomPortfolio Portfolio { get; set; } = new CustomPortfolio();
        
        /// <summary>
        /// 组合涨幅（24H，算数平均）
        /// </summary>
        public decimal AveragePriceChangePercent { get; set; }
        
        /// <summary>
        /// 组合30天涨幅（算数平均）
        /// </summary>
        public decimal AveragePriceChangePercent30d { get; set; }
        
        /// <summary>
        /// 合约实时数据列表
        /// </summary>
        public List<PortfolioSymbolData> SymbolsData { get; set; } = new List<PortfolioSymbolData>();
        
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; }
    }
    
    /// <summary>
    /// 合约实时数据
    /// </summary>
    public class PortfolioSymbolData
    {
        /// <summary>
        /// 合约代码
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; } = string.Empty;
        
        /// <summary>
        /// 24小时涨跌幅
        /// </summary>
        public decimal PriceChangePercent { get; set; }
        
        /// <summary>
        /// 当前价格
        /// </summary>
        public decimal LastPrice { get; set; }
        
        /// <summary>
        /// 24小时成交额（USDT）
        /// </summary>
        public decimal QuoteVolume { get; set; }
        
        /// <summary>
        /// 30天最高价
        /// </summary>
        public decimal HighPrice30d { get; set; }
        
        /// <summary>
        /// 30天最低价
        /// </summary>
        public decimal LowPrice30d { get; set; }
        
        /// <summary>
        /// 30天涨幅（当前价相对30天最低价的涨幅）
        /// </summary>
        public decimal PriceChangePercent30d { get; set; }
        
        /// <summary>
        /// 流通市值（流通数量 × 当前价格）
        /// </summary>
        public decimal CirculatingMarketCap { get; set; }
        
        /// <summary>
        /// 24H量比（24H成交额 ÷ 流通市值）
        /// </summary>
        public decimal VolumeRatio { get; set; }
        
        /// <summary>
        /// 合约备注（从API获取）
        /// </summary>
        public string ContractRemark { get; set; } = string.Empty;
        
        /// <summary>
        /// 涨跌幅显示文本
        /// </summary>
        public string PriceChangeDisplay => PriceChangePercent >= 0 
            ? $"+{PriceChangePercent:F2}%" 
            : $"{PriceChangePercent:F2}%";
        
        /// <summary>
        /// 价格显示文本
        /// </summary>
        public string PriceDisplay => $"${LastPrice:F4}";
        
        /// <summary>
        /// 成交额显示文本
        /// </summary>
        public string VolumeDisplay
        {
            get
            {
                if (QuoteVolume >= 1_000_000_000)
                    return $"${QuoteVolume / 1_000_000_000:F2}B";
                if (QuoteVolume >= 1_000_000)
                    return $"${QuoteVolume / 1_000_000:F2}M";
                if (QuoteVolume >= 1_000)
                    return $"${QuoteVolume / 1_000:F2}K";
                return $"${QuoteVolume:F2}";
            }
        }
        
        /// <summary>
        /// 30天涨幅显示文本
        /// </summary>
        public string PriceChange30dDisplay => PriceChangePercent30d >= 0 
            ? $"+{PriceChangePercent30d:F2}%" 
            : $"{PriceChangePercent30d:F2}%";
        
        /// <summary>
        /// 30天最高价显示文本
        /// </summary>
        public string HighPrice30dDisplay => HighPrice30d > 0 ? $"${HighPrice30d:F4}" : "-";
        
        /// <summary>
        /// 30天最低价显示文本
        /// </summary>
        public string LowPrice30dDisplay => LowPrice30d > 0 ? $"${LowPrice30d:F4}" : "-";
        
        /// <summary>
        /// 流通市值显示文本
        /// </summary>
        public string CirculatingMarketCapDisplay
        {
            get
            {
                if (CirculatingMarketCap <= 0)
                    return "-";
                
                if (CirculatingMarketCap >= 1_000_000_000)
                    return $"${CirculatingMarketCap / 1_000_000_000:F2}B";
                if (CirculatingMarketCap >= 1_000_000)
                    return $"${CirculatingMarketCap / 1_000_000:F1}M";
                if (CirculatingMarketCap >= 1_000)
                    return $"${CirculatingMarketCap / 1_000:F0}K";
                return $"${CirculatingMarketCap:F2}";
            }
        }
        
        /// <summary>
        /// 量比显示文本（百分比格式）
        /// </summary>
        public string VolumeRatioDisplay
        {
            get
            {
                if (VolumeRatio <= 0)
                    return "-";
                
                // 转换为百分比
                var percentValue = VolumeRatio * 100;
                
                if (percentValue >= 10)
                    return $"{percentValue:F1}%";
                if (percentValue >= 1)
                    return $"{percentValue:F2}%";
                return $"{percentValue:F3}%";
            }
        }
    }
    
    /// <summary>
    /// 自定义板块存储文件
    /// </summary>
    public class CustomPortfolioFile
    {
        /// <summary>
        /// 文件版本
        /// </summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 组合列表
        /// </summary>
        public List<CustomPortfolio> Portfolios { get; set; } = new List<CustomPortfolio>();
    }
} 