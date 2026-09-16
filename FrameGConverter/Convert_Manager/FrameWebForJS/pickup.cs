using System;
using System.Collections.Generic;
using System.Text;

namespace Convert_Manager.FrameWebForJS
{
    public class Pickup
    {
        public string name;
        public Dictionary<string, int> com = new Dictionary<string, int>();
    }

    public class pickup
    {
        public const string KEY = "pickup";
        private const string wFile1 = "PickUp.tmp";
        private const string wFile2 = "PickUpName.tmp";

        Dictionary<string, Pickup> PickupList = new Dictionary<string, Pickup>();

        public pickup(Dictionary<string, string> wdata)
        {
            /// 番号
            if (!wdata.ContainsKey(pickup.wFile1))
                return;
            if (!wdata.ContainsKey(pickup.wFile2))
                return;

            var str1 = wdata[pickup.wFile1];
            var str2 = wdata[pickup.wFile2];

            int index = 1;
            while (str1.Length > 9)
            {
                // 名称
                string str = comon.byteSubstr(ref str2, 5);
                str = comon.byteSubstr(ref str2, 40).Trim();

                // 組合せ係数
                var pik = new Pickup() { name = str };
                var tmp1 = comon.byteSubstr(ref str1, 400);
                for (int i = 0; i < 100; i++)
                {
                    var tmp3 = comon.byteSubstr(ref tmp1, 4).Trim();
                    if (0 < tmp3.Length)
                    {
                        var comNo = Convert.ToInt32(tmp3);
                        var key = "C" + (i+1);
                        if (!pik.com.ContainsKey(key))
                            pik.com.Add(key, comNo);
                    }
                }
                PickupList.Add(index.ToString(), pik);
                index++;
            }
        }


        public Dictionary<string, Dictionary<string, object>> GetPickup()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();

            int row = 1;
            foreach (var a in PickupList)
            {
                var b = new Dictionary<string, object>();
                b.Add("row", row);
                b.Add("name", a.Value.name);

                foreach (var c in a.Value.com)
                {
                    b.Add(c.Key, c.Value);
                }
                result.Add(a.Key, b);
                row++;
            }

            return result;
        }
    }
}
