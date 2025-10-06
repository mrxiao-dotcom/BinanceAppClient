using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using BinanceApps.Core.Interfaces;

namespace BinanceApps.WPF
{
    public partial class PortfolioEditorDialog : Window
    {
        private readonly CustomPortfolioService _portfolioService;
        private readonly PortfolioGroupService? _groupService;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly CustomPortfolio? _editingPortfolio;
        private readonly List<PortfolioSymbol> _selectedSymbols = new();
        private List<string> _allSymbols = new();
        private System.Windows.Threading.DispatcherTimer? _searchTimer;
        
        /// <summary>
        /// 创建模式构造函数
        /// </summary>
        public PortfolioEditorDialog(CustomPortfolioService portfolioService, PortfolioGroupService? groupService, IBinanceSimulatedApiClient apiClient)
        {
            InitializeComponent();
            _portfolioService = portfolioService;
            _groupService = groupService;
            _apiClient = apiClient;
            _editingPortfolio = null;
            Title = "创建组合";
        }
        
        /// <summary>
        /// 编辑模式构造函数
        /// </summary>
        public PortfolioEditorDialog(CustomPortfolioService portfolioService, PortfolioGroupService? groupService, IBinanceSimulatedApiClient apiClient, CustomPortfolio portfolio)
        {
            InitializeComponent();
            _portfolioService = portfolioService;
            _groupService = groupService;
            _apiClient = apiClient;
            _editingPortfolio = portfolio;
            Title = "编辑组合";
        }
        
        /// <summary>
        /// 窗口加载事件
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 加载所有合约列表
                await LoadAllSymbolsAsync();
                
                // 加载分组列表到下拉框
                LoadGroupsToComboBox();
                
                // 如果是编辑模式，加载现有数据
                if (_editingPortfolio != null)
                {
                    txtName.Text = _editingPortfolio.Name;
                    txtDescription.Text = _editingPortfolio.Description;
                    cmbGroupName.Text = _editingPortfolio.GroupName;
                    
                    // 加载已有的合约
                    _selectedSymbols.AddRange(_editingPortfolio.Symbols);
                    UpdateSelectedSymbolsList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 加载分组列表到下拉框
        /// </summary>
        private void LoadGroupsToComboBox()
        {
            cmbGroupName.Items.Clear();
            
            if (_groupService == null)
            {
                return;
            }
            
            // 获取所有分组
            var groups = _groupService.GetAllGroups();
            foreach (var group in groups)
            {
                cmbGroupName.Items.Add(group.Name);
            }
            
            // 如果没有分组，提示用户创建
            if (groups.Count == 0)
            {
                var item = new ComboBoxItem 
                { 
                    Content = "（请先创建分组）", 
                    IsEnabled = false,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
                cmbGroupName.Items.Add(item);
            }
            else
            {
                // 默认选择第一个
                cmbGroupName.SelectedIndex = 0;
            }
        }
        
        /// <summary>
        /// 加载所有可用的合约列表
        /// </summary>
        private async Task LoadAllSymbolsAsync()
        {
            try
            {
                var symbols = await _apiClient.GetAllSymbolsInfoAsync();
                _allSymbols = symbols
                    .Select(s => s.Symbol)
                    .OrderBy(s => s)
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载合约列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _allSymbols = new List<string>();
            }
        }
        
        /// <summary>
        /// 搜索按钮点击
        /// </summary>
        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchSymbols();
        }
        
        /// <summary>
        /// 搜索框文本变化事件（实时搜索）
        /// </summary>
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 取消之前的定时器
            if (_searchTimer != null)
            {
                _searchTimer.Stop();
            }
            
            // 创建新的定时器，延迟 300ms 执行搜索（防抖）
            _searchTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            
            _searchTimer.Tick += (s, args) =>
            {
                _searchTimer.Stop();
                SearchSymbols();
            };
            
            _searchTimer.Start();
        }
        
