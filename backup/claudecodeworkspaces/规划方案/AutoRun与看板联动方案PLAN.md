# AutoRun 与 UC_StatesCards 看板联动方案

**创建日期**: 2026-04-30
**更新日期**: 2026-04-30
**版本**: v2.0
**目标**: 通过消息订阅模式，在 AutoRun 处理入站/出站信息时，同步数据到 UC_StatesCards 看板视图

---

## 1. 职责划分

| 模块 | 职责 |
|------|------|
| **MOM** | 仅反馈当前产品的 NG/OK 状况 |
| **AutoRun** | 负责累积统计（Total, Ok, Ng, YieldRate）、历史记录管理、发送消息 |
| **DashboardService** | 统计管理，调用 Messenger 发送消息 |
| **UC_Home** | 继承 ObservableRecipient，订阅消息并更新 UC_StatesCards |
| **UC_StatesCards** | 仅负责 UI 展示（接收数据，不负责统计） |

**核心原则**:
- MOM 反馈即时的产品结果
- AutoRun 负责累积统计并发送消息
- UC_Home 订阅消息并更新看板，**解耦核心**
- 使用 **CommunityToolkit.Mvvm.Messaging** 管理消息订阅

---

## 2. 使用 CommunityToolkit.Mvvm.Messaging

### 2.1 为什么使用 Messaging

| 对比项 | 手写事件 | Messaging |
|--------|---------|-----------|
| 代码量 | 多（定义事件、委托、手动订阅/取消订阅） | **少**（record + 一行订阅） |
| 订阅管理 | 手动（容易忘 -=，导致内存泄漏） | **自动**（ObservableRecipient 管理） |
| 多订阅者 | 需自己实现 | **原生支持** |
| 线程安全 | 需自己处理 | **内置** |
| 消息类型 | 类/结构体 | **record**（不可变、简洁） |

### 2.2 核心组件

```csharp
// 1. 定义消息（Record）- 轻量且类型安全
public record DashboardUpdateMessage(DashboardData Data);
public record StatusLightUpdateMessage(string Result, string CellCode, string Time);

// 2. 发送消息（DashboardService）
Messenger.Default.Send(new DashboardUpdateMessage(data));
Messenger.Default.Send(new StatusLightUpdateMessage(result, cellCode, time));

// 3. 接收消息（UC_Home）- 自动订阅/取消订阅
public partial class UC_Home : ObservableRecipient
{
    protected override void OnMessageReceived(object message)
    {
        switch (message)
        {
            case DashboardUpdateMessage msg: ...
            case StatusLightUpdateMessage msg: ...
        }
    }
}
```

---

## 3. 架构设计

### 3.1 数据流

```
┌──────────────────────────────────────────────────────────────────────────┐
│                              AutoRun                                     │
│  ProductArrive() / ProductLeadArrive()                                   │
│       │                                                                  │
│       ▼                                                                  │
│  DashboardService.RecordArrive() / RecordExit()                          │
│       │                                                                  │
│       ▼                                                                  │
│  Messenger.Default.Send(DashboardUpdateMessage)                          │
│  Messenger.Default.Send(StatusLightUpdateMessage)                       │
└──────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ (Weak Reference)
┌──────────────────────────────────────────────────────────────────────────┐
│                    UC_Home : ObservableRecipient                          │
│  IsActive = true → 自动订阅 Messenger                                   │
│       │                                                                  │
│       ▼                                                                  │
│  OnMessageReceived(message)                                             │
│       │                                                                  │
│       ├── DashboardUpdateMessage → _dash.UpdateDashboard()              │
│       └── StatusLightUpdateMessage → _dash.UpdateStatusLight()         │
└──────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                       UC_StatesCards (UI展示)                            │
└──────────────────────────────────────────────────────────────────────────┘
```

### 3.2 类关系图

```
┌─────────────────────┐          ┌─────────────────────────────────┐
│   AutoRun           │          │  CommunityToolkit.Mvvm          │
│  (发布者)            │          │  Messenger.Default              │
└─────────┬───────────┘          └─────────┬───────────────────────┘
          │                                │
          │ Send()                         │ Send() / 订阅
          ▼                                ▼
┌─────────────────────┐          ┌─────────────────────────────────┐
│ DashboardService    │          │  ObservableRecipient            │
│  RecordArrive()     │          │  (UC_Home)                      │
│  RecordExit()        │          │  IsActive = true                │
│  - TotalCount       │          │  OnMessageReceived()           │
│  - RecentRecords    │          │                                 │
└─────────────────────┘          └─────────────────────────────────┘
```

