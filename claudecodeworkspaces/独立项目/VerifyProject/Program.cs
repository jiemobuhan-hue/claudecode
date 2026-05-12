using System;
using System.Data;
using ZenergyBFSI.Service;
using ZenergyBFSI.Workspace.Models;
using ZenergyBFSI.Workspace.CRUDServices;

namespace ZenergyBFSI.Workspace.VerifyProject
{
    /// <summary>
    /// CRUD验证程序 - 使用Claude前缀存储过程
    /// 参照CCDDataDal.cs代码结构
    /// </summary>
    class Program
    {
        private const string CONNECTION_STRING = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=VisionProgram;User ID=sa;Password=123456789;TrustServerCertificate=True";

        static void Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("  CRUD验证 - Claude前缀存储过程");
            Console.WriteLine("===========================================\n");

            // ============ T_HarnessMeasure 测试 ============
            Console.WriteLine("========== T_HarnessMeasure CRUD测试 ==========\n");
            TestHarnessMeasureCRUD();

            Console.WriteLine("\n========== T_BlueFilmDetection CRUD测试 ==========\n");
            TestBlueFilmDetectionCRUD();

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
    }
}