using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 组合分组信息
    /// </summary>
    public class PortfolioGroup
    {
        /// <summary>
        /// 分组ID（GUID）
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// 分组名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 分组说明
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// 分组颜色（用于UI显示，可选）
        /// </summary>
        public string Color { get; set; } = "#0078D4";
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 排序顺序（数字越小越靠前）
        /// </summary>
        public int SortOrder { get; set; } = 0;
    }
    
    /// <summary>
    /// 分组存储文件
    /// </summary>
    public class PortfolioGroupFile
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
        /// 分组列表
        /// </summary>
        public List<PortfolioGroup> Groups { get; set; } = new List<PortfolioGroup>();
    }
} 