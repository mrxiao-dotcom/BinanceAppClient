using System;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RegisterSrv.AutoUpdate;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 修复下载 URL 问题的自定义更新管理器
    /// 自动设置 HttpClient 的 BaseAddress 并输出详细调试信息
    /// </summary>
    public class FixedUpdateManager
    {
        private readonly UpdateClient _updateClient;
        private readonly string _serverUrl;
        private readonly UpdateConfig _config;

        public FixedUpdateManager(UpdateConfig config)
        {
            _config = config;
            _serverUrl = config.ServerUrl.TrimEnd('/');
            
            // 创建带有 BaseAddress 的 HttpClient
            var httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri(_serverUrl),
                Timeout = TimeSpan.FromMinutes(10)
            };
            
            // 使用自定义 HttpClient 创建 UpdateClient
            _updateClient = new UpdateClient(config.ServerUrl, config.AppId, httpClient);
            
            Console.WriteLine($"✅ FixedUpdateManager 已初始化");
            Console.WriteLine($"   BaseAddress: {_serverUrl}");
        }

        /// <summary>
        /// 智能安装更新：只覆盖需要更新的文件，保护配置文件
        /// </summary>
        private async Task<bool> SmartInstallUpdateAsync(UpdateInfo updateInfo, string targetDirectory, string[] protectedPatterns)
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"BinanceApps_Update_{DateTime.Now.Ticks}");
            var downloadPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"update_{updateInfo.Version}_{Guid.NewGuid()}.zip");
            
            try
            {
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"📥 开始智能更新安装");
                Console.WriteLine($"   目标目录: {targetDirectory}");
                Console.WriteLine($"   临时目录: {tempDir}");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                // 1. 下载更新包
                Console.WriteLine($"⬇️  第 1 步：下载更新包...");
                var httpClient = new System.Net.Http.HttpClient
                {
                    BaseAddress = new Uri(_serverUrl),
                    Timeout = TimeSpan.FromMinutes(10)
                };
                
                using (var response = await httpClient.GetAsync(updateInfo.DownloadUrl))
                {
                    response.EnsureSuccessStatusCode();
                    await using (var fs = new System.IO.FileStream(downloadPath, System.IO.FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                    Console.WriteLine($"   ✅ 下载完成: {new System.IO.FileInfo(downloadPath).Length / 1024.0 / 1024.0:F2} MB");
                }
                
                // 2. 解压更新包
                Console.WriteLine($"📦 第 2 步：解压更新包...");
                System.IO.Directory.CreateDirectory(tempDir);
                System.IO.Compression.ZipFile.ExtractToDirectory(downloadPath, tempDir);
                Console.WriteLine($"   ✅ 解压完成");
                
                // 3. 分析文件
                var updateFiles = System.IO.Directory.GetFiles(tempDir, "*", System.IO.SearchOption.AllDirectories);
                Console.WriteLine($"📋 第 3 步：分析文件（共 {updateFiles.Length} 个文件）");
                
                int updatedCount = 0;
                int skippedCount = 0;
                int protectedCount = 0;
                
                foreach (var sourceFile in updateFiles)
                {
                    var relativePath = System.IO.Path.GetRelativePath(tempDir, sourceFile);
                    var targetFile = System.IO.Path.Combine(targetDirectory, relativePath);
                    var fileName = System.IO.Path.GetFileName(sourceFile);
                    
                    // 检查是否是受保护的文件
                    bool isProtected = false;
                    foreach (var pattern in protectedPatterns)
                    {
                        if (pattern.Contains("*"))
                        {
                            // 通配符匹配
                            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                            if (System.Text.RegularExpressions.Regex.IsMatch(fileName, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            {
                                isProtected = true;
                                break;
                            }
                        }
                        else if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            isProtected = true;
                            break;
                        }
                    }
                    
                    if (isProtected)
                    {
                        Console.WriteLine($"   🛡️  保护: {relativePath}");
                        protectedCount++;
                        continue;
                    }
                    
                    // 检查文件是否需要更新
                    bool needUpdate = true;
                    if (System.IO.File.Exists(targetFile))
                    {
                        var sourceHash = GetFileHash(sourceFile);
                        var targetHash = GetFileHash(targetFile);
                        needUpdate = sourceHash != targetHash;
                        
                        if (!needUpdate)
                        {
                            skippedCount++;
                            continue;
                        }
                    }
                    
                    // 复制文件
                    try
                    {
                        var targetDir = System.IO.Path.GetDirectoryName(targetFile);
                        if (!System.IO.Directory.Exists(targetDir))
                        {
                            System.IO.Directory.CreateDirectory(targetDir!);
                        }
                        
                        System.IO.File.Copy(sourceFile, targetFile, true);
                        Console.WriteLine($"   ✅ 更新: {relativePath}");
                        updatedCount++;
                    }
                    catch (Exception copyEx)
                    {
                        Console.WriteLine($"   ⚠️  复制失败: {relativePath} - {copyEx.Message}");
                    }
                }
                
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"✅ 智能更新完成");
                Console.WriteLine($"   总文件: {updateFiles.Length}");
                Console.WriteLine($"   已更新: {updatedCount}");
                Console.WriteLine($"   已跳过: {skippedCount}（相同）");
                Console.WriteLine($"   受保护: {protectedCount}");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                // 保存服务器版本号
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        App.SaveCurrentVersion(updateInfo.Version);
                    });
                    Console.WriteLine($"💾 已保存服务器版本号: {updateInfo.Version}");
                }
                catch { }
                
                return true; // 更新成功
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 智能更新失败: {ex.Message}");
                Console.WriteLine($"   堆栈: {ex.StackTrace}");
                return false; // 更新失败
            }
            finally
            {
                // 清理临时文件
                try
                {
                    if (System.IO.File.Exists(downloadPath))
                    {
                        System.IO.File.Delete(downloadPath);
                    }
                    if (System.IO.Directory.Exists(tempDir))
                    {
                        System.IO.Directory.Delete(tempDir, true);
                    }
                    Console.WriteLine($"🗑️  已清理临时文件");
                }
                catch { }
            }
        }
        
        /// <summary>
        /// 计算文件哈希（用于比较文件是否相同）
        /// </summary>
        private string GetFileHash(string filePath)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = System.IO.File.OpenRead(filePath))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        public async Task<bool> CheckAndUpdateAsync(Window? owner = null, bool silent = false)
        {
            string? preDownloadedFile = null; // 记录预下载的文件路径
            
            try
            {
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"🔍 开始检查更新");
                Console.WriteLine($"   服务器: {_serverUrl}");
                Console.WriteLine($"   应用ID: {_config.AppId}");
                Console.WriteLine($"   应用名称: {_config.AppName}");
                Console.WriteLine($"   当前版本: {_config.CurrentVersion}");
                Console.WriteLine($"   检查 URL: {_serverUrl}/api/update/check?appId={_config.AppId}&currentVersion={_config.CurrentVersion}");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                // 检查更新
                var checkStartTime = DateTime.Now;
                var checkResult = await _updateClient.CheckUpdateAsync(_config.CurrentVersion);
                var checkDuration = (DateTime.Now - checkStartTime).TotalMilliseconds;
                
                Console.WriteLine($"📡 更新检查响应 (耗时: {checkDuration:F0} ms)");
                Console.WriteLine($"   IsSuccess: {checkResult.IsSuccess}");
                Console.WriteLine($"   HasUpdate: {checkResult.HasUpdate}");
                
                if (!checkResult.IsSuccess)
                {
                    Console.WriteLine($"❌ 检查更新失败");
                    Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    if (!silent)
                    {
                        MessageBox.Show("检查更新失败，请查看输出窗口获取详细信息", 
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return false;
                }
                
                if (!checkResult.HasUpdate)
                {
                    Console.WriteLine($"✅ 已是最新版本");
                    Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    if (!silent)
                    {
                        MessageBox.Show("当前已是最新版本", "检查更新", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return false;
                }
                
                var updateInfo = checkResult.UpdateInfo;
                if (updateInfo == null)
                {
                    Console.WriteLine($"❌ 更新信息为空");
                    Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    return false;
                }
                
                Console.WriteLine($"📦 发现新版本: {updateInfo.Version}");
                Console.WriteLine($"📥 下载 URL: '{updateInfo.DownloadUrl}'");
                Console.WriteLine($"📊 文件大小: {updateInfo.FileSize / 1024.0 / 1024.0:F2} MB");
                Console.WriteLine($"🔐 MD5: {updateInfo.FileMD5}");
                Console.WriteLine($"🔒 强制更新: {updateInfo.IsForceUpdate}");
                
                // 检查 URL 格式
                bool isAbsoluteUrl = Uri.IsWellFormedUriString(updateInfo.DownloadUrl, UriKind.Absolute);
                Console.WriteLine($"✓ URL 类型: {(isAbsoluteUrl ? "绝对路径" : "相对路径")}");
                
                if (!isAbsoluteUrl)
                {
                    Console.WriteLine($"🔧 相对路径将使用 BaseAddress: {_serverUrl}");
                }
                
                // 如果 MD5 为空，需要下载后计算实际 MD5
                if (string.IsNullOrEmpty(updateInfo.FileMD5))
                {
                    Console.WriteLine($"⚠️ 警告：服务器未提供 MD5");
                    Console.WriteLine($"⚠️ 将先下载文件并计算实际 MD5 值");
                    
                    // 使用固定的文件名（基于版本号），避免重复下载
                    string tempFile = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(), 
                        $"BinanceApps_Update_{updateInfo.Version}.zip"
                    );
                    
                    // 检查文件是否已存在
                    if (System.IO.File.Exists(tempFile))
                    {
                        Console.WriteLine($"🔍 发现已存在的文件: {tempFile}");
                        Console.WriteLine($"   跳过下载，直接验证文件");
                    }
                    else
                    {
                        // 文件不存在，需要下载
                        Console.WriteLine($"📥 预下载文件到: {tempFile}");
                    
                        try
                        {
                            Console.WriteLine($"📥 预下载文件以计算 MD5...");
                        Console.WriteLine($"   下载地址: {_serverUrl}{updateInfo.DownloadUrl}");
                        Console.WriteLine($"   临时位置: {tempFile}");
                        Console.WriteLine($"   文件大小: {updateInfo.FileSize / 1024.0 / 1024.0:F2} MB");
                        
                        var startTime = DateTime.Now;
                        long downloadedBytes = 0;
                        
                        using (var httpClient = new System.Net.Http.HttpClient { BaseAddress = new Uri(_serverUrl), Timeout = TimeSpan.FromMinutes(10) })
                        {
                            Console.WriteLine($"   发送下载请求...");
                            var response = await httpClient.GetAsync(updateInfo.DownloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                            
                            Console.WriteLine($"   响应状态: {(int)response.StatusCode} {response.StatusCode}");
                            response.EnsureSuccessStatusCode();
                            
                            var contentLength = response.Content.Headers.ContentLength ?? updateInfo.FileSize;
                            Console.WriteLine($"   开始接收数据，总大小: {contentLength / 1024.0 / 1024.0:F2} MB");
                            
                            await using (var contentStream = await response.Content.ReadAsStreamAsync())
                            await using (var fs = new System.IO.FileStream(tempFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 8192, true))
                            {
                                var buffer = new byte[8192];
                                int bytesRead;
                                int lastProgress = -1;
                                
                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fs.WriteAsync(buffer, 0, bytesRead);
                                    downloadedBytes += bytesRead;
                                    
                                    // 显示进度（每10%显示一次）
                                    int progress = (int)((downloadedBytes * 100) / contentLength);
                                    if (progress / 10 > lastProgress / 10)
                                    {
                                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                                        var speed = downloadedBytes / elapsed / 1024.0 / 1024.0;
                                        Console.WriteLine($"   进度: {progress}% ({downloadedBytes / 1024.0 / 1024.0:F2}/{contentLength / 1024.0 / 1024.0:F2} MB) - 速度: {speed:F2} MB/s");
                                        lastProgress = progress;
                                    }
                                }
                            }
                        }
                        
                        var totalTime = (DateTime.Now - startTime).TotalSeconds;
                        var avgSpeed = downloadedBytes / totalTime / 1024.0 / 1024.0;
                        Console.WriteLine($"✅ 预下载完成！");
                        Console.WriteLine($"   下载大小: {downloadedBytes / 1024.0 / 1024.0:F2} MB");
                        Console.WriteLine($"   耗时: {totalTime:F1} 秒");
                        Console.WriteLine($"   平均速度: {avgSpeed:F2} MB/s");
                        
                        // 检查文件头（验证是否是有效的 ZIP 文件）
                        Console.WriteLine($"🔍 检查文件头...");
                        byte[] headerBytes = new byte[200]; // 读取前 200 字节
                        int headerBytesRead = 0;
                        await using (var fs = new System.IO.FileStream(tempFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                        {
                            if (fs.Length < 4)
                            {
                                throw new Exception($"文件太小，不是有效的 ZIP 文件 (大小: {fs.Length} 字节)");
                            }
                            headerBytesRead = await fs.ReadAsync(headerBytes, 0, Math.Min(200, (int)fs.Length));
                        }
                        
                        var headerHex = BitConverter.ToString(headerBytes, 0, Math.Min(4, headerBytesRead)).Replace("-", " ");
                        Console.WriteLine($"   文件头 (十六进制): {headerHex}");
                        Console.WriteLine($"   文件大小: {new System.IO.FileInfo(tempFile).Length} 字节");
                        
                        // ZIP 文件应该以 PK (50 4B) 开头
                        if (headerBytes[0] == 0x50 && headerBytes[1] == 0x4B)
                        {
                            Console.WriteLine($"   ✅ 文件头正确 (PK signature)");
                        }
                        else
                        {
                            Console.WriteLine($"   ❌ 文件头不正确！这不是有效的 ZIP 文件");
                            
                            // 显示文件开头内容
                            var previewText = System.Text.Encoding.UTF8.GetString(headerBytes, 0, Math.Min(headerBytesRead, 200));
                            Console.WriteLine($"   文件开头内容预览 (前100字符):");
                            Console.WriteLine($"   {previewText.Substring(0, Math.Min(100, previewText.Length)).Replace("\r", "\\r").Replace("\n", "\\n")}");
                            
                            throw new Exception($"服务器返回的不是有效的 ZIP 文件。文件头: {headerHex}");
                        }
                        
                        // 计算 MD5
                        Console.WriteLine($"🔐 计算文件 MD5...");
                        string md5String;
                        using (var md5 = System.Security.Cryptography.MD5.Create())
                        using (var stream = System.IO.File.OpenRead(tempFile))
                        {
                            var hash = md5.ComputeHash(stream);
                            md5String = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                            Console.WriteLine($"✅ 计算的 MD5: {md5String}");
                        }
                        
                        // 验证 ZIP 文件完整性
                        Console.WriteLine($"🔍 验证 ZIP 文件完整性...");
                        try
                        {
                            using (var zipArchive = System.IO.Compression.ZipFile.OpenRead(tempFile))
                            {
                                Console.WriteLine($"✅ ZIP 文件有效，包含 {zipArchive.Entries.Count} 个文件");
                                // 列出前几个文件（调试用）
                                var firstFiles = zipArchive.Entries.Take(5).Select(e => e.FullName).ToList();
                                Console.WriteLine($"   文件列表（前5个）:");
                                foreach (var f in firstFiles)
                                {
                                    Console.WriteLine($"   - {f}");
                                }
                            }
                        }
                        catch (Exception zipEx)
                        {
                            Console.WriteLine($"❌ ZIP 文件验证失败: {zipEx.Message}");
                            Console.WriteLine($"   文件可能损坏或格式错误");
                            Console.WriteLine($"   文件路径: {tempFile}");
                            Console.WriteLine($"   文件大小: {new System.IO.FileInfo(tempFile).Length} 字节");
                            
                            // 保留文件以便检查
                            Console.WriteLine($"💾 损坏的文件已保留，请手动检查: {tempFile}");
                            throw new Exception($"下载的更新包无效: {zipEx.Message}");
                        }
                        
                        // 更新 UpdateInfo
                        updateInfo = new UpdateInfo
                        {
                            Version = updateInfo.Version,
                            DownloadUrl = updateInfo.DownloadUrl,
                            FileSize = updateInfo.FileSize,
                            FileMD5 = md5String,
                            IsForceUpdate = updateInfo.IsForceUpdate
                        };
                        
                        Console.WriteLine($"🔧 已使用实际 MD5 值");
                        
                        // 保存预下载的文件路径，后续直接使用，不再重复下载
                        preDownloadedFile = tempFile;
                        Console.WriteLine($"💾 预下载文件已保存: {tempFile}");
                        Console.WriteLine($"✅ 将直接使用此文件，不再重复下载");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ 预下载失败: {ex.Message}");
                        if (System.IO.File.Exists(tempFile))
                        {
                            try { System.IO.File.Delete(tempFile); } catch { }
                        }
                        throw;
                    }
                    }  // 关闭 else 块
                    
                    // 无论文件是否预先存在，都需要计算MD5（如果还没有计算）
                    if (string.IsNullOrEmpty(updateInfo.FileMD5))
                    {
                        // 计算 MD5
                        Console.WriteLine($"🔐 计算文件 MD5...");
                        string md5String;
                        using (var md5 = System.Security.Cryptography.MD5.Create())
                        using (var stream = System.IO.File.OpenRead(tempFile))
                        {
                            var hash = md5.ComputeHash(stream);
                            md5String = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                            Console.WriteLine($"✅ 计算的 MD5: {md5String}");
                        }
                        
                        // 更新 UpdateInfo
                        updateInfo = new UpdateInfo
                        {
                            Version = updateInfo.Version,
                            DownloadUrl = updateInfo.DownloadUrl,
                            FileSize = updateInfo.FileSize,
                            FileMD5 = md5String,
                            IsForceUpdate = updateInfo.IsForceUpdate
                        };
                        
                        Console.WriteLine($"🔧 已使用实际 MD5 值");
                        
                        // 保存预下载的文件路径，后续直接使用，不再重复下载
                        preDownloadedFile = tempFile;
                        Console.WriteLine($"💾 预下载文件已保存: {tempFile}");
                        Console.WriteLine($"✅ 将直接使用此文件，不再重复下载");
                    }
                }  // 关闭 if (string.IsNullOrEmpty(updateInfo.FileMD5))
                
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                // 显示更新对话框
                if (!silent || updateInfo.IsForceUpdate)
                {
                    bool shouldUpdate = false;
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var result = MessageBox.Show(
                            $"发现新版本 {updateInfo.Version}\n\n" +
                            $"当前版本：{_config.CurrentVersion}\n" +
                            $"文件大小：{updateInfo.FileSize / 1024.0 / 1024.0:F2} MB\n\n" +
                            $"是否立即更新？",
                            "发现新版本",
                            updateInfo.IsForceUpdate ? MessageBoxButton.OK : MessageBoxButton.YesNo,
                            MessageBoxImage.Information
                        );
                        shouldUpdate = (result == MessageBoxResult.Yes || result == MessageBoxResult.OK);
                    });
                    
                    if (!shouldUpdate)
                    {
                        Console.WriteLine($"⏭️  用户选择稍后更新");
                        return false;
                    }
                }
                
                // 下载并安装
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"⬇️  开始正式下载并安装更新...");
                Console.WriteLine($"   下载地址: {_serverUrl}{updateInfo.DownloadUrl}");
                Console.WriteLine($"   版本: {updateInfo.Version}");
                Console.WriteLine($"   文件大小: {updateInfo.FileSize / 1024.0 / 1024.0:F2} MB");
                Console.WriteLine($"   MD5: {updateInfo.FileMD5}");
                Console.WriteLine($"   HttpClient BaseAddress: {_serverUrl}");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                var downloadStartTime = DateTime.Now;
                
                // 获取当前应用程序的安装目录
                var appDirectory = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                ) ?? AppDomain.CurrentDomain.BaseDirectory;
                
                Console.WriteLine($"📥 使用外部更新程序（避免DLL锁定问题）");
                Console.WriteLine($"   应用程序目录: {appDirectory}");
                
                // 确定更新包路径
                var updatePackagePath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), 
                    $"BinanceApps_Update_{updateInfo.Version}.zip"
                );
                
                // 检查是否需要下载：
                // 1. 如果预下载了，使用预下载的文件
                // 2. 如果文件已存在，验证MD5，如果正确则直接使用
                // 3. 否则重新下载
                
                bool needDownload = true;
                
                // 检查预下载
                if (!string.IsNullOrEmpty(preDownloadedFile) && System.IO.File.Exists(preDownloadedFile))
                {
                    Console.WriteLine($"✅ 使用预下载的文件");
                    Console.WriteLine($"   源文件: {preDownloadedFile}");
                    Console.WriteLine($"   目标: {updatePackagePath}");
                    
                    // 如果路径不同，移动文件
                    if (!string.Equals(preDownloadedFile, updatePackagePath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (System.IO.File.Exists(updatePackagePath))
                        {
                            System.IO.File.Delete(updatePackagePath);
                        }
                        System.IO.File.Move(preDownloadedFile, updatePackagePath);
                        Console.WriteLine($"   文件已移动到最终位置");
                    }
                    needDownload = false;
                }
                // 检查文件是否已存在
                else if (System.IO.File.Exists(updatePackagePath))
                {
                    Console.WriteLine($"🔍 发现已存在的更新包: {updatePackagePath}");
                    Console.WriteLine($"   验证文件完整性...");
                    
                    try
                    {
                        // 验证文件MD5
                        string existingMd5;
                        using (var md5 = System.Security.Cryptography.MD5.Create())
                        using (var stream = System.IO.File.OpenRead(updatePackagePath))
                        {
                            var hash = md5.ComputeHash(stream);
                            existingMd5 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        }
                        
                        Console.WriteLine($"   文件MD5: {existingMd5}");
                        Console.WriteLine($"   服务器MD5: {updateInfo.FileMD5}");
                        
                        if (!string.IsNullOrEmpty(updateInfo.FileMD5) && 
                            existingMd5.Equals(updateInfo.FileMD5, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"✅ 文件完整，直接使用（避免重复下载）");
                            needDownload = false;
                        }
                        else
                        {
                            Console.WriteLine($"⚠️  文件MD5不匹配或服务器未提供MD5，重新下载");
                            System.IO.File.Delete(updatePackagePath);
                        }
                    }
                    catch (Exception verifyEx)
                    {
                        Console.WriteLine($"⚠️  验证失败: {verifyEx.Message}，重新下载");
                        try { System.IO.File.Delete(updatePackagePath); } catch { }
                    }
                }
                
                // 需要下载
                if (needDownload)
                {
                    Console.WriteLine($"📥 下载更新包...");
                    Console.WriteLine($"   目标: {updatePackagePath}");
                    
                    try
                    {
                        using (var httpClient = new System.Net.Http.HttpClient { BaseAddress = new Uri(_serverUrl), Timeout = TimeSpan.FromMinutes(10) })
                        {
                            var response = await httpClient.GetAsync(updateInfo.DownloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                            response.EnsureSuccessStatusCode();
                            
                            var contentLength = response.Content.Headers.ContentLength ?? updateInfo.FileSize;
                            long downloadedBytes = 0;
                            int lastProgress = -1;
                            var startTime = DateTime.Now;
                            
                            await using (var contentStream = await response.Content.ReadAsStreamAsync())
                            await using (var fs = new System.IO.FileStream(updatePackagePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 8192, true))
                            {
                                var buffer = new byte[8192];
                                int bytesRead;
                                
                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fs.WriteAsync(buffer, 0, bytesRead);
                                    downloadedBytes += bytesRead;
                                    
                                    // 显示进度
                                    int progress = (int)((downloadedBytes * 100) / contentLength);
                                    if (progress / 10 > lastProgress / 10)
                                    {
                                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                                        var speed = downloadedBytes / elapsed / 1024.0 / 1024.0;
                                        Console.WriteLine($"   进度: {progress}% - 速度: {speed:F2} MB/s");
                                        lastProgress = progress;
                                    }
                                }
                            }
                            
                            Console.WriteLine($"✅ 下载完成: {downloadedBytes / 1024.0 / 1024.0:F2} MB");
                        }
                    }
                    catch (Exception downloadEx)
                    {
                        Console.WriteLine($"❌ 下载失败: {downloadEx.Message}");
                        return false;
                    }
                }
                
                var downloadEndTime = DateTime.Now;
                var downloadDuration = (downloadEndTime - downloadStartTime).TotalSeconds;
                
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"📋 下载完成");
                Console.WriteLine($"   总耗时: {downloadDuration:F1} 秒");
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                // 使用外部更新程序
                var installResult = true;
                
                if (installResult)
                {
                    Console.WriteLine($"✅ 更新包已下载");
                    Console.WriteLine($"   准备启动外部更新程序...");
                    
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        // 提示即将更新
                        var restart = MessageBox.Show(
                            $"更新包已下载完成！\n\n" +
                            $"新版本：{updateInfo.Version}\n" +
                            $"当前版本：{_config.CurrentVersion}\n\n" +
                            $"点击\"确定\"将关闭应用程序并自动完成更新，\n" +
                            $"更新完成后将自动重启。\n\n" +
                            $"点击\"取消\"稍后手动更新。",
                            "准备更新",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Information
                        );
                        
                        if (restart == MessageBoxResult.OK)
                        {
                            Console.WriteLine($"🔄 启动外部更新程序...");
                            
                            try
                            {
                                // 获取当前应用程序的路径
                                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                                    ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                                    
                                Console.WriteLine($"📍 应用程序路径: {exePath}");
                                
                                // 保存新版本号到 App.config（在应用退出前保存）
                                try
                                {
                                    App.SaveCurrentVersion(updateInfo.Version);
                                    Console.WriteLine($"💾 已保存新版本号: {updateInfo.Version}");
                                }
                                catch (Exception saveEx)
                                {
                                    Console.WriteLine($"⚠️  保存版本号失败: {saveEx.Message}");
                                }
                                
                                // 保存新版本号到 App.config（在应用退出前保存）
                                try
                                {
                                    App.SaveCurrentVersion(updateInfo.Version);
                                    Console.WriteLine($"💾 已保存新版本号: {updateInfo.Version}");
                                }
                                catch (Exception saveEx)
                                {
                                    Console.WriteLine($"⚠️  保存版本号失败: {saveEx.Message}");
                                }
                                
                                // 使用 UpdateHelper 启动更新脚本
                                UpdateHelper.StartUpdate(updatePackagePath, appDirectory, exePath, updateInfo.Version);
                                Console.WriteLine($"✅ 更新程序已启动");
                                
                                // 延迟关闭应用程序，确保更新脚本启动
                                await Task.Delay(1000);
                                Application.Current.Shutdown();
                            }
                            catch (Exception restartEx)
                            {
                                Console.WriteLine($"❌ 重启失败: {restartEx.Message}");
                                MessageBox.Show(
                                    $"自动重启失败，请手动重启应用程序。\n\n错误: {restartEx.Message}",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning
                                );
                            }
                        }
                        else
                        {
                            Console.WriteLine($"ℹ️  用户选择稍后手动重启");
                            MessageBox.Show(
                                "更新已下载，请手动重启应用程序以应用更新。",
                                "提示",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        }
                    });
                    
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ 更新安装失败");
                    Console.WriteLine($"   可能的原因:");
                    Console.WriteLine($"   1. 下载的文件损坏或不完整");
                    Console.WriteLine($"   2. MD5 校验失败（服务器返回的 MD5 为空：'{updateInfo.FileMD5}'）");
                    Console.WriteLine($"   3. ZIP 文件格式错误");
                    Console.WriteLine($"   4. 文件权限问题");
                    Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    
                    MessageBox.Show(
                        "更新安装失败！\n\n" +
                        "可能的原因：\n" +
                        "1. 服务器上的更新包文件损坏\n" +
                        "2. MD5 校验失败\n" +
                        "3. ZIP 文件格式错误\n\n" +
                        "请检查服务器上传的更新包是否正确。\n" +
                        "详细信息请查看输出窗口。", 
                        "更新失败", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"❌ 更新过程异常:");
                Console.WriteLine($"   类型: {ex.GetType().Name}");
                Console.WriteLine($"   消息: {ex.Message}");
                
                if (ex.StackTrace != null)
                {
                    Console.WriteLine($"   堆栈: {ex.StackTrace}");
                }
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
                    if (ex.InnerException.StackTrace != null)
                    {
                        Console.WriteLine($"   内部堆栈: {ex.InnerException.StackTrace}");
                    }
                }
                Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                if (!silent)
                {
                    MessageBox.Show($"更新失败：{ex.Message}\n\n详细信息请查看输出窗口", 
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                return false;
            }
        }
    }
} 