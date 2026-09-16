using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Convert_Manager.FrameWebForJS
{
    public class NoticePoint
    {
        public int row;

        public string m;   // 要素番号
        public double[] Points;
    }

    public class notice_points
    {
        public const string KEY = "notice_points";
        private const string wFile = "Chakumoku.tmp";

        private List<NoticePoint> noticepoints = new List<NoticePoint>();

        public notice_points(Dictionary<string, string> wdata)
        {
            if (!wdata.ContainsKey(notice_points.wFile))
                return;

            var str = wdata[notice_points.wFile];

            // 1行の抽出
            var lst1 = new List<string>();
            while (str.Length > 0)
            {
                string line = comon.byteSubstr(ref str, 1053);
                lst1.Add(line);
            }
            int row = 0;
            noticepoints = new List<NoticePoint>();
            foreach (var line in lst1)
            {
                str = line;

                var np = new NoticePoint();

                /// 部材
                string tmp = comon.byteSubstr(ref str, 4);
                np.m = tmp.Trim();

                // 
                var p = new List<double>();
                for (int i = 0; i < 50; i++)
                {
                    tmp = comon.byteSubstr(ref str, 9).Trim();
                    if(0 < tmp.Length)
                    {
                        p.Add(Convert.ToDouble(tmp));
                    }
                }
                if(0 < p.Count)
                {
                    var partitions = notice_points.partition<double>(p, 20);
                    foreach(var pnt in partitions)
                    {
                        noticepoints.Add(
                            new NoticePoint() { 
                                row = row,
                                m=np.m,
                                Points = pnt.ToArray()
                            });
                        row++;
                    }
                }

            }
        }

        private static List<List<T>> partition<T>(List<T> values, int chunkSize)
        {
            return values.Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / chunkSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }


        public List<NoticePoint> GetNoticePoint()
        {
            return noticepoints;
        }


        /// <summary>
        /// 分割した部材に合わせて着目点を修正する
        /// </summary>
        /// <param name="newMember"></param>
        internal void addNewMember(Dictionary<string, Member> newMember, node _node, member _member)
        {
            // 分割前の要素
            var k1 = newMember.First();
            var i1 = Convert.ToInt32(k1.Key);
            var len1 = k1.Value.Length(_node);

            // 分割後の要素
            var k2 = newMember.Last();    
            // var len2 = k2.Value.Length(_node);


            // 分割した部材に合わせて着目点を修正する
            var temp = new List<NoticePoint>();
            foreach (var np in this.noticepoints)
            {
                int k = Convert.ToInt32(np.m);
                if (i1 < k)
                {   // 分割前の要素より大きい部材番号
                    np.m = (k + 1).ToString();
                    np.row += 1;
                    temp.Add(np);

                } else if(k1.Key== np.m)
                {
                    // 分割前の部材
                    var ld1 = new List<double>();
                    foreach (var p in np.Points)
                        if(p< len1)
                            ld1.Add(p);
                    var np1 = new NoticePoint() { 
                        m = np.m, 
                        row = np.row, 
                        Points=ld1.ToArray() 
                    };
                    temp.Add(np1);

                    // 分割後の部材
                    var ld2 = new List<double>();
                    foreach (var p in np.Points)
                        if (len1 < p)
                            ld2.Add(p - len1);
                    var np2 = new NoticePoint() { 
                        m = (k + 1).ToString(), 
                        row = np.row+1, 
                        Points = ld2.ToArray() 
                    };
                    temp.Add(np2);
                }
                else
                {
                    temp.Add(np);
                }
            }

            this.noticepoints = temp;
        }

    }
}
