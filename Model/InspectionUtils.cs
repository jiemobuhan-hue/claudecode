using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenergyBFSI.Model
{
    // ── 缺陷复判相关枚举与类 ──────────────────────────────────────

    public enum ReviewStatus
    {
        Unreviewed,
        OK,
        NG
    }

    public class DefectRegion
    {
        public string DefectId { get; set; } = Guid.NewGuid().ToString("N");
        public string DefectType { get; set; } = "";       // 划痕 / 凹坑 / 异物 / 气泡 / 其他
        public double Confidence { get; set; }              // 0.0 ~ 1.0
        public double X { get; set; }                       // 归一化坐标 0.0~1.0
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public ReviewStatus Status { get; set; } = ReviewStatus.Unreviewed;
        public string ReviewUser { get; set; } = "";
        public string ReviewComment { get; set; } = "";
        public DateTime? ReviewTime { get; set; }
        public string DefectImagePath { get; set; } = ""; // 缺陷专用图路径，为空则从原图裁剪
    }

    public class InspectionUtils
    {
        // ── 看板数据 ──────────────────────────────────────────────────
        public class DashboardData
        {
            // ── KPI ──────────────────────────────────────────────────
            public int Total { get; set; }
            public int Ok { get; set; }
            public int Ng { get; set; }
            public double YieldRate { get; set; }

            // ── 图表 ─────────────────────────────────────────────────
            public System.Collections.Generic.List<HourlyData> Hourly { get; set; }
            public System.Collections.Generic.List<NgTypeData> NgTypes { get; set; }
            public System.Collections.Generic.List<RecentRecord> Recent { get; set; }

            // ── 分页（Bug 6 新增）────────────────────────────────────
            /// <summary>
            /// 数据库时间窗口（最近12小时）内的真实总记录数，由 DashboardWorker 的 COUNT(*) 查询返回。
            /// 用于前端计算 _totalPages = ceil(TotalCount / PageSize)。
            /// 注意：Total（KPI）是"当前页已出站记录数"，与 TotalCount（分页用途）含义不同。
            /// </summary>
            public int TotalCount { get; set; }

            /// <summary>
            /// 当前数据对应的页码（0-based），由 DashboardWorker._pageIndex 透传。
            /// 前端收到更新时应将 _currentPage 同步为此值，而非无条件归零。
            /// </summary>
            public int PageIndex { get; set; }
        }

        #region [TASK-REFACTOR-005] HourlyData — 新增"检测中"统计 | 2026-05-15 | AI生成
        public class HourlyData
        {
            public int Hour { get; set; } = 0;     // 小时 (0~23 整数)
            public int Ok { get; set; }             // 出站 OK
            public int Ng { get; set; }             // 出站 NG
            public int Pending { get; set; }        // 进站未完成（检测中）
        }
        #endregion

        public class NgTypeData
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
        }

        public class RecentRecord
        {
            public string CellCode { get; set; } = "";
            public string DateTime { get; set; } = "";
            public string StationId { get; set; } = "";
            public string OverallResult { get; set; } = "";  // "OK"|"NG"
            public string NgTypes { get; set; } = "";  // "SCRATCH|DENT"
            public int ProcessMs { get; set; }
            public bool IsInbound { get; set; }  // true=进站, false=出站
        }

        // ── 复检页数据 ────────────────────────────────────────────────
        public class InspectionRecord
        {
            public string Recordpath { get; set; } = "";
            public string RecordId { get; set; } = "";
            public string CellCode { get; set; } = "";
            public string DateTime { get; set; } = "";
            public string LineId { get; set; } = "";
            public string StationId { get; set; } = "";
            public string OverallResult { get; set; } = "";
            public string NgTypes { get; set; } = "";
            public int ProcessMs { get; set; }
        }

        public class ImageRecord
        {
            public string ImageId { get; set; } = "";
            public string CellCode { get; set; } = "";
            public string StationId { get; set; } = "";
            public string AngleName { get; set; } = "";
            public string ImagePath { get; set; } = "";  // 本地绝对路径
            public string VisionResult { get; set; } = "";  // "OK"|"NG"
            public double VisionScore { get; set; }        // 0.0~1.0
            public string NgType { get; set; } = "";
            /// <summary>格式 "x;y;w;h"，像素坐标，相对于原图</summary>
            public string DefectBbox { get; set; } = "";
            public bool IsManualReviewed { get; set; }
            public string ManualResult { get; set; } = "";
            public string ManualTime { get; set; } = "";
            public string ManualUser { get; set; } = "";
            public string ManualComment { get; set; } = "";

            public List<DefectRegion> Defects { get; set; } = new List<DefectRegion>();

            private static readonly string[] _defaultDefectTypes = { "划痕", "凹坑", "异物" };
            private static readonly Random _rng = new Random();

            /// <summary>生成默认缺陷数据，视觉坐标就绪后替换为数据库查询</summary>
            public static List<DefectRegion> GenerateDefaultDefects()
            {
                var defects = new List<DefectRegion>();
                int count = _rng.Next(1, 4); // 1~3 个缺陷
                for (int i = 0; i < count; i++)
                {
                    defects.Add(new DefectRegion
                    {
                        DefectId = Guid.NewGuid().ToString("N"),
                        DefectType = _defaultDefectTypes[_rng.Next(_defaultDefectTypes.Length)],
                        Confidence = Math.Round(0.7 + _rng.NextDouble() * 0.25, 2),
                        X = Math.Round(0.1 + _rng.NextDouble() * 0.5, 2),   // 0.10~0.60
                        Y = Math.Round(0.1 + _rng.NextDouble() * 0.5, 2),   // 0.10~0.60
                        Width = Math.Round(0.05 + _rng.NextDouble() * 0.15, 2),  // 0.05~0.20
                        Height = Math.Round(0.05 + _rng.NextDouble() * 0.10, 2), // 0.05~0.15
                        Status = ReviewStatus.Unreviewed
                    });
                }
                return defects;
            }
        }

        // ── 事件参数 ──────────────────────────────────────────────────
        public class SearchArgs : EventArgs
        {
            public string CellCode { get; set; } = "";
            public string DateFrom { get; set; } = "";
            public string DateTo { get; set; } = "";
            public string ResultFilter { get; set; } = "ALL";  // "ALL"|"OK"|"NG"
            public int PageIndex { get; set; }
            public int PageSize { get; set; } = 20;
        }

        public class SaveReviewArgs : EventArgs
        {
            public string ImageId { get; set; } = "";
            public string DefectId { get; set; } = "";  // 空字符串=整图复检，非空=缺陷级复检
            public string Result { get; set; } = "";  // "OK"|"NG"
            public string User { get; set; } = "";
            public string Comment { get; set; } = "";
        }

        // ── 看板快照（不可变数据容器）────────────────────────────────
        public sealed class DashboardSnapshot
        {
            public int Total { get; }
            public int Ok { get; }
            public int Ng { get; }
            public double YieldRate { get; }

            public IReadOnlyList<HourlyData> Hourly { get; }
            public IReadOnlyList<NgTypeData> NgTypes { get; }
            public IReadOnlyList<RecentRecord> Recent { get; }

            public int TotalCount { get; }      // 总记录数
            public int PageIndex { get; }       // 当前页（0-based）
            public int PageSize { get; }        // 每页条数
            public int TotalPages { get; }      // 总页数

            public long SequenceNumber { get; } // 序列号（变化检测）

            public DashboardSnapshot(
                int total, int ok, int ng,
                IReadOnlyList<HourlyData> hourly,
                IReadOnlyList<NgTypeData> ngTypes,
                IReadOnlyList<RecentRecord> recent,
                int totalCount, int pageIndex, int pageSize,
                long sequenceNumber)
            {
                Total = total;
                Ok = ok;
                Ng = ng;
                #region [TASK-REFACTOR-001] 良率分母修正 | 2026-05-15 | AI生成
                // 良率 = OK / (OK + NG)，分母仅含已完成检测的出站记录，进站记录不参与良率计算
                YieldRate = (ok + ng) > 0 ? ok * 100.0 / (ok + ng) : 0;
                #endregion
                Hourly = hourly;
                NgTypes = ngTypes;
                Recent = recent;
                TotalCount = totalCount;
                PageIndex = pageIndex;
                PageSize = pageSize;
                TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;
                SequenceNumber = sequenceNumber;
            }

            /// <summary>
            /// 创建默认空快照
            /// </summary>
            public static DashboardSnapshot Empty => new DashboardSnapshot(
                0, 0, 0,
                new List<HourlyData>(),
                new List<NgTypeData>(),
                new List<RecentRecord>(),
                0, 0, 20,
                0);
        }
    }
}
