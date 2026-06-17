using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ZenergyBFSI.Model.Vision;

namespace ZenergyBFSI.Service
{
    #region [TASK-INTEGRATE-001] 蓝膜配方参数 CRUD | 2026-05-15 | AI集成
    // ─────────────────────────────────────────────────────────────────
    // 来源：claudecodeworkspaces/独立项目/CRUDServices/BlueFilmRecipeParametersRepository.cs
    // 数据库：VisionProgram (SQL Server)
    // 依赖：SqlHelper（Service/SqlServerDapperHelper.cs）
    //       存储过程（Data/CreateBlueFilmRecipeParameters.sql）
    // ─────────────────────────────────────────────────────────────────
    public class BlueFilmRecipeParametersRepository
    {
        private readonly string _connectionString;

        private const string InsertProcName = "Proc_InsertBlueFilmRecipeParameters";
        private const string GetAllProcName = "PROC_Claude_GetAllBlueFilmRecipeParameters";
        private const string GetByParameterIDProcName = "PROC_Claude_GetBlueFilmRecipeParametersByParameterID";
        private const string UpdateProcName = "PROC_Claude_UpdateBlueFilmRecipeParameters";
        private const string DeleteProcName = "PROC_Claude_DeleteBlueFilmRecipeParameters";
        private const string GetCountProcName = "PROC_Claude_GetBlueFilmRecipeParametersCount";

        public BlueFilmRecipeParametersRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public int Insert(T_BlueFilmRecipeParameters model)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@ParameterID", model.ParameterID ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@Description", model.Description ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@UpdateTime", model.UpdateTime ?? DateTime.Now),
                new System.Data.SqlClient.SqlParameter("@ACK", model.ACK ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@Enable", model.Enable),
                new System.Data.SqlClient.SqlParameter("@ParameterName", model.ParameterName ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ParameterType", model.ParameterType ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@UpperSpecificationsLimit", model.UpperSpecificationsLimit ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@LowerSpecificationsLimit", model.LowerSpecificationsLimit ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@Unit", model.Unit ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@status", model.status ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField1", model.ReserveField1 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField2", model.ReserveField2 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField3", model.ReserveField3 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField4", model.ReserveField4 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField5", model.ReserveField5 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField6", model.ReserveField6 ?? (object)DBNull.Value)
            };
            return SqlHelper.ExecuteNonQuery(_connectionString, InsertProcName, 2, parameters);
        }

        public async Task<int> InsertAsync(T_BlueFilmRecipeParameters model)
        {
            return await Task.Run(() => Insert(model));
        }

        public List<T_BlueFilmRecipeParameters> GetAll()
        {
            var dt = SqlHelper.GetDataTable(_connectionString, GetAllProcName, 2);
            return MapDataTableToList(dt);
        }

        public async Task<List<T_BlueFilmRecipeParameters>> GetAllAsync()
        {
            return await Task.Run(() => GetAll());
        }

        public T_BlueFilmRecipeParameters GetByParameterID(string parameterID)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@ParameterID", parameterID)
            };
            var dt = SqlHelper.GetDataTable(_connectionString, GetByParameterIDProcName, 2, parameters);
            if (dt.Rows.Count > 0)
                return MapRowToModel(dt.Rows[0]);
            return null;
        }

        public async Task<T_BlueFilmRecipeParameters> GetByParameterIDAsync(string parameterID)
        {
            return await Task.Run(() => GetByParameterID(parameterID));
        }

        public int Update(T_BlueFilmRecipeParameters model)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@ParameterID", model.ParameterID ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@Description", model.Description ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@UpdateTime", model.UpdateTime ?? DateTime.Now),
                new System.Data.SqlClient.SqlParameter("@ACK", model.ACK ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@Enable", model.Enable),
                new System.Data.SqlClient.SqlParameter("@ParameterName", model.ParameterName ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ParameterType", model.ParameterType ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@UpperSpecificationsLimit", model.UpperSpecificationsLimit ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@LowerSpecificationsLimit", model.LowerSpecificationsLimit ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@Unit", model.Unit ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@status", model.status ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField1", model.ReserveField1 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField2", model.ReserveField2 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField3", model.ReserveField3 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField4", model.ReserveField4 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField5", model.ReserveField5 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@ReserveField6", model.ReserveField6 ?? (object)DBNull.Value)
            };
            return SqlHelper.ExecuteNonQuery(_connectionString, UpdateProcName, 2, parameters);
        }

        public async Task<int> UpdateAsync(T_BlueFilmRecipeParameters model)
        {
            return await Task.Run(() => Update(model));
        }

        public int Delete(string parameterID)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@ParameterID", parameterID)
            };
            return SqlHelper.ExecuteNonQuery(_connectionString, DeleteProcName, 2, parameters);
        }

        public async Task<int> DeleteAsync(string parameterID)
        {
            return await Task.Run(() => Delete(parameterID));
        }

        public long GetCount()
        {
            var result = SqlHelper.ExecuteScalar(_connectionString, GetCountProcName, 2);
            return result == null ? 0 : Convert.ToInt64(result);
        }

        public async Task<long> GetCountAsync()
        {
            return await Task.Run(() => GetCount());
        }

        private T_BlueFilmRecipeParameters MapRowToModel(DataRow row)
        {
            return new T_BlueFilmRecipeParameters
            {
                ParameterID = row["ParameterID"] == DBNull.Value ? null : row["ParameterID"].ToString(),
                Description = row["Description"] == DBNull.Value ? null : row["Description"].ToString(),
                UpdateTime = row["UpdateTime"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["UpdateTime"]),
                ACK = row["ACK"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["ACK"]),
                Enable = row["Enable"] == DBNull.Value ? 1 : Convert.ToInt32(row["Enable"]),
                ParameterName = row["ParameterName"] == DBNull.Value ? null : row["ParameterName"].ToString(),
                ParameterType = row["ParameterType"] == DBNull.Value ? null : row["ParameterType"].ToString(),
                UpperSpecificationsLimit = row["UpperSpecificationsLimit"] == DBNull.Value ? null : row["UpperSpecificationsLimit"].ToString(),
                LowerSpecificationsLimit = row["LowerSpecificationsLimit"] == DBNull.Value ? null : row["LowerSpecificationsLimit"].ToString(),
                Unit = row["Unit"] == DBNull.Value ? null : row["Unit"].ToString(),
                status = row["status"] == DBNull.Value ? null : row["status"].ToString(),
                ReserveField1 = row["ReserveField1"] == DBNull.Value ? null : row["ReserveField1"].ToString(),
                ReserveField2 = row["ReserveField2"] == DBNull.Value ? null : row["ReserveField2"].ToString(),
                ReserveField3 = row["ReserveField3"] == DBNull.Value ? null : row["ReserveField3"].ToString(),
                ReserveField4 = row["ReserveField4"] == DBNull.Value ? null : row["ReserveField4"].ToString(),
                ReserveField5 = row["ReserveField5"] == DBNull.Value ? null : row["ReserveField5"].ToString(),
                ReserveField6 = row["ReserveField6"] == DBNull.Value ? null : row["ReserveField6"].ToString()
            };
        }

        private List<T_BlueFilmRecipeParameters> MapDataTableToList(DataTable dt)
        {
            var list = new List<T_BlueFilmRecipeParameters>();
            foreach (DataRow row in dt.Rows)
                list.Add(MapRowToModel(row));
            return list;
        }
    }
    #endregion
}
