using FrameWebforCS.three;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FrameWebforCS
{
    public partial class AppComponent : Form
    {
        private ThreeComponent three;
        public AppComponent()
        {
            InitializeComponent();

            three = new ThreeComponent(glControl1);
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            splitContainer1.SplitterDistance = Math.Min(splitContainer1.SplitterDistance, SidebarComponent1.Width);
        }
    }
}
