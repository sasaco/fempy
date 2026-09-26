using FarPoint.Win.Spread;
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
        private const int type_count = 6;

        public InputFixMemberComponent()
        {
            InitializeComponent();

            for (int i = 0; i < type_count; i++)
            {
                var fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

                fpSpread1_Sheet1.SheetName = (i + 1).ToString();
                fpSpread1_Sheet1.AutoGenerateColumns = false;
                fpSpread1_Sheet1.DataAutoCellTypes = false;
                fpSpread1_Sheet1.DataAutoHeadings = false;
                fpSpread1_Sheet1.RowHeaderAutoText = HeaderAutoText.Numbers;
                fpSpread1_Sheet1.StartingRowNumber = 1;

                setColumn(fpSpread1_Sheet1);
                fpSpread1_Sheet1.DataSource = InputFixMemberService.Instance.GetRows(fpSpread1_Sheet1.SheetName);
            }

            float w = 0;
            FarPoint.Win.Spread.SheetView fs = (SheetView)fpSpread1.Sheets.First();
            var col = fs.Columns;
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
                fpSpread1_Sheet1.ColumnCount = 5;

                header.Cells[0, 0].Text = "部材";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "変位拘束";
                header.Cells[1, 1].Text = "部材軸方向";
                header.Cells[0, 2].Text = "";
                header.Cells[1, 2].Text = "部材Y軸";
                header.Cells[0, 3].Text = "";
                header.Cells[1, 3].Text = "部材Z軸";
                header.Cells[0, 4].Text = "回転拘束";
                header.Cells[1, 4].Text = "(kNm/rad/m)";

                header.Cells[0, 1].ColumnSpan = 3;

                var column = fpSpread1_Sheet1.Columns;
                string[] fields = ["M", "Tx", "Ty", "Tz", "Tr"];
                for (int i = 0; i < fields.Length; i++)
                    column[i].DataField = fields[i];

                column[0].Width = 50;
                for (var i = 1; i < column.Count; i++)
                {
                    column[i].Width = 100;
                }
            }
            else
            {
                fpSpread1_Sheet1.ColumnCount = 3;

                header.Cells[0, 0].Text = "部材";
                header.Cells[1, 0].Text = "No";
                header.Cells[0, 1].Text = "部材軸方向";
                header.Cells[1, 1].Text = "(kN/m/m)";
                header.Cells[0, 2].Text = "部材直角方向";
                header.Cells[1, 2].Text = "(kN/m/m)";

                var column = fpSpread1_Sheet1.Columns;
                string[] fields = ["M", "Tx", "Ty"];
                for (int i = 0; i < fields.Length; i++)
                    column[i].DataField = fields[i];

                column[0].Width = 50;
                for (var i = 1; i < column.Count; i++)
                {
                    column[i].Width = 100;
                }
            }
        }
    }
}
