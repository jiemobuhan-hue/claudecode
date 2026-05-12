using DevExpress.ClipboardSource.SpreadsheetML;
using DevExpress.Entity.Model.Metadata;
using NLog.LayoutRenderers;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Windows;
using System.Windows.Media.Media3D;
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
            //PassFlag = passFlag;
            //参数 TODO
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
            #region MOM数据校验
            //float weight = data.前称重重量;
            //float loss = data.化成失液量;
            //if (data.前称重结果 == "复投")
            //{
            //    if(data.第一次前称重量 < Settings.前称重上限 && data.第一次前称重量 > Settings.前称重下限)
            //    {
            //        weight = data.第一次前称重量;
            //    }
            //    else
            //    {
            //        weight = (Settings.前称重上限 + Settings.前称重下限) / 2;
            //        loss = 0;
            //        UC_Operation.I.WriteLog($"复投数据异常！第一次前称重量:{data.第一次前称重量}不在规格内，复投失败！", "Warn");
            //    }
            //}
            //switch (param.Description)
            //{
            //    case "二次注液时间": Value = $"{data.注液时间}"; break;
            //    case "保压时间": Value = $"{data.保压时间}"; break;
            //    case "二次注液前称重工位": Value = $"{data.前称重工位}"; break;
            //    case "二次注液后称重工位": Value = $"{data.后称重工位}"; break;
            //    case "目标注液量": TargetValue = $"{data.目标注液量}"; Value = $"{data.实际注液量}"; break;
            //    case "二次注液前称重": Value = $"{weight}"; break;
            //    case "二次注液后称重": Value = $"{data.后称重重量}"; break;
            //    case "二次注液保有量": Value = $"{data.实际保有量}"; break;
            //    case "化成电解液失液量": Value = $"{loss}"; break;
            //    case "二次注液前抽真空": Value = $"{data.保压真空目标值}"; break;
            //    case "二次注液抽真空时间": Value = $"{data.抽真空时间}"; break;
            //    case "二次注液高真空值": Value = $"{data.保压前真空}"; break;
            //    case "二次注液低真空值": Value = $"{data.保压后真空}"; break;
            //    case "二次注液正压值": Value = $"{data.注液正压目标值}"; break;
            //    case "二次注液压钉高度": Value = $"{data.胶钉高度}"; break;

            //    case "二次注液注液杯号": Value = $"{data.注液工位}"; break;
            //    case "二次注液正压时间": Value = $"{data.正压时间}"; break;
            //    case "真空变化值": Value = $"{data.保压前真空 - data.保压后真空}"; break;
            //    case "二次注液模组": Value = $"{data.注液工位}"; break;

            //    case "全压钉前抽真空值": Value = $"{Settings.压钉真空值}"; break;//TODO
            //    case "全压钉前抽真空时间": Value = $"{Settings.压钉真空时间}"; break;//TODO
            //    case "二次注液打钉结果": Value = $"{data.胶钉检测结果}"; break;
            //    case "保液量结果": Value = $"{data.后称重结果}"; break;//TODO 保液量和后称重结果区分
            //}
            #endregion
            float val = float.NaN;
            ParameterResult = "OK";
            switch (param.Description)
            {
                //case "二次注液时间": val = data.注液时间; Value = val.ToString(); break;
                //case "保压时间": val = data.保压时间; Value = val.ToString(); break;
                //case "二次注液前称重工位": Value = data.前称重工位; break;
                //case "二次注液后称重工位": Value = data.后称重工位; break;
                //case "目标注液量": val = data.目标注液量; Value = val.ToString(); break;
                //case "二次注液前称重": val = weight; Value = val.ToString(); break;
                //case "二次注液后称重": val = data.后称重重量; Value = val.ToString(); break;
                //case "二次注液保有量": val = data.实际保有量; Value = val.ToString(); break;
                //case "化成电解液失液量": val = loss; Value = val.ToString(); break;
                //case "二次注液前抽真空": val = data.保压真空目标值; Value = val.ToString(); break;
                //case "二次注液抽真空时间": val = data.抽真空时间; Value = val.ToString(); break;
                //case "二次注液高真空值": val = PlcHandler.I.GetOBJ("高真空目标值设定").vFloat; Value = val.ToString(); break;
                //case "二次注液低真空值": val = PlcHandler.I.GetOBJ("低压目标值设定").vFloat; Value = val.ToString(); break;
                //case "二次注液正压值": val = data.注液正压目标值; Value = val.ToString(); break;
                //case "二次注液压钉高度": val = data.胶钉高度; Value = val.ToString(); break;

                //case "二次注液注液杯号": Value = data.注液工位; break;
                //case "二次注液正压时间": val = data.正压时间; Value = val.ToString(); break;
                //case "真空变化值": val = data.保压前真空 - data.保压后真空; Value = val.ToString(); break;
                //case "二次注液模组": Value = data.注液工位; break;

                //case "全压钉前抽真空值": val = Settings.压钉真空值; Value = val.ToString(); break;//TODO
                //case "全压钉前抽真空时间": val = Settings.压钉真空时间; Value = val.ToString(); break;//TODO
                //case "二次注液打钉结果": Value = data.胶钉检测结果; if (Value != "OK") ParameterResult = "NG"; break;
                //case "保液量结果": Value = data.后称重结果; if (Value != "OK") ParameterResult = "NG"; break;//TODO 保液量和后称重结果区分
            }
            if (val != float.NaN&&(UpperLimit!="0" || LowerLomit != "0"))
            {
                //OK判定
                if (!string.IsNullOrEmpty(UpperLimit) && float.TryParse(UpperLimit, out float upper))
                {
                    if (val > upper)
                    {
                        ParameterResult = "NG";
                        data.出站结果 = "NG";
                        UC_Operation.I.WriteLog($"{param.Description} NG. ({val}>{upper})", "Warn");
                    }
                }
                if (!string.IsNullOrEmpty(LowerLomit) && float.TryParse(LowerLomit, out float lower))
                {
                    if (val < lower)
                    {
                        ParameterResult = "NG";
                        data.出站结果 = "NG";
                        UC_Operation.I.WriteLog($"{param.Description} NG. ({val}<{lower})", "Warn");
                    }
                }
            }
        }
    }
}
