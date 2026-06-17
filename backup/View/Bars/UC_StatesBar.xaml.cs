using DevExpress.Mvvm;
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

namespace ZenergyBFSI.View.Bars
{
    /// <summary>
    /// UC_StatesBar.xaml 的交互逻辑
    /// </summary>
    public partial  class UC_StatesBar : UserControl
    {
 
        public UC_StatesBarVM uC_StatesBarVM = new UC_StatesBarVM();
        public  UC_StatesBar()
        {
            InitializeComponent();
            this.DataContext = uC_StatesBarVM;
        }

    }
    public class UC_StatesBarVM: ViewModelBase
    {
        // PLC 状态颜色逻辑
        //public Brush PlcStatusColor  => IsPlcConnected ? Brushes.LimeGreen : Brushes.Red;
        public Brush PlcStatusColor
        {
            get { return GetValue<Brush>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("PlcStatusColor");
                }
            }
        }


        public Brush MesStatusColor => IsMomConnected ? Brushes.LimeGreen : Brushes.Red;

        // 运行模式颜色逻辑
        public Brush ModeColor => IsAutoMode ?
            (Brush)new BrushConverter().ConvertFrom("#2E7D32") : // 深绿 (M3 Success)
            (Brush)new BrushConverter().ConvertFrom("#EF6C00");   // 深橙 (M3 Warning)

        public bool IsPlcConnected 
        { 
            get{
                return GetValue<bool>();
            }
            set{
                if (SetValue(value))
                {
                    RaisePropertyChanged("PlcStatusColor");
                    this.PlcStatusColor = this.IsPlcConnected ? Brushes.LimeGreen : Brushes.Red;
                }
                
            }
       }
        public bool IsMomConnected { get; set; }
        public bool IsAutoMode { get; set; }
        public string CurrentUserName { get; set; }
    }
}
