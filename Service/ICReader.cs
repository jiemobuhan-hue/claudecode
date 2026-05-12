using RinKit;
using System;
using System.Threading;
using System.Threading.Tasks;
using ZenergyBFSI.View;

namespace ZenergyBFSI.Model
{
    public class ICReader
    {

        /// <summary>
        /// IC读卡器线程
        /// </summary>
        Task Task_ICReader;
        /// <summary>
        /// 读卡器开关
        /// </summary>
        bool run = false;

        public bool Run { get => run; set => run = value; }
        /// </summary>
        /// <summary>
        /// IC读卡器实例对象
        /// </summary>
        private static ICReader Instance;
        private static object syncRoot = new object();
        private ICReader() { }

        public static ICReader I
        {
            get
            {
                if (Instance == null)
                {
                    lock (syncRoot)
                    {
                        if (Instance == null) Instance = new ICReader();
                    }
                }
                return Instance;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public bool Init()
        {
            Task_ICReader = new Task(Thread_ICReader);
            Task_ICReader.Start();
            Run = true;
            UC_Operation.I.WriteLog($"读卡器初始化成功", "Info");
            return Run;
        }
        /// <summary>
        /// IC读卡器接收线程函数
        /// </summary>
        private void Thread_ICReader()
        {
            while (true)
            {
                if (Run)
                {
                    try
                    {
                        byte[] buffer = new byte[16];
                        byte[] snr = new byte[6] { 255, 255, 255, 255, 255, 255 };
                        int nRet = XKC601U.MF_Read(0, 16, 1, snr, buffer);
                        if (nRet != 0)
                        {
                            XKC601U.ControlLED(9, 3, new byte[1]);
                            if (nRet == 1)
                            {
                                UC_Operation.I.WriteLog("读卡器:未读到卡!","Warn");
                            }
                            else
                            {

                                UC_Operation.I.WriteLog(XKC601U.showStatue(nRet), "Debug");
                            }
                        }
                        else
                        {
                            XKC601U.ControlBuzzer(9, 3, new byte[1]);
                            UC_Operation.I.WriteLog(ByteHelper.BytesToHexString(snr, 6), "Info");
                        }

                    }
                    catch (Exception ex)
                    {
                        Rlog.Error(ex.Message + "\r\n" + ex.StackTrace);
                        break;
                    }
                }
                Thread.Sleep(200);
            }
        }

    }
}
