using HslCommunication;
using HslCommunication.Profinet.Omron;
using Newtonsoft.Json;
using RinKit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using ZenergyBFSI.Model;
using ZenergyBFSI.Model.Device;
using ZenergyBFSI.View;

namespace ZenergyBFSI.Service
{
    public class PlcHandler
    {
        private static PlcHandler _instance;
        private static object _syncRoot = new object();

  
        private RTimer _timer = null;
        //private OmronCipNet _omronCipNet = null;
        public PLCOmronFins _omronFins = null;
        private List<PlcObj> _listPlcObj = null;
        private List<PlcBlock> _listPlcBlock = null;
        private Timer _timerLink;
        private bool _connected = false;
        private bool _Inited = false;
        public long TS { get; set; } = -1;

        private PlcHandler()
        {
        }

        public static PlcHandler I
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new PlcHandler();
                        }
                    }
                }
                return _instance;
            }
        }

        public void Init()
        {
            #region 旧链接示例
            //if (_omronCipNet == null)
            //{
            //    lock (_syncRoot)
            //    {
            //        if (_omronCipNet == null)
            //        {
            //            Task.Run(() =>
            //            {
            //                try
            //                {
            //                    PlcPrepare();
            //                    Connect_OmronCipNet();
            //                    _timerLink = new Timer(Settings.PLC循环等待 * 1000);
            //                    _timerLink.Elapsed += HeartBeat_Elapsed;
            //                    _timerLink.Start();
            //                    //UC_Operation.I.WriteLog("PLC连接成功!", "Info");
            //                }
            //                catch { UC_Operation.I.WriteLog("PLC连接失败!", "Error"); }
            //            });
            //        }
            //    }
            //}
            #endregion
            if (_omronFins == null)
            {
                lock (_syncRoot)
                {
                    if (_omronFins == null)
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                
                                PlcPrepare();
                                //Connect_OmronCipNet();
                                Connect_Omron();
                                _timerLink = new Timer(Settings.PLC循环等待 * 1000);
                                _timerLink.Elapsed += HeartBeat_Elapsed;
                                _timerLink.Start();
                                //UC_Operation.I.WriteLog("PLC连接成功!", "Info");
                            }
                            catch { UC_Operation.I.WriteLog("PLC连接失败!", "Error"); }
                        });
                    }
                }
            }
        }

        public void PlcPrepare()
        {
            Rdb.SelectList(out List<PlcObj> list);
            _instance._listPlcObj = list;
            var blocks = new List<PlcBlock>();
            foreach (var obj in list)
            {
                switch (obj.Mode)
                {
                    case 1:
                    case 2:
                        {
                            blocks.Add(new PlcBlock(obj.Adress, obj.Type, obj.Mode));
                        }
                        break;
                    case 4:
                    case 5:
                        {
                            var str = obj.Type.Split('|');
                            if (str.Length == 4 && blocks.Where(b => b.Adress == str[3]).Count() < 1)
                            {
                                blocks.Add(new PlcBlock(str, obj.Mode));
                            }
                        }
                        break;
                    default: UC_Operation.I.WriteLog($"PlcObj {obj.Adress} Mode{obj.Mode} 错误1", "Error"); break;
                }
                if (obj.Type == "String") obj.vString = "";//TODO
            }
            _instance._listPlcBlock = blocks;
        }

        public void Close()
        {
            _connected = false;
            if (_timer != null) _timer.Stop();
            Rlog.Warn("PlcObserver Close");
        }
        /// <summary>
        /// </summary>
        /// <returns></returns>
        public bool Connect_Omron()
        {
            if (_connected)
            { 
                UC_Operation.I.WriteLog("PLC重复连接!!!", "Warn");
                return true;
            }
            _omronFins = new PLCOmronFins(Settings.PLC_IP, Settings.PLC_Port);
            OperateResult res = _omronFins.omronFinsNet.ConnectServer();
            if (res.IsSuccess)
            {
                _connected = true;
                _timer = new RTimer(Settings.PLC循环等待, Timer_Elapsed);
                if (Settings.GetPower(0))
                    _timer.Start();
                UC_Operation.I.WriteLog($"PLC连接成功！OmronCipNet IP:{_omronFins.omronFinsNet.IpAddress}", "Info");
            }
            else
            {
                _connected = false;
                UC_Operation.I.WriteLog($"PLC连接失败！OmronCipNet IP:{_omronFins.omronFinsNet.IpAddress}", "Warn");
            }
            return res.IsSuccess;
        }


        private void HeartBeat_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_connected)
            {
                UC_Operation.I.WriteLog($"PLC重连警告！", "Warn");
                if (_timer != null) _timer.Stop();
                lock (_syncRoot)
                {
                    //Connect_OmronCipNet();
                    Connect_Omron();
                }
            }

        }
        /// <summary>
        /// PLC同步数据
        /// </summary>
        private void Timer_Elapsed(object ticks)
        {
            if (!_connected)
            {
                return;
            }
            else
            {
                lock (_syncRoot)
                {
                    if ((long)ticks > Settings.PLC超时警告)//判断超时
                    {
                        UC_Operation.I.WriteLog($"PLC通讯延时警告！{(long)ticks / 10000}ms", "Warn");
                    }
                    try
                    {
                        bool flag = true;
                        TS = DataHelper.TimeMS;
                        foreach (var block in _instance._listPlcBlock)
                        {
                            //块Mode
                            switch (block.Mode)
                            {
                                case 1:
                                    {
                                        switch (block.BlockType)
                                        {
                                            case "UInt16":
                                                {
                                                    var obj = _instance._listPlcObj.Where(o => o.Adress == block.Adress).FirstOrDefault();
                                                    OperateResult<ushort> readResult = _omronFins.omronFinsNet.ReadUInt16(obj.Adress);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        obj.vInt = readResult.Content;
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{obj.Adress} : {obj.vInt}");
                                                    }
                                                    else
                                                    {
                                                        UC_Operation.I.WriteLog($"读PLC失败1.{obj.Adress}:{readResult.Message}", "Error");
                                                        flag = false;
                                                    }
                                                }
                                                break;
                                            case "Real":
                                                {
                                                    var obj = _instance._listPlcObj.Where(o => o.Adress == block.Adress).FirstOrDefault();
                                                    OperateResult<float> readResult = _omronFins.omronFinsNet.ReadFloat(obj.Adress);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        obj.vFloat = readResult.Content;
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{obj.Adress} : {obj.vFloat}");
                                                    }
                                                    else
                                                    {
                                                        UC_Operation.I.WriteLog($"读PLC失败1.{obj.Adress}:{readResult.Message}", "Error");
                                                        flag = false;
                                                    }
                                                }
                                                break;
                                            case "String":
                                                {
                                                    var obj = _instance._listPlcObj.Where(o => o.Adress == block.Adress).FirstOrDefault();
                                                    OperateResult<string> readResult = _omronFins.omronFinsNet.ReadString(obj.Adress, 100);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        obj.vString = readResult.Content;
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{obj.Adress} : {obj.vString}");
                                                    }
                                                    else
                                                    {
                                                        UC_Operation.I.WriteLog($"读PLC失败2.{obj.Adress}:{readResult.Message}", "Error");
                                                        flag = false;
                                                    }
                                                }
                                                break;
                                            default: UC_Operation.I.WriteLog($"PlcBlock {block.Adress} BlockType.{block.BlockType} 错误2", "Error"); break;
                                        }
                                    }
                                    break;
                                case 2:
                                    {
                                        switch (block.BlockType)
                                        {
                                            case "UInt16":
                                                {
                                                    var obj = _instance._listPlcObj.Where(o => o.Adress == block.Adress).FirstOrDefault();
                                                    OperateResult<ushort> readResult = _omronFins.omronFinsNet.ReadUInt16(obj.Adress);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{obj.Adress} : {obj.vInt}");
                                                        if (!_Inited)
                                                        {
                                                            obj.vInt = readResult.Content;
                                                        }
                                                        if (obj.vInt != readResult.Content)
                                                        {
                                                            //OperateResult writeResult = _omronFins.omronFinsNet.Write(obj.Adress, (ushort)obj.vInt);
                                                            //if (writeResult.IsSuccess)
                                                            //{
                                                            //    if (Settings.PLC日志 > 0) UC_Operation.I.WriteLog($"写PLC成功.{obj.Adress} => {obj.vInt}");
                                                            //}
                                                            //else
                                                            //{
                                                            //    UC_Operation.I.WriteLog($"写PLC失败1.{obj.Adress}:{writeResult.Message}", "Error");
                                                            //    flag = false;
                                                            //}
                                                        }
                                                        else
                                                        {
                                                            obj.oSync = true;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        UC_Operation.I.WriteLog($"读PLC失败3.{obj.Adress}:{readResult.Message}", "Error");
                                                        flag = false;
                                                    }
                                                }
                                                break;
                                            case "Real":
                                                {
                                                    var obj = _instance._listPlcObj.Where(o => o.Adress == block.Adress).FirstOrDefault();
                                                    OperateResult<float> readResult = _omronFins.omronFinsNet.ReadFloat(obj.Adress);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{obj.Adress} : {obj.vFloat}");
                                                        if (!_Inited)
                                                        {
                                                            obj.vFloat = readResult.Content;
                                                        }
                                                        if (obj.vFloat != readResult.Content)
                                                        {
                                                            OperateResult writeResult = _omronFins.omronFinsNet.Write(obj.Adress, obj.vFloat);
                                                            if (writeResult.IsSuccess)
                                                            {
                                                                if (Settings.PLC日志 > 0) UC_Operation.I.WriteLog($"写PLC成功.{obj.Adress} => {obj.vFloat}");
                                                            }
                                                            else
                                                            {
                                                                UC_Operation.I.WriteLog($"写PLC失败2.{obj.Adress}:{writeResult.Message}", "Error");
                                                                flag = false;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            obj.oSync = true;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        UC_Operation.I.WriteLog($"读PLC失败3.{obj.Adress}:{readResult.Message}", "Error");
                                                        flag = false;
                                                    }
                                                }
                                                break;
                                            case "String":
                                                //String长度暂定不超过100
                                                {
                                                    var obj = _instance._listPlcObj.Where(o => o.Adress == block.Adress).FirstOrDefault();
                                                    OperateResult<string> readResult = _omronFins.omronFinsNet.ReadString(obj.Adress,50);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{obj.Adress} : {obj.vString}");
                                                        if (!_Inited)
                                                        {
                                                            obj.vString = readResult.Content;
                                                            var str = obj.vString;
                                                        }
                                                        if (obj.vString != readResult.Content)
                                                        {
                                                            //OperateResult writeResult = _omronFins.omronFinsNet.Write(obj.Adress, obj.vString);
                                                            //if (writeResult.IsSuccess)
                                                            //{
                                                            //    if (Settings.PLC日志 > 0) UC_Operation.I.WriteLog($"写PLC成功.{obj.Adress} => {obj.vString}");
                                                            //}
                                                            //else
                                                            //{
                                                            //    UC_Operation.I.WriteLog($"写PLC失败3.{obj.Adress}:{writeResult.Message}", "Error");
                                                            //    flag = false;
                                                            //}
                                                        }
                                                        else
                                                        {
                                                            obj.oSync = true;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        UC_Operation.I.WriteLog($"读PLC失败4.{obj.Adress}:{readResult.Message}", "Error");
                                                        flag = false;
                                                    }
                                                }
                                                break;
                                            case "UTF8":
                                                {
                                                    var obj = _instance._listPlcObj.Where(o => o.Adress == block.Adress).FirstOrDefault();
                                                    OperateResult<string> readResult = _omronFins.omronFinsNet.ReadString(obj.Adress,100,Encoding.UTF8);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{obj.Adress} : {obj.vString}");
                                                        if (!_Inited)
                                                        {
                                                            obj.vString = readResult.Content;
                                                        }
                                                        if (obj.vString != readResult.Content)
                                                        {
                                                            OperateResult writeResult = _omronFins.omronFinsNet.Write(obj.Adress, obj.vString, Encoding.UTF8);
                                                            if (writeResult.IsSuccess)
                                                            {
                                                                if (Settings.PLC日志 > 0) UC_Operation.I.WriteLog($"写PLC成功.{obj.Adress} => {obj.vString}");
                                                            }
                                                            else
                                                            {
                                                                UC_Operation.I.WriteLog($"写PLC失败3.{obj.Adress}:{writeResult.Message}", "Error");
                                                                flag = false;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            obj.oSync = true;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        UC_Operation.I.WriteLog($"读PLC失败4.{obj.Adress}:{readResult.Message}", "Error");
                                                        flag = false;
                                                    }
                                                }
                                                break;
                                            default: UC_Operation.I.WriteLog($"PlcBlock {block.Adress} BlockType.{block.BlockType} 错误3", "Error"); break;
                                        }
                                    }
                                    break;
                                case 4:
                                    {
                                        //块Type
                                        switch (block.BlockType)
                                        {
                                            case "Byte":
                                                {
                                                    //块读取
                                                    OperateResult<byte[]> readResult = _omronFins.omronFinsNet.Read(block.Adress, block.BlockLength);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{block.Adress}:{DataHelper.ToBitString(readResult.Content)}");
                                                        var plcData = readResult.Content;
                                                        if (plcData == null) { UC_Operation.I.WriteLog($"PlcData Null！{block.Adress}", "Warn"); continue; }
                                                        //Obj解析
                                                        if (block.ObjType == "Bit")
                                                        {
                                                            //PC赋值
                                                            var bits = new BitArray(plcData);
                                                            for (int i = 0; i < bits.Length; i++)
                                                            {
                                                                var adress = $"{block.Adress}.{i}";
                                                                var obj = _instance._listPlcObj.Where(o => o.Adress == adress).FirstOrDefault();
                                                                if (obj == null) { UC_Operation.I.WriteLog($"无效adress！{adress}", "Error"); continue; }
                                                                obj.vBool = bits[i];
                                                            }
                                                        }
                                                        else
                                                        {
                                                            UC_Operation.I.WriteLog($"无效ObjType1.{block.Adress} {block.ObjType}", "Error");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        flag = false;
                                                        UC_Operation.I.WriteLog($"读PLC失败5.{block.Adress}:{readResult.Message}", "Error");
                                                    }
                                                }
                                                break;
                                            case "UInt16":
                                                {
                                                    //块读取
                                                    OperateResult<ushort[]> readResult = _omronFins.omronFinsNet.ReadUInt16(block.Adress, block.BlockLength);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{block.Adress}:{DataHelper.ToIntString(readResult.Content)}");
                                                        //Obj解析
                                                        if (block.ObjType == "UInt16")
                                                        {
                                                            //PC赋值
                                                            for (int i = 0; i < readResult.Content.Length; i++)
                                                            {
                                                                var adress = $"{block.Adress}.{i}";
                                                                var obj = _instance._listPlcObj.Where(o => o.Adress == adress).FirstOrDefault();
                                                                if (obj == null) { UC_Operation.I.WriteLog($"无效adress！{adress}", "Error"); continue; }
                                                                obj.vInt = readResult.Content[i];
                                                            }
                                                        }
                                                        else
                                                        {
                                                            UC_Operation.I.WriteLog($"无效ObjType2.{block.Adress} {block.ObjType}", "Error");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        flag = false;
                                                        UC_Operation.I.WriteLog($"读PLC失败6.{block.Adress}:{readResult.Message}", "Error");
                                                    }
                                                }
                                                break;
                                            case "Real":
                                                {
                                                    //块读取
                                                    OperateResult<float[]> readResult = _omronFins.omronFinsNet.ReadFloat(block.Adress, block.BlockLength);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{block.Adress}:{DataHelper.ToFloatString(readResult.Content)}");
                                                        //Obj解析
                                                        if (block.ObjType == "Real")
                                                        {
                                                            //PC赋值
                                                            for (int i = 0; i < readResult.Content.Length; i++)
                                                            {
                                                                var adress = $"{block.Adress}.{i}";
                                                                var obj = _instance._listPlcObj.Where(o => o.Adress == adress).FirstOrDefault();
                                                                if (obj == null) { UC_Operation.I.WriteLog($"无效adress！{adress}", "Error"); continue; }
                                                                obj.vFloat = readResult.Content[i];
                                                            }
                                                        }
                                                        else
                                                        {
                                                            UC_Operation.I.WriteLog($"无效ObjType3.{block.Adress} {block.ObjType}", "Error");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        flag = false;
                                                        UC_Operation.I.WriteLog($"读PLC失败7.{block.Adress}:{readResult.Message}", "Error");
                                                    }
                                                }
                                                break;
                                            default:
                                                {
                                                    UC_Operation.I.WriteLog($"PlcBlock {block.Adress} BlockType.{block.BlockType} 错误4", "Error");
                                                }
                                                break;
                                        }
                                    }
                                    break;
                                case 5:
                                    {
                                        switch (block.BlockType)
                                        {
                                            case "Byte":
                                                {
                                                    var same = true;
                                                    //PLC取值
                                                    byte[] plcData = null;
                                                    OperateResult<byte[]> readResult = _omronFins.omronFinsNet.Read(block.Adress, block.BlockLength);
                                                    if (readResult.IsSuccess)
                                                    {
                                                        if (Settings.PLC日志 > 1) UC_Operation.I.WriteLog($"{block.Adress}:{DataHelper.ToBitString(readResult.Content)}");
                                                        plcData = readResult.Content;
                                                    }
                                                    else
                                                    {
                                                        flag = false;
                                                        Rlog.Warn($"{readResult.Message}");
                                                    }
                                                    if (plcData == null) { UC_Operation.I.WriteLog($"PlcData Null！{block.Adress}", "Warn"); continue; }

                                                    //PC取值
                                                    byte[] pcData = null;
                                                    var objList = _instance._listPlcObj.Where(o => o.Type == $"{block.BlockType}|{block.BlockLength}|{block.ObjType}|{block.Adress}").ToList();
                                                    var bits = new List<bool>();
                                                    if (block.ObjType == "Bit")
                                                    {
                                                        foreach (var obj in objList) { bits.Add(obj.vBool); }
                                                    }
                                                    else
                                                    {
                                                        UC_Operation.I.WriteLog($"无效ObjType4.{block.Adress} {block.ObjType}", "Error");
                                                    }
                                                    pcData = DataHelper.BitsToBytes(bits);
                                                    //PLC&PC比较
                                                    if (plcData.Length != pcData.Length) { UC_Operation.I.WriteLog($"PLC&PC数据长度不一致！{block.Adress}", "Error"); continue; }
                                                    for (int i = 0; i < pcData.Length; i++)
                                                    {
                                                        if (plcData[i] != pcData[i]) same = false;
                                                    }
                                                    if (!same)
                                                    {
                                                        ////写值
                                                        //OperateResult writeResult = _omronFins.omronFinsNet.Write(block.Adress, pcData);
                                                        //if (writeResult.IsSuccess)
                                                        //{
                                                        //    if (Settings.PLC日志 > 0) UC_Operation.I.WriteLog($"写PLC成功.{block.Adress}");
                                                        //}
                                                        //else
                                                        //{
                                                        //    flag = false;
                                                        //    UC_Operation.I.WriteLog($"写PLC失败4.{block.Adress}:{writeResult.Message}", "Error");
                                                        //}
                                                    }
                                                }
                                                break;
                                            case "UInt16":
                                                {
                                                    UC_Operation.I.WriteLog($"PlcBlock {block.Adress} BlockType.{block.BlockType} 错误8", "Error");
                                                }
                                                break;
                                            case "Real":
                                                {
                                                    UC_Operation.I.WriteLog($"PlcBlock {block.Adress} BlockType.{block.BlockType} 错误8", "Error");
                                                }
                                                break;
                                            default:
                                                {
                                                    UC_Operation.I.WriteLog($"PlcBlock {block.Adress} BlockType.{block.BlockType} 错误8", "Error");
                                                }
                                                break;
                                        }
                                    }
                                    break;
                                default: UC_Operation.I.WriteLog($"PlcBlock {block.Adress} Mode{block.Mode} 错误6", "Error"); break;
                            }
                        }

                        if (flag)
                        {
                            _Inited = true;
                        }
                        else
                        {
                            UC_Operation.I.WriteLog($"PLC数据同步错误！", "Warn");
                        }
                    }
                    catch (Exception ex)
                    {
                        Rlog.Error($"PLC通讯异常！ {ex.Message}\r\n{ex.StackTrace}");
                        //if (ex.Message.Contains("XXX"))
                        //{
                        //    connected = false;
                        //}
                    }

                }
            }
        }
        public PlcObj GetOBJ(string name)
        {
            return _listPlcObj.Where(o => o.Name == name).FirstOrDefault();
        }
        public List<PlcObj> GetOBJ()
        {
            return _listPlcObj;
        }
        public void ReadTest()
        {
            var operateResult = _omronFins.omronFinsNet.Read("PLC_TO_PC", 1);
            //OperateResult<bool[]> operateResult = omronCipNet.ReadBool("PLC_TO_SR4", 76 * 8);
            if (operateResult.IsSuccess)
            {
                UC_Operation.I.WriteLog($"{JsonConvert.SerializeObject(operateResult.Content)}", "Info");
            }
            else
            {
                UC_Operation.I.WriteLog($"{operateResult.Message}", "Warn");
            }
        }

        public void WriteTest()
        {
            OperateResult operateResult = _omronFins.omronFinsNet.Write("PC_TO_PLC", new ushort[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            if (operateResult.IsSuccess)
            {
                UC_Operation.I.WriteLog($"{operateResult.Message}", "Info");
            }
            else
            {
                UC_Operation.I.WriteLog($"{operateResult.Message}", "Warn");
            }
        }


    }
}
