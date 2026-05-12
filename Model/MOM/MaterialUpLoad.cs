using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 上料接口，MOM端口的请求
    /// </summary>
    internal class MaterialUpLoad_Request : BaseRequest
    {
    }
    internal class MaterialUpLoad_Response : BaseResponse
    {
        public List<MaterialUpLoad_MaterialInfo> MaterialInfo { get; set; } = new List<MaterialUpLoad_MaterialInfo>();
    }
    internal class MaterialUpLoad_MaterialInfo
    {
        public string ProductNo { get; set; }
        public string LabelNo { get; set; }
        public string Location { get; set; }
        public string Quantity { get; set; }
        public string UomCode { get; set; }
        public string AvailableFlag { get; set; }
        public string AvailableMessage { get; set; }

        public MaterialUpLoad_MaterialInfo()
        {
        }

        public MaterialUpLoad_MaterialInfo(string productNo, string labelNo, string location, string quantity, string uomCode, string availableFlag, string availableMessage)
        {
            ProductNo = productNo;
            LabelNo = labelNo;
            Location = location;
            Quantity = quantity;
            UomCode = uomCode;
            AvailableFlag = availableFlag;
            AvailableMessage = availableMessage;
        }
    }
}
