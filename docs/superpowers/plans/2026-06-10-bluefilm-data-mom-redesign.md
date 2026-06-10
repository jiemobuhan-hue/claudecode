# T_BlueFilmDataMOM 表结构重设计 — 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 `T_BlueFilmDataMOM` 表追加 8 个参数列，保留旧 NG 列兼容，同步更新主项目和 VerifyProject3 的 Model + Repository + 测试用例。

**Architecture:** 单表扩展方案 — 保持 `T_BlueFilmDataMOM` 一张表，追加 ParamterCode / ParameterDesc / Value / UpperLimit / LowerLomit / TargetValue / Unit / ParameterResult 八列。一行记录 = 一个缺陷实例的一个参数值。

**Tech Stack:** .NET Framework 4.8 (主项目), .NET 8 (VerifyProject3), SQL Server (VisionProgram), ADO.NET / Dapper

**注意:** `MOM_ParameterInfo` 类仅在 `Model/Vision/T_BlueFilmDataMOM.cs` 中定义，`MomHandler.cs` 实际使用 WCF 生成的 `ParameterInfo` 类型，不受影响。删除 `MOM_ParameterInfo` 在 Task 4 完成即可。

---

### Task 1: SQL Server — DDL 追加 8 列

**前置条件:** 需在 VisionProgram 数据库执行，`DESKTOP-0F9L4KO\RJ` 实例。

- [ ] **Step 1: 执行 ALTER TABLE**

```sql
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

- [ ] **Step 2: 验证列已新增**

```sql
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'T_BlueFilmDataMOM'
  AND COLUMN_NAME IN ('ParamterCode','ParameterDesc','Value','UpperLimit','LowerLomit','TargetValue','Unit','ParameterResult');
```

预期：返回 8 行。

---

### Task 2: SQL Server — 修改 Insert 存储过程

- [ ] **Step 1: 修改 `Proc_InsertBlueFilmDataMOM`**

```sql
ALTER PROCEDURE Proc_InsertBlueFilmDataMOM
    @SideCellType   NVARCHAR(10),
    @CellCode       NVARCHAR(50),
    @DetectionArea  NVARCHAR(10),
    @DetectionResults NVARCHAR(10),
    @NGtypeNum      INT,
    @NGtype1        NVARCHAR(10),
    @NGtype2        NVARCHAR(10),
    @NGtype3        NVARCHAR(10),
    @CreateTime     DATETIME,
    @ParamterCode   NVARCHAR(100) = NULL,
    @ParameterDesc  NVARCHAR(200) = NULL,
    @Value          NVARCHAR(50)  = NULL,
    @UpperLimit     NVARCHAR(50)  = NULL,
    @LowerLomit     NVARCHAR(50)  = NULL,
    @TargetValue    NVARCHAR(50)  = NULL,
    @Unit           NVARCHAR(20)  = NULL,
    @ParameterResult NVARCHAR(20) = NULL
AS
BEGIN
    INSERT INTO T_BlueFilmDataMOM (
        SideCellType, CellCode, DetectionArea, DetectionResults,
        NGtypeNum, NGtype1, NGtype2, NGtype3, CreateTime,
        ParamterCode, ParameterDesc, Value, UpperLimit, LowerLomit,
        TargetValue, Unit, ParameterResult
    ) VALUES (
        @SideCellType, @CellCode, @DetectionArea, @DetectionResults,
        @NGtypeNum, @NGtype1, @NGtype2, @NGtype3, @CreateTime,
        @ParamterCode, @ParameterDesc, @Value, @UpperLimit, @LowerLomit,
        @TargetValue, @Unit, @ParameterResult
    );
END
```

> 新参数全部默认 NULL，确保旧调用方（不传新参数）不受影响。

- [ ] **Step 2: 验证存储过程**

```sql
EXEC Proc_InsertBlueFilmDataMOM
    @SideCellType = 'T', @CellCode = 'TEST_PARAM', @DetectionArea = 'A',
    @DetectionResults = 'OK', @NGtypeNum = 0,
    @NGtype1 = NULL, @NGtype2 = NULL, @NGtype3 = NULL,
    @CreateTime = GETDATE(),
    @ParamterCode = 'LENGTH', @ParameterDesc = '气泡#1-长度',
    @Value = '3.2', @UpperLimit = '5.0', @LowerLomit = '0.5',
    @TargetValue = '2.0', @Unit = 'mm', @ParameterResult = 'OK';
