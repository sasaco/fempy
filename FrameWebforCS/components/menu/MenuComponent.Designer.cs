namespace FrameWebforCS
{
    partial class MenuComponent
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
            pictureBox1 = new PictureBox();
            menuStrip1 = new MenuStrip();
            ファイルToolStripMenuItem = new ToolStripMenuItem();
            新規作成ToolStripMenuItem = new ToolStripMenuItem();
            ファイルを開くToolStripMenuItem = new ToolStripMenuItem();
            ファイルを保存ToolStripMenuItem = new ToolStripMenuItem();
            プリセットを開くToolStripMenuItem = new ToolStripMenuItem();
            印刷ToolStripMenuItem = new ToolStripMenuItem();
            button1 = new Button();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 4;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            tableLayoutPanel1.Controls.Add(pictureBox1, 0, 0);
            tableLayoutPanel1.Controls.Add(menuStrip1, 1, 0);
            tableLayoutPanel1.Controls.Add(button1, 3, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(903, 24);
            tableLayoutPanel1.TabIndex = 1;
            // 
            // pictureBox1
            // 
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Image = Properties.Resources.logo;
            pictureBox1.Location = new Point(3, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(18, 18);
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox1.TabIndex = 1;
            pictureBox1.TabStop = false;
            // 
            // menuStrip1
            // 
            menuStrip1.Dock = DockStyle.Fill;
            menuStrip1.Items.AddRange(new ToolStripItem[] { ファイルToolStripMenuItem, 印刷ToolStripMenuItem });
            menuStrip1.Location = new Point(24, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(224, 24);
            menuStrip1.TabIndex = 2;
            menuStrip1.Text = "menuStrip1";
            // 
            // ファイルToolStripMenuItem
            // 
            ファイルToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { 新規作成ToolStripMenuItem, ファイルを開くToolStripMenuItem, ファイルを保存ToolStripMenuItem, プリセットを開くToolStripMenuItem });
            ファイルToolStripMenuItem.Name = "ファイルToolStripMenuItem";
            ファイルToolStripMenuItem.Size = new Size(53, 20);
            ファイルToolStripMenuItem.Text = "ファイル";
            // 
            // 新規作成ToolStripMenuItem
            // 
            新規作成ToolStripMenuItem.Name = "新規作成ToolStripMenuItem";
            新規作成ToolStripMenuItem.Size = new Size(180, 22);
            新規作成ToolStripMenuItem.Text = "新規作成";
            // 
            // ファイルを開くToolStripMenuItem
            // 
            ファイルを開くToolStripMenuItem.Name = "ファイルを開くToolStripMenuItem";
            ファイルを開くToolStripMenuItem.Size = new Size(180, 22);
            ファイルを開くToolStripMenuItem.Text = "ファイルを開く";
            // 
            // ファイルを保存ToolStripMenuItem
            // 
            ファイルを保存ToolStripMenuItem.Name = "ファイルを保存ToolStripMenuItem";
            ファイルを保存ToolStripMenuItem.Size = new Size(180, 22);
            ファイルを保存ToolStripMenuItem.Text = "ファイルを保存";
            // 
            // プリセットを開くToolStripMenuItem
            // 
            プリセットを開くToolStripMenuItem.Name = "プリセットを開くToolStripMenuItem";
            プリセットを開くToolStripMenuItem.Size = new Size(180, 22);
            プリセットを開くToolStripMenuItem.Text = "プリセットを開く";
            // 
            // 印刷ToolStripMenuItem
            // 
            印刷ToolStripMenuItem.Name = "印刷ToolStripMenuItem";
            印刷ToolStripMenuItem.Size = new Size(43, 20);
            印刷ToolStripMenuItem.Text = "印刷";
            // 
            // button1
            // 
            button1.Dock = DockStyle.Fill;
            button1.FlatAppearance.BorderSize = 0;
            button1.FlatStyle = FlatStyle.Flat;
            button1.Location = new Point(806, 0);
            button1.Margin = new Padding(3, 0, 3, 0);
            button1.Name = "button1";
            button1.Size = new Size(94, 24);
            button1.TabIndex = 4;
            button1.Text = "login";
            button1.UseVisualStyleBackColor = true;
            // 
            // MenuComponent
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            Controls.Add(tableLayoutPanel1);
            Name = "MenuComponent";
            Size = new Size(903, 24);
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private TableLayoutPanel tableLayoutPanel1;
        private PictureBox pictureBox1;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem ファイルToolStripMenuItem;
        private ToolStripMenuItem 新規作成ToolStripMenuItem;
        private ToolStripMenuItem ファイルを開くToolStripMenuItem;
        private ToolStripMenuItem ファイルを保存ToolStripMenuItem;
        private ToolStripMenuItem プリセットを開くToolStripMenuItem;
        private ToolStripMenuItem 印刷ToolStripMenuItem;
        private Button button1;
    }
}
