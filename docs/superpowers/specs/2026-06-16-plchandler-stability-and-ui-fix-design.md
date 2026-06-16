# PLCHandler 稳定性修复 & UI 重新设计

## 背景

蓝膜外观检测上位机 PLCHandler 模块存在两个紧耦合问题：
1. PLC 通信连接频繁断开且无法自恢复
2. UI 信号监控页面切换 PLC 时数据显示混乱

## 根因分析（7 个问题，按严重度排序）

| # | 位置 | 问题 | 严重度 |
|---|------|------|--------|
| 1 | `PlcChannel.cs:100-106` | 单次信号读取失败立即触发全量重连 | P1 |
| 2 | `PlcChannel.cs:136` | `RetryPolicy` 10 次重试耗尽后永久 Faulted，无自恢复 | P0 |
| 3 | `AutoRun.cs:489-518` | `DeviceLink()` 用 `Thread.Sleep(1000)` 阻塞主线程，`SetInt_Plc` fire-and-forget | P0 |
| 4 | `OmronConnection.cs` | `OmronFinsNet` 被 8 个 Station Task + 心跳循环并发访问，`HslCommunication` 底层 Socket 非线程安全 | P1 |
| 5 | `OmronConnection.cs:37` | `ConnectServer()` 的超时只作用于 `Task.Run` wrapper，不作用于底层 socket | P2 |
| 6 | `OmronConnection` | 未配置 `ReceiveTimeOut` / TCP KeepAlive，静默断连无法检测 | P2 |
| 7 | `PLCBoardViewModel.cs:183-199` | `RefreshSignals()` 切换 PLC 时只追加不清除旧信号条目 | P1 |

## 修复范围

本次只修 P0 + P1 问题（#1, #2, #3, #4, #7）。P2 留待后续观察 TCP 层表现后再决定。

---

## 一、UI 重新设计

### 当前问题

`PLCBoard.xaml` 用左侧 PLC 列表 + 右侧内容区布局，信号监控通过 `SelectedPlcId` 筛选。`PLCBoardViewModel.RefreshSignals()` 切换 PLC 时**只追加不清除**旧信号——`_signals` 集合持续累积来自多个 PLC 的 `SignalDisplayItem`，导致数据显示混杂。

### 新设计

**去掉左侧导航面板，顶部改为 TabControl**：

```
┌──────────────────────────────────────────┐
│  [PLC1: omron_1 ●]  [PLC2: omron_2 ●]   │  ← TabItem, 绑定 PlcStatusItem
├──────────────────────────────────────────┤
│  ● │ 名称    │ 地址  │ 类型 │ 当前值 │ 更新 │  ← DevExpress GridControl
│  ● │ 来料触发 │ D100  │ Int  │  1    │14:32 │
│  ...                                     │
├──────────────────────────────────────────┤
│  ● 1/2 PLC 已连接 │ 32 信号 │ 14:32:15   │  ← 状态栏（不变）
└──────────────────────────────────────────┘
```

**数据层改动**：

`PLCBoardViewModel` 将单例 `ObservableCollection<SignalDisplayItem> _signals` 改为字典分桶：

```csharp
// 每个 PlcId 一个独立的 ObservableCollection
private Dictionary<string, ObservableCollection<SignalDisplayItem>> _signalsByPlc;

// UI 绑定 — 根据当前选中的 Tab 切换 ItemsSource
public ObservableCollection<SignalDisplayItem> ActiveSignals { get; set; }
```

`OnSignalUpdate` 路由逻辑：
1. 根据 `update.PlcId` 找到对应桶
2. 写入该桶（更新或新增 SignalDisplayItem）
3. 无需跨 PLC 过滤，无污染

**改动量**：
- `PLCBoardViewModel.cs` — ~30 行（`_signalsByPlc` 字典 + `ActiveSignals` 切换逻辑）
- `PLCBoard.xaml` — ~30 行（左侧面板 → TabControl）
- `SignalMonitorView.xaml` — ~10 行（Tab 内容嵌入）

---

## 二、轮询/重连架构修复

### 2.1 PlcChannel — 连续失败容错

**改前**：`PollLoopAsync` 中任一信号读取失败 → 立即 `ReconnectLoopAsync`

**改后**：
```
_consecutiveFailures (int, 初值 0)
  ├── result.IsOk  → _consecutiveFailures = 0  (重置)
  └── !result.IsOk → _consecutiveFailures++
                      ├── < 3  → continue（跳过，读下一个信号）
                      └── ≥ 3  → ReconnectLoopAsync（触发重连）
```