```

查询验证：`SELECT * FROM T_BlueFilmDataMOM WHERE CellCode = 'TEST_PARAM'`

---

### Task 3: SQL Server — 修改 Select 存储过程

- [ ] **Step 1: 修改 `PROC_GetBlueFilmDataMOM`**

在 SELECT 列表末尾追加 8 列（含中文别名）：

```sql
ALTER PROCEDURE PROC_GetBlueFilmDataMOM
    @pageIndex INT,
    @pageSize INT,
    @startTime DATETIME,
    @endTime DATETIME,
    @CellCode NVARCHAR(50)
AS
BEGIN
    -- ... 保持原有逻辑不变 ...

    -- SELECT 中追加以下 8 列：
    --   ParamterCode   AS 工艺参数代码,
    --   ParameterDesc  AS 参数描述,
    --   Value          AS 测量值,
    --   UpperLimit     AS 上限,
    --   LowerLomit     AS 下限,
    --   TargetValue    AS 目标值,
    --   Unit           AS 单位,
    --   ParameterResult AS 参数判定结果
END
```

> 注意：此存储过程 COUNT 部分有已知 bug（走 `T_BlueFilmSide`），但本次不修复，仅追加列。

- [ ] **Step 2: 验证**

```sql
EXEC PROC_GetBlueFilmDataMOM
    @pageIndex = 1, @pageSize = 10,
    @startTime = '2000-01-01', @endTime = '2099-12-31',
    @CellCode = 'TEST_PARAM'
```

预期：返回表含新增的 8 个中文列。

---

### Task 4: 更新主项目 Model — `T_BlueFilmDataMOM`

**Files:**
- Modify: `Model/Vision/T_BlueFilmDataMOM.cs`

- [ ] **Step 1: 追加 8 个属性，删除 `MOM_ParameterInfo` 类**

将文件内容替换为：

```csharp
using System;

namespace ZenergyBFSI.Model.Vision
{
    /// <summary>
    /// 蓝膜MOM数据实体类 (对应数据库表 T_BlueFilmDataMOM)
    /// 保留旧 NG 列兼容，新增 8 个参数列
    /// </summary>
    public class T_BlueFilmDataMOM
    {
        // ── 保留字段 ──
        public int? Num { get; set; }
        public string SideCellType { get; set; }
        public string CellCode { get; set; }
        public string DetectionArea { get; set; }
        public string DetectionResults { get; set; }
        public DateTime? CreateTime { get; set; }

