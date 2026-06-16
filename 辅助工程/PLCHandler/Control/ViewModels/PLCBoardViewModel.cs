using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Media;
using PLCHandler;
using PLCHandler.Models;

namespace ViewModels
{
    public class PLCBoardViewModel : ViewModelBase, IDisposable
    {
        public readonly PlcMonitor _monitor;
        private IDisposable _statusSub;
        private IDisposable _signalSub;
        private string _selectedPlcId;
        private string _selectedView = "PLCConnection";
        private PlcStatusItem _selectedPlc;
        private ObservableCollection<PlcStatusItem> _plcList = new();
        private readonly Dictionary<string, ObservableCollection<SignalDisplayItem>> _signalsByPlc = new();
        private ObservableCollection<SignalDisplayItem> _activeSignals = new();
        private string _plcCountLabel = "未连接";
        private string _signalCountLabel = "0 信号";
        private string _lastRefreshLabel = "最后刷新: --:--:--";

        public ObservableCollection<PlcStatusItem> PlcList => _plcList;
        public ObservableCollection<SignalDisplayItem> Signals => _activeSignals;

        public ObservableCollection<SignalDisplayItem> ActiveSignals
        {
            get => _activeSignals;
            set { if (SetProperty(ref _activeSignals, value)) OnPropertyChanged(nameof(Signals)); }
        }

        public string SelectedPlcId
        {
            get => _selectedPlcId;
            set
            {
                if (SetProperty(ref _selectedPlcId, value))
                    RefreshSignals();
            }
        }

        public string SelectedView
        {
            get => _selectedView;
            set => SetProperty(ref _selectedView, value);
        }

        public PlcStatusItem SelectedPlc
        {
            get => _selectedPlc;
            set => SetProperty(ref _selectedPlc, value);
        }

        public string PlcCountLabel
        {
            get => _plcCountLabel;
            set => SetProperty(ref _plcCountLabel, value);
        }

        public string SignalCountLabel
        {
            get => _signalCountLabel;
            set => SetProperty(ref _signalCountLabel, value);
        }

        public string LastRefreshLabel
        {
            get => _lastRefreshLabel;
            set => SetProperty(ref _lastRefreshLabel, value);
        }

        public bool AnyConnected => _plcList.Any(p => p.State == ConnectionState.Connected);

        public PLCBoardViewModel(PlcMonitor monitor)
        {
            _monitor = monitor;
            _monitor.LoadConfigs();

            // Subscribe to status changes from all channels
            _statusSub = _monitor.StatusStream
                .ObserveOnDispatcher()
                .Subscribe(OnStatusUpdate);

            // Subscribe to signal updates from all channels (dynamically adds new channels)
            _signalSub = _monitor.SignalStream
                .ObserveOnDispatcher()
                .Subscribe(OnSignalUpdate);

            // Start all channels first (creates Channel entries, then starts async polling)
            _monitor.StartAll();

            // Load initial PLC list (channels now exist for SignalDefs lookup)
            RefreshPlcList();
            RefreshSignals();

            // Periodic stats refresh
            var statusTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            statusTimer.Tick += (s, e) => RefreshStats();
            statusTimer.Start();

            System.Diagnostics.Debug.WriteLine("[MainViewModel] initialized with PlcMonitor");
        }

        private void OnStatusUpdate(PlcStatus status)
        {
            var item = _plcList.FirstOrDefault(p => p.Id == status.PlcId);
            if (item != null)
            {
                item.State = status.State;
                item.RetryCount = status.RetryCount;
            }
            else
            {
                var cfg = _monitor.PlcConfigs.FirstOrDefault(c => c.Id == status.PlcId);
                _plcList.Add(new PlcStatusItem
                {
                    Id = status.PlcId,
                    Name = cfg?.Name ?? status.PlcName,
                    Brand = cfg?.Brand.ToString() ?? "",
                    IpAddress = cfg?.IpAddress ?? "",
                    Port = cfg?.Port ?? 0,
                    State = status.State,
                    SignalCount = _monitor.Channels.TryGetValue(status.PlcId, out var ch)
                        ? ch.SignalDefs.Count : 0
                });
            }
            RefreshStats();
        }

        private void OnSignalUpdate(SignalUpdate update)
        {
            if (!_signalsByPlc.ContainsKey(update.PlcId))
                _signalsByPlc[update.PlcId] = new ObservableCollection<SignalDisplayItem>();

            var bucket = _signalsByPlc[update.PlcId];

            var existing = bucket.FirstOrDefault(s => s.Id == update.SignalId);
            if (existing != null)
            {
                existing.Apply(update);
            }
            else
            {
                var cfg = _monitor.Channels.Values
                    .SelectMany(c => c.SignalDefs)
                    .FirstOrDefault(s => s.Id == update.SignalId);
                if (cfg != null)
                    bucket.Add(new SignalDisplayItem(cfg, update));
            }

            LastRefreshLabel = $"最后刷新: {DateTime.Now:HH:mm:ss}";
        }

        private void RefreshPlcList()
        {
            _plcList.Clear();
            foreach (var cfg in _monitor.PlcConfigs)
            {
                _monitor.Channels.TryGetValue(cfg.Id, out var channel);
                _plcList.Add(new PlcStatusItem
                {
                    Id = cfg.Id,
                    Name = cfg.Name,
                    Brand = cfg.Brand.ToString(),
                    IpAddress = cfg.IpAddress,
                    Port = cfg.Port,
                    State = channel?.State ?? ConnectionState.Disconnected,
                    SignalCount = channel?.SignalDefs.Count ?? 0
                });
            }
            RefreshStats();
            if (string.IsNullOrEmpty(_selectedPlcId) && _plcList.Count > 0)
                _selectedPlcId = _plcList[0].Id;
        }

