using System;
using static DevExpress.Office.Utils.HdcOriginModifier;

namespace ZenergyBFSI.Model
{
    internal class PlcBlock
    {
        public PlcBlock()
        {
        }

        public PlcBlock(string[] str,int mode)
        {
            BlockType = str[0];
            BlockLength = Convert.ToUInt16(str[1]);
            ObjType = str[2];
            Adress = str[3];
            Mode = mode;
        }
        public PlcBlock(string adress, string type, int mode)
        {
            BlockType= type;
            Adress = adress;
            Mode = mode;
        }

        public string BlockType { get; set; }
        public ushort BlockLength { get; set; }
        public string ObjType { get; set; }
        public string Adress { get; set; }
        public int Mode { get; set; }
        //0:关闭；
        //1：单读；2：单写；3：单读+写；
        //4：块读；5块写；6：块读+写；
    }
}
