using System;
using System.Collections.Generic;
using System.Text;

namespace Convert_Manager.FrameWebForJS
{
    public class Joint
    {
        public int row;

        public string m;   // 部材番号
        //public int xi;
        //public int yi;
        public int zi;
        //public int xj;
        //public int yj;
        public int zj;
    }

    public class joint
    {
        public const string KEY = "joint";
        private const string wFile = "B_Ketugou.tmp";

        private Dictionary<string, List<Joint>> JointList = new Dictionary<string, List<Joint>>();

        public string message = "";

        public joint(Dictionary<string, string> wdata)
        {
            if (!wdata.ContainsKey(joint.wFile))
                return;

            var str = wdata[joint.wFile];

            // 1行の抽出
            var lst1 = new List<string>();
            while (str.Length > 0)
            {
                string line = comon.byteSubstr(ref str, 99);
                lst1.Add(line);
            }

            var jj = new List<Joint[]>();

            foreach (var line in lst1)
            {
                str = line;

                /// 部材
                string tmp = comon.byteSubstr(ref str, 3);
                string No = tmp.Trim();

                // 結合条件
                var JO = new Joint[16];
                for (int i = 0; i < 16; i++)
                {
                    JO[i] = new Joint() { m = No };
                    tmp = comon.byteSubstr(ref str, 1).Trim();
                    var xi = (0 < tmp.Length) ? Convert.ToInt32(tmp) : 1;
                    tmp = comon.byteSubstr(ref str, 1).Trim();
                    var yi = (0 < tmp.Length) ? Convert.ToInt32(tmp) : 1;
                    tmp = comon.byteSubstr(ref str, 1).Trim();
                    JO[i].zi = (0 < tmp.Length) ? Convert.ToInt32(tmp) : -1;
                    tmp = comon.byteSubstr(ref str, 1).Trim();
                    var xj = (0 < tmp.Length) ? Convert.ToInt32(tmp) : 1;
                    tmp = comon.byteSubstr(ref str, 1).Trim();
                    var yj = (0 < tmp.Length) ? Convert.ToInt32(tmp) : 1;
                    tmp = comon.byteSubstr(ref str, 1).Trim();
                    JO[i].zj = (0 < tmp.Length) ? Convert.ToInt32(tmp) : -1;

                    if (xi*yi*xj*yj == 0) { 
                        if (message.Length == 0)
                        {
                            message = "計算できない結合条件が設定されています";
                        }
                    }
                }
                jj.Add(JO);
            }

            // 集計
            for (var j = 0; j < 16; j++)
            {
                var jot = new List<Joint>();
                var row = 1;
                foreach (var JO in jj)
                {
                    var e = JO[j];

                    // 有効判定
                    if (e.zi != -1 && e.zj != -1)
                    {
                        if (e.zi == -1) e.zi = 1;
                        if (e.zj == -1) e.zj = 1;

                        e.row = row;
                        jot.Add(e);
                        row++;
                    }
                }

                if (0 < jot.Count)
                    this.JointList.Add((j + 1).ToString(), jot);
            }

        }



        public Dictionary<string, List<Joint>> GetJoint()
        {
            return JointList;
        }


        /// <summary>
        /// old_mNo 以降の要素番号を+1する
        /// </summary>
        /// <param name="old_mNo"></param>
        internal void addNewMember(string old_mNo)
        {
            int old_iNo = Convert.ToInt32(old_mNo);

            var temp1 = new Dictionary<string, List<Joint>>();
            foreach (var fm1 in this.JointList)
            {
                var temp2 = new List<Joint>();
                foreach (var fm2 in fm1.Value)
                {
                    int im = Convert.ToInt32(fm2.m);
                    if (old_iNo < im)
                    {
                        // old_mNo 以降の要素
                        fm2.m = (im + 1).ToString();
                        fm2.row += 1;
                        temp2.Add(fm2);
                    }
                    else if (im == old_iNo)
                    {
                        temp2.Add(new Joint()   // 分割前の部材
                        {
                            row = fm2.row,
                            m = fm2.m,
                            zi = fm2.zi,
                            zj = 1
                        });
                        temp2.Add(new Joint()   // 分割後の部材
                        {
                            row = fm2.row + 1,
                            m = (im + 1).ToString(),
                            zi = 1,
                            zj = fm2.zj
                        });
                    }
                    else
                    {
                        temp2.Add(fm2);
                    }
                }
                temp1.Add(fm1.Key, temp2);
            }
            this.JointList = temp1;
        }
    }
}
