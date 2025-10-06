# BinanceApps - 币安市场数据分析工具

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)
[![WPF](https://img.shields.io/badge/UI-WPF-lightblue.svg)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

一个基于 WPF 的币安期货市场数据分析工具，提供实时行情监控、波动率分析、成交量趋势追踪等功能。

## 🚀 主要功能

### 📊 市场数据分析
- **24H涨幅排名** - 实时显示涨幅最大的交易对
- **放量增长监控** - 分析成交量异常增长的合约
- **波动率一览** - 市场波动率统计和可视化分析
- **成交额趋势图** - 柱状图展示每日成交额变化及5日移动平均线

### 📈 数据可视化
- **响应式图表** - 自适应窗口大小的专业图表
- **交互式排序** - 点击表头进行升序/降序排列
- **实时数据更新** - 自动获取最新市场数据
- **专业UI设计** - 现代化的用户界面

### 🔧 技术特性
- **模拟/真实API** - 支持模拟数据和真实API切换
- **数据缓存机制** - 智能K线数据缓存和增量更新
- **异步编程** - 非阻塞UI操作
- **资源管理** - 完善的内存和资源管理

## 📸 界面截图

### 主界面
- 清晰的导航菜单
- 实时数据显示
- 专业的数据表格

### 波动率一览
- 波动率热力图
- 成交额柱状图
- 5日移动平均线
- 当前24H总成交额

### 排名列表
- 可排序的数据表格
- 实时涨跌幅显示
- 成交量分析

## 🛠️ 技术栈

- **框架**: .NET 8.0 + WPF
- **架构**: 多层架构 (Core, WPF, Services)
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **HTTP客户端**: HttpClient
- **数据格式**: JSON
- **测试框架**: xUnit

## 📁 项目结构

```
BinanceApps/
├── src/
│   ├── BinanceApps.Core/           # 核心业务逻辑
│   │   ├── Models/                 # 数据模型
│   │   ├── Services/               # 业务服务
│   │   └── Interfaces/             # 接口定义
│   ├── BinanceApps.WPF/            # WPF用户界面
│   │   ├── MainWindow.xaml         # 主窗口
│   │   ├── ApiSettingsWindow.xaml  # API设置窗口
│   │   └── VolatilityDetailsWindow.xaml # 波动率详情
│   ├── BinanceApps.Account/        # 账户管理模块
│   ├── BinanceApps.MarketData/     # 市场数据模块
│   ├── BinanceApps.Storage/        # 数据存储模块
│   └── BinanceApps.Trading/        # 交易模块
├── tests/                          # 单元测试
│   └── BinanceApps.Core.Tests/
└── Examples/                       # 使用示例
```

## 🚀 快速开始

### 环境要求
- .NET 8.0 SDK
- Windows 10/11
- Visual Studio 2022 或 VS Code

### 安装步骤

1. **克隆仓库**
```bash
git clone https://github.com/mrxiao-dotcom/BinanceApps.git
cd BinanceApps
```

2. **还原依赖**
```bash
dotnet restore
```

3. **构建项目**
```bash
dotnet build
```

4. **运行应用程序**
```bash
dotnet run --project src/BinanceApps.WPF
```

### 配置说明

#### API配置
编辑 `src/BinanceApps.WPF/appsettings.json`:

```json
{
  "BinanceApi": {
    "BaseUrl": "https://fapi.binance.com",
    "UseSimulatedData": true,
    "RateLimitPerMinute": 1200,
    "RequestTimeout": "00:00:30"
  }
}
```

- `UseSimulatedData`: 设置为 `true` 使用模拟数据，`false` 使用真实API
- `RateLimitPerMinute`: API请求频率限制
- `RequestTimeout`: 请求超时时间

## 📊 功能详解

### 24H涨幅排名
- 显示24小时内涨幅最大的永续合约
- 实时价格、涨跌幅、成交量信息
- 支持点击表头排序

### 放量增长分析
- 对比昨日和今日成交量
- 计算放量增长倍数
- 过去10日平均成交量对比
- 可视化显示放量程度

### 波动率监控
- 按日期统计市场波动率
- 热力图显示波动程度
- 支持点击查看详细信息

### 成交额趋势图
- 每日总成交额柱状图
- 5日移动平均线
- 当前24H实时总成交额
- 响应式图表设计

## 🔧 开发说明

### 添加新功能
1. 在 `BinanceApps.Core` 中定义接口和模型
2. 实现业务逻辑服务
3. 在 WPF 项目中添加UI界面
4. 注册依赖注入服务

### 数据流程
1. `IBinanceApiClient` 获取原始数据
2. 数据处理和缓存在 `Services` 中
3. WPF界面绑定处理后的数据
4. 用户交互触发数据更新

### 测试
```bash
# 运行所有测试
dotnet test

# 运行特定测试项目
dotnet test tests/BinanceApps.Core.Tests/
```

## 📝 更新日志

### v1.0.0 (2024-01-XX)
- ✅ 基础市场数据获取功能
- ✅ 24H涨幅排名
- ✅ 放量增长分析
- ✅ 波动率一览
- ✅ 响应式成交额图表
- ✅ 可排序数据表格
- ✅ 模拟数据支持
- ✅ 完善的错误处理

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## ⚠️ 免责声明

本工具仅用于市场数据分析和学习目的，不构成投资建议。使用本工具进行交易决策的风险由用户自行承担。

## 🔗 相关链接

- [币安API文档](https://binance-docs.github.io/apidocs/futures/cn/)
- [.NET 8.0 文档](https://docs.microsoft.com/en-us/dotnet/)
- [WPF 开发指南](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)

## 📞 联系方式

如有问题或建议，请通过以下方式联系：

- 提交 [GitHub Issue](https://github.com/mrxiao-dotcom/BinanceApps/issues)
- 发送邮件至项目维护者

---

⭐ 如果这个项目对您有帮助，请给个 Star！ 