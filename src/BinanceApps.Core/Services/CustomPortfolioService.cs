using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 自定义板块服务
    /// </summary>
    public class CustomPortfolioService
    {
        private readonly ILogger<CustomPortfolioService> _logger;
        private readonly string _dataFilePath;
        private CustomPortfolioFile? _portfolioData;
        
        public CustomPortfolioService(ILogger<CustomPortfolioService> logger)
        {
            _logger = logger;
            
            // 使用 AppData 目录
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BinanceApps"
            );
            
            // 确保目录存在
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            _dataFilePath = Path.Combine(appDataPath, "custom_portfolios.json");
            _logger.LogInformation($"自定义板块数据文件: {_dataFilePath}");
        }
        
        /// <summary>
        /// 初始化服务，加载数据
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await LoadDataAsync();
                _logger.LogInformation($"自定义板块数据加载完成，共 {_portfolioData?.Portfolios.Count ?? 0} 个组合");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化自定义板块服务失败");
                _portfolioData = new CustomPortfolioFile();
            }
        }
        
        /// <summary>
        /// 加载数据
        /// </summary>
        private async Task LoadDataAsync()
        {
            if (!File.Exists(_dataFilePath))
            {
                _logger.LogInformation("数据文件不存在，创建新文件");
                _portfolioData = new CustomPortfolioFile();
                await SaveDataAsync();
                return;
            }
            
            try
            {
                var json = await File.ReadAllTextAsync(_dataFilePath);
                _portfolioData = JsonSerializer.Deserialize<CustomPortfolioFile>(json);
                
                if (_portfolioData == null)
                {
                    _portfolioData = new CustomPortfolioFile();
                }
                
                _logger.LogInformation($"加载了 {_portfolioData.Portfolios.Count} 个组合");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载数据文件失败");
                _portfolioData = new CustomPortfolioFile();
            }
        }
        
        /// <summary>
        /// 保存数据
        /// </summary>
        private async Task SaveDataAsync()
        {
            try
            {
                if (_portfolioData == null)
                {
                    _portfolioData = new CustomPortfolioFile();
                }
                
                _portfolioData.LastUpdated = DateTime.UtcNow;
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var json = JsonSerializer.Serialize(_portfolioData, options);
                await File.WriteAllTextAsync(_dataFilePath, json);
                
                _logger.LogInformation("数据保存成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存数据失败");
                throw;
            }
        }
        
        /// <summary>
        /// 获取所有组合
        /// </summary>
        public List<CustomPortfolio> GetAllPortfolios()
        {
            return _portfolioData?.Portfolios ?? new List<CustomPortfolio>();
        }
        
        /// <summary>
        /// 根据ID获取组合
        /// </summary>
        public CustomPortfolio? GetPortfolioById(string id)
        {
            return _portfolioData?.Portfolios.FirstOrDefault(p => p.Id == id);
        }
        
        /// <summary>
        /// 创建组合
        /// </summary>
        public async Task<CustomPortfolio> CreatePortfolioAsync(string name, string description, List<PortfolioSymbol> symbols, string groupName = "")
        {
            try
            {
                var portfolio = new CustomPortfolio
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description,
                    GroupName = groupName,
                    Symbols = symbols ?? new List<PortfolioSymbol>(),
                    CreatedTime = DateTime.UtcNow,
                    LastModifiedTime = DateTime.UtcNow
                };
                
                if (_portfolioData == null)
                {
                    _portfolioData = new CustomPortfolioFile();
                }
                
                _portfolioData.Portfolios.Add(portfolio);
                await SaveDataAsync();
                
                _logger.LogInformation($"创建组合成功: {name}");
                return portfolio;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建组合失败: {name}");
                throw;
            }
        }
        
        /// <summary>
        /// 更新组合
        /// </summary>
        public async Task<bool> UpdatePortfolioAsync(string id, string name, string description, List<PortfolioSymbol> symbols, string groupName = "")
        {
            try
            {
                var portfolio = GetPortfolioById(id);
                if (portfolio == null)
                {
                    _logger.LogWarning($"组合不存在: {id}");
                    return false;
                }
                
                portfolio.Name = name;
                portfolio.Description = description;
                portfolio.GroupName = groupName;
                portfolio.Symbols = symbols ?? new List<PortfolioSymbol>();
                portfolio.LastModifiedTime = DateTime.UtcNow;
                
                await SaveDataAsync();
                
                _logger.LogInformation($"更新组合成功: {name}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新组合失败: {id}");
                throw;
            }
        }
        
        /// <summary>
        /// 删除组合
        /// </summary>
        public async Task<bool> DeletePortfolioAsync(string id)
        {
            try
            {
                if (_portfolioData == null)
                {
                    return false;
                }
                
                var portfolio = GetPortfolioById(id);
                if (portfolio == null)
                {
                    _logger.LogWarning($"组合不存在: {id}");
                    return false;
                }
                
                _portfolioData.Portfolios.Remove(portfolio);
                await SaveDataAsync();
                
                _logger.LogInformation($"删除组合成功: {portfolio.Name}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除组合失败: {id}");
                throw;
            }
        }
        
        /// <summary>
        /// 获取数据文件路径（用于诊断）
        /// </summary>
        public string GetDataFilePath()
        {
            return _dataFilePath;
        }
        
        /// <summary>
        /// 导出所有组合到JSON文件
        /// </summary>
        public async Task<bool> ExportToFileAsync(string filePath)
        {
            try
            {
                if (_portfolioData == null)
                {
                    _logger.LogWarning("没有数据可以导出");
                    return false;
                }
                
                // 创建导出数据
                var exportData = new CustomPortfolioFile
                {
                    Version = _portfolioData.Version,
                    LastUpdated = DateTime.UtcNow,
                    Portfolios = _portfolioData.Portfolios
                };
                
                // 序列化为JSON（格式化输出）
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var json = JsonSerializer.Serialize(exportData, options);
                await File.WriteAllTextAsync(filePath, json);
                
                _logger.LogInformation($"成功导出 {exportData.Portfolios.Count} 个组合到: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"导出组合失败: {filePath}");
                return false;
            }
        }
        
        /// <summary>
        /// 从JSON文件导入组合（覆盖现有数据）
        /// </summary>
        public async Task<bool> ImportFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning($"导入文件不存在: {filePath}");
                    return false;
                }
                
                // 读取并反序列化JSON
                var json = await File.ReadAllTextAsync(filePath);
                var importData = JsonSerializer.Deserialize<CustomPortfolioFile>(json);
                
                if (importData == null || importData.Portfolios == null)
                {
                    _logger.LogWarning("导入文件格式无效");
                    return false;
                }
                
                // 备份当前数据
                var backupPath = _dataFilePath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                if (File.Exists(_dataFilePath))
                {
                    File.Copy(_dataFilePath, backupPath, true);
                    _logger.LogInformation($"已创建数据备份: {backupPath}");
                }
                
                // 覆盖现有数据
                _portfolioData = importData;
                _portfolioData.LastUpdated = DateTime.UtcNow;
                
                // 保存到本地
                await SaveDataAsync();
                
                _logger.LogInformation($"成功导入 {importData.Portfolios.Count} 个组合，已创建备份");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"导入组合失败: {filePath}");
                return false;
            }
        }
        
        /// <summary>
        /// 获取当前组合数据的统计信息
        /// </summary>
        public (int portfolioCount, int totalSymbols, List<string> groups) GetStatistics()
        {
            if (_portfolioData == null || _portfolioData.Portfolios == null)
            {
                return (0, 0, new List<string>());
            }
            
            var portfolioCount = _portfolioData.Portfolios.Count;
            var totalSymbols = _portfolioData.Portfolios.Sum(p => p.SymbolCount);
            var groups = _portfolioData.Portfolios
                .Select(p => p.GroupName)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct()
                .OrderBy(g => g)
                .ToList();
            
            return (portfolioCount, totalSymbols, groups);
        }
    }
} 