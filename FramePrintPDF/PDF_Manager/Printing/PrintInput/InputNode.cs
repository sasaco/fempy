using Newtonsoft.Json.Linq;
using PDF_Manager.Comon;
using PDF_Manager.Printing.Comon;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    public class Vector3
    {
        private double _x;
        private double _y;
        private double _z;

        public double x
        {
            get
            {
                return this._x;
            }
            set
            {
                this._x = double.IsNaN(value) ? 0 : value;
            }
        }
        public double y
        {
            get
            {
                return this._y;
            }
            set
            {
                this._y = double.IsNaN(value) ? 0 : value;
            }
        }
        public double z
        {
            get
            {
                return this._z;
            }
            set
            {
                this._z = double.IsNaN(value) ? 0 : value;
            }
        }
    }

    internal class InputNode : PrintableBaseA
    {
        # region 初期化・データの集計

        public const string KEY = "node";

        private Dictionary<string, Vector3> nodes = new Dictionary<string, Vector3>();

        /// <summary>
        /// データを読み込む
        /// </summary>
        /// <param name="dataManager"></param>
        /// <param name="value"></param>
        public InputNode(Dictionary<string, object> value)
        {
            if (!value.ContainsKey(KEY))
                return;

            //nodeデータを取得する
            var target = JObject.FromObject(value[KEY]).ToObject<Dictionary<string, object>>();

            // データを抽出する
            for (var i = 0; i < target.Count; i++)
            {
                var key = target.ElementAt(i).Key;
                var item = JObject.FromObject(target.ElementAt(i).Value);

                var pos = new Vector3();
                pos.x = dataManager.parseDouble(item["x"]);
                pos.y = dataManager.parseDouble(item["y"]);
                pos.z = dataManager.parseDouble(item["z"]);
                this.nodes.Add(key, pos);
            }
        }

        #endregion


        #region 印刷処理

        // タイトル
        private string title;
        // 2次元か3次元か
        private int dimension;
        // テーブル
        private Table myTable;

        /// <summary>
        /// 印刷前の初期化処理
        /// </summary>
        private int printInit(PdfDocument mc, PrintData data)
        {
            int columns = 0; // 列数で３次元の場合は2列、2次元の場合は3列

            this.dimension = data.dimension;
            if (this.dimension == 3)
            {   // 3次元
                columns = 2;

                int cols = columns * 4 + 1;

                //テーブルの作成
                this.myTable = new Table(2, cols);

                // テーブルの幅
                for (var i = 0; i < cols; i++)
                    this.myTable.ColWidth[i] = 85.0;
                this.myTable.ColWidth[0] = 15.0; // 格点No
                this.myTable.ColWidth[1] = 75.0; // 格点No
                this.myTable.ColWidth[2] = this.myTable.ColWidth[1]; // 格点No
                this.myTable.ColWidth[3] = this.myTable.ColWidth[1]; // 格点No
                this.myTable.ColWidth[4] = 20.0; // 格点No
                this.myTable.ColWidth[5] = this.myTable.ColWidth[0];
                this.myTable.ColWidth[6] = this.myTable.ColWidth[1]; // 格点No
                this.myTable.ColWidth[7] = this.myTable.ColWidth[1]; // 格点No
                this.myTable.ColWidth[8] = this.myTable.ColWidth[1]; // 格点No


                // ヘッダー
                switch (data.language)
                {
                    case "en":
                        this.title = "Node Data";
                        this.myTable[0, 0] = "Node";
                        this.myTable[1, 0] = "No";
                        this.myTable[1, 1] = "X";
                        this.myTable[1, 2] = "Y";
                        this.myTable[1, 3] = "Z";
                        break;

                    case "cn":
                        this.title = "节点";
                        this.myTable[0, 0] = "节点";
                        this.myTable[1, 0] = "No";
                        this.myTable[1, 1] = "X";
                        this.myTable[1, 2] = "Y";
                        this.myTable[1, 3] = "Z";
                        break;

                    default:
                        this.title = "格点データ";
                        this.myTable[0, 0] = "格点";
                        this.myTable[1, 0] = "No";
                        this.myTable[1, 1] = "X";
                        this.myTable[1, 2] = "Y";
                        this.myTable[1, 3] = "Z";
                        break;
                }
                this.myTable[0, 5] = this.myTable[0, 0];
                this.myTable[1, 5] = this.myTable[1, 0];
                this.myTable[1, 6] = this.myTable[1, 1];
                this.myTable[1, 7] = this.myTable[1, 2];
                this.myTable[1, 8] = this.myTable[1, 3];
            }
            else
            {   // 2次元
                columns = 3;

                int cols = columns * 3;

                //テーブルの作成
                this.myTable = new Table(2, cols);

                // テーブルの幅
                for (var i = 0; i < cols; i++)
                    this.myTable.ColWidth[i] = 70;
                this.myTable.ColWidth[0] = 20.0; // 格点No
                this.myTable.ColWidth[3] = 40;
                this.myTable.ColWidth[6] = this.myTable.ColWidth[3];

                // ヘッダー
                switch (data.language)
                {
                    case "en":
                        this.title = "Node Data";
                        this.myTable[0, 0] = "Node";
                        this.myTable[1, 0] = "No";
                        this.myTable[1, 1] = "X　";
                        this.myTable[1, 2] = "Y　";
                        break;

                    case "cn":
                        this.title = "节点";
                        this.myTable[0, 0] = "节点";
                        this.myTable[1, 0] = "编码";
                        this.myTable[1, 1] = "X　";
                        this.myTable[1, 2] = "Y　";
                        break;

                    default:
                        this.title = "格点データ";
                        this.myTable[0, 0] = "格点";
                        this.myTable[1, 0] = "No";
                        this.myTable[1, 1] = "X　";
                        this.myTable[1, 2] = "Y　";
                        break;
                }
                this.myTable[0, 3] = this.myTable[0, 0];
                this.myTable[1, 3] = this.myTable[1, 0];
                this.myTable[1, 4] = this.myTable[1, 1];
                this.myTable[1, 5] = this.myTable[1, 2];
                this.myTable[0, 6] = this.myTable[0, 0];
                this.myTable[1, 6] = this.myTable[1, 0];
                this.myTable[1, 7] = this.myTable[1, 1];
                this.myTable[1, 8] = this.myTable[1, 2];

                // 表題の文字位置
                this.myTable.AlignX[0, 0] = "R";    // 右寄せ
                this.myTable.AlignX[1, 0] = "R";    // 右寄せ
                this.myTable.AlignX[1, 1] = "R";    // 右寄せ
                this.myTable.AlignX[1, 2] = "R";    // 右寄せ
                this.myTable.AlignX[0, 3] = "R";    // 右寄せ
                this.myTable.AlignX[1, 3] = "R";    // 右寄せ
                this.myTable.AlignX[1, 4] = "R";    // 右寄せ
                this.myTable.AlignX[1, 5] = "R";    // 右寄せ
                this.myTable.AlignX[0, 6] = "R";    // 右寄せ
                this.myTable.AlignX[1, 6] = "R";    // 右寄せ
                this.myTable.AlignX[1, 7] = "R";    // 右寄せ
                this.myTable.AlignX[1, 8] = "R";    // 右寄せ

            }

            return columns;
        }


        /// <summary>
        /// 1ページに入れるコンテンツを集計する
        /// </summary>
        /// <param name="target">印刷対象の配列</param>
        /// <param name="rows">行数</param>
        /// <returns>印刷する用の配列</returns>
        private Table getPageContents(Dictionary<string, Vector3> target, int rows, int columns)
        {
            int r = this.myTable.Rows;

            int count = this.myTable.Columns;
            int c = count / columns;

            // 行コンテンツを生成
            var table = this.myTable.Clone();
            table.ReDim(row: r + rows);

            table.RowHeight[r] = printManager.LineSpacing2; // 表題と body の間

            if (dimension == 3)
            {
                for (var i = 0; i < rows; i++)
                {
                    var lines = new string[count];

                    for (var j = 0; j < columns; j++)
                    {
                        int index = i + (rows * j);

                        if (target.Count <= index)
                            continue;

                        string No = target.ElementAt(index).Key;
                        Vector3 item = target.ElementAt(index).Value;

                        table[r + i, 0 + c * j + j] = No;
                        table.AlignX[r + i, 0 + c * j + j] = "R";
                        table[r + i, 1 + c * j + j] = printManager.toString(item.x, 3);
                        table.AlignX[r + i, 1 + c * j + j] = "R";
                        table[r + i, 2 + c * j + j] = printManager.toString(item.y, 3);
                        table.AlignX[r + i, 2 + c * j + j] = "R";

                        if (this.dimension == 3)
                        {
                            table[r + i, 3 + c * j + j] = printManager.toString(item.z, 3);
                            table.AlignX[r + i, 3 + c * j + j] = "R";
                        }
                    }
                }

            }
            else
            {
                for (var i = 0; i < rows; i++)
                {
                    var lines = new string[count];

                    for (var j = 0; j < columns; j++)
                    {
                        int index = i + (rows * j);

                        if (target.Count <= index)
                            continue;

                        string No = target.ElementAt(index).Key;
                        Vector3 item = target.ElementAt(index).Value;

                        table[r + i, 0 + c * j] = No;
                        table.AlignX[r + i, 0 + c * j] = "R";
                        table[r + i, 1 + c * j] = printManager.toString(item.x, 3);
                        table.AlignX[r + i, 1 + c * j] = "R";
                        table[r + i, 2 + c * j] = printManager.toString(item.y, 3);
                        table.AlignX[r + i, 2 + c * j] = "R";

                        if (this.dimension == 3)
                        {
                            table[r + i, 3 + c * j] = printManager.toString(item.z, 3);
                            table.AlignX[r + i, 3 + c * j] = "R";
                        }
                    }
                }
            }

            return table;
        }

        #region PrintableBaseA.printPDF()用メソッド定義
        protected override bool HasAnyData() => nodes.Count > 0;
        protected override void PrintInit(PdfDocument mc, PrintData data, out string[] titles, out int headerRows, out Table.NupInfo[] nupInfo)
        {
            printInit(mc, data);

            titles = new[] { title, };
            headerRows = myTable.Rows;
            nupInfo = dimension == 3 ? new[] { new Table.NupInfo(0, 3), new Table.NupInfo(5, 8), } : new[] { new Table.NupInfo(0, 2), new Table.NupInfo(3, 5), new Table.NupInfo(6, 8), };
        }
        protected override Table GetTable() => getPageContents(nodes, nodes.Count, 1);
        #endregion

        #endregion


        #region 他のモジュールのヘルパー関数

        // 格点データの取得
        public Dictionary<string, Vector3> Nodes
        {
            get
            {
                return this.nodes;
            }
        }

        /// <summary>
        /// 節点座標を返す
        /// </summary>
        /// <param name="nodeNo"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// 
        public Vector3 GetNodePos(string nodeNo)
        {
            if (this.nodes.Count <= 0)
                return null;

            if (!this.nodes.ContainsKey(nodeNo))
                return null;

            var target = this.nodes[nodeNo];

            var result = new Vector3();

            result.x = double.IsNaN(target.x) ? 0 : target.x;
            result.y = double.IsNaN(target.y) ? 0 : target.y;
            result.z = double.IsNaN(target.z) ? 0 : target.z;

            return result;
        }

        #endregion

    }

}

