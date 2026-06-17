# 异步数据底座实现计划 — 自动机 DB 解耦

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 AutoRun 中的同步 SQL Server 读写解耦到后台队列，自动机线程 <1ms 返回，三库容灾写入。

**Architecture:** 单例 `BlueFilmDataQueueManager` 维护 3 个 `ConcurrentQueue<T>` + 3 个后台消费者 Task。读取用 `TaskCompletionSource` 异步回调，写入用 fire-and-forget 入队。WriteRouter 按本地→远程1→远程2 顺序写入，远程 3s 超时隔离，失败入重试缓冲区 30s 周期补录。

**Tech Stack:** .NET Framework 4.8, C# 9.0, ConcurrentQueue<T>, TaskCompletionSource, ADO.NET (SqlConnection/SqlCommand), 现有 BlueFilmDetectionRepository / BlueFilmDataMOMRepository

**Files:**
- Create: `Service/DataReadRequest.cs`, `Service/DataRetryItem.cs`, `Service/BlueFilmDataQueueManager.cs`
- Modify: `Service/Settings.cs`, `Model/AutoRun.cs`, `View/UC_Setting.xaml.cs`, `View/StateCards/UC_SettingsPages.xaml`

---

### Task 1: 新增连接字符串 Settings 属性

**Files:**
- Modify: `Service/Settings.cs`

- [ ] **Step 1: 在 Settings.cs 中 Database 区域新增 3 个连接字符串属性**

在 `public static string PLC_IP` 行之后，`private static Settings _instance` 行之前，插入：

```csharp
// ── 三库连接字符串（异步数据底座） ──
// 本地主库 — 具名实例 @"" 中写单反斜杠 \RJ
public static string SQLServer本地连接 { get; internal set; }
    = @"Data Source=DESKTOP-0F9L4KO\RJ;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";

// 远程库1
public static string SQLServer远程连接1 { get; internal set; }
    = @"Data Source=DESKTOP-NHDST87;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";

// 远程库2
public static string SQLServer远程连接2 { get; internal set; }
    = @"Data Source=DESKTOP-2ADDTIC;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";
```

- [ ] **Step 2: 验证编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Service/Settings.cs
git commit -m "feat(settings): add 3 SQL Server connection strings for async data layer"
```

---

### Task 2: 创建读取请求模型 DataReadRequest

**Files:**
- Create: `Service/DataReadRequest.cs`

- [ ] **Step 1: 创建 Service/DataReadRequest.cs**

```csharp
using System;
using System.Threading.Tasks;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 异步读取请求 — 承载电芯码 + TaskCompletionSource 回调
    /// 自动机通过 EnqueueReadAsync 入队后，后台 ReadConsumer 查询完成时
    /// 通过 Completion.SetResult() 唤醒自动机线程
    /// </summary>
    internal class DataReadRequest
    {
        /// <summary>电芯条码，用于查询 3 个 SQL Server 视觉库</summary>
        public string CellCode { get; set; }

        /// <summary>通道编号 (1-4)，用于日志追踪</summary>
        public int ChannelNo { get; set; }

        /// <summary>任务完成源，后台消费者通过 SetResult 返回聚合后的 CellData</summary>
        public TaskCompletionSource<CellData> Completion { get; set; }

        /// <summary>入队时间戳，用于 10s 整体超时保护</summary>
        public DateTime EnqueueTime { get; set; }
    }
}
```

- [ ] **Step 2: 验证编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Service/DataReadRequest.cs
git commit -m "feat: add DataReadRequest model for async visual data reading"
```

---

### Task 3: 创建重试记录模型 DataRetryItem

**Files:**
- Create: `Service/DataRetryItem.cs`

- [ ] **Step 1: 创建 Service/DataRetryItem.cs**

