using PLCHandler.View;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ViewModels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace PLCHandler.Control.View
{
    /// <summary>
    /// PLCBoard.xaml 的交互逻辑
    /// </summary>
    public partial class PLCBoard : UserControl
    {
        private readonly PLCBoardViewModel _vm;  
        public PLCBoard()
        {
            InitializeComponent();
            var configDir = System.IO.Path.Combine(
               System.IO.Path.GetDirectoryName(
                   System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
               "Config"
           );
            var configService = new PlcConfigService(configDir);
            var monitor = new PlcMonitor(configService);
            _vm = new PLCBoardViewModel(monitor);
            DataContext = _vm;

            contentArea.Content = new PLCConnectionView { DataContext = _vm };

         

            //Closed += (s, e) => _vm.Dispose();
        }
        private void NavRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (contentArea == null) return;

            var tag = (sender as System.Windows.Controls.RadioButton)?.Tag as string;
            if (tag == "SignalMonitor")
            {
                _vm.SelectedView = "SignalMonitor";
                contentArea.Content = new  SignalMonitorView { DataContext = _vm };
            }
            else
            {
                _vm.SelectedView = "PLCConnection";
                contentArea.Content = new  PLCConnectionView { DataContext = _vm };
            }
        }

    }
}
