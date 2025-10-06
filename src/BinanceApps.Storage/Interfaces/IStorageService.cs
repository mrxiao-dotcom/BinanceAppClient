namespace BinanceApps.Storage.Interfaces
{
    /// <summary>
    /// 存储服务接口
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// 基础存储路径
        /// </summary>
        string BasePath { get; }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        /// <param name="path">路径</param>
        void EnsureDirectoryExists(string path);

        /// <summary>
        /// 文件是否存在
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否存在</returns>
        bool FileExists(string filePath);

        /// <summary>
        /// 目录是否存在
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>是否存在</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// 获取文件大小
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件大小（字节）</returns>
        long GetFileSize(string filePath);

        /// <summary>
        /// 获取文件最后修改时间
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>最后修改时间</returns>
        DateTime GetFileLastModified(string filePath);

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功</returns>
        bool DeleteFile(string filePath);

        /// <summary>
        /// 删除目录
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="recursive">是否递归删除</param>
        /// <returns>是否成功</returns>
        bool DeleteDirectory(string path, bool recursive = true);

        /// <summary>
        /// 复制文件
        /// </summary>
        /// <param name="sourcePath">源路径</param>
        /// <param name="targetPath">目标路径</param>
        /// <param name="overwrite">是否覆盖</param>
        /// <returns>是否成功</returns>
        bool CopyFile(string sourcePath, string targetPath, bool overwrite = false);

        /// <summary>
        /// 移动文件
        /// </summary>
        /// <param name="sourcePath">源路径</param>
        /// <param name="targetPath">目标路径</param>
        /// <returns>是否成功</returns>
        bool MoveFile(string sourcePath, string targetPath);

        /// <summary>
        /// 获取目录中的所有文件
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="searchPattern">搜索模式</param>
        /// <param name="recursive">是否递归</param>
        /// <returns>文件路径列表</returns>
        string[] GetFiles(string path, string searchPattern = "*", bool recursive = false);

        /// <summary>
        /// 获取目录中的所有子目录
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="searchPattern">搜索模式</param>
        /// <param name="recursive">是否递归</param>
        /// <returns>目录路径列表</returns>
        string[] GetDirectories(string path, string searchPattern = "*", bool recursive = false);
    }

    /// <summary>
    /// 文件存储服务接口
    /// </summary>
    public interface IFileStorageService : IStorageService
    {
        /// <summary>
        /// 读取文本文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="encoding">编码</param>
        /// <returns>文件内容</returns>
        Task<string> ReadTextAsync(string filePath, System.Text.Encoding? encoding = null);

        /// <summary>
        /// 写入文本文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">内容</param>
        /// <param name="encoding">编码</param>
        /// <param name="append">是否追加</param>
        /// <returns>是否成功</returns>
        Task<bool> WriteTextAsync(string filePath, string content, System.Text.Encoding? encoding = null, bool append = false);

        /// <summary>
        /// 读取二进制文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件内容</returns>
        Task<byte[]> ReadBytesAsync(string filePath);

        /// <summary>
        /// 写入二进制文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="data">数据</param>
        /// <returns>是否成功</returns>
        Task<bool> WriteBytesAsync(string filePath, byte[] data);

        /// <summary>
        /// 读取JSON文件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <returns>反序列化后的对象</returns>
        Task<T?> ReadJsonAsync<T>(string filePath);

        /// <summary>
        /// 写入JSON文件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <param name="obj">要序列化的对象</param>
        /// <param name="indented">是否格式化</param>
        /// <returns>是否成功</returns>
        Task<bool> WriteJsonAsync<T>(string filePath, T obj, bool indented = true);

        /// <summary>
        /// 读取CSV文件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <returns>反序列化后的对象列表</returns>
        Task<List<T>> ReadCsvAsync<T>(string filePath) where T : class, new();

        /// <summary>
        /// 写入CSV文件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <param name="data">要序列化的对象列表</param>
        /// <returns>是否成功</returns>
        Task<bool> WriteCsvAsync<T>(string filePath, List<T> data) where T : class;
    }

    /// <summary>
    /// 日志存储服务接口
    /// </summary>
    public interface ILogStorageService : IFileStorageService
    {
        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        /// <param name="exception">异常信息</param>
        /// <returns>是否成功</returns>
        Task<bool> WriteLogAsync(string level, string message, string category = "General", Exception? exception = null);

        /// <summary>
        /// 写入信息日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        /// <returns>是否成功</returns>
        Task<bool> WriteInfoAsync(string message, string category = "General");

        /// <summary>
        /// 写入警告日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        /// <returns>是否成功</returns>
        Task<bool> WriteWarningAsync(string message, string category = "General");

        /// <summary>
        /// 写入错误日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        /// <param name="exception">异常信息</param>
        /// <returns>是否成功</returns>
        Task<bool> WriteErrorAsync(string message, string category = "General", Exception? exception = null);

        /// <summary>
        /// 清理旧日志
        /// </summary>
        /// <param name="daysToKeep">保留天数</param>
        /// <returns>清理的文件数量</returns>
        Task<int> CleanupOldLogsAsync(int daysToKeep = 30);

        /// <summary>
        /// 获取日志文件列表
        /// </summary>
        /// <param name="category">日志分类</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>日志文件列表</returns>
        Task<List<string>> GetLogFilesAsync(string? category = null, DateTime? startDate = null, DateTime? endDate = null);
    }

    /// <summary>
    /// 配置存储服务接口
    /// </summary>
    public interface IConfigurationStorageService : IFileStorageService
    {
        /// <summary>
        /// 读取配置
        /// </summary>
        /// <typeparam name="T">配置类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值</returns>
        Task<T?> GetConfigurationAsync<T>(string key, T? defaultValue = default);

        /// <summary>
        /// 写入配置
        /// </summary>
        /// <typeparam name="T">配置类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        /// <returns>是否成功</returns>
        Task<bool> SetConfigurationAsync<T>(string key, T value);

        /// <summary>
        /// 删除配置
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>是否成功</returns>
        Task<bool> RemoveConfigurationAsync(string key);

        /// <summary>
        /// 配置是否存在
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>是否存在</returns>
        Task<bool> ConfigurationExistsAsync(string key);

        /// <summary>
        /// 获取所有配置键
        /// </summary>
        /// <returns>配置键列表</returns>
        Task<List<string>> GetAllConfigurationKeysAsync();

        /// <summary>
        /// 备份配置
        /// </summary>
        /// <param name="backupPath">备份路径</param>
        /// <returns>是否成功</returns>
        Task<bool> BackupConfigurationAsync(string backupPath);

        /// <summary>
        /// 恢复配置
        /// </summary>
        /// <param name="backupPath">备份路径</param>
        /// <returns>是否成功</returns>
        Task<bool> RestoreConfigurationAsync(string backupPath);
    }
} 