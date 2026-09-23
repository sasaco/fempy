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
        private AppRoutingModule helper = AppRoutingModule.Instance;

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
            helper.contentsDailogShow(typeof(InputElementsComponent));
        }

        private void rbNode_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputNodesComponent));
        }

        private void rbSupport_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputFixNodeComponent));
        }

        private void rbMember_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputMembersComponent));
        }

        private void rbShell_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputPanelComponent));
        }

        private void rbJoint_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputJointComponent));
        }

        private void rbNotice_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputNoticePointsComponent));
        }

        private void rbSpring_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputFixMemberComponent));
        }

        private void rbLoad_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputLoadNameComponent));
        }

        private void rbCombine_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(InputDefineComponent));
        }

        private void rbDisp_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(ResultDisgComponent));
        }

        private void rbReact_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(ResultReacComponent));
        }

        private void rbForce_CheckedChanged(object sender, EventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb == null) return;
            if (!rb.Checked) return;
            helper.contentsDailogShow(typeof(ResultFsecComponent));
        }
    }
}
