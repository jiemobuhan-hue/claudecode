 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ZenergyBFSI.Model;
using static ZenergyBFSI.Model.InspectionUtils;
using DevExpress.Xpf.Charts;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using ZenergyBFSI.Service;

namespace ZenergyBFSI.View.StateCards
{
    ///// <summary>
    ///// UC_StatesCards.xaml 的交互逻辑
    ///// </summary>
    //public partial class UC_StatesCards : UserControl
    //{
    //    private readonly DispatcherTimer _clockTimer;

    //    public UC_StatesCards()
    //    {
    //        InitializeComponent();

    //        // 初始化时钟
    //        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    //        _clockTimer.Tick += (s, e) => TxtClock.Text = DateTime.Now.ToString("HH:mm:ss");
    //        _clockTimer.Start();

    //        // 设置今日日期
    //        DpDate.SelectedDate = DateTime.Today;

    //        // TODO: 绑定 RefreshButton Click 事件，调用你的数据加载逻辑
    //        BtnRefresh.Click += (s, e) => LoadData();

    //        // TODO: 初始加载
    //        LoadData();
    //    }

    //    // ── 你只需要填充这两个方法 ─────────────────────────────────

    //    /// <summary>加载看板数据，更新所有命名控件</summary>
    //    private void LoadData()
    //    {
    //        // 示例：直接操作命名元素（也可改为 ViewModel 绑定）
    //        // TxtTotal.Text = data.Total.ToString();
    //        // TxtOk.Text    = data.Ok.ToString();
    //        // TxtNg.Text    = data.Ng.ToString();
    //        // TxtRate.Text  = $"{data.YieldRate:F2}%";
    //        // DgRecords.ItemsSource = data.Records;
    //        // UpdateStatusLight(data.LastResult);
    //    }

    //    /// <summary>更新状态大灯颜色与文字</summary>
    //    public void UpdateStatusLight(string result)
    //    {
    //        // 根据 result ("OK"/"NG") 修改 StatusLight.Fill / StatusGlow.Stroke 颜色
    //        // 以及 TxtStatus.Text / TxtLastCellCode.Text / TxtLastTime.Text
    //    }


    //    public UC_StatesCards(CsvDataService csv, Func<string, Task> pushJs)
    //    {
    //        InitializeComponent();
    //        _csv = csv;
    //        _pushJs = pushJs;
    //        Loaded += OnLoaded;
    //        Unloaded += OnUnloaded;
    //    }

    //    private readonly CsvDataService _csv;
    //    private readonly Func<string, Task> _pushJs;   // 封装 ExecuteScriptAsync

    //    private DateTime _date = DateTime.Today;
    //    private string _shift = "all";

    //    private DispatcherTimer _dashTimer = null;
    //    private DispatcherTimer _liveTimer = null;

    //    // ── 生命周期 ──────────────────────────────────────────────
    //    private void OnLoaded(object sender, RoutedEventArgs e)
    //    {
    //        _dashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
    //        _dashTimer.Tick += async (_, __) => await RefreshDashAsync();
    //        _dashTimer.Start();

    //        _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
    //        _liveTimer.Tick += async (_, __) => await RefreshLiveAsync();
    //        _liveTimer.Start();
    //    } 
    //    private void OnUnloaded(object sender, RoutedEventArgs e)
    //    {
    //        _dashTimer?.Stop();
    //        _liveTimer?.Stop();
    //    }

    //    // ── 接收 JS PostMessage（由 MainWindow 路由过来）────────────
    //    public async Task OnWebMessage(string type, string payload)
    //    {
    //        switch (type)
    //        {
    //            case "page_ready":
    //                // JS 已初始化完毕，立即推送一次完整数据
    //                await RefreshDashAsync();
    //                await RefreshLiveAsync();
    //                break;

    //            case "refresh":
    //                await RefreshDashAsync();
    //                await RefreshLiveAsync();
    //                break;

    //            case "date_changed":
    //                if (DateTime.TryParse(payload, out var d))
    //                {
    //                    _date = d;
    //                    await RefreshDashAsync();
    //                }
    //                break;

    //            case "shift_changed":        // payload: "all"|"A"|"B"|"C"
    //                _shift = payload;
    //                await RefreshDashAsync();
    //                break;
    //        }
    //    }

    //    // ── 刷新看板 ─────────────────────────────────────────────
    //    private async Task RefreshDashAsync()
    //    {
    //        try
    //        {
    //            // 最简写法：CsvDataService 直接返回 JSON 字符串，无需二次序列化
    //            string json = _csv.GetDashboardSummaryJson(_date);

    //            // 若需要班次过滤，对 DTO 做处理后再序列化：
    //            // var dto = FilterByShift(json, _shift);
    //            // json = JsonSerializer.Serialize(dto);

