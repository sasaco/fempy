using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FrameWebforCS.components.result
{
    public partial class ResultReacComponent : UserControl
    {
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;

        public ResultReacComponent()
        {
            InitializeComponent();
            fpSpread1.EditModeOn += DataHelperModule.faSpread_EditModeOn;

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();
            fpSpread1_Sheet1.SheetName = "基本ケース";
            SetSheet1(fpSpread1_Sheet1);

            float w = 0;
            var col = fpSpread1_Sheet1.Columns;
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
                _Sheet.ColumnCount = 7;

                header.Cells[0, 0].Text = "節点";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "支点反力(KN)";
                header.Cells[1, 1].Text = "X方向";
                header.Cells[0, 2].Text = "";
                header.Cells[1, 2].Text = "Y方向";
                header.Cells[0, 3].Text = "";
                header.Cells[1, 3].Text = "Z方向";
                header.Cells[0, 4].Text = "回転反力(kN・m)";
                header.Cells[1, 4].Text = "X軸回り";
                header.Cells[0, 5].Text = "";
                header.Cells[1, 5].Text = "Y軸回り";
                header.Cells[0, 6].Text = "";
                header.Cells[1, 6].Text = "Z軸回り";

                header.Cells[0, 1].ColumnSpan = 3;
                header.Cells[0, 4].ColumnSpan = 3;

                var column = _Sheet.Columns;
                column[0].Width = 50;
            }
            else
            {
                _Sheet.ColumnCount = 4;

                header.Cells[0, 0].Text = "節点";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "支点反力(KN)";
                header.Cells[1, 1].Text = "X方向";
                header.Cells[0, 2].Text = "";
                header.Cells[1, 2].Text = "Y方向";
                header.Cells[0, 3].Text = "回転反力";
                header.Cells[1, 3].Text = "(kN・m)";

                header.Cells[0, 1].ColumnSpan = 2;

                var column = _Sheet.Columns;
                column[0].Width = 50;
                for (var i = 1; i < column.Count; i++)
                {
                    column[i].Width = 80;
                }
            }
        }
    }
}
