using RinKit;
using System;
using System.Windows;

namespace ZenergyBFSI.Model
{
    internal class Settings
    {
        public static string 电芯型号 { get; internal set; } = "Test";
        #region  扫码枪IP配置
        //public static string 扫码枪1IP { get; internal set; } = "192.168.250.15";
        //public static int 扫码枪1Port { get; internal set; } = 9004;
        //public static string 扫码枪2IP { get; internal set; } = "192.168.250.16";
        //public static int 扫码枪2Port { get; internal set; } = 9004;
        //public static string 扫码枪3IP { get; internal set; } = "192.168.250.17";
        //public static int 扫码枪3Port { get; internal set; } = 9004;
        //public static string 扫码枪4IP { get; internal set; } = "192.168.250.18";
        //public static int 扫码枪4Port { get; internal set; } = 9004;
        //public static string 扫码枪5IP { get; internal set; } = "192.168.250.19";//直连PLC，预留
        //public static int 扫码枪5Port { get; internal set; } = 9004;
        #endregion
        public static int IO同步循环等待 { get; internal set; } = 80;//ms
        public static int 自动机循环等待 { get; internal set; } = 80;//ms
        public static int 注控通讯等待 { get; internal set; } = 80;//ms
        public static int 扫码超时计数 { get; internal set; } = 10;//*100ms
        public static int 错误等待 { get; internal set; } = 10000;//ms
        public static int PLC循环等待 { get; internal set; } = 80;//ms
        public static int PLC超时警告 { get; internal set; } = 300 * 10000;//ticks,ms*10000
        public static int 显示日志级别 { get; internal set; } = 1;
        public static int PLC日志 { get; internal set; } = 0;
        public static int PLC_Port { get; internal set; } = 9600;
        public static int PLC_SA { get; internal set; } = 247;
        public static int MOM在线 { get; internal set; } = 1;
        public static int 清料模式 { get; internal set; } = 1;
        public static string MOM地址 { get; internal set; } = "http://10.6.33.3:8007/wcfhttpservice";//http://10.5.33.11:8007/WcfHttpService http://10.6.33.10:8007/WcfHttpService http://10.6.33.4:8007/WcfHttpService
        public static int MOM心跳间隔 { get; internal set; } = 3000;//ms
        public static int MOM联机计数 { get; internal set; } = 20;//ms
        public static uint Power { get; internal set; } = 1;//位控制
        public static int 自启动 { get; internal set; } = 0;
        public static string Software { get; internal set; } = "ZenergyBFSI";
        public static string EquipmentCode { get; internal set; } = "Test1";
        public static string PLC_IP { get; internal set; } = "127.0.0.1";
        public static string SQLite路径 { get; internal set; } = "";
        public static string SQLServer视觉地址 { get; internal set; } = "DESKTOP-0F9L4KO\\RJ";
        public static string SQLServer视觉库名 { get; internal set; } = "VisionProgram";
        public static string SQLServer视觉用户 { get; internal set; } = "merj";
        public static string SQLServer视觉密码 { get; internal set; } = "1234@abcD";
        private static Settings _instance = null;
        private static readonly object _syncRoot = new object();
        private static bool[] _power = null;

        static Settings()
        {
            if (_instance == null)
            {
                lock (_syncRoot)
                {
                    if (_instance == null) _instance = new Settings();
                }
            }
        }

        /// <summary>
        /// 权限
        /// 0：PLC数据同步
        /// 1：显示自动机循环时间日志
        /// </summary>
        public static bool GetPower(int index)
        {
            if (_power == null) _power = DataHelper.UintToBits(Power);
            return _power[index];
        }

        public static void Save(string pName = "")
        {
            lock (_syncRoot)
            {
                try
                {
                    Rdb.SaveSettings(_instance, pName);
                }
                catch (Exception)
                {
                    MessageBox.Show("Settings保存失败！");
                }
            }
        }

        public static void New()
        {
            lock (_syncRoot)
            {
                try
                {
                    Rdb.NewSettings(_instance);
                }
                catch (Exception)
                {
                    MessageBox.Show("Settings保存失败！");
                }
            }
        }

