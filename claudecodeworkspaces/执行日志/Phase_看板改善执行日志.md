# 看板改善执行日志

**日期**: 2026-04-30
**操作人**: Claude Code
**Phase**: 看板改善 - 统计数据不准修复 + UC_StatesCards 功能完善

---

## 问题诊断

### AutoRun 数据问题（根本原因）

| # | 问题 | 严重度 | 位置 |
|---|------|--------|------|
| R1 | `RecordArrive("code", "OK", no)` 在 `if(res==1)` **之前**无条件调用，每80ms执行一次 | **P0** | AutoRun.cs:217 |
| R2 | PLC trigger 无边缘检测，signal 保持1时同一产品被重复计数 | P1 | AutoRun.cs |
| R3 | `way = 1` 硬覆盖视觉算法结果，所有出站结果都是"结果一" | P1 | AutoRun.cs:459 |
| R4 | DashboardService 无入站去重，相同电芯码可重复入站 | P2 | DashboardService.cs |

### UC_StatesCards 问题

| # | 问题 | 位置 |
|---|------|------|
| B1 | `RedrawHourly()` 方法体为空 | xaml.cs:355 |
| B2 | 小时图表使用 XAML 硬编码数据 | xaml |
| B3 | NG饼图使用 XAML 硬编码数据 | xaml |
| B4 | `ApplyNgTypes` 被注释掉 | xaml.cs:361 |
| B5 | `_dashTimer` 间隔 = 100秒（注释写10秒） | xaml.cs:73 |
| B6 | `_liveTimer` 间隔 = 30秒（注释写3秒） | xaml.cs:78 |
| B7 | `LoadDashboardAsync` 使用硬编码假数据 | xaml.cs:110 |
| B8 | `LoadLiveStatusAsync` 使用硬编码假数据 | xaml.cs:107 |

---

## 已执行修改

### 1. AutoRun.cs — 删除 phantom RecordArrive

**文件**: `Service/AutoRun.cs`

**修改**: 删除 Line 217 的无条件调用
```csharp
// 删除了:
// DashboardService.I.RecordArrive("code", "OK", no);
```

**效果**: 消除每次循环（80ms）的 phantom 统计 entry

---

### 2. UC_Home.xaml.cs — 修复 Timer 间隔

**文件**: `View/UC_Home.xaml.cs`

**修改1**:
```csharp
// 修改前 (错误)
_dashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(100) };
// 修改后
_dashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
```

**修改2**:
```csharp
// 修改前 (错误)
_liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
// 修改后
_liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
```

**效果**: 看板刷新 10秒/次，状态灯刷新 3秒/次

---

### 3. DashboardService.cs — 添加 GetDashboardData 公开方法

**文件**: `Service/DashboardService.cs`

**添加**:
```csharp
/// <summary>
/// 获取当前看板数据（供UI拉取）
/// </summary>
public InspectionUtils.DashboardData GetDashboardData()
{
    return BuildDashboardData();
}
```

**效果**: UC_Home 可通过 `DashboardService.I.GetDashboardData()` 获取真实统计数据

---

### 4. UC_Home.xaml.cs — 接入真实数据源

**文件**: `View/UC_Home.xaml.cs`

**修改**: `LoadDashboardAsync` 从硬编码改为从 DashboardService 获取
```csharp
private async Task LoadDashboardAsync()
{
    var data = DashboardService.I.GetDashboardData();
    _dash.UpdateDashboard(data);
}
```

**添加**: `using ZenergyBFSI.Service;`

**效果**: 看板显示真实统计数据

---

### 5. UC_StatesCards.xaml.cs — 实现 RedrawHourly

**文件**: `View/StateCards/UC_StatesCards.xaml.cs`

**添加 using**:
```csharp
using DevExpress.Xpf.Charts;
```

