using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZenergyBFSI.Model;
using static ZenergyBFSI.Model.InspectionUtils;

namespace ZenergyBFSI.Service
{
    public sealed class DashboardWorker : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

        private int _pageIndex = 0;
        public static int PageSize => 500;
        private long _sequenceNumber = 0;
        private bool _disposed = false;

        private bool _isRunning = false;
        private readonly object _runLock = new object();

        #region [TASK-REFACTOR-004] 班次固定窗口 | 2026-05-15 | AI生成
        // ─────────────────────────────────────────────────────────────────
        // 替换原滑动窗口 (now - 12h → now)，改为按班次固定窗口：
        //   全天 "all"  → 选定日期 00:00:00 ~ 次日 00:00:00 (24h)
        //   A班  "A"    → 选定日期 08:00:00 ~ 20:00:00 (白班 12h)
        //   B班  "B"    → 前一天 20:00:00 ~ 选定日期 08:00:00 (晚班 12h)
        //                 B班跨天，属于"昨晚到今天早上"，数据已完整可查。
        //   C班  "C"    → 预留，同 A班时间
        // 窗口固定后，5秒定时器仅刷新同一窗口内数据，不随时间推移滑动。
        // ─────────────────────────────────────────────────────────────────
        private string _shift = "all";
        private DateTime _selectedDate = DateTime.Today;
        private DateTime _windowStart;
        private DateTime _windowEnd;

        /// <summary>当前班次标签</summary>
        public string CurrentShift => _shift;

        /// <summary>当前选定日期</summary>
        public DateTime SelectedDate => _selectedDate;

        /// <summary>
        /// 设置看板筛选条件（班次 + 日期），自动触发重新查询。
        /// 由 DashboardService 转发，最终从 UC_StatesCards 事件驱动。
        /// </summary>
        public void SetFilter(string shift, DateTime? date = null)
        {
            _shift = shift ?? "all";
            if (date.HasValue)
                _selectedDate = date.Value;

            var day = _selectedDate.Date;
            switch (_shift)
            {
                case "A":
                    _windowStart = day.AddHours(8);             // 当天 08:00
                    _windowEnd   = day.AddHours(20);            // 当天 20:00
                    break;
                case "B":
                    _windowStart = day.AddDays(-1).AddHours(20); // 昨天 20:00
                    _windowEnd   = day.AddHours(8);              // 当天 08:00
                    break;
                case "C":
                    _windowStart = day.AddHours(8);             // 预留，暂同 A
                    _windowEnd   = day.AddHours(20);
                    break;
                default: // "all"
                    _windowStart = day;                         // 当天 00:00
                    _windowEnd   = day.AddDays(1);              // 次日 00:00
                    break;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[DashboardWorker] SetFilter shift={_shift} date={_selectedDate:yyyy-MM-dd} " +
                $"window=[{_windowStart:yyyy-MM-dd HH:mm} ~ {_windowEnd:yyyy-MM-dd HH:mm}]");

            _pageIndex = 0;  // 切换条件时回到首页
            ExecuteQueryAsync();
        }
        #endregion

        #region [TASK-REFACTOR-001] 出站判定条件 | 2026-05-15 | AI生成
        // 统一维护，消除 QueryAndParse / ParseRecords / KPI查询 中的重复定义
        private const string OutboundCondition =
            "(是否复投='是' OR 视觉检测参数一!='' OR 视觉检测参数二!='' OR 视觉检测参数三!='' OR 视觉检测参数四!='' OR 视觉检测参数五!='' OR 视觉检测参数六!='')";
        #endregion

        public event EventHandler<DashboardSnapshot> SnapshotReady;

