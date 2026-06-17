using PLCHandler.Control.View;
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
 

namespace ZenergyBFSI.View
{
    /// <summary>
    /// UC_PLCMonitor.xaml 的交互逻辑
    /// </summary>
    public partial class UC_PLCMonitor : UserControl
    {
        private static PLCBoard xPlcHandler;
        private static object _syncRoot = new object();
        public static PLCBoard PLC
        {
            get
            {
                if (xPlcHandler == null)
                {
                    lock (_syncRoot)
                    {
                        if (xPlcHandler == null)
                        {
                            xPlcHandler = new ();
                        }
                    }
                }
                return xPlcHandler;
            }
        }
        public UC_PLCMonitor()
        {
            InitializeComponent();
            xPlcHandler = this.xPLCHandler;
        }
    }
}
