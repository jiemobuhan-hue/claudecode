
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
}
