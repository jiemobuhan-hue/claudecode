using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 原材料下机操作的MOM接口
    /// </summary>
    internal class MaterialDownLoad_Request : BaseRequest
    {
        public List<MaterialDownLoad_MaterialInfo> MaterialInfo { get; set; } = new List<MaterialDownLoad_MaterialInfo>();
    }
    internal class MaterialDownLoad_Response : BaseResponse
    {
    }
    internal class MaterialDownLoad_MaterialInfo
    {
        public string MaterialQuantity { get; set; } = "";
        public string LabelNo { get; set; } = "";

        public MaterialDownLoad_MaterialInfo()
        {
        }

        public MaterialDownLoad_MaterialInfo(string materialQuantity, string labelNo)
        {
            MaterialQuantity = materialQuantity;
            LabelNo = labelNo;
        }
    }
}
