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
    public partial class InputNoticePointsComponent : UserControl
    {
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;

        public InputNoticePointsComponent()
        {
            InitializeComponent();

            fpSpread1.EditModeOn += fpSpread1.faSpread_EditModeOn;

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

            fpSpread1_Sheet1.SheetName = "着目点";
            fpSpread1_Sheet1.AutoGenerateColumns = false;
            fpSpread1_Sheet1.DataAutoCellTypes = false;
            fpSpread1_Sheet1.DataAutoHeadings = false;
            fpSpread1_Sheet1.RowHeaderAutoText = HeaderAutoText.Numbers;
            fpSpread1_Sheet1.StartingRowNumber = 1;

            var column = fpSpread1_Sheet1.Columns;
            foreach (Column col in column)
            {
                col.Locked = false;
            }

            var header = fpSpread1_Sheet1.ColumnHeader;
            header.RowCount = 2;

            fpSpread1_Sheet1.ColumnCount = 22;

            header.Cells[0, 0].Text = "部材";
            header.Cells[1, 0].Text = "No";
            header.Cells[0, 1].Text = "部材長";
            header.Cells[1, 1].Text = "(m)";
            header.Cells[0, 2].Text = "i端からの距離(m)";
            header.Cells[0, 2].HorizontalAlignment = CellHorizontalAlignment.Left;

            for (int i = 2; i < column.Count; i++)
            {
                header.Cells[1, i].Text = "L" + (i - 1);
                column[i].Width = 80;
            }

            header.Cells[0, 2].ColumnSpan = column.Count - 2;

            column[0].Width = 50;
            column[1].Width = 80;

            column[1].Locked = true;
            column[1].BackColor = SystemColors.Control;
            column[0].DataField = "M";
            for (int i = 2; i < column.Count; i++)
                column[i].DataField = "P" + (i - 1).ToString();
            fpSpread1_Sheet1.DataSource = InputNoticePointsService.Instance.NoticePoints;

        }




    }
}
