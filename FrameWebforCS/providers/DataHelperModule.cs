using FarPoint.Win.Spread;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.providers
{
    internal static class DataHelperModule
    {
        // locked 設定してるセルの編集を禁止する
        public static void faSpread_EditModeOn(object sender, EventArgs e)
        {
            FpSpread? fp = sender as FpSpread;
            if (fp == null) return;

            Cell? targetCell = fp.ActiveSheet.ActiveCell;
            if (targetCell.Locked)
            {
                fp.StopCellEditing();
                return;
            }
        }
    }
}