**关键代码**（`PlcChannel.cs`）：
```csharp
if (!result.IsOk) {
    _consecutiveFailures++;
    if (_consecutiveFailures >= 3) {
        State = ConnectionState.Reconnecting;
        await ReconnectLoopAsync(ct);
        return;
    }
    continue; // 单次失败不触发重连
} else {
    _consecutiveFailures = 0;
}
```

### 2.2 RetryPolicy — 永久重试

**改前**：10 次指数退避后 `IsExhausted = true` → 永久 `Faulted`

**改后**：
- 前 10 次：现有逻辑不变（500ms → 1s → 2s → 4s → ... → 30s）
- 第 11+ 次：固定 30s 间隔持续重试，`IsExhausted` 始终返回 `false`
- 新增 `IsDegraded` 属性标识超出最大退避次数
- `State` 保持 `Reconnecting`，不用 `Faulted`

**改动**（`RetryPolicy.cs`）：
```csharp
private readonly int _permanentRetryIntervalMs = 30000;

public bool IsDegraded => _retryCount > _maxRetries;

public async Task<bool> WaitForNextRetryAsync(CancellationToken ct) {
    _retryCount++;
    int delay;
    if (_retryCount <= _maxRetries)
        delay = Math.Min(_baseDelayMs * (int)Math.Pow(2, _retryCount - 1), _maxDelayMs);
    else
        delay = _permanentRetryIntervalMs;
    await Task.Delay(delay, ct);
    return true;
}
```

### 2.3 OmronConnection — 串行化

**改前**：所有 `Read*`/`Write*`/`Connect*` 无锁并发访问 `OmronFinsNet`

**改后**：`OmronConnection` 内部加 `SemaphoreSlim(1, 1)`，关键操作串行化：

```csharp
private readonly SemaphoreSlim _sem = new SemaphoreSlim(1, 1);

public OperateResult<bool> ReadBool(string address) {
    _sem.Wait();
    try { return _plc.ReadBool(address); }
    finally { _sem.Release(); }
}
```

全量写操作（`ConnectAsync`、`DisconnectAsync`、`Read*`、`Write*`）均加锁。

### 2.4 AutoRun.DeviceLink — async 化

**改前**：
- `Thread.Sleep(1000)` 阻塞主循环
- `SetInt_Plc(...)` 返回 `Task` 不 await
- `heartloop` bool toggle 不可靠

**改后**：
```csharp
private async Task<bool> DeviceLinkAsync() {
    if (_monitor != null && _monitor.IsConnected("omron_1") && _monitor.IsConnected("omron_2")) {
        await SetInt_Plc("PLC心跳响应", 1);
        await SetInt_Plc("出站心跳", 1);
        // 更新 UI 状态颜色 ...
        return true;
    } else {
        // 断连：心跳写 0
        await SetInt_Plc("PLC心跳响应", 0);
        await SetInt_Plc("出站心跳", 0);
        return false;
    }
}
```

调用侧：`Thread_Run` 主循环中 `DeviceLink()` → `await DeviceLinkAsync()`，去掉 `heartloop` 变量。

---

## 三、完整改动清单

| 文件 | 改动 | 行数 |
|------|------|------|
| `PlcChannel.cs` | 连续失败计数器 + 容错逻辑 | ~20 |
| `RetryPolicy.cs` | `IsExhausted` → 永久重试 + `IsDegraded` | ~15 |
| `OmronConnection.cs` | `SemaphoreSlim` 串行化所有读写 | ~10 |
| `AutoRun.cs` | `DeviceLink` async 化，去掉 `Thread.Sleep` | ~20 |
| `PLCBoardViewModel.cs` | `_signalsByPlc` 字典分桶 + `ActiveSignals` | ~30 |
| `PLCBoard.xaml` | 左侧导航 → TabControl | ~30 |
| `SignalMonitorView.xaml` | Tab 内嵌绑定 | ~10 |
| **合计** | | **~135** |

无新文件，无架构变更，不引入新依赖。

---

## 四、验证标准

1. **连续失败容错**：模拟 PLC 偶发超时（≤2 次），信号列表不触发重连，日志显示 `continue`
2. **永久重试**：断开 PLC 网络 > 2 分钟，恢复后 30s 内自动重连成功
3. **UI 无混淆**：切换 PLC1/PLC2 Tab 各 10 次，信号列表始终只显示对应 PLC 的信号
4. **心跳不阻塞**：改后 `DeviceLinkAsync` 不再出现 `Thread.Sleep`，主循环间隔稳定在 ~80ms
5. **并发安全**：8 工位同时运行时不再出现 `HslCommunication` Socket 异常
