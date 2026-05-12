using DevExpress.Mvvm;
using RinKit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
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
using ZenergyBFSI.Model;
using ZenergyBFSI.Properties;
using ZenergyBFSI.Service;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// UC_History.xaml 的交互逻辑
    /// 该控件处理软件日志相关的逻辑，负责展示、筛选等，依托DEV的table为原型
    /// </summary>
    public partial class UC_History : UserControl
    {
        #region 视图内部变量
        HistoryVM _vm;
        #endregion
        public UC_History()
        {
            InitializeComponent();
            _vm = new HistoryVM() { StartTime = DateTime.Today.AddDays(-7), EndTime = DateTime.Today.AddDays(1) };
            DataContext = _vm;

            #region 初始化
            //// 使用示例
            //SQLiteGenericHelper.DropTable( "CellData");
            //int randomNumber = _threadLocalRandom.Value.Next(1, 10000);
            ////创建100个示例数据
            //var list = new List<CellData>();
            //for (int i = 0; i < 100; i++)
            //{
            //    list.Add(new CellData
            //    {
            //        Id = i,
            //        电芯码 = _threadLocalRandom.Value.Next(1, 10000).ToString(),
            //        MOM出站结果 = "OK",
            //        MOM查询来料状态 = "OK",
            //        Ng类型1 = "OK",
            //        Ng类型2 = "OK",
            //        Ng类型3 = "OK",
            //        Ng类型4 = "OK",
            //        Ng类型5 = "OK",   
            //        Ng类型6 = "OK",   
            //        Ng类型7 = "OK",
            //        Ng类型8 = "OK",
            //        Ng类型数量 = 0,
            //        是否复投 = false,
            //        出站时间 = "暂定",
            //        人工复判次数 = 0,
            //        视觉检测结果 = "暂定",
            //        检验位置 = "暂定",
            //        入站结果 = "暂定",
            //        出站结果 = "暂定", 
            //    }
            //    );
            //}
            //SQLiteGenericHelper.SaveListToDb<CellData>(list, "CellData");

            #endregion  
        }
        #region 初始化
        //private static readonly ThreadLocal<Random> 
        //    _threadLocalRandom =
        //new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));
        #endregion
        /// <summary>
        /// 日志页面的加载动作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.StartTime = DateTime.Today.AddDays(-7);
            _vm.EndTime = DateTime.Today.AddDays(1);
            DataSearch_Show();
        }
        /// <summary>
        /// 查询操作操作
        /// </summary>
        public void DataSearch_Show()
        {
            //throw new NotImplementedException();
            try
            {
                var start = DataHelper.DateToTimeStamp(_vm.StartTime, 86400);
                var end = DataHelper.DateToTimeStamp(_vm.EndTime, 86400);
                //参考下方操作获取数据
                Rdb.SelectList(out List<CellData> list, $"Select * From CellData Where TimeStamp>={start} AND TimeStamp<{end} ORDER BY Id DESC LIMIT 100000");//History
                if (list != null)
                {
                    ObservableCollection<CellData> vlist = new ObservableCollection<CellData>(list);
 
                    _vm.InfoList = vlist;
                }
                //Rdata.SelectData($@"Select * From OCV Where TimeStamp > '{Handler.DateToTS(input_StartTime)}' and TimeStamp < '{Handler.DateToTS(input_EndTime)}' Order by Id Desc", out DataTable dt);
                //gridControl.ItemsSource = dt;
            }
            catch (Exception ex)
            {
                Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
            }
        } 
        /// <summary>
        /// 运行日志记录相关的页面视图数据数据模型，重点关注InfoList用于DEV表格渲染
        /// </summary>
        internal class HistoryVM : ViewModelBase
        {
            public DateTime StartTime
            {
                get { return GetValue<DateTime>(); }
                set
                {
                    if (SetValue(value))
                    {
                        RaisePropertyChanged("StartTime");
                    }
                }
            }
            public DateTime EndTime
            {
                get { return GetValue<DateTime>(); }
                set
                {
                    if (SetValue(value))
                    {
                        RaisePropertyChanged("EndTime");
                    }
                }
            }
            public ObservableCollection<CellData> InfoList
            {
                get { return GetValue<ObservableCollection<CellData>>(); }
                set
                {
                    if (SetValue(value))
                    {
                        RaisePropertyChanged("InfoList");
                    }
                }
            }
        }

        /// <summary>
        /// 来料信息的实体类，根据不同项目有不同的数据结构要求
        /// </summary>
        internal class HistoryInfo
        {
            /// <summary>
            /// 构造函数按实际使用自行修改 需要考虑具体使用情况
            /// </summary>
            public HistoryInfo()
            {

            }
            public int 电芯记录数量 { get; set; }
            public string 搜索开始日期 { get; set; }
            public string 搜索结束日期 { get; set; }
        }
        /// <summary>
        /// Ribbon菜单栏查询操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Search_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            DataSearch_Show();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetTS_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            //未确定操作、跟配置参数有关
            //try
            //{
            //    if (Settings.Power > 99) SetTime();
            //}
            //catch (Exception ex)
            //{
            //    Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
            //}
        }
        private void SetTime()
        {
            //try
            //{
            //    Rdb.SelectList(out List<CellData> list, "Select * From CellData");
            //    foreach (var data in list)
            //    {
            //        data.TimeStamp = DataHelper.DateToTimeStamp(data.进站时间);
            //        Rdb.ChangeRow(data, "TimeStamp");
            //    }
            //    Settings.Power = 7;
            //}
            //catch (Exception ex)
            //{
            //    UC_Operation.I.WriteLog($"SetTime异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
            //}
        }

        /// <summary>
        /// 登出操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Logout_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            Main.Logout();
        }

        /// <summary>
        /// 打印操作 调用WPF内部给dev实现打印
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Print_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            FrameworkElement fe = new FrameworkElement();
            gridView.ShowPrintPreview(fe);
        }
        /// <summary>
        /// 导出操作涉及到文件IO流操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Export_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            try
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.Filter = "CSV|*.csv";
                if (dlg.ShowDialog() != true)
                    return;
                gridView.ExportToCsv(dlg.FileName);
                MessageBox.Show("导出成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("系统错误！");
                Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
    }
}
