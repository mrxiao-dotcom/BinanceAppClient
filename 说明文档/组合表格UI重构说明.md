# 组合表格UI重构说明

## 📋 重构概述

按照用户需求，将组合监控窗口的UI进行了重大重构：
1. ✅ 组合区域和明细区域等宽（各占50%，可拖动调整）
2. ✅ 组合区域改为表格样式，与明细区域一致
3. ✅ 添加可排序的表头
4. ✅ 组合和明细都添加了序号列

## 🎯 修改内容

### 1. XAML修改 (`CustomPortfolioWindow.xaml`)

#### 列宽调整为等宽
```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="*" MinWidth="600"/>  <!-- 组合列表（等宽，可调） -->
    <ColumnDefinition Width="Auto"/>    <!-- 分割条 -->
    <ColumnDefinition Width="*" MinWidth="600"/>    <!-- 组合明细（等宽，可调） -->
</Grid.ColumnDefinitions>
```

#### 添加组合列表表头区域
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>  <!-- 标题 -->
    <RowDefinition Height="Auto"/>  <!-- 分组标签 -->
    <RowDefinition Height="Auto"/>  <!-- 表头 ✨ 新增 -->
    <RowDefinition Height="*"/>     <!-- 组合列表 -->
</Grid.RowDefinitions>

<!-- 表头 -->
<Border Grid.Row="2" Background="#F0F0F0" BorderBrush="#E0E0E0" 
        BorderThickness="0,0,0,1" Padding="8,5">
    <StackPanel x:Name="panelPortfolioHeader" Orientation="Horizontal"/>
</Border>
```

### 2. C# 代码修改 (`CustomPortfolioWindow.xaml.cs`)

#### 添加排序状态字段
```csharp
// 组合列表排序状态
private string _portfolioSortColumn = ""; // Name, Change24h, Change30d, Count, Volume
private bool _portfolioSortAscending = true;

