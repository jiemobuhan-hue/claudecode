using PLCHandler.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace PLCHandler
{
    public sealed class PlcChannel : IDisposable
    {
        private readonly PlcConfig _config;
        private readonly List<SignalData> _signals;
        private readonly IPlcConnection _connection;
        private readonly SignalReader _reader;
        private readonly RetryPolicy _retryPolicy;
        private readonly Subject<SignalUpdate> _signalSubject = new();
        private readonly ConcurrentDictionary<string, Result<object>> _latestValues = new();
        private CancellationTokenSource _loopCts;
        private ConnectionState _state;
        private readonly object _stateLock = new object();

        public string PlcId => _config.Id;
        public IObservable<SignalUpdate> Signals => _signalSubject;
        public IReadOnlyList<SignalData> SignalDefs => _signals;

        public ConnectionState State
        {
            get { lock (_stateLock) return _state; }
            private set
            {
                lock (_stateLock) _state = value;
                StatusChanged?.Invoke(this, new PlcStatus(PlcId, value, _config.Name, _retryPolicy.RetryCount));
            }
        }

        public event EventHandler<PlcStatus> StatusChanged;

        public PlcChannel(PlcConfig config, List<SignalData> signals, IPlcConnection connection)
        {
            _config = config;
            _signals = signals;
            _connection = connection;
            _reader = new SignalReader(_connection);
            _retryPolicy = new RetryPolicy();
            _state = ConnectionState.Disconnected;
        }

        public void Start()
        {
            if (_loopCts != null) return;

            _loopCts = new CancellationTokenSource();
            _ = RunLoopAsync(_loopCts.Token);
        }

        public void Stop()
        {
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = null;
            State = ConnectionState.Disconnected;
        }

        private async Task RunLoopAsync(CancellationToken ct)
        {
            // Phase 1: Connect
            var connected = await _connection.ConnectAsync(ct);
            State = _connection.State;

            if (!connected)
            {
                await ReconnectLoopAsync(ct);
                return;
            }

            _retryPolicy.Reset();

            // Phase 2: Polling loop
            await PollLoopAsync(ct);
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            var intervalMs = _config.PollingIntervalMs > 0 ? _config.PollingIntervalMs : 500;

            while (!ct.IsCancellationRequested)
            {
                foreach (var signal in _signals)
                {
                    if (ct.IsCancellationRequested) break;

                    var result = await _reader.ReadValueAsync(signal);
                    _latestValues[signal.Id] = result;  // cache latest value
                    var update = new SignalUpdate(signal.Id, signal.PlcId, result, DateTime.Now);
                    _signalSubject.OnNext(update);

                    // Any read failure means the connection is dead → reconnect
                    if (!result.IsOk)
                    {
                        State = ConnectionState.Reconnecting;
                        await ReconnectLoopAsync(ct);
                        return;
                    }
                }

                try { await Task.Delay(intervalMs, ct); }
                catch (OperationCanceledException) { break; }
            }

            await _connection.DisconnectAsync();
        }

        private async Task ReconnectLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_retryPolicy.IsExhausted)
            {
                State = ConnectionState.Reconnecting;

                var canRetry = await _retryPolicy.WaitForNextRetryAsync(ct);
                if (!canRetry || ct.IsCancellationRequested) break;

                var connected = await _connection.ConnectAsync(ct);
                State = _connection.State;

                if (connected)
                {
                    _retryPolicy.Reset();
                    State = ConnectionState.Connected;
                    await PollLoopAsync(ct);  // go straight to polling, don't re-connect
                    return;
                }
            }

            State = ConnectionState.Faulted;
        }

        #region 外部调用 API —— 值缓存 + 单次读写

        /// <summary>获取信号的最新缓存值（非轮询，零延迟）</summary>
        public bool TryGetLatestValue(string signalId, out Result<object> value)
        {
            return _latestValues.TryGetValue(signalId, out value);
        }

        /// <summary>不依赖轮询循环，立即读取一个信号的值</summary>
        public async Task<Result<object>> ReadOnceAsync(SignalData signal)
        {
            var result = await _reader.ReadValueAsync(signal);
            _latestValues[signal.Id] = result;
            return result;
        }

        /// <summary>不依赖轮询循环，立即写入一个值到 PLC 地址</summary>
        public async Task<Result<bool>> WriteOnceAsync(string address, byte[] data)
        {
            try
            {
                var result = await Task.Run(() => _connection.Write(address, data));
                return result.IsSuccess ? Result<bool>.Ok(true) : Result<bool>.Fail(result.Message);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }
        public async Task<Result<bool>> WriteOnceAsync(string address, int data)
        {
            try
            {
                var result = await Task.Run(() => _connection.WriteInt(address, data));
                return result.IsSuccess ? Result<bool>.Ok(true) : Result<bool>.Fail(result.Message);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }
        #endregion

        public void Dispose()
        {
            Stop();
            _signalSubject?.OnCompleted();
            _signalSubject?.Dispose();
            _connection?.Dispose();
        }
    }

    public sealed class PlcStatus
    {
        public string PlcId { get; }
        public ConnectionState State { get; }
        public string PlcName { get; }
        public int RetryCount { get; }

        public PlcStatus(string plcId, ConnectionState state, string plcName, int retryCount)
        {
            PlcId = plcId;
            State = state;
            PlcName = plcName;
            RetryCount = retryCount;
        }
    }
}
