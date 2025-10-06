using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace BinanceApps.WPF
{
    public class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        [STAThread]
        public static void Main()
        {
            // 分配控制台窗口
            AllocConsole();
            Console.WriteLine("=== BinanceApps 启动 ===");
            Console.WriteLine("控制台已启用，可以查看详细输出");

            try
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 应用程序启动失败: {ex.Message}");
                Console.WriteLine($"详细错误: {ex}");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
            finally
            {
                // 释放控制台窗口
                FreeConsole();
            }
        }
    }
} 