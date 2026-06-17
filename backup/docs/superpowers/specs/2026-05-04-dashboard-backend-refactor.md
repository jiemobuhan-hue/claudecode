# Dashboard 看板后端重构设计规范

**日期**: 2026-05-04
**项目**: ZenergyBFSI 蓝膜外观检测上位机
**状态**: 设计中

---

## 1. 目标

解决 Dashboard 看板 UI 卡顿问题，将数据库查询和聚合计算从 UI 线程移到 BackgroundWorker，确保：
- 数据从 CellData 数据库实时查询（最近 4 小时）
- 每次查询 ~1000 条记录，内存累计 ~10000 条
- 5 秒定时刷新不阻塞 UI
- 数据变化时才触发消息，避免消息风暴
- RecentRecords 支持分页浏览

---

## 2. 架构概览

```
┌──────────────────────────────────────────────────────────┐
│                    BackgroundWorker                       │
│  ┌────────────┐    ┌────────────┐    ┌────────────┐      │
│  │ 定时器     │───→│ 数据库查询  │───→│ 数据解析    │      │
│  │ (5秒间隔)  │    │ + 聚合     │    │ + 分页     │      │
│  └────────────┘    └────────────┘    └────────────┘      │
└──────────────────────────────────────────────────────────┘
                              │ OnCompleted
                              ↓
┌──────────────────────────────────────────────────────────┐
│                  DashboardService                        │
│  · 线程安全数据容器（锁）                                   │
│  · 持有最新 DashboardSnapshot                             │
│  · 比较数据变化后触发 DashboardUpdateMessage              │
│  · 对外提供只读数据访问接口                                │
└──────────────────────────────────────────────────────────┘
                              ↑ 绑定
                              │
┌──────────────────────────────────────────────────────────┐
│              UC_StatesCards (UI 层，不变)                 │
│  · UpdateDashboard(DashboardData)                        │
│  · ApplyKpi / ApplyNgTypes / ApplyRecords                │
│  · RedrawHourly                                          │
└──────────────────────────────────────────────────────────┘
```

---

## 3. 核心组件

### 3.1 DashboardSnapshot（不可变数据快照）

```csharp
public sealed class DashboardSnapshot
{
    // 累计 KPI
    public int Total { get; }
    public int Ok { get; }
    public int Ng { get; }
    public double YieldRate { get; }

    // 时段产量（最近 4 小时，每小时一条）
    public IReadOnlyList<HourlyData> Hourly { get; }

    // NG 类型分布（出站记录统计）
    public IReadOnlyList<NgTypeData> NgTypes { get; }

    // 最近记录（分页）
    public IReadOnlyList<RecentRecord> Recent { get; }

    // 分页信息
    public int TotalCount { get; }      // 总记录数
    public int PageIndex { get; }        // 当前页（0-based）
    public int PageSize { get; }         // 每页条数
    public int TotalPages { get; }        // 总页数

    // 序列号（用于变化检测）
    public long SequenceNumber { get; }
}
```

**设计原则**：
- `sealed` + 不可变属性，UI 线程只读不修改
- 每次更新生成新实例，旧实例可安全丢弃
- `SequenceNumber` 自增用于判断数据是否变化

### 3.2 DashboardWorker（后台工作线程）

**职责**：
- 管理 5 秒定时器
- 执行数据库查询（Task.Run）
- 解析 CellData 记录，计算聚合
- 完成后报告结果

**定时逻辑**：
```
启动 → 等待 5 秒 → 查询 → 解析 → 发送结果 → 等待 5 秒 → ...
```

**查询策略**：
- 时间窗口：最近 4 小时
- 入站记录：`SELECT * FROM CellData WHERE 进站时间 >= @p0 ORDER BY 进站时间 DESC`
- 出站记录：通过视觉检测参数判断（任一有值 OR 是否复投=1）
- 分页：LIMIT 100 OFFSET (pageIndex * pageSize)

### 3.3 DashboardService（线程安全容器）

**职责**：
- 持有当前 `DashboardSnapshot`
- 接收 Worker 报告的结果
- 比较新旧数据，差异显著时触发 `DashboardUpdateMessage`
- 对外提供只读访问

**线程安全**：
- 内部使用 `lock (_syncRoot)` 保护 `_currentSnapshot`
- 对外暴露 `DashboardSnapshot CurrentSnapshot { get; }`
- 消息发送在 UI 线程（Dispatcher.Invoke）

**变化检测**：
- 比较 `SequenceNumber`
- 或比较关键字段（Total, Ok, Ng）
- 变化超过阈值（如 5%）才发消息

### 3.4 分页支持

**查询参数**：
- `pageIndex`: 当前页（0-based）
- `pageSize`: 每页条数（默认 20）

**UI 控件**（在 UC_StatesCards 中已有部分支持）：
- 页码显示："第 X/Y 页"
- 首页/上一页/下一页/末页 按钮
- 跳转到指定页

**实现要点**：
- 分页查询使用 SQLite LIMIT/OFFSET
- 总记录数通过 `SELECT COUNT(*)` 单独查询
- 切换页时立即查询，不依赖缓存

---