```csharp
using System;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 远程库写入失败重试记录
    /// 当远程 SQL Server 写入超时或异常时，记录入 _retryBuffer
    /// 后台定时器每 30s 遍历重试，最多 10 次后标记 Failed 丢弃
    /// </summary>
    internal class DataRetryItem
    {
        /// <summary>目标库完整连接字符串</summary>
        public string TargetConnectionString { get; set; }

        /// <summary>目标服务器名称（用于日志）</summary>
        public string TargetServerName { get; set; }

        /// <summary>待写入的数据实体（T_BlueFilmDetection 或 T_BlueFilmDataMOM）</summary>
        public object Payload { get; set; }

        /// <summary>负载类型标识："Detection" 或 "MOM"</summary>
        public string PayloadType { get; set; }

        /// <summary>已重试次数</summary>
        public int RetryCount { get; set; }

        /// <summary>首次失败时间</summary>
        public DateTime FirstFailTime { get; set; }

        /// <summary>最近一次失败时间</summary>
        public DateTime LastFailTime { get; set; }

        /// <summary>最近一次失败的错误信息</summary>
        public string LastErrorMessage { get; set; }
    }
}
```

- [ ] **Step 2: 验证编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Service/DataRetryItem.cs
git commit -m "feat: add DataRetryItem model for remote DB write retry"
```

---

### Task 4: 创建 BlueFilmDataQueueManager（上篇 — 字段 + 公开 API）

**Files:**
- Create: `Service/BlueFilmDataQueueManager.cs`

- [ ] **Step 1: 创建文件骨架 — 单例 + 队列字段 + 公开 API**

```csharp
using RinKit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

                // 启动后台消费者
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
            _cts?.Cancel();
            _retryTimer?.Dispose();

            try
            {
                // 等待消费完成（最多 5 秒）
                var tasks = new[] { _readConsumerTask, _writeDetectionConsumerTask, _writeMOMConsumerTask };
                Task.WaitAll(tasks, 5000);
            }
            catch { /* 超时不阻塞 Dispose */ }

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
            if (data != null)
                _writeDetectionQueue.Enqueue(data);
        }

        /// <summary>
        /// 非阻塞入队 — MOM 出站数据写入三库
        /// 耗时: <1ms（仅入队操作）
        /// </summary>
        public void EnqueueMOMOutbound(T_BlueFilmDataMOM data)
        {
            if (data != null)
                _writeMOMQueue.Enqueue(data);
        }

        /// <summary>
        /// 批量入队检测结果
        /// </summary>
        public void EnqueueDetectionResultBatch(IEnumerable<T_BlueFilmDetection> dataList)
        {
            if (dataList == null) return;
            foreach (var data in dataList)
                if (data != null)
                    _writeDetectionQueue.Enqueue(data);
        }

        #endregion
    }
}
```

- [ ] **Step 2: 验证编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Service/BlueFilmDataQueueManager.cs
git commit -m "feat(queue): add BlueFilmDataQueueManager singleton skeleton with public API"
```

---

### Task 5: BlueFilmDataQueueManager（中篇 — ReadConsumer + 聚合逻辑）

**Files:**
- Modify: `Service/BlueFilmDataQueueManager.cs`

- [ ] **Step 1: 在 `#endregion` (公开 API) 之后，类结束 `}` 之前，插入 ReadConsumer 实现**

```csharp
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
                    try
                    {
                        var result = await ProcessReadRequestAsync(request);
                        // 10s 整体超时保护
                        if ((DateTime.Now - request.EnqueueTime).TotalSeconds > 10)
                            result = new CellData { 电芯码 = request.CellCode, 出站结果 = "OK" };

                        request.Completion.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        Rlog.Error($"[BlueFilmDataQueue] ReadConsumer 异常: {ex.Message}");
                        // 确保不泄漏 TCS
                        request.Completion.TrySetResult(
                            new CellData { 电芯码 = request.CellCode, 出站结果 = "OK" });
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
                "本地(DESKTOP-0F9L4KO\\RJ)",
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
                .Where(t => t.DetectionResults != "OK"
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

                var defectStr = string.Join(",", defectCounts.Select(kv => $"{kv.Key}×{kv.Value}"));
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
```

