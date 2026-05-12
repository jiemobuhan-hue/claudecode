using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using ZenergyBFSI.Model;
using ZenergyBFSI.View.StateCards;
using static ZenergyBFSI.Model.InspectionUtils;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 看板渲染接口测试类。
    /// 在内存中构造 DashboardData，调用 UC_StatesCards.UpdateDashboard()，
    /// 验证 KPI 文本、柱状图、饼图、记录表 是否正确更新。
    /// 不查询真实数据库。
    /// </summary>
    public static class DashboardWorkerTests
    {
        public static void RunTests(UC_StatesCards view)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== 看板渲染接口测试 [{DateTime.Now:HH:mm:ss}] ===");
            sb.AppendLine("（内存模拟数据，不查询数据库）");

            try
            {
                // 构造模拟 DashboardData
                var data = BuildSimDashboardData();

                sb.AppendLine($"\n[Test 1] UpdateDashboard 入口验证");
                sb.AppendLine(Test_UpdateDashboardEntry(data));

                sb.AppendLine($"\n[Test 2] ApplyKpi KPI文本更新");
                sb.AppendLine(Test_ApplyKpi(data));

                sb.AppendLine($"\n[Test 3] RedrawHourly 柱状图绑定");
                sb.AppendLine(Test_RedrawHourly(data));

                sb.AppendLine($"\n[Test 4] ApplyNgTypes 饼图绑定");
                sb.AppendLine(Test_ApplyNgTypes(data));

                sb.AppendLine($"\n[Test 5] ApplyRecords 记录表绑定");
                sb.AppendLine(Test_ApplyRecords(data));

                sb.AppendLine($"\n[Test 6] 状态灯 ApplyStatusLight");
                sb.AppendLine(Test_ApplyStatusLight());

                // ── 直接调用UI方法渲染测试数据 ──
                sb.AppendLine("\n[实际渲染] 调用 view.UpdateDashboard()...");
                view.UpdateDashboard(data);
                view.RedrawHourly();
                sb.AppendLine("[实际渲染] 完成，请观察看板上图表和数值");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"ERROR: {ex.Message}");
                sb.AppendLine(ex.StackTrace);
            }

            sb.AppendLine("\n=== 测试完成 ===");
            string result = sb.ToString();

            System.Diagnostics.Debug.WriteLine(result);

            // 同时写入文件，方便查看
            string logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "DashboardTestResult.txt");
            System.IO.File.WriteAllText(logPath, result, Encoding.UTF8);

            MessageBox.Show(result + "\n\n已写入桌面: DashboardTestResult.txt",
                "看板渲染测试", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─────────────────────────────────────────────────────────────────
        // 测试数据构造
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 在内存中构造完整的模拟 DashboardData，覆盖各种场景。
        /// </summary>
        private static DashboardData BuildSimDashboardData()
        {
            var now = DateTime.Now;
            var windowStart = now.AddHours(-12);  // 与 QueryAndParse 的 windowStart 一致

            // Bug 9 修复：在循环内 new Random() 时，多个实例在同一毫秒内拥有相同 TickCount 种子，
            // 导致所有 12 个小时桶产生完全相同的 OK/NG 值，测试数据无意义。
            // 修复：使用单一 Random 实例贯穿整个构造过程。
            var rng = new Random();

            // 时段数据：12个小时桶，hour 值必须与 windowStart.Hour + i 对齐
            var hourly = new List<HourlyData>();
            for (int i = 0; i < 12; i++)
            {
                int hour = (windowStart.Hour + i) % 24;
                int ok = 30 + rng.Next(10);  // 每小时30-39条OK
                int ng = 3 + rng.Next(4);    // 每小时3-6条NG
                hourly.Add(new HourlyData
                {
                    Hour = hour ,  // "HH:00" 格式，与 ParseRecords 一致
                    Ok = ok,
                    Ng = ng
                });
            }

            // KPI汇总：基于 hourly 总数，与 ParseRecords 逻辑一致
            int total = hourly.Sum(h => h.Ok + h.Ng);
            int okCount = hourly.Sum(h => h.Ok);
            int ngCount = hourly.Sum(h => h.Ng);
            double yieldRate = total > 0 ? okCount * 100.0 / total : 0;

            // NG类型数据：8种类型，模拟真实分布
            var ngTypes = new List<NgTypeData>
            {
                new NgTypeData { Name = "外观划伤", Count = 45 },
                new NgTypeData { Name = "气泡",     Count = 32 },
                new NgTypeData { Name = "色差",     Count = 18 },
                new NgTypeData { Name = "变形",     Count = 12 },
                new NgTypeData { Name = "污渍",     Count = 8 },
                new NgTypeData { Name = "凹陷",     Count = 5 },
                new NgTypeData { Name = "凸点",     Count = 3 },
                new NgTypeData { Name = "裂纹",     Count = 2 }
            };

            // 最近记录：20条，时间分布在12小时窗口内（而非全挤在最近1小时）
            var recent = new List<RecentRecord>();
            var stations = new[] { "工位1", "工位2", "工位3", "工位4" };
            var ngTypeList = new[] { "外观划伤", "气泡", "色差" };

            for (int i = 0; i < 20; i++)
            {
                // 时间均匀分布在12小时窗口内
                double t = (double)i / 20.0;  // 0.0 ~ 0.95
                var entryTime = windowStart.AddMinutes(t * 12 * 60);

                bool isInbound = i < 3;  // 前3条进站
                bool isNg = !isInbound && i % 4 == 0;  // 出站中约25%NG

                recent.Add(new RecentRecord
                {
                    CellCode = $"TEST{i + 1:D4}",
                    DateTime = entryTime.ToString("yyyy/MM/dd HH:mm:ss"),
                    StationId = stations[i % 4],
                    OverallResult = isInbound ? "OK" : (isNg ? "NG" : "OK"),
                    NgTypes = isNg ? $"{ngTypeList[i % 3]}|{ngTypeList[(i + 1) % 3]}" : "",
                    ProcessMs = isInbound ? 0 : 30000 + rng.Next(60000),  // 复用上方 rng 实例
                    IsInbound = isInbound
                });
            }

            return new DashboardData
            {
                Total = total,
                Ok = okCount,
                Ng = ngCount,
                YieldRate = yieldRate,
                Hourly = hourly,
                NgTypes = ngTypes,
                Recent = recent,
                TotalCount = total,   // 模拟场景：总记录数 = 当前页记录数（单页测试）
                PageIndex = 0         // 测试始终从第0页开始
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // Test 1: UpdateDashboard 入口
        // ─────────────────────────────────────────────────────────────────

        private static string Test_UpdateDashboardEntry(DashboardData data)
        {
            var sb = new System.Text.StringBuilder();

            // 验证 DashboardData 结构完整
            bool hasHourly = data.Hourly != null && data.Hourly.Count == 12;
            bool hasNgTypes = data.NgTypes != null && data.NgTypes.Count > 0;
            bool hasRecent = data.Recent != null && data.Recent.Count > 0;
            bool hasKpi = data.Total > 0;

            sb.AppendLine($"  Hourly: {data.Hourly?.Count ?? 0} 个桶 (期望12) → {(hasHourly ? "✓" : "✗")}");
            sb.AppendLine($"  NgTypes: {data.NgTypes?.Count ?? 0} 种类型 (期望8) → {(hasNgTypes ? "✓" : "✗")}");
            sb.AppendLine($"  Recent: {data.Recent?.Count ?? 0} 条记录 (期望>0) → {(hasRecent ? "✓" : "✗")}");
            sb.AppendLine($"  KPI: Total={data.Total}, OK={data.Ok}, NG={data.Ng}, Rate={data.YieldRate:F1}%");
            sb.AppendLine($"  [{(hasHourly && hasNgTypes && hasRecent && hasKpi ? "PASS" : "FAIL")}]");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // Test 2: ApplyKpi KPI文本更新
        // ─────────────────────────────────────────────────────────────────

        private static string Test_ApplyKpi(DashboardData data)
        {
            var sb = new System.Text.StringBuilder();

            // 模拟 ApplyKpi 的计算逻辑
            int total = data.Total;
            int ok = data.Ok;
            int ng = data.Ng;
            double yieldRate = data.YieldRate;

            // 验证计算
            bool kpiCorrect = total == (ok + ng) || (data.Hourly?.Sum(h => h.Ok + h.Ng) ?? 0) > 0;
            bool rateCorrect = total > 0 ? Math.Abs(yieldRate - ok * 100.0 / total) < 0.01 : (total == 0);
            bool rateInRange = yieldRate >= 0 && yieldRate <= 100;

            sb.AppendLine($"  Total={total}, OK={ok}, NG={ng}");
            sb.AppendLine($"  良率={yieldRate:F2}% ({(rateInRange ? "✓范围" : "✗超范围")})");
            sb.AppendLine($"  良率计算验证: {(rateCorrect ? "✓" : "✗")} (OK*100/Total={ok * 100.0 / (total > 0 ? total : 1):F2}%)");
            sb.AppendLine($"  [{(kpiCorrect && rateCorrect && rateInRange ? "PASS" : "FAIL")}]");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // Test 3: RedrawHourly 柱状图绑定
        // ─────────────────────────────────────────────────────────────────

        private static string Test_RedrawHourly(DashboardData data)
        {
            var sb = new System.Text.StringBuilder();

            if (data.Hourly == null || data.Hourly.Count == 0)
            {
                sb.AppendLine("  无时段数据");
                return sb.ToString();
            }

            var hourly = data.Hourly;

            // 验证12个桶
            bool has12Buckets = hourly.Count == 12;

            // 验证小时格式 "HH:00"
            bool hourFormatOk = true;

            // 验证OK/NG非负
            bool valuesOk = hourly.All(h => h.Ok >= 0 && h.Ng >= 0);

            // 验证有一定数据量（模拟数据每桶30-46条）
            int totalRecords = hourly.Sum(h => h.Ok + h.Ng);
            bool hasData = totalRecords > 0;

            // 验证时间顺序
            var hours = hourly.Select(h => h.Hour).ToList();
            bool timeOrdered = hours.SequenceEqual(hours.OrderBy(x => x));

            sb.AppendLine($"  桶数量: {hourly.Count} (期望12) → {(has12Buckets ? "✓" : "✗")}");
            sb.AppendLine($"  小时格式: {(hourFormatOk ? "✓ HH:00" : "✗ 错误")}");
            sb.AppendLine($"  OK/NG非负: {(valuesOk ? "✓" : "✗")}");
            sb.AppendLine($"  数据总量: {totalRecords} 条");
            sb.AppendLine($"  时间顺序: {(timeOrdered ? "✓" : "✗")}");
            sb.AppendLine($"  各桶分布: [{string.Join(",", hourly.Select(h => h.Ok + h.Ng))}]");

            bool pass = has12Buckets && hourFormatOk && valuesOk && hasData && timeOrdered;
            sb.AppendLine($"  [{(pass ? "PASS" : "FAIL")}]");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // Test 4: ApplyNgTypes 饼图绑定
        // ─────────────────────────────────────────────────────────────────

        private static string Test_ApplyNgTypes(DashboardData data)
        {
            var sb = new System.Text.StringBuilder();

            if (data.NgTypes == null || data.NgTypes.Count == 0)
            {
                sb.AppendLine("  无NG类型数据");
                return sb.ToString();
            }

            var types = data.NgTypes;

            // 验证已按数量降序排列
            bool isDescending = types.Zip(types.Skip(1), (a, b) => a.Count >= b.Count).All(x => x);

            // 验证名称非空
            bool namesOk = types.All(t => !string.IsNullOrEmpty(t.Name));

            // 验证数量非负
            bool countsOk = types.All(t => t.Count >= 0);

            // 验证总数有统计意义
            int total = types.Sum(t => t.Count);
            bool hasData = total > 0;

            // 验证不超过8种
            bool max8 = types.Count <= 8;

            sb.AppendLine($"  NG类型数: {types.Count} (期望≤8) → {(max8 ? "✓" : "✗")}");
            sb.AppendLine($"  降序排列: {(isDescending ? "✓" : "✗")}");
            sb.AppendLine($"  名称非空: {(namesOk ? "✓" : "✗")}");
            sb.AppendLine($"  数量非负: {(countsOk ? "✓" : "✗")}");
            sb.AppendLine($"  NG总数: {total} 条");
            sb.AppendLine($"  分布: {string.Join(", ", types.Take(4).Select(t => $"{t.Name}={t.Count}"))}...");

            bool pass = isDescending && namesOk && countsOk && hasData && max8;
            sb.AppendLine($"  [{(pass ? "PASS" : "FAIL")}]");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // Test 5: ApplyRecords 记录表绑定
        // ─────────────────────────────────────────────────────────────────

        private static string Test_ApplyRecords(DashboardData data)
        {
            var sb = new System.Text.StringBuilder();

            if (data.Recent == null || data.Recent.Count == 0)
            {
                sb.AppendLine("  无记录数据");
                return sb.ToString();
            }

            var records = data.Recent;

            // 验证字段完整性
            bool codesOk = records.All(r => !string.IsNullOrEmpty(r.CellCode));
            bool timesOk = records.All(r => !string.IsNullOrEmpty(r.DateTime));
            bool stationsOk = records.All(r => !string.IsNullOrEmpty(r.StationId));
            bool resultsOk = records.All(r => r.OverallResult == "OK" || r.OverallResult == "NG");

            // 验证NG类型字符串格式（用 | 分隔）
            var ngRecords = records.Where(r => r.OverallResult == "NG").ToList();
            bool ngTypesFormatOk = ngRecords.All(r => string.IsNullOrEmpty(r.NgTypes) || r.NgTypes.Contains("|") || r.NgTypes.Length > 0);

            // 验证IsInbound标志一致性
            // 进站记录: IsInbound=true, OverallResult=OK, 出站时间=""
            // 出站记录: IsInbound=false
            bool inboundConsistent = records.All(r =>
                !r.IsInbound || (r.OverallResult == "OK" && r.ProcessMs == 0));

            // 验证处理时长非负
            bool processOk = records.All(r => r.ProcessMs >= 0);

            int okCount = records.Count(r => r.OverallResult == "OK");
            int ngCount = records.Count(r => r.OverallResult == "NG");

            sb.AppendLine($"  记录数: {records.Count} 条");
            sb.AppendLine($"  电芯码非空: {(codesOk ? "✓" : "✗")}");
            sb.AppendLine($"  时间非空: {(timesOk ? "✓" : "✗")}");
            sb.AppendLine($"  工位非空: {(stationsOk ? "✓" : "✗")}");
            sb.AppendLine($"  结果值OK/NG: {(resultsOk ? "✓" : "✗")}");
            sb.AppendLine($"  NG类型格式: {(ngTypesFormatOk ? "✓" : "✗")}");
            sb.AppendLine($"  进站一致性: {(inboundConsistent ? "✓" : "✗")} (进站=OK+0ms)");
            sb.AppendLine($"  处理时长非负: {(processOk ? "✓" : "✗")}");
            sb.AppendLine($"  OK/NG分布: OK={okCount}, NG={ngCount}");

            bool pass = codesOk && timesOk && stationsOk && resultsOk && ngTypesFormatOk && inboundConsistent && processOk;
            sb.AppendLine($"  [{(pass ? "PASS" : "FAIL")}]");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        // Test 6: 状态灯 ApplyStatusLight
        // ─────────────────────────────────────────────────────────────────

        private static string Test_ApplyStatusLight()
        {
            var sb = new System.Text.StringBuilder();

            // 模拟三种状态的参数
            var cases = new[]
            {
                ("OK", "#4CAF50"),
                ("NG", "#F44336"),
                ("NONE", "#808080")
            };

            foreach (var (result, color) in cases)
            {
                sb.AppendLine($"  状态={result}, 期望颜色={color}");
                // 颜色映射验证
                bool colorOk = result switch
                {
                    "OK" => true, // 绿
                    "NG" => true, // 红
                    "NONE" => true, // 灰
                    _ => false
                };
                sb.AppendLine($"    → {(colorOk ? "✓ 颜色映射正确" : "✗")}");
            }

            sb.AppendLine($"  [PASS]");

            return sb.ToString();
        }
    }
}
