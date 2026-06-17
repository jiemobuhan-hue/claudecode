using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionProgram.Main.ProjectClass.SqlDB;
using VisionProgram.Models.DModels;
using VisionProgram.Models.UIModels;

/****************************************************************
*   作者：AMR 软件部
*   CLR版本：4.0.30319.42000
*   创建时间：2022/9/19 18:32:23
*   描述说明：CCD Dao类
*
*   修改历史：
*
*
*****************************************************************/
namespace VisionProgram.Main.DAL
{
    public class CCDDataDal : BaseDal
    {
        private const string SelectByPageProcName = "PROC_GetMarkDataByPage";
        private const string SelectByPageProcName1 = "PROC_GetPoleDataByPage";
        private const string SelectProcName = "PROC_GetPolePosition";
        private const string InsertProcName = "Proc_InsertPoleAddress_Mark";
        private const string InsertProcName1 = "Proc_InsertPoleAddress_Pole";


        /// <summary>
        /// 分页查询单据列表
        /// </summary>
        /// <param name="paraModel"></param>
        /// <param name="startIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public DataSet LoadCCDDataByPage(QueryCCDDataModel paraModel, int startIndex, int pageSize)
        {
            DataSet ds = GetPageDs<QueryCCDDataModel>(SqlInfoManager.L_sqlConnection[0], SelectByPageProcName, 2, paraModel, startIndex, pageSize);
            return ds;
        }

        /// <summary>
        /// 分页查询单据列表
        /// </summary>
        /// <param name="paraModel"></param>
        /// <param name="startIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public DataSet LoadCCDPoleDataByPage(QueryCCDDataModel paraModel, int startIndex, int pageSize)
        {
            DataSet ds = GetPageDs<QueryCCDDataModel>(SqlInfoManager.L_sqlConnection[0], SelectByPageProcName1, 2, paraModel, startIndex, pageSize);
            return ds;
        }
        /// <summary>
        /// 查询极柱位置数据
        /// </summary>
        /// <param name="paraModel"></param>
        /// <returns></returns>
        public DataTable LoadCCDData(PoleDataModel paraModel)
        {
            DataTable dt = GetList<PoleDataModel>(SqlInfoManager.L_sqlConnection[0], SelectProcName, 2, paraModel);
            return dt;
        }
        /// <summary>
        /// 查询极柱位置数据
        /// </summary>
        /// <param name="paraModel"></param>
        /// <returns></returns>
        public DataTable LoadCCDData1(PoleData1Model paraModel)
        {
            DataTable dt = GetList<PoleData1Model>(SqlInfoManager.L_sqlConnection[0], SelectProcName, 2, paraModel);
            return dt;
        }

        /// <summary>
        /// 插入
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public int InsertCCDData(CCDDataModel ccdData)
        {
            List<SqlParameter> listParas = SqlHelper.CreateParameters<CCDDataModel>(ccdData);
            return Add(SqlInfoManager.L_sqlConnection[0], InsertProcName, 2, listParas.ToArray());
        }

        /// <summary>
        /// 插入
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public int InsertCCDData1(CCDData1Model ccdData)
        {
            List<SqlParameter> listParas = SqlHelper.CreateParameters<CCDData1Model>(ccdData);
            return Add(SqlInfoManager.L_sqlConnection[0], InsertProcName1, 2, listParas.ToArray());
        }

    }
}
