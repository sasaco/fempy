using FrameWebforCS.providers;
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
        private DataHelperModule helper;
        public AppComponent()
        {
            InitializeComponent();

            three = new ThreeComponent(glControl1);
            helper = DataHelperModule.Instance;
            helper.FloatingWindow = toolStrip1;
        }

  
    }
}
