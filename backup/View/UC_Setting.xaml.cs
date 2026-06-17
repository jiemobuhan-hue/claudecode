using DevExpress.Charts.Native;
using DevExpress.Mvvm;
using DevExpress.Xpf.Printing;
using Newtonsoft.Json.Linq;
using RinKit; 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ZenergyBFSI.Model;
using ZenergyBFSI.Service;
using static DevExpress.Xpf.Core.NativeMethods;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// UC_Setting.xaml 的交互逻辑
    /// </summary>
    public partial class UC_Setting : UserControl
    {
        SettingViewModel _vm = new SettingViewModel();
        public UC_Setting()
        {
            _vm.LoadSettings();
            InitializeComponent();
            _vm.LoadSettings();
            DataContext = _vm;
            this.SettingPages.momBtnSave.Click += Save_MOM;
            this.SettingPages.momBtnRefresh.Click += Refresh_MOM;
            this.SettingPages.plcBtnSave.Click += Save_PLCAddress;
            this.SettingPages.plcBtnRefresh.Click += Refresh_PLCAddress;
        }
        /// <summary>
        /// 更新MOM管控参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Refresh_MOM(object sender, RoutedEventArgs e)
        {
            Rdb.SelectList(out List<ParameterInfo> list, "SELECT * FROM ParameterInfo WHERE Enable=1");
            _vm.ParamList = new ObservableCollection<ParameterInfo>(list);
        }

        /// <summary>
        /// 保存MOM参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Save_MOM(object sender, RoutedEventArgs e)
        {
            this.SettingPages.MOMGridView.PostEditor();
            var data = this.SettingPages.MOMgridControl.ItemsSource as ObservableCollection<ParameterInfo>;
            if (data != null)
            {
                try
                {
                    // 3. 在这里执行你的保存逻辑（如存入 SQL 数据库）
                    //SQLiteGenericHelper.ClearTable("ParameterInfo");


                    SQLiteGenericHelper.BulkUpsert<ParameterInfo>(data, keyPropertyName: nameof(ParameterInfo.ParameterCode), "ParameterInfo");
                    MessageBox.Show("保存成功！");


                }
                catch (Exception )
                {

                    throw;
                }
     
            }

        }

        /// <summary>
        /// 保存PLC地址
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Save_PLCAddress(object sender, RoutedEventArgs e)
        {
            this.SettingPages.PLCGridView.PostEditor();
            var data = this.SettingPages.PLCGridControl.ItemsSource as ObservableCollection<PlcObj>;
            if (data != null)
            {
                try
                {
                    // 3. 在这里执行你的保存逻辑（如存入 SQL 数据库）
                    //SQLiteGenericHelper.ClearTable("PlcObj");
                    SQLiteGenericHelper.BulkUpsert<PlcObj>(data,"Name", "PlcObj");
                    MessageBox.Show("保存成功！");
                }
                catch (Exception ex)
                {
                    var a = ex.Message;
                    //throw;
                }

            }

        }


        private void Refresh_PLCAddress(object sender, RoutedEventArgs e)
        {
 
            Rdb.SelectList(out List<PlcObj> PLClist, "SELECT * FROM PlcObj ");  
            _vm.PLCAddressList = new ObservableCollection<PlcObj>(PLClist);
        }
        private void Save_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            
            //Settings.电芯型号 = _vm.电芯型号;
            //Settings.EquipmentCode = _vm.设备编号;
            //Settings.注液机构编号 = _vm.注液机构编号;
            //Settings.插钉机构编号 = _vm.插钉机构编号;
            //Settings.MOM联机计数 = _vm.MOM联机计数;
            //Settings.保液量目标 = _vm.电解液保有量;
            //Settings.前称重上限 = _vm.前称重上限;
            //Settings.前称重下限 = _vm.前称重下限;
            //Settings.后称重上限 = _vm.后称重上限;
            //Settings.后称重下限 = _vm.后称重下限;
            //Settings.胶钉高度上限 = _vm.胶钉高度上限;
            //Settings.胶钉高度下限 = _vm.胶钉高度下限;
            //Settings.保液量上限 = _vm.保液量上限;
            //Settings.保液量下限 = _vm.保液量下限;
            //Settings.保压真空值 = _vm.保压真空值;
            //Settings.保压时间 = _vm.保压时间;
            //Settings.注液正压值 = _vm.注液正压值;
            //Settings.正压时间 = _vm.正压时间;
            //Settings.MOM在线 = _vm.MOM在线;
            //Settings.清料模式 = _vm.清料模式;
            //Settings.一注前称重 = _vm.一注前称重;
            //Settings.一注后称重 = _vm.一注后称重;
            //Settings.注液量 = _vm.注液量;
            //Settings.失液量上限 = _vm.失液量上限;
            //Settings.失液量下限 = _vm.失液量下限;
            //Settings.注液偏差阈值 = _vm.注液偏差阈值;
            //Settings.注液偏差次数 = _vm.注液偏差次数;
            Settings.Save();
        }

        private void Load_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            //Settings.Load();
            //_vm.LoadSettings();
        }

        private void Logout_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            Main.Logout();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            #region 参考设置选项
            //_vm.ParamList = MomHandler.I.AllParameter();

            //_vm.电芯型号 = Settings.电芯型号;
            //_vm.设备编号 = Settings.EquipmentCode;
            //_vm.注液机构编号 = Settings.注液机构编号;
            //_vm.插钉机构编号 = Settings.插钉机构编号;
            //_vm.MOM联机计数 = Settings.MOM联机计数;
            //_vm.电解液保有量 = Settings.保液量目标;
            //_vm.前称重上限 = Settings.前称重上限;
            //_vm.前称重下限 = Settings.前称重下限;
            //_vm.后称重上限 = Settings.后称重上限;
            //_vm.后称重下限 = Settings.后称重下限;
            //_vm.胶钉高度上限 = Settings.胶钉高度上限;
            //_vm.胶钉高度下限 = Settings.胶钉高度下限;
            //_vm.保液量上限 = Settings.保液量上限;
            //_vm.保液量下限 = Settings.保液量下限;
            //_vm.保压真空值 = Settings.保压真空值;
            //_vm.保压时间 = Settings.保压时间;
            //_vm.注液正压值 = Settings.注液正压值;
            //_vm.正压时间 = Settings.正压时间;
            //_vm.MOM在线 = Settings.MOM在线;
            //_vm.清料模式 = Settings.清料模式;
            //_vm.一注前称重 = Settings.一注前称重;
            //_vm.一注后称重 = Settings.一注后称重;
            //_vm.注液量 = Settings.注液量;
            //_vm.失液量上限 = Settings.失液量上限;
            //_vm.失液量下限 = Settings.失液量下限;
            //_vm.注液偏差阈值 = Settings.注液偏差阈值;
            //_vm.注液偏差次数 = Settings.注液偏差次数;
            #endregion

            //_vm.LoadSettings();
        }
 
    }


    internal class SettingViewModel : ViewModelBase
    {
        
        public ObservableCollection<ParameterInfo> ParamList
        {
            get { return GetValue<ObservableCollection<ParameterInfo>>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("ParamList");
                }
            }
        }
        public ObservableCollection<PlcObj> PLCAddressList
        {
            get { return GetValue<ObservableCollection<PlcObj>>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("PLCAddressList");
                }
            }
        }

        public SettingViewModel()
        {

        }

        public void LoadSettings()
        {
            try
            {
                Settings.Save();
                  Rdb.SelectList(out List<ParameterInfo> list, "SELECT * FROM ParameterInfo WHERE Enable=1");
                  Rdb.SelectList(out List<PlcObj> PLClist, "SELECT * FROM PlcObj ");
                ParamList = new ObservableCollection<ParameterInfo>(list) ;
                PLCAddressList = new ObservableCollection<PlcObj>(PLClist) ;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"配置加载异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
            }
        }
    }
}