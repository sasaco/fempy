using Newtonsoft.Json.Linq;
using PDF_Manager.Comon;
using PDF_Manager.Printing.Comon;
using System.Collections.Generic;
using System.Linq;


namespace PDF_Manager.Printing
{
    public class Reac
    {
        public string id;    // 節点番号
        public double tx;
        public double ty;
        public double tz;
        public double mx;
        public double my;
        public double mz;

        // 組み合わせで使う
        public string caseStr = null;
        public string comb = null;
    }


    internal class ResultReac : PrintableBaseB
    {
        public const string KEY = "reac";

        private Dictionary<string, object> reacs = new Dictionary<string, object>();
        private Dictionary<string, string> reacnames = new Dictionary<string, string>();

        public ResultReac(Dictionary<string, object> value)
        {
            if (!value.ContainsKey(KEY))
                return;

            // データを取得する．
            var target = JObject.FromObject(value[KEY]).ToObject<Dictionary<string, object>>();

            // データを抽出する
            for (var i = 0; i < target.Count; i++)
            {
                var key = dataManager.toString(target.ElementAt(i).Key);  // ケース番号
                var val = JToken.FromObject(target.ElementAt(i).Value);

                if (val.Type == JTokenType.Array)
                {
                    JArray Dis = (JArray)val;
                    var _reac = new List<Reac>();
                    for (int j = 0; j < Dis.Count; j++)
                    {
                        JToken item = Dis[j];

                        var ds = new Reac();

                        ds.id = dataManager.toString(item["id"]);
                        ds.tx = dataManager.parseDouble(item["tx"]);
                        ds.ty = dataManager.parseDouble(item["ty"]);
                        ds.tz = dataManager.parseDouble(item["tz"]);
                        ds.mx = dataManager.parseDouble(item["mx"]);
                        ds.my = dataManager.parseDouble(item["my"]);
                        ds.mz = dataManager.parseDouble(item["mz"]);

                        _reac.Add(ds);

                    }
                    this.reacs.Add(key, _reac);

                }
                else if (val.Type == JTokenType.Object)
                {   // LL：連行荷重の時
                    var Rec = ((JObject)val).ToObject<Dictionary<string, object>>();
                    // var _reac = ResultReacCombine.getReacCombine(Rec);
                    this.reacs.Add(key, Rec);
                }

            }

            if (!value.ContainsKey("reacName"))
                return;

            // データを取得する．
            var targetName = JArray.FromObject(value["reacName"]);

            //LLか基本形かを判定しながら1行1行確認
            for (int i = 0; i < target.Count; i++)
            {
                // タイトルを入れる
                var load = targetName[i];
                string[] loadNew = new string[2];

                loadNew[0] = load[0].ToString();
                loadNew[1] = load[1].ToString();

                reacnames.Add(loadNew[0], loadNew[1]);

            }
        }

        ///印刷処理

        ///タイトル
        private string title;
        ///２次元か３次元か
        private int dimension;
        ///テーブル
        private Table myTable;
        ///印刷用テーブル
        private Table _table;


