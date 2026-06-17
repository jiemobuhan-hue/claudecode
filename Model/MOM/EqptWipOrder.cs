namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 设备工单绑定 - 将工单与设备绑定
    /// </summary>
    internal class EqptWipOrder_Request : BaseRequest
    {
        public string WipOrderNo { get; set; } = "";
        public EqptWipOrder_Request() { }
    }

    internal class EqptWipOrder_Response : BaseResponse
    {
        public EqptWipOrder_Response() { }
    }
}
