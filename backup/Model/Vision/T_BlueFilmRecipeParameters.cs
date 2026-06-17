using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenergyBFSI.Model.Vision
{
    public class T_BlueFilmRecipeParameters
    {
        public string ParameterID { get; set; } 
        public string Description { get; set; } = "";
        public DateTime? UpdateTime { get; set; }
        public int? ACK { get; set; }
        public int Enable { get; set; } = 1;
        public string ParameterName { get; set; }
        public string ParameterType { get; set; } = "";
        public string UpperSpecificationsLimit { get; set; } = "";
        public string LowerSpecificationsLimit { get; set; } = "";
        public string Unit { get; set; } = "";
        public string status { get; set; } = "";
        public string ReserveField1 { get; set; } = "";
        public string ReserveField2 { get; set; } = "";
        public string ReserveField3 { get; set; } = "";
        public string ReserveField4 { get; set; } = "";
        public string ReserveField5 { get; set; } = "";
        public string ReserveField6 { get; set; } = "";
    }
}
