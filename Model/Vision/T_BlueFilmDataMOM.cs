using System;

namespace ZenergyBFSI.Model.Vision
{
    /// <summary>
    /// 蓝膜MOM数据实体类 (对应数据库表 T_BlueFilmDataMOM)
    /// 一行 = 一个缺陷实例的一个检测参数
    /// </summary>
    public class T_BlueFilmDataMOM
    {
        public int? Num { get; set; }
        public string SideCellType { get; set; }
        public string CellCode { get; set; }
        public DateTime? CreateTime { get; set; }

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
