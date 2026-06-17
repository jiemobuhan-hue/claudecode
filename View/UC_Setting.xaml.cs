using DevExpress.Mvvm;
using RinKit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ZenergyBFSI.Model;
using ZenergyBFSI.Service;

namespace ZenergyBFSI.View
{
    public partial class UC_Setting : UserControl
    {
        private SettingViewModel _vm = new SettingViewModel();

        public UC_Setting()
        {
            _vm.LoadSettings();
            InitializeComponent();
            DataContext = _vm;

            this.SettingPages.momBtnSave.Click += Save_MOM;
            this.SettingPages.momBtnRefresh.Click += Refresh_MOM;
            this.SettingPages.plcBtnSave.Click += Save_PLCAddress;
            this.SettingPages.plcBtnRefresh.Click += Refresh_PLCAddress;
        }

        private void Refresh_MOM(object sender, RoutedEventArgs e)
        {
            Rdb.SelectList(out List<ParameterInfo> list, "SELECT * FROM ParameterInfo WHERE Enable=1");
            _vm.ParamList = new ObservableCollection<ParameterInfo>(list);
        }

        private void Save_MOM(object sender, RoutedEventArgs e)
        {
            this.SettingPages.MOMGridView.PostEditor();
            var data = this.SettingPages.MOMgridControl.ItemsSource as ObservableCollection<ParameterInfo>;
            if (data != null)
            {
                try
                {
                    SQLiteGenericHelper.BulkUpsert<ParameterInfo>(data, keyPropertyName: nameof(ParameterInfo.ParameterCode), "ParameterInfo");
                    MessageBox.Show("MOM参数保存成功！");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"MOM参数保存失败：{ex.Message}");
                }
            }
        }

        private void Save_PLCAddress(object sender, RoutedEventArgs e)
        {
            this.SettingPages.PLCGridView.PostEditor();
            var data = this.SettingPages.PLCGridControl.ItemsSource as ObservableCollection<PlcObj>;
            if (data != null)
            {
                try
                {
                    SQLiteGenericHelper.BulkUpsert<PlcObj>(data, "Name", "PlcObj");
                    MessageBox.Show("PLC信号配置保存成功！");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"PLC信号配置保存失败：{ex.Message}");
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
            _vm.SaveAll();
        }

        private void Load_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            Settings.Load();
            _vm.LoadSettings();
        }

