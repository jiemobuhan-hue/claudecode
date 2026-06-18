using RinKit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZenergyBFSI.Model;
using ZenergyBFSI.Service.CRUDServices;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 蓝膜数据异步读取管理器 — 单例
    /// 上位机只负责从 3 台视觉工控机 SQL Server 读取检测数据，绝不做写入
    ///
    /// 读路径: EnqueueReadAsync → _readQueue → ReadConsumer → 3库查询 → TCS.SetResult → 自动机 await 恢复
    /// </summary>
    public sealed class BlueFilmDataQueueManager : IDisposable
    {
        #region 单例

        private static readonly Lazy<BlueFilmDataQueueManager> _instance =
            new Lazy<BlueFilmDataQueueManager>(() => new BlueFilmDataQueueManager(), true);

        public static BlueFilmDataQueueManager I => _instance.Value;

        private BlueFilmDataQueueManager() { }

        #endregion

        #region 队列

        private readonly ConcurrentQueue<DataReadRequest> _readQueue = new ConcurrentQueue<DataReadRequest>();

        #endregion

        #region 后台消费

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _readConsumerTask;
        private Timer _healthCheckTimer;

        private BlueFilmDetectionRepository _detectionRepo1;
        private BlueFilmDetectionRepository _detectionRepo2;
        private BlueFilmDetectionRepository _detectionRepo3;

        #endregion

        #region 数据库连接状态

        private readonly ConcurrentDictionary<string, bool> _dbHealth
            = new ConcurrentDictionary<string, bool>();

        public event Action DbHealthChanged;

        public IReadOnlyDictionary<string, bool> GetDbHealth() =>
            new Dictionary<string, bool>(_dbHealth);

        private bool IsHealthy(string label) =>
            _dbHealth.TryGetValue(label, out var v) && v;

        private void SetDbHealthy(string label, bool healthy)
        {
            var old = _dbHealth.TryGetValue(label, out var v) && v;
            _dbHealth[label] = healthy;
            if (old != healthy)
                DbHealthChanged?.Invoke();
        }

        public string PingAllDatabases()
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine($"=== DB Ping {DateTime.Now:HH:mm:ss} ===");

            PingSingle(@"DB1(DESKTOP-0F9L4KO\RJ)", Settings.SQLServer本地连接, results);
            PingSingle("DB2(DESKTOP-NHDST87)", Settings.SQLServer远程连接1, results);
            PingSingle("DB3(DESKTOP-2ADDTIC)", Settings.SQLServer远程连接2, results);

            return results.ToString();
        }

        private void PingSingle(string label, string connString, System.Text.StringBuilder results)
        {
            try
            {
                // 追加 Connection Timeout，SQL Server 驱动级超时，不依赖 Task.Wait
                string finalConnString = connString;
                if (!connString.Contains("Connect Timeout") && !connString.Contains("Connection Timeout"))
                {
                    finalConnString = connString.TrimEnd(';') + ";Connect Timeout=2;";
                }

                using var conn = new System.Data.SqlClient.SqlConnection(finalConnString);
                conn.Open();
                conn.Close();
                SetDbHealthy(label, true);
                results.AppendLine($"  {label} ✅ 在线");
            }
            catch (Exception ex)
            {
                SetDbHealthy(label, false);
                results.AppendLine($"  {label} ❌ [{ex.GetType().Name}] {ex.Message}");
            }
        }

        #endregion

        #region 生命周期

        private volatile bool _initialized = false;
        public bool IsInitialized => _initialized;
        private readonly object _initLock = new object();
        private volatile bool _disposed = false;

        public void Init()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;

                _detectionRepo1 = new BlueFilmDetectionRepository(Settings.SQLServer本地连接);
                _detectionRepo2 = new BlueFilmDetectionRepository(Settings.SQLServer远程连接1);
                _detectionRepo3 = new BlueFilmDetectionRepository(Settings.SQLServer远程连接2);

                _readConsumerTask = Task.Run(() => ReadConsumerLoop(_cts.Token), _cts.Token);

                _healthCheckTimer = new Timer(_ => PingAllDatabases(), null, 5000, 15000);

                _initialized = true;
                Rlog.Info("[BlueFilmDataQueue] 队列管理器初始化完成，只读消费者 + 健康检查已启动");
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();
            _healthCheckTimer?.Dispose();

            while (_readQueue.TryDequeue(out var orphan))
            {
                orphan.Completion.TrySetResult(
                    new CellData { 电芯码 = orphan.CellCode ?? "", 出站结果 = "OK" });
            }

            try
            {
                Task.WaitAll(new[] { _readConsumerTask }, 5000);
            }
            catch { }

            _cts?.Dispose();
        }

        #endregion

        #region 公开 API

        public Task<CellData> EnqueueReadAsync(string cellCode, int channelNo)
        {
            if (_disposed)
                return Task.FromResult(new CellData { 电芯码 = cellCode ?? "", 出站结果 = "OK" });

            var tcs = new TaskCompletionSource<CellData>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _readQueue.Enqueue(new DataReadRequest
            {
                CellCode = cellCode,
                ChannelNo = channelNo,
                Completion = tcs,
                EnqueueTime = DateTime.Now
            });

            return tcs.Task;
        }

        #endregion

        #region ReadConsumer

        private async Task ReadConsumerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_readQueue.TryDequeue(out var request))
                {
                    CellData result = null;
                    try
                    {
                        result = await ProcessReadRequestAsync(request);
                        if ((DateTime.Now - request.EnqueueTime).TotalSeconds > 5)
                            result = new CellData { 电芯码 = request.CellCode, 出站结果 = "OK" };
                    }
                    catch (Exception ex)
                    {
                        Rlog.Error($"[BlueFilmDataQueue] ReadConsumer 异常: {ex.Message}");
                    }
                    finally
                    {
                        request.Completion.TrySetResult(
                            result ?? new CellData { 电芯码 = request.CellCode, 出站结果 = "OK" });
                    }
                }
                else
                {
                    await Task.Delay(10, token);
                }
            }
        }

        private async Task<CellData> ProcessReadRequestAsync(DataReadRequest request)
        {
            var allRecords = new List<T_BlueFilmDetection>();
            var result = new CellData
            {
                电芯码 = request.CellCode,
                出站结果 = "OK",
                Ng类型数量 = 0
            };

            // 只查已知在线的库，离线直接跳过，省掉 3s×N 的超时等待
            var tasks = new List<Task>();
            if (IsHealthy(@"DB1(DESKTOP-0F9L4KO\RJ)"))
                tasks.Add(QuerySingleDbAsync(_detectionRepo1, @"DB1(DESKTOP-0F9L4KO\RJ)", request.CellCode, allRecords, 3000));
            if (IsHealthy("DB2(DESKTOP-NHDST87)"))
                tasks.Add(QuerySingleDbAsync(_detectionRepo2, "DB2(DESKTOP-NHDST87)", request.CellCode, allRecords, 3000));
            if (IsHealthy("DB3(DESKTOP-2ADDTIC)"))
                tasks.Add(QuerySingleDbAsync(_detectionRepo3, "DB3(DESKTOP-2ADDTIC)", request.CellCode, allRecords, 3000));

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);

            if (allRecords.Count > 0)
                AggregateDefects(ref result, allRecords);

            return result;
        }

        private async Task QuerySingleDbAsync(
            BlueFilmDetectionRepository repo,
            string serverLabel,
            string cellCode,
            List<T_BlueFilmDetection> accumulator,
            int timeoutMs)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                var records = await Task.Run(() => repo.GetByCellCode(cellCode), cts.Token);
                SetDbHealthy(serverLabel, true);
                if (records != null && records.Count > 0)
                {
                    lock (accumulator)
                        accumulator.AddRange(records);
                }
            }
            catch (OperationCanceledException)
            {
                SetDbHealthy(serverLabel, false);
                Rlog.Warn($"[BlueFilmDataQueue] 查询超时 | 服务器: {serverLabel} | 电芯码: {cellCode}");
            }
            catch (AggregateException ex) when (ex.InnerException != null)
            {
                SetDbHealthy(serverLabel, false);
                Rlog.Error($"[BlueFilmDataQueue] 查询失败 | 服务器: {serverLabel} | {ex.InnerException.Message}");
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                SetDbHealthy(serverLabel, false);
                Rlog.Error($"[BlueFilmDataQueue] SQL错误 | 服务器: {serverLabel} | 错误号: {ex.Number} | {ex.Message}");
            }
            catch (Exception ex)
            {
                SetDbHealthy(serverLabel, false);
                Rlog.Error($"[BlueFilmDataQueue] 查询异常 | 服务器: {serverLabel} | {ex.Message}");
            }
        }

        private void AggregateDefects(ref CellData data, List<T_BlueFilmDetection> records)
        {
            var areaGroups = records
                .Where(t => !string.Equals(t.DetectionResults?.Trim(), "OK", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(t.DetectionArea))
                .GroupBy(t => t.DetectionArea.Trim());

            int ngIndex = 0;
            foreach (var areaGroup in areaGroups)
            {
                if (ngIndex >= 8) break;

                var defectCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var record in areaGroup)
                {
                    foreach (var ng in new[] { record.NGtype1, record.NGtype2, record.NGtype3 })
                    {
                        var ngVal = ng?.Trim();
                        if (!string.IsNullOrEmpty(ngVal))
                        {
                            if (defectCounts.ContainsKey(ngVal))
                                defectCounts[ngVal]++;
                            else
                                defectCounts[ngVal] = 1;
                        }
                    }
                }

                if (defectCounts.Count == 0) continue;

                var defectStr = string.Join(",", defectCounts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}×{kv.Value}"));
                var ngValue = $"{areaGroup.Key}外观缺陷{defectStr}";

                SetNgField(ref data, ngIndex, ngValue);
                ngIndex++;
                data.出站结果 = "NG";
            }

            data.Ng类型数量 = ngIndex;
        }

        private static void SetNgField(ref CellData data, int index, string value)
        {
            switch (index)
            {
                case 0: data.Ng类型1 = value; break;
                case 1: data.Ng类型2 = value; break;
                case 2: data.Ng类型3 = value; break;
                case 3: data.Ng类型4 = value; break;
                case 4: data.Ng类型5 = value; break;
                case 5: data.Ng类型6 = value; break;
                case 6: data.Ng类型7 = value; break;
                case 7: data.Ng类型8 = value; break;
            }
        }

        #endregion
    }
}
