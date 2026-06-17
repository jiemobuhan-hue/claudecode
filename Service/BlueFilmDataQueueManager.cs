using Newtonsoft.Json;
using RinKit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZenergyBFSI.Model;
using ZenergyBFSI.Model.Vision;
using ZenergyBFSI.Service.CRUDServices;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 蓝膜数据异步队列管理器 — 单例
    /// 将 SQL Server 读写从自动机线程彻底解耦
    ///
    /// 读路径: EnqueueReadAsync → _readQueue → ReadConsumer → 3库查询 → TCS.SetResult → 自动机 await 恢复
    /// 写路径: EnqueueDetectionResult/EnqueueMOMOutbound → _writeQueue → WriteConsumer → WriteRouter(本地→远程1→远程2)
    /// 容灾:  远程写入失败 → _retryBuffer → RetryTimer 30s → 最多 10 次 → 丢弃
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

        // 读取请求队列：自动机 Enqueue → 后台消费者 Dequeue → 查 3 库 → TCS 回调
        private readonly ConcurrentQueue<DataReadRequest> _readQueue = new ConcurrentQueue<DataReadRequest>();

        // 检测结果写入队列（T_BlueFilmDetection）
        private readonly ConcurrentQueue<T_BlueFilmDetection> _writeDetectionQueue = new ConcurrentQueue<T_BlueFilmDetection>();

        // MOM 出站写入队列（T_BlueFilmDataMOM）
        private readonly ConcurrentQueue<T_BlueFilmDataMOM> _writeMOMQueue = new ConcurrentQueue<T_BlueFilmDataMOM>();

        // 远程库写入失败重试缓冲区
        private readonly ConcurrentQueue<DataRetryItem> _retryBuffer = new ConcurrentQueue<DataRetryItem>();

        #endregion

        #region 后台消费

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _readConsumerTask;
        private Task _writeDetectionConsumerTask;
        private Task _writeMOMConsumerTask;
        private Timer _retryTimer;

        // 三库 Repository 实例（在 Init 时创建）
        private BlueFilmDetectionRepository _detectionRepoLocal;
        private BlueFilmDetectionRepository _detectionRepoRemote1;
        private BlueFilmDetectionRepository _detectionRepoRemote2;

        private BlueFilmDataMOMRepository _momRepoLocal;
        private BlueFilmDataMOMRepository _momRepoRemote1;
        private BlueFilmDataMOMRepository _momRepoRemote2;

        #endregion

        #region 生命周期

        private volatile bool _initialized = false;
        private readonly object _initLock = new object();
        private volatile bool _disposed = false;

        /// <summary>
        /// 初始化队列管理器 — 创建 3 组 Repository + 启动 3 个后台消费者 + 重试定时器
        /// 由 AutoRun.Init() 或 App 启动时调用
        /// </summary>
        public void Init()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;

                var connLocal = Settings.SQLServer本地连接;
                var connRemote1 = Settings.SQLServer远程连接1;
                var connRemote2 = Settings.SQLServer远程连接2;

                // 创建三库 Repository
                _detectionRepoLocal  = new BlueFilmDetectionRepository(connLocal);
                _detectionRepoRemote1 = new BlueFilmDetectionRepository(connRemote1);
                _detectionRepoRemote2 = new BlueFilmDetectionRepository(connRemote2);

                _momRepoLocal  = new BlueFilmDataMOMRepository(connLocal);
                _momRepoRemote1 = new BlueFilmDataMOMRepository(connRemote1);
                _momRepoRemote2 = new BlueFilmDataMOMRepository(connRemote2);

                // 启动后台消费者 (方法将在 Tasks 5-6 中实现)
                _readConsumerTask = Task.Run(() => ReadConsumerLoop(_cts.Token), _cts.Token);
                _writeDetectionConsumerTask = Task.Run(() => WriteDetectionConsumerLoop(_cts.Token), _cts.Token);
                _writeMOMConsumerTask = Task.Run(() => WriteMOMConsumerLoop(_cts.Token), _cts.Token);

                // 启动重试定时器（首延迟 30s，之后每 30s）
                _retryTimer = new Timer(_ => RetryCallback(), null, 30000, 30000);

                _initialized = true;
                Rlog.Info("[BlueFilmDataQueue] 队列管理器初始化完成，3 个后台消费者已启动");
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();

            // Drain remaining read requests to avoid TCS leaks (awaiters would hang forever)
            while (_readQueue.TryDequeue(out var orphan))
            {
                orphan.Completion.TrySetResult(
                    new CellData { 电芯码 = orphan.CellCode ?? "", 出站结果 = "OK" });
            }

            _retryTimer?.Dispose();

            try
            {
                // 等待消费完成（最多 5 秒）
                var tasks = new[] { _readConsumerTask, _writeDetectionConsumerTask, _writeMOMConsumerTask };
                Task.WaitAll(tasks, 5000);
            }
            catch (AggregateException) { /* 超时不阻塞 Dispose */ }

            _cts?.Dispose();
        }

        #endregion

        #region 公开 API（自动机调用）

        /// <summary>
        /// 异步读取视觉检测结果 — 返回 Task<CellData>，自动机线程通过 await 等待
        /// 内部: 入队 → 后台查 3 库 → 聚合 → TCS.SetResult → 自动机恢复
        /// 耗时: <1ms（仅入队操作）
        /// </summary>
        /// <param name="cellCode">电芯条码</param>
        /// <param name="channelNo">通道编号 (1-4)</param>
        /// <returns>聚合后的 CellData（含 Ng类型1~8 缺陷描述）</returns>
        public Task<CellData> EnqueueReadAsync(string cellCode, int channelNo)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BlueFilmDataQueueManager));
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

        /// <summary>
        /// 非阻塞入队 — 检测结果写入三库（本地优先，远程容错）
        /// 耗时: <1ms（仅入队操作）
        /// </summary>
        public void EnqueueDetectionResult(T_BlueFilmDetection data)
        {
            if (_disposed) { Rlog.Warn("[BlueFilmDataQueue] 队列已释放，丢弃检测结果写入"); return; }
            if (data != null)
                _writeDetectionQueue.Enqueue(data);
        }

        /// <summary>
        /// 非阻塞入队 — MOM 出站数据写入三库
        /// 耗时: <1ms（仅入队操作）
        /// </summary>
        public void EnqueueMOMOutbound(T_BlueFilmDataMOM data)
        {
            if (_disposed) { Rlog.Warn("[BlueFilmDataQueue] 队列已释放，丢弃MOM出站写入"); return; }
            if (data != null)
                _writeMOMQueue.Enqueue(data);
        }

        /// <summary>
        /// 批量入队检测结果
        /// </summary>
        public void EnqueueDetectionResultBatch(IEnumerable<T_BlueFilmDetection> dataList)
        {
            if (_disposed) { Rlog.Warn("[BlueFilmDataQueue] 队列已释放，丢弃批量检测结果写入"); return; }
            if (dataList == null) return;
            foreach (var data in dataList)
                if (data != null)
                    _writeDetectionQueue.Enqueue(data);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════
        // 以下区域将在 Tasks 5-6 中添加：
        //   #region ReadConsumer   → Task 5
        //   #region WriteConsumer  → Task 6
        //   #region RetryTimer     → Task 6
        // ═══════════════════════════════════════════════════════════

        #region ReadConsumer — 异步读取三库视觉检测结果

        /// <summary>
        /// 读取消费者循环 — 从 _readQueue 取请求，查 3 库，聚合结果，通过 TCS 唤醒自动机
        /// 空闲时 10ms 休眠避免 CPU 空转
        /// </summary>
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
                        // 10s 整体超时保护
                        if ((DateTime.Now - request.EnqueueTime).TotalSeconds > 10)
                            result = new CellData { 电芯码 = request.CellCode, 出站结果 = "OK" };
                    }
                    catch (Exception ex)
                    {
                        Rlog.Error($"[BlueFilmDataQueue] ReadConsumer 异常: {ex.Message}");
                    }
                    finally
                    {
                        // 确保不泄漏 TCS
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

        /// <summary>
        /// 处理单个读取请求 — 查本地 + 2 远程库，聚合 NG 缺陷
        /// </summary>
        private async Task<CellData> ProcessReadRequestAsync(DataReadRequest request)
        {
            var allRecords = new List<T_BlueFilmDetection>();
            var result = new CellData
            {
                电芯码 = request.CellCode,
                出站结果 = "OK",
                Ng类型数量 = 0
            };

            // 1. 本地库查询（优先，超时 10s）
            await QuerySingleDbAsync(
                _detectionRepoLocal,
                @"本地(DESKTOP-0F9L4KO\RJ)",
                request.CellCode,
                allRecords,
                timeoutMs: 10000);

            // 2. 远程库1 查询（3s 超时，独立异常隔离）
            await QuerySingleDbAsync(
                _detectionRepoRemote1,
                "远程1(DESKTOP-NHDST87)",
                request.CellCode,
                allRecords,
                timeoutMs: 3000);

            // 3. 远程库2 查询（3s 超时，独立异常隔离）
            await QuerySingleDbAsync(
                _detectionRepoRemote2,
                "远程2(DESKTOP-2ADDTIC)",
                request.CellCode,
                allRecords,
                timeoutMs: 3000);

            // 聚合 NG 缺陷到 CellData
            if (allRecords.Count > 0)
                AggregateDefects(ref result, allRecords);

            return result;
        }

        /// <summary>
        /// 查询单个数据库 — 超时隔离，异常不抛出
        /// </summary>
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
                if (records != null && records.Count > 0)
                {
                    lock (accumulator)
                        accumulator.AddRange(records);
                }
            }
            catch (OperationCanceledException)
            {
                Rlog.Warn($"[BlueFilmDataQueue] 查询超时 | 服务器: {serverLabel} | 电芯码: {cellCode} | 超时: {timeoutMs}ms");
            }
            catch (Exception ex)
            {
                Rlog.Error($"[BlueFilmDataQueue] 查询失败 | 服务器: {serverLabel} | 电芯码: {cellCode} | 异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 聚合 NG 缺陷 — 按 DetectionArea 分组，去重计数，填入 CellData.Ng类型1~8
        /// 逻辑与 AutoRun.UpdateCellDataFromSQLserver 一致
        /// </summary>
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

        #region WriteConsumer — 三库写入（本地优先，远程容错 3s 超时）

        /// <summary>
        /// 检测结果写入消费者
        /// </summary>
        private async Task WriteDetectionConsumerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_writeDetectionQueue.TryDequeue(out var data))
                {
                    try { await WriteDetectionToAllDbsAsync(data); }
                    catch (Exception ex) { Rlog.Error($"[BlueFilmDataQueue] WriteDetectionConsumer 异常: {ex.Message}"); }
                }
                else
                {
                    await Task.Delay(10, token);
                }
            }
        }

        /// <summary>
        /// MOM 出站写入消费者
        /// </summary>
        private async Task WriteMOMConsumerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_writeMOMQueue.TryDequeue(out var data))
                {
                    try { await WriteMOMToAllDbsAsync(data); }
                    catch (Exception ex) { Rlog.Error($"[BlueFilmDataQueue] WriteMOMConsumer 异常: {ex.Message}"); }
                }
                else
                {
                    await Task.Delay(10, token);
                }
            }
        }

        /// <summary>
        /// WriteRouter — Detection 写入三库
        /// 本地优先确保 → 远程1 (3s) → 远程2 (3s)
        /// </summary>
        private async Task WriteDetectionToAllDbsAsync(T_BlueFilmDetection data)
        {
            // 1. 本地库（优先确保，不设超时）
            try
            {
                await Task.Run(() => _detectionRepoLocal.Insert(data));
            }
            catch (Exception ex)
            {
                Rlog.Error($"[BlueFilmDataQueue] 本地库写入失败 | 类型: Detection | 异常: {ex.Message} | 时间: {DateTime.Now}");
                throw; // 本地库失败不吞异常
            }

            // 2. 远程库1（3s 超时，失败入重试队列）
            await WriteToRemoteWithRetryAsync(
                data, _detectionRepoRemote1,
                "DESKTOP-NHDST87",
                Settings.SQLServer远程连接1,
                "Detection");

            // 3. 远程库2（3s 超时，失败入重试队列）
            await WriteToRemoteWithRetryAsync(
                data, _detectionRepoRemote2,
                "DESKTOP-2ADDTIC",
                Settings.SQLServer远程连接2,
                "Detection");
        }

        /// <summary>
        /// WriteRouter — MOM 出站写入三库
        /// </summary>
        private async Task WriteMOMToAllDbsAsync(T_BlueFilmDataMOM data)
        {
            // 1. 本地库
            try
            {
                await Task.Run(() => _momRepoLocal.Insert(data));
            }
            catch (Exception ex)
            {
                Rlog.Error($"[BlueFilmDataQueue] 本地库写入失败 | 类型: MOM | 异常: {ex.Message} | 时间: {DateTime.Now}");
                throw;
            }

            // 2. 远程库1
            await WriteToRemoteWithRetryAsync(
                data, _momRepoRemote1,
                "DESKTOP-NHDST87",
                Settings.SQLServer远程连接1,
                "MOM");

            // 3. 远程库2
            await WriteToRemoteWithRetryAsync(
                data, _momRepoRemote2,
                "DESKTOP-2ADDTIC",
                Settings.SQLServer远程连接2,
                "MOM");
        }

        /// <summary>
        /// 远程库写入 — 3s 超时，失败入重试缓冲区
        /// dynamic repo 参数在运行时调度 Insert(T) 方法
        /// </summary>
        private async Task WriteToRemoteWithRetryAsync<T>(
            T payload,
            dynamic repo,
            string serverName,
            string connString,
            string payloadType)
        {
            using var cts = new CancellationTokenSource(3000);
            try
            {
                await Task.Run(() =>
                {
                    repo.Insert(payload);
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                EnqueueRetryItem(connString, serverName, payload, payloadType,
                    "写入超时 (3s)");
            }
            catch (Exception ex)
            {
                EnqueueRetryItem(connString, serverName, payload, payloadType,
                    ex.Message);
            }
        }

        /// <summary>
        /// 将失败记录入重试缓冲区
        /// </summary>
        private void EnqueueRetryItem(string connString, string serverName,
            object payload, string payloadType, string errorMessage)
        {
            var now = DateTime.Now;
            _retryBuffer.Enqueue(new DataRetryItem
            {
                TargetConnectionString = connString,
                TargetServerName = serverName,
                Payload = payload,
                PayloadType = payloadType,
                RetryCount = 1,
                FirstFailTime = now,
                LastFailTime = now,
                LastErrorMessage = errorMessage
            });

            Rlog.Error($"[BlueFilmDataQueue] 远程库写入失败 | 服务器: {serverName} | 类型: {payloadType} | 异常: {errorMessage} | 时间: {now:yyyy-MM-dd HH:mm:ss}");
        }

        #endregion

        #region RetryTimer — 30s 周期重试补录

        /// <summary>
        /// 重试定时器回调 — 遍历 _retryBuffer，
        /// 成功移除，失败重新入队，超过 10 次标记 Failed 丢弃
        /// </summary>
        private void RetryCallback()
        {
            if (_retryBuffer.IsEmpty) return;

            // 取出所有待重试项
            var items = new List<DataRetryItem>();
            while (_retryBuffer.TryDequeue(out var item))
                items.Add(item);

            foreach (var item in items)
            {
                if (item.RetryCount >= 10)
                {
                    Rlog.Error($"[BlueFilmDataQueue] 重试次数耗尽 | 服务器: {item.TargetServerName} | 首次失败: {item.FirstFailTime:yyyy-MM-dd HH:mm:ss} | 数据: {JsonConvert.SerializeObject(item.Payload)}");
                    continue; // 丢弃
                }

                item.RetryCount++;
                item.LastFailTime = DateTime.Now;

                try
                {
                    using var cts = new CancellationTokenSource(3000);
                    var task = Task.Run(() =>
                    {
                        if (item.PayloadType == "Detection")
                        {
                            var repo = new BlueFilmDetectionRepository(item.TargetConnectionString);
                            repo.Insert((T_BlueFilmDetection)item.Payload);
                        }
                        else if (item.PayloadType == "MOM")
                        {
                            var repo = new BlueFilmDataMOMRepository(item.TargetConnectionString);
                            repo.Insert((T_BlueFilmDataMOM)item.Payload);
                        }
                    }, cts.Token);

                    if (task.Wait(3000, cts.Token))
                    {
                        Rlog.Info($"[BlueFilmDataQueue] 重试成功 | 服务器: {item.TargetServerName} | 第{item.RetryCount}次");
                        continue;
                    }

                    item.LastErrorMessage = "重试超时 (3s)";
                }
                catch (OperationCanceledException)
                {
                    item.LastErrorMessage = "重试超时 (3s)";
                }
                catch (Exception ex)
                {
                    item.LastErrorMessage = ex.Message;
                }

                // 失败，重新入队
                _retryBuffer.Enqueue(item);
            }
        }

        #endregion
    }
}
