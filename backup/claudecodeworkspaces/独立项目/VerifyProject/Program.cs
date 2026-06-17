using System;
using System.Data;
using ZenergyBFSI.Service;
using ZenergyBFSI.Workspace.Models;
using ZenergyBFSI.Workspace.CRUDServices;
using System.Data.SqlClient;

namespace ZenergyBFSI.Workspace.VerifyProject
{
    /// <summary>
    /// CRUD验证程序 - 使用Claude前缀存储过程
    /// 参照CCDDataDal.cs代码结构
    /// </summary>
    class Program
    {
        private const string CONNECTION_STRING = "Data Source=DESKTOP-0F9L4KO\\RJ;Initial Catalog=VisionProgram;User ID=merj;Password=1234@abcD;TrustServerCertificate=True";

        static void Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("  CRUD验证 - Claude前缀存储过程");
            Console.WriteLine("===========================================\n");

            //// ============ T_HarnessMeasure 测试 ============
            //Console.WriteLine("========== T_HarnessMeasure CRUD测试 ==========\n");
            //TestHarnessMeasureCRUD();

            //Console.WriteLine("\n========== T_BlueFilmDetection CRUD测试 ==========\n");
            //TestBlueFilmDetectionCRUD();

            Console.WriteLine("\n========== T_BlueFilmRecipeParameters CRUD测试 ==========\n");
            TestBlueFilmRecipeParametersCRUD();

            Console.WriteLine("\n===========================================");
            Console.WriteLine("  所有测试完成!");
            Console.WriteLine("===========================================");
        }

