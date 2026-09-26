using FarPoint.Win.Spread;
using FrameWebforCS.components.input;
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

        private void myFpSpread_KeyDown(object? sender, KeyEventArgs e)
        {
            //if (e.KeyCode != Keys.Delete || e.Modifiers != Keys.None || EditMode)
            //    return;

        }
    }
}
