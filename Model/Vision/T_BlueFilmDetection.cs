using System;

namespace ZenergyBFSI.Model
{
    /// <summary>
    /// 蓝膜检测记录实体类 (对应数据库表 T_BlueFilmDetection)
    /// 列: Num(PK), CellType, CellCode, Reinvestment, DetectionArea,
    ///      DetectionResults, NGtypeNum, NGtype1, NGtype2, NGtype3, CreateTime
    /// </summary>
    public class T_BlueFilmDetection
    {
        public int? Num { get; set; }
        public string CellType { get; set; }
        public string CellCode { get; set; }
        public int? Reinvestment { get; set; }
        public string DetectionArea { get; set; }
        public string DetectionResults { get; set; }
        public int? NGtypeNum { get; set; }
        public string NGtype1 { get; set; }
        public string NGtype2 { get; set; }
        public string NGtype3 { get; set; }
        public DateTime? CreateTime { get; set; }
    }
}
