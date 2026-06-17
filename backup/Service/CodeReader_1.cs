using RinKit;
using System;
using System.Threading.Tasks;
using ZenergyBFSI.Model;
using ZenergyBFSI.View;

namespace ZenergyBFSI.Service
{
    /// <summary>
    /// 读卡器控制类
    /// 在本项目中不再被使用
    /// </summary>
    public sealed class CodeReader_1
    {
        private static CodeReader_1 _instance;
        private static object _syncRoot = new object();
        private static SR1000 _device = new SR1000();
        private static string _code = "";
        private static bool _linked = false;


        private CodeReader_1() { }

        public static CodeReader_1 I
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null) _instance = new CodeReader_1();
                    }
                }
                return _instance;
            }
        }

        public bool Link() { return _linked; }

        public void SetCode(string code)
        {
            _code = code;
            UC_Operation.I.WriteLog($"通道1扫码结果:{code}");
        }
        public string GetCode()
        {
            return _code;
        }
        internal void ClearCode()
        {
            _device.Cancel();
            _code = "";
            UC_Operation.I.WriteLog($"通道1扫码结果清理");
        }
        public void Init()
        {
            UC_Operation.I.WriteLog("扫码枪1初始化...", "Debug");
            Task.Run(() =>
            {
                //try
                //{
                //    _device.Connect(Settings.扫码枪1IP, Settings.扫码枪1Port, SetCode);
                //    UC_Operation.I.WriteLog("扫码枪1连接成功!", "Info");
                //    _linked = true;
                //}
                //catch { UC_Operation.I.WriteLog("扫码枪1连接失败!", "Error"); }
            });
        }

        public int Scan()
        {
            int count = 0;
            try
            {
                count = _device.Scan();
                UC_Operation.I.WriteLog($"扫码枪1扫码");
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"扫码枪1Scan异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
            }
            return count;
        }

        public void Cancel()
        {
            try
            {
                _device.Cancel();
                UC_Operation.I.WriteLog($"扫码枪1Cancel");
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"扫码枪1Cancel异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
            }
        }

        public void Close()
        {
            try
            {
                _device.Disconnect();
                _instance = null;
                Rlog.Warn("扫码枪1 Close");
            }
            catch (Exception ex)
            {
                UC_Operation.I.WriteLog($"扫码枪1Close异常！{ex.Message}\r\n {ex.StackTrace}", "Error");
            }
        }
    }
}
