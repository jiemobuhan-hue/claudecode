using DevExpress.Mvvm;
using DevExpress.Xpf.Core.Native;
using DevExpress.XtraRichEdit.Layout;
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
using System.Windows.Threading;
using ZenergyBFSI.Model;
using ZenergyBFSI.Model.Messages;
using ZenergyBFSI.Properties;
using ZenergyBFSI.View.StateCards;
using ZenergyBFSI.Service;
using static DevExpress.Xpo.Logger.LogManager;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// UC_Home.xaml 的交互逻辑
    /// 该控件提供主界面信息显示，考虑给出相关的设备运行
    /// </summary>
    public partial class UC_Home : UserControl
    {
        HomeVM _vm = new HomeVM();
        Timer _timer = new Timer(1000);
        private readonly UC_StatesCards _dash;
        private readonly CsvDataService _csv;
        private readonly UC_Operation _insp;
        public UC_Home()
        {
            InitializeComponent();
            _timer.Start();
            DataContext = _vm;
            _csv = new CsvDataService(@".\Data");
            _dash = this.DashBoard;
        }




        /// <summary>
        /// 复位操作按钮事件
        /// 考虑一下设备的整体复位逻辑，小心状态错位与撞机
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reset_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            if (MessageBox.Show("是否确认初始化数据？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                //参考复位操作

                //CodeReader_1.I.Cancel();
                //CodeReader_2.I.Cancel();
                //CodeReader_3.I.Cancel();
                //CodeReader_4.I.Cancel();
                //MomHandler.I.ClearIn();
                //MomHandler.I.ClearOut();
                //InjectionControler_1.I.Reset();
                //InjectionControler_2.I.Reset();
                //InjectionControler_3.I.Reset();
                //InjectionControler_4.I.Reset();
                ////AutoRun.I.ResetIO();

                //Rdb.QueueIn($"UPDATE CellData SET 二注结束 = 1");
                ////Rdb.QueueIn($"UPDATE CellState SET 离开 = 1");

                ////AutoRun.I.ListState.Clear();
                //AutoRun.I.ListData.Clear();
            }
        }

        /// <summary>
        /// 强制完成按钮事件
        /// 请做好数据赋值处理，防止无效数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Compel_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            if (MessageBox.Show("是否确认强制完成所有电芯（已到后称重）的检测？请在检测无法完成时执行。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                //AutoRun.I.UpMOM();
            }
        }

        /// <summary>
        /// 报警复位按钮事件
        /// 请考虑PLC通讯相关防呆操作以及最重要的三防操作避免人身伤亡
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Recover_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            if (MessageBox.Show("是否确认复位报警？请完成异常排查。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                AutoRun.I.LossCount = 0;
                AutoRun.I.Alarm("", 0);
                AutoRun.I.Init();
            }
        }

        /// <summary>
        /// 点检按钮事件
        /// 请考虑处理信号交互的延时以及点检异常处理以及对其它操作的影响
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Inspection_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            //参考代码

            //new WD_Inspection().Show();
        }
    }

    //主页视图的视图模型，储存相关显示数据的动态变量
    /// <summary>
    /// 主页视图模型
    /// </summary>
    class HomeVM : ViewModelBase
    {
        #region 参考数据项
        //public string Text1 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("Text1"); } } }
        //public string Text2 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("Text2"); } } }
        //public string SMQ1 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("SMQ1"); } } }
        //public string SMQ2 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("SMQ2"); } } }
        //public string SMQ3 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("SMQ3"); } } }
        //public string SMQ4 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("SMQ4"); } } }
        ////public string SMQ5 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("SMQ5"); } } }
        ////public string SMQ6 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("SMQ6"); } } }
        ////public string SMQ7 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("SMQ7"); } } }
        ////public string SMQ8 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("SMQ8"); } } }
        //public string ZYB1 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("ZYB1"); } } }
        //public string ZYB2 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("ZYB2"); } } }
        //public string ZYB3 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("ZYB3"); } } }
        //public string ZYB4 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("ZYB4"); } } }
        //public string PLC1 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("PLC1"); } } }
        //public string MOM1 { get { return GetValue<string>(); } set { if (SetValue(value)) { RaisePropertyChanged("MOM1"); } } }

        #endregion
        public List<HomeInfo> HomeInfoList
        {
            get { return GetValue<List<HomeInfo>>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("HomeInfoList");
                }
            }
        }
    }

    /// <summary>
    /// 主页数据模型 根据项目要求创建
    /// </summary>
    internal class HomeInfo
    {
        /// <summary>
        /// 自定义构造
        /// </summary>
        public HomeInfo()
        {

        }
    }
}
