# PLCHandler 稳定性修复 & UI 重新设计 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 PLCHandler 连接不稳定（单次失败触发重连、重试耗尽永久 Faulted、并发无锁、DeviceLink 阻塞主线程）和 UI 信号混淆（切换 PLC 不清旧数据）两个紧耦合问题。

**Architecture:** 最小修复方案 — 不改文件结构、不新增文件。在现有 PlcChannel/RetryPolicy/OmronConnection/AutoRun/PLCBoardViewModel 上做局部改动。轮询层加连续失败容错（阈值 3），重连层加永久重试（30s 间隔），读写层加 SemaphoreSlim 串行化，UI 层改字典分桶 + TabControl。

**Tech Stack:** C# 9.0, .NET Framework 4.8, HslCommunication 12.8.2, DevExpress v19.1, System.Reactive

---

## 文件改动映射

| 文件 | 职责 | 改动类型 |
|------|------|----------|
| `辅助工程/PLCHandler/PLCHandler/Channel/RetryPolicy.cs` | 重试策略：永久重试 | 修改 |
| `辅助工程/PLCHandler/PLCHandler/Connections/OmronConnection.cs` | FINS 连接：串行化所有读写 | 修改 |
| `辅助工程/PLCHandler/PLCHandler/Channel/PlcChannel.cs` | 轮询通道：连续失败容错 | 修改 |
| `Service/AutoRun.cs` | 自动机：DeviceLink async 化 | 修改 |
| `辅助工程/PLCHandler/Control/ViewModels/PLCBoardViewModel.cs` | ViewModel：信号分桶 | 修改 |
| `辅助工程/PLCHandler/Control/View/PLCBoard.xaml` | 布局：左侧面板→TabControl | 修改 |
| `辅助工程/PLCHandler/Control/View/PLCBoard.xaml.cs` | 事件：Tab 切换处理 | 修改 |
| `辅助工程/PLCHandler/Control/View/SignalMonitorView.xaml` | 信号视图：移除 PlcId 列 | 修改 |

---

### Task 1: RetryPolicy — 永久重试

**Files:**
- Modify: `辅助工程/PLCHandler/PLCHandler/Channel/RetryPolicy.cs`

- [ ] **Step 1: 添加 `_permanentRetryIntervalMs` 字段和 `IsDegraded` 属性**

修改 `RetryPolicy.cs`，在现有字段后添加：

```csharp
// 在 private readonly int _maxDelayMs; 之后添加：
private readonly int _permanentRetryIntervalMs;

// 在 public bool IsExhausted => _retryCount >= _maxRetries; 之后添加：
public bool IsDegraded => _retryCount > _maxRetries;
```

- [ ] **Step 2: 修改构造函数，接收 `_permanentRetryIntervalMs`**

修改构造函数签名和体：

```csharp
// 改前：
public RetryPolicy(int maxRetries = 10, int baseDelayMs = 500, int maxDelayMs = 30000)

// 改后：
public RetryPolicy(int maxRetries = 10, int baseDelayMs = 500, int maxDelayMs = 30000, int permanentRetryIntervalMs = 30000)
{
    _maxRetries = maxRetries;
    _baseDelayMs = baseDelayMs;
    _maxDelayMs = maxDelayMs;
    _permanentRetryIntervalMs = permanentRetryIntervalMs;
}
```

- [ ] **Step 3: 重写 `WaitForNextRetryAsync`，10 次后改用固定间隔**

```csharp
// 完整替换 WaitForNextRetryAsync 方法：
public async Task<bool> WaitForNextRetryAsync(CancellationToken ct = default)
{
    _retryCount++;

    int delay;
    if (_retryCount <= _maxRetries)
    {
        delay = Math.Min(_baseDelayMs * (int)Math.Pow(2, _retryCount - 1), _maxDelayMs);
    }
    else
    {
        delay = _permanentRetryIntervalMs;
    }

    try
    {
        await Task.Delay(delay, ct);
        return true;
    }
    catch (OperationCanceledException)
    {
        return false;
    }
}
```

