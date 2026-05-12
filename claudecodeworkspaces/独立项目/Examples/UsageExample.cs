using System;
using System.Collections.Generic;
using ZenergyBFSI.Workspace.Models;
using ZenergyBFSI.Workspace.CRUDServices;

namespace ZenergyBFSI.Workspace.Examples
{
    /// <summary>
    /// CRUD操作使用示例
    /// 使用Claude前缀存储过程
    /// 参照CCDDataDal.cs代码结构
    /// </summary>
    public class UsageExample
    {
        private const string CONNECTION_STRING = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=VisionProgram;User ID=sa;Password=123456789;TrustServerCertificate=True";

        public static void RunExamples()
        {
            var blueFilmRepo = new BlueFilmDetectionRepository(CONNECTION_STRING);
            var harnessRepo = new HarnessMeasureRepository(CONNECTION_STRING);

            Console.WriteLine("========== 蓝膜检测 CRUD 示例 ==========");

            // 1. Insert
            Console.WriteLine("\n--- 插入记录 ---");
            var blueFilm = new T_BlueFilmDetection
            {
                BottomCellType = "TypeA",
                CellCode = "CELL001",
                DetectionArea = "Area1",
                DetectionResults = "OK",
                NGtypeNum = 0,
                CreateTime = DateTime.Now
            };
            int insertResult = blueFilmRepo.Insert(blueFilm);
            Console.WriteLine($"插入结果: {insertResult} 行受影响");

            // 2. GetByCellCode
            Console.WriteLine("\n--- 根据电芯码查询 ---");
            var cellCodeRecords = blueFilmRepo.GetByCellCode("CELL001");
            Console.WriteLine($"电芯码 CELL001 共有 {cellCodeRecords.Count} 条记录");

            // 3. GetByNum
            Console.WriteLine("\n--- 根据Num查询 ---");
            if (cellCodeRecords.Count > 0 && cellCodeRecords[0].Num.HasValue)
            {
                var singleRecord = blueFilmRepo.GetByNum(cellCodeRecords[0].Num.Value);
                Console.WriteLine($"Num={singleRecord?.Num}, CellCode={singleRecord?.CellCode}");
            }

            // 4. GetAll
            Console.WriteLine("\n--- 查询所有记录 ---");
            var allRecords = blueFilmRepo.GetAll();
            Console.WriteLine($"共有 {allRecords.Count} 条记录");

            // 5. Update
            Console.WriteLine("\n--- 更新记录 ---");
            if (cellCodeRecords.Count > 0 && cellCodeRecords[0].Num.HasValue)
            {
                var recordToUpdate = blueFilmRepo.GetByNum(cellCodeRecords[0].Num.Value);
                if (recordToUpdate != null)
                {
                    recordToUpdate.DetectionResults = "NG";
                    recordToUpdate.NGtypeNum = 1;
                    recordToUpdate.NGtype1 = "气泡";
                    int updateResult = blueFilmRepo.Update(recordToUpdate);
                    Console.WriteLine($"更新结果: {updateResult} 行受影响");
                }
            }

            // 6. GetCount
            Console.WriteLine("\n--- 获取记录总数 ---");
            long count = blueFilmRepo.GetCount();
            Console.WriteLine($"蓝膜检测记录总数: {count}");

            // 7. Delete
            Console.WriteLine("\n--- 删除记录 ---");
            if (cellCodeRecords.Count > 0 && cellCodeRecords[0].Num.HasValue)
            {
                int deleteResult = blueFilmRepo.Delete(cellCodeRecords[0].Num.Value);
                Console.WriteLine($"删除结果: {deleteResult} 行受影响");
            }

            Console.WriteLine("\n========== 线束测量 CRUD 示例 ==========");

            // 1. Insert
            Console.WriteLine("\n--- 插入线束测量记录 ---");
            var harness = new T_HarnessMeasure
            {
                PackCode = "PACK001",
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
            int harnessInsertResult = harnessRepo.Insert(harness);
            Console.WriteLine($"插入结果: {harnessInsertResult} 行受影响");

            // 2. GetByPackCode
            Console.WriteLine("\n--- 根据包装码查询 ---");
            var packCodeRecords = harnessRepo.GetByPackCode("PACK001");
            Console.WriteLine($"包装码 PACK001 共有 {packCodeRecords.Count} 条记录");

            // 3. Update
            Console.WriteLine("\n--- 更新记录 ---");
            if (packCodeRecords.Count > 0 && packCodeRecords[0].Num.HasValue)
            {
                var recordToUpdate = harnessRepo.GetByNum(packCodeRecords[0].Num.Value);
                if (recordToUpdate != null)
                {
                    recordToUpdate.Result = "NG";
                    recordToUpdate.MarkNumber = 200;
                    int updateResult = harnessRepo.Update(recordToUpdate);
                    Console.WriteLine($"更新结果: {updateResult} 行受影响");
                }
            }

            // 4. GetCount
            Console.WriteLine("\n--- 获取记录总数 ---");
            long harnessCount = harnessRepo.GetCount();
            Console.WriteLine($"线束测量记录总数: {harnessCount}");

            // 5. Delete
            Console.WriteLine("\n--- 删除记录 ---");
            if (packCodeRecords.Count > 0 && packCodeRecords[0].Num.HasValue)
            {
                int deleteResult = harnessRepo.Delete(packCodeRecords[0].Num.Value);
                Console.WriteLine($"删除结果: {deleteResult} 行受影响");
            }

            Console.WriteLine("\n========== 异步操作示例 ==========");

            async void RunAsyncExamples()
            {
                var blueFilmRepoAsync = new BlueFilmDetectionRepository(CONNECTION_STRING);

                var newRecord = new T_BlueFilmDetection
                {
                    BottomCellType = "AsyncType",
                    CellCode = "CELL_ASYNC",
                    DetectionArea = "AreaAsync",
                    DetectionResults = "OK",
                    NGtypeNum = 0,
                    CreateTime = DateTime.Now
                };
                int asyncInsertResult = await blueFilmRepoAsync.InsertAsync(newRecord);
                Console.WriteLine($"异步插入结果: {asyncInsertResult} 行受影响");

                var asyncAllRecords = await blueFilmRepoAsync.GetAllAsync();
                Console.WriteLine($"异步查询到 {asyncAllRecords.Count} 条记录");
            }

            RunAsyncExamples();
        }
    }
}