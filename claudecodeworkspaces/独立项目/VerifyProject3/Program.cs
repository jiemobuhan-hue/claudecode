using System.Data;
using System.Data.SqlClient;
using System.Text;
using VerifyProject.Models;
using VerifyProject.Repositories;
using VerifyProject;

Console.OutputEncoding = Encoding.UTF8;

// 本地开发机
const string CONN_LOCAL = "Data Source=DESKTOP-0F9L4KO\\RJ;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";
// 局域网旧数据库 NHDST87
const string CONN_REMOTE = "Data Source=NHDST87;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";

const string CONN = CONN_LOCAL;  // ← 切换服务器改这里
int total = 0, passed = 0;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║  配方 + 检测数据 部署与验证              ║");
Console.WriteLine("╚══════════════════════════════════════════╝");

// ── 1. 部署存储过程 ──
DeployProcedures();

// ── 2. 写入配方 ──
SeedRecipes();

// ── 3. 写入样本检测数据 ──
SeedSampleData();

// ── 4. 验证 ──
VerifyAll();

Console.WriteLine($"\n{'═',40}");
Console.WriteLine($"  {passed}/{total} 通过");
Console.WriteLine(passed == total ? "  全部通过" : $"  {total - passed} 项失败");

#region 部署存储过程

void DeployProcedures()
{
    Console.WriteLine("\n── 部署存储过程 ──");
    using var conn = new SqlConnection(CONN); conn.Open();

    // 清理旧 SP（非 Claude 命名）
    string[] oldSps = { "Proc_InsertBlueFilmDataMOM", "PROC_GetBlueFilmDataMOM", "Proc_InsertBlueFilmDetection", "Proc_InsertBlueFilmRecipeParameters" };
    foreach (var sp in oldSps)
    {
        try { using var cmd = new SqlCommand($"IF OBJECT_ID('{sp}','P') IS NOT NULL DROP PROCEDURE {sp}", conn); cmd.ExecuteNonQuery(); } catch { }
    }

    // 1) PROC_Claude_InsertBlueFilmDataMOM
    TrySp(conn, "PROC_Claude_InsertBlueFilmDataMOM", @"
CREATE PROCEDURE PROC_Claude_InsertBlueFilmDataMOM
    @SideCellType   NVARCHAR(10),
    @CellCode       NVARCHAR(50),
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
    INSERT INTO T_BlueFilmDataMOM (SideCellType, CellCode, CreateTime,
        ParamterCode, ParameterDesc, Value, UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult)
    VALUES (@SideCellType, @CellCode, @CreateTime,
        @ParamterCode, @ParameterDesc, @Value, @UpperLimit, @LowerLomit, @TargetValue, @Unit, @ParameterResult);
END");

    // 2) PROC_Claude_GetBlueFilmDataMOM
    TrySp(conn, "PROC_Claude_GetBlueFilmDataMOM", @"
CREATE PROCEDURE PROC_Claude_GetBlueFilmDataMOM
    @pageIndex INT, @pageSize INT, @startTime DATETIME, @endTime DATETIME, @CellCode NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @startRow INT = (@pageIndex - 1) * @pageSize + 1;
    DECLARE @endRow   INT = @pageIndex * @pageSize;

    SELECT ROW_NUMBER() OVER (ORDER BY CreateTime DESC) AS RowNum,
        SideCellType, CellCode, CreateTime,
        ParamterCode, ParameterDesc, Value, UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult
    INTO #DS
    FROM T_BlueFilmDataMOM
    WHERE (@CellCode = 'ALL' OR CellCode = @CellCode)
      AND CreateTime >= @startTime AND CreateTime <= @endTime;

    SELECT COUNT(*) AS TotalCount FROM #DS;

    SELECT RowNum AS 序号,
        SideCellType AS 电芯类型, CellCode AS 电芯条码, CreateTime AS 创建时间,
        ParamterCode AS 工艺参数代码, ParameterDesc AS 参数描述, Value AS 测量值,
        UpperLimit AS 上限, LowerLomit AS 下限, TargetValue AS 目标值,
        Unit AS 单位, ParameterResult AS 参数判定结果
    FROM #DS
    WHERE RowNum BETWEEN @startRow AND @endRow ORDER BY RowNum;

    DROP TABLE #DS;
END");

    // 3) PROC_Claude_InsertBlueFilmRecipeParameters
    TrySp(conn, "PROC_Claude_InsertBlueFilmRecipeParameters", @"
CREATE PROCEDURE PROC_Claude_InsertBlueFilmRecipeParameters
    @ParameterID    NVARCHAR(100),
    @ParameterName  NVARCHAR(200),
    @Description    NVARCHAR(500),
    @ParameterType  NVARCHAR(50)  = '',
    @UpperSpecLimit NVARCHAR(50)  = '0',
    @LowerSpecLimit NVARCHAR(50)  = '0',
    @Unit           NVARCHAR(20)  = ''
AS
BEGIN
    IF EXISTS (SELECT 1 FROM T_BlueFilmRecipeParameters WHERE ParameterID=@ParameterID)
        UPDATE T_BlueFilmRecipeParameters SET
            ParameterName=@ParameterName, Description=@Description, ParameterType=@ParameterType,
            UpperSpecificationsLimit=@UpperSpecLimit, LowerSpecificationsLimit=@LowerSpecLimit,
            Unit=@Unit, Enable=1, status=N'启用', UpdateTime=GETDATE()
        WHERE ParameterID=@ParameterID;
    ELSE
        INSERT INTO T_BlueFilmRecipeParameters (ParameterID, ParameterName, Description, ParameterType,
            UpperSpecificationsLimit, LowerSpecificationsLimit, Unit, Enable, status, UpdateTime)
        VALUES (@ParameterID, @ParameterName, @Description, @ParameterType,
            @UpperSpecLimit, @LowerSpecLimit, @Unit, 1, N'启用', GETDATE());
END");

    Console.WriteLine("  完成。");
}

void TrySp(SqlConnection conn, string name, string body)
{
    try
    {
        using var drop = new SqlCommand($"IF OBJECT_ID('{name}','P') IS NOT NULL DROP PROCEDURE {name}", conn);
        drop.ExecuteNonQuery();
        using var create = new SqlCommand(body, conn);
        create.ExecuteNonQuery();
        Console.WriteLine($"  [OK] {name}");
    }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] {name}: {ex.Message}"); }
}

#endregion

#region 写入配方 T_BlueFilmRecipeParameters

void SeedRecipes()
{
    Console.WriteLine("\n── 写入配方 T_BlueFilmRecipeParameters ──");
    try
    {
        using var conn = new SqlConnection(CONN); conn.Open();
        int cnt = 0;
        foreach (var p in SeedData.BlueFilmRecipes)
        {
            using var cmd = new SqlCommand("PROC_Claude_InsertBlueFilmRecipeParameters", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ParameterID", p.ParameterID);
            cmd.Parameters.AddWithValue("@ParameterName", p.ParameterName);
            cmd.Parameters.AddWithValue("@Description", p.Description);
            cmd.Parameters.AddWithValue("@ParameterType", p.ParameterType);
            cmd.Parameters.AddWithValue("@UpperSpecLimit", p.UpperLimit);
            cmd.Parameters.AddWithValue("@LowerSpecLimit", p.LowerLimit);
            cmd.Parameters.AddWithValue("@Unit", (object)(string.IsNullOrEmpty(p.Unit) ? DBNull.Value : p.Unit));
            cmd.ExecuteNonQuery();
            cnt++;
        }
        Console.WriteLine($"  [OK] {cnt} 条配方写入");
    }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] {ex.Message}"); }
}

#endregion

#region 写入样本检测数据 T_BlueFilmDataMOM

void SeedSampleData()
{
    Console.WriteLine("\n── 写入样本检测数据 T_BlueFilmDataMOM ──");
    try
    {
        using var conn = new SqlConnection(CONN); conn.Open();
        using var del = new SqlCommand("DELETE FROM T_BlueFilmDataMOM WHERE CellCode LIKE 'SAMPLE%'", conn);
        del.ExecuteNonQuery();

        var repo = new BlueFilmDataMOMRepository(CONN);
        foreach (var d in SeedData.SampleMOMData)
        {
            repo.Insert(new T_BlueFilmDataMOM
            {
                SideCellType = d.SideCellType,
                CellCode = d.CellCode,
                CreateTime = DateTime.Now,
                ParamterCode = d.ParamterCode,
                ParameterDesc = d.ParameterDesc,
                Value = d.Value,
                UpperLimit = d.UpperLimit,
                LowerLomit = d.LowerLimit,
                TargetValue = d.TargetValue,
                Unit = d.Unit,
                ParameterResult = d.ParameterResult
            });
        }
        Console.WriteLine($"  [OK] {SeedData.SampleMOMData.Count} 条样本写入 (OK/NG 混合)");
    }
    catch (Exception ex) { Console.WriteLine($"  [FAIL] {ex.Message}"); }
}

#endregion

#region 验证

void VerifyAll()
{
    Console.WriteLine("\n── 验证 ──");
    VerifyRecipeCount();
    VerifyMOMInsert();
    VerifyMOMQuery();
}

void VerifyRecipeCount()
{
    try
    {
        using var conn = new SqlConnection(CONN); conn.Open();
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM T_BlueFilmRecipeParameters WHERE ParameterID LIKE 'BF-%'", conn);
        long n = (int)cmd.ExecuteScalar();
        Pass("T_BlueFilmRecipeParameters 配方数", n >= 200, $"{n} 条 (期望≥200)");
    }
    catch (Exception ex) { Fail($"配方查询: {ex.Message}"); }
}

void VerifyMOMInsert()
{
    try
    {
        var repo = new BlueFilmDataMOMRepository(CONN);
        var code = $"SAMPLE_VERIFY_{DateTime.Now:HHmmss}";
        var num = repo.Insert(new T_BlueFilmDataMOM
        {
            SideCellType = "A面", CellCode = code,
            CreateTime = DateTime.Now,
            ParamterCode = "BF-QR-001",
            ParameterDesc = "验证-二维码划伤长度",
            Value = "0", UpperLimit = "0", LowerLomit = "0",
            TargetValue = "0", Unit = "", ParameterResult = "OK"
        });
        Pass("INSERT → Num", num != null, $"Num={num}");

        var r = repo.GetByNum(num!.Value);
        Pass("GetByNum", r != null && r.ParamterCode == "BF-QR-001");
        Pass("  新列 ParameterDesc", r != null && r.ParameterDesc == "验证-二维码划伤长度");

        repo.Delete(num.Value);
        Pass("Delete", repo.GetByNum(num.Value) == null);
    }
    catch (Exception ex) { Fail($"MOM CRUD: {ex.Message}"); }
}

void VerifyMOMQuery()
{
    try
    {
        using var conn = new SqlConnection(CONN); conn.Open();
        using var cmd = new SqlCommand("PROC_Claude_GetBlueFilmDataMOM", conn)
        { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@pageIndex", 1);
        cmd.Parameters.AddWithValue("@pageSize", 10);
        cmd.Parameters.AddWithValue("@startTime", new DateTime(2000, 1, 1));
        cmd.Parameters.AddWithValue("@endTime", new DateTime(2099, 12, 31));
        cmd.Parameters.AddWithValue("@CellCode", "SAMPLE_001");
        var ds = new DataSet();
        using var da = new SqlDataAdapter(cmd);
        da.Fill(ds);
        Pass("PROC_Claude_GetBlueFilmDataMOM (2表)", ds.Tables.Count >= 2, $"返回{ds.Tables.Count}个表");
    }
    catch (Exception ex) { Pass($"PROC_Claude_GetBlueFilmDataMOM", false, ex.Message); }
}

#endregion

#region 辅助

void Pass(string op, bool ok, string detail = "")
{
    total++;
    if (ok) passed++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {op}{(string.IsNullOrEmpty(detail) ? "" : $"  ({detail})")}");
}

void Fail(string err) { total++; Console.WriteLine($"  [FAIL] {err}"); }

#endregion
