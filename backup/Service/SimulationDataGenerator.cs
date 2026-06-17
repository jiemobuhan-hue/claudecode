using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.Service
{
    #region [TASK-SIM-001] 看板模拟数据生成器 | 2026-05-15 | AI生成
    // ─────────────────────────────────────────────────────────────────
    // 用途：向 SQLite CellData 表写入仿真的产线检测记录。
    //       数据写入后，由现有管道自动呈现到 UC_StatesCards：
    //         DashboardWorker (5s定时) → DashboardService → Messenger
    //         → UC_StatesCards.OnDashboardUpdateMessage()
    //
    // 调用方式：
    //   await SimulationDataGenerator.GenerateAsync(500);
    //   SimulationDataGenerator.Generate(500);           // 同步（阻塞）
    //
    // 数据特征：
    //   - 记录均匀分布在昨天+今天 48 小时内
    //   - 确保白班(08-20)和晚班(昨天20~今天08)均有完整数据覆盖
    //   - 默认 20% 进站记录（未完成检测）、20% NG 率
    //   - NG 记录随机携带 1~3 种缺陷类型（共8种）
    //   - 电芯码格式：SIM{yyMMddHHmmss}-{序号}，每轮生成唯一
    // ─────────────────────────────────────────────────────────────────
    public static class SimulationDataGenerator
    {
        #region 模拟参数常量 — [TASK-SIM-001]
        private static readonly string[] Stations =
            { "工位1", "工位2", "工位3", "工位4" };

        private static readonly string[] NgTypesPool =
            { "外观划伤", "气泡", "色差", "变形", "污渍", "凹陷", "凸点", "裂纹" };

        #region [TASK-REFACTOR-004] 时间分布修正 | 2026-05-15 | AI生成
        // 覆盖昨天 00:00 ~ 今天 23:59 (48h)，确保 B班 (昨天 20:00 ~ 今天 08:00)
        // 的所有时段都有数据可查。
        private static readonly TimeSpan DataSpan = TimeSpan.FromHours(48);
        #endregion
        private static readonly TimeSpan MinProcess = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxProcess = TimeSpan.FromSeconds(120);
        #endregion

        // ════════════════════════════════════════════════════════════
        //  公开 API
        // ════════════════════════════════════════════════════════════

        #region [TASK-SIM-001] Generate / GenerateAsync — 生成并写入模拟数据

        /// <summary>
        /// 同步生成模拟记录并写入 SQLite。UI 线程调用会阻塞，建议调 <see cref="GenerateAsync"/>。
        /// </summary>
        /// <param name="recordCount">记录总数，默认 500（一页）</param>
        /// <param name="ngRate">出站记录中 NG 占比，默认 0.20</param>
        /// <param name="inboundRate">进站（未完成检测）记录占比，默认 0.20</param>
        public static void Generate(int recordCount = 500, double ngRate = 0.20, double inboundRate = 0.20)
        {
            GenerateAsync(recordCount, ngRate, inboundRate).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步生成模拟记录并写入 SQLite。不阻塞 UI 线程。
        /// </summary>
        public static async Task GenerateAsync(int recordCount = 500, double ngRate = 0.20, double inboundRate = 0.20)
        {
            if (recordCount <= 0) return;

            var list = BuildCellDataList(recordCount, ngRate, inboundRate);

            // 先清理旧模拟数据，确保 BulkUpsert 走 INSERT 路径（电芯码唯一 → 不会误 UPDATE）
            await ClearAsync();

            // 写入 SQLite：以 电芯码 为唯一键 Upsert
            await SQLiteGenericHelper.BulkUpsertAsync(list, keyPropertyName: "电芯码");
        }
        #endregion

        #region [TASK-SIM-001] Clear / ClearAsync — 清理所有模拟数据

        /// <summary>同步删除所有模拟记录（电芯码以 SIM 开头）。</summary>
        public static void Clear()
        {
            SQLiteGenericHelper.ExecuteNonQuery(
                "DELETE FROM CellData WHERE \"电芯码\" LIKE @p0", "SIM%");
        }

        /// <summary>异步删除所有模拟记录。</summary>
        public static Task ClearAsync()
        {
            return SQLiteGenericHelper.ExecuteNonQueryAsync(
                "DELETE FROM CellData WHERE \"电芯码\" LIKE @p0", "SIM%");
        }
        #endregion

        // ════════════════════════════════════════════════════════════
        //  CellData 构造
        // ════════════════════════════════════════════════════════════

        #region [TASK-SIM-002] BuildCellDataList — CellData 记录构造 | 2026-05-15 | AI生成
        // ─────────────────────────────────────────────────────────────────
        // 字段填充规则（对齐 DashboardWorker.QueryAndParse 的判定逻辑）：
        //
        //  isOutbound = 视觉检测参数一~六 任一非空
        //
        //  时间分布（TASK-REFACTOR-004 修正）：
        //    昨天 00:00 ~ 今天 23:59 均匀分布（48h），B班窗口（昨天20~今天08）全覆盖。
        //
        //  进站记录 (inboundRate)：
        //    视觉参数留空 → isOutbound=false → 计入 KPI Total
        //
        //  出站记录 (1 - inboundRate)：
        //    视觉检测参数一 = "OK"/"NG" → 触发 isOutbound
        //    OK：出站结果="OK"，无 NG 类型
        //    NG：出站结果="NG"，随机 1~3 种缺陷
        // ─────────────────────────────────────────────────────────────────
        private static List<CellData> BuildCellDataList(int recordCount, double ngRate, double inboundRate)
        {
            var list = new List<CellData>(recordCount);
            var rng = new Random();
            var now = DateTime.Now;
            var runTag = $"{now:yyMMddHHmmss}"; // 本轮生成的唯一标识

            // 昨天 00:00:00 作为起点，记录在 48h 内均匀分布（昨天+今天全覆盖）
            var yesterdayStart = now.Date.AddDays(-1);
            for (int i = 0; i < recordCount; i++)
            {
                double progress = (double)i / recordCount;
                var entryTime = yesterdayStart.AddSeconds(DataSpan.TotalSeconds * progress);

                bool isInbound = rng.NextDouble() < inboundRate;
                bool isNg = !isInbound && rng.NextDouble() < ngRate;

                var cell = new CellData
                {
                    电芯码 = $"SIM{runTag}-{i:D5}",
                    进站时间 = entryTime.ToString("yyyy/MM/dd HH:mm:ss"),
                    检验位置 = Stations[rng.Next(Stations.Length)],
                };

                if (isInbound)
                {
                    // 进站：视觉参数全部留空，isOutbound 判定为 false
                    cell.是否复投 = "否";
                }
                else
                {
                    // 出站：至少填充一个视觉参数以触发 isOutbound
                    cell.视觉检测参数一 = isNg ? "NG" : "OK";
                    cell.视觉检测参数二 = "检测B";
                    cell.视觉检测参数三 = "检测C";
                    cell.出站结果 = isNg ? "NG" : "OK";
                    cell.出站时间 = entryTime
                        .AddSeconds(MinProcess.TotalSeconds + rng.NextDouble() * (MaxProcess.TotalSeconds - MinProcess.TotalSeconds))
                        .ToString("yyyy/MM/dd HH:mm:ss");

                    if (isNg)
                    {
                        // 随机 1~3 种缺陷，不重复
                        int ngCount = 1 + rng.Next(3);
                        cell.Ng类型数量 = ngCount;

                        var assigned = new HashSet<string>();
                        for (int n = 0; n < ngCount; n++)
                        {
                            string t;
                            int attempts = 0;
                            do { t = NgTypesPool[rng.Next(NgTypesPool.Length)]; attempts++; }
                            while (!assigned.Add(t) && attempts < 20);
                            SetNgType(cell, n, t);
                        }
                    }
                }

                list.Add(cell);
            }

            return list;
        }

        /// <summary>按索引设置 Ng类型1~8 字段。</summary>
        private static void SetNgType(CellData cell, int index, string value)
        {
            switch (index)
            {
                case 0: cell.Ng类型1 = value; break;
                case 1: cell.Ng类型2 = value; break;
                case 2: cell.Ng类型3 = value; break;
                case 3: cell.Ng类型4 = value; break;
                case 4: cell.Ng类型5 = value; break;
                case 5: cell.Ng类型6 = value; break;
                case 6: cell.Ng类型7 = value; break;
                case 7: cell.Ng类型8 = value; break;
            }
        }
        #endregion
    }
    #endregion
}
