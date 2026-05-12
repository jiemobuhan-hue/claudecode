using System;

namespace ZenergyBFSI.Workspace.Models
{
    /// <summary>
    /// 蓝膜检测记录实体类 (对应数据库表 T_BlueFilmDetection)
    /// </summary>
    public class T_BlueFilmDetection
    {
        public int? Num { get; set; }
        public string BottomCellType { get; set; }
        public string CellCode { get; set; }
        public string DetectionArea { get; set; }
        public string DetectionResults { get; set; }
        public int? NGtypeNum { get; set; }
        public string NGtype1 { get; set; }
        public string NGtype2 { get; set; }
        public string NGtype3 { get; set; }
        public DateTime? CreateTime { get; set; }
    }
}