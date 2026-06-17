# Phase 2 执行日志

**日期**: 2026-04-30
**操作人**: Claude Code
**Phase**: 2 - UC_Home 订阅消息

---

## 执行内容

### 修改 View/UC_Home.xaml.cs

**文件**: `D:\蓝膜外观检测上位机\ZenergyBFSI0430\View\UC_Home.xaml.cs`

#### 添加 using

```csharp
using ZenergyBFSI.Model.Messages;
```

#### OnUnloaded 添加取消订阅

```csharp
private void OnUnloaded(object sender, RoutedEventArgs e)
{
    // 取消消息订阅
    Messenger.Default.Unregister<DashboardUpdateMessage>(this);
    Messenger.Default.Unregister<StatusLightUpdateMessage>(this);
}
```

#### OnLoaded 中添加注册订阅

```csharp
private async void OnLoaded(object sender, RoutedEventArgs e)
{
    // 注册消息订阅
    Messenger.Default.Register<DashboardUpdateMessage>(this, OnDashboardUpdate);
    Messenger.Default.Register<StatusLightUpdateMessage>(this, OnStatusLightUpdate);

    // ... 原有代码 ...
}
```

#### 新增消息处理方法

```csharp
/// <summary>
/// 处理 DashboardUpdateMessage 消息
/// </summary>
private void OnDashboardUpdate(DashboardUpdateMessage message)
{
    Dispatcher.Invoke(() =>
    {
        _dash.UpdateDashboard(message.Data);
    });
}

/// <summary>
/// 处理 StatusLightUpdateMessage 消息
/// </summary>
private void OnStatusLightUpdate(StatusLightUpdateMessage message)
{
    Dispatcher.Invoke(() =>
    {
        _dash.UpdateStatusLight(message.Result, message.CellCode, message.Time);
    });
}
```

---

## 消息流程

```
AutoRun.ProductArrive()
    ↓
DashboardService.RecordArrive()
    ↓
Messenger.Default.Send(new StatusLightUpdateMessage(...))
Messenger.Default.Send(new DashboardUpdateMessage(...))
    ↓
UC_Home.OnStatusLightUpdate() / OnDashboardUpdate()
    ↓
_dash.UpdateStatusLight() / _dash.UpdateDashboard()
```

---

## 待验证项 (V4)

| 编号 | 验证项 | 状态 |
|------|--------|------|
| V4.1 | UC_Home 有 ZenergyBFSI.Model.Messages using | 待验证 |
| V4.2 | Messenger.Default.Register 调用存在 | 已完成 |
| V4.3 | OnDashboardUpdate 方法存在 | 已完成 |
| V4.4 | OnStatusLightUpdate 方法存在 | 已完成 |
| V4.5 | Unregister 在 OnUnloaded 中调用 | 已完成 |
| V4.6 | Dispatcher.Invoke 用于UI更新 | 已完成 |

---

## 下一步

- **Phase 3**: 修改 AutoRun 调用 RecordArrive/RecordExit