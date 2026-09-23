using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FrameWebforCS.components.input
{
    public partial class InputLoadComponent : UserControl
    {
        private InputDataService _input = InputDataService.Instance;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet2;

        public InputLoadComponent()
        {
            InitializeComponent();

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

            SetSheet1();

            fpSpread1_Sheet2 = fpSpread1.AddNewSheetView();

            SetSheet2();

            float w = 0;
            var col = fpSpread1_Sheet2.Columns;
            for (int i = 0; i < col.Count; i++)
            {
                w += col[i].Width;
            }
            w += 100;

            this.Width = (int)w;
        }

        private void SetSheet1()
        {
            fpSpread1_Sheet1.SheetName = "荷重名称";

            var column = fpSpread1_Sheet1.Columns;

            var header = fpSpread1_Sheet1.ColumnHeader;

            fpSpread1_Sheet1.ColumnCount = 7;

            header.Cells[0, 0].Text = "割増係数";
            header.Cells[0, 1].Text = "記号";
            header.Cells[0, 2].Text = "名称";
            header.Cells[0, 3].Text = "支点";
            header.Cells[0, 4].Text = "断面";
            header.Cells[0, 5].Text = "バネ";
            header.Cells[0, 6].Text = "結合";

            column[2].Width = 200;
            column[3].Width = 50;
            column[4].Width = 50;
            column[5].Width = 50;
            column[6].Width = 50;
        }

        private void SetSheet2()
        {
            fpSpread1_Sheet2.SheetName = "荷重強度";
            var column = fpSpread1_Sheet2.Columns;

            var header = fpSpread1_Sheet2.ColumnHeader;
            header.RowCount = 3;

            if (_input.dimension == 3)
            {
                fpSpread1_Sheet2.ColumnCount = 15;

                header.Cells[0, 0].Text = "要素荷重";
                header.Cells[1, 0].Text = "部材No";
                header.Cells[2, 0].Text = "1";
                header.Cells[1, 1].Text = "";
                header.Cells[2, 1].Text = "2";
                header.Cells[1, 2].Text = "方向";
                header.Cells[2, 2].Text = "(x,y,z)";
                header.Cells[1, 3].Text = "マーク";
                header.Cells[2, 3].Text = "(1,2,9,11)";
                header.Cells[1, 4].Text = "L1";
                header.Cells[2, 4].Text = "(m)";
                header.Cells[1, 5].Text = "L2";
                header.Cells[2, 5].Text = "(m)";
                header.Cells[1, 6].Text = "P1";
                header.Cells[2, 6].Text = "(kN/m)";
                header.Cells[1, 7].Text = "P2";
                header.Cells[2, 7].Text = "(kN/m)";

                header.Cells[0, 0].ColumnSpan = 8;
                header.Cells[1, 0].ColumnSpan = 2;

                column[0].Width = 50;
                column[1].Width = 50;

                //
                header.Cells[0, 8].Text = "節点荷重";
                header.Cells[1, 8].Text = "節点";
                header.Cells[2, 8].Text = "No";
                header.Cells[1, 9].Text = "X";
                header.Cells[2, 9].Text = "(kN)";
                header.Cells[1, 10].Text = "Y";
                header.Cells[2, 10].Text = "(kN)";
                header.Cells[1, 11].Text = "Z";
                header.Cells[2, 11].Text = "(kN)";
                header.Cells[1, 12].Text = "RX";
                header.Cells[2, 12].Text = "(kN・m)";
                header.Cells[1, 13].Text = "RY";
                header.Cells[2, 13].Text = "(kN・m)";
                header.Cells[1, 14].Text = "RZ";
                header.Cells[2, 14].Text = "(kN・m)";

                header.Cells[0, 8].ColumnSpan = 6;

                column[8].Width = 50;
            }
            else
            {

            }
        }


    }
}
