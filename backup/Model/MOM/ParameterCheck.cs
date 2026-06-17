using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// MOM参数管控
    /// </summary>
    internal class ParameterCheck_Request : BaseRequest
    {
        public string EmployeeNo { get; set; } = "";
        public string Password { get; set; } = "";
        public string WipOrderNo { get; set; } = "";
        public string EquipmentModel { get; set; } = "";
        public List<ParameterCheck_ParameterInfo> ParameterInfo { get; set; } = new List<ParameterCheck_ParameterInfo>();

        public ParameterCheck_Request()
        {
        }

        public ParameterCheck_Request(string employeeNo, string password, string wipOrderNo, string equipmentModel)
        {
            EmployeeNo = employeeNo;
            Password = password;
            WipOrderNo = wipOrderNo;
            EquipmentModel = equipmentModel;
        }
    }
    internal class ParameterCheck_Response : BaseResponse
    {
    }
    internal class ParameterCheck_ParameterInfo
    {
        public string ParameterCode { get; set; } = "";
        public string ParameterType  { get; set; } = "";
        public string Value { get; set; } = "";
        public string TargetValue    { get; set; } = "";
        public string UOMCode { get; set; } = "";
        public string UpperControlLimit { get; set; } = "";
        public string LowerControlLimit { get; set; } = "";
        public string UpperSpecificationsLimit { get; set; } = "";//规格上限值
        public string LowerSpecificationsLimit { get; set; } = "";//规格下限值
        public string Description { get; set; } = "";

        public ParameterCheck_ParameterInfo()
        {
        }

        public ParameterCheck_ParameterInfo(string parameterCode, string parameterType, string value, string targetValue, string uOMCode, string upperControlLimit, string lowerControlLimit, string upperSpecificationsLimit, string lowerSpecificationsLimit, string description)
        {
            ParameterCode = parameterCode;
            ParameterType = parameterType;
            Value = value;
            TargetValue = targetValue;
            UOMCode = uOMCode;
            UpperControlLimit = upperControlLimit;
            LowerControlLimit = lowerControlLimit;
            UpperSpecificationsLimit = upperSpecificationsLimit;
            LowerSpecificationsLimit = lowerSpecificationsLimit;
            Description = description;
        }
    }
}