- [ ] **Step 4: 验证编译**

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "辅助工程/PLCHandler/PLCHandler.csproj" -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add 辅助工程/PLCHandler/PLCHandler/Channel/RetryPolicy.cs
git commit -m "fix(plc): RetryPolicy now retries every 30s instead of giving up after 10 attempts"
```

---

### Task 2: OmronConnection — SemaphoreSlim 串行化

**Files:**
- Modify: `辅助工程/PLCHandler/PLCHandler/Connections/OmronConnection.cs`

- [ ] **Step 1: 添加 SemaphoreSlim 字段**

在 `private readonly object _lock = new object();` 之后添加：

```csharp
private readonly SemaphoreSlim _ioSem = new SemaphoreSlim(1, 1);
```

在文件顶部确保 `using System.Threading;` 存在（`System.Threading` 命名空间中有 `SemaphoreSlim`）。

- [ ] **Step 2: ConnectAsync 加锁**

修改 `ConnectAsync` 方法，`State = ConnectionState.Connecting;` 之前加锁入口：

```csharp
public async Task<bool> ConnectAsync(CancellationToken ct = default)
{
    await _ioSem.WaitAsync(ct);
    try
    {
        State = ConnectionState.Connecting;
        // ... 其余代码不变 ...
    }
    finally
    {
        _ioSem.Release();
    }
}
```

- [ ] **Step 3: DisconnectAsync 加锁**

```csharp
public async Task DisconnectAsync()
{
    await _ioSem.WaitAsync();
    try
    {
        await Task.Run(() =>
        {
            _plc.ConnectClose();
            State = ConnectionState.Disconnected;
        });
    }
    finally
    {
        _ioSem.Release();
    }
}
```

- [ ] **Step 4: 所有 Read*/Write* 方法加锁**

对 `ReadBool`、`ReadInt16`、`ReadUInt16`、`ReadInt32`、`ReadUInt32`、`ReadInt64`、`ReadUInt64`、`ReadFloat`、`ReadDouble`、`ReadString`、`ReadBoolArray`、`ReadInt16Array`、`ReadInt32Array`、`ReadByteArray`、`Write`、`WriteInt` 共 16 个方法，每个方法体包裹为：

```csharp
// 以 ReadBool 为例：
public OperateResult<bool> ReadBool(string address)
{
    _ioSem.Wait();
    try { return _plc.ReadBool(address); }
    finally { _ioSem.Release(); }
}
```

注意：同步方法使用 `_ioSem.Wait()`（阻塞），异步方法使用 `await _ioSem.WaitAsync()`。

- [ ] **Step 5: Dispose 中释放 SemaphoreSlim**

修改 `Dispose()`：

```csharp
public void Dispose()
{
    _plc?.ConnectClose();
    _ioSem?.Dispose();
}
```

- [ ] **Step 6: 验证编译**

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "辅助工程/PLCHandler/PLCHandler.csproj" -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add 辅助工程/PLCHandler/PLCHandler/Connections/OmronConnection.cs
git commit -m "fix(plc): serialize all OmronConnection I/O via SemaphoreSlim to prevent concurrent socket access"
```

---

### Task 3: PlcChannel — 连续失败容错

**Files:**
- Modify: `辅助工程/PLCHandler/PLCHandler/Channel/PlcChannel.cs`

- [ ] **Step 1: 添加连续失败计数器字段**

在 `private readonly object _stateLock = new object();` 之后添加：

```csharp
private int _consecutiveFailures = 0;
private const int FailureThreshold = 3;
```

- [ ] **Step 2: 修改 PollLoopAsync，单次失败不触发重连**

找到 `PollLoopAsync` 方法中的信号读取循环（约第 90-110 行），将：

```csharp
// 改前：
if (!result.IsOk)
{
    State = ConnectionState.Reconnecting;
    await ReconnectLoopAsync(ct);
    return;
}
```

替换为：

```csharp
// 改后：
if (!result.IsOk)
{
    _consecutiveFailures++;
    if (_consecutiveFailures >= FailureThreshold)
    {
        State = ConnectionState.Reconnecting;
        await ReconnectLoopAsync(ct);
        return;
    }
    // 单次/少数失败跳过，继续读下一个信号
    continue;
}
else
{
    _consecutiveFailures = 0;  // 成功时重置计数器
}
```

- [ ] **Step 3: ReconnectLoopAsync 中移除 IsExhausted 判断**

在 `ReconnectLoopAsync` 中，将 `!_retryPolicy.IsExhausted` 改为 `true`（因为 RetryPolicy 永远不 exhaust 了）：

```csharp
// 改前：
while (!ct.IsCancellationRequested && !_retryPolicy.IsExhausted)

// 改后：
while (!ct.IsCancellationRequested)
```

成功重连后重置 `_consecutiveFailures = 0`：

在 `if (connected)` 块中，`_retryPolicy.Reset();` 之后添加：

```csharp
_consecutiveFailures = 0;
```

