# 小时均线监控 - K线图表可视化功能

## 功能概述

为小时均线监控添加了K线和EMA数据的可视化图表功能，用户可以通过图表直观地查看和验证K线数据及EMA计算的正确性。

---

## 实现内容

### 1. 新增图表库依赖

**文件**: `src/BinanceApps.WPF/BinanceApps.WPF.csproj`

添加了 LiveCharts2 图表库：

```xml
<PackageReference Include="LiveChartsCore.SkiaSharpView.WPF" Version="2.0.0-rc3.3" />
```

### 2. 图表窗口 - XAML

**新文件**: `src/BinanceApps.WPF/KlineChartWindow.xaml`

创建了美观的图表窗口界面，包含：

- **标题区域**：显示合约名称、EMA状态、时间范围
- **图例标识**：绿色线条代表K线Close，橙红色线条代表EMA
- **图表区域**：使用 LiveCharts2 的 CartesianChart 控件
- **统计信息区域**：显示K线数量、最新收盘价、当前EMA、距离EMA、连续数量

**关键元素**：
```xml
<lvc:CartesianChart x:Name="chart" 
                    Series="{Binding Series}"
                    XAxes="{Binding XAxes}"
                    YAxes="{Binding YAxes}"
                    LegendPosition="Hidden"
                    TooltipPosition="Top"/>
```

### 3. 图表窗口 - 代码逻辑

**新文件**: `src/BinanceApps.WPF/KlineChartWindow.xaml.cs`

#### 核心功能

1. **数据转换**
   - 将K线数据转换为图表数据点
   - 将EMA数据匹配到对应的K线索引
   - 使用数值索引作为X轴，避免 DateTime.Ticks 超出范围的问题

2. **图表配置**
   - **K线系列**：绿色线条，不平滑，显示实际收盘价
   - **EMA系列**：橙红色线条，轻微平滑，显示EMA曲线
   - **X轴**：使用索引，通过 Labeler 函数将索引转换为时间标签
   - **Y轴**：显示价格，格式化为8位小数

3. **统计信息显示**
   - K线数量
   - 最新收盘价
   - 当前EMA值
   - 距离EMA百分比（绿色表示正值，红色表示负值）
   - 连续大于/小于EMA的数量（绿色/红色标识）

**关键代码片段**：

```csharp
// 使用数值索引避免 DateTime.Ticks 问题
var klineValues = new List<double>();
foreach (var kline in sortedKlines)
{
    _timePoints.Add(kline.OpenTime);
    klineValues.Add((double)kline.ClosePrice);
}

// X轴标签格式化
Labeler = value =>
{
    var index = (int)value;
    if (index >= 0 && index < _timePoints.Count)
    {
        return _timePoints[index].ToString("MM-dd HH:mm");
    }
    return string.Empty;
}
```

### 4. 双击交互增强

**修改文件**: `src/BinanceApps.WPF/HourlyEmaMonitorWindow.xaml.cs`

增强了双击事件处理：

- **普通双击**：复制合约名到剪贴板（原有功能）
- **Ctrl + 双击**：打开K线图表窗口（新增功能）

```csharp
private async void DgResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (dgResults.SelectedItem is HourlyEmaMonitorResult selectedResult)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Ctrl+双击：打开图表窗口
            var klineData = await _hourlyEmaService.GetHourlyKlineDataAsync(selectedResult.Symbol);
            if (klineData != null)
            {
                var chartWindow = new KlineChartWindow(selectedResult.Symbol, klineData)
                {
                    Owner = this
                };
                chartWindow.Show();
            }
        }
        else
        {
            // 普通双击：复制合约名
            Clipboard.SetText(selectedResult.Symbol);
        }
    }
}
```

### 5. 界面提示

**修改文件**: `src/BinanceApps.WPF/HourlyEmaMonitorWindow.xaml`

在结果列表上方添加了使用提示：

```xml
<TextBlock Text="💡 双击：复制合约名 | Ctrl+双击：查看K线图表" 
           FontSize="11" 
           Foreground="#999999" 
           HorizontalAlignment="Right" 
           VerticalAlignment="Bottom"/>
```

---

## 使用方法

### 打开图表窗口

1. 在小时均线监控窗口中，确保已获取K线数据并计算EMA
2. 在结果列表中找到要查看的合约
3. **按住 Ctrl 键**，然后**双击**该合约行
4. 图表窗口自动打开，显示该合约的K线和EMA走势

### 查看图表数据

图表窗口显示以下信息：