        ///印刷前の初期化処理
        ///
        private void printInit(PdfDocument mc, PrintData data)
        {
            this.dimension = data.dimension;

            if (this.dimension == 3)
            {///3次元

                ///テーブルの作成
                this.myTable = new Table(4, 7);

                ///テーブルの幅
                this.myTable.ColWidth[0] = 15.0;//節点No
                this.myTable.ColWidth[1] = 80.0;//X方向の移動量
                this.myTable.ColWidth[2] = 80.0;//Y方向の移動量
                this.myTable.ColWidth[3] = 80.0;//Z方向の移動量
                this.myTable.ColWidth[4] = 80.0;//X軸周りの回転量
                this.myTable.ColWidth[5] = 80.0;//Y軸周りの回転量
                this.myTable.ColWidth[6] = 80.0;//Z軸周りの回転量

                this.myTable.RowHeight[1] = printManager.LineSpacing2;

                this.myTable.AlignX[0, 0] = "L";
                this.myTable.AlignX[1, 0] = "L";
                this.myTable.AlignX[1, 2] = "R";
                this.myTable.AlignX[1, 3] = "R";
                this.myTable.AlignX[1, 5] = "R";
                this.myTable.AlignX[1, 6] = "R";
                this.myTable.AlignX[2, 0] = "L";
                this.myTable.AlignX[2, 1] = "R";
                this.myTable.AlignX[2, 2] = "R";
                this.myTable.AlignX[2, 3] = "R";
                this.myTable.AlignX[2, 4] = "R";
                this.myTable.AlignX[2, 5] = "R";
                this.myTable.AlignX[2, 6] = "R";
                this.myTable.AlignX[3, 1] = "R";
                this.myTable.AlignX[3, 2] = "R";
                this.myTable.AlignX[3, 3] = "R";
                this.myTable.AlignX[3, 4] = "R";
                this.myTable.AlignX[3, 5] = "R";
                this.myTable.AlignX[3, 6] = "R";

                // 表題
                this.myTable[3, 1] = "(kN)";
                this.myTable[3, 2] = "(kN)";
                this.myTable[3, 3] = "(kN)";
                this.myTable[3, 4] = "(kN・m)";
                this.myTable[3, 5] = "(kN・m)";
                this.myTable[3, 6] = "(kN・m)";

                switch (data.language)
                {
                    case "en":
                        this.title = "Supporting Reaction";
                        this.myTable[1, 0] = "Node";
                        this.myTable[2, 0] = "No";
                        this.myTable.AlignX[1, 2] = "L";
                        this.myTable[1, 2] = "Supporting Reaction";
                        this.myTable[2, 1] = "X";
                        this.myTable[2, 2] = "Y";
                        this.myTable[1, 3] = "";
                        this.myTable[2, 3] = "Z";
                        this.myTable.AlignX[1, 5] = "L";
                        this.myTable[1, 5] = "Rotational Reaction";
                        this.myTable[2, 4] = "X";
                        this.myTable[2, 5] = "Y";
                        this.myTable[1, 6] = "";
                        this.myTable[2, 6] = "Z";
                        break;

                    case "cn":
                        this.title = "支座反力";
                        this.myTable[1, 0] = "节点";
                        this.myTable[2, 0] = "编码";
                        this.myTable[1, 1] = "X方向的";
                        this.myTable[2, 1] = "支座反力";
                        this.myTable[1, 2] = "Y方向的";
                        this.myTable[2, 2] = "支座反力";
                        this.myTable[1, 3] = "Z方向的";
                        this.myTable[2, 3] = "支座反力";
                        this.myTable[1, 4] = "绕X轴的";
                        this.myTable[2, 4] = "旋转反力";
                        this.myTable[1, 5] = "绕Y轴的";
                        this.myTable[2, 5] = "旋转反力";
                        this.myTable[1, 6] = "绕Z轴的";
                        this.myTable[2, 6] = "旋转反力";
                        break;

                    default:
                        this.title = "反力データ";
                        this.myTable[1, 0] = "節点";
                        this.myTable[2, 0] = "No";
                        this.myTable[1, 1] = "X方向の";
                        this.myTable[2, 1] = "支点反力";
                        this.myTable[1, 2] = "Y方向の";
                        this.myTable[2, 2] = "支点反力";
                        this.myTable[1, 3] = "Z方向の";
                        this.myTable[2, 3] = "支点反力";
                        this.myTable[1, 4] = "X軸周りの";
                        this.myTable[2, 4] = "回転反力";
                        this.myTable[1, 5] = "Y軸周りの";
                        this.myTable[2, 5] = "回転反力";
                        this.myTable[1, 6] = "Z軸周りの";
                        this.myTable[2, 6] = "回転反力";
                        break;
                }

                //表題の文字位置
            }
            else
            {//2次元

                ///テーブルの作成
                this.myTable = new Table(4, 8);

                ///テーブルの幅
                this.myTable.ColWidth[0] = 20.0;//節点No
                this.myTable.ColWidth[1] = 72.5;//X方向の移動量
                this.myTable.ColWidth[2] = 72.5;//Y方向の移動量
                this.myTable.ColWidth[3] = 72.5;//X軸周りの回転量
                this.myTable.ColWidth[4] = 40.0;
                this.myTable.ColWidth[5] = this.myTable.ColWidth[1];
                this.myTable.ColWidth[6] = this.myTable.ColWidth[2];
                this.myTable.ColWidth[7] = this.myTable.ColWidth[3];

                this.myTable.RowHeight[1] = printManager.LineSpacing2;

                this.myTable.AlignX[0, 0] = "L";
                this.myTable.AlignX[1, 0] = "R";
                this.myTable.AlignX[1, 1] = "R";
                this.myTable.AlignX[1, 2] = "R";
                this.myTable.AlignX[1, 3] = "R";
                this.myTable.AlignX[1, 4] = "R";
                this.myTable.AlignX[1, 5] = "R";
                this.myTable.AlignX[1, 6] = "R";
                this.myTable.AlignX[1, 7] = "R";
                this.myTable.AlignX[2, 0] = "R";
                this.myTable.AlignX[2, 1] = "R";
                this.myTable.AlignX[2, 2] = "R";
                this.myTable.AlignX[2, 3] = "R";
                this.myTable.AlignX[2, 4] = "R";
                this.myTable.AlignX[2, 5] = "R";
                this.myTable.AlignX[2, 6] = "R";
                this.myTable.AlignX[2, 7] = "R";
                this.myTable.AlignX[3, 1] = "R";
                this.myTable.AlignX[3, 2] = "R";
                this.myTable.AlignX[3, 3] = "R";
                this.myTable.AlignX[3, 4] = "R";
                this.myTable.AlignX[3, 5] = "R";
                this.myTable.AlignX[3, 6] = "R";
                this.myTable.AlignX[3, 7] = "R";

                // 表題
                this.myTable[3, 1] = "(kN)";
                this.myTable[3, 2] = "(kN)";
                this.myTable[3, 3] = "(kN・m)";

                switch (data.language)
                {
                    case "en":
                        this.title = "Supporting Reaction";
                        this.myTable[1, 0] = "Node";
                        this.myTable[2, 0] = "No";
                        this.myTable.AlignX[1, 2] = "R";
                        this.myTable.AlignX[1, 5] = this.myTable.AlignX[1, 2];
                        this.myTable[1, 2] = "Supporting Reaction";
                        this.myTable[2, 1] = "X";
                        this.myTable[1, 1] = "";
                        this.myTable[2, 2] = "Y";
                        this.myTable[1, 3] = "Rotational";
                        this.myTable[2, 3] = "Reaction";
                        break;

                    case "cn":
                        this.title = "支座反力";
                        this.myTable[1, 0] = "节点";
                        this.myTable[2, 0] = "编码";
                        this.myTable[1, 1] = "X方向的";
                        this.myTable[2, 1] = "支座反力";
                        this.myTable[1, 2] = "Y方向的";
                        this.myTable[2, 2] = "支座反力";
                        this.myTable[2, 3] = "旋转反力";

                        break;

                    default:
                        this.title = "反力データ";
                        this.myTable[1, 0] = "節点";
                        this.myTable[2, 0] = "No";
                        this.myTable[1, 1] = "X方向の";
                        this.myTable[2, 1] = "支点反力";
                        this.myTable[1, 2] = "Y方向の";
                        this.myTable[2, 2] = "支点反力";
                        this.myTable[2, 3] = "回転反力";
                        break;
                }
                this.myTable[1, 4] = this.myTable[1, 0];
                this.myTable[2, 4] = this.myTable[2, 0];
                this.myTable[1, 5] = this.myTable[1, 1];
                this.myTable[2, 5] = this.myTable[2, 1];
                this.myTable[1, 6] = this.myTable[1, 2];
                this.myTable[2, 6] = this.myTable[2, 2];
                this.myTable[1, 7] = this.myTable[1, 3];
                this.myTable[2, 7] = this.myTable[2, 3];
                this.myTable[3, 5] = this.myTable[3, 1];
                this.myTable[3, 6] = this.myTable[3, 2];
                this.myTable[3, 7] = this.myTable[3, 3];

            }
        }