// 明细列表排序状态
private string _currentSortColumn = ""; // Change, Price, Volume
private bool _sortAscending = false;
```

#### DisplayPortfoliosList() - 显示组合列表
```csharp
private void DisplayPortfoliosList()
{
    // 1. 更新分组标签
    UpdateGroupTabs();
    
    // 2. 创建表头 ✨ 新增
    CreatePortfolioListHeader();
    
    // 3. 清空组合列表
    panelPortfolios.Children.Clear();
    
    // ... 空列表处理 ...
    
    // 4. 根据当前分组筛选组合
    var filteredData = _currentGroupFilter == "全部" 
        ? _portfolioRuntimeDataList 
        : _portfolioRuntimeDataList.Where(r => r.Portfolio.GroupName == _currentGroupFilter).ToList();
    
    // 5. 应用排序 ✨ 新增
    var sortedData = ApplyPortfolioSorting(filteredData);
    
    // 6. 显示筛选并排序后的组合
    int index = 1;
    foreach (var runtimeData in sortedData)
    {
        var row = CreatePortfolioRow(runtimeData, index); // ✨ 使用新的表格行方法
        panelPortfolios.Children.Add(row);
        index++;
    }
}
```

#### CreatePortfolioListHeader() - 创建组合列表表头 ✨ 新增
```csharp
private void CreatePortfolioListHeader()
{
    panelPortfolioHeader.Children.Clear();
    
    var grid = new Grid();
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // 序号
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); // 名称
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 24H涨幅
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 30天涨幅
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 数量
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 成交额
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 操作
    
    // 序号列（不可排序）
    // 可排序列：Name, Change24h, Change30d, Count, Volume
    // 操作列（不可排序）
}
```

#### CreateSortablePortfolioHeader() - 创建可排序表头 ✨ 新增
```csharp
private TextBlock CreateSortablePortfolioHeader(string column, string title)
{
    var isCurrentColumn = _portfolioSortColumn == column;
    var arrow = isCurrentColumn ? (_portfolioSortAscending ? " ↑" : " ↓") : "";
    
    // 创建可点击的表头文本
    // 支持：鼠标悬停变色、点击排序、排序指示箭头
}
```

#### ApplyPortfolioSorting() - 应用组合列表排序 ✨ 新增
```csharp
private List<PortfolioRuntimeData> ApplyPortfolioSorting(List<PortfolioRuntimeData> data)
{
    if (string.IsNullOrEmpty(_portfolioSortColumn))
    {
        return data;
    }
    
    IOrderedEnumerable<PortfolioRuntimeData> sorted = _portfolioSortColumn switch
    {
        "Name" => _portfolioSortAscending ? data.OrderBy(d => d.Portfolio.Name) : data.OrderByDescending(d => d.Portfolio.Name),
        "Change24h" => _portfolioSortAscending ? data.OrderBy(d => d.AveragePriceChangePercent) : data.OrderByDescending(d => d.AveragePriceChangePercent),
        "Change30d" => _portfolioSortAscending ? data.OrderBy(d => d.AveragePriceChangePercent30d) : data.OrderByDescending(d => d.AveragePriceChangePercent30d),
        "Count" => _portfolioSortAscending ? data.OrderBy(d => d.Portfolio.SymbolCount) : data.OrderByDescending(d => d.Portfolio.SymbolCount),
        "Volume" => _portfolioSortAscending ? data.OrderBy(d => d.SymbolsData.Sum(s => s.QuoteVolume)) : data.OrderByDescending(d => d.SymbolsData.Sum(s => s.QuoteVolume)),
        _ => data.OrderBy(d => d.Portfolio.Name)
    };
    
    return sorted.ToList();
}
```

#### CreatePortfolioRow() - 创建组合行（表格样式） ✨ 新增
```csharp
private Border CreatePortfolioRow(PortfolioRuntimeData runtimeData, int index)
{
    // 主容器：单行Border，无圆角，仅底部边框
    var border = new Border
    {
        BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
        BorderThickness = new Thickness(0, 0, 0, 1),
        Margin = new Thickness(0),
        Padding = new Thickness(8, 5, 8, 5),
        Background = _selectedPortfolioId == portfolio.Id 
            ? new SolidColorBrush(Color.FromRgb(230, 240, 255))
            : new SolidColorBrush(Colors.White),
        Cursor = System.Windows.Input.Cursors.Hand
    };
    
    // Grid布局，7列（序号、名称、24H涨幅、30天涨幅、数量、成交额、操作）
    var grid = new Grid();
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // 序号
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); // 名称
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 24H涨幅
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 30天涨幅
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 数量
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 成交额
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 操作
    
    // 序号
    var indexText = new TextBlock
    {
        Text = index.ToString(),
        FontSize = 12,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = new SolidColorBrush(Colors.Gray)
    };
    Grid.SetColumn(indexText, 0);
    grid.Children.Add(indexText);
    
    // 名称、24H涨幅、30天涨幅、数量、成交额、操作按钮（改、删）
    
    // 保留双击复制合约列表、单击选中功能
}
```

#### 明细区域序号修改

##### CreateTableHeader() - 添加序号列
```csharp
// 在Grid列定义的开始添加序号列
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });  // 序号

// 序号表头
var numberHeader = new TextBlock
{
    Text = "序号",
    FontWeight = FontWeights.Bold,
    FontSize = 12,
    Foreground = new SolidColorBrush(Colors.DarkGray),
    TextAlignment = TextAlignment.Center
};
Grid.SetColumn(numberHeader, 0);
grid.Children.Add(numberHeader);

// 其他列索引全部+1：合约名称(1)、24H涨幅(2)、价格(3)...
```

##### DisplayPortfolioDetails() - 传递序号
```csharp
var sortedData = ApplySorting(runtimeData.SymbolsData);
int index = 1;
foreach (var symbolData in sortedData)
{
    var symbolCard = CreateSymbolDetailCard(symbolData, index); // ✨ 传递序号
    panelSymbolDetails.Children.Add(symbolCard);
    index++;
}
```

##### CreateSymbolDetailCard() - 修改签名和内部结构
```csharp
// 方法签名修改
private Border CreateSymbolDetailCard(PortfolioSymbolData symbolData, int index) // ✨ 添加index参数

// 添加序号列定义
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });  // 序号

// 添加序号显示
var indexText = new TextBlock
{
    Text = index.ToString(),
    FontSize = 12,
    Foreground = new SolidColorBrush(Colors.Gray),
    TextAlignment = TextAlignment.Center,
    VerticalAlignment = VerticalAlignment.Center
};
Grid.SetColumn(indexText, 0);
grid.Children.Add(indexText);