- [ ] **Step 2: 验证编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Service/BlueFilmDataQueueManager.cs
git commit -m "feat(queue): add ReadConsumer with 3-DB query and NG defect aggregation"
```

---

### Task 6: BlueFilmDataQueueManager（下篇 — WriteRouter + 重试定时器）

**Files:**
- Modify: `Service/BlueFilmDataQueueManager.cs`

- [ ] **Step 1: 在 ReadConsumer #endregion 之后，Dispose 之前，插入写入消费者和重试逻辑**

注意：需要在文件顶部添加 `using Newtonsoft.Json;` 用于重试日志的序列化。

```csharp
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
                    // 使用 Insert 方法（BlueFilmDetectionRepository 和 BlueFilmDataMOMRepository 都有 Insert）
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
                        // 成功，不重新入队
                        Rlog.Info($"[BlueFilmDataQueue] 重试成功 | 服务器: {item.TargetServerName} | 第{item.RetryCount}次");
                        continue;
                    }

                    // 超时
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
```

- [ ] **Step 2: 验证编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Service/BlueFilmDataQueueManager.cs
git commit -m "feat(queue): add WriteRouter with 3-DB write + 30s retry timer"
```

---

### Task 7: 重构 AutoRun.cs — 替换阻塞 DB 调用为异步队列

**Files:**
- Modify: `Model/AutoRun.cs`

- [ ] **Step 1: 移除旧的 Repository 字段和连接字符串方法**

定位 `AutoRun.cs` 第 284-288 行，删除：

```csharp
// 删除这三行：
private string GetVisionConnectionString() =>
    $"Data Source={Settings.SQLServer视觉地址};Initial Catalog={Settings.SQLServer视觉库名};User ID={Settings.SQLServer视觉用户};Password={Settings.SQLServer视觉密码};TrustServerCertificate=True";

private HarnessMeasureRepository _localHarnessMeasureRepositoryA;
private BlueFilmDetectionRepository _localBlueFilmDetectionRepositoryA;
```

- [ ] **Step 2: 修改 Init() 方法 — 用队列管理器替代 Repository 初始化**

定位 `AutoRun.cs` Init() 方法第 320-321 行，替换：

```csharp
// 旧代码：
_localHarnessMeasureRepositoryA = new HarnessMeasureRepository(GetVisionConnectionString());
_localBlueFilmDetectionRepositoryA = new BlueFilmDetectionRepository(GetVisionConnectionString());

// 替换为：
BlueFilmDataQueueManager.I.Init();
```

- [ ] **Step 3: 替换 ProductLeadStationHandler.ExecuteActionAsync 中的同步读取**

定位 `AutoRun.cs` 第 1726-1732 行区域，替换：

```csharp
// 旧代码：
if (data != null)
{
    lock (_listDataLock)
    {
        try
        {
            //_owner.UpdateCellDataFromSQLserver(ref data);

        }catch(Exception ex)
        {

        }
        
    }

    int way = _owner.getlead(data);
```

```csharp
// 新代码：
if (data != null)
{
    // 异步读取视觉检测结果 — 不阻塞自动机线程
    data = await BlueFilmDataQueueManager.I.EnqueueReadAsync(tempcode, _channelNo);

    int way = _owner.getlead(data);
```

- [ ] **Step 4: 在 SQLite BulkUpsert 后追加 MOM 出站入队**

定位 `AutoRun.cs` 第 1762-1763 行附近，在 `SQLiteGenericHelper.BulkUpsert` 之后追加：

```csharp
var temp = new List<CellData> { data };
SQLiteGenericHelper.BulkUpsert<CellData>(temp, "电芯码", "CellData");

// 构建 MOM 出站数据并入队（非阻塞）
var momData = new T_BlueFilmDataMOM
{
    CellCode = data.电芯码,
    SideCellType = Settings.电芯型号,
    CreateTime = DateTime.Now,
    ParamterCode = "OutboundResult",
    ParameterResult = data.出站结果,
    Value = data.视觉检测结果
};
BlueFilmDataQueueManager.I.EnqueueMOMOutbound(momData);
```

- [ ] **Step 5: 移除 _listDataLock 对象和使用它的 lock 块（已不再需要）**

删除 `AutoRun.cs` 中的：
```csharp
private static readonly object _listDataLock = new object();
```

- [ ] **Step 6: 移除不再需要的 using 语句**

从文件顶部删除：
```csharp
using ZenergyBFSI.Service.CRUDServices; // 如无其他使用
```

如果 `MomHandler` 或 `DashboardService` 等仍在用，则保留相关的 using。

- [ ] **Step 7: 验证编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded. 预期无编译错误。

- [ ] **Step 8: Commit**

