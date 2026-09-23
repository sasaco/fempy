namespace FrameWebforCS.components.input
{
    partial class InputElementsComponent
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
            panel1 = new Panel();
            fpSpread1 = new FarPoint.Win.Spread.FpSpread(FarPoint.Win.Spread.LegacyBehaviors.None, null);
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)fpSpread1).BeginInit();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(fpSpread1);
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(658, 473);
            panel1.TabIndex = 1;
            // 
            // fpSpread1
            // 
            fpSpread1.Dock = DockStyle.Fill;
            fpSpread1.Font = new Font("ＭＳ Ｐゴシック", 11F);
            fpSpread1.Location = new Point(0, 0);
            fpSpread1.Name = "fpSpread1";
            fpSpread1.Size = new Size(658, 473);
            fpSpread1.TabIndex = 0;
            // 
            // InputElementsComponent
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.ActiveCaption;
            Controls.Add(panel1);
            Name = "InputElementsComponent";
            Size = new Size(658, 473);
            panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)fpSpread1).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private Panel panel1;
        private FarPoint.Win.Spread.FpSpread fpSpread1;
    }
}
