using PLCBar.Service;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PLCBar.PlcHandler
{
    // ────────────────────────────────────────────────────────────
    // 单行 PLC 配置项（绑定到列表中的每张卡片）
    // ────────────────────────────────────────────────────────────
    public class PlcConfigItem : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private string _plcId;
        private string _ipAddress;
        private string _port;
        private bool   _isConnected;

        public string PlcId
        {
            get => _plcId;
            set { _plcId = value; OnPropertyChanged(); }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                _ipAddress = value;
                OnPropertyChanged();
                ValidateIp();
            }
        }

        public string Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged();
                ValidatePort();
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusForeground));
            }
        }

        // ── 状态指示 ───────────────────────────────────────────
        public Color  StatusColor      => IsConnected ? Color.FromRgb(0x4A, 0xDE, 0x80)
                                                       : Color.FromRgb(0xF8, 0x71, 0x71);
        public string StatusText       => IsConnected ? "已连接" : "未连接";
        public Brush  StatusForeground => IsConnected ? new SolidColorBrush(Color.FromRgb(0x0F, 0x6E, 0x56))
                                                       : new SolidColorBrush(Color.FromRgb(0xA3, 0x2D, 0x2D));

        // ── 验证 ───────────────────────────────────────────────
        private readonly System.Collections.Generic.Dictionary<string, string> _errors = new();

        public bool HasErrors => _errors.Count > 0;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (propertyName != null && _errors.TryGetValue(propertyName, out var msg))
                return new[] { msg };
            return Enumerable.Empty<string>();
        }

        private void ValidateIp()
        {
            const string key = nameof(IpAddress);
            if (string.IsNullOrWhiteSpace(_ipAddress))
            {
                _errors[key] = "IP 地址不能为空";
            }
            else if (!IPAddress.TryParse(_ipAddress, out _))
            {
                _errors[key] = "IP 地址格式无效（示例：192.168.1.101）";
            }
            else
            {
                _errors.Remove(key);
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(key));
            OnPropertyChanged(nameof(HasErrors));
        }

        private void ValidatePort()
        {
            const string key = nameof(Port);
            if (!int.TryParse(_port, out int p) || p < 1 || p > 65535)
                _errors[key] = "端口需为 1~65535 的整数";
            else
                _errors.Remove(key);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(key));
            OnPropertyChanged(nameof(HasErrors));
        }

        // ── INotifyPropertyChanged ─────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ────────────────────────────────────────────────────────────
    // ViewModel
    // ────────────────────────────────────────────────────────────
    public class PlcConfigViewModel : INotifyPropertyChanged
    {
        private readonly PlcConfigService _configService = new();
        private string _message = string.Empty;

        public event EventHandler<bool>? CloseRequested;

        public ObservableCollection<PlcConfigItem> PlcItems { get; } = new();

        // ── 提示消息 ────────────────────────────────────────────
        public string Message
        {
            get => _message;
            private set { _message = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMessage)); }
        }
        public bool HasMessage => !string.IsNullOrEmpty(_message);

        // ── Commands ────────────────────────────────────────────
        public ICommand SaveCommand   { get; }
        public ICommand AddPlcCommand { get; }
        public ICommand DeletePlcCommand { get; }

        // ────────────────────────────────────────────────────────
        public PlcConfigViewModel(
            System.Collections.Generic.Dictionary<string, (string IpAddress, int Port)> connections,
            System.Collections.Generic.Dictionary<string, bool> statuses)
        {
            // 从当前运行时填充列表
            foreach (var (id, info) in connections)
            {
                statuses.TryGetValue(id, out bool connected);
                PlcItems.Add(new PlcConfigItem
                {
                    PlcId       = id,
                    IpAddress   = info.IpAddress,
                    Port        = info.Port.ToString(),
                    IsConnected = connected
                });
            }

            // 如果当前没有任何 PLC，加一个默认项
            if (PlcItems.Count == 0)
                PlcItems.Add(new PlcConfigItem { PlcId = "PLC1", IpAddress = "192.168.1.101", Port = "9600" });

            SaveCommand      = new RelayCommand(ExecuteSave, CanSave);
            AddPlcCommand    = new RelayCommand(ExecuteAdd);
            DeletePlcCommand = new RelayCommand<string>(ExecuteDelete);
        }

        // ── 保存 ────────────────────────────────────────────────
        private bool CanSave()
            => PlcItems.All(p => !p.HasErrors) && PlcItems.Count > 0;

        private void ExecuteSave()
        {
            try
            {
                var dict = PlcItems.ToDictionary(
                    p => p.PlcId,
                    p => (p.IpAddress, int.Parse(p.Port)));

                _configService.SaveConfig(dict);

                Message = $"已保存 {PlcItems.Count} 个 PLC 配置到 plc_config.json，重启后生效。";

                // 延迟关闭，让用户看到提示
                System.Threading.Tasks.Task.Delay(1200).ContinueWith(_ =>
                    Application.Current.Dispatcher.Invoke(() => CloseRequested?.Invoke(this, true)));
            }
            catch (Exception ex)
            {
                Message = $"保存失败：{ex.Message}";
            }
        }

        // ── 添加 ────────────────────────────────────────────────
        private void ExecuteAdd()
        {
            int next = PlcItems.Count + 1;
            // 保证 PlcId 不重复
            string newId = $"PLC{next}";
            while (PlcItems.Any(p => p.PlcId == newId))
                newId = $"PLC{++next}";

            PlcItems.Add(new PlcConfigItem
            {
                PlcId     = newId,
                IpAddress = "192.168.1.101",
                Port      = "9600"
            });
            Message = string.Empty;
        }

        // ── 删除 ────────────────────────────────────────────────
        private void ExecuteDelete(string? plcId)
        {
            if (plcId == null) return;
            if (PlcItems.Count <= 1)
            {
                Message = "至少需要保留一个 PLC 配置。";
                return;
            }
            var item = PlcItems.FirstOrDefault(p => p.PlcId == plcId);
            if (item != null)
            {
                PlcItems.Remove(item);
                Message = string.Empty;
            }
        }

        // ── INotifyPropertyChanged ──────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ────────────────────────────────────────────────────────────
    // 通用 RelayCommand
    // ────────────────────────────────────────────────────────────
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute    = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute    = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
    }
}
