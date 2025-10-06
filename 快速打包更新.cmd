@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo 快速打包更新（智能更新专用）
echo ========================================
echo.

:: 配置路径
set PUBLISH_DIR=src\BinanceApps.WPF\publish
set ZIP_NAME=BinanceApps_v1.0.8.zip

echo 📋 打包配置：
echo   源目录: %PUBLISH_DIR%
echo   输出文件: %ZIP_NAME%
echo.

:: 步骤 1：检查 publish 目录是否存在
echo 🔍 步骤 1/3：检查发布目录...
if not exist "%PUBLISH_DIR%" (
    echo.
    echo ❌ 错误：publish 目录不存在！
    echo.
    echo 📍 期望位置: %CD%\%PUBLISH_DIR%
    echo.
    echo 💡 解决方法：
    echo    1. 在 VS2022 中右键点击 BinanceApps.WPF 项目
    echo    2. 选择 "发布"
    echo    3. 确保发布到 publish 目录
    echo.
    pause
    exit /b 1
)

:: 检查必需文件
if not exist "%PUBLISH_DIR%\BinanceApps.WPF.exe" (
    echo.
    echo ❌ 错误：publish 目录中没有找到 BinanceApps.WPF.exe
    echo.
    echo 💡 请先在 VS2022 中发布项目
    echo.
    pause
    exit /b 1
)

echo ✅ 发布目录检查通过
echo.

:: 步骤 2：清理旧的 ZIP 文件
echo 🗑️  步骤 2/3：清理旧文件...
if exist "%ZIP_NAME%" (
    del /f /q "%ZIP_NAME%"
    echo ✅ 已删除旧的 ZIP 文件
) else (
    echo ℹ️  没有旧文件需要清理
)
echo.

:: 步骤 3：打包 ZIP（包含所有文件）
echo 📦 步骤 3/3：打包 ZIP...
pushd %PUBLISH_DIR%
powershell -Command "Compress-Archive -Path '*' -DestinationPath '..\..\..\%ZIP_NAME%' -Force"
popd

:: 检查打包结果
if exist "%ZIP_NAME%" (
    echo.
    echo ========================================
    echo ✅ 打包成功！
    echo ========================================
    echo.
    echo 📍 ZIP 文件位置: %CD%\%ZIP_NAME%
    
    :: 显示文件大小
    for %%A in (%ZIP_NAME%) do (
        set /a SIZE_BYTES=%%~zA
        set /a SIZE_KB=!SIZE_BYTES!/1024
        set /a SIZE_MB=!SIZE_BYTES!/1024/1024
        echo 📏 文件大小: !SIZE_BYTES! 字节 ^(!SIZE_KB! KB / !SIZE_MB! MB^)
    )
    
    echo.
    echo 💡 提示：
    echo    ✅ 注册码保存在 AppData 目录，更新不影响注册码
    echo    ✅ 智能更新只覆盖变更的文件
    echo.
    echo 📋 下一步：
    echo    1. 验证 ZIP 文件内容
    echo    2. 上传到更新服务器（版本 1.0.8）
    echo    3. 在客户端测试更新功能
    echo.
    

) else (
    echo.
    echo ========================================
    echo ❌ 打包失败！
    echo ========================================
    echo.
    echo 可能原因：
    echo   - PowerShell 执行权限不足
    echo   - publish 目录为空
    echo   - 磁盘空间不足
    echo.
)

pause 