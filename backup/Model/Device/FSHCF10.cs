using HslCommunication.ModBus;
using RinKit;
using System;
using System.Collections;
using System.Threading.Tasks;
using ZenergyBFSI.View;

namespace ZenergyBFSI.Model
{
    //FSH_CF10
    internal class FSHCF10
    {
        #region adress
        /*
        序号 标签	数据类型	Modbus地址	读/写	单位	下限	上限
        1 	1#吐出量	Float	4x 7000	读/写	克	0 	9999.99 
        2 	1#流量	Float	4x 7002	读/写	克/秒	1 	15 
        3 	1#补偿量	Float	4x 7004	读/写	克	-99.99 	99.99 
        4 	1#排气冲程	Float	4x 7006	读/写	圈	0.01 	9999.99 
        5 	1#排气转速	Float	4x 7008	读/写	转/分钟	10 	240 
        6 	IP地址第4位	UInt16	4x 7010	只读		0 	255 
        7 	IP地址第3位	UInt16	4x 7011	只读		0 	255 
        8 	IP地址第2位	UInt16	4x 7012	只读		0 	255 
        9 	IP地址第1位	UInt16	4x 7013	只读		0 	255 
        10 	1#系数	Float	4x 7014	只读		0.5 	1.5 
        11 	1#排气当前冲程	Float	4x 7016	只读	圈	0 	9999.99 
        12 	未使用		4x 7018				
        13 	1#写入吐出量	Float	4x 7020	只写	克	0 	9999.99 
        14 	1#写入补偿量	Float	4x 7022	只写	克	-99.99 	99.99 
        15 	2#吐出量	Float	4x 7024	读/写	克	0 	9999.99 
        16 	2#流量	Float	4x 7026	读/写	克/秒	1 	15 
        17 	2#补偿量	Float	4x 7028	读/写	克	-99.99 	99.99 
        18 	2#排气冲程	Float	4x 7030	读/写	圈	0.01 	9999.99 
        19 	2#排气转速	Float	4x 7032	读/写	转/分钟	10 	240 
        20 	2#系数	Float	4x 7034	只读		0.5 	1.5 
        21 	2#排气当前冲程	Float	4x 7036	只读	圈	0.0 	10000.0 
        22 	未使用		4x 7038				
        23 	2#写入吐出量	Float	4x 7040	只写	克	0.0 	10000.0 
        24 	2#写入补偿量	Float	4x 7042	只写	克	-100.0 	100.0 

        注意：Modbus地址是字地址，offset是byte，二倍
         */
        static string Adress_字数据 = "7000";
        static int Offset_吐出量1 = 0;
        static int Offset_流量1 = 4;
        static int Offset_补偿量1 = 8;
        static int Offset_排气冲程1 = 12;
        static int Offset_排气转速1 = 16;
        static int Offset_系数1 = 28;
        static int Offset_排气当前冲程1 = 32;
        static int Offset_写入吐出量1 = 40;
        static int Offset_写入补偿量1 = 44;
        static int Offset_吐出量2 = 48;
        static int Offset_流量2 = 52;
        static int Offset_补偿量2 = 56;
        static int Offset_排气冲程2 = 60;
        static int Offset_排气转速2 = 64;
        static int Offset_系数2 = 68;
        static int Offset_排气当前冲程2 = 72;
        static int Offset_写入吐出量2 = 80;
        static int Offset_写入补偿量2 = 84;
        static string Adress_流量 = "7002";
        static string Adress_写入吐出量 = "7020";
        static string Adress_写入补偿量 = "7022";
        static string Adress_命令 = "100";
        static string Adress_状态 = "101";
        #endregion

        public FSHCF10()
        {
        }

        ModbusTcpNet _tcp = null;
        Task _task;
        object _syncRoot = new object();
        int _state = 0;//-999:断开；-200:断开；-100:注液中；-1:注液启动；0;空闲；1:完成；
        int _count = 0;
        int _tpg = 0;//注液1g耗时

        public int State { get => _state; set => _state = value; }
        /// <summary>
        /// 初始化
        /// </summary>
        public void Connect(string ip, int port, bool listen = false)
        {
            try
            {
                _tcp = new ModbusTcpNet(ip, port);
                _task = new Task(Thread);
                if (listen)
                {
                    _task.Start();
                }
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"注控Connect异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
            }
        }

