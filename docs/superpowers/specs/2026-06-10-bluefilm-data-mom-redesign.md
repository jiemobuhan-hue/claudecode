# T_BlueFilmDataMOM 表结构重设计

**日期**: 2026-06-10 | **状态**: 已确认 | **方案**: 单表扩展 (方案一)

## 业务背景

蓝膜视觉检测设备拍照后识别电芯表面缺陷（气泡、划伤、针孔等），每个缺陷有多个可测量参数（长度、宽度、面积等），每个参数需要记录测量值、规格上下限、目标值、单位、判定结果，并上传 MES 系统。

当前 `T_BlueFilmDataMOM` 表是**一行一电芯**的扁平结构，缺陷信息通过 `NGtype1/2/3` 三个固定列记录，无法满足"一个电芯多个缺陷，每个缺陷多个参数"的细粒度记录需求。

## 设计决策

- **方案一：单表扩展** — 在现有表追加 8 个参数列，保留所有旧列兼容
- **一行 = 一个缺陷实例的一个参数**（例如一个电芯 3 缺陷 × 4 参数 = 12 行）
- `Num` 保持自增主键，不加业务唯一约束
- 所有参数列用 `NVARCHAR` 存储，与配方表 `T_BlueFilmRecipeParameters` 风格一致，避免类型转换问题

### 字段分工

| 字段 | 职责 |
|------|------|
| `ParamterCode` | 配方约定的标准工艺参数代码（机器可读） |
| `ParameterDesc` | 缺陷上下文描述：类型 + 位置 + 序号（人类可读） |
| `DetectionResults` | 该参数的整体判定 (OK/NG) |
| `ParameterResult` | 该参数的细粒度判定结果 |
| `Value` / `UpperLimit` / `LowerLomit` / `TargetValue` / `Unit` | 测量值、判定依据及量纲 |

## 表结构

```sql
-- 新增列
ALTER TABLE T_BlueFilmDataMOM ADD
    ParamterCode   NVARCHAR(100) NULL,
    ParameterDesc  NVARCHAR(200) NULL,
    Value          NVARCHAR(50)  NULL,
    UpperLimit     NVARCHAR(50)  NULL,
    LowerLomit     NVARCHAR(50)  NULL,
    TargetValue    NVARCHAR(50)  NULL,
    Unit           NVARCHAR(20)  NULL,
    ParameterResult NVARCHAR(20) NULL;
```

旧列 (`Num`, `SideCellType`, `CellCode`, `DetectionArea`, `DetectionResults`, `NGtypeNum`, `NGtype1`, `NGtype2`, `NGtype3`, `CreateTime`) 全部保留。

## C# Model

```csharp
public class T_BlueFilmDataMOM
{
    // ── 保留字段 ──
    public int? Num { get; set; }
    public string SideCellType { get; set; }
    public string CellCode { get; set; }
    public string DetectionArea { get; set; }
    public string DetectionResults { get; set; }
    public DateTime? CreateTime { get; set; }

    // ── 兼容字段（旧 NG 结构，保留读取不再写入） ──
    public int? NGtypeNum { get; set; }
    public string NGtype1 { get; set; }
    public string NGtype2 { get; set; }
    public string NGtype3 { get; set; }

    // ── 新增字段 ──
    public string ParamterCode { get; set; } = "";
    public string ParameterDesc { get; set; } = "";
    public string Value { get; set; } = "";
    public string UpperLimit { get; set; } = "";
    public string LowerLomit { get; set; } = "";
    public string TargetValue { get; set; } = "";
    public string Unit { get; set; } = "";
    public string ParameterResult { get; set; } = "";
}
```

同文件中删除 `MOM_ParameterInfo` 类。

## 影响范围

### Repository
- `Service/CRUDServices/BlueFilmDataMOMRepository.cs` — Insert/Update/Query 追加 8 个新列
- `Repositories/BlueFilmDataMOMRepository.cs` (VerifyProject3) — 同步修改

### 存储过程
- `Proc_InsertBlueFilmDataMOM` — 增加 8 个参数
- `PROC_GetBlueFilmDataMOM` — SELECT 增加 8 列

### Service
- `Service/AutoRun.cs` — 数据填充逻辑从单行改为多行（按缺陷×参数展开）
- `Service/MomHandler.cs` — 删除 `MOM_ParameterInfo`，改用 `T_BlueFilmDataMOM` 直传

### 验证工具
- `VerifyProject3/Models/T_BlueFilmDataMOM.cs` — 同步 8 个新属性
- `VerifyProject3/Program.cs` — 测试用例增加新字段覆盖

## 实施步骤

| # | 步骤 | 位置 |
|---|------|------|
| 1 | DDL: ALTER TABLE 追加 8 列 | SQL Server |
| 2 | 修改 `Proc_InsertBlueFilmDataMOM` 存储过程 | SQL Server |
| 3 | 修改 `PROC_GetBlueFilmDataMOM` 存储过程 | SQL Server |
| 4 | 更新 Model `T_BlueFilmDataMOM`（+8 属性，-MOM_ParameterInfo） | 主项目 + VerifyProject3 |
| 5 | 更新 Repository CRUD | 主项目 + VerifyProject3 |
| 6 | 更新 `AutoRun.cs` 填充逻辑 | Service |
| 7 | 更新 `MomHandler.cs` 去 MOM_ParameterInfo | Service |
| 8 | 更新 VerifyProject3 测试用例 | Program.cs |
| 9 | 构建验证 | dotnet build / msbuild |