    //            // 调用 JS 全局函数 window.updateDashboard(json)
    //            await _pushJs($"updateDashboard({json})");
    //        }
    //        catch (Exception ex)
    //        {
    //            System.Diagnostics.Debug.WriteLine($"[Dashboard] Dash error: {ex.Message}");
    //        }
    //    }

    //    // ── 刷新状态灯 ────────────────────────────────────────────
    //    private async Task RefreshLiveAsync()
    //    {
    //        try
    //        {
    //            string json = _csv.GetLiveStatusJson();
    //            await _pushJs($"updateLiveStatus({json})");
    //        }
    //        catch (Exception ex)
    //        {
    //            System.Diagnostics.Debug.WriteLine($"[Dashboard] Live error: {ex.Message}");
    //        }
    //    }

    //    // ── 班次过滤示例（可选）──────────────────────────────────
    //    private static DashboardDto FilterByShift(string rawJson, string shift)
    //    {
    //        var dto = JsonSerializer.Deserialize<DashboardDto>(rawJson,
    //            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    //        if (shift == "all" || dto.Hourly == null) return dto;
    //        var (hFrom, hTo) = (0, 23);
    //        switch (shift)
    //        {
    //            case "A": (hFrom, hTo) = (0, 7);break;
    //            case "B": (hFrom, hTo) = (8, 15); break;
    //            case "C": (hFrom, hTo) = (16, 23); break;
    //        }
    //        //var (hFrom, hTo) = shift switch
    //        //{
    //        //    "A" => (0, 7),
    //        //    "B" => (8, 15),
    //        //    "C" => (16, 23),
    //        //    _ => (0, 23)
    //        //};

    //        dto.Hourly = dto.Hourly.FindAll(h =>
    //            int.TryParse(h.Hour?.Split(':')[0], out int hh) && hh >= hFrom && hh <= hTo);

    //        dto.Ok = dto.Hourly.Sum(h => h.Ok);
    //        dto.Ng = dto.Hourly.Sum(h => h.Ng);
    //        dto.Total = dto.Ok + dto.Ng;
    //        dto.YieldRate = dto.Total > 0 ? Math.Round(dto.Ok * 100.0 / dto.Total, 2) : 0;
    //        return dto;
    //    }

    //    // ── 外部触发：如 PLC 信号来了直接推 ─────────────────────
    //    /// <summary>外部调用：强制立即刷新状态灯（如 PLC 触发）</summary>
    //    public async Task ForceRefreshLive() => await RefreshLiveAsync();

    //    /// <summary>外部调用：强制立即刷新完整看板</summary>
    //    public async Task ForceRefreshDash() => await RefreshDashAsync();
    //}  

    /// <summary>
    /// UC_StatesCards.xaml 的交互逻辑
    /// </summary>
    public partial class UC_StatesCards : UserControl
    {
        // ════════════════════════════════════════════════════════
        //  公开 API
        // ════════════════════════════════════════════════════════

        /// <summary>刷新按钮 / 班次切换 / 日期变更时触发</summary>
        public event EventHandler RefreshRequested;

        /// <summary>日期选择器变更</summary>
        public event EventHandler<DateTime> DateChanged;

        /// <summary>班次切换，参数 "all"|"A"|"B"|"C"</summary>
        public event EventHandler<string> ShiftChanged;

        // ── 推送看板数据（外部调用）──────────────────────────────
        /// <summary>更新全部看板数据（KPI、图表、记录）</summary>
        public void UpdateDashboard(DashboardData data)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _lastData = data;

                // 分页更新：从 data 中获取总页数（需 DashboardData 提供 TotalPages 字段）
                // 暂时使用记录数估算，后续应从 data.TotalPages 获取
                int pageSize = 20; // 每页记录数，需与后端保持一致
                int totalRecords = data.Recent?.Count ?? 0;
                _totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
                _currentPage = 0;

                ApplyKpi(data);
                ApplyNgTypes(data.NgTypes);
                ApplyRecords(data.Recent);

