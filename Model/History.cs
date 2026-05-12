using System;
using ZenergyBFSI.Model;

namespace ZenergyBFSI.Model
{
    internal class History
    {
        public string 电芯码 { get; set; }
        public string 进站时间 { get; set; } = DateTime.Now.ToString();

         
        public string 出站结果 { get; set; } = "";
        public string 出站时间 { get; set; } = "";

        public History()
        {
        }

        public History(CellData data)
        {
            电芯码 = data.电芯码;
            进站时间 = data.进站时间;
            出站结果 = data.出站结果;
            出站时间 = data.出站时间;
        }

    }
}
//internal class History0
//{
//    public string 电芯码 { get; set; }
//    public string 进站时间 { get; set; } = DateTime.Now.ToString();
//    public float 一注前称重 { get; set; } = 0;
//    public float 一注后称重 { get; set; } = 0;
//    public float 前称重重量 { get; set; } = 0;
//    public float 后称重重量 { get; set; } = 0;
//    public float 化成失液量 { get; set; } = 0;
//    public float 目标注液量 { get; set; } = 0;
//    public float 实际注液量 { get; set; } = 0;

//    public string 出站结果 { get; set; } = "";
//    public string 出站时间 { get; set; } = "";

//    public History0()
//    {
//    }

//    public History0(CellData data)
//    {
//        电芯码 = data.电芯码;
//        进站时间 = data.进站时间;
//        一注前称重 = data.一注前称重;
//        一注后称重 = data.一注后称重;
//        前称重重量 = data.前称重重量;
//        后称重重量 = data.后称重重量;
//        化成失液量 = data.化成失液量;
//        目标注液量 = data.目标注液量;

//        出站结果 = data.出站结果;
//        出站时间 = data.出站时间;
//    }

//    //public float 第一次前称重量 = 0;

//}