        /// <summary>
        /// 信息接收线程函数
        /// </summary>
        private void Thread()
        {
            while (true)
            {
                lock (_syncRoot)
                {
                    try
                    {
                        var res = _tcp.Read(Adress_状态, 1);
                        if (res.IsSuccess)
                        {
                            var bits = new BitArray(res.Content);
                            var ok = bits[2];
                            if (_state != 0)
                            {
                                UC_Operation.I.WriteLog($"{_tcp.IpAddress}:{DataHelper.ToBitString(res.Content)}{ok},{_state}", "Debug");
                            }
                            if (_count > 100)
                            {
                                _tpg = GetTPG();
                                Rlog.Debug($"{_tcp.IpAddress}.TPG:{_tpg}");
                                _count = 0;
                            }
                            else
                            {
                                _count++;
                            }
                        }
                        else
                        {
                            _state = -999;
                        }
                    }
                    catch (Exception ex)
                    {
                        _state = -200;
                        Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
                    }
                    finally
                    {
                        System.Threading.Thread.Sleep(Settings.注控通讯等待);
                    }
                }
            }
        }

        public bool Inject(float val)
        {
            if (val <= 0 || val > 100)
            {
                _state = 1;
                return false;
            }
            else
            {
                lock (_syncRoot)
                {
                    _state = -1;
                    var res1 = _tcp.Write(Adress_写入吐出量, val);
                    if (!res1.IsSuccess)
                    {
                        UC_Operation.I.WriteLog(_tcp.IpAddress + res1.Message, "Error"); return false;
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"{_tcp.IpAddress}:注液量{val}g", "Success");
                    }
                    System.Threading.Thread.Sleep(200);
                    var res2 = _tcp.Write(Adress_命令, 1);
                    int wt = Convert.ToInt32(val * _tpg) + 1000;
                    if (!res2.IsSuccess)
                    {
                        UC_Operation.I.WriteLog(_tcp.IpAddress + res2.Message, "Error"); return false;
                    }
                    else
                    {
                        UC_Operation.I.WriteLog($"{_tcp.IpAddress}:注液时间{wt}ms", "Success");
                    }
                    System.Threading.Thread.Sleep(wt);
                    _state = 1;
                    var res3 = _tcp.Write(Adress_命令, 0);
                    if (!res3.IsSuccess)
                    {
                        UC_Operation.I.WriteLog(_tcp.IpAddress + res2.Message, "Error"); return false;
                    }
                    else
                    {
                        UC_Operation.I.WriteLog(_tcp.IpAddress + ":注液完成", "Info");
                    }
                }
            }
            return true;
        }

        public bool Reset()
        {
            var res1 = _tcp.Write(Adress_命令, 0);
            if (!res1.IsSuccess)
            {
                UC_Operation.I.WriteLog(_tcp.IpAddress + res1.Message, "Error"); return false;
            }
            System.Threading.Thread.Sleep(100);
            _state = 0;
            UC_Operation.I.WriteLog(_tcp.IpAddress + ":复位", "Info");
            return true;
        }
        public int GetTPG()
        {
            var res = _tcp.Read(Adress_字数据, 24);
            if (res.IsSuccess)
            {
                var ll = BitConverter.ToSingle(FlipBADC(res.Content, Offset_流量1), 0);
                return 1000 / Convert.ToInt32(ll);
            }
            else
            {
                throw new Exception(res.Message);
            }
        }

