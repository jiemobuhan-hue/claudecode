using DevExpress.Mvvm;
using RinKit;
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
using ZenergyBFSI.Model;

namespace ZenergyBFSI.View
{
    /// <summary>
    /// UC_Production.xaml 的交互逻辑
    /// </summary>
    public partial class UC_Production : UserControl
    {
        ProductionVM _vm;
        public UC_Production()
        {
            InitializeComponent();
            _vm = new ProductionVM() { StartTime = DateTime.Today.AddDays(-7), EndTime = DateTime.Today.AddDays(1) };
            DataContext = _vm;
        }

        public void DataSearch()
        {
            //try
            //{
            //    var startT = DataHelper.DateToTimeStamp(_vm.StartTime, 86400);
            //    var endT = DataHelper.DateToTimeStamp(_vm.EndTime, 86400);
            //    Rdb.SelectList(out List<CellData> list, $"Select * From CellData Where TimeStamp>={startT} AND TimeStamp<{endT} AND 二注结束=1 ORDER BY Id DESC LIMIT 100000");
            //    if (list != null)
            //    {
            //        var start = list.FirstOrDefault();
            //        var end = list.LastOrDefault();
            //        List<ProductionInfo> vlist = new List<ProductionInfo>();
            //        foreach (var data in list)
            //        {
            //            if (DateTime.TryParse(data.进站时间, out DateTime date))
            //            {
            //                var shift = "";
            //                if (date.Hour < 8)
            //                {
            //                    shift += $"{date.Year}-{date.Month}-{date.Day - 1}夜";
            //                }
            //                else if (date.Hour >= 8 && date.Hour <= 20)
            //                {
            //                    shift += $"{date.Year}-{date.Month}-{date.Day}白";
            //                }
            //                else
            //                {
            //                    shift += $"{date.Year}-{date.Month}-{date.Day}夜";
            //                }
            //                ProductionInfo item = vlist.Where(i => i.班次 == shift).FirstOrDefault();
            //                if (item == null)
            //                {
            //                    item = new ProductionInfo() { 班次 = shift };
            //                    vlist.Add(item);
            //                }
            //                item.总产量++;
            //                //if (date.Hour == 8 || date.Hour == 9 || date.Hour == 20 || date.Hour == 21) item.产量1++;
            //                //if (date.Hour == 10 || date.Hour == 11 || date.Hour == 22 || date.Hour == 23) item.产量2++;
            //                //if (date.Hour == 12 || date.Hour == 13 || date.Hour == 0 || date.Hour == 1) item.产量3++;
            //                //if (date.Hour == 14 || date.Hour == 15 || date.Hour == 2 || date.Hour == 3) item.产量4++;
            //                //if (date.Hour == 16 || date.Hour == 17 || date.Hour == 4 || date.Hour == 5) item.产量5++;
            //                //if (date.Hour == 18 || date.Hour == 19 || date.Hour == 6 || date.Hour == 7) item.产量6++;
            //                if (date.Hour == 8 || date.Hour == 20) item.产量1++;
            //                if (date.Hour == 9 || date.Hour == 21) item.产量2++;
            //                if (date.Hour == 10 || date.Hour == 22) item.产量3++;
            //                if (date.Hour == 11 || date.Hour == 23) item.产量4++;
            //                if (date.Hour == 12 || date.Hour == 0) item.产量5++;
            //                if (date.Hour == 13 || date.Hour == 1) item.产量6++;
            //                if (date.Hour == 14 || date.Hour == 2) item.产量7++;
            //                if (date.Hour == 15 || date.Hour == 3) item.产量8++;
            //                if (date.Hour == 16 || date.Hour == 4) item.产量9++;
            //                if (date.Hour == 17 || date.Hour == 5) item.产量10++;
            //                if (date.Hour == 18 || date.Hour == 6) item.产量11++;
            //                if (date.Hour == 19 || date.Hour == 7) item.产量12++;
            //                if (data.入站结果 == "NG") { item.入站NG++; item.总NG++; continue; }
            //                if (data.拔钉结果 == "NG") { item.拔钉NG++; item.总NG++; continue; }
            //                if (data.前称重结果 == "NG") { item.前称重NG++; item.总NG++; continue; }
            //                if (data.真空检测结果 == "NG") { item.真空NG++; item.总NG++; continue; }
            //                if (data.后称重结果 == "NG") { item.后称重NG++; item.总NG++; continue; }
            //                if (data.胶钉检测结果 == "NG") { item.胶钉NG++; item.总NG++; continue; }
            //            }
            //            else
            //            {
            //                Rlog.Error("进站时间异常!");
            //            }
            //            foreach (var item in vlist)
            //            {
            //                var rate = (float)item.总NG / item.总产量;
            //                item.优率 = rate > 0 ? ((1 - rate) * 100).ToString("f2") + "%" : "100%";
            //            }
            //        }
            //        _vm.InfoList = vlist;
            //    }
            //    //Rdata.SelectData($@"Select * From OCV Where TimeStamp > '{Handler.DateToTS(input_StartTime)}' and TimeStamp < '{Handler.DateToTS(input_EndTime)}' Order by Id Desc", out DataTable dt);
            //    //gridControl.ItemsSource = dt;
            //}
            //catch (Exception ex)
            //{
            //    Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
            //}
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {

        }
        private void Search_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            DataSearch();
        }

        private void SetTS_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            try
            {
                //Rdata.SelectData($@"Select * From OCV", out DataTable dt);
                //foreach (DataRow row in dt.Rows)
                //{
                //    //long time = (Convert.ToDateTime(row["Time"]).Ticks - 621355968000000000L) / 10000000 - 28800;
                //    Rdata.Execute($"UPDATE OCV Set TimeStamp={Handler.DateToTS(row["Time"].ToString())} Where Id={row["Id"]}");
                //}
            }
            catch (Exception ex)
            {
                Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

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

        private void Print_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            FrameworkElement fe = new FrameworkElement();
            gridView.ShowPrintPreview(fe);
        }

        private void Logout_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            Main.Logout();
        }

    }

    internal class ProductionVM : ViewModelBase
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

        public List<ProductionInfo> InfoList
        {
            get { return GetValue<List<ProductionInfo>>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("InfoList");
                }
            }
        }

    }

    internal class ProductionInfo
    {
        public string 班次 { get; set; } = "";
        public string 优率 { get; set; } = "";
        public int 总产量 { get; set; } = 0;
        //public int 产量1 { get; set; } = 0;
        //public int 产量2 { get; set; } = 0;
        //public int 产量3 { get; set; } = 0;
        //public int 产量4 { get; set; } = 0;
        //public int 产量5 { get; set; } = 0;
        //public int 产量6 { get; set; } = 0;
        //public int 产量7 { get; set; } = 0;
        //public int 产量8 { get; set; } = 0;
        //public int 产量9 { get; set; } = 0;
        //public int 产量10 { get; set; } = 0;
        //public int 产量11 { get; set; } = 0;
        //public int 产量12 { get; set; } = 0;
        //public int 总NG { get; set; } = 0;
        //public int 入站NG { get; set; } = 0;
        //public int 拔钉NG { get; set; } = 0;
        //public int 前称重NG { get; set; } = 0;
        //public int 真空NG { get; set; } = 0;
        //public int 后称重NG { get; set; } = 0;
        //public int 胶钉NG { get; set; } = 0;
    }
}
