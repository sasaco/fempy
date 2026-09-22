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
            tableLayoutPanel1 = new TableLayoutPanel();
            panel1 = new Panel();
            button1 = new Button();
            rbForce = new RadioButton();
            rbReact = new RadioButton();
            rbDisp = new RadioButton();
            rbCombine = new RadioButton();
            rbLoad = new RadioButton();
            rbSpring = new RadioButton();
            rbNotice = new RadioButton();
            rbJoint = new RadioButton();
            rbShell = new RadioButton();
            rbMember = new RadioButton();
            rbSupport = new RadioButton();
            rbNode = new RadioButton();
            rbElement = new RadioButton();
            btToggle = new Button();
            tableLayoutPanel1.SuspendLayout();
            panel1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(panel1, 0, 0);
            tableLayoutPanel1.Controls.Add(btToggle, 0, 2);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(6);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 853F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle());
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
            tableLayoutPanel1.Size = new Size(167, 1321);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // panel1
            // 
            panel1.Controls.Add(button1);
            panel1.Controls.Add(rbForce);
            panel1.Controls.Add(rbReact);
            panel1.Controls.Add(rbDisp);
            panel1.Controls.Add(rbCombine);
            panel1.Controls.Add(rbLoad);
            panel1.Controls.Add(rbSpring);
            panel1.Controls.Add(rbNotice);
            panel1.Controls.Add(rbJoint);
            panel1.Controls.Add(rbShell);
            panel1.Controls.Add(rbMember);
            panel1.Controls.Add(rbSupport);
            panel1.Controls.Add(rbNode);
            panel1.Controls.Add(rbElement);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(6, 6);
            panel1.Margin = new Padding(6);
            panel1.Name = "panel1";
            panel1.Size = new Size(155, 841);
            panel1.TabIndex = 0;
            // 
            // button1
            // 
            button1.Location = new Point(2, 0);
            button1.Name = "button1";
            button1.Size = new Size(150, 46);
            button1.TabIndex = 14;
            button1.Text = "計算";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // rbForce
            // 
            rbForce.AutoSize = true;
            rbForce.Location = new Point(6, 745);
            rbForce.Margin = new Padding(6);
            rbForce.Name = "rbForce";
            rbForce.Size = new Size(117, 36);
            rbForce.TabIndex = 13;
            rbForce.TabStop = true;
            rbForce.Text = "断面力";
            rbForce.UseVisualStyleBackColor = true;
            rbForce.CheckedChanged += rbForce_CheckedChanged;
            // 
            // rbReact
            // 
            rbReact.AutoSize = true;
            rbReact.Location = new Point(6, 691);
            rbReact.Margin = new Padding(6);
            rbReact.Name = "rbReact";
            rbReact.Size = new Size(93, 36);
            rbReact.TabIndex = 12;
            rbReact.TabStop = true;
            rbReact.Text = "反力";
            rbReact.UseVisualStyleBackColor = true;
            rbReact.CheckedChanged += rbReact_CheckedChanged;
            // 
            // rbDisp
            // 
            rbDisp.AutoSize = true;
            rbDisp.Location = new Point(6, 638);
            rbDisp.Margin = new Padding(6);
            rbDisp.Name = "rbDisp";
            rbDisp.Size = new Size(93, 36);
            rbDisp.TabIndex = 11;
            rbDisp.TabStop = true;
            rbDisp.Text = "変位";
            rbDisp.UseVisualStyleBackColor = true;
            rbDisp.CheckedChanged += rbDisp_CheckedChanged;
            // 
            // rbCombine
            // 
            rbCombine.AutoSize = true;
            rbCombine.Location = new Point(6, 563);
            rbCombine.Margin = new Padding(6);
            rbCombine.Name = "rbCombine";
            rbCombine.Size = new Size(113, 36);
            rbCombine.TabIndex = 10;
            rbCombine.TabStop = true;
            rbCombine.Text = "組合せ";
            rbCombine.UseVisualStyleBackColor = true;
            rbCombine.CheckedChanged += rbCombine_CheckedChanged;
            // 
            // rbLoad
            // 
            rbLoad.AutoSize = true;
            rbLoad.Location = new Point(6, 510);
            rbLoad.Margin = new Padding(6);
            rbLoad.Name = "rbLoad";
            rbLoad.Size = new Size(93, 36);
            rbLoad.TabIndex = 9;
            rbLoad.TabStop = true;
            rbLoad.Text = "荷重";
            rbLoad.UseVisualStyleBackColor = true;
            rbLoad.CheckedChanged += rbLoad_CheckedChanged;
            // 
            // rbSpring
            // 
            rbSpring.AutoSize = true;
            rbSpring.Location = new Point(6, 457);
            rbSpring.Margin = new Padding(6);
            rbSpring.Name = "rbSpring";
            rbSpring.Size = new Size(84, 36);
            rbSpring.TabIndex = 8;
            rbSpring.TabStop = true;
            rbSpring.Text = "バネ";
            rbSpring.UseVisualStyleBackColor = true;
            rbSpring.CheckedChanged += rbSpring_CheckedChanged;
            // 
            // rbNotice
            // 
            rbNotice.AutoSize = true;
            rbNotice.Location = new Point(6, 403);
            rbNotice.Margin = new Padding(6);
            rbNotice.Name = "rbNotice";
            rbNotice.Size = new Size(117, 36);
            rbNotice.TabIndex = 7;
            rbNotice.TabStop = true;
            rbNotice.Text = "着目点";
            rbNotice.UseVisualStyleBackColor = true;
            rbNotice.CheckedChanged += rbNotice_CheckedChanged;
            // 
            // rbJoint
            // 
            rbJoint.AutoSize = true;
            rbJoint.Location = new Point(6, 350);
            rbJoint.Margin = new Padding(6);
            rbJoint.Name = "rbJoint";
            rbJoint.Size = new Size(93, 36);
            rbJoint.TabIndex = 6;
            rbJoint.TabStop = true;
            rbJoint.Text = "結合";
            rbJoint.UseVisualStyleBackColor = true;
            rbJoint.CheckedChanged += rbJoint_CheckedChanged;
            // 
            // rbShell
            // 
            rbShell.AutoSize = true;
            rbShell.Location = new Point(6, 297);
            rbShell.Margin = new Padding(6);
            rbShell.Name = "rbShell";
            rbShell.Size = new Size(104, 36);
            rbShell.TabIndex = 5;
            rbShell.TabStop = true;
            rbShell.Text = "パネル";
            rbShell.UseVisualStyleBackColor = true;
            rbShell.CheckedChanged += rbShell_CheckedChanged;
            // 
            // rbMember
            // 
            rbMember.AutoSize = true;
            rbMember.Location = new Point(6, 243);
            rbMember.Margin = new Padding(6);
            rbMember.Name = "rbMember";
            rbMember.Size = new Size(93, 36);
            rbMember.TabIndex = 4;
            rbMember.TabStop = true;
            rbMember.Text = "部材";
            rbMember.UseVisualStyleBackColor = true;
            rbMember.CheckedChanged += rbMember_CheckedChanged;
            // 
            // rbSupport
            // 
            rbSupport.AutoSize = true;
            rbSupport.Location = new Point(6, 190);
            rbSupport.Margin = new Padding(6);
            rbSupport.Name = "rbSupport";
            rbSupport.Size = new Size(93, 36);
            rbSupport.TabIndex = 3;
            rbSupport.TabStop = true;
            rbSupport.Text = "支点";
            rbSupport.UseVisualStyleBackColor = true;
            rbSupport.CheckedChanged += rbSupport_CheckedChanged;
            // 
            // rbNode
            // 
            rbNode.AutoSize = true;
            rbNode.Location = new Point(6, 137);
            rbNode.Margin = new Padding(6);
            rbNode.Name = "rbNode";
            rbNode.Size = new Size(93, 36);
            rbNode.TabIndex = 2;
            rbNode.TabStop = true;
            rbNode.Text = "節点";
            rbNode.UseVisualStyleBackColor = true;
            rbNode.CheckedChanged += rbNode_CheckedChanged;
            // 
            // rbElement
            // 
            rbElement.AutoSize = true;
            rbElement.Location = new Point(6, 83);
            rbElement.Margin = new Padding(6);
            rbElement.Name = "rbElement";
            rbElement.Size = new Size(93, 36);
            rbElement.TabIndex = 1;
            rbElement.TabStop = true;
            rbElement.Text = "材料";
            rbElement.UseVisualStyleBackColor = true;
            rbElement.CheckedChanged += rbElement_CheckedChanged;
            // 
            // btToggle
            // 
            btToggle.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btToggle.Location = new Point(6, 1266);
            btToggle.Margin = new Padding(6);
            btToggle.Name = "btToggle";
            btToggle.Size = new Size(155, 49);
            btToggle.TabIndex = 1;
            btToggle.Text = "<";
            btToggle.UseVisualStyleBackColor = true;
            // 
            // SidebarComponent
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tableLayoutPanel1);
            Margin = new Padding(6);
            Name = "SidebarComponent";
            Size = new Size(167, 1321);
            tableLayoutPanel1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private Panel panel1;
        private RadioButton rbNotice;
        private RadioButton rbJoint;
        private RadioButton rbShell;
        private RadioButton rbMember;
        private RadioButton rbSupport;
        private RadioButton rbNode;
        private RadioButton rbElement;
        private RadioButton rbCombine;
        private RadioButton rbLoad;
        private RadioButton rbSpring;
        private RadioButton rbForce;
        private RadioButton rbReact;
        private RadioButton rbDisp;
        private Button btToggle;
        private Button button1;
    }
}
