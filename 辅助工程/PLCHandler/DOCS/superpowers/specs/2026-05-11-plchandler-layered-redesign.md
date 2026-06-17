# PLCHandler 分层重构设计

## 1. 概述与目标

将 PLCHandler 从单体上帝对象重构为分层架构，解决连接状态不可靠、数据读取静默失败、错误信息混入数据流三大根因问题。

**核心目标：**
- 连接状态从模糊 `bool` 升级为显式五态状态机
- 数据读取从 `object`+错误字符串 升级为 `Result<T>` 值类型
- 信号通知从 `event` 升级为 `IObservable<T>` 声明式流
- 消除 PlcHandler 上帝对象，拆为 PlcMonitor + PlcChannel + IPlcConnection 三层
- 内置指数退避重连策略
- 目标品牌：Siemens S7-1200、Omron FinsTCP
- 目标信号类型：Int、Short、数组，只做监控不写

## 2. 分层架构

```
┌─────────────────────────────────────────────────────┐
│  UI Layer (MVVM + DevExpress)                       │
│  MainViewModel → 只订阅 IObservable，不做业务逻辑    │
├─────────────────────────────────────────────────────┤
│  Monitor Layer (PlcMonitor)                         │
│  编排：Channel 创建/销毁，汇总状态流                  │
│  每个 PLC 对应一个 PlcChannel，生命周期隔离            │
├─────────────────────────────────────────────────────┤
│  Channel Layer (PlcChannel)                         │
│  单 PLC 完整生命周期：连接→轮询→读取→重连             │
│  持有 IPlcConnection + SignalReader + RetryPolicy    │
│  对外暴露 IObservable<SignalUpdate> 和 State         │
├─────────────────────────────────────────────────────┤
│  Connection Layer (IPlcConnection)                  │
│  Connect / Disconnect / Read，同步方法               │
│  连接状态机：Disconnected→Connecting→Connected       │
│                     ↑                   ↓           │
│                Reconnecting ←─── Faulted            │
└─────────────────────────────────────────────────────┘
```

## 3. Result<T> 值类型

成功和失败的路径完全分离，不使用 object + 错误字符串：

```csharp
public readonly struct Result<T>
{
    public bool IsOk { get; }
    public T Value { get; }          // IsOk=true 时有效
    public string Error { get; }     // IsOk=false 时有效
    
    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(string error) => new(error);
}
```

SignalReader 返回 `Result<object>`，调用方直接判断 `IsOk`：

```csharp
var result = await reader.ReadValueAsync(signal);
if (result.IsOk)
    signal.Value = result.Value;
else
    signal.LastError = result.Error;
```

## 4. 连接状态机

五态枚举替代 `bool IsConnected`：

| 状态 | 含义 | UI 展示 |
|------|------|---------|
| Disconnected | 初始，未发起连接 | 灰色 "离线" |
| Connecting | TCP 握手中 | 黄色 "连接中..." |
| Connected | 已连接可读写 | 绿色 "已连接" |
| Reconnecting | 断开后自动重连中 | 橙色 "重连中(第N次)" |
| Faulted | 连续失败超限，需人工介入 | 红色 "故障" |

`IPlcConnection` 暴露 `State` 属性和 `IObservable<ConnectionState> StateChanges` 流。

## 5. IObservable 信号流

用 System.Reactive 替代 `event EventHandler<SignalData>`：

```csharp
public class SignalUpdate
{
    public string SignalId { get; init; }
    public string PlcId { get; init; }
    public Result<object> Value { get; init; }
    public DateTime Timestamp { get; init; }
}
```

PlcChannel 暴露 `IObservable<SignalUpdate> Signals`。

ViewModel 声明式订阅，自动在 UI 线程更新：

```csharp
_channel.Signals
    .ObserveOnDispatcher()
    .Subscribe(update => ApplyUpdate(update));
```

三层数据模型不混用：

| 层级 | 模型 | 职责 |
|------|------|------|
| 配置 | `SignalData` | Id/Name/Address/DataType/ArrayLength — 静态配置，来自 JSON/CSV |
| 运行时 | `SignalUpdate` | SignalId/PlcId/Result\<object\> Value/Timestamp — 每次读取的 DTO |
| UI | `SignalDisplayItem` | DisplayValue/ValueColor/IsConnected/LastError — ViewModel 绑定项，从 SignalUpdate 增量更新 |

## 6. 重连策略

内置于 PlcChannel，不在连接层：

- 指数退避：`backoff = min(2^n * 500ms, 30s)`
- 最大重试次数：10 次
- 超过上限 → State = Faulted，停止重连
- 连接恢复 → 重置计数，State = Connected

## 7. PlcChannel

单 PLC 的完整生命周期容器：

```csharp
public class PlcChannel : IDisposable
{
    public string PlcId { get; }
    public ConnectionState State { get; }
    public IObservable<SignalUpdate> Signals { get; }
    
    public void Start();   // 连接 → 启动轮询
    public void Stop();    // 停止轮询 → 断开
}
```

内部持有 IPlcConnection、SignalReader、RetryPolicy、轮询 Timer。

## 8. PlcMonitor

编排层，管理所有 Channel：

```csharp
public class PlcMonitor
{
    public IReadOnlyDictionary<string, PlcChannel> Channels { get; }
    public IObservable<PlcStatus> StatusStream { get; }
    
    public PlcChannel AddChannel(PlcConfig config, IList<SignalData> signals);
    public void RemoveChannel(string plcId);
}
```

## 9. UI 层

布局不变（左侧导航 + 右侧内容区）。ViewModel 通过 DI 接收 PlcMonitor，用 Rx 订阅 Stream 自动更新绑定集合。

## 10. 文件结构

```
PLCHandler/
├── Core/
│   ├── Result.cs
│   ├── ConnectionState.cs
│   └── SignalUpdate.cs
├── Connection/
│   ├── IPlcConnection.cs
│   ├── SiemensConnection.cs
│   ├── OmronConnection.cs
│   ├── MitsubishiConnection.cs
│   └── ModbusConnection.cs
├── Channel/
│   ├── PlcChannel.cs
│   ├── SignalReader.cs
│   └── RetryPolicy.cs
├── PlcMonitor.cs
├── PlcConfigService.cs
└── Models/
    ├── PlcConfig.cs
    ├── SignalData.cs
    └── ...
View/
├── PLCConnectionView.xaml/.cs
└── SignalMonitorView.xaml/.cs
ViewModels/
├── MainViewModel.cs
├── PlcStatusItem.cs
└── SignalDisplayItem.cs
```

## 11. 非功能要求

- 编译 0 errors
- 配置为 127.0.0.1 时可启动，无崩溃，UI 展示 "连接中→重连中→故障" 完整状态链
- 连接真 PLC 时信号实时刷新
- 100+ 信号下 UI 不卡顿
- SIEMENS S7-1200 和 OMRON FinsTCP 两个品牌优先保证可用
