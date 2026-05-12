using Newtonsoft.Json;
using RinKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZenergyBFSI.Model;
using ZenergyBFSI.Model.MOM;
using ZenergyBFSI.MOM;
using ZenergyBFSI.View;
using ParameterInfo = ZenergyBFSI.Model.ParameterInfo;

namespace ZenergyBFSI.Service
{
    public class MomHandler
    {
        private static MomHandler _instance;
        private static object _syncRoot = new object();
        private Task _taskMomAlive;
        private bool _threadON;
        private int _count;
        private WsWcfServiceClient _momOffical = new WsWcfServiceClient();
        private List<ParameterInfo> _listParam = new List<ParameterInfo>();

        private List<MaterialUpLoad_MaterialInfo> _material = new List<MaterialUpLoad_MaterialInfo>();
        internal List<MaterialUpLoad_MaterialInfo> Material { get => _material; set => _material = value; }

        private List<CellData> _history = new List<CellData>();

        public long TS { get; set; } = -1;
        //private EqptRun_Response _eqpt;

        private MomHandler()
        {
        }

        public static MomHandler I
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new MomHandler();
                        }
                    }
                }
                return _instance;
            }
        }

        public bool Init()
        {
            lock (_syncRoot)
            {
                try
                {
                    UC_Operation.I.WriteLog("MOM初始化...", "Debug");
                    Rdb.SelectList(out _listParam, "SELECT * FROM ParameterInfo WHERE Enable=1");
                    //Rdb.SelectList(out List<CellData> list, @"Select * From CellData WHERE 前称重结果='OK' ORDER BY Id DESC LIMIT 100000");
                    Rdb.SelectList(out List<CellData> list, @"Select * From CellData ");
                    I._history = list.OrderBy(c => c.Id).ToList();
                    _momOffical.Endpoint.Address = new System.ServiceModel.EndpointAddress(Settings.MOM地址);
                    _momOffical.Open();

                    //MOM心跳线程
                    _taskMomAlive = new Task(Thread_EqptAlive);
                    _taskMomAlive.Start();
                    _threadON = true;
                    _count = Settings.MOM联机计数 - 1;
                    UC_Operation.I.WriteLog($"MOM初始化成功", "Info");
                    return true;
                }
                catch (Exception ex)
                {
                    UC_Operation.I.WriteLog($"PLC Init异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
                }
            }
            return false;
        }

        public void Close()
        {
            _momOffical.Close();
            _instance._threadON = false;
            _instance = null;
            UC_Operation.I.WriteLog("MomHandler Close", "Warn");
        }

        public void UpdateHistory(CellData data)
        {
            lock (_syncRoot)
            {
                try
                {
                    I._history.RemoveAt(0);
                    I._history.Add(data);
                    UC_Operation.I.WriteLog($"UpdateHistory:{_history.First()?.Id} - {_history.Last()?.Id}", "Debug");
                }
                catch (Exception ex)
                {
                    UC_Operation.I.WriteLog($"UpdateHistory异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
                }
            }
        }

        internal int ParameterCount()
        {
            return _listParam.Count() > 0 ? _listParam.Count() : 999;
        }

        internal ParameterInfo GetParameter(string description)
        {
            if (_listParam == null)
                return null;
            else
                return _listParam.Where(p => p.Description == description).FirstOrDefault();
        }

        internal List<ParameterInfo> AllParameter()
        {
            return _listParam;
        }

        private void Thread_EqptAlive()
        {
            while (_threadON)
            { 
                if (Settings.MOM在线 < 1) continue;
                try
                { 
                    if (_momOffical.State == System.ServiceModel.CommunicationState.Opened)
                    {
                        var req = new EqptAlive_Request();
                        var resJson = MomSendMessage("EqptAlive", JsonConvert.SerializeObject(new EqptAlive_Request()));
                        var resData = JsonConvert.DeserializeObject<EqptAlive_Response>(resJson.CommandResponseJson);
                        TS = DataHelper.TimeMS;
                        if (resData.KeyFlag != "0"&&WD_Alert.Alarmnums<1)
                        {
                            UC_Operation.I.WriteLog($"MOM心跳:{resData.KeyFlag}，{resData.MOMMessage}", "Warn");
                            UC_Operation.I.Alert(resData.MOMMessage);
                            //TODO PLC停机
                            if (uint.TryParse(resData.KeyFlag, out uint flag))
                            {
                                bool isStop = DataHelper.UintToBits(flag)[5];
                                AutoRun.I.Alarm(resData.MOMMessage, isStop ? 2 : 1);
                            }
                        }
                        else
                        {
                            AutoRun.I.Alarm("", 0);
                        }
                    }
                    else
                    {
                        UC_Operation.I.WriteLog("MOM心跳异常!", "Warn");

                    }
                    //当MOM联机计数达到一定数量的时候进行下列操作，一般情况下一次联机间隔15S
                    if (_count > Settings.MOM联机计数)
                    {
                        //版本上传
                        I.VersionUpLoad();
                        //MOM上料
                        I.MaterialUpLoad();
                        //MOM参数校验
                        ParameterPLC(_listParam);
                        //MOM联机获取参数
                        I.EqptRun();
                        //if (!I.ParameterCheck(_listParam))
                        //{
                        //    //MOM联机获取参数
                        //    I.EqptRun();
                        //}
                        //历史数据
                        //lock (_syncRoot)
                        //{
                        //    Rdb.SelectList(out List<CellData> list, @"Select * From CellData WHERE 前称重结果='OK' ORDER BY Id DESC LIMIT 100000");
                        //    I._history = list.OrderBy(c => c.Id).ToList();
                        //}
                        _count = 0;
                    }
                    else
                    {
                        _count++;
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("没有终结点在侦听可以接受消息的"))
                    {
                        UC_Operation.I.WriteLog("MOM通讯中断！", "Warn");
                    }
                    else if (ex.Message.Contains("database"))
                    {
                        UC_Operation.I.WriteLog("数据库繁忙，历史数据查询失败！" + ex.Message, "Warn");
                    }
                    else
                    {
                        UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                    }
                    Thread.Sleep(Settings.错误等待);
                }
                Thread.Sleep(Settings.MOM心跳间隔);
            }
        }

        private void VersionUpLoad()
        {
            //TODO
        }

        internal CellData GetHistoryQCZ(string 电芯码)
        {
            lock (_syncRoot)
            {
                return _history.Where(c => c.电芯码 == 电芯码 /*&& c.前称重结果 == "OK"*/).FirstOrDefault();
            }
        }

        internal CellData GetHistoryJDJC(string 电芯码)
        {
            lock (_syncRoot)
            {
                return _history.Where(c => c.电芯码 == 电芯码 /*&& c.胶钉检测结果 != ""*/).FirstOrDefault();
            }
        }

        private void ParameterPLC(List<ParameterInfo> listParam)
        {
            foreach (var param in listParam)
            {
                switch (param.Description)
                {
                    #region Mom参数接口回调


                    //case "二次注液时间": param.Value = $"{Settings.注液时间}"; break;
                    //case "保压时间": param.Value = $"{Settings.保压时间}"; break;
                    //case "二次注液前称重工位": param.Value = param.TargetValue; break;
                    //case "二次注液后称重工位": param.Value = param.TargetValue; break;
                    //case "目标注液量": param.Value = param.TargetValue; break;
                    //case "二次注液前称重": param.Value = $"{(Settings.前称重上限 + Settings.前称重下限) / 2}"; break;
                    //case "二次注液后称重": param.Value = $"{(Settings.后称重上限 + Settings.后称重下限) / 2}"; break;
                    //case "二次注液保有量": param.Value = $"{Settings.保液量目标}"; ; break;
                    //case "化成电解液失液量": param.Value = param.TargetValue; break;
                    //case "二次注液前抽真空": param.Value = $"{Settings.保压真空值}"; break;
                    //case "二次注液抽真空时间": param.Value = $"{Settings.抽真空时间}"; break;
                    //case "二次注液高真空值": param.Value = $"{Settings.保压真空值}"; break;
                    //case "二次注液低真空值": param.Value = $"{Settings.注液真空值}"; break;
                    //case "二次注液正压值": param.Value = $"{Settings.注液正压值}"; break;
                    //case "二次注液压钉高度": param.Value = param.TargetValue; break;

                    //case "二次注液注液杯号": param.Value = param.TargetValue; break;
                    //case "二次注液正压时间": param.Value = $"{Settings.正压时间}"; break;
                    //case "真空变化值": param.Value = param.TargetValue; break;
                    //case "二次注液模组": param.Value = param.TargetValue; break;

                    //case "全压钉前抽真空值": param.Value = $"{Settings.压钉真空值}"; break;//TODO
                    //case "全压钉前抽真空时间": param.Value = $"{Settings.压钉真空时间}"; break;//TODO
                    //case "二次注液打钉结果": param.Value = param.TargetValue; break;
                    //case "保液量结果": param.Value = param.TargetValue; break;//TODO 保液量和后称重结果区分
                    #endregion
                }
            }
        }

        #region Method
        private MessageResponse MomSendMessage(string cmd, string parameterJson, bool logFlag = true)
        {

            if (_momOffical.State == System.ServiceModel.CommunicationState.Opened)
            {
                var data = new MessageRequest()
                {
                    CommandId = cmd,
                    RequestDate = DateTime.UtcNow,
                    MessageGuid = Guid.NewGuid(),
                    CommandRequestJson = parameterJson
                };
                if (logFlag)
                    Rlog.Debug($"{cmd}_Request:{parameterJson}");
                var res = _momOffical.SendMessage(data);
                if (logFlag)
                    Rlog.Debug($"{cmd}_Response:{res.CommandResponseJson}");
                return res;
            }
            else
            {
                UC_Operation.I.WriteLog("MOM通讯失败!", "Warn");
                return null;
            }
        }

        internal bool LimitCheck(string pName, float val)
        {
            var param = _listParam.Where(p => p.Description == pName).FirstOrDefault();
            if (param == null)
            {
                UC_Operation.I.WriteLog($"MOM参数未获取:{pName}", "Error");
                return false;
            }
            //if (float.TryParse(value, out float val))
            //{
            //    UC_Operation.I.WriteLog($"MOM参数VALUE错误:{value}", "Error");
            //    return false;
            //}
            if (!float.TryParse(param.UpperSpecificationsLimit.Trim(), out float up))
            {
                UC_Operation.I.WriteLog($"MOM参数Upper错误:{param.UpperSpecificationsLimit}", "Error");
                return false;
            }
            if (!float.TryParse(param.LowerSpecificationsLimit.Trim(), out float low))
            {
                UC_Operation.I.WriteLog($"MOM参数Lower错误:{param.LowerSpecificationsLimit}", "Error");
                return false;
            }
            if (val > up)
            {
                UC_Operation.I.WriteLog($"MOM参数LimitCheck:{val}> {up}", "Warn");
                return false;
            }
            if (val < low)
            {
                UC_Operation.I.WriteLog($"MOM参数LimitCheck:{val} < {low}", "Warn");
                return false;
            }
            return true;
        }

        internal bool EqptRun()
        {
            try
            {
                var req = new EqptRun_Request();
                var resJson = MomSendMessage("EqptRun", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<EqptRun_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM联机:OK", "Info");
                Rlog.Debug(resJson.CommandResponseJson);
                if (resData.ParameterInfo.Count > 0)
                {
                    if (!ParameterCheck(_listParam, resData.ParameterInfo))
                    {
                        UC_Operation.I.WriteLog($"MOM参数不一致", "Warn");
                        Rdb.Do("UPDATE ParameterInfo SET Enable = 0");
                        var list = new List<ParameterInfo>();
                        foreach (var item in resData.ParameterInfo)
                        {
                            var param = new ParameterInfo(item);
                            list.Add(param);
                            Rdb.Insert(param, true);
                            _listParam = list;
                            switch (param.Description)
                            {
                                #region 参数校验处理逻辑
                                //case "二次注液保有量":
                                //    {
                                //        if (float.TryParse(param.TargetValue, out float djybyl))
                                //        {
                                //            Settings.保液量目标 = djybyl;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液保有量 TargetValue 错误！", "Warn");
                                //        }
                                //        if (float.TryParse(param.UpperSpecificationsLimit, out float djyby2))
                                //        {
                                //            Settings.保液量上限 = djyby2;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液保有量 Upper 错误！", "Warn");
                                //        }
                                //        if (float.TryParse(param.LowerSpecificationsLimit, out float djyby3))
                                //        {
                                //            Settings.保液量下限 = djyby3;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液保有量 Lower 错误！", "Warn");
                                //        }
                                //    }
                                //    break;
                                //case "二次注液前称重":
                                //    {
                                //        if (float.TryParse(param.UpperSpecificationsLimit, out float qczsx))
                                //        {
                                //            Settings.前称重上限 = qczsx;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液前称重 错误！", "Warn");
                                //        }
                                //        if (float.TryParse(param.LowerSpecificationsLimit, out float qczxx))
                                //        {
                                //            Settings.前称重下限 = qczxx;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液前称重 错误！", "Warn");
                                //        }
                                //    }
                                //    break;
                                //case "化成电解液失液量":
                                //    {
                                //        if (float.TryParse(param.UpperSpecificationsLimit, out float qczsx))
                                //        {
                                //            Settings.失液量上限 = qczsx;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液前称重 错误！", "Warn");
                                //        }
                                //        if (float.TryParse(param.LowerSpecificationsLimit, out float qczxx))
                                //        {
                                //            Settings.失液量下限 = qczxx;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液前称重 错误！", "Warn");
                                //        }
                                //    }
                                //    break;
                                //case "二次注液后称重":
                                //    {
                                //        if (float.TryParse(param.UpperSpecificationsLimit, out float qczsx))
                                //        {
                                //            Settings.后称重上限 = qczsx;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液后称重 错误！", "Warn");
                                //        }
                                //        if (float.TryParse(param.LowerSpecificationsLimit, out float qczxx))
                                //        {
                                //            Settings.后称重下限 = qczxx;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液后称重 错误！", "Warn");
                                //        }
                                //    }
                                //    break;
                                //case "二次注液压钉高度":
                                //    {
                                //        if (float.TryParse(param.UpperSpecificationsLimit, out float qczsx))
                                //        {
                                //            Settings.胶钉高度上限 = qczsx;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液压钉高度 错误！", "Warn");
                                //        }
                                //        if (float.TryParse(param.LowerSpecificationsLimit, out float qczxx))
                                //        {
                                //            Settings.胶钉高度下限 = qczxx;
                                //        }
                                //        else
                                //        {
                                //            UC_Operation.I.WriteLog($"MOM.二次注液压钉高度 错误！", "Warn");
                                //        }
                                //    }
                                //    break;
                                #endregion
                            }
                            Settings.Save();
                        }
                        UC_Operation.I.WriteLog($"MOM参数已更新", "Info");
                    }
                }
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool ParameterCheck(List<ParameterInfo> pre, List<EqptRun_ParameterInfo> now)
        {
            if (pre.Count != now.Count) return false;
            for (int i = 0; i < pre.Count; i++)
            {
                if (pre[i].ParameterCode != now[i].ParameterCode) return false;
                if (pre[i].ParameterType != now[i].ParameterType) return false;
                if (pre[i].TargetValue != now[i].TargetValue) return false;
                if (pre[i].UOMCode != now[i].UOMCode) return false;
                if (pre[i].UpperControlLimit != now[i].UpperControlLimit) return false;
                if (pre[i].LowerControlLimit != now[i].LowerControlLimit) return false;
                if (pre[i].UpperSpecificationsLimit != now[i].UpperSpecificationsLimit) return false;
                if (pre[i].LowerSpecificationsLimit != now[i].LowerSpecificationsLimit) return false;
                if (pre[i].Description != now[i].Description) return false;
            }
            return true;
        }

        internal bool EqptStatus(string locationID, string statusCode, string reasonCode, string description, string startDate)
        {
            try
            {
                var req = new EqptStatus_Request(locationID, statusCode, reasonCode, description, startDate);
                var resJson = MomSendMessage("EqptStatus", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<EqptStatus_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM设备状态上传.", "Info");
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool EqptAlert(List<EqptAlert_AlertInfo> AlertInfo)
        {
            try
            {
                var req = new EqptAlert_Request();
                req.AlertInfo = AlertInfo;
                var resJson = MomSendMessage("EqptAlert", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<EqptAlert_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM设备报警上传.", "Info");
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool PartUpLoad()
        {
            try
            {
                var req = new PartUpLoad_Request();
                var resJson = MomSendMessage("PartUpLoad", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<PartUpLoad_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM关键零部件上机:{resJson.CommandResponseJson}", "Info");
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool PartDownLoad()
        {
            try
            {
                var req = new PartDownLoad_Request();
                req.PartInfo.Add(new PartDownLoad_PartInfo("partNo", "location", "partName", "useLifetime"));//TODO
                var resJson = MomSendMessage("PartDownLoad", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<PartDownLoad_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM关键零部件下机.", "Info");
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool MaterialUpLoad()
        {
            try
            {
                var req = new MaterialUpLoad_Request();
                var resJson = MomSendMessage("MaterialUpLoad", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<MaterialUpLoad_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                Material = resData.MaterialInfo;
                foreach (var item in Material)
                {
                    UC_Operation.I.WriteLog($"{item.Location} | {item.ProductNo} | {item.LabelNo} | {item.Quantity} {item.UomCode}", "Debug");
                }
                UC_Operation.I.WriteLog($"MOM原材料上机.完成", "Info");
                Rlog.Debug(resJson.CommandResponseJson);
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool MaterialDownLoad()
        {
            try
            {
                var req = new MaterialDownLoad_Request();
                req.MaterialInfo.Add(new MaterialDownLoad_MaterialInfo("materialQuantity", "labelNo"));//TODO
                var resJson = MomSendMessage("MaterialDownLoad", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<MaterialDownLoad_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM原材料下机.完成", "Info");
                Rlog.Debug(resJson.CommandResponseJson);
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool Injection2Input(List<Injection2Input_Request_SerialNo> serialNos)
        {
            try
            {
                var req = new Injection2Input_Request();
                req.SerialNos = serialNos;
                var resJson = MomSendMessage("Injection2Input", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<Injection2Input_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM二次注液进站:{resJson.CommandResponseJson}", "Info");
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool CellOutput(List<CellOutput_SerialNo> SerialNos)
        {
            try
            {
                var req = new CellOutput_Request();
                req.SerialNos = SerialNos;
                var resJson = MomSendMessage("CellOutput", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<CellOutput_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM电芯出站.{resData.MOMMessage}", "Info");
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        internal bool ParameterCheck(List<ParameterCheck_ParameterInfo> ParameterInfo)
        {
            try
            {
                var req = new ParameterCheck_Request();
                req.ParameterInfo = ParameterInfo;
                var resJson = MomSendMessage("ParameterCheck", JsonConvert.SerializeObject(req));
                var resData = JsonConvert.DeserializeObject<ParameterCheck_Response>(resJson.CommandResponseJson);
                if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); return false; }
                UC_Operation.I.WriteLog($"MOM参数一致性.完成", "Info");
                Rlog.Debug(resData.MOMMessage);
                return resData.ResultFlag;
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                return false;
            }
        }

        #endregion

        #region Function
        internal bool ParameterCheck(List<ParameterInfo> ParameterInfo)
        {
            //TODO 实时值校验
            List<ParameterCheck_ParameterInfo> list = new List<ParameterCheck_ParameterInfo>();
            foreach (var param in ParameterInfo)
            {
                list.Add(new ParameterCheck_ParameterInfo(param.ParameterCode, param.ParameterType, param.Value, param.TargetValue, param.UOMCode, param.UpperControlLimit, param.LowerControlLimit, param.UpperSpecificationsLimit, param.LowerSpecificationsLimit, param.Description));
            }
            return ParameterCheck(list);
        }
        internal void ClearIn()
        {
            _listCheckIn.Clear();
        }
        internal void ClearOut()
        {
            _listCheckOut.Clear();
        }
        // List<Inj2Check> _listCheckIn = new List<Inj2Check>();
        List<BFSICheck> _listCheckIn = new List<BFSICheck>();
        /// <summary>
        /// 入站MOM查询，按电芯码查询
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        internal BFSICheck MomCheckIn(string code)
        {
            if (Settings.MOM在线 != 1)
            {
                return new BFSICheck()
                {
                    SerialNo = code,
                    //Weight = Settings.一注前称重 + rand,
                    //Weight1 = Settings.一注后称重 + rand,
                    //Weight = Settings.一注前称重,
                    //Weight1 = Settings.一注后称重,
                    Result = 4
                };
            }
            bool flag = true;
            #region 装载数据过程
            //var djy = Material.Where(m => m.Location == Settings.注液机构编号).FirstOrDefault();
            //if (djy != null)
            //{
            //    if (float.TryParse(djy.Quantity, out float num))
            //    {
            //        if (num < 1) { UC_Operation.I.WriteLog($"电解液不足.{num}", "Warn"); flag = false; }
            //    }
            //    else
            //    {
            //        UC_Operation.I.WriteLog($"电解液数据错误.{Settings.注液机构编号}", "Warn"); flag = false;
            //    }
            //}
            //else
            //{
            //    UC_Operation.I.WriteLog($"电解液无上料数据.{Settings.注液机构编号}", "Warn"); flag = false;
            //}
            //var jd = Material.Where(m => m.Location == Settings.插钉机构编号).FirstOrDefault();
            //if (jd != null)
            //{
            //    if (float.TryParse(jd.Quantity, out float num))
            //    {
            //        if (num < 1) { UC_Operation.I.WriteLog($"胶钉不足.{num}", "Warn"); flag = false; }
            //    }
            //    else
            //    {
            //        UC_Operation.I.WriteLog($"胶钉数据错误.{Settings.注液机构编号}", "Warn"); flag = false;
            //    }
            //}
            //else
            //{
            //    UC_Operation.I.WriteLog($"胶钉无上料数据.{Settings.插钉机构编号}", "Warn"); flag = false;
            //}
            //if (!flag)
            //{
            //    return new Inj2Check()
            //    {
            //        SerialNo = code,
            //        Result = 5
            //    };
            //}

            //var check = _listCheckIn.Where(c => c.SerialNo == code).FirstOrDefault();
            //if (check == null) { check = new Inj2Check() { SerialNo = code }; _listCheckIn.Add(check); }

            //if (check.Result == 0) { Injection2Input_Async(code); check.Result = -1; }
            #endregion
            var check = _listCheckIn.Where(c => c.SerialNo == code).FirstOrDefault();
            if (check == null) { check = new BFSICheck() { SerialNo = code }; _listCheckIn.Add(check); }

            if (check.Result == 0) { Injection2Input_Async(code); check.Result = -1; }
            return check;
            //return /*check*/;
        }

        internal void Injection2Input_Async(string code)
        {
            Task.Run(() =>
            {
                try
                {
                    var req = new Injection2Input_Request();
                    req.SerialNos.Add(new Injection2Input_Request_SerialNo() { SerialNo = code });
                    var resJson = MomSendMessage("Injection2Input", JsonConvert.SerializeObject(req));
                    var resData = JsonConvert.DeserializeObject<Injection2Input_Response>(resJson.CommandResponseJson);
                    if (!resData.ResultFlag) { UC_Operation.I.WriteLog(resData.MOMMessage, "Warn"); }
                    else
                    {
                        foreach (var data in resData.SerialNos)
                        {
                            var check = _listCheckIn.Where(c => c.SerialNo == data.SerialNo).FirstOrDefault();
                            if (check == null) { UC_Operation.I.WriteLog($"Inj2Check接口数据缺失.{data.SerialNo}", "Warn"); continue; }
                            if (float.TryParse(data.Weight, out float weight)) { check.Weight = weight; } else { UC_Operation.I.WriteLog($"Inj2Check接口Weight错误{check.Weight}.{data.SerialNo}", "Warn"); }
                            if (float.TryParse(data.Weight1, out float weight1)) { check.Weight1 = weight1; } else { UC_Operation.I.WriteLog($"Inj2Check接口Weight1错误{check.Weight1}.{data.SerialNo}", "Warn"); }
                            check.Result = data.Result ? 1 : 2;
                        }
                    }
                    UC_Operation.I.WriteLog($"MOM二次注液进站:{resJson.CommandResponseJson}", "Info");
                }
                catch (Exception ex)
                {
                    ClearIn();
                    UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
                }
            });
        }

        List<OutCheck> _listCheckOut = new List<OutCheck>();
        //internal OutCheck MomCheckOut(CellData data)
        //{
        //    if (Settings.MOM在线 != 1)
        //    {
        //        return new OutCheck()
        //        {
        //            SerialNo = data.电芯码,
        //            Result = 4
        //        };
        //    }
        //    var check = _listCheckOut.Where(c => c.SerialNo == data.电芯码).FirstOrDefault();
        //    if (check == null) { check = new OutCheck() { SerialNo = data.电芯码 }; _listCheckOut.Add(check); }
        //    if (check.Result == 0) { check.Result = -1; CellOutput_Async(data); }
        //    return check;
        //}

        //internal void CellOutput_Async(CellData data)
        //{
        //    Task.Run(() =>
        //    {
        //        try
        //        {
        //            var serialNo = data.电芯码;
        //            var req = new CellOutput_Request();
        //            var cell = new CellOutput_SerialNo(serialNo, Settings.电芯型号, data);

        //            //电解液(KG)、胶钉(个) 计算数量
        //            var djy = Material.Where(m => m.Location == Settings.注液机构编号).FirstOrDefault();
        //            var jd = Material.Where(m => m.Location == Settings.插钉机构编号).FirstOrDefault();
        //            if (djy != null)
        //            {
        //                var f = data.实际注液量 > 0 ? Math.Round(data.实际注液量) / 1000 : 0;
        //                cell.MaterialInfo.Add(new CellOutput_SerialNo_MaterialInfo(djy.LabelNo, f.ToString()));
        //            }
        //            else
        //            {
        //                UC_Operation.I.WriteLog($"电解液无上料数据.{Settings.注液机构编号}", "Warn");
        //            }
        //            if (jd != null)
        //            {
        //                cell.MaterialInfo.Add(new CellOutput_SerialNo_MaterialInfo(jd.LabelNo, "1"));
        //            }
        //            else
        //            {
        //                UC_Operation.I.WriteLog($"胶钉无上料数据.{Settings.插钉机构编号}", "Warn");
        //            }

        //            req.SerialNos.Add(cell);
        //            var resJson = MomSendMessage("CellOutput", JsonConvert.SerializeObject(req));
        //            var resData = JsonConvert.DeserializeObject<CellOutput_Response>(resJson.CommandResponseJson);
        //            var check = _listCheckOut.Where(c => c.SerialNo == serialNo).FirstOrDefault();
        //            if (check == null) { UC_Operation.I.WriteLog($"出站check数据缺失.{serialNo}", "Warn"); }
        //            else
        //            {
        //                if (!resData.ResultFlag)
        //                {
        //                    UC_Operation.I.WriteLog($"{resData.MOMMessage}.{serialNo}", "Warn");
        //                    check.Result = 2;
        //                }
        //                else
        //                {
        //                    check.Result = 1;
        //                    UC_Operation.I.WriteLog($"二次注液出站.{serialNo}", "Info");
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            ClearOut();
        //            UC_Operation.I.WriteLog(ex.Message + "\r\n" + ex.StackTrace, "Error");
        //        }
        //    });
        //}
        #endregion
    }
    //class Inj2Check
    //{
    //    public string SerialNo { get; set; } = "";
    //    public float Weight { get; set; } = 0;
    //    public float Weight1 { get; set; } = 0;
    //    public int Result { get; set; } = 0;//-1:通讯中；0:等待；1:OK；2:NG；3:通讯失败；4:离线；5:电解液不足；6:胶钉不足
    //    public Inj2Check() { }

    //}
    /// <summary>
    /// MOM提前查询结果
    /// </summary>
    class BFSICheck
    {
        public string SerialNo { get; set; } = "";
        public float Weight { get; set; } = 0;
        public float Weight1 { get; set; } = 0;
        public int Result { get; set; } = 0;//-1:通讯中；0:等待；1:OK；2:NG；3:通讯失败；4:离线；5:电解液不足；6:胶钉不足
        public BFSICheck() { }

    }
    /// <summary>
    /// 出站MOM接口
    /// </summary>
    class OutCheck
    {
        public string SerialNo { get; set; } = "";
        public int Result { get; set; } = 0;//-1:通讯中;0:等待;1:OK;2:NG;3:通讯失败,4:离线
        public OutCheck() { }

    }
}
