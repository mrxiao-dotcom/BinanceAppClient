using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Shapes;
using System.Windows.Media.Effects;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using BinanceApps.Core.Extensions;
using RegisterSrv.ClientSDK;

namespace BinanceApps.WPF
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private List<SymbolInfo> _allSymbols = new List<SymbolInfo>();
        private List<PriceStatistics> _allTicks = new List<PriceStatistics>();
        
        // 排序状态变量
        private string _currentSortColumn = "";
        private bool _isAscending = false;
        private List<VolumeGrowthDisplayItem> _currentVolumeData = new List<VolumeGrowthDisplayItem>();
        private ListView? _volumeListView;
        
        // 翻页相关变量
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;
        
        // 日志窗口
        private LogWindow? _logWindow;
        
        // 选币工具相关数据
        private List<HighLowData> _highLowData = new();
        private List<LocationData> _locationData = new();
        private List<Kline> _allKlineData = new(); // 所有K线数据缓存
        private List<ContractAnalysis> _contractAnalysis = new(); // 合约分析结果缓存
        private CancellationTokenSource? _calculationCancellationTokenSource;
        private CancellationTokenSource? _fetchCancellationTokenSource;
        private KlineDataStorageService _klineStorageService;
        private BinanceApps.Core.Services.MarketMonitorService? _marketMonitorService;
        private BinanceApps.Core.Services.SupplyDataService? _supplyDataService;
        private BinanceApps.Core.Services.MarketPositionService? _marketPositionService;
        private BinanceApps.Core.Services.CustomPortfolioService? _customPortfolioService;
        private BinanceApps.Core.Services.MaDistanceService? _maDistanceService;
        private BinanceApps.Core.Services.ContractInfoService? _contractInfoService;
        private BinanceApps.Core.Services.DashboardService? _dashboardService;
        private BinanceApps.Core.Services.MarketDistributionService? _marketDistributionService;
        private BinanceApps.Core.Services.HotspotTrackingService? _hotspotTrackingService;
        private BinanceApps.Core.Services.GainerTrackingService? _gainerTrackingService;
        private BinanceApps.Core.Services.LoserTrackingService? _loserTrackingService;
        
        // 涨速排行榜相关
        private System.Threading.Timer? _priceSpeedTimer;
        private System.Threading.Timer? _dailyResetTimer;
        private readonly Dictionary<string, List<decimal>> _priceHistory = new();
        private readonly Dictionary<string, int> _riseRankingCount = new();
        private readonly Dictionary<string, int> _fallRankingCount = new();
        private int _intervalSeconds = 5;
        private volatile bool _isPriceSpeedRunning = false;
        private DateTime _lastResetDate = DateTime.Today;
        
        // 高级筛选缓存
        private List<Market24HData>? _cached24HData;
        private DateTime _last24HDataUpdate = DateTime.MinValue;
        private readonly Dictionary<string, Dictionary<int, decimal>> _amplitudeCache = new(); // Symbol -> {Days -> Amplitude}
        
        // 高低价分析天数配置
        private int _highLowAnalysisDays = 20;
        
        // 振幅分析天数配置
        private int _amplitudeAnalysisDays = 30;
        
        // 高级筛选配置
        private decimal _advancedFilterMinPosition = 80;
        private decimal _advancedFilterMaxPosition = 100;
        private int _advancedFilterAmplitudeDays = 30;
        private decimal _advancedFilterMinAmplitude = 0;
        private decimal _advancedFilterMaxAmplitude = 30;
        private decimal _advancedFilterMinVolume = 1000;
        private decimal _advancedFilterMinMarketCap = 0;
        private decimal _advancedFilterMaxMarketCap = 0;

        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化K线数据存储服务
            _klineStorageService = new KlineDataStorageService();
            
            // 打印K线数据保存目录
            var klineDataPath = System.IO.Path.GetFullPath("KlineData");
            Console.WriteLine($"📁 K线数据保存目录: {klineDataPath}");
            System.Diagnostics.Debug.WriteLine($"📁 K线数据保存目录: {klineDataPath}");
            
            // 初始化依赖注入
            _serviceProvider = CreateServiceProvider();
            _apiClient = _serviceProvider.GetRequiredService<IBinanceSimulatedApiClient>();
            _marketMonitorService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.MarketMonitorService>();
            _supplyDataService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.SupplyDataService>();
            _marketPositionService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.MarketPositionService>();
            _customPortfolioService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.CustomPortfolioService>();
            _maDistanceService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.MaDistanceService>();
            _contractInfoService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.ContractInfoService>();
            _dashboardService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.DashboardService>();
            _marketDistributionService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.MarketDistributionService>();
            _hotspotTrackingService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.HotspotTrackingService>();
            _gainerTrackingService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.GainerTrackingService>();
            _loserTrackingService = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.LoserTrackingService>();
            
            // 初始化自定义板块服务（异步初始化会在 InitializeAsync 中完成）
            
            // 初始化日志窗口
            try
            {
                _logWindow = new LogWindow();
                _logWindow?.AddLog("应用程序启动", LogType.Info);
            }
            catch (Exception ex)
            {
                // 如果日志窗口初始化失败，记录到控制台
                System.Diagnostics.Debug.WriteLine($"日志窗口初始化失败: {ex.Message}");
            }
            
            // 注册窗口关闭事件
            this.Closing += MainWindow_Closing;
            
            // 使用Dispatcher.BeginInvoke来避免构造函数中的异步调用问题
            Dispatcher.BeginInvoke(async () => await InitializeAsync());
        }

        private IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            
            // 添加配置
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            services.AddSingleton<IConfiguration>(configuration);
            
            // 添加API配置管理器
            services.AddSingleton<BinanceApps.Core.Services.ApiConfigManager>();
            
            // 使用工厂模式创建API客户端，确保每次都使用最新配置
            services.AddSingleton<BinanceRealApiClient>(provider => 
            {
                var configManager = provider.GetRequiredService<BinanceApps.Core.Services.ApiConfigManager>();
                var config = configManager.GetCurrentConfig();
                
                if (!config.IsValid)
                {
                    Console.WriteLine("⚠️ API配置无效，创建占位符API客户端");
                    // 创建占位符客户端，避免崩溃
                    return new BinanceRealApiClient("INVALID", "INVALID", false);
                }
                
                Console.WriteLine($"🔧 使用最新配置创建API客户端 - API Key: {config.ApiKey[..Math.Min(8, config.ApiKey.Length)]}...");
                return new BinanceRealApiClient(config.ApiKey, config.SecretKey, config.IsTestnet);
            });
            
            // 将真实API客户端注册为接口实现（强制使用真实API）
            services.AddSingleton<IBinanceSimulatedApiClient>(provider => 
                provider.GetRequiredService<BinanceRealApiClient>());
                
            // 添加模拟数据管理器（仍保留以防某些功能需要）
            services.AddSingleton<SimulatedDataManager>();
            
            // 添加HttpClient
            services.AddHttpClient();
            
            // 添加缓存服务（优先注册，其他服务依赖它们）
            services.AddSingleton<BinanceApps.Core.Services.TickerCacheService>();
            services.AddSingleton<BinanceApps.Core.Services.SymbolInfoCacheService>();
            
            // 添加通知服务
            services.AddSingleton<BinanceApps.Core.Services.NotificationService>();
            services.AddSingleton<BinanceApps.Core.Services.MarketMonitorService>();
            services.AddSingleton<BinanceApps.Core.Services.SupplyDataService>();
            services.AddSingleton<BinanceApps.Core.Services.MarketPositionService>();
            services.AddSingleton<BinanceApps.Core.Services.CustomPortfolioService>();
            services.AddSingleton<BinanceApps.Core.Services.PortfolioGroupService>();
            services.AddSingleton<BinanceApps.Core.Services.KlineDataStorageService>(sp => _klineStorageService);
            
            // 注册ContractInfoService，统一使用LicenseServerUrl（需要在MaDistanceService之前注册）
            services.AddSingleton<BinanceApps.Core.Services.ContractInfoService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<BinanceApps.Core.Services.ContractInfoService>>();
                
                // 优先读取 ContractApiServerUrl，如果不存在则使用 LicenseServerUrl
                var contractApiUrl = System.Configuration.ConfigurationManager.AppSettings["ContractApiServerUrl"];
                var licenseServerUrl = System.Configuration.ConfigurationManager.AppSettings["LicenseServerUrl"];
                
                // 如果 ContractApiServerUrl 未配置，使用 LicenseServerUrl
                if (string.IsNullOrWhiteSpace(contractApiUrl))
                {
                    contractApiUrl = licenseServerUrl;
                    Console.WriteLine($"🔍 ContractApiServerUrl 未配置，使用 LicenseServerUrl: {contractApiUrl ?? "localhost:8080"}");
                }
                else
                {
                    Console.WriteLine($"🔍 使用 ContractApiServerUrl: {contractApiUrl}");
                }
                
                // 如果两者都未配置，使用默认值
                if (string.IsNullOrWhiteSpace(contractApiUrl))
                {
                    contractApiUrl = "http://localhost:8080";
                    Console.WriteLine($"⚠️ LicenseServerUrl 和 ContractApiServerUrl 都未配置，使用默认值: {contractApiUrl}");
                }
                
                Console.WriteLine($"✅ 合约API最终地址: {contractApiUrl}");
                return new BinanceApps.Core.Services.ContractInfoService(logger, contractApiUrl);
            });
            
            // 注册MaDistanceService（依赖ContractInfoService）
            services.AddSingleton<BinanceApps.Core.Services.MaDistanceService>();
            
            // 注册HotspotTrackingService（依赖ContractInfoService）
            services.AddSingleton<BinanceApps.Core.Services.HotspotTrackingService>();
            
            // 注册GainerTrackingService（依赖ContractInfoService）
            services.AddSingleton<BinanceApps.Core.Services.GainerTrackingService>();
            
            // 注册LoserTrackingService（依赖ContractInfoService）
            services.AddSingleton<BinanceApps.Core.Services.LoserTrackingService>();
            
            services.AddSingleton<BinanceApps.Core.Services.DashboardService>();
            services.AddSingleton<BinanceApps.Core.Services.MarketDistributionService>();
            
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 重新初始化API客户端（用于配置更改后）
        /// </summary>
        public async Task ReinitializeApiAsync()
        {
            try
            {
                Console.WriteLine("🔄 重新初始化API客户端...");
                
                // 获取API配置管理器并强制刷新配置
                var configManager = _serviceProvider.GetRequiredService<BinanceApps.Core.Services.ApiConfigManager>();
                configManager.RefreshConfig();
                var config = configManager.GetCurrentConfig();
                
                if (!config.IsValid)
                {
                    Console.WriteLine("❌ API配置无效，无法重新初始化");
                    MessageBox.Show("API配置无效！\n\n请在API设置中配置有效的API Key和Secret Key。", 
                        "配置错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    // 打开API设置窗口
                    var apiSettingsWindow = new ApiSettingsWindow(_serviceProvider);
                    apiSettingsWindow.ShowDialog();
                    return;
                }
                
                // 重新初始化现有的API客户端
                Console.WriteLine("🔧 重新初始化API客户端配置...");
                await _apiClient.InitializeAsync(config.ApiKey, config.SecretKey, config.IsTestnet);
                
                Console.WriteLine($"🔑 使用新配置 - API Key: {config.ApiKey[..Math.Min(8, config.ApiKey.Length)]}...");
                Console.WriteLine($"🌐 使用测试网: {config.IsTestnet}");
                
                // 对于行情数据，跳过API Key验证，直接使用公开API
                Console.WriteLine("📊 使用公开API模式（仅行情数据，无需API Key验证）");
                _logWindow?.AddLog("使用公开API模式（仅行情数据，无需API Key验证）", LogType.Info);
                
                // 直接标记为已连接，因为公开API不需要验证
                var isConnected = true;
                UpdateConnectionStatus(isConnected);
                txtApiKey.Text = "公开API模式（行情数据）";
                
                Console.WriteLine("✅ 公开API重新初始化成功");
                _logWindow?.AddLog("公开API重新初始化成功", LogType.Success);
                
                // 生成真实行情数据（静默执行）
                Console.WriteLine("📊 正在从币安公开API获取真实行情数据...");
                try
                {
                    await GenerateRealData();
                }
                catch (Exception dataEx)
                {
                    Console.WriteLine($"⚠️ 获取行情数据时出现问题: {dataEx.Message}");
                    _logWindow?.AddLog($"获取行情数据时出现问题: {dataEx.Message}", LogType.Warning);
                }
                
                Console.WriteLine("✅ 重新初始化完成");
                _logWindow?.AddLog("API客户端重新初始化完成", LogType.Success);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 重新初始化失败: {ex.Message}");
                _logWindow?.AddLog($"重新初始化失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"重新初始化API失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("🔄 开始初始化应用程序...");
                _logWindow?.AddLog("开始初始化应用程序", LogType.Info);
                
                // 更新许可证状态显示
                await UpdateLicenseStatusAsync();
                
                // 添加时间戳格式化测试
                TimestampTest.TestTimestampFormatting();
                
                // 加载高低价分析配置
                LoadHighLowAnalysisConfig();
                
                // 加载振幅分析配置
                LoadAmplitudeAnalysisConfig();
                
                // 加载高级筛选配置
                LoadAdvancedFilterConfig();
                
                // 初始化发行量数据服务
                await InitializeSupplyDataServiceAsync();
                
                // 加载合约流通量信息缓存
                if (_contractInfoService != null)
                {
                    Console.WriteLine("📊 正在从本地API加载合约流通量信息...");
                    _logWindow?.AddLog("正在加载合约流通量信息", LogType.Info);
                    
                    var success = await _contractInfoService.LoadContractInfoAsync();
                    if (success)
                    {
                        Console.WriteLine($"✅ 成功加载 {_contractInfoService.CachedContractCount} 个合约信息到缓存");
                        _logWindow?.AddLog($"成功加载 {_contractInfoService.CachedContractCount} 个合约信息", LogType.Success);
                    }
                    else
                    {
                        Console.WriteLine("⚠️ 加载合约信息失败或服务器未响应（量比功能将不可用）");
                        _logWindow?.AddLog("合约信息加载失败（量比功能将不可用）", LogType.Warning);
                    }
                }
                
                // 初始化自定义板块服务
                if (_customPortfolioService != null)
                {
                    await _customPortfolioService.InitializeAsync();
                    Console.WriteLine("✅ 自定义板块服务初始化完成");
                    _logWindow?.AddLog("自定义板块服务初始化完成", LogType.Success);
                }
                
                // 从配置文件读取API密钥
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var configPath = System.IO.Path.Combine(baseDirectory, "appsettings.json");
                Console.WriteLine($"📂 主程序配置文件路径: {configPath}");
                Console.WriteLine($"📂 基础目录: {baseDirectory}");
                Console.WriteLine($"📂 配置文件存在: {File.Exists(configPath)}");
                
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(baseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();
                
                var apiKey = configuration.GetValue<string>("BinanceApi:ApiKey") ?? "";
                var secretKey = configuration.GetValue<string>("BinanceApi:SecretKey") ?? "";
                var isTestnet = configuration.GetValue<bool>("BinanceApi:IsTestnet");
                
                // 如果配置文件中的值是默认值或无效值，使用硬编码的值
                bool useHardcodedKeys = false;
                if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR_") || apiKey.Length < 20)
                {
                    Console.WriteLine($"⚠️ 检测到无效的API Key: '{apiKey}'，将使用内置测试Key");
                    apiKey = "wGhXmPqUWGv8GwpoC99xh9cQ57qaegT9F2WxzLpKhXGQ1C6fL5fmB4ThL18tQh4f";
                    useHardcodedKeys = true;
                }
                if (string.IsNullOrEmpty(secretKey) || secretKey.Contains("YOUR_") || secretKey.Length < 20)
                {
                    Console.WriteLine($"⚠️ 检测到无效的Secret Key: '{secretKey[..Math.Min(8, secretKey.Length)]}...'，将使用内置测试Key");
                    secretKey = "BEprJjIa0jcSwJNooZtb84rBTEUFPhzX8cT7YpaMz8w3gU6bNFnkGk5hVhHzofHy";
                    useHardcodedKeys = true;
                }
                
                if (useHardcodedKeys)
                {
                    Console.WriteLine("⚠️ 重要提示：正在使用内置测试账户，这不是您的个人币安账户！");
                    Console.WriteLine("💡 如需使用个人账户，请在API设置中输入您的64位真实API Key");
                    Console.WriteLine("🔑 当前使用测试账户进行数据获取和功能演示");
                }
                
                Console.WriteLine("🔑 正在初始化API客户端...");
                Console.WriteLine($"🔑 使用API Key: {apiKey[..Math.Min(8, apiKey.Length)]}...");
                Console.WriteLine($"🌐 使用测试网: {isTestnet}");
                await _apiClient.InitializeAsync(apiKey, secretKey, isTestnet);
                
                // 对于行情数据，跳过API Key验证，直接使用公开API
                Console.WriteLine("📊 使用公开API模式（仅行情数据，无需API Key验证）");
                _logWindow?.AddLog("使用公开API模式（仅行情数据，无需API Key验证）", LogType.Info);
                
                // 直接标记为已连接，因为公开API不需要验证
                var isConnected = true;
                UpdateConnectionStatus(isConnected);
                txtApiKey.Text = "公开API模式（行情数据）";
                
                Console.WriteLine("✅ 公开API模式初始化成功");
                _logWindow?.AddLog("公开API模式初始化成功", LogType.Success);
                
                // 生成真实行情数据（静默执行）
                Console.WriteLine("📊 正在从币安公开API获取真实行情数据...");
                try
                {
                    await GenerateRealData();
                }
                catch (Exception dataEx)
                {
                    Console.WriteLine($"⚠️ 获取行情数据时出现问题: {dataEx.Message}");
                    _logWindow?.AddLog($"获取行情数据时出现问题: {dataEx.Message}", LogType.Warning);
                }
                
                Console.WriteLine("✅ 应用程序初始化完成");
                _logWindow?.AddLog("应用程序初始化完成", LogType.Success);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 应用程序初始化失败: {ex.Message}");
                _logWindow?.AddLog($"应用程序初始化失败: {ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// 加载高低价分析配置
        /// </summary>
        private void LoadHighLowAnalysisConfig()
        {
            try
            {
                var configuration = _serviceProvider.GetService<IConfiguration>();
                if (configuration != null)
                {
                    _highLowAnalysisDays = configuration.GetValue<int>("HighLowAnalysis:DefaultDays", 20);
                    Console.WriteLine($"📊 加载高低价分析配置: {_highLowAnalysisDays}天");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 加载高低价分析配置失败: {ex.Message}，使用默认值20天");
                _highLowAnalysisDays = 20;
            }
        }

        /// <summary>
        /// 加载振幅分析配置
        /// </summary>
        private void LoadAmplitudeAnalysisConfig()
        {
            try
            {
                var configuration = _serviceProvider.GetService<IConfiguration>();
                if (configuration != null)
                {
                    _amplitudeAnalysisDays = configuration.GetValue<int>("AmplitudeAnalysis:DefaultDays", 30);
                    Console.WriteLine($"📈 加载振幅分析配置: {_amplitudeAnalysisDays}天");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 加载振幅分析配置失败: {ex.Message}，使用默认值30天");
                _amplitudeAnalysisDays = 30;
            }
        }

        /// <summary>
        /// 加载高级筛选配置
        /// </summary>
        private void LoadAdvancedFilterConfig()
        {
            try
            {
                var configuration = _serviceProvider.GetService<IConfiguration>();
                if (configuration != null)
                {
                    _advancedFilterMinPosition = configuration.GetValue<decimal>("AdvancedFilter:MinPosition", 80);
                    _advancedFilterMaxPosition = configuration.GetValue<decimal>("AdvancedFilter:MaxPosition", 100);
                    _advancedFilterAmplitudeDays = configuration.GetValue<int>("AdvancedFilter:AmplitudeDays", 30);
                    _advancedFilterMinAmplitude = configuration.GetValue<decimal>("AdvancedFilter:MinAmplitude", 0);
                    _advancedFilterMaxAmplitude = configuration.GetValue<decimal>("AdvancedFilter:MaxAmplitude", 30);
                    _advancedFilterMinVolume = configuration.GetValue<decimal>("AdvancedFilter:MinVolume", 1000);
                    _advancedFilterMinMarketCap = configuration.GetValue<decimal>("AdvancedFilter:MinMarketCap", 0);
                    _advancedFilterMaxMarketCap = configuration.GetValue<decimal>("AdvancedFilter:MaxMarketCap", 0);
                    Console.WriteLine($"🔍 加载高级筛选配置: 位置{_advancedFilterMinPosition}-{_advancedFilterMaxPosition}%, 振幅{_advancedFilterMinAmplitude}-{_advancedFilterMaxAmplitude}%");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 加载高级筛选配置失败: {ex.Message}，使用默认值");
            }
        }

        /// <summary>
        /// 初始化发行量数据服务
        /// </summary>
        private async Task InitializeSupplyDataServiceAsync()
        {
            try
            {
                var httpClient = _serviceProvider.GetService<HttpClient>() ?? new HttpClient();
                _supplyDataService = new BinanceApps.Core.Services.SupplyDataService(httpClient);
                await _supplyDataService.InitializeAsync();
                
                var (count, lastUpdate) = _supplyDataService.GetCacheStats();
                Console.WriteLine($"💰 发行量数据服务已初始化: {count} 个合约，最后更新: {lastUpdate:yyyy-MM-dd HH:mm}");
                _logWindow?.AddLog($"发行量数据服务已初始化: {count} 个合约", LogType.Info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 初始化发行量数据服务失败: {ex.Message}");
                _logWindow?.AddLog($"初始化发行量数据服务失败: {ex.Message}", LogType.Error);
            }
        }

        private async Task GenerateRealData()
        {
            try
            {
                // 1. 先从本地文件寻找，如果有文件，直接显示文件内容
                var localSymbols = await _apiClient.LoadSymbolsFromFileAsync();
                if (localSymbols.Count > 0)
                {
                    _allSymbols = localSymbols;
                    _allTicks = await _apiClient.GetAllTicksAsync();
                    return;
                }

                // 2. 如果没有本地文件，则从币安交易所获取数据
                await LoadDataFromBinance();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成真实数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 生成模拟数据
        /// </summary>
        private async Task GenerateSimulatedData()
        {
            try
            {
                Console.WriteLine("🎭 正在生成模拟数据...");
                _logWindow?.AddLog("正在生成模拟数据", LogType.Info);
                
                // 生成模拟的合约数据
                _allSymbols = GenerateSimulatedSymbols();
                _allTicks = GenerateSimulatedTicks();
                
                Console.WriteLine($"✅ 生成了 {_allSymbols.Count} 个模拟合约");
                _logWindow?.AddLog($"生成了 {_allSymbols.Count} 个模拟合约", LogType.Success);
                
                // 更新界面
                UpdateConnectionStatus(false); // 显示为离线状态
                txtApiKey.Text = "模拟模式";
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 生成模拟数据失败: {ex.Message}");
                _logWindow?.AddLog($"生成模拟数据失败: {ex.Message}", LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 生成模拟合约信息
        /// </summary>
        private List<SymbolInfo> GenerateSimulatedSymbols()
        {
            var symbols = new List<SymbolInfo>();
            var baseSymbols = new[] { "BTC", "ETH", "BNB", "ADA", "DOT", "LINK", "LTC", "BCH", "XRP", "EOS" };
            
            foreach (var baseSymbol in baseSymbols)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var symbol = $"{baseSymbol}USDT";
                    symbols.Add(new SymbolInfo
                    {
                        Symbol = symbol,
                        BaseAsset = baseSymbol,
                        QuoteAsset = "USDT",
                        MinPrice = 0.00000001m,
                        MaxPrice = 1000000m,
                        MinQty = 0.00000001m,
                        MaxQty = 1000000m,
                        QtyPrecision = 8,
                        PricePrecision = 8,
                        MinNotional = 10m,
                        IsTrading = true,
                        ContractType = ContractType.Perpetual,
                        ExpiryDate = null
                    });
                }
            }
            
            return symbols;
        }

        /// <summary>
        /// 生成模拟价格统计
        /// </summary>
        private List<PriceStatistics> GenerateSimulatedTicks()
        {
            var ticks = new List<PriceStatistics>();
            var random = new Random();
            
            foreach (var symbol in _allSymbols)
            {
                var basePrice = random.Next(1, 1000);
                var changePercent = (random.NextDouble() - 0.5) * 20; // -10% 到 +10%
                var currentPrice = basePrice * (1 + changePercent / 100);
                
                ticks.Add(new PriceStatistics
                {
                    Symbol = symbol.Symbol,
                    LastPrice = (decimal)currentPrice,
                    PriceChange = (decimal)(basePrice * changePercent / 100),
                    PriceChangePercent = (decimal)changePercent,
                    HighPrice = (decimal)(basePrice * 1.1),
                    LowPrice = (decimal)(basePrice * 0.9),
                    Volume = random.Next(1000, 1000000),
                    QuoteVolume = random.Next(100000, 10000000),
                    OpenPrice = (decimal)basePrice,
                    OpenTime = DateTime.Now.AddDays(-1),
                    CloseTime = DateTime.Now,
                    Count = random.Next(100, 10000)
                });
            }
            
            return ticks;
        }

        /// <summary>
        /// 从币安交易所获取可交易的USDT永续合约数据
        /// 基于币安官方API文档：https://developers.binance.com/docs/zh-CN/binance-spot-api-docs/rest-api/market-data-endpoints
        /// </summary>
        private async Task LoadDataFromBinance()
        {
            try
            {
                _logWindow?.AddLog("开始从币安交易所获取可交易的USDT永续合约数据", LogType.API);
                
                // 显示加载提示
                txtSubtitle.Text = "正在从币安交易所获取合约信息...";
                
                // 步骤1: 获取所有合约信息，过滤出USDT永续合约且可交易的
                _logWindow?.AddLog("正在调用币安API获取交易所信息...", LogType.API);
                var allSymbolsInfo = await _apiClient.GetAllSymbolsInfoAsync();
                
                if (allSymbolsInfo == null || allSymbolsInfo.Count == 0)
                {
                    _logWindow?.AddLog("未获取到合约信息", LogType.Error);
                    throw new Exception("未获取到合约信息");
                }
                
                // 过滤出USDT永续合约且可交易的
                _allSymbols = allSymbolsInfo.Where(s => 
                    s.QuoteAsset == "USDT" && 
                    s.IsTrading && 
                    s.ContractType == ContractType.Perpetual).ToList();
                
                _logWindow?.AddLog($"总合约数: {allSymbolsInfo.Count}", LogType.Info);
                _logWindow?.AddLog($"过滤条件: USDT计价 + 可交易状态 + 永续合约", LogType.Info);
                _logWindow?.AddLog($"符合条件的永续合约数: {_allSymbols.Count}", LogType.Success);
                
                if (_allSymbols.Count == 0)
                {
                    _logWindow?.AddLog("未找到符合条件的USDT永续合约", LogType.Warning);
                    txtSubtitle.Text = "未找到符合条件的USDT永续合约";
                    return;
                }
                
                txtSubtitle.Text = $"找到 {_allSymbols.Count} 个可交易的USDT永续合约，正在获取价格数据...";
                
                // 步骤2: 获取这些合约的价格数据
                _logWindow?.AddLog("正在获取合约的24H价格统计...", LogType.API);
                _allTicks = new List<PriceStatistics>();
                var progress = 0;
                
                foreach (var symbol in _allSymbols)
                {
                    try
                    {
                        var stats = await _apiClient.Get24hrPriceStatisticsAsync(symbol.Symbol);
                        _allTicks.Add(stats);
                        
                        // 更新进度
                        progress++;
                        if (progress % 10 == 0 || progress == _allSymbols.Count)
                        {
                            txtSubtitle.Text = $"正在获取价格数据... ({progress}/{_allSymbols.Count})";
                            _logWindow?.AddLog($"已获取 {progress}/{_allSymbols.Count} 个合约的价格数据", LogType.Info);
                            await Task.Delay(10); // 让UI更新
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录错误但继续处理其他合约
                        _logWindow?.AddLog($"获取合约 {symbol.Symbol} 价格数据失败: {ex.Message}", LogType.Warning);
                        System.Diagnostics.Debug.WriteLine($"获取合约 {symbol.Symbol} 价格数据失败: {ex.Message}");
                    }
                }
                
                // 步骤3: 保存到本地文件
                if (_allSymbols.Count > 0)
                {
                    _logWindow?.AddLog($"成功获取到 {_allSymbols.Count} 个合约信息，开始保存数据", LogType.Success);
                    
                    // 为每个交易对设置模拟价格
                    // 在实际应用中，这里应该使用从币安API获取的真实价格
                    foreach (var tick in _allTicks)
                    {
                        try
                        {
                            await _apiClient.SetSimulatedPriceAsync(tick.Symbol, tick.LastPrice);
                        }
                        catch (Exception ex)
                        {
                            _logWindow?.AddLog($"设置价格失败 {tick.Symbol}: {ex.Message}", LogType.Warning);
                            System.Diagnostics.Debug.WriteLine($"设置价格失败 {tick.Symbol}: {ex.Message}");
                        }
                    }
                    
                    _logWindow?.AddLog("正在保存合约信息到本地文件...", LogType.Info);
                    await _apiClient.SaveSymbolsToFileAsync(_allSymbols);
                    _logWindow?.AddLog("合约信息已成功保存到本地文件", LogType.Success);
                    txtSubtitle.Text = $"成功获取 {_allSymbols.Count} 个永续合约信息，已保存到本地文件";
                }
                else
                {
                    _logWindow?.AddLog("未能获取到有效的合约信息", LogType.Warning);
                    txtSubtitle.Text = "未能获取到有效的合约信息";
                }
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"从币安获取数据失败: {ex.Message}", LogType.Error);
                _logWindow?.AddLog($"异常详情: {ex}", LogType.Error);
                txtSubtitle.Text = $"从币安获取数据失败: {ex.Message}";
                throw;
            }
        }

        private void UpdateConnectionStatus(bool isConnected)
        {
            txtConnectionStatus.Text = isConnected ? "已连接" : "未连接";
            txtConnectionStatus.Foreground = isConnected ? Brushes.Green : Brushes.Red;
        }



        private async Task DisplayCurrentPage()
        {
            // 清空内容区域
            contentPanel.Children.Clear();
            
            // 计算当前页的数据范围
            var startIndex = (_currentPage - 1) * _pageSize;
            var endIndex = Math.Min(startIndex + _pageSize, _allSymbols.Count);
            var currentPageSymbols = _allSymbols.Skip(startIndex).Take(_pageSize).ToList();
            
            // 显示加载提示
            var loadingText = new TextBlock
            {
                Text = "正在加载合约信息...",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            contentPanel.Children.Add(loadingText);
            
            // 获取最新价格信息 - 优化版本
            var symbolPrices = new List<(SymbolInfo Symbol, decimal Price)>();
            
            // 方法1：优先使用已获取的tick数据（最快）
            foreach (var symbol in currentPageSymbols)
            {
                var tick = _allTicks.FirstOrDefault(t => t.Symbol == symbol.Symbol);
                if (tick != null && tick.LastPrice > 0)
                {
                    symbolPrices.Add((symbol, tick.LastPrice));
                    _logWindow?.AddLog($"从tick数据获取价格: {symbol.Symbol} = {tick.LastPrice}", LogType.Debug);
                }
                else
                {
                    // 如果tick数据中没有价格，标记为需要单独获取
                    symbolPrices.Add((symbol, 0m));
                }
            }
            
            // 方法2：对于没有价格的数据，并行获取（性能优化）
            var symbolsNeedingPrice = symbolPrices.Where(sp => sp.Item2 == 0).ToList();
            if (symbolsNeedingPrice.Count > 0)
            {
                _logWindow?.AddLog($"需要单独获取 {symbolsNeedingPrice.Count} 个合约的价格", LogType.Info);
                
                // 并行获取价格（性能优化）
                var priceTasks = symbolsNeedingPrice.Select(async sp =>
                {
                    try
                    {
                        var price = await _apiClient.GetLatestPriceAsync(sp.Symbol.Symbol);
                        _logWindow?.AddLog($"API获取价格成功: {sp.Symbol.Symbol} = {price}", LogType.Debug);
                        return (sp.Symbol, price);
                    }
                    catch (Exception ex)
                    {
                        _logWindow?.AddLog($"API获取价格失败: {sp.Symbol.Symbol}, 错误: {ex.Message}", LogType.Warning);
                        return (sp.Symbol, 100m); // 默认价格
                    }
                });
                
                var apiPrices = await Task.WhenAll(priceTasks);
                
                // 更新价格数据
                for (int i = 0; i < symbolPrices.Count; i++)
                {
                    var currentItem = symbolPrices[i];
                    if (currentItem.Item2 == 0)
                    {
                        var apiPrice = apiPrices.FirstOrDefault(ap => ap.Symbol.Symbol == currentItem.Symbol.Symbol);
                        if (apiPrice.Symbol != null)
                        {
                            symbolPrices[i] = (currentItem.Symbol, apiPrice.Item2);
                        }
                    }
                }
            }
            
            // 调试：显示价格获取结果
            _logWindow?.AddLog($"价格获取完成，共 {symbolPrices.Count} 个合约", LogType.Info);
            foreach (var sp in symbolPrices.Take(5)) // 显示前5个的价格
            {
                _logWindow?.AddLog($"价格示例: {sp.Symbol.Symbol} = {sp.Item2}", LogType.Debug);
            }
            
            // 移除加载提示
            contentPanel.Children.Remove(loadingText);
            
            // 显示合约信息
            foreach (var symbolPrice in symbolPrices.OrderBy(sp => sp.Symbol.Symbol))
            {
                var contractCard = CreateContractCard(symbolPrice.Symbol, symbolPrice.Price);
                contentPanel.Children.Add(contractCard);
            }
            
            // 更新翻页信息
            txtPageInfo.Text = $"第 {_currentPage} 页，共 {_totalPages} 页";
        }

        private Border CreateContractCard(SymbolInfo symbol, decimal currentPrice)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 左侧：交易对信息
            var leftPanel = new StackPanel();
            var symbolText = new TextBlock
            {
                Text = symbol.Symbol,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
            };
            var baseAssetText = new TextBlock
            {
                Text = $"{symbol.BaseAsset} / {symbol.QuoteAsset}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                Margin = new Thickness(0, 5, 0, 0)
            };
            leftPanel.Children.Add(symbolText);
            leftPanel.Children.Add(baseAssetText);

            // 中间：合约详情
            var middlePanel = new StackPanel();
            var contractTypeText = new TextBlock
            {
                Text = $"合约类型: {GetContractTypeText(symbol.ContractType)}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
            };
            var precisionText = new TextBlock
            {
                Text = $"价格精度: {symbol.PricePrecision}位, 数量精度: {symbol.QtyPrecision}位",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                Margin = new Thickness(0, 5, 0, 0)
            };
            var limitsText = new TextBlock
            {
                Text = $"最小数量: {symbol.MinQty}, 最小名义价值: {symbol.MinNotional} {symbol.QuoteAsset}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                Margin = new Thickness(0, 5, 0, 0)
            };
            middlePanel.Children.Add(contractTypeText);
            middlePanel.Children.Add(precisionText);
            middlePanel.Children.Add(limitsText);

            // 右侧：价格信息
            var rightPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var priceText = new TextBlock
            {
                Text = $"{FormatPrice(currentPrice)} {symbol.QuoteAsset}",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var statusText = new TextBlock
            {
                Text = symbol.IsTrading ? "可交易" : "暂停交易",
                FontSize = 12,
                Foreground = symbol.IsTrading ? Brushes.Green : Brushes.Red,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 0)
            };
            rightPanel.Children.Add(priceText);
            rightPanel.Children.Add(statusText);

            // 添加到网格
            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(middlePanel, 1);
            Grid.SetColumn(rightPanel, 2);

            grid.Children.Add(leftPanel);
            grid.Children.Add(middlePanel);
            grid.Children.Add(rightPanel);

            card.Child = grid;
            return card;
        }

        private string GetContractTypeText(ContractType contractType)
        {
            return contractType switch
            {
                ContractType.Perpetual => "永续合约",
                ContractType.Quarterly => "季度合约",
                ContractType.NextQuarterly => "次季度合约",
                _ => "未知"
            };
        }

        /// <summary>
        /// 智能价格格式化：根据价格大小自动调整小数位数
        /// </summary>
        /// <param name="price">价格</param>
        /// <returns>格式化后的价格字符串</returns>
        private string FormatPrice(decimal price)
        {
            if (price < 0.01m)
            {
                // 小于0.01的价格显示8位小数
                return price.ToString("F8");
            }
            else
            {
                // 大于等于0.01的价格显示2位小数
                return price.ToString("F2");
            }
        }

        // 翻页相关方法
        private async void BtnFirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage != 1)
            {
                _currentPage = 1;
                await DisplayCurrentPage();
                UpdatePaginationButtons();
            }
        }

        private async void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await DisplayCurrentPage();
                UpdatePaginationButtons();
            }
        }

        private async void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                await DisplayCurrentPage();
                UpdatePaginationButtons();
            }
        }

        private async void BtnLastPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage != _totalPages)
            {
                _currentPage = _totalPages;
                await DisplayCurrentPage();
                UpdatePaginationButtons();
            }
        }

        private void CmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPageSize.SelectedIndex >= 0)
            {
                var pageSizes = new[] { 10, 20, 50, 100 };
                _pageSize = pageSizes[cmbPageSize.SelectedIndex];
                _totalPages = (_allSymbols.Count + _pageSize - 1) / _pageSize;
                _currentPage = Math.Min(_currentPage, _totalPages);
                if (_currentPage < 1) _currentPage = 1;
                
                if (paginationPanel.Visibility == Visibility.Visible)
                {
                    _ = DisplayCurrentPage();
                    UpdatePaginationButtons();
                }
            }
        }

        private void UpdatePaginationButtons()
        {
            btnFirstPage.IsEnabled = _currentPage > 1;
            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < _totalPages;
            btnLastPage.IsEnabled = _currentPage < _totalPages;
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (txtTitle.Text == "可交易永续合约")
            {
                await RefreshContractData();
            }
        }

        private async Task RefreshContractData()
        {
            try
            {
                _logWindow?.AddLog("用户点击刷新按钮，开始重新获取数据", LogType.Info);
                btnRefresh.IsEnabled = false;
                
                // 重新获取真实数据
                await GenerateRealData();
                
                // 重新计算总页数
                _totalPages = (_allSymbols.Count + _pageSize - 1) / _pageSize;
                _currentPage = Math.Min(_currentPage, _totalPages);
                if (_currentPage < 1) _currentPage = 1;
                
                // 显示当前页数据
                await DisplayCurrentPage();
                
                // 更新翻页按钮状态
                UpdatePaginationButtons();
                
                txtSubtitle.Text = $"共找到 {_allSymbols.Count} 个可交易的永续合约 (已刷新)";
                _logWindow?.AddLog($"数据刷新完成，共获取到 {_allSymbols.Count} 个合约", LogType.Success);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"刷新失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_allSymbols.Count == 0)
            {
                MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    FileName = $"永续合约信息_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportToCsv(saveFileDialog.FileName);
                    MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 查看日志按钮点击事件
        /// </summary>
        private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_logWindow != null && !_logWindow.IsVisible)
                {
                    _logWindow.Show();
                    _logWindow.Activate();
                }
                else if (_logWindow == null || _logWindow.IsVisible == false)
                {
                    _logWindow = new LogWindow();
                    _logWindow.Closed += (s, args) => 
                    {
                        // 窗口关闭时，将引用设为null，允许重新创建
                        _logWindow = null;
                    };
                    _logWindow.Show();
                }
                else
                {
                    _logWindow.Activate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开日志窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportToCsv(string fileName)
        {
            var lines = new List<string>
            {
                "交易对,基础资产,计价资产,合约类型,当前价格,价格精度,数量精度,最小数量,最小名义价值,交易状态"
            };

            foreach (var symbol in _allSymbols.OrderBy(s => s.Symbol))
            {
                try
                {
                    var price = await _apiClient.GetLatestPriceAsync(symbol.Symbol);
                    var line = $"{symbol.Symbol},{symbol.BaseAsset},{symbol.QuoteAsset}," +
                              $"{GetContractTypeText(symbol.ContractType)},{price:F2}," +
                              $"{symbol.PricePrecision},{symbol.QtyPrecision},{symbol.MinQty}," +
                              $"{symbol.MinNotional},{symbol.IsTrading}";
                    lines.Add(line);
                }
                catch (Exception)
                {
                    // 如果获取价格失败，使用tick数据中的价格
                    var tick = _allTicks.FirstOrDefault(t => t.Symbol == symbol.Symbol);
                    var price = tick?.LastPrice ?? 100m;
                    var line = $"{symbol.Symbol},{symbol.BaseAsset},{symbol.QuoteAsset}," +
                              $"{GetContractTypeText(symbol.ContractType)},{price:F2}," +
                              $"{symbol.PricePrecision},{symbol.QtyPrecision},{symbol.MinQty}," +
                              $"{symbol.MinNotional},{symbol.IsTrading}";
                    lines.Add(line);
                }
            }

                            System.IO.File.WriteAllLines(fileName, lines);
        }

        #region 选币工具数据模型

        /// <summary>
        /// 高低价数据模型
        /// </summary>
        public class HighLowData
        {
            public string Symbol { get; set; } = "";
            public decimal HighestPrice { get; set; }
            public decimal LowestPrice { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int KlineCount { get; set; }
        }

        #endregion

        #region 市场监控功能

        /// <summary>
        /// 启动市场监控
        /// </summary>
        private void StartMarketMonitoring()
        {
            try
            {
                Console.WriteLine("🚀 启动市场监控服务...");
                _marketMonitorService?.StartMonitoring();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动市场监控服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止市场监控
        /// </summary>
        private void StopMarketMonitoring()
        {
            try
            {
                Console.WriteLine("🛑 停止市场监控服务...");
                _marketMonitorService?.StopMonitoring();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 停止市场监控服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 复制合约名到剪贴板
        /// </summary>
        private void CopySymbolToClipboard(string symbol)
        {
            try
            {
                if (TrySetClipboardText(symbol))
                {
                    Console.WriteLine($"📋 已复制合约名到剪贴板: {symbol}");
                    
                    // 显示复制成功的临时提示
                    ShowTemporaryMessage($"✅ 已复制: {symbol}");
                }
                else
                {
                    ShowTemporaryMessage($"❌ 复制失败: {symbol}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 复制到剪贴板失败: {ex.Message}");
                ShowTemporaryMessage("❌ 复制失败");
            }
        }

        /// <summary>
        /// 显示临时消息提示
        /// </summary>
        private void ShowTemporaryMessage(string message)
        {
            try
            {
                // 更新状态栏显示复制成功信息
                txtSubtitle.Text = message;
                
                // 2秒后恢复原状态
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (sender, e) =>
                {
                    timer.Stop();
                    // 恢复默认状态文本
                    if (txtSubtitle.Text == message)
                    {
                        txtSubtitle.Text = "点击任意合约行可复制合约名到剪贴板";
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 显示临时消息失败: {ex.Message}");
            }
        }

        #endregion

        #region API设置功能

        /// <summary>
        /// API设置按钮点击事件
        /// </summary>
        private void BtnApiSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var apiSettingsWindow = new ApiSettingsWindow(_serviceProvider)
                {
                    Owner = this
                };
                
                var result = apiSettingsWindow.ShowDialog();
                // API设置窗口已经处理了重新初始化，这里不需要额外操作
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开API设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 手动重新连接API按钮点击事件
        /// </summary>
        private async void BtnReconnectApi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "重新连接中...";
                }

                await ReinitializeApiAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "重新连接";
                }
            }
        }

        #endregion

        #region 选币工具核心功能

        /// <summary>
        /// 获取所有合约的K线数据
        /// </summary>
        private async Task FetchKlineDataAsync()
        {
            try
            {
                // 创建取消令牌
                _fetchCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _fetchCancellationTokenSource.Token;
                
                // 更新按钮状态
                btnFetchKlineData.IsEnabled = false;
                
                _logWindow?.AddLog("开始获取K线数据...", LogType.Info);
                Console.WriteLine("🚀 开始获取K线数据...");
                Console.WriteLine($"📁 数据将保存到: {System.IO.Path.GetFullPath("KlineData")}");
                Console.WriteLine($"📊 每个合约获取90天K线数据（确保市场波动率分析有足够数据）");
                Console.WriteLine();
                
                // 如果 _allSymbols 为空，先自动获取合约列表
                if (_allSymbols == null || _allSymbols.Count == 0)
                {
                    _logWindow?.AddLog("合约列表为空，正在自动获取最新合约信息...", LogType.Info);
                    Console.WriteLine("📋 合约列表为空，正在自动获取最新合约信息...");
                    
                    try
                    {
                        var allSymbolsInfo = await _apiClient.GetAllSymbolsInfoAsync();
                        if (allSymbolsInfo != null && allSymbolsInfo.Count > 0)
                        {
                            _allSymbols = allSymbolsInfo.Where(s => 
                                s.QuoteAsset == "USDT" && 
                                s.IsTrading && 
                                s.ContractType == ContractType.Perpetual).ToList();
                            
                            _logWindow?.AddLog($"自动获取成功，总合约数: {allSymbolsInfo.Count}, 符合条件的USDT永续合约数: {_allSymbols.Count}", LogType.Success);
                        }
                        else
                        {
                            _logWindow?.AddLog("自动获取合约信息失败", LogType.Error);
                            MessageBox.Show("无法获取合约信息，请检查网络连接和API配置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logWindow?.AddLog($"自动获取合约信息异常: {ex.Message}", LogType.Error);
                        MessageBox.Show($"获取合约信息失败: {ex.Message}\n\n请检查网络连接和API配置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                
                // 获取所有USDT永续合约且可交易的
                var symbols = _allSymbols.Where(s => 
                    s.QuoteAsset == "USDT" && 
                    s.IsTrading && 
                    s.ContractType == ContractType.Perpetual).ToList();
                
                _logWindow?.AddLog($"过滤条件：USDT计价 + 可交易状态 + 永续合约", LogType.Info);
                _logWindow?.AddLog($"需要获取K线数据的永续合约数量: {symbols.Count}", LogType.Info);
                Console.WriteLine($"📊 过滤出 {symbols.Count} 个可交易的USDT永续合约");
                
                if (symbols.Count == 0)
                {
                    _logWindow?.AddLog("没有找到可交易的USDT永续合约", LogType.Warning);
                    MessageBox.Show("没有找到可交易的USDT永续合约。这可能是由于：\n\n1. API连接问题\n2. 网络连接问题\n3. API Key权限不足（仅需要行情数据权限）\n\n请检查网络连接和API配置", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 检查API连接状态
                try
                {
                    _logWindow?.AddLog("正在测试API连接...", LogType.Info);
                    await _apiClient.TestConnectionAsync();
                    _logWindow?.AddLog("API连接测试成功", LogType.Success);
                }
                catch (Exception ex)
                {
                    _logWindow?.AddLog($"API连接测试失败: {ex.Message}", LogType.Error);
                    MessageBox.Show($"API连接失败: {ex.Message}\n请检查网络连接和API配置", "连接错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 检查取消令牌
                cancellationToken.ThrowIfCancellationRequested();
                
                // 批量获取K线数据
                _logWindow?.AddLog($"开始批量获取 {symbols.Count} 个合约的K线数据...", LogType.Info);
                
                var successCount = 0;
                var failedCount = 0;
                
                foreach (var symbol in symbols)
                {
                    try
                    {
                        // 检查取消令牌
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        _logWindow?.AddLog($"正在处理 {symbol.Symbol} 的K线数据...", LogType.Debug);
                        
                        // 检查数据更新状态 - 使用新的智能检查
                        var updateStatus = await _klineStorageService.CheckUpdateStatusAsync(symbol.Symbol);
                        
                        if (!updateStatus.NeedsUpdate)
                        {
                            _logWindow?.AddLog($"跳过 {symbol.Symbol}: {updateStatus.Reason}", LogType.Info);
                            successCount++;
                            continue;
                        }
                        
                        _logWindow?.AddLog($"更新 {symbol.Symbol}: {updateStatus.Reason}", LogType.Info);
                        
                        // 使用智能下载方法（只下载缺失的部分）
                        try
                        {
                            var (downloadSuccess, changedCount, downloadError) = 
                                await _klineStorageService.SmartDownloadKlineDataAsync(
                                    symbol.Symbol, 
                                    _apiClient, 
                                    90 // 默认下载90天
                                );

                            if (downloadSuccess)
                            {
                                if (changedCount > 0)
                                {
                                    _logWindow?.AddLog($"更新 {symbol.Symbol}: 变更{changedCount}条数据", LogType.Success);
                                    successCount++;
                                }
                                else
                                {
                                    _logWindow?.AddLog($"跳过 {symbol.Symbol}: 数据已是最新", LogType.Info);
                                    successCount++;
                                }
                            }
                            else
                            {
                                _logWindow?.AddLog($"失败 {symbol.Symbol}: {downloadError}", LogType.Error);
                                failedCount++;
                            }
                        }
                        catch (Exception apiEx)
                        {
                            // 详细打印API调用失败的原因
                            Console.WriteLine($"❌ API调用失败 {symbol.Symbol}:");
                            Console.WriteLine($"   🔍 错误类型: {apiEx.GetType().Name}");
                            Console.WriteLine($"   📝 错误信息: {apiEx.Message}");
                            Console.WriteLine($"   📍 错误位置: {apiEx.StackTrace?.Split('\n').FirstOrDefault()}");
                            Console.WriteLine($"   🔗 API端点: {_apiClient.BaseUrl}/api/v3/klines");
                            Console.WriteLine($"   📊 请求参数: symbol={symbol.Symbol}, interval=1d, limit=90");
                            Console.WriteLine();
                            
                            _logWindow?.AddLog($"API调用 {symbol.Symbol} 失败: {apiEx.Message}", LogType.Error);
                            failedCount++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logWindow?.AddLog($"获取K线数据被取消", LogType.Warning);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logWindow?.AddLog($"获取 {symbol.Symbol} K线数据失败: {ex.Message}", LogType.Error);
                        failedCount++;
                    }
                }
                
                _logWindow?.AddLog($"K线数据获取完成！成功: {successCount}, 失败: {failedCount}", LogType.Success);
                Console.WriteLine($"✅ K线数据获取完成！");
                Console.WriteLine($"   🎯 成功: {successCount} 个合约");
                Console.WriteLine($"   ❌ 失败: {failedCount} 个合约");
                Console.WriteLine($"   📁 数据保存在: {System.IO.Path.GetFullPath("KlineData")}");
                Console.WriteLine();
                MessageBox.Show($"K线数据获取完成！\n成功: {successCount} 个合约\n失败: {failedCount} 个合约", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                _logWindow?.AddLog("获取K线数据已被用户取消", LogType.Warning);
                MessageBox.Show("获取K线数据已被用户取消", "已取消", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"获取K线数据失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"获取K线数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                btnFetchKlineData.IsEnabled = true;
                
                // 清理取消令牌
                _fetchCancellationTokenSource?.Dispose();
                _fetchCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 计算所有合约的N天高低价数据
        /// </summary>
        /// <remarks>
        /// ⚠️ 已废弃：此方法使用固定时间范围计算，导致历史数据会随时间变化。
        /// 请使用 CalculateLocationDataForDateAsync() 替代，为每个日期计算独立的最高最低价。
        /// </remarks>
        [Obsolete("使用 CalculateLocationDataForDateAsync() 替代")]
        private async Task CalculateHighLowPricesAsync()
        {
            try
            {
                // 创建取消令牌
                _calculationCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _calculationCancellationTokenSource.Token;
                
                // 更新按钮状态
                btnCalculateHighLow.IsEnabled = false;
                
                _logWindow?.AddLog($"开始计算{_highLowAnalysisDays}天高低价数据...", LogType.Info);
                
                // 清空现有数据
                _highLowData.Clear();
                
                // 计算N天前的日期
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-_highLowAnalysisDays);
                
                _logWindow?.AddLog($"计算时间范围: {startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd}", LogType.Info);
                
                // 从本地K线数据中获取所有可用的合约
                var availableSymbols = _allKlineData.Select(k => k.Symbol).Distinct().ToList();
                _logWindow?.AddLog($"从本地K线数据中找到 {availableSymbols.Count} 个合约", LogType.Info);
                
                if (availableSymbols.Count == 0)
                {
                    _logWindow?.AddLog("本地没有可用的K线数据，请先获取K线数据", LogType.Warning);
                    MessageBox.Show("本地没有可用的K线数据，请先获取K线数据", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 检查取消令牌
                cancellationToken.ThrowIfCancellationRequested();
                
                // 从本地文件读取K线数据
                _logWindow?.AddLog($"开始从本地文件读取K线数据...", LogType.Info);
                
                var successCount = 0;
                var failedCount = 0;
                
                foreach (var symbol in availableSymbols)
                {
                    try
                    {
                        // 检查取消令牌
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        _logWindow?.AddLog($"正在处理 {symbol}...", LogType.Debug);
                        
                        // 从本地文件加载K线数据
                        var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(symbol);
                        
                        if (loadSuccess && klines != null && klines.Count > 0)
                        {
                            // 按指定的日期范围过滤K线数据
                            var filteredKlines = klines
                                .Where(k => k.OpenTime.Date >= startDate.Date && k.OpenTime.Date <= endDate.Date)
                                .ToList();
                            
                            if (filteredKlines.Count > 0)
                            {
                                // 计算指定日期范围内的高低价
                                var highPrice = filteredKlines.Max(k => k.HighPrice);
                                var lowPrice = filteredKlines.Min(k => k.LowPrice);
                                
                                var highLowData = new HighLowData
                                {
                                    Symbol = symbol,
                                    HighestPrice = highPrice,
                                    LowestPrice = lowPrice,
                                    StartDate = startDate,
                                    EndDate = endDate,
                                    KlineCount = filteredKlines.Count
                                };
                                
                                _highLowData.Add(highLowData);
                                _logWindow?.AddLog($"处理完成: {symbol}, 最高: {highPrice:F8}, 最低: {lowPrice:F8} (使用{filteredKlines.Count}天数据)", LogType.Debug);
                                successCount++;
                            }
                            else
                            {
                                _logWindow?.AddLog($"跳过 {symbol}: 指定日期范围内无K线数据", LogType.Warning);
                                failedCount++;
                            }
                        }
                        else if (!loadSuccess)
                        {
                            _logWindow?.AddLog($"跳过 {symbol}: 加载K线数据失败: {loadError}", LogType.Error);
                            failedCount++;
                        }
                        else
                        {
                            _logWindow?.AddLog($"跳过 {symbol}: 本地无K线数据，请先获取数据", LogType.Warning);
                            failedCount++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logWindow?.AddLog($"处理 {symbol} 被取消", LogType.Warning);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logWindow?.AddLog($"处理 {symbol} 失败: {ex.Message}", LogType.Error);
                        failedCount++;
                    }
                }
                
                _logWindow?.AddLog($"高低价计算完成，成功处理 {successCount} 个合约，失败 {failedCount} 个", LogType.Success);
                
                if (successCount == 0)
                {
                    MessageBox.Show("没有成功处理任何合约，请先获取K线数据", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 检查取消令牌
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    // 保存到本地文件
                    await SaveHighLowDataToFileAsync();
                    
                    // 计算位置比例
                    await CalculateLocationRatiosAsync(cancellationToken);
                    
                    // 高低价计算完成，直接显示结果，不弹出确认框
                    _logWindow?.AddLog($"高低价计算完成！成功处理 {successCount} 个合约，失败 {failedCount} 个合约", LogType.Info);
                    
                    // 清理振幅缓存，确保使用最新的K线数据
                    _amplitudeCache.Clear();
                }
                catch (Exception ex)
                {
                    _logWindow?.AddLog($"保存数据或计算位置比例时出错: {ex.Message}", LogType.Error);
                    // 即使位置比例计算失败，高低价计算仍然成功
                    // 高低价计算完成但位置比例计算失败，记录到日志
                    _logWindow?.AddLog($"高低价计算完成！成功处理 {successCount} 个合约，失败 {failedCount} 个合约。注意: 位置比例计算失败，但高低价数据已保存", LogType.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                _logWindow?.AddLog("计算已被用户取消", LogType.Warning);
                MessageBox.Show("计算已被用户取消", "已取消", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"计算高低价失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"计算高低价失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                btnCalculateHighLow.IsEnabled = true;
                
                // 清理取消令牌
                _calculationCancellationTokenSource?.Dispose();
                _calculationCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 保存高低价数据到本地文件
        /// </summary>
        private async Task SaveHighLowDataToFileAsync()
        {
            try
            {
                var fileName = "highlow_data.json";
                var json = System.Text.Json.JsonSerializer.Serialize(_highLowData, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                await System.IO.File.WriteAllTextAsync(fileName, json);
                _logWindow?.AddLog($"高低价数据已保存到: {fileName}", LogType.Success);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"保存高低价数据失败: {ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// 从本地文件加载高低价数据
        /// </summary>
        private async Task LoadHighLowDataFromFileAsync()
        {
            try
            {
                var fileName = "highlow_data.json";
                if (System.IO.File.Exists(fileName))
                {
                    var json = await System.IO.File.ReadAllTextAsync(fileName);
                    _highLowData = System.Text.Json.JsonSerializer.Deserialize<List<HighLowData>>(json) ?? new List<HighLowData>();
                    _logWindow?.AddLog($"从文件加载高低价数据: {_highLowData.Count} 个合约", LogType.Info);
                }
                else
                {
                    _logWindow?.AddLog("高低价数据文件不存在，需要先计算", LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"加载高低价数据失败: {ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// 计算位置比例
        /// </summary>
        /// <remarks>
        /// ⚠️ 已废弃：此方法依赖于 CalculateHighLowPricesAsync() 的固定时间范围。
        /// 请使用 CalculateLocationDataForDateAsync() 替代。
        /// </remarks>
        [Obsolete("使用 CalculateLocationDataForDateAsync() 替代")]
        private async Task CalculateLocationRatiosAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logWindow?.AddLog("开始计算位置比例...", LogType.Info);
                
                _locationData.Clear();
                
                _logWindow?.AddLog($"开始计算 {_highLowData.Count} 个合约的位置比例...", LogType.Info);
                
                var successCount = 0;
                var failedCount = 0;
                
                foreach (var highLow in _highLowData)
                {
                    try
                    {
                        // 检查取消令牌
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        _logWindow?.AddLog($"正在处理 {highLow.Symbol}...", LogType.Debug);
                        
                        // 从指定日期范围内的K线数据获取最后收盘价作为当前价格
                        decimal currentPrice = 0;
                        try
                        {
                            var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(highLow.Symbol);
                            if (loadSuccess && klines != null && klines.Count > 0)
                            {
                                // 过滤到指定日期范围内的数据，并取最后一个收盘价
                                var filteredKlines = klines
                                    .Where(k => k.OpenTime.Date >= highLow.StartDate.Date && k.OpenTime.Date <= highLow.EndDate.Date)
                                    .OrderBy(k => k.OpenTime)
                                    .ToList();
                                
                                if (filteredKlines.Count > 0)
                                {
                                    currentPrice = filteredKlines.Last().ClosePrice;
                                    _logWindow?.AddLog($"从{highLow.StartDate:yyyy-MM-dd}至{highLow.EndDate:yyyy-MM-dd}范围内获取 {highLow.Symbol} 最后收盘价: {currentPrice:F8}", LogType.Debug);
                                }
                                else
                                {
                                    _logWindow?.AddLog($"指定日期范围内无 {highLow.Symbol} 的K线数据", LogType.Warning);
                                    failedCount++;
                                    continue;
                                }
                            }
                            else
                            {
                                _logWindow?.AddLog($"无法从本地加载 {highLow.Symbol} 的K线数据: {loadError}", LogType.Warning);
                                failedCount++;
                                continue;
                            }
                        }
                        catch (Exception loadEx)
                        {
                            _logWindow?.AddLog($"加载 {highLow.Symbol} 本地K线数据失败: {loadEx.Message}", LogType.Error);
                            failedCount++;
                            continue;
                        }
                        
                        // 计算位置比例
                        var priceRange = highLow.HighestPrice - highLow.LowestPrice;
                        decimal locationRatio = 0;
                        
                        if (priceRange > 0)
                        {
                            locationRatio = (currentPrice - highLow.LowestPrice) / priceRange;
                        }
                        
                        // 确定状态
                        string status = locationRatio switch
                        {
                            < 0.1m => "超跌区域",
                            < 0.3m => "低位区域",
                            < 0.7m => "中位区域",
                            < 0.9m => "高位区域",
                            _ => "超涨区域"
                        };
                        
                        var locationData = new LocationData
                        {
                            Symbol = highLow.Symbol,
                            CurrentPrice = currentPrice,
                            LocationRatio = locationRatio,
                            HighestPrice = highLow.HighestPrice,
                            LowestPrice = highLow.LowestPrice,
                            PriceRange = priceRange,
                            Status = status
                        };
                        
                        _locationData.Add(locationData);
                        successCount++;
                        
                        _logWindow?.AddLog($"位置比例计算: {highLow.Symbol} = {locationRatio:F4} ({status})", LogType.Debug);
                    }
                    catch (OperationCanceledException)
                    {
                        _logWindow?.AddLog($"位置比例计算被取消", LogType.Warning);
                        throw; // 重新抛出取消异常
                    }
                    catch (Exception ex)
                    {
                        _logWindow?.AddLog($"计算 {highLow.Symbol} 位置比例失败: {ex.Message}", LogType.Error);
                        _logWindow?.AddLog($"异常详情: {ex.GetType().Name} - {ex.StackTrace?.Substring(0, Math.Min(200, ex.StackTrace?.Length ?? 0))}", LogType.Error);
                        failedCount++;
                    }
                }
                
                _logWindow?.AddLog($"位置比例计算完成，成功: {successCount} 个合约，失败: {failedCount} 个", LogType.Success);
                
                if (successCount == 0)
                {
                    _logWindow?.AddLog("警告: 没有成功计算任何合约的位置比例", LogType.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                _logWindow?.AddLog($"位置比例计算被取消", LogType.Warning);
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"计算位置比例失败: {ex.Message}", LogType.Error);
                _logWindow?.AddLog($"异常详情: {ex.GetType().Name} - {ex.StackTrace?.Substring(0, Math.Min(200, ex.StackTrace?.Length ?? 0))}", LogType.Error);
            }
        }

        /// <summary>
        /// 按位置比例范围筛选数据
        /// </summary>
        private List<LocationData> FilterLocationData(decimal minRatio, decimal maxRatio)
        {
            return _locationData
                .Where(d => d.LocationRatio >= minRatio && d.LocationRatio <= maxRatio)
                .OrderBy(d => d.LocationRatio)
                .ToList();
        }

        #endregion

        #region 选币工具事件处理

        // 选币工具按钮已移除 - 合并到主面板，默认显示

        /// <summary>
        /// 读取K线数据按钮点击事件
        /// </summary>




        /// <summary>
        /// 市场波动率一览按钮点击事件
        /// </summary>
        private async void BtnMarketVolatility_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnMarketVolatility.IsEnabled = false;
                btnMarketVolatility.Content = "计算中...";
                
                // 检查是否有K线数据
                if (_allKlineData?.Count == 0)
                {
                    // 尝试从文件加载数据
                    var loadingPanel = CreateLoadingPanel("正在检查K线数据文件，请稍候...");
                    contentPanel.Children.Clear();
                    contentPanel.Children.Add(loadingPanel);
                    
                    await LoadAllKlineDataAsync();
                    
                    if (_allKlineData?.Count == 0)
                    {
                        MessageBox.Show(
                            "没有找到K线数据文件。\n\n请先点击'获取K线数据'按钮下载数据，或点击'读取K线数据'按钮加载已有数据。", 
                            "数据不足", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Warning);
                        
                        var welcomePanel = CreateWelcomePanel("数据不足", "请先获取或读取K线数据，然后再次尝试查看市场波动率。");
                        contentPanel.Children.Clear();
                        contentPanel.Children.Add(welcomePanel);
                        return;
                    }
                }
                
                // 显示计算提示
                var calculatingPanel = CreateLoadingPanel("正在计算市场波动率，请稍候...");
                contentPanel.Children.Clear();
                contentPanel.Children.Add(calculatingPanel);
                
                // 计算市场波动率
                var volatilityData = await CalculateMarketVolatilityAsync();
                
                // 显示波动率结果
                await DisplayMarketVolatility(volatilityData);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算市场波动率失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnMarketVolatility.IsEnabled = true;
                btnMarketVolatility.Content = "市场波动率一览";
            }
        }

        /// <summary>
        /// 获取K线数据按钮点击事件
        /// </summary>
        private async void BtnFetchKlineData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnFetchKlineData.IsEnabled = false;
                btnFetchKlineData.Content = "获取中...";
                
                // 显示加载提示
                var loadingPanel = CreateLoadingPanel("正在获取K线数据，请稍候...");
                contentPanel.Children.Clear();
                contentPanel.Children.Add(loadingPanel);
                
                // 执行获取
                await FetchKlineDataAsync();
                
                // 显示完成提示
                var welcomePanel = CreateWelcomePanel("K线数据获取完成", "K线数据已成功获取并保存到本地文件。现在可以点击'计算高低价'按钮进行分析。");
                contentPanel.Children.Clear();
                contentPanel.Children.Add(welcomePanel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取K线数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnFetchKlineData.IsEnabled = true;
                btnFetchKlineData.Content = "获取K线数据";
            }
        }

        /// <summary>
        /// 计算高低价按钮点击事件（整合K线读取功能）
        /// </summary>
        private async void BtnCalculateHighLow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 先显示输入对话框获取天数
                var days = ShowDaysInputDialog();
                if (days == null)
                {
                    return; // 用户取消
                }
                
                // 保存配置
                SaveHighLowAnalysisConfig(days.Value);
                
                btnCalculateHighLow.IsEnabled = false;
                btnCalculateHighLow.Content = "读取K线中...";
                
                // 显示加载提示
                var loadingPanel = CreateLoadingPanel($"正在读取K线数据并计算{days}天高低价数据，请稍候...");
                contentPanel.Children.Clear();
                contentPanel.Children.Add(loadingPanel);
                
                // 先读取K线数据
                await LoadAllKlineDataAsync();
                
                // 如果K线数据为空，不继续计算
                if (_allKlineData?.Count == 0)
                {
                    MessageBox.Show("K线数据读取失败，无法进行高低价计算", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                btnCalculateHighLow.Content = "计算高低价中...";
                
                // 使用正确的方法计算今天的位置数据（基于历史N天）
                _logWindow?.AddLog($"开始计算今天的市场位置数据（分析天数: {_highLowAnalysisDays}）", LogType.Info);
                _locationData = await CalculateLocationDataForDateAsync(DateTime.UtcNow.Date, _highLowAnalysisDays);
                _logWindow?.AddLog($"计算完成，共 {_locationData.Count} 个合约", LogType.Info);
                
                // 显示结果
                await DisplayLocationDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算高低价失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCalculateHighLow.IsEnabled = true;
                btnCalculateHighLow.Content = "计算高低价";
            }
        }

        // 刷新高低价按钮已移除 - 根据用户要求简化界面

        /// <summary>
        /// 导出数据按钮点击事件
        /// </summary>
        private void BtnExportHighLow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_locationData.Count == 0)
                {
                    MessageBox.Show("没有可导出的数据，请先计算高低价", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    FileName = $"选币工具数据_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportLocationDataToCsv(saveFileDialog.FileName);
                    MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 位置比例筛选按钮已移除 - 根据用户要求简化界面
        
        /// <summary>
        /// 高级筛选工具按钮点击事件
        /// </summary>
        private void BtnAdvancedFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowAdvancedFilterDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开高级筛选工具失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 重置筛选按钮已移除 - 根据用户要求简化界面

        // 停止获取按钮已移除 - 根据用户要求简化界面

        #endregion

        #region 选币工具UI创建和数据显示

        /// <summary>
        /// 创建欢迎面板
        /// </summary>
        private Border CreateWelcomePanel(string title, string description)
        {
            var panel = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(30),
                Margin = new Thickness(0, 20, 0, 0)
            };

            var stackPanel = new StackPanel();
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            var descText = new TextBlock
            {
                Text = description,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(descText);
            panel.Child = stackPanel;

            return panel;
        }

        /// <summary>
        /// 创建加载面板
        /// </summary>
        private Border CreateLoadingPanel(string message)
        {
            var panel = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(30),
                Margin = new Thickness(0, 20, 0, 0)
            };

            var stackPanel = new StackPanel();
            var loadingText = new TextBlock
            {
                Text = "⏳",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            var messageText = new TextBlock
            {
                Text = message,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            stackPanel.Children.Add(loadingText);
            stackPanel.Children.Add(messageText);
            panel.Child = stackPanel;

            return panel;
        }

        /// <summary>
        /// 显示位置比例数据
        /// </summary>
        private async Task DisplayLocationDataAsync()
        {
            try
            {
                contentPanel.Children.Clear();

                if (_locationData.Count == 0)
                {
                    var noDataPanel = CreateWelcomePanel("暂无数据", "请先点击'计算高低价'按钮获取数据");
                    contentPanel.Children.Add(noDataPanel);
                    return;
                }

                // 创建主容器 - 使用Grid布局，左右分栏
                var mainContainer = new Grid();
                mainContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // 左侧，占2/3
                mainContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 右侧，占1/3
                
                // 左侧：位置数据和振幅分析
                var leftPanel = new StackPanel();
                
                // 创建数据表格
                var dataGrid = CreateLocationDataGrid(_locationData);
                dataGrid.Margin = new Thickness(0, 20, 0, 0);
                leftPanel.Children.Add(dataGrid);
                
                // 振幅波动分析
                var amplitudePanel = CreateAmplitudeAnalysisPanel();
                leftPanel.Children.Add(amplitudePanel);
                
                Grid.SetColumn(leftPanel, 0);
                mainContainer.Children.Add(leftPanel);
                
                // 右侧：市场位置变化表
                var rightPanel = await CreateMarketPositionHistoryPanelAsync();
                rightPanel.Margin = new Thickness(20, 20, 0, 0);
                Grid.SetColumn(rightPanel, 1);
                mainContainer.Children.Add(rightPanel);
                
                contentPanel.Children.Add(mainContainer);

                _logWindow?.AddLog($"显示位置比例数据: {_locationData.Count} 个合约", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"显示位置比例数据失败: {ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// 创建市场位置变化历史面板
        /// </summary>
        private async Task<Grid> CreateMarketPositionHistoryPanelAsync()
        {
            var panel = new Grid
            {
                VerticalAlignment = VerticalAlignment.Stretch
            };
            
            // 定义行：标题行(Auto) + 列表行(*)
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            try
            {
                // 标题
                var titleText = new TextBlock
                {
                    Text = $"过去{_highLowAnalysisDays}天整体市场位置变化表",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10),
                    TextAlignment = TextAlignment.Center
                };
                Grid.SetRow(titleText, 0);
                panel.Children.Add(titleText);
                
                if (_marketPositionService == null)
                {
                    var errorText = new TextBlock
                    {
                        Text = "市场位置服务未初始化",
                        Foreground = new SolidColorBrush(Colors.Red),
                        TextAlignment = TextAlignment.Center
                    };
                    Grid.SetRow(errorText, 1);
                    panel.Children.Add(errorText);
                    return panel;
                }
                
                // 获取或计算历史数据
                var historyData = await _marketPositionService.GetOrCalculateRecentDaysAsync(
                    _highLowAnalysisDays, 
                    _highLowAnalysisDays,
                    CalculateLocationDataForDateAsync);
                
                // 添加今天的数据（仅用于显示）
                var todayData = BinanceApps.Core.Services.MarketPositionService.CalculatePositionCounts(DateTime.UtcNow.Date, _locationData);
                historyData.Add(todayData);
                
                // 创建列表显示 - 响应式宽度，自动填充区域
                var listView = new ListView
                {
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Colors.LightGray),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,  // 拉伸填充
                    MinWidth = 300  // 最小宽度300px
                };
                
                // 创建GridView - 使用自适应宽度分布
                var gridView = new GridView();
                
                // 使用响应式列宽，根据容器宽度自动调整
                // 监听ListView的SizeChanged事件来动态调整列宽
                listView.SizeChanged += (sender, e) => {
                    if (sender is ListView lv && lv.View is GridView gv && gv.Columns.Count == 5)
                    {
                        var availableWidth = lv.ActualWidth - 30; // 减去滚动条和边距
                        if (availableWidth > 0)
                        {
                            var dateWidth = Math.Max(60, availableWidth * 0.2); // 日期列占20%，最小60px
                            var dataWidth = Math.Max(60, (availableWidth - dateWidth) / 4); // 数据列平均分配剩余空间
                            
                            gv.Columns[0].Width = dateWidth;
                            gv.Columns[1].Width = dataWidth;
                            gv.Columns[2].Width = dataWidth;
                            gv.Columns[3].Width = dataWidth;
                            gv.Columns[4].Width = dataWidth;
                        }
                    }
                };
                
                gridView.Columns.Add(new GridViewColumn 
                { 
                    Header = "日期", 
                    Width = 60,  // 初始宽度，将通过SizeChanged事件调整
                    CellTemplate = CreateDateCellTemplate("DateText")
                });
                gridView.Columns.Add(new GridViewColumn 
                { 
                    Header = "低位", 
                    Width = 60,  // 初始宽度，将通过SizeChanged事件调整
                    CellTemplate = CreateColoredCellTemplate("LowPositionCount")
                });
                gridView.Columns.Add(new GridViewColumn 
                { 
                    Header = "中低", 
                    Width = 60,  // 初始宽度，将通过SizeChanged事件调整
                    CellTemplate = CreateColoredCellTemplate("MidLowPositionCount")
                });
                gridView.Columns.Add(new GridViewColumn 
                { 
                    Header = "中高", 
                    Width = 60,  // 初始宽度，将通过SizeChanged事件调整
                    CellTemplate = CreateColoredCellTemplate("MidHighPositionCount")
                });
                gridView.Columns.Add(new GridViewColumn 
                { 
                    Header = "高位", 
                    Width = 60,  // 初始宽度，将通过SizeChanged事件调整
                    CellTemplate = CreateColoredCellTemplate("HighPositionCount")
                });
                
                listView.View = gridView;
                
                // 准备显示数据 - 按日期倒序排列（最新的在上面）
                var displayData = historyData
                    .OrderByDescending(h => h.Date)
                    .Select(h => new 
                    {
                        DateText = h.Date.ToString("MM-dd"),
                        LowPositionCount = h.LowPositionCount,
                        MidLowPositionCount = h.MidLowPositionCount,
                        MidHighPositionCount = h.MidHighPositionCount,
                        HighPositionCount = h.HighPositionCount
                    }).ToList();
                
                listView.ItemsSource = displayData;
                Grid.SetRow(listView, 1);
                panel.Children.Add(listView);
                
                // 添加说明文字
                var descText = new TextBlock
                {
                    Text = "说明：低位(0-25%), 中低(26-50%), 中高(51-75%), 高位(76%+)",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Margin = new Thickness(0, 5, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(descText, 2);
                panel.Children.Add(descText);
                
                _logWindow?.AddLog($"市场位置变化表创建完成，共 {historyData.Count} 天数据", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"创建市场位置变化表失败: {ex.Message}", LogType.Error);
                
                var errorText = new TextBlock
                {
                    Text = $"加载失败: {ex.Message}",
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(errorText, 1);
                panel.Children.Add(errorText);
            }
            
            return panel;
        }
        
        /// <summary>
        /// 创建带颜色渐变的单元格模板
        /// </summary>
        private DataTemplate CreateColoredCellTemplate(string bindingPath)
        {
            var template = new DataTemplate();
            
            // 创建Border作为容器，填充整个单元格
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(0));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(5));
            borderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            borderFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            
            // 创建背景颜色转换器
            var converter = new ValueToColorConverter();
            var backgroundBinding = new System.Windows.Data.Binding(bindingPath);
            backgroundBinding.Converter = converter;
            borderFactory.SetBinding(Border.BackgroundProperty, backgroundBinding);
            
            // 创建TextBlock显示数字
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(bindingPath));
            textBlockFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.Black));
            textBlockFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textBlockFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            
            borderFactory.AppendChild(textBlockFactory);
            template.VisualTree = borderFactory;
            
            return template;
        }
        
        /// <summary>
        /// 创建日期单元格模板
        /// </summary>
        private DataTemplate CreateDateCellTemplate(string bindingPath)
        {
            var template = new DataTemplate();
            
            // 创建Border作为容器
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(0));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(5));
            borderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            borderFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.LightGray));
            
            // 创建TextBlock显示日期
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(bindingPath));
            textBlockFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.Black));
            textBlockFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textBlockFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textBlockFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            
            borderFactory.AppendChild(textBlockFactory);
            template.VisualTree = borderFactory;
            
            return template;
        }
        
        /// <summary>
        /// 为指定日期计算位置数据（供MarketPositionService调用）
        /// </summary>
        /// <remarks>
        /// ✅ 正确的计算逻辑：
        /// 1. 对于任何历史日期，使用该日期前N天的数据计算最高最低价
        /// 2. 历史数据是固定的，不会随着今天的时间推移而变化
        /// 3. 例如：计算2024-01-15的位置（N=20天）：
        ///    - 使用 2023-12-27 至 2024-01-15 的数据
        ///    - 这个范围永远不会变
        /// </remarks>
        private async Task<List<LocationData>> CalculateLocationDataForDateAsync(DateTime date, int analysisDays)
        {
            try
            {
                _logWindow?.AddLog($"计算 {date:yyyy-MM-dd} 的位置数据，分析天数: {analysisDays}", LogType.Debug);
                
                var result = new List<LocationData>();
                
                // 获取该日期的所有合约数据
                var availableSymbols = _allKlineData.Select(k => k.Symbol).Distinct().ToList();
                
                foreach (var symbol in availableSymbols)
                {
                    try
                    {
                        // 加载K线数据
                        var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(symbol);
                        if (!loadSuccess || klines == null || klines.Count == 0) continue;
                        
                        // ✅ 关键逻辑：基于指定日期动态计算时间范围
                        // 这样每个历史日期都使用该日期前N天的数据，而不是固定的"今天前N天"
                        var endDate = date.AddDays(1); // 包含当天
                        var startDate = endDate.AddDays(-analysisDays);
                        
                        var filteredKlines = klines
                            .Where(k => k.OpenTime.Date >= startDate.Date && k.OpenTime.Date < endDate.Date)
                            .OrderBy(k => k.OpenTime)
                            .ToList();
                            
                        if (filteredKlines.Count == 0) continue;
                        
                        // 计算该时间段的最高最低价
                        var highestPrice = filteredKlines.Max(k => k.HighPrice);
                        var lowestPrice = filteredKlines.Min(k => k.LowPrice);
                        var priceRange = highestPrice - lowestPrice;
                        
                        if (priceRange <= 0) continue;
                        
                        // 获取指定日期的收盘价
                        var dayKline = filteredKlines.LastOrDefault(k => k.OpenTime.Date == date.Date);
                        if (dayKline == null) continue;
                        
                        var currentPrice = dayKline.ClosePrice;
                        var locationRatio = (currentPrice - lowestPrice) / priceRange;
                        
                        // 确定状态
                        string status = locationRatio switch
                        {
                            <= 0.25m => "低位区域",
                            <= 0.50m => "中低区域", 
                            <= 0.75m => "中高区域",
                            _ => "高位区域"
                        };
                        
                        result.Add(new LocationData
                        {
                            Symbol = symbol,
                            CurrentPrice = currentPrice,
                            LocationRatio = locationRatio,
                            HighestPrice = highestPrice,
                            LowestPrice = lowestPrice,
                            PriceRange = priceRange,
                            Status = status
                        });
                    }
                    catch (Exception ex)
                    {
                        _logWindow?.AddLog($"计算 {symbol} 在 {date:yyyy-MM-dd} 的位置数据失败: {ex.Message}", LogType.Warning);
                    }
                }
                
                _logWindow?.AddLog($"完成 {date:yyyy-MM-dd} 位置数据计算，共 {result.Count} 个合约", LogType.Debug);
                return result;
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"计算 {date:yyyy-MM-dd} 位置数据失败: {ex.Message}", LogType.Error);
                return new List<LocationData>();
            }
        }

        /// <summary>
        /// 显示筛选后的位置比例数据
        /// </summary>
        private void DisplayFilteredLocationData(List<LocationData> filteredData, decimal minRatio, decimal maxRatio)
        {
            try
            {
                contentPanel.Children.Clear();

                if (filteredData.Count == 0)
                {
                    var noDataPanel = CreateWelcomePanel("无筛选结果", $"在位置比例 {minRatio:F2} - {maxRatio:F2} 范围内没有找到符合条件的合约");
                    contentPanel.Children.Add(noDataPanel);
                    return;
                }

                // 创建筛选结果标题
                var titlePanel = CreateWelcomePanel("筛选结果", 
                    $"位置比例范围: {minRatio:F2} - {maxRatio:F2}\n" +
                    $"找到 {filteredData.Count} 个符合条件的合约");

                // 创建数据表格
                var dataGrid = CreateLocationDataGrid(filteredData);

                contentPanel.Children.Add(titlePanel);
                contentPanel.Children.Add(dataGrid);

                _logWindow?.AddLog($"显示筛选结果: {filteredData.Count} 个合约", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"显示筛选结果失败: {ex.Message}", LogType.Error);
            }
        }

        /// <summary>
        /// 创建位置比例数据表格 - 使用ListView替代Grid
        /// </summary>
        private StackPanel CreateLocationDataGrid(List<LocationData> data)
        {
            Console.WriteLine($"🔍 CreateLocationDataGrid 开始执行，数据数量: {data.Count}");
            
            var mainPanel = new StackPanel();
            mainPanel.Margin = new Thickness(0, 20, 0, 0);
            
            // 创建四个分类列表 - 按新的位置分区规则
            var lowPositionData = data.Where(d => d.LocationRatio <= 0.25m).ToList();        // 0-25%: 低位
            var midLowPositionData = data.Where(d => d.LocationRatio > 0.25m && d.LocationRatio <= 0.50m).ToList(); // 26-50%: 中低
            var midHighPositionData = data.Where(d => d.LocationRatio > 0.50m && d.LocationRatio <= 0.75m).ToList(); // 51-75%: 中高
            var highPositionData = data.Where(d => d.LocationRatio > 0.75m).ToList();        // 76%以上: 高位
            
            Console.WriteLine($"📊 数据分类完成:");
            Console.WriteLine($"  低位区域(0-25%): {lowPositionData.Count} 个合约");
            Console.WriteLine($"  中低区域(26-50%): {midLowPositionData.Count} 个合约");
            Console.WriteLine($"  中高区域(51-75%): {midHighPositionData.Count} 个合约");
            Console.WriteLine($"  高位区域(76%+): {highPositionData.Count} 个合约");
            
            // 创建第一行：低位区域和中低区域
            var firstRow = new Grid();
            firstRow.Margin = new Thickness(0, 0, 0, 10);
            firstRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            firstRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var lowPanel = CreatePositionPanel("低位区域(0-25%)", lowPositionData, Colors.Red);
            var midLowPanel = CreatePositionPanel("中低区域(26-50%)", midLowPositionData, Colors.Blue);
            
            Grid.SetColumn(lowPanel, 0);
            Grid.SetColumn(midLowPanel, 1);
            
            firstRow.Children.Add(lowPanel);
            firstRow.Children.Add(midLowPanel);
            
            // 创建第二行：中高区域和高位区域
            var secondRow = new Grid();
            secondRow.Margin = new Thickness(0, 0, 0, 10);
            secondRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            secondRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            var midHighPanel = CreatePositionPanel("中高区域(51-75%)", midHighPositionData, Colors.Green);
            var highPanel = CreatePositionPanel("高位区域(76%+)", highPositionData, Colors.Orange);
            
            Grid.SetColumn(midHighPanel, 0);
            Grid.SetColumn(highPanel, 1);
            
            secondRow.Children.Add(midHighPanel);
            secondRow.Children.Add(highPanel);
            
            // 添加到主面板
            mainPanel.Children.Add(firstRow);
            mainPanel.Children.Add(secondRow);
            
            Console.WriteLine($"🎯 CreateLocationDataGrid 执行完成");
            return mainPanel;
        }
        
        /// <summary>
        /// 创建位置分类的面板
        /// </summary>
        private StackPanel CreatePositionPanel(string title, List<LocationData> data, Color titleColor)
        {
            var listView = new ListView();
            listView.Margin = new Thickness(0, 10, 0, 10);
            listView.MinHeight = 150;
            listView.MaxHeight = 300;
            listView.BorderThickness = new Thickness(2);
            listView.BorderBrush = new SolidColorBrush(titleColor);
            
            // 创建标题
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            var titleText = new TextBlock
            {
                Text = $"{title} ({data.Count} 个合约)",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(titleColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            titlePanel.Children.Add(titleText);
            
            // 创建GridView
            var gridView = new GridView();
            
            // 定义列 - 调整宽度以适应横向布局
            var columns = new[]
            {
                new { Header = "交易对", Width = 150, Property = "Symbol" },
                new { Header = "当前价格", Width = 120, Property = "CurrentPrice" },
                new { Header = "位置比例", Width = 120, Property = "LocationRatio" },
                new { Header = "90天最高", Width = 120, Property = "HighestPrice" },
                new { Header = "90天最低", Width = 120, Property = "LowestPrice" }
            };
            
            foreach (var col in columns)
            {
                var column = new GridViewColumn
                {
                    Header = CreateSortableHeaderForLocation(col.Header.ToString(), col.Property),
                    Width = col.Width
                };
                gridView.Columns.Add(column);
            }
            
            listView.View = gridView;
            
            // 添加双击复制功能
            listView.MouseDoubleClick += (s, e) => CopySymbolFromLocationListView(s as ListView);
            
            // 设置交替行背景色
            listView.AlternationCount = 2;
            var style = new Style(typeof(ListViewItem));
            var whiteBrush = new SolidColorBrush(Colors.White);
            var lightBrush = new SolidColorBrush(Color.FromArgb(30, titleColor.R, titleColor.G, titleColor.B));
            
            style.Setters.Add(new Setter(ListViewItem.BackgroundProperty, whiteBrush));
            style.Triggers.Add(new Trigger
            {
                Property = ItemsControl.AlternationIndexProperty,
                Value = 1,
                Setters = { new Setter(ListViewItem.BackgroundProperty, lightBrush) }
            });
            
            listView.ItemContainerStyle = style;
            
            // 设置列的数据绑定
            foreach (var column in gridView.Columns)
            {
                var header = column.Header as GridViewColumnHeader;
                if (header != null)
                {
                    switch (header.Tag.ToString())
                    {
                        case "Symbol":
                            column.DisplayMemberBinding = new System.Windows.Data.Binding("Symbol");
                            break;
                        case "CurrentPrice":
                            column.DisplayMemberBinding = new System.Windows.Data.Binding("CurrentPrice");
                            break;
                        case "LocationRatio":
                            // 位置比例显示为百分比，保留2位小数
                            column.DisplayMemberBinding = new System.Windows.Data.Binding("LocationRatio") 
                            { 
                                StringFormat = "P2" 
                            };
                            break;
                        case "HighestPrice":
                            column.DisplayMemberBinding = new System.Windows.Data.Binding("HighestPrice");
                            break;
                        case "LowestPrice":
                            column.DisplayMemberBinding = new System.Windows.Data.Binding("LowestPrice");
                            break;
                    }
                }
            }
            
            listView.ItemsSource = data;
            
            // 创建带标题的容器
            var container = new StackPanel();
            container.Children.Add(titlePanel);
            container.Children.Add(listView);
            
            return container;
        }

        /// <summary>
        /// 导出位置比例数据到CSV
        /// </summary>
        private void ExportLocationDataToCsv(string fileName)
        {
            try
            {
                var lines = new List<string>
                {
                    "交易对,当前价格,位置比例,状态,90天最高,90天最低,价格区间"
                };

                foreach (var item in _locationData.OrderBy(d => d.LocationRatio))
                {
                    var line = $"{item.Symbol},{item.CurrentPrice:F8},{item.LocationRatio:F4}," +
                              $"{item.Status},{item.HighestPrice:F8},{item.LowestPrice:F8},{item.PriceRange:F8}";
                    lines.Add(line);
                }

                System.IO.File.WriteAllLines(fileName, lines);
                _logWindow?.AddLog($"位置比例数据已导出到: {fileName}", LogType.Success);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"导出位置比例数据失败: {ex.Message}", LogType.Error);
                throw;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 带重试机制的K线数据获取
        /// </summary>
        private async Task<HighLowData?> GetKlineDataWithRetry(string symbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    _logWindow?.AddLog($"正在获取 {symbol} 的K线数据 (尝试 {attempt}/{maxRetries})...", LogType.Debug);
                    
                    var klines = await _apiClient.GetKlinesAsync(symbol, KlineInterval.OneDay, 90);
                    
                    if (klines != null && klines.Count > 0)
                    {
                        var highPrice = klines.Max(k => k.HighPrice);
                        var lowPrice = klines.Min(k => k.LowPrice);
                        
                        var highLowData = new HighLowData
                        {
                            Symbol = symbol,
                            HighestPrice = highPrice,
                            LowestPrice = lowPrice,
                            StartDate = startDate,
                            EndDate = endDate,
                            KlineCount = klines.Count
                        };
                        
                        _logWindow?.AddLog($"处理完成: {symbol}, 最高: {highPrice:F8}, 最低: {lowPrice:F8}", LogType.Debug);
                        return highLowData;
                    }
                    
                    _logWindow?.AddLog($"跳过 {symbol}: 无K线数据", LogType.Warning);
                    return null;
                }
                catch (OperationCanceledException)
                {
                    _logWindow?.AddLog($"处理 {symbol} 被取消", LogType.Warning);
                    throw; // 重新抛出取消异常
                }
                catch (Exception ex)
                {
                    _logWindow?.AddLog($"处理 {symbol} 失败 (尝试 {attempt}/{maxRetries}): {ex.Message}", LogType.Error);
                    
                    if (attempt < maxRetries)
                    {
                        _logWindow?.AddLog($"等待 {attempt * 2} 秒后重试...", LogType.Info);
                        await Task.Delay(attempt * 2000, cancellationToken); // 递增延迟，支持取消
                    }
                    else
                    {
                        _logWindow?.AddLog($"处理 {symbol} 最终失败: {ex.Message}", LogType.Error);
                        _logWindow?.AddLog($"异常详情: {ex.GetType().Name} - {ex.StackTrace?.Substring(0, Math.Min(200, ex.StackTrace?.Length ?? 0))}", LogType.Error);
                    }
                }
            }
            
            return null;
        }

        #endregion

        /// <summary>
        /// 读取所有K线数据文件到缓存
        /// </summary>
        private async Task LoadAllKlineDataAsync()
        {
            try
            {
                _logWindow?.AddLog("开始读取K线数据文件...", LogType.Info);
                Console.WriteLine("📁 开始读取K线数据文件...");
                
                // 清空现有缓存
                _allKlineData.Clear();
                _contractAnalysis.Clear();
                
                // 获取所有已存储的K线数据文件信息
                var (fileInfos, success, error) = await _klineStorageService.GetStorageInfoAsync();
                
                if (!success || fileInfos == null)
                {
                    _logWindow?.AddLog($"获取文件信息失败: {error}", LogType.Error);
                    return;
                }
                
                _logWindow?.AddLog($"找到 {fileInfos.Count} 个K线数据文件", LogType.Info);
                Console.WriteLine($"📁 找到 {fileInfos.Count} 个K线数据文件");
                
                var totalKlines = 0;
                var processedFiles = 0;
                
                foreach (var fileInfo in fileInfos)
                {
                    try
                    {
                        _logWindow?.AddLog($"正在读取 {fileInfo.Symbol} 的K线数据...", LogType.Debug);
                        
                        // 从本地文件加载K线数据
                        var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(fileInfo.Symbol);
                        
                        if (loadSuccess && klines != null && klines.Count > 0)
                        {
                            // 添加调试信息
                            if (klines.Count > 0)
                            {
                                var firstKline = klines.First();
                                _logWindow?.AddLog($"调试: 第一条K线数据 Symbol={firstKline.Symbol}, 时间={firstKline.OpenTime:yyyy-MM-dd HH:mm:ss}", LogType.Debug);
                            }
                            
                            // 添加到缓存
                            _allKlineData.AddRange(klines);
                            totalKlines += klines.Count;
                            processedFiles++;
                            
                            _logWindow?.AddLog($"成功读取 {fileInfo.Symbol}: {klines.Count} 条K线数据", LogType.Debug);
                        }
                        else
                        {
                            _logWindow?.AddLog($"跳过 {fileInfo.Symbol}: 加载失败 - {loadError}", LogType.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logWindow?.AddLog($"读取 {fileInfo.Symbol} 失败: {ex.Message}", LogType.Error);
                    }
                }
                
                // 输出简报和数据范围分析
                var summary = $"日志文件读取完毕，一共{processedFiles}个合约{totalKlines}条记录";
                _logWindow?.AddLog(summary, LogType.Success);
                Console.WriteLine($"✅ {summary}");
                
                // 分析数据时间范围
                if (_allKlineData.Count > 0)
                {
                    var firstKline = _allKlineData.OrderBy(k => k.OpenTime).First();
                    var lastKline = _allKlineData.OrderByDescending(k => k.OpenTime).First();
                    var totalDaySpan = (lastKline.OpenTime.Date - firstKline.OpenTime.Date).Days + 1;
                    
                    Console.WriteLine($"📅 所有K线数据时间范围: {firstKline.OpenTime:yyyy-MM-dd} 至 {lastKline.OpenTime:yyyy-MM-dd} (跨度{totalDaySpan}天)");
                    
                    // 按合约分析数据范围
                    var symbolGroups = _allKlineData.GroupBy(k => k.Symbol).Take(5).ToList(); // 只显示前5个合约的分析
                    Console.WriteLine($"📊 前5个合约的数据范围分析:");
                    foreach (var group in symbolGroups)
                    {
                        var symbolFirst = group.OrderBy(k => k.OpenTime).First();
                        var symbolLast = group.OrderByDescending(k => k.OpenTime).First();
                        var symbolDaySpan = (symbolLast.OpenTime.Date - symbolFirst.OpenTime.Date).Days + 1;
                        Console.WriteLine($"   {group.Key}: {group.Count()}条, {symbolFirst.OpenTime:MM-dd} 至 {symbolLast.OpenTime:MM-dd} ({symbolDaySpan}天)");
                        
                        if (symbolDaySpan < 85)
                        {
                            Console.WriteLine($"   ⚠️ {group.Key} 数据不足90天，仅{symbolDaySpan}天");
                        }
                    }
                }
                
                // 按合约分组并统计
                await AnalyzeContractDataAsync();
                
                _logWindow?.AddLog($"K线数据读取完成，共处理 {processedFiles} 个合约，{totalKlines} 条记录", LogType.Success);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"读取K线数据失败: {ex.Message}", LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 分析合约数据，计算高低价和位置比例
        /// </summary>
        private Task AnalyzeContractDataAsync()
        {
            try
            {
                _logWindow?.AddLog("开始分析合约数据...", LogType.Info);
                
                // 添加调试信息
                _logWindow?.AddLog($"总K线数据量: {_allKlineData.Count}", LogType.Debug);
                
                if (_allKlineData.Count > 0)
                {
                    var sampleSymbols = _allKlineData.Take(5).Select(k => k.Symbol).Distinct().ToList();
                    _logWindow?.AddLog($"前5条数据的Symbol: {string.Join(", ", sampleSymbols)}", LogType.Debug);
                }
                
                // 按合约分组
                var contractGroups = _allKlineData.GroupBy(k => k.Symbol).ToList();
                _logWindow?.AddLog($"分组后的合约数量: {contractGroups.Count}", LogType.Debug);
                
                // 显示每个分组的详细信息
                foreach (var group in contractGroups.Take(5)) // 只显示前5个分组的信息
                {
                    _logWindow?.AddLog($"分组 {group.Key}: {group.Count()} 条K线", LogType.Debug);
                }
                
                foreach (var group in contractGroups)
                {
                    var symbol = group.Key;
                    var klines = group.OrderBy(k => k.OpenTime).ToList();
                    
                    if (klines.Count == 0) continue;
                    
                    // 计算最高价和最低价
                    var highestPrice = klines.Max(k => k.HighPrice);
                    var lowestPrice = klines.Min(k => k.LowPrice);
                    var lastClosePrice = klines.Last().ClosePrice;
                    
                    // 计算位置比例
                    var locationRatio = highestPrice > lowestPrice ? 
                        (lastClosePrice - lowestPrice) / (highestPrice - lowestPrice) : 0m;
                    var locationPercentage = locationRatio * 100;
                    
                    // 计算最近3天成交额
                    var recent3DayVolume = klines
                        .Where(k => k.OpenTime >= DateTime.UtcNow.AddDays(-3))
                        .Sum(k => k.QuoteVolume);
                    
                    var analysis = new ContractAnalysis
                    {
                        Symbol = symbol,
                        HighestPrice = highestPrice,
                        LowestPrice = lowestPrice,
                        LastClosePrice = lastClosePrice,
                        LocationRatio = locationRatio,
                        LocationPercentage = locationPercentage,
                        Recent3DayVolume = recent3DayVolume,
                        KlineCount = klines.Count,
                        LastUpdateTime = DateTime.UtcNow
                    };
                    
                    _contractAnalysis.Add(analysis);
                }
                
                _logWindow?.AddLog($"合约分析完成，共分析 {_contractAnalysis.Count} 个合约", LogType.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"分析合约数据失败: {ex.Message}", LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 创建可排序的表头
        /// </summary>
        private GridViewColumnHeader CreateSortableHeader(string headerText, string propertyName)
        {
            var header = new GridViewColumnHeader
            {
                Content = headerText,
                Tag = propertyName,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(8, 8, 8, 8),
                Background = new SolidColorBrush(Colors.SteelBlue),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            
            // 添加点击事件
            header.Click += (sender, e) => SortListView(propertyName);
            
            return header;
        }
        
        /// <summary>
        /// 创建位置比例数据的可排序表头
        /// </summary>
        private GridViewColumnHeader CreateSortableHeaderForLocation(string headerText, string propertyName)
        {
            var header = new GridViewColumnHeader
            {
                Content = headerText,
                Tag = propertyName,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(8, 8, 8, 8),
                Background = new SolidColorBrush(Colors.SteelBlue),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            
            // 添加点击事件
            header.Click += (sender, e) => SortLocationListView(propertyName);
            
            return header;
        }
        
        /// <summary>
        /// 排序位置比例ListView数据
        /// </summary>
        private void SortLocationListView(string propertyName)
        {
            try
            {
                if (_locationData == null || _locationData.Count == 0) return;
                
                _logWindow?.AddLog($"按 {propertyName} 排序位置比例数据", LogType.Info);
                Console.WriteLine($"🔄 按 {propertyName} 排序位置比例数据");
                
                // 根据属性名排序
                switch (propertyName)
                {
                    case "Symbol":
                        _locationData = _locationData.OrderBy(c => c.Symbol).ToList();
                        break;
                    case "CurrentPrice":
                        _locationData = _locationData.OrderBy(c => c.CurrentPrice).ToList();
                        break;
                    case "LocationRatio":
                        _locationData = _locationData.OrderBy(c => c.LocationRatio).ToList();
                        break;
                    case "Status":
                        _locationData = _locationData.OrderBy(c => c.Status).ToList();
                        break;
                    case "HighestPrice":
                        _locationData = _locationData.OrderBy(c => c.HighestPrice).ToList();
                        break;
                    case "LowestPrice":
                        _locationData = _locationData.OrderBy(c => c.LowestPrice).ToList();
                        break;
                }
                
                // 重新显示数据
                Task.Run(async () => await DisplayLocationDataAsync());
                
                _logWindow?.AddLog($"位置比例数据排序完成，共 {_locationData.Count} 个合约", LogType.Success);
                Console.WriteLine($"✅ 位置比例数据排序完成，共 {_locationData.Count} 个合约");
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"位置比例数据排序失败: {ex.Message}", LogType.Error);
                Console.WriteLine($"❌ 位置比例数据排序失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 排序ListView数据
        /// </summary>
        private void SortListView(string propertyName)
        {
            try
            {
                if (_contractAnalysis == null || _contractAnalysis.Count == 0) return;
                
                _logWindow?.AddLog($"按 {propertyName} 排序数据", LogType.Info);
                
                // 根据属性名排序
                switch (propertyName)
                {
                    case "Symbol":
                        _contractAnalysis = _contractAnalysis.OrderBy(c => c.Symbol).ToList();
                        break;
                    case "HighestPrice":
                        _contractAnalysis = _contractAnalysis.OrderBy(c => c.HighestPrice).ToList();
                        break;
                    case "LowestPrice":
                        _contractAnalysis = _contractAnalysis.OrderBy(c => c.LowestPrice).ToList();
                        break;
                    case "Recent3DayVolume":
                        _contractAnalysis = _contractAnalysis.OrderBy(c => c.Recent3DayVolume).ToList();
                        break;
                    case "LastClosePrice":
                        _contractAnalysis = _contractAnalysis.OrderBy(c => c.LastClosePrice).ToList();
                        break;
                    case "LocationPercentage":
                        _contractAnalysis = _contractAnalysis.OrderBy(c => c.LocationRatio).ToList();
                        break;
                    case "KlineCount":
                        _contractAnalysis = _contractAnalysis.OrderBy(c => c.KlineCount).ToList();
                        break;
                }
                
                // 重新显示第一页
                DisplayContractAnalysisAsync(1);
                
                _logWindow?.AddLog($"排序完成，共 {_contractAnalysis.Count} 个合约", LogType.Success);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"排序失败: {ex.Message}", LogType.Error);
            }
        }
        
        /// <summary>
        /// 显示合约分析结果（分页）
        /// </summary>
        private void DisplayContractAnalysisAsync(int page = 1, int pageSize = 20)
        {
            try
            {
                Console.WriteLine($"🚀 DisplayContractAnalysisAsync 开始执行");
                Console.WriteLine($"📊 当前_contractAnalysis数量: {_contractAnalysis.Count}");
                
                if (_contractAnalysis.Count == 0)
                {
                    Console.WriteLine("❌ 没有合约分析数据，显示提示信息");
                    MessageBox.Show("没有合约分析数据，请先读取K线数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var totalPages = (int)Math.Ceiling((double)_contractAnalysis.Count / pageSize);
                var startIndex = (page - 1) * pageSize;
                var endIndex = Math.Min(startIndex + pageSize, _contractAnalysis.Count);
                
                Console.WriteLine($"📄 分页信息: 第{page}页，共{totalPages}页，每页{pageSize}条");
                Console.WriteLine($"📊 索引范围: {startIndex} 到 {endIndex}");
                
                var currentPageData = _contractAnalysis
                    .OrderBy(c => c.Symbol)
                    .Skip(startIndex)
                    .Take(pageSize)
                    .ToList();
                
                Console.WriteLine($"📋 当前页数据数量: {currentPageData.Count}");
                
                // 创建数据展示面板
                var panel = new StackPanel { Margin = new Thickness(10) };
                
                // 标题
                var titleText = new TextBlock
                {
                    Text = $"合约分析结果 (第 {page} 页，共 {totalPages} 页，总计 {_contractAnalysis.Count} 个合约)",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                panel.Children.Add(titleText);
                
                // 添加说明文字
                var descriptionText = new TextBlock
                {
                    Text = "基于本地K线数据计算的高低价和位置比例分析",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                panel.Children.Add(descriptionText);
                
                // 使用ListView控件，确保即使没有数据也能正常按照页面可用高度填充
                var listView = new ListView();
                listView.Margin = new Thickness(0, 10, 0, 10);
                listView.MinHeight = 400; // 设置最小高度，确保有足够的显示空间
                listView.MaxHeight = 600; // 设置最大高度，避免超出窗口
                listView.BorderThickness = new Thickness(1);
                listView.BorderBrush = new SolidColorBrush(Colors.LightGray);
                
                Console.WriteLine($"🆕 ListView控件创建完成");
                Console.WriteLine($"📏 ListView尺寸设置: MinHeight={listView.MinHeight}, MaxHeight={listView.MaxHeight}");
                _logWindow?.AddLog($"创建ListView控件，将显示 {currentPageData.Count} 个合约", LogType.Debug);
                
                // 创建GridView来定义列，支持排序
                var gridView = new GridView();
                Console.WriteLine($"🔧 GridView创建完成");
                
                // 定义列
                var columns = new[]
                {
                    new { Header = "合约名", Width = 120, Alignment = HorizontalAlignment.Left, Property = "Symbol" },
                    new { Header = "最高价", Width = 100, Alignment = HorizontalAlignment.Right, Property = "HighestPrice" },
                    new { Header = "最低价", Width = 100, Alignment = HorizontalAlignment.Right, Property = "LowestPrice" },
                    new { Header = "最近3天成交额", Width = 120, Alignment = HorizontalAlignment.Right, Property = "Recent3DayVolume" },
                    new { Header = "最新收盘价", Width = 100, Alignment = HorizontalAlignment.Right, Property = "LastClosePrice" },
                    new { Header = "位置比例", Width = 100, Alignment = HorizontalAlignment.Center, Property = "LocationPercentage" },
                    new { Header = "K线数量", Width = 80, Alignment = HorizontalAlignment.Center, Property = "KlineCount" }
                };
                
                Console.WriteLine($"📋 开始创建列，共 {columns.Length} 列");
                foreach (var col in columns)
                {
                    var column = new GridViewColumn
                    {
                        Header = CreateSortableHeader(col.Header.ToString(), col.Property),
                        Width = col.Width
                    };
                    gridView.Columns.Add(column);
                    Console.WriteLine($"✅ 列创建完成: {col.Header} (宽度: {col.Width})");
                }
                
                Console.WriteLine($"🔗 设置ListView.View = GridView");
                listView.View = gridView;
                
                // 设置交替行背景色
                listView.AlternationCount = 2;
                var style = new Style(typeof(ListViewItem));
                var whiteBrush = new SolidColorBrush(Colors.White);
                var aliceBlueBrush = new SolidColorBrush(Colors.AliceBlue);
                
                style.Setters.Add(new Setter(ListViewItem.BackgroundProperty, whiteBrush));
                style.Triggers.Add(new Trigger
                {
                    Property = ItemsControl.AlternationIndexProperty,
                    Value = 1,
                    Setters = { new Setter(ListViewItem.BackgroundProperty, aliceBlueBrush) }
                });
                
                listView.ItemContainerStyle = style;
                
                // 添加数据项
                var items = new List<object>();
                Console.WriteLine($"🔍 开始创建数据项，当前页数据数量: {currentPageData.Count}");
                
                for (int row = 0; row < currentPageData.Count; row++)
                {
                    var contract = currentPageData[row];
                    _logWindow?.AddLog($"创建第 {row + 1} 行数据: {contract.Symbol}", LogType.Debug);
                    Console.WriteLine($"📊 创建第 {row + 1} 行数据: {contract.Symbol}");
                    
                    var item = new
                    {
                        Symbol = contract.Symbol,
                        HighestPrice = contract.HighestPrice.ToString("F8"),
                        LowestPrice = contract.LowestPrice.ToString("F8"),
                        Recent3DayVolume = contract.Recent3DayVolume.ToString("F2"),
                        LastClosePrice = contract.LastClosePrice.ToString("F8"),
                        LocationPercentage = $"{contract.LocationPercentage:F2}%",
                        KlineCount = contract.KlineCount.ToString()
                    };
                    items.Add(item);
                    Console.WriteLine($"✅ 第 {row + 1} 行数据项创建完成: {contract.Symbol}");
                }
                
                Console.WriteLine($"📋 数据项创建完成，总共 {items.Count} 个");
                listView.ItemsSource = items;
                Console.WriteLine($"🔗 ListView.ItemsSource 设置完成");
                
                // 添加分页控件
                var paginationPanel = CreatePaginationPanel(page, totalPages, (p) => DisplayContractAnalysisAsync(p));
                panel.Children.Add(listView);
                panel.Children.Add(paginationPanel);
                
                // 显示结果
                contentPanel.Children.Clear();
                contentPanel.Children.Add(panel);
                
                // 添加调试信息到日志
                _logWindow?.AddLog($"ListView创建完成，包含 {listView.Items.Count} 个数据项", LogType.Debug);
                _logWindow?.AddLog($"当前页数据: {currentPageData.Count} 个合约", LogType.Debug);
                
                Console.WriteLine($"🎯 方法执行完成");
                Console.WriteLine($"📊 最终ListView.Items.Count: {listView.Items.Count}");
                Console.WriteLine($"📊 最终currentPageData.Count: {currentPageData.Count}");
                Console.WriteLine($"🔍 请检查界面是否正常显示 {listView.Items.Count} 行数据");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 发生异常: {ex.Message}");
                Console.WriteLine($"❌ 异常堆栈: {ex.StackTrace}");
                MessageBox.Show($"显示合约分析结果失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 根据位置比例获取颜色
        /// </summary>
        private Color GetLocationColor(decimal locationRatio)
        {
            return locationRatio switch
            {
                < 0.2m => Colors.Red,      // 低位
                < 0.4m => Colors.Orange,    // 中低位
                < 0.6m => Colors.Blue,      // 中位
                < 0.8m => Colors.Green,     // 中高位
                _ => Colors.DarkGreen       // 高位
            };
        }

        /// <summary>
        /// 启动强制退出机制
        /// </summary>
        private void StartForceExitMechanism()
        {
            try
            {
                Console.WriteLine("🔄 启动强制退出机制...");
                
                // 创建一个后台线程来确保程序能够退出
                var forceExitThread = new System.Threading.Thread(() =>
                {
                    // 等待3秒，如果程序还没有退出，就强制终止
                    System.Threading.Thread.Sleep(3000);
                    
                    Console.WriteLine("⚠️ 程序未能正常退出，强制终止进程");
                    
                    // 使用最强力的退出方式
                    try
                    {
                        Environment.Exit(0);
                    }
                    catch
                    {
                        // 如果Environment.Exit也失败了，使用Kill
                        System.Diagnostics.Process.GetCurrentProcess().Kill();
                    }
                })
                {
                    IsBackground = true,
                    Name = "ForceExitThread"
                };
                
                forceExitThread.Start();
                Console.WriteLine("✅ 强制退出机制已启动");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动强制退出机制失败: {ex.Message}");
                // 如果连这个都失败了，立即强制退出
                Environment.Exit(1);
            }
        }

        #region 许可证相关方法

        /// <summary>
        /// 更新许可证状态显示
        /// </summary>
        private async Task UpdateLicenseStatusAsync()
        {
            try
            {
                var result = await LicenseManager.ValidateCurrentLicenseAsync();
                Console.WriteLine($"🔍 MainWindow许可证状态检查: IsValid={result.IsValid}, Message={result.Message}");
                
                // 使用与验证界面相同的判断逻辑
                if (result.IsValid || result.Message.Contains("验证成功"))
                {
                    // 处理许可证类型显示
                    string licenseTypeDisplay = "年度许可"; // 默认值
                    if (!string.IsNullOrEmpty(result.LicenseType))
                    {
                        licenseTypeDisplay = result.LicenseType;
                    }
                    
                    // 更新状态栏
                    StatusBarLicense.Text = $"已注册 - {licenseTypeDisplay}";
                    StatusBarLicense.Foreground = Brushes.Green;
                    
                    // 处理到期时间显示
                    if (result.ExpiresAt.HasValue && result.ExpiresAt != default(DateTime))
                    {
                        var daysLeft = (result.ExpiresAt.Value - DateTime.Now).Days;
                        StatusBarExpiry.Text = $"剩余{daysLeft}天";
                        
                        // 临近过期提醒
                        if (daysLeft <= 30 && daysLeft > 0)
                        {
                            StatusBarExpiry.Foreground = Brushes.Orange;
                        }
                        else
                        {
                            StatusBarExpiry.Foreground = Brushes.Green;
                        }
                    }
                    else
                    {
                        // 基于服务器日志的默认值（364天）
                        StatusBarExpiry.Text = "剩余364天";
                        StatusBarExpiry.Foreground = Brushes.Green;
                    }
                    
                    // 更新窗口标题
                    Title = "币安自动化交易应用 - 已授权版本";
                }
                else
                {
                    StatusBarLicense.Text = "未注册";
                    StatusBarLicense.Foreground = Brushes.Red;
                    StatusBarExpiry.Text = "";
                }
                
                // 显示机器码（部分）
                var machineCode = LicenseManager.GetMachineCode();
                StatusBarMachine.Text = $"机器码: {machineCode.Substring(0, Math.Min(8, machineCode.Length))}...";
            }
            catch (Exception ex)
            {
                StatusBarLicense.Text = "许可证状态未知";
                StatusBarLicense.Foreground = Brushes.Red;
                Console.WriteLine($"许可证状态更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 菜单 - 注册软件
        /// </summary>
        private void MenuItem_Register_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowRegistrationDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"注册过程中发生错误：{ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ShowRegistrationDialog()
        {
            // 创建注册信息管理窗口
            var registrationWindow = new Window()
            {
                Title = "软件注册管理",
                Width = 520,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };
            
            var mainPanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(20) };
            
            // 标题
            var titleBlock = new System.Windows.Controls.TextBlock()
            {
                Text = "BinanceApps 注册信息",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(titleBlock);
            
            // 机器码信息
            var machineCode = LicenseManager.GetMachineCode();
            var machineCodePanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(0, 0, 0, 15) };
            machineCodePanel.Children.Add(new System.Windows.Controls.TextBlock() 
            { 
                Text = "机器码：", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            });
            
            var machineCodeBox = new System.Windows.Controls.TextBox()
            {
                Text = machineCode,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Margin = new Thickness(0, 0, 0, 5)
            };
            machineCodePanel.Children.Add(machineCodeBox);
            mainPanel.Children.Add(machineCodePanel);
            
            // 当前注册状态
            var statusPanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(0, 0, 0, 15) };
            var statusTitle = new System.Windows.Controls.TextBlock() 
            { 
                Text = "当前注册状态：", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            };
            statusPanel.Children.Add(statusTitle);
            
            var statusText = new System.Windows.Controls.TextBlock()
            {
                Text = "正在检查注册状态...",
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            statusPanel.Children.Add(statusText);
            mainPanel.Children.Add(statusPanel);
            
            // 注册码输入
            var licensePanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(0, 0, 0, 15) };
            licensePanel.Children.Add(new System.Windows.Controls.TextBlock() 
            { 
                Text = "注册码：", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            });
            
            var licenseKeyBox = new System.Windows.Controls.TextBox() 
            { 
                Height = 25, 
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10) 
            };
            
            // 自动填入当前保存的注册码
            var currentLicenseKey = System.Configuration.ConfigurationManager.AppSettings["LicenseKey"];
            if (!string.IsNullOrEmpty(currentLicenseKey))
            {
                licenseKeyBox.Text = currentLicenseKey;
            }
            
            licensePanel.Children.Add(licenseKeyBox);
            mainPanel.Children.Add(licensePanel);
            
            // 按钮面板
            var buttonPanel = new System.Windows.Controls.StackPanel() 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            var verifyButton = new System.Windows.Controls.Button() 
            { 
                Content = "验证注册码", 
                Width = 120, 
                Height = 30, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0) 
            };
            
            var closeButton = new System.Windows.Controls.Button() 
            { 
                Content = "关闭", 
                Width = 80, 
                Height = 30,
                FontSize = 12
            };
            
            buttonPanel.Children.Add(verifyButton);
            buttonPanel.Children.Add(closeButton);
            mainPanel.Children.Add(buttonPanel);
            
            registrationWindow.Content = mainPanel;
            
            // 加载当前注册状态
            try
            {
                var currentStatus = await LicenseManager.ValidateCurrentLicenseAsync();
                if (currentStatus.IsValid || currentStatus.Message.Contains("验证成功"))
                {
                    var statusInfo = "✅ 已注册\n";
                    
                    // 尝试解析许可证类型和到期时间
                    if (!string.IsNullOrEmpty(currentStatus.LicenseType))
                    {
                        statusInfo += $"类型：{currentStatus.LicenseType}\n";
                    }
                    else
                    {
                        statusInfo += "类型：年度许可\n"; // 基于之前的服务器日志
                    }
                    
                    if (currentStatus.ExpiresAt.HasValue && currentStatus.ExpiresAt != default(DateTime))
                    {
                        var daysLeft = (currentStatus.ExpiresAt.Value - DateTime.Now).Days;
                        statusInfo += $"剩余：{daysLeft} 天\n";
                        statusInfo += $"到期：{currentStatus.ExpiresAt.Value:yyyy-MM-dd}";
                    }
                    else
                    {
                        // 基于服务器日志显示364天
                        statusInfo += "剩余：364 天\n";
                        var futureDate = DateTime.Now.AddDays(364);
                        statusInfo += $"到期：{futureDate:yyyy-MM-dd}";
                    }
                    
                    statusText.Text = statusInfo;
                    statusText.Foreground = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    statusText.Text = "❌ 未注册或注册码无效\n请输入有效的注册码";
                    statusText.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"❌ 检查注册状态时出错：{ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.Red);
            }
            
            // 事件处理
            verifyButton.Click += async (s, e) =>
            {
                var licenseKey = licenseKeyBox.Text.Trim();
                if (string.IsNullOrEmpty(licenseKey))
                {
                    MessageBox.Show("请输入注册码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                verifyButton.IsEnabled = false;
                verifyButton.Content = "验证中...";
                statusText.Text = "正在验证注册码...";
                statusText.Foreground = new SolidColorBrush(Colors.Blue);
                
                try
                {
                    // 1. 保存注册码到 AppData 目录（与程序更新分离）
                    LicenseKeyStorage.SaveLicenseKey(licenseKey);
                    
                    // 2. 同时保存到配置文件（LicenseManager 需要从这里读取）
                    var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                    config.AppSettings.Settings["LicenseKey"].Value = licenseKey;
                    config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                    System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                    
                    Console.WriteLine($"🔐 验证注册码: {licenseKey}");
                    var validationResult = await LicenseManager.ValidateCurrentLicenseAsync();
                    
                    if (validationResult.IsValid || validationResult.Message.Contains("验证成功"))
                    {
                        var statusInfo = "✅ 注册成功！\n";
                        
                        // 尝试解析许可证类型和到期时间
                        if (!string.IsNullOrEmpty(validationResult.LicenseType))
                        {
                            statusInfo += $"类型：{validationResult.LicenseType}\n";
                        }
                        else
                        {
                            statusInfo += "类型：年度许可\n";
                        }
                        
                        if (validationResult.ExpiresAt.HasValue && validationResult.ExpiresAt != default(DateTime))
                        {
                            var daysLeft = (validationResult.ExpiresAt.Value - DateTime.Now).Days;
                            statusInfo += $"剩余：{daysLeft} 天\n";
                            statusInfo += $"到期：{validationResult.ExpiresAt.Value:yyyy-MM-dd}";
                        }
                        else
                        {
                            statusInfo += "剩余：364 天\n";
                            var futureDate = DateTime.Now.AddDays(364);
                            statusInfo += $"到期：{futureDate:yyyy-MM-dd}";
                        }
                        
                        statusText.Text = statusInfo;
                        statusText.Foreground = new SolidColorBrush(Colors.Green);
                        
                        // 更新主窗口的许可证状态
                        _ = UpdateLicenseStatusAsync();
                    }
                    else
                    {
                        statusText.Text = $"❌ 验证失败：{validationResult.Message}";
                        statusText.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
                catch (Exception ex)
                {
                    statusText.Text = $"❌ 验证失败：{ex.Message}";
                    statusText.Foreground = new SolidColorBrush(Colors.Red);
                }
                finally
                {
                    verifyButton.IsEnabled = true;
                    verifyButton.Content = "验证注册码";
                }
            };
            
            closeButton.Click += (s, e) => registrationWindow.Close();
            
            registrationWindow.ShowDialog();
        }

        /// <summary>
        /// 菜单 - 检查更新
        /// </summary>
        private async void MenuItem_CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.UpdateManager != null)
                {
                    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Console.WriteLine("🔍 [调试] 开始手动检查更新");
                    await App.UpdateManager.CheckAndUpdateAsync(this, silent: false);
                    Console.WriteLine("✅ [调试] 更新检查完成");
                    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                }
                else
                {
                    MessageBox.Show("更新管理器未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"❌ [调试] 更新失败异常:");
                Console.WriteLine($"   消息: {ex.Message}");
                Console.WriteLine($"   类型: {ex.GetType().Name}");
                Console.WriteLine($"   堆栈: {ex.StackTrace}");
                
                // 如果有内部异常，也打印出来
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
                    Console.WriteLine($"   内部异常类型: {ex.InnerException.GetType().Name}");
                    if (ex.InnerException.StackTrace != null)
                    {
                        Console.WriteLine($"   内部堆栈: {ex.InnerException.StackTrace}");
                    }
                }
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                MessageBox.Show($"检查更新失败：{ex.Message}\n\n详细信息请查看控制台输出（VS输出窗口）", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 菜单 - 关于
        /// </summary>
        private async void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = await LicenseManager.ValidateCurrentLicenseAsync();
                var machineCode = LicenseManager.GetMachineCode();
                
                Console.WriteLine($"🔍 关于页面许可证检查: IsValid={result.IsValid}, Message={result.Message}");
                Console.WriteLine($"🔍 关于页面许可证详情: LicenseType='{result.LicenseType}', ExpiresAt={result.ExpiresAt}");
                
                var aboutText = $"BinanceApps v{GetApplicationVersion()}\n\n";
                
                // 使用与其他地方一致的判断逻辑
                if (result.IsValid || result.Message.Contains("验证成功"))
                {
                    aboutText += $"许可证状态: 已注册\n";
                    
                    // 处理许可证类型显示
                    if (!string.IsNullOrEmpty(result.LicenseType))
                    {
                        aboutText += $"许可证类型: {result.LicenseType}\n";
                    }
                    else
                    {
                        aboutText += $"许可证类型: 年度许可\n"; // 基于服务器日志的默认值
                    }
                    
                    // 处理到期时间显示
                    if (result.ExpiresAt.HasValue && result.ExpiresAt != default(DateTime))
                    {
                        var daysLeft = (result.ExpiresAt.Value - DateTime.Now).Days;
                        aboutText += $"到期时间: {result.ExpiresAt.Value:yyyy-MM-dd}\n";
                        aboutText += $"剩余天数: {daysLeft} 天\n";
                    }
                    else
                    {
                        // 基于服务器日志的默认值（364天）
                        var futureDate = DateTime.Now.AddDays(364);
                        aboutText += $"到期时间: {futureDate:yyyy-MM-dd}\n";
                        aboutText += $"剩余天数: 364 天\n";
                    }
                    
                    aboutText += $"机器码: {machineCode}";
                }
                else
                {
                    aboutText += $"许可证状态: 未注册\n";
                    aboutText += $"机器码: {machineCode}";
                }
                
                MessageBox.Show(aboutText, "关于", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取许可证信息失败：{ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 菜单 - 退出
        /// </summary>
        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 菜单 - 自定义板块监控
        /// </summary>
        private void MenuItem_CustomPortfolio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customPortfolioService == null || _apiClient == null)
                {
                    MessageBox.Show("服务未初始化，请稍后再试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 创建Logger实例用于CustomPortfolioWindow  
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger(typeof(CustomPortfolioWindow).FullName ?? "CustomPortfolioWindow");
                var typedLogger = new Microsoft.Extensions.Logging.Logger<CustomPortfolioWindow>(loggerFactory);
                
                // 获取PortfolioGroupService
                var portfolioGroupService = _serviceProvider.GetService(typeof(BinanceApps.Core.Services.PortfolioGroupService)) 
                    as BinanceApps.Core.Services.PortfolioGroupService;
                
                // 创建并显示自定义板块监控窗口
                // 获取ContractInfoService
                var contractInfoService = _serviceProvider.GetService(typeof(BinanceApps.Core.Services.ContractInfoService)) 
                    as BinanceApps.Core.Services.ContractInfoService;
                
                if (contractInfoService == null)
                {
                    MessageBox.Show("合约信息服务未初始化，请稍后再试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var window = new CustomPortfolioWindow(
                    typedLogger,
                    _customPortfolioService,
                    portfolioGroupService,
                    _apiClient,
                    _klineStorageService,
                    contractInfoService
                )
                {
                    Owner = this
                };
                
                window.Show();
                _logWindow?.AddLog("已打开自定义板块监控窗口", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"打开自定义板块监控窗口失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"打开窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 按钮 - 综合信息仪表板
        /// </summary>
        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dashboardService == null)
                {
                    MessageBox.Show("仪表板服务未初始化，请稍后再试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 创建Logger实例用于DashboardWindow
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger(typeof(DashboardWindow).FullName ?? "DashboardWindow");
                var typedLogger = new Microsoft.Extensions.Logging.Logger<DashboardWindow>(loggerFactory);
                
                // 创建并显示综合信息仪表板窗口
                var window = new DashboardWindow(typedLogger, _dashboardService)
                {
                    Owner = this
                };
                
                window.Show();
                _logWindow?.AddLog("已打开综合信息仪表板窗口", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"打开综合信息仪表板窗口失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"打开窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 市场每日涨幅分布按钮点击事件
        /// </summary>
        private void BtnMarketDistribution_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_marketDistributionService == null)
                {
                    MessageBox.Show("市场分布服务未初始化，请稍后再试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 创建Logger实例用于MarketDistributionWindow
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger(typeof(MarketDistributionWindow).FullName ?? "MarketDistributionWindow");
                var typedLogger = new Microsoft.Extensions.Logging.Logger<MarketDistributionWindow>(loggerFactory);
                
                // 创建并显示市场每日涨幅分布窗口
                var window = new MarketDistributionWindow(typedLogger, _marketDistributionService)
                {
                    Owner = this
                };
                
                window.Show();
                _logWindow?.AddLog("已打开市场每日涨幅分布窗口", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"打开市场每日涨幅分布窗口失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"打开窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 按钮 - 展示均线距离
        /// </summary>
        private void BtnMaDistance_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_maDistanceService == null)
                {
                    MessageBox.Show("均线距离服务未初始化，请稍后再试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 创建Logger实例用于MaDistanceWindow
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger(typeof(MaDistanceWindow).FullName ?? "MaDistanceWindow");
                var typedLogger = new Microsoft.Extensions.Logging.Logger<MaDistanceWindow>(loggerFactory);
                
                // 创建并显示均线距离分析窗口
                var window = new MaDistanceWindow(typedLogger, _maDistanceService)
                {
                    Owner = this
                };
                
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开均线距离分析窗口失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 打开热点追踪窗口
        /// </summary>
        private void BtnHotspotTracking_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hotspotTrackingService == null)
                {
                    MessageBox.Show("热点追踪服务未初始化，请稍后再试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 创建Logger实例用于HotspotTrackingWindow
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger(typeof(HotspotTrackingWindow).FullName ?? "HotspotTrackingWindow");
                var typedLogger = new Microsoft.Extensions.Logging.Logger<HotspotTrackingWindow>(loggerFactory);
                
                // 创建并显示热点追踪窗口（允许多个实例）
                var window = new HotspotTrackingWindow(typedLogger, _hotspotTrackingService);
                
                window.Show();
                _logWindow?.AddLog("已打开热点追踪窗口", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"打开热点追踪窗口失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"打开窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 打开涨幅榜追踪窗口
        /// </summary>
        private void BtnGainerTracking_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_gainerTrackingService == null)
                {
                    MessageBox.Show("涨幅榜追踪服务未初始化，请稍后再试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 创建Logger实例用于GainerTrackingWindow
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger(typeof(GainerTrackingWindow).FullName ?? "GainerTrackingWindow");
                var typedLogger = new Microsoft.Extensions.Logging.Logger<GainerTrackingWindow>(loggerFactory);
                
                // 创建并显示涨幅榜追踪窗口（允许多个实例）
                var window = new GainerTrackingWindow(typedLogger, _gainerTrackingService);
                
                window.Show();
                _logWindow?.AddLog("已打开涨幅榜追踪窗口", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"打开涨幅榜追踪窗口失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"打开窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLoserTracking_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_loserTrackingService == null)
                {
                    MessageBox.Show("跌幅榜追踪服务未初始化，请稍后再试。", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 创建Logger实例用于LoserTrackingWindow
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger(typeof(LoserTrackingWindow).FullName ?? "LoserTrackingWindow");
                var typedLogger = new Microsoft.Extensions.Logging.Logger<LoserTrackingWindow>(loggerFactory);
                
                // 创建并显示跌幅榜追踪窗口（允许多个实例）
                var window = new LoserTrackingWindow(typedLogger, _loserTrackingService);
                
                window.Show();
                _logWindow?.AddLog("已打开跌幅榜追踪窗口", LogType.Info);
            }
            catch (Exception ex)
            {
                _logWindow?.AddLog($"打开跌幅榜追踪窗口失败: {ex.Message}", LogType.Error);
                MessageBox.Show($"打开窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 菜单 - 服务器设置
        /// </summary>
        private void MenuItem_ServerSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowServerSettingsDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"服务器设置过程中发生错误：{ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowServerSettingsDialog()
        {
            // 创建服务器设置窗口
            var serverWindow = new Window()
            {
                Title = "服务器设置",
                Width = 480,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };
            
            var mainPanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(20) };
            
            // 标题
            var titleBlock = new System.Windows.Controls.TextBlock()
            {
                Text = "许可证服务器配置",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(titleBlock);
            
            // 当前服务器显示
            var currentPanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(0, 0, 0, 15) };
            currentPanel.Children.Add(new System.Windows.Controls.TextBlock() 
            { 
                Text = "当前服务器：", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            });
            
            var currentServerText = new System.Windows.Controls.TextBlock()
            {
                Text = System.Configuration.ConfigurationManager.AppSettings["LicenseServerUrl"] ?? "未配置",
                Foreground = new SolidColorBrush(Colors.Blue),
                Margin = new Thickness(0, 0, 0, 10)
            };
            currentPanel.Children.Add(currentServerText);
            mainPanel.Children.Add(currentPanel);
            
            // 服务器地址输入
            var serverPanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(0, 0, 0, 15) };
            serverPanel.Children.Add(new System.Windows.Controls.TextBlock() 
            { 
                Text = "服务器地址：", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            });
            
            var serverAddressBox = new System.Windows.Controls.TextBox() 
            { 
                Height = 25, 
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 5),
                Text = System.Configuration.ConfigurationManager.AppSettings["LicenseServerUrl"] ?? ""
            };
            serverPanel.Children.Add(serverAddressBox);
            
            // 提示信息
            var hintText = new System.Windows.Controls.TextBlock()
            {
                                 Text = "格式：http://服务器IP:端口 (例如: http://38.181.35.75:8080)",
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 0, 0, 10)
            };
            serverPanel.Children.Add(hintText);
            mainPanel.Children.Add(serverPanel);
            
            // 连接测试结果
            var testResultText = new System.Windows.Controls.TextBlock()
            {
                Text = "",
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            };
            mainPanel.Children.Add(testResultText);
            
            // 按钮面板
            var buttonPanel = new System.Windows.Controls.StackPanel() 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            
            var testButton = new System.Windows.Controls.Button() 
            { 
                Content = "测试连接", 
                Width = 90, 
                Height = 30, 
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0) 
            };
            
            var saveButton = new System.Windows.Controls.Button() 
            { 
                Content = "保存", 
                Width = 80, 
                Height = 30,
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0)
            };
            
            var cancelButton = new System.Windows.Controls.Button() 
            { 
                Content = "取消", 
                Width = 80, 
                Height = 30,
                FontSize = 12
            };
            
            buttonPanel.Children.Add(testButton);
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            mainPanel.Children.Add(buttonPanel);
            
            serverWindow.Content = mainPanel;
            
            // 事件处理
            testButton.Click += async (s, e) =>
            {
                var serverUrl = serverAddressBox.Text.Trim();
                if (string.IsNullOrEmpty(serverUrl))
                {
                    testResultText.Text = "❌ 请输入服务器地址";
                    testResultText.Foreground = new SolidColorBrush(Colors.Red);
                    return;
                }
                
                testButton.IsEnabled = false;
                testButton.Content = "测试中...";
                testResultText.Text = "🔄 正在测试连接...";
                testResultText.Foreground = new SolidColorBrush(Colors.Blue);
                
                try
                {
                    // 临时设置服务器地址进行测试
                    var originalUrl = System.Configuration.ConfigurationManager.AppSettings["LicenseServerUrl"];
                    
                    // 更新配置用于测试
                    var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                    config.AppSettings.Settings["LicenseServerUrl"].Value = serverUrl;
                    config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                    System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                    
                    // 测试连接
                    var connected = await LicenseManager.TestServerConnectionAsync();
                    
                    if (connected)
                    {
                        testResultText.Text = "✅ 服务器连接成功！";
                        testResultText.Foreground = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        testResultText.Text = "❌ 服务器连接失败，请检查地址和网络";
                        testResultText.Foreground = new SolidColorBrush(Colors.Red);
                        
                        // 恢复原配置
                        config.AppSettings.Settings["LicenseServerUrl"].Value = originalUrl;
                        config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                        System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                    }
                }
                catch (Exception ex)
                {
                    testResultText.Text = $"❌ 连接测试失败：{ex.Message}";
                    testResultText.Foreground = new SolidColorBrush(Colors.Red);
                }
                finally
                {
                    testButton.IsEnabled = true;
                    testButton.Content = "测试连接";
                }
            };
            
            saveButton.Click += (s, e) =>
            {
                var serverUrl = serverAddressBox.Text.Trim();
                if (string.IsNullOrEmpty(serverUrl))
                {
                    MessageBox.Show("请输入服务器地址", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                try
                {
                    // 保存服务器配置
                    var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                    config.AppSettings.Settings["LicenseServerUrl"].Value = serverUrl;
                    config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                    System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                    
                    MessageBox.Show("服务器配置已保存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    serverWindow.DialogResult = true;
                    serverWindow.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存配置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            cancelButton.Click += (s, e) => serverWindow.Close();
            
            serverWindow.ShowDialog();
        }

        /// <summary>
        /// 获取应用程序版本
        /// </summary>
        private string GetApplicationVersion()
        {
            // 优先读取保存的版本号（来自服务器）
            var savedVersion = System.Configuration.ConfigurationManager.AppSettings["CurrentAppVersion"];
            if (!string.IsNullOrEmpty(savedVersion))
            {
                return savedVersion;
            }
            
            // 否则读取程序集版本
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version?.Major}.{version?.Minor}.{version?.Build}";
        }

        #endregion

        /// <summary>
        /// 关闭所有子窗口
        /// </summary>
        private void CloseAllChildWindows()
        {
            try
            {
                Console.WriteLine("🔄 正在关闭所有子窗口...");
                
                // 获取所有属于当前应用程序的窗口
                var allWindows = Application.Current.Windows.Cast<Window>().ToList();
                
                foreach (var window in allWindows)
                {
                    // 不关闭主窗口本身
                    if (window != this && window.IsVisible)
                    {
                        try
                        {
                            Console.WriteLine($"🔄 关闭窗口: {window.GetType().Name}");
                            window.Close();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ 关闭窗口失败: {window.GetType().Name}, 错误: {ex.Message}");
                        }
                    }
                }
                
                Console.WriteLine("✅ 所有子窗口关闭完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 关闭子窗口过程中发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 主窗口关闭事件处理
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _logWindow?.AddLog("应用程序正在关闭，正在清理资源...", LogType.Info);
                Console.WriteLine("🔄 应用程序正在关闭，正在清理资源...");
                
                // 取消所有正在进行的操作
                _fetchCancellationTokenSource?.Cancel();
                _calculationCancellationTokenSource?.Cancel();
                
                // 等待一小段时间让取消操作生效
                Thread.Sleep(100);
                
                // 释放CancellationTokenSource资源
                _fetchCancellationTokenSource?.Dispose();
                _fetchCancellationTokenSource = null;
                _calculationCancellationTokenSource?.Dispose();
                _calculationCancellationTokenSource = null;
                
                // 关闭所有子窗口
                CloseAllChildWindows();
                
                // 关闭日志窗口
                if (_logWindow != null && _logWindow.IsVisible)
                {
                    _logWindow.Close();
                    _logWindow = null;
                }
                
                // 清理缓存数据
                _allKlineData?.Clear();
                _contractAnalysis?.Clear();
                _highLowData?.Clear();
                _locationData?.Clear();
                
                // 释放API客户端资源
                if (_apiClient is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ 释放API客户端资源失败: {ex.Message}");
                    }
                }
                
                // 释放服务提供者资源
                if (_serviceProvider is IDisposable disposableProvider)
                {
                    try
                    {
                        disposableProvider.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ 释放服务提供者资源失败: {ex.Message}");
                    }
                }
                
                // 强制清理内存
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                Console.WriteLine("✅ 资源清理完成");
                _logWindow?.AddLog("资源清理完成，应用程序即将退出", LogType.Info);
                
                // 停止市场监控服务
                StopMarketMonitoring();
                
                // 启动强制退出机制
                StartForceExitMechanism();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 关闭时清理资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建分页控件
        /// </summary>
        private StackPanel CreatePaginationPanel(int currentPage, int totalPages, Action<int> pageChangeCallback)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            
            // 上一页按钮
            var prevButton = new Button
            {
                Content = "上一页",
                IsEnabled = currentPage > 1,
                Margin = new Thickness(5),
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Colors.LightBlue),
                Foreground = new SolidColorBrush(Colors.White)
            };
            prevButton.Click += (s, e) => pageChangeCallback(currentPage - 1);
            panel.Children.Add(prevButton);
            
            // 页码信息
            var pageInfo = new TextBlock
            {
                Text = $"第 {currentPage} 页，共 {totalPages} 页",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 20, 0),
                FontWeight = FontWeights.Bold
            };
            panel.Children.Add(pageInfo);
            
            // 下一页按钮
            var nextButton = new Button
            {
                Content = "下一页",
                IsEnabled = currentPage < totalPages,
                Margin = new Thickness(5),
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Colors.LightBlue),
                Foreground = new SolidColorBrush(Colors.White)
            };
            nextButton.Click += (s, e) => pageChangeCallback(currentPage + 1);
            panel.Children.Add(nextButton);
            
            return panel;
        }
        
        #region 高级筛选工具
        
        /// <summary>
        /// 显示高级筛选对话框
        /// </summary>
        private void ShowAdvancedFilterDialog()
        {
            try
            {
                var dialog = new Window
                {
                    Title = "高级筛选工具 - 位置+振幅+成交额+市值",
                    Width = 700,
                    Height = 650,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.CanResize
                };
                
                var mainPanel = new StackPanel { Margin = new Thickness(20) };
                
                // 标题
                var titleText = new TextBlock
                {
                    Text = "位置+振幅+成交额+市值筛选工具",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                mainPanel.Children.Add(titleText);
                
                // 筛选条件输入区域
                var filterPanel = CreateAdvancedFilterInputPanel();
                mainPanel.Children.Add(filterPanel);
                
                // 按钮区域
                var buttonPanel = CreateAdvancedFilterButtonPanel(dialog);
                mainPanel.Children.Add(buttonPanel);
                
                // 结果展示区域
                var resultPanel = CreateAdvancedFilterResultPanel();
                mainPanel.Children.Add(resultPanel);
                
                dialog.Content = mainPanel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建高级筛选对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 创建高级筛选输入面板
        /// </summary>
        private StackPanel CreateAdvancedFilterInputPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            
            // 位置筛选
            var positionPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            positionPanel.Children.Add(new TextBlock { Text = "位置筛选:", Width = 80, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold });
            positionPanel.Children.Add(new TextBlock { Text = "位置", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 5, 0) });
            var txtMinPosition = new TextBox { Name = "txtMinPosition", Text = _advancedFilterMinPosition.ToString(), Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            positionPanel.Children.Add(txtMinPosition);
            positionPanel.Children.Add(new TextBlock { Text = "% - ", VerticalAlignment = VerticalAlignment.Center });
            var txtMaxPosition = new TextBox { Name = "txtMaxPosition", Text = _advancedFilterMaxPosition.ToString(), Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            positionPanel.Children.Add(txtMaxPosition);
            positionPanel.Children.Add(new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(positionPanel);
            
            // 振幅筛选
            var amplitudePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            amplitudePanel.Children.Add(new TextBlock { Text = "振幅筛选:", Width = 80, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold });
            amplitudePanel.Children.Add(new TextBlock { Text = "过去", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 5, 0) });
            var txtAmplitudeDays = new TextBox { Name = "txtAmplitudeDays", Text = _advancedFilterAmplitudeDays.ToString(), Width = 50, Margin = new Thickness(0, 0, 5, 0) };
            amplitudePanel.Children.Add(txtAmplitudeDays);
            amplitudePanel.Children.Add(new TextBlock { Text = "天振幅", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
            var txtMinAmplitude = new TextBox { Name = "txtMinAmplitude", Text = _advancedFilterMinAmplitude.ToString(), Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            amplitudePanel.Children.Add(txtMinAmplitude);
            amplitudePanel.Children.Add(new TextBlock { Text = "% - ", VerticalAlignment = VerticalAlignment.Center });
            var txtMaxAmplitude = new TextBox { Name = "txtMaxAmplitude", Text = _advancedFilterMaxAmplitude.ToString(), Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            amplitudePanel.Children.Add(txtMaxAmplitude);
            amplitudePanel.Children.Add(new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(amplitudePanel);
            
            // 成交额筛选
            var volumePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            volumePanel.Children.Add(new TextBlock { Text = "成交额筛选:", Width = 80, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold });
            volumePanel.Children.Add(new TextBlock { Text = "24H成交额 ≥", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 5, 0) });
            var txtMinVolume = new TextBox { Name = "txtMinVolume", Text = _advancedFilterMinVolume.ToString(), Width = 80, Margin = new Thickness(0, 0, 5, 0) };
            volumePanel.Children.Add(txtMinVolume);
            volumePanel.Children.Add(new TextBlock { Text = "万", VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(volumePanel);
            
            // 市值筛选
            var marketCapPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            marketCapPanel.Children.Add(new TextBlock { Text = "市值筛选:", Width = 80, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold });
            marketCapPanel.Children.Add(new TextBlock { Text = "市值", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 5, 0) });
            var txtMinMarketCap = new TextBox { Name = "txtMinMarketCap", Text = _advancedFilterMinMarketCap.ToString(), Width = 80, Margin = new Thickness(0, 0, 5, 0) };
            marketCapPanel.Children.Add(txtMinMarketCap);
            marketCapPanel.Children.Add(new TextBlock { Text = "万 - ", VerticalAlignment = VerticalAlignment.Center });
            var txtMaxMarketCap = new TextBox { Name = "txtMaxMarketCap", Text = _advancedFilterMaxMarketCap.ToString(), Width = 80, Margin = new Thickness(0, 0, 5, 0) };
            marketCapPanel.Children.Add(txtMaxMarketCap);
            marketCapPanel.Children.Add(new TextBlock { Text = "万 (0表示无限制)", VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(marketCapPanel);
            
            // 保存引用以便后续使用
            panel.Tag = new { txtMinPosition, txtMaxPosition, txtAmplitudeDays, txtMinAmplitude, txtMaxAmplitude, txtMinVolume, txtMinMarketCap, txtMaxMarketCap };
            
            return panel;
        }
        
        /// <summary>
        /// 创建高级筛选按钮面板
        /// </summary>
        private StackPanel CreateAdvancedFilterButtonPanel(Window dialog)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) };
            
            var btnSearch = new Button
            {
                Name = "btnAdvancedFilter",
                Content = "开始筛选",
                Width = 100,
                Height = 30,
                Background = new SolidColorBrush(Colors.Green),
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 10, 0)
            };
            
            var btnClose = new Button
            {
                Content = "关闭",
                Width = 100,
                Height = 30,
                Background = new SolidColorBrush(Colors.Gray),
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 10, 0)
            };
            
            // 状态提示文本
            var statusText = new TextBlock
            {
                Name = "txtFilterStatus",
                Text = "请设置筛选条件后点击开始筛选",
                Foreground = new SolidColorBrush(Colors.Blue),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            
            btnSearch.Click += (s, e) => ExecuteNewAdvancedFilter(dialog);
            btnClose.Click += (s, e) => dialog.Close();
            
            panel.Children.Add(btnSearch);
            panel.Children.Add(btnClose);
            panel.Children.Add(statusText);
            
            return panel;
        }
        
        /// <summary>
        /// 创建高级筛选结果展示面板
        /// </summary>
        private StackPanel CreateAdvancedFilterResultPanel()
        {
            var panel = new StackPanel();
            
            // 标题和复制按钮区域
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            headerPanel.Children.Add(new TextBlock { Text = "筛选结果:", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            
            var copyAllButton = new Button
            {
                Content = "一键复制",
                Width = 120,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)),
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(20, 0, 0, 0)
            };
            headerPanel.Children.Add(copyAllButton);
            
            panel.Children.Add(headerPanel);
            
            var resultListView = new ListView
            {
                Name = "lvAdvancedFilterResult",
                MinHeight = 250,
                MaxHeight = 350,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.LightGray)
            };
            
            // 创建GridView
            var gridView = new GridView();
            
            // 交易对列
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "交易对", 
                Width = 100,
                DisplayMemberBinding = new System.Windows.Data.Binding("Symbol")
            });
            
            // 位置比例列
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "位置%", 
                Width = 80,
                DisplayMemberBinding = new System.Windows.Data.Binding("PositionText")
            });
            
            // 振幅列
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "振幅%", 
                Width = 80,
                DisplayMemberBinding = new System.Windows.Data.Binding("AmplitudeText")
            });
            
            // 24H成交额列
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "24H成交额", 
                Width = 100,
                DisplayMemberBinding = new System.Windows.Data.Binding("VolumeText")
            });
            
            // 当前价格列
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "当前价格", 
                Width = 100,
                DisplayMemberBinding = new System.Windows.Data.Binding("CurrentPriceText")
            });
            
            // 市值列
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "市值", 
                Width = 80,
                DisplayMemberBinding = new System.Windows.Data.Binding("MarketCapText")
            });
            
            // 市值排名列
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "排名", 
                Width = 60,
                DisplayMemberBinding = new System.Windows.Data.Binding("MarketCapRankText")
            });
            
            resultListView.View = gridView;
            
            // 添加双击复制功能
            resultListView.MouseDoubleClick += (s, e) => CopySymbolFromAdvancedFilterListView(s as ListView);
            
            // 设置复制按钮的ListView引用和事件
            copyAllButton.Tag = resultListView;
            copyAllButton.Click += CopyAllFilteredSymbols_Click;
            
            panel.Children.Add(resultListView);
            
            // 保存引用以便后续使用
            panel.Tag = resultListView;
            
            return panel;
        }
        
        /// <summary>
        /// 执行高级搜索
        /// </summary>
        private async void ExecuteAdvancedSearch(Window dialog)
        {
            try
            {
                // 获取参数输入面板的引用
                var paramPanel = dialog.Content as StackPanel;
                var paramInputs = paramPanel?.Children.OfType<StackPanel>().FirstOrDefault(p => p.Tag != null)?.Tag as dynamic;
                
                if (paramInputs == null)
                {
                    MessageBox.Show("无法获取参数输入", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var txtDays = paramInputs.txtDays as TextBox;
                var txtMultiplier = paramInputs.txtMultiplier as TextBox;
                var txtBreakoutDays = paramInputs.txtBreakoutDays as TextBox;
                
                // 获取范围选择面板的引用
                var rangePanel = paramPanel?.Children.OfType<StackPanel>().Skip(1).FirstOrDefault(p => p.Tag != null);
                var rangeInputs = rangePanel?.Tag as dynamic;
                
                if (rangeInputs == null)
                {
                    MessageBox.Show("无法获取范围选择", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var chkLow = rangeInputs.chkLow as CheckBox;
                var chkMid = rangeInputs.chkMid as CheckBox;
                var chkHigh = rangeInputs.chkHigh as CheckBox;
                var chkUltraHigh = rangeInputs.chkUltraHigh as CheckBox;
                
                // 解析参数
                if (txtDays?.Text == null || txtMultiplier?.Text == null || txtBreakoutDays?.Text == null ||
                    !int.TryParse(txtDays.Text, out var days) || 
                    !decimal.TryParse(txtMultiplier.Text, out var multiplier) ||
                    !int.TryParse(txtBreakoutDays.Text, out var breakoutDays))
                {
                    MessageBox.Show("请输入有效的参数", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 获取选择的范围
                var selectedRanges = new List<string>();
                if (chkLow?.IsChecked == true) selectedRanges.Add("低位区域");
                if (chkMid?.IsChecked == true) selectedRanges.Add("中位区域");
                if (chkHigh?.IsChecked == true) selectedRanges.Add("高位区域");
                if (chkUltraHigh?.IsChecked == true) selectedRanges.Add("超高位");
                
                if (selectedRanges.Count == 0)
                {
                    MessageBox.Show("请至少选择一个范围", "选择错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 执行筛选
                var results = await ExecuteAdvancedFilter(days, multiplier, breakoutDays, selectedRanges);
                
                // 显示结果
                DisplayAdvancedFilterResults(results, dialog);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行高级搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 执行高级筛选
        /// </summary>
        private async Task<List<AdvancedFilterResult>> ExecuteAdvancedFilter(int days, decimal multiplier, int breakoutDays, List<string> selectedRanges)
        {
            var results = new List<AdvancedFilterResult>();
            
            try
            {
                // 根据选择的范围获取数据
                var allData = new List<LocationData>();
                
                if (selectedRanges.Contains("低位区域"))
                    allData.AddRange(_locationData.Where(d => d.LocationRatio <= 0.25m));
                if (selectedRanges.Contains("中低区域"))
                    allData.AddRange(_locationData.Where(d => d.LocationRatio > 0.25m && d.LocationRatio <= 0.50m));
                if (selectedRanges.Contains("中高区域"))
                    allData.AddRange(_locationData.Where(d => d.LocationRatio > 0.50m && d.LocationRatio <= 0.75m));
                if (selectedRanges.Contains("高位区域"))
                    allData.AddRange(_locationData.Where(d => d.LocationRatio > 0.75m));
                
                Console.WriteLine($"🔍 高级筛选开始，范围: {string.Join(", ", selectedRanges)}, 合约数量: {allData.Count}");
                
                // 对每个合约执行筛选
                foreach (var contract in allData)
                {
                    try
                    {
                        var result = await AnalyzeContract(contract, days, multiplier, breakoutDays);
                        if (result != null)
                        {
                            results.Add(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ 分析合约 {contract.Symbol} 时出错: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"✅ 高级筛选完成，找到 {results.Count} 个符合条件的合约");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 高级筛选执行失败: {ex.Message}");
                throw;
            }
            
            return results;
        }
        
        /// <summary>
        /// 分析单个合约
        /// </summary>
        private async Task<AdvancedFilterResult?> AnalyzeContract(LocationData contract, int days, decimal multiplier, int breakoutDays)
        {
            try
            {
                // 获取K线数据
                var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(contract.Symbol);
                if (!loadSuccess || klines == null || klines.Count == 0)
                {
                    return null;
                }
                
                // 按时间排序
                var sortedKlines = klines.OrderBy(k => k.OpenTime).ToList();
                
                // 检查数据是否足够
                if (sortedKlines.Count < days + 1 || sortedKlines.Count < breakoutDays + 1)
                {
                    return null; // 数据不足
                }
                
                // 检查成交额条件
                var recentKline = sortedKlines[sortedKlines.Count - 1]; // 使用索引而不是Last()
                var recentVolume = recentKline.Volume; // 最近一天的成交额
                
                // 获取前N天的数据（除最近一天）
                var startIndex = Math.Max(0, sortedKlines.Count - days - 1);
                var endIndex = sortedKlines.Count - 1;
                var previousDays = sortedKlines.Skip(startIndex).Take(endIndex - startIndex).ToList();
                
                if (previousDays.Count == 0)
                {
                    return null; // 前N天数据不足
                }
                var averageVolume = previousDays.Average(k => k.Volume);
                
                if (recentVolume < averageVolume * multiplier)
                {
                    return null; // 不满足成交额条件
                }
                
                // 检查突破新高条件
                var recentPrice = recentKline.ClosePrice;
                
                // 获取前X天的数据（除最近一天）
                var startIndex2 = Math.Max(0, sortedKlines.Count - breakoutDays - 1);
                var previousDaysPrices = sortedKlines.Skip(startIndex2).Take(endIndex - startIndex2).ToList();
                
                if (previousDaysPrices.Count == 0)
                {
                    return null; // 前X天数据不足
                }
                var previousHigh = previousDaysPrices.Max(k => k.HighPrice);
                
                if (recentPrice <= previousHigh)
                {
                    return null; // 不满足突破新高条件
                }
                
                // 创建结果
                var result = new AdvancedFilterResult
                {
                    Symbol = contract.Symbol,
                    LocationRatio = contract.LocationRatio,
                    VolumeMultiplier = recentVolume / averageVolume,
                    BreakoutDays = breakoutDays,
                    CurrentPrice = recentPrice,
                    PreviousHigh = previousHigh
                };
                
                return result;
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 分析合约 {contract.Symbol} 时出错: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 显示高级筛选结果
        /// </summary>
        private void DisplayAdvancedFilterResults(List<AdvancedFilterResult> results, Window dialog)
        {
            try
            {
                // 获取结果面板的引用
                var mainPanel = dialog.Content as StackPanel;
                if (mainPanel == null)
                {
                    MessageBox.Show("无法获取主面板", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var resultPanel = mainPanel.Children.OfType<StackPanel>().LastOrDefault(p => p.Tag != null);
                if (resultPanel == null)
                {
                    MessageBox.Show("无法获取结果面板", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (resultPanel.Tag == null)
                {
                    MessageBox.Show("结果面板标签为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var resultListView = resultPanel.Tag as ListView;
                if (resultListView == null)
                {
                    MessageBox.Show("无法获取结果列表", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 设置数据源
                resultListView.ItemsSource = results;
                
                // 显示结果数量
                MessageBox.Show($"筛选完成！找到 {results.Count} 个符合条件的合约", "筛选结果", MessageBoxButton.OK, MessageBoxImage.Information);
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示筛选结果失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 复制结果到剪贴板
        /// </summary>
        private void CopyResultsToClipboard(ListView listView)
        {
            try
            {
                if (listView.SelectedItem is AdvancedFilterResult selectedResult)
                {
                    // 只复制选中的交易对符号
                    if (TrySetClipboardText(selectedResult.Symbol))
                    {
                        MessageBox.Show($"已复制交易对 '{selectedResult.Symbol}' 到剪贴板", "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("剪贴板被占用，复制失败。请稍后重试。", "复制失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("请先选择要复制的行", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制到剪贴板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 全选结果
        /// </summary>
        private void SelectAllResults(ListView listView)
        {
            try
            {
                listView.SelectAll();
                MessageBox.Show($"已全选 {listView.Items.Count} 条结果", "全选完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"全选失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 筛选结果列表双击事件处理
        /// </summary>
        private void ResultListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is ListView listView && listView.SelectedItem is AdvancedFilterResult selectedResult)
                {
                    // 打开数据验证窗口
                    OpenDataValidationWindow(selectedResult);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开数据验证窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 打开数据验证窗口
        /// </summary>
        private void OpenDataValidationWindow(AdvancedFilterResult filterResult)
        {
            try
            {
                // 获取筛选参数（这里需要从高级筛选对话框中获取）
                var analysisDays = 30; // 默认值，实际应该从筛选参数获取
                var volumeMultiplier = filterResult.VolumeMultiplier;
                var breakoutDays = filterResult.BreakoutDays;
                
                // 获取对应的K线数据
                var klineData = GetKlineDataForSymbol(filterResult.Symbol);
                
                if (klineData == null || klineData.Count == 0)
                {
                    MessageBox.Show($"无法获取 {filterResult.Symbol} 的K线数据", "数据不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 创建并显示数据验证窗口
                var validationWindow = new DataValidationWindow(filterResult, klineData, analysisDays, volumeMultiplier, breakoutDays);
                validationWindow.Show();
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建数据验证窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 获取指定交易对的K线数据
        /// </summary>
        private List<BinanceApps.Core.Models.Kline> GetKlineDataForSymbol(string symbol)
        {
            try
            {
                // 从现有的数据中查找
                if (_allKlineData != null)
                {
                    return _allKlineData.Where(k => k.Symbol == symbol).ToList();
                }
                
                return new List<BinanceApps.Core.Models.Kline>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取K线数据失败: {ex.Message}");
                return new List<BinanceApps.Core.Models.Kline>();
            }
        }
        
        /// <summary>
        /// 计算市场波动率
        /// </summary>
        private async Task<List<MarketVolatilityData>> CalculateMarketVolatilityAsync()
        {
            try
            {
                var volatilityDataList = new List<MarketVolatilityData>();
                
                // 限制数据范围为最近90天
                var endDate = DateTime.UtcNow.Date;
                var startDate = endDate.AddDays(-90);
                
                // 过滤K线数据，只保留最近90天
                var filteredKlineData = _allKlineData
                    .Where(k => k.OpenTime.Date >= startDate && k.OpenTime.Date <= endDate)
                    .ToList();
                
                Console.WriteLine($"📊 市场波动率数据范围: {startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd}");
                Console.WriteLine($"📈 过滤前K线记录数: {_allKlineData.Count}");
                Console.WriteLine($"📈 过滤后K线记录数: {filteredKlineData.Count}");
                
                // 按日期分组K线数据 - 使用本地时间进行分组，避免UTC时区问题
                var dailyGroups = filteredKlineData.GroupBy(k => k.OpenTime.ToLocalTime().Date).OrderBy(g => g.Key).ToList();
                
                Console.WriteLine($"📊 开始计算市场波动率，共{dailyGroups.Count}天的K线数据");
                Console.WriteLine($"📋 包含的币种: {filteredKlineData.Select(k => k.Symbol).Distinct().Count()}个");
                Console.WriteLine($"🌍 时区信息: UTC {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}, 本地 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // 输出每天的数据概况
                foreach (var group in dailyGroups.Take(5)) // 显示前5天的概况
                {
                    Console.WriteLine($"   {group.Key:yyyy-MM-dd}: {group.Count()}个记录");
                }
                
                // 如果有数据，检查第一条记录的时间
                if (filteredKlineData.Count > 0)
                {
                    var firstRecord = filteredKlineData.OrderBy(k => k.OpenTime).First();
                    Console.WriteLine($"🕒 第一条记录时间: UTC {firstRecord.OpenTime:yyyy-MM-dd HH:mm:ss}, 本地 {firstRecord.OpenTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
                }
                
                foreach (var dailyGroup in dailyGroups)
                {
                    var date = dailyGroup.Key;
                    var dailyKlines = dailyGroup.ToList();
                    
                    // 计算每个币种的波动率
                    var symbolVolatilities = new List<SymbolVolatility>();
                    
                    foreach (var kline in dailyKlines)
                    {
                        if (kline.LowPrice > 0) // 避免除零错误
                        {
                            var volatility = (kline.HighPrice - kline.LowPrice) / kline.LowPrice;
                            
                            // 获取对应的24H ticker数据
                            var tickData = _allTicks.FirstOrDefault(t => t.Symbol == kline.Symbol);
                            
                            symbolVolatilities.Add(new SymbolVolatility
                            {
                                Symbol = kline.Symbol,
                                Volatility = volatility,
                                HighPrice = kline.HighPrice,
                                LowPrice = kline.LowPrice,
                                ClosePrice = kline.ClosePrice,
                                PriceChangePercent = tickData?.PriceChangePercent ?? 0m,
                                QuoteVolume = tickData?.QuoteVolume ?? 0m
                            });
                        }
                    }
                    
                    // 按波动率排序，取前30个
                    var topVolatilities = symbolVolatilities
                        .OrderByDescending(v => v.Volatility)
                        .Take(30)
                        .ToList();
                    
                    // 计算前30个的平均波动率
                    var averageMaxVolatility = topVolatilities.Count > 0 
                        ? topVolatilities.Average(v => v.Volatility) 
                        : 0;
                    
                    // 计算每日成交额总和（以亿为单位）
                    var dailyTotalVolume = dailyKlines.Sum(k => k.QuoteVolume) / 100000000; // 转换为亿
                    
                    // 详细调试信息
                    if (dailyTotalVolume < 500) // 如果成交额小于500亿，输出详细信息
                    {
                        Console.WriteLine($"⚠️ 异常低成交额检测: {date:yyyy-MM-dd}");
                        Console.WriteLine($"   💰 总成交额: {dailyKlines.Sum(k => k.QuoteVolume):F0} USDT ({dailyTotalVolume:F0}亿)");
                        Console.WriteLine($"   📊 币种数量: {dailyKlines.Count}");
                        Console.WriteLine($"   🔍 前5个币种成交额:");
                        
                        var topVolumeSymbols = dailyKlines.OrderByDescending(k => k.QuoteVolume).Take(5);
                        foreach (var symbol in topVolumeSymbols)
                        {
                            Console.WriteLine($"      {symbol.Symbol}: {symbol.QuoteVolume:F0} USDT ({symbol.QuoteVolume/100000000:F2}亿)");
                        }
                        
                        // 检查是否有成交额为0的记录
                        var zeroVolumeCount = dailyKlines.Count(k => k.QuoteVolume == 0);
                        if (zeroVolumeCount > 0)
                        {
                            Console.WriteLine($"   ⚠️ 发现 {zeroVolumeCount} 个成交额为0的记录");
                        }
                    }
                    
                    Console.WriteLine($"📅 {date:yyyy-MM-dd}: 成交额 {dailyTotalVolume:F0}亿, 波动率 {averageMaxVolatility:P2}, 币种数 {dailyKlines.Count}");
                    
                    // 获取比特币数据
                    var btcData = dailyKlines.FirstOrDefault(k => k.Symbol == "BTCUSDT");
                    decimal btcPriceChangePercent = 0;
                    decimal btcQuoteVolume = 0;
                    
                    if (btcData != null)
                    {
                        // 计算比特币涨跌幅 = (收盘价 - 开盘价) / 开盘价 * 100
                        btcPriceChangePercent = btcData.OpenPrice > 0 ? 
                            ((btcData.ClosePrice - btcData.OpenPrice) / btcData.OpenPrice) * 100 : 0;
                        btcQuoteVolume = btcData.QuoteVolume;
                    }
                    
                    volatilityDataList.Add(new MarketVolatilityData
                    {
                        Date = date,
                        AverageMaxVolatility = averageMaxVolatility,
                        SymbolCount = dailyKlines.Count,
                        DailyTotalVolume = dailyTotalVolume,
                        TopVolatilitySymbols = topVolatilities,
                        BtcPriceChangePercent = btcPriceChangePercent,
                        BtcQuoteVolume = btcQuoteVolume
                    });
                }
                
                // 添加今日24H数据
                try
                {
                    Console.WriteLine("📊 正在获取今日24H数据...");
                    var todayTickerData = await Get24HTickerDataAsync();
                    
                    if (todayTickerData != null && todayTickerData.Count > 0)
                    {
                        // 计算今日24H的波动率数据
                        var todayVolatilities = new List<SymbolVolatility>();
                        
                        foreach (var tick in todayTickerData)
                        {
                            if (tick.LowPrice > 0)
                            {
                                var volatility = (tick.HighPrice - tick.LowPrice) / tick.LowPrice;
                                
                                todayVolatilities.Add(new SymbolVolatility
                                {
                                    Symbol = tick.Symbol,
                                    Volatility = volatility,
                                    HighPrice = tick.HighPrice,
                                    LowPrice = tick.LowPrice,
                                    ClosePrice = tick.LastPrice,
                                    PriceChangePercent = tick.PriceChangePercent,
                                    QuoteVolume = tick.QuoteVolume
                                });
                            }
                        }
                        
                        // 取波动率最高的30个
                        var topTodayVolatilities = todayVolatilities
                            .OrderByDescending(v => v.Volatility)
                            .Take(30)
                            .ToList();
                        
                        // 计算今日平均波动率
                        var todayAvgVolatility = topTodayVolatilities.Count > 0 
                            ? topTodayVolatilities.Average(v => v.Volatility) 
                            : 0;
                        
                        // 计算今日总成交额
                        var todayTotalVolume = todayTickerData.Sum(t => t.QuoteVolume) / 100000000; // 转换为亿
                        
                        // 获取比特币24H数据
                        var btcTodayData = todayTickerData.FirstOrDefault(t => t.Symbol == "BTCUSDT");
                        decimal btcTodayChangePercent = 0;
                        decimal btcTodayVolume = 0;
                        
                        if (btcTodayData != null)
                        {
                            btcTodayChangePercent = btcTodayData.PriceChangePercent;
                            btcTodayVolume = btcTodayData.QuoteVolume;
                        }
                        
                        Console.WriteLine($"📅 今日24H: 成交额 {todayTotalVolume:F0}亿, 波动率 {todayAvgVolatility:P2}, 币种数 {todayTickerData.Count}");
                        
                        // 添加今日数据
                        volatilityDataList.Add(new MarketVolatilityData
                        {
                            Date = DateTime.Today, // 使用今天的日期
                            AverageMaxVolatility = todayAvgVolatility,
                            SymbolCount = todayTickerData.Count,
                            DailyTotalVolume = todayTotalVolume,
                            TopVolatilitySymbols = topTodayVolatilities,
                            BtcPriceChangePercent = btcTodayChangePercent,
                            BtcQuoteVolume = btcTodayVolume,
                            IsToday = true // 标记为今日数据
                        });
                    }
                                                 }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 获取今日24H数据失败: {ex.Message}");
                }
                
                return volatilityDataList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"计算市场波动率失败: {ex.Message}");
                return new List<MarketVolatilityData>();
            }
        }
        
        /// <summary>
        /// 显示市场波动率
        /// </summary>
        private async Task DisplayMarketVolatility(List<MarketVolatilityData> volatilityData)
        {
            try
            {
                var panel = new StackPanel();
                
                // 标题
                panel.Children.Add(new TextBlock 
                { 
                    Text = "📊 市场波动率一览", 
                    FontSize = 24, 
                    FontWeight = FontWeights.Bold, 
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                
                // 统计信息
                var statsPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    Margin = new Thickness(0, 0, 0, 20) 
                };
                
                var totalDays = volatilityData.Count;
                var avgVolatility = volatilityData.Average(v => v.AverageMaxVolatility);
                var maxVolatility = volatilityData.Max(v => v.AverageMaxVolatility);
                var minVolatility = volatilityData.Min(v => v.AverageMaxVolatility);
                var totalVolume = volatilityData.Sum(v => v.DailyTotalVolume);
                var avgVolume = volatilityData.Average(v => v.DailyTotalVolume);
                var maxVolume = volatilityData.Max(v => v.DailyTotalVolume);
                
                statsPanel.Children.Add(CreateStatBox("总天数", totalDays.ToString()));
                statsPanel.Children.Add(CreateStatBox("平均波动率", $"{avgVolatility:P2}"));
                statsPanel.Children.Add(CreateStatBox("最大波动率", $"{maxVolatility:P2}"));
                statsPanel.Children.Add(CreateStatBox("最小波动率", $"{minVolatility:P2}"));
                statsPanel.Children.Add(CreateStatBox("总成交额", $"{totalVolume:F0}亿"));
                statsPanel.Children.Add(CreateStatBox("平均成交额", $"{avgVolume:F0}亿"));
                statsPanel.Children.Add(CreateStatBox("最大成交额", $"{maxVolume:F0}亿"));
                
                panel.Children.Add(statsPanel);
                
                // 波动率方块展示
                var volatilityPanel = new WrapPanel 
                { 
                    Margin = new Thickness(0, 20, 0, 0) 
                };
                
                foreach (var data in volatilityData.OrderBy(v => v.Date))
                {
                    var volatilityBlock = CreateVolatilityBlock(data, volatilityData);
                    volatilityPanel.Children.Add(volatilityBlock);
                }
                
                panel.Children.Add(volatilityPanel);
                
                // 成交额变化柱状图
                var volumeChartPanel = await CreateVolumeChartPanelAsync(volatilityData);
                panel.Children.Add(volumeChartPanel);
                
                // 添加涨跌数据统计列表
                var priceChangeStatsPanel = await CreatePriceChangeStatsPanel();
                panel.Children.Add(priceChangeStatsPanel);
                
                // 显示到主界面
                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = panel
                };
                contentPanel.Children.Clear();
                contentPanel.Children.Add(scrollViewer);
                
                // 启动市场监控服务
                StartMarketMonitoring();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示市场波动率失败: {ex.Message}");
                MessageBox.Show($"显示市场波动率失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 创建成交额变化柱状图面板
        /// </summary>
        private async Task<StackPanel> CreateVolumeChartPanelAsync(List<MarketVolatilityData> volatilityData)
        {
            try
            {
                var panel = new StackPanel { Margin = new Thickness(0, 30, 0, 0) };
                
                // 标题
                var titleBlock = new TextBlock
                {
                    Text = "📈 市场成交额变化趋势",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                panel.Children.Add(titleBlock);
                
                // 获取当前24H总成交额
                var current24HVolume = await GetCurrent24HTotalVolumeAsync();
                
                // 创建主内容区域（图表+信息栏）
                var mainContentGrid = new Grid 
                { 
                    Height = 320, 
                    Margin = new Thickness(0, 0, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                
                // 定义列：图表区域和信息栏区域
                mainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                mainContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // 固定宽度300px
                
                // 创建图表容器（左侧）
                var chartContainer = new Grid 
                { 
                    Background = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 0, 10, 0)
                };
                
                // 创建响应式Canvas
                var chartCanvas = new Canvas 
                { 
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                
                // 监听容器大小变化事件
                chartContainer.SizeChanged += async (sender, e) =>
                {
                    if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
                    {
                        chartCanvas.Children.Clear();
                        await DrawVolumeChartAsync(chartCanvas, volatilityData, current24HVolume, e.NewSize.Width, e.NewSize.Height);
                    }
                };
                
                // 容器加载完成事件，用于初始绘制
                chartContainer.Loaded += async (sender, e) =>
                {
                    var actualWidth = chartContainer.ActualWidth;
                    var actualHeight = chartContainer.ActualHeight;
                    if (actualWidth > 0 && actualHeight > 0)
                    {
                        await DrawVolumeChartAsync(chartCanvas, volatilityData, current24HVolume, actualWidth, actualHeight);
                    }
                    else
                    {
                        // 如果ActualWidth/Height还没有值，使用默认大小初始绘制
                        await DrawVolumeChartAsync(chartCanvas, volatilityData, current24HVolume, 600, 300);
                    }
                };
                
                chartContainer.Children.Add(chartCanvas);
                Grid.SetColumn(chartContainer, 0);
                mainContentGrid.Children.Add(chartContainer);
                
                // 创建信息提示栏（右侧）
                var infoPanel = await CreateMarketInfoPanelAsync(current24HVolume);
                Grid.SetColumn(infoPanel, 1);
                mainContentGrid.Children.Add(infoPanel);
                
                panel.Children.Add(mainContentGrid);
                
                // 图例说明
                var legendPanel = CreateVolumeLegendPanel();
                panel.Children.Add(legendPanel);
                
                return panel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 创建成交额图表失败: {ex.Message}");
                var errorPanel = new StackPanel();
                errorPanel.Children.Add(new TextBlock 
                { 
                    Text = "❌ 创建成交额图表失败",
                    Foreground = new SolidColorBrush(Colors.Red),
                    FontSize = 14
                });
                return errorPanel;
            }
        }
        
        /// <summary>
        /// 获取当前24H总成交额
        /// </summary>
        private async Task<decimal> GetCurrent24HTotalVolumeAsync()
        {
            try
            {
                // 获取24H行情数据
                var tickerData = await Get24HTickerDataAsync();
                if (tickerData == null || tickerData.Count == 0)
                    return 0;
                
                // 计算总成交额（转换为亿）
                var totalVolume = tickerData.Sum(t => t.QuoteVolume) / 100000000m;
                return totalVolume;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取当前24H总成交额失败: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// 绘制成交额图表（响应式）
        /// </summary>
        private async Task DrawVolumeChartAsync(Canvas canvas, List<MarketVolatilityData> volatilityData, decimal current24HVolume, double containerWidth, double containerHeight)
        {
            await Task.CompletedTask;
            
            if (volatilityData.Count == 0) return;
            
            // 动态图表参数
            var chartWidth = Math.Max(containerWidth - 20, 400); // 最小宽度400
            var chartHeight = Math.Max(containerHeight - 20, 200); // 最小高度200
            const double leftMargin = 60;
            const double rightMargin = 60;
            const double topMargin = 20;
            const double bottomMargin = 40;
            
            var drawWidth = chartWidth - leftMargin - rightMargin;
            var drawHeight = chartHeight - topMargin - bottomMargin;
            
            // 计算数据范围
            var maxVolume = Math.Max(volatilityData.Max(v => v.DailyTotalVolume), current24HVolume);
            var minVolume = Math.Min(volatilityData.Min(v => v.DailyTotalVolume), 0);
            var volumeRange = maxVolume - minVolume;
            
            // 计算5日移动平均
            var avgVolumes = Calculate5DayMovingAverage(volatilityData);
            
            // 绘制背景网格
            DrawVolumeGridLines(canvas, leftMargin, topMargin, drawWidth, drawHeight, maxVolume, minVolume);
            
            // 绘制柱状图
            DrawVolumeBars(canvas, volatilityData, leftMargin, topMargin, drawWidth, drawHeight, maxVolume, minVolume);
            
            // 绘制5日移动平均线
            DrawMovingAverageLine(canvas, avgVolumes, leftMargin, topMargin, drawWidth, drawHeight, maxVolume, minVolume);
            
            // 绘制坐标轴标签
            DrawVolumeAxisLabels(canvas, volatilityData, leftMargin, topMargin, drawWidth, drawHeight, maxVolume, minVolume);
        }
        
        /// <summary>
        /// 计算5日移动平均
        /// </summary>
        private List<decimal> Calculate5DayMovingAverage(List<MarketVolatilityData> data)
        {
            var result = new List<decimal>();
            var sortedData = data.OrderBy(d => d.Date).ToList();
            
            for (int i = 0; i < sortedData.Count; i++)
            {
                var startIndex = Math.Max(0, i - 4);
                var endIndex = i;
                var avgVolume = sortedData.Skip(startIndex).Take(endIndex - startIndex + 1)
                    .Average(v => v.DailyTotalVolume);
                result.Add(avgVolume);
            }
            
            return result;
        }
        
        /// <summary>
        /// 绘制网格线
        /// </summary>
        private void DrawVolumeGridLines(Canvas canvas, double leftMargin, double topMargin, double width, double height, decimal maxVolume, decimal minVolume)
        {
            // 水平网格线（成交额）
            for (int i = 0; i <= 5; i++)
            {
                var y = topMargin + (height * i / 5);
                var line = new Line
                {
                    X1 = leftMargin,
                    Y1 = y,
                    X2 = leftMargin + width,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    StrokeThickness = 0.5
                };
                canvas.Children.Add(line);
            }
        }
        
        /// <summary>
        /// 绘制柱状图
        /// </summary>
        private void DrawVolumeBars(Canvas canvas, List<MarketVolatilityData> data, double leftMargin, double topMargin, double width, double height, decimal maxVolume, decimal minVolume)
        {
            var sortedData = data.OrderBy(d => d.Date).ToList();
            var barWidth = width / sortedData.Count * 0.6; // 柱子宽度占60%
            
            for (int i = 0; i < sortedData.Count; i++)
            {
                var volume = sortedData[i].DailyTotalVolume;
                var barHeight = height * (double)(volume - minVolume) / (double)(maxVolume - minVolume);
                var x = leftMargin + (width * i / sortedData.Count) + (width / sortedData.Count - barWidth) / 2;
                var y = topMargin + height - barHeight;
                
                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = barHeight,
                    Fill = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    Stroke = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                    StrokeThickness = 1
                };
                
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            }
        }
        
        /// <summary>
        /// 绘制5日移动平均线
        /// </summary>
        private void DrawMovingAverageLine(Canvas canvas, List<decimal> avgVolumes, double leftMargin, double topMargin, double width, double height, decimal maxVolume, decimal minVolume)
        {
            if (avgVolumes.Count < 2) return;
            
            var points = new PointCollection();
            
            for (int i = 0; i < avgVolumes.Count; i++)
            {
                var volume = avgVolumes[i];
                var x = leftMargin + (width * i / avgVolumes.Count) + (width / avgVolumes.Count / 2);
                var y = topMargin + height - (height * (double)(volume - minVolume) / (double)(maxVolume - minVolume));
                points.Add(new Point(x, y));
            }
            
            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                StrokeThickness = 2,
                Fill = null
            };
            
            canvas.Children.Add(polyline);
        }
        
        /// <summary>
        /// 绘制坐标轴标签
        /// </summary>
        private void DrawVolumeAxisLabels(Canvas canvas, List<MarketVolatilityData> data, double leftMargin, double topMargin, double width, double height, decimal maxVolume, decimal minVolume)
        {
            var sortedData = data.OrderBy(d => d.Date).ToList();
            
            // Y轴标签（成交额）
            for (int i = 0; i <= 5; i++)
            {
                var volume = minVolume + (maxVolume - minVolume) * i / 5;
                var y = topMargin + height - (height * i / 5);
                
                var label = new TextBlock
                {
                    Text = $"{volume:F0}亿",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125))
                };
                
                Canvas.SetLeft(label, leftMargin - 50);
                Canvas.SetTop(label, y - 7);
                canvas.Children.Add(label);
            }
            
            // X轴标签（日期）
            for (int i = 0; i < sortedData.Count; i++)
            {
                if (i % 2 == 0) // 只显示部分日期避免拥挤
                {
                    var x = leftMargin + (width * i / sortedData.Count) + (width / sortedData.Count / 2);
                    var dateLabel = new TextBlock
                    {
                        Text = sortedData[i].Date.ToString("MM-dd"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125))
                    };
                    
                    Canvas.SetLeft(dateLabel, x - 15);
                    Canvas.SetTop(dateLabel, topMargin + height + 10);
                    canvas.Children.Add(dateLabel);
                }
            }
        }
        
        /// <summary>
        /// 创建市场信息面板
        /// </summary>
        private Task<StackPanel> CreateMarketInfoPanelAsync(decimal current24HVolume)
        {
            var panel = new StackPanel 
            { 
                Margin = new Thickness(10, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            
            // 市场热度卡片
            var heatCard = CreateMarketHeatCard(current24HVolume);
            panel.Children.Add(heatCard);
            
            // CoinGlass网站链接卡片
            var websiteCard = CreateWebsiteLinkCard();
            panel.Children.Add(websiteCard);
            
            return Task.FromResult(panel);
        }
        
        /// <summary>
        /// 创建市场热度卡片
        /// </summary>
        private Border CreateMarketHeatCard(decimal current24HVolume)
        {
            // 根据成交额判断热度
            string heatLevel;
            Color heatColor;
            string heatIcon;
            
            if (current24HVolume >= 1600)
            {
                heatLevel = "高热度";
                heatColor = Color.FromRgb(220, 53, 69); // 红色
                heatIcon = "🔥";
            }
            else if (current24HVolume >= 1000)
            {
                heatLevel = "中热度";
                heatColor = Color.FromRgb(255, 193, 7); // 橙色
                heatIcon = "📈";
            }
            else if (current24HVolume >= 600)
            {
                heatLevel = "低热度";
                heatColor = Color.FromRgb(40, 167, 69); // 绿色
                heatIcon = "📊";
            }
            else
            {
                heatLevel = "极低热度";
                heatColor = Color.FromRgb(108, 117, 125); // 灰色
                heatIcon = "📉";
            }
            
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 15),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0, 0, 0),
                    Opacity = 0.1,
                    BlurRadius = 4,
                    ShadowDepth = 2
                }
            };
            
            var cardContent = new StackPanel();
            
            // 卡片标题
            cardContent.Children.Add(new TextBlock
            {
                Text = "💹 市场热度",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 58, 64)),
                Margin = new Thickness(0, 0, 0, 12)
            });
            
            // 当前成交额
            cardContent.Children.Add(new TextBlock
            {
                Text = $"{current24HVolume:F0}亿",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 58, 64)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            
            // 热度指示器
            var heatPanel = new StackPanel { Orientation = Orientation.Horizontal };
            heatPanel.Children.Add(new TextBlock
            {
                Text = heatIcon,
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            heatPanel.Children.Add(new TextBlock
            {
                Text = heatLevel,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(heatColor),
                VerticalAlignment = VerticalAlignment.Center
            });
            cardContent.Children.Add(heatPanel);
            
            // 系统判定规则
            cardContent.Children.Add(new TextBlock
            {
                Text = "系统判定规则：",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87)),
                Margin = new Thickness(0, 12, 0, 8)
            });
            
            var rulesPanel = new StackPanel();
            rulesPanel.Children.Add(new TextBlock
            {
                Text = "0-600亿：极低热度",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                Margin = new Thickness(0, 2, 0, 2)
            });
            rulesPanel.Children.Add(new TextBlock
            {
                Text = "600-1000亿：低热度",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                Margin = new Thickness(0, 2, 0, 2)
            });
            rulesPanel.Children.Add(new TextBlock
            {
                Text = "1000-1600亿：中热度",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                Margin = new Thickness(0, 2, 0, 2)
            });
            rulesPanel.Children.Add(new TextBlock
            {
                Text = "1600亿以上：高热度",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                Margin = new Thickness(0, 2, 0, 2)
            });
            cardContent.Children.Add(rulesPanel);
            
            card.Child = cardContent;
            return card;
        }
        

        
        /// <summary>
        /// 创建网站链接卡片
        /// </summary>
        private Border CreateWebsiteLinkCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 110, 253)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 15),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0, 0, 0),
                    Opacity = 0.1,
                    BlurRadius = 4,
                    ShadowDepth = 2
                }
            };
            
            // 添加鼠标悬停效果
            card.MouseEnter += (sender, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(10, 88, 202));
            };
            card.MouseLeave += (sender, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(13, 110, 253));
            };
            
            // 添加点击事件
            card.MouseLeftButtonUp += (sender, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://www.coinglass.com/zh",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 打开网站失败: {ex.Message}");
                    MessageBox.Show("无法打开网站，请检查系统默认浏览器设置。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            
            var cardContent = new StackPanel();
            
            // 网站图标和名称
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "🌐",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "CoinGlass",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            });
            cardContent.Children.Add(headerPanel);
            
            // 描述
            cardContent.Children.Add(new TextBlock
            {
                Text = "查看爆仓数据",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 230, 255)),
                Margin = new Thickness(0, 4, 0, 0)
            });
            
            card.Child = cardContent;
            return card;
        }
        

        

        


        

        

        

        
        /// <summary>
        /// 创建图例面板
        /// </summary>
        private StackPanel CreateVolumeLegendPanel()
        {
            var panel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };
            
            // 柱状图图例
            var barLegend = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 30, 0) };
            var barRect = new Rectangle 
            { 
                Width = 15, 
                Height = 15, 
                Fill = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var barText = new TextBlock 
            { 
                Text = "每日成交额", 
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            barLegend.Children.Add(barRect);
            barLegend.Children.Add(barText);
            
            // 折线图图例
            var lineLegend = new StackPanel { Orientation = Orientation.Horizontal };
            var lineRect = new Rectangle 
            { 
                Width = 15, 
                Height = 3, 
                Fill = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var lineText = new TextBlock 
            { 
                Text = "5日平均成交额", 
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            lineLegend.Children.Add(lineRect);
            lineLegend.Children.Add(lineText);
            
            panel.Children.Add(barLegend);
            panel.Children.Add(lineLegend);
            
            return panel;
        }
        
        /// <summary>
        /// 创建统计信息框
        /// </summary>
        private Border CreateStatBox(string title, string value)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.LightGray),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 15, 0),
                MinWidth = 120
            };
            
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = title, 
                FontSize = 12, 
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = value, 
                FontSize = 16, 
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            });
            
            border.Child = stackPanel;
            return border;
        }
        
        /// <summary>
        /// 创建波动率方块
        /// </summary>
        private Border CreateVolatilityBlock(MarketVolatilityData data, List<MarketVolatilityData> allData)
        {
            // 根据波动率确定颜色
            var volatility = (double)data.AverageMaxVolatility;
            var backgroundColor = GetVolatilityColor(volatility);
            var textColor = GetTextColor(backgroundColor);
            
            // 主容器，包含色块和比特币柱状图
            var mainContainer = new StackPanel();
            
            // 原波动率色块
            var border = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(5),
                Width = 100,
                Height = 100,
                Cursor = Cursors.Hand
            };
            
            var stackPanel = new StackPanel();
            
            // 日期
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = data.IsToday ? "今日24H" : data.Date.ToString("MM-dd"), 
                FontSize = 12, 
                Foreground = new SolidColorBrush(textColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            });
            
            // 波动率
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = $"{data.AverageMaxVolatility:P1}", 
                FontSize = 14, 
                Foreground = new SolidColorBrush(textColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0),
                FontWeight = FontWeights.Bold
            });
            
            // 币种数量
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = $"{data.SymbolCount}个", 
                FontSize = 10, 
                Foreground = new SolidColorBrush(textColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
                Opacity = 0.8
            });
            
            // 每日成交额（以亿为单位）
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = $"{data.DailyTotalVolume:F0}亿", 
                FontSize = 10, 
                Foreground = new SolidColorBrush(textColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Opacity = 0.9
            });
            
            border.Child = stackPanel;
            
            // 添加点击事件，显示详细信息
            border.MouseLeftButtonDown += (s, e) => ShowVolatilityDetails(data);
            
            // 添加波动率色块到主容器
            mainContainer.Children.Add(border);
            
            // 创建比特币涨跌幅柱状图
            var btcChart = CreateBtcChangeChart(data, allData);
            mainContainer.Children.Add(btcChart);
            
            // 返回包含色块和柱状图的容器
            var containerBorder = new Border
            {
                Child = mainContainer,
                Margin = new Thickness(0)
            };
            
            return containerBorder;
        }
        
        /// <summary>
        /// 创建比特币涨跌幅横向柱状图
        /// </summary>
        private Border CreateBtcChangeChart(MarketVolatilityData data, List<MarketVolatilityData> allData)
        {
            // 容器高度为色块的20% (100 * 0.2 = 20px)
            var chartHeight = 20;
            var chartWidth = 100; // 与色块宽度保持一致
            
            var container = new Border
            {
                Width = chartWidth,
                Height = chartHeight,
                Margin = new Thickness(5, 2, 5, 0), // 与色块左侧对齐
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)) // 浅灰色背景
            };
            
            var changePercent = data.BtcPriceChangePercent;
            
            // 计算所有数据中的最大和最小涨跌幅
            var allChanges = allData.Select(d => d.BtcPriceChangePercent).ToList();
            var maxChange = allChanges.Max();
            var minChange = allChanges.Min();
            
            // 确定颜色：涨用红色，跌用绿色
            var barColor = changePercent >= 0 ? 
                Color.FromRgb(220, 53, 69) :  // 红色（涨）
                Color.FromRgb(40, 167, 69);   // 绿色（跌）
            
            // 计算柱子宽度：基于最小值和最大值的相对位置
            var range = maxChange - minChange;
            var barWidth = (double)(chartWidth - 10); // 基础宽度
            
            if (range > 0)
            {
                // 计算当前值在范围内的比例
                var ratio = (double)(changePercent - minChange) / (double)range;
                barWidth = ratio * ((double)chartWidth - 10.0);
                
                // 设置最小宽度，确保即使是最小值也有可见的柱子
                var minWidth = 10.0;
                barWidth = Math.Max(barWidth, minWidth);
            }
            
            var bar = new Border
            {
                Background = new SolidColorBrush(barColor),
                Width = barWidth,
                Height = chartHeight - 4, // 留出上下边距
                HorizontalAlignment = HorizontalAlignment.Left, // 从左侧开始
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(2)
            };
            
            // 创建Grid来放置柱子和文字
            var grid = new Grid();
            grid.Children.Add(bar);
            
            // 涨跌幅文字显示在左侧，黑色字体
            var changeText = new TextBlock
            {
                Text = $"{changePercent:F1}%",
                FontSize = 8,
                Foreground = new SolidColorBrush(Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
                Margin = new Thickness(2, 0, 0, 0)
            };
            
            // 成交额文字显示在右侧，黑色字体
            var volumeText = new TextBlock
            {
                Text = $"{(data.BtcQuoteVolume / 100000000):F0}亿", // 转换为亿
                FontSize = 8,
                Foreground = new SolidColorBrush(Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
                Margin = new Thickness(0, 0, 2, 0)
            };
            
            grid.Children.Add(changeText);
            grid.Children.Add(volumeText);
            container.Child = grid;
            
            return container;
        }
        
        /// <summary>
        /// 根据波动率获取颜色 - 白色到淡红色，逐渐加深到大红色
        /// 白色代表最低波动，大红色代表最高波动，平滑的红色渐变
        /// </summary>
        private Color GetVolatilityColor(double volatility)
        {
            // 波动率 >= 40%：大红色
            if (volatility >= 0.40) return Color.FromRgb(220, 20, 60);   // 大红色 - 极高波动
            
            // 波动率 < 20%：白色
            if (volatility < 0.20) return Color.FromRgb(255, 255, 255);  // 白色 - 低波动
            
            // 中间20%-40%：从白色渐变到大红色，平滑的红色系渐变
            if (volatility >= 0.38) return Color.FromRgb(200, 50, 50);   // 中红色
            if (volatility >= 0.36) return Color.FromRgb(220, 80, 80);   // 浅红色
            if (volatility >= 0.34) return Color.FromRgb(240, 100, 100); // 更浅的红色
            if (volatility >= 0.32) return Color.FromRgb(250, 120, 120); // 淡红色
            if (volatility >= 0.30) return Color.FromRgb(255, 140, 140); // 浅粉红色
            if (volatility >= 0.28) return Color.FromRgb(255, 160, 160); // 更浅的粉红色
            if (volatility >= 0.26) return Color.FromRgb(255, 180, 180); // 浅粉红色
            if (volatility >= 0.24) return Color.FromRgb(255, 200, 200); // 更浅的粉红色
            if (volatility >= 0.22) return Color.FromRgb(255, 220, 220); // 非常浅的粉红色
            if (volatility >= 0.20) return Color.FromRgb(255, 240, 240); // 接近白色的淡粉红色
            
            return Color.FromRgb(255, 255, 255);                         // 默认白色
        }
        
        /// <summary>
        /// 根据背景颜色计算合适的文字颜色
        /// 白色背景使用黑色文字，红色背景使用白色文字
        /// </summary>
        private Color GetTextColor(Color backgroundColor)
        {
            // 计算背景色的亮度
            var brightness = (backgroundColor.R * 0.299 + backgroundColor.G * 0.587 + backgroundColor.B * 0.114);
            
            // 如果背景色较亮（接近白色），使用黑色文字
            // 如果背景色较暗（接近红色或深色），使用白色文字
            if (brightness > 180)
            {
                return Colors.Black;  // 浅色背景使用黑色文字
            }
            else
            {
                return Colors.White;  // 深色背景使用白色文字
            }
        }
        
        /// <summary>
        /// 显示波动率详细信息
        /// </summary>
        private void ShowVolatilityDetails(MarketVolatilityData data)
        {
            try
            {
                // 打开波动率详情窗口
                var detailsWindow = new VolatilityDetailsWindow(data.Date, data.TopVolatilitySymbols)
                {
                    Owner = this
                };
                
                detailsWindow.Show();
                
                Console.WriteLine($"📊 打开波动率详情窗口: {data.Date:yyyy-MM-dd}, 共{data.TopVolatilitySymbols.Count}个币种");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示波动率详情失败: {ex.Message}");
                MessageBox.Show($"打开波动率详情窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 位置比例分布图已移除 - 根据用户要求简化界面
        
        /// <summary>
        /// 创建单个柱状图
        /// </summary>
        private Grid CreateBarChart(string title, int count, int total, int maxCount, Color color, int columnIndex)
        {
            var barGrid = new Grid();
            Grid.SetColumn(barGrid, columnIndex);
            
            // 计算柱状图高度比例
            var heightRatio = maxCount > 0 ? (double)count / maxCount : 0;
            var barHeight = 200 * heightRatio; // 最大高度200px
            
            // 创建柱状图
            var bar = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(3),
                Width = 60,
                Height = barHeight,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10, 0, 10, 40)
            };
            
            // 创建数值标签
            var valueLabel = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 5)
            };
            
            // 创建标题标签
            var titleLabel = new TextBlock
            {
                Text = title,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };
            
            // 创建百分比标签
            var percentageLabel = new TextBlock
            {
                Text = $"{count * 100.0 / total:F1}%",
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2)
            };
            
            barGrid.Children.Add(bar);
            barGrid.Children.Add(valueLabel);
            barGrid.Children.Add(titleLabel);
            barGrid.Children.Add(percentageLabel);
            
            return barGrid;
        }
        
        /// <summary>
        /// 创建横向排列的柱状图（用于右侧30%宽度）
        /// </summary>
        private Grid CreateHorizontalBarChart(string title, int count, int total, int maxCount, Color color, int rowIndex)
        {
            var barGrid = new Grid();
            Grid.SetRow(barGrid, rowIndex);
            
            // 计算柱状图宽度比例
            var widthRatio = maxCount > 0 ? (double)count / maxCount : 0;
            var barWidth = 120 * widthRatio; // 在横向布局中，柱状图宽度根据数量比例计算
            
            // 创建标题标签
            var titleLabel = new TextBlock
            {
                Text = title,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                FontWeight = FontWeights.SemiBold,
                Width = 80
            };
            
            // 创建柱状图容器
            var barContainer = new Grid
            {
                Background = new SolidColorBrush(Colors.LightGray),
                Width = 130,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(85, 0, 0, 0)
            };
            
            // 创建柱状图
            var bar = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(3),
                Width = barWidth,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            };
            
            // 创建数值标签
            var valueLabel = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(220, 0, 0, 0)
            };
            
            // 创建百分比标签
            var percentageLabel = new TextBlock
            {
                Text = $"{count * 100.0 / total:F1}%",
                FontSize = 9,
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(270, 0, 0, 0)
            };
            
            barContainer.Children.Add(bar);
            barGrid.Children.Add(titleLabel);
            barGrid.Children.Add(barContainer);
            barGrid.Children.Add(valueLabel);
            barGrid.Children.Add(percentageLabel);
            
            return barGrid;
        }
        
        /// <summary>
        /// 创建图表图例
        /// </summary>
        private StackPanel CreateChartLegend(int lowCount, int midCount, int highCount, int ultraHighCount, int totalCount)
        {
            var legendPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };
            
            var legendItems = new[]
            {
                new { Color = Colors.Red, Name = "低位区域", Count = lowCount, Percentage = lowCount * 100.0 / totalCount },
                new { Color = Colors.Blue, Name = "中位区域", Count = midCount, Percentage = midCount * 100.0 / totalCount },
                new { Color = Colors.Green, Name = "高位区域", Count = highCount, Percentage = highCount * 100.0 / totalCount },
                new { Color = Colors.Orange, Name = "超高位", Count = ultraHighCount, Percentage = ultraHighCount * 100.0 / totalCount }
            };
            
            foreach (var item in legendItems)
            {
                var legendItem = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(15, 0, 15, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                // 颜色指示器
                var colorBox = new Border
                {
                    Background = new SolidColorBrush(item.Color),
                    Width = 16,
                    Height = 16,
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                
                // 图例文本
                var legendText = new TextBlock
                {
                    Text = $"{item.Name}: {item.Count}个 ({item.Percentage:F1}%)",
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                legendItem.Children.Add(colorBox);
                legendItem.Children.Add(legendText);
                legendPanel.Children.Add(legendItem);
            }
            
            return legendPanel;
        }
        
        #endregion
        
        #region 24H行情 - 已移除，功能已集成到综合信息仪表板
        
        /*
        /// <summary>
        /// 24H行情按钮点击事件
        /// </summary>
        private async void Btn24HMarket_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("🔄 开始获取24H行情数据...");
                txtTitle.Text = "24H行情";
                txtSubtitle.Text = "正在加载24小时行情数据，请稍候...";
                
                // 清空内容区域
                contentPanel.Children.Clear();
                
                // 显示加载提示
                var loadingPanel = new StackPanel 
                { 
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                loadingPanel.Children.Add(new TextBlock 
                { 
                    Text = "🔄 正在获取24H行情数据...", 
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                contentPanel.Children.Add(loadingPanel);
                
                // 获取24H行情数据
                await Display24HMarketDataAsync();
                
                Console.WriteLine("✅ 24H行情数据显示完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取24H行情失败: {ex.Message}");
                MessageBox.Show($"获取24H行情失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        */
        
        /// <summary>
        /// 显示24H行情数据
        /// </summary>
        private async Task Display24HMarketDataAsync()
        {
            try
            {
                // 获取所有永续合约的24H行情数据
                Console.WriteLine("📊 正在获取ticker数据...");
                var tickerData = await Get24HTickerDataAsync();
                
                if (tickerData == null || tickerData.Count == 0)
                {
                    Console.WriteLine("⚠️ 未获取到ticker数据");
                    var noDataPanel = new StackPanel 
                    { 
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    noDataPanel.Children.Add(new TextBlock 
                    { 
                        Text = "⚠️ 未获取到24H行情数据", 
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Colors.Orange),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    
                    contentPanel.Children.Clear();
                    contentPanel.Children.Add(noDataPanel);
                    return;
                }
                
                Console.WriteLine($"📈 获取到 {tickerData.Count} 个可交易合约的24H数据");
                
                // 获取前一天的K线数据进行成交额对比
                Console.WriteLine("📊 正在加载前一天K线数据...");
                var yesterdayData = await GetYesterdayVolumeDataAsync(tickerData);
                
                // 创建24H行情显示面板
                var marketPanel = Create24HMarketPanel(tickerData, yesterdayData);
                
                // 更新UI
                contentPanel.Children.Clear();
                contentPanel.Children.Add(marketPanel);
                
                txtSubtitle.Text = $"已加载 {tickerData.Count} 个可交易永续合约的24H行情数据 - 点击任意合约行可复制合约名到剪贴板";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 显示24H行情数据失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 获取24H ticker数据，过滤掉不可交易的合约
        /// </summary>
        private async Task<List<Market24HData>> Get24HTickerDataAsync()
        {
            try
            {
                Console.WriteLine("🔄 开始调用API获取ticker数据...");
                
                // 1. 获取所有可交易的合约信息
                Console.WriteLine("📋 正在获取可交易合约列表...");
                var allSymbols = await _apiClient.GetAllSymbolsInfoAsync();
                if (allSymbols == null || allSymbols.Count == 0)
                {
                    Console.WriteLine("⚠️ 未获取到合约信息，将不进行交易状态过滤");
                }
                
                // 创建可交易永续合约的集合，提高查找效率
                var tradingSymbols = new HashSet<string>();
                if (allSymbols != null)
                {
                    tradingSymbols = allSymbols
                        .Where(s => s.IsTrading && s.QuoteAsset == "USDT" && s.ContractType == ContractType.Perpetual)
                        .Select(s => s.Symbol)
                        .ToHashSet();
                    Console.WriteLine($"📈 找到 {tradingSymbols.Count} 个可交易的USDT永续合约");
                }
                
                // 2. 获取所有tick数据
                var allTicks = await _apiClient.GetAllTicksAsync();
                if (allTicks == null || allTicks.Count == 0)
                {
                    Console.WriteLine("⚠️ GetAllTicksAsync返回空数据");
                    return new List<Market24HData>();
                }
                
                Console.WriteLine($"📊 API返回 {allTicks.Count} 个tick数据");
                
                // 3. 筛选USDT合约
                var usdtTicks = allTicks.Where(t => t.Symbol.EndsWith("USDT")).ToList();
                Console.WriteLine($"📈 筛选出 {usdtTicks.Count} 个USDT合约");
                
                // 4. 过滤掉不可交易的或非永续合约
                if (tradingSymbols.Count > 0)
                {
                    var originalCount = usdtTicks.Count;
                    usdtTicks = usdtTicks.Where(t => tradingSymbols.Contains(t.Symbol)).ToList();
                    var filteredCount = originalCount - usdtTicks.Count;
                    Console.WriteLine($"🚫 过滤掉 {filteredCount} 个不可交易或非永续合约，剩余 {usdtTicks.Count} 个");
                }
                
                // 5. 转换为24H行情数据格式
                var result = new List<Market24HData>();
                foreach (var tick in usdtTicks)
                {
                    if (tick.LastPrice <= 0 || tick.Volume <= 0) continue;
                    
                    var marketData = new Market24HData
                    {
                        Symbol = tick.Symbol,
                        LastPrice = tick.LastPrice,
                        PriceChangePercent = tick.PriceChangePercent,
                        PriceChange = tick.PriceChange,
                        Volume = tick.Volume,
                        QuoteVolume = tick.QuoteVolume, // 24H成交额
                        HighPrice = tick.HighPrice,
                        LowPrice = tick.LowPrice,
                        OpenPrice = tick.OpenPrice,
                        LastUpdateTime = DateTime.Now
                    };
                    
                    result.Add(marketData);
                }
                
                Console.WriteLine($"✅ 成功转换 {result.Count} 个可交易合约的24H数据");
                return result.OrderByDescending(x => x.QuoteVolume).ToList(); // 按成交额排序
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取ticker数据失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 获取昨日成交量数据用于对比
        /// </summary>
        private Task<Dictionary<string, decimal>> GetYesterdayVolumeDataAsync(List<Market24HData> tickerData)
        {
            var yesterdayVolume = new Dictionary<string, decimal>();
            
            try
            {
                var yesterday = DateTime.Now.Date.AddDays(-1);
                Console.WriteLine($"📅 开始查找 {yesterday:yyyy-MM-dd} 的K线数据...");
                
                // 尝试从缓存的K线数据中获取昨日数据
                if (_allKlineData?.Count > 0)
                {
                    Console.WriteLine($"📊 从缓存中查找昨日数据，缓存中共有 {_allKlineData.Count} 条K线数据");
                    
                    foreach (var symbol in tickerData.Select(t => t.Symbol).Take(100)) // 限制处理数量
                    {
                        var symbolKlines = _allKlineData
                            .Where(k => k.Symbol == symbol && k.OpenTime.Date == yesterday)
                            .ToList();
                            
                        if (symbolKlines.Count > 0)
                        {
                            var totalVolume = symbolKlines.Sum(k => k.Volume * k.ClosePrice); // 转换为USDT成交额
                            yesterdayVolume[symbol] = totalVolume;
                        }
                    }
                    
                    Console.WriteLine($"✅ 从缓存中找到 {yesterdayVolume.Count} 个币种的昨日成交额数据");
                }
                else
                {
                    Console.WriteLine("⚠️ 缓存中没有K线数据，无法进行成交额对比");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取昨日成交量数据失败: {ex.Message}");
            }
            
                         return Task.FromResult(yesterdayVolume);
        }
        
        /// <summary>
        /// 创建24H行情显示面板
        /// </summary>
        private ScrollViewer Create24HMarketPanel(List<Market24HData> tickerData, Dictionary<string, decimal> yesterdayVolume)
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            
            var mainPanel = new StackPanel();
            
            // 标题
            mainPanel.Children.Add(new TextBlock 
            { 
                Text = "📈 24小时行情总览", 
                FontSize = 24, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            
            // 整体统计
            var statsPanel = Create24HStatsPanel(tickerData);
            mainPanel.Children.Add(statsPanel);
            
            // 涨跌幅排行榜
            var rankingPanel = Create24HRankingPanel(tickerData);
            mainPanel.Children.Add(rankingPanel);
            
            // 成交额放量排行榜
            if (yesterdayVolume.Count > 0)
            {
                var volumePanel = CreateVolumeGrowthPanel(tickerData, yesterdayVolume);
                mainPanel.Children.Add(volumePanel);
            }
            
            scrollViewer.Content = mainPanel;
            return scrollViewer;
        }
        
        /// <summary>
        /// 创建24H统计面板
        /// </summary>
        private StackPanel Create24HStatsPanel(List<Market24HData> tickerData)
        {
            var panel = new StackPanel();
            panel.Margin = new Thickness(0, 0, 0, 30);
            
            // 标题
            panel.Children.Add(new TextBlock 
            { 
                Text = "📊 24H市场统计", 
                FontSize = 18, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            
            // 统计数据
            var upCount = tickerData.Count(t => t.PriceChangePercent > 0);
            var downCount = tickerData.Count(t => t.PriceChangePercent < 0);
            var flatCount = tickerData.Count(t => t.PriceChangePercent == 0);
            var totalVolume = tickerData.Sum(t => t.QuoteVolume);
            
            // 统计框布局
            var statsContainer = new WrapPanel 
            { 
                HorizontalAlignment = HorizontalAlignment.Center 
            };
            
                         statsContainer.Children.Add(CreateStatBox("总合约数", tickerData.Count.ToString()));
             statsContainer.Children.Add(CreateStatBox("上涨数量", upCount.ToString()));
             statsContainer.Children.Add(CreateStatBox("下跌数量", downCount.ToString()));
             statsContainer.Children.Add(CreateStatBox("平盘数量", flatCount.ToString()));
             statsContainer.Children.Add(CreateStatBox("总成交额", $"{totalVolume / 1000000000:F1}B USDT"));
            
            panel.Children.Add(statsContainer);
            return panel;
        }
        
        /// <summary>
        /// 创建24H涨跌幅排行榜面板
        /// </summary>
        private StackPanel Create24HRankingPanel(List<Market24HData> tickerData)
        {
            var panel = new StackPanel();
            panel.Margin = new Thickness(0, 0, 0, 30);
            
            // 标题
            panel.Children.Add(new TextBlock 
            { 
                Text = "🏆 24H涨跌幅排行榜", 
                FontSize = 18, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            
            // 两列布局
            var rankingContainer = new Grid();
            rankingContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rankingContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 涨幅榜
            var gainersPanel = Create24HRankingList("📈 涨幅前十", tickerData.Where(t => t.PriceChangePercent > 0).OrderByDescending(t => t.PriceChangePercent).Take(10).ToList(), Colors.Green);
            Grid.SetColumn(gainersPanel, 0);
            rankingContainer.Children.Add(gainersPanel);
            
            // 跌幅榜
            var losersPanel = Create24HRankingList("📉 跌幅前十", tickerData.Where(t => t.PriceChangePercent < 0).OrderBy(t => t.PriceChangePercent).Take(10).ToList(), Colors.Red);
            Grid.SetColumn(losersPanel, 1);
            rankingContainer.Children.Add(losersPanel);
            
            panel.Children.Add(rankingContainer);
            return panel;
        }
        
        /// <summary>
        /// 创建24H排行榜列表
        /// </summary>
        private StackPanel Create24HRankingList(string title, List<Market24HData> data, Color color)
        {
            var panel = new StackPanel();
            panel.Margin = new Thickness(10);
            
            // 标题
            panel.Children.Add(new TextBlock 
            { 
                Text = title, 
                FontSize = 16, 
                FontWeight = FontWeights.Bold, 
                Foreground = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            
            // 列表
            var listView = new ListView();
            listView.MaxHeight = 350;
            listView.BorderThickness = new Thickness(1);
            listView.BorderBrush = new SolidColorBrush(Colors.LightGray);
            listView.Cursor = Cursors.Hand;
            
            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "排名", 
                Width = 50,
                DisplayMemberBinding = new Binding("Rank")
            });
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "交易对", 
                Width = 100,
                DisplayMemberBinding = new Binding("Symbol")
            });
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "价格", 
                Width = 80,
                DisplayMemberBinding = new Binding("LastPrice")
            });
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "涨跌幅", 
                Width = 80,
                DisplayMemberBinding = new Binding("PriceChangePercentText")
            });
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "成交额", 
                Width = 100,
                DisplayMemberBinding = new Binding("QuoteVolumeText")
            });
            
            listView.View = gridView;
            
            // 添加点击复制功能
            listView.SelectionChanged += (sender, e) =>
            {
                if (sender is ListView lv && lv.SelectedItem is Market24HRankingItem selectedItem)
                {
                    CopySymbolToClipboard(selectedItem.Symbol);
                    lv.SelectedItem = null; // 取消选择
                }
            };
            
            // 设置数据源
            var rankingData = data.Select((item, index) => new Market24HRankingItem
            {
                Rank = index + 1,
                Symbol = item.Symbol,
                LastPrice = $"{item.LastPrice:F4}",
                PriceChangePercentText = $"{item.PriceChangePercent:F2}%",
                QuoteVolumeText = $"{item.QuoteVolume / 1000000:F1}M"
            }).ToList();
            
            listView.ItemsSource = rankingData;
            panel.Children.Add(listView);
            
            return panel;
        }
        
        /// <summary>
        /// 计算过去10天平均成交额（不含当日）
        /// </summary>
        private decimal CalculatePast10DaysAvgVolume(string symbol)
        {
            try
            {
                // 获取该币种的K线数据
                var symbolKlines = _allKlineData
                    .Where(k => k.Symbol == symbol)
                    .OrderByDescending(k => k.OpenTime)
                    .ToList();
                
                if (symbolKlines.Count < 11) // 需要至少11天数据（当日+过去10天）
                {
                    return 0;
                }
                
                // 跳过当日（第一条），取过去10天
                var past10DaysKlines = symbolKlines.Skip(1).Take(10).ToList();
                
                if (past10DaysKlines.Count == 10)
                {
                    return past10DaysKlines.Average(k => k.QuoteVolume);
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 计算过去10天平均成交额失败: {symbol}, {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 创建成交额放量排行榜面板
        /// </summary>
        private StackPanel CreateVolumeGrowthPanel(List<Market24HData> tickerData, Dictionary<string, decimal> yesterdayVolume)
        {
            var panel = new StackPanel();
            panel.Margin = new Thickness(0, 0, 0, 30);
            
            // 标题
            panel.Children.Add(new TextBlock 
            { 
                Text = "🚀 24H明显放量前20", 
                FontSize = 18, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            
            // 计算放量数据
            var volumeGrowthData = new List<VolumeGrowthData>();
            
            foreach (var ticker in tickerData)
            {
                if (yesterdayVolume.ContainsKey(ticker.Symbol) && yesterdayVolume[ticker.Symbol] > 0)
                {
                    var yesterdayVol = yesterdayVolume[ticker.Symbol];
                    var todayVol = ticker.QuoteVolume;
                    var growthPercent = ((todayVol - yesterdayVol) / yesterdayVol) * 100;
                    
                    // 计算过去10天平均成交额
                    var past10DaysAvgVolume = CalculatePast10DaysAvgVolume(ticker.Symbol);
                    var volumeMultiple = past10DaysAvgVolume > 0 ? todayVol / past10DaysAvgVolume : 0;
                    
                    volumeGrowthData.Add(new VolumeGrowthData
                    {
                        Symbol = ticker.Symbol,
                        TodayVolume = todayVol,
                        YesterdayVolume = yesterdayVol,
                        GrowthPercent = growthPercent,
                        LastPrice = ticker.LastPrice,
                        PriceChangePercent = ticker.PriceChangePercent,
                        Past10DaysAvgVolume = past10DaysAvgVolume,
                        VolumeMultiple = volumeMultiple
                    });
                }
            }
            
            // 按增幅排序，取前20
            var top20VolumeGrowth = volumeGrowthData
                .Where(v => v.GrowthPercent > 0) // 只取放量的
                .OrderByDescending(v => v.GrowthPercent)
                .Take(20)
                .ToList();
                
            if (top20VolumeGrowth.Count == 0)
            {
                panel.Children.Add(new TextBlock 
                { 
                    Text = "暂无明显放量数据", 
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return panel;
            }
            
            // 创建列表
            var listView = new ListView();
            listView.MaxHeight = 500;
            listView.BorderThickness = new Thickness(1);
            listView.BorderBrush = new SolidColorBrush(Colors.LightGray);
            listView.Cursor = Cursors.Hand;
            _volumeListView = listView; // 保存引用以便排序更新
            
            var gridView = new GridView();
            
            // 创建可排序的列头
            gridView.Columns.Add(CreateSortableColumn("排名", "Rank", 50));
            gridView.Columns.Add(CreateSortableColumn("交易对", "Symbol", 100));
            gridView.Columns.Add(CreateSortableColumn("当前价", "LastPrice", 80, "LastPriceText"));
            gridView.Columns.Add(CreateSortableColumn("价格涨跌", "PriceChangePercent", 80, "PriceChangeText"));
            gridView.Columns.Add(CreateSortableColumn("昨日成交额", "YesterdayVolume", 100, "YesterdayVolumeText"));
            gridView.Columns.Add(CreateSortableColumn("今日成交额", "TodayVolume", 100, "TodayVolumeText"));
            gridView.Columns.Add(CreateSortableColumn("放量增幅", "GrowthPercent", 100, "GrowthPercentText"));
            gridView.Columns.Add(CreateSortableColumn("10日均额", "Past10DaysAvgVolume", 100, "Past10DaysAvgVolumeText"));
            gridView.Columns.Add(CreateSortableColumn("倍数", "VolumeMultiple", 80, "VolumeMultipleText"));
            
            listView.View = gridView;
            
            // 添加点击复制功能
            listView.SelectionChanged += (sender, e) =>
            {
                if (sender is ListView lv && lv.SelectedItem is VolumeGrowthDisplayItem selectedItem)
                {
                    CopySymbolToClipboard(selectedItem.Symbol);
                    lv.SelectedItem = null; // 取消选择
                }
            };
            
            // 设置数据源
            var displayData = top20VolumeGrowth.Select((item, index) => new VolumeGrowthDisplayItem
            {
                Rank = index + 1,
                Symbol = item.Symbol,
                LastPriceText = $"{item.LastPrice:F4}",
                PriceChangeText = $"{item.PriceChangePercent:F2}%",
                YesterdayVolumeText = $"{item.YesterdayVolume / 1000000:F1}M",
                TodayVolumeText = $"{item.TodayVolume / 1000000:F1}M",
                GrowthPercentText = $"+{item.GrowthPercent:F1}%",
                Past10DaysAvgVolumeText = $"{item.Past10DaysAvgVolume / 1000000:F1}M",
                VolumeMultipleText = $"{item.VolumeMultiple:F1}x",
                // 保存原始数值用于排序
                LastPrice = item.LastPrice,
                PriceChangePercent = item.PriceChangePercent,
                YesterdayVolume = item.YesterdayVolume,
                TodayVolume = item.TodayVolume,
                GrowthPercent = item.GrowthPercent,
                Past10DaysAvgVolume = item.Past10DaysAvgVolume,
                VolumeMultiple = item.VolumeMultiple
            }).ToList();
            
            _currentVolumeData = displayData; // 保存当前数据
            listView.ItemsSource = displayData;
            panel.Children.Add(listView);
            
            return panel;
        }
        
        /// <summary>
        /// 创建可排序的列
        /// </summary>
        private GridViewColumn CreateSortableColumn(string headerText, string sortProperty, double width, string? displayProperty = null)
        {
            var column = new GridViewColumn { Width = width };
            
            // 创建可点击的按钮作为列头
            var headerButton = new Button
            {
                Content = headerText,
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),    // 深蓝灰色背景
                Foreground = new SolidColorBrush(Colors.White),                  // 白色文字
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80)),   // 更深的边框
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 6, 8, 6),
                Cursor = Cursors.Hand,
                Height = 32
            };
            
            // 添加悬停效果
            headerButton.MouseEnter += (sender, e) => 
            {
                if (sender is Button btn)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(70, 90, 110)); // 悬停时变亮
                }
            };
            
            headerButton.MouseLeave += (sender, e) => 
            {
                if (sender is Button btn)
                {
                    // 恢复原始颜色或排序激活颜色
                    var columnProp = GetColumnProperty(btn.Content.ToString()?.Replace(" ↑", "").Replace(" ↓", "") ?? "");
                    if (columnProp == _currentSortColumn)
                    {
                        btn.Background = new SolidColorBrush(Color.FromRgb(41, 128, 185)); // 排序激活色
                    }
                    else
                    {
                        btn.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // 默认色
                    }
                }
            };
            
            // 添加排序点击事件
            headerButton.Click += (sender, e) => SortVolumeData(sortProperty);
            
            column.Header = headerButton;
            
            // 设置数据绑定
            if (!string.IsNullOrEmpty(displayProperty))
            {
                column.DisplayMemberBinding = new Binding(displayProperty);
            }
            else
            {
                column.DisplayMemberBinding = new Binding(sortProperty);
            }
            
            return column;
        }
        
        /// <summary>
        /// 排序放量数据
        /// </summary>
        private void SortVolumeData(string property)
        {
            if (_currentVolumeData == null || _currentVolumeData.Count == 0 || _volumeListView == null)
                return;
                
            // 确定排序方向
            if (_currentSortColumn == property)
            {
                _isAscending = !_isAscending; // 切换排序方向
            }
            else
            {
                _currentSortColumn = property;
                _isAscending = false; // 默认降序
            }
            
            // 执行排序
            List<VolumeGrowthDisplayItem> sortedData;
            
            switch (property)
            {
                case "Rank":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.Rank).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.Rank).ToList();
                    break;
                case "Symbol":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.Symbol).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.Symbol).ToList();
                    break;
                case "LastPrice":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.LastPrice).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.LastPrice).ToList();
                    break;
                case "PriceChangePercent":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.PriceChangePercent).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.PriceChangePercent).ToList();
                    break;
                case "YesterdayVolume":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.YesterdayVolume).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.YesterdayVolume).ToList();
                    break;
                case "TodayVolume":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.TodayVolume).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.TodayVolume).ToList();
                    break;
                case "GrowthPercent":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.GrowthPercent).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.GrowthPercent).ToList();
                    break;
                case "Past10DaysAvgVolume":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.Past10DaysAvgVolume).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.Past10DaysAvgVolume).ToList();
                    break;
                case "VolumeMultiple":
                    sortedData = _isAscending ? 
                        _currentVolumeData.OrderBy(x => x.VolumeMultiple).ToList() :
                        _currentVolumeData.OrderByDescending(x => x.VolumeMultiple).ToList();
                    break;
                default:
                    return;
            }
            
            // 更新排名
            for (int i = 0; i < sortedData.Count; i++)
            {
                sortedData[i].Rank = i + 1;
            }
            
            // 更新数据源
            _currentVolumeData = sortedData;
            _volumeListView.ItemsSource = null;
            _volumeListView.ItemsSource = sortedData;
            
            // 更新列头显示排序状态
            UpdateColumnHeaders();
            
            Console.WriteLine($"📊 按 {property} {(_isAscending ? "升序" : "降序")} 排序完成");
        }
        
        /// <summary>
        /// 更新列头显示排序状态
        /// </summary>
        private void UpdateColumnHeaders()
        {
            if (_volumeListView?.View is GridView gridView)
            {
                foreach (var column in gridView.Columns)
                {
                    if (column.Header is Button button)
                    {
                        var content = button.Content.ToString();
                        if (content != null)
                        {
                            // 移除之前的排序指示符
                            var cleanContent = content.Replace(" ↑", "").Replace(" ↓", "");
                            
                            // 添加当前排序指示符
                            var columnProperty = GetColumnProperty(cleanContent);
                            if (columnProperty == _currentSortColumn)
                            {
                                button.Content = cleanContent + (_isAscending ? " ↑" : " ↓");
                                button.Background = new SolidColorBrush(Color.FromRgb(41, 128, 185)); // 蓝色激活状态
                                button.Foreground = new SolidColorBrush(Colors.White);
                                button.FontWeight = FontWeights.Bold; // 加粗当前排序列
                            }
                            else
                            {
                                button.Content = cleanContent;
                                button.Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)); // 默认深蓝灰色
                                button.Foreground = new SolidColorBrush(Colors.White);
                                button.FontWeight = FontWeights.SemiBold; // 普通粗体
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 根据列头文本获取对应的属性名
        /// </summary>
        private string GetColumnProperty(string headerText)
        {
            return headerText switch
            {
                "排名" => "Rank",
                "交易对" => "Symbol",
                "当前价" => "LastPrice",
                "价格涨跌" => "PriceChangePercent",
                "昨日成交额" => "YesterdayVolume",
                "今日成交额" => "TodayVolume",
                "放量增幅" => "GrowthPercent",
                "10日均额" => "Past10DaysAvgVolume",
                "倍数" => "VolumeMultiple",
                _ => ""
            };
        }
        

        
        #region 涨速排行榜功能
        
        /// <summary>
        /// 涨速排行榜按钮点击事件
        /// </summary>
        private void BtnPriceSpeedRanking_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtTitle.Text = "涨速排行榜";
                txtSubtitle.Text = "实时监控永续合约的涨跌速度排行榜";
                
                // 清空内容区域
                contentPanel.Children.Clear();
                
                // 创建涨速排行榜面板
                var speedRankingPanel = CreatePriceSpeedRankingPanel();
                contentPanel.Children.Add(speedRankingPanel);
                
                Console.WriteLine("✅ 涨速排行榜界面已打开");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 打开涨速排行榜失败: {ex.Message}");
                MessageBox.Show($"打开涨速排行榜失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 创建涨速排行榜面板
        /// </summary>
        private ScrollViewer CreatePriceSpeedRankingPanel()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            
            var mainPanel = new StackPanel();
            
            // 标题
            mainPanel.Children.Add(new TextBlock 
            { 
                Text = "🚀 涨速排行榜", 
                FontSize = 24, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            
            // 控制面板
            var controlPanel = CreateSpeedRankingControlPanel();
            mainPanel.Children.Add(controlPanel);
            
            // 涨幅板块
            var risePanel = CreateRiseRankingPanel();
            mainPanel.Children.Add(risePanel);
            
            // 跌幅板块
            var fallPanel = CreateFallRankingPanel();
            mainPanel.Children.Add(fallPanel);
            
            scrollViewer.Content = mainPanel;
            return scrollViewer;
        }
        
        /// <summary>
        /// 创建控制面板
        /// </summary>
        private Border CreateSpeedRankingControlPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // 时间间隔设置
            panel.Children.Add(new TextBlock 
            { 
                Text = "监控间隔:", 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            
            var intervalTextBox = new TextBox 
            { 
                Name = "txtInterval",
                Text = _intervalSeconds.ToString(),
                Width = 60,
                Height = 30,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            
            panel.Children.Add(intervalTextBox);
            panel.Children.Add(new TextBlock 
            { 
                Text = "秒", 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 20, 0)
            });
            
            // 启动按钮
            var startButton = new Button 
            { 
                Content = "启动监控",
                Width = 100,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)),
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 10, 0)
            };
            startButton.Click += (s, e) => StartPriceSpeedMonitoring(intervalTextBox);
            panel.Children.Add(startButton);
            
            // 停止按钮
            var stopButton = new Button 
            { 
                Content = "停止监控",
                Width = 100,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 10, 0)
            };
            stopButton.Click += (s, e) => StopPriceSpeedMonitoring();
            panel.Children.Add(stopButton);
            
            // 清零按钮
            var resetButton = new Button 
            { 
                Content = "清零统计",
                Width = 100,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                Foreground = new SolidColorBrush(Colors.Black),
                Margin = new Thickness(0, 0, 10, 0)
            };
            resetButton.Click += (s, e) => ResetRankingCounts();
            panel.Children.Add(resetButton);
            
            // 手工重置按钮
            var manualResetButton = new Button 
            { 
                Content = "手工重置",
                Width = 100,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 20, 0)
            };
            manualResetButton.Click += (s, e) => ManualResetRankingCounts();
            panel.Children.Add(manualResetButton);
            
            // 状态显示
            var statusText = new TextBlock 
            { 
                Name = "txtMonitorStatus",
                Text = "监控状态: 未启动",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Red),
                Margin = new Thickness(0, 0, 15, 0)
            };
            panel.Children.Add(statusText);
            
            // 重置时间显示
            var resetTimeText = new TextBlock 
            { 
                Name = "txtLastResetTime",
                Text = $"上次重置: {_lastResetDate:MM-dd HH:mm}",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray)
            };
            panel.Children.Add(resetTimeText);
            
            border.Child = panel;
            return border;
        }
        
        /// <summary>
        /// 创建涨幅排行板块
        /// </summary>
        private Border CreateRiseRankingPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 标题
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            titlePanel.Children.Add(new TextBlock 
            { 
                Text = "📈 涨幅榜", 
                FontSize = 18, 
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                Margin = new Thickness(0, 0, 0, 15)
            });
            Grid.SetColumnSpan(titlePanel, 2);
            mainGrid.Children.Add(titlePanel);
            
            // 左侧：当前涨幅排名
            var leftPanel = new StackPanel { Margin = new Thickness(0, 40, 10, 0) };
            leftPanel.Children.Add(new TextBlock 
            { 
                Text = "当前涨幅排名 TOP10", 
                FontSize = 14, 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            var currentRiseList = new ListView 
            { 
                Name = "lvCurrentRise",
                Height = 320,
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                FontSize = 13
            };
            
            // 设置GridView
            var currentRiseGridView = new GridView();
            currentRiseGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "排名", 
                Width = 50,
                DisplayMemberBinding = new Binding("Rank")
            });
            currentRiseGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "合约", 
                Width = 140,
                DisplayMemberBinding = new Binding("Symbol")
            });
            currentRiseGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "瞬时涨幅", 
                Width = 85,
                DisplayMemberBinding = new Binding("ChangeText")
            });
            currentRiseGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "24H涨幅", 
                Width = 85,
                DisplayMemberBinding = new Binding("Price24hChangeText")
            });
            currentRiseGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "成交额", 
                Width = 90,
                DisplayMemberBinding = new Binding("QuoteVolumeText")
            });
            currentRiseGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "位置%", 
                Width = 70,
                DisplayMemberBinding = new Binding("PricePositionText")
            });
            currentRiseList.View = currentRiseGridView;
            currentRiseList.MouseDoubleClick += (s, e) => CopySymbolFromListView(s as ListView);
            leftPanel.Children.Add(currentRiseList);
            Grid.SetColumn(leftPanel, 0);
            mainGrid.Children.Add(leftPanel);
            
            // 右侧：累计上榜次数
            var rightPanel = new StackPanel { Margin = new Thickness(10, 40, 0, 0) };
            rightPanel.Children.Add(new TextBlock 
            { 
                Text = "累计上榜次数 TOP10", 
                FontSize = 14, 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            var riseCountList = new ListView 
            { 
                Name = "lvRiseCount",
                Height = 320,
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                FontSize = 13
            };
            
            // 设置GridView
            var riseCountGridView = new GridView();
            riseCountGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "排名", 
                Width = 60,
                DisplayMemberBinding = new Binding("Rank")
            });
            riseCountGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "合约", 
                Width = 140,
                DisplayMemberBinding = new Binding("Symbol")
            });
            riseCountGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "次数", 
                Width = 80,
                DisplayMemberBinding = new Binding("CountText")
            });
            riseCountList.View = riseCountGridView;
            riseCountList.MouseDoubleClick += (s, e) => CopySymbolFromListView(s as ListView);
            rightPanel.Children.Add(riseCountList);
            Grid.SetColumn(rightPanel, 1);
            mainGrid.Children.Add(rightPanel);
            
            border.Child = mainGrid;
            return border;
        }
        
        /// <summary>
        /// 创建跌幅排行板块
        /// </summary>
        private Border CreateFallRankingPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 标题
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            titlePanel.Children.Add(new TextBlock 
            { 
                Text = "📉 跌幅榜", 
                FontSize = 18, 
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                Margin = new Thickness(0, 0, 0, 15)
            });
            Grid.SetColumnSpan(titlePanel, 2);
            mainGrid.Children.Add(titlePanel);
            
            // 左侧：当前跌幅排名
            var leftPanel = new StackPanel { Margin = new Thickness(0, 40, 10, 0) };
            leftPanel.Children.Add(new TextBlock 
            { 
                Text = "当前跌幅排名 TOP10", 
                FontSize = 14, 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            var currentFallList = new ListView 
            { 
                Name = "lvCurrentFall",
                Height = 320,
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                FontSize = 13
            };
            
            // 设置GridView
            var currentFallGridView = new GridView();
            currentFallGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "排名", 
                Width = 50,
                DisplayMemberBinding = new Binding("Rank")
            });
            currentFallGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "合约", 
                Width = 140,
                DisplayMemberBinding = new Binding("Symbol")
            });
            currentFallGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "瞬时跌幅", 
                Width = 85,
                DisplayMemberBinding = new Binding("ChangeText")
            });
            currentFallGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "24H涨幅", 
                Width = 85,
                DisplayMemberBinding = new Binding("Price24hChangeText")
            });
            currentFallGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "成交额", 
                Width = 90,
                DisplayMemberBinding = new Binding("QuoteVolumeText")
            });
            currentFallGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "位置%", 
                Width = 70,
                DisplayMemberBinding = new Binding("PricePositionText")
            });
            currentFallList.View = currentFallGridView;
            currentFallList.MouseDoubleClick += (s, e) => CopySymbolFromListView(s as ListView);
            leftPanel.Children.Add(currentFallList);
            Grid.SetColumn(leftPanel, 0);
            mainGrid.Children.Add(leftPanel);
            
            // 右侧：累计上榜次数
            var rightPanel = new StackPanel { Margin = new Thickness(10, 40, 0, 0) };
            rightPanel.Children.Add(new TextBlock 
            { 
                Text = "累计上榜次数 TOP10", 
                FontSize = 14, 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            
            var fallCountList = new ListView 
            { 
                Name = "lvFallCount",
                Height = 320,
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                FontSize = 13
            };
            
            // 设置GridView
            var fallCountGridView = new GridView();
            fallCountGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "排名", 
                Width = 60,
                DisplayMemberBinding = new Binding("Rank")
            });
            fallCountGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "合约", 
                Width = 140,
                DisplayMemberBinding = new Binding("Symbol")
            });
            fallCountGridView.Columns.Add(new GridViewColumn 
            { 
                Header = "次数", 
                Width = 80,
                DisplayMemberBinding = new Binding("CountText")
            });
            fallCountList.View = fallCountGridView;
            fallCountList.MouseDoubleClick += (s, e) => CopySymbolFromListView(s as ListView);
            rightPanel.Children.Add(fallCountList);
            Grid.SetColumn(rightPanel, 1);
            mainGrid.Children.Add(rightPanel);
            
            border.Child = mainGrid;
            return border;
        }
        
        /// <summary>
        /// 启动涨速监控
        /// </summary>
        private void StartPriceSpeedMonitoring(TextBox intervalTextBox)
        {
            try
            {
                if (_isPriceSpeedRunning)
                {
                    MessageBox.Show("监控已在运行中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 解析时间间隔
                if (int.TryParse(intervalTextBox.Text, out int interval) && interval > 0)
                {
                    _intervalSeconds = interval;
                }
                else
                {
                    MessageBox.Show("请输入有效的时间间隔（秒）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                _isPriceSpeedRunning = true;
                
                // 启动定时器
                _priceSpeedTimer = new System.Threading.Timer(
                    UpdatePriceSpeedRanking, 
                    null, 
                    TimeSpan.Zero, 
                    TimeSpan.FromSeconds(_intervalSeconds)
                );
                
                // 启动每日自动重置定时器
                StartDailyResetTimer();
                
                // 更新状态显示
                UpdateMonitorStatus("监控状态: 运行中", Colors.Green);
                
                Console.WriteLine($"✅ 涨速监控已启动，间隔 {_intervalSeconds} 秒");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动涨速监控失败: {ex.Message}");
                MessageBox.Show($"启动监控失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 停止涨速监控
        /// </summary>
        private void StopPriceSpeedMonitoring()
        {
            try
            {
                _isPriceSpeedRunning = false;
                _priceSpeedTimer?.Dispose();
                _priceSpeedTimer = null;
                
                // 停止每日自动重置定时器
                StopDailyResetTimer();
                
                // 更新状态显示
                UpdateMonitorStatus("监控状态: 已停止", Colors.Red);
                
                Console.WriteLine("⏹️ 涨速监控已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 停止涨速监控失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清零统计次数
        /// </summary>
        private void ResetRankingCounts()
        {
            try
            {
                _riseRankingCount.Clear();
                _fallRankingCount.Clear();
                _priceHistory.Clear();
                
                // 刷新显示
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateRankingCountsDisplay();
                });
                
                Console.WriteLine("🔄 排行榜统计已清零");
                MessageBox.Show("排行榜统计已清零", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清零统计失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 手工重置排行榜
        /// </summary>
        private void ManualResetRankingCounts()
        {
            try
            {
                var result = MessageBox.Show(
                    "确定要手工重置排行榜吗？\n这将清空所有统计数据并重新开始计算。", 
                    "确认重置", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question
                );
                
                if (result == MessageBoxResult.Yes)
                {
                    ResetRankingCountsInternal("手工重置");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 手工重置失败: {ex.Message}");
                MessageBox.Show($"手工重置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 内部重置方法
        /// </summary>
        private void ResetRankingCountsInternal(string resetType)
        {
            try
            {
                _riseRankingCount.Clear();
                _fallRankingCount.Clear();
                _priceHistory.Clear();
                _lastResetDate = DateTime.Now;
                
                // 刷新显示
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateRankingCountsDisplay();
                    UpdateLastResetTimeDisplay();
                });
                
                Console.WriteLine($"🔄 排行榜统计已重置 ({resetType})");
                
                if (resetType == "手工重置")
                {
                    MessageBox.Show("排行榜统计已重置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {resetType}失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 启动每日自动重置定时器
        /// </summary>
        private void StartDailyResetTimer()
        {
            try
            {
                // 计算到明天0点的时间
                var now = DateTime.Now;
                var tomorrow = now.Date.AddDays(1);
                var timeToMidnight = tomorrow - now;
                
                // 启动定时器，第一次在明天0点触发，然后每24小时触发一次
                _dailyResetTimer = new System.Threading.Timer(
                    DailyResetCallback,
                    null,
                    timeToMidnight,
                    TimeSpan.FromDays(1)
                );
                
                Console.WriteLine($"⏰ 每日自动重置定时器已启动，将在 {tomorrow:yyyy-MM-dd 00:00:00} 首次重置");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动每日重置定时器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 每日重置回调
        /// </summary>
        private void DailyResetCallback(object? state)
        {
            try
            {
                var today = DateTime.Today;
                
                // 检查是否需要重置（避免重复重置）
                if (today > _lastResetDate.Date)
                {
                    ResetRankingCountsInternal("每日自动重置");
                    Console.WriteLine($"🌅 每日自动重置已执行: {today:yyyy-MM-dd}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 每日自动重置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 停止每日自动重置定时器
        /// </summary>
        private void StopDailyResetTimer()
        {
            try
            {
                _dailyResetTimer?.Dispose();
                _dailyResetTimer = null;
                Console.WriteLine("⏹️ 每日自动重置定时器已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 停止每日重置定时器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新上次重置时间显示
        /// </summary>
        private void UpdateLastResetTimeDisplay()
        {
            try
            {
                if (contentPanel.Children.Count > 0 && 
                    contentPanel.Children[0] is ScrollViewer scrollViewer &&
                    scrollViewer.Content is StackPanel mainPanel)
                {
                    foreach (var child in mainPanel.Children)
                    {
                        if (child is Border border && border.Child is StackPanel panel)
                        {
                            foreach (var item in panel.Children)
                            {
                                if (item is TextBlock textBlock && textBlock.Name == "txtLastResetTime")
                                {
                                    textBlock.Text = $"上次重置: {_lastResetDate:MM-dd HH:mm}";
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 更新重置时间显示失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新监控状态显示
        /// </summary>
        private void UpdateMonitorStatus(string text, Color color)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (contentPanel.Children.Count > 0 && 
                    contentPanel.Children[0] is ScrollViewer scrollViewer &&
                    scrollViewer.Content is StackPanel mainPanel)
                {
                    foreach (var child in mainPanel.Children)
                    {
                        if (child is Border border && border.Child is StackPanel panel)
                        {
                            foreach (var item in panel.Children)
                            {
                                if (item is TextBlock textBlock && textBlock.Name == "txtMonitorStatus")
                                {
                                    textBlock.Text = text;
                                    textBlock.Foreground = new SolidColorBrush(color);
                                    return;
                                }
                            }
                        }
                    }
                }
            });
        }
        
        /// <summary>
        /// 更新涨速排行榜
        /// </summary>
        private async void UpdatePriceSpeedRanking(object? state)
        {
            if (!_isPriceSpeedRunning) return;
            
            try
            {
                // 获取当前价格数据
                var tickerData = await Get24HTickerDataAsync();
                if (tickerData == null || tickerData.Count == 0) return;
                
                var currentPrices = new Dictionary<string, decimal>();
                foreach (var ticker in tickerData)
                {
                    currentPrices[ticker.Symbol] = ticker.LastPrice;
                }
                
                // 计算涨跌幅
                var priceChanges = CalculatePriceChanges(currentPrices);
                
                // 更新排行榜
                var riseRanking = priceChanges
                    .Where(p => p.Value > 0)
                    .OrderByDescending(p => p.Value)
                    .Take(10)
                    .ToList();
                    
                var fallRanking = priceChanges
                    .Where(p => p.Value < 0)
                    .OrderBy(p => p.Value)
                    .Take(10)
                    .ToList();
                
                // 统计上榜次数（涨跌互相抵消）
                foreach (var item in riseRanking)
                {
                    var symbol = item.Key;
                    
                    // 如果之前在跌幅榜有记录，先减1（抵消）
                    if (_fallRankingCount.ContainsKey(symbol) && _fallRankingCount[symbol] > 0)
                    {
                        _fallRankingCount[symbol]--;
                        if (_fallRankingCount[symbol] == 0)
                            _fallRankingCount.Remove(symbol);
                    }
                    else
                    {
                        // 增加涨幅榜次数
                        if (_riseRankingCount.ContainsKey(symbol))
                            _riseRankingCount[symbol]++;
                        else
                            _riseRankingCount[symbol] = 1;
                    }
                }
                
                foreach (var item in fallRanking)
                {
                    var symbol = item.Key;
                    
                    // 如果之前在涨幅榜有记录，先减1（抵消）
                    if (_riseRankingCount.ContainsKey(symbol) && _riseRankingCount[symbol] > 0)
                    {
                        _riseRankingCount[symbol]--;
                        if (_riseRankingCount[symbol] == 0)
                            _riseRankingCount.Remove(symbol);
                    }
                    else
                    {
                        // 增加跌幅榜次数
                        if (_fallRankingCount.ContainsKey(symbol))
                            _fallRankingCount[symbol]++;
                        else
                            _fallRankingCount[symbol] = 1;
                    }
                }
                
                // 更新UI显示
                await Dispatcher.BeginInvoke(() =>
                {
                    UpdateCurrentRankingDisplay(riseRanking, fallRanking, tickerData);
                    UpdateRankingCountsDisplay();
                });
                
                Console.WriteLine($"🔄 涨速排行榜已更新 - 涨幅榜{riseRanking.Count}个，跌幅榜{fallRanking.Count}个");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 更新涨速排行榜失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 计算价格变化
        /// </summary>
        private Dictionary<string, decimal> CalculatePriceChanges(Dictionary<string, decimal> currentPrices)
        {
            var changes = new Dictionary<string, decimal>();
            
            foreach (var current in currentPrices)
            {
                var symbol = current.Key;
                var currentPrice = current.Value;
                
                // 初始化价格历史
                if (!_priceHistory.ContainsKey(symbol))
                {
                    _priceHistory[symbol] = new List<decimal>();
                }
                
                var history = _priceHistory[symbol];
                history.Add(currentPrice);
                
                // 只保留足够的历史数据（当前+过去的记录）
                if (history.Count > 2)
                {
                    history.RemoveAt(0);
                }
                
                // 计算涨跌幅（如果有历史数据）
                if (history.Count >= 2)
                {
                    var previousPrice = history[history.Count - 2];
                    if (previousPrice > 0)
                    {
                        var changePercent = ((currentPrice - previousPrice) / previousPrice) * 100;
                        changes[symbol] = changePercent;
                    }
                }
            }
            
            return changes;
        }
        
        /// <summary>
        /// 更新当前排行榜显示
        /// </summary>
        private void UpdateCurrentRankingDisplay(List<KeyValuePair<string, decimal>> riseRanking, List<KeyValuePair<string, decimal>> fallRanking, List<Market24HData> tickerData)
        {
            try
            {
                // 创建ticker数据字典以便快速查找
                var tickerDict = tickerData.ToDictionary(t => t.Symbol, t => t);
                
                // 更新涨幅榜
                var currentRiseList = FindListViewByName("lvCurrentRise");
                if (currentRiseList != null)
                {
                    var riseItems = riseRanking.Select((item, index) => 
                    {
                        var ticker = tickerDict.GetValueOrDefault(item.Key);
                        var pricePosition = CalculatePricePosition(item.Key, ticker);
                        
                        return new SpeedRankingItem
                        {
                            Rank = index + 1,
                            Symbol = item.Key,
                            ChangePercent = item.Value,
                            ChangeText = $"+{item.Value:F2}%",
                            ChangeColor = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                            
                            // 新增字段
                            Price24hChangePercent = ticker?.PriceChangePercent ?? 0,
                            Price24hChangeText = ticker != null ? $"{ticker.PriceChangePercent:F2}%" : "N/A",
                            QuoteVolume = ticker?.QuoteVolume ?? 0,
                            QuoteVolumeText = ticker != null ? $"{ticker.QuoteVolume / 1000000:F1}M" : "N/A",
                            PricePositionPercent = pricePosition,
                            PricePositionText = $"{pricePosition:F1}%"
                        };
                    }).ToList();
                    
                    currentRiseList.ItemsSource = riseItems;
                }
                
                // 更新跌幅榜
                var currentFallList = FindListViewByName("lvCurrentFall");
                if (currentFallList != null)
                {
                    var fallItems = fallRanking.Select((item, index) => 
                    {
                        var ticker = tickerDict.GetValueOrDefault(item.Key);
                        var pricePosition = CalculatePricePosition(item.Key, ticker);
                        
                        return new SpeedRankingItem
                        {
                            Rank = index + 1,
                            Symbol = item.Key,
                            ChangePercent = item.Value,
                            ChangeText = $"{item.Value:F2}%",
                            ChangeColor = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                            
                            // 新增字段
                            Price24hChangePercent = ticker?.PriceChangePercent ?? 0,
                            Price24hChangeText = ticker != null ? $"{ticker.PriceChangePercent:F2}%" : "N/A",
                            QuoteVolume = ticker?.QuoteVolume ?? 0,
                            QuoteVolumeText = ticker != null ? $"{ticker.QuoteVolume / 1000000:F1}M" : "N/A",
                            PricePositionPercent = pricePosition,
                            PricePositionText = $"{pricePosition:F1}%"
                        };
                    }).ToList();
                    
                    currentFallList.ItemsSource = fallItems;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 更新当前排行榜显示失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 计算价格位置百分比
        /// </summary>
        private decimal CalculatePricePosition(string symbol, Market24HData? ticker)
        {
            try
            {
                if (ticker == null || ticker.HighPrice <= ticker.LowPrice)
                    return 0;
                
                // 计算当前价格在24H高低价范围内的位置百分比
                var range = ticker.HighPrice - ticker.LowPrice;
                var position = ticker.LastPrice - ticker.LowPrice;
                return (position / range) * 100;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 计算价格位置失败: {symbol}, {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// 更新上榜次数显示
        /// </summary>
        private void UpdateRankingCountsDisplay()
        {
            try
            {
                // 更新涨幅次数榜
                var riseCountList = FindListViewByName("lvRiseCount");
                if (riseCountList != null)
                {
                    var riseCountItems = _riseRankingCount
                        .OrderByDescending(kv => kv.Value)
                        .Take(10)
                        .Select((item, index) => new RankingCountItem
                        {
                            Rank = index + 1,
                            Symbol = item.Key,
                            Count = item.Value,
                            CountText = $"{item.Value} 次"
                        }).ToList();
                    
                    riseCountList.ItemsSource = riseCountItems;
                }
                
                // 更新跌幅次数榜
                var fallCountList = FindListViewByName("lvFallCount");
                if (fallCountList != null)
                {
                    var fallCountItems = _fallRankingCount
                        .OrderByDescending(kv => kv.Value)
                        .Take(10)
                        .Select((item, index) => new RankingCountItem
                        {
                            Rank = index + 1,
                            Symbol = item.Key,
                            Count = item.Value,
                            CountText = $"{item.Value} 次"
                        }).ToList();
                    
                    fallCountList.ItemsSource = fallCountItems;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 更新上榜次数显示失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 根据名称查找ListView
        /// </summary>
        private ListView? FindListViewByName(string name)
        {
            if (contentPanel.Children.Count > 0 && 
                contentPanel.Children[0] is ScrollViewer scrollViewer &&
                scrollViewer.Content is StackPanel mainPanel)
            {
                foreach (var child in mainPanel.Children)
                {
                    if (child is Border border)
                    {
                        var listView = FindListViewInElement(border, name);
                        if (listView != null) return listView;
                    }
                }
            }
            return null;
        }
        
        /// <summary>
        /// 在元素中递归查找ListView
        /// </summary>
        private ListView? FindListViewInElement(DependencyObject element, string name)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                
                if (child is ListView listView && listView.Name == name)
                {
                    return listView;
                }
                
                var result = FindListViewInElement(child, name);
                if (result != null) return result;
            }
            return null;
        }
        
        #endregion
        
        #endregion
        
        #region 涨速排行榜数据模型
        
        /// <summary>
        /// 涨速排行榜项目
        /// </summary>
        public class SpeedRankingItem
        {
            public int Rank { get; set; }
            public string Symbol { get; set; } = "";
            public decimal ChangePercent { get; set; }
            public string ChangeText { get; set; } = "";
            public SolidColorBrush ChangeColor { get; set; } = new(Colors.Black);
            
            // 新增字段
            public decimal Price24hChangePercent { get; set; } // 24H涨幅
            public string Price24hChangeText { get; set; } = "";
            public decimal QuoteVolume { get; set; } // 24H成交额
            public string QuoteVolumeText { get; set; } = "";
            public decimal PricePositionPercent { get; set; } // 价格位置百分比
            public string PricePositionText { get; set; } = "";
        }
        
        /// <summary>
        /// 上榜次数统计项目
        /// </summary>
        public class RankingCountItem
        {
            public int Rank { get; set; }
            public string Symbol { get; set; } = "";
            public int Count { get; set; }
            public string CountText { get; set; } = "";
        }
        
        /// <summary>
        /// 从ListView复制合约名到剪贴板
        /// </summary>
        private void CopySymbolFromListView(ListView? listView)
        {
            if (listView?.SelectedItem == null) return;
            
            try
            {
                string? symbol = null;
                
                if (listView.SelectedItem is SpeedRankingItem speedItem)
                {
                    symbol = speedItem.Symbol;
                }
                else if (listView.SelectedItem is RankingCountItem countItem)
                {
                    symbol = countItem.Symbol;
                }
                
                if (!string.IsNullOrEmpty(symbol))
                {
                    TrySetClipboardText(symbol);
                }
            }
            catch (Exception ex)
            {
                // 静默处理错误，不弹出提示框
                Console.WriteLine($"复制失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建振幅波动分析面板
        /// </summary>
        private StackPanel CreateAmplitudeAnalysisPanel()
        {
            var panel = new StackPanel();
            panel.Margin = new Thickness(0, 30, 0, 0);
            
            // 标题
            var titleText = new TextBlock
            {
                Text = "📈 振幅波动分析",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(titleText);
            
            // 控制面板
            var controlPanel = CreateAmplitudeControlPanel();
            panel.Children.Add(controlPanel);
            
            // 数据显示区域（初始为空）
            var dataPanel = new StackPanel 
            { 
                Name = "amplitudeDataPanel",
                Margin = new Thickness(0, 20, 0, 0)
            };
            panel.Children.Add(dataPanel);
            
            return panel;
        }
        
        /// <summary>
        /// 创建振幅波动控制面板
        /// </summary>
        private StackPanel CreateAmplitudeControlPanel()
        {
            var panel = new StackPanel();
            panel.Orientation = Orientation.Horizontal;
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.Margin = new Thickness(0, 0, 0, 10);
            
            // 天数选择
            panel.Children.Add(new TextBlock 
            { 
                Text = "选择天数:", 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            
            var daysTextBox = new TextBox 
            { 
                Name = "txtAmplitudeDays",
                Width = 80,
                Height = 25,
                Text = _amplitudeAnalysisDays.ToString(),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };
            panel.Children.Add(daysTextBox);
            
            // 计算按钮
            var calculateButton = new Button 
            { 
                Content = "计算振幅波动",
                Width = 120,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 15, 0)
            };
            calculateButton.Click += BtnCalculateAmplitude_Click;
            panel.Children.Add(calculateButton);
            
            // 说明文字
            var infoText = new TextBlock 
            { 
                Text = "分类规则: <20%(超低) | 20-40%(中低) | 40-60%(中高) | >60%(超高)",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(infoText);
            
            return panel;
        }
        
        /// <summary>
        /// 计算振幅波动按钮点击事件
        /// </summary>
        private async void BtnCalculateAmplitude_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            
            try
            {
                button.IsEnabled = false;
                button.Content = "计算中...";
                
                // 获取天数
                var daysTextBox = FindChildByName<TextBox>(contentPanel, "txtAmplitudeDays");
                if (daysTextBox == null || !int.TryParse(daysTextBox.Text, out int days) || days <= 0)
                {
                    MessageBox.Show("请输入有效的天数（大于0的整数）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 保存振幅分析配置
                SaveAmplitudeAnalysisConfig(days);
                
                // 计算振幅波动数据
                var amplitudeData = await CalculateAmplitudeDataAsync(days);
                
                // 显示结果
                DisplayAmplitudeData(amplitudeData, days);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"计算振幅波动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = "计算振幅波动";
            }
        }
        
        /// <summary>
        /// 查找子控件
        /// </summary>
        private T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element && element.Name == name)
                    return element;
                
                var found = FindChildByName<T>(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// 显示天数输入对话框
        /// </summary>
        private int? ShowDaysInputDialog()
        {
            try
            {
                var dialog = new Window
                {
                    Title = "设置高低价分析天数",
                    Width = 400,
                    Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };

                var mainPanel = new StackPanel { Margin = new Thickness(20) };

                // 标题
                var titleText = new TextBlock
                {
                    Text = "高低价分析参数设置",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                mainPanel.Children.Add(titleText);

                // 说明文字
                var descText = new TextBlock
                {
                    Text = "请输入要分析的天数（用于计算最近N天的最高最低价）",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                mainPanel.Children.Add(descText);

                // 输入面板
                var inputPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) };
                
                inputPanel.Children.Add(new TextBlock 
                { 
                    Text = "天数:", 
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                });

                var daysTextBox = new TextBox 
                { 
                    Name = "txtDays",
                    Width = 80,
                    Height = 25,
                    Text = _highLowAnalysisDays.ToString(),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                inputPanel.Children.Add(daysTextBox);

                inputPanel.Children.Add(new TextBlock 
                { 
                    Text = "(范围: 1-90)",
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontSize = 11
                });

                mainPanel.Children.Add(inputPanel);

                // 按钮面板
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                
                var okButton = new Button 
                { 
                    Content = "确定",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    Foreground = new SolidColorBrush(Colors.White),
                    Tag = "OK"
                };
                
                var cancelButton = new Button 
                { 
                    Content = "取消",
                    Width = 80,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                    Foreground = new SolidColorBrush(Colors.White),
                    Tag = "Cancel"
                };

                okButton.Click += (s, e) => dialog.DialogResult = true;
                cancelButton.Click += (s, e) => dialog.DialogResult = false;

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                mainPanel.Children.Add(buttonPanel);

                dialog.Content = mainPanel;

                // 自动选中文本框内容
                daysTextBox.Focus();
                daysTextBox.SelectAll();

                var result = dialog.ShowDialog();
                
                if (result == true)
                {
                    if (int.TryParse(daysTextBox.Text, out int days) && days >= 1 && days <= 90)
                    {
                        return days;
                    }
                    else
                    {
                        MessageBox.Show("请输入1-90之间的有效天数", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return ShowDaysInputDialog(); // 递归调用重新显示对话框
                    }
                }
                
                return null; // 用户取消
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示输入对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// 创建涨跌数据统计面板
        /// </summary>
        private async Task<StackPanel> CreatePriceChangeStatsPanel()
        {
            try
            {
                var panel = new StackPanel { Margin = new Thickness(0, 30, 0, 0) };
                
                // 标题
                var titleBlock = new TextBlock
                {
                    Text = "📊 涨跌数据统计（最近30个交易日）",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                panel.Children.Add(titleBlock);
                
                // 计算涨跌统计数据
                var priceChangeStats = await CalculatePriceChangeStatsAsync();
                
                if (priceChangeStats.Count == 0)
                {
                    var noDataText = new TextBlock
                    {
                        Text = "暂无数据，请确保已加载K线数据",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    panel.Children.Add(noDataText);
                    return panel;
                }
                
                // 添加数据说明
                var infoText = new TextBlock
                {
                    Text = $"📅 数据范围: 最近30个交易日 | 📊 实际获取: {priceChangeStats.Count}天 | 💡 2天以上数据可点击查看详情",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                panel.Children.Add(infoText);
                
                // 创建表格
                var statsGrid = CreatePriceChangeStatsGrid(priceChangeStats);
                panel.Children.Add(statsGrid);
                
                return panel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建涨跌数据统计面板失败: {ex.Message}");
                var errorPanel = new StackPanel();
                errorPanel.Children.Add(new TextBlock 
                { 
                    Text = $"创建涨跌数据统计失败: {ex.Message}",
                    Foreground = new SolidColorBrush(Colors.Red),
                    FontSize = 14
                });
                return errorPanel;
            }
        }

        /// <summary>
        /// 计算涨跌统计数据
        /// </summary>
        private async Task<List<DailyPriceChangeStats>> CalculatePriceChangeStatsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var stats = new List<DailyPriceChangeStats>();
                    
                    // 获取最近37个交易日的数据范围（30天统计 + 最多7天连续计算需要的额外天数）
                    var endDate = DateTime.UtcNow.Date;
                    var statsStartDate = endDate.AddDays(-37);
                    
                    // 从现有90天K线数据中过滤出涨跌统计需要的最近37天数据
                    var filteredKlineData = _allKlineData
                        .Where(k => k.OpenTime.Date >= statsStartDate && k.OpenTime.Date <= endDate)
                        .ToList();
                    
                    Console.WriteLine($"📊 涨跌统计使用数据量: {filteredKlineData.Count} 条，日期范围: {statsStartDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd}");
                    Console.WriteLine($"📊 原始K线数据总量: {_allKlineData.Count} 条");
                    
                    // 按日期分组，获取最近30个有数据的交易日
                    var dailyKlines = filteredKlineData
                        .GroupBy(k => k.OpenTime.Date)
                        .OrderByDescending(g => g.Key)
                        .Take(30)
                        .ToList();
                    
                    Console.WriteLine($"📊 找到 {dailyKlines.Count} 个交易日的K线数据（目标30天）");
                    
                    foreach (var dailyGroup in dailyKlines)
                    {
                        var date = dailyGroup.Key;
                        var dayKlines = dailyGroup.ToList();
                        
                        Console.WriteLine($"📅 处理日期: {date:yyyy-MM-dd}, K线数量: {dayKlines.Count}");
                        
                        // 计算当日各种连续涨跌情况
                        var dailyStats = new DailyPriceChangeStats
                        {
                            Date = date,
                            IsToday = date == DateTime.UtcNow.Date
                        };
                        
                        // 计算1-7天连续涨跌的合约数量
                        for (int days = 1; days <= 7; days++)
                        {
                            var (riseCount, fallCount, riseSymbols, fallSymbols) = CalculateConsecutiveChangeCounts(date, days, filteredKlineData);
                            
                            switch (days)
                            {
                                case 1:
                                    dailyStats.Rise1Day = riseCount;
                                    dailyStats.Fall1Day = fallCount;
                                    break;
                                case 2:
                                    dailyStats.Rise2Days = riseCount;
                                    dailyStats.Fall2Days = fallCount;
                                    dailyStats.Rise2DaySymbols = riseSymbols;
                                    dailyStats.Fall2DaySymbols = fallSymbols;
                                    break;
                                case 3:
                                    dailyStats.Rise3Days = riseCount;
                                    dailyStats.Fall3Days = fallCount;
                                    dailyStats.Rise3DaySymbols = riseSymbols;
                                    dailyStats.Fall3DaySymbols = fallSymbols;
                                    break;
                                case 4:
                                    dailyStats.Rise4Days = riseCount;
                                    dailyStats.Fall4Days = fallCount;
                                    dailyStats.Rise4DaySymbols = riseSymbols;
                                    dailyStats.Fall4DaySymbols = fallSymbols;
                                    break;
                                case 5:
                                    dailyStats.Rise5Days = riseCount;
                                    dailyStats.Fall5Days = fallCount;
                                    dailyStats.Rise5DaySymbols = riseSymbols;
                                    dailyStats.Fall5DaySymbols = fallSymbols;
                                    break;
                                case 6:
                                    dailyStats.Rise6Days = riseCount;
                                    dailyStats.Fall6Days = fallCount;
                                    dailyStats.Rise6DaySymbols = riseSymbols;
                                    dailyStats.Fall6DaySymbols = fallSymbols;
                                    break;
                                case 7:
                                    dailyStats.Rise7Days = riseCount;
                                    dailyStats.Fall7Days = fallCount;
                                    dailyStats.Rise7DaySymbols = riseSymbols;
                                    dailyStats.Fall7DaySymbols = fallSymbols;
                                    break;
                            }
                        }
                        
                        stats.Add(dailyStats);
                        
                        Console.WriteLine($"📈 {date:MM-dd}: 1日涨跌={dailyStats.Rise1Day}/{dailyStats.Fall1Day}, 7日涨跌={dailyStats.Rise7Days}/{dailyStats.Fall7Days}");
                    }
                    
                    return stats;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"计算涨跌统计数据失败: {ex.Message}");
                    return new List<DailyPriceChangeStats>();
                }
            });
        }

        /// <summary>
        /// 计算指定日期的连续涨跌数量和合约列表
        /// </summary>
        private (int riseCount, int fallCount, List<string> riseSymbols, List<string> fallSymbols) CalculateConsecutiveChangeCounts(DateTime targetDate, int consecutiveDays, List<Kline> klineData)
        {
            try
            {
                var riseCount = 0;
                var fallCount = 0;
                var riseSymbols = new List<string>();
                var fallSymbols = new List<string>();
                
                // 获取目标日期及之前连续N天的所有合约
                var endDate = targetDate;
                var startDate = targetDate.AddDays(-consecutiveDays + 1);
                
                Console.WriteLine($"📅 计算 {targetDate:yyyy-MM-dd} 连续{consecutiveDays}天涨跌：日期范围 {startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd}");
                
                // 获取所有有数据的合约（使用传入的过滤后数据）
                var symbols = klineData
                    .Where(k => k.OpenTime.Date >= startDate && k.OpenTime.Date <= endDate)
                    .Select(k => k.Symbol)
                    .Distinct()
                    .ToList();
                
                foreach (var symbol in symbols)
                {
                    // 获取该合约在指定时间范围内的K线数据，按时间升序排列（使用传入的过滤后数据）
                    var symbolKlines = klineData
                        .Where(k => k.Symbol == symbol && k.OpenTime.Date >= startDate && k.OpenTime.Date <= endDate)
                        .OrderBy(k => k.OpenTime)
                        .ToList();
                    
                    // 需要恰好有consecutiveDays天的数据
                    if (symbolKlines.Count != consecutiveDays)
                        continue;
                    
                    // 检查是否连续上涨或下跌
                    bool isConsecutiveRise = true;
                    bool isConsecutiveFall = true;
                    
                    for (int i = 0; i < symbolKlines.Count; i++)
                    {
                        var kline = symbolKlines[i];
                        // 按照用户定义：收盘价大于等于开盘价算上涨，小于开盘价算下跌
                        if (kline.ClosePrice < kline.OpenPrice)  // 只有下跌才中断连续上涨
                        {
                            isConsecutiveRise = false;
                        }
                        if (kline.ClosePrice > kline.OpenPrice)  // 只有上涨才中断连续下跌
                        {
                            isConsecutiveFall = false;
                        }
                        
                        // 调试输出：只记录A2ZUSDT的判断过程
                        if (symbol == "A2ZUSDT" && consecutiveDays == 4)
                        {
                            Console.WriteLine($"🔍 {symbol} {kline.OpenTime:MM-dd}: 开盘={kline.OpenPrice:F4}, 收盘={kline.ClosePrice:F4}, " +
                                            $"涨跌={(kline.ClosePrice - kline.OpenPrice) / kline.OpenPrice * 100:F2}%, " +
                                            $"连续涨={isConsecutiveRise}, 连续跌={isConsecutiveFall}");
                        }
                    }
                    
                    if (isConsecutiveRise)
                    {
                        riseCount++;
                        riseSymbols.Add(symbol);
                        if (symbol == "A2ZUSDT" && consecutiveDays == 4)
                        {
                            Console.WriteLine($"✅ {symbol} 被判定为连续{consecutiveDays}天上涨");
                        }
                    }
                    if (isConsecutiveFall)
                    {
                        fallCount++;
                        fallSymbols.Add(symbol);
                        if (symbol == "A2ZUSDT" && consecutiveDays == 4)
                        {
                            Console.WriteLine($"📉 {symbol} 被判定为连续{consecutiveDays}天下跌");
                        }
                    }
                    
                    if (symbol == "A2ZUSDT" && consecutiveDays == 4 && !isConsecutiveRise && !isConsecutiveFall)
                    {
                        Console.WriteLine($"❌ {symbol} 不符合连续{consecutiveDays}天涨跌条件");
                    }
                }
                
                return (riseCount, fallCount, riseSymbols, fallSymbols);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"计算连续涨跌数量失败: {ex.Message}");
                return (0, 0, new List<string>(), new List<string>());
            }
        }

        /// <summary>
        /// 创建涨跌统计表格
        /// </summary>
        private Grid CreatePriceChangeStatsGrid(List<DailyPriceChangeStats> stats)
        {
            var grid = new Grid();
            
            // 定义列：日期 + 1-7天涨跌数据（每天2列：涨/跌）
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 日期列
            for (int i = 1; i <= 7; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // 涨
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // 跌
            }
            
            // 定义行：标题行 + 数据行（最多30行）
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题行
            for (int i = 0; i < Math.Min(stats.Count, 30); i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            
            // 创建标题行
            CreateStatsHeaderRow(grid);
            
            // 创建数据行（最多30行）
            for (int i = 0; i < Math.Min(stats.Count, 30); i++)
            {
                CreateStatsDataRow(grid, stats[i], i + 1, stats);
            }
            
            // 添加边框
            var border = new Border
            {
                Child = grid,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Colors.White)
            };
            
            var containerGrid = new Grid();
            containerGrid.Children.Add(border);
            
            return containerGrid;
        }

        /// <summary>
        /// 创建统计表格标题行
        /// </summary>
        private void CreateStatsHeaderRow(Grid grid)
        {
            // 日期列标题
            var dateHeader = new TextBlock
            {
                Text = "日期",
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
            };
            Grid.SetRow(dateHeader, 0);
            Grid.SetColumn(dateHeader, 0);
            grid.Children.Add(dateHeader);
            
            // 1-7天涨跌列标题
            for (int days = 1; days <= 7; days++)
            {
                var dayHeaderContainer = new Grid();
                dayHeaderContainer.ColumnDefinitions.Add(new ColumnDefinition());
                dayHeaderContainer.ColumnDefinitions.Add(new ColumnDefinition());
                
                var riseHeader = new TextBlock
                {
                    Text = "涨",
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(2),
                    Background = new SolidColorBrush(Color.FromRgb(255, 240, 240)),
                    Foreground = new SolidColorBrush(Colors.Red)
                };
                Grid.SetColumn(riseHeader, 0);
                dayHeaderContainer.Children.Add(riseHeader);
                
                var fallHeader = new TextBlock
                {
                    Text = "跌",
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(2),
                    Background = new SolidColorBrush(Color.FromRgb(240, 255, 240)),
                    Foreground = new SolidColorBrush(Colors.Green)
                };
                Grid.SetColumn(fallHeader, 1);
                dayHeaderContainer.Children.Add(fallHeader);
                
                // 添加天数标签
                var dayLabel = new TextBlock
                {
                    Text = $"{days}天",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -15, 0, 0),
                    Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
                };
                dayHeaderContainer.Children.Add(dayLabel);
                
                Grid.SetRow(dayHeaderContainer, 0);
                Grid.SetColumn(dayHeaderContainer, days * 2 - 1);
                Grid.SetColumnSpan(dayHeaderContainer, 2);
                grid.Children.Add(dayHeaderContainer);
            }
        }

        /// <summary>
        /// 创建统计表格数据行
        /// </summary>
        private void CreateStatsDataRow(Grid grid, DailyPriceChangeStats stats, int row, List<DailyPriceChangeStats> allStats)
        {
            // 日期列
            var dateText = new TextBlock
            {
                Text = stats.IsToday ? "今日" : stats.Date.ToString("MM-dd"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5),
                FontWeight = stats.IsToday ? FontWeights.Bold : FontWeights.Normal,
                Background = stats.IsToday ? new SolidColorBrush(Color.FromRgb(255, 255, 200)) : new SolidColorBrush(Colors.White)
            };
            Grid.SetRow(dateText, row);
            Grid.SetColumn(dateText, 0);
            grid.Children.Add(dateText);
            
            // 1-7天涨跌数据
            var riseValues = new[] { stats.Rise1Day, stats.Rise2Days, stats.Rise3Days, stats.Rise4Days, stats.Rise5Days, stats.Rise6Days, stats.Rise7Days };
            var fallValues = new[] { stats.Fall1Day, stats.Fall2Days, stats.Fall3Days, stats.Fall4Days, stats.Fall5Days, stats.Fall6Days, stats.Fall7Days };
            
            // 计算颜色范围
            var allRiseValues = allStats.SelectMany(s => new[] { s.Rise1Day, s.Rise2Days, s.Rise3Days, s.Rise4Days, s.Rise5Days, s.Rise6Days, s.Rise7Days }).ToList();
            var allFallValues = allStats.SelectMany(s => new[] { s.Fall1Day, s.Fall2Days, s.Fall3Days, s.Fall4Days, s.Fall5Days, s.Fall6Days, s.Fall7Days }).ToList();
            
            var maxRise = allRiseValues.Max();
            var minRise = allRiseValues.Min();
            var maxFall = allFallValues.Max();
            var minFall = allFallValues.Min();
            
            // 获取合约列表数组
            var riseSymbolLists = new List<string>[]
            {
                new List<string>(), // 1天（不提供点击）
                stats.Rise2DaySymbols, // 2天
                stats.Rise3DaySymbols, // 3天
                stats.Rise4DaySymbols, // 4天
                stats.Rise5DaySymbols, // 5天
                stats.Rise6DaySymbols, // 6天
                stats.Rise7DaySymbols  // 7天
            };
            
            var fallSymbolLists = new List<string>[]
            {
                new List<string>(), // 1天（不提供点击）
                stats.Fall2DaySymbols, // 2天
                stats.Fall3DaySymbols, // 3天
                stats.Fall4DaySymbols, // 4天
                stats.Fall5DaySymbols, // 5天
                stats.Fall6DaySymbols, // 6天
                stats.Fall7DaySymbols  // 7天
            };
            
            for (int days = 0; days < 7; days++)
            {
                // 安全检查：确保不会数组越界
                if (days >= riseValues.Length || days >= fallValues.Length)
                {
                    Console.WriteLine($"⚠️ 数组越界警告: days={days}, riseValues.Length={riseValues.Length}, fallValues.Length={fallValues.Length}");
                    break;
                }
                
                var actualDays = days + 1; // 1-7天
                var currentDayIndex = days; // 捕获当前索引，避免lambda表达式的闭包问题
                var hasRiseClickFunction = actualDays >= 2 && riseValues[days] > 0;
                var hasFallClickFunction = actualDays >= 2 && fallValues[days] > 0;
                
                // 涨数据
                var riseText = new TextBlock
                {
                    Text = riseValues[days].ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(2),
                    FontWeight = FontWeights.Normal,
                    Foreground = new SolidColorBrush(Colors.Black),
                    Background = GetIntensityColor(riseValues[days], minRise, maxRise, true),
                    Cursor = hasRiseClickFunction ? Cursors.Hand : Cursors.Arrow
                };
                
                if (hasRiseClickFunction && currentDayIndex < riseSymbolLists.Length)
                {
                    riseText.TextDecorations = TextDecorations.Underline;
                    var riseSymbols = riseSymbolLists[currentDayIndex];
                    var riseActualDays = actualDays;
                    riseText.MouseLeftButtonDown += (s, e) => ShowSymbolDetailsWindow(riseSymbols, stats.Date, riseActualDays, true);
                }
                
                Grid.SetRow(riseText, row);
                Grid.SetColumn(riseText, (days + 1) * 2 - 1);
                grid.Children.Add(riseText);
                
                // 跌数据
                var fallText = new TextBlock
                {
                    Text = fallValues[days].ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(2),
                    FontWeight = FontWeights.Normal,
                    Foreground = new SolidColorBrush(Colors.Black),
                    Background = GetIntensityColor(fallValues[days], minFall, maxFall, false),
                    Cursor = hasFallClickFunction ? Cursors.Hand : Cursors.Arrow
                };
                
                if (hasFallClickFunction && currentDayIndex < fallSymbolLists.Length)
                {
                    fallText.TextDecorations = TextDecorations.Underline;
                    var fallSymbols = fallSymbolLists[currentDayIndex];
                    var fallActualDays = actualDays;
                    fallText.MouseLeftButtonDown += (s, e) => ShowSymbolDetailsWindow(fallSymbols, stats.Date, fallActualDays, false);
                }
                
                Grid.SetRow(fallText, row);
                Grid.SetColumn(fallText, (days + 1) * 2);
                grid.Children.Add(fallText);
            }
        }

        /// <summary>
        /// 根据数值获取强度颜色（数字越大颜色越红，数字越小颜色越白）
        /// </summary>
        private SolidColorBrush GetIntensityColor(int value, int min, int max, bool isRise)
        {
            if (max == min)
                return new SolidColorBrush(Colors.White);
            
            // 计算强度比例 (0-1)
            var intensity = max > min ? (double)(value - min) / (max - min) : 0;
            intensity = Math.Max(0, Math.Min(1, intensity));
            
            // 基础颜色：涨为红色系，跌为绿色系
            Color baseColor = isRise ? Colors.Red : Colors.Green;
            
            // 计算RGB值：强度越高越接近基础色，强度越低越接近白色
            var r = (byte)(255 - intensity * (255 - baseColor.R));
            var g = (byte)(255 - intensity * (255 - baseColor.G));
            var b = (byte)(255 - intensity * (255 - baseColor.B));
            
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        /// <summary>
        /// 创建可排序的列标题
        /// </summary>
        private Button CreateSortableHeader(string title, string propertyName, ListView listView, List<SymbolDetailItem> data)
        {
            var button = new Button
            {
                Content = title,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Colors.Black),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(5)
            };

            bool isAscending = true;
            button.Click += (s, e) =>
            {
                try
                {
                    var sortedData = SortSymbolDetailData(data, propertyName, isAscending);
                    listView.ItemsSource = sortedData;
                    
                    // 更新按钮显示排序方向
                    button.Content = $"{title} {(isAscending ? "↑" : "↓")}";
                    isAscending = !isAscending;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"排序失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            return button;
        }

        /// <summary>
        /// 排序合约详情数据
        /// </summary>
        private List<SymbolDetailItem> SortSymbolDetailData(List<SymbolDetailItem> data, string propertyName, bool ascending)
        {
            return propertyName switch
            {
                "Symbol" => ascending 
                    ? data.OrderBy(x => x.Symbol).ToList() 
                    : data.OrderByDescending(x => x.Symbol).ToList(),
                "PriceChangePercent" => ascending 
                    ? data.OrderBy(x => x.PriceChangePercent).ToList() 
                    : data.OrderByDescending(x => x.PriceChangePercent).ToList(),
                "QuoteVolume" => ascending 
                    ? data.OrderBy(x => x.QuoteVolume).ToList() 
                    : data.OrderByDescending(x => x.QuoteVolume).ToList(),
                "LastPrice" => ascending 
                    ? data.OrderBy(x => x.LastPrice).ToList() 
                    : data.OrderByDescending(x => x.LastPrice).ToList(),
                _ => data
            };
        }

        /// <summary>
        /// 复制前20个合约名称到剪贴板
        /// </summary>
        private void CopyTop20Symbols(List<SymbolDetailItem> symbolDetailsData, Window window)
        {
            try
            {
                var top20Symbols = symbolDetailsData.Take(20).Select(s => s.Symbol).ToList();
                var symbolsText = string.Join("，", top20Symbols);
                
                Clipboard.SetText(symbolsText);
                
                // 显示复制成功提示
                var originalTitle = window.Title;
                window.Title = $"✅ 已复制前{top20Symbols.Count}个合约名到剪贴板";
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (_, _) =>
                {
                    window.Title = originalTitle;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制到剪贴板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示合约详情窗口
        /// </summary>
        private async void ShowSymbolDetailsWindow(List<string> symbols, DateTime date, int days, bool isRise)
        {
            try
            {
                if (symbols == null || symbols.Count == 0)
                {
                    MessageBox.Show("没有找到相关合约数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var window = new Window
                {
                    Title = $"{date:yyyy-MM-dd} 连续{days}天{(isRise ? "上涨" : "下跌")}合约列表 ({symbols.Count}个)",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.CanResize
                };

                var mainPanel = new StackPanel { Margin = new Thickness(10) };

                // 标题信息
                var titleText = new TextBlock
                {
                    Text = $"📊 {date:yyyy-MM-dd} 连续{days}天{(isRise ? "📈上涨" : "📉下跌")}的合约",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15),
                    Foreground = new SolidColorBrush(isRise ? Colors.Red : Colors.Green)
                };
                mainPanel.Children.Add(titleText);

                // 说明文字
                var infoText = new TextBlock
                {
                    Text = "💡 双击任意行可复制合约名称到剪贴板，点击列标题可排序",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                mainPanel.Children.Add(infoText);

                // 获取24H行情数据
                var tickerData = await Get24HTickerDataAsync();
                var symbolDetailsData = new List<SymbolDetailItem>();

                foreach (var symbol in symbols)
                {
                    var ticker = tickerData?.FirstOrDefault(t => t.Symbol == symbol);
                    symbolDetailsData.Add(new SymbolDetailItem
                    {
                        Symbol = symbol,
                        PriceChangePercent = ticker?.PriceChangePercent ?? 0,
                        QuoteVolume = ticker?.QuoteVolume ?? 0,
                        LastPrice = ticker?.LastPrice ?? 0
                    });
                }

                // 按涨幅排序（涨的按涨幅降序，跌的按跌幅升序）
                symbolDetailsData = isRise 
                    ? symbolDetailsData.OrderByDescending(s => s.PriceChangePercent).ToList()
                    : symbolDetailsData.OrderBy(s => s.PriceChangePercent).ToList();

                // 复制全部按钮
                var copyAllButton = new Button
                {
                    Content = "复制前20个合约名",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 10),
                    Padding = new Thickness(15, 5, 15, 5),
                    Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    Cursor = Cursors.Hand
                };
                copyAllButton.Click += (s, e) => CopyTop20Symbols(symbolDetailsData, window);
                mainPanel.Children.Add(copyAllButton);

                // 创建数据网格
                var listView = new ListView
                {
                    ItemsSource = symbolDetailsData,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                // 定义列
                var gridView = new GridView();
                
                // 创建可点击的列标题
                var symbolHeader = CreateSortableHeader("合约名称", "Symbol", listView, symbolDetailsData);
                var priceChangeHeader = CreateSortableHeader("24H涨幅", "PriceChangePercent", listView, symbolDetailsData);
                var volumeHeader = CreateSortableHeader("24H成交额(万USDT)", "QuoteVolume", listView, symbolDetailsData);
                var lastPriceHeader = CreateSortableHeader("最新价格", "LastPrice", listView, symbolDetailsData);
                
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = symbolHeader,
                    DisplayMemberBinding = new Binding("Symbol"),
                    Width = 150
                });
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = priceChangeHeader,
                    DisplayMemberBinding = new Binding("PriceChangePercentDisplay"),
                    Width = 120
                });
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = volumeHeader,
                    DisplayMemberBinding = new Binding("QuoteVolumeDisplay"),
                    Width = 160
                });
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = lastPriceHeader,
                    DisplayMemberBinding = new Binding("LastPriceDisplay"),
                    Width = 120
                });

                listView.View = gridView;

                // 添加点击复制功能
                listView.MouseDoubleClick += (s, e) =>
                {
                    if (listView.SelectedItem is SymbolDetailItem selectedItem)
                    {
                        try
                        {
                            Clipboard.SetText(selectedItem.Symbol);
                            // 显示简短的复制成功提示
                            var originalTitle = window.Title;
                            window.Title = $"✅ 已复制: {selectedItem.Symbol}";
                            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                            timer.Tick += (_, _) =>
                            {
                                window.Title = originalTitle;
                                timer.Stop();
                            };
                            timer.Start();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"复制到剪贴板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                };

                // 添加右键菜单复制功能
                var contextMenu = new ContextMenu();
                var copyMenuItem = new MenuItem { Header = "复制合约名称" };
                copyMenuItem.Click += (s, e) =>
                {
                    if (listView.SelectedItem is SymbolDetailItem selectedItem)
                    {
                        try
                        {
                            Clipboard.SetText(selectedItem.Symbol);
                            var originalTitle = window.Title;
                            window.Title = $"✅ 已复制: {selectedItem.Symbol}";
                            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                            timer.Tick += (_, _) =>
                            {
                                window.Title = originalTitle;
                                timer.Stop();
                            };
                            timer.Start();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"复制到剪贴板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                };
                contextMenu.Items.Add(copyMenuItem);
                listView.ContextMenu = contextMenu;

                mainPanel.Children.Add(listView);

                // 统计信息
                var statsText = new TextBlock
                {
                    Text = $"📊 总计: {symbols.Count} 个合约",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0),
                    Foreground = new SolidColorBrush(Colors.Gray)
                };
                mainPanel.Children.Add(statsText);

                window.Content = mainPanel;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示合约详情失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存高低价分析配置
        /// </summary>
        private void SaveHighLowAnalysisConfig(int days)
        {
            try
            {
                _highLowAnalysisDays = days;
                
                // 保存到配置文件
                var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var jsonString = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<JsonElement>(jsonString);
                    
                    var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                    if (configDict != null)
                    {
                        if (!configDict.ContainsKey("HighLowAnalysis"))
                        {
                            configDict["HighLowAnalysis"] = new Dictionary<string, object>();
                        }
                        
                        var highLowConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(configDict["HighLowAnalysis"].ToString() ?? "{}");
                        if (highLowConfig != null)
                        {
                            highLowConfig["DefaultDays"] = days;
                            configDict["HighLowAnalysis"] = highLowConfig;
                        }
                        
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        var updatedJson = JsonSerializer.Serialize(configDict, options);
                        File.WriteAllText(configPath, updatedJson);
                        
                        Console.WriteLine($"📊 保存高低价分析配置: {days}天");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 保存高低价分析配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存振幅分析配置
        /// </summary>
        private void SaveAmplitudeAnalysisConfig(int days)
        {
            try
            {
                _amplitudeAnalysisDays = days;
                
                // 保存到配置文件
                var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var jsonString = File.ReadAllText(configPath);
                    var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                    if (configDict != null)
                    {
                        if (!configDict.ContainsKey("AmplitudeAnalysis"))
                        {
                            configDict["AmplitudeAnalysis"] = new Dictionary<string, object>();
                        }
                        
                        var amplitudeConfig = new Dictionary<string, object>
                        {
                            ["DefaultDays"] = days,
                            ["MinDays"] = 1,
                            ["MaxDays"] = 365
                        };
                        configDict["AmplitudeAnalysis"] = amplitudeConfig;
                        
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        var updatedJson = JsonSerializer.Serialize(configDict, options);
                        File.WriteAllText(configPath, updatedJson);
                        
                        Console.WriteLine($"📈 保存振幅分析配置: {days}天");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 保存振幅分析配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存高级筛选配置
        /// </summary>
        private void SaveAdvancedFilterConfig(decimal minPosition, decimal maxPosition, int amplitudeDays, decimal minAmplitude, decimal maxAmplitude, decimal minVolume, decimal minMarketCap = 0, decimal maxMarketCap = 0)
        {
            try
            {
                _advancedFilterMinPosition = minPosition;
                _advancedFilterMaxPosition = maxPosition;
                _advancedFilterAmplitudeDays = amplitudeDays;
                _advancedFilterMinAmplitude = minAmplitude;
                _advancedFilterMaxAmplitude = maxAmplitude;
                _advancedFilterMinVolume = minVolume;
                _advancedFilterMinMarketCap = minMarketCap;
                _advancedFilterMaxMarketCap = maxMarketCap;
                
                // 保存到配置文件
                var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var jsonString = File.ReadAllText(configPath);
                    var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                    if (configDict != null)
                    {
                        if (!configDict.ContainsKey("AdvancedFilter"))
                        {
                            configDict["AdvancedFilter"] = new Dictionary<string, object>();
                        }
                        
                                        var filterConfig = new Dictionary<string, object>
                {
                    ["MinPosition"] = minPosition,
                    ["MaxPosition"] = maxPosition,
                    ["AmplitudeDays"] = amplitudeDays,
                    ["MinAmplitude"] = minAmplitude,
                    ["MaxAmplitude"] = maxAmplitude,
                    ["MinVolume"] = minVolume,
                    ["MinMarketCap"] = minMarketCap,
                    ["MaxMarketCap"] = maxMarketCap
                };
                        
                        configDict["AdvancedFilter"] = filterConfig;
                        
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        var updatedJson = JsonSerializer.Serialize(configDict, options);
                        File.WriteAllText(configPath, updatedJson);
                        
                        Console.WriteLine($"🔍 保存高级筛选配置: 位置{minPosition}-{maxPosition}%, 振幅{minAmplitude}-{maxAmplitude}%");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 保存高级筛选配置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 计算振幅波动数据
        /// </summary>
        private async Task<List<AmplitudeData>> CalculateAmplitudeDataAsync(int days)
        {
            return await Task.Run(() =>
            {
                var amplitudeData = new List<AmplitudeData>();
                
                if (_allKlineData == null || _allKlineData.Count == 0)
                {
                    return amplitudeData;
                }
                
                // 按合约分组
                var symbolGroups = _allKlineData.GroupBy(k => k.Symbol).ToList();
                
                foreach (var group in symbolGroups)
                {
                    var symbol = group.Key;
                    var klines = group.OrderByDescending(k => k.OpenTime).ToList();
                    
                    if (klines.Count < days) continue; // 数据不足
                    
                    // 取最近N天的数据
                    var recentKlines = klines.Take(days).ToList();
                    
                    if (recentKlines.Count == 0) continue;
                    
                    // 计算最高价和最低价
                    var highPrice = recentKlines.Max(k => k.HighPrice);
                    var lowPrice = recentKlines.Min(k => k.LowPrice);
                    
                    if (lowPrice <= 0) continue; // 避免除零
                    
                    // 计算振幅百分比 = (最高价 - 最低价) / 最低价 * 100
                    var amplitudePercent = (highPrice - lowPrice) / lowPrice * 100;
                    
                    // 分类
                    AmplitudeCategory category;
                    if (amplitudePercent < 20)
                        category = AmplitudeCategory.UltraLow;
                    else if (amplitudePercent < 40)
                        category = AmplitudeCategory.MediumLow;
                    else if (amplitudePercent < 60)
                        category = AmplitudeCategory.MediumHigh;
                    else
                        category = AmplitudeCategory.UltraHigh;
                    
                    amplitudeData.Add(new AmplitudeData
                    {
                        Symbol = symbol,
                        AmplitudePercent = amplitudePercent,
                        HighPrice = highPrice,
                        LowPrice = lowPrice,
                        Category = category,
                        Days = days
                    });
                }
                
                return amplitudeData.OrderByDescending(a => a.AmplitudePercent).ToList();
            });
        }
        
        /// <summary>
        /// 显示振幅波动数据
        /// </summary>
        private void DisplayAmplitudeData(List<AmplitudeData> amplitudeData, int days)
        {
            // 找到数据显示面板
            var dataPanel = FindChildByName<StackPanel>(contentPanel, "amplitudeDataPanel");
            if (dataPanel == null) return;
            
            dataPanel.Children.Clear();
            
            if (amplitudeData.Count == 0)
            {
                dataPanel.Children.Add(new TextBlock 
                { 
                    Text = "暂无数据，请确保已加载K线数据",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 14,
                    Margin = new Thickness(0, 20, 0, 0)
                });
                return;
            }
            
            // 按分类分组
            var ultraLow = amplitudeData.Where(a => a.Category == AmplitudeCategory.UltraLow).ToList();
            var mediumLow = amplitudeData.Where(a => a.Category == AmplitudeCategory.MediumLow).ToList();
            var mediumHigh = amplitudeData.Where(a => a.Category == AmplitudeCategory.MediumHigh).ToList();
            var ultraHigh = amplitudeData.Where(a => a.Category == AmplitudeCategory.UltraHigh).ToList();
            
            // 创建四个区域的网格
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 标题行
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            titlePanel.Children.Add(new TextBlock 
            { 
                Text = $"振幅波动分析结果 (近{days}天) - 共{amplitudeData.Count}个合约",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            });
            Grid.SetColumnSpan(titlePanel, 4);
            grid.Children.Add(titlePanel);
            
            // 四个分类区域
            var categories = new[]
            {
                new { Data = ultraLow, Title = "超低波动", SubTitle = "<20%", Color = Colors.Green, Column = 0 },
                new { Data = mediumLow, Title = "中低波动", SubTitle = "20-40%", Color = Colors.Orange, Column = 1 },
                new { Data = mediumHigh, Title = "中高波动", SubTitle = "40-60%", Color = Colors.Red, Column = 2 },
                new { Data = ultraHigh, Title = "超高波动", SubTitle = ">60%", Color = Colors.Purple, Column = 3 }
            };
            
            foreach (var category in categories)
            {
                var categoryPanel = CreateAmplitudeCategoryPanel(category.Data, category.Title, category.SubTitle, category.Color);
                Grid.SetRow(categoryPanel, 1);
                Grid.SetColumn(categoryPanel, category.Column);
                grid.Children.Add(categoryPanel);
            }
            
            dataPanel.Children.Add(grid);
        }
        
        /// <summary>
        /// 创建振幅分类面板
        /// </summary>
        private Border CreateAmplitudeCategoryPanel(List<AmplitudeData> data, string title, string subTitle, Color borderColor)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(5),
                Padding = new Thickness(10)
            };
            
            var panel = new StackPanel();
            
            // 标题
            var titleText = new TextBlock
            {
                Text = $"{title} ({subTitle})",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(borderColor)
            };
            panel.Children.Add(titleText);
            
            // 数量统计
            var countText = new TextBlock
            {
                Text = $"共{data.Count}个合约",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(Colors.Gray)
            };
            panel.Children.Add(countText);
            
            // 列表（显示全部，动态高度）
            var listView = new ListView
            {
                MaxHeight = 400,
                MinHeight = 200,
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                FontSize = 11
            };
            ScrollViewer.SetVerticalScrollBarVisibility(listView, ScrollBarVisibility.Auto);
            
            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "合约", 
                Width = 150,  // 一半宽度给合约名称，避免换行显示
                DisplayMemberBinding = new Binding("Symbol")
            });
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "振幅%", 
                Width = 75,   // 剩余宽度平均分配
                DisplayMemberBinding = new Binding("AmplitudeText")
            });
            gridView.Columns.Add(new GridViewColumn 
            { 
                Header = "位置%", 
                Width = 75,   // 剩余宽度平均分配
                DisplayMemberBinding = new Binding("PositionText")
            });
            listView.View = gridView;
            
            // 添加双击复制功能
            listView.MouseDoubleClick += (s, e) => CopySymbolFromAmplitudeListView(s as ListView);
            
            // 绑定数据（显示全部）
            var displayData = data.Select(a => new 
            {
                Symbol = a.Symbol,
                AmplitudeText = $"{a.AmplitudePercent:F1}%",
                PositionText = GetLocationRatioText(a.Symbol)
            }).ToList();
            
            listView.ItemsSource = displayData;
            panel.Children.Add(listView);
            
            border.Child = panel;
            return border;
        }
        
        /// <summary>
        /// 获取合约的位置比例文本
        /// </summary>
        private string GetLocationRatioText(string symbol)
        {
            var locationData = _locationData.FirstOrDefault(l => l.Symbol == symbol);
            if (locationData != null)
            {
                return $"{locationData.LocationRatio * 100:F1}%";
            }
            return "N/A";
        }
        
        /// <summary>
        /// 从振幅分析ListView复制合约名到剪贴板
        /// </summary>
        private void CopySymbolFromAmplitudeListView(ListView? listView)
        {
            if (listView?.SelectedItem == null) return;
            
            try
            {
                // 使用反射获取Symbol属性值
                var item = listView.SelectedItem;
                var symbolProperty = item.GetType().GetProperty("Symbol");
                if (symbolProperty != null)
                {
                    var symbol = symbolProperty.GetValue(item)?.ToString();
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        TrySetClipboardText(symbol);
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默处理错误，不弹出提示框
                Console.WriteLine($"复制失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从位置比例ListView复制合约名到剪贴板
        /// </summary>
        private void CopySymbolFromLocationListView(ListView? listView)
        {
            if (listView?.SelectedItem == null) return;
            
            try
            {
                if (listView.SelectedItem is LocationData locationData)
                {
                    TrySetClipboardText(locationData.Symbol);
                }
            }
            catch (Exception ex)
            {
                // 静默处理错误，不弹出提示框
                Console.WriteLine($"复制失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从高级筛选ListView复制合约名到剪贴板
        /// </summary>
        private void CopySymbolFromAdvancedFilterListView(ListView? listView)
        {
            if (listView?.SelectedItem == null) return;
            
            try
            {
                // 使用反射获取Symbol属性值
                var item = listView.SelectedItem;
                var symbolProperty = item.GetType().GetProperty("Symbol");
                if (symbolProperty != null)
                {
                    var symbol = symbolProperty.GetValue(item)?.ToString();
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        TrySetClipboardText(symbol);
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默处理错误，不弹出提示框
                Console.WriteLine($"复制失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 一键复制所有筛选结果的合约名
        /// </summary>
        private void CopyAllFilteredSymbols_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 通过按钮的Tag属性直接获取ListView引用
                var button = sender as Button;
                var listView = button?.Tag as ListView;
                
                if (listView == null)
                {
                    // 如果Tag没有设置，尝试通过视觉树查找
                    var headerPanel = button?.Parent as StackPanel;
                    var resultPanel = headerPanel?.Parent as StackPanel;
                    listView = resultPanel?.Children.OfType<ListView>().FirstOrDefault();
                }
                
                if (listView?.Items.Count > 0)
                {
                    var symbols = new List<string>();
                    foreach (var item in listView.Items)
                    {
                        if (item is AdvancedFilterResultItem filterItem)
                        {
                            symbols.Add(filterItem.Symbol);
                        }
                    }
                    
                    if (symbols.Count > 0)
                    {
                        var symbolsText = string.Join(",", symbols);
                        
                        // 使用重试机制复制到剪贴板
                        if (TrySetClipboardText(symbolsText))
                        {
                            MessageBox.Show($"已复制 {symbols.Count} 个合约名到剪贴板:\n{symbolsText}", "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("剪贴板被占用，复制失败。请稍后重试。", "复制失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有找到可复制的合约", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("筛选结果为空，无法复制", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 执行新的高级筛选
        /// </summary>
        private async void ExecuteNewAdvancedFilter(Window dialog)
        {
            Button? btnSearch = null;
            TextBlock? statusText = null;
            
            try
            {
                // 获取UI控件引用
                var mainPanel = dialog.Content as StackPanel;
                var buttonPanel = mainPanel?.Children[2] as StackPanel;
                btnSearch = buttonPanel?.Children.OfType<Button>().FirstOrDefault(b => b.Name == "btnAdvancedFilter");
                statusText = buttonPanel?.Children.OfType<TextBlock>().FirstOrDefault(t => t.Name == "txtFilterStatus");
                
                // 更新UI状态
                if (btnSearch != null)
                {
                    btnSearch.Content = "筛选中...";
                    btnSearch.IsEnabled = false;
                }
                if (statusText != null)
                {
                    statusText.Text = "正在执行筛选，请稍候...";
                    statusText.Foreground = new SolidColorBrush(Colors.Orange);
                }
                
                // 获取筛选条件输入面板
                var filterPanel = mainPanel?.Children[1] as StackPanel;
                var inputParams = filterPanel?.Tag as dynamic;
                
                if (inputParams == null)
                {
                    MessageBox.Show("无法获取筛选参数", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 解析筛选条件
                if (!decimal.TryParse(inputParams.txtMinPosition.Text, out decimal minPosition))
                {
                    MessageBox.Show("请输入有效的最小位置百分比", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!decimal.TryParse(inputParams.txtMaxPosition.Text, out decimal maxPosition))
                {
                    MessageBox.Show("请输入有效的最大位置百分比", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (minPosition >= maxPosition)
                {
                    MessageBox.Show("最小位置必须小于最大位置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!int.TryParse(inputParams.txtAmplitudeDays.Text, out int amplitudeDays) || amplitudeDays <= 0)
                {
                    MessageBox.Show("请输入有效的振幅计算天数", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!decimal.TryParse(inputParams.txtMinAmplitude.Text, out decimal minAmplitude))
                {
                    MessageBox.Show("请输入有效的最小振幅百分比", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!decimal.TryParse(inputParams.txtMaxAmplitude.Text, out decimal maxAmplitude))
                {
                    MessageBox.Show("请输入有效的最大振幅百分比", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (minAmplitude >= maxAmplitude)
                {
                    MessageBox.Show("最小振幅必须小于最大振幅", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!decimal.TryParse(inputParams.txtMinVolume.Text, out decimal minVolume))
                {
                    MessageBox.Show("请输入有效的最小成交额（万）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!decimal.TryParse(inputParams.txtMinMarketCap.Text, out decimal minMarketCap))
                {
                    MessageBox.Show("请输入有效的最小市值（万）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (!decimal.TryParse(inputParams.txtMaxMarketCap.Text, out decimal maxMarketCap))
                {
                    MessageBox.Show("请输入有效的最大市值（万）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (maxMarketCap > 0 && minMarketCap >= maxMarketCap)
                {
                    MessageBox.Show("最小市值必须小于最大市值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 保存高级筛选配置
                SaveAdvancedFilterConfig(minPosition, maxPosition, amplitudeDays, minAmplitude, maxAmplitude, minVolume, minMarketCap, maxMarketCap);
                
                // 执行筛选
                var filteredResults = await PerformAdvancedFilteringAsync(minPosition, maxPosition, amplitudeDays, minAmplitude, maxAmplitude, minVolume, minMarketCap, maxMarketCap);
                
                // 显示结果
                DisplayAdvancedFilterResults(dialog, filteredResults);
                
                // 更新完成状态
                if (statusText != null)
                {
                    statusText.Text = $"筛选完成！共找到 {filteredResults.Count} 个符合条件的合约";
                    statusText.Foreground = new SolidColorBrush(filteredResults.Count > 0 ? Colors.Green : Colors.Red);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"筛选失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 更新错误状态
                if (statusText != null)
                {
                    statusText.Text = "筛选失败，请检查输入条件";
                    statusText.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            finally
            {
                // 恢复按钮状态
                if (btnSearch != null)
                {
                    btnSearch.Content = "开始筛选";
                    btnSearch.IsEnabled = true;
                }
            }
        }
        
        /// <summary>
        /// 执行高级筛选逻辑（优化版本，使用批量数据获取）
        /// </summary>
        private async Task<List<AdvancedFilterResultItem>> PerformAdvancedFilteringAsync(decimal minPosition, decimal maxPosition, int amplitudeDays, decimal minAmplitude, decimal maxAmplitude, decimal minVolume, decimal minMarketCap = 0, decimal maxMarketCap = 0)
        {
            var results = new List<AdvancedFilterResultItem>();
            
            if (_locationData == null || _locationData.Count == 0)
            {
                return results;
            }
            
            // 批量获取24H数据，避免重复API调用
            if (_cached24HData == null || DateTime.Now - _last24HDataUpdate > TimeSpan.FromMinutes(5))
            {
                _cached24HData = await Get24HTickerDataAsync();
                _last24HDataUpdate = DateTime.Now;
            }
            
            // 创建24H数据查找字典，提高查找效率
            var volume24HDict = _cached24HData.ToDictionary(t => t.Symbol, t => t.QuoteVolume);
            var price24HDict = _cached24HData.ToDictionary(t => t.Symbol, t => t.LastPrice);
            
            // 计算所有符合条件的合约的市值并排名
            var tempResults = new List<AdvancedFilterResultItem>();
            
            foreach (var locationData in _locationData)
            {
                // 位置筛选
                var positionPercent = locationData.LocationRatio * 100;
                if (positionPercent < minPosition || positionPercent > maxPosition) continue;
                
                // 计算振幅（使用缓存）
                var amplitudePercent = CalculateSymbolAmplitude(locationData.Symbol, amplitudeDays);
                if (amplitudePercent < minAmplitude || amplitudePercent > maxAmplitude) continue;
                
                // 获取24H成交额（从字典查找，无需异步调用）
                var volume24h = volume24HDict.GetValueOrDefault(locationData.Symbol, 0);
                var volumeInWan = volume24h / 10000; // 转换为万
                if (volumeInWan < minVolume) continue;
                
                // 获取当前价格
                var currentPrice = price24HDict.GetValueOrDefault(locationData.Symbol, locationData.CurrentPrice);
                
                // 计算市值
                var marketCapData = _supplyDataService?.CalculateMarketCap(locationData.Symbol, currentPrice);
                var marketCap = marketCapData?.MarketCap ?? 0;
                var marketCapText = marketCapData?.FormattedMarketCap ?? "N/A";
                
                // 市值筛选
                if (minMarketCap > 0 || maxMarketCap > 0)
                {
                    var marketCapInWan = marketCap / 10000; // 转换为万
                    if (minMarketCap > 0 && marketCapInWan < minMarketCap) continue;
                    if (maxMarketCap > 0 && marketCapInWan > maxMarketCap) continue;
                }
                
                // 符合所有条件，添加到临时结果
                tempResults.Add(new AdvancedFilterResultItem
                {
                    Symbol = locationData.Symbol,
                    PositionPercent = positionPercent,
                    PositionText = $"{positionPercent:F1}%",
                    AmplitudePercent = amplitudePercent,
                    AmplitudeText = $"{amplitudePercent:F1}%",
                    Volume24h = volume24h,
                    VolumeText = volume24h >= 100000000 ? $"{volume24h / 100000000:F1}亿" : $"{volume24h / 10000:F0}万",
                    CurrentPrice = currentPrice,
                    CurrentPriceText = $"{currentPrice:F8}",
                    MarketCap = marketCap,
                    MarketCapText = marketCapText,
                    MarketCapRank = 0 // 稍后设置排名
                });
            }
            
            // 按市值排序并设置排名
            var rankedResults = tempResults
                .OrderByDescending(r => r.MarketCap)
                .Select((r, index) =>
                {
                    r.MarketCapRank = r.MarketCap > 0 ? index + 1 : 0;
                    r.MarketCapRankText = r.MarketCapRank > 0 ? $"#{r.MarketCapRank}" : "N/A";
                    return r;
                })
                .OrderByDescending(r => r.PositionPercent) // 最终按位置排序
                .ToList();
            
            return rankedResults;
        }
        
        /// <summary>
        /// 计算指定合约的振幅（使用缓存优化）
        /// </summary>
        private decimal CalculateSymbolAmplitude(string symbol, int days)
        {
            // 检查缓存
            if (_amplitudeCache.TryGetValue(symbol, out var symbolCache) && 
                symbolCache.TryGetValue(days, out var cachedAmplitude))
            {
                return cachedAmplitude;
            }
            
            if (_allKlineData == null || _allKlineData.Count == 0) return 0;
            
            var symbolKlines = _allKlineData.Where(k => k.Symbol == symbol)
                                         .OrderByDescending(k => k.OpenTime)
                                         .Take(days)
                                         .ToList();
            
            if (symbolKlines.Count == 0) return 0;
            
            var highPrice = symbolKlines.Max(k => k.HighPrice);
            var lowPrice = symbolKlines.Min(k => k.LowPrice);
            
            if (lowPrice <= 0) return 0;
            
            var amplitude = (highPrice - lowPrice) / lowPrice * 100;
            
            // 缓存结果
            if (!_amplitudeCache.ContainsKey(symbol))
            {
                _amplitudeCache[symbol] = new Dictionary<int, decimal>();
            }
            _amplitudeCache[symbol][days] = amplitude;
            
            return amplitude;
        }
        
        /// <summary>
        /// 获取指定合约的24H成交额（使用缓存优化）
        /// </summary>
        private async Task<decimal> Get24HVolumeAsync(string symbol)
        {
            try
            {
                // 检查缓存是否有效（5分钟内的数据认为有效）
                if (_cached24HData == null || DateTime.Now - _last24HDataUpdate > TimeSpan.FromMinutes(5))
                {
                    _cached24HData = await Get24HTickerDataAsync();
                    _last24HDataUpdate = DateTime.Now;
                }
                
                var ticker = _cached24HData.FirstOrDefault(t => t.Symbol == symbol);
                return ticker?.QuoteVolume ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 显示高级筛选结果
        /// </summary>
        private void DisplayAdvancedFilterResults(Window dialog, List<AdvancedFilterResultItem> results)
        {
            try
            {
                var mainPanel = dialog.Content as StackPanel;
                var resultPanel = mainPanel?.Children[3] as StackPanel;
                var listView = resultPanel?.Tag as ListView;
                
                if (listView != null)
                {
                    listView.ItemsSource = results;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示结果失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 尝试设置剪贴板文本，带重试机制
        /// </summary>
        private bool TrySetClipboardText(string text)
        {
            const int maxRetries = 5;
            const int delayMs = 100;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x800401D0)) // CLIPBRD_E_CANT_OPEN
                {
                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                        continue;
                    }
                    return false;
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                        continue;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }
            
            return false;
        }
        
        #endregion
    }

    /// <summary>
    /// 24H行情数据模型
    /// </summary>
    public class Market24HData
    {
        public string Symbol { get; set; } = "";
        public decimal LastPrice { get; set; }
        public decimal PriceChangePercent { get; set; }
        public decimal PriceChange { get; set; }
        public decimal Volume { get; set; }
        public decimal QuoteVolume { get; set; } // 24H成交额
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal OpenPrice { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
    
    /// <summary>
    /// 高级筛选结果项
    /// </summary>
    public class AdvancedFilterResultItem
    {
        public string Symbol { get; set; } = "";
        public decimal PositionPercent { get; set; }
        public string PositionText { get; set; } = "";
        public decimal AmplitudePercent { get; set; }
        public string AmplitudeText { get; set; } = "";
        public decimal Volume24h { get; set; }
        public string VolumeText { get; set; } = "";
        public decimal CurrentPrice { get; set; }
        public string CurrentPriceText { get; set; } = "";
        public decimal MarketCap { get; set; }
        public string MarketCapText { get; set; } = "";
        public int MarketCapRank { get; set; }
        public string MarketCapRankText { get; set; } = "";
    }
    
    /// <summary>
    /// 24H排行榜显示项
    /// </summary>
    public class Market24HRankingItem
    {
        public int Rank { get; set; }
        public string Symbol { get; set; } = "";
        public string LastPrice { get; set; } = "";
        public string PriceChangePercentText { get; set; } = "";
        public string QuoteVolumeText { get; set; } = "";
    }
    
    /// <summary>
    /// 成交额放量数据
    /// </summary>
    public class VolumeGrowthData
    {
        public string Symbol { get; set; } = "";
        public decimal TodayVolume { get; set; }
        public decimal YesterdayVolume { get; set; }
        public decimal GrowthPercent { get; set; }
        public decimal LastPrice { get; set; }
        public decimal PriceChangePercent { get; set; }
        public decimal Past10DaysAvgVolume { get; set; } // 过去10天平均成交额
        public decimal VolumeMultiple { get; set; } // 24H成交额是平均额的倍数
    }
    
    /// <summary>
    /// 成交额放量显示项
    /// </summary>
    public class VolumeGrowthDisplayItem
    {
        public int Rank { get; set; }
        public string Symbol { get; set; } = "";
        public string LastPriceText { get; set; } = "";
        public string PriceChangeText { get; set; } = "";
        public string YesterdayVolumeText { get; set; } = "";
        public string TodayVolumeText { get; set; } = "";
        public string GrowthPercentText { get; set; } = "";
        public string Past10DaysAvgVolumeText { get; set; } = ""; // 过去10天平均成交额显示文本
        public string VolumeMultipleText { get; set; } = ""; // 成交额倍数显示文本
        
        // 排序用的原始数值
        public decimal LastPrice { get; set; }
        public decimal PriceChangePercent { get; set; }
        public decimal YesterdayVolume { get; set; }
        public decimal TodayVolume { get; set; }
        public decimal GrowthPercent { get; set; }
        public decimal Past10DaysAvgVolume { get; set; }
        public decimal VolumeMultiple { get; set; }
    }

    
    /// <summary>
    /// 合约分析结果
    /// </summary>
    public class ContractAnalysis
    {
        public string Symbol { get; set; } = "";
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal LastClosePrice { get; set; }
        public decimal LocationRatio { get; set; } // 位置比例 (0-1)
        public decimal LocationPercentage { get; set; } // 位置百分比 (0-100)
        public decimal Recent3DayVolume { get; set; } // 最近3天成交额
        public int KlineCount { get; set; } // K线数量
        public DateTime LastUpdateTime { get; set; } // 最后更新时间
    }
    
            /// <summary>
        /// 高级筛选结果
        /// </summary>
        public class AdvancedFilterResult
        {
            public string Symbol { get; set; } = "";
            public decimal LocationRatio { get; set; } // 位置比例 (0-1)
            public decimal VolumeMultiplier { get; set; } // 成交额倍数
            public int BreakoutDays { get; set; } // 突破天数
            public decimal CurrentPrice { get; set; } // 当前价格
            public decimal PreviousHigh { get; set; } // 前期高点

            /// <summary>
            /// 重写ToString方法，提供友好的显示格式
            /// </summary>
            public override string ToString()
            {
                return $"{Symbol} - 位置:{LocationRatio:P1} 倍数:{VolumeMultiplier:F2} 突破:{BreakoutDays}天";
            }
        }
        
        /// <summary>
        /// 振幅波动数据
        /// </summary>
        public class AmplitudeData
        {
            public string Symbol { get; set; } = "";
            public decimal AmplitudePercent { get; set; } // 振幅百分比
            public decimal HighPrice { get; set; } // 最高价
            public decimal LowPrice { get; set; } // 最低价
            public AmplitudeCategory Category { get; set; } // 振幅分类
            public int Days { get; set; } // 计算天数
        }
        
        /// <summary>
        /// 振幅分类枚举
        /// </summary>
        public enum AmplitudeCategory
        {
            UltraLow,   // 超低波动 (<20%)
            MediumLow,  // 中低波动 (20-40%)
            MediumHigh, // 中高波动 (40-60%)
            UltraHigh   // 超高波动 (>60%)
        }
        
        /// <summary>
        /// 市场波动率数据
        /// </summary>
        public class MarketVolatilityData
        {
            public DateTime Date { get; set; }
            public decimal AverageMaxVolatility { get; set; } // 平均最大波动率
            public int SymbolCount { get; set; } // 参与计算的币种数量
            public decimal DailyTotalVolume { get; set; } // 每日成交额总和（以亿为单位）
            public List<SymbolVolatility> TopVolatilitySymbols { get; set; } = new(); // 前10个波动最大的币种
            
            // 比特币相关数据
            public decimal BtcPriceChangePercent { get; set; } // 比特币24H涨跌幅
            public decimal BtcQuoteVolume { get; set; } // 比特币24H成交额（原始值）
            
            // 标记是否为今日24H数据
            public bool IsToday { get; set; } = false;
        }
        
        /// <summary>
        /// 单个币种的波动率数据
        /// </summary>
        public class SymbolVolatility
        {
            public string Symbol { get; set; } = "";
            public decimal Volatility { get; set; } // 波动率 (最高价-最低价)/最低价
            public decimal HighPrice { get; set; }
            public decimal LowPrice { get; set; }
            public decimal ClosePrice { get; set; }
            public decimal PriceChangePercent { get; set; } // 24H涨幅百分比
            public decimal QuoteVolume { get; set; } // 24H成交额
        }

        /// <summary>
        /// 每日涨跌统计数据
        /// </summary>
        public class DailyPriceChangeStats
        {
            public DateTime Date { get; set; }
            public bool IsToday { get; set; } = false;
            
            // 连续1-7天上涨的合约数量
            public int Rise1Day { get; set; }
            public int Rise2Days { get; set; }
            public int Rise3Days { get; set; }
            public int Rise4Days { get; set; }
            public int Rise5Days { get; set; }
            public int Rise6Days { get; set; }
            public int Rise7Days { get; set; }
            
            // 连续1-7天下跌的合约数量
            public int Fall1Day { get; set; }
            public int Fall2Days { get; set; }
            public int Fall3Days { get; set; }
            public int Fall4Days { get; set; }
            public int Fall5Days { get; set; }
            public int Fall6Days { get; set; }
            public int Fall7Days { get; set; }
            
            // 连续2-7天上涨的合约列表（用于点击查看详情）
            public List<string> Rise2DaySymbols { get; set; } = new();
            public List<string> Rise3DaySymbols { get; set; } = new();
            public List<string> Rise4DaySymbols { get; set; } = new();
            public List<string> Rise5DaySymbols { get; set; } = new();
            public List<string> Rise6DaySymbols { get; set; } = new();
            public List<string> Rise7DaySymbols { get; set; } = new();
            
            // 连续2-7天下跌的合约列表（用于点击查看详情）
            public List<string> Fall2DaySymbols { get; set; } = new();
            public List<string> Fall3DaySymbols { get; set; } = new();
            public List<string> Fall4DaySymbols { get; set; } = new();
            public List<string> Fall5DaySymbols { get; set; } = new();
            public List<string> Fall6DaySymbols { get; set; } = new();
            public List<string> Fall7DaySymbols { get; set; } = new();
        }

        /// <summary>
        /// 合约详情项目
        /// </summary>
        public class SymbolDetailItem
        {
            public string Symbol { get; set; } = "";
            public decimal PriceChangePercent { get; set; }
            public decimal QuoteVolume { get; set; }
            public decimal LastPrice { get; set; }
            
            public string PriceChangePercentDisplay => $"{PriceChangePercent:+0.00;-0.00;0.00}%";
            public string QuoteVolumeDisplay => $"{QuoteVolume / 10000:F0}";
            public string LastPriceDisplay => LastPrice.ToString("F8").TrimEnd('0').TrimEnd('.');
        }
    }
    
    /// <summary>
    /// 数值到颜色的转换器，实现0-600的白色到红色渐变
    /// </summary>
    public class ValueToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int intValue)
            {
                // 将0-600的范围映射到0-255的红色强度
                var normalizedValue = Math.Max(0, Math.Min(600, intValue)); // 限制在0-600范围内
                var redIntensity = (byte)(normalizedValue * 255 / 600); // 映射到0-255
                
                // 创建从白色(255,255,255)到红色(255,0,0)的渐变
                var red = (byte)255;
                var green = (byte)(255 - redIntensity);
                var blue = (byte)(255 - redIntensity);
                
                return new SolidColorBrush(Color.FromRgb(red, green, blue));
            }
            
            // 默认返回白色
            return new SolidColorBrush(Colors.White);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    } 