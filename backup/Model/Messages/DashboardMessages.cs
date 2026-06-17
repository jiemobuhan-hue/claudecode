using ZenergyBFSI.Model;

namespace ZenergyBFSI.Model.Messages
{
    /// <summary>
    /// 看板数据更新消息
    /// </summary>
    public class DashboardUpdateMessage
    {
        public InspectionUtils.DashboardData Data { get; set; }

        public DashboardUpdateMessage(InspectionUtils.DashboardData data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// 产线状态灯更新消息
    /// </summary>
    public class StatusLightUpdateMessage
    {
        public string Result { get; set; }      // OK/NG/离线
        public string CellCode { get; set; }    // 电芯码
        public string Time { get; set; }        // 时间字符串 HH:mm:ss

        public StatusLightUpdateMessage(string result, string cellCode, string time)
        {
            Result = result;
            CellCode = cellCode;
            Time = time;
        }
    }

    /// <summary>
    /// 出站更新消息（视觉检测结果）
    /// </summary>
    public class ExitUpdateMessage
    {
        public string CellCode { get; set; }    // 电芯码
        public string ExitResult { get; set; }  // OK/NG
        public string NgTypes { get; set; }    // NG类型 "类型1|类型2"

        public ExitUpdateMessage(string cellCode, string exitResult, string ngTypes)
        {
            CellCode = cellCode;
            ExitResult = exitResult;
            NgTypes = ngTypes;
        }
    }
}