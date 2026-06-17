namespace ZenergyBFSI.Model.MOM
{
    internal class EqptStatus_Request : BaseRequest
    {
        public string LocationID { get; set; } = "";
        public string StatusCode { get; set; } = "";
        public string ReasonCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string StartDate { get; set; } = "";

        public EqptStatus_Request()
        {
        }

        public EqptStatus_Request(string locationID, string statusCode, string reasonCode, string description, string startDate)
        {
            LocationID = locationID;
            StatusCode = statusCode;
            ReasonCode = reasonCode;
            Description = description;
            StartDate = startDate;
        }
    }
    internal class EqptStatus_Response : BaseResponse
    {
    }
}
