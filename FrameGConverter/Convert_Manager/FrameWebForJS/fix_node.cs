using System;
using System.Collections.Generic;
using System.Text;

namespace Convert_Manager.FrameWebForJS
{
    public class FixNode
    {
        public int row;

        public string n;    // 節点番号
        public double tx;
        public double ty;
        //public double tz;
        //public double rx;
        //public double ry;
        public double rz;

        [NonSerialized]
        public double count; // 部材数

    }

    public class fix_node
    {
        public const string KEY = "fix_node";
        private const string wFile2 = "$2.txt"; // 支点の個数
        private const string wFile3 = "$3.txt";

        private Dictionary<string, List<FixNode>> FixNodeList = new Dictionary<string, List<FixNode>>();

        public fix_node(Dictionary<string, string> wdata)
        {

            // 節点番号と 奥行本数の読み込み
            if (!wdata.ContainsKey(fix_node.wFile2))
                return;

            var str = wdata[fix_node.wFile2];
            string[] del = { "\r\n" };
            string[] arr = str.Split(del, StringSplitOptions.None);

            var fn_base = new List<FixNode>();
            for (int i = 1; i < arr.Length; i++)
            {
                var lst1 = arr[i].Split("\t");

                if (lst1.Length < 2)
                    continue;

                var row = Convert.ToInt32(lst1[0]);
                var No = lst1[1];
                double count = 1;
                if (lst1[2].Trim().Length > 0)
                {
                    count = Convert.ToDouble(lst1[2]);
                    if (count == Double.NaN) count = 1;
                    if (count == 0) count = 1;
                }

                fn_base.Add(new FixNode() { row = row, n = No, count = count });
            }

            // バネ値の読み込み
            if (!wdata.ContainsKey(fix_node.wFile3))
                return;

            str = wdata[fix_node.wFile3];
            arr = str.Split(del, StringSplitOptions.None);

            for (var j = 0; j < 16; j++) {
                var fn_list = new List<FixNode>();
                for (int i = 1; i < arr.Length; i++)
                {
                    var lst1 = arr[i].Split("\t");

                    if (lst1.Length < 2)
                        continue;

                    var fb = fn_base[i - 1];
                    var fn = new FixNode() { row = fb.row, n = fb.n, count = fb.count };
                    var tmp = lst1[1 + j * 3];
                    tmp = tmp.Trim();
                    fn.tx = (0 < tmp.Length) ? Convert.ToDouble(tmp) : 0;
                    tmp = lst1[2 + j * 3];
                    fn.ty = (0 < tmp.Length) ? Convert.ToDouble(tmp) : 0;
                    tmp = lst1[3 + j * 3];
                    fn.rz = (0 < tmp.Length) ? Convert.ToDouble(tmp) : 0;

                    if ((fn.tx + fn.ty + fn.rz) !=0)
                        fn_list.Add(fn);

                }
                FixNodeList.Add((j + 1).ToString(), fn_list);
            }
        }


        public Dictionary<string, List<FixNode>> GetFixNode()
        {
            return FixNodeList;
        }

        /// <summary>
        /// nNo 以上の節点番号を+1する
        /// </summary>
        /// <param name="nNo"></param>
        internal void addNewNode(string nNo)
        {
            int iNo = Convert.ToInt32(nNo);
            foreach(var f1 in this.FixNodeList)
            {
                foreach(var f2 in f1.Value)
                {
                    int k = Convert.ToInt32(f2.n);
                    if(iNo <= k)
                    {
                        f2.n = (k + 1).ToString();
                    }

                }

            }
        }
    }
}