## 4. 数据流

### 4.1 初始化流程

```
1. DashboardService 构造 → 启动 DashboardWorker
2. Worker 立即执行第一次查询（不等待定时器）
3. 查询完成 → 解析 → 报告结果
4. Service 收到结果 → 初始化 _currentSnapshot → 发送 DashboardUpdateMessage
5. UC_StatesCards.UpdateDashboard() 收到消息 → 更新 UI
```

### 4.2 定时刷新流程

```
1. 定时器触发（5秒）
2. Worker 执行 QueryRaw<CellData>(...)  →  Task.Run（后台线程）
3. 查询返回 ~1000 条 CellData 记录
4. 解析：计算 HourlyData[4]、NgTypes[]、RecentRecords[分页]
5. 生成新 DashboardSnapshot（SequenceNumber 自增）
6. 报告完成 → Service.ReceiveSnapshot()
7. Service 检测变化 → 差异显著则发送 DashboardUpdateMessage
8. UC_StatesCards.UpdateDashboard() 更新 UI
```

### 4.3 分页切换流程

```
1. 用户点击"下一页"
2. UC_StatesCards 触发 PageChanged 事件
3. Service.SetPageIndex(pageIndex)
4. Service 立即触发 LoadPage(pageIndex) → Worker 查询
5. Worker 查询 LIMIT/OFFSET → 返回该页 RecentRecords
6. 新 DashboardSnapshot 生成 → 发送 DashboardUpdateMessage
7. UI 更新（仅 RecentRecords 变化，其他保持不变）
```

---

## 5. 数据库查询

### 5.1 入站记录查询

```sql
SELECT * FROM CellData
WHERE 进站时间 >= @p0
ORDER BY 进站时间 DESC
LIMIT @p1 OFFSET @p2
```

- @p0 = 4 小时前的时间字符串
- @p1 = pageSize
- @p2 = pageIndex * pageSize

### 5.2 总记录数查询

```sql
SELECT COUNT(*) FROM CellData
WHERE 进站时间 >= @p0
```

### 5.3 出站 NG 类型查询

```sql
SELECT Ng类型1, Ng类型2, ..., Ng类型8
FROM CellData
WHERE 进站时间 >= @p0
AND (视觉检测参数一 IS NOT NULL AND 视觉检测参数一 != ''
  OR 视觉检测参数二 IS NOT NULL AND 视觉检测参数二 != ''
  OR ... OR 是否复投 = 1)
```

### 5.4 时段产量查询

入站时按小时聚合，SQLite 不支持 DATEPART，使用 C# 解析：
```
进站时间格式: "yyyy/MM/dd/HH:mm:ss"
解析 DateTime → 取 .Hour → 按小时分组统计 OK/NG
```

---

## 6. 类设计

### 6.1 新增类

| 类名 | 文件 | 说明 |
|------|------|------|
| `DashboardSnapshot` | Model/InspectionUtils.cs | 不可变数据快照，包含分页信息 |
| `DashboardWorker` | Service/DashboardWorker.cs | BackgroundWorker，负责查询和解析 |
| `DashboardService` 重构 | Service/DashboardService.cs | 线程安全容器，移除数据库直接访问 |

### 6.2 修改类

| 类名 | 修改内容 |
|------|---------|
| `InspectionUtils.RecentRecord` | 添加 `IsInbound` 字段（已有） |
| `UC_StatesCards.xaml.cs` | 添加分页事件处理（已有基础，需增强） |

---

## 7. 消息机制

| 消息 | 触发条件 | 接收方 |
|------|---------|--------|
| `DashboardUpdateMessage` | 数据变化显著（Total/Ok/Ng 任一变化 或 Hourly 任意小时变化） | UC_StatesCards.UpdateDashboard() |
| `StatusLightUpdateMessage` | 入站时触发（保持现有逻辑） | UC_StatesCards.ApplyStatusLight() |

**变化阈值**：
- 任意 KPI (Total/Ok/Ng) 差值 > 0
- 任意 HourlyData .Ok 或 .Ng 差值 > 0
- NgTypes 任意 Count 差值 > 0

---

## 8. 出站判定逻辑（不变）

```
出站条件：视觉检测参数一~六 任一有值  OR  是否复投 = 1
进站条件：视觉检测参数一~六 全部为空  AND  是否复投 = false
```

---

## 9. 实现顺序

1. **DashboardSnapshot** — 定义不可变数据结构
2. **DashboardWorker** — 后台查询 + 解析逻辑
3. **DashboardService 重构** — 移除数据库直接访问，接收 Worker 结果，触发消息
4. **UC_StatesCards 分页增强** — 添加分页控件事件处理
5. **测试验证** — 用 PLC 模拟器测试 5 秒刷新和分页

---

## 10. 注意事项

- Worker 使用 `Task.Run` 而非 Thread，确保线程池复用
- Service 锁保护 `_currentSnapshot`，UI 线程只读不写
- 入站/出站记录合并时去重（按电芯码 + 进站时间）
- 定时器在 Worker 忙时跳过本次执行（避免积压）
- 内存中不缓存历史分页数据，每次翻页重新查询