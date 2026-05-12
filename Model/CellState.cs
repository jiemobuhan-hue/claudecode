using RinKit;
using System;

namespace ZenergyBFSI.Model
{
    public class CellState
    {
        public long Time { get; set; } = DataHelper.TimeMS;
        public string 电芯码 { get; set; }
        public int 通道 { get; set; }
        public string 工位 { get; set; }
        public int 离开 { get; set; } = 0;
        public int Step { get; set; } = 0;
    }
}
