# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

蓝膜外观检测上位机系统 — WPF 桌面应用 (.NET Framework 4.8)，用于动力电池蓝膜外观检测产线。与 Omron PLC 通信控制 4 通道产线，对接 MOM 系统上报生产数据。

## 构建与运行

```bash
# 还原 NuGet 包（需要 NuGet CLI）
nuget restore ZenergyBFSI.sln

# 编译（需要 MSBuild 或 Visual Studio）
msbuild ZenergyBFSI.sln /p:Configuration=Debug /p:Platform="Any CPU"

# 运行
bin\Debug\ZenergyBFSI.exe
```

解决方案包含两个项目：
- `ZenergyBFSI` — 主 WPF 应用
- `辅助工程/PLCHandler/PLCHandler.csproj` — PLC 通信辅助库

无单元测试项目。`Service/DashboardWorkerTests.cs` 已删除（git status 显示 `D`）。

## 核心架构

### 启动流程

`App.xaml.cs` → `Rlog.Init()` / `Rdb.Init()` → `Main.Window_Loaded` → `DeviceInit()`:
1. `MomHandler.I.Init()` — 建立 WCF 连接，启动心跳线程
2. `AutoRun.I.Init()` — 初始化 8 个工位（4通道 × 来料+分流），启动所有 Station 状态机

### 服务层（全部单例）

| 服务 | 入口 | 职责 |
|------|------|------|
| `AutoRun.I` | `Service/AutoRun.cs` | 产线自动化核心：8 工位状态机、PLC IO 读写、MOM 查单、视觉数据聚合 |
| `MomHandler.I` | `Service/MomHandler.cs` | MOM WCF 通信：心跳、入站查单、出站上报、参数校验 |
| `DashboardService.I` | `Service/DashboardService.cs` | 看板数据服务：通过 `DashboardWorker` 5秒定时查询 SQLite 生成 DashboardSnapshot |
| `Settings` | `Model/Settings.cs` | 静态配置类（中文属性名如 `电芯型号`、`MOM地址`），通过 `Rdb` 持久化到 SQLite |

### 数据库

- **SQLite（本地）**: 两条路径 — 旧代码用 `Rdb`（RinKit），新代码用 `SQLiteGenericHelper`。后者有写队列（`DbWriteQueue`）防锁，WAL 模式。`BulkUpsert<T>()` 用 UPDATE-then-INSERT 策略。
- **SQL Server（远程）**: `SqlServerDapperHelper`（Dapper），用于查询各工位视觉工控机上的检测结果。`HarnessMeasureRepository` / `BlueFilmDetectionRepository` 封装具体查询。

### PLC 通信

`PLCHandler` 子项目封装了 Omron FINS 协议通信（基于 `HslCommunication`）。`AutoRun` 持有 `PlcMonitor` 实例（从 `UC_PLCMonitor` 的 DataContext 获取），通过 `TryGetLatestByName` / `ReadOnceByNameAsync` / `WriteByNameAsync` 读写 PLC 信号。信号名如 `"PLC通道1来料触发"`、`"PLC心跳响应"`。

### 工位状态机

`AutoRun.Station` 是独立的状态机循环：`心跳检测 → 信号等待 → 动作执行`。8 个工位各自 `Task.Run`，通过 CTC 协调启停。全局心跳检测在主循环中统一执行，心跳丢失时暂停所有工站。实现了 `IStationHandler` 接口的有：
- `ProductArriveStationHandler` — 来料扫码，MOM 入站查单，写 SQLite
- `ProductLeadStationHandler` — 视觉检测分流，聚合 SQL Server 检测结果，回写 PLC 分流通道

### 视图层

WPF 页面在 `View/` 下，使用 DevExpress v19.1 + MaterialDesignThemes 5.3.1 双 UI 库混合。`Main.xaml` 是主窗口，包含侧边栏导航和状态栏。子页面如 `UC_Home`、`UC_Operation`、`UC_Monitor`、`UC_PLCMonitor` 等嵌入主窗口。`View/StateCards/` 下的 `UC_StatesCards` 是核心看板卡片视图（良率、NG 类型、小时产出）。

### 看板数据流

`DashboardWorker` 定时 5 秒从 SQLite 查询 CellData → `DashboardSnapshot` → `DashboardService.OnSnapshotReady` → `Messenger.Default.Send(DashboardUpdateMessage)` → UI 绑定刷新。支持班次（A/B/C/all）和日期筛选，分页 500 条/页。

### 模拟模式

`App.config` 中 `SimulationMode=true` 时，`SimulationDataGenerator` 周期生成假 CellData 写入 SQLite，供无 PLC 硬件时调试。

## 重要约定

- **所有新服务的写操作必须通过 `SQLiteGenericHelper`**（走 `DbWriteQueue` 串行化），不要直接用 `Rdb` 做写入，否则会触发 SQLite "database is locked"
- **Settings 属性名是中文**，如 `Settings.电芯型号`、`Settings.MOM地址`。新增配置项沿用中文命名
- **RinKit 框架**（`Rlog`、`Rdb`）是项目的基础设施层，不可移除。`Rlog.Init("Debug", "C:\\Log\\")` 在 App 启动时执行
- **HSL 授权**: `App.xaml.cs` 中有硬编码的 `HslCommunication.Authorization.SetAuthorizationCode(...)`，不要删除
- **主窗口关闭被拦截**: `Main.Window_Closing` 中 `e.Cancel = true`，必须通过确认对话框退出（`Process.GetCurrentProcess().Kill()`）
- **WCF 服务引用**在 `Connected Services/MOM/`，由 Visual Studio 自动生成，不应手动修改 `Reference.cs`

## gstack

本项目配置使用 gstack。所有网页浏览任务使用 `/browse` 技能，禁止使用 `mcp__claude-in-chrome__*` 工具。
