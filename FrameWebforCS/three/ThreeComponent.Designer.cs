namespace FrameWebforCS.three
{
    partial class ThreeComponent
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
            dockPanel1 = new WeifenLuo.WinFormsUI.Docking.DockPanel();
            SuspendLayout();
            // 
            // dockPanel1
            // 
            dockPanel1.Dock = DockStyle.Fill;
            dockPanel1.Location = new Point(0, 0);
            dockPanel1.Name = "dockPanel1";
            dockPanel1.Size = new Size(810, 359);
            dockPanel1.TabIndex = 0;
            // 
            // ThreeComponent
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.ControlLightLight;
            Controls.Add(dockPanel1);
            Name = "ThreeComponent";
            Size = new Size(810, 359);
            ResumeLayout(false);
        }

        #endregion

        private WeifenLuo.WinFormsUI.Docking.DockPanel dockPanel1;
    }
}