        // ── 兼容字段（旧 NG 结构） ──
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
}
```

> `MOM_ParameterInfo` 类已删除——`T_BlueFilmDataMOM` 直接携带 MOM 所需全部字段。

- [ ] **Step 2: Commit**

```bash
git add Model/Vision/T_BlueFilmDataMOM.cs
git commit -m "feat: add 8 parameter columns to T_BlueFilmDataMOM model, remove MOM_ParameterInfo"
```

---

### Task 5: 更新主项目 Repository — `BlueFilmDataMOMRepository`

**Files:**
- Modify: `Service/CRUDServices/BlueFilmDataMOMRepository.cs`

- [ ] **Step 1: Insert — 追加 8 个存储过程参数**

在 `Insert` 方法的存储过程参数块中，`@CreateTime` 之后追加：

```csharp
cmd.Parameters.AddWithValue("@ParamterCode", (object)model.ParamterCode ?? DBNull.Value);
cmd.Parameters.AddWithValue("@ParameterDesc", (object)model.ParameterDesc ?? DBNull.Value);
cmd.Parameters.AddWithValue("@Value", (object)model.Value ?? DBNull.Value);
cmd.Parameters.AddWithValue("@UpperLimit", (object)model.UpperLimit ?? DBNull.Value);
cmd.Parameters.AddWithValue("@LowerLomit", (object)model.LowerLomit ?? DBNull.Value);
cmd.Parameters.AddWithValue("@TargetValue", (object)model.TargetValue ?? DBNull.Value);
cmd.Parameters.AddWithValue("@Unit", (object)model.Unit ?? DBNull.Value);
cmd.Parameters.AddWithValue("@ParameterResult", (object)model.ParameterResult ?? DBNull.Value);
```

- [ ] **Step 2: Update SQL — 追加 8 列**

将 `Update` 方法中的 UPDATE SQL 替换为：

```csharp
public int Update(T_BlueFilmDataMOM model)
{
    return ExecNonQuery(@"
        UPDATE T_BlueFilmDataMOM SET
            SideCellType = @SideCellType, CellCode = @CellCode,
            DetectionArea = @DetectionArea, DetectionResults = @DetectionResults,
            NGtypeNum = @NGtypeNum,
            NGtype1 = @NGtype1, NGtype2 = @NGtype2, NGtype3 = @NGtype3,
            ParamterCode = @ParamterCode,
            ParameterDesc = @ParameterDesc,
            Value = @Value,
            UpperLimit = @UpperLimit,
            LowerLomit = @LowerLomit,
            TargetValue = @TargetValue,
            Unit = @Unit,
            ParameterResult = @ParameterResult
        WHERE Num = @Num",
        new SqlParameter("@Num", (object)model.Num ?? 0),
        new SqlParameter("@SideCellType", (object)model.SideCellType ?? DBNull.Value),
        new SqlParameter("@CellCode", (object)model.CellCode ?? DBNull.Value),
        new SqlParameter("@DetectionArea", (object)model.DetectionArea ?? DBNull.Value),
        new SqlParameter("@DetectionResults", (object)model.DetectionResults ?? DBNull.Value),
        new SqlParameter("@NGtypeNum", (object)model.NGtypeNum ?? 0),
        new SqlParameter("@NGtype1", (object)model.NGtype1 ?? DBNull.Value),
        new SqlParameter("@NGtype2", (object)model.NGtype2 ?? DBNull.Value),
        new SqlParameter("@NGtype3", (object)model.NGtype3 ?? DBNull.Value),
        new SqlParameter("@ParamterCode", (object)model.ParamterCode ?? DBNull.Value),
        new SqlParameter("@ParameterDesc", (object)model.ParameterDesc ?? DBNull.Value),
        new SqlParameter("@Value", (object)model.Value ?? DBNull.Value),
        new SqlParameter("@UpperLimit", (object)model.UpperLimit ?? DBNull.Value),
        new SqlParameter("@LowerLomit", (object)model.LowerLomit ?? DBNull.Value),
        new SqlParameter("@TargetValue", (object)model.TargetValue ?? DBNull.Value),
        new SqlParameter("@Unit", (object)model.Unit ?? DBNull.Value),
        new SqlParameter("@ParameterResult", (object)model.ParameterResult ?? DBNull.Value));
}
```

- [ ] **Step 3: Mapping — 英文列名映射 `MapTable` 追加 8 列**

在 `MapTable` 方法的 `new T_BlueFilmDataMOM { ... }` 块中，`CreateTime` 之后追加：

```csharp
ParamterCode = Str(row, "ParamterCode"),
ParameterDesc = Str(row, "ParameterDesc"),
Value = Str(row, "Value"),
UpperLimit = Str(row, "UpperLimit"),
LowerLomit = Str(row, "LowerLomit"),
TargetValue = Str(row, "TargetValue"),
Unit = Str(row, "Unit"),
ParameterResult = Str(row, "ParameterResult"),
```

- [ ] **Step 4: Mapping — 中文列名映射 `MapFromChineseColumns` 追加 8 列**

在 `MapFromChineseColumns` 方法的 `new T_BlueFilmDataMOM { ... }` 块中，`CreateTime` 之后追加：

```csharp
ParamterCode = Str(row, "工艺参数代码"),
ParameterDesc = Str(row, "参数描述"),
Value = Str(row, "测量值"),
UpperLimit = Str(row, "上限"),
LowerLomit = Str(row, "下限"),
TargetValue = Str(row, "目标值"),
Unit = Str(row, "单位"),
ParameterResult = Str(row, "参数判定结果"),
```

- [ ] **Step 5: Commit**

```bash
git add Service/CRUDServices/BlueFilmDataMOMRepository.cs
git commit -m "feat: add 8 parameter columns to BlueFilmDataMOMRepository CRUD"
```

---

### Task 6: 更新 AutoRun.cs — SP 验证测试覆盖新列

**Files:**
- Modify: `Service/AutoRun.cs:690-701`

- [ ] **Step 1: 在测试 INSERT 中追加新字段**

将第 693-698 行的 `new T_BlueFilmDataMOM { ... }` 替换为：

```csharp
var numMom = repoMOM.Insert(new T_BlueFilmDataMOM
{
    SideCellType = "T", CellCode = codeMOM,
    DetectionArea = "A", DetectionResults = "OK", NGtypeNum = 0,
    CreateTime = DateTime.Now,
    ParamterCode = "TEST_CODE",
    ParameterDesc = "测试缺陷#1-参数A",
    Value = "3.2",
    UpperLimit = "5.0",
    LowerLomit = "0.5",
    TargetValue = "2.0",
    Unit = "mm",
    ParameterResult = "OK"
});
```

- [ ] **Step 2: 追加新列验证断言**

在 `Chk("PROC_GetBlueFilmDataMOM", ...)` 行之后追加：

```csharp
var momResult = repoMOM.GetByCellCode(codeMOM).FirstOrDefault();
Chk("  新列 ParamterCode", momResult != null && momResult.ParamterCode == "TEST_CODE");
Chk("  新列 ParameterDesc", momResult != null && momResult.ParameterDesc == "测试缺陷#1-参数A");
Chk("  新列 Value", momResult != null && momResult.Value == "3.2");
Chk("  新列 Unit", momResult != null && momResult.Unit == "mm");
Chk("  新列 ParameterResult", momResult != null && momResult.ParameterResult == "OK");
```

- [ ] **Step 3: Commit**

```bash
git add Service/AutoRun.cs
git commit -m "feat: extend AutoRun SP verification with new T_BlueFilmDataMOM parameter columns"
```

---

### Task 7: 更新 VerifyProject3 Model

**Files:**
- Modify: `claudecodeworkspaces/独立项目/VerifyProject3/Models/T_BlueFilmDataMOM.cs`

- [ ] **Step 1: 追加 8 个属性**

将文件内容替换为：

```csharp
using System;

namespace VerifyProject.Models
{
    #region T_BlueFilmDataMOM — VisionProgram.dbo.T_BlueFilmDataMOM

