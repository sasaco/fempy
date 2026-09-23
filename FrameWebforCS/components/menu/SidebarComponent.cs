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
        private AppRoutingModule routing = new AppRoutingModule();

        private Dictionary<string, Type> targetComponents = new Dictionary<string, Type>
        {
            { "element", typeof(InputElementsComponent) }, // 材料
            { "node", typeof(InputNodesComponent) }, // 節点
            { "rigid", typeof(InputMembersComponent) }, // 剛域
            { "member", typeof(InputMembersComponent) }, // 部材
            { "notice_points", typeof(InputNoticePointsComponent) }, // 着目点
            { "shell", typeof(InputPanelComponent) }, // パネル
            { "solid", null }, // ソリッド
            { "fix_node", typeof(InputFixNodeComponent) }, // 支点
            { "fix_member", typeof(InputFixMemberComponent) }, // バネ
            { "joint", typeof(InputJointComponent) }, // 結合
            { "load", typeof(InputLoadNameComponent) }, // 荷重
            { "Combine", typeof(InputDefineComponent) } , // 組合せ
            { "disg", typeof(ResultDisgComponent) }, // 変位: 基本Case
            { "combdisg", typeof(ResultCombineDisgComponent) }, // 変位: 組合せ
            { "pickdisg", typeof(ResultPickupDisgComponent) }, // 変位: ピックアップ
            { "reac", typeof(ResultReacComponent) }, //反力: 基本Case
            { "combreac", typeof(ResultCombineReacComponent) }, //反力: 組合せ
            { "pickreac", typeof(ResultPickupReacComponent) }, //反力: ピックアップ
            { "fsec", typeof(ResultFsecComponent) }, //断面力: 基本Case
            { "combfsec", typeof(ResultCombineFsecComponent) }, //断面力: 組合せ
            { "pickfsec", typeof(ResultPickupFsecComponent) }, //断面力: ピックアップ
        };

        public SidebarComponent()
        {
            InitializeComponent();
            treeView1.ExpandAll();

        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var key = e.Node?.Name;

            if (key == null)
                return;

            if (targetComponents.ContainsKey(key)) 
            {
                var value = targetComponents[key];
                if (value != null)
                {
                    routing.contentsDailogShow(value, e.Node?.Text);
                    e.Node?.Checked = true;
                }
            }
            
        }


    }
}