        static void TestHarnessMeasureCRUD()
        {
            var repo = new HarnessMeasureRepository(CONNECTION_STRING);
            string testPackCode = "CLAUDE_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            int insertedNum = 0;

            // 1. Insert
            Console.WriteLine("[1] Insert - 使用Proc_InsertHarnessMeasure");
            var harness = new T_HarnessMeasure
            {
                PackCode = testPackCode,
                MarkNumber = 100,
                Result = "OK",
                Width1 = 1.1m,
                Width2 = 2.2m,
                Width3 = 3.3m,
                Width4 = 4.4m,
                Width5 = 5.5m,
                Width6 = 6.6m,
                WidthStandard = 100.0m,
                CreateTime = DateTime.Now
            };
            try
            {
                int rows = repo.Insert(harness);
                Console.WriteLine($"    插入结果: {rows} 行受影响");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    插入失败: {ex.Message}");
                return;
            }

            // 2. GetByPackCode
            Console.WriteLine("\n[2] GetByPackCode - 使用PROC_Claude_GetHarnessMeasureByPackCode");
            try
            {
                var records = repo.GetByPackCode(testPackCode);
                if (records.Count > 0)
                {
                    Console.WriteLine($"    查询到 {records.Count} 条记录");
                    Console.WriteLine($"    Num: {records[0].Num}, PackCode: {records[0].PackCode}, Result: {records[0].Result}");
                    insertedNum = records[0].Num.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    查询失败: {ex.Message}");
            }

            // 3. GetByNum
            Console.WriteLine("\n[3] GetByNum - 使用PROC_Claude_GetHarnessMeasureByNum");
            if (insertedNum > 0)
            {
                try
                {
                    var record = repo.GetByNum(insertedNum);
                    if (record != null)
                    {
                        Console.WriteLine($"    Num: {record.Num}, MarkNumber: {record.MarkNumber}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    查询失败: {ex.Message}");
                }
            }

            // 4. Update
            Console.WriteLine("\n[4] Update - 使用PROC_Claude_UpdateHarnessMeasure");
            if (insertedNum > 0)
            {
                try
                {
                    var record = repo.GetByNum(insertedNum);
                    if (record != null)
                    {
                        record.Result = "NG";
                        record.MarkNumber = 200;
                        int updateRows = repo.Update(record);
                        Console.WriteLine($"    更新结果: {updateRows} 行受影响");

                        // 验证更新
                        var updated = repo.GetByNum(insertedNum);
                        if (updated != null)
                        {
                            Console.WriteLine($"    验证: Result={updated.Result}, MarkNumber={updated.MarkNumber}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    更新失败: {ex.Message}");
                }
            }

            // 5. GetCount
            Console.WriteLine("\n[5] GetCount - 使用PROC_Claude_GetHarnessMeasureCount");
            try
            {
                long count = repo.GetCount();
                Console.WriteLine($"    记录总数: {count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    获取总数失败: {ex.Message}");
            }

            // 6. Delete
            Console.WriteLine("\n[6] Delete - 使用PROC_Claude_DeleteHarnessMeasure");
            if (insertedNum > 0)
            {
                try
                {
                    int deleteRows = repo.Delete(insertedNum);
                    Console.WriteLine($"    删除结果: {deleteRows} 行受影响");
                    Console.WriteLine("\n    ✅ T_HarnessMeasure 完整CRUD测试通过!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    删除失败: {ex.Message}");
                }
            }
        }

        static void TestBlueFilmDetectionCRUD()
        {
            var repo = new BlueFilmDetectionRepository(CONNECTION_STRING);
            string testCellCode = "CLAUDE_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            int insertedNum = 0;

            // 1. Insert
            Console.WriteLine("[1] Insert - 使用Proc_InsertBlueFilmDetection");
            var blueFilm = new T_BlueFilmDetection
            {
                BottomCellType = "TestType",
                CellCode = testCellCode,
                DetectionArea = "Area1",
                DetectionResults = "OK",
                NGtypeNum = 0,
                CreateTime = DateTime.Now
            };
            try
            {
                int rows = repo.Insert(blueFilm);
                Console.WriteLine($"    插入结果: {rows} 行受影响");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    插入失败: {ex.Message}");
                return;
            }

            // 2. GetByCellCode
            Console.WriteLine("\n[2] GetByCellCode - 使用PROC_Claude_GetBlueFilmDetectionByCellCode");
            try
            {
                var records = repo.GetByCellCode(testCellCode);
                if (records.Count > 0)
                {
                    Console.WriteLine($"    查询到 {records.Count} 条记录");
                    Console.WriteLine($"    Num: {records[0].Num}, CellCode: {records[0].CellCode}");
                    insertedNum = records[0].Num.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    查询失败: {ex.Message}");
            }

            // 3. GetByNum
            Console.WriteLine("\n[3] GetByNum - 使用PROC_Claude_GetBlueFilmDetectionByNum");
            if (insertedNum > 0)
            {
                try
                {
                    var record = repo.GetByNum(insertedNum);
                    if (record != null)
                    {
                        Console.WriteLine($"    Num: {record.Num}, BottomCellType: {record.BottomCellType}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    查询失败: {ex.Message}");
                }
            }

            // 4. Update
            Console.WriteLine("\n[4] Update - 使用PROC_Claude_UpdateBlueFilmDetection");
            if (insertedNum > 0)
            {
                try
                {
                    var record = repo.GetByNum(insertedNum);
                    if (record != null)
                    {
                        record.DetectionResults = "NG";
                        record.NGtypeNum = 1;
                        record.NGtype1 = "气泡";
                        int updateRows = repo.Update(record);
                        Console.WriteLine($"    更新结果: {updateRows} 行受影响");

                        // 验证更新
                        var updated = repo.GetByNum(insertedNum);
                        if (updated != null)
                        {
                            Console.WriteLine($"    验证: DetectionResults={updated.DetectionResults}, NGtypeNum={updated.NGtypeNum}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    更新失败: {ex.Message}");
                }
            }

            // 5. GetAll
            Console.WriteLine("\n[5] GetAll - 使用PROC_Claude_GetAllBlueFilmDetection");
            try
            {
                var all = repo.GetAll();
                Console.WriteLine($"    共有 {all.Count} 条记录");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    查询失败: {ex.Message}");
            }

            // 6. GetCount
            Console.WriteLine("\n[6] GetCount - 使用PROC_Claude_GetBlueFilmDetectionCount");
            try
            {
                long count = repo.GetCount();
                Console.WriteLine($"    记录总数: {count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    获取总数失败: {ex.Message}");
            }

            // 7. Delete
            Console.WriteLine("\n[7] Delete - 使用PROC_Claude_DeleteBlueFilmDetection");
            if (insertedNum > 0)
            {
                try
                {
                    int deleteRows = repo.Delete(insertedNum);
                    Console.WriteLine($"    删除结果: {deleteRows} 行受影响");
                    Console.WriteLine("\n    ✅ T_BlueFilmDetection 完整CRUD测试通过!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    删除失败: {ex.Message}");
                }
            }
        }
        static void TestBlueFilmRecipeParametersCRUD()
        {
            // 先确保表和存储过程存在
            EnsureTableAndProcsExist();

            var repo = new BlueFilmRecipeParametersRepository(CONNECTION_STRING);
            string testParamID = "PARAM_" + DateTime.Now.ToString("yyyyMMddHHmmss");

            // 1. Insert
            Console.WriteLine("[1] Insert - 使用Proc_InsertBlueFilmRecipeParameters");
            var recipe = new T_BlueFilmRecipeParameters
            {
                ParameterID = testParamID,
                Description = "测试参数",
                ParameterName = "曝光时间",
                ParameterType = "float",
                UpperSpecificationsLimit = "100.0",
                LowerSpecificationsLimit = "10.0",
                Unit = "ms",
                status = "启用",
                Enable = 1,
                UpdateTime = DateTime.Now
            };
            try
            {
                int rows = repo.Insert(recipe);
                Console.WriteLine($"    插入结果: {rows} 行受影响");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    插入失败: {ex.Message}");
                return;
            }

            // 2. GetByParameterID
            Console.WriteLine("\n[2] GetByParameterID - 使用PROC_Claude_GetBlueFilmRecipeParametersByParameterID");
            try
            {
                var record = repo.GetByParameterID(testParamID);
                if (record != null)
                {
                    Console.WriteLine($"    查询成功: '{record.ParameterID}', Name='{record.ParameterName}', Type='{record.ParameterType}'");
                }
                else
                {
                    Console.WriteLine($"    未找到记录 {testParamID}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    查询失败: {ex.Message}");
            }

            // 3. Update
            Console.WriteLine("\n[3] Update - 使用PROC_Claude_UpdateBlueFilmRecipeParameters");
            try
            {
                var record = repo.GetByParameterID(testParamID);
                if (record != null)
                {
                    record.Description = "已更新描述";
                    record.UpperSpecificationsLimit = "200.0";
                    record.status = "禁用";
                    int updateRows = repo.Update(record);
                    Console.WriteLine($"    更新结果: {updateRows} 行受影响");

                    // 验证更新
                    var updated = repo.GetByParameterID(testParamID);
                    if (updated != null)
                    {
                        Console.WriteLine($"    验证: Description='{updated.Description}', USL='{updated.UpperSpecificationsLimit}', status='{updated.status}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    更新失败: {ex.Message}");
            }

            // 4. GetAll
            Console.WriteLine("\n[4] GetAll - 使用PROC_Claude_GetAllBlueFilmRecipeParameters");
            try
            {
                var all = repo.GetAll();
                Console.WriteLine($"    共有 {all.Count} 条记录");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    查询失败: {ex.Message}");
            }

            // 5. GetCount
            Console.WriteLine("\n[5] GetCount - 使用PROC_Claude_GetBlueFilmRecipeParametersCount");
            try
            {
                long count = repo.GetCount();
                Console.WriteLine($"    记录总数: {count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    获取总数失败: {ex.Message}");
            }

            // 6. Delete
            //Console.WriteLine("\n[6] Delete - 使用PROC_Claude_DeleteBlueFilmRecipeParameters");
            //try
            //{
            //    int deleteRows = repo.Delete(testParamID);
            //    Console.WriteLine($"    删除结果: {deleteRows} 行受影响");
            //    Console.WriteLine("\n    ✅ T_BlueFilmRecipeParameters 完整CRUD测试通过!");
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"    删除失败: {ex.Message}");
            //}
        }

        static void EnsureTableAndProcsExist()
        {
            try
            {
                using (var conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // 读取 SQL 脚本文件并批量执行（不支持 GO 分隔符，拆分为多条）
                        string sqlPath = System.IO.Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "..", "..", "..",
                            "CreateBlueFilmRecipeParameters.sql");
                        if (!System.IO.File.Exists(sqlPath))
                        {
                            Console.WriteLine($"    SQL脚本不存在: {sqlPath}，跳过自动建表。");
                            return;
                        }
                        string fullSql = System.IO.File.ReadAllText(sqlPath);
                        // GO 分隔为批次
                        var batches = fullSql.Split(new[] { "\nGO\n", "\r\nGO\r\n", "\nGO\r\n", "\r\nGO\n" },
                            StringSplitOptions.RemoveEmptyEntries);
                        foreach (var batch in batches)
                        {
                            var trimmed = batch.Trim();
                            if (string.IsNullOrEmpty(trimmed)) continue;
                            cmd.CommandText = trimmed;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                Console.WriteLine("    表和存储过程已确认/创建。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    自动建表失败: {ex.Message}");
                Console.WriteLine($"    请手动执行 CreateBlueFilmRecipeParameters.sql");
            }
        }
    }
}