- [ ] **Step 4: 验证编译**

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "辅助工程/PLCHandler/PLCHandler.csproj" -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add 辅助工程/PLCHandler/PLCHandler/Channel/PlcChannel.cs
git commit -m "fix(plc): require 3 consecutive read failures before triggering reconnect"
```

---

### Task 4: AutoRun.DeviceLink — async 化

**Files:**
- Modify: `Service/AutoRun.cs`

- [ ] **Step 1: 修改 DeviceLink 方法签名和实现**

找到 `private bool DeviceLink()` 方法（约第 489 行），将整个方法替换为：

```csharp
private async Task<bool> DeviceLinkAsync()
{
    if (_monitor != null && _monitor.IsConnected("omron_1") && _monitor.IsConnected("omron_2"))
    {
        await SetInt_Plc("PLC心跳响应", 1);
        await SetInt_Plc("出站心跳", 1);
        Main.uC_StatesBar.uC_StatesBarVM.IsMomConnected = true;
        Main.uC_StatesBar.uC_StatesBarVM.PlcStatusColor = Brushes.LimeGreen;
        return true;
    }
    else
    {
        await SetInt_Plc("PLC心跳响应", 0);
        await SetInt_Plc("出站心跳", 0);
        Main.uC_StatesBar.uC_StatesBarVM.IsMomConnected = false;
        Main.uC_StatesBar.uC_StatesBarVM.PlcStatusColor = Brushes.Red;
        return false;
    }
}
```

同时删除 `heartloop` 字段声明（约第 493 行附近的局部变量）。

- [ ] **Step 2: 修改 Thread_Run 中的调用处**

找到 `Thread_Run` 方法中 `CheckGlobalHeartbeat` 的调用（约第 370-383 行），将 `DeviceLink()` 改为 `await DeviceLinkAsync()`：

```csharp
// 改前：
bool heartbeatOk = CheckGlobalHeartbeat();

// CheckGlobalHeartbeat 方法中的：
bool deviceOk = DeviceLink();

// 改后（在 CheckGlobalHeartbeat 中）：
bool deviceOk = await DeviceLinkAsync();
```

同时将 `CheckGlobalHeartbeat` 方法签名改为 `async Task<bool>`：

```csharp
// 改前：
private bool CheckGlobalHeartbeat()

// 改后：
private async Task<bool> CheckGlobalHeartbeatAsync()
```

在 `Thread_Run` 调用处改为：

```csharp
bool heartbeatOk = await CheckGlobalHeartbeatAsync();
```

- [ ] **Step 3: 验证编译**

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Service/AutoRun.cs
git commit -m "fix(plc): make DeviceLink async, remove Thread.Sleep(1000) blocking from heartbeat loop"
```

---

### Task 5: PLCBoardViewModel — 信号分桶

**Files:**
- Modify: `辅助工程/PLCHandler/Control/ViewModels/PLCBoardViewModel.cs`

- [ ] **Step 1: 将 `_signals` 改为字典分桶**

将（约第 21 行）：

```csharp
private ObservableCollection<SignalDisplayItem> _signals = new();
```

替换为：

```csharp
private readonly Dictionary<string, ObservableCollection<SignalDisplayItem>> _signalsByPlc = new();
private ObservableCollection<SignalDisplayItem> _activeSignals = new();
```

添加 `ActiveSignals` 属性（约第 28 行，在 `Signals` 属性附近）：

```csharp
public ObservableCollection<SignalDisplayItem> ActiveSignals
{
    get => _activeSignals;
    set => SetProperty(ref _activeSignals, value);
}
```

保留现有的 `public ObservableCollection<SignalDisplayItem> Signals => _signals;` 改为：

```csharp
public ObservableCollection<SignalDisplayItem> Signals => _activeSignals;
```

- [ ] **Step 2: 修改 `RefreshSignals` 方法**

```csharp
private void RefreshSignals()
{
    if (string.IsNullOrEmpty(_selectedPlcId)) return;

    // 确保目标 PLC 的信号桶存在
    if (!_signalsByPlc.ContainsKey(_selectedPlcId))
    {
        _signalsByPlc[_selectedPlcId] = new ObservableCollection<SignalDisplayItem>();
    }

    var bucket = _signalsByPlc[_selectedPlcId];
    var signalDefs = _monitor.Channels.TryGetValue(_selectedPlcId, out var ch)
        ? ch.SignalDefs.ToList()
        : new List<SignalData>();

    // 只添加当前 PLC 的信号定义（旧条目清除后重新初始化）
    bucket.Clear();
    foreach (var def in signalDefs)
        bucket.Add(new SignalDisplayItem(def));

    // 切换 ActiveSignals 到当前 PLC 的桶
    ActiveSignals = bucket;
    SignalCountLabel = $"{bucket.Count} 信号";
}
```

