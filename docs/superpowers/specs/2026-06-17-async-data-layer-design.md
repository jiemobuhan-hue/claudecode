# 异步数据底座设计 — 自动机 DB 解耦

**日期**: 2026-06-17
**分支**: master
**状态**: 设计中

---

## 1. 问题

### 1.1 现状

`AutoRun.cs` 中 `ProductLeadStationHandler.ExecuteActionAsync`（第 1732 行）在自动机主循环中同步调用 `UpdateCellDataFromSQLserver()`，该方法通过 `_localBlueFilmDetectionRepositoryA.GetByCellCode()` 直接打开 `SqlConnection` 查询远程 SQL Server 视觉数据库。

**阻塞链条**:
```
自动机 Station 循环 (50ms tick)
  → WaitForSignalAsync (PLC 触发检测)
  → ExecuteActionAsync
    → SQLite 查询 CellData (本机, 快)
    → UpdateCellDataFromSQLserver → SqlConnection.Open() + SP 执行 (远程, 不可控)
    → getlead() 视觉排序
    → PLC 写入结果
    → SQLite BulkUpsert
```

当远程 SQL Server 因网络抖动/死锁/表扫描变慢时，`Open()` 或存储过程可能耗时 30s+，直接卡死整个自动机线程，导致 PLC 心跳信号中断，引发线体停机报警。

系统需要连接 3 台结构完全相同的 SQL Server:
- 本地 `DESKTOP-0F9L4KO\RJ` (具名实例)
- 远程 `DESKTOP-NHDST87`
- 远程 `DESKTOP-2ADDTIC`

### 1.2 目标

- 自动机线程绝对流畅，任何 DB 操作 <1ms 返回
- 读操作异步化：用 `TaskCompletionSource` + 后台消费者替代同步查询
- 写操作异步化：fire-and-forget 入队，后台批量消费
- 三库写入：本地优先确保，远程 3s 超时隔离，失败自动重试
- 零依赖：仅使用 .NET Framework 4.8 内置类型 + 现有 ADO.NET

---

## 2. 连接字符串

### 2.1 Settings 新增属性

```csharp
// 本地主库 — 注意: 具名实例在 @"" 逐字字符串中写单反斜杠
public static string SQLServer本地连接 { get; internal set; }
    = @"Data Source=DESKTOP-0F9L4KO\RJ;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";

public static string SQLServer远程连接1 { get; internal set; }
    = @"Data Source=DESKTOP-NHDST87;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";

public static string SQLServer远程连接2 { get; internal set; }
    = @"Data Source=DESKTOP-2ADDTIC;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";
```

### 2.2 转义说明

当前 `Settings.SQLServer视觉地址` 值为 `"DESKTOP-0F9L4KO\\RJ"`。在 C# `@""` 逐字字符串中，`\\` 代表两个字面反斜杠字符，传给 SQL Server 会变成 `DESKTOP-0F9L4KO\\RJ` 导致连接失败。正确写法是 `@"DESKTOP-0F9L4KO\RJ"`（逐字字符串中单反斜杠即字面反斜杠）。

---

## 3. 数据模型

### 3.1 T_BlueFilmDetection (已存在，不改)

位置: `Model/Vision/T_BlueFilmDetection.cs`

| 列 | 类型 | 说明 |
|----|------|------|
| Num | int? (PK, identity) | 自增主键 |
| CellType | string | 电芯型号 |
| CellCode | string | 电芯条码 |
| Reinvestment | int? | 是否复投 |
| DetectionArea | string | 检测区域 (面) |
| DetectionResults | string | 检测结果 (OK/NG) |
| NGtypeNum | int? | NG 类型数量 |
| NGtype1-3 | string | NG 类型详情 |
| CreateTime | DateTime? | 创建时间 |

### 3.2 T_BlueFilmDataMOM (已存在，不改)

位置: `Model/Vision/T_BlueFilmDataMOM.cs`

