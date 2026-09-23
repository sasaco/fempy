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
    public partial class InputFixNodeComponent : UserControl
    {
        private InputDataService _input = InputDataService.Instance;
        private const int type_count = 6;
        private List<FarPoint.Win.Spread.SheetView> fpSpread1_Sheets;


        public InputFixNodeComponent()
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
            header.RowCount = 2;

            if (_input.dimension == 3)
            {
                fpSpread1_Sheet1.ColumnCount = 7;

                header.Cells[0, 0].Text = "節点";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "変位拘束";
                header.Cells[1, 1].Text = "X方向";
                header.Cells[0, 2].Text = "";
                header.Cells[1, 2].Text = "Y方向";
                header.Cells[0, 3].Text = "";
                header.Cells[1, 3].Text = "Z方向";
                header.Cells[0, 4].Text = "回転拘束";
                header.Cells[1, 4].Text = "X軸回り";
                header.Cells[0, 5].Text = "";
                header.Cells[1, 5].Text = "Y軸回り";
                header.Cells[0, 6].Text = "";
                header.Cells[1, 6].Text = "Z軸回り";

                header.Cells[0, 1].ColumnSpan = 3;
                header.Cells[0, 4].ColumnSpan = 3;

                var column = fpSpread1_Sheet1.Columns;
                column[0].Width = 50;
            }
            else
            {
                fpSpread1_Sheet1.ColumnCount = 4;

                header.Cells[0, 0].Text = "節点";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "変位拘束";
                header.Cells[1, 1].Text = "X方向";
                header.Cells[0, 2].Text = "";
                header.Cells[1, 2].Text = "Y方向";
                header.Cells[0, 3].Text = "回転拘束";
                header.Cells[1, 3].Text = "(kN・m/rad)";

                header.Cells[0, 1].ColumnSpan = 2;

                var column = fpSpread1_Sheet1.Columns;
                column[0].Width = 50;
                for (var i = 1; i < column.Count; i++)
                {
                    column[i].Width = 80;
                }
            }
        }
    }
}