        private void Logout_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            Main.Logout();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) { }
    }

    internal class SettingViewModel : ViewModelBase
    {
        public SettingViewModel() { }

        // ── 系统运行 ──
        public bool 开机自启动
        {
            get => Settings.自启动 == 1;
            set { Settings.自启动 = value ? 1 : 0; RaisePropertyChanged(); }
        }

        public int 自动机循环等待
        {
            get => Settings.自动机循环等待;
            set { Settings.自动机循环等待 = value; RaisePropertyChanged(); }
        }

        public int PLC循环等待
        {
            get => Settings.PLC循环等待;
            set { Settings.PLC循环等待 = value; RaisePropertyChanged(); }
        }

        public int PLC超时警告
        {
            get => Settings.PLC超时警告 / 10000;
            set { Settings.PLC超时警告 = value * 10000; RaisePropertyChanged(); }
        }

        public int 错误等待
        {
            get => Settings.错误等待;
            set { Settings.错误等待 = value; RaisePropertyChanged(); }
        }

        public int 扫码超时计数
        {
            get => Settings.扫码超时计数;
            set { Settings.扫码超时计数 = value; RaisePropertyChanged(); }
        }

        // ── 数据库配置 ──
        public string SQLite路径
        {
            get
            {
                if (string.IsNullOrEmpty(Settings.SQLite路径))
                    Settings.SQLite路径 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Local.db");
                return Settings.SQLite路径;
            }
            set { Settings.SQLite路径 = value; RaisePropertyChanged(); }
        }

        public string SQLServer视觉地址
        {
            get => Settings.SQLServer视觉地址;
            set { Settings.SQLServer视觉地址 = value; RaisePropertyChanged(); }
        }

        public string SQLServer视觉库名
        {
            get => Settings.SQLServer视觉库名;
            set { Settings.SQLServer视觉库名 = value; RaisePropertyChanged(); }
        }

        public string SQLServer视觉用户
        {
            get => Settings.SQLServer视觉用户;
            set { Settings.SQLServer视觉用户 = value; RaisePropertyChanged(); }
        }

        public string SQLServer视觉密码
        {
            get => Settings.SQLServer视觉密码;
            set { Settings.SQLServer视觉密码 = value; RaisePropertyChanged(); }
        }

        // ── MOM 连接 ──
        public string MOM地址
        {
            get => Settings.MOM地址;
            set { Settings.MOM地址 = value; RaisePropertyChanged(); }
        }

        public int MOM心跳间隔
        {
            get => Settings.MOM心跳间隔;
            set { Settings.MOM心跳间隔 = value; RaisePropertyChanged(); }
        }

        public int MOM联机计数
        {
            get => Settings.MOM联机计数;
            set { Settings.MOM联机计数 = value; RaisePropertyChanged(); }
        }

        public bool MOM在线
        {
            get => Settings.MOM在线 == 1;
            set { Settings.MOM在线 = value ? 1 : 0; RaisePropertyChanged(); }
        }

        // ── PLC 配置 ──
        public string PLC_IP
        {
            get => Settings.PLC_IP;
            set { Settings.PLC_IP = value; RaisePropertyChanged(); }
        }

        public int PLC_Port
        {
            get => Settings.PLC_Port;
            set { Settings.PLC_Port = value; RaisePropertyChanged(); }
        }

        public int PLC_SA
        {
            get => Settings.PLC_SA;
            set { Settings.PLC_SA = value; RaisePropertyChanged(); }
        }

        // ── 设备信息 ──
        public string EquipmentCode
        {
            get => Settings.EquipmentCode;
            set { Settings.EquipmentCode = value; RaisePropertyChanged(); }
        }

        public string 电芯型号
        {
            get => Settings.电芯型号;
            set { Settings.电芯型号 = value; RaisePropertyChanged(); }
        }

        public string Software
        {
            get => Settings.Software;
            set { Settings.Software = value; RaisePropertyChanged(); }
        }

        // ── Grid 数据 ──
        public ObservableCollection<ParameterInfo> ParamList
        {
            get { return GetValue<ObservableCollection<ParameterInfo>>(); }
            set
            {
                if (SetValue(value))
                    RaisePropertyChanged("ParamList");
            }
        }

        public ObservableCollection<PlcObj> PLCAddressList
        {
            get { return GetValue<ObservableCollection<PlcObj>>(); }
            set
            {
                if (SetValue(value))
                    RaisePropertyChanged("PLCAddressList");
            }
        }

        public void LoadSettings()
        {
            try
            {
                Settings.Load();

                // Notify all properties
                RaisePropertyChanged("开机自启动");
                RaisePropertyChanged("自动机循环等待");
                RaisePropertyChanged("PLC循环等待");
                RaisePropertyChanged("PLC超时警告");
                RaisePropertyChanged("错误等待");
                RaisePropertyChanged("扫码超时计数");
                RaisePropertyChanged("SQLite路径");
                RaisePropertyChanged("SQLServer视觉地址");
                RaisePropertyChanged("SQLServer视觉库名");
                RaisePropertyChanged("SQLServer视觉用户");
                RaisePropertyChanged("SQLServer视觉密码");
                RaisePropertyChanged("MOM地址");
                RaisePropertyChanged("MOM心跳间隔");
                RaisePropertyChanged("MOM联机计数");
                RaisePropertyChanged("MOM在线");
                RaisePropertyChanged("PLC_IP");
                RaisePropertyChanged("PLC_Port");
                RaisePropertyChanged("PLC_SA");
                RaisePropertyChanged("EquipmentCode");
                RaisePropertyChanged("电芯型号");
                RaisePropertyChanged("Software");

                Rdb.SelectList(out List<ParameterInfo> list, "SELECT * FROM ParameterInfo WHERE Enable=1");
                Rdb.SelectList(out List<PlcObj> PLClist, "SELECT * FROM PlcObj ");
                ParamList = new ObservableCollection<ParameterInfo>(list);
                PLCAddressList = new ObservableCollection<PlcObj>(PLClist);
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"配置加载异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
            }
        }

        public void SaveAll()
        {
            try
            {
                Settings.Save();
                MessageBox.Show("所有设置已保存。\n部分设置需重启应用后生效。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置保存失败：{ex.Message}");
            }
        }
    }
}
