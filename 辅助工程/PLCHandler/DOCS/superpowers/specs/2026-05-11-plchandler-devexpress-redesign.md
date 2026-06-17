# PLCHandler DevExpress UI 重构设计

## 1. 概述与目标

将 PLCHandler WPF 项目从标准 WPF 组件迁移到 DevExpress v19.1 组件，提升 UI 灵活性和数据展示能力。

**核心目标：**
- 用 dxNavBar（TileBar 风格）替代 ListBox 实现导航
- 用 dxGrid TableView 替代标准 DataGrid 实现信号监控
- 事件驱动增量更新（SignalUpdated 事件触发单行刷新）
- 类型自适应格式化显示（Bool/Int/Float/String/Array）
- 保留现有业务逻辑（PlcHandler / ConnectionPool / SignalReader）

## 2. 布局设计

```
┌─────────────────────────────────────────────────────────────┐
│ Title Bar: "PLC Monitor"                                    │
├────────────┬────────────────────────────────────────────────┤
│            │                                                 │
│  dxNavBar  │   主内容区 (根据选择切换)                       │
│  (TileBar  │                                                 │
│   Style)   │   [PLC连接] → dxGrid 显示PLC列表                │
│            │   [信号监控] → dxGrid TableView 实时监控        │
│  • PLC连接  │                                                │
│  • 信号监控  │                                                │
│            │                                                 │
├────────────┴────────────────────────────────────────────────┤
│ dxStatusBar: 连接统计 | 最后刷新时间 | PLC品牌图标           │
└─────────────────────────────────────────────────────────────┘
```

**Navigation Selection="SignalMonitor"** 时显示信号监控面板（滑出）。

## 3. 页面详细设计

### 3.1 PLC连接页面 (View: PLCConnectionView)

左侧 dxGrid 列表，右侧详情面板（dxPropertyGrid 或简单 Form）

**dxGrid 列定义：**
| 列 | 宽度 | 内容 |
|----|------|------|
| 状态图标 | 40 | 绿色● / 灰色● / 红色● |
| PLC名称 | 120 | Text |
| 品牌 | 80 | Omron/Siemens/Mitsubishi/Modbus |
| IP:Port | 130 | 192.168.1.10:9600 |
| 信号数 | 60 | 数字 |
| 操作 | 100 | 连接/断开按钮 |

**详情面板：**
- PLC名称（只读）
- 连接状态（Badge: 在线/离线/连接中）
- IP / Port / Slot / Channel（可编辑）
- 信号数量统计
- 连接/断开按钮

### 3.2 信号监控页面 (View: SignalMonitorView)

dxGrid TableView，实时刷新，支持排序/过滤

**dxGrid 列定义：**
| 列 | 宽度 | 内容 |
|----|------|------|
| 状态图标 | 40 | 绿色● / 灰色● (IsConnected) |
| 信号名称 | 120 | Text |
| 地址 | 100 | Text (地址格式) |
| 数据类型 | 70 | Bool/Int/Float/String... |
| 当前值 | 150 | 自适应格式化 |
| 变化标记 | 60 | ★ 变红时高亮 |
| 最后更新 | 90 | HH:mm:ss |
| PLC来源 | 80 | PLC.ID |

**dxGrid TableView 配置：**
- `AllowEditing="False"`（只读）
- `ShowFilterPanel="True"`（过滤面板）
- `EnableSmartColumns="True"`（自适应列宽）
- `RowStyle="SignalChangedRowStyle"`（IsChanged=True 时行高亮）
- `AutoUpdateTotalCount="True"`（实时更新行数）

**条件格式：**
- `IsChanged=True` → 行背景 #2E7D32（绿色高亮）
- `IsConnected=False` → 行背景 #424242（灰色）
- `Value.StartsWith("Err:")` → 值列红色文字
- `Value.StartsWith("N/A(")` → 值列橙色文字

### 3.3 Status Bar

```
[●] 2/3 PLC 已连接  |  最后刷新: 14:32:05  |  监控: 41 信号  |  Omron:2  Siemens:1
```

## 4. 数据绑定设计

### ViewModels

**MainViewModel** — 根 VM
- `SelectedView` (string: "PLCConnection" / "SignalMonitor")
- `PlcList` (ObservableCollection<PlcStatusItem>)
- `Signals` (ObservableCollection<SignalDisplayItem>)
- `ConnectionStats` (string)
- `LastRefreshTime` (string)

**PlcStatusItem** — PLC 列表项（实现 INotifyPropertyChanged）
- `IsConnected`, `Name`, `Brand`, `IpAddress`, `Port`, `SignalCount`
- `StatusColor` (Brush: Green/Gray/Red)

**SignalDisplayItem** — 信号行（实现 INotifyPropertyChanged）
- `IsConnected`, `Id`, `Name`, `Address`, `DataType`
- `Value` (object), `DisplayValue` (string), `IsChanged`
- `LastUpdateTime`, `PlcId`
- `StatusColor`, `ValueColor` (用于条件格式)

**DisplayValue 类型自适应格式化逻辑：**
```
Bool      → "True" / "False" / "ON" / "OFF"
Int/Float → 直接 ToString()
String    → 原始字符串
Array     → "[1, 2, 3, ...]"
Error     → "N/A(原因)" 橙色
```

## 5. 事件驱动刷新设计

**SignalUpdated 事件链：**
```
PollingService.SignalUpdated
  → MainViewModel.OnSignalUpdated(sender, signal)
    → 找到对应的 SignalDisplayItem
    → 更新 Value / DisplayValue / IsChanged / LastUpdateTime
    → dxGrid 自动刷新该行（智能刷新机制）
```

**关键点：**
- OnSignalUpdated 在 UI 线程执行（Dispatcher.BeginInvoke 确保）
- dxGrid 的 `InlineCoverNotification` 处理单行刷新
- 不需要全量 Refresh，dxGrid 自动跟踪 ItemsSource 变化

## 6. 文件结构

```
View/
  SignalMonitorView.xaml/.cs     ← 信号监控面板
  PLCConnectionView.xaml/.cs     ← PLC连接面板
ViewModels/
  MainViewModel.cs               ← 已有，扩展 SelectedView
  PlcStatusItem.cs              ← 已有，扩展 StatusColor
  SignalDisplayItem.cs           ← 已有，扩展 DisplayValue/ValueColor
MainWindow.xaml/.cs             ← 重写，dxNavBar + dxGrid
```

## 7. 依赖组件

- `DevExpress.Xpf.Grid.v19.1` — dxGrid TableView
- `DevExpress.Xpf.Core.v19.1` — dxNavBar, dxStatusBar
- `DevExpress.Xpf.Charts.v19.1` — 预留（未来趋势图）
- `DevExpress.Data.v19.1` — 数据绑定基础

## 8. 非功能要求

- 编译通过，0 errors
- 保留现有业务逻辑（PlcHandler / ConnectionPool / SignalReader 不变）
- 事件驱动刷新性能：支持 100+ 信号实时更新
- 深色主题支持（Office2016White theme）