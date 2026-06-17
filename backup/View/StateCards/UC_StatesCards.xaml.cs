
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
                Messenger.Default.Register<DashboardUpdateMessage>(this, OnDashboardUpdateMessage);
                Messenger.Default.Register<StatusLightUpdateMessage>(this, OnStatusLightUpdateMessage);

                #region [TASK-SIM-003] 模拟数据集成入口 | 2026-05-15 | AI生成
                // ─────────────────────────────────────────────────────────
                // 启用模拟模式时，向 SQLite 写入仿真检测记录，
                // 之后由 DashboardWorker 5秒定时查询 → 自动刷新看板。
                //
                // 取消下方注释即可启用：
                //if (Settings.SimulationMode)
                //    _ = SimulationDataGenerator.GenerateAsync(500);
                //
                // 手动清理模拟数据：
                //   SimulationDataGenerator.Clear();
                // ─────────────────────────────────────────────────────────
                #endregion
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
            };
            _clockTimer.Start();
        }

        // ════════════════════════════════════════════════════════
        //  KPI 更新
        // ════════════════════════════════════════════════════════
        #region [TASK-REFACTOR-002] ApplyKpi — KPI 显示修正 | 2026-05-15 | AI生成
        // 修复：
        //   1. OK/NG 占比基于出站总数 (d.Ok + d.Ng)，而非 Total（含进站）
        //   2. TxtRecordCount 使用 d.TotalCount（窗口真实总量），而非当前页条数
        //   3. 良率达标判定阈值保持 99.5%
        private void ApplyKpi(DashboardData d)
        {
            int outboundTotal = d.Ok + d.Ng;

            TxtTotal.Text = d.Total.ToString();
            TxtOk.Text = d.Ok.ToString();
            TxtNg.Text = d.Ng.ToString();
            TxtRate.Text = $"{d.YieldRate:F2}%";

            #region [TASK-REFACTOR-004] 班次标签映射 | 2026-05-15 | AI生成
            string shiftLabel = _currentShift switch
            {
                "A" => "白班 (08:00-20:00)",
                "B" => "晚班 (昨20:00-今08:00)",
                "C" => "C班",
                _   => "全天 (00:00-24:00)"
            };
            TxtTotalSub.Text = $"{shiftLabel}累计";
            #endregion
            TxtOkSub.Text = outboundTotal > 0
                ? $"占比 {d.Ok * 100.0 / outboundTotal:F1}%"
                : "--";
            TxtNgSub.Text = outboundTotal > 0
                ? $"占比 {d.Ng * 100.0 / outboundTotal:F1}%"
                : "--";
            TxtRateSub.Text = d.YieldRate >= 99.5
                ? "✓ 达标 (目标 ≥99.5%)"
                : "✗ 未达标 (目标 ≥99.5%)";
            TxtRecordCount.Text = $"第 {_currentPage + 1}/{_totalPages} 页 共 {d.TotalCount} 条";

            _hourlyData = d.Hourly;
        }
        #endregion


        #region [TASK-REFACTOR-005] RedrawHourly — 三分类堆叠柱 | 2026-05-15 | AI生成
        // 新增 "检测中" 堆叠柱，与 OK/NG 并列显示。
        // 顺序：检测中（蓝）→ OK（绿）→ NG（红），底部→顶部堆叠。
        private int _lastHourlyHash = 0;

        public void RedrawHourly()
        {
            if (_hourlyData == null || _hourlyData.Count == 0) return;

            var diagram = NgHourlyChart?.Diagram as XYDiagram2D;
            if (diagram == null) return;

            int hash = ComputeHourlyHash(_hourlyData);
            if (hash == _lastHourlyHash && diagram.Series.Count > 0) return;
            _lastHourlyHash = hash;

            diagram.Series.Clear();

            var pendingSeries = new BarStackedSeries2D
            {
                DisplayName = "检测中",
                Brush = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                LabelsVisibility = true
            };
            pendingSeries.Label = new SeriesLabel { TextPattern = "{V}" };

            var okSeries = new BarStackedSeries2D
            {
                DisplayName = "OK",
                Brush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                LabelsVisibility = true
            };
            okSeries.Label = new SeriesLabel { TextPattern = "{V}" };

            var ngSeries = new BarStackedSeries2D
            {
                DisplayName = "NG",
                Brush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                LabelsVisibility = true
            };
            ngSeries.Label = new SeriesLabel { TextPattern = "{V}" };

            foreach (var h in _hourlyData)
            {
                pendingSeries.Points.Add(new SeriesPoint(h.Hour, h.Pending));
                okSeries.Points.Add(new SeriesPoint(h.Hour, h.Ok));
                ngSeries.Points.Add(new SeriesPoint(h.Hour, h.Ng));
            }
            diagram.Series.Add(pendingSeries);
            diagram.Series.Add(okSeries);
            diagram.Series.Add(ngSeries);
        }

        private static int ComputeHourlyHash(List<HourlyData> hourly)
        {
            int hash = 17;
            foreach (var h in hourly)
            {
                hash = hash * 31 + h.Hour;
                hash = hash * 31 + h.Pending;
                hash = hash * 31 + h.Ok;
                hash = hash * 31 + h.Ng;
            }
            return hash;
        }
        #endregion

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
        #region [TASK-REFACTOR-004] 事件处理 — 班次/日期驱动看板查询 | 2026-05-15 | AI生成
        // 班次切换或日期变更时，直接调用 DashboardService.SetFilter，
        // 由 DashboardWorker 重算固定窗口并重新查询。
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            DashboardService.I.SetFilter(_currentShift, DpDate.SelectedDate);
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        private void DpDate_SelectedDateChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (DpDate.SelectedDate.HasValue)
            {
                DashboardService.I.SetFilter(_currentShift, DpDate.SelectedDate.Value);
                DateChanged?.Invoke(this, DpDate.SelectedDate.Value);
            }
        }

        private void ShiftRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _currentShift = tag;
                DashboardService.I.SetFilter(tag, DpDate.SelectedDate);
                ShiftChanged?.Invoke(this, tag);
            }
        }
        #endregion

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
