namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 合约信息（从自定义API获取）
    /// </summary>
    public class ContractInfo
    {
        /// <summary>
        /// 合约名称（如 BTC）
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 合约地址
        /// </summary>
        public string? ContractAddress { get; set; }
        
        /// <summary>
        /// 符号/全称（如 Bitcoin）
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 发行总量
        /// </summary>
        public decimal TotalSupply { get; set; }
        
        /// <summary>
        /// 流通数量
        /// </summary>
        public decimal CirculatingSupply { get; set; }
        
        /// <summary>
        /// 描述
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// 备注（从API获取）
        /// </summary>
        public string? Remark { get; set; }
        
        /// <summary>
        /// 小数位数
        /// </summary>
        public int Decimals { get; set; }
    }
    
    /// <summary>
    /// API响应包装
    /// </summary>
    public class ContractApiResponse
    {
        public bool Success { get; set; }
        public ContractInfo? Data { get; set; }
    }
    
    /// <summary>
    /// API响应列表包装
    /// </summary>
    public class ContractListApiResponse
    {
        public bool Success { get; set; }
        public List<ContractInfo>? Data { get; set; }
    }
} 