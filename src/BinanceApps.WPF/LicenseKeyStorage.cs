using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 注册码存储管理器 - 将注册码保存到 AppData 目录
    /// 这样更新程序时不会影响注册码
    /// </summary>
    public static class LicenseKeyStorage
    {
        private static readonly string AppDataPath;
        private static readonly string LicenseFilePath;
        private static readonly string ExpiryFilePath;
        private static readonly string LicenseInfoFilePath;
        
        static LicenseKeyStorage()
        {
            // 使用 LocalApplicationData 目录
            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BinanceApps"
            );
            
            // 确保目录存在
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
            
            LicenseFilePath = Path.Combine(AppDataPath, "license.dat");
            ExpiryFilePath = Path.Combine(AppDataPath, "expiry.dat");
            LicenseInfoFilePath = Path.Combine(AppDataPath, "license_info.json");
        }
        
        /// <summary>
        /// 保存注册码
        /// </summary>
        public static void SaveLicenseKey(string licenseKey)
        {
            try
            {
                if (string.IsNullOrEmpty(licenseKey))
                {
                    Console.WriteLine("⚠️  警告：尝试保存空的注册码");
                    return;
                }
                
                // 简单加密（防止直接查看）
                var encryptedKey = EncryptString(licenseKey);
                
                File.WriteAllText(LicenseFilePath, encryptedKey);
                Console.WriteLine($"💾 注册码已保存到: {LicenseFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存注册码失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 读取注册码
        /// </summary>
        public static string? GetLicenseKey()
        {
            try
            {
                if (!File.Exists(LicenseFilePath))
                {
                    Console.WriteLine($"ℹ️  注册码文件不存在: {LicenseFilePath}");
                    return null;
                }
                
                var encryptedKey = File.ReadAllText(LicenseFilePath);
                if (string.IsNullOrEmpty(encryptedKey))
                {
                    return null;
                }
                
                // 解密
                var licenseKey = DecryptString(encryptedKey);
                Console.WriteLine($"✅ 从 AppData 读取到注册码: {licenseKey}");
                return licenseKey;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 读取注册码失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 删除注册码
        /// </summary>
        public static void DeleteLicenseKey()
        {
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    File.Delete(LicenseFilePath);
                    Console.WriteLine($"🗑️  注册码已删除: {LicenseFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 删除注册码失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 检查是否存在注册码
        /// </summary>
        public static bool HasLicenseKey()
        {
            return File.Exists(LicenseFilePath);
        }
        
        /// <summary>
        /// 获取存储路径（用于调试）
        /// </summary>
        public static string GetStoragePath()
        {
            return LicenseFilePath;
        }
        
        /// <summary>
        /// 保存许可证信息（到期时间、许可证类型等）
        /// </summary>
        public static void SaveLicenseInfo(DateTime? expiresAt, string? licenseType)
        {
            try
            {
                var info = new
                {
                    ExpiresAt = expiresAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                    LicenseType = licenseType,
                    SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(info, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(LicenseInfoFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存许可证信息失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 读取缓存的许可证信息
        /// </summary>
        public static (DateTime? ExpiresAt, string? LicenseType) GetLicenseInfo()
        {
            try
            {
                if (!File.Exists(LicenseInfoFilePath))
                {
                    return (null, null);
                }
                
                var json = File.ReadAllText(LicenseInfoFilePath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                DateTime? expiresAt = null;
                string? licenseType = null;
                
                if (root.TryGetProperty("ExpiresAt", out var expiresAtProp) && 
                    expiresAtProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var expiresAtStr = expiresAtProp.GetString();
                    if (!string.IsNullOrEmpty(expiresAtStr) && DateTime.TryParse(expiresAtStr, out var parsedDate))
                    {
                        expiresAt = parsedDate;
                    }
                }
                
                if (root.TryGetProperty("LicenseType", out var licenseTypeProp) && 
                    licenseTypeProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    licenseType = licenseTypeProp.GetString();
                }
                
                return (expiresAt, licenseType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 读取许可证信息缓存失败: {ex.Message}");
                return (null, null);
            }
        }
        
        /// <summary>
        /// 手动设置到期时间（用于服务器无法返回时）
        /// </summary>
        public static void SetExpiryManually(DateTime expiresAt, string licenseType = "年度许可")
        {
            SaveLicenseInfo(expiresAt, licenseType);
        }
        
        // ==================== 加密/解密方法 ====================
        
        /// <summary>
        /// 简单加密（使用 Base64 + 固定密钥）
        /// 注意：这不是高安全性加密，只是防止直接查看
        /// </summary>
        private static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;
            
            // 使用简单的 XOR + Base64
            var key = GetMachineSpecificKey();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = new byte[plainBytes.Length];
            
            for (int i = 0; i < plainBytes.Length; i++)
            {
                encryptedBytes[i] = (byte)(plainBytes[i] ^ key[i % key.Length]);
            }
            
            return Convert.ToBase64String(encryptedBytes);
        }
        
        /// <summary>
        /// 简单解密
        /// </summary>
        private static string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;
            
            try
            {
                var key = GetMachineSpecificKey();
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var decryptedBytes = new byte[encryptedBytes.Length];
                
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    decryptedBytes[i] = (byte)(encryptedBytes[i] ^ key[i % key.Length]);
                }
                
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// 获取机器相关的加密密钥
        /// </summary>
        private static byte[] GetMachineSpecificKey()
        {
            // 使用机器名和用户名作为密钥的一部分
            var keySource = $"{Environment.MachineName}_{Environment.UserName}_BinanceApps2024";
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(keySource));
        }
    }
} 