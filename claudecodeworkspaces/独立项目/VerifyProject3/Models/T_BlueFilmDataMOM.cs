using System;

namespace VerifyProject.Models
{
    #region T_BlueFilmDataMOM — VisionProgram.dbo.T_BlueFilmDataMOM

    // 一行 = 一个缺陷实例的一个检测参数
    // 2026-06-10 移除冗余字段 DetectionArea, DetectionResults, NGtypeNum/1/2/3

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

    #endregion
}
