using Newtonsoft.Json.Linq;
using PDF_Manager.Comon;
using PDF_Manager.Printing.Comon;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    public class Combine
    {
        public string name;
        public Dictionary<string, double> coef = new Dictionary<string, double>();
    }

    internal class InputCombine : PrintableBaseA
    {
        public const string KEY = "combine";
        public Dictionary<int, Combine> combines = new Dictionary<int, Combine>();


        public InputCombine(){}

        public InputCombine(Dictionary<string, object> value)
        {
            if (!value.ContainsKey(KEY))
                return;

            // データを取得する．
            var target = JObject.FromObject(value[KEY]).ToObject<Dictionary<string, object>>();

            // データを抽出する
            for (var i = 0; i < target.Count; i++)
            {
                // conbineNo
                var key = target.ElementAt(i).Key;
                int index = dataManager.parseInt(key);

                // define を構成する 基本荷重No群
                var item = JObject.FromObject(target.ElementAt(i).Value).ToObject<Dictionary<string, object>>();

                var _combine = new Combine();

                //var combNo = new Combine();
                var combNo = new List<int>();

                for (int j = 0; j < item.Count; j++)
                {
                    var id = item.ElementAt(j).Key;  // "C1", "C2"...
                    var val = item.ElementAt(j).Value;

                    if (id.Contains("name"))
                    {
                        _combine.name = val.ToString();
                    }
                    else if (id.Contains("C"))
                    {
                        double coef = dataManager.parseDouble(val);
                        _combine.coef.Add(id, coef);
                    }
                    else
                    {
                        continue;
                    }
                }

                foreach (int Key in this.combines.Keys)
                {
                    combNo.Add(Key);
                }

                this.combines.Add(index, _combine);
            }
        }



        // タイトル
        private string title;
        // 2次元か3次元か
        private int dimension;
        // テーブル
        private Table myTable;
        #region 印刷処理
        // 節点情報
        private InputNode Node = null;

        /// <summary>
        /// 印刷前の初期化処理
        /// </summary>
        private void printInit(PdfDocument mc, PrintData data)
        {
            this.dimension = data.dimension;

            //テーブルの作成
            this.myTable = new Table(1, 11);

            // テーブルの幅
            this.myTable.ColWidth[0] = 30.0; // 格点No
            this.myTable.ColWidth[1] = 20.0;
            this.myTable.ColWidth[2] = 160.0;
            this.myTable.ColWidth[3] = 35.0;
            this.myTable.ColWidth[4] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[5] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[6] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[7] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[8] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[9] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[10] = this.myTable.ColWidth[3];

            this.myTable[0, 1] = "";
            this.myTable[0, 0] = "CombNo";
            this.myTable[0, 3] = "C1";
            this.myTable[0, 4] = "C2";
            this.myTable[0, 5] = "C3";
            this.myTable[0, 6] = "C4";
            this.myTable[0, 7] = "C5";
            this.myTable[0, 8] = "C6";
            this.myTable[0, 9] = "C7";
            this.myTable[0, 10] = "C8";
            switch (data.language)
            {
                case "en":
                    this.title = "Combine DATA";
                    this.myTable[0, 2] = "Name of load";
                    break;

                case "cn":
                    this.title = "组合";
                    this.myTable[0, 2] = "载重名称";
                    break;

                default:
                    this.title = "Combine データ";
                    this.myTable[0, 2] = "荷重名称";
                    break;
            }


            this.myTable.AlignX[0, 0] = "L";    // 左寄せ
            this.myTable.AlignX[0, 3] = "R";    // 右寄せ
            this.myTable.AlignX[0, 4] = "R";    // 右寄せ
            this.myTable.AlignX[0, 5] = "R";    // 右寄せ
            this.myTable.AlignX[0, 6] = "R";    // 右寄せ
            this.myTable.AlignX[0, 7] = "R";    // 右寄せ
            this.myTable.AlignX[0, 8] = "R";    // 右寄せ
            this.myTable.AlignX[0, 9] = "R";    // 右寄せ
            this.myTable.AlignX[0, 10] = "R";    // 右寄せ

        }


        /// <summary>
        /// 1ページに入れるコンテンツを集計する 3次元の場合
        /// </summary>
        /// <param name="target">印刷対象の配列</param>
        /// <param name="rows">行数</param>
        /// <returns>印刷する用の配列</returns>
        private Table getPageContents3D(Dictionary<int, Combine> target)
        {
            int r = this.myTable.Rows;
            int rows = target.Count*2;

            // 行コンテンツを生成
            var table = this.myTable.Clone();
            table.ReDim(row: r + rows);
            table.RowHeight[1] = printManager.LineSpacing2; // 表題と body の間

            var _target = new List<Combine>();
            foreach (Combine Value in target.Values)
            {
                _target.Add(Value);
            }


            for (var i = 0; i < target.Count; i++)
            {
                if (r + 1 > rows)
                    break;

                Combine item = _target[i];

                var combNo = new List<int>();
                foreach (int Key in target.Keys)
                {
                    combNo.Add(Key);
                }

                var coef_Key = new List<string>();
                foreach (string Key in item.coef.Keys)
                {
                    string key = Key.Replace("C", "");
                    coef_Key.Add(key);
                }

                var coef_Value = new List<double>();
                foreach (double Value in item.coef.Values)
                {
                    coef_Value.Add(Value);
                }

                for (var j = 0; j < 3; j++)
                {
                    table[r, j] = printManager.toString(combNo[i]);
                    table.AlignX[r, j] = "R";
                    j++;
                    j++;
                    table[r, j] = printManager.toString(item.name);
                    table.AlignX[r, j] = "L";
                    j++;

                    if (item.coef.Count <= 8)　　//case８以下の時
                    {
                        for (var k = 0; k < item.coef.Count; k++)
                        {
                            table[r, j] = printManager.toString(coef_Key[k]);
                            table.AlignX[r, j] = "R";
                            table[r+1, j] = printManager.toString(coef_Value[k], 2);
                            table.AlignX[r+1, j] = "R";
                            j++;
                        }
                    }
                    else　　//case８以上の時
                    {
                        int linenum = 0;

                        for (var k = 0; k < item.coef.Count; k++)
                        {
                            if (r + 1 > rows)
                                break;
                            table[r, j] = printManager.toString(coef_Key[k]);
                            table.AlignX[r, j] = "R";
                            table[r + 1, j] = printManager.toString(coef_Value[k], 2);
                            table.AlignX[r+1, j] = "R";
                            j++;
                            if (k - 8 * linenum == 7 && k != 0)
                            {
                                if (k + 1 == item.coef.Count)
                                    break;
                                rows = rows + 2;
                                table.ReDim(row: this.myTable.Rows + rows);
                                j = 3;
                                r = r + 2;
                                linenum++;
                            }
                        }
                    }
                    r = r + 2;
                    break;
                }
            }

            return table;
        }

        private Dictionary<int, Combine> ReSize(Dictionary<int, Combine> target)
        {
            var item = target;

            var retmp = new Dictionary<int, Combine>();
            var _key = new List<int>();
            var _value = new List<Combine>();

            foreach (int key in item.Keys)
            {
                _key.Add(key);
            }

            foreach (Combine value in item.Values)
            {
                _value.Add(value);
            }

            var lin_num = _value.Count() % 8 + 1;

            for(int k = 0; k < lin_num; k++)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (_value.Count() <= 0)
                        break;

                    if (i == 0)
                    {
                        retmp.Add(_key[k], _value.First());
                        _value.Remove(_value.First());
                    }
                    else
                    {
                        retmp.Add(_key[k], _value.First());
                        _value.Remove(_value.First());
                    }
                }
            }

            return retmp;

        }

        #region PrintableBaseA.printPDF()用メソッド定義
        protected override bool HasAnyData() => combines.Count > 0;
        protected override void PrintInit(PdfDocument mc, PrintData data, out string[] titles, out int headerRows, out Table.NupInfo[] nupInfo)
        {
            printInit(mc, data);

            titles = new[] { title, };
            headerRows = myTable.Rows;
            nupInfo = Table.OneUpInfo;
        }
        protected override Table GetTable() => getPageContents3D(combines);
        #endregion

        #endregion
    }
}