        public void ReadData(ref DataINJ state)
        {
            var res = _tcp.Read(Adress_字数据, 24);
            if (res.IsSuccess)
            {
                state.Time = DataHelper.TimeMS;
                state.吐出量 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_吐出量1), 0);
                state.流量 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_流量1), 0);
                state.补偿量 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_补偿量1), 0);
                state.排气冲程 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_排气冲程1), 0);
                state.排气转速 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_排气转速1), 0);
                state.系数 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_系数1), 0);
                state.排气当前冲程 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_排气当前冲程1), 0);
                state.写入吐出量 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_写入吐出量1), 0);
                state.写入补偿量 = BitConverter.ToSingle(FlipBADC(res.Content, Offset_写入补偿量1), 0);
            }
            else
            {
                throw new Exception(res.Message);
            }
        }

        public void SetData(ref DataINJ state, int index = 0)
        {
            switch (index)
            {
                case 0:
                    {
                        var res = _tcp.Write(Adress_写入吐出量, state.写入吐出量);
                        if (!res.IsSuccess) { throw new Exception(res.Message); }
                    }
                    break;
                case 1:
                    {
                        var res = _tcp.Write(Adress_写入补偿量, state.写入补偿量);
                        if (!res.IsSuccess) { throw new Exception(res.Message); }
                    }
                    break;
            }
        }

        public bool CMD(ushort v)
        {
            var res = _tcp.Write(Adress_命令, v);
            if (!res.IsSuccess)
            {
                throw new Exception(res.Message);
            }
            return res.IsSuccess;
        }

        byte[] test = new byte[] { 0xE6, 0x66, 0x42, 0xF6 }; //230 102 66 246

        public void Test()
        {
            //var r1 = _tcp.ReadInt16("1");
            var data = BitConverter.GetBytes(123.45f);//102 230 66 246
            var r4 = _tcp.Write("3", FlipBADC(data));
            var r3 = _tcp.Read("3", 2);
            UC_Operation.I.WriteLog($"r3.{BitConverter.ToSingle(FlipBADC(r3.Content), 0)}", "Info");
        }

        public string GetDataStr(byte[] data)
        {
            string res = "";
            res += $"吐出量1:{BitConverter.ToSingle(FlipBADC(data, Offset_吐出量1), 0)} \r\n";
            res += $"流量1:{BitConverter.ToSingle(FlipBADC(data, Offset_流量1), 0)} \r\n";
            res += $"补偿量1:{BitConverter.ToSingle(FlipBADC(data, Offset_补偿量1), 0)} \r\n";
            res += $"排气冲程1:{BitConverter.ToSingle(FlipBADC(data, Offset_排气冲程1), 0)} \r\n";
            res += $"排气转速1:{BitConverter.ToSingle(FlipBADC(data, Offset_排气转速1), 0)} \r\n";
            res += $"系数1:{BitConverter.ToSingle(FlipBADC(data, Offset_系数1), 0)} \r\n";
            res += $"排气当前冲程1:{BitConverter.ToSingle(FlipBADC(data, Offset_排气当前冲程1), 0)} \r\n";
            res += $"写入吐出量1:{BitConverter.ToSingle(FlipBADC(data, Offset_写入吐出量1), 0)} \r\n";
            res += $"写入补偿量1:{BitConverter.ToSingle(FlipBADC(data, Offset_写入补偿量1), 0)} \r\n";
            res += $"吐出量2:{BitConverter.ToSingle(FlipBADC(data, Offset_吐出量2), 0)} \r\n";
            res += $"流量2:{BitConverter.ToSingle(FlipBADC(data, Offset_流量2), 0)} \r\n";
            res += $"补偿量2:{BitConverter.ToSingle(FlipBADC(data, Offset_补偿量2), 0)} \r\n";
            res += $"排气冲程2:{BitConverter.ToSingle(FlipBADC(data, Offset_排气冲程2), 0)} \r\n";
            res += $"排气转速2:{BitConverter.ToSingle(FlipBADC(data, Offset_排气转速2), 0)} \r\n";
            res += $"系数2:{BitConverter.ToSingle(FlipBADC(data, Offset_系数2), 0)} \r\n";
            res += $"排气当前冲程2:{BitConverter.ToSingle(FlipBADC(data, Offset_排气当前冲程2), 0)} \r\n";
            res += $"写入吐出量2:{BitConverter.ToSingle(FlipBADC(data, Offset_写入吐出量2), 0)} \r\n";
            res += $"写入补偿量2:{BitConverter.ToSingle(FlipBADC(data, Offset_写入补偿量2), 0)} \r\n";
            return res;
        }

        public static byte[] FlipBADC(byte[] data, int start = 0) { return new byte[] { data[start + 1], data[start], data[start + 3], data[start + 2] }; }
        public static byte[] FlipCDAB(byte[] data, int start = 0) { return new byte[] { data[start + 2], data[start + 3], data[start], data[start + 1] }; }
        public static byte[] FlipDCBA(byte[] data, int start = 0) { return new byte[] { data[start + 3], data[start + 2], data[start + 1], data[start] }; }

    }
}
