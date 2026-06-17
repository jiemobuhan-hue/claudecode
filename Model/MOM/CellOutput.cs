using System.Collections.Generic;
using ZenergyBFSI.Service;
using ZenergyBFSI.View;

namespace ZenergyBFSI.Model.MOM
{
    //出站物料MOM参数
    internal class CellOutput_Request : BaseRequest
    {
        public List<CellOutput_SerialNo> SerialNos { get; set; } = new List<CellOutput_SerialNo>();
    }
    internal class CellOutput_Response : BaseResponse
    {
    }
    public class CellOutput_SerialNo
    {
        public string SerialNo { get; set; } = "";
        public string ProductType { get; set; } = "";
        public bool PassFlag { get; set; } = true;
        public List<CellOutput_SerialNo_PartInfo> PartInfo { get; set; } = new List<CellOutput_SerialNo_PartInfo>();
        public List<CellOutput_SerialNo_MaterialInfo> MaterialInfo { get; set; } = new List<CellOutput_SerialNo_MaterialInfo>();
        public List<CellOutput_SerialNo_Parameters> Parameters { get; set; } = new List<CellOutput_SerialNo_Parameters>();

        public CellOutput_SerialNo()
        {
        }

        public CellOutput_SerialNo(string serialNo, string productType, bool passFlag)
        {
            SerialNo = serialNo;
            ProductType = productType;
            PassFlag = passFlag;
        }
        public CellOutput_SerialNo(string serialNo, string productType, CellData data)
        {
            SerialNo = serialNo;
            ProductType = productType;
            foreach (ParameterInfo param in MomHandler.I.AllParameter())
            {
                Parameters.Add(new CellOutput_SerialNo_Parameters(param, data));
            }
            if (data.出站结果 == "NG")
            {
                PassFlag = false;
            }
            if (Parameters.Count < MomHandler.I.ParameterCount())
            {
                UC_Operation.I.WriteLog($"{serialNo} 参数计数错误,Parameters:{Parameters.Count} < {MomHandler.I.ParameterCount()}");
                PassFlag = false;
            }
        }
    }
    public class CellOutput_SerialNo_PartInfo
    {
        public string PartNO { get; set; } = "";
        public string Location { get; set; } = "";
        public string Lifetime { get; set; } = "";

        public CellOutput_SerialNo_PartInfo()
        {
        }

        public CellOutput_SerialNo_PartInfo(string partNO, string location, string lifetime)
        {
            PartNO = partNO;
            Location = location;
            Lifetime = lifetime;
        }
    }
    public class CellOutput_SerialNo_MaterialInfo
    {
        public string LabelNo { get; set; } = "";
        public string Quantity { get; set; } = "";

        public CellOutput_SerialNo_MaterialInfo()
        {
        }

        public CellOutput_SerialNo_MaterialInfo(string labelNo, string quantity)
        {
            LabelNo = labelNo;
            Quantity = quantity;
        }
    }
    public class CellOutput_SerialNo_Parameters
    {
        public string ParamterCode { get; set; } = "";
        public string ParameterDesc { get; set; } = "";
        public string Value { get; set; } = "";
        public string UpperLimit { get; set; } = "";
        public string LowerLomit { get; set; } = "";
        public string TargetValue { get; set; } = "";
        public string ParameterResult { get; set; } = "";

        public CellOutput_SerialNo_Parameters()
        {
        }

        public CellOutput_SerialNo_Parameters(string paramterCode, string parameterDesc, string value, string upperLimit, string lowerLomit, string targetValue, string parameterResult)
        {
            ParamterCode = paramterCode;
            ParameterDesc = parameterDesc;
            Value = value;
            UpperLimit = upperLimit;
            LowerLomit = lowerLomit;
            TargetValue = targetValue;
            ParameterResult = parameterResult;
        }

        internal CellOutput_SerialNo_Parameters(ParameterInfo param, CellData data)
        {
            ParamterCode = param.ParameterCode;
            ParameterDesc = param.Description;
            UpperLimit = param.UpperSpecificationsLimit;
            LowerLomit = param.LowerSpecificationsLimit;
            TargetValue = param.TargetValue;
            ParameterResult = "OK";
        }
    }
}
