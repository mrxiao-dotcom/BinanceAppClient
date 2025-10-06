using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 更新助手 - 处理应用程序更新的辅助工具
    /// </summary>
    public static class UpdateHelper
    {
        /// <summary>
        /// 创建并启动更新脚本
        /// </summary>
        /// <param name="updatePackagePath">更新包路径（ZIP文件）</param>
        /// <param name="targetDirectory">目标安装目录</param>
        /// <param name="currentExePath">当前程序路径</param>
        /// <param name="newVersion">新版本号</param>
        public static void StartUpdate(string updatePackagePath, string targetDirectory, string currentExePath, string newVersion)
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🔄 准备启动更新程序");
            Console.WriteLine($"   更新包: {updatePackagePath}");
            Console.WriteLine($"   目标目录: {targetDirectory}");
            Console.WriteLine($"   当前进程: {Process.GetCurrentProcess().Id}");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            // 创建更新脚本
            var scriptPath = Path.Combine(Path.GetTempPath(), $"BinanceApps_Update_{DateTime.Now.Ticks}.cmd");
            var scriptContent = GenerateUpdateScript(
                updatePackagePath, 
                targetDirectory, 
                currentExePath, 
                scriptPath,
                newVersion
            );
            
            // 注册编码提供程序以支持 GBK
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            File.WriteAllText(scriptPath, scriptContent, Encoding.GetEncoding("GBK")); // 使用GBK编码以支持中文
            
            Console.WriteLine($"✅ 更新脚本已创建: {scriptPath}");
            Console.WriteLine("📋 脚本内容:");
            Console.WriteLine(scriptContent);
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            // 启动更新脚本
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                WorkingDirectory = targetDirectory,
                CreateNoWindow = false, // 显示窗口，方便调试
                UseShellExecute = true
            };
            
            Process.Start(startInfo);
            Console.WriteLine("✅ 更新程序已启动");
        }
        
        /// <summary>
        /// 生成更新脚本
        /// </summary>
        private static string GenerateUpdateScript(
            string updatePackagePath, 
            string targetDirectory, 
            string currentExePath,
            string scriptPath,
            string newVersion)
        {
            var processId = Process.GetCurrentProcess().Id;
            var tempExtractDir = Path.Combine(Path.GetTempPath(), $"BinanceApps_Extract_{DateTime.Now.Ticks}");
            var exeName = Path.GetFileName(currentExePath);
            
            var script = new StringBuilder();
            script.AppendLine("@echo off");
            script.AppendLine("chcp 936 >nul"); // 使用GBK编码（936）避免乱码
            script.AppendLine("echo ========================================");
            script.AppendLine("echo BinanceApps 自动更新程序");
            script.AppendLine($"echo 版本: {newVersion}");
            script.AppendLine("echo ========================================");
            script.AppendLine("echo.");
            script.AppendLine();
            
            // 步骤 1：等待主程序退出
            script.AppendLine("echo 步骤 1/5：等待主程序退出...");
            script.AppendLine($"taskkill /PID {processId} /F >nul 2>&1");
            script.AppendLine("timeout /t 2 /nobreak >nul");
            script.AppendLine("echo   已退出");
            script.AppendLine("echo.");
            script.AppendLine();
            
            // 步骤 2：解压更新包
            script.AppendLine("echo 步骤 2/5：解压更新包...");
            script.AppendLine($"powershell -Command \"Expand-Archive -Path '{updatePackagePath}' -DestinationPath '{tempExtractDir}' -Force\"");
            script.AppendLine("if errorlevel 1 (");
            script.AppendLine("    echo   解压失败！");
            script.AppendLine("    pause");
            script.AppendLine("    exit /b 1");
            script.AppendLine(")");
            script.AppendLine("echo   解压完成");
            script.AppendLine("echo.");
            script.AppendLine();
            
            // 步骤 3：备份当前版本（可选）
            script.AppendLine("echo 步骤 3/5：备份当前版本...");
            var backupDir = Path.Combine(targetDirectory, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
            script.AppendLine($"if exist \"{currentExePath}\" (");
            script.AppendLine($"    mkdir \"{backupDir}\" >nul 2>&1");
            script.AppendLine($"    copy \"{currentExePath}\" \"{backupDir}\\\" >nul 2>&1");
            script.AppendLine($"    echo   已备份主程序到: {backupDir}");
            script.AppendLine(") else (");
            script.AppendLine("    echo   主程序不存在，跳过备份");
            script.AppendLine(")");
            script.AppendLine("echo.");
            script.AppendLine();
            
            // 步骤 4：复制新文件（智能更新）
            script.AppendLine("echo 步骤 4/5：更新文件...");
            script.AppendLine("echo   正在复制新文件...");
            script.AppendLine();
            
            // 保护的文件列表
            script.AppendLine(":: 受保护的文件（不覆盖）");
            script.AppendLine($"set PROTECTED_FILES=App.config appsettings.json");
            script.AppendLine($"set PROTECTED_EXTS=.db .log .dat");
            script.AppendLine();
            
            // 使用 robocopy 复制文件（排除受保护的文件）
            script.AppendLine(":: 复制所有文件，排除受保护的文件");
            script.AppendLine($"robocopy \"{tempExtractDir}\" \"{targetDirectory}\" /E /XO /XF App.config appsettings.json *.db *.log *.dat /NFL /NDL /NJH /NJS /nc /ns /np");
            script.AppendLine("if errorlevel 8 (");
            script.AppendLine("    echo   复制失败！错误代码: %errorlevel%");
            script.AppendLine("    pause");
            script.AppendLine("    exit /b 1");
            script.AppendLine(")");
            script.AppendLine("echo   文件更新完成");
            script.AppendLine("echo.");
            script.AppendLine();
            
            // 步骤 5：清理并重启
            script.AppendLine("echo 步骤 5/5：清理并重启...");
            script.AppendLine($"if exist \"{updatePackagePath}\" del /f /q \"{updatePackagePath}\" >nul 2>&1");
            script.AppendLine($"if exist \"{tempExtractDir}\" rd /s /q \"{tempExtractDir}\" >nul 2>&1");
            script.AppendLine("echo   临时文件已清理");
            script.AppendLine("echo.");
            script.AppendLine();
            
            // 重启应用
            script.AppendLine("echo ========================================");
            script.AppendLine($"echo 更新完成！正在启动新版本 v{newVersion}...");
            script.AppendLine("echo ========================================");
            script.AppendLine("timeout /t 2 /nobreak >nul");
            script.AppendLine($"start \"\" \"{currentExePath}\"");
            script.AppendLine();
            
            // 自我删除
            script.AppendLine(":: 删除自身");
            script.AppendLine("timeout /t 1 /nobreak >nul");
            script.AppendLine($"del /f /q \"{scriptPath}\" >nul 2>&1");
            script.AppendLine("exit");
            
            return script.ToString();
        }
    }
} 