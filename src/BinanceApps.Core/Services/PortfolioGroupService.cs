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
    /// 组合分组管理服务
    /// </summary>
    public class PortfolioGroupService
    {
        private readonly ILogger<PortfolioGroupService> _logger;
        private readonly string _dataFilePath;
        private PortfolioGroupFile? _groupData;
        
        public PortfolioGroupService(ILogger<PortfolioGroupService> logger)
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
            
            _dataFilePath = Path.Combine(appDataPath, "portfolio_groups.json");
            _logger.LogInformation($"分组数据文件: {_dataFilePath}");
        }
        
        /// <summary>
        /// 初始化服务，加载数据
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await LoadDataAsync();
                _logger.LogInformation($"分组数据加载完成，共 {_groupData?.Groups.Count ?? 0} 个分组");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化分组服务失败");
                _groupData = new PortfolioGroupFile();
            }
        }
        
        /// <summary>
        /// 加载数据
        /// </summary>
        private async Task LoadDataAsync()
        {
            if (!File.Exists(_dataFilePath))
            {
                _logger.LogInformation("分组数据文件不存在，创建新文件");
                _groupData = new PortfolioGroupFile();
                await SaveDataAsync();
                return;
            }
            
            try
            {
                var json = await File.ReadAllTextAsync(_dataFilePath);
                _groupData = JsonSerializer.Deserialize<PortfolioGroupFile>(json);
                
                if (_groupData == null)
                {
                    _groupData = new PortfolioGroupFile();
                }
                
                _logger.LogInformation($"加载了 {_groupData.Groups.Count} 个分组");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载分组数据文件失败");
                _groupData = new PortfolioGroupFile();
            }
        }
        
        /// <summary>
        /// 保存数据
        /// </summary>
        private async Task SaveDataAsync()
        {
            try
            {
                if (_groupData == null)
                {
                    _groupData = new PortfolioGroupFile();
                }
                
                _groupData.LastUpdated = DateTime.UtcNow;
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var json = JsonSerializer.Serialize(_groupData, options);
                await File.WriteAllTextAsync(_dataFilePath, json);
                
                _logger.LogInformation("分组数据保存成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存分组数据失败");
                throw;
            }
        }
        
        /// <summary>
        /// 获取所有分组
        /// </summary>
        public List<PortfolioGroup> GetAllGroups()
        {
            return _groupData?.Groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToList() 
                ?? new List<PortfolioGroup>();
        }
        
        /// <summary>
        /// 根据ID获取分组
        /// </summary>
        public PortfolioGroup? GetGroupById(string id)
        {
            return _groupData?.Groups.FirstOrDefault(g => g.Id == id);
        }
        
        /// <summary>
        /// 根据名称获取分组
        /// </summary>
        public PortfolioGroup? GetGroupByName(string name)
        {
            return _groupData?.Groups.FirstOrDefault(g => g.Name == name);
        }
        
        /// <summary>
        /// 创建分组
        /// </summary>
        public async Task<PortfolioGroup> CreateGroupAsync(string name, string description = "", string color = "#0078D4")
        {
            try
            {
                // 检查名称是否已存在
                if (_groupData?.Groups.Any(g => g.Name == name) == true)
                {
                    throw new Exception($"分组名称 '{name}' 已存在");
                }
                
                var group = new PortfolioGroup
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description,
                    Color = color,
                    CreatedTime = DateTime.UtcNow,
                    SortOrder = (_groupData?.Groups.Count ?? 0) * 10
                };
                
                if (_groupData == null)
                {
                    _groupData = new PortfolioGroupFile();
                }
                
                _groupData.Groups.Add(group);
                await SaveDataAsync();
                
                _logger.LogInformation($"创建分组成功: {name}");
                return group;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建分组失败: {name}");
                throw;
            }
        }
        
        /// <summary>
        /// 更新分组
        /// </summary>
        public async Task<bool> UpdateGroupAsync(string id, string name, string description = "", string color = "#0078D4")
        {
            try
            {
                var group = GetGroupById(id);
                if (group == null)
                {
                    _logger.LogWarning($"分组不存在: {id}");
                    return false;
                }
                
                // 检查新名称是否与其他分组重复
                if (_groupData?.Groups.Any(g => g.Id != id && g.Name == name) == true)
                {
                    throw new Exception($"分组名称 '{name}' 已存在");
                }
                
                group.Name = name;
                group.Description = description;
                group.Color = color;
                
                await SaveDataAsync();
                
                _logger.LogInformation($"更新分组成功: {name}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新分组失败: {id}");
                throw;
            }
        }
        
        /// <summary>
        /// 删除分组
        /// </summary>
        public async Task<bool> DeleteGroupAsync(string id)
        {
            try
            {
                if (_groupData == null)
                {
                    return false;
                }
                
                var group = GetGroupById(id);
                if (group == null)
                {
                    _logger.LogWarning($"分组不存在: {id}");
                    return false;
                }
                
                _groupData.Groups.Remove(group);
                await SaveDataAsync();
                
                _logger.LogInformation($"删除分组成功: {group.Name}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除分组失败: {id}");
                throw;
            }
        }
        
        /// <summary>
        /// 调整分组顺序
        /// </summary>
        public async Task<bool> ReorderGroupsAsync(List<string> groupIds)
        {
            try
            {
                if (_groupData == null)
                {
                    return false;
                }
                
                for (int i = 0; i < groupIds.Count; i++)
                {
                    var group = GetGroupById(groupIds[i]);
                    if (group != null)
                    {
                        group.SortOrder = i * 10;
                    }
                }
                
                await SaveDataAsync();
                _logger.LogInformation("分组顺序调整成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调整分组顺序失败");
                throw;
            }
        }
    }
} 