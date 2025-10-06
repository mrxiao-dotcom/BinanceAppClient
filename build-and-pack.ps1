# BinanceApps 构建和打包脚本

param(
    [Parameter(Mandatory=$true)]
    [string]$Version  # 例如：1.0.1
)

# 配置
$ProjectDir = "D:\CSharpProjects\BinanceAppsClient\src\BinanceApps.WPF"
$PublishDir = "$ProjectDir\publish"
$OutputZip = "$ProjectDir\BinanceApps_v$Version.zip"

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  BinanceApps 构建和打包工具 v1.0" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 目标版本: $Version" -ForegroundColor Green
Write-Host ""

# 步骤 1：清理旧文件
Write-Host "━━━ 步骤 1/4：清理旧文件 ━━━" -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
    Write-Host "✓ 已删除旧的 publish 目录" -ForegroundColor Gray
}
if (Test-Path $OutputZip) {
    Remove-Item $OutputZip -Force
    Write-Host "✓ 已删除旧的 ZIP 文件" -ForegroundColor Gray
}
Write-Host "✅ 清理完成`n" -ForegroundColor Green

# 步骤 2：发布应用程序
Write-Host "━━━ 步骤 2/4：构建应用程序 ━━━" -ForegroundColor Yellow
Write-Host "正在执行: dotnet publish..." -ForegroundColor Gray
cd $ProjectDir
dotnet publish -c Release -r win-x64 --self-contained false -o $PublishDir 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 构建失败！请检查编译错误。" -ForegroundColor Red
    exit 1
}

# 统计文件数量
$FileCount = (Get-ChildItem -Path $PublishDir -Recurse -File).Count
$FolderCount = (Get-ChildItem -Path $PublishDir -Recurse -Directory).Count
Write-Host "✅ 构建完成" -ForegroundColor Green
Write-Host "   📁 文件数: $FileCount" -ForegroundColor Gray
Write-Host "   📂 文件夹数: $FolderCount`n" -ForegroundColor Gray

# 步骤 3：制作 ZIP 包
Write-Host "━━━ 步骤 3/4：制作更新包 ━━━" -ForegroundColor Yellow
Write-Host "正在压缩文件..." -ForegroundColor Gray
Compress-Archive -Path "$PublishDir\*" -DestinationPath $OutputZip -Force

if (-not (Test-Path $OutputZip)) {
    Write-Host "❌ ZIP 包创建失败！" -ForegroundColor Red
    exit 1
}

$ZipSize = (Get-Item $OutputZip).Length / 1MB
Write-Host "✅ 压缩完成" -ForegroundColor Green
Write-Host "   📦 文件大小: $([math]::Round($ZipSize, 2)) MB`n" -ForegroundColor Gray

# 步骤 4：验证 ZIP 包结构
Write-Host "━━━ 步骤 4/4：验证 ZIP 包结构 ━━━" -ForegroundColor Yellow
$VerifyDir = "$ProjectDir\verify-temp"
if (Test-Path $VerifyDir) {
    Remove-Item $VerifyDir -Recurse -Force
}
Expand-Archive -Path $OutputZip -DestinationPath $VerifyDir

$RootFiles = Get-ChildItem -Path $VerifyDir -File | Select-Object -First 3
$HasCorrectStructure = ($RootFiles.Count -gt 0)

if ($HasCorrectStructure) {
    Write-Host "✅ ZIP 包结构正确" -ForegroundColor Green
    Write-Host "   根目录文件示例:" -ForegroundColor Gray
    $RootFiles | ForEach-Object { Write-Host "   - $($_.Name)" -ForegroundColor Gray }
} else {
    Write-Host "⚠️  警告: ZIP 包结构可能不正确，请手动检查" -ForegroundColor Yellow
}

# 清理验证目录
Remove-Item $VerifyDir -Recurse -Force
Write-Host ""

# 最终报告
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  ✅ 更新包制作完成！" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "📄 更新包位置: " -NoNewline
Write-Host "$OutputZip" -ForegroundColor Cyan
Write-Host "📊 文件大小: " -NoNewline
Write-Host "$([math]::Round($ZipSize, 2)) MB" -ForegroundColor Cyan
Write-Host "🔢 版本号: " -NoNewline
Write-Host "$Version" -ForegroundColor Cyan
Write-Host ""
Write-Host "📤 下一步操作:" -ForegroundColor Yellow
Write-Host "   1. 访问服务器管理界面: http://192.168.1.101:8080" -ForegroundColor Gray
Write-Host "   2. 导航到'版本管理'" -ForegroundColor Gray
Write-Host "   3. 上传 ZIP 文件并填写版本信息" -ForegroundColor Gray
Write-Host "   4. 启动应用程序测试自动更新" -ForegroundColor Gray
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan 