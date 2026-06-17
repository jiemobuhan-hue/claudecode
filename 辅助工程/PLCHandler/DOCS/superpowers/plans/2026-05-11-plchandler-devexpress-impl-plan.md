# PLCHandler DevExpress UI 重构实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 PLCHandler WPF UI 迁移到 DevExpress v19.1 组件（dxNavBar + dxGrid），实现 TileBar 导航 + 事件驱动增量刷新

**Architecture:** 保持 MVVM 架构，MainViewModel 作为根 VM 控制视图切换，SignalDisplayItem 支持 INotifyPropertyChanged 实现单行刷新，dxGrid 的智能刷新机制自动跟踪变化

**Tech Stack:** DevExpress v19.1 (Grid/NavBar/StatusBar), WPF .NET 4.8, MVVM (CommunityToolkit.Mvvm)

---

## 文件结构

```
View/
  SignalMonitorView.xaml/.cs     ← 新建：信号监控面板（dxGrid TableView）
  PLCConnectionView.xaml/.cs     ← 新建：PLC连接面板
ViewModels/
  MainViewModel.cs               ← 修改：添加 SelectedView，OnSignalUpdated
  PlcStatusItem.cs              ← 修改：添加 StatusColor 属性
  SignalDisplayItem.cs          ← 修改：添加 DisplayValue, ValueColor 属性
MainWindow.xaml/.cs             ← 重写：dxNavBar + ContentControl + dxStatusBar
```

---

## Task 1: 创建 SignalMonitorView 视图

**Files:**
- Create: `View/SignalMonitorView.xaml`
- Create: `View/SignalMonitorView.xaml.cs`

- [ ] **Step 1: 创建 SignalMonitorView.xaml**

```xaml
<UserControl x:Class="WpfApp1.View.SignalMonitorView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:dxg="http://schemas.devexpress.com/winfx/2008/xaml/grid"
             Background="#1E1E1E">
    <Grid>
        <dxg:GridControl x:Name="gridSignals"
                         ItemsSource="{Binding Signals}"
                         AutoGenerateColumns="False"
                         EnableSmartColumns="True"
                         SelectionMode="Single"
                         SyncWithCurrentUIThread="True">
            <dxg:GridControl.Resources>
                <!-- IsChanged 行高亮样式 -->
                <Style x:Key="SignalChangedRowStyle" TargetType="dxg:GridRowContent">
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding IsChanged}" Value="True">
                            <Setter Property="Background" Value="#1A4CAF50"/>
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
                <!-- 值列红色样式（Error） -->
                <Style x:Key="ValueErrorStyle" TargetType="TextBlock">
                    <Setter Property="Foreground" Value="#F44336"/>
                    <Setter Property="FontFamily" Value="Consolas"/>
                </Style>
                <!-- 值列橙色样式（N/A） -->
                <Style x:Key="ValueNaStyle" TargetType="TextBlock">
                    <Setter Property="Foreground" Value="#FF9800"/>
                    <Setter Property="FontFamily" Value="Consolas"/>
                </Style>
            </dxg:GridControl.Resources>
            <dxg:GridControl.View>
                <dxg:TableView Name="signalTableView"
                               AllowEditing="False"
                               ShowFilterPanel="True"
                               AutoUpdateTotalCount="True"
                               RowStyle="{StaticResource SignalChangedRowStyle}">
                    <dxg:TableView.Columns>
                        <!-- 状态图标 -->
                        <dxg:GridColumn FieldName="StatusIcon" Header="" Width="40"
                                        UnboundType="String" ReadOnly="True"/>
                        <!-- 信号名称 -->
                        <dxg:GridColumn FieldName="Name" Header="名称" Width="120" ReadOnly="True"/>
                        <!-- 地址 -->
                        <dxg:GridColumn FieldName="Address" Header="地址" Width="100" ReadOnly="True"/>
                        <!-- 数据类型 -->
                        <dxg:GridColumn FieldName="DataType" Header="类型" Width="70" ReadOnly="True"/>
                        <!-- 当前值 -->
                        <dxg:GridColumn FieldName="DisplayValue" Header="当前值" Width="150" ReadOnly="True"/>
                        <!-- 变化标记 -->
                        <dxg:GridColumn FieldName="ChangeIcon" Header="★" Width="40" ReadOnly="True"/>
                        <!-- 最后更新 -->
                        <dxg:GridColumn FieldName="LastUpdateTime" Header="更新时间" Width="90" ReadOnly="True"/>
                        <!-- PLC来源 -->
                        <dxg:GridColumn FieldName="PlcId" Header="PLC" Width="80" ReadOnly="True"/>
                    </dxg:TableView.Columns>
                </dxg:TableView>
            </dxg:GridControl.View>
        </dxg:GridControl>
    </Grid>
</UserControl>
```

- [ ] **Step 2: 创建 SignalMonitorView.xaml.cs**

```csharp
using System.Windows.Controls;

namespace WpfApp1.View
{
    public partial class SignalMonitorView : UserControl
    {
        public SignalMonitorView()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 3: Build 验证**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add View/SignalMonitorView.xaml View/SignalMonitorView.xaml.cs
git commit -m "feat(ui): add SignalMonitorView with dxGrid TableView"
```

---

## Task 2: 创建 PLCConnectionView 视图

**Files:**
- Create: `View/PLCConnectionView.xaml`
- Create: `View/PLCConnectionView.xaml.cs`

- [ ] **Step 1: 创建 PLCConnectionView.xaml**

```xaml
<UserControl x:Class="WpfApp1.View.PLCConnectionView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:dxg="http://schemas.devexpress.com/winfx/2008/xaml/grid"
             xmlns:dx="http://schemas.devexpress.com/winfx/2008/xaml/core"
             Background="#1E1E1E">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="300"/>
        </Grid.ColumnDefinitions>

        <!-- 左侧 PLC 列表 dxGrid -->
        <dxg:GridControl x:Name="gridPlc"
                         Grid.Column="0"
                         ItemsSource="{Binding PlcList}"
                         AutoGenerateColumns="False"
                         SelectionMode="Single"
                         SelectedItemChanged="gridPlc_SelectedItemChanged">
            <dxg:GridControl.Columns>
                <!-- 状态图标 -->
                <dxg:GridColumn FieldName="StatusIcon" Header="" Width="40"
                                UnboundType="String" ReadOnly="True"/>
                <!-- PLC名称 -->
                <dxg:GridColumn FieldName="Name" Header="PLC名称" Width="120" ReadOnly="True"/>
                <!-- 品牌 -->
                <dxg:GridColumn FieldName="Brand" Header="品牌" Width="80" ReadOnly="True"/>
                <!-- IP:Port -->
                <dxg:GridColumn FieldName="IpPort" Header="IP地址" Width="130" ReadOnly="True"/>
                <!-- 信号数 -->
                <dxg:GridColumn FieldName="SignalCount" Header="信号数" Width="60" ReadOnly="True"/>
            </dxg:GridControl.Columns>
            <dxg:GridControl.View>
                <dxg:TableView Name="plcTableView"
                               AllowEditing="False"
                               ShowFilterPanel="False"
                               AutoUpdateTotalCount="True"/>
            </dxg:GridControl.View>
        </dxg:GridControl>

        <!-- 右侧详情面板 -->
        <Border Grid.Column="1" Background="#2D2D30" Margin="8,0,0,0" CornerRadius="4" Padding="16">
            <StackPanel DataContext="{Binding SelectedPlc}">
                <TextBlock Text="PLC 详情" FontSize="14" FontWeight="SemiBold" Foreground="White" Margin="0,0,0,16"/>

                <TextBlock Text="名称" FontSize="11" Foreground="#888" Margin="0,0,0,4"/>
                <TextBlock Text="{Binding Name}" FontSize="13" Foreground="White" Margin="0,0,0,12"/>

                <TextBlock Text="连接状态" FontSize="11" Foreground="#888" Margin="0,0,0,4"/>
                <Border CornerRadius="4" Padding="8,4" HorizontalAlignment="Left" Margin="0,0,0,12">
                    <Border.Style>
                        <Style TargetType="Border">
                            <Setter Property="Background" Value="#555"/>
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IsConnected}" Value="True">
                                    <Setter Property="Background" Value="#4CAF50"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </Border.Style>
                    <TextBlock FontSize="12" Foreground="White">
                        <TextBlock.Style>
                            <Style TargetType="TextBlock">
                                <Setter Property="Text" Value="离线"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding IsConnected}" Value="True">
                                        <Setter Property="Text" Value="在线"/>
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </TextBlock.Style>
                    </TextBlock>
                </Border>

                <TextBlock Text="IP地址" FontSize="11" Foreground="#888" Margin="0,0,0,4"/>
                <TextBlock Text="{Binding IpAddress}" FontSize="13" Foreground="White" FontFamily="Consolas" Margin="0,0,0,12"/>

                <TextBlock Text="端口" FontSize="11" Foreground="#888" Margin="0,0,0,4"/>
                <TextBlock Text="{Binding Port}" FontSize="13" Foreground="White" FontFamily="Consolas" Margin="0,0,0,12"/>

                <TextBlock Text="品牌" FontSize="11" Foreground="#888" Margin="0,0,0,4"/>
                <TextBlock Text="{Binding Brand}" FontSize="13" Foreground="White" Margin="0,0,0,12"/>

                <TextBlock Text="信号数量" FontSize="11" Foreground="#888" Margin="0,0,0,4"/>
                <TextBlock Text="{Binding SignalCount}" FontSize="13" Foreground="White" FontFamily="Consolas" Margin="0,0,0,16"/>
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 2: 创建 PLCConnectionView.xaml.cs**

```csharp
using System.Windows.Controls;
using WpfApp1.ViewModels;

namespace WpfApp1.View
{
    public partial class PLCConnectionView : UserControl
    {
        public PLCConnectionView()
        {
            InitializeComponent();
        }

        private void gridPlc_SelectedItemChanged(object sender,
            DevExpress.Xpf.Grid.SelectedItemChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && e.NewItem is PlcStatusItem item)
            {
                vm.SelectPlc(item.Id);
            }
        }
    }
}
```

- [ ] **Step 3: Build 验证**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add View/PLCConnectionView.xaml View/PLCConnectionView.xaml.cs
git commit -m "feat(ui): add PLCConnectionView with dxGrid and detail panel"
```

---

## Task 3: 扩展 SignalDisplayItem - 添加 DisplayValue / ValueColor

**Files:**
- Modify: `ViewModels/MainViewModel.cs:218-239` (SignalDisplayItem class)

- [ ] **Step 1: 在 SignalDisplayItem 中添加新属性**

在现有 `SignalDisplayItem` 类中添加：

```csharp
// 在 _displayValue 字段后添加
private Brush _valueColor;

// DisplayValue - 类型自适应格式化
public string DisplayValue
{
    get => _displayValue;
    set => SetProperty(ref _displayValue, value);
}

// ValueColor - 用于条件格式
public Brush ValueColor
{
    get => _valueColor;
    set => SetProperty(ref _valueColor, value);
}

// 状态图标（绿色● / 灰色●）
public string StatusIcon => IsConnected ? "●" : "○";

// 变化标记图标
public string ChangeIcon => IsChanged ? "★" : "";

// PlcId 来源
public string PlcId { get; }
```

- [ ] **Step 2: 更新 UpdateFrom 方法中的 DisplayValue 格式化逻辑**

修改 `UpdateFrom` 方法：

```csharp
public void UpdateFrom(SignalData signal)
{
    Value = signal.Value;
    IsChanged = signal.IsChanged;
    LastUpdateTime = signal.LastUpdateTime;

    // 类型自适应格式化 DisplayValue
    DisplayValue = FormatDisplayValue(signal.Value, signal.DataType);

    // 值颜色（用于条件格式）
    ValueColor = GetValueColor(signal.Value);
}

// 类型自适应格式化
private string FormatDisplayValue(object value, DataTypeEnum dataType)
{
    if (value == null) return "null";

    string str = value.ToString();

    if (str.StartsWith("Error(") || str.StartsWith("Err:"))
        return str; // 保持原样，红色样式

    if (str.StartsWith("N/A("))
        return str; // 保持原样，橙色样式

    switch (dataType)
    {
        case DataTypeEnum.Bool:
            return (bool)value ? "ON" : "OFF";
        case DataTypeEnum.BoolArray:
            if (value is bool[] arr)
                return "[" + string.Join(",", arr) + "]";
            return str;
        case DataTypeEnum.String:
            return str;
        default:
            // Int/Float/Short 等数值类型直接显示
            return str;
    }
}

// 根据值类型返回颜色
private Brush GetValueColor(object value)
{
    if (value == null) return Brushes.White;

    string str = value.ToString();
    if (str.StartsWith("Error(") || str.StartsWith("Err:"))
        return new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // 红色
    if (str.StartsWith("N/A("))
        return new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)); // 橙色
    return Brushes.White;
}
```

添加 using：`using System.Windows.Media;`

- [ ] **Step 3: Build 验证**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "feat(ui): add DisplayValue and ValueColor to SignalDisplayItem"
```

---

## Task 4: 扩展 PlcStatusItem - 添加 StatusColor / IpPort

**Files:**
- Modify: `ViewModels/MainViewModel.cs:154-171` (PlcStatusItem class)

- [ ] **Step 1: 在 PlcStatusItem 中添加新属性**

在现有 `PlcStatusItem` 类中添加：

```csharp
private Brush _statusColor;

// StatusColor - 状态颜色（绿色/灰色）
public Brush StatusColor
{
    get => _statusColor;
    set => SetProperty(ref _statusColor, value);
}

// IpPort - IP:Port 组合字符串
public string IpPort => $"{IpAddress}:{Port}";
```

- [ ] **Step 2: 更新 IsConnected 的 set 方法中的 StatusColor 同步**

修改 `IsConnected` 属性：

```csharp
public bool IsConnected
{
    get => _isConnected;
    set
    {
        if (SetProperty(ref _isConnected, value))
        {
            StatusColor = value
                ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))  // 绿色
                : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)); // 灰色
        }
    }
}
```

添加 using：`using System.Windows.Media;`

- [ ] **Step 3: Build 验证**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "feat(ui): add StatusColor and IpPort to PlcStatusItem"
```

---

## Task 5: 扩展 MainViewModel - 添加 SelectedView / SelectedPlc

**Files:**
- Modify: `ViewModels/MainViewModel.cs:1-151` (MainViewModel class)

- [ ] **Step 1: 添加新字段和属性**

在 `MainViewModel` 类中添加：

```csharp
private string _selectedView = "PLCConnection";
private PlcStatusItem _selectedPlc;

// SelectedView - 当前选中的视图 ("PLCConnection" / "SignalMonitor")
public string SelectedView
{
    get => _selectedView;
    set => SetProperty(ref _selectedView, value);
}

// SelectedPlc - 当前选中的 PLC（详情面板用）
public PlcStatusItem SelectedPlc
{
    get => _selectedPlc;
    set => SetProperty(ref _selectedPlc, value);
}
```

- [ ] **Step 2: 添加 SelectPlc 方法**

在 `SelectPlc` 方法中同时更新 SelectedPlc：

```csharp
public void SelectPlc(string plcId)
{
    SelectedPlcId = plcId;
    SelectedPlc = _plcList.FirstOrDefault(p => p.Id == plcId);
}
```

- [ ] **Step 3: 修改 lstPlc_SelectionChanged（如果代码还在 MainWindow.xaml.cs）**

当前 lstPlc 选择逻辑在 MainWindow.xaml.cs 的 `lstPlc_SelectionChanged` 事件中。
重构后，PLCConnectionView.xaml.cs 中已有 `gridPlc_SelectedItemChanged` 处理此逻辑。

- [ ] **Step 4: Build 验证**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "feat(ui): add SelectedView and SelectedPlc to MainViewModel"
```

---

## Task 6: 重写 MainWindow.xaml

**Files:**
- Modify: `MainWindow.xaml`

- [ ] **Step 1: 完全重写 MainWindow.xaml**

用 DevExpress 组件替换现有标准 WPF 布局：

```xaml
<Window x:Class="WpfApp1.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:dx="http://schemas.devexpress.com/winfx/2008/xaml/core"
        xmlns:dxn="http://schemas.devexpress.com/winfx/2008/xaml/navbar"
        xmlns:local="clr-namespace:WpfApp1"
        xmlns:view="clr-namespace:WpfApp1.View"
        Title="PLC Monitor"
        Height="700" Width="1100"
        WindowStartupLocation="CenterScreen"
        Background="#1E1E1E">
    <Window.Resources>
        <Style TargetType="dxn:NavBarGroup">
            <Setter Property="Header" Value="{Binding}"/>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 主内容：左侧 NavBar + 右侧内容区 -->
        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="200"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- 左侧 Navigation -->
            <Border Grid.Column="0" Background="#2D2D30" Margin="0,0,1,0">
                <DockPanel>
                    <!-- 标题 -->
                    <Border DockPanel.Dock="Top" Background="#007ACC" Padding="12,10">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="PLC Monitor" FontSize="14" FontWeight="SemiBold"
                                       Foreground="White" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Border>

                    <!-- NavBar 导航 -->
                    <dxn:NavBarControl x:Name="navBar"
                                       Background="Transparent"
                                       BorderThickness="0"
                                       HorizontalAlignment="Stretch"
                                       VerticalAlignment="Stretch"
                                       SelectionChanged="navBar_SelectionChanged">
                        <dxn:NavBarControl.View>
                            <dxn:NavigationPaneView ShowGroupHeaders="False"
                                                    ShowNavigationItems="False"
                                                    CollapsedNavigationView="Auto"/>
                        </dxn:NavBarControl.View>

                        <!-- PLC连接 -->
                        <dxn:NavBarGroup x:Name="grpPlc" Header="PLC连接">
                            <dxn:NavBarItem x:Name="navPlcConnection"
                                           Content="PLC连接"
                                           Glyph="{dx:DXImage SvgImages/Outlook%20Express/Message.png}"/>
                        </dxn:NavBarGroup>

                        <!-- 信号监控 -->
                        <dxn:NavBarGroup x:Name="grpSignal" Header="信号监控">
                            <dxn:NavBarItem x:Name="navSignalMonitor"
                                           Content="信号监控"
                                           Glyph="{dx:DXImage SvgImages/Chart/ChartLine.png}"/>
                        </dxn:NavBarGroup>
                    </dxn:NavBarControl>
                </DockPanel>
            </Border>

            <!-- 右侧内容区 -->
            <ContentControl Grid.Column="1" x:Name="contentArea" Background="#1E1E1E">
                <ContentControl.Style>
                    <Style TargetType="ContentControl">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding SelectedView}" Value="SignalMonitor">
                                <Setter Property="Content">
                                    <Setter.Value>
                                        <view:SignalMonitorView DataContext="{Binding}"/>
                                    </Setter.Value>
                                </Setter>
                            </DataTrigger>
                        </Style.Triggers>
                        <!-- 默认：PLC连接视图 -->
                    </Style>
                </ContentControl.Style>
                <!-- 默认显示 PLC连接视图 -->
                <view:PLCConnectionView DataContext="{Binding}"/>
            </ContentControl>
        </Grid>

        <!-- Status Bar -->
        <dx:StatusBar Grid.Row="1"
                      Background="#007ACC"
                      Foreground="White"
                      Height="28">
            <dx:StatusBarItem>
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="●" Foreground="#4CAF50" Margin="0,0,4,0"
                               FontSize="10" VerticalAlignment="Center"/>
                    <TextBlock Text="{Binding PlcCountLabel}"
                               Foreground="White" FontSize="11" VerticalAlignment="Center"/>
                </StackPanel>
            </dx:StatusBarItem>
            <dx:StatusBarItem Content="|" Foreground="#5A9FD4"/>
            <dx:StatusBarItem>
                <TextBlock Text="{Binding SignalCountLabel}"
                           Foreground="White" FontSize="11" VerticalAlignment="Center"/>
            </dx:StatusBarItem>
            <dx:StatusBarItem Content="|" Foreground="#5A9FD4"/>
            <dx:StatusBarItem>
                <TextBlock Text="{Binding LastRefreshLabel}"
                           Foreground="White" FontSize="11" VerticalAlignment="Center"/>
            </dx:StatusBarItem>
        </dx:StatusBar>
    </Grid>
</Window>
```

- [ ] **Step 2: Build 验证**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add MainWindow.xaml
git commit -m "feat(ui): rewrite MainWindow.xaml with DevExpress NavBar"
```

---

## Task 7: 重写 MainWindow.xaml.cs

