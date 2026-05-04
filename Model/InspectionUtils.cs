using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenergyBFSI.Model
{
    public class InspectionUtils
    {
        // ── 看板数据 ──────────────────────────────────────────────────
        public class DashboardData
        {
            public int Total { get; set; }
            public int Ok { get; set; }
            public int Ng { get; set; }
            public double YieldRate { get; set; }   // 0~100

            public List<HourlyData> Hourly { get; set; } = new List<HourlyData>();
            public List<NgTypeData> NgTypes { get; set; } = new List<NgTypeData>();
            public List<RecentRecord> Recent { get; set; } = new List<RecentRecord>();
        }

        public class HourlyData
        {
            public string Hour { get; set; } = "";  // "08:00"
            public int Ok { get; set; }
            public int Ng { get; set; }
        }

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
                YieldRate = total > 0 ? ok * 100.0 / total : 0;
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
