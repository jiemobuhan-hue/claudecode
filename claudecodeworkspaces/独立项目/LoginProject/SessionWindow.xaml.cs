using System.Windows;

namespace LoginProject
{
    public partial class SessionWindow : Window
    {
        public SessionWindow()
        {
            InitializeComponent();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            // 显示退出确认对话框
            var logoutWindow = new SessionLogoutWindow();
            logoutWindow.Owner = this;
            logoutWindow.ShowDialog();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}