# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

蓝膜外观检测上位机系统 — WPF 桌面应用 (.NET Framework 4.8, C# 9.0)，用于动力电池蓝膜外观检测产线。与 Omron PLC 通信控制 4 通道产线，对接 MOM 系统上报生产数据。

## 构建与运行

```bash
# 还原 NuGet 包
nuget restore ZenergyBFSI.sln

# 编译 (Debug 平台为 x64, Release 为 AnyCPU)
# VS 2022 实测可用路径:
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" ZenergyBFSI.sln -p:Configuration=Debug -t:Build -verbosity:minimal

# 或者用 dotnet msbuild (需装 Build Tools):
dotnet msbuild ZenergyBFSI.sln -p:Configuration=Debug -t:Build

# 运行
bin\Debug\ZenergyBFSI.exe
```

解决方案包含两个项目：
- `ZenergyBFSI` — 主 WPF 应用
- `辅助工程/PLCHandler/PLCHandler.csproj` — PLC 通信辅助库

无单元测试项目。`Service/DashboardWorkerTests.cs` 已删除（git status 显示 `D`）。

注意：`backup/` 目录是代码备份，不是工作副本。`claudecodeworkspaces/独立项目/` 下是独立的辅助项目（LoginProject, VerifyProject, VerifyProject3, PLCBar），不属于主应用。

## 核心架构

### 启动流程

`App.xaml.cs` `Application_Startup`:
1. 单实例检查（`Process.GetProcessesByName`）
2. `SystemSleepHelper.PreventSleepAndDisplayOff()` — 禁止系统休眠/关屏
3. `Rlog.Init("Debug", "C:\\Log\\")` → `Rdb.Init(200)` → `CsvHelper.Init("C:\\Data\\")`
4. `HslCommunication.Authorization.SetAuthorizationCode(...)` — HSL 授权

`Main.Window_Loaded` → `DeviceInit()`:
1. `MomHandler.I.Init()` — 建立 WCF 连接，启动心跳/调度/离线回放三个后台线程
2. `AutoRun.I.Init()` — 初始化 8 个工位（4通道 × 来料+分流），启动所有 Station 状态机

### 关键依赖

| 依赖 | 用途 |
|------|------|
| **DevExpress v19.1** | UI 控件库（Grid, Chart, Ribbon, Layout 等），从 `lib/` 本地引用 |
| **MaterialDesignThemes 5.3.1** | Material Design 主题 + 控件样式 |
| **CommunityToolkit.Mvvm 8.4.0** | MVVM 工具包（`ObservableObject`, `RelayCommand`, `Messenger`） |
| **HslCommunication 12.8.2** | Omron FINS PLC 通信协议栈 |
| **RinKit** | 项目基础框架（`Rlog` 日志、`Rdb` 数据库、`CsvHelper`），从 `lib/` 本地引用 |
| **NLog** | 辅助日志（ApplicationInsights 的底层日志输出） |
| **Microsoft.ApplicationInsights** | Azure 遥测上报 |
| **SkiaSharp 3.119** | 2D 图形渲染（视觉检测结果标注） |
| **Microsoft.Web.WebView2** | 嵌入式 Web 浏览器控件 |
| **Dapper 2.1.72 + Dapper.Contrib 2.0.78** | SQL Server 轻量 ORM |
| **Newtonsoft.Json 13.0.1** | JSON 序列化（MOM 上下行） |
| **LiveChartsCore 2.0** | 图表渲染（看板统计图） |
| **MQTTnet** | MQTT 协议通信（从 `lib/` 本地引用） |
| **Microsoft.Xaml.Behaviors 1.1** | WPF 行为附加（EventTrigger 等） |
| **EntityFramework 6** | 旧 ORM 路径（从 `lib/` 本地引用，仅 RinKit/Rdb 使用） |
| **Microsoft.Data.Sqlite 3.1.32 + EF Core 3.1.32** | 新 SQLite 路径（`SQLiteGenericHelper` 使用） |

### 服务层（全部单例）

| 服务 | 入口 | 职责 |
|------|------|------|
| `AutoRun.I` | `Service/AutoRun.cs` | 产线自动化核心：8 工位状态机、PLC IO 读写、MOM 查单、视觉数据聚合 |
| `MomHandler.I` | `Service/MomHandler.cs` | MOM WCF 通信：心跳、入站查单、出站上报、参数校验 |
| `DashboardService.I` | `Service/DashboardService.cs` | 看板数据服务：通过 `DashboardWorker` 5秒定时查询 SQLite 生成 DashboardSnapshot |
| `Settings` | `Service/Settings.cs` | 静态配置类（中文属性名如 `电芯型号`、`MOM地址`），通过 `Rdb` 持久化到 SQLite |

### 工位状态机（AutoRun.Station）

`AutoRun` 内部定义了并行化工位框架（DeepSeek 设计），是产线自动化的核心引擎。

**状态枚举：**
- `StationState` — `Idle → Running → Paused（心跳丢失） / Error`
- `GlobalHeartbeatState` — `Healthy / Lost / Recovering`，全局心跳状态，所有工站共享
- `AutomatonState` — `Stopped / Running / Error`，自动机全局状态

**`IStationHandler` 接口**（所有工位业务逻辑必须实现）:
```csharp
Task<bool> CheckHeartbeatAsync(CancellationToken token);  // 心跳检测，框架自动施加 1s 超时
Task<bool> WaitForSignalAsync(CancellationToken token);   // 一次性信号判断，返回 true 触发 ExecuteActionAsync
Task ExecuteActionAsync(CancellationToken token);          // 信号捕获后执行的动作
```

**`Station` 类** 是独立的状态机循环：`心跳检测 → 信号等待 → 动作执行`。全局心跳丢失时 `_globalPaused = true` 暂停所有工站；恢复确认 2s 后自动继续。

实现了 `IStationHandler` 的处理器：
- `ProductArriveStationHandler` — 来料扫码，MOM 入站查单，写 SQLite
- `ProductLeadStationHandler` — 视觉检测分流，聚合 SQL Server 检测结果，回写 PLC 分流通道

新增工位类型时实现 `IStationHandler` 接口，然后在 `AutoRun.Init()` 中注册即可。

### MOM 通信韧性架构（三层防护）

`MomHandler` 内部组合了三层韧性机制，确保 MOM 服务短暂不可用时生产数据不丢失：

1. **`MomClient`** — WCF 客户端封装，支持超时控制（默认 3s/次）和指数退避重试（最多 3 次）。每次重试前重建 Faulted/超时连接。
2. **`MomCircuitBreaker`** — 熔断器（阈值 5 次连续失败，冷却 30s）。Closed → Open → HalfOpen → Closed 状态机。熔断期间拒绝请求，冷却后发一次探测请求决定是否恢复。
3. **`MomOfflineQueue`** — 离线队列（SQLite 表 `MomOfflineQueue`）。熔断或网络异常时将请求 JSON 持久化到本地，后台 `OfflineReplayLoop` 定时回放。失败记录最多重试 10 次后标记 Failed，7 天 TTL 自动清理。软上限 10000 条，硬上限 50000 条。

MOM 请求发送路径：`MomHandler` → `MomCircuitBreaker.AllowRequest()` → `MomClient.SendWithRetryAsync()` → 失败时 `MomOfflineQueue.EnqueueAsync()`。

### 数据库

- **SQLite（本地）**: 两条路径 — 旧代码用 `Rdb`（RinKit，基于 System.Data.SQLite），新代码用 `SQLiteGenericHelper`（基于 Microsoft.Data.Sqlite + EF Core 3.1.32）。后者有写队列（`DbWriteQueue`）防锁，WAL 模式。`BulkUpsert<T>()` 用 UPDATE-then-INSERT 策略。两个 SQLite 驱动共存，不可混用连接。
- **SQL Server（远程）**: `SqlServerDapperHelper`（Dapper + Microsoft.Data.SqlClient），用于查询各工位视觉工控机上的检测结果。支持双连接字符串（本地 `SQLServerConnection` + 远程 `NHDST87Connection`，最近提交 `f42a3c5` 新增）。Repository 层在 `Service/CRUDServices/` 下：`HarnessMeasureRepository` / `BlueFilmDetectionRepository` / `BlueFilmRecipeParametersRepository`。SQL Server 存储过程命名遵循 `PROC_Claude_*` 约定。建表/SP 脚本在 `Data/` 目录下。

### PLC 通信

`PLCHandler` 子项目封装了 Omron FINS 协议通信（基于 `HslCommunication`）。`AutoRun` 持有 `PlcMonitor` 实例（从 `UC_PLCMonitor` 的 DataContext 获取），通过 `TryGetLatestByName` / `ReadOnceByNameAsync` / `WriteByNameAsync` 读写 PLC 信号。信号名如 `"PLC通道1来料触发"`、`"PLC心跳响应"`。

PLC 开发也有专门的技能支持：`/plc-dev` 命令提供从需求分析→流程图→信号定义→代码生成→校验→诊断的完整流水线。架构规范见 `ELECTRICAL.md`。

### 视图层

WPF 页面在 `View/` 下，使用 DevExpress v19.1 + MaterialDesignThemes 5.3.1 双 UI 库混合。`Main.xaml` 是主窗口，包含侧边栏导航和状态栏。子页面如 `UC_Home`、`UC_Operation`、`UC_Monitor`、`UC_PLCMonitor` 等嵌入主窗口。`View/StateCards/` 下的 `UC_StatesCards` 是核心看板卡片视图（良率、NG 类型、小时产出）。应用内嵌 Roboto Condensed + Noto Sans 字体资源。

### 看板数据流

`DashboardWorker` 定时 5 秒从 SQLite 查询 CellData → `DashboardSnapshot` → `DashboardService.OnSnapshotReady` → `Messenger.Default.Send(DashboardUpdateMessage)` → UI 绑定刷新。支持班次（A/B/C/all）和日期筛选，分页 500 条/页。

### 模拟模式

`App.config` 中 `SimulationMode=true` 时，`SimulationDataGenerator` 周期生成假 CellData 写入 SQLite（间隔由 `SimulationInterval` 键控制，默认 60000ms），供无 PLC 硬件时调试。模拟数据电芯码以 `SIM` 前缀标识，可通过 `SimulationDataGenerator.ClearAsync()` 一键清理。

`App.config` 还配置了 `configBuilders`（UserSecrets，ID: `dc5068e7-8a16-4515-84e6-46c9a3c114b7`），敏感连接字符串等可通过 User Secrets 覆盖，不入版本控制。

## 重要约定

- **所有新服务的写操作必须通过 `SQLiteGenericHelper`**（走 `DbWriteQueue` 串行化），不要直接用 `Rdb` 做写入，否则会触发 SQLite "database is locked"
- **Settings 属性名是中文**，如 `Settings.电芯型号`、`Settings.MOM地址`。新增配置项沿用中文命名
- **RinKit 框架**（`Rlog`、`Rdb`）是项目的基础设施层，不可移除。`Rlog.Init("Debug", "C:\\Log\\")` 在 App 启动时执行
- **HSL 授权**: `App.xaml.cs` 中有硬编码的 `HslCommunication.Authorization.SetAuthorizationCode(...)`，不要删除
- **主窗口关闭被拦截**: `Main.Window_Closing` 中 `e.Cancel = true`，必须通过确认对话框退出（`Process.GetCurrentProcess().Kill()`）
- **WCF 服务引用**在 `Connected Services/MOM/`，由 Visual Studio 自动生成，不应手动修改 `Reference.cs`
- **MOM 端点覆盖**: `MomClient` 在运行时通过 `Settings.MOM地址` 覆盖 WCF 端点地址，`App.config` 中的 `<client><endpoint>` 地址仅作为 fallback。修改 MOM 服务器地址应通过 Settings 页面而非直接改 App.config
- **两个 SQLite 驱动共存**: `Rdb`（RinKit）基于 System.Data.SQLite，`SQLiteGenericHelper` 基于 Microsoft.Data.Sqlite + EF Core。两者连接字符串格式不同，不可混用。新代码写入必须走 `SQLiteGenericHelper`
- **Debug 平台为 x64**: 不是 AnyCPU，编译时注意平台选择

## gstack

本项目配置使用 gstack。所有网页浏览任务使用 `/browse` 技能，禁止使用 `mcp__claude-in-chrome__*` 工具。
