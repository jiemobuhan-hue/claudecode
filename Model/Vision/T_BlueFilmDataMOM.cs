using System;

namespace ZenergyBFSI.Model.Vision
{
    /// <summary>
    /// 蓝膜MOM数据实体类 (对应数据库表 T_BlueFilmDataMOM)
    /// 保留旧 NG 列兼容，新增 8 个参数列
    /// </summary>
    public class T_BlueFilmDataMOM
    {
        // ── 保留字段 ──
        public int? Num { get; set; }
        public string SideCellType { get; set; }
        public string CellCode { get; set; }
        public string DetectionArea { get; set; }
        public string DetectionResults { get; set; }
        public DateTime? CreateTime { get; set; }

        // ── 兼容字段（旧 NG 结构） ──
        public int? NGtypeNum { get; set; }
        public string NGtype1 { get; set; }
        public string NGtype2 { get; set; }
        public string NGtype3 { get; set; }

        // ── 新增字段 ──
        public string ParamterCode { get; set; } = "";
        public string ParameterDesc { get; set; } = "";
        public string Value { get; set; } = "";
        public string UpperLimit { get; set; } = "";
        public string LowerLomit { get; set; } = "";
        public string TargetValue { get; set; } = "";
        public string Unit { get; set; } = "";
        public string ParameterResult { get; set; } = "";
    }
}
