using FarPoint.Win.Spread;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.components
{
    internal class myFpSpread : FarPoint.Win.Spread.FpSpread
    {
        public myFpSpread() 
        {
            AccessibleDescription = "";
            Font = new Font("ＭＳ ゴシック", 9F);
            KeyDown += myFpSpread_KeyDown;
        }

        public SheetView AddNewSheetView()
        {
            var fpSpread1_Sheet1 = base.AddNewSheetView();

            return fpSpread1_Sheet1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (SheetView sheet in Sheets)
                {
                    if (sheet.DataSource != null)
                        sheet.DataSource = null;
                }
            }

            base.Dispose(disposing);
        }

        private void myFpSpread_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete || e.Modifiers != Keys.None || this.EditMode)
                return;

            var sheet = ActiveSheet;
            if (sheet?.DataSource == null)
                return;

            int row = sheet.ActiveRowIndex;
            int column = sheet.ActiveColumnIndex;
            if (row < 0 || row >= sheet.RowCount || column < 0 || column >= sheet.ColumnCount)
                return;

            sheet.Cells[row, column].Value = null;
            e.SuppressKeyPress = true;
        }

        // locked 設定してるセルの編集を禁止する
        public void faSpread_EditModeOn(object sender, EventArgs e)
        {
            FpSpread? fp = sender as FpSpread;
            if (fp == null) return;

            Cell? targetCell = fp.ActiveSheet.ActiveCell;
            if (targetCell.BackColor == SystemColors.Control)
            {
                fp.StopCellEditing();
                return;
            }
        }
    }
}
