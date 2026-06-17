namespace ZenergyBFSI.Model
{
    public class MomCheckResult
    {
        public string SerialNo { get; set; } = "";
        public MomResultCode Result { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public enum MomResultCode
    {
        Communicating = -1,
        Waiting = 0,
        Ok = 1,
        Ng = 2,
        Failed = 3,
        Offline = 4
    }
}
