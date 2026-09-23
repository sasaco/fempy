using FrameWebforCS.components.input;
using FrameWebforCS.components.result;
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
        private AppRoutingModule routing = AppRoutingModule.Instance;

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
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputElementsComponent), tableLayoutPanel1.Height);
        }

        private void rbNode_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputNodesComponent), tableLayoutPanel1.Height);
        }

        private void rbSupport_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputFixNodeComponent), tableLayoutPanel1.Height);
        }

        private void rbMember_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputMembersComponent), tableLayoutPanel1.Height);
        }

        private void rbShell_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputPanelComponent), tableLayoutPanel1.Height);
        }

        private void rbJoint_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputJointComponent), tableLayoutPanel1.Height);
        }

        private void rbNotice_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputNoticePointsComponent), tableLayoutPanel1.Height);
        }

        private void rbSpring_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputFixMemberComponent), tableLayoutPanel1.Height);
        }

        private void rbLoad_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputLoadNameComponent), tableLayoutPanel1.Height);
        }

        private void rbCombine_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(InputDefineComponent), tableLayoutPanel1.Height);
        }

        private void rbDisp_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(ResultDisgComponent), tableLayoutPanel1.Height);
        }

        private void rbReact_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(ResultReacComponent), tableLayoutPanel1.Height);
        }

        private void rbForce_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            routing.contentsDailogShow(typeof(ResultFsecComponent), tableLayoutPanel1.Height);
        }
    }
}