// 其他列索引全部+1：合约名称(1)、24H涨幅(2)、价格(3)...
```

## 🎨 UI效果对比

### 修改前（卡片式）
```
┌─────────────────────────────────┐
│  组合名称: 经典主流               │
│  24H:+2.5% ↑  30天:+5.3% ↑  4个  │
│  成交额: $120M                   │
│  [改] [删]                       │
└─────────────────────────────────┘
┌─────────────────────────────────┐
│  组合名称: 次新潜力               │
│  ...                             │
└─────────────────────────────────┘
```

### 修改后（表格式）
```
┌─────────────────────────────────────────────────────────────┐
│ 序号 │ 组合名称   │ 24H涨幅 │ 30天涨幅 │ 数量 │ 成交额  │ 操作 │
├──────┼───────────┼─────────┼─────────┼─────┼────────┼──────┤
│  1   │ 经典主流   │ +2.5% ↑ │ +5.3% ↑ │ 4个  │ $120M  │ 改 删 │
│  2   │ 次新潜力   │ +1.8% ↑ │ +3.2% ↑ │ 6个  │ $85M   │ 改 删 │
│  3   │ 黑马板块   │ -0.5% ↓ │ +2.1% ↑ │ 8个  │ $50M   │ 改 删 │
└─────────────────────────────────────────────────────────────┘
```

## ✨ 新功能特性

### 组合列表表头排序
- 点击表头可排序（支持升序/降序/无序循环）
- 当前排序列显示蓝色，带↑/↓箭头
- 鼠标悬停在非当前排序列上，颜色变浅蓝提示可点击
- 支持排序列：组合名称、24H涨幅、30天涨幅、数量、成交额

### 序号显示
- 组合列表和明细列表都显示序号
- 序号随着排序、筛选（分组）自动更新
- 序号居中、灰色显示，不可点击

### 等宽可调布局
- 组合区域和明细区域初始各占50%宽度
- 中间的GridSplitter可拖动调整宽度比例
- 最小宽度600px，确保内容不被挤压

### 表格行交互
- 单击选中（背景变浅蓝）
- 双击复制所有合约符号（逗号分隔）
- "改"和"删"按钮事件阻止冒泡，避免触发行选中

## 🔧 测试要点

1. **表格布局**
   - [ ] 组合列表和明细区域宽度相等
   - [ ] 表头列宽与内容列宽对齐
   - [ ] GridSplitter可拖动调整宽度
   - [ ] 序号列宽度合适（45/50px）

2. **排序功能**
   - [ ] 点击组合列表表头可排序
   - [ ] 排序箭头方向正确（↑升序/↓降序）
   - [ ] 明细列表排序功能不受影响
   - [ ] 序号随排序自动更新

3. **序号显示**
   - [ ] 组合列表序号从1开始递增
   - [ ] 明细列表序号从1开始递增
   - [ ] 筛选分组后，序号重新计数
   - [ ] 排序后，序号重新计数

4. **交互功能**
   - [ ] 单击组合行选中（背景变蓝）
   - [ ] 双击组合行复制合约列表
   - [ ] "改"和"删"按钮正常工作
   - [ ] 明细区域的双击复制合约功能正常

5. **数据正确性**
   - [ ] 24H涨幅、30天涨幅计算正确
   - [ ] 成交额格式化正确（$XXB/$XXM/$XXK）
   - [ ] 颜色显示正确（涨=绿、跌=红、平=灰）
   - [ ] 数量统计正确

## 📦 文件清单

- ✅ `CustomPortfolioWindow.xaml` - XAML布局
- ✅ `CustomPortfolioWindow.xaml.cs` - 代码逻辑
- ✅ `组合表格UI重构说明.md` - 本文档

## 🎯 后续优化建议

1. **响应式布局**：当窗口宽度不足时，自动隐藏部分列（如30天最低、备注）
2. **固定表头**：滚动内容时，表头保持可见
3. **快捷键**：支持键盘上下键选择组合
4. **批量操作**：支持Ctrl+点击多选组合
5. **导出功能**：导出组合列表为CSV/Excel

---

**重构完成日期**: 2025-10-02  
**修改人**: AI Assistant  
**版本**: 1.0 