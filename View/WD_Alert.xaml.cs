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
using System.Windows.Forms;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// WD_Alert.xaml 的交互逻辑
    /// </summary>
    public partial class WD_Alert : Window,IDisposable
    {
        public static int Alarmnums = 0;
        public WD_Alert(string msg)
        {
            InitializeComponent();
            tbMsg.Text = msg;
            main.Width = /*Screen.PrimaryScreen.Bounds.Width*/800;
            main.Height = /*Screen.PrimaryScreen.Bounds.Height*/800;
            Alarmnums++;
        }

        public void Dispose()
        {
            Alarmnums--;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            UC_Operation.I.Alert("");
            Alarmnums--;
            Close();
        }

         
    }
}
