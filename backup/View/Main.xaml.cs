using RinKit;
using RinKitWPF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ViewModels;
using ZenergyBFSI.Model;
using ZenergyBFSI.Service;
using ZenergyBFSI.View.Bars;
using ViewModelBase = RinKitWPF.ViewModelBase;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// Main.xaml 的交互逻辑
    /// </summary>
    public partial class Main : Window
    {
        //持有实例
        public static Main _instance;

        //根线程异步锁
        private static object _syncRoot = new object();

        //用户实例
        static User _user;

        //登录窗口实例
        static PA_AddUser _adduser;

        public static UC_StatesBar uC_StatesBar;

        //窗口模型实例
        private static VM_Main _vm = new VM_Main();

        /// <summary>
        /// 应用窗口的实例化代码
        /// </summary>
        public Main()
        {
            InitializeComponent();
            uC_StatesBar = this.RunStates;
        }

        /// <summary>
        /// 设备初始化代码
        /// </summary>
        private void DeviceInit()
        {
            var flag = true;
            if (!MomHandler.I.Init()) flag = false;
            if (Settings.Power > 0)
            {
                //PlcHandler.I.Init();
                AutoRun.I.Init();
            }
        }

        /// <summary>
        /// 窗口初始化代码
        /// </summary>
        private void WindowInit()
        {
            _vm.VersionTxt = $"V{Assembly.GetExecutingAssembly().GetName().Version} Designed By AMR";
            _vm.UserTxt = $"当前用户 ：无";
        }
        
        /// <summary>
        /// 窗口加载代码
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
 
                   // 同步封送到 UI 线程（等待执行完成） 
                        DeviceInit(); 
 
        }

        /// <summary>
        /// 键盘输入事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == "\r")
            {
                var card = _vm.InputTxt.Trim();
                LogIn(card);
                _vm.InputTxt = "";
            }
            else
            {
                _vm.InputTxt += e.Text;
            }
        }

        /// <summary>
        /// 登录代码
        /// </summary>
        /// <param name="card"></param>
        private void LogIn(string card)
        {
            Rdb.SelectList(out List<User> users, $@"SELECT * FROM ""User"" WHERE CardNo = '{card}'");
            if (users != null && users.Count > 0)
            {
                _user = users.First();
                _vm.UserTxt = $"当前用户 ：{_user.Code}-{_user.Name} ({_user.Role})";
               AutoRun.I.Power = _user.Power;
                UC_Operation.I.WriteLog($"用户 ： {_user.Code}-{_user.Name} ({_user.Role}) 已登录", "Debug");
                switch (_user.Power)
                {
                   case 0:
                        it_user.Visibility = Visibility.Collapsed;
                        it_setting.Visibility = Visibility.Collapsed;
                        it_singal.Visibility = Visibility.Collapsed;
                        //UC_Operation.RoleControl(_user.Power);
                        break;
                    case 1:
                        it_user.Visibility = Visibility.Collapsed;
                        it_setting.Visibility = Visibility.Collapsed;
                        it_singal.Visibility = Visibility.Collapsed;
                        //UC_Operation.RoleControl(_user.Power);
                        break;
                    case 2:
                        it_user.Visibility = Visibility.Collapsed;
                        it_setting.Visibility = Visibility.Collapsed;
                        it_singal.Visibility = Visibility.Collapsed;
                        //UC_Operation.RoleControl(_user.Power);
                        break;
                    case 3:
                        it_user.Visibility = Visibility.Visible;
                        it_setting.Visibility = Visibility.Visible;
                        it_singal.Visibility = Visibility.Collapsed;
                        //UC_Operation.RoleControl(_user.Power);
                        break;
                    case 4:
                        it_user.Visibility = Visibility.Visible;
                        it_setting.Visibility = Visibility.Visible;
                        it_singal.Visibility = Visibility.Visible;
                        //UC_Operation.RoleControl(_user.Power);
                        break;
                    default:
                        Rlog.Warn("未注册权限!");
                        break;
                }
            }
            //else
            //{
            //    UC_Operation.I.WriteLog($"未注册此用户,卡号:{card}", "Warn");
            //}
        }

        /// <summary>
        /// 登出代码
        /// </summary>
        public static void Logout()
        {
            //_user = new User("无", "", "", 0);
            //AutoRun.I.Power = _user.Power;
            //_vm.UserTxt = $"当前用户 ：无";
        }

        /// <summary>
        /// 登录窗口浮出代码
        /// </summary>
        /// <returns></returns>
        public static int ShowAddUser()
        {
            int res = 0;
            //ICReader.I.Run = true;
            //if (_user != null && _user.Power > 3)
            //{
            //    _adduser = new PA_AddUser();
            //    if (_adduser.ShowDialog() == true)
            //    {

            //        if (_adduser.ic_load.Text.Length < 3)
            //        {
            //            MessageBox.Show("请录入卡号！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            //        }
            //        else
            //        {
            //            var user = new User(_adduser.tb_name.Text, _adduser.tb_code.Text, _adduser.ic_load.Text, Convert.ToInt32(((ComboBoxItem)_adduser.cb_power.SelectedItem).Tag));
            //            //var sql = $"INSERT INTO User(Name, Code, CardNo, Power,Role) VALUES ('{user.Name}', '{user.Code}', '{user.CardNo}',{user.Power},'{user.Role}')";
            //            Rdb.Insert(user, false);
            //        }
            //    }
            //    else
            //    {
            //        Rlog.Trace("WD_AddUser Cancel");
            //    }
            //    _adduser = null;
            //}
            //else
            //{
            //    res = 10;
            //}
            //ICReader.I.Run = false;
            return res;
        }


        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            if (MessageBox.Show("确定关闭程序？", "Warn", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Process.GetCurrentProcess().Kill();
            }
        }

 
    }

    /// <summary>
    /// 关于窗口的模型类
    /// </summary>
    public class VM_Main : ViewModelBase
    {
        //public User user;

        private string versionTxt = "";
        public string VersionTxt { get { return versionTxt; } set { versionTxt = value; OnPropertyChanged("VersionTxt"); } }

        private string inputTxt = "";
        public string InputTxt { get { return inputTxt; } set { inputTxt = value; OnPropertyChanged("InputTxt"); } }

        private string userTxt = "";
        public string UserTxt { get { return userTxt; } set { userTxt = value; OnPropertyChanged("UserTxt"); } }

    }
}
