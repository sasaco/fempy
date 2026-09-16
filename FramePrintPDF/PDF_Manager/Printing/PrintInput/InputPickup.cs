using Newtonsoft.Json.Linq;
using PDF_Manager.Comon;
using PDF_Manager.Printing.Comon;
using System.Collections.Generic;
using System.Linq;


namespace PDF_Manager.Printing
{
    public class Pickup
    {
        public string name;
        public Dictionary<string, int> com = new Dictionary<string, int>();
    }



    internal class InputPickup : PrintableBaseA
    {
        public const string KEY = "pickup";

        public Dictionary<int, Pickup> pickups = new Dictionary<int, Pickup>();

        public InputPickup(Dictionary<string, object> value)
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

                var _pickup = new Pickup();
                for (int j = 0; j < item.Count; j++)
                {
                    var id = item.ElementAt(j).Key;  // "C1", "C2"...
                    var val = item.ElementAt(j).Value;

                    if (id.Contains("name"))
                    {
                        _pickup.name = val.ToString();
                    }
                    else if (id.Contains("C"))
                    {
                        int no = dataManager.parseInt(val);
                        _pickup.com.Add(id, no);
                    }
                    else
                    {
                        continue;
                    }
                }
                this.pickups.Add(index, _pickup);
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
            this.myTable = new Table(1, 13);

            // テーブルの幅
            this.myTable.ColWidth[0] = 30.0; // 格点No
            this.myTable.ColWidth[1] = 20.0;
            this.myTable.ColWidth[2] = 160.0;
            this.myTable.ColWidth[3] = 30.0;
            this.myTable.ColWidth[4] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[5] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[6] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[7] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[8] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[9] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[10] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[11] = this.myTable.ColWidth[3];
            this.myTable.ColWidth[12] = this.myTable.ColWidth[3];

            // 表題
            this.myTable[0, 1] = "";
            this.myTable[0, 3] = "C1";
            this.myTable[0, 4] = "C2";
            this.myTable[0, 5] = "C3";
            this.myTable[0, 6] = "C4";
            this.myTable[0, 7] = "C5";
            this.myTable[0, 8] = "C6";
            this.myTable[0, 9] = "C7";
            this.myTable[0, 10] = "C8";
            this.myTable[0, 11] = "C9";
            this.myTable[0, 12] = "C10";
            switch (data.language)
            {
                case "en":
                    this.title = "PickUp DATA";
                    this.myTable[0, 0] = "No";
                    this.myTable[0, 2] = "Load name";
                    break;

                case "cn":
                    this.title = "聚集";
                    this.myTable[0, 0] = "编码";
                    this.myTable[0, 2] = "载重名称";
                    break;

                default:
                    this.title = "PickUpデータ";
                    this.myTable[0, 0] = "No";
                    this.myTable[0, 2] = "荷重名称";
                    break;
            }

            this.myTable.AlignX[0, 0] = "R";    // 左寄せ
            this.myTable.AlignX[0, 3] = "R";    // 右寄せ
            this.myTable.AlignX[0, 4] = "R";    // 右寄せ
            this.myTable.AlignX[0, 5] = "R";    // 右寄せ
            this.myTable.AlignX[0, 6] = "R";    // 右寄せ
            this.myTable.AlignX[0, 7] = "R";    // 右寄せ
            this.myTable.AlignX[0, 8] = "R";    // 右寄せ
            this.myTable.AlignX[0, 9] = "R";    // 右寄せ
            this.myTable.AlignX[0, 10] = "R";    // 右寄せ
            this.myTable.AlignX[0, 11] = "R";    // 右寄せ
            this.myTable.AlignX[0, 12] = "R";    // 右寄せ

        }


        /// <summary>
        /// 1ページに入れるコンテンツを集計する 3次元の場合
        /// </summary>
        /// <param name="target">印刷対象の配列</param>
        /// <param name="rows">行数</param>
        /// <returns>印刷する用の配列</returns>
        private Table getPageContents3D(Dictionary<int, Pickup> target)
        {
            int r = this.myTable.Rows;
            int rows = target.Count;

            // 行コンテンツを生成
            var table = this.myTable.Clone();
            table.ReDim(row: r + rows);
            table.RowHeight[1] = printManager.LineSpacing2; // 表題と body の間

            var _target = new List<Pickup>();
            foreach (Pickup Value in target.Values)
            {
                _target.Add(Value);
            }


            for (var i = 0; i < target.Count; i++)
            {
                //if (r + 1 > rows)
                //    break;

                Pickup item = _target[i];

                var No = new List<int>();
                foreach (int Key in target.Keys)
                {
                    No.Add(Key);
                }

                var com_Key = new List<string>();
                foreach (string Key in item.com.Keys)
                {
                    string key = Key.Replace("C", "");
                    com_Key.Add(key);
                }

                var com_Value = new List<double>();
                foreach (double Value in item.com.Values)
                {
                    com_Value.Add(Value);
                }

                for (var j = 0; j < 3; j++)// PickUpNoのループ
                {
                    table[r, j] = printManager.toString(No[i]);
                    table.AlignX[r, j] = "R";
                    j++;
                    j++;
                    table[r, j] = printManager.toString(item.name);
                    table.AlignX[r, j] = "L";
                    j++;

                    if (item.com.Count <= 8)　　//case10以下の時
                    {
                        for (var k = 0; k < item.com.Count; k++)
                        {
                            table[r, j] = printManager.toString(com_Value[k]);
                            table.AlignX[r, j] = "R";
                            j++;
                        }
                    }
                    else
                    {
                        int linenum = 0;

                        for (var k = 0; k < item.com.Count; k++)
                        {
                            if (r + 1 > rows)
                                break;
                            table[r, j] = printManager.toString(com_Value[k]);
                            table.AlignX[r, j] = "R";
                            j++;
                            if (k - 10 * linenum == 9 && k != 0)
                            {
                                if (k + 1 == item.com.Count)
                                    break;
                                rows = rows + 2;
                                table.ReDim(row: r + rows);
                                j = 3;
                                r++;
                                linenum++;
                            }
                        }
                    }
                    r++;
                    break;
                }
            }

            return table;
        }

        #region PrintableBaseA.printPDF()用メソッド定義
        protected override bool HasAnyData() => pickups.Count > 0;
        protected override void PrintInit(PdfDocument mc, PrintData data, out string[] titles, out int headerRows, out Table.NupInfo[] nupInfo)
        {
            printInit(mc, data);

            titles = new[] { title, };
            headerRows = myTable.Rows;
            nupInfo = Table.OneUpInfo;
        }
        protected override Table GetTable() => getPageContents3D(pickups);
        #endregion

        #endregion
    }
}
