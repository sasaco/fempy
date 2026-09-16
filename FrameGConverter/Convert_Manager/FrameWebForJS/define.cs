using System;
using System.Collections.Generic;
using System.Text;

namespace Convert_Manager.FrameWebForJS
{
    public class Define
    {
        public int row;
        public Dictionary<string, int> CaseNumbers = new Dictionary<string, int>();
    }

    public class define
    {
        public const string KEY = "define";
        private const string wFile1 = "Ku_DefNo.tmp";
        private const string wFile2 = "Ku_Define2.tmp";

        Dictionary<string, Define> DefineList = new Dictionary<string, Define>();

        public define(Dictionary<string, string> wdata)
        {
            /// 番号
            if (!wdata.ContainsKey(define.wFile1))
                return;
            if (!wdata.ContainsKey(define.wFile2))
                return;

            var str1 = wdata[define.wFile1];
            var str2 = wdata[define.wFile2];

            while (str1.Length > 9)
            {
                var tmp = comon.byteSubstr(ref str1, 10);
                var no = tmp.Trim();

                string str = comon.byteSubstr(ref str2, 180);

                var df = new Define();

                for (int i = 0; i < 30; i++)
                {
                    tmp = comon.byteSubstr(ref str, 6).Trim();
                    if (0 < tmp.Length)
                    {
                        df.CaseNumbers.Add("C"+(i+1), Convert.ToInt32(tmp));
                    }
                }
                DefineList.Add(no, df);
            }
        }


        public Dictionary<string, Dictionary<string, int>> GetDefine()
        {
            var result = new Dictionary<string, Dictionary<string, int>>();

            int row = 1;
            foreach(var a in DefineList)
            {
                var b = new Dictionary<string, int>();
                b.Add("row", row);
                foreach(var c in a.Value.CaseNumbers)
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
