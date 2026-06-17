using System.Collections.Generic;

namespace ZenergyBFSI.Model.MOM
{
    internal class Injection2Input_Request : BaseRequest
    {
        public List<Injection2Input_Request_SerialNo> SerialNos { get; set; } = new List<Injection2Input_Request_SerialNo>();
    }
    internal class Injection2Input_Response : BaseResponse
    {
        public List<Injection2Input_Response_SerialNo> SerialNos { get; set; } = new List<Injection2Input_Response_SerialNo>();
    }
    internal class Injection2Input_Request_SerialNo
    {
        public string SerialNo { get; set; } = "";

        public Injection2Input_Request_SerialNo()
        {
        }

        public Injection2Input_Request_SerialNo(string serialNo)
        {
            SerialNo = serialNo;
        }
    }
    internal class Injection2Input_Response_SerialNo
    {
        public Injection2Input_Response_SerialNo()
        {
        }

        public Injection2Input_Response_SerialNo(string serialNo, bool result, string weight, string weight1)
        {
            SerialNo = serialNo;
            Result = result;
            Weight = weight;
            Weight1 = weight1;
        }

        public string SerialNo { get; set; } = "";
        public bool Result { get; set; } = false;
        public string Weight { get; set; } = "";//一注前称重
        public string Weight1 { get; set; } = "";//一注后称重
    }
}
