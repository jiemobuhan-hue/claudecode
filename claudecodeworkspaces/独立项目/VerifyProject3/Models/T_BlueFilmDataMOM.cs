using System;

namespace VerifyProject.Models
{
    #region T_BlueFilmDataMOM — VisionProgram.dbo.T_BlueFilmDataMOM

    // 来源: INFORMATION_SCHEMA 实际查询
    //   Num              int      PK, is_identity=1
    //   SideCellType     nchar(10)
    //   CellCode         nvarchar(50)
    //   DetectionArea    nchar(10)      (注意: 无 Reinvestment 列)
    //   DetectionResults nchar(10)
    //   NGtypeNum        int
    //   NGtype1          nchar(10)
    //   NGtype2          nchar(10)
    //   NGtype3          nchar(10)
    //   CreateTime       datetime
    //   [新增 2026-06-10] ParamterCode, ParameterDesc, Value,
    //     UpperLimit, LowerLomit, TargetValue, Unit, ParameterResult

    public class T_BlueFilmDataMOM
    {
        public int? Num { get; set; }
        public string SideCellType { get; set; }
        public string CellCode { get; set; }
        public string DetectionArea { get; set; }
        public string DetectionResults { get; set; }
        public int? NGtypeNum { get; set; }
        public string NGtype1 { get; set; }
        public string NGtype2 { get; set; }
        public string NGtype3 { get; set; }
        public DateTime? CreateTime { get; set; }

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

    #endregion
}
