using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FrameWebforCS.components.result
{
    public partial class ResultCombineReacComponent : UserControl
    {
        internal FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;

        public ResultCombineReacComponent()
        {
            InitializeComponent();
            fpSpread1.EditModeOn += fpSpread1.faSpread_EditModeOn;

            Dictionary<string, object> result = getCombineReac();

            foreach (var item in result)
            {
                var fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

                fpSpread1_Sheet1.SheetName = item.Key;

                SetSheet2(fpSpread1_Sheet1);
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

        public void setActiveSheet(int index)
        {
            this.fpSpread1.ActiveSheetIndex = index;
        }


        private void SetSheet2(SheetView _Sheet)
        {
            ResultReacComponent.SetSheet1(_Sheet);

            _Sheet.AddColumns(_Sheet.ColumnCount, 1);

            int index = _Sheet.ColumnCount - 1;
            var header = _Sheet.ColumnHeader;
            var column = _Sheet.Columns;

            header.Cells[0, index].Text = "組み合わせ";
            header.Cells[1, index].Text = " ";
            column[index].Width = 200;
        }
        public virtual Dictionary<string, object> getCombineReac()
        {
            return InputDataService.Instance.getCombineReac();
        }
    }
}

