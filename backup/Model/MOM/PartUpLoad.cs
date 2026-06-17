using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 关键零部件上机，更换零部件
    /// </summary>
    internal class PartUpLoad_Request : BaseRequest
    {
        public PartUpLoad_Request() { }
    }
    internal class PartUpLoad_Response : BaseResponse
    {
        public List<PartUpLoad_PartInfo> PartInfo { get; set; } = new List<PartUpLoad_PartInfo>();

        public PartUpLoad_Response() { }
    }

    internal class PartUpLoad_PartInfo
    {
        public string PartNo { get; set; }
        public string Location { get; set; }
        public string PartName { get; set; }
        public string UseLifetime { get; set; }
        public string WarningLife { get; set; }
    }
}