---

## 4. 数据模型（使用现有 InspectionUtils）

**已存在**:
- `InspectionUtils.DashboardData`
- `InspectionUtils.RecentRecord`
- `InspectionUtils.HourlyData`
- `InspectionUtils.NgTypeData`

**新增消息 Record**:
```csharp
// Model/Messages/DashboardMessages.cs
namespace ZenergyBFSI.Model.Messages
{
    // 入站消息（包含完整看板数据）
    public record DashboardUpdateMessage(InspectionUtils.DashboardData Data);

    // 状态灯消息（轻量）
    public record StatusLightUpdateMessage(
        string Result,     // OK/NG/离线
        string CellCode,    // 电芯码
        string Time         // 时间字符串
    );

    // 出站消息（更新视觉检测结果）
    public record ExitUpdateMessage(
        string CellCode,
        string ExitResult,
        string NgTypes
    );
}
```

---

## 5. DashboardService 设计

```csharp
// Service/DashboardService.cs
using CommunityToolkit.Mvvm.Messaging;
using ZenergyBFSI.Model.Messages;

public class DashboardService
{
    private static DashboardService _instance;
    public static DashboardService I => GetInstance();

    // ── 累计统计 ──────────────────────────────
    public int TotalCount { get; private set; }
    public int OkCount { get; private set; }
    public int NgCount { get; private set; }
    public double YieldRate => TotalCount > 0 ? OkCount * 100.0 / TotalCount : 0;

    // ── 历史记录 ─────────────────────────────
    private readonly List<InspectionUtils.RecentRecord> _recentRecords = new();
    public IReadOnlyList<InspectionUtils.RecentRecord> RecentRecords => _recentRecords.AsReadOnly();

    // ── 时段数据 ─────────────────────────────
    private readonly List<InspectionUtils.HourlyData> _hourlyData = new();
    public IReadOnlyList<InspectionUtils.HourlyData> HourlyData => _hourlyData.AsReadOnly();

    // ── NG类型统计 ───────────────────────────
    private readonly List<InspectionUtils.NgTypeData> _ngTypes = new();
    public IReadOnlyList<InspectionUtils.NgTypeData> NgTypes => _ngTypes.AsReadOnly();

    // ── 记录方法 ─────────────────────────────
    public void RecordArrive(string cellCode, string entryResult, int stationNo)
    {
        lock (_syncRoot)
        {
            TotalCount++;
            if (entryResult == "OK") OkCount++;
            else NgCount++;

            _recentRecords.Insert(0, new InspectionUtils.RecentRecord
            {
                CellCode = cellCode,
                DateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                StationId = stationNo.ToString(),
                OverallResult = entryResult
            });

            // 限制1000条
            if (_recentRecords.Count > 1000) _recentRecords.RemoveAt(_recentRecords.Count - 1);

            UpdateHourlyData(DateTime.Now.Hour, entryResult);
        }

        // 发送消息
        Messenger.Default.Send(new StatusLightUpdateMessage(entryResult, cellCode, DateTime.Now.ToString("HH:mm:ss")));
        Messenger.Default.Send(new DashboardUpdateMessage(BuildDashboardData()));
    }

    public void RecordExit(string cellCode, string exitResult, string ngTypes)
    {
        lock (_syncRoot)
        {
            var record = _recentRecords.FirstOrDefault(r => r.CellCode == cellCode);
            if (record != null)
            {
                record.OverallResult = exitResult;
                record.NgTypes = ngTypes;

                if (!string.IsNullOrEmpty(ngTypes))
                {
                    foreach (var type in ngTypes.Split('|'))
                    {
                        var existing = _ngTypes.FirstOrDefault(t => t.Name == type);
                        if (existing != null) existing.Count++;
                        else _ngTypes.Add(new InspectionUtils.NgTypeData { Name = type, Count = 1 });
                    }
                }
            }
        }

        Messenger.Default.Send(new DashboardUpdateMessage(BuildDashboardData()));
    }

    private InspectionUtils.DashboardData BuildDashboardData()
    {
        return new InspectionUtils.DashboardData
        {
            Total = TotalCount,
            Ok = OkCount,
            Ng = NgCount,
            YieldRate = YieldRate,
            Hourly = _hourlyData.ToList(),
            NgTypes = _ngTypes.ToList(),
            Recent = _recentRecords.Take(100).ToList()
        };
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            TotalCount = OkCount = NgCount = 0;
            _recentRecords.Clear();
            _hourlyData.Clear();
            _ngTypes.Clear();
        }
    }
}
```

