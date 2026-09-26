using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using static THREE.ArcballControls;

namespace FrameWebforCS.components.result
{
    public partial class ResultFsecComponent : UserControl
    {
        private ResultFsecService _input = ResultFsecService.Instance;

        public ResultFsecComponent()
        {
            InitializeComponent();
            fpSpread1.EditModeOn += fpSpread1.faSpread_EditModeOn;

            var result = _input.getFsec();

            foreach (var item in result)
            {
                var fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();
                fpSpread1_Sheet1.SheetName = item.Key;

                SetSheet1(fpSpread1_Sheet1);
            }

            float w = 0;
            FarPoint.Win.Spread.SheetView fs = (SheetView)fpSpread1.Sheets.First();
            var col = fs.Columns;
            for (int i = 0; i < col.Count; i++)
            {
                w += col[i].Width;
            }
            w += 100;

            this.Width = (int)w;

        }

        public void setActiveSheet(int index)
        {
            this.fpSpread1.ActiveSheetIndex = index;
        }

        internal static void SetSheet1(SheetView _Sheet)
        {
            var header = _Sheet.ColumnHeader;
            header.RowCount = 2;

            if (InputDataService.Instance.dimension == 3)
            {
                _Sheet.ColumnCount = 9;

                header.Cells[0, 0].Text = "部材";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "節点";
                header.Cells[1, 1].Text = "No";
                header.Cells[0, 2].Text = "着目位置";
                header.Cells[1, 2].Text = "(m)";
                header.Cells[0, 3].Text = "軸方向力";
                header.Cells[1, 3].Text = "(kN)";
                header.Cells[0, 4].Text = "せん断力(kN)";
                header.Cells[1, 4].Text = "Y軸方向";
                header.Cells[0, 5].Text = "";
                header.Cells[1, 5].Text = "Z軸方向";
                header.Cells[0, 6].Text = "ねじりモーメント";
                header.Cells[1, 6].Text = "(kN・m)";
                header.Cells[0, 7].Text = "曲げモーメント(kN・m)";
                header.Cells[1, 7].Text = "Y軸回り";
                header.Cells[0, 8].Text = "";
                header.Cells[1, 8].Text = "Z軸回り";

                header.Cells[0, 4].ColumnSpan = 2;
                header.Cells[0, 7].ColumnSpan = 2;

                var column = _Sheet.Columns;
                column[0].Width = 50;
                column[1].Width = 50;
            }
            else
            {
                _Sheet.ColumnCount = 9;

                header.Cells[0, 0].Text = "部材";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "節点";
                header.Cells[1, 1].Text = "No";
                header.Cells[0, 2].Text = "着目位置";
                header.Cells[1, 2].Text = "(m)";
                header.Cells[0, 3].Text = "軸方向力";
                header.Cells[1, 3].Text = "(kN)";
                header.Cells[0, 4].Text = "せん断力";
                header.Cells[1, 4].Text = "(kN)";
                header.Cells[0, 5].Text = "曲げモーメント";
                header.Cells[1, 5].Text = "(kN・m)";

                var column = _Sheet.Columns;
                column[0].Width = 50;
                column[1].Width = 50;
            }
        }

    }
}
