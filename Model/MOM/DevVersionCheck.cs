using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 设备软件版本监测 - 向MOM上报设备各模块的软件版本信息
    /// </summary>
    internal class DevVersionCheck_Request : BaseRequest
    {
        public List<DevVersionCheck_SoftwareInfo> SoftwareInfo { get; set; } = new List<DevVersionCheck_SoftwareInfo>();
        public DevVersionCheck_Request() { }
    }

    internal class DevVersionCheck_Response : BaseResponse
    {
        public DevVersionCheck_Response() { }
    }

    public class DevVersionCheck_SoftwareInfo
    {
        public string Module { get; set; } = "";
        public string Version { get; set; } = "";

        public DevVersionCheck_SoftwareInfo() { }

        public DevVersionCheck_SoftwareInfo(string module, string version)
        {
            Module = module;
            Version = version;
        }
    }
}
