using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// PA_AddUser.xaml 的交互逻辑 
    /// 这是一个权限管理的应用窗口，主要负责权限模块的登录
    /// </summary>
    public partial class PA_AddUser : Window
    {
        public string code = "";
        public string name = "";
        public string cardNo = "FFFF";
        public PA_AddUser()
        {
            InitializeComponent();
        }
        private void Submit(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        public void Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