| 列 | 类型 | 说明 |
|----|------|------|
| Num | int? (PK, identity) | 自增主键 |
| SideCellType | string | 电芯型号 |
| CellCode | string | 电芯条码 |
| CreateTime | DateTime? | 创建时间 |
| ParamterCode | string | 工艺参数代码 |
| ParameterDesc | string | 参数描述 |
| Value | string | 测量值 |
| UpperLimit | string | 上限 |
| LowerLomit | string | 下限 |
| TargetValue | string | 目标值 |
| Unit | string | 单位 |
| ParameterResult | string | 参数判定结果 |

### 3.3 新增：读取请求模型

```csharp
/// <summary>
/// 异步读取请求 — 承载电芯码 + TaskCompletionSource 回调
/// </summary>
internal class ReadRequest
{
    public string CellCode { get; set; }
    public int ChannelNo { get; set; }
    public TaskCompletionSource<CellData> Completion { get; set; }
    public DateTime EnqueueTime { get; set; }
    // 超时保护: 如果入队超过 10 秒没被消费，自动返回默认 CellData
}
```

### 3.4 新增：重试记录模型

```csharp
/// <summary>
/// 远程库写入失败重试记录
/// </summary>
internal class RetryItem
{
    public string TargetConnectionString { get; set; }
    public string TargetServerName { get; set; }
    public object Payload { get; set; }        // T_BlueFilmDetection 或 T_BlueFilmDataMOM
    public string PayloadType { get; set; }     // "Detection" 或 "MOM"
    public int RetryCount { get; set; }
    public DateTime FirstFailTime { get; set; }
    public DateTime LastFailTime { get; set; }
    public string LastErrorMessage { get; set; }
}
```

---

## 4. 队列管理器设计

### 4.1 BlueFilmDataQueueManager

文件: `Service/BlueFilmDataQueueManager.cs`

单例，懒加载线程安全初始化。内部维护 3 个 `ConcurrentQueue<T>` + 1 个 `ConcurrentQueue<RetryItem>`。

**核心字段**:
```csharp
private readonly ConcurrentQueue<ReadRequest> _readQueue = new();
private readonly ConcurrentQueue<T_BlueFilmDetection> _writeDetectionQueue = new();
private readonly ConcurrentQueue<T_BlueFilmDataMOM> _writeMOMQueue = new();
private readonly ConcurrentQueue<RetryItem> _retryBuffer = new();

private readonly CancellationTokenSource _cts = new();
private Task _readConsumerTask;
private Task _writeDetectionConsumerTask;
private Task _writeMOMConsumerTask;
private Timer _retryTimer;  // 30s 周期
```

**公开 API**:

```csharp
// 异步读取 — 返回 Task<CellData>，自动机可 await
public Task<CellData> EnqueueReadAsync(string cellCode, int channelNo)

// 非阻塞写入 — void 立即返回
public void EnqueueDetectionResult(T_BlueFilmDetection data)
public void EnqueueMOMOutbound(T_BlueFilmDataMOM data)
public void EnqueueDetectionResultBatch(IEnumerable<T_BlueFilmDetection> dataList)
```

### 4.2 EnqueueReadAsync 实现

```csharp
public Task<CellData> EnqueueReadAsync(string cellCode, int channelNo)
{
    var tcs = new TaskCompletionSource<CellData>(TaskCreationOptions.RunContinuationsAsynchronously);
    _readQueue.Enqueue(new ReadRequest
    {
        CellCode = cellCode,
        ChannelNo = channelNo,
        Completion = tcs,
        EnqueueTime = DateTime.Now
    });
    return tcs.Task;  // <1ms 返回
}
```

### 4.3 EnqueueDetectionResult / EnqueueMOMOutbound 实现

```csharp
public void EnqueueDetectionResult(T_BlueFilmDetection data)
{
    _writeDetectionQueue.Enqueue(data);  // <1ms 返回
}

public void EnqueueMOMOutbound(T_BlueFilmDataMOM data)
{
    _writeMOMQueue.Enqueue(data);  // <1ms 返回
}
```

---

## 5. 后台消费者

### 5.1 ReadConsumer