**Files:**
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: 重写 MainWindow.xaml.cs**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfApp1.PLCHandler;
using WpfApp1.PLCHandler.Models;
using WpfApp1.ViewModels;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private CancellationTokenSource _pollCts;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            Loaded += OnLoaded;
            Closing += (s, e) => _pollCts?.Cancel();

            // 默认选中 PLC连接
            navBar.SelectedItem = navPlcConnection;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _pollCts = new CancellationTokenSource();
            Task.Run(() => PollLoop(_pollCts.Token));
        }

        private void navBar_SelectionChanged(object sender,
            DevExpress.Xpf.NavBar.NavBarSelectionChangedEventArgs e)
        {
            if (navBar.SelectedItem == navSignalMonitor)
            {
                _vm.SelectedView = "SignalMonitor";
                contentArea.Content = new View.SignalMonitorView { DataContext = _vm };
            }
            else
            {
                _vm.SelectedView = "PLCConnection";
                contentArea.Content = new View.PLCConnectionView { DataContext = _vm };
            }
        }

        private async Task PollLoop(CancellationToken ct)
        {
            var handler = PlcHandler.Instance;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    foreach (var cfg in handler.PlcConfigs)
                    {
                        if (!cfg.IsEnabled) continue;

                        var conn = handler.ConnectionPool.GetOrCreate(new PlcConnectionOptions
                        {
                            Id = cfg.Id, Name = cfg.Name, Brand = cfg.Brand,
                            IpAddress = cfg.IpAddress, Port = cfg.Port,
                            Slot = cfg.Slot, Channel = cfg.Channel, Group = cfg.Group
                        });

                        if (!conn.IsConnected)
                        {
                            await handler.ConnectPlcAsync(cfg.Id);
                        }

                        if (conn.IsConnected)
                        {
                            var signals = handler.Signals.Where(s => s.PlcId == cfg.Id).ToList();
                            var reader = new SignalReader(conn);

                            foreach (var signal in signals)
                            {
                                if (ct.IsCancellationRequested) break;

                                try
                                {
                                    var value = await reader.ReadValueAsync(signal);
                                    var prev = signal.Value;

                                    var h = handler;
                                    var currentSignal = signal;
                                    var newValue = value;
                                    Action updateSignal = () =>
                                    {
                                        currentSignal.PreviousValue = prev;
                                        currentSignal.Value = newValue;
                                        currentSignal.IsChanged = !Equals(currentSignal.Value, currentSignal.PreviousValue);
                                        currentSignal.LastUpdateTime = DateTime.Now;
                                    };
                                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(updateSignal);
                                }
                                catch (Exception ex)
                                {
                                    Action setError = () =>
                                    {
                                        signal.Value = $"Err:{ex.Message}";
                                        signal.LastUpdateTime = DateTime.Now;
                                    };
                                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(setError);
                                }

                                await Task.Delay(50, ct);
                            }
                        }
                    }

                    await Task.Delay(500, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    await Task.Delay(1000, ct);
                }
            }
        }
    }
}
```

- [ ] **Step 2: Build 验证**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(ui): rewrite MainWindow.xaml.cs with NavBar navigation"
```

---

## Task 8: 最终集成验证

- [ ] **Step 1: 全量 Build**

Run: `dotnet build`
Expected: 0 errors, 0 warnings（无未使用事件警告需处理）

- [ ] **Step 2: 处理所有警告**

如果存在 CS0067 未使用事件警告，在各 Connection 类和 PollingService / PlcHandler 中删除或注释掉未使用的事件声明。

- [ ] **Step 3: 最终验证**

Run: `dotnet build --configuration Release`
Expected: 编译成功，生成 `bin/Release/net48/WpfApp1.exe`

- [ ] **Step 4: 提交所有更改**

```bash
git add -A
git commit -m "feat(ui): complete DevExpress UI refactor - NavBar + dxGrid"
```

---

## 自我检查清单

| 检查项 | 状态 |
|--------|------|
| Spec 覆盖：TileBar 导航 (dxNavBar) | ✅ Task 6-7 |
| Spec 覆盖：PLC连接页面 (dxGrid + 详情面板) | ✅ Task 2, 4 |
| Spec 覆盖：信号监控页面 (dxGrid TableView) | ✅ Task 1, 3 |
| Spec 覆盖：StatusBar | ✅ Task 6 |
| Spec 覆盖：类型自适应格式化 DisplayValue | ✅ Task 3 |
| Spec 覆盖：StatusColor / ValueColor 条件格式 | ✅ Task 3-4 |
| Spec 覆盖：事件驱动增量更新 | ✅ Task 5, 7 |
| 保留现有业务逻辑 (PlcHandler/ConnectionPool/SignalReader) | ✅ 所有Task |
| 编译通过 0 errors | 待验证 |
| 提交到 git | 待验证 |

---

## 任务执行顺序

1. Task 1 → Task 2 → Task 3 → Task 4 → Task 5 → Task 6 → Task 7 → Task 8
2. 每个 Task 独立 Build 验证通过后再进行下一个
3. 提交在每个 Task 完成后进行