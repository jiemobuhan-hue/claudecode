
using System;
using System.Collections.Generic;
using System.Linq;
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
using ZenergyBFSI.Model.Messages;
using DevExpress.Mvvm;

namespace ZenergyBFSI.View.StateCards
{
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
            Dispatcher.Invoke(() =>
            {
                _lastData = data;

                // Bug 5+6+7 修复：
                // 1. _totalPages 必须使用服务端返回的总记录数（data.TotalCount），
                //    而非当页记录数（data.Recent.Count ≤ PageSize = 500），否则永远算出 1 页。
                // 2. _currentPage 应跟随服务端回传的 PageIndex，
                //    而非无条件归零（归零会使翻页操作在下一次刷新后被撤销）。
                int pageSize = DashboardWorker.PageSize;
                int dbTotal = data.TotalCount;  // 数据库窗口内的真实总记录数
                _totalPages = Math.Max(1, (int)Math.Ceiling(dbTotal / (double)pageSize));
                _currentPage = data.PageIndex;  // 与后端保持同步，不强制归零

                ApplyKpi(data);
                ApplyNgTypes(data.NgTypes);
                ApplyRecords(data.Recent);

                RedrawHourly();
            });
        }

        /// <summary>更新产线状态大灯</summary>
        public void UpdateStatusLight(string result, string cellCode, string time)
        {
            Dispatcher.InvokeAsync(() => ApplyStatusLight(result, cellCode, time));
        }

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

            StartClock();
            Loaded += (_, __) =>
            {
                // Bug 4 修复：移除此处重复的 Messenger 订阅。
                // DashboardUpdateMessage / StatusLightUpdateMessage 已由 UC_Home.OnLoaded 订阅，
                // UC_Home 会直接调用 _dash.UpdateDashboard() / _dash.UpdateStatusLight()。
                // 若在此处重复注册，每条消息会触发 UpdateDashboard 两次，导致 _currentPage 被二次重置。
                // Messenger.Default.Register<DashboardUpdateMessage>(this, OnDashboardUpdateMessage);
                // Messenger.Default.Register<StatusLightUpdateMessage>(this, OnStatusLightUpdateMessage);

                //RedrawHourly();

                // 直接调用 UpdateDashboard 渲染测试数据（不依赖数据库）
                //DashboardWorkerTests.RunTests(this);
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
            {

                TxtClock.Text = DateTime.Now.ToString("HH:mm:ss");

                #region 测试数据
                var now = DateTime.Now;
                var windowStart = now.AddHours(-12);  // 与 QueryAndParse 的 windowStart 一致

                // Bug 9 修复：在循环内 new Random() 时，多个实例在同一毫秒内拥有相同 TickCount 种子，
                // 导致所有 12 个小时桶产生完全相同的 OK/NG 值，测试数据无意义。
                // 修复：使用单一 Random 实例贯穿整个构造过程。
                var rng = new Random();

                // 时段数据：12个小时桶，hour 值必须与 windowStart.Hour + i 对齐
                var hourly = new List<HourlyData>();
                for (int i = 0; i < 12; i++)
                {
                    int hour = (windowStart.Hour + i) % 24;
                    int ok = 30 + rng.Next(10);  // 每小时30-39条OK
                    int ng = 3 + rng.Next(4);    // 每小时3-6条NG
                    hourly.Add(new HourlyData
                    {
                        Hour = hour,  // "HH:00" 格式，与 ParseRecords 一致
                        Ok = ok,
                        Ng = ng
                    });
                }

                // KPI汇总：基于 hourly 总数，与 ParseRecords 逻辑一致
                int total = hourly.Sum(h => h.Ok + h.Ng);
                int okCount = hourly.Sum(h => h.Ok);
                int ngCount = hourly.Sum(h => h.Ng);
                double yieldRate = total > 0 ? okCount * 100.0 / total : 0;

                // NG类型数据：8种类型，模拟真实分布
                var ngTypes = new List<NgTypeData>
            {
                new NgTypeData { Name = "外观划伤", Count = 30 + rng.Next(10) },
                new NgTypeData { Name = "气泡",     Count = 32+ rng.Next(10) },
                new NgTypeData { Name = "色差",     Count = 18 + rng.Next(10)},
                new NgTypeData { Name = "变形",     Count = 12 + rng.Next(10)},
                new NgTypeData { Name = "污渍",     Count = 8+ rng.Next(10) },
                new NgTypeData { Name = "凹陷",     Count = 5 + rng.Next(10)},
                new NgTypeData { Name = "凸点",     Count = 3 + rng.Next(10)},
                new NgTypeData { Name = "裂纹",     Count = 2 + rng.Next(10)}
            };

                // 最近记录：20条，时间分布在12小时窗口内（而非全挤在最近1小时）
                var recent = new List<RecentRecord>();
                var stations = new[] { "工位1", "工位2", "工位3", "工位4" };
                var ngTypeList = new[] { "外观划伤", "气泡", "色差" };

                for (int i = 0; i < 20; i++)
                {
                    // 时间均匀分布在12小时窗口内
                    double t = (double)i / 20.0;  // 0.0 ~ 0.95
                    var entryTime = windowStart.AddMinutes(t * 12 * 60);

                    bool isInbound = i < 3;  // 前3条进站
                    bool isNg = !isInbound && i % 4 == 0;  // 出站中约25%NG

                    recent.Add(new RecentRecord
                    {
                        CellCode = $"TEST{i + 1:D4}",
                        DateTime = entryTime.ToString("yyyy/MM/dd HH:mm:ss"),
                        StationId = stations[i % 4],
                        OverallResult = isInbound ? "OK" : (isNg ? "NG" : "OK"),
                        NgTypes = isNg ? $"{ngTypeList[i % 3]}|{ngTypeList[(i + 1) % 3]}" : "",
                        ProcessMs = isInbound ? 0 : 30000 + rng.Next(60000),  // 复用上方 rng 实例
                        IsInbound = isInbound
                    });
                }

                var data= new DashboardData
                {
                    Total = total,
                    Ok = okCount,
                    Ng = ngCount,
                    YieldRate = yieldRate,
                    Hourly = hourly,
                    NgTypes = ngTypes,
                    Recent = recent,
                    TotalCount = total,   // 模拟场景：总记录数 = 当前页记录数（单页测试）
                    PageIndex = 0         // 测试始终从第0页开始
                };
                this.UpdateDashboard(data); 
                #endregion

            };
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


        public void RedrawHourly()
        {
            if (_hourlyData == null || _hourlyData.Count == 0) return;
            #region 可疑代码 
            var diagram = NgHourlyChart?.Diagram as XYDiagram2D;
            if (diagram == null) return;

            diagram.Series.Clear();

            var okSeries = new BarStackedSeries2D
            {
                DisplayName = "OK 产量",
                Brush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                LabelsVisibility = true
            };
            okSeries.Label = new SeriesLabel { TextPattern = "{V}" };

            var ngSeries = new BarStackedSeries2D
            {
                DisplayName = "NG 产量",
                Brush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                LabelsVisibility = true
            };
            ngSeries.Label = new SeriesLabel { TextPattern = "{V}" };

            foreach (var h in _hourlyData)
            {
                okSeries.Points.Add(new SeriesPoint(h.Hour, h.Ok));
                ngSeries.Points.Add(new SeriesPoint(h.Hour, h.Ng));

            }
            diagram.Series.Add(okSeries);
            diagram.Series.Add(ngSeries);
            #endregion


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
                // Bug 8 修复：空 NgTypes 字符串 Split('|') 会产生 [""]，
                // 导致 DataGrid NG 类型列出现空 badge。过滤掉空项。
                NgTypeList = (r.NgTypes ?? "")
                    .Split('|')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList(),
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
