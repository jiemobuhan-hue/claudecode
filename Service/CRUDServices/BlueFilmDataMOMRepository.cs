using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using ZenergyBFSI.Model.Vision;

namespace ZenergyBFSI.Service.CRUDServices
{
    #region T_BlueFilmDataMOM CRUD — PROC_Claude_InsertBlueFilmDataMOM + 直接 SQL 回退

    public class BlueFilmDataMOMRepository
    {
        private readonly string _connectionString;

        public BlueFilmDataMOMRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Insert

        public int? Insert(T_BlueFilmDataMOM model)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using (var cmd = new SqlCommand("PROC_Claude_InsertBlueFilmDataMOM", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@SideCellType", (object)model.SideCellType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CellCode", (object)model.CellCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreateTime", (object)model.CreateTime ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@ParamterCode", (object)model.ParamterCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ParameterDesc", (object)model.ParameterDesc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Value", (object)model.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UpperLimit", (object)model.UpperLimit ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LowerLomit", (object)model.LowerLomit ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TargetValue", (object)model.TargetValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Unit", (object)model.Unit ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ParameterResult", (object)model.ParameterResult ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new SqlCommand("SELECT CAST(@@IDENTITY AS INT)", conn))
            {
                var v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
            }
        }

        public async Task<int?> InsertAsync(T_BlueFilmDataMOM model)
        {
            return await Task.Run(() => Insert(model));
        }

        #endregion

        #region Query

        public List<T_BlueFilmDataMOM> GetAll()
        {
            var dt = ExecQuery("SELECT * FROM T_BlueFilmDataMOM ORDER BY Num DESC");
            return MapTable(dt);
        }

        public async Task<List<T_BlueFilmDataMOM>> GetAllAsync()
        {
            return await Task.Run(() => GetAll());
        }

        public T_BlueFilmDataMOM GetByNum(int num)
        {
            var dt = ExecQuery("SELECT * FROM T_BlueFilmDataMOM WHERE Num = @p0",
                new SqlParameter("@p0", num));
            return dt.Rows.Count > 0 ? MapTable(dt)[0] : null;
        }

        public async Task<T_BlueFilmDataMOM> GetByNumAsync(int num)
        {
            return await Task.Run(() => GetByNum(num));
        }

        public List<T_BlueFilmDataMOM> GetByCellCode(string cellCode)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("PROC_Claude_GetBlueFilmDataMOM", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@pageIndex", 1);
            cmd.Parameters.AddWithValue("@pageSize", int.MaxValue);
            cmd.Parameters.AddWithValue("@startTime", new DateTime(2000, 1, 1));
            cmd.Parameters.AddWithValue("@endTime", new DateTime(2099, 12, 31));
            cmd.Parameters.AddWithValue("@CellCode", string.IsNullOrEmpty(cellCode) ? "ALL" : cellCode);

            var ds = new DataSet();
            using var da = new SqlDataAdapter(cmd);
            da.Fill(ds);
            if (ds.Tables.Count < 2) return new List<T_BlueFilmDataMOM>();

            return MapFromChineseColumns(ds.Tables[1]);
        }

        public async Task<List<T_BlueFilmDataMOM>> GetByCellCodeAsync(string cellCode)
        {
            return await Task.Run(() => GetByCellCode(cellCode));
        }

        public long GetCount()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM T_BlueFilmDataMOM", conn);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        public async Task<long> GetCountAsync()
        {
            return await Task.Run(() => GetCount());
        }

        #endregion

        #region Update / Delete

        public int Update(T_BlueFilmDataMOM model)
        {
            return ExecNonQuery(@"
                UPDATE T_BlueFilmDataMOM SET
                    SideCellType = @SideCellType, CellCode = @CellCode,
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
                new SqlParameter("@ParamterCode", (object)model.ParamterCode ?? DBNull.Value),
                new SqlParameter("@ParameterDesc", (object)model.ParameterDesc ?? DBNull.Value),
                new SqlParameter("@Value", (object)model.Value ?? DBNull.Value),
                new SqlParameter("@UpperLimit", (object)model.UpperLimit ?? DBNull.Value),
                new SqlParameter("@LowerLomit", (object)model.LowerLomit ?? DBNull.Value),
                new SqlParameter("@TargetValue", (object)model.TargetValue ?? DBNull.Value),
                new SqlParameter("@Unit", (object)model.Unit ?? DBNull.Value),
                new SqlParameter("@ParameterResult", (object)model.ParameterResult ?? DBNull.Value));
        }

        public async Task<int> UpdateAsync(T_BlueFilmDataMOM model)
        {
            return await Task.Run(() => Update(model));
        }

        public int Delete(int num)
        {
            return ExecNonQuery("DELETE FROM T_BlueFilmDataMOM WHERE Num = @p0",
                new SqlParameter("@p0", num));
        }

        public async Task<int> DeleteAsync(int num)
        {
            return await Task.Run(() => Delete(num));
        }

        #endregion

        #region Mapping

        private List<T_BlueFilmDataMOM> MapFromChineseColumns(DataTable dt)
        {
            var list = new List<T_BlueFilmDataMOM>();
            foreach (DataRow row in dt.Rows)
                list.Add(new T_BlueFilmDataMOM
                {
                    Num = null,
                    SideCellType = Str(row, "电芯类型"),
                    CellCode = Str(row, "电芯条码"),
                    CreateTime = Dt(row, "创建时间"),
                    ParamterCode = Str(row, "工艺参数代码"),
                    ParameterDesc = Str(row, "参数描述"),
                    Value = Str(row, "测量值"),
                    UpperLimit = Str(row, "上限"),
                    LowerLomit = Str(row, "下限"),
                    TargetValue = Str(row, "目标值"),
                    Unit = Str(row, "单位"),
                    ParameterResult = Str(row, "参数判定结果"),
                });
            return list;
        }

        private List<T_BlueFilmDataMOM> MapTable(DataTable dt)
        {
            var list = new List<T_BlueFilmDataMOM>();
            foreach (DataRow row in dt.Rows)
                list.Add(new T_BlueFilmDataMOM
                {
                    Num = Int(row, "Num"),
                    SideCellType = Str(row, "SideCellType"),
                    CellCode = Str(row, "CellCode"),
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
