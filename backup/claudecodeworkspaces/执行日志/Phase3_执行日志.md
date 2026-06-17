# Phase 3 执行日志

**日期**: 2026-04-30
**操作人**: Claude Code
**Phase**: 3 - AutoRun 发送消息

---

## 执行内容

### 修改 Service/AutoRun.cs

**文件**: `D:\蓝膜外观检测上位机\ZenergyBFSI0430\Service\AutoRun.cs`

#### 1. ProductArrive 添加 RecordArrive

**位置**: `ProductArrive(int no)` 方法内，CellData 保存后

```csharp
// 记录入站统计（看板数据更新）
DashboardService.I.RecordArrive(code, MOMRes, no);
```

**说明**: 
- `code` - 电芯码
- `MOMRes` - MOM查询结果 "OK"/"NG"/"离线"
- `no` - 工位号 1-4

#### 2. ProductLeadArrive 添加 RecordExit

**位置**: `ProductLeadArrive(int no)` 方法内，CellData Upsert 后

```csharp
// 记录出站统计（看板数据更新）
string ngTypes = string.Join("|", new[] { data.Ng类型1, data.Ng类型2, data.Ng类型3, data.Ng类型4, data.Ng类型5, data.Ng类型6, data.Ng类型7, data.Ng类型8 }
    .Where(s => !string.IsNullOrEmpty(s)));
DashboardService.I.RecordExit(code, data.视觉检测结果, ngTypes);
```

**说明**:
- `code` - 电芯码
- `data.视觉检测结果` - 视觉检测结果
- `ngTypes` - NG类型拼接字符串（用"|"分隔）

---

## 消息流程

```
AutoRun.ProductArrive(no)
    ↓
MOM查询 → MOMRes = "OK" / "NG" / "离线"
    ↓
SQLiteGenericHelper.BulkUpsert<CellData>(...)
    ↓
DashboardService.I.RecordArrive(code, MOMRes, no)
    ↓
Messenger.Default.Send(StatusLightUpdateMessage)
Messenger.Default.Send(DashboardUpdateMessage)
    ↓
UC_Home.OnStatusLightUpdate() / OnDashboardUpdate()
    ↓
_dash.UpdateStatusLight() / _dash.UpdateDashboard()
```

```
AutoRun.ProductLeadArrive(no)
    ↓
视觉检测 → data.视觉检测结果
    ↓
SQLiteGenericHelper.BulkUpsert<CellData>(...)
    ↓
DashboardService.I.RecordExit(code, data.视觉检测结果, ngTypes)
    ↓
Messenger.Default.Send(DashboardUpdateMessage)
    ↓
UC_Home.OnDashboardUpdate()
    ↓
_dash.UpdateDashboard()
```

---

## 待验证项 (V5)

| 编号 | 验证项 | 状态 |
|------|--------|------|
| V5.1 | AutoRun.cs 有 `using ZenergyBFSI.Service;` | 待验证 |
| V5.2 | ProductArrive 中 RecordArrive 调用存在 | 待验证 |
| V5.3 | ProductLeadArrive 中 RecordExit 调用存在 | 待验证 |
| V5.4 | ngTypes 拼接逻辑正确（过滤空值） | 待验证 |
| V5.5 | RecordExit 参数顺序: (cellCode, exitResult, ngTypes) | 待验证 |

---

## 下一步

- **Phase 4**: 添加 SimulationMode 配置到 App.config 和 Settings
- **Phase 5**: 验证编译和运行时行为
