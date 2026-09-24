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
    public partial class InputJointComponent : UserControl
    {
        private InputDataService _input = InputDataService.Instance;
        private const int type_count = 6;
        private List<FarPoint.Win.Spread.SheetView> fpSpread1_Sheets;


        public InputJointComponent()
        {
            InitializeComponent();

            fpSpread1_Sheets = new List<SheetView>();

            for (int i = 0; i < type_count; i++)
            {

                var fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

                fpSpread1_Sheet1.SheetName = "TYPE-" + i.ToString();

                setColumn(fpSpread1_Sheet1);

                fpSpread1_Sheets.Add(fpSpread1_Sheet1);
            }

            float w = 0;
            var col = fpSpread1_Sheets.First().Columns;
            for (int i = 0; i < col.Count; i++)
            {
                w += col[i].Width;
            }
            w += 100;

            this.Width = (int)w;

        }

        private void setColumn(FarPoint.Win.Spread.SheetView fpSpread1_Sheet1)
        {
            var header = fpSpread1_Sheet1.ColumnHeader;

            if (_input.dimension == 3)
            {
                header.RowCount = 2;
                fpSpread1_Sheet1.ColumnCount = 7;

                header.Cells[0, 0].Text = "部材";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "i端";
                header.Cells[1, 1].Text = "X";
                header.Cells[0, 2].Text = "";
                header.Cells[1, 2].Text = "Y";
                header.Cells[0, 3].Text = "";
                header.Cells[1, 3].Text = "Z";
                header.Cells[0, 4].Text = "j端";
                header.Cells[1, 4].Text = "X";
                header.Cells[0, 5].Text = "";
                header.Cells[1, 5].Text = "Y";
                header.Cells[0, 6].Text = "";
                header.Cells[1, 6].Text = "Z";

                header.Cells[0, 1].ColumnSpan = 3;
                header.Cells[0, 4].ColumnSpan = 3;

                foreach (Column column in fpSpread1_Sheet1.Columns)
                {
                    column.Width = 50;
                }
            }
            else
            {
                header.RowCount = 1;
                fpSpread1_Sheet1.ColumnCount = 3;

                header.Cells[0, 0].Text = "部材No";
                header.Cells[0, 1].Text = "i端";
                header.Cells[0, 2].Text = "j端";

                var column = fpSpread1_Sheet1.Columns;
                column[0].Width = 80;
                column[1].Width = 50;
                column[2].Width = 50;

            }


        }

    }
}

