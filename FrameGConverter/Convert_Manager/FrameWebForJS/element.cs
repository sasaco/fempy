using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Convert_Manager.FrameWebForJS
{
    public class Element
    {
        public double E;
        // public double G;
        public double Xp;
        public double A;
        // public double J;
        //public double Iy;
        public double Iz;
        public string name;

        // FrameWebには無い
        [NonSerialized]
        public string mark;
        [NonSerialized]
        public double n; // 部材数
    }

    public class element
    {
        public const string eKEY = "element";

        protected Dictionary<string, Dictionary<string, Element>> ElementList = new Dictionary<string, Dictionary<string, Element>>();

        public Dictionary<string, Dictionary<string, Element>> GetElement()
        {
            return ElementList;
        }


        public void setElementList(List<object[]> ee)
        {
            // 材料集計
            for (var j = 0; j < 6; j++)
            {
                var eee = new Dictionary<string, Element>();
                foreach (var val in ee)
                {
                    var e = new Element();

                    var No = (string)val[0];
                    e.mark = (string)val[1];
                    e.name = (string)val[2];
                    var n = (double)val[3];
                    e.E = (double)val[4];
                    e.Xp = (double)val[5];
                    var A = (double[])val[6];
                    var Iz = (double[])val[7];

                    if (A[j] != 0)
                    {
                        e.A = A[j] * n;
                    }
                    if (Iz[j] != 0)
                    {
                        e.Iz = Iz[j] * n;
                    }
                    // 有効判定
                    if (e.E != 0 && e.A != 0 && e.Iz != 0)
                    {
                        eee.Add(No, e);
                    }

                }

                if (0 < eee.Count)
                    this.ElementList.Add((j + 1).ToString(), eee);
            }

        }

        /// <summary>
        /// 同じ数値の諸元を探して 材料番号を返す.
        /// 無ければ新しい諸元を生成して 新No を返す.
        /// </summary>
        /// <returns></returns>
        internal string GetElementNo(string eNo, double A, double Iz)
        {
            var Targets = new Dictionary<string, Element>();
            int maxNo = -1;

            foreach (var e1 in this.ElementList)
            {
                if (!e1.Value.ContainsKey(eNo))
                { // もし既に登録済の諸元になかったら新しい諸元を追加する処理に飛ぶ
                    e1.Value.Add(eNo,
                        new Element()
                        {
                            A = A,
                            Iz = Iz,
                        });
                    return eNo;
                }

                var eNoe = e1.Value[eNo];
                foreach (var e2 in e1.Value)
                {
                    if (eNoe.E == e2.Value.E && e2.Value.A == A && e2.Value.Iz == Iz
                         && eNoe.Xp == e2.Value.Xp) 
                    {   // 諸元が同じ
                        if (!Targets.ContainsKey(e2.Key))
                        {   // まだ登録が無ければ
                            Targets.Add(e2.Key, e2.Value); // 追加する
                        }
                    }
                    else if (Targets.ContainsKey(e2.Key)) { 
                        // 既に登録済で諸元が異なっていたら
                        Targets.Remove(e2.Key); // 削除する
                    }

                    // 最大の材料番号を調べる
                    var iNo = Convert.ToInt32(e2.Key);
                    if (maxNo < iNo)
                        maxNo = iNo;
                }
            }

            if (Targets.Count > 0)
            {   // 最初の要素を取得
                return Targets.First().Key;
            }


            // もし既に登録済の諸元になかったら新しい諸元を追加する
            Element eNee = this.ElementList.First().Value[eNo];
            string newNo = (maxNo + 1).ToString();
            foreach (var e1 in this.ElementList)
            {
                e1.Value.Add(newNo,
                    new Element() { 
                        E = eNee.E,
                        A = A,
                        Iz = Iz,
                        Xp = eNee.Xp,
                        name = "剛域"
                    });
            }
            return newNo;
        }
    }
}
