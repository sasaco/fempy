namespace FrameWebforCS.components.result
{
    partial class ResultCombineFsecComponent
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
            if (disposing)
            {
                DisposeCombineResources();
                components?.Dispose();
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
            modePanel = new Panel();
            modeLabel = new Label();
            modeSelector = new ComboBox();
            statusLabel = new Label();
            fpSpread1 = new myFpSpread();
            ((System.ComponentModel.ISupportInitialize)fpSpread1).BeginInit();
            modePanel.SuspendLayout();
            SuspendLayout();
            // modePanel
            modePanel.Controls.Add(modeSelector);
            modePanel.Controls.Add(modeLabel);
            modePanel.Dock = DockStyle.Top;
            modePanel.Location = new Point(0, 0);
            modePanel.Name = "modePanel";
            modePanel.Size = new Size(760, 34);
            modePanel.TabIndex = 0;
            // modeLabel
            modeLabel.AutoSize = true;
            modeLabel.Location = new Point(10, 9);
            modeLabel.Name = "modeLabel";
            modeLabel.Text = "着目項目";
            // modeSelector
            modeSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            modeSelector.Location = new Point(88, 5);
            modeSelector.Name = "modeSelector";
            modeSelector.Size = new Size(240, 25);
            modeSelector.TabIndex = 0;
            // statusLabel
            statusLabel.AutoEllipsis = true;
            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.Location = new Point(0, 544);
            statusLabel.Name = "statusLabel";
            statusLabel.Padding = new Padding(8, 2, 8, 2);
            statusLabel.Size = new Size(760, 22);
            statusLabel.TabIndex = 2;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // fpSpread1
            fpSpread1.Dock = DockStyle.Fill;
            fpSpread1.Location = new Point(0, 34);
            fpSpread1.Name = "fpSpread1";
            fpSpread1.Size = new Size(760, 510);
            fpSpread1.TabIndex = 1;
            //
            // ResultCombineFsecComponent
            //
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(fpSpread1);
            Controls.Add(statusLabel);
            Controls.Add(modePanel);
            Name = "ResultCombineFsecComponent";
            Size = new Size(760, 566);
            ((System.ComponentModel.ISupportInitialize)fpSpread1).EndInit();
            modePanel.ResumeLayout(false);
            modePanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private myFpSpread fpSpread1;
        private Panel modePanel;
        private Label modeLabel;
        private ComboBox modeSelector;
        private Label statusLabel;
    }
}
