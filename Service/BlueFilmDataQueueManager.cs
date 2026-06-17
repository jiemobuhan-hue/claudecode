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

        #pragma warning disable CS1998 // Async method lacks 'await' — 将在后续 Task 中实现
        private async Task ReadConsumerLoop(CancellationToken token)
        {
            // 将在 Task 5 中实现
            await Task.CompletedTask;
        }

        private async Task WriteDetectionConsumerLoop(CancellationToken token)
        {
            // 将在 Task 6 中实现
            await Task.CompletedTask;
        }

        private async Task WriteMOMConsumerLoop(CancellationToken token)
        {
            // 将在 Task 6 中实现
            await Task.CompletedTask;
        }
        #pragma warning restore CS1998

        private void RetryCallback()
        {
            // 将在 Task 6 中实现
        }
    }
}