```
loop:
  _readQueue.TryDequeue(out request)
  如果没有: Task.Delay(10ms), continue

  CellData result = new CellData { 电芯码 = request.CellCode }

  // 1. 查本地库 (优先)
  try {
    using conn = new SqlConnection(CONN_LOCAL)
    conn.Open()
    records = SP_GetBlueFilmDetection(cellCode)
    result = MergeRecords(result, records)
  } catch { Rlog.Warn }

  // 2. 查远程库1 (3s 超时, 独立 try-catch)
  try {
    using cts = new CancellationTokenSource(3000)
    Task.Run(() => { conn.Open(); SP... }, cts.Token)
    if 成功: result = MergeRecords(result, records)
  } catch (OperationCanceledException) { Rlog.Warn("远程库1查询超时") }
  catch (Exception ex) { Rlog.Error("远程库1查询失败", ex) }

  // 3. 查远程库2 (同上)

  // 聚合 NG 缺陷到 CellData.Ng类型1~8
  AggregateDefects(result, allRecords)

  // 唤醒自动机
  request.Completion.SetResult(result)
```

### 5.2 WriteRouter (Detection / MOM 共用)

```
对于每条写入记录：
  1. 写入 CONN_LOCAL:
     using conn = new SqlConnection(CONN_LOCAL)
     conn.Open()
     SP_Insert(record)
     // 本地不设超时或较长超时，必须成功

  2. 写入 CONN_REMOTE1 (3s CommandTimeout + CancellationToken):
     try {
       Task.Run(() => { ... }, 3000ms cts)
     } catch (OperationCanceledException) {
       _retryBuffer.Enqueue(RetryItem {
         TargetName = "DESKTOP-NHDST87",
         Payload = record,
         ...
       })
       Rlog.Error($"远程库1写入超时 [{DateTime.Now}]")
     } catch (Exception ex) {
       同上
     }

  3. 写入 CONN_REMOTE2 (同上)
```

### 5.3 重试定时器

```csharp
_retryTimer = new Timer(_ =>
{
    var items = new List<RetryItem>();
    while (_retryBuffer.TryDequeue(out var item))
        items.Add(item);

    foreach (var item in items)
    {
        if (item.RetryCount >= 10)
        {
            Rlog.Error($"重试次数耗尽 [{item.TargetServerName}], 数据: {JsonConvert.SerializeObject(item.Payload)}");
            continue; // 丢弃
        }

        item.RetryCount++;
        item.LastFailTime = DateTime.Now;

        try
        {
            using var cts = new CancellationTokenSource(3000);
            var task = Task.Run(() => WriteSingle(item.TargetConnectionString, item.Payload, item.PayloadType), cts.Token);
            if (task.Wait(3000, cts.Token))
            {
                // 成功，不重新入队
                Rlog.Info($"重试成功 [{item.TargetServerName}] 第{item.RetryCount}次");
                continue;
            }
        }
        catch (Exception ex)
        {
            item.LastErrorMessage = ex.Message;
        }

        // 失败，重新入队
        _retryBuffer.Enqueue(item);
    }
}, null, 30000, 30000); // 首次 30s，之后每 30s
```

---

## 6. AutoRun.cs 集成

### 6.1 位置 1: 分流工位读取视觉数据（ProductLeadStationHandler）

**当前代码** (`AutoRun.cs` ~1726-1732):
```csharp
if (data != null)
{
    lock (_listDataLock)
    {
        try { _owner.UpdateCellDataFromSQLserver(ref data); }
        catch (Exception ex) { }
    }
    int way = _owner.getlead(data);
```

**替换为**:
```csharp
if (data != null)
{
    // 异步读取视觉检测结果 — 不阻塞自动机线程
    data = await BlueFilmDataQueueManager.I.EnqueueReadAsync(tempcode, _channelNo);

    int way = _owner.getlead(data);
```

不再需要 `lock (_listDataLock)`，因为 `EnqueueReadAsync` 内部通过队列串行化所有 DB 访问。

### 6.2 位置 2: 分流出站 MOM 上报

**当前代码** (`AutoRun.cs` ~1762-1763):
```csharp
var temp = new List<CellData> { data };
SQLiteGenericHelper.BulkUpsert<CellData>(temp, "电芯码", "CellData");
```

