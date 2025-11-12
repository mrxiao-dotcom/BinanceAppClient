# 清理并重新编译脚本
# 用于解决编译缓存问题

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  清理并重新编译 BinanceApps 项目" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 清理解决方案
Write-Host "[1/6] 清理解决方案..." -ForegroundColor Yellow
dotnet clean --configuration Debug
dotnet clean --configuration Release
Write-Host "✅ 清理完成" -ForegroundColor Green
Write-Host ""

# 2. 删除 bin 和 obj 目录
Write-Host "[2/6] 删除 bin 和 obj 目录..." -ForegroundColor Yellow
$binObjDirs = Get-ChildItem -Path "src" -Include "bin","obj" -Recurse -Directory -ErrorAction SilentlyContinue
$count = ($binObjDirs | Measure-Object).Count
if ($count -gt 0) {
    $binObjDirs | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "✅ 已删除 $count 个目录" -ForegroundColor Green
} else {
    Write-Host "✅ 没有需要删除的目录" -ForegroundColor Green
}
Write-Host ""

# 3. 恢复 NuGet 包
Write-Host "[3/6] 恢复 NuGet 包..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ NuGet 包恢复成功" -ForegroundColor Green
} else {
    Write-Host "❌ NuGet 包恢复失败" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 4. 编译 Core 项目
Write-Host "[4/6] 编译 BinanceApps.Core..." -ForegroundColor Yellow
Push-Location "src\BinanceApps.Core"
dotnet build --configuration Release
$coreResult = $LASTEXITCODE
Pop-Location
if ($coreResult -eq 0) {
    Write-Host "✅ Core 项目编译成功" -ForegroundColor Green
} else {
    Write-Host "❌ Core 项目编译失败" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 5. 编译 WPF 项目
Write-Host "[5/6] 编译 BinanceApps.WPF..." -ForegroundColor Yellow
Push-Location "src\BinanceApps.WPF"
dotnet build --configuration Release
$wpfResult = $LASTEXITCODE
Pop-Location
if ($wpfResult -eq 0) {
    Write-Host "✅ WPF 项目编译成功" -ForegroundColor Green
} else {
    Write-Host "❌ WPF 项目编译失败" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 6. 显示结果
Write-Host "[6/6] 编译完成！" -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ✅ 所有项目编译成功！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "现在可以运行应用程序了：" -ForegroundColor Yellow
Write-Host "  cd src\BinanceApps.WPF" -ForegroundColor White
Write-Host "  dotnet run --configuration Release" -ForegroundColor White
Write-Host ""

