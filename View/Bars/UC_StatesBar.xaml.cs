using DevExpress.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ZenergyBFSI.Model;
using ZenergyBFSI.Service;

namespace ZenergyBFSI.View.Bars
{
    public partial class UC_StatesBar : UserControl
    {
        public UC_StatesBarVM uC_StatesBarVM = new UC_StatesBarVM();

        public UC_StatesBar()
        {
            InitializeComponent();
            this.DataContext = uC_StatesBarVM;
        }
    }

    public class StationDotInfo : INotifyPropertyChanged
    {
        private Brush _color = Brushes.Gray;
        private string _toolTip = "";

        public string Name { get; }

        public Brush Color
        {
            get => _color;
            set
            {
                if (!Equals(_color, value))
                {
                    _color = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
                }
            }
        }

        public string ToolTip
        {
            get => _toolTip;
            set
            {
                if (_toolTip != value)
                {
                    _toolTip = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolTip)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public StationDotInfo(string name)
        {
            Name = name;
            ToolTip = $"{name}: Idle";
        }
    }

    public class UC_StatesBarVM : ViewModelBase
    {
        private DispatcherTimer _pollTimer;

        public UC_StatesBarVM()
        {
            for (int i = 1; i <= 8; i++)
            {
                string name = i <= 4 ? $"来料{i}" : $"分流{i - 4}";
                StationDots.Add(new StationDotInfo(name));
            }

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();
        }

        // ---- PLC / MOM (existing, push pattern from AutoRun.DeviceLinkAsync) ----

        public Brush PlcStatusColor
        {
            get { return GetValue<Brush>(); }
            set
            {
                if (SetValue(value))
                    RaisePropertyChanged("PlcStatusColor");
            }
        }

        public Brush MesStatusColor => IsMomConnected ? Brushes.LimeGreen : Brushes.Red;

        public Brush ModeColor => IsAutoMode ?
            (Brush)new BrushConverter().ConvertFrom("#2E7D32") :
            (Brush)new BrushConverter().ConvertFrom("#EF6C00");

        public bool IsPlcConnected
        {
            get { return GetValue<bool>(); }
            set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("PlcStatusColor");
                    this.PlcStatusColor = this.IsPlcConnected ? Brushes.LimeGreen : Brushes.Red;
                }
            }
        }

        private bool _isMomConnected;
        public bool IsMomConnected
        {
            get => _isMomConnected;
            set
            {
                if (_isMomConnected != value)
                {
                    _isMomConnected = value;
                    RaisePropertyChanged("MesStatusColor");
                }
            }
        }
        public bool IsAutoMode { get; set; }
        public string CurrentUserName { get; set; }

        // ---- 自动机状态 ----

