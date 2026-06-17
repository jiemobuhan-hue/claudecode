using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ZenergyBFSI.Service;
using ZenergyBFSI.Workspace.Models;

namespace ZenergyBFSI.Workspace.CRUDServices
{
    /// <summary>
    /// 蓝膜检测CRUD服务类
    /// 参照CCDDataDal.cs代码结构，使用SqlHelper调用Claude前缀存储过程
    /// </summary>
    public class BlueFilmDetectionRepository
    {
        private readonly string _connectionString;

        // Claude前缀存储过程名称常量
        private const string InsertProcName = "Proc_InsertBlueFilmDetection";
        private const string GetAllProcName = "PROC_Claude_GetAllBlueFilmDetection";
        private const string GetByNumProcName = "PROC_Claude_GetBlueFilmDetectionByNum";
        private const string GetByCellCodeProcName = "PROC_Claude_GetBlueFilmDetectionByCellCode";
        private const string UpdateProcName = "PROC_Claude_UpdateBlueFilmDetection";
        private const string DeleteProcName = "PROC_Claude_DeleteBlueFilmDetection";
        private const string GetCountProcName = "PROC_Claude_GetBlueFilmDetectionCount";

        public BlueFilmDetectionRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// 插入蓝膜检测记录
        /// </summary>
        public int Insert(T_BlueFilmDetection model)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@BottomCellType", model.BottomCellType ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@CellCode", model.CellCode ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@DetectionArea", model.DetectionArea ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@DetectionResults", model.DetectionResults ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@NGtypeNum", model.NGtypeNum ?? 0),
                new System.Data.SqlClient.SqlParameter("@NGtype1", model.NGtype1 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@NGtype2", model.NGtype2 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@NGtype3", model.NGtype3 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@CreateTime", model.CreateTime ?? DateTime.Now)
            };
            return SqlHelper.ExecuteNonQuery(_connectionString, InsertProcName, 2, parameters);
        }

        /// <summary>
        /// 异步插入蓝膜检测记录
        /// </summary>
        public async Task<int> InsertAsync(T_BlueFilmDetection model)
        {
            return await Task.Run(() => Insert(model));
        }

        /// <summary>
        /// 查询所有蓝膜检测记录
        /// </summary>
        public List<T_BlueFilmDetection> GetAll()
        {
            var dt = SqlHelper.GetDataTable(_connectionString, GetAllProcName, 2);
            return MapDataTableToList(dt);
        }

        /// <summary>
        /// 异步查询所有蓝膜检测记录
        /// </summary>
        public async Task<List<T_BlueFilmDetection>> GetAllAsync()
        {
            return await Task.Run(() => GetAll());
        }

        /// <summary>
        /// 根据Num查询蓝膜检测记录
        /// </summary>
        public T_BlueFilmDetection GetByNum(int num)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@Num", num)
            };
            var dt = SqlHelper.GetDataTable(_connectionString, GetByNumProcName, 2, parameters);
            if (dt.Rows.Count > 0)
            {
                return MapRowToModel(dt.Rows[0]);
            }
            return null;
        }

        /// <summary>
        /// 异步根据Num查询蓝膜检测记录
        /// </summary>
        public async Task<T_BlueFilmDetection> GetByNumAsync(int num)
        {
            return await Task.Run(() => GetByNum(num));
        }

        /// <summary>
        /// 根据电芯码查询检测记录
        /// </summary>
        public List<T_BlueFilmDetection> GetByCellCode(string cellCode)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@CellCode", cellCode)
            };
            var dt = SqlHelper.GetDataTable(_connectionString, GetByCellCodeProcName, 2, parameters);
            return MapDataTableToList(dt);
        }

        /// <summary>
        /// 异步根据电芯码查询检测记录
        /// </summary>
        public async Task<List<T_BlueFilmDetection>> GetByCellCodeAsync(string cellCode)
        {
            return await Task.Run(() => GetByCellCode(cellCode));
        }

        /// <summary>
        /// 更新蓝膜检测记录
        /// </summary>
        public int Update(T_BlueFilmDetection model)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@Num", model.Num ?? 0),
                new System.Data.SqlClient.SqlParameter("@BottomCellType", model.BottomCellType ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@CellCode", model.CellCode ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@DetectionArea", model.DetectionArea ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@DetectionResults", model.DetectionResults ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@NGtypeNum", model.NGtypeNum ?? 0),
                new System.Data.SqlClient.SqlParameter("@NGtype1", model.NGtype1 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@NGtype2", model.NGtype2 ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@NGtype3", model.NGtype3 ?? (object)DBNull.Value)
            };
            return SqlHelper.ExecuteNonQuery(_connectionString, UpdateProcName, 2, parameters);
        }

        /// <summary>
        /// 异步更新蓝膜检测记录
        /// </summary>
        public async Task<int> UpdateAsync(T_BlueFilmDetection model)
        {
            return await Task.Run(() => Update(model));
        }

        /// <summary>
        /// 删除蓝膜检测记录
        /// </summary>
        public int Delete(int num)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@Num", num)
            };
            return SqlHelper.ExecuteNonQuery(_connectionString, DeleteProcName, 2, parameters);
        }

        /// <summary>
        /// 异步删除蓝膜检测记录
        /// </summary>
        public async Task<int> DeleteAsync(int num)
        {
            return await Task.Run(() => Delete(num));
        }

        /// <summary>
        /// 获取记录总数
        /// </summary>
        public long GetCount()
        {
            var result = SqlHelper.ExecuteScalar(_connectionString, GetCountProcName, 2);
            return result == null ? 0 : Convert.ToInt64(result);
        }

        /// <summary>
        /// 异步获取记录总数
        /// </summary>
        public async Task<long> GetCountAsync()
        {
            return await Task.Run(() => GetCount());
        }

        private T_BlueFilmDetection MapRowToModel(DataRow row)
        {
            return new T_BlueFilmDetection
            {
                Num = row["Num"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["Num"]),
                BottomCellType = row["BottomCellType"] == DBNull.Value ? null : row["BottomCellType"].ToString().Trim(),
                CellCode = row["CellCode"] == DBNull.Value ? null : row["CellCode"].ToString(),
                DetectionArea = row["DetectionArea"] == DBNull.Value ? null : row["DetectionArea"].ToString().Trim(),
                DetectionResults = row["DetectionResults"] == DBNull.Value ? null : row["DetectionResults"].ToString().Trim(),
                NGtypeNum = row["NGtypeNum"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["NGtypeNum"]),
                NGtype1 = row["NGtype1"] == DBNull.Value ? null : row["NGtype1"].ToString().Trim(),
                NGtype2 = row["NGtype2"] == DBNull.Value ? null : row["NGtype2"].ToString().Trim(),
                NGtype3 = row["NGtype3"] == DBNull.Value ? null : row["NGtype3"].ToString().Trim(),
                CreateTime = row["CreateTime"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["CreateTime"])
            };
        }

        private List<T_BlueFilmDetection> MapDataTableToList(DataTable dt)
        {
            var list = new List<T_BlueFilmDetection>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapRowToModel(row));
            }
            return list;
        }
    }
}