```bash
git add Model/AutoRun.cs
git commit -m "refactor(automaton): replace sync SQL Server calls with async queue manager"
```

---

### Task 8: 更新 Settings 页面 — 新增三库连接字符串 VM 属性

**Files:**
- Modify: `View/UC_Setting.xaml.cs`
- Modify: `View/StateCards/UC_SettingsPages.xaml`

- [ ] **Step 1: 在 SettingViewModel 中新增 3 个连接字符串属性**

在 `View/UC_Setting.xaml.cs` 的 `SettingViewModel` 类中，`SQLServer视觉密码` 属性之后，`MOM地址` 属性之前，插入：

```csharp
        // ── SQL Server 三库连接字符串 ──
        public string SQLServer本地连接
        {
            get => Settings.SQLServer本地连接;
            set { Settings.SQLServer本地连接 = value; RaisePropertyChanged(); }
        }

        public string SQLServer远程连接1
        {
            get => Settings.SQLServer远程连接1;
            set { Settings.SQLServer远程连接1 = value; RaisePropertyChanged(); }
        }

        public string SQLServer远程连接2
        {
            get => Settings.SQLServer远程连接2;
            set { Settings.SQLServer远程连接2 = value; RaisePropertyChanged(); }
        }
```

- [ ] **Step 2: 在 LoadSettings() 中添加新属性的通知**

在 `LoadSettings()` 方法的 `RaisePropertyChanged` 调用块中，`SQLServer视觉密码` 之后添加：

```csharp
                RaisePropertyChanged("SQLServer本地连接");
                RaisePropertyChanged("SQLServer远程连接1");
                RaisePropertyChanged("SQLServer远程连接2");
```

- [ ] **Step 3: 在 UC_SettingsPages.xaml 中添加三库连接字符串 UI**

在 `UC_SettingsPages.xaml` 的数据库配置 Section 中，`SQLServer视觉密码` Grid 之后，`</StackPanel>` 和 `</Border>` 之前（约第 210 行），插入新的 UI 区域：

```xml
                        <Separator Margin="0,16" Background="#EEE"/>

                        <TextBlock Text="三库连接字符串（异步数据底座）" FontSize="13" FontWeight="SemiBold"
                                   Foreground="#2962FF" Margin="0,0,0,10"/>

                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="140"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="本地主库连接" Style="{StaticResource FieldLabelStyle}"/>
                            <TextBox Grid.Column="1" Text="{Binding SQLServer本地连接}"
                                     Style="{StaticResource FieldInputStyle}"/>
                        </Grid>

                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="140"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="远程库1 连接" Style="{StaticResource FieldLabelStyle}"/>
                            <TextBox Grid.Column="1" Text="{Binding SQLServer远程连接1}"
                                     Style="{StaticResource FieldInputStyle}"/>
                        </Grid>

                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="140"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="远程库2 连接" Style="{StaticResource FieldLabelStyle}"/>
                            <TextBox Grid.Column="1" Text="{Binding SQLServer远程连接2}"
                                     Style="{StaticResource FieldInputStyle}"/>
                        </Grid>
```

- [ ] **Step 4: 验证编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add View/UC_Setting.xaml.cs View/StateCards/UC_SettingsPages.xaml
git commit -m "feat(ui): add 3-DB connection string fields to settings page"
```

---

### Task 9: 最终验证 — 编译 + 代码审查

- [ ] **Step 1: 完整编译**

```bash
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: 0 errors, 0 warnings (或仅 pre-existing warnings).

- [ ] **Step 2: 检查 using 语句 — AutoRun.cs 中移除未使用的 Service.CRUDServices using**

```bash
grep -n "using ZenergyBFSI.Service.CRUDServices" Model/AutoRun.cs
```

如果该 using 在移除 Repository 字段和 `UpdateCellDataFromSQLserver` 调用后不再需要，删除它。

- [ ] **Step 3: 检查 _listDataLock 引用 — 确保所有 lock 块已清理**

```bash
grep -n "_listDataLock" Model/AutoRun.cs
```

应该无输出（已删除）。

- [ ] **Step 4: 最终 Commit**

```bash
git add Model/AutoRun.cs
git diff --cached --stat
git commit -m "chore: final cleanup — remove unused usings and lock objects"
```