---

## 6. UC_Home 订阅消息

```csharp
// View/UC_Home.xaml.cs
using CommunityToolkit.Mvvm.Messaging;
using ZenergyBFSI.Model.Messages;
using ZenergyBFSI.View.StateCards;

public partial class UC_Home : ObservableRecipient
{
    private readonly UC_StatesCards _dash;

    public UC_Home()
    {
        InitializeComponent();
        _dash = this.DashBoard;

        // 激活消息接收（自动订阅 Messenger）
        IsActive = true;
    }

    // 当 IsActive = true 时，自动接收消息
    protected override void OnMessageReceived(object message)
    {
        switch (message)
        {
            case DashboardUpdateMessage msg:
                Dispatcher.Invoke(() => _dash.UpdateDashboard(msg.Data));
                break;

            case StatusLightUpdateMessage msg:
                Dispatcher.Invoke(() => _dash.UpdateStatusLight(msg.Result, msg.CellCode, msg.Time));
                break;
        }
    }

    // ObservableRecipient 自动管理订阅，无需手动 += / -=
    // 不用担心内存泄漏
}
```

---

## 7. AutoRun 发送消息

```csharp
// Service/AutoRun.cs
using ZenergyBFSI.Model.Messages;

private void ProductArrive(int no)
{
    // ... 现有逻辑 ...

    // 获取电芯码和MOM结果后
    string momResult = "OK";  // 从 MomHandler.I.MomCheckIn 获取

    // 写入PLC结果
    SetInt($"PLC通道{no}来料结果", momResult == "OK" ? 1 : 2);

    // 发送消息（自动统计并推送给订阅者）
    DashboardService.I.RecordArrive(code, momResult, no);
}

private void ProductLeadArrive(int no)
{
    // ... 现有逻辑 ...

    string exitResult = "OK";
    string ngTypes = "";

    // 写入PLC分流结果
    SetInt($"PLC通道{no}分流通道结果", way);

    // 发送消息
    DashboardService.I.RecordExit(code, exitResult, ngTypes);
}
```

---

## 8. 模拟数据生成（离线模式）

### 8.1 配置项

```xml
<!-- App.config -->
<add key="SimulationMode" value="true"/>
<add key="SimulationInterval" value="60000"/>  <!-- 1分钟 -->
```

### 8.2 模拟定时器

```csharp
// Service/AutoRun.cs
private Timer _simulationTimer;

public bool Init()
{
    // ... 现有初始化 ...

    if (Settings.SimulationMode)
    {
        _simulationTimer = new Timer(SimulateCallback, null,
            Settings.SimulationInterval, Settings.SimulationInterval);
    }
}

private void SimulateCallback(object state)
{
    // 模拟入站
    string code = $"SIM{DateTime.Now:yyyyMMddHHmmss}";
    string result = new Random().Next(100) < 90 ? "OK" : "NG";
    int stationNo = new Random().Next(1, 5);
    DashboardService.I.RecordArrive(code, result, stationNo);

    // 模拟出站（延迟30秒）
    Task.Delay(30000).ContinueWith(_ =>
    {
        var last = DashboardService.I.RecentRecords.FirstOrDefault();
        if (last != null)
        {
            string exitResult = new Random().Next(100) < 90 ? "OK" : "NG";
            string ngTypes = exitResult == "NG" ? "外观缺陷" : "";
            DashboardService.I.RecordExit(last.CellCode, exitResult, ngTypes);
        }
    });
}
```

---

## 9. 实现步骤

### Phase 1: 消息模型定义

| 序号 | 文件 | 操作 | 说明 |
|------|------|------|------|
| 1.1 | `Model/Messages/DashboardMessages.cs` | 新增 | 定义 record 消息类型 |
| 1.2 | `Service/DashboardService.cs` | 新增 | 统计管理，使用 Messenger 发送消息 |

### Phase 2: UC_Home 接收消息

| 序号 | 文件 | 操作 | 说明 |
|------|------|------|------|
| 2.1 | `View/UC_Home.xaml.cs` | 修改 | 继承 ObservableRecipient，重写 OnMessageReceived |
| 2.2 | `ZenergyBFSI.csproj` | 修改 | 添加 CommunityToolkit.Mvvm NuGet 包 |

### Phase 3: AutoRun 发送消息

