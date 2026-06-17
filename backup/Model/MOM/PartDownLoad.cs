using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 关键零部件上下机
    /// </summary>
    internal class PartDownLoad_Request : BaseRequest
    {
        public List<PartDownLoad_PartInfo> PartInfo { get; set; } = new List<PartDownLoad_PartInfo>();
    }
    internal class PartDownLoad_Response : BaseResponse
    {
    }
    internal class PartDownLoad_PartInfo
    {
        public string PartNo { get; set; }
        public string Location { get; set; }
        public string PartName { get; set; }
        public string UseLifetime { get; set; }

        public PartDownLoad_PartInfo()
        {
        }

        public PartDownLoad_PartInfo(string partNo, string location, string partName, string useLifetime)
        {
            PartNo = partNo;
            Location = location;
            PartName = partName;
            UseLifetime = useLifetime;
        }
    }
}
