using System;

namespace ZenergyBFSI.Model
{
    /// <summary>
    /// 线束测量记录实体�?(对应数据库表 T_HarnessMeasure)
    /// </summary>
    public class T_HarnessMeasure
    {
        public int? Num { get; set; }
        public string PackCode { get; set; }
        public int? MarkNumber { get; set; }
        public string Result { get; set; }
        public decimal? Width1 { get; set; }
        public decimal? Width2 { get; set; }
        public decimal? Width3 { get; set; }
        public decimal? Width4 { get; set; }
        public decimal? Width5 { get; set; }
        public decimal? Width6 { get; set; }
        public decimal? WidthStandard { get; set; }
        public DateTime? CreateTime { get; set; }
    }
}