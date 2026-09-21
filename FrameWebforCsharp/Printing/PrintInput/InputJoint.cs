using Newtonsoft.Json.Linq;
using PDF_Manager.Comon;
using PDF_Manager.Printing.Comon;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    public class Joint
    {
        public string m;   // 部材番号
        public int? xi;
        public int? yi;
        public int? zi;
        public int? xj;
        public int? yj;
        public int? zj;
    }


    internal class InputJoint : PrintableBaseB
    {
        public const string KEY = "joint";

        private Dictionary<int, List<Joint>> joints = new Dictionary<int, List<Joint>>();

        public InputJoint(Dictionary<string, object> value)
        {
            if (!value.ContainsKey(KEY))
                return;

            // データを取得する．
            var target = JObject.FromObject(value[KEY]).ToObject<Dictionary<string, object>>();

            // データを抽出する
            for (var i = 0; i < target.Count; i++)
            {
                var key = dataManager.parseInt(target.ElementAt(i).Key);  // タイプ番号
                JArray Joi = JArray.FromObject(target.ElementAt(i).Value);

                var _joint = new List<Joint>();

                for (int j = 0; j < Joi.Count; j++)
                {
                    JToken item = Joi[j];

                    var jo = new Joint();

                    jo.m = dataManager.toString(item["m"]);
                    jo.xi = dataManager.isNull(item["xi"]) ? (int?)null : dataManager.parseInt(item["xi"]);
                    jo.yi = dataManager.isNull(item["yi"]) ? (int?)null : dataManager.parseInt(item["yi"]);
                    jo.zi = dataManager.isNull(item["zi"]) ? (int?)null : dataManager.parseInt(item["zi"]);
                    jo.xj = dataManager.isNull(item["xj"]) ? (int?)null : dataManager.parseInt(item["xj"]);
                    jo.yj = dataManager.isNull(item["yj"]) ? (int?)null : dataManager.parseInt(item["yj"]);
                    jo.zj = dataManager.isNull(item["zj"]) ? (int?)null : dataManager.parseInt(item["zj"]);

                    _joint.Add(jo);

                }
                this.joints.Add(key, _joint);
            }
        }

        ///印刷処理

        ///タイトル
        private string title;
        ///２次元か３次元か
        private int dimension;
        ///テーブル(Member)
        private Table myTable;
        ///テーブル(印刷用)
        private Table _table;
        ///テーブル(印刷用)
        private Table table;
        ///節点情報
        private InputNode Node = null;
        ///材料情報
        private InputElement Element = null;

        private void printJoint(PrintData data)
        {
            if (dimension == 3)//３次元
            {
                ///テーブルの作成
                this.myTable = new Table(3, 7);

                ///テーブルの幅
                this.myTable.ColWidth[0] = 20.0;//節点荷重
                this.myTable.ColWidth[1] = 60.0;//X
                this.myTable.ColWidth[2] = this.myTable.ColWidth[1];//Y
                this.myTable.ColWidth[3] = this.myTable.ColWidth[2];//Z
                this.myTable.ColWidth[4] = this.myTable.ColWidth[3];//X
                this.myTable.ColWidth[5] = this.myTable.ColWidth[4];//Y
                this.myTable.ColWidth[6] = this.myTable.ColWidth[5];//Z

                myTable.RowHeight[1] = printManager.LineSpacing2;

                // 表題
                this.title = "結合データ";
                this.myTable[1, 2] = "I端側";
                this.myTable[1, 5] = "J端側";
                this.myTable[2, 1] = "X";
                this.myTable[2, 2] = "Y";
                this.myTable[2, 3] = "Z";
                this.myTable[2, 4] = "X";
                this.myTable[2, 5] = "Y";
                this.myTable[2, 6] = "Z";

                //表題の文字位置
                this.myTable.AlignX[0, 0] = "L";    // 左寄せ
                this.myTable.AlignX[1, 0] = "L";    // 左寄せ
                this.myTable.AlignX[2, 0] = "R";    // 左寄せ
                this.myTable.AlignX[2, 1] = "R";
                this.myTable.AlignX[2, 2] = "R";
                this.myTable.AlignX[2, 3] = "R";
                this.myTable.AlignX[2, 4] = "R";
                this.myTable.AlignX[2, 5] = "R";
                this.myTable.AlignX[2, 6] = "R";

            }
            else//2次元
            {
                ///テーブルの作成
                this.myTable = new Table(3, 6);

                ///テーブルの幅
                this.myTable.ColWidth[0] = 20.0;//節点荷重
                this.myTable.ColWidth[1] = 100.0;//節点番号
                this.myTable.ColWidth[2] = 40.0;//X
                this.myTable.ColWidth[3] = this.myTable.ColWidth[1];//Y
                this.myTable.ColWidth[4] = this.myTable.ColWidth[1];//R
                this.myTable.ColWidth[5] = this.myTable.ColWidth[2];//R

                myTable.RowHeight[1] = printManager.LineSpacing2;

                // 表題
                this.title = "結合データ";
                this.myTable[1, 0] = "部材";
                this.myTable[2, 0] = "No";
                this.myTable[1, 1] = "I端側";
                this.myTable[1, 2] = "J端側";
                this.myTable[1, 4] = this.myTable[1, 1];
                this.myTable[1, 5] = this.myTable[1, 2];
                this.myTable[1, 3] = this.myTable[1, 0];
                this.myTable[2, 3] = this.myTable[2, 0];
                this.myTable[2, 4] = this.myTable[2, 1];
                this.myTable[2, 5] = this.myTable[2, 2];

                //表題の文字位置
                this.myTable.AlignX[0, 0] = "L";    // 左寄せ
                this.myTable.AlignX[1, 0] = "L";    // 左寄せ
                this.myTable.AlignX[2, 0] = "R";    // 左寄せ
                this.myTable.AlignX[1, 3] = "L";
                this.myTable.AlignX[2, 0] = "R";
                this.myTable.AlignX[2, 3] = "R";
                this.myTable.AlignX[1, 3] = "R";
                //this.myTable.AlignX[2, 2] = "R";
                //this.myTable.AlignX[2, 4] = "R";
                //this.myTable.AlignX[2, 5] = "R";

            }
            // 表題（2次元と3次元共通部分）
            switch (data.language)
            {
                case "en":
                    this.myTable[1, 0] = "Node Load";
                    this.myTable[2, 0] = "Node No";
                    break;

                case "cn":
                    this.myTable[1, 0] = "节点载重";
                    this.myTable[2, 0] = "节点编码";
                    break;

                default:
                    this.myTable[1, 0] = "部材";
                    this.myTable[2, 0] = "No";
                    break;
            }
        }

        ///印刷前の初期化処理
        ///
        private void printInit(PdfDocument mc, PrintData data)
        {
            this.dimension = data.dimension;

            ////結合データのヘッダー作成
            printJoint(data);
        }

        /// <summary>
        /// 1ページに入れるコンテンツを集計する
        /// </summary>
        /// <param name="target">印刷対象の配列</param>
        /// <param name="rows">行数</param>
        /// <returns>印刷する用の配列</returns>
        private Table getPageContents(Dictionary<int, List<Joint>> target)
        {
            int r = this.myTable.Rows;
            var list = new List<Joint>();

            // 行コンテンツを生成
            var table = this.myTable.Clone();

            for (var i = 0; i < target.Count; i++)// typeのループ
            {
                if (target.Count <= i)
                    break;

                if (dimension == 3)//3次元
                {
                    int rows = target.ElementAt(i).Value.Count;
                    table.ReDim(row: r + rows);

                    table.RowHeight[r] = printManager.LineSpacing2;

                    int No = target.ElementAt(i).Key;

                    list = target.ElementAt(i).Value;

                    for (int ii = 0; ii < list.Count; ii++)// 中身のループ
                    {
                        int j = 0;
                        table[r, j] = printManager.toString(list[ii].m);
                        table.AlignX[r, j] = "R";
                        j++;
                        table[r, j] = printManager.toString(list[ii].xi);
                        table.AlignX[r, j] = "R";
                        j++;
                        table[r, j] = printManager.toString(list[ii].yi);
                        table.AlignX[r, j] = "R";
                        j++;
                        table[r, j] = printManager.toString(list[ii].zi);
                        table.AlignX[r, j] = "R";
                        j++;
                        table[r, j] = printManager.toString(list[ii].xj);
                        table.AlignX[r, j] = "R";
                        j++;
                        table[r, j] = printManager.toString(list[ii].yj);
                        table.AlignX[r, j] = "R";
                        j++;
                        table[r, j] = printManager.toString(list[ii].zj);
                        table.AlignX[r, j] = "R";
                        j++;

                        r++;
                    }
                }
                else//２次元
                {
                    list = target.ElementAt(i).Value;

                    int columns = 1; // この段階ではデータを2段組みせず、printPDF()内で2段組みする

                    int count = this.myTable.Columns;
                    int c = count / columns;

                    int rows = list.Count;

                    // 行コンテンツを生成
                    if (columns >= 1)
                    {
                        rows = rows / columns;
                        if (rows % 2 != 0)
                        {
                            rows++;
                        }
                    }
                    table.ReDim(row: r + rows);

                    table.RowHeight[r] = printManager.LineSpacing2;

                    int Rows = list.Count / columns;


                    for (var ii = 0; ii < Rows; ii++)
                    {
                        for (var j = 0; j < columns; j++)
                        {
                            var index = ii + Rows * j; //左側：j=0 ∴index = ii, 右側：j=1, ∴index = ii+rows
                            if (list.Count <= index)
                                continue;

                            var item = list[index];

                            table[r + ii, 0 + c * j] = printManager.toString(item.m);
                            table.AlignX[r + ii, 0 + c * j] = "R";
                            table[r + ii, 1 + c * j] = printManager.toString(item.zi);
                            table[r + ii, 2 + c * j] = printManager.toString(item.zj);
                        }
                    }

                    return table;

                }
            }
            if (table != null)
            {
                table.ClearDraft();
                if (table.Rows == this.myTable.Rows)
                {
                    table = null;
                }
            }
            return table;
        }

        #region PrintableBaseB.printPDF()用メソッド定義
        protected override bool HasAnyData() => joints.Any();
        protected override void PrintInit(PdfDocument mc, PrintData data, out string[] titles, out int headerRows, out Table.NupInfo[] nupInfo)
        {
            // タイトル などの初期化
            printInit(mc, data);

            titles = new[] { title, };
            headerRows = myTable.Rows;
            nupInfo = dimension == 3 ? Table.OneUpInfo : new[] { new Table.NupInfo(0, 2), new Table.NupInfo(3, 5), };
        }
        protected override IEnumerable<Table> GetTables(PdfDocument mc, PrintData data, int indexPage)
        {
            foreach (var tmp0 in joints)
            {
                var table = getPageContents(new Dictionary<int, List<Joint>> { { tmp0.Key, tmp0.Value }, });
                table[0, 0] = string.Format("Type{0}", tmp0.Key); // タイプ番号

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
                // ケース(Type)全体が出力できなければ改ページ
                return printableRows[0] < tableRows;
            }
        }
        #endregion
    }


    /*
    // 集まったデータはすべてここに格納する
    List<string> title = new List<string>();
    List<List<string[]>> data = new List<List<string[]>>();

    title = new List<string>();
    data = new List<List<string[]>>();


    for (int i = 0; i < target.Count; i++)
    {
        JArray Elem = JArray.FromObject(target.ElementAt(i).Value);

        // タイトルを入れる．
        switch (mc.language)
        {
            case "ja":
                title.Add("タイプ" + target.ElementAt(i).Key);
                break;
            case "en":
                title.Add("Type" + target.ElementAt(i).Key);
                break;
        }

        List<string[]> table = new List<string[]>();

        for (int j = 0; j < Elem.Count; j++)
        {
            JToken item = Elem[j];

            string[] line = new String[7];

            line[0] = dataManager.TypeChange(item["m"]);
            line[1] = mc.Dimension(dataManager.TypeChange(item["xi"]));
            line[2] = mc.Dimension(dataManager.TypeChange(item["yi"]));
            line[3] = dataManager.TypeChange(item["zi"]);
            line[4] = mc.Dimension(dataManager.TypeChange(item["xj"]));
            line[5] = mc.Dimension(dataManager.TypeChange(item["yj"]));
            line[6] = dataManager.TypeChange(item["zj"]);

            table.Add(line);
        }
        data.Add(table);
    }
    */
    /*
    public void JointPDF(PdfDoc mc)
    {
        // 全行の取得
        int count = 20;
        for (int i = 0; i < title.Count; i++)
        {
            count += (data[i].Count + 6) * mc.single_Yrow;
        }
        // 改ページ判定
        mc.DataCountKeep(count);

        //　ヘッダー
        string[,] header_content3D = {
            { "部材", "", "i端側", "", "", "j端側", "" },
            { "No", "X", "Y", "Z", "X", "Y", "Z" },
        };
        string[,] header_content2D = {
            { "部材", "", "i端側", "", "", "j端側", "" },
            { "No", "", "", "Z", "", "", "Z" },
        };

        // ヘッダーのx方向の余白
        int[,] header_Xspacing3D = {
            { 10, 60, 120, 180, 240, 300, 360 },
            { 10, 60, 120, 180, 240, 300, 360 },
        };

        int[,] header_Xspacing2D = {
            { 10, 0, 120, 0, 0, 300, 0 },
            { 10, 0, 0, 120, 0, 0, 300 },
        };

        // ボディーのx方向の余白　-1
        int[,] body_Xspacing3D = {
            { 17, 67, 127, 187, 247, 307, 367 }
        };

        int[,] body_Xspacing2D = {
            { 17, 0, 0, 127, 0, 0, 307 }
        };

        // タイトルの印刷
        switch (mc.language)
        {
            case "ja":
                mc.PrintContent("結合データ", 0);
                break;
            case "en":
                mc.PrintContent("Fixity DATA", 0);
                //　ヘッダー
                header_content3D[0, 0] = "Member";
                header_content3D[0, 2] = "Node-I";
                header_content3D[0, 5] = "Node-J";

                header_content2D[0, 0] = "Member";
                header_content2D[0, 2] = "Node-I";
                header_content2D[0, 5] = "Node-J";

                break;
        }
        mc.CurrentRow(2);
        mc.CurrentColumn(0);

        string[,] header_content = mc.dimension == 3 ? header_content3D : header_content2D;
        int[,] header_Xspacing = mc.dimension == 3 ? header_Xspacing3D : header_Xspacing2D;
        int[,] body_Xspacing = mc.dimension == 3 ? body_Xspacing3D : body_Xspacing2D;

        int k = 0;

        for (int i = 0; i < data.Count; i++)
        {
            //  1タイプ内でページをまたぐかどうか
            mc.TypeCount(i, 6, data[i].Count, title[i]);

            // タイプの印刷
            mc.CurrentColumn(0);
            mc.PrintContent(title[i], 0);
            mc.CurrentRow(2);


            // ヘッダーの印刷
            mc.Header(header_content, header_Xspacing);

            for (int j = 0; j < data[i].Count; j++)
            {
                for (int l = 0; l < data[i][j].Length; l++)
                {
                    mc.CurrentColumn(body_Xspacing[k, l]); //x方向移動
                    mc.PrintContent(data[i][j][l]); // print
                }
                if (i == data.Count - 1 && j == data[i].Count - 1)
                {
                    mc.CurrentRow(1); // y方向移動
                }
            }
        }

    }
    */
}

