using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using GrapeCity.Win.Spread.InputMan.CellType;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Reflection.Metadata;
using System.Text;
using System.Windows.Forms;

namespace FrameWebforCS.components.input
{
    public partial class InputMembersComponent : UserControl
    {
        private InputDataService _input = InputDataService.Instance;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet2;

        public InputMembersComponent()
        {
            InitializeComponent();

            fpSpread1.EditModeOn += fpSpread1.faSpread_EditModeOn;

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

            ConfigureSheet(fpSpread1_Sheet1);

            SetSheet1();

            float w = 0;
            var col = fpSpread1_Sheet1.Columns;
            for (int i = 0; i < col.Count; i++)
            {
                w += col[i].Width;
            }
            w += 100;

            this.Width = (int)w;


            fpSpread1_Sheet2 = fpSpread1.AddNewSheetView();

            ConfigureSheet(fpSpread1_Sheet2);

            SetSheet2();

        }

        private static void ConfigureSheet(SheetView sheet)
        {
            sheet.AutoGenerateColumns = false;
            sheet.DataAutoCellTypes = false;
            sheet.DataAutoHeadings = false;
            sheet.RowHeaderAutoText = HeaderAutoText.Numbers;
            sheet.StartingRowNumber = 1;
            sheet.DataSource = InputMembersService.Instance.Members;
        }

        private void SetSheet1()
        {
            fpSpread1_Sheet1.SheetName = "部材";

            var column = fpSpread1_Sheet1.Columns;
            foreach (Column col in column)
            {
                col.Locked = false;
            }

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
                header.Cells[0, 3].Text = "材料";
                header.Cells[1, 3].Text = "No";
                header.Cells[0, 4].Text = "コードアングル";
                header.Cells[1, 4].Text = "(°)";
                header.Cells[0, 5].Text = "材料名称";
                header.Cells[1, 5].Text = " ";

                header.Cells[0, 0].ColumnSpan = 2;

                column[0].Width = 50;
                column[1].Width = 50;
                column[2].Width = 80;
                column[3].Width = 50;
                column[4].Width = 150;
                column[5].Width = 150;

                column[2].Locked = true;
                column[5].Locked = true;
            }
            else
            {
                fpSpread1_Sheet1.ColumnCount = 5;

                header.Cells[0, 0].Text = "節点";
                header.Cells[1, 0].Text = "i端";
                header.Cells[0, 1].Text = "";
                header.Cells[1, 1].Text = "j端";
                header.Cells[0, 2].Text = "部材長";
                header.Cells[1, 2].Text = "(m)";
                header.Cells[0, 3].Text = "材料";
                header.Cells[1, 3].Text = "No";
                header.Cells[0, 4].Text = "材料名称";
                header.Cells[1, 4].Text = " ";

                header.Cells[0, 0].ColumnSpan = 2;

                column[0].Width = 50;
                column[1].Width = 50;
                column[2].Width = 80;
                column[3].Width = 50;
                column[4].Width = 150;

                column[2].Locked = true;
                column[4].Locked = true;
            }


            foreach(Column col in column)
            {
                if (col.Locked)
                {
                    col.BackColor = SystemColors.Control;
                }
            }

            column[0].DataField = nameof(clsMember.Ni);
            column[1].DataField = nameof(clsMember.Nj);
            column[3].DataField = nameof(clsMember.E);
            if (_input.dimension == 3)
                column[4].DataField = nameof(clsMember.Cg);

        }

        private void SetSheet2()
        {
            fpSpread1_Sheet2.SheetName = "剛域";
            var column = fpSpread1_Sheet2.Columns;
            foreach (Column col in column)
            {
                col.Locked = false;
            }

            var header = fpSpread1_Sheet2.ColumnHeader;
            header.RowCount = 2;

            fpSpread1_Sheet2.ColumnCount = 7;

            header.Cells[0, 0].Text = "部材長";
            header.Cells[1, 0].Text = "(m)";
            header.Cells[0, 1].Text = "材料";
            header.Cells[1, 1].Text = "No";
            header.Cells[0, 2].Text = "材料名称";
            header.Cells[1, 2].Text = " ";
            header.Cells[0, 3].Text = "剛域";
            header.Cells[1, 3].Text = "i端の距離";
            header.Cells[0, 4].Text = "";
            header.Cells[1, 4].Text = "j端の距離";
            header.Cells[0, 5].Text = "材料";
            header.Cells[1, 5].Text = "No";
            header.Cells[0, 6].Text = "材料名称";
            header.Cells[1, 6].Text = " ";

            header.Cells[0, 3].ColumnSpan = 2;

            column[0].Width = 80;
            column[1].Width = 50;
            column[2].Width = 150;
            column[3].Width = 80;
            column[4].Width = 80;
            column[5].Width = 50;
            column[6].Width = 150;

            for (int i=0; i<3; i++)
            {
                column[i].Locked = true;
                column[i].BackColor = SystemColors.Control;
            }
            column[6].Locked = true;
            column[6].BackColor = SystemColors.Control;

            column[1].DataField = nameof(clsMember.E);

        }

        public void setActiveSheet(int index)
        {
            this.fpSpread1.ActiveSheetIndex = index;　
        }

    }
}
