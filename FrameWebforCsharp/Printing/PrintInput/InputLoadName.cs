using Newtonsoft.Json.Linq;
using PDF_Manager.Comon;
using PDF_Manager.Printing.Comon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PDF_Manager.Printing
{
    public class LoadName
    {
        public double rate;
        public string symbol;
        public string Actual_load;
        public string name;
        public int fix_node;
        public int element;
        public int fix_member;
        public int joint;
    }

    internal class InputLoadName : PrintableBaseA
    {
        public const string KEY = "loadName";

        public Dictionary<int, LoadName> loadnames = new Dictionary<int, LoadName>();

        public InputLoadName(Dictionary<string, object> value)
        {
            if (!value.ContainsKey(InputLoad.KEY))
                return;

            //nodeデータを取得する
            var target = JObject.FromObject(value[InputLoad.KEY]).ToObject<Dictionary<string, object>>();

            // データを抽出する
            for (var i = 0; i < target.Count; i++)
            {

                var key = target.ElementAt(i).Key;
                if (int.TryParse(key, out int index)) // 連行荷重は小数点の荷重番号で排除する
                {
                    var item = JObject.FromObject(target.ElementAt(i).Value);
                    var ln = new LoadName();

                    ln.rate = dataManager.parseDouble(item["rate"]);
                    ln.symbol = dataManager.toString(item["symbol"]);
                    ln.name = dataManager.toString(item["name"]);
                    ln.fix_node = dataManager.parseInt(item["fix_node"]);
                    ln.element = dataManager.parseInt(item["element"]);
                    ln.fix_member = dataManager.parseInt(item["fix_member"]);
                    ln.joint = dataManager.parseInt(item["joint"]);

                    this.loadnames.Add(index, ln);

                }
            }
        }

        ///印刷処理

        ///タイトル
        private string title;
        ///２次元か３次元か
        private int dimension;
        ///テーブル
        private Table myTable;
        ///節点情報
        private InputNode Node = null;
        ///材料情報
        private InputElement Element = null;


        ///印刷前の初期化処理
        ///
        private void printInit(PdfDocument mc, PrintData data)
        {
            this.dimension = data.dimension;

            ///テーブルの作成
            this.myTable = new Table(2, 9);

            ///テーブルの幅
            this.myTable.ColWidth[0] = 45.0;//Case
            this.myTable.ColWidth[1] = 60.0;//割増係数
            this.myTable.ColWidth[2] = 30.0;//記号
            this.myTable.ColWidth[3] = 240.0;//荷重名称
            this.myTable.ColWidth[4] = 25.0;//支点
            this.myTable.ColWidth[5] = 25.0;//断面
            this.myTable.ColWidth[6] = 25.0;//部材
            this.myTable.ColWidth[7] = 25.0;//地盤

            switch (data.language)
            {
                case "en":
                    this.title = "Basic Case";
                    this.myTable[0, 0] = "Case";
                    this.myTable[1, 0] = "No";
                    this.myTable[1, 1] = "C.F";
                    this.myTable[1, 2] = "Symbol";
                    this.myTable[1, 3] = "Load name";
                    this.myTable[0, 4] = "　　Structural conditions";
                    this.myTable[1, 4] = "Support";
                    this.myTable[1, 5] = "Section";
                    this.myTable[1, 6] = "Spring";
                    this.myTable[1, 7] = "Joint";
                    break;

                case "cn":
                    this.title = "基本载重案例";
                    this.myTable[0, 0] = "Case";
                    this.myTable[1, 0] = "No";
                    this.myTable[1, 1] = "附加系数";
                    this.myTable[1, 2] = "符号";
                    this.myTable[1, 3] = "载重名称";
                    this.myTable[0, 4] = "　　结构条件";
                    this.myTable[1, 4] = "支点";
                    this.myTable[1, 5] = "截面";
                    this.myTable[1, 6] = "弹簧";
                    this.myTable[1, 7] = "连接";
                    break;

                default:
                    this.title = "基本荷重DATA";
                    this.myTable[0, 0] = "Case";
                    this.myTable[1, 0] = "No";
                    this.myTable[1, 1] = "割増係数";
                    this.myTable[1, 2] = "記号";
                    this.myTable[1, 3] = "荷重名称";
                    this.myTable[0, 4] = "　　構造系条件";
                    this.myTable[1, 4] = "支点";
                    this.myTable[1, 5] = "断面";
                    this.myTable[1, 6] = "バネ";
                    this.myTable[1, 7] = "結合";
                    break;
            }

            //表題の文字位置
            this.myTable.AlignX[0, 5] = "L";    // 左寄せ
        }

        /// <summary>
        /// 1ページに入れるコンテンツを集計する
        /// </summary>
        /// <param name="target">印刷対象の配列</param>
        /// <param name="rows">行数</param>
        /// <returns>印刷する用の配列</returns>
        private Table getPageContents(Dictionary<int, LoadName> target)
        {
            int r = this.myTable.Rows;
            int rows = target.Count;

            // 行コンテンツを生成
            var table = this.myTable.Clone();
            table.ReDim(row: r + rows);

            table.RowHeight[r] = printManager.LineSpacing2;

            for (var i = 0; i < rows; i++)
            {
                int No = target.ElementAt(i).Key;
                LoadName item = target.ElementAt(i).Value;

                int j = 0;
                table[r, j] = No.ToString();
                table.AlignX[r, j] = "R";
                j++;
                table[r, j] = printManager.toString(item.rate, 3);
                ///table.AlignX[r, j] = "R";
                j++;
                table[r, j] = printManager.toString(item.symbol);
                table.AlignX[r, j] = "L";
                j++;
                table[r, j] = printManager.toString(item.name);
                table.AlignX[r, j] = "L";
                j++;
                table[r, j] = printManager.toString(item.fix_node);
                j++;
                table[r, j] = printManager.toString(item.element);
                j++;
                table[r, j] = printManager.toString(item.fix_member);
                j++;
                table[r, j] = printManager.toString(item.joint);
                j++;

                r++;
            }

            return table;
        }

        #region PrintableBaseA.printPDF()用メソッド定義
        protected override bool HasAnyData() => loadnames.Count > 0;
        protected override void PrintInit(PdfDocument mc, PrintData data, out string[] titles, out int headerRows, out Table.NupInfo[] nupInfo)
        {
            printInit(mc, data);

            titles = new[] { title, };
            headerRows = myTable.Rows;
            nupInfo = Table.OneUpInfo;
        }
        protected override Table GetTable() => getPageContents(loadnames);
        protected override bool RequiresNewpage(int[] printableRows, int tableRows, int headerRows)
        {
            // ページに入る行数が5行未満なら改ページ。ただし、テーブル全体が5行未満の場合を除く
            return printableRows[0] < Math.Min(5, tableRows);
        }
        #endregion
    }
}