using DevExpress.XtraPrinting.Native;
using RinKit;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZenergyBFSI.Model
{
    internal class SR1000
    {

        private Socket _socketCodeReader;
        private Task _taskCodeReader;
        private bool _taskON = false;
        private bool _scanning = false;
        private int _scanningCount = 0;
        public delegate void ResultCode(string code);
        private ResultCode SetCode;

        public SR1000()
        {
        }

        public void Connect(string ip, int port, ResultCode setcode)
        {
            SetCode = setcode;
            IPEndPoint point = new IPEndPoint(IPAddress.Parse(ip), port);
            _socketCodeReader = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socketCodeReader.Connect(point);
            _taskCodeReader = new Task(Thread_CodeReader);
            _taskCodeReader.Start();
            _taskON = true;
        }

        public void Disconnect()
        {
            _socketCodeReader.Close();
            _taskON = false;
        }

        /// <summary>
        /// 读码器信息接收线程函数
        /// </summary>
        private void Thread_CodeReader()
        {
            //TODO 字节处理优化
            while (_taskON)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int len = _socketCodeReader.Receive(buffer);
                    Rlog.Trace("Socket_CodeReader.Receive" + len);
                    if (len > 0)
                    {
                        string words = Encoding.ASCII.GetString(buffer, 0, len);
                        if (!string.IsNullOrEmpty(words))
                        {
                            if (words.Contains("CANCEL,"))
                            {
                                Rlog.Trace("CANCEL");
                            }
                            else if (words.Contains(","))
                            {
                                Rlog.Warn("SR1000未识别指令:" + words);
                            }
                            else
                            {
                                //words = words.Replace('\n', '\0');
                                if (words == "ERROR") { Rlog.Warn($"SR1000结果:{words}"); }
                                else
                                {
                                    if (words.Contains("\r"))
                                    {
                                        words = words.Split('\r')[0];
                                    }
                                    words = words.Trim();
                                    if (words.Contains(":"))
                                    {
                                        words = words.Split(':')[0];
                                        //TODO 扫码抢数据
                                    }
                                    SetCode(words);
                                    _scanningCount = 0;
                                    Rlog.Info($"SR1000结果:{words}");
                                }
                            }
                        }
                        _scanning = false;
                    }
                }
                catch (Exception ex)
                {
                    Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
                    Thread.Sleep(Settings.错误等待);
                }
                finally
                {
                    if (_scanning) _scanningCount++;
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// 执行扫码
        /// </summary>
        public int Scan()
        {
            if (!_scanning)
            {
                _scanning = true;
                SendCmd_CodeReader("LON");
                Rlog.Trace("扫码中...");
            }
            else
            {
                //TODO校验扫码抢是否在扫码中
                if (_scanningCount > 10) Cancel();
            }
            return _scanningCount;
        }

        public void ScanManual()
        {
            SetCode("");
            SendCmd_CodeReader("LON");
            Rlog.Trace("手动扫码");
        }


        public void Cancel()
        {
            SetCode("");
            SendCmd_CodeReader("CANCEL");
            Rlog.Trace("取消扫码");
        }

        /// <summary>
        /// 读码器指令下发
        /// </summary>
        private void SendCmd_CodeReader(string cmd)
        {
            try
            {
                Rlog.Trace($"CodeReader SendCmd : {cmd} .");
                byte[] buffer_Cmd = Encoding.ASCII.GetBytes(cmd);
                byte[] buffer = ByteHelper.ByteJoint(buffer_Cmd, new byte[] { 0x0D });
                _socketCodeReader.Send(buffer);
            }
            catch (Exception ex)
            {
                Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

    }
}
