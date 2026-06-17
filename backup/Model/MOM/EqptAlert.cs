using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 设备异常报警的MOM接口
    /// </summary>
    internal class EqptAlert_Request : BaseRequest
    {
        public List<EqptAlert_AlertInfo> AlertInfo { get; set; } = new List<EqptAlert_AlertInfo>();
    }
    internal class EqptAlert_Response : BaseResponse
    {
    }
    internal class EqptAlert_AlertInfo
    {
        public string AlertCode { get; set; } = "";
        public string AlertReset { get; set; } = "";
        public string AlertDescription { get; set; } = "";
        public string AlertLevel { get; set; } = "";
        public string PartCode { get; set; } = "";
        public string ProductType { get; set; } = "";
        public EqptAlert_AlertInfo() { }

        public EqptAlert_AlertInfo(string alertCode, string alertReset, string alertDescription, string alertLevel, string partCode, string productType)
        {
            AlertCode = alertCode;
            AlertReset = alertReset;
            AlertDescription = alertDescription;
            AlertLevel = alertLevel;
            PartCode = partCode;
            ProductType = productType;
        }
    }
}
