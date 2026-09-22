using FrameWebforCS.components.input;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FrameWebforCS.components.menu
{
    public partial class SidebarComponent : UserControl
    {
        private DataHelperModule helper = DataHelperModule.Instance;

        public SidebarComponent()
        {
            InitializeComponent();
        }

        // 計算ボタンクリック
        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void rbElement_CheckedChanged(object sender, EventArgs e)
        {
            helper.ChangeWindow(typeof(InputElementsComponent));
        }

        private void rbNode_CheckedChanged(object sender, EventArgs e)
        {
            helper.ChangeWindow(typeof(InputNodesComponent));
        }

        private void rbSupport_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbMember_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbShell_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbJoint_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbNotice_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbSpring_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbLoad_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbCombine_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbDisp_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbReact_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void rbForce_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
