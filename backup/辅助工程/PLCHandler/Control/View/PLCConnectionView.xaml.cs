using System.Windows.Controls;
using ViewModels;

namespace PLCHandler.View
{
    public partial class PLCConnectionView : UserControl
    {
        public PLCConnectionView()
        {
            InitializeComponent();
        }

        private void gridPlc_SelectedItemChanged(object sender,
            DevExpress.Xpf.Grid.SelectedItemChangedEventArgs e)
        {
            if (DataContext is PLCBoardViewModel vm && e.NewItem is PlcStatusItem item)
            {
                vm.SelectPlc(item.Id);
            }
        }
    }
}