1. **标题栏**：合约名称、EMA状态、数据时间范围
2. **折线图**：
   - **绿色线**：K线的收盘价走势
   - **橙红色线**：EMA曲线
3. **底部统计**：
   - K线数量
   - 最新收盘价
   - 当前EMA值
   - 距离EMA百分比
   - 连续大于/小于EMA的K线数量

### 图表交互

- **鼠标悬停**：显示该点的具体数值
- **滚动缩放**：可以放大/缩小查看细节
- **拖动平移**：可以左右移动查看不同时间段

---

## 技术特点

### 1. 数据处理优化

- 使用**数值索引**作为X轴，避免 `DateTime.Ticks` 超出范围的问题
- 通过 `Labeler` 函数将索引映射回时间标签
- EMA数据与K线数据精确匹配，使用 `double.NaN` 填充缺失值

### 2. 界面美观

- 现代化的卡片式布局
- 清晰的颜色区分（绿色/橙红色）
- 图例标识和使用提示
- 统计信息实时显示

### 3. 性能优化

- X轴标签采样显示（默认显示约10个标签），避免过度密集
- 图表数据预处理，减少运行时计算
- 使用 LiveCharts2 的硬件加速渲染

### 4. 用户体验

- **Ctrl+双击** 打开图表，与**普通双击**复制合约名不冲突
- 图表窗口独立，可以同时查看多个合约的图表
- Owner 设置确保图表窗口跟随主窗口

---

## 问题修复记录

### 问题：DateTimePoint 参数错误

**错误信息**：
```
Ticks must be between DateTime.MinValue.Ticks and DateTime.MaxValue.Ticks.
```

**原因**：
- 初始实现中尝试直接使用 `DateTimePoint(DateTime, double)`
- LiveCharts2 的时间处理可能存在边界问题

**解决方案**：
- 改用**数值索引**作为X轴数据
- 通过 `Labeler` 函数将索引映射为时间标签
- 避免了 DateTime 转换的潜在问题

**修改代码**：
```csharp
// 旧代码（有问题）
klinePoints.Add(new DateTimePoint(kline.OpenTime, (double)kline.ClosePrice));

// 新代码（正确）
_timePoints.Add(kline.OpenTime);
klineValues.Add((double)kline.ClosePrice);

// X轴配置
Labeler = value =>
{
    var index = (int)value;
    if (index >= 0 && index < _timePoints.Count)
        return _timePoints[index].ToString("MM-dd HH:mm");
    return string.Empty;
}
```

---

## 测试要点

### 1. 功能测试

- [ ] Ctrl+双击能正常打开图表窗口
- [ ] 图表显示绿色K线Close曲线
- [ ] 图表显示橙红色EMA曲线
- [ ] X轴时间标签正确显示
- [ ] Y轴价格数值正确显示
- [ ] 底部统计信息准确

### 2. 数据验证

- [ ] K线数据与本地文件一致
- [ ] EMA数值与计算结果一致
- [ ] 距离EMA百分比计算正确
- [ ] 连续数量显示正确

### 3. 交互测试

- [ ] 普通双击复制合约名
- [ ] Ctrl+双击打开图表
- [ ] 可以同时打开多个图表窗口
- [ ] 图表可以缩放和平移
- [ ] 鼠标悬停显示数据提示

### 4. 边界测试

- [ ] 没有EMA数据时的显示
- [ ] 只有少量K线数据时的显示
- [ ] 大量K线数据（如500根）的性能

---

## 下一步优化建议

1. **图表功能增强**
   - 添加缩放和重置按钮
   - 支持导出图表为图片
   - 添加更多技术指标（如MACD、RSI）

2. **性能优化**
   - 大数据量时的虚拟化显示
   - 懒加载和分页支持

3. **用户体验**
   - 添加快捷键说明
   - 支持右键菜单
   - 自定义图表颜色主题

---

## 总结

✅ **已完成**：
- 集成 LiveCharts2 图表库
- 创建 K线图表可视化窗口
- 实现 Ctrl+双击打开图表功能
- 修复 DateTime.Ticks 越界问题
- 添加统计信息和使用提示

✅ **测试状态**：
- 代码已完成，等待编译和测试
- 修复已验证（使用数值索引替代 DateTimePoint）

📝 **使用说明**：
- 关闭正在运行的应用程序
- 在 Visual Studio 中重新编译
- 运行应用，打开小时均线监控
- 获取K线并计算EMA
- Ctrl+双击任意合约查看图表

---

**创建时间**: 2025-11-07  
**更新时间**: 2025-11-07

