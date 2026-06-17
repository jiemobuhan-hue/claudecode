# Dashboard 模拟数据与渲染接口实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 重写模拟数据生成模块（独立静态类），梳理看板渲染接口，明确查询时间段和刷新触发机制。

**Architecture:**
- 模拟数据模块独立为 `SimDataGenerator` 静态类，不嵌入 DashboardWorker
- 看板渲染通过 `DashboardService.TriggerRefresh()` 手动触发，默认 5 秒轮询
- 时间窗口统一为 `TimeSpan.FromHours(12)`，从当前时间往前 12 小时

**Tech Stack:** C# / WPF / SQLite / DevExpress ChartControl

---

## 文件结构

| 文件 | 职责 |
|------|------|
| `Service/DashboardWorker.cs` | 移除模拟逻辑，只保留查询解析；对外暴露 `RequestRefresh()` |
| `Service/DashboardService.cs` | 单例封装，对外暴露 `TriggerRefresh()` / `GetSnapshot()` |
| `Service/SimDataGenerator.cs` | **新建** - 静态模拟数据生成器 |
| `View/StateCards/UC_StatesCards.xaml.cs` | 接收 Messenger 消息更新看板，Loaded 时启动持续模拟 |
| `View/StateCards/UC_StatesCards.xaml` | 看板 UI（柱状图 `NgHourlyChart`、饼图 `NgPieChart`、KPI 文本） |

---

## Part 1: 模拟数据模块

### Task 1: 创建 SimDataGenerator 静态类

**Files:**
- Create: `Service/SimDataGenerator.cs`

- [ ] **Step 1: 创建 SimDataGenerator.cs 框架**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 模拟电芯数据生成器。
    /// 每次生成独立的 CellData 记录，写入 SQLite 数据库。
    /// </summary>
    public static class SimDataGenerator
    {
        private static int _counter = 0;
        private static readonly Random _random = new Random();

        /// <summary>生成并写入模拟数据到数据库。</summary>
        /// <param name="count">总记录数</param>
        /// <param name="hoursBack">数据时间范围：往前多少小时（从当前时间）</param>
        /// <param name="codePrefix">电芯码前缀，默认 "SIM"</param>
        public static void Generate(int count, int hoursBack = 12, string codePrefix = "SIM")
        {
            var records = GenerateRecords(count, hoursBack, codePrefix);
            if (records.Count > 0)
            {
                SQLiteGenericHelper.BulkUpsert(records, "电芯码", "CellData");
            }
        }

        /// <summary>清空 CellData 表（测试用）</summary>
        public static void Clear()
        {
            SQLiteGenericHelper.ExecuteRaw("DELETE FROM CellData");
            _counter = 0;
        }

        private static List<CellData> GenerateRecords(int count, int hoursBack, string codePrefix)
        {
            var records = new List<CellData>(count);
            var now = DateTime.Now;
            var windowStart = now.AddHours(-hoursBack);

            for (int i = 0; i < count; i++)
            {
                _counter++;
                string cellCode = $"{codePrefix}{_counter:D6}";

                // 时间：在 window 内均匀分布
                double t = (i + _random.NextDouble()) / count;
                var entryTime = windowStart.AddMinutes(t * hoursBack * 60);
                if (entryTime > now) entryTime = now.AddMinutes(-_random.Next(1, 60));

                // 60% 出站，40% 进站
                bool isOutbound = _random.NextDouble() < 0.60;

                var record = new CellData
                {
                    电芯码 = cellCode,
                    进站时间 = entryTime.ToString("yyyy/MM/dd HH:mm:ss"),
                    检验位置 = GetRandomStation(),
                    入站结果 = "OK",
                    出站结果 = "",
                    出站时间 = "",
                    是否复投 = false,
                    Ng类型数量 = 0,
                    MOM出站结果 = "0"
                };

                if (isOutbound)
                {
                    FillOutbound(record, entryTime);
                }

                records.Add(record);
            }

            return records;
        }

        private static void FillOutbound(CellData record, DateTime entryTime)
        {
            bool isNg = _random.NextDouble() < 0.12; // 12% NG率
            record.出站结果 = isNg ? "NG" : "OK";

            int processSec = _random.Next(30, 300);
            var exitTime = entryTime.AddSeconds(processSec);
            if (exitTime > DateTime.Now) exitTime = DateTime.Now.AddSeconds(-_random.Next(1, 60));
            record.出站时间 = exitTime.ToString("yyyy/MM/dd HH:mm:ss");

            // 视觉检测参数（1~6个随机填充）
            int paramCount = _random.Next(1, 7);
            for (int i = 0; i < paramCount; i++)
            {
                switch (i)
                {
                    case 0: record.视觉检测参数一 = "正常"; break;
                    case 1: record.视觉检测参数二 = "正常"; break;
                    case 2: record.视觉检测参数三 = "正常"; break;
                    case 3: record.视觉检测参数四 = "正常"; break;
                    case 4: record.视觉检测参数五 = "正常"; break;
                    case 5: record.视觉检测参数六 = "正常"; break;
                }
            }

            if (isNg)
            {
                int ngCount = _random.Next(1, 4);
                record.Ng类型数量 = ngCount;
                var ngTypes = GetRandomNgTypes(ngCount);
                for (int i = 0; i < ngCount && i < 8; i++)
                {
                    switch (i)
                    {
                        case 0: record.Ng类型1 = ngTypes[0]; break;
                        case 1: record.Ng类型2 = ngTypes[1]; break;
                        case 2: record.Ng类型3 = ngTypes[2]; break;
                    }
                }
            }

            record.视觉检测状态 = "1";
            record.视觉检测结果 = isNg ? "NG" : "OK";
        }

        private static string GetRandomStation()
        {
            int r = _random.Next(100);
            if (r < 40) return "工位1";
            if (r < 70) return "工位2";
            if (r < 90) return "工位3";
            return "工位4";
        }

        private static string[] GetRandomNgTypes(int count)
        {
            var pool = new[] { "外观划伤", "气泡", "色差", "变形", "污渍", "凹陷", "凸点", "裂纹" };
            return pool.OrderBy(_ => _random.Next()).Take(count).ToArray();
        }
    }
}
```

- [ ] **Step 2: 验证编译**

Run: (在 Visual Studio 中编译项目，或命令行 `msbuild`)

---

### Task 2: 清理 DashboardWorker 中的模拟逻辑

**Files:**
- Modify: `Service/DashboardWorker.cs` - 删除所有模拟相关字段和方法

- [ ] **Step 1: 删除模拟相关字段和方法**

从 `DashboardWorker` 中删除：
```csharp
// 删除这些字段：
private DispatcherTimer _simTimer;
private readonly Random _random;
private bool _simulationRunning;
private int _simCounter;

