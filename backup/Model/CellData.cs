using RinKit;
using System;

namespace ZenergyBFSI.Model
{
    public class CellData
    {
        public int Id { get; set; } = 0;
        public long TimeStamp { get; set; } = DataHelper.TimeStamp;
        public string 电芯码 { get; set; }
        public string 进站时间 { get; set; } = DateTime.Now.ToString();

        public string 检验位置 { get; set; }
        public string 是否复投 { get; set; }


        public int Ng类型数量 { get; set; }
        public string Ng类型1 { get; set; }
        public string Ng类型2 { get; set; }
        public string Ng类型3 { get; set; }
        public string Ng类型4 { get; set; }
        public string Ng类型5 { get; set; }
        public string Ng类型6 { get; set; }
        public string Ng类型7 { get; set; }
        public string Ng类型8 { get; set; }

        //MOM查询后的参数，根据MOM接口对接修改结果
        public string 入站结果 { get; set; } = "";
        public string 出站结果 { get; set; } = "";
        public string 出站时间 { get; set; } = "";
        public string 视觉检测状态 { get; set; } = "";//0：生产中；1：结束；-1：视觉检测；-2：备用；
        public string 视觉检测参数一 { get; set; } = "";
        public string 视觉检测参数二 { get; set; } = "";
        public string 视觉检测参数三 { get; set; } = "";
        public string 视觉检测参数四 { get; set; } = "";
        public string 视觉检测参数五 { get; set; } = "";
        public string 视觉检测参数六 { get; set; } = "";

        public string MOM查询来料状态 { get; set; } = "";
        public string MOM出站结果 { get; set; } = "0";
        public string 视觉检测结果 { get; set; } = "";
        public int 人工复判次数 { get; set; }
    }
}
//public class CellData
//{
//    public int Id { get; set; } = 0;
//    public long TimeStamp { get; set; } = DataHelper.TimeStamp;
//    public string 电芯码 { get; set; }
//    public string 进站时间 { get; set; } = DateTime.Now.ToString();
//    public float 一注前称重 { get; set; } = 0;
//    public float 一注后称重 { get; set; } = 0;
//    public float 前称重重量 { get; set; } = 0;
//    public float 后称重重量 { get; set; } = 0;
//    public float 化成失液量 { get; set; } = 0;
//    public float 目标注液量 { get; set; } = 0;
//    public float 实际注液量 { get; set; } = 0;
//    //public float 目标保有量 { get; set; } = Settings.保液量目标;
//    public float 实际保有量 { get; set; } = 0;
//    //public float 保压真空目标值 { get; set; } = Settings.保压真空值;
//    //public float 抽真空时间 { get; set; } = Settings.抽真空时间;
//    //public float 保压时间 { get; set; } = Settings.保压时间;
//    // public float 注液时间 { get; set; } = Settings.注液时间;
//    public float 保压前真空 { get; set; } = 0;
//    public float 保压后真空 { get; set; } = 0;
//    // public float 注液正压目标值 { get; set; } = Settings.注液正压值;
//    // public float 正压时间 { get; set; } = Settings.正压时间;
//    public float 胶钉高度 { get; set; } = 0;
//    public string 入站结果 { get; set; } = "";
//    public string 拔钉结果 { get; set; } = "";
//    public string 前称重结果 { get; set; } = "";
//    public string 真空检测结果 { get; set; } = "";
//    public string 后称重结果 { get; set; } = "";
//    public string 胶钉检测结果 { get; set; } = "";
//    public string 注液工位 { get; set; } = "";
//    public string 前称重工位 { get; set; } = "";
//    public string 后称重工位 { get; set; } = "";
//    public string 出站结果 { get; set; } = "";
//    public string 出站时间 { get; set; } = "";
//    public int 二注结束 { get; set; } = 0;//0：生产中；1：结束；-1：注液中；-2：后称；

//    public float 第一次前称重量 = 0;
//}