        /// <summary>
        /// 1ページに入れるコンテンツを集計する
        /// </summary>
        /// <param name="target">印刷対象の配列</param>
        /// <param name="rows">行数</param>
        /// <returns>印刷する用の配列</returns>
        private Table getPageContents(List<Reac> target)
        {
            int r = this.myTable.Rows;


            int columns = 1; // この段階ではデータを2段組みせず、printPDF()内で2段組みする
            int count = this.myTable.Columns;
            int c = count / columns;

            int rows = target.Count;


            // 行コンテンツを生成
            var table = this.myTable.Clone();
            table.ReDim(row: r + rows);

            if(table.RowHeight.Length < r)
                table.RowHeight[r] = printManager.LineSpacing2;

            if (dimension == 3)　　//３次元
            {
                for (var i = 0; i < rows; i++)
                {
                    var item = target[i];

                    int j = 0;
                    table[r, j] = printManager.toString(item.id);
                    table.AlignX[r, j] = "R";
                    j++;
                    table[r, j] = printManager.toString(item.tx, 2);
                    table.AlignX[r, j] = "R";
                    j++;
                    table[r, j] = printManager.toString(item.ty, 2);
                    table.AlignX[r, j] = "R";
                    j++;
                    table[r, j] = printManager.toString(item.tz, 2);
                    table.AlignX[r, j] = "R";
                    j++;
                    table[r, j] = printManager.toString(item.mx, 2);
                    table.AlignX[r, j] = "R";
                    j++;
                    table[r, j] = printManager.toString(item.my, 2);
                    table.AlignX[r, j] = "R";
                    j++;
                    table[r, j] = printManager.toString(item.mz, 2);
                    table.AlignX[r, j] = "R";
                    j++;

                    r++;
                }
            }

            else　　//２次元
            {
                int Rows = (target.Count + (columns - 1)) / columns;

                for (var i = 0; i < Rows; i++)
                {
                    for (var j = 0; j < columns; j++)
                    {
                        var index = i + Rows * j; //左側：j=0 ∴index = i, 右側：j=1, ∴index = i+Rows
                        if (target.Count <= index)
                            continue;

                        var item = target[index];

                        table[r + i, 0 + c * j] = printManager.toString(item.id);
                        table.AlignX[r + i, 0 + c * j] = "R";
                        table[r + i, 1 + c * j] = printManager.toString(item.tx, 2);
                        table.AlignX[r + i, 1 + c * j] = "R";
                        table[r + i, 2 + c * j] = printManager.toString(item.ty, 2);
                        table.AlignX[r + i, 2 + c * j] = "R";
                        table[r + i, 3 + c * j] = printManager.toString(item.mz, 2);
                        table.AlignX[r + i, 3 + c * j] = "R";
                    }
                }

            }

            return table;
        }