// 删除这些方法：
// - StartSimulation()
// - StopSimulation()
// - ScheduleNextSim()
// - OnSimTimerTick()
// - GenerateAndInsertSimulatedData()
// - GenerateBatchSimulatedData()
// - FillOutboundData()
// - GetWeightedRandomStation()
// - Dispose() 中的 _simTimer?.Stop()
```

保留：
- `_timer` + `OnTimerTick`（5秒轮询查询）
- `QueryAndParse()` - 核心查询解析
- `ParseRecords()` - 核心解析逻辑
- `RequestRefresh()` - 手动刷新触发

- [ ] **Step 2: 验证编译**

---

### Task 3: 简化 DashboardService 接口

**Files:**
- Modify: `Service/DashboardService.cs`

- [ ] **Step 1: 修改 DashboardService**

```csharp
// 删除 StartSimulation / StopSimulation 方法

// 保留并明确：
public void TriggerRefresh()
{
    _worker?.RequestRefresh();
}

public DashboardSnapshot GetSnapshot()
{
    lock (_syncRoot)
    {
        return _currentSnapshot;
    }
}
```

- [ ] **Step 2: 验证编译**

---

### Task 4: 清理 UC_StatesCards 中的模拟调用

**Files:**
- Modify: `View/StateCards/UC_StatesCards.xaml.cs`

- [ ] **Step 1: 修改 StartClock / Loaded**

```csharp
private void StartClock()
{
    _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    _clockTimer.Tick += (_, __) =>
    {
        TxtClock.Text = DateTime.Now.ToString("HH:mm:ss");
    };
    _clockTimer.Start();
}

// Loaded 中：启动模拟改为手动调用一次（填充初始数据）
Loaded += (_, __) =>
{
    Messenger.Default.Register<DashboardUpdateMessage>(this, OnDashboardUpdateMessage);
    Messenger.Default.Register<StatusLightUpdateMessage>(this, OnStatusLightUpdateMessage);

    // 生成 200 条初始模拟数据（一次性，不持续）
    SimDataGenerator.Generate(200, hoursBack: 12);

    RedrawHourly();
};
```

- [ ] **Step 2: 验证编译**

---

## Part 2: 看板渲染接口

### Task 5: 明确 QueryAndParse 时间窗口和刷新触发

**Files:**
- Modify: `Service/DashboardWorker.cs`

- [ ] **Step 1: 确认 QueryAndParse 时间逻辑**

```csharp
// 时间窗口：当前时间 - 12小时
var now = DateTime.Now;
var windowStart = now - _timeWindow;  // _timeWindow = TimeSpan.FromHours(12)
var windowStartStr = windowStart.ToString("yyyy/MM/dd HH:mm:ss");

