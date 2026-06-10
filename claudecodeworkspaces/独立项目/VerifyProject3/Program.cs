using System.Data.SqlClient;
using System.Text;
using VerifyProject.Models;
using VerifyProject.Repositories;
using VerifyProject;

Console.OutputEncoding = Encoding.UTF8;

const string CONN = "Data Source=DESKTOP-0F9L4KO\\RJ;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";
var tag = $"VERIFY_{DateTime.Now:yyyyMMddHHmmss}";
int total = 0, passed = 0;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║  VisionProgram 三表 CRUD 验证            ║");
Console.WriteLine("╚══════════════════════════════════════════╝");

// ── 一次性 SQL 部署: DDL + 存储过程 ──
RunSetup();

//Test_BlueFilmDetection();
Test_BlueFilmDataMOM();
//Test_BlueFilmRecipeParameters();

Console.WriteLine($"\n{'═',40}");
Console.WriteLine($"  {passed}/{total} 通过");
if (passed == total) Console.WriteLine("  全部通过");
else Console.WriteLine($"  {total - passed} 项失败");

#region T_BlueFilmDetection

void Test_BlueFilmDetection()
{
    Console.WriteLine("\n── T_BlueFilmDetection ──");
    var repo = new BlueFilmDetectionRepository(CONN);
    var code = $"{tag}_BFD";
    int? num = null;

    try
    {
        // [1] Insert
        num = repo.Insert(new T_BlueFilmDetection
        {
            CellType = "TestType", CellCode = code, Reinvestment = 0,
            DetectionArea = "Area1", DetectionResults = "OK",
            NGtypeNum = 0, CreateTime = DateTime.Now
        });
        Pass("INSERT (sp) → Num", num != null, $"Num={num}");

        // [2] GetByNum
        if (num != null)
        {
            var r = repo.GetByNum(num.Value);
            Pass("GetByNum", r != null && r.CellCode == code);
        }
        else Skip("GetByNum");

        // [3] GetByCellCode (分页sp, 中文列名)
        {
            var list = repo.GetByCellCode(code);
            Pass("GetByCellCode (sp)", list.Count > 0, $"{list.Count}条");
        }

        // [4] GetAll
        {
            var all = repo.GetAll();
            Pass("GetAll", all.Count > 0, $"{all.Count}条");
        }

        // [5] Count
        {
            long n = repo.GetCount();
            Pass("GetCount", n > 0, $"{n}条");
        }

        // [6] Update
        if (num != null)
        {
            var r = repo.GetByNum(num.Value);
            if (r != null)
            {
                r.DetectionResults = "NG"; r.NGtypeNum = 1; r.NGtype1 = "气泡";
                int rows = repo.Update(r);
                Pass("Update", rows > 0);
                var u = repo.GetByNum(num.Value);
                Pass("  验证 Update", u != null && u.DetectionResults == "NG");
            }
        }
        else Skip("Update");

        // [7] Delete
        if (num != null)
        {
            int rows = repo.Delete(num.Value);
            Pass("Delete", rows > 0);
            var d = repo.GetByNum(num.Value);
            Pass("  验证 Delete", d == null);
        }
        else Skip("Delete");
    }
    catch (Exception ex) { Fail(ex.Message); }

    SafeCleanup("T_BlueFilmDetection", "CellCode", code);
}

#endregion

#region T_BlueFilmDataMOM