- [ ] **Step 3: 修改 `OnSignalUpdate` 方法**

```csharp
private void OnSignalUpdate(SignalUpdate update)
{
    // 确保目标 PLC 的桶存在
    if (!_signalsByPlc.ContainsKey(update.PlcId))
    {
        _signalsByPlc[update.PlcId] = new ObservableCollection<SignalDisplayItem>();
    }

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
        {
            var item = new SignalDisplayItem(cfg, update);
            bucket.Add(item);
        }
    }

    LastRefreshLabel = $"最后刷新: {DateTime.Now:HH:mm:ss}";
}
```

删除原有的 `lock (_signalsLock)` 和 `_signalsLock` 字段 — 分桶后各 PLC 的信号写入已天然隔离，无需锁。

- [ ] **Step 4: 修改构造函数中 `RefreshSignals()` 调用**

在构造函数结尾处（约第 92 行），`RefreshSignals()` 调用无需修改，因为 `_selectedPlcId` 在 `RefreshPlcList()` 中已设置默认值。

- [ ] **Step 5: 验证编译**

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "辅助工程/PLCHandler/PLCHandler.csproj" -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add 辅助工程/PLCHandler/Control/ViewModels/PLCBoardViewModel.cs
git commit -m "fix(ui): replace shared _signals collection with per-PLC dictionary to prevent cross-contamination"
```

---

### Task 6: PLCBoard.xaml — 左侧导航 → TabControl

**Files:**
- Modify: `辅助工程/PLCHandler/Control/View/PLCBoard.xaml`
- Modify: `辅助工程/PLCHandler/Control/View/PLCBoard.xaml.cs`

- [ ] **Step 1: 替换 XAML 布局**

将 `PLCBoard.xaml` 中 `<Grid>` 内的左侧导航面板（约第 16-63 行）替换为 `TabControl`：

```xml
<UserControl x:Class="PLCHandler.Control.View.PLCBoard"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" 
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008" 
             xmlns:local="clr-namespace:PLCHandler.Control.View"
             mc:Ignorable="d" 
             d:DesignHeight="450" d:DesignWidth="800">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 顶部 TabControl -->
        <TabControl Grid.Row="0"
                    x:Name="plcTabs"
                    SelectionChanged="PlcTabs_SelectionChanged"
                    Background="#2D2D30"
                    BorderBrush="#3E3E42"
                    BorderThickness="0,0,0,1">
            <TabControl.Resources>
                <Style TargetType="TabItem">
                    <Setter Property="Background" Value="#2D2D30"/>
                    <Setter Property="Foreground" Value="#888"/>
                    <Setter Property="FontSize" Value="13"/>
                    <Setter Property="Padding" Value="16,8"/>
                    <Setter Property="BorderThickness" Value="0"/>
                    <Style.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter Property="Background" Value="#1E1E1E"/>
                            <Setter Property="Foreground" Value="White"/>
                        </Trigger>
                    </Style.Triggers>
                </Style>
            </TabControl.Resources>
        </TabControl>

        <!-- 主内容区：信号监控 Grid -->
        <ContentControl Grid.Row="1" x:Name="contentArea" Background="#1E1E1E">
            <local:SignalMonitorView x:Name="signalView" DataContext="{Binding}"/>
        </ContentControl>

        <!-- Status Bar -->
        <Border Grid.Row="2" Background="#007ACC" Height="28" Padding="8,0">
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                <TextBlock Text="●" Foreground="#4CAF50" Margin="0,0,6,0"
                           FontSize="10" VerticalAlignment="Center"/>
                <TextBlock Text="{Binding PlcCountLabel}"
                           Foreground="White" FontSize="11" VerticalAlignment="Center"/>
                <TextBlock Text=" | " Foreground="#5A9FD4" FontSize="11" VerticalAlignment="Center"/>
                <TextBlock Text="{Binding SignalCountLabel}"
                           Foreground="White" FontSize="11" VerticalAlignment="Center"/>
                <TextBlock Text=" | " Foreground="#5A9FD4" FontSize="11" VerticalAlignment="Center"/>
                <TextBlock Text="{Binding LastRefreshLabel}"
                           Foreground="White" FontSize="11" VerticalAlignment="Center"/>
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 2: 修改代码后置文件**

