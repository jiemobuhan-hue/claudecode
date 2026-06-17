using RinKit;
using ZenergyBFSI.Model.MOM;

namespace ZenergyBFSI.Model
{
    public class ParameterInfo
    {
        public long Time { get; set; } = DataHelper.TimeMS;
        public int Enable { get; set; } = 1;
        public string ParameterCode { get; set; } = "";
        public string ParameterType { get; set; } = "";
        public string TargetValue { get; set; } = "";
        public string Value { get; set; } = "";
        public string UOMCode { get; set; } = "";
        public string UpperControlLimit { get; set; } = "";
        public string LowerControlLimit { get; set; } = "";
        public string UpperSpecificationsLimit { get; set; } = "";
        public string LowerSpecificationsLimit { get; set; } = "";
        public string Description { get; set; } = "";

        public ParameterInfo()
        {
        }

        public ParameterInfo(EqptRun_ParameterInfo run)
        {
            ParameterCode = run.ParameterCode;
            ParameterType = run.ParameterType;
            TargetValue = run.TargetValue;
            UOMCode = run.UOMCode;
            UpperControlLimit = run.UpperControlLimit;
            LowerControlLimit = run.LowerControlLimit;
            UpperSpecificationsLimit = run.UpperSpecificationsLimit;
            LowerSpecificationsLimit = run.LowerSpecificationsLimit;
            Description = run.Description;
        }
    }
}