void Test_BlueFilmDataMOM()
{
    Console.WriteLine("\n── T_BlueFilmDataMOM ──");
    var repo = new BlueFilmDataMOMRepository(CONN);
    var code = $"{tag}_MOM";
    int? num = null;

    try
    {
        // [1] Insert (无 Reinvestment 参数)
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
        Pass("INSERT (sp) → Num", num != null, $"Num={num}");

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

        // [2] GetByNum
        if (num != null)
        {
            var r = repo.GetByNum(num.Value);
            Pass("GetByNum", r != null && r.SideCellType == "SideTest");
        }
        else Skip("GetByNum");

        // [3] GetByCellCode (直接SQL)
        {
            var list = repo.GetByCellCode(code);
            Pass("GetByCellCode", list.Count > 0, $"{list.Count}条");
        }

        // [3.5] PROC_Claude_GetBlueFilmDataMOM
        {
            try
            {
                using var conn = new SqlConnection(CONN); conn.Open();
                using var cmd = new SqlCommand("PROC_Claude_GetBlueFilmDataMOM", conn)
                { CommandType = System.Data.CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@pageIndex", 1);
                cmd.Parameters.AddWithValue("@pageSize", int.MaxValue);
                cmd.Parameters.AddWithValue("@startTime", new DateTime(2000, 1, 1));
                cmd.Parameters.AddWithValue("@endTime", new DateTime(2099, 12, 31));
                cmd.Parameters.AddWithValue("@CellCode", code);
                var ds = new System.Data.DataSet();
                using var da = new SqlDataAdapter(cmd);
                da.Fill(ds);
                Pass("PROC_Claude_GetBlueFilmDataMOM (sp)", ds.Tables.Count >= 2,
                    $"返回{ds.Tables.Count}个表 (总计/分页)");
            }
            catch (Exception ex)
            {
                Pass($"PROC_Claude_GetBlueFilmDataMOM (sp)", false, ex.Message);
            }
        }

        // [4] GetAll
        {
            var all = repo.GetAll();
            Pass("GetAll", all.Count > 0, $"{all.Count}条");
        }

        // [5] Count
        {
            long n = repo.GetCount();
            Pass("GetCount", n > 0, $"{n}条");
        }

        // [6] Update
        if (num != null)
        {
            var r = repo.GetByNum(num.Value);
            if (r != null)
            {
                r.DetectionResults = "NG"; r.NGtypeNum = 2;
                r.NGtype1 = "划伤"; r.NGtype2 = "气泡";
                r.ParamterCode = "UPDATED_CODE";
                r.ParameterDesc = "已更新描述";
                r.Value = "8.1";
                r.UpperLimit = "10.0";
                r.LowerLomit = "0.1";
                r.TargetValue = "5.0";
                r.Unit = "μm";
                r.ParameterResult = "NG";
                int rows = repo.Update(r);
                Pass("Update", rows > 0);
                var u = repo.GetByNum(num.Value);
                Pass("  验证 Update", u != null && u.DetectionResults == "NG");
                Pass("  验证 ParamterCode 更新", u != null && u.ParamterCode == "UPDATED_CODE");
                Pass("  验证 ParameterResult 更新", u != null && u.ParameterResult == "NG");
            }
        }
        else Skip("Update");

        // [7] Delete
        if (num != null)
        {
            int rows = repo.Delete(num.Value);
            Pass("Delete", rows > 0);
            var d = repo.GetByNum(num.Value);
            Pass("  验证 Delete", d == null);
        }
        else Skip("Delete");
    }
    catch (Exception ex) { Fail(ex.Message); }

    SafeCleanup("T_BlueFilmDataMOM", "CellCode", code);
}

#endregion

#region T_BlueFilmRecipeParameters

void Test_BlueFilmRecipeParameters()
{
    Console.WriteLine("\n── T_BlueFilmRecipeParameters ──");
    var repo = new BlueFilmRecipeParametersRepository(CONN);
    var pid = $"{tag}_PARAM";

    try
    {
        // [1] Insert
        int rows = repo.Insert(new T_BlueFilmRecipeParameters
        {
            ParameterID = pid, Description = "验证测试", Enable = 1,
            ParameterName = "测试参数", ParameterType = "float",
            UpperSpecificationsLimit = "100.0", LowerSpecificationsLimit = "10.0",
            Unit = "ms", status = "启用", UpdateTime = DateTime.Now, ACK = 1
        });
        Pass("INSERT (sp)", rows > 0);

        // [2] GetByParameterID
        {
            var r = repo.GetByParameterID(pid);
            Pass("GetByParameterID (sp)", r != null && r.ParameterName == "测试参数");
        }

        // [3] GetAll
        {
            var all = repo.GetAll();
            Pass("GetAll (sp)", all.Count > 0, $"{all.Count}条");
        }

        // [4] GetCount
        {
            long n = repo.GetCount();
            Pass("GetCount (sp)", n > 0, $"{n}条");
        }

        // [5] Update
        {
            var r = repo.GetByParameterID(pid);
            if (r != null)
            {
                r.Description = "已更新"; r.status = "禁用";
                r.ParameterType = "int"; r.UpperSpecificationsLimit = "200.0";
                int urows = repo.Update(r);
                Pass("Update (sp)", urows > 0);
                var u = repo.GetByParameterID(pid);
                Pass("  验证 Update", u != null && u.status == "禁用");
            }
        }

        // [6] Delete
        {
            int drows = repo.Delete(pid);
            Pass("Delete (sp)", drows > 0);
            var d = repo.GetByParameterID(pid);
            Pass("  验证 Delete", d == null);
        }
    }
    catch (Exception ex) { Fail(ex.Message); }

    SafeCleanup("T_BlueFilmRecipeParameters", "ParameterID", pid);
}

#endregion

#region SQL 一次性部署

void RunSetup()
{
    Console.WriteLine("\n── SQL 部署: DDL + 存储过程 ──");
    using var conn = new SqlConnection(CONN); conn.Open();

    // 1. ALTER TABLE: 追加 8 列（幂等）
    Console.WriteLine("  [1/3] 检查列...");
    var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using (var cmd = new SqlCommand(
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM'", conn))
    using (var r = cmd.ExecuteReader())
        while (r.Read()) existingCols.Add(r.GetString(0));

    TryExec(conn, "ALTER TABLE T_BlueFilmDataMOM ADD ParamterCode   NVARCHAR(100) NULL",
        existingCols, "ParamterCode");
    TryExec(conn, "ALTER TABLE T_BlueFilmDataMOM ADD ParameterDesc  NVARCHAR(200) NULL",
        existingCols, "ParameterDesc");
    TryExec(conn, "ALTER TABLE T_BlueFilmDataMOM ADD Value          NVARCHAR(50)  NULL",
        existingCols, "Value");
    TryExec(conn, "ALTER TABLE T_BlueFilmDataMOM ADD UpperLimit     NVARCHAR(50)  NULL",
        existingCols, "UpperLimit");
    TryExec(conn, "ALTER TABLE T_BlueFilmDataMOM ADD LowerLomit     NVARCHAR(50)  NULL",
        existingCols, "LowerLomit");
    TryExec(conn, "ALTER TABLE T_BlueFilmDataMOM ADD TargetValue    NVARCHAR(50)  NULL",
        existingCols, "TargetValue");
    TryExec(conn, "ALTER TABLE T_BlueFilmDataMOM ADD Unit           NVARCHAR(20)  NULL",
        existingCols, "Unit");
    TryExec(conn, "ALTER TABLE T_BlueFilmDataMOM ADD ParameterResult NVARCHAR(20) NULL",
        existingCols, "ParameterResult");

    // 2. 重建 PROC_Claude_InsertBlueFilmDataMOM
    Console.WriteLine("  [2/4] 重建 PROC_Claude_InsertBlueFilmDataMOM...");
    try
    {
        using var check = new SqlCommand(
            "IF OBJECT_ID('PROC_Claude_InsertBlueFilmDataMOM','P') IS NOT NULL DROP PROCEDURE PROC_Claude_InsertBlueFilmDataMOM", conn);
        check.ExecuteNonQuery();

        using var create = new SqlCommand(@"
CREATE PROCEDURE PROC_Claude_InsertBlueFilmDataMOM
    @SideCellType   NVARCHAR(10),
    @CellCode       NVARCHAR(50),
    @DetectionArea  NVARCHAR(10),
    @DetectionResults NVARCHAR(10),
    @NGtypeNum      INT = 0,
    @NGtype1        NVARCHAR(10) = NULL,
    @NGtype2        NVARCHAR(10) = NULL,
    @NGtype3        NVARCHAR(10) = NULL,
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
", conn);
        create.ExecuteNonQuery();
        Console.WriteLine("  [OK] PROC_Claude_InsertBlueFilmDataMOM 已重建");
    }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] {ex.Message}"); }

    // 3. 重建 PROC_Claude_GetBlueFilmDataMOM
    Console.WriteLine("  [3/4] 重建 PROC_Claude_GetBlueFilmDataMOM...");
    try
    {
        using var check = new SqlCommand(
            "IF OBJECT_ID('PROC_Claude_GetBlueFilmDataMOM','P') IS NOT NULL DROP PROCEDURE PROC_Claude_GetBlueFilmDataMOM", conn);
        check.ExecuteNonQuery();

        using var create = new SqlCommand(@"
CREATE PROCEDURE PROC_Claude_GetBlueFilmDataMOM
    @pageIndex INT,
    @pageSize  INT,
    @startTime DATETIME,
    @endTime   DATETIME,
    @CellCode  NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @startRow INT = (@pageIndex - 1) * @pageSize + 1;
    DECLARE @endRow   INT = @pageIndex * @pageSize;

    SELECT
        ROW_NUMBER() OVER (ORDER BY CreateTime DESC) AS RowNum,
        SideCellType, CellCode, DetectionArea, DetectionResults,
        NGtypeNum, NGtype1, NGtype2, NGtype3, CreateTime,
        ParamterCode, ParameterDesc, Value,
        UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult
    INTO #DataSource
    FROM T_BlueFilmDataMOM
    WHERE (@CellCode = 'ALL' OR CellCode = @CellCode)
      AND CreateTime >= @startTime
      AND CreateTime <= @endTime;

    SELECT COUNT(*) AS TotalCount
    FROM T_BlueFilmDataMOM
    WHERE (@CellCode = 'ALL' OR CellCode = @CellCode)
      AND CreateTime >= @startTime AND CreateTime <= @endTime;

    SELECT
        RowNum AS 序号,
        SideCellType AS 电芯类型,
        CellCode AS 电芯条码,
        DetectionArea AS 检测区域,
        DetectionResults AS 检测结果,
        NGtypeNum AS NG类型数量,
        NGtype1 AS NG类型1, NGtype2 AS NG类型2, NGtype3 AS NG类型3,
        CreateTime AS 创建时间,
        ParamterCode AS 工艺参数代码,
        ParameterDesc AS 参数描述,
        Value AS 测量值,
        UpperLimit AS 上限,
        LowerLomit AS 下限,
        TargetValue AS 目标值,
        Unit AS 单位,
        ParameterResult AS 参数判定结果
    FROM #DataSource
    WHERE RowNum BETWEEN @startRow AND @endRow
    ORDER BY RowNum;

    DROP TABLE #DataSource;
END
", conn);
        create.ExecuteNonQuery();
        Console.WriteLine("  [OK] PROC_Claude_GetBlueFilmDataMOM 已重建");
    }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] {ex.Message}"); }

    // 4. 导入配方参数种子数据（通过 C# 代码写入，不依赖 SQL 文件）
    Console.WriteLine("  [4/4] 导入配方参数种子数据...");
    try
    {
        // 先清旧 Claude 数据
        using var del = new SqlCommand(
            "DELETE FROM T_BlueFilmRecipeParameters WHERE ParameterID LIKE N'Claude%'", conn);
        del.ExecuteNonQuery();

        int inserted = 0, skipped = 0;
        foreach (var p in SeedData.BlueFilmParams)
        {
            using var check = new SqlCommand(
                "SELECT COUNT(*) FROM T_BlueFilmRecipeParameters WHERE ParameterID=@p0", conn);
            check.Parameters.AddWithValue("@p0", p.ParameterID);
            if ((int)check.ExecuteScalar() > 0) { skipped++; continue; }

            using var ins = new SqlCommand(@"
                INSERT INTO T_BlueFilmRecipeParameters
                    (ParameterID, Description, Enable, ParameterName, ParameterType,
                     UpperSpecificationsLimit, LowerSpecificationsLimit, Unit, status)
                VALUES
                    (@p0, @p1, 1, @p0, @p2, @p3, @p4, @p5, N'启用')", conn);
            ins.Parameters.AddWithValue("@p0", p.ParameterID);
            ins.Parameters.AddWithValue("@p1", (object)p.Description ?? DBNull.Value);
            ins.Parameters.AddWithValue("@p2", p.ParameterType);
            ins.Parameters.AddWithValue("@p3", p.UpperSpec);
            ins.Parameters.AddWithValue("@p4", p.LowerSpec);
            ins.Parameters.AddWithValue("@p5", (object)(string.IsNullOrEmpty(p.Unit) ? DBNull.Value : p.Unit));
            ins.ExecuteNonQuery();
            inserted++;
        }
        Console.WriteLine($"  [OK] 种子数据: {inserted} 条写入, {skipped} 条跳过");
    }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] 种子数据: {ex.Message}"); }

    Console.WriteLine("  部署完成。\n");
}

void TryExec(SqlConnection conn, string sql, HashSet<string> existing, string colName)
{
    if (existing.Contains(colName))
    {
        Console.WriteLine($"  [跳过] 列 {colName} 已存在");
        return;
    }
    try
    {
        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
        Console.WriteLine($"  [OK] 添加列 {colName}");
    }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] 添加列 {colName}: {ex.Message}"); }
}

#endregion

#region 辅助

void Pass(string op, bool ok, string detail = "")
{
    total++;
    if (ok) passed++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {op}{(string.IsNullOrEmpty(detail) ? "" : $"  ({detail})")}");
}

void Skip(string op) => Console.WriteLine($"  [SKIP] {op}  — Num=null");

void Fail(string err) { total++; Console.WriteLine($"  [FAIL] {err}"); }

void SafeCleanup(string table, string col, string val)
{
    try
    {
        using var conn = new SqlConnection(CONN); conn.Open();
        using var cmd = new SqlCommand($"DELETE FROM [{table}] WHERE [{col}]=@v", conn);
        cmd.Parameters.AddWithValue("@v", val);
        int n = cmd.ExecuteNonQuery();
        if (n > 0) Console.WriteLine($"  [清理] {table}: {n}条");
    }
    catch (Exception ex) { Console.WriteLine($"  [清理] {table}: {ex.Message}"); }
}

#endregion
