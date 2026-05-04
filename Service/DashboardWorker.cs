using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.Service
{
    public sealed class DashboardWorker : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _timeWindow = TimeSpan.FromHours(4);

        private int _pageIndex = 0;
        private int _pageSize = 20;
        private long _sequenceNumber = 0;
        private bool _disposed = false;

        private bool _isRunning = false;
        private readonly object _runLock = new object();

        public event EventHandler<DashboardSnapshot> SnapshotReady;

        public DashboardWorker()
        {
            _timer = new DispatcherTimer { Interval = _interval };
            _timer.Tick += OnTimerTick;
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

        private DashboardSnapshot QueryAndParse()
        {
            var now = DateTime.Now;
            var fourHoursAgo = now - _timeWindow;
            var fourHoursAgoStr = fourHoursAgo.ToString("yyyy/MM/dd HH:mm:ss");

            // Query inbound records with pagination
            int offset = _pageIndex * _pageSize;
            var records = SQLiteGenericHelper.QueryRaw<CellData>(
                @"SELECT * FROM CellData WHERE 进站时间 >= @p0 ORDER BY 进站时间 DESC LIMIT @p1 OFFSET @p2",
                fourHoursAgoStr, _pageSize, offset);

            // Query total count
            var totalCountObj = SQLiteGenericHelper.ExecuteScalar<object>(
                "SELECT COUNT(*) FROM CellData WHERE 进站时间 >= @p0", fourHoursAgoStr);
            int totalCount = Convert.ToInt32(totalCountObj);

            // Parse records
            var (kpi, hourly, ngTypes, recent) = ParseRecords(records, fourHoursAgo);

            Interlocked.Increment(ref _sequenceNumber);
            return new DashboardSnapshot(
                kpi.Total, kpi.Ok, kpi.Ng,
                hourly, ngTypes, recent,
                totalCount, _pageIndex, _pageSize,
                _sequenceNumber);
        }

        private (KpiResult kpi, List<HourlyData> hourly, List<NgTypeData> ngTypes, List<RecentRecord> recent)
            ParseRecords(List<CellData> records, DateTime windowStart)
        {
            // Determine outbound records:
            // 出站条件：视觉检测参数一~六 任一有值 OR 是否复投=1
            Func<CellData, bool> isOutbound = c =>
                c.是否复投
                || !string.IsNullOrEmpty(c.视觉检测参数一)
                || !string.IsNullOrEmpty(c.视觉检测参数二)
                || !string.IsNullOrEmpty(c.视觉检测参数三)
                || !string.IsNullOrEmpty(c.视觉检测参数四)
                || !string.IsNullOrEmpty(c.视觉检测参数五)
                || !string.IsNullOrEmpty(c.视觉检测参数六);

            var outboundRecords = records.Where(isOutbound).ToList();
            var inboundRecords = records.Where(c => !isOutbound(c)).ToList();

            // KPI: all records in window
            int total = records.Count;
            int ok = outboundRecords.Count(c => c.出站结果 != "NG");
            int ng = outboundRecords.Count(c => c.出站结果 == "NG");

            // HourlyData: group inbound records by hour
            var hourlyDict = new Dictionary<int, HourlyData>();
            for (int i = 0; i < 4; i++)
            {
                var hour = (windowStart.Hour + i) % 24;
                hourlyDict[hour] = new HourlyData { Hour = hour.ToString("D2") + ":00", Ok = 0, Ng = 0 };
            }

            foreach (var rec in inboundRecords)
            {
                if (DateTime.TryParse(rec.进站时间, out var dt))
                {
                    var hourKey = dt.Hour;
                    if (hourlyDict.TryGetValue(hourKey, out var hd))
                    {
                        hd.Ok++;
                    }
                }
            }
            foreach (var rec in outboundRecords)
            {
                if (DateTime.TryParse(rec.进站时间, out var dt))
                {
                    var hourKey = dt.Hour;
                    if (hourlyDict.TryGetValue(hourKey, out var hd))
                    {
                        hd.Ng++;
                    }
                }
            }
            var hourly = hourlyDict.Values.OrderBy(h => h.Hour).ToList();

            // NG types: aggregate from outbound NG records
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
                        if (!ngTypeCounts.ContainsKey(ngType))
                            ngTypeCounts[ngType] = 0;
                        ngTypeCounts[ngType]++;
                    }
                }
            }
            var ngTypes = ngTypeCounts
                .OrderByDescending(kv => kv.Value)
                .Take(8)
                .Select(kv => new NgTypeData { Name = kv.Key, Count = kv.Value })
                .ToList();

            // RecentRecord: combine inbound/outbound with IsInbound flag
            var recent = new List<RecentRecord>();
            foreach (var rec in records)
            {
                bool outbound = isOutbound(rec);
                var overallResult = outbound
                    ? (rec.出站结果 == "NG" ? "NG" : "OK")
                    : "OK";

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

            return (new KpiResult { Total = total, Ok = ok, Ng = ng }, hourly, ngTypes, recent);
        }

        private struct KpiResult { public int Total; public int Ok; public int Ng; }

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
        }
    }
}