| 序号 | 文件 | 操作 | 说明 |
|------|------|------|------|
| 3.1 | `Service/AutoRun.cs` | 修改 | ProductArrive 中调用 DashboardService.I.RecordArrive |
| 3.2 | `Service/AutoRun.cs` | 修改 | ProductLeadArrive 中调用 DashboardService.I.RecordExit |
| 3.3 | `Service/AutoRun.cs` | 修改 | Init 中添加模拟定时器（SimulationMode） |

### Phase 4: 配置

| 序号 | 文件 | 操作 | 说明 |
|------|------|------|------|
| 4.1 | `App.config` | 修改 | 添加 SimulationMode 和 SimulationInterval 配置 |
| 4.2 | `Service/Settings.cs` | 修改 | 添加 SimulationMode 属性 |

### Phase 5: 验证

| 序号 | 内容 |
|------|------|
| 5.1 | 验证 CommunityToolkit.Mvvm 包正常引用 |
| 5.2 | 验证 UC_Home 正确继承 ObservableRecipient |
| 5.3 | 验证消息发送/接收正常 |
| 5.4 | 验证入站数据正确同步到看板 |
| 5.5 | 验证出站数据正确更新记录 |
| 5.6 | 验证模拟模式正常工作 |

---

## 10. 文件变更清单

| 文件路径 | 操作 | 说明 |
|----------|------|------|
| `Model/Messages/DashboardMessages.cs` | 新增 | 定义 record 消息类型 |
| `Service/DashboardService.cs` | 新增 | 统计管理 + Messenger 发送 |
| `Service/AutoRun.cs` | 修改 | 调用 RecordArrive/RecordExit，添加模拟定时器 |
| `Service/Settings.cs` | 修改 | 添加 SimulationMode 属性 |
| `App.config` | 修改 | 添加模拟模式配置 |
| `View/UC_Home.xaml.cs` | 修改 | 继承 ObservableRecipient |
| `ZenergyBFSI.csproj` | 修改 | 添加 CommunityToolkit.Mvvm 包引用 |

---

## 11. NuGet 包依赖

```xml
<!-- ZenergyBFSI.csproj -->
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
</ItemGroup>
```

---

## 12. 关键设计决策

1. **使用 record 定义消息**: 不可变、简洁、编译器自动生成 Equals/GetHashCode
2. **ObservableRecipient 管理订阅**: IsActive=true 自动订阅，false 自动取消，无需手动 += / -=
3. **WeakReference 通信**: Messenger 使用弱引用，不会阻止 UI 控件被 GC
4. **线程安全**: 统计操作加锁，UI 更新使用 Dispatcher.Invoke
5. **历史记录限制**: 最多 1000 条，推送时取最新 100 条

---

## 13. 消息订阅模式优势

| 对比项 | 手写事件 | CommunityToolkit.Mvvm.Messaging |
|--------|---------|----------------------------------|
| 代码量 | 多 | **少** |
| 订阅管理 | 手动（易忘-=） | **自动**（ObservableRecipient） |
| 多订阅者 | 需自己实现 | **原生支持** |
| 内存泄漏风险 | 高 | **低** |
| 可测试性 | 难 | **易**（Mock Messenger） |

---

## 14. 注意事项

1. **IsActive 必须设置**: ObservableRecipient 只在 IsActive=true 时接收消息
2. **Dispatcher 更新UI**: UC_Home 在 OnMessageReceived 中需用 Dispatcher.Invoke 调用 _dash 方法
3. **模拟可切换**: SimulationMode 通过配置开关，实际设备接入时关闭即可
4. **消息不可变**: record 消息创建后不能修改，适合单向数据流

---

# 需求验证清单

**项目**: AutoRun 与 UC_StatesCards 看板联动
**版本**: v2.0

---

## 验证清单

### V1: 环境验证

| 编号 | 验证项 | 预期结果 | 验证方法 |
|------|--------|---------|---------|
| V1.1 | CommunityToolkit.Mvvm 包引用 | csproj 中存在 PackageReference | 检查 ZenergyBFSI.csproj |
| V1.2 | 命名空间引用 | using CommunityToolkit.Mvvm.Messaging 可用 | 编译检查 |

### V2: 消息模型验证

| 编号 | 验证项 | 预期结果 | 验证方法 |
|------|--------|---------|---------|
| V2.1 | DashboardMessages.cs 创建 | 文件存在且语法正确 | 编译检查 |
| V2.2 | DashboardUpdateMessage record | 包含 DashboardData 属性 | 代码审查 |
| V2.3 | StatusLightUpdateMessage record | 包含 Result, CellCode, Time 属性 | 代码审查 |

