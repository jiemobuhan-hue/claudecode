using System.Windows;

namespace LoginProject
{
    public partial class SessionLogoutWindow : Window
    {
        public SessionLogoutWindow()
        {
            InitializeComponent();
        }

        private void btnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // 确认退出，关闭应用程序
            Application.Current.Shutdown();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // 取消退出，关闭对话框
            this.DialogResult = false;
            this.Close();
        }
    }
}