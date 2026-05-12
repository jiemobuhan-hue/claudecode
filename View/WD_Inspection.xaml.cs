using DevExpress.Mvvm;
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
using System.Windows.Shapes;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// WD_Inspection.xaml 的交互逻辑
    /// </summary>
    public partial class WD_Inspection : Window
    {
        Timer _timer = new Timer(1000);
        InspectionVM _vm = new InspectionVM();
        public WD_Inspection()
        {
            InitializeComponent();
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();
            DataContext = _vm;
        }
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (string.IsNullOrEmpty(_vm.点检时间))
            {
                _vm.Text = "开班点检未完成，请在触摸屏完成[前后称点检、真空点检]";
            }
            else
            {
                _vm.Text = "开班点检已完成";
            }
            //_vm.前称重重量1 = AutoRun.I.Inspection.前称重点检1;
            //_vm.前称重重量2 = AutoRun.I.Inspection.前称重点检2;
            //_vm.前称重重量3 = AutoRun.I.Inspection.前称重点检3;
            //_vm.前称重重量4 = AutoRun.I.Inspection.前称重点检4;
            //_vm.后称重重量1 = AutoRun.I.Inspection.后称重点检1;
            //_vm.后称重重量2 = AutoRun.I.Inspection.后称重点检2;
            //_vm.后称重重量3 = AutoRun.I.Inspection.后称重点检3;
            //_vm.后称重重量4 = AutoRun.I.Inspection.后称重点检4;
            //_vm.工位1真空变化值1 = AutoRun.I.Inspection.工位1真空变化值1;
            //_vm.工位1真空变化值2 = AutoRun.I.Inspection.工位1真空变化值2;
            //_vm.工位1真空变化值3 = AutoRun.I.Inspection.工位1真空变化值3;
            //_vm.工位1真空变化值4 = AutoRun.I.Inspection.工位1真空变化值4;
            //_vm.工位1真空变化值5 = AutoRun.I.Inspection.工位1真空变化值5;
            //_vm.工位1真空变化值6 = AutoRun.I.Inspection.工位1真空变化值6;
            //_vm.工位1真空变化值7 = AutoRun.I.Inspection.工位1真空变化值7;
            //_vm.工位1真空变化值8 = AutoRun.I.Inspection.工位1真空变化值8;
            //_vm.工位2真空变化值1 = AutoRun.I.Inspection.工位2真空变化值1;
            //_vm.工位2真空变化值2 = AutoRun.I.Inspection.工位2真空变化值2;
            //_vm.工位2真空变化值3 = AutoRun.I.Inspection.工位2真空变化值3;
            //_vm.工位2真空变化值4 = AutoRun.I.Inspection.工位2真空变化值4;
            //_vm.工位2真空变化值5 = AutoRun.I.Inspection.工位2真空变化值5;
            //_vm.工位2真空变化值6 = AutoRun.I.Inspection.工位2真空变化值6;
            //_vm.工位2真空变化值7 = AutoRun.I.Inspection.工位2真空变化值7;
            //_vm.工位2真空变化值8 = AutoRun.I.Inspection.工位2真空变化值8;
            //_vm.工位3真空变化值1 = AutoRun.I.Inspection.工位3真空变化值1;
            //_vm.工位3真空变化值2 = AutoRun.I.Inspection.工位3真空变化值2;
            //_vm.工位3真空变化值3 = AutoRun.I.Inspection.工位3真空变化值3;
            //_vm.工位3真空变化值4 = AutoRun.I.Inspection.工位3真空变化值4;
            //_vm.工位3真空变化值5 = AutoRun.I.Inspection.工位3真空变化值5;
            //_vm.工位3真空变化值6 = AutoRun.I.Inspection.工位3真空变化值6;
            //_vm.工位3真空变化值7 = AutoRun.I.Inspection.工位3真空变化值7;
            //_vm.工位3真空变化值8 = AutoRun.I.Inspection.工位3真空变化值8;
            //_vm.工位4真空变化值1 = AutoRun.I.Inspection.工位4真空变化值1;
            //_vm.工位4真空变化值2 = AutoRun.I.Inspection.工位4真空变化值2;
            //_vm.工位4真空变化值3 = AutoRun.I.Inspection.工位4真空变化值3;
            //_vm.工位4真空变化值4 = AutoRun.I.Inspection.工位4真空变化值4;
            //_vm.工位4真空变化值5 = AutoRun.I.Inspection.工位4真空变化值5;
            //_vm.工位4真空变化值6 = AutoRun.I.Inspection.工位4真空变化值6;
            //_vm.工位4真空变化值7 = AutoRun.I.Inspection.工位4真空变化值7;
            //_vm.工位4真空变化值8 = AutoRun.I.Inspection.工位4真空变化值8;
            //_vm.工位5真空变化值1 = AutoRun.I.Inspection.工位5真空变化值1;
            //_vm.工位5真空变化值2 = AutoRun.I.Inspection.工位5真空变化值2;
            //_vm.工位5真空变化值3 = AutoRun.I.Inspection.工位5真空变化值3;
            //_vm.工位5真空变化值4 = AutoRun.I.Inspection.工位5真空变化值4;
            //_vm.工位5真空变化值5 = AutoRun.I.Inspection.工位5真空变化值5;
            //_vm.工位5真空变化值6 = AutoRun.I.Inspection.工位5真空变化值6;
            //_vm.工位5真空变化值7 = AutoRun.I.Inspection.工位5真空变化值7;
            //_vm.工位5真空变化值8 = AutoRun.I.Inspection.工位5真空变化值8;
            //_vm.工位6真空变化值1 = AutoRun.I.Inspection.工位6真空变化值1;
            //_vm.工位6真空变化值2 = AutoRun.I.Inspection.工位6真空变化值2;
            //_vm.工位6真空变化值3 = AutoRun.I.Inspection.工位6真空变化值3;
            //_vm.工位6真空变化值4 = AutoRun.I.Inspection.工位6真空变化值4;
            //_vm.工位6真空变化值5 = AutoRun.I.Inspection.工位6真空变化值5;
            //_vm.工位6真空变化值6 = AutoRun.I.Inspection.工位6真空变化值6;
            //_vm.工位6真空变化值7 = AutoRun.I.Inspection.工位6真空变化值7;
            //_vm.工位6真空变化值8 = AutoRun.I.Inspection.工位6真空变化值8;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }
        private void BarButtonItem_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
        }
    }
    class InspectionVM : ViewModelBase
    {
        public string Text { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("Text"); } } }
        public string 点检时间 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("点检时间"); } } }
        //public float 前称重重量1 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("前称重重量1"); } } }
        //public float 前称重重量2 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("前称重重量2"); } } }
        //public float 前称重重量3 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("前称重重量3"); } } }
        //public float 前称重重量4 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("前称重重量4"); } } }
        //public float 后称重重量1 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("后称重重量1"); } } }
        //public float 后称重重量2 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("后称重重量2"); } } }
        //public float 后称重重量3 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("后称重重量3"); } } }
        //public float 后称重重量4 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("后称重重量4"); } } }
        //public float 工位1真空变化值1 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位1真空变化值1"); } } }
        //public float 工位1真空变化值2 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位1真空变化值2"); } } }
        //public float 工位1真空变化值3 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位1真空变化值3"); } } }
        //public float 工位1真空变化值4 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位1真空变化值4"); } } }
        //public float 工位1真空变化值5 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位1真空变化值5"); } } }
        //public float 工位1真空变化值6 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位1真空变化值6"); } } }
        //public float 工位1真空变化值7 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位1真空变化值7"); } } }
        //public float 工位1真空变化值8 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位1真空变化值8"); } } }
        //public float 工位2真空变化值1 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位2真空变化值1"); } } }
        //public float 工位2真空变化值2 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位2真空变化值2"); } } }
        //public float 工位2真空变化值3 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位2真空变化值3"); } } }
        //public float 工位2真空变化值4 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位2真空变化值4"); } } }
        //public float 工位2真空变化值5 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位2真空变化值5"); } } }
        //public float 工位2真空变化值6 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位2真空变化值6"); } } }
        //public float 工位2真空变化值7 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位2真空变化值7"); } } }
        //public float 工位2真空变化值8 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位2真空变化值8"); } } }
        //public float 工位3真空变化值1 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位3真空变化值1"); } } }
        //public float 工位3真空变化值2 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位3真空变化值2"); } } }
        //public float 工位3真空变化值3 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位3真空变化值3"); } } }
        //public float 工位3真空变化值4 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位3真空变化值4"); } } }
        //public float 工位3真空变化值5 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位3真空变化值5"); } } }
        //public float 工位3真空变化值6 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位3真空变化值6"); } } }
        //public float 工位3真空变化值7 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位3真空变化值7"); } } }
        //public float 工位3真空变化值8 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位3真空变化值8"); } } }
        //public float 工位4真空变化值1 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位4真空变化值1"); } } }
        //public float 工位4真空变化值2 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位4真空变化值2"); } } }
        //public float 工位4真空变化值3 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位4真空变化值3"); } } }
        //public float 工位4真空变化值4 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位4真空变化值4"); } } }
        //public float 工位4真空变化值5 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位4真空变化值5"); } } }
        //public float 工位4真空变化值6 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位4真空变化值6"); } } }
        //public float 工位4真空变化值7 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位4真空变化值7"); } } }
        //public float 工位4真空变化值8 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位4真空变化值8"); } } }
        //public float 工位5真空变化值1 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位5真空变化值1"); } } }
        //public float 工位5真空变化值2 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位5真空变化值2"); } } }
        //public float 工位5真空变化值3 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位5真空变化值3"); } } }
        //public float 工位5真空变化值4 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位5真空变化值4"); } } }
        //public float 工位5真空变化值5 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位5真空变化值5"); } } }
        //public float 工位5真空变化值6 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位5真空变化值6"); } } }
        //public float 工位5真空变化值7 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位5真空变化值7"); } } }
        //public float 工位5真空变化值8 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位5真空变化值8"); } } }
        //public float 工位6真空变化值1 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位6真空变化值1"); } } }
        //public float 工位6真空变化值2 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位6真空变化值2"); } } }
        //public float 工位6真空变化值3 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位6真空变化值3"); } } }
        //public float 工位6真空变化值4 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位6真空变化值4"); } } }
        //public float 工位6真空变化值5 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位6真空变化值5"); } } }
        //public float 工位6真空变化值6 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位6真空变化值6"); } } }
        //public float 工位6真空变化值7 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位6真空变化值7"); } } }
        //public float 工位6真空变化值8 { get { return GetValue<float>(); } set { if (SetValue(value)) { RaisePropertyChanged("工位6真空变化值8"); } } }
        public List<InspectionInfo> InspectionInfoList
        {
            get { return GetValue<List<InspectionInfo>>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("HomeInfoList");
                }
            }
        }
    }
}
