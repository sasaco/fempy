namespace FrameWebforCS.components.input
{
    partial class InputCombineComponent
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
            fpSpread1 = new FarPoint.Win.Spread.FpSpread(FarPoint.Win.Spread.LegacyBehaviors.None, null);
            ((System.ComponentModel.ISupportInitialize)fpSpread1).BeginInit();
            SuspendLayout();
            // 
            // fpSpread1
            // 
            fpSpread1.AccessibleDescription = "";
            fpSpread1.Dock = DockStyle.Fill;
            fpSpread1.Font = new Font("ＭＳ ゴシック", 9F);
            fpSpread1.Location = new Point(0, 0);
            fpSpread1.Name = "fpSpread1";
            fpSpread1.Size = new Size(150, 150);
            fpSpread1.TabIndex = 0;
            // 
            // InputCombineComponent
            // 
            AutoScaleDimensions = new SizeF(192F, 192F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(fpSpread1);
            Name = "InputCombineComponent";
            ((System.ComponentModel.ISupportInitialize)fpSpread1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private FarPoint.Win.Spread.FpSpread fpSpread1;
    }
}
