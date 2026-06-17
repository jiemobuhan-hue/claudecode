using System.Windows;
using System.Windows.Media;

namespace LoginProject
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            // 简单的验证
            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtUsername.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("请输入密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPassword.Focus();
                return;
            }

            // TODO: 这里添加实际的登录验证逻辑
            // 演示用：简单的硬编码验证
            if (username == "admin" && password == "123456")
            {
                MessageBox.Show("登录成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                // 打开会话管理窗口
                SessionWindow sessionWindow = new SessionWindow();
                sessionWindow.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("用户名或密码错误", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }
    }
}