### V3: DashboardService 验证

| 编号 | 验证项 | 预期结果 | 验证方法 |
|------|--------|---------|---------|
| V3.1 | 单例模式 | DashboardService.I 可访问 | 编译检查 |
| V3.2 | RecordArrive 方法 | 累计统计 + 发送消息 | 调用测试 |
| V3.3 | RecordExit 方法 | 更新记录 + 发送消息 | 调用测试 |
| V3.4 | BuildDashboardData | 返回 InspectionUtils.DashboardData | 调用测试 |
| V3.5 | 线程安全 | 统计操作加锁 | 代码审查 |

### V4: UC_Home 验证

| 编号 | 验证项 | 预期结果 | 验证方法 |
|------|--------|---------|---------|
| V4.1 | 继承 ObservableRecipient | UC_Home : ObservableRecipient | 代码审查 |
| V4.2 | IsActive 设置 | IsActive = true 在构造函数中 | 代码审查 |
| V4.3 | OnMessageReceived 实现 | 重写方法并处理 DashboardUpdateMessage 和 StatusLightUpdateMessage | 代码审查 |
| V4.4 | Dispatcher.Invoke 调用 | UI 更新使用 Dispatcher.Invoke | 代码审查 |
| V4.5 | 自动订阅/取消订阅 | 无手动 += / -= 代码 | 代码审查 |

### V5: AutoRun 验证

| 编号 | 验证项 | 预期结果 | 验证方法 |
|------|--------|---------|---------|
| V5.1 | ProductArrive 调用 RecordArrive | MOM结果返回后调用 DashboardService.I.RecordArrive | 代码审查 |
| V5.2 | ProductLeadArrive 调用 RecordExit | 视觉检测完成后调用 DashboardService.I.RecordExit | 代码审查 |
| V5.3 | 模拟定时器 | SimulationMode=true 时定时器启动 | 配置测试 |
| V5.4 | 模拟入站 | 生成电芯码 + 随机OK/NG + 调用RecordArrive | 日志检查 |
| V5.5 | 模拟出站 | 延迟30秒 + 调用RecordExit | 日志检查 |

### V6: 功能验证（模拟模式）

| 编号 | 验证项 | 预期结果 | 验证方法 |
|------|--------|---------|---------|
| V6.1 | SimulationMode 配置 | App.config 中存在 SimulationMode=true | 检查配置 |
| V6.2 | 模拟间隔 | 1分钟触发一次模拟入站 | 计时验证 |
| V6.3 | 看板状态灯更新 | 每分钟 StatusLight 更新 | UI观察 |
| V6.4 | 看板KPI更新 | Total/Ok/Ng/Rate 正确累计 | UI观察 |
| V6.5 | 历史记录增加 | RecentRecords 列表增长 | 日志检查 |

### V7: 集成验证

| 编号 | 验证项 | 预期结果 | 验证方法 |
|------|--------|---------|---------|
| V7.1 | 消息发送/接收链路 | AutoRun → DashboardService → UC_Home → UC_StatesCards | UI观察 |
| V7.2 | 无内存泄漏 | 多次切换 Tab 后内存稳定 | 性能监控 |
| V7.3 | 线程安全 | 并发调用 RecordArrive 不崩溃 | 压力测试 |

---

## 验证结果记录

| 编号 | 验证日期 | 验证人 | 结果 | 备注 |
|------|---------|--------|------|------|
| V1.1 | | | | |
| V1.2 | | | | |
| V2.1 | | | | |
| V2.2 | | | | |
| V2.3 | | | | |
| V3.1 | | | | |
| V3.2 | | | | |
| V3.3 | | | | |
| V3.4 | | | | |
| V3.5 | | | | |
| V4.1 | | | | |
| V4.2 | | | | |
| V4.3 | | | | |
| V4.4 | | | | |
| V4.5 | | | | |
| V5.1 | | | | |
| V5.2 | | | | |
| V5.3 | | | | |
| V5.4 | | | | |
| V5.5 | | | | |
| V6.1 | | | | |
| V6.2 | | | | |
| V6.3 | | | | |
| V6.4 | | | | |
| V6.5 | | | | |
| V7.1 | | | | |
| V7.2 | | | | |
| V7.3 | | | | |

---

## 通过标准

- **所有验证项必须通过 (PASS)**
- 任何 FAIL 都需要修复后重新验证
- V7 集成验证需在前6阶段全部通过后进行