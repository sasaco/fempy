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
    public partial class InputCombineComponent : UserControl
    {
        private InputDataService _input = InputDataService.Instance;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet2;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet3;

        public InputCombineComponent()
        {
            InitializeComponent();

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();
            SetSheet1();

            fpSpread1_Sheet2 = fpSpread1.AddNewSheetView();
            SetSheet2();

            fpSpread1_Sheet3 = fpSpread1.AddNewSheetView();
            SetSheet3();
        }

        private void SetSheet1()
        {
            fpSpread1_Sheet1.SheetName = "DEFINE";
            ConfigureRows(fpSpread1_Sheet1);

            var column = fpSpread1_Sheet1.Columns;
            var header = fpSpread1_Sheet1.ColumnHeader;

            fpSpread1_Sheet1.ColumnCount = 50;
            for(int i =0; i< fpSpread1_Sheet1.ColumnCount; i++)
            {
                header.Cells[0, i].Text = "C" + (i + 1).ToString();
                column[i].DataField = "C" + (i + 1).ToString();
                column[i].Width = 50;
            }
            fpSpread1_Sheet1.DataSource = InputCombineService.Instance.DefineRows;
        }

        private void SetSheet2()
        {
            fpSpread1_Sheet2.SheetName = "COMBINE";
            ConfigureRows(fpSpread1_Sheet2);
            var column = fpSpread1_Sheet2.Columns;
            var header = fpSpread1_Sheet2.ColumnHeader;

            List<string> difine = _input.GetDifineCase();

            fpSpread1_Sheet2.ColumnCount = difine.Count + 1;

            for (int i = 0; i < fpSpread1_Sheet2.ColumnCount - 1; i++)
            {
                header.Cells[0, i].Text = difine[i];
                column[i].DataField = "C" + (i + 1).ToString();
                column[i].Width = 50;
            }

            int j = fpSpread1_Sheet2.ColumnCount - 1;
            header.Cells[0, j].Text = "名称";
            column[j].DataField = "name";
            column[j].Width = 200;
            fpSpread1_Sheet2.FrozenTrailingColumnCount = 1;
            fpSpread1_Sheet2.DataSource = InputCombineService.Instance.CombineRows;
        }

        private void SetSheet3()
        {
            fpSpread1_Sheet3.SheetName = "PICKUP";
            ConfigureRows(fpSpread1_Sheet3);

            var column = fpSpread1_Sheet3.Columns;
            var header = fpSpread1_Sheet3.ColumnHeader;

            fpSpread1_Sheet3.ColumnCount = 50 + 1;
            for (int i = 0; i < fpSpread1_Sheet3.ColumnCount - 1; i++)
            {
                header.Cells[0, i].Text = "C" + (i + 1).ToString();
                column[i].DataField = "C" + (i + 1).ToString();
                column[i].Width = 50;
            }

            int j = fpSpread1_Sheet3.ColumnCount - 1;
            header.Cells[0, j].Text = "名称";
            column[j].DataField = "name";
            column[j].Width = 200;
            fpSpread1_Sheet3.FrozenTrailingColumnCount = 1;
            fpSpread1_Sheet3.DataSource = InputCombineService.Instance.PickupRows;
        }

        private static void ConfigureRows(SheetView sheet)
        {
            sheet.AutoGenerateColumns = false;
            sheet.DataAutoCellTypes = false;
            sheet.DataAutoHeadings = false;
            sheet.RowHeaderAutoText = HeaderAutoText.Numbers;
            sheet.StartingRowNumber = 1;
        }
    }
}
