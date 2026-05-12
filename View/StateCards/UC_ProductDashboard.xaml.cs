using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace ZenergyBFSI.View.StateCards
{
    /// <summary>
    /// UC_ProductDashboard.xaml 的交互逻辑
    /// </summary>
    public partial class UC_ProductDashboard : UserControl
    {
        public UC_ProductDashboard()
        {
            InitializeComponent();
        }
    }

    public class UC_ProductDashboardViewModel : BindableBase
    {
        public UC_ProductDashboardViewModel()
        {
            // 初始化模拟数据
            LoadDashboardData();

            // 初始化命令
            SwitchTrendCommand = new DelegateCommand<string>(OnSwitchTrend);
        }

        #region 属性 (Properties)

        private string _totalOutput = "0";
        public string TotalOutput
        {
            get => _totalOutput; set =>  
            SetProperty(ref _totalOutput, value, nameof(TotalOutput));
        }
        
        private string _yieldRate = "0.00%";
        public string YieldRate
        {
            get => _yieldRate;
            set => SetProperty(ref _yieldRate, value, nameof(YieldRate));
        }

        // 产量指标列表（对应你 UI 左侧的 UniformGrid）
        private ObservableCollection<ProductionItem> _productionStats;
        public ObservableCollection<ProductionItem> ProductionStats
        {
            get => _productionStats;
            set => SetProperty(ref _productionStats, value, nameof(TotalOutput));
        }

        #endregion

        #region 命令 (Commands)

        public DelegateCommand<string> SwitchTrendCommand { get; private set; }

        private void OnSwitchTrend(string trendType)
        {
            // 根据点击切换折线图逻辑
            // 例如：UpdateChartData(trendType);
        }

        #endregion

        #region 私有方法

        private void LoadDashboardData()
        {
            // 模拟从数据库或 PLC 读取数据
            TotalOutput = "67,381";
            YieldRate = "98.5%";

            ProductionStats = new ObservableCollection<ProductionItem>
            {
                new ProductionItem { Title = "总产量", Value = "67,381", Trend = "9.42% ⬆️", IsTrendUp = true },
                new ProductionItem { Title = "NG类型一", Value = "1,530", Trend = "8.22% ⬇️", IsTrendUp = false },
                new ProductionItem { Title = "产品类型二", Value = "65,959", Trend = "10.54% ⬆️", IsTrendUp = true },
                new ProductionItem { Title = "产品类型三", Value = "42,100", Trend = "2.1% ⬆️", IsTrendUp = true },
                new ProductionItem { Title = "产品类型四", Value = "23,859", Trend = "1.5% ⬇️", IsTrendUp = false }
            };
        }

        #endregion
    }
}
