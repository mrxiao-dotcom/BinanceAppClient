using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace BinanceApps.WPF
{
    public partial class LogWindow : Window
    {
        private int _logCount = 0;
        
        public LogWindow()
        {
            InitializeComponent();
            
            // 注册窗口关闭事件，确保资源正确释放
            this.Closing += LogWindow_Closing;
        }

        /// <summary>
        /// 添加日志条目
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="logType">日志类型</param>
        public void AddLog(string message, LogType logType = LogType.Info)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var typePrefix = GetLogTypePrefix(logType);
                var logEntry = $"[{timestamp}] {typePrefix} {message}";
                
                // 在UI线程中更新日志
                Dispatcher.Invoke(() =>
                {
                    txtLogContent.AppendText(logEntry + Environment.NewLine);
                    txtLogContent.ScrollToEnd();
                    
                    _logCount++;
                    txtLogCount.Text = $"日志条数: {_logCount}";
                    txtLastUpdate.Text = $"最后更新: {timestamp}";
                    
                    // 根据日志类型更新状态
                    switch (logType)
                    {
                        case LogType.Error:
                            txtStatus.Text = "错误";
                            break;
                        case LogType.Warning:
                            txtStatus.Text = "警告";
                            break;
                        case LogType.Success:
                            txtStatus.Text = "成功";
                            break;
                        default:
                            txtStatus.Text = "就绪";
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                // 如果日志记录失败，至少尝试在控制台输出
                System.Diagnostics.Debug.WriteLine($"日志记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取日志类型前缀
        /// </summary>
        private string GetLogTypePrefix(LogType logType)
        {
            return logType switch
            {
                LogType.Info => "[INFO]",
                LogType.Success => "[SUCCESS]",
                LogType.Warning => "[WARNING]",
                LogType.Error => "[ERROR]",
                LogType.API => "[API]",
                LogType.Debug => "[DEBUG]",
                _ => "[INFO]"
            };
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLogContent.Clear();
            _logCount = 0;
            txtLogCount.Text = "日志条数: 0";
            txtLastUpdate.Text = "最后更新: -";
            txtStatus.Text = "就绪";
        }

        /// <summary>
        /// 复制日志到剪贴板
        /// </summary>
        private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtLogContent.Text);
                MessageBox.Show("日志已复制到剪贴板", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存日志到文件
        /// </summary>
        private void BtnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    DefaultExt = "log",
                    FileName = $"币安API日志_{DateTime.Now:yyyyMMdd_HHmmss}.log"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, txtLogContent.Text, Encoding.UTF8);
                    MessageBox.Show($"日志已保存到: {saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清空所有日志
        /// </summary>
        public void ClearAllLogs()
        {
            Dispatcher.Invoke(() =>
            {
                txtLogContent.Clear();
                _logCount = 0;
                txtLogCount.Text = "日志条数: 0";
                txtLastUpdate.Text = "最后更新: -";
                txtStatus.Text = "就绪";
            });
        }

        /// <summary>
        /// 日志窗口关闭事件处理
        /// </summary>
        private void LogWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 清空日志内容，释放内存
                txtLogContent.Clear();
                _logCount = 0;
                
                // 不调用Dispatcher.InvokeShutdown()，让应用程序自己管理Dispatcher的生命周期
                System.Diagnostics.Debug.WriteLine("日志窗口正在关闭，资源已清理");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"日志窗口关闭时出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 日志类型枚举
    /// </summary>
    public enum LogType
    {
        Info,
        Success,
        Warning,
        Error,
        API,
        Debug
    }
} 