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
        private AppRoutingModule routing;
        public AppComponent()
        {
            InitializeComponent();

            splitContainer1.SplitterDistance = SidebarComponent1.Width;


            three = new ThreeComponent(glControl1);
            routing = AppRoutingModule.Instance;
            routing.ContentsDailog = toolStrip1;
        }

  
    }
}
