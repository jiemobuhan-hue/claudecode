using PLCBar.PlcHandler;
using PLCBar.Service;
using System.Windows;

namespace PLCBar.View
{
    public partial class PlcConfigWindow : Window
    {
        private readonly PlcConfigViewModel _vm;

        public PlcConfigWindow()
        {
            InitializeComponent();

            // 从 PlcHandler 读取当前连接信息作为初始值
            var currentConnections = PlcHandler.I.GetAllPlcConnectionInfo();
            var currentStatus     = PlcHandler.I.GetConnectionStatus();

            _vm = new PlcConfigViewModel(currentConnections, currentStatus);
            _vm.CloseRequested += (_, saved) =>
            {
                DialogResult = saved;
                Close();
            };

            DataContext = _vm;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