    // 来源: INFORMATION_SCHEMA 实际查询
    //   Num              int      PK, is_identity=1
    //   SideCellType     nchar(10)
    //   CellCode         nvarchar(50)
    //   DetectionArea    nchar(10)      (注意: 无 Reinvestment 列)
    //   DetectionResults nchar(10)
    //   NGtypeNum        int
    //   NGtype1          nchar(10)
    //   NGtype2          nchar(10)
    //   NGtype3          nchar(10)
    //   CreateTime       datetime
    //   [新增 2026-06-10] ParamterCode, ParameterDesc, Value,
    //     UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult

    public class T_BlueFilmDataMOM
    {
        public int? Num { get; set; }
        public string SideCellType { get; set; }
        public string CellCode { get; set; }
        public string DetectionArea { get; set; }
        public string DetectionResults { get; set; }
        public int? NGtypeNum { get; set; }
        public string NGtype1 { get; set; }
        public string NGtype2 { get; set; }
        public string NGtype3 { get; set; }
        public DateTime? CreateTime { get; set; }

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

    #endregion
}
```

- [ ] **Step 2: Commit**

```bash
git add claudecodeworkspaces/独立项目/VerifyProject3/Models/T_BlueFilmDataMOM.cs
git commit -m "feat(VerifyProject3): add 8 parameter columns to T_BlueFilmDataMOM model"
```

---

### Task 8: 更新 VerifyProject3 Repository

**Files:**
- Modify: `claudecodeworkspaces/独立项目/VerifyProject3/Repositories/BlueFilmDataMOMRepository.cs`

- [ ] **Step 1: Insert — 追加 8 个存储过程参数**

在 `Insert` 方法的 `@CreateTime` 之后追加：

```csharp
cmd.Parameters.AddWithValue("@ParamterCode", (object)m.ParamterCode ?? DBNull.Value);
cmd.Parameters.AddWithValue("@ParameterDesc", (object)m.ParameterDesc ?? DBNull.Value);
cmd.Parameters.AddWithValue("@Value", (object)m.Value ?? DBNull.Value);
cmd.Parameters.AddWithValue("@UpperLimit", (object)m.UpperLimit ?? DBNull.Value);
cmd.Parameters.AddWithValue("@LowerLomit", (object)m.LowerLomit ?? DBNull.Value);
cmd.Parameters.AddWithValue("@TargetValue", (object)m.TargetValue ?? DBNull.Value);
cmd.Parameters.AddWithValue("@Unit", (object)m.Unit ?? DBNull.Value);
cmd.Parameters.AddWithValue("@ParameterResult", (object)m.ParameterResult ?? DBNull.Value);
```

- [ ] **Step 2: Update — SQL 追加 8 列**

将 `Update` 方法中的 UPDATE SQL 替换为：

```csharp
public int Update(T_BlueFilmDataMOM m)
{
    return ExecNonQuery(@"
        UPDATE T_BlueFilmDataMOM SET
            SideCellType=@SideCellType, CellCode=@CellCode,
            DetectionArea=@DetectionArea, DetectionResults=@DetectionResults,
            NGtypeNum=@NGtypeNum,
            NGtype1=@NGtype1, NGtype2=@NGtype2, NGtype3=@NGtype3,
            ParamterCode=@ParamterCode,
            ParameterDesc=@ParameterDesc,
            Value=@Value,
            UpperLimit=@UpperLimit,
            LowerLomit=@LowerLomit,
            TargetValue=@TargetValue,
            Unit=@Unit,
            ParameterResult=@ParameterResult
        WHERE Num=@Num",
        new SqlParameter("@Num", (object)m.Num ?? 0),
        new SqlParameter("@SideCellType", (object)m.SideCellType ?? DBNull.Value),
        new SqlParameter("@CellCode", (object)m.CellCode ?? DBNull.Value),
        new SqlParameter("@DetectionArea", (object)m.DetectionArea ?? DBNull.Value),
        new SqlParameter("@DetectionResults", (object)m.DetectionResults ?? DBNull.Value),
        new SqlParameter("@NGtypeNum", (object)m.NGtypeNum ?? 0),
        new SqlParameter("@NGtype1", (object)m.NGtype1 ?? DBNull.Value),
        new SqlParameter("@NGtype2", (object)m.NGtype2 ?? DBNull.Value),
        new SqlParameter("@NGtype3", (object)m.NGtype3 ?? DBNull.Value),
        new SqlParameter("@ParamterCode", (object)m.ParamterCode ?? DBNull.Value),
        new SqlParameter("@ParameterDesc", (object)m.ParameterDesc ?? DBNull.Value),
        new SqlParameter("@Value", (object)m.Value ?? DBNull.Value),
        new SqlParameter("@UpperLimit", (object)m.UpperLimit ?? DBNull.Value),
        new SqlParameter("@LowerLomit", (object)m.LowerLomit ?? DBNull.Value),
        new SqlParameter("@TargetValue", (object)m.TargetValue ?? DBNull.Value),
        new SqlParameter("@Unit", (object)m.Unit ?? DBNull.Value),
        new SqlParameter("@ParameterResult", (object)m.ParameterResult ?? DBNull.Value));
}
```

- [ ] **Step 3: MapTable 映射追加 8 列**

在 `MapTable` 方法的 `new T_BlueFilmDataMOM { ... }` 块中，`CreateTime` 之后追加：

```csharp
ParamterCode = Str(row, "ParamterCode"),
ParameterDesc = Str(row, "ParameterDesc"),
Value = Str(row, "Value"),
UpperLimit = Str(row, "UpperLimit"),
LowerLomit = Str(row, "LowerLomit"),
TargetValue = Str(row, "TargetValue"),
Unit = Str(row, "Unit"),
ParameterResult = Str(row, "ParameterResult"),
```

- [ ] **Step 4: Commit**

```bash
git add claudecodeworkspaces/独立项目/VerifyProject3/Repositories/BlueFilmDataMOMRepository.cs
git commit -m "feat(VerifyProject3): add 8 parameter columns to BlueFilmDataMOMRepository CRUD"
```

---

### Task 9: 更新 VerifyProject3 测试用例

**Files:**
- Modify: `claudecodeworkspaces/独立项目/VerifyProject3/Program.cs:105-201`

- [ ] **Step 1: INSERT 测试追加新字段**

将第 115-119 行的 `new T_BlueFilmDataMOM { ... }` 替换为：

```csharp
num = repo.Insert(new T_BlueFilmDataMOM
{
    SideCellType = "SideTest", CellCode = code,
    DetectionArea = "Area1", DetectionResults = "OK",
    NGtypeNum = 0, CreateTime = DateTime.Now,
    ParamterCode = "VERIFY_CODE",
    ParameterDesc = "验证缺陷#1-参数A",
    Value = "3.2",
    UpperLimit = "5.0",
    LowerLomit = "0.5",
    TargetValue = "2.0",
    Unit = "mm",
    ParameterResult = "OK"
});
```

- [ ] **Step 2: INSERT 验证后追加新列断言**

在 `Pass("INSERT (sp) → Num", num != null, $"Num={num}")` 之后追加：

```csharp
// 验证新列
if (num != null)
{
    var inserted = repo.GetByNum(num.Value);
    Pass("  新列 ParamterCode", inserted != null && inserted.ParamterCode == "VERIFY_CODE");
    Pass("  新列 ParameterDesc", inserted != null && inserted.ParameterDesc == "验证缺陷#1-参数A");
    Pass("  新列 Value", inserted != null && inserted.Value == "3.2");
    Pass("  新列 Unit", inserted != null && inserted.Unit == "mm");
    Pass("  新列 ParameterResult", inserted != null && inserted.ParameterResult == "OK");
}
```

- [ ] **Step 3: Update 测试覆盖新列**

在 `Update` 测试块中，找到 `r.DetectionResults = "NG"; r.NGtypeNum = 2;` 行之后，追加新列更新：

```csharp
r.ParamterCode = "UPDATED_CODE";
r.ParameterDesc = "已更新描述";
r.Value = "8.1";
r.UpperLimit = "10.0";
r.LowerLomit = "0.1";
r.TargetValue = "5.0";
r.Unit = "μm";
r.ParameterResult = "NG";
```

并在 Update 验证断言后追加：

```csharp
Pass("  验证 ParamterCode 更新", u != null && u.ParamterCode == "UPDATED_CODE");
Pass("  验证 ParameterResult 更新", u != null && u.ParameterResult == "NG");
```

- [ ] **Step 4: Commit**

```bash
git add claudecodeworkspaces/独立项目/VerifyProject3/Program.cs
git commit -m "test(VerifyProject3): extend T_BlueFilmDataMOM test with new parameter columns"
```

---

### Task 10: 更新 VerifyProject3 CLAUDE.md

**Files:**
- Modify: `claudecodeworkspaces/独立项目/VerifyProject3/CLAUDE.md`

- [ ] **Step 1: 更新表结构说明**

在 CLAUDE.md 中 `T_BlueFilmDataMOM` 的备注行改为：

```
| `T_BlueFilmDataMOM` | `T_BlueFilmDataMOM` | 无 Reinvestment 字段，用 SideCellType 替代 CellType；2026-06-10 新增 8 个参数列 |
```

并在文件末尾的"重要约定"节追加：

```
- `T_BlueFilmDataMOM` 表 2026-06-10 新增 8 个参数列：ParamterCode, ParameterDesc, Value, UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult。旧 NG 列保留兼容但不再主动写入
```

- [ ] **Step 2: Commit**

```bash
git add claudecodeworkspaces/独立项目/VerifyProject3/CLAUDE.md
git commit -m "docs(VerifyProject3): update CLAUDE.md for T_BlueFilmDataMOM new columns"
```

---

### Task 11: 构建验证

- [ ] **Step 1: 验证 VerifyProject3 构建**

```bash
cd claudecodeworkspaces/独立项目/VerifyProject3 && dotnet build
```

预期：Build succeeded，0 errors。

- [ ] **Step 2: 运行 VerifyProject3 验证**

> 确保 SQL Server 可连接后执行：

```bash
cd claudecodeworkspaces/独立项目/VerifyProject3 && dotnet run
```

预期：T_BlueFilmDataMOM 测试全部 PASS，无 FAIL。

- [ ] **Step 3: 验证主项目构建**

```bash
msbuild ZenergyBFSI.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

预期：Build succeeded，0 errors。

- [ ] **Step 4: Commit 构建结果（如有微调）**

```bash
git add -A
git commit -m "chore: final build verification after T_BlueFilmDataMOM redesign"
```