        /// <summary>
        /// 搜索框回车事件
        /// </summary>
        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 停止定时器，立即执行搜索
                if (_searchTimer != null)
                {
                    _searchTimer.Stop();
                }
                SearchSymbols();
            }
        }
        
        /// <summary>
        /// 搜索合约
        /// </summary>
        private void SearchSymbols()
        {
            var searchText = txtSearch.Text.Trim().ToUpper();
            
            if (string.IsNullOrEmpty(searchText))
            {
                // 空搜索时清空候选列表
                panelSearchResults.Children.Clear();
                var hintText = new TextBlock
                {
                    Text = "请输入合约名称进行搜索...",
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(10)
                };
                panelSearchResults.Children.Add(hintText);
                return;
            }
            
            // 模糊搜索
            var results = _allSymbols
                .Where(s => s.Contains(searchText))
                .Take(20) // 限制结果数量
                .ToList();
            
            DisplaySearchResults(results);
        }
        
        /// <summary>
        /// 显示搜索结果
        /// </summary>
        private void DisplaySearchResults(List<string> results)
        {
            panelSearchResults.Children.Clear();
            
            if (results.Count == 0)
            {
                var noResultText = new TextBlock
                {
                    Text = "未找到匹配的合约",
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Margin = new Thickness(10)
                };
                panelSearchResults.Children.Add(noResultText);
                return;
            }
            
            foreach (var symbol in results)
            {
                // 检查是否已添加
                var isAdded = _selectedSymbols.Any(s => s.Symbol == symbol);
                
                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(8, 6, 8, 6)
                };
                
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                var symbolText = new TextBlock
                {
                    Text = symbol,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13
                };
                Grid.SetColumn(symbolText, 0);
                grid.Children.Add(symbolText);
                
                if (isAdded)
                {
                    var addedLabel = new TextBlock
                    {
                        Text = "已添加",
                        Foreground = new SolidColorBrush(Colors.Gray),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(addedLabel, 1);
                    grid.Children.Add(addedLabel);
                }
                else
                {
                    var btnAdd = new Button
                    {
                        Content = "添加",
                        Width = 60,
                        Height = 24,
                        Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)),
                        Foreground = new SolidColorBrush(Colors.White),
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        Tag = symbol
                    };
                    btnAdd.Click += BtnAddSymbol_Click;
                    Grid.SetColumn(btnAdd, 1);
                    grid.Children.Add(btnAdd);
                }
                
                border.Child = grid;
                panelSearchResults.Children.Add(border);
            }
        }
        
        /// <summary>
        /// 添加合约按钮点击
        /// </summary>
        private void BtnAddSymbol_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var symbol = button?.Tag as string;
            
            if (string.IsNullOrEmpty(symbol))
                return;
            
            // 添加到已选列表
            _selectedSymbols.Add(new PortfolioSymbol
            {
                Symbol = symbol,
                Remark = string.Empty,
                AddedTime = DateTime.UtcNow
            });
            
            // 刷新UI
            UpdateSelectedSymbolsList();
            
            // 重新搜索以更新按钮状态
            SearchSymbols();
        }
        
        /// <summary>
        /// 更新已选合约列表
        /// </summary>
        private void UpdateSelectedSymbolsList()
        {
            panelSelectedSymbols.Children.Clear();
            txtSymbolCount.Text = $"({_selectedSymbols.Count}个)";
            
            if (_selectedSymbols.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "暂无合约\n请使用上方搜索功能添加合约",
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Margin = new Thickness(10, 20, 10, 10),
                    TextWrapping = TextWrapping.Wrap
                };
                panelSelectedSymbols.Children.Add(emptyText);
                return;
            }
            
            foreach (var symbol in _selectedSymbols)
            {
                var symbolCard = CreateSelectedSymbolCard(symbol);
                panelSelectedSymbols.Children.Add(symbolCard);
            }
        }
        
        /// <summary>
        /// 创建已选合约卡片
        /// </summary>
        private Border CreateSelectedSymbolCard(PortfolioSymbol symbol)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Colors.White)
            };
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // 第一行：合约名称和删除按钮
            var topPanel = new Grid();
            topPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var symbolText = new TextBlock
            {
                Text = symbol.Symbol,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(symbolText, 0);
            topPanel.Children.Add(symbolText);
            
            var btnDelete = new Button
            {
                Content = "删除",
                Width = 60,
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = symbol
            };
            btnDelete.Click += BtnRemoveSymbol_Click;
            Grid.SetColumn(btnDelete, 1);
            topPanel.Children.Add(btnDelete);
            
            Grid.SetRow(topPanel, 0);
            grid.Children.Add(topPanel);
            
            // 第二行：备注输入
            var remarkPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            
            var remarkLabel = new TextBlock
            {
                Text = "备注:",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 0, 0, 3)
            };
            remarkPanel.Children.Add(remarkLabel);
            
            var remarkTextBox = new TextBox
            {
                Text = symbol.Remark,
                MinHeight = 45,
                MaxHeight = 80,
                Padding = new Thickness(5),
                FontSize = 12,
                Tag = symbol,
                TextWrapping = TextWrapping.Wrap, // 支持多行
                AcceptsReturn = true, // 允许回车换行
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto // 自动滚动条
            };
            remarkTextBox.TextChanged += RemarkTextBox_TextChanged;
            remarkPanel.Children.Add(remarkTextBox);
            
            Grid.SetRow(remarkPanel, 1);
            grid.Children.Add(remarkPanel);
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 备注输入变化事件
        /// </summary>
        private void RemarkTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var symbol = textBox?.Tag as PortfolioSymbol;
            
            if (symbol != null)
            {
                symbol.Remark = textBox!.Text;
            }
        }
        
        /// <summary>
        /// 删除合约按钮点击
        /// </summary>
        private void BtnRemoveSymbol_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var symbol = button?.Tag as PortfolioSymbol;
            
            if (symbol != null)
            {
                _selectedSymbols.Remove(symbol);
                UpdateSelectedSymbolsList();
                
                // 如果当前显示搜索结果，刷新以更新按钮状态
                if (panelSearchResults.Children.Count > 0)
                {
                    SearchSymbols();
                }
            }
        }
        
        /// <summary>
        /// 保存按钮点击
        /// </summary>
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                var name = txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("请输入组合名称", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtName.Focus();
                    return;
                }
                
                if (_selectedSymbols.Count == 0)
                {
                    var result = MessageBox.Show(
                        "组合中没有合约，确定要保存吗？",
                        "确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );
                    
                    if (result != MessageBoxResult.Yes)
                        return;
                }
                
                var description = txtDescription.Text.Trim();
                var groupName = cmbGroupName.Text.Trim();
                
                // 如果没有选择分组，提示用户
                if (string.IsNullOrEmpty(groupName))
                {
                    MessageBox.Show("请选择组合所属分组", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    cmbGroupName.Focus();
                    return;
                }
                
                // 禁用按钮防止重复点击
                btnSave.IsEnabled = false;
                btnSave.Content = "保存中...";
                
                // 保存或更新
                if (_editingPortfolio != null)
                {
                    // 更新模式
                    await _portfolioService.UpdatePortfolioAsync(
                        _editingPortfolio.Id,
                        name,
                        description,
                        _selectedSymbols,
                        groupName
                    );
                }
                else
                {
                    // 创建模式
                    await _portfolioService.CreatePortfolioAsync(
                        name,
                        description,
                        _selectedSymbols,
                        groupName
                    );
                }
                
                // 关闭对话框
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                btnSave.IsEnabled = true;
                btnSave.Content = "保存";
            }
        }
        
        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        /// <summary>
        /// 批量导入按钮点击
        /// </summary>
        private async void BtnBatchImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = txtBatchImport.Text?.Trim();
                if (string.IsNullOrEmpty(input))
                {
                    MessageBox.Show("请输入要导入的合约列表", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 禁用按钮
                btnBatchImport.IsEnabled = false;
                btnBatchImport.Content = "导入中...";
                
                // 分隔符：逗号（中英文）、井号、分号（中英文）、空格、换行
                var separators = new[] { ',', '，', '#', ';', '；', ' ', '\r', '\n', '\t' };
                var symbols = input.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToUpper())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();
                
                if (symbols.Count == 0)
                {
                    MessageBox.Show("未找到有效的合约名称", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 获取所有合约信息（如果还没加载）
                if (_allSymbols == null || _allSymbols.Count == 0)
                {
                    await LoadAllSymbolsAsync();
                }
                
                int addedCount = 0;
                int skippedExist = 0;
                int skippedInvalid = 0;
                var invalidSymbols = new List<string>();
                
                foreach (var symbol in symbols)
                {
                    // 检查是否已存在于组合中
                    if (_selectedSymbols.Any(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
                    {
                        skippedExist++;
                        continue;
                    }
                    
                    // 检查是否是有效的币安合约
                    var isValid = _allSymbols?.Any(s => 
                        s.Equals(symbol, StringComparison.OrdinalIgnoreCase)) ?? false;
                    
                    if (isValid)
                    {
                        _selectedSymbols.Add(new PortfolioSymbol 
                        { 
                            Symbol = symbol,
                            Remark = "" 
                        });
                        addedCount++;
                    }
                    else
                    {
                        skippedInvalid++;
                        invalidSymbols.Add(symbol);
                    }
                }
                
                // 更新显示
                UpdateSelectedSymbolsList();
                
                // 清空输入框
                txtBatchImport.Clear();
                
                // 显示导入结果
                var message = $"批量导入完成！\n\n" +
                    $"✅ 成功添加: {addedCount} 个\n" +
                    $"⊘ 已存在跳过: {skippedExist} 个\n" +
                    $"✖ 无效合约: {skippedInvalid} 个";
                
                if (invalidSymbols.Count > 0 && invalidSymbols.Count <= 10)
                {
                    message += $"\n\n无效合约:\n{string.Join(", ", invalidSymbols)}";
                }
                else if (invalidSymbols.Count > 10)
                {
                    message += $"\n\n无效合约（前10个）:\n{string.Join(", ", invalidSymbols.Take(10))}\n...等{invalidSymbols.Count}个";
                }
                
                MessageBox.Show(message, "批量导入结果", 
                    MessageBoxButton.OK, 
                    addedCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量导入失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnBatchImport.IsEnabled = true;
                btnBatchImport.Content = "批量导入";
            }
        }
    }
} 