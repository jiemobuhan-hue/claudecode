namespace ZenergyBFSI.Model
{
    public class DataINJ
    {
        public long Time { get; set; } = 0;
        public float 吐出量 { get; set; } = 0;
        public float 流量 { get; set; } = 0;
        public float 补偿量 { get; set; } = 0;
        public float 排气冲程 { get; set; } = 0;
        public float 排气转速 { get; set; } = 0;
        public float 系数 { get; set; } = 0;
        public float 排气当前冲程 { get; set; } = 0;
        public float 写入吐出量 { get; set; } = 0;
        public float 写入补偿量 { get; set; } = 0;
        //public float 吐出量2 { get; set; } = 0;
        //public float 流量2 { get; set; } = 0;
        //public float 补偿量2 { get; set; } = 0;
        //public float 排气冲程2 { get; set; } = 0;
        //public float 排气转速2 { get; set; } = 0;
        //public float 系数2 { get; set; } = 0;
        //public float 排气当前冲程2 { get; set; } = 0;
        //public float 写入吐出量2 { get; set; } = 0;
        //public float 写入补偿2量 { get; set; } = 0;

        public bool 操作锁定 { get; set; } = false;
    }
}
