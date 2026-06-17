using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 电芯入站检查 - 向MOM查询电芯是否允许进入当前工位
    /// </summary>
    internal class CellInput_Request : BaseRequest
    {
        public List<CellInput_SerialNo> SerialNos { get; set; } = new List<CellInput_SerialNo>();
        public CellInput_Request() { }
    }

    internal class CellInput_Response : BaseResponse
    {
        public List<CellInput_SerialNoResult> SerialNos { get; set; } = new List<CellInput_SerialNoResult>();
        public CellInput_Response() { }
    }

    public class CellInput_SerialNo
    {
        public string SerialNo { get; set; } = "";
        public bool GetProductTypeFlag { get; set; } = false;

        public CellInput_SerialNo() { }

        public CellInput_SerialNo(string serialNo, bool getProductTypeFlag = false)
        {
            SerialNo = serialNo;
            GetProductTypeFlag = getProductTypeFlag;
        }
    }

    public class CellInput_SerialNoResult
    {
        public string SerialNo { get; set; } = "";
        public bool Result { get; set; } = false;
        public string ProductType { get; set; } = "";
    }
}
