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
        }

        public SheetView AddNewSheetView()
        {
            var fpSpread1_Sheet1 = base.AddNewSheetView();

            return fpSpread1_Sheet1;
        }
    }
}
