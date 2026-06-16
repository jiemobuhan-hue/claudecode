using PLCHandler.View;
using System.Windows;
using System.Windows.Controls;
using ViewModels;

namespace PLCHandler.Control.View
{
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

            RefreshTabs();
        }

        public void RefreshTabs()
        {
            plcTabs.Items.Clear();
            foreach (var plc in _vm.PlcList)
            {
                var tab = new TabItem
                {
                    Header = $"{plc.Name} {plc.StatusIcon}",
                    Tag = plc.Id
                };
                plcTabs.Items.Add(tab);
            }

            if (plcTabs.Items.Count > 0)
            {
                plcTabs.SelectedIndex = 0;
                if (plcTabs.SelectedItem is TabItem firstTab)
                    _vm.SelectPlc(firstTab.Tag as string);
            }
        }

        private void PlcTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (plcTabs.SelectedItem is TabItem tab && tab.Tag is string plcId)
                _vm.SelectPlc(plcId);
        }
    }
}