                RedrawHourly();   // 依赖画布尺寸，延迟触发
            });
        }

        /// <summary>更新产线状态大灯</summary>
        public void UpdateStatusLight(string result, string cellCode, string time)
        {
            Dispatcher.InvokeAsync(() => ApplyStatusLight(result, cellCode, time));
        }

        /// <summary>返回当前选中日期（可为 null）</summary>
        public DateTime? SelectedDate => DpDate.SelectedDate;

        /// <summary>返回当前选中班次</summary>
        public string SelectedShift => _currentShift;

        // ════════════════════════════════════════════════════════
        //  内部状态
        // ════════════════════════════════════════════════════════
        private string _currentShift = "all";
        private DashboardData _lastData;
        private List<HourlyData> _hourlyData = new List<HourlyData>();
        private Storyboard _pulseStory;
        private DispatcherTimer _clockTimer = null;
        private int _currentPage = 0;
        private int _totalPages = 0;

        // ── NG 类型条形图宽度计算辅助类 ─────────────────────────
        private class NgBarItem
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
            public double BarWidth { get; set; }  // 像素宽，由 code-behind 算
            // 提供给 DataGrid NG 类型列的字符串列表
            public List<string> NgTypeList { get; set; } = new List<string>();
        }

        // DataGrid 行的包装，添加 NgTypeList 属性
        private class RecordRow
        {
            public string CellCode { get; set; } = "";
            public string DateTime { get; set; } = "";
            public string StationId { get; set; } = "";
            public string OverallResult { get; set; } = "";
            public int ProcessMs { get; set; }
            public List<string> NgTypeList { get; set; } = new List<string>();
            public bool IsInbound { get; set; }  // true=进站, false=出站
        }

        // ════════════════════════════════════════════════════════
        //  构造 & 初始化
        // ════════════════════════════════════════════════════════
        public UC_StatesCards()
        {

            InitializeComponent();

            DpDate.SelectedDate = DateTime.Today;

            //StartClock();
            Loaded += (_, __) =>
            {
                // 订阅看板消息（在 UC_Home 之后，确保消息链路建立）
                Messenger.Default.Register<DashboardUpdateMessage>(this, OnDashboardUpdateMessage);
                Messenger.Default.Register<StatusLightUpdateMessage>(this, OnStatusLightUpdateMessage);

                // 启动模拟（构造函数中调用时 UC_Home.OnLoaded 还未执行，消息订阅未建立）
                DashboardService.I.StartSimulation(10);

                RedrawHourly();
            };
            DataContext = this;
        }

        private void OnDashboardUpdateMessage(DashboardUpdateMessage msg)
        {
            UpdateDashboard(msg.Data);
        }

        private void OnStatusLightUpdateMessage(StatusLightUpdateMessage msg)
        {
            UpdateStatusLight(msg.Result, msg.CellCode, msg.Time);
        }

 

        private void StartClock()
        {
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, __) =>
                TxtClock.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
        }

        // ════════════════════════════════════════════════════════
        //  KPI 更新
        // ════════════════════════════════════════════════════════
        private void ApplyKpi(DashboardData d)
        {
            TxtTotal.Text = d.Total.ToString();
            TxtOk.Text = d.Ok.ToString();
            TxtNg.Text = d.Ng.ToString();
            TxtRate.Text = $"{d.YieldRate:F2}%";

            TxtTotalSub.Text = $"{(_currentShift == "all" ? "全天" : _currentShift + "班")}累计";
            TxtOkSub.Text = d.Total > 0 ? $"占比 {d.Ok * 100.0 / d.Total:F1}%" : "--";
            TxtNgSub.Text = d.Total > 0 ? $"占比 {d.Ng * 100.0 / d.Total:F1}%" : "--";
            TxtRateSub.Text = d.YieldRate >= 99.5 ? "✓ 达标 (目标 ≥99.5%)" : "✗ 未达标 (目标 ≥99.5%)";
            TxtRecordCount.Text = $"第 {_currentPage + 1}/{_totalPages} 页 共 {d.Recent.Count} 条";

            _hourlyData = d.Hourly;
        }

        // ════════════════════════════════════════════════════════
        //  时段产量 Canvas 绘图
        // ════════════════════════════════════════════════════════
        //private void HourlyCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawHourly();
        //public class HourlyChartsViewModel
        //{
        //    public int[] Values1 { get; set; } = new int[] { 4, 7, 1 };
        //    public int[] Values2 { get; set; } = new int[] { 3, 2, 1 };
        //    public int[] Values3 { get; set; } = new int[] { 4, 6, 6 };
        //    public int[] Values4 { get; set; } = new int[] { 3, 7, 9 };
        //    public string[] Labels { get; set; } = new string[] { "时段一", "时段二", "时段三" };

        //}


        private void RedrawHourly()
        {
            // 从 _hourlyData 动态绑定到 ChartControl
            if (_hourlyData == null || _hourlyData.Count == 0) return;

            var diagram = NgHourlyChart?.Diagram as XYDiagram2D;
            if (diagram == null) return;

            // 清除现有 series
            diagram.Series.Clear();

            // 构建 OK 和 NG 两个 series
            var okSeries = new BarStackedSeries2D
            {
                DisplayName = "OK 产量",
                Brush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                LabelsVisibility = true
            };
            var ngSeries = new BarStackedSeries2D
            {
                DisplayName = "NG 产量",
                Brush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                LabelsVisibility = true
            };

            foreach (var h in _hourlyData)
            {
                okSeries.Points.Add(new SeriesPoint(h.Hour, h.Ok));
                ngSeries.Points.Add(new SeriesPoint(h.Hour, h.Ng));
            }

            diagram.Series.Add(okSeries);
            diagram.Series.Add(ngSeries);
        }

        private void ApplyNgTypes(List<NgTypeData> types)
        {
            // 动态绑定 NG 饼图
            if (types == null || types.Count == 0)
            {
                NgPieSeries.Points.Clear();
                return;
            }

            NgPieSeries.Points.Clear();
            foreach (var t in types)
            {
                NgPieSeries.Points.Add(new SeriesPoint(t.Name, t.Count));
            }
        }

        // ════════════════════════════════════════════════════════
        //  最近记录 DataGrid
        // ════════════════════════════════════════════════════════
        private List<RecordRow> _allRows = new List<RecordRow>();

        private void ApplyRecords(List<RecentRecord> records)
        {
            _allRows = records.Select(r => new RecordRow
            {
                CellCode = r.CellCode,
                DateTime = r.DateTime,
                StationId = r.StationId,
                OverallResult = r.OverallResult,
                ProcessMs = r.ProcessMs,
                NgTypeList = (r.NgTypes ?? "")
                .Split('|').ToList(),
                IsInbound = r.IsInbound
            }).ToList();

            ApplyNgFilter();
        }

        private void ApplyNgFilter()
        {
            DgRecords.ItemsSource = TglNgOnly.IsChecked == true
                ? _allRows.Where(r => r.OverallResult == "NG").ToList()
                : _allRows;
        }

        // ════════════════════════════════════════════════════════
        //  状态大灯
        // ════════════════════════════════════════════════════════
        private void ApplyStatusLight(string result, string cellCode, string time)
        {
            _pulseStory?.Stop(EllGlow);

            TxtLightLabel.Text = result == "NONE" ? "--" : result;
            TxtLightCode.Text = cellCode ?? "--";
            TxtLightTime.Text = time ?? "--";

            Color lightColor, glowColor, glowStroke;

            if (result == "OK")
            {
                lightColor = Color.FromRgb(0x4C, 0xAF, 0x50);
                glowColor = Color.FromRgb(0x66, 0xBB, 0x6A);
                glowStroke = Color.FromRgb(0x4C, 0xAF, 0x50);
                TxtLightLabel.Foreground = Brushes.White;
            }
            else if (result == "NG")
            {
                lightColor = Color.FromRgb(0xF4, 0x43, 0x36);
                glowColor = Color.FromRgb(0xFF, 0x52, 0x52);
                glowStroke = Color.FromRgb(0xF4, 0x43, 0x36);
                TxtLightLabel.Foreground = Brushes.White;
            }
            else
            {
                lightColor = glowColor = Colors.Gray;
                glowStroke = Colors.Gray;
                TxtLightLabel.Foreground = Brushes.White;
                return;
            }

            // 更新渐变和光晕颜色
            var brush = new RadialGradientBrush(glowColor, lightColor);
            EllLight.Fill = brush;
            EllGlow.Stroke = new SolidColorBrush(glowStroke);
            LightGlow.Color = lightColor;

            // 脉冲动画
            var duration = result == "NG"
                ? new Duration(TimeSpan.FromSeconds(0.6))
                : new Duration(TimeSpan.FromSeconds(1.2));

            var anim = new DoubleAnimation(0.3, 1.0, duration)
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase()
            };

            _pulseStory = new Storyboard();
            Storyboard.SetTarget(_pulseStory, EllGlow);
            Storyboard.SetTargetProperty(_pulseStory,
                new PropertyPath(UIElement.OpacityProperty));
            _pulseStory.Children.Add(anim);
            _pulseStory.Begin(EllGlow, true);
        }

        // ════════════════════════════════════════════════════════
        //  事件处理
        // ════════════════════════════════════════════════════════
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => RefreshRequested?.Invoke(this, EventArgs.Empty);

        private void DpDate_SelectedDateChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (DpDate.SelectedDate.HasValue)
                DateChanged?.Invoke(this, DpDate.SelectedDate.Value);
        }

        private void ShiftRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _currentShift = tag;
                ShiftChanged?.Invoke(this, tag);
            }
        }

        private void TglNgOnly_Checked(object sender, RoutedEventArgs e)
            => ApplyNgFilter();

        // ════════════════════════════════════════════════════════
        //  分页按钮事件处理
        // ════════════════════════════════════════════════════════
        private void BtnFirstPage_Click(object sender, RoutedEventArgs e)
        {
            DashboardService.I.SetPage(0);
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 0)
            {
                DashboardService.I.SetPage(_currentPage - 1);
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages - 1)
            {
                DashboardService.I.SetPage(_currentPage + 1);
            }
        }

        private void BtnLastPage_Click(object sender, RoutedEventArgs e)
        {
            DashboardService.I.SetPage(_totalPages - 1);
        }
    }
}
