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
    public partial class InputNodesComponent : UserControl
    {
        private InputDataService _input = InputDataService.Instance;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;


        public InputNodesComponent()
        {
            InitializeComponent();

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

            fpSpread1_Sheet1.SheetName = "Node";

            var header = fpSpread1_Sheet1.ColumnHeader;
            var column = fpSpread1_Sheet1.Columns;

            if (_input.dimension == 3)
            {
                fpSpread1_Sheet1.ColumnCount = 3;
                header.Cells[0, 0].Text = "X";
                header.Cells[0, 1].Text = "Y";
                header.Cells[0, 2].Text = "Z";
                column[0].Width = 80;
                column[1].Width = 80;
                column[2].Width = 80;
            }
            else
            {
                fpSpread1_Sheet1.ColumnCount = 2;
                header.Cells[0, 0].Text = "X";
                header.Cells[0, 1].Text = "Y";
                column[0].Width = 80;
                column[1].Width = 80;
            }

            float w = 0;
            var col = fpSpread1_Sheet1.Columns;
            for (int i = 0; i < col.Count; i++)
            {
                w += col[i].Width;
            }
            w += 100;

            this.Width = (int)w;

        }


    }
}
