using RinKit;
using RinKitWPF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ZenergyBFSI.Model;
using ZenergyBFSI.View.StateCards;
using static DevExpress.Xpo.Logger.LogManager;
using Path = System.IO.Path;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// UC_Operation.xaml 的交互逻辑
    /// 该控件主要处理系统相关设备功能的的调试，存在PLC信号交互、WEB信号交互
    /// </summary>
    public partial class UC_Operation : UserControl
    {

        #region webview2资源
        // ── 路径常量 ──────────────────────────────────────────────
        private static readonly string AppDir =
           Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string WwwRoot = Path.Combine(AppDir, "wwwroot");
        private static readonly string DataDir = Path.Combine(AppDir, "Data");
        private static readonly string ImgDir = Path.Combine(AppDir, "Images");

        // 虚拟主机名（离线核心）
        private const string VHost = "visionapp.local";   // HTML/JS/CSS/字体/Chart.js
        private const string ImgVHost = "localimg";           // 检测图片

        private const string DashUrl = "https://visionapp.local/dashboard.html";
        private const string InspUrl = "https://visionapp.local/inspection.html";

        // ── 服务 & 页面控制器 ──────────────────────────────────────
        private CsvDataService _csv = null;
        private WebViewBridge _bridge = null;
        private UC_StatesCards _dashCtrl = null;
        private UC_StatesCards _inspCtrl = null;
        private string _curPage = "";
        private DispatcherTimer _clock = null;
        #endregion

        private static UC_Operation _instance;
        private static object _syncRoot = new object();
        private static VM_Operation _vm = new VM_Operation();
        private FileSystemWatcher csvWatcher;
        private string csvPath= System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inspection_data.csv");
        private DateTime lastWrite = DateTime.MinValue;

        /// <summary>
        /// 设备调试静态实例
        /// </summary>
        public static UC_Operation I
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new UC_Operation();
                        }
                    }
                }
                return _instance;
            }
        }
        public UC_Operation()
        {
            InitializeComponent();
        }
 





        public void Alert(string msg)
        {
            if (!string.IsNullOrEmpty(msg) && string.IsNullOrEmpty(_vm.Text4))
            {
                _vm.Text4 = msg;
                Dispatcher.Invoke(() =>
                {
                    new WD_Alert(msg).Show();
                });
            }
            else
            {
                _vm.Text4 = "";
            }
        }

        /// <summary>
        /// 输出日志到界面
        /// </summary>
        /// <param name="content">内容</param>
        /// <param name="type">类别，颜色区分</param>
        bool _inited = false;
        int _logMax = 1000;
        List<WinLog> _cache = new List<WinLog>();
        public void InitLogger(int interval = 500, int max = 1000)
        {
            if (_inited) return;
            _inited = true;
            _logMax = max;
        }

        public void WriteLog(string content, string type = "")
        {
            if (string.IsNullOrEmpty(content)) return;
            switch (type)
            {
                case "Error":
                    Rlog.Error(content);
                    if (Settings.显示日志级别 <= 5)
                        _cache.Add(new WinLog(type, "red", content));
                    break;
                case "Warn":
                    Rlog.Warn(content);
                    if (Settings.显示日志级别 <= 4)
                        _cache.Add(new WinLog(type, "orange", content));
                    break;
                case "Success":
                    Rlog.Info(content);
                    if (Settings.显示日志级别 <= 3)
                        _cache.Add(new WinLog(type, "green", content));
                    break;
                case "Info":
                    Rlog.Info(content);
                    if (Settings.显示日志级别 <= 2)
                        _cache.Add(new WinLog(type, "blue", content));
                    break;
                case "Debug":
                    Rlog.Debug(content);
                    if (Settings.显示日志级别 <= 1)
                        _cache.Add(new WinLog(type, "black", content));
                    break;
                default:
                    Rlog.Trace(content);
                    if (Settings.显示日志级别 <= 0)
                        _cache.Add(new WinLog(type, "black", content));
                    break;
            }
        }

        /// <summary>
        /// 调试页面的加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {

        }
        // 内部消息模型
        private class WebMsg
        {
            public string Type { get; set; } = "";
            public string Payload { get; set; }
        }

    }
    /// <summary>
    /// 调试页面视图模型
    /// 针对操作页面渲染数据整理的数据结构实体
    /// </summary>
    public class VM_Operation : ViewModelBase
    {
        private string code;
        public string Code { get { return code; } set { code = value; OnPropertyChanged("Code"); } }

        private string text1;
        public string Text1 { get { return text1; } set { text1 = value; OnPropertyChanged("Text1"); } }

        private string text2;
        public string Text2 { get { return text2; } set { text2 = value; OnPropertyChanged("Text2"); } }

        private string text3;
        public string Text3 { get { return text3; } set { text3 = value; OnPropertyChanged("Text3"); } }

        private string text4;
        public string Text4 { get { return text4; } set { text4 = value; OnPropertyChanged("Text4"); } }
        public List<WinLog> ListLog { get { return _listLog; } set { _listLog = value; OnPropertyChanged("ListLog"); } }
        private List<WinLog> _listLog = new List<WinLog>();
    }

    /// <summary>
    /// 运行日志类，为设备调试提供日志类实体
    /// </summary>
    public class WinLog
    {
        public string Time { get; set; } = DateTime.Now.ToString();
        public string Type { get; set; }
        public string Content { get; set; }
        public string Foreground { get; set; }

        public WinLog()
        {
        }

        public WinLog(string type, string foreground, string content)
        {
            Type = type;
            Foreground = foreground;
            Content = content;
        }
    }


}
