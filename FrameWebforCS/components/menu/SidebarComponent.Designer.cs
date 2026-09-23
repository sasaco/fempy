using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace FrameWebforCS.components.menu
{
    partial class SidebarComponent
    {
        /// <summary> 
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region コンポーネント デザイナーで生成されたコード

        /// <summary> 
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を 
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            TreeNode treeNode1 = new TreeNode("材料");
            TreeNode treeNode2 = new TreeNode("節点");
            TreeNode treeNode3 = new TreeNode("剛域");
            TreeNode treeNode4 = new TreeNode("部材", new TreeNode[] { treeNode3 });
            TreeNode treeNode5 = new TreeNode("着目点");
            TreeNode treeNode6 = new TreeNode("パネル");
            TreeNode treeNode7 = new TreeNode("ソリッド");
            TreeNode treeNode8 = new TreeNode("支点");
            TreeNode treeNode9 = new TreeNode("バネ");
            TreeNode treeNode10 = new TreeNode("結合");
            TreeNode treeNode11 = new TreeNode("荷重");
            TreeNode treeNode12 = new TreeNode("組合せ");
            TreeNode treeNode13 = new TreeNode("入力", new TreeNode[] { treeNode1, treeNode2, treeNode4, treeNode5, treeNode6, treeNode7, treeNode8, treeNode9, treeNode10, treeNode11, treeNode12 });
            TreeNode treeNode14 = new TreeNode("基本Case");
            TreeNode treeNode15 = new TreeNode("組合せ");
            TreeNode treeNode16 = new TreeNode("ピックアップ");
            TreeNode treeNode17 = new TreeNode("変位", new TreeNode[] { treeNode14, treeNode15, treeNode16 });
            TreeNode treeNode18 = new TreeNode("基本Case");
            TreeNode treeNode19 = new TreeNode("組合せ");
            TreeNode treeNode20 = new TreeNode("ピックアップ");
            TreeNode treeNode21 = new TreeNode("反力", new TreeNode[] { treeNode18, treeNode19, treeNode20 });
            TreeNode treeNode22 = new TreeNode("基本Case");
            TreeNode treeNode23 = new TreeNode("組合せ");
            TreeNode treeNode24 = new TreeNode("ピックアップ");
            TreeNode treeNode25 = new TreeNode("断面力", new TreeNode[] { treeNode22, treeNode23, treeNode24 });
            TreeNode treeNode26 = new TreeNode("出力", new TreeNode[] { treeNode17, treeNode21, treeNode25 });
            treeView1 = new System.Windows.Forms.TreeView();
            SuspendLayout();
            // 
            // treeView1
            // 
            treeView1.Dock = DockStyle.Fill;
            treeView1.Location = new Point(0, 0);
            treeView1.Name = "treeView1";
            treeNode1.Name = "element";
            treeNode1.Text = "材料";
            treeNode2.Name = "node";
            treeNode2.Text = "節点";
            treeNode3.Name = "rigid";
            treeNode3.Text = "剛域";
            treeNode4.Name = "member";
            treeNode4.Text = "部材";
            treeNode5.Name = "notice_points";
            treeNode5.Text = "着目点";
            treeNode6.Name = "shell";
            treeNode6.Text = "パネル";
            treeNode7.Name = "solid";
            treeNode7.Text = "ソリッド";
            treeNode8.Name = "fix_node";
            treeNode8.Text = "支点";
            treeNode9.Name = "fix_member";
            treeNode9.Text = "バネ";
            treeNode10.Name = "joint";
            treeNode10.Text = "結合";
            treeNode11.Name = "load";
            treeNode11.Text = "荷重";
            treeNode12.Name = "Combine";
            treeNode12.Text = "組合せ";
            treeNode13.Name = "input";
            treeNode13.Text = "入力";
            treeNode14.Name = "disg";
            treeNode14.Text = "基本Case";
            treeNode15.Name = "combdisg";
            treeNode15.Text = "組合せ";
            treeNode16.Name = "pickdisg";
            treeNode16.Text = "ピックアップ";
            treeNode17.Name = "node_displacements";
            treeNode17.Text = "変位";
            treeNode18.Name = "reac";
            treeNode18.Text = "基本Case";
            treeNode19.Name = "combreac";
            treeNode19.Text = "組合せ";
            treeNode20.Name = "pickreac";
            treeNode20.Text = "ピックアップ";
            treeNode21.Name = "reaction_forces";
            treeNode21.Text = "反力";
            treeNode22.Name = "fsec";
            treeNode22.Text = "基本Case";
            treeNode23.Name = "combfsec";
            treeNode23.Text = "組合せ";
            treeNode24.Name = "pickfsec";
            treeNode24.Text = "ピックアップ";
            treeNode25.Name = "element_stresses";
            treeNode25.Text = "断面力";
            treeNode26.Name = "output";
            treeNode26.Text = "出力";
            treeView1.Nodes.AddRange(new TreeNode[] { treeNode13, treeNode26 });
            treeView1.Size = new Size(150, 619);
            treeView1.TabIndex = 1;
            treeView1.AfterSelect += treeView1_AfterSelect;
            // 
            // SidebarComponent
            // 
            AutoScroll = true;
            Controls.Add(treeView1);
            Name = "SidebarComponent";
            Size = new Size(150, 619);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TreeView treeView1;
    }
}
