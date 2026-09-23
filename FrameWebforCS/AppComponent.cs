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
        private AppRoutingModule helper;
        public AppComponent()
        {
            InitializeComponent();

            three = new ThreeComponent(glControl1);
            helper = AppRoutingModule.Instance;
            helper.ContentsDailog = toolStrip1;
        }

  
    }
}
