using System.Data.SqlClient;
using System.Text;
using VerifyProject.Models;
using VerifyProject.Repositories;

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

        // [3.5] PROC_GetBlueFilmDataMOM — 之前 COUNT 走 T_BlueFilmSide(bug)，声称已修复
        {
            try
            {
                using var conn = new SqlConnection(CONN); conn.Open();
                using var cmd = new SqlCommand("PROC_GetBlueFilmDataMOM", conn)
                { CommandType = System.Data.CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@pageIndex", 1);
                cmd.Parameters.AddWithValue("@pageSize", int.MaxValue);
                cmd.Parameters.AddWithValue("@startTime", new DateTime(2000, 1, 1));
                cmd.Parameters.AddWithValue("@endTime", new DateTime(2099, 12, 31));
                cmd.Parameters.AddWithValue("@CellCode", code);
                var ds = new System.Data.DataSet();
                using var da = new SqlDataAdapter(cmd);
                da.Fill(ds);
                Pass("PROC_GetBlueFilmDataMOM (sp)", ds.Tables.Count >= 2,
                    $"返回{ds.Tables.Count}个表 (总计/分页)");
            }
            catch (Exception ex)
            {
                Pass($"PROC_GetBlueFilmDataMOM (sp)", false, ex.Message);
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
    try
    {
        using var conn = new SqlConnection(CONN); conn.Open();

        // 1. ALTER TABLE: 追加 8 列（先查 INFORMATION_SCHEMA 避免重复添加报错）
        var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = new SqlCommand(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='T_BlueFilmDataMOM'", conn))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) existingCols.Add(r.GetString(0));

        var newCols = new (string Name, string Type)[]
        {
            ("ParamterCode",   "NVARCHAR(100) NULL"),
            ("ParameterDesc",  "NVARCHAR(200) NULL"),
            ("Value",          "NVARCHAR(50)  NULL"),
            ("UpperLimit",     "NVARCHAR(50)  NULL"),
            ("LowerLomit",     "NVARCHAR(50)  NULL"),
            ("TargetValue",    "NVARCHAR(50)  NULL"),
            ("Unit",           "NVARCHAR(20)  NULL"),
            ("ParameterResult","NVARCHAR(20)  NULL"),
        };

        foreach (var (name, type) in newCols)
        {
            if (existingCols.Contains(name))
            {
                Console.WriteLine($"  [跳过] 列 {name} 已存在");
                continue;
            }
            using var cmd = new SqlCommand(
                $"ALTER TABLE T_BlueFilmDataMOM ADD {name} {type}", conn);
            cmd.ExecuteNonQuery();
            Console.WriteLine($"  [OK] 添加列 {name}");
        }

        // 2. ALTER PROCEDURE: Proc_InsertBlueFilmDataMOM
        using (var cmd = new SqlCommand(@"
IF OBJECT_ID('Proc_InsertBlueFilmDataMOM','P') IS NOT NULL
    EXEC sp_executesql N'
        DROP PROCEDURE Proc_InsertBlueFilmDataMOM;
        EXEC(''CREATE PROCEDURE Proc_InsertBlueFilmDataMOM
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
        END'');
    '", conn))
        {
            cmd.ExecuteNonQuery();
            Console.WriteLine("  [OK] Proc_InsertBlueFilmDataMOM 已更新（新增8参数）");
        }

        // 3. ALTER PROCEDURE: PROC_GetBlueFilmDataMOM — 追加8列中文别名
        using (var cmd = new SqlCommand(@"
IF OBJECT_ID('PROC_GetBlueFilmDataMOM','P') IS NOT NULL
    EXEC sp_executesql N'
        DROP PROCEDURE PROC_GetBlueFilmDataMOM;
        EXEC(''CREATE PROCEDURE PROC_GetBlueFilmDataMOM
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

            ;WITH DataSource AS (
                SELECT
                    ROW_NUMBER() OVER (ORDER BY CreateTime DESC) AS RowNum,
                    SideCellType,
                    CellCode,
                    DetectionArea,
                    DetectionResults,
                    NGtypeNum,
                    NGtype1, NGtype2, NGtype3,
                    CreateTime,
                    ParamterCode, ParameterDesc, Value,
                    UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult
                FROM T_BlueFilmDataMOM
                WHERE (@CellCode = ''''ALL'''' OR CellCode = @CellCode)
                  AND CreateTime >= @startTime
                  AND CreateTime <= @endTime
            )
            SELECT
                (SELECT COUNT(*) FROM T_BlueFilmDataMOM
                 WHERE (@CellCode = ''''ALL'''' OR CellCode = @CellCode)
                   AND CreateTime >= @startTime AND CreateTime <= @endTime) AS TotalCount,

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
            FROM DataSource
            WHERE RowNum BETWEEN @startRow AND @endRow
            ORDER BY RowNum;
        END'');
    '", conn))
        {
            cmd.ExecuteNonQuery();
            Console.WriteLine("  [OK] PROC_GetBlueFilmDataMOM 已更新（追加8列中文别名）");
        }

        Console.WriteLine("  部署完成。\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [部署失败] {ex.Message}");
        Console.WriteLine("  请检查 SQL Server 连接和权限。\n");
    }
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
