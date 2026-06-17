using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenergyBFSI.Model
{
    public class User
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string CardNo { get; set; }

        private int power;
        public int Power
        {
            get => power;
            set
            {
                power = value;
                switch (value)
                {
                    case 1: role = "操作员"; break;
                    case 2: role = "技术员"; break;
                    case 3: role = "工程师"; break;
                    case 4: role = "管理员"; break;
                    case 5: role = "ADMIN"; break;
                }
            }
        }


        private string role;
        public string Role { get => role; set => role = value; }


        public User()
        {
        }

        public User(string name, string code, string cardNo, int power)
        {
            Name = name;
            Code = code;
            CardNo = cardNo;
            Power = power;
        }
    }
}
