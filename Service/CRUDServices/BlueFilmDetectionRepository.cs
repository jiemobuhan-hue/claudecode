using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.Service
{
    #region T_BlueFilmDetection CRUD — 存储过程 + 直接 SQL 回退

    // 存储过程 (2个):
    //   Proc_InsertBlueFilmDetection
    //     @CellType nvarchar(10), @CellCode nvarchar(50), @Reinvestment int,
    //     @DetectionArea nvarchar(10), @DetectionResults nvarchar(10),
    //     @NGtypeNum int, @NGtype1 nvarchar(10), @NGtype2 nvarchar(10),
    //     @NGtype3 nvarchar(10), @CreateTime datetime
    //     Num 为 identity, 存储过程不返回 Num, 用 @@IDENTITY 获取
    //
    //   PROC_GetBlueFilmDetection (分页, 返回中文列名, 不含 Num 列)
    //     @pageIndex int, @pageSize int, @startTime datetime,
    //     @endTime datetime, @CellCode nvarchar(50)
    //     返回列: 电芯类型, 电芯条码, 是否复投, 检测区域, 检测结果,
    //             NG类型数量, NG类型1, NG类型2, NG类型3, 创建时间
    //
    // 缺失存储过程 (直接 SQL):
    //   GetByNum / GetAll / GetCount / Update / Delete

    public class BlueFilmDetectionRepository
    {
        private readonly string _connectionString;

        public BlueFilmDetectionRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Insert

        // 返回自增 Num (使用 @@IDENTITY, 因为此库 SCOPE_IDENTITY 异常返回 NULL)
        public int? Insert(T_BlueFilmDetection model)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using (var cmd = new SqlCommand("Proc_InsertBlueFilmDetection", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@CellType", (object)model.CellType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CellCode", (object)model.CellCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Reinvestment", (object)model.Reinvestment ?? 0);
                cmd.Parameters.AddWithValue("@DetectionArea", (object)model.DetectionArea ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DetectionResults", (object)model.DetectionResults ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NGtypeNum", (object)model.NGtypeNum ?? 0);
                cmd.Parameters.AddWithValue("@NGtype1", (object)model.NGtype1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NGtype2", (object)model.NGtype2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NGtype3", (object)model.NGtype3 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreateTime", (object)model.CreateTime ?? DateTime.Now);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new SqlCommand("SELECT CAST(@@IDENTITY AS INT)", conn))
            {
                var v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            }
        }

        public async Task<int?> InsertAsync(T_BlueFilmDetection model)
        {
            return await Task.Run(() => Insert(model));
        }

        #endregion

        #region Query

        // PROC_GetBlueFilmDetection 返回中文列名且不含 Num
        public List<T_BlueFilmDetection> GetByCellCode(string cellCode)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("PROC_GetBlueFilmDetection", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@pageIndex", 1);
            cmd.Parameters.AddWithValue("@pageSize", int.MaxValue);
            cmd.Parameters.AddWithValue("@startTime", new DateTime(2000, 1, 1));
            cmd.Parameters.AddWithValue("@endTime", new DateTime(2099, 12, 31));
            cmd.Parameters.AddWithValue("@CellCode", string.IsNullOrEmpty(cellCode) ? "ALL" : cellCode);

            var ds = new DataSet();
            using var da = new SqlDataAdapter(cmd);
            da.Fill(ds);
            if (ds.Tables.Count < 2) return new List<T_BlueFilmDetection>();

            return MapFromChineseColumns(ds.Tables[1]);
        }

        public async Task<List<T_BlueFilmDetection>> GetByCellCodeAsync(string cellCode)
        {
            return await Task.Run(() => GetByCellCode(cellCode));
        }

        // 直接 SQL — 无对应存储过程
        public List<T_BlueFilmDetection> GetAll()
        {
            var dt = ExecQuery("SELECT * FROM T_BlueFilmDetection ORDER BY Num DESC");
            return MapFromEnglishColumns(dt);
        }

        public async Task<List<T_BlueFilmDetection>> GetAllAsync()
        {
            return await Task.Run(() => GetAll());
        }

        public T_BlueFilmDetection GetByNum(int num)
        {
            var dt = ExecQuery("SELECT * FROM T_BlueFilmDetection WHERE Num = @p0",
                new SqlParameter("@p0", num));
            return dt.Rows.Count > 0 ? MapFromEnglishColumns(dt)[0] : null;
        }

        public async Task<T_BlueFilmDetection> GetByNumAsync(int num)
        {
            return await Task.Run(() => GetByNum(num));
        }

        public long GetCount()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM T_BlueFilmDetection", conn);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public async Task<long> GetCountAsync()
        {
            return await Task.Run(() => GetCount());
        }

        #endregion

        #region Update / Delete — 直接 SQL

        public int Update(T_BlueFilmDetection model)
        {
            return ExecNonQuery(@"
                UPDATE T_BlueFilmDetection SET
                    CellType = @CellType, CellCode = @CellCode,
                    Reinvestment = @Reinvestment, DetectionArea = @DetectionArea,
                    DetectionResults = @DetectionResults, NGtypeNum = @NGtypeNum,
                    NGtype1 = @NGtype1, NGtype2 = @NGtype2, NGtype3 = @NGtype3
                WHERE Num = @Num",
                new SqlParameter("@Num", (object)model.Num ?? 0),
                new SqlParameter("@CellType", (object)model.CellType ?? DBNull.Value),
                new SqlParameter("@CellCode", (object)model.CellCode ?? DBNull.Value),
                new SqlParameter("@Reinvestment", (object)model.Reinvestment ?? 0),
                new SqlParameter("@DetectionArea", (object)model.DetectionArea ?? DBNull.Value),
                new SqlParameter("@DetectionResults", (object)model.DetectionResults ?? DBNull.Value),
                new SqlParameter("@NGtypeNum", (object)model.NGtypeNum ?? 0),
                new SqlParameter("@NGtype1", (object)model.NGtype1 ?? DBNull.Value),
                new SqlParameter("@NGtype2", (object)model.NGtype2 ?? DBNull.Value),
                new SqlParameter("@NGtype3", (object)model.NGtype3 ?? DBNull.Value));
        }

        public async Task<int> UpdateAsync(T_BlueFilmDetection model)
        {
            return await Task.Run(() => Update(model));
        }

        public int Delete(int num)
        {
            return ExecNonQuery("DELETE FROM T_BlueFilmDetection WHERE Num = @p0",
                new SqlParameter("@p0", num));
        }

        public async Task<int> DeleteAsync(int num)
        {
            return await Task.Run(() => Delete(num));
        }

        #endregion

        #region 列映射

        // PROC_GetBlueFilmDetection 返回中文列名
        private List<T_BlueFilmDetection> MapFromChineseColumns(DataTable dt)
        {
            var list = new List<T_BlueFilmDetection>();
            foreach (DataRow row in dt.Rows)
                list.Add(new T_BlueFilmDetection
                {
                    Num = null, // 存储过程不返回 Num
                    CellType = Str(row, "电芯类型"),
                    CellCode = Str(row, "电芯条码"),
                    Reinvestment = Int(row, "是否复投"),
                    DetectionArea = Str(row, "检测区域"),
                    DetectionResults = Str(row, "检测结果"),
                    NGtypeNum = Int(row, "NG类型数量"),
                    NGtype1 = Str(row, "NG类型1"),
                    NGtype2 = Str(row, "NG类型2"),
                    NGtype3 = Str(row, "NG类型3"),
                    CreateTime = Dt(row, "创建时间")
                });
            return list;
        }

        // 直接 SQL (SELECT *) 返回英文列名
        private List<T_BlueFilmDetection> MapFromEnglishColumns(DataTable dt)
        {
            var list = new List<T_BlueFilmDetection>();
            foreach (DataRow row in dt.Rows)
                list.Add(new T_BlueFilmDetection
                {
                    Num = Int(row, "Num"),
                    CellType = Str(row, "CellType"),
                    CellCode = Str(row, "CellCode"),
                    Reinvestment = Int(row, "Reinvestment"),
                    DetectionArea = Str(row, "DetectionArea"),
                    DetectionResults = Str(row, "DetectionResults"),
                    NGtypeNum = Int(row, "NGtypeNum"),
                    NGtype1 = Str(row, "NGtype1"),
                    NGtype2 = Str(row, "NGtype2"),
                    NGtype3 = Str(row, "NGtype3"),
                    CreateTime = Dt(row, "CreateTime")
                });
            return list;
        }

        #endregion

        #region 底层 SQL 执行

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
            using var conn = new SqlConnection(_connectionString); conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var p in ps) cmd.Parameters.Add(p);
            var dt = new DataTable();
            using var da = new SqlDataAdapter(cmd); da.Fill(dt);
            return dt;
        }

        private int ExecNonQuery(string sql, params SqlParameter[] ps)
        {
            using var conn = new SqlConnection(_connectionString); conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var p in ps) cmd.Parameters.Add(p);
            return cmd.ExecuteNonQuery();
        }

        #endregion
    }

    #endregion
}
