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
    public partial class ResultCombineDisgComponent : UserControl
    {
        internal FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;

        public ResultCombineDisgComponent()
        {
            InitializeComponent();
            fpSpread1.EditModeOn += DataHelperModule.faSpread_EditModeOn;

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();
            fpSpread1_Sheet1.SheetName = "COMBINE";
            SetSheet2(fpSpread1_Sheet1);


            float w = 0;
            var col = fpSpread1_Sheet1.Columns;
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
            ResultDisgComponent.SetSheet1(_Sheet);

            _Sheet.AddColumns(_Sheet.ColumnCount, 1);

            int index = _Sheet.ColumnCount - 1;
            var header = _Sheet.ColumnHeader;
            var column = _Sheet.Columns;

            header.Cells[0, index].Text = "組み合わせ";
            header.Cells[1, index].Text = " ";
            column[index].Width = 200;

        }
    }
}