        private void RefreshSignals()
        {
            if (string.IsNullOrEmpty(_selectedPlcId)) return;

            if (!_signalsByPlc.ContainsKey(_selectedPlcId))
                _signalsByPlc[_selectedPlcId] = new ObservableCollection<SignalDisplayItem>();

            var bucket = _signalsByPlc[_selectedPlcId];
            var signalDefs = _monitor.Channels.TryGetValue(_selectedPlcId, out var ch)
                ? ch.SignalDefs.ToList()
                : new List<SignalData>();

            bucket.Clear();
            foreach (var def in signalDefs)
                bucket.Add(new SignalDisplayItem(def));

            ActiveSignals = bucket;
            SignalCountLabel = $"{bucket.Count} 信号";
        }

        private void RefreshStats()
        {
            int connected = _plcList.Count(p => p.State == ConnectionState.Connected);
            PlcCountLabel = connected > 0
                ? $"{connected}/{_plcList.Count} PLC 已连接"
                : $"{_plcList.Count} 个 PLC";
            OnPropertyChanged(nameof(AnyConnected));
        }

        public void SelectPlc(string plcId)
        {
            SelectedPlcId = plcId;
            SelectedPlc = _plcList.FirstOrDefault(p => p.Id == plcId);
        }

        public void Dispose()
        {
            _statusSub?.Dispose();
            _signalSub?.Dispose();
            _monitor?.Dispose();
        }
    }

    // ---- Sub-classes kept in same file for compatibility ----

    public class PlcStatusItem : ViewModelBase
    {
        private ConnectionState _state = ConnectionState.Disconnected;
        private int _retryCount;
        private Brush _statusColor;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int SignalCount { get; set; }
        public int RetryCount
        {
            get => _retryCount;
            set => SetProperty(ref _retryCount, value);
        }

        public ConnectionState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusIcon));
                    StatusColor = _state switch
                    {
                        ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                        ConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
                        ConnectionState.Reconnecting => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                        ConnectionState.Faulted => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                        _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
                    };
                }
            }
        }

        public bool IsConnected => _state == ConnectionState.Connected;

        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public string IpPort => $"{IpAddress}:{Port}";

        public string StatusText => _state switch
        {
            ConnectionState.Connected => "已连接",
            ConnectionState.Connecting => "连接中...",
            ConnectionState.Reconnecting => $"重连中(第{_retryCount}次)",
            ConnectionState.Faulted => "故障",
            _ => "离线"
        };

        public string StatusIcon => _state switch
        {
            ConnectionState.Connected => "●",
            ConnectionState.Connecting => "◐",
            ConnectionState.Reconnecting => "◑",
            ConnectionState.Faulted => "○",
            _ => "○"
        };
    }

    public class SignalDisplayItem : ViewModelBase
    {
        private object _value;
        private bool _isChanged;
        private DateTime _lastUpdateTime;
        private string _displayValue;
        private Brush _valueColor;
        private string _lastError;

        public string Id { get; }
        public string Name { get; }
        public string Address { get; }
        public DataTypeEnum DataType { get; }
        public int ArrayLength { get; }
        public string Group { get; }
        public string PlcId { get; }

        public object Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public bool IsChanged
        {
            get => _isChanged;
            set => SetProperty(ref _isChanged, value);
        }

        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set => SetProperty(ref _lastUpdateTime, value);
        }

        public string DisplayValue
        {
            get => _displayValue;
            set => SetProperty(ref _displayValue, value);
        }

        public Brush ValueColor
        {
            get => _valueColor;
            set => SetProperty(ref _valueColor, value);
        }

        public string LastError
        {
            get => _lastError;
            set => SetProperty(ref _lastError, value);
        }

        public string StatusIcon => LastError != null ? "⚠" : "●";
        public string ChangeIcon => IsChanged ? "★" : "";

        /// <summary>Create from config (initial state, no data yet)</summary>
        public SignalDisplayItem(SignalData def)
        {
            Id = def.Id;
            Name = def.Name;
            Address = def.Address;
            DataType = def.DataType;
            ArrayLength = def.ArrayLength;
            Group = def.Group;
            PlcId = def.PlcId;
            DisplayValue = "---";
            ValueColor = Brushes.White;
            LastUpdateTime = DateTime.MinValue;
        }

        /// <summary>Create from config + update (has data)</summary>
        public SignalDisplayItem(SignalData def, SignalUpdate update) : this(def)
        {
            Apply(update);
        }

        public void Apply(SignalUpdate update)
        {
            LastUpdateTime = update.Timestamp;

            if (update.Value.IsOk)
            {
                var previousValue = Value;
                Value = update.Value.Value;
                IsChanged = !Equals(Value, previousValue);
                DisplayValue = FormatValue(update.Value.Value, DataType);
                ValueColor = Brushes.White;
                LastError = null;
            }
            else
            {
                IsChanged = false;
                LastError = update.Value.Error;
                DisplayValue = $"Err: {update.Value.Error}";
                ValueColor = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
            }

            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(ChangeIcon));
        }

        private static string FormatValue(object value, DataTypeEnum dataType)
        {
            if (value == null) return "null";

            return dataType switch
            {
                DataTypeEnum.Bool => (bool)value ? "ON" : "OFF",
                DataTypeEnum.BoolArray when value is bool[] barr => "[" + string.Join(",", barr) + "]",
                DataTypeEnum.ShortArray when value is short[] sarr => "[" + string.Join(",", sarr) + "]",
                DataTypeEnum.IntArray when value is int[] iarr => "[" + string.Join(",", iarr) + "]",
                _ => value.ToString()
            };
        }
    }
}
