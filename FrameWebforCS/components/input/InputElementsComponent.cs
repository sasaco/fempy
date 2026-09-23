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
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;

        public InputElementsComponent(int target_height)
        {
            InitializeComponent();

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();
            fpSpread1_Sheet1.ColumnCount = 8;
            var header = fpSpread1_Sheet1.ColumnHeader;
            header.RowCount = 2;
            var c1 = header.Cells[0, 0];
            header.Cells[0, 0].Text = "弾性係数";
            header.Cells[1, 0].Text = "E(kN/m2)";
            header.Cells[0, 1].Text = "せん断弾性係数";
            header.Cells[1, 1].Text = "G(kN/m2)";
            header.Cells[0, 2].Text = "膨張係数";
            header.Cells[1, 2].Text = "";
            header.Cells[0, 3].Text = "断面積";
            header.Cells[1, 3].Text = "A(m2)";
            header.Cells[0, 4].Text = "ねじり定数";
            header.Cells[1, 4].Text = "J(m4)";
            header.Cells[0, 5].Text = "断面二次モーメント";
            header.Cells[1, 5].Text = "Iy(m4)";
            header.Cells[0, 6].Text = "";
            header.Cells[1, 6].Text = "Iz(m4)";
            header.Cells[0, 7].Text = "名前";
            header.Cells[1, 7].Text = "";


            panel1.Height = target_height;
            panel1.Width = fpSpread1.Width;
            //this.Height = panel1.Height+100;
            //this.Width = panel1.Width+100;

        }

        public void setElementJson(string json)
        {
            // Implementation for setting element JSON
        }
    }
}