将 `PLCBoard.xaml.cs` 替换为：

```csharp
using PLCHandler.View;
using System.Windows;
using System.Windows.Controls;
using ViewModels;

namespace PLCHandler.Control.View
{
    public partial class PLCBoard : UserControl
    {
        private readonly PLCBoardViewModel _vm;

        public PLCBoard()
        {
            InitializeComponent();
            var configDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "Config"
            );
            var configService = new PlcConfigService(configDir);
            var monitor = new PlcMonitor(configService);
            _vm = new PLCBoardViewModel(monitor);
            DataContext = _vm;

            // 初始化 Tab 页
            RefreshTabs();
        }

        public void RefreshTabs()
        {
            plcTabs.Items.Clear();
            foreach (var plc in _vm.PlcList)
            {
                var tab = new TabItem
                {
                    Header = $"{plc.Name} {plc.StatusIcon}",
                    Tag = plc.Id
                };
                plcTabs.Items.Add(tab);
            }

            // 默认选中第一个
            if (plcTabs.Items.Count > 0)
            {
                plcTabs.SelectedIndex = 0;
                _vm.SelectPlc(((TabItem)plcTabs.SelectedItem).Tag as string);
            }
        }

        private void PlcTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (plcTabs.SelectedItem is TabItem tab && tab.Tag is string plcId)
            {
                _vm.SelectPlc(plcId);
            }
        }
    }
}
```

- [ ] **Step 3: 验证编译**

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "辅助工程/PLCHandler/PLCHandler.csproj" -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add 辅助工程/PLCHandler/Control/View/PLCBoard.xaml 辅助工程/PLCHandler/Control/View/PLCBoard.xaml.cs
git commit -m "feat(ui): replace left nav panel with TabControl for PLC signal monitoring"
```

---

### Task 7: SignalMonitorView — 移除 PlcId 列

**Files:**
- Modify: `辅助工程/PLCHandler/Control/View/SignalMonitorView.xaml`

- [ ] **Step 1: 移除 PlcId 列**

在 `SignalMonitorView.xaml` 中，删除最后一列：

```xml
<!-- 删除这行： -->
<dxg:GridColumn FieldName="PlcId" Header="PLC" Width="80" ReadOnly="True"/>
```

因为 Tab 页已按 PLC 分组，`PlcId` 列冗余。SignalMonitorView 的 `ItemsSource` 保持绑定到 `{Binding Signals}`，但由于 `PLCBoardViewModel.Signals` 现在返回 `ActiveSignals`（Task 5 改动），自动只显示当前选中 PLC 的信号。

- [ ] **Step 2: 验证编译**

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "辅助工程/PLCHandler/PLCHandler.csproj" -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add 辅助工程/PLCHandler/Control/View/SignalMonitorView.xaml
git commit -m "feat(ui): remove redundant PlcId column now that tabs provide PLC context"
```

---

### Task 8: 全量编译验证

**Files:** 无（只验证）

- [ ] **Step 1: 全量编译解决方案**

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal
```

Expected: Build succeeded. Zero errors, zero warnings should be below existing baseline.

- [ ] **Step 2: 确认无 Thread.Sleep 残留**

```bash
grep -n "Thread.Sleep" Service/AutoRun.cs
```

Expected: No matches in the heartbeat / DeviceLink path.

---

## 验证测试

以下为手动验证步骤（项目无自动测试框架）：

1. **连续失败容错**：在 PLC IP 不可达场景下，观察日志 — 单次读失败应输出 `continue`，连续 3 次后才触发 `Reconnecting`
2. **永久重试**：断开 PLC 网络 > 2 分钟，恢复后 30s 内应自动重连成功，不再停留在 `Faulted`
3. **UI 无混淆**：切换 PLC1/PLC2 Tab 各 10 次，信号列表始终只显示对应 PLC 的信号，无跨 PLC 数据残留
4. **心跳不阻塞**：主循环日志间隔应稳定在 ~80ms，不再出现 `Thread.Sleep(1000)` 导致的延迟
5. **并发安全**：8 工位同时运行，观察不再出现 `HslCommunication` Socket 异常或 `ObjectDisposedException`

---

## 回滚方案

所有改动集中在 7 个提交中，按 Task 1-7 顺序。如需回滚：

```bash
# 查看提交
git log --oneline -7

# 逐提交回滚（从最后一个开始）
git revert <commit-hash>
```

每个 Task 独立可回滚，不互相依赖（Task 3 依赖 Task 1 的 RetryPolicy 改动，但 Task 1 向后兼容）。