        public static void Load()
        {
            try
            {
                Rdb.LoadSettings(ref _instance);
            }
            catch (Exception)
            {
                MessageBox.Show("Settings读取失败！");
            }
        }

    }
}
//internal class Settings
//{
//    public static string 电芯型号 { get; internal set; } = "104AH";



//    public static string 扫码枪1IP { get; internal set; } = "192.168.250.15";
//    public static int 扫码枪1Port { get; internal set; } = 9004;
//    public static string 扫码枪2IP { get; internal set; } = "192.168.250.16";
//    public static int 扫码枪2Port { get; internal set; } = 9004;
//    public static string 扫码枪3IP { get; internal set; } = "192.168.250.17";
//    public static int 扫码枪3Port { get; internal set; } = 9004;
//    public static string 扫码枪4IP { get; internal set; } = "192.168.250.18";
//    public static int 扫码枪4Port { get; internal set; } = 9004;
//    public static string 扫码枪5IP { get; internal set; } = "192.168.250.19";//直连PLC，预留
//    public static int 扫码枪5Port { get; internal set; } = 9004;

//    public static int IO同步循环等待 { get; internal set; } = 80;//ms
//    public static int 自动机循环等待 { get; internal set; } = 80;//ms
//    public static int 注控通讯等待 { get; internal set; } = 80;//ms
//    public static int 扫码超时计数 { get; internal set; } = 10;//*100ms
//    public static int 错误等待 { get; internal set; } = 10000;//ms
//    public static int PLC循环等待 { get; internal set; } = 80;//ms
//    public static int PLC超时警告 { get; internal set; } = 300 * 10000;//ticks,ms*10000
//    public static int 显示日志级别 { get; internal set; } = 1;
//    public static int PLC日志 { get; internal set; } = 0;
//    public static int PLC_Port { get; internal set; } = 44818;
//    public static int PLC_SA { get; internal set; } = 247;
//    public static int MOM在线 { get; internal set; } = 1;
//    public static int 清料模式 { get; internal set; } = 1;
//    public static string MOM地址 { get; internal set; } = "http://10.6.33.3:8007/wcfhttpservice";//http://10.5.33.11:8007/WcfHttpService http://10.6.33.10:8007/WcfHttpService http://10.6.33.4:8007/WcfHttpService
//    public static int MOM心跳间隔 { get; internal set; } = 3000;//ms
//    public static int MOM联机计数 { get; internal set; } = 20;//ms
//    public static uint Power { get; internal set; } = 1;//位控制
//    public static int 自启动 { get; internal set; } = 0;
//    public static string Software { get; internal set; } = "ZenergyBFSI";
//    public static string EquipmentCode { get; internal set; } = "P05ECZY01";
//    public static string PLC_IP { get; internal set; } = "127.0.0.1";
//    private static Settings _instance = null;
//    private static readonly object _syncRoot = new object();
//    private static bool[] _power = null;

//    static Settings()
//    {
//        if (_instance == null)
//        {
//            lock (_syncRoot)
//            {
//                if (_instance == null) _instance = new Settings();
//            }
//        }
//    }

//    /// <summary>
//    /// 权限
//    /// 0：PLC数据同步
//    /// 1：显示自动机循环时间日志
//    /// </summary>
//    public static bool GetPower(int index)
//    {
//        if (_power == null) _power = DataHelper.UintToBits(Power);
//        return _power[index];
//    }

//    public static void Save(string pName = "")
//    {
//        lock (_syncRoot)
//        {
//            try
//            {
//                Rdb.SaveSettings(_instance, pName);
//            }
//            catch (Exception)
//            {
//                MessageBox.Show("Settings保存失败！");
//            }
//        }
//    }

//    public static void New()
//    {
//        lock (_syncRoot)
//        {
//            try
//            {
//                Rdb.NewSettings(_instance);
//            }
//            catch (Exception)
//            {
//                MessageBox.Show("Settings保存失败！");
//            }
//        }
//    }

//    public static void Load()
//    {
//        try
//        {
//            Rdb.LoadSettings(ref _instance);
//        }
//        catch (Exception)
//        {
//            MessageBox.Show("Settings读取失败！");
//        }
//    }

//}