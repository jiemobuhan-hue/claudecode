using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ZenergyBFSI.Service;
using ZenergyBFSI.Workspace.Models;

namespace ZenergyBFSI.Workspace.CRUDServices
{
    /// <summary>
    /// 线束测量CRUD服务类
    /// 参照CCDDataDal.cs代码结构，使用SqlHelper调用Claude前缀存储过程
    /// </summary>
    public class HarnessMeasureRepository
    {
        private readonly string _connectionString;

        // Claude前缀存储过程名称常量
        private const string InsertProcName = "Proc_InsertHarnessMeasure";
        private const string GetAllProcName = "PROC_Claude_GetAllHarnessMeasure";
        private const string GetByNumProcName = "PROC_Claude_GetHarnessMeasureByNum";
        private const string GetByPackCodeProcName = "PROC_Claude_GetHarnessMeasureByPackCode";
        private const string UpdateProcName = "PROC_Claude_UpdateHarnessMeasure";
        private const string DeleteProcName = "PROC_Claude_DeleteHarnessMeasure";
        private const string GetCountProcName = "PROC_Claude_GetHarnessMeasureCount";

        public HarnessMeasureRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// 插入线束测量记录
        /// </summary>
        public int Insert(T_HarnessMeasure model)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@PackCode", model.PackCode ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@MarkNumber", model.MarkNumber ?? 0),
                new System.Data.SqlClient.SqlParameter("@Result", model.Result ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@Width1", model.Width1 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width2", model.Width2 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width3", model.Width3 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width4", model.Width4 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width5", model.Width5 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width6", model.Width6 ?? 0),
                new System.Data.SqlClient.SqlParameter("@WidthStandard", model.WidthStandard ?? 0),
                new System.Data.SqlClient.SqlParameter("@CreateTime", model.CreateTime ?? DateTime.Now)
            };
            return SqlHelper.ExecuteNonQuery(_connectionString, InsertProcName, 2, parameters);
        }

        /// <summary>
        /// 异步插入线束测量记录
        /// </summary>
        public async Task<int> InsertAsync(T_HarnessMeasure model)
        {
            return await Task.Run(() => Insert(model));
        }

        /// <summary>
        /// 查询所有线束测量记录
        /// </summary>
        public List<T_HarnessMeasure> GetAll()
        {
            var dt = SqlHelper.GetDataTable(_connectionString, GetAllProcName, 2);
            return MapDataTableToList(dt);
        }

        /// <summary>
        /// 异步查询所有线束测量记录
        /// </summary>
        public async Task<List<T_HarnessMeasure>> GetAllAsync()
        {
            return await Task.Run(() => GetAll());
        }

        /// <summary>
        /// 根据Num查询线束测量记录
        /// </summary>
        public T_HarnessMeasure GetByNum(int num)
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
        /// 异步根据Num查询线束测量记录
        /// </summary>
        public async Task<T_HarnessMeasure> GetByNumAsync(int num)
        {
            return await Task.Run(() => GetByNum(num));
        }

        /// <summary>
        /// 根据包装码查询线束测量记录
        /// </summary>
        public List<T_HarnessMeasure> GetByPackCode(string packCode)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@PackCode", packCode)
            };
            var dt = SqlHelper.GetDataTable(_connectionString, GetByPackCodeProcName, 2, parameters);
            return MapDataTableToList(dt);
        }

        /// <summary>
        /// 异步根据包装码查询线束测量记录
        /// </summary>
        public async Task<List<T_HarnessMeasure>> GetByPackCodeAsync(string packCode)
        {
            return await Task.Run(() => GetByPackCode(packCode));
        }

        /// <summary>
        /// 更新线束测量记录
        /// </summary>
        public int Update(T_HarnessMeasure model)
        {
            var parameters = new System.Data.SqlClient.SqlParameter[]
            {
                new System.Data.SqlClient.SqlParameter("@Num", model.Num ?? 0),
                new System.Data.SqlClient.SqlParameter("@PackCode", model.PackCode ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@MarkNumber", model.MarkNumber ?? 0),
                new System.Data.SqlClient.SqlParameter("@Result", model.Result ?? (object)DBNull.Value),
                new System.Data.SqlClient.SqlParameter("@Width1", model.Width1 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width2", model.Width2 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width3", model.Width3 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width4", model.Width4 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width5", model.Width5 ?? 0),
                new System.Data.SqlClient.SqlParameter("@Width6", model.Width6 ?? 0),
                new System.Data.SqlClient.SqlParameter("@WidthStandard", model.WidthStandard ?? 0)
            };
            return SqlHelper.ExecuteNonQuery(_connectionString, UpdateProcName, 2, parameters);
        }

        /// <summary>
        /// 异步更新线束测量记录
        /// </summary>
        public async Task<int> UpdateAsync(T_HarnessMeasure model)
        {
            return await Task.Run(() => Update(model));
        }

        /// <summary>
        /// 删除线束测量记录
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
        /// 异步删除线束测量记录
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

        private T_HarnessMeasure MapRowToModel(DataRow row)
        {
            return new T_HarnessMeasure
            {
                Num = row["Num"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["Num"]),
                PackCode = row["PackCode"] == DBNull.Value ? null : row["PackCode"].ToString(),
                MarkNumber = row["MarkNumber"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["MarkNumber"]),
                Result = row["Result"] == DBNull.Value ? null : row["Result"].ToString(),
                Width1 = row["Width1"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["Width1"]),
                Width2 = row["Width2"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["Width2"]),
                Width3 = row["Width3"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["Width3"]),
                Width4 = row["Width4"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["Width4"]),
                Width5 = row["Width5"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["Width5"]),
                Width6 = row["Width6"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["Width6"]),
                WidthStandard = row["WidthStandard"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["WidthStandard"]),
                CreateTime = row["CreateTime"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["CreateTime"])
            };
        }

        private List<T_HarnessMeasure> MapDataTableToList(DataTable dt)
        {
            var list = new List<T_HarnessMeasure>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(MapRowToModel(row));
            }
            return list;
        }
    }
}