        public AutoRun.AutomatonState AutomatonState
        {
            get => GetValue<AutoRun.AutomatonState>();
            private set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("AutomatonState");
                    RaisePropertyChanged("AutomatonStateColor");
                    RaisePropertyChanged("AutomatonStateText");
                }
            }
        }

        public Brush AutomatonStateColor => AutomatonState switch
        {
            AutoRun.AutomatonState.Running => Brushes.LimeGreen,
            AutoRun.AutomatonState.Error => Brushes.Red,
            _ => Brushes.Gray
        };

        public string AutomatonStateText => AutomatonState switch
        {
            AutoRun.AutomatonState.Running => "自动运行中",
            AutoRun.AutomatonState.Error => "自动机故障",
            _ => "自动机停止"
        };

        // ---- 心跳状态 ----

        public AutoRun.GlobalHeartbeatState HeartbeatState
        {
            get => GetValue<AutoRun.GlobalHeartbeatState>();
            private set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("HeartbeatState");
                    RaisePropertyChanged("HeartbeatStateColor");
                    RaisePropertyChanged("HeartbeatStateText");
                }
            }
        }

        public Brush HeartbeatStateColor => HeartbeatState switch
        {
            AutoRun.GlobalHeartbeatState.Healthy => Brushes.LimeGreen,
            AutoRun.GlobalHeartbeatState.Lost => Brushes.Red,
            AutoRun.GlobalHeartbeatState.Recovering =>
                new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            _ => Brushes.Gray
        };

        public string HeartbeatStateText => HeartbeatState switch
        {
            AutoRun.GlobalHeartbeatState.Healthy => "心跳正常",
            AutoRun.GlobalHeartbeatState.Lost => "心跳丢失",
            AutoRun.GlobalHeartbeatState.Recovering => "心跳恢复中",
            _ => "未知"
        };

        // ---- 故障计数 ----

        public int ErrorCount
        {
            get => GetValue<int>();
            private set
            {
                if (SetValue(value))
                {
                    RaisePropertyChanged("ErrorCount");
                    RaisePropertyChanged("ErrorCountText");
                    RaisePropertyChanged("ErrorCountVisible");
                }
            }
        }

        public string ErrorCountText => ErrorCount > 0 ? $"故障: {ErrorCount}" : "";
        public Visibility ErrorCountVisible =>
            ErrorCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        // ---- 8工位指示灯 ----

        public ObservableCollection<StationDotInfo> StationDots { get; }
            = new ObservableCollection<StationDotInfo>();

        // ---- 数据库连接状态 ----

        private Brush _dbLocalStatus = Brushes.Gray;
        public Brush DbLocalStatus
        {
            get => _dbLocalStatus;
            set
            {
                if (_dbLocalStatus != value)
                {
                    _dbLocalStatus = value;
                    RaisePropertyChanged("DbLocalStatus");
                    RaisePropertyChanged("DbLocalToolTip");
                }
            }
        }

        private Brush _dbRemote1Status = Brushes.Gray;
        public Brush DbRemote1Status
        {
            get => _dbRemote1Status;
            set
            {
                if (_dbRemote1Status != value)
                {
                    _dbRemote1Status = value;
                    RaisePropertyChanged("DbRemote1Status");
                    RaisePropertyChanged("DbRemote1ToolTip");
                }
            }
        }

        private Brush _dbRemote2Status = Brushes.Gray;
        public Brush DbRemote2Status
        {
            get => _dbRemote2Status;
            set
            {
                if (_dbRemote2Status != value)
                {
                    _dbRemote2Status = value;
                    RaisePropertyChanged("DbRemote2Status");
                    RaisePropertyChanged("DbRemote2ToolTip");
                }
            }
        }

        public string DbLocalToolTip => "本地库 " + (DbLocalStatus == Brushes.LimeGreen ? "在线" : "离线");
        public string DbRemote1ToolTip => "远程库1 " + (DbRemote1Status == Brushes.LimeGreen ? "在线" : "离线");
        public string DbRemote2ToolTip => "远程库2 " + (DbRemote2Status == Brushes.LimeGreen ? "在线" : "离线");

        // ---- 时钟 ----

        public DateTime CurrentTime
        {
            get => GetValue<DateTime>();
            private set
            {
                if (SetValue(value))
                    RaisePropertyChanged("CurrentTime");
            }
        }

        // ---- 轮询 ----

        private void OnPollTick(object sender, EventArgs e)
        {
            try
            {
                var autoRun = AutoRun.I;

                AutomatonState = autoRun.CurrentAutomatonState;
                HeartbeatState = autoRun.CurrentHeartbeatState;
                ErrorCount = autoRun.Flag_Error;

                // MOM 状态从 MomHandler 读取（非 PLC 连接状态）
                IsMomConnected = MomHandler.I.IsOnline;

                var states = autoRun.StationStates;
                for (int i = 0; i < StationDots.Count && i < 8; i++)
                {
                    int stationId = i + 1;
                    if (states.TryGetValue(stationId, out var state))
                    {
                        StationDots[i].Color = state switch
                        {
                            AutoRun.StationState.Idle => Brushes.Gray,
                            AutoRun.StationState.Running => Brushes.LimeGreen,
                            AutoRun.StationState.Paused =>
                                new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                            AutoRun.StationState.Error => Brushes.Red,
                            _ => Brushes.Gray
                        };
                        var stateText = state switch
                        {
                            AutoRun.StationState.Idle => "空闲",
                            AutoRun.StationState.Running => "运行中",
                            AutoRun.StationState.Paused => "暂停",
                            AutoRun.StationState.Error => "故障",
                            _ => "未知"
                        };
                        StationDots[i].ToolTip = $"{StationDots[i].Name}: {stateText}";
                    }
                }

                // 数据库连接状态
                try
                {
                    var dbHealth = BlueFilmDataQueueManager.I.GetDbHealth();
                    DbLocalStatus = dbHealth.TryGetValue(@"本地(DESKTOP-0F9L4KO\RJ)", out var dbl) && dbl
                        ? Brushes.LimeGreen : Brushes.Red;
                    DbRemote1Status = dbHealth.TryGetValue("DESKTOP-NHDST87", out var dbr1) && dbr1
                        ? Brushes.LimeGreen : Brushes.Red;
                    DbRemote2Status = dbHealth.TryGetValue("DESKTOP-2ADDTIC", out var dbr2) && dbr2
                        ? Brushes.LimeGreen : Brushes.Red;
                }
                catch { /* QueueManager may not be initialized yet */ }

                CurrentTime = DateTime.Now;
            }
            catch
            {
                // AutoRun may not be initialized yet
            }
        }
    }
}
