using RinKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Timer = System.Timers.Timer;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// UC_Control.xaml 的交互逻辑
    /// 该控件属于对IO信号的检测与交互控件，涉及到一个空间的IO读取线程
    /// </summary>
    public partial class UC_Control : UserControl
    {
        Timer timer;
        /// <summary>
        /// IO读取线程
        /// </summary>
        //Task Task_ReadIO;
        public UC_Control()
        {
            InitializeComponent();
            timer = new Timer(100);
            Signal_O0.yes.Click += SignalClick;
            Signal_O0.yes.Tag = "Signal_O0_yes";
            Signal_O0.no.Click += SignalClick;
            Signal_O0.no.Tag = "Signal_O0_no";
            Signal_O1.yes.Click += SignalClick;
            Signal_O1.yes.Tag = "Signal_O1_yes";
            Signal_O1.no.Click += SignalClick;
            Signal_O1.no.Tag = "Signal_O1_no";
            Signal_O2.yes.Click += SignalClick;
            Signal_O2.yes.Tag = "Signal_O2_yes";
            Signal_O2.no.Click += SignalClick;
            Signal_O2.no.Tag = "Signal_O2_no";
            Signal_O3.yes.Click += SignalClick;
            Signal_O3.yes.Tag = "Signal_O3_yes";
            Signal_O3.no.Click += SignalClick;
            Signal_O3.no.Tag = "Signal_O3_no";
            Signal_O4.yes.Click += SignalClick;
            Signal_O4.yes.Tag = "Signal_O4_yes";
            Signal_O4.no.Click += SignalClick;
            Signal_O4.no.Tag = "Signal_O4_no";
            Signal_O5.yes.Click += SignalClick;
            Signal_O5.yes.Tag = "Signal_O5_yes";
            Signal_O5.no.Click += SignalClick;
            Signal_O5.no.Tag = "Signal_O5_no";
            Signal_O6.yes.Click += SignalClick;
            Signal_O6.yes.Tag = "Signal_O6_yes";
            Signal_O6.no.Click += SignalClick;
            Signal_O6.no.Tag = "Signal_O6_no";
            Signal_O7.yes.Click += SignalClick;
            Signal_O7.yes.Tag = "Signal_O7_yes";
            Signal_O7.no.Click += SignalClick;
            Signal_O7.no.Tag = "Signal_O7_no";
            Signal_O8.yes.Click += SignalClick;
            Signal_O8.yes.Tag = "Signal_O8_yes";
            Signal_O8.no.Click += SignalClick;
            Signal_O8.no.Tag = "Signal_O8_no";
            Signal_O9.yes.Click += SignalClick;
            Signal_O9.yes.Tag = "Signal_O9_yes";
            Signal_O9.no.Click += SignalClick;
            Signal_O9.no.Tag = "Signal_O9_no";
            Signal_O10.yes.Click += SignalClick;
            Signal_O10.yes.Tag = "Signal_O10_yes";
            Signal_O10.no.Click += SignalClick;
            Signal_O10.no.Tag = "Signal_O10_no";
            Signal_O11.yes.Click += SignalClick;
            Signal_O11.yes.Tag = "Signal_O11_yes";
            Signal_O11.no.Click += SignalClick;
            Signal_O11.no.Tag = "Signal_O11_no";
            Signal_O12.yes.Click += SignalClick;
            Signal_O12.yes.Tag = "Signal_O12_yes";
            Signal_O12.no.Click += SignalClick;
            Signal_O12.no.Tag = "Signal_O12_no";
            Signal_O13.yes.Click += SignalClick;
            Signal_O13.yes.Tag = "Signal_O13_yes";
            Signal_O13.no.Click += SignalClick;
            Signal_O13.no.Tag = "Signal_O13_no";
            Signal_O14.yes.Click += SignalClick;
            Signal_O14.yes.Tag = "Signal_O14_yes";
            Signal_O14.no.Click += SignalClick;
            Signal_O14.no.Tag = "Signal_O14_no";
            Signal_O15.yes.Click += SignalClick;
            Signal_O15.yes.Tag = "Signal_O15_yes";
            Signal_O15.no.Click += SignalClick;
            Signal_O15.no.Tag = "Signal_O15_no";
            timer.Elapsed += Timer_Elapsed;
        }
        /// <summary>
        /// 点击IO信号按钮的处理事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void SignalClick(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            //switch (btn.Tag)
            //{
            //    case "Signal_O0_yes":
            //        AutoRun.I.Bits_Out[0] =false;
            //        break;
            //    case "Signal_O0_no":
            //        AutoRun.I.Bits_Out[0]=true;
            //        break;
            //    case "Signal_O1_yes":
            //        AutoRun.I.Bits_Out[1]=false;
            //        break;
            //    case "Signal_O1_no":
            //        AutoRun.I.Bits_Out[1]=true;
            //        break;
            //    case "Signal_O2_yes":
            //        AutoRun.I.Bits_Out[2]=false;
            //        break;
            //    case "Signal_O2_no":
            //        AutoRun.I.Bits_Out[2]=true;
            //        break;
            //    case "Signal_O3_yes":
            //        AutoRun.I.Bits_Out[3]=false;
            //        break;
            //    case "Signal_O3_no":
            //        AutoRun.I.Bits_Out[3]=true;
            //        break;
            //    case "Signal_O4_yes":
            //        AutoRun.I.Bits_Out[4]=false;
            //        break;
            //    case "Signal_O4_no":
            //        AutoRun.I.Bits_Out[4]=true;
            //        break;
            //    case "Signal_O5_yes":
            //        AutoRun.I.Bits_Out[5]=false;
            //        break;
            //    case "Signal_O5_no":
            //        AutoRun.I.Bits_Out[5]=true;
            //        break;
            //    case "Signal_O6_yes":
            //        AutoRun.I.Bits_Out[6]=false;
            //        break;
            //    case "Signal_O6_no":
            //        AutoRun.I.Bits_Out[6]=true;
            //        break;
            //    case "Signal_O7_yes":
            //        AutoRun.I.Bits_Out[7]=false;
            //        break;
            //    case "Signal_O7_no":
            //        AutoRun.I.Bits_Out[7]=true;
            //        break;
            //    case "Signal_O8_yes":
            //        AutoRun.I.Bits_Out[8]=false;
            //        break;
            //    case "Signal_O8_no":
            //        AutoRun.I.Bits_Out[8]=true;
            //        break;
            //    case "Signal_O9_yes":
            //        AutoRun.I.Bits_Out[9]=false;
            //        break;
            //    case "Signal_O9_no":
            //        AutoRun.I.Bits_Out[9]=true;
            //        break;
            //    case "Signal_O10_yes":
            //        AutoRun.I.Bits_Out[10]=false;
            //        break;
            //    case "Signal_O10_no":
            //        AutoRun.I.Bits_Out[10]=true;
            //        break;
            //    case "Signal_O11_yes":
            //        AutoRun.I.Bits_Out[11]=false;
            //        break;
            //    case "Signal_O11_no":
            //        AutoRun.I.Bits_Out[11]=true;
            //        break;
            //    case "Signal_O12_yes":
            //        AutoRun.I.Bits_Out[12]=false;
            //        break;
            //    case "Signal_O12_no":
            //        AutoRun.I.Bits_Out[12]=true;
            //        break;
            //    case "Signal_O13_yes":
            //        AutoRun.I.Bits_Out[13]=false;
            //        break;
            //    case "Signal_O13_no":
            //        AutoRun.I.Bits_Out[13]=true;
            //        break;
            //    case "Signal_O14_yes":
            //        AutoRun.I.Bits_Out[14]=false;
            //        break;
            //    case "Signal_O14_no":
            //        AutoRun.I.Bits_Out[14]=true;
            //        break;
            //    case "Signal_O15_yes":
            //        AutoRun.I.Bits_Out[15]=false;
            //        break;
            //    case "Signal_O15_no":
            //        AutoRun.I.Bits_Out[15]=true;
            //        break;
            //}
        }
        /// <summary>
        /// IO信号的记录？？？
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                Rlog.Trace("Thread_ReadIO" + "[threadid:" + Thread.CurrentThread.ManagedThreadId + "]");
                Dispatcher.Invoke(() => {
                    //Signal_I0.Set(AutoRun.I.Bits_In[0]);
                    //Signal_I1.Set(AutoRun.I.Bits_In[1]);
                    //Signal_I2.Set(AutoRun.I.Bits_In[2]);
                    //Signal_I3.Set(AutoRun.I.Bits_In[3]);
                    //Signal_I4.Set(AutoRun.I.Bits_In[4]);
                    //Signal_I5.Set(AutoRun.I.Bits_In[5]);
                    //Signal_I6.Set(AutoRun.I.Bits_In[6]);
                    //Signal_I7.Set(AutoRun.I.Bits_In[7]);
                    //Signal_I8.Set(AutoRun.I.Bits_In[8]);
                    //Signal_I9.Set(AutoRun.I.Bits_In[9]);
                    //Signal_I10.Set(AutoRun.I.Bits_In[10]);
                    //Signal_I11.Set(AutoRun.I.Bits_In[11]);
                    //Signal_I12.Set(AutoRun.I.Bits_In[12]);
                    //Signal_I13.Set(AutoRun.I.Bits_In[13]);
                    //Signal_I14.Set(AutoRun.I.Bits_In[14]);
                    //Signal_I15.Set(AutoRun.I.Bits_In[15]);
                    //Signal_O0.Set(AutoRun.I.Bits_Out[0]);
                    //Signal_O1.Set(AutoRun.I.Bits_Out[1]);
                    //Signal_O2.Set(AutoRun.I.Bits_Out[2]);
                    //Signal_O3.Set(AutoRun.I.Bits_Out[3]);
                    //Signal_O4.Set(AutoRun.I.Bits_Out[4]);
                    //Signal_O5.Set(AutoRun.I.Bits_Out[5]);
                    //Signal_O6.Set(AutoRun.I.Bits_Out[6]);
                    //Signal_O7.Set(AutoRun.I.Bits_Out[7]);
                    //Signal_O8.Set(AutoRun.I.Bits_Out[8]);
                    //Signal_O9.Set(AutoRun.I.Bits_Out[9]);
                    //Signal_O10.Set(AutoRun.I.Bits_Out[10]);
                    //Signal_O11.Set(AutoRun.I.Bits_Out[11]);
                    //Signal_O12.Set(AutoRun.I.Bits_Out[12]);
                    //Signal_O13.Set(AutoRun.I.Bits_Out[13]);
                    //Signal_O14.Set(AutoRun.I.Bits_Out[14]);
                    //Signal_O15.Set(AutoRun.I.Bits_Out[15]);
                });
            }
            catch (Exception ex)
            {
                Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
        
        /// <summary>
        /// 开始点击的效果处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Start_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            timer.Start();
            btn_Start.IsVisible = false;
            btn_Stop.IsVisible = true;
        }

        /// <summary>
        /// 停止点击的效果处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Stop_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            timer.Stop();
            btn_Start.IsVisible = true;
            btn_Stop.IsVisible = false;
        }
        /// <summary>
        /// 登出点击的效果处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Logout_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            Main.Logout();
        }

        /// <summary>
        /// 调试点击的效果处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Debug_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            //AutoRun.I.Sync = 0;
        }


    }
}
