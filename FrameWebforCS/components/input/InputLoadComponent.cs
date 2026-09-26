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
    public partial class InputLoadComponent : UserControl
    {
        private readonly InputLoadService _service = InputLoadService.Instance;
        private readonly ComboBox _caseSelector = new();
        private InputDataService _input = InputDataService.Instance;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet2;

        public InputLoadComponent()
        {
            InitializeComponent();

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

            SetSheet1();

            fpSpread1_Sheet2 = fpSpread1.AddNewSheetView();

            SetSheet2();

            _caseSelector.Dock = DockStyle.Top;
            _caseSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _caseSelector.AccessibleName = "荷重ケース";
            _caseSelector.SelectedIndexChanged += (_, _) =>
            {
                if (_caseSelector.SelectedItem is string id)
                {
                    _service.SelectCase(id);
                    fpSpread1_Sheet2.SheetName = id;
                }
            };
            Controls.Add(_caseSelector);
            _service.CasesChanged += RefreshCaseSelector;
            RefreshCaseSelector(this, EventArgs.Empty);

            float w = 0;
            var col = fpSpread1_Sheet2.Columns;
            for (int i = 0; i < col.Count; i++)
            {
                w += col[i].Width;
            }
            w += 100;

            this.Width = (int)w;
        }

        private void SetSheet1()
        {
            fpSpread1_Sheet1.SheetName = "荷重名称";
            ConfigureRows(fpSpread1_Sheet1);

            var column = fpSpread1_Sheet1.Columns;

            var header = fpSpread1_Sheet1.ColumnHeader;

            fpSpread1_Sheet1.ColumnCount = 7;
            string[] fields = { "LL_pitch", "symbol", "name", "fix_node", "fix_member", "element", "joint" };
            for (int i = 0; i < fields.Length; i++)
                column[i].DataField = fields[i];

            header.Cells[0, 0].Text = "割増係数";
            header.Cells[0, 1].Text = "記号";
            header.Cells[0, 2].Text = "名称";
            header.Cells[0, 3].Text = "支点";
            header.Cells[0, 4].Text = "断面";
            header.Cells[0, 5].Text = "バネ";
            header.Cells[0, 6].Text = "結合";

            column[2].Width = 400;
            column[3].Width = 50;
            column[4].Width = 50;
            column[5].Width = 50;
            column[6].Width = 50;
            fpSpread1_Sheet1.DataSource = _service.LoadNames;
        }

        private void SetSheet2()
        {
            fpSpread1_Sheet2.SheetName = "荷重強度";
            ConfigureRows(fpSpread1_Sheet2);
            var column = fpSpread1_Sheet2.Columns;

            var header = fpSpread1_Sheet2.ColumnHeader;
            header.RowCount = 3;

            fpSpread1_Sheet2.ColumnCount = 16;
            string[] fields = { "LoadId", "m1", "m2", "direction", "mark", "L1", "L2",
                "P1", "P2", "n", "tx", "ty", "tz", "rx", "ry", "rz" };
            for (int i = 0; i < fields.Length; i++)
                column[i].DataField = fields[i];
            column[0].Locked = true;

            header.Cells[0, 0].Text = "実荷重番号";
            header.Cells[1, 0].Text = "";
            header.Cells[2, 0].Text = "";

            header.Cells[0, 1].Text = "要素荷重";
            header.Cells[1, 1].Text = "部材No";
            header.Cells[2, 1].Text = "1";
            header.Cells[1, 2].Text = "";
            header.Cells[2, 2].Text = "2";
            header.Cells[1, 3].Text = "方向";
            header.Cells[2, 3].Text = "(x,y,z)";
            header.Cells[1, 4].Text = "マーク";
            header.Cells[2, 4].Text = "(1,2,9,11)";
            header.Cells[1, 5].Text = "L1";
            header.Cells[2, 5].Text = "(m)";
            header.Cells[1, 6].Text = "L2";
            header.Cells[2, 6].Text = "(m)";
            header.Cells[1, 7].Text = "P1";
            header.Cells[2, 7].Text = "(kN/m)";
            header.Cells[1, 8].Text = "P2";
            header.Cells[2, 8].Text = "(kN/m)";

            header.Cells[0, 0].RowSpan = 3;
            header.Cells[0, 1].ColumnSpan = 8;
            header.Cells[1, 1].ColumnSpan = 2;

            column[0].Width = 50;
            column[1].Width = 50;
            column[2].Width = 50;

            //
            header.Cells[0, 9].Text = "節点荷重";
            header.Cells[1, 9].Text = "節点";
            header.Cells[2, 9].Text = "No";
            header.Cells[1, 10].Text = "X";
            header.Cells[2, 10].Text = "(kN)";
            header.Cells[1, 11].Text = "Y";
            header.Cells[2, 11].Text = "(kN)";
            header.Cells[1, 12].Text = "Z";
            header.Cells[2, 12].Text = "(kN)";
            header.Cells[1, 13].Text = "RX";
            header.Cells[2, 13].Text = "(kN・m)";
            header.Cells[1, 14].Text = "RY";
            header.Cells[2, 14].Text = "(kN・m)";
            header.Cells[1, 15].Text = "RZ";
            header.Cells[2, 15].Text = "(kN・m)";

            header.Cells[0, 9].ColumnSpan = 6;

            column[9].Width = 50;
            fpSpread1_Sheet2.DataSource = _service.IntensityRows;
        }

        private static void ConfigureRows(SheetView sheet)
        {
            sheet.AutoGenerateColumns = false;
            sheet.DataAutoCellTypes = false;
            sheet.DataAutoHeadings = false;
            sheet.RowHeaderAutoText = HeaderAutoText.Numbers;
            sheet.StartingRowNumber = 1;
        }

        private void RefreshCaseSelector(object? sender, EventArgs e)
        {
            string selected = _service.SelectedCaseId;
            var caseIds = new List<string> { selected };
            foreach (string id in _service.CaseIds)
                if (!caseIds.Contains(id)) caseIds.Add(id);
            _caseSelector.BeginUpdate();
            try
            {
                _caseSelector.Items.Clear();
                foreach (string id in caseIds)
                    _caseSelector.Items.Add(id);
                _caseSelector.SelectedItem = selected;
            }
            finally
            {
                _caseSelector.EndUpdate();
            }
        }


    }
}
