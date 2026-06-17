using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 工单请求 - 向MOM请求设备在生产状态的工单信息
    /// </summary>
    internal class WipOrderRequest_Request : BaseRequest
    {
        public WipOrderRequest_Request() { }
    }

    internal class WipOrderRequest_Response : BaseResponse
    {
        public List<WipOrderRequest_WipOrder> WipOrder { get; set; } = new List<WipOrderRequest_WipOrder>();
        public WipOrderRequest_Response() { }
    }

    public class WipOrderRequest_WipOrder
    {
        public string WipOrderNo { get; set; } = "";
    }
}
