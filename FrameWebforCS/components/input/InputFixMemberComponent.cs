using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using static THREE.ArcballControls;

namespace FrameWebforCS.components.input
{
    public partial class InputFixMemberComponent : UserControl
    {
        private InputDataService _input = InputDataService.Instance;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;

        public InputFixMemberComponent()
        {
            InitializeComponent();

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

            fpSpread1_Sheet1.SheetName = "Mamber";
            var header = fpSpread1_Sheet1.ColumnHeader;
            header.RowCount = 2;

            if (_input.dimension == 3)
            {
                fpSpread1_Sheet1.ColumnCount = 6;

                header.Cells[0, 0].Text = "節点";
                header.Cells[1, 0].Text = "i端";
                header.Cells[0, 1].Text = "";
                header.Cells[1, 1].Text = "j端";
                header.Cells[0, 2].Text = "部材長";
                header.Cells[1, 2].Text = "(m)";
                header.Cells[0, 3].Text = "材料No";
                header.Cells[1, 3].Text = " ";
                header.Cells[0, 4].Text = "コードアングル";
                header.Cells[1, 4].Text = "(°)";
                header.Cells[0, 5].Text = "材料名称";
                header.Cells[1, 5].Text = " ";

                header.Cells[0, 0].ColumnSpan = 2;

                var column = fpSpread1_Sheet1.Columns;
                column[0].Width = 50;
                column[1].Width = 50;
                column[2].Width = 80;
                column[3].Width = 50;
                column[4].Width = 150;
                column[5].Width = 150;

                column[2].Locked = true;
            }
            else
            {
                fpSpread1_Sheet1.ColumnCount = 6;

                header.Cells[0, 0].Text = "節点";
                header.Cells[1, 0].Text = "i端";
                header.Cells[0, 1].Text = "";
                header.Cells[1, 1].Text = "j端";
                header.Cells[0, 2].Text = "部材長";
                header.Cells[1, 2].Text = "(m)";
                header.Cells[0, 3].Text = "材料No";
                header.Cells[1, 3].Text = " ";
                header.Cells[0, 4].Text = "コードアングル";
                header.Cells[1, 4].Text = "(°)";
                header.Cells[0, 5].Text = "材料名称";
                header.Cells[1, 5].Text = " ";

                header.Cells[0, 0].ColumnSpan = 2;

                var column = fpSpread1_Sheet1.Columns;
                column[0].Width = 50;
                column[1].Width = 50;
                column[2].Width = 80;
                column[3].Width = 50;
                column[4].Width = 150;
                column[5].Width = 150;

                column[2].Locked = true;
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
