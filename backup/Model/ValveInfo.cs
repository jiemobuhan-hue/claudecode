using System;

namespace ZenergyBFSI.Model
{
    public class ValveInfo
    {
        public ValveInfo()
        {
        }
        public string 类型 { get; set; } = "";
 
        public int 状态 { get; set; } = 0;//0:未开始，1：进料中，2：结束
    }
}
