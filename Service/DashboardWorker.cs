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
        private readonly TimeSpan _timeWindow = TimeSpan.FromHours(4);

        private int _pageIndex = 0;
        private int _pageSize = 20;
        private long _sequenceNumber = 0;
        private bool _disposed = false;

        private bool _isRunning = false;
        private readonly object _runLock = new object();

        // 模拟数据生成器
        private DispatcherTimer _simTimer;
        private readonly Random _random = new Random();
        private bool _simulationRunning = false;
        private int _simCounter = 0;

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
            // 注意：LIMIT/OFFSET 必须内嵌整数，不能用参数绑定
            int offset = _pageIndex * _pageSize;
            var records = SQLiteGenericHelper.QueryRaw<CellData>(
                $@"SELECT * FROM CellData WHERE 进站时间 >= @p0 ORDER BY 进站时间 DESC LIMIT {_pageSize} OFFSET {offset}",
                fourHoursAgoStr);

            System.Diagnostics.Debug.WriteLine($"[DashboardWorker] 查询到 {records.Count} 条记录，4小时前={fourHoursAgoStr}");

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

        /// <summary>
        /// 启动模拟模式：定时生成随机测试数据插入数据库
        /// </summary>
        /// <param name="intervalSeconds">模拟间隔（秒）</param>
        public void StartSimulation(int intervalSeconds = 3)
        {
            if (_simulationRunning) return;
            _simulationRunning = true;

            // 立即插入一条模拟数据
            InsertSimulatedData();

            // 启动定时器
            _simTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(intervalSeconds) };
            _simTimer.Tick += (s, e) => InsertSimulatedData();
            _simTimer.Start();

            System.Diagnostics.Debug.WriteLine($"[DashboardWorker] 模拟模式启动，每 {intervalSeconds} 秒插入数据");
        }

        /// <summary>
        /// 停止模拟模式
        /// </summary>
        public void StopSimulation()
        {
            if (!_simulationRunning) return;
            _simulationRunning = false;
            _simTimer?.Stop();
            _simTimer = null;
            System.Diagnostics.Debug.WriteLine($"[DashboardWorker] 模拟模式停止");
        }

        private void EnsureCellDataTable()
        {
            if (SQLiteGenericHelper.TableExists("CellData")) return;

            SQLiteGenericHelper.CreateTable(@"CREATE TABLE CellData (
                Id INTEGER PRIMARY KEY,
                TimeStamp INTEGER,
                电芯码 TEXT,
                进站时间 TEXT,
                检验位置 TEXT,
                是否复投 INTEGER DEFAULT 0,
                Ng类型数量 INTEGER DEFAULT 0,
                Ng类型1 TEXT,
                Ng类型2 TEXT,
                Ng类型3 TEXT,
                Ng类型4 TEXT,
                Ng类型5 TEXT,
                Ng类型6 TEXT,
                Ng类型7 TEXT,
                Ng类型8 TEXT,
                入站结果 TEXT,
                出站结果 TEXT,
                出站时间 TEXT,
                视觉检测状态 TEXT,
                视觉检测参数一 TEXT,
                视觉检测参数二 TEXT,
                视觉检测参数三 TEXT,
                视觉检测参数四 TEXT,
                视觉检测参数五 TEXT,
                视觉检测参数六 TEXT,
                MOM查询来料状态 TEXT,
                MOM出站结果 TEXT DEFAULT '0',
                视觉检测结果 TEXT,
                人工复判次数 INTEGER DEFAULT 0
            )");
            System.Diagnostics.Debug.WriteLine("[DashboardWorker] CellData 表创建成功");
        }

        private void InsertSimulatedData()
        {
            try
            {
                // 确保 CellData 表存在
                EnsureCellDataTable();
                _simCounter++;
                string cellCode = $"SIM{_simCounter:D6}";
                bool isInbound = _random.NextDouble() < 0.6; // 60% 进站, 40% 出站

                var now = DateTime.Now;
                var data = new CellData
                {
                    电芯码 = cellCode,
                    进站时间 = now.ToString("yyyy/MM/dd HH:mm:ss"),
                    入站结果 = "OK",
                    出站结果 = "",
                    出站时间 = "",
                    是否复投 = false,
                    检验位置 = $"工位{_random.Next(1, 5)}"
                };

                if (!isInbound)
                {
                    // 出站数据
                    data.出站结果 = _random.NextDouble() < 0.85 ? "OK" : "NG"; // 85% OK, 15% NG
                    data.出站时间 = now.ToString("yyyy/MM/dd HH:mm:ss");
                    data.入站结果 = "OK";

                    // 随机填充视觉检测参数（表示已出站）
                    int paramCount = _random.Next(1, 7);
                    for (int i = 0; i < paramCount; i++)
                    {
                        switch (i)
                        {
                            case 0: data.视觉检测参数一 = "正常"; break;
                            case 1: data.视觉检测参数二 = "正常"; break;
                            case 2: data.视觉检测参数三 = "正常"; break;
                            case 3: data.视觉检测参数四 = "正常"; break;
                            case 4: data.视觉检测参数五 = "正常"; break;
                            case 5: data.视觉检测参数六 = "正常"; break;
                        }
                    }

                    // 随机NG类型
                    if (data.出站结果 == "NG")
                    {
                        data.Ng类型数量 = _random.Next(1, 4);
                        string[] ngTypes = { "外观划伤", "气泡", "色差", "变形", "污渍", "凹陷", "凸点", "裂纹" };
                        for (int i = 0; i < data.Ng类型数量 && i < 8; i++)
                        {
                            string type = ngTypes[_random.Next(ngTypes.Length)];
                            switch (i)
                            {
                                case 0: data.Ng类型1 = type; break;
                                case 1: data.Ng类型2 = type; break;
                                case 2: data.Ng类型3 = type; break;
                                case 3: data.Ng类型4 = type; break;
                                case 4: data.Ng类型5 = type; break;
                                case 5: data.Ng类型6 = type; break;
                                case 6: data.Ng类型7 = type; break;
                                case 7: data.Ng类型8 = type; break;
                            }
                        }
                    }
                }

                // 插入数据库
                var temp = new List<CellData> { data };
                SQLiteGenericHelper.BulkUpsert(temp, "电芯码", "CellData");

                System.Diagnostics.Debug.WriteLine(
                    $"[DashboardWorker] 插入模拟数据: {cellCode}, 进站={isInbound}, 出站结果={data.出站结果}, 视觉参数一={data.视觉检测参数一}");

                // 触发刷新
                RequestRefresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardWorker] 插入模拟数据异常: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
        }
    }
}