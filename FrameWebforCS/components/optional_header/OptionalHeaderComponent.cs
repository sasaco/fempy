using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.components.optional_header
{
    internal class OptionalHeaderComponent : MenuStrip
    {
        public OptionalHeaderComponent()
        {
            InitializeComponent();
        }

        private ToolStripComboBox toolStripComboBox1;
        private ToolStripComboBox toolStripComboBox2;

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            toolStripComboBox1 = new ToolStripComboBox();
            toolStripComboBox2 = new ToolStripComboBox();

            // 
            // toolStripComboBox1
            // 
            toolStripComboBox1.Items.AddRange(new object[] { "2D", "3D" });
            toolStripComboBox1.MaxDropDownItems = 2;
            toolStripComboBox1.Name = "toolStripComboBox1";
            toolStripComboBox1.Size = new Size(121, 23);
            // 
            // toolStripComboBox2
            // 
            toolStripComboBox2.Name = "toolStripComboBox2";
            toolStripComboBox2.Size = new Size(121, 23);

            this.Items.AddRange(new ToolStripItem[] { toolStripComboBox1, toolStripComboBox2 });

        }

    }
}
