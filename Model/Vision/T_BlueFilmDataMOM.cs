using RinKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace ZenergyBFSI.Model.Vision
{
    public class T_BlueFilmDataMOM
    {
        public string Applicable_location { get; set; } = "";
        public string ParameterName { get; set; } = "";
        public string ParameterType { get; set; } = "";
        public string UpperSpecificationsLimit { get; set; } = "";
        public string LowerSpecificationsLimit { get; set; } = ""; 
        public string Unit { get; set; } = "";
        public string status { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class MOM_ParameterInfo
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
    }
}