**实现 `RedrawHourly()`**:
```csharp
private void RedrawHourly()
{
    if (_hourlyData == null || _hourlyData.Count == 0) return;
    var diagram = NgHourlyChart?.Diagram as XYDiagram2D;
    if (diagram == null) return;

    diagram.Series.Clear();

    var okSeries = new BarStackedSeries2D { DisplayName = "OK 产量", ... };
    var ngSeries = new BarStackedSeries2D { DisplayName = "NG 产量", ... };

    foreach (var h in _hourlyData)
    {
        okSeries.Points.Add(new SeriesPoint(h.Hour, h.Ok));
        ngSeries.Points.Add(new SeriesPoint(h.Hour, h.Ng));
    }

    diagram.Series.Add(okSeries);
    diagram.Series.Add(ngSeries);
}
```

---

### 6. UC_StatesCards.xaml.cs — 实现 NG饼图动态绑定

**文件**: `View/StateCards/UC_StatesCards.xaml.cs`

**修改 `ApplyNgTypes()`**:
```csharp
private void ApplyNgTypes(List<NgTypeData> types)
{
    if (types == null || types.Count == 0)
    {
        NgPieSeries.Points.Clear();
        return;
    }

    NgPieSeries.Points.Clear();
    foreach (var t in types)
    {
        NgPieSeries.Points.Add(new SeriesPoint(t.Name, t.Count));
    }
}
```

**效果**: NG饼图从硬编码数据改为显示真实 NG 类型统计

---

### 7. UC_StatesCards.xaml — ChartControl 添加 x:Name

**文件**: `View/StateCards/UC_StatesCards.xaml`

**修改**:
```xml
<dxc:ChartControl x:Name="NgHourlyChart">
```

---

## 待处理项（本次未执行）

| # | 项目 | 说明 | 优先级 |
|---|------|------|--------|
| T1 | 添加边缘检测防止重复计数 | AutoRun ProductArrive 中添加状态锁存 | P1 |
| T2 | 移除 way=1 硬覆盖 | 让视觉算法真实结果生效 | P1 |
| T3 | DashboardService 入站去重 | 相同电芯码不重复入站 | P2 |
| T4 | ProcessMs 填充 | RecentRecord.ProcessMs 未被设置 | P3 |
| T5 | 班次过滤 FilterByShift | 方法存在但未被调用 | P3 |
| T6 | 接真实缺陷类型 | Ng类型1~8 应接 T_HarnessMeasure/T_BlueFilmDetection 真实字段 | P1 |

---

## 附加修改（用户添加）

### AutoRun.cs — 调试用 RecordArrive

**位置**: Line 217，`if(res==1)` 判断之前

```csharp
DashboardService.I.RecordArrive("1213854188", "NG", no);
```

**说明**: 用户自行添加的调试调用，用于测试看板显示效果

### AutoRun.cs — 临时 NG 类型数据

**位置**: `UpdateCellDataFromSQLserver` 方法

```csharp
data.视觉检测结果 = "NG";  // TODO: 临时写死为NG以便测试饼图
data.Ng类型数量 = 3;
data.Ng类型1 = "外观缺陷";
data.Ng类型2 = "尺寸超差";
data.Ng类型3 = "性能不合格";
// Ng类型4~8 留空，待接真实数据
```

**说明**: 临时数据用于测试饼图显示，正式需接真实缺陷类型字段

---

## 执行进度

| 任务 | 状态 |
|------|------|
| 删除 phantom RecordArrive | ✅ 完成 |
| 修复 Timer 间隔 | ✅ 完成 |
| 添加 GetDashboardData | ✅ 完成 |
| 接入真实数据源 | ✅ 完成 |
| 实现 RedrawHourly | ✅ 完成 |
| 实现 NG饼图绑定 | ✅ 完成 |
| 添加边缘检测 | ⏳ 待处理 |
| 移除 way=1 硬覆盖 | ⏳ 待处理 |
| 入站去重 | ⏳ 待处理 |
