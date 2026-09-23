using FarPoint.Win.Spread;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FrameWebforCS.components.input
{
    public partial class InputElementsComponent : UserControl
    {
        private const int type_count = 6;
        private List<FarPoint.Win.Spread.SheetView> fpSpread1_Sheets;

        public InputElementsComponent()
        {
            InitializeComponent();

            fpSpread1_Sheets = new List<SheetView>();

            for (int i = 0; i < type_count; i++) { 

                var fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

                fpSpread1_Sheet1.SheetName = "TYPE-" + i.ToString();

                var header = fpSpread1_Sheet1.ColumnHeader;
                header.RowCount = 2;

                fpSpread1_Sheet1.ColumnCount = 8;

                header.Cells[0, 0].Text = "弾性係数";
                header.Cells[1, 0].Text = "E(kN/m2)";
                header.Cells[0, 1].Text = "せん断弾性係数";
                header.Cells[1, 1].Text = "G(kN/m2)";
                header.Cells[0, 2].Text = "膨張係数";
                header.Cells[1, 2].Text = " ";
                header.Cells[0, 3].Text = "断面積";
                header.Cells[1, 3].Text = "A(m2)";
                header.Cells[0, 4].Text = "ねじり定数";
                header.Cells[1, 4].Text = "J(m4)";
                header.Cells[0, 5].Text = "断面二次モーメント";
                header.Cells[1, 5].Text = "Iy(m4)";
                header.Cells[0, 6].Text = header.Cells[0, 5].Text;
                header.Cells[1, 6].Text = "Iz(m4)";
                header.Cells[0, 7].Text = "名前";
                header.Cells[1, 7].Text = " ";

                header.Cells[0, 5].ColumnSpan = 2;

                var column = fpSpread1_Sheet1.Columns;
                column[0].Width = 80;
                column[1].Width = 150;
                column[2].Width = 80;
                column[3].Width = 80;
                column[4].Width = 100;
                column[5].Width = 80;
                column[6].Width = 80;
                column[7].Width = 150;

                fpSpread1_Sheets.Add(fpSpread1_Sheet1);
            }

            this.Width = 900;

        }



        public void setElementJson(string json)
        {
            // Implementation for setting element JSON
        }
    }
}