        #region PrintableBaseB.printPDF()用メソッド定義
        protected override bool HasAnyData() => reacs.Any();
        protected override void PrintInit(PdfDocument mc, PrintData data, out string[] titles, out int headerRows, out Table.NupInfo[] nupInfo)
        {
            // タイトル などの初期化
            printInit(mc, data);

            titles = new[] { title, };
            headerRows = myTable.Rows;
            nupInfo = dimension == 3 ? Table.OneUpInfo : new[] { new Table.NupInfo(0, 3), new Table.NupInfo(4, 7), };
        }
        protected override IEnumerable<Table> GetTables(PdfDocument mc, PrintData data,  int indexPage)
        {
            if(reacnames.Count == 0) yield break;

            for (var j = 0; j < reacs.Count; j++)
            {
                var key = reacs.ElementAt(j).Key;  // ケース番号

                var caseNo = reacnames.ElementAt(j).Key;
                var caseName = reacnames.ElementAt(j).Value;
                var ds = reacs.ElementAt(j).Value;

                // LL：連行荷重の時 -----------------------------------------------------------
                if (ds is Dictionary<string, object>)
                {
                    // 行間が狭いので、改行無効化の取り消し
                    mc.addCurrentY(+printManager.FontHeight); // Table.LineSpacing3

                    // ResultReacCombine クラスに印刷してもらう
                    var dct1 = new Dictionary<string, object>() { { key, ds } };
                    var dct2 = new List<string[]>() { new string[] { caseNo, caseName } };
                    var dct3 = new Dictionary<string, object>(){
                        { "reacCombine", dct1 },
                        { "reacCombineName", dct2 }
                        };
                    var dg = new ResultReacCombine(dct3);
                    dg.printPDF(mc, data, ref indexPage);
                    continue;
                }
                // ---------------------------------------------------------------------------

                var table = getPageContents(ds as List<Reac>);
                table[0, 0] = caseNo + " " + caseName;

                yield return table;
            }
        }
        protected override bool RequiresNewpage(PdfDocument mc, int[] printableRows, int tableRows, int headerRows, ref int judgeCount)
        {
            if (++judgeCount > 1)
            {
                // 2回目以降の判定。データ行が1行も入らない場合は改ページ
                return printableRows[0] < headerRows + 1;
            }
            else
            {
                // 1回目の判定
                // 現在のページに何も出力されていない場合は改ページしない
                if (mc.currentPos.Equals(mc.initialPos))
                {
                    return false;
                }
                // ケース全体が出力できなければ改ページ
                return printableRows[0] < tableRows;
            }
        }
        #endregion
    }
}
