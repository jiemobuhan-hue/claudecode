using System.Data;
using System.Data.SqlClient;
using VerifyProject.Models;

namespace VerifyProject.Repositories
{
    #region T_BlueFilmDataMOM CRUD

    // 存储过程 (2个):
    //   PROC_Claude_InsertBlueFilmDataMOM
    //     @SideCellType nvarchar(10), @CellCode nvarchar(50),
    //     @DetectionArea nvarchar(10), @DetectionResults nvarchar(10),
    //     @NGtypeNum int, @NGtype1 nvarchar(10), @NGtype2 nvarchar(10),
    //     @NGtype3 nvarchar(10), @CreateTime datetime
    //     (注意: 无 @Reinvestment 参数!)
    //
    //   PROC_Claude_GetBlueFilmDataMOM (分页, 返回中文列名)
    //     注意: 有 bug — COUNT 走 T_BlueFilmSide (不存在的表)
    //     因此放弃使用此存储过程, 查询全部走直接 SQL
    //
    // 缺失: GetByNum / GetAll / GetByCellCode / GetCount / Update / Delete → 直接 SQL

    public class BlueFilmDataMOMRepository
    {
        private readonly string _conn;
        public BlueFilmDataMOMRepository(string cs) { _conn = cs; }

        #region Insert

        public int? Insert(T_BlueFilmDataMOM m)
        {
            // 注意: 无 @Reinvestment 参数
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using (var cmd = new SqlCommand("PROC_Claude_InsertBlueFilmDataMOM", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@SideCellType", (object)m.SideCellType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CellCode", (object)m.CellCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DetectionArea", (object)m.DetectionArea ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DetectionResults", (object)m.DetectionResults ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NGtypeNum", (object)m.NGtypeNum ?? 0);
                cmd.Parameters.AddWithValue("@NGtype1", (object)m.NGtype1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NGtype2", (object)m.NGtype2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NGtype3", (object)m.NGtype3 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreateTime", (object)m.CreateTime ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@ParamterCode", (object)m.ParamterCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ParameterDesc", (object)m.ParameterDesc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Value", (object)m.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UpperLimit", (object)m.UpperLimit ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LowerLomit", (object)m.LowerLomit ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TargetValue", (object)m.TargetValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Unit", (object)m.Unit ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ParameterResult", (object)m.ParameterResult ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new SqlCommand("SELECT CAST(@@IDENTITY AS INT)", conn))
            {
                var v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            }
        }

        #endregion

        #region Query — 全部直接 SQL (分页存储过程有 bug)

        public List<T_BlueFilmDataMOM> GetAll()
        {
            var dt = ExecQuery("SELECT * FROM T_BlueFilmDataMOM ORDER BY Num DESC");
            return MapTable(dt);
        }

        public T_BlueFilmDataMOM GetByNum(int num)
        {
            var dt = ExecQuery("SELECT * FROM T_BlueFilmDataMOM WHERE Num=@p0",
                new SqlParameter("@p0", num));
            return dt.Rows.Count > 0 ? MapTable(dt)[0] : null;
        }

        public List<T_BlueFilmDataMOM> GetByCellCode(string cellCode)
        {
            var dt = ExecQuery(
                "SELECT * FROM T_BlueFilmDataMOM WHERE CellCode=@p0 ORDER BY CreateTime DESC",
                new SqlParameter("@p0", cellCode));
            return MapTable(dt);
        }

        public long GetCount()
        {
            using var conn = new SqlConnection(_conn); conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM T_BlueFilmDataMOM", conn);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        #endregion

        #region Update / Delete

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

        public int Delete(int num)
        {
            return ExecNonQuery("DELETE FROM T_BlueFilmDataMOM WHERE Num=@p0",
                new SqlParameter("@p0", num));
        }

        #endregion

        #region Mapping

        private List<T_BlueFilmDataMOM> MapTable(DataTable dt)
        {
            var list = new List<T_BlueFilmDataMOM>();
            foreach (DataRow row in dt.Rows)
                list.Add(new T_BlueFilmDataMOM
                {
                    Num = Int(row, "Num"),
                    SideCellType = Str(row, "SideCellType"),
                    CellCode = Str(row, "CellCode"),
                    DetectionArea = Str(row, "DetectionArea"),
                    DetectionResults = Str(row, "DetectionResults"),
                    NGtypeNum = Int(row, "NGtypeNum"),
                    NGtype1 = Str(row, "NGtype1"),
                    NGtype2 = Str(row, "NGtype2"),
                    NGtype3 = Str(row, "NGtype3"),
                    CreateTime = Dt(row, "CreateTime"),
                    ParamterCode = Str(row, "ParamterCode"),
                    ParameterDesc = Str(row, "ParameterDesc"),
                    Value = Str(row, "Value"),
                    UpperLimit = Str(row, "UpperLimit"),
                    LowerLomit = Str(row, "LowerLomit"),
                    TargetValue = Str(row, "TargetValue"),
                    Unit = Str(row, "Unit"),
                    ParameterResult = Str(row, "ParameterResult"),
                });
            return list;
        }

        #endregion

        #region 底层

        private static string Str(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col)) return null;
            var v = row[col]; return v == DBNull.Value ? null : v.ToString().Trim();
        }
        private static int? Int(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col)) return null;
            var v = row[col]; if (v == DBNull.Value) return null;
            try { return Convert.ToInt32(v); } catch { return null; }
        }
        private static DateTime? Dt(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col)) return null;
            var v = row[col]; if (v == DBNull.Value) return null;
            try { return Convert.ToDateTime(v); } catch { return null; }
        }
        private DataTable ExecQuery(string sql, params SqlParameter[] ps)
        {
            using var conn = new SqlConnection(_conn); conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var p in ps) cmd.Parameters.Add(p);
            var dt = new DataTable();
            using var da = new SqlDataAdapter(cmd); da.Fill(dt);
            return dt;
        }
        private int ExecNonQuery(string sql, params SqlParameter[] ps)
        {
            using var conn = new SqlConnection(_conn); conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var p in ps) cmd.Parameters.Add(p);
            return cmd.ExecuteNonQuery();
        }

        #endregion
    }

    #endregion
}
