using PLCHandler.Models;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace PLCHandler
{
    public sealed class PlcMonitor : IDisposable
    {
        private readonly Dictionary<string, PlcChannel> _channels = new();
        private readonly Dictionary<string, IDisposable> _channelSignalSubs = new();
        private readonly Subject<PlcStatus> _statusSubject = new();
        private readonly Subject<SignalUpdate> _signalSubject = new();
        private readonly PlcConfigService _configService;
        private List<PlcConfig> _plcConfigs = new();
        private List<SignalData> _signals = new();

        public IReadOnlyDictionary<string, PlcChannel> Channels => _channels;
        public IObservable<PlcStatus> StatusStream => _statusSubject.AsObservable();
        public IObservable<SignalUpdate> SignalStream => _signalSubject.AsObservable();
        public IReadOnlyList<PlcConfig> PlcConfigs => _plcConfigs;
        public PlcConfigService ConfigService => _configService;

        public PlcMonitor(PlcConfigService configService)
        {
            _configService = configService;
        }

        public void LoadConfigs()
        {
            _plcConfigs = _configService.LoadPlcConfigs();
            _signals = _configService.LoadSignals();
        }

        public PlcChannel AddChannel(PlcConfig config)
        {
            if (_channels.ContainsKey(config.Id))
                return _channels[config.Id];

            var signals = _signals.FindAll(s => s.PlcId == config.Id);
            var options = new PlcConnectionOptions
            {
                Id = config.Id,
                Name = config.Name,
                Brand = config.Brand,
                IpAddress = config.IpAddress,
                Port = config.Port,
                Slot = config.Slot,
                Channel = config.Channel,
                Group = config.Group
            };

            var connection = PlcConnectionFactory.Create(options);
            var channel = new PlcChannel(config, signals, connection);
            channel.StatusChanged += OnChannelStatusChanged;
            _channels[config.Id] = channel;

            // Forward this channel's signal updates to the merged stream
            _channelSignalSubs[config.Id] = channel.Signals
                .Subscribe(update => _signalSubject.OnNext(update));

            channel.Start();

            return channel;
        }

        public void AddPlcConfig(PlcConfig config)
        {
            _plcConfigs.Add(config);
        }

        public void RemoveChannel(string plcId)
        {
            if (_channels.TryGetValue(plcId, out var channel))
            {
                channel.StatusChanged -= OnChannelStatusChanged;
                channel.Stop();
                channel.Dispose();
                _channels.Remove(plcId);
            }
            if (_channelSignalSubs.TryGetValue(plcId, out var sub))
            {
                sub.Dispose();
                _channelSignalSubs.Remove(plcId);
            }
        }

        public void StartAll()
        {
            foreach (var config in _plcConfigs)
            {
                if (config.IsEnabled)
                    AddChannel(config);
            }
        }

        public void StopAll()
        {
            foreach (var id in new List<string>(_channels.Keys))
                RemoveChannel(id);
        }

        public void SaveConfigs()
        {
            _configService.SavePlcConfigs(_plcConfigs);
            _configService.SaveSignals(_signals);
        }

        public void AddSignal(SignalData signal)
        {
            _signals.Add(signal);
        }

        public void RemoveSignal(string signalId)
        {
            _signals.RemoveAll(s => s.Id == signalId);
        }

        private void OnChannelStatusChanged(object sender, PlcStatus status)
        {
            _statusSubject.OnNext(status);
        }

        #region 外部调用 API —— 拉取查询 + 单次读写

        /// <summary>查询单个信号的最新缓存值</summary>
        public bool TryGetLatestValue(string signalId, out Result<object> value)
        {
            foreach (var ch in _channels.Values)
            {
                if (ch.TryGetLatestValue(signalId, out value))
                    return true;
            }
            value = default;
            return false;
        }

        /// <summary>PLC 是否处于已连接状态</summary>
        public bool IsConnected(string plcId)
        {
            return _channels.TryGetValue(plcId, out var ch) && ch.State == ConnectionState.Connected;
        }

        /// <summary>获取 PLC 的完整连接状态</summary>
        public ConnectionState GetPlcState(string plcId)
        {
            return _channels.TryGetValue(plcId, out var ch) ? ch.State : ConnectionState.Disconnected;
        }

        /// <summary>不依赖轮询，对指定 PLC 上的一个信号执行单次读取</summary>
        public async Task<Result<object>> ReadOnceAsync(string plcId, SignalData signal)
        {
            if (!_channels.TryGetValue(plcId, out var ch))
                return Result<object>.Fail($"PLC '{plcId}' not found");

            return await ch.ReadOnceAsync(signal);
        }

        /// <summary>不依赖轮询，向指定 PLC 地址写入数据</summary>
        public async Task<Result<bool>> WriteAsync(string plcId, string address, byte[] data)
        {
            if (!_channels.TryGetValue(plcId, out var ch))
                return Result<bool>.Fail($"PLC '{plcId}' not found");

            return await ch.WriteOnceAsync(address, data);
        }
        public async Task<Result<bool>> WriteAsync(string plcId, string address, int data)
        {
            if (!_channels.TryGetValue(plcId, out var ch))
                return Result<bool>.Fail($"PLC '{plcId}' not found");

            return await ch.WriteOnceAsync(address, data);
        }

        /// <summary>获取指定 PLC 的所有信号定义（含配置）</summary>
        public IReadOnlyList<SignalData> GetSignalsByPlc(string plcId)
        {
            return _signals.FindAll(s => s.PlcId == plcId);
        }

        /// <summary>获取指定 PLC 的所有信号定义 + 最新值快照，方便外部遍历</summary>
        public List<(SignalData Config, Result<object> Latest)> GetAllSignals(string plcId)
        {
            var signals = _signals.FindAll(s => s.PlcId == plcId);
            var result = new List<(SignalData, Result<object>)>(signals.Count);
            foreach (var s in signals)
            {
                TryGetLatestValue(s.Id, out var val);
                result.Add((s, val));
            }
            return result;
        }

        // ---- 按名称查询（不指定 PLC，自动跨全部 Channel 查找）----

        /// <summary>根据信号名称查找 SignalData，自动解析所属 PlcId</summary>
        public bool TryGetSignalByName(string name, out SignalData signal)
        {
            signal = _signals.Find(s => s.Name == name);
            return signal != null;
        }

        /// <summary>按名称从缓存读取最新值（不指定 PLC）</summary>
        public bool TryGetLatestByName(string name, out Result<object> value)
        {
            if (TryGetSignalByName(name, out var signal))
                return TryGetLatestValue(signal.Id, out value);
            value = default;
            return false;
        }

        /// <summary>按名称单次直接读取（不指定 PLC，自动解析 PlcId 并派发到对应 Channel）</summary>
        public async Task<Result<object>> ReadOnceByNameAsync(string name)
        {
            if (!TryGetSignalByName(name, out var signal))
                return Result<object>.Fail($"Signal '{name}' not found");
            return await ReadOnceAsync(signal.PlcId, signal);
        }

        /// <summary>按名称单次写入（不指定 PLC）</summary>
        public async Task<Result<bool>> WriteByNameAsync(string name, byte[] data)
        {
            if (!TryGetSignalByName(name, out var signal))
                return Result<bool>.Fail($"Signal '{name}' not found");
            return await WriteAsync(signal.PlcId, signal.Address, data);
        }
        public async Task<Result<bool>> WriteByNameAsync(string name, int data)
        {
            if (!TryGetSignalByName(name, out var signal))
                return Result<bool>.Fail($"Signal '{name}' not found");
            return await WriteAsync(signal.PlcId, signal.Address, data);
        }

        #endregion

        public void Dispose()
        {
            StopAll();
            foreach (var sub in _channelSignalSubs.Values)
                sub.Dispose();
            _channelSignalSubs.Clear();
            _statusSubject?.OnCompleted();
            _statusSubject?.Dispose();
            _signalSubject?.OnCompleted();
            _signalSubject?.Dispose();
        }
    }
}
