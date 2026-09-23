using FarPoint.Win.Spread;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FrameWebforCS.components.input
{
    public partial class InputElementsComponent : UserControl
    {
        private FarPoint.Win.Spread.SheetView fpSpread1_Sheet1;

        public InputElementsComponent()
        {
            InitializeComponent();

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();

        }

        public void setElementJson(string json)
        {
            // Implementation for setting element JSON
        }
    }
}
