using DevExpress.Mvvm;
using RinKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// UC_Monitor.xaml 的交互逻辑
    /// 主要是与PLC地址的排查页面
    /// </summary>
    public partial class UC_Monitor : UserControl
    {
        MonitorVM _vm = new MonitorVM();
        Timer _timer = new Timer(100);
        bool _flag = false;
        bool _busy = false;
        public UC_Monitor()
        {
            InitializeComponent();
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();
            DataContext = _vm;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_flag)
            {
                if (_busy) return;
                _busy = true;
                try
                {
                    #region PLC信号监视循环线程的执行代码参考
                    //List<PlcInfo> list = new List<PlcInfo>();
                    //foreach (var obj in PlcHandler.I.GetOBJ())
                    //{
                    //    if (!string.IsNullOrEmpty(_vm.Contain) && !obj.Name.Contains(_vm.Contain)) continue;
                    //    if (!string.IsNullOrEmpty(_vm.NotContain) && obj.Name.Contains(_vm.NotContain)) continue;
                    //    switch (obj.Mode)
                    //    {
                    //        case 1:
                    //        case 2:
                    //            {
                    //                switch (obj.Type)
                    //                {
                    //                    case "String":
                    //                        list.Add(new PlcInfo(obj.Name, obj.Adress, $"{obj.vString}"));
                    //                        break;
                    //                    case "UInt16":
                    //                        list.Add(new PlcInfo(obj.Name, obj.Adress, $"{obj.vInt}"));
                    //                        break;
                    //                }
                    //            }
                    //            break;
                    //        case 4:
                    //        case 5:
                    //            {
                    //                string type = obj.Type.Split('|')[2];
                    //                switch (type)
                    //                {
                    //                    case "Bit":
                    //                        list.Add(new PlcInfo(obj.Name, obj.Adress, $"{obj.vBool}"));
                    //                        break;
                    //                    case "Real":
                    //                        list.Add(new PlcInfo(obj.Name, obj.Adress, $"{obj.vFloat}"));
                    //                        break;
                    //                    case "UInt16":
                    //                        list.Add(new PlcInfo(obj.Name, obj.Adress, $"{obj.vInt}"));
                    //                        break;
                    //                }
                    //            }
                    //            break;
                    //    }
                    //}
                    //_vm.PlcDataList = list;
                    #endregion
                }
                catch (Exception ex)
                {
                    Rlog.Error($"Monitor异常！{ex.Message}\r\n {ex.StackTrace}");
                }
                finally
                {
                    _busy = false;
                }

            }
        }


        private void Start_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            _flag = true;
            start.IsVisible = false;
            stop.IsVisible = true;
        }
        private void Stop_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            _flag = false;
            start.IsVisible = true;
            stop.IsVisible = false;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
        }
        private void Export_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            try
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.Filter = "CSV|*.csv";
                if (dlg.ShowDialog() != true)
                    return;
                gridView.ExportToCsv(dlg.FileName);
                MessageBox.Show("导出成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("系统错误！");
                Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void Print_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            FrameworkElement fe = new FrameworkElement();
            gridView.ShowPrintPreview(fe);
        }

        private void Logout_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            Main.Logout();
        }
    }

    internal class MonitorVM : ViewModelBase
    {
        public string Contain
        {
            get { return GetValue<string>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("Contain");
                }
            }
        }
        public string NotContain
        {
            get { return GetValue<string>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("NotContain");
                }
            }
        }
        public List<PlcInfo> PlcDataList
        {
            get { return GetValue<List<PlcInfo>>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("PlcDataList");
                }
            }
        }
    }
    internal class PlcInfo
    {
        public string Name { get; set; }
        public string Adress { get; set; }
        public string Value { get; set; }

        public PlcInfo(string name, string adress, string value)
        {
            Name = name;
            Adress = adress;
            Value = value;
        }
    }
}
