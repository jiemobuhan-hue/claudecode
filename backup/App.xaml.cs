
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

using NLog;
using RinKit;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;
using ZenergyBFSI.Model;
using ZenergyBFSI.Service;

namespace ZenergyBFSI
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //这里防止APP多开，当然也可以通过互斥体来实现，这里就不赘述了
            var AppName = "ZenergyBFSI";

            SystemSleepHelper.PreventSleepAndDisplayOff();
            if (Process.GetProcessesByName(AppName).ToList().Count > 1)
            {
                MessageBox.Show("程序正在运行...");
                Process.GetCurrentProcess().Kill();
            }
            //日志、通讯、设置等功能的初始化，后续可以继续初始化更多类似模块，这里是入口
            Rlog.Init("Debug", "C:\\Log\\");
            Rdb.Init(200);
            //Settings.New(); 
            // 等价于 AddSkiaSharp() + AddDefaultMappers() + AddDefaultTheme()


            CsvHelper.Init("C:\\Data\\");

            //授权等防呆操作，后续可以继续添加更多类似操作，这里是入口
            if (HslCommunication.Authorization.SetAuthorizationCode("80c457a7-4bbf-46bd-b371-616d0daff11a"))
            {
                Rlog.Info("HSL Actived");
            }
            else
            {
                Rlog.Info("HSL Active Failed!!!");
            }

            //// 构建 DbContextOptions（每次 factory 被调用时共享同一个 options 对象）
            //var options = new DbContextOptionsBuilder<AppDbContext>()
            //    .UseSqlite($"Data Source=Local.db")
            //    .Options;

            //// 注册工厂：每次调用 factory 时 new 一个新的 DbContext 实例
            //SQLiteGenericHelper.Initialize(() => new AppDbContext(options));


            //// 在 App.xaml.cs 的 OnStartup 中
            //var dbPath = Path.Combine(
            //    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            //    "MyApp", "local.db"
            //);

            //SQLiteGenericHelper.Initialize(
            //    dbPath,
            //    // 这个工厂会接收一个 DbContextOptions，用它构造你的 AppDbContext
            //    options => new AppDbContext((DbContextOptions<AppDbContext>)options)
            //);
            //try
            //{
            //    if (Settings.自启动 > 0)
            //    {
            //        RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            //        registryKey.SetValue(AppName, $"{AppDomain.CurrentDomain.BaseDirectory}{AppName}.exe");
            //        Rlog.Info($"启动项已添加:{AppName}");
            //    }
            //    else if (Settings.自启动 < 0)
            //    {
            //        Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true).DeleteValue(AppName);
            //        Rlog.Warn($"启动项已删除:{AppName}");
            //    }
            //}
            //catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

    }

    public static class SystemSleepHelper
    {
        // 1. 定义 API 需要的标志位枚举
        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_SYSTEM_REQUIRED = 0x00000001,  // 阻止系统进入睡眠
            ES_DISPLAY_REQUIRED = 0x00000002, // 阻止屏幕关闭
            ES_CONTINUOUS = 0x80000000        // 持续有效，直到下一次调用
        }

        // 2. 导入 Windows API 函数
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        /// <summary>
        /// 阻止系统睡眠和屏幕关闭
        /// </summary>
        public static void PreventSleepAndDisplayOff()
        {
            // 组合所需标志，通常需要 ES_CONTINUOUS 来使设置持续生效
            var flags = EXECUTION_STATE.ES_CONTINUOUS
                        | EXECUTION_STATE.ES_SYSTEM_REQUIRED
                        | EXECUTION_STATE.ES_DISPLAY_REQUIRED;
            SetThreadExecutionState(flags);

            // 可选：记录日志，确认已调用
            // Console.WriteLine("已请求阻止系统睡眠和关闭显示器。");
        }

        /// <summary>
        /// 恢复系统默认的睡眠和屏幕关闭行为
        /// </summary>
        public static void ResumeSystemSleeping()
        {
            // 只传入 ES_CONTINUOUS 来清除先前设置的其他标志
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);

            // 可选：记录日志，确认已恢复
            // Console.WriteLine("已恢复系统默认的节能策略。");
        }
    }
}