**追加** (SQLite 写入保留在自动机线程，因为本地极快):
```csharp
var temp = new List<CellData> { data };
SQLiteGenericHelper.BulkUpsert<CellData>(temp, "电芯码", "CellData");

// 非阻塞: 构建 MOM 出站数据并入队
var momData = BuildMOMOutbound(data);
BlueFilmDataQueueManager.I.EnqueueMOMOutbound(momData);
```

### 6.3 Init 变更

`AutoRun.Init()` 中:
```csharp
// 旧: 初始化 Repository
_localBlueFilmDetectionRepositoryA = new BlueFilmDetectionRepository(GetVisionConnectionString());

// 新: 初始化队列管理器
BlueFilmDataQueueManager.I.Init();
```

不再需要在 AutoRun 中持有 Repository 实例，队列管理器内部管理 3 个 Repository。

---

## 7. 异常隔离与日志

### 7.1 超时策略

| 操作 | 超时 | 异常处理 |
|------|------|---------|
| 本地库读取 | 10s CommandTimeout | 记录 Rlog.Warn，返回部分结果 |
| 远程库读取 | 3s CancellationToken | 独立 try-catch，不影响其他库 |
| 本地库写入 | 10s CommandTimeout | 记录 Rlog.Error，不入重试队列 (本地库必须可用) |
| 远程库写入 | 3s CancellationToken | 入重试缓冲区，Rlog.Error |
| EnqueueReadAsync 整体 | 10s (入队到完成) | 超时返回空白 CellData，不阻塞自动机 |

### 7.2 日志格式

```
Rlog.Error($"[BlueFilmDataQueue] 远程库写入失败 | 服务器: {serverName} | 类型: {payloadType} | 异常: {ex.Message} | 时间: {DateTime.Now}");
Rlog.Error($"[BlueFilmDataQueue] 重试次数耗尽 | 服务器: {serverName} | 数据: {JsonConvert.SerializeObject(payload)}");
Rlog.Warn($"[BlueFilmDataQueue] 查询超时 | 服务器: {serverName} | 电芯码: {cellCode} | 超时: 3s");
```

---

## 8. 文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Service/Settings.cs` | 修改 | 新增 3 个连接字符串属性 |
| `Service/BlueFilmDataQueueManager.cs` | 新建 | 队列管理器单例 + 消费者 + 重试 |
| `Service/DataReadRequest.cs` | 新建 | 读取请求模型 |
| `Service/DataRetryItem.cs` | 新建 | 重试记录模型 |
| `Model/AutoRun.cs` | 修改 | 替换 UpdateCellDataFromSQLserver + MOM 写入为 Enqueue 调用 |
| `ViewModels/SettingViewModel.cs` | 修改 | 新增 3 个连接字符串属性绑定 |

---

## 9. 清理项

AutoRun.cs 中需要移除的成员:
- `private HarnessMeasureRepository _localHarnessMeasureRepositoryA` (line 287)
- `private BlueFilmDetectionRepository _localBlueFilmDetectionRepositoryA` (line 288)
- `private string GetVisionConnectionString()` (line 284-285)
- `_listDataLock` 对象和相关 lock 块 (不再需要，队列串行化代替)

这些成员由 `BlueFilmDataQueueManager` 内部管理。

---

## 10. 风险与注意事项

1. **本地库必须可用**: 不做重试队列，本地库挂掉直接异常。这是合理的 — 本地 SQL Server 和上位机在同一台工控机上，不可用概率极低。
2. **TaskCompletionSource 内存**: `EnqueueReadAsync` 创建 TCS，后台消费者必须确保最终调用 `SetResult/SetCanceled/SetException`，否则造成内存泄漏。通过 10s 超时兜底。
3. **Graceful Shutdown**: `BlueFilmDataQueueManager.Dispose()` 需 `_cts.Cancel()` + `Task.WhenAny(consumers, 5000)` 等待消费完成。
4. **现有 SQLite 写入保留在自动机线程**: `SQLiteGenericHelper.BulkUpsert` 走本地 `DbWriteQueue` 已在 <1ms 内返回，不是瓶颈。