        public DashboardWorker()
        {
            _timer = new DispatcherTimer { Interval = _interval };
            _timer.Tick += OnTimerTick;
            // 初始化默认窗口（今天全天）
            SetFilter("all", DateTime.Today);
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DashboardWorker));
            _timer.Start();
            ExecuteQueryAsync();
        }

        public void Stop() { _timer.Stop(); }

        public void SetPage(int pageIndex)
        {
            _pageIndex = pageIndex;
            ExecuteQueryAsync();
        }

        /// <summary>
        /// 手动触发看板刷新。调用后异步查询数据库，产生新的 DashboardSnapshot，
        /// 通过 SnapshotReady 事件通知订阅者（DashboardService）。
        /// </summary>
        public void RequestRefresh() { ExecuteQueryAsync(); }

        private void OnTimerTick(object sender, EventArgs e)
        {
            ExecuteQueryAsync();
        }

        private async void ExecuteQueryAsync()
        {
            lock (_runLock) { if (_isRunning) return; _isRunning = true; }
            try
            {
                await Task.Run(() =>
                {
                    var snapshot = QueryAndParse();
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SnapshotReady?.Invoke(this, snapshot);
                    }));
                });
            }
            finally { lock (_runLock) { _isRunning = false; } }
        }

        #region [TASK-REFACTOR-004] QueryAndParse — 基于班次固定窗口 | 2026-05-15 | AI生成
        // ─────────────────────────────────────────────────────────────────
        // 窗口为 _windowStart ~ _windowEnd，由 SetFilter() 根据班次+日期计算。
        // 查询条件改为 BETWEEN 双边界，替换原滑动 >= windowStart。
        // ─────────────────────────────────────────────────────────────────
        private DashboardSnapshot QueryAndParse()
        {
            var startStr = _windowStart.ToString("yyyy/MM/dd HH:mm:ss");
            var endStr = _windowEnd.ToString("yyyy/MM/dd HH:mm:ss");

            // ① 窗口总记录数（含进站+出站），用于分页
            var totalCountObj = SQLiteGenericHelper.ExecuteScalar<object>(
                "SELECT COUNT(*) FROM CellData WHERE 进站时间 >= @p0 AND 进站时间 < @p1",
                startStr, endStr);
            int totalCount = Convert.ToInt32(totalCountObj);

            // ② KPI 聚合 — 全窗口，仅出站记录
            var (totalOutbound, ok, ng) = QueryKpi(startStr, endStr);

            // ③ 时段分布 — 全窗口，出站记录按小时聚合 OK/NG
            var hourly = QueryHourly(startStr, endStr);

            // ④ 最近记录（分页）+ NG类型 — 来自当前页
            int offset = _pageIndex * PageSize;
            var records = SQLiteGenericHelper.QueryRaw<CellData>(
                $@"SELECT * FROM CellData WHERE 进站时间 >= @p0 AND 进站时间 < @p1 ORDER BY 进站时间 DESC LIMIT {PageSize} OFFSET {offset}",
                startStr, endStr);

            var (ngTypes, recent) = ParsePageRecords(records);

            System.Diagnostics.Debug.WriteLine(
                $"[DashboardWorker] shift={_shift} date={_selectedDate:yyyy-MM-dd} " +
                $"窗口总量={totalCount} 出站={totalOutbound} OK={ok} NG={ng} " +
                $"良率={(totalOutbound > 0 ? ok * 100.0 / totalOutbound : 0):F1}% " +
                $"当前页={records.Count}条");

            Interlocked.Increment(ref _sequenceNumber);
            return new DashboardSnapshot(
                totalCount, ok, ng,
                hourly, ngTypes, recent,
                totalCount, _pageIndex, PageSize,
                _sequenceNumber);
        }
        #endregion

        #region [TASK-REFACTOR-004] QueryKpi — 班次窗口 KPI 聚合 | 2026-05-15 | AI生成
        private (int totalOutbound, int ok, int ng) QueryKpi(string startStr, string endStr)
        {
            var okObj = SQLiteGenericHelper.ExecuteScalar<object>(
                $"SELECT COUNT(*) FROM CellData WHERE 进站时间 >= @p0 AND 进站时间 < @p1 AND {OutboundCondition} AND 出站结果 != 'NG'",
                startStr, endStr);
            var ngObj = SQLiteGenericHelper.ExecuteScalar<object>(
                $"SELECT COUNT(*) FROM CellData WHERE 进站时间 >= @p0 AND 进站时间 < @p1 AND {OutboundCondition} AND 出站结果 = 'NG'",
                startStr, endStr);
            int ok = Convert.ToInt32(okObj);
            int ng = Convert.ToInt32(ngObj);
            return (ok + ng, ok, ng);
        }
        #endregion

        #region [TASK-REFACTOR-005] QueryHourly — 三分类时段分布 | 2026-05-15 | AI生成
        // ─────────────────────────────────────────────────────────────────
        // 分三类统计每个小时桶：
        //   检测中 (Pending)：进站记录，未触发 OutboundCondition
        //   OK：出站记录，出站结果 != "NG"
        //   NG：出站记录，出站结果 == "NG"
        //
        // 分两次查询：进站（无出站结果列）和出站（有出站结果），分别聚合。
        // ─────────────────────────────────────────────────────────────────
        private List<HourlyData> QueryHourly(string startStr, string endStr)
        {
            int totalHours = (int)(_windowEnd - _windowStart).TotalHours;
            if (totalHours <= 0) totalHours = 24;

            var hourlyDict = new Dictionary<int, HourlyData>();
            for (int i = 0; i < totalHours; i++)
            {
                var hour = (_windowStart.Hour + i) % 24;
                hourlyDict[hour] = new HourlyData { Hour = hour };
            }

            // ① 进站记录 → 检测中桶（只查时间列）
            var inboundSources = SQLiteGenericHelper.QueryRaw<InboundSource>(
                $"SELECT 进站时间 FROM CellData WHERE 进站时间 >= @p0 AND 进站时间 < @p1 AND NOT ({OutboundCondition})",
                startStr, endStr);
            foreach (var src in inboundSources)
            {
                if (string.IsNullOrEmpty(src.进站时间)) continue;
                if (!DateTime.TryParse(src.进站时间, out var dt)) continue;
                if (hourlyDict.TryGetValue(dt.Hour, out var bucket))
                    bucket.Pending++;
            }

            // ② 出站记录 → OK/NG桶（已限定 OutboundCondition）
            var outboundSources = SQLiteGenericHelper.QueryRaw<HourlySource>(
                $"SELECT 进站时间, 出站结果 FROM CellData WHERE 进站时间 >= @p0 AND 进站时间 < @p1 AND {OutboundCondition}",
                startStr, endStr);
            foreach (var src in outboundSources)
            {
                if (string.IsNullOrEmpty(src.进站时间)) continue;
                if (!DateTime.TryParse(src.进站时间, out var dt)) continue;
                if (!hourlyDict.TryGetValue(dt.Hour, out var bucket)) continue;

                if (src.出站结果 == "NG")
                    bucket.Ng++;
                else
                    bucket.Ok++;
            }

            return hourlyDict.Keys
                .OrderBy(h => (h - _windowStart.Hour + 24) % 24)
                .Select(h => hourlyDict[h])
                .ToList();
        }

        /// <summary>进站记录轻量 DTO，仅含时间列。</summary>
        private class InboundSource
        {
            public string 进站时间 { get; set; }
        }

        /// <summary>出站记录轻量 DTO。</summary>
        private class HourlySource
        {
            public string 进站时间 { get; set; }
            public string 出站结果 { get; set; }
        }
        #endregion

        #region [TASK-REFACTOR-001] ParsePageRecords — 从当前页解析 NG类型 + 最近记录 | 2026-05-15 | AI生成
        // ─────────────────────────────────────────────────────────────────
        // KPI 和 Hourly 已由 QueryKpi / QueryHourly 基于全窗口独立查询，
        // 此方法仅处理 NG类型（从当前页采样）和最近记录列表。
        // 修复：进站记录 OverallResult 显示为 "检测中" 而非 "OK"。
        // ─────────────────────────────────────────────────────────────────
        private (List<NgTypeData> ngTypes, List<RecentRecord> recent)
            ParsePageRecords(List<CellData> records)
        {
            // 出站判定 — 与 OutboundCondition 保持一致
            bool IsOutbound(CellData c) =>
                c.是否复投 == "是"
                || !string.IsNullOrEmpty(c.视觉检测参数一)
                || !string.IsNullOrEmpty(c.视觉检测参数二)
                || !string.IsNullOrEmpty(c.视觉检测参数三)
                || !string.IsNullOrEmpty(c.视觉检测参数四)
                || !string.IsNullOrEmpty(c.视觉检测参数五)
                || !string.IsNullOrEmpty(c.视觉检测参数六);

            var outboundRecords = records.Where(IsOutbound).ToList();

            // ── NG类型统计（当前页采样，代表性足够）──
            var ngTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var rec in outboundRecords.Where(c => c.出站结果 == "NG" || c.Ng类型数量 > 0))
            {
                var ngTypeList = new[]
                {
                    rec.Ng类型1, rec.Ng类型2, rec.Ng类型3, rec.Ng类型4,
                    rec.Ng类型5, rec.Ng类型6, rec.Ng类型7, rec.Ng类型8
                };
                foreach (var ngType in ngTypeList)
                {
                    if (!string.IsNullOrEmpty(ngType))
                    {
                        ngTypeCounts.TryGetValue(ngType, out int cur);
                        ngTypeCounts[ngType] = cur + 1;
                    }
                }
            }
            var ngTypes = ngTypeCounts
                .OrderByDescending(kv => kv.Value)
                .Take(8)
                .Select(kv => new NgTypeData { Name = kv.Key, Count = kv.Value })
                .ToList();

            // ── 最近记录 ──
            var recent = new List<RecentRecord>();
            foreach (var rec in records)
            {
                bool outbound = IsOutbound(rec);
                string overallResult;
                if (!outbound)
                    overallResult = "检测中";
                else
                    overallResult = rec.出站结果 == "NG" ? "NG" : "OK";

                string ngTypesStr = "";
                if (outbound && rec.Ng类型数量 > 0)
                {
                    var types = new[]
                    {
                        rec.Ng类型1, rec.Ng类型2, rec.Ng类型3, rec.Ng类型4,
                        rec.Ng类型5, rec.Ng类型6, rec.Ng类型7, rec.Ng类型8
                    }.Where(t => !string.IsNullOrEmpty(t)).ToArray();
                    ngTypesStr = string.Join("|", types);
                }

                int processMs = 0;
                if (outbound && DateTime.TryParse(rec.出站时间, out var exitTime)
                    && DateTime.TryParse(rec.进站时间, out var enterTime))
                {
                    processMs = (int)(exitTime - enterTime).TotalMilliseconds;
                }

                recent.Add(new RecentRecord
                {
                    CellCode = rec.电芯码 ?? "",
                    DateTime = rec.进站时间 ?? "",
                    StationId = rec.检验位置 ?? "",
                    OverallResult = overallResult,
                    NgTypes = ngTypesStr,
                    ProcessMs = processMs,
                    IsInbound = !outbound
                });
            }

            return (ngTypes, recent);
        }
        #endregion

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
        }
    }
}
