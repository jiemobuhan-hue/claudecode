using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    /// <summary>
    /// 设备运行模式同步MOM接口，当前设备是否同意入网，人员是否有权限
    /// </summary>
    internal class EqptRun_Request : BaseRequest
    {
        public string EmployeeNo { get; set; } = "";
        public string Password { get; set; } = "";
        public string EquipmentModel { get; set; } = "";
        //0->联机模式：MOM会给设备下达工艺参数信息，设备出入站要判断MOM回复信息是否尾OK值，如果反馈NG需要将电芯放入到NG口
        //1->离线模式：设备需要单机记录所有的生产信息以便MOM回复后上传信息
        //2->调机模式：设备运行修改MOM提供的工艺参数，正常调用出入站接口，所有这个模式下产出的电芯都需要放入到NG口待人为判定是否可以进入下一站。调机结束后需要再次调用本接口，恢复为联机模式，MOM重新下达工艺参数信息
        public EqptRun_Request() { }
    }
    internal class EqptRun_Response : BaseResponse
    {
        public string ProductDesc { get; set; } = "";
        public string FirstArticleNum { get; set; } = "";
        public string DebugNum { get; set; } = "";
        public string ParamVersion { get; set; } = "";
        public bool ParamRefreshFlag { get; set; } = false;
        //  设备是否需要接收MOM下发的工艺参数
        //True->接收工艺参数
        //False->不接受
        public List<EqptRun_ParameterInfo> ParameterInfo { get; set; } = new List<EqptRun_ParameterInfo>();
        public EqptRun_Response() { }
    }
    public class EqptRun_ParameterInfo
    {
        public string ParameterCode { get; set; } = "";
        public string ParameterType { get; set; } = "";
        public string TargetValue { get; set; } = "";
        public string UOMCode { get; set; } = "";
        public string UpperControlLimit { get; set; } = "";
        public string LowerControlLimit { get; set; } = "";
        public string UpperSpecificationsLimit { get; set; } = "";
        public string LowerSpecificationsLimit { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
