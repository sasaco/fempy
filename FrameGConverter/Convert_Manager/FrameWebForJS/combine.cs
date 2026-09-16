using System;
using System.Collections.Generic;
using System.Text;

namespace Convert_Manager.FrameWebForJS
{
    public class Combine
    {
        public string name;
        public Dictionary<string, double> coef = new Dictionary<string, double>();
    }

    public class combine
    {
        public const string KEY = "combine";
        private const string wFile1 = "Ku_Combine.tmp";
        private const string wFile2 = "Ku_ComNa.tmp";

        Dictionary<string, Combine> CombineList = new Dictionary<string, Combine>();

        public combine(Dictionary<string, string> wdata)
        {
            /// 番号
            if (!wdata.ContainsKey(combine.wFile1))
                return;
            if (!wdata.ContainsKey(combine.wFile2))
                return;

            var str1 = wdata[combine.wFile1];
            var str2 = wdata[combine.wFile2];

            int index = 1;
            while (str1.Length > 9)
            {
                // 全体の割り増し係数
                var tmp = comon.byteSubstr(ref str1, 10).Trim();
                var coef0 = (0 < tmp.Length) ? Convert.ToDouble(tmp): 1;
                
                // 組合せ名称
                string str = comon.byteSubstr(ref str2, 5);
                str = comon.byteSubstr(ref str2, 40).Trim();

                // 組合せ係数
                var com = new Combine() { name = str };
                var tmp1 = comon.byteSubstr(ref str1, 240);
                var tmp2 = comon.byteSubstr(ref str1, 120);
                for (int i = 0; i < 30; i++)
                {
                    var tmp3 = comon.byteSubstr(ref tmp1, 8).Trim();
                    var tmp4 = comon.byteSubstr(ref tmp2, 4).Trim();
                    if (0 < tmp4.Length)
                    {
                        var coef = Convert.ToDouble(tmp3);
                        var key = "C" + tmp4;
                        if (!com.coef.ContainsKey(key))
                            com.coef.Add(key, coef);
                        else
                            com.coef[key] += coef; // 同じケースが既に登録されていたら加算する
                    }
                }
                CombineList.Add(index.ToString(), com);
                index++;
            }
        }


        public Dictionary<string, Dictionary<string, object>> GetCombine()
        {
            var result = new Dictionary<string, Dictionary<string, object>>();

            int row = 1;
            foreach (var a in CombineList)
            {
                var b = new Dictionary<string, object>();
                b.Add("row", row);
                b.Add("name", a.Value.name);

                foreach (var c in a.Value.coef)
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