// 查询：进站时间在窗口内的所有记录，按时间倒序
var records = SQLiteGenericHelper.QueryRaw<CellData>(
    $@"SELECT * FROM CellData WHERE 进站时间 >= @p0 
        ORDER BY 进站时间 DESC LIMIT {_pageSize} OFFSET {offset}",
    windowStartStr);
```

**刷新触发链：**
```
DashboardService.TriggerRefresh()
  → DashboardWorker.RequestRefresh()
    → ExecuteQueryAsync() [异步]
      → QueryAndParse()
        → ParseRecords(records, windowStart)
          → SnapshotReady?.Invoke(this, snapshot)
            → DashboardService.OnSnapshotReady()
              → Messenger.Default.Send(DashboardUpdateMessage)
                → UC_StatesCards.OnDashboardUpdateMessage()
                  → UpdateDashboard(data)
                    → ApplyKpi() + RedrawHourly() + ApplyNgTypes()
```

- [ ] **Step 2: 添加注释明确刷新接口**

在 `RequestRefresh()` 上添加：
```csharp
/// <summary>
/// 手动触发看板刷新。调用后异步查询数据库，产生新的 DashboardSnapshot，
/// 通过 SnapshotReady 事件通知订阅者（DashboardService）。
/// </summary>
public void RequestRefresh() { ExecuteQueryAsync(); }
```

---

### Task 6: 验证 HourlyData 12 小时桶逻辑

**Files:**
- Modify: `Service/DashboardWorker.cs` - ParseRecords 方法

- [ ] **Step 1: 检查并修复小时桶生成**

当前代码（有问题）：
```csharp
// windowStart.Hour 可能是 02:00，生成 [02,03,04,...,13]
// 但如果当前14:00有记录14:30，hour=14 不在桶里
for (int i = 0; i < 12; i++)
{
    var hour = (windowStart.Hour + i) % 24;
    ...
}
```

**修正方案**：从 `windowStart.Hour + 1` 开始生成 12 个桶，确保覆盖整个窗口：
```csharp
// windowStart = 02:00，生成 [03,04,05,06,07,08,09,10,11,12,13,14]
// 覆盖 03:00 ~ 14:00，共12个小时柱
for (int i = 1; i <= 12; i++)
{
    var hour = (windowStart.Hour + i) % 24;
    hourlyDict[hour] = new HourlyData { Hour = hour.ToString("D2") + ":00", Ok = 0, Ng = 0 };
}
```

- [ ] **Step 2: 验证编译**

---

## Part 3: 测试验证

### Task 7: 手动测试流程

**测试前提：运行程序，打开看板页面（UC_StatesCards）**

- [ ] **Step 1: 确认初始数据已生成**

观察 Debug Output 或日志，确认：
```
SimDataGenerator: 生成 200 条记录写入 CellData
DashboardWorker: 查询到 N 条记录，窗口起点=XX:XX
```

- [ ] **Step 2: 观察 KPI 数值**

- Total ≈ 200
- OK ≈ 200 * 60% ≈ 120（部分出站，部分进站）
- NG ≈ 200 * 60% * 12% ≈ 14（仅出站记录中的 NG）
- YieldRate ≈ 120/(120+14) ≈ 90%

- [ ] **Step 3: 观察柱状图**

应有 12 根柱子（覆盖过去 12 小时的小时分段），OK(绿)和 NG(红)堆叠显示

- [ ] **Step 4: 观察饼图**

显示 NG 类型分布（外观划伤、气泡、色差等），颜色各异

- [ ] **Step 5: 手动触发刷新**

分页按钮或刷新按钮点击后，`TriggerRefresh()` 被调用，看板数据更新

---

## 自查清单

- [ ] `SimDataGenerator.cs` 不再嵌入 DashboardWorker，独立存在
- [ ] `DashboardWorker` 不再包含任何 `_simTimer`、`StartSimulation` 相关代码
- [ ] 小时桶从 `windowStart.Hour + 1` 开始，生成 12 个桶
- [ ] `DashboardService.TriggerRefresh()` 明确为手动刷新入口
- [ ] `UC_StatesCards` Loaded 时调用 `SimDataGenerator.Generate(200)` 一次
- [ ] 所有修改文件编译通过

---

## 执行方式

**1. Subagent-Driven (recommended)** - 每 Task 一个子代理，完成后审查

**2. Inline Execution** - 本会话批量执行，带检查点
