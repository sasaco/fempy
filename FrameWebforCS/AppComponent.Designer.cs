using FrameWebforCS.components.menu;
using FrameWebforCS.three;
using OpenTK.WinForms;

namespace FrameWebforCS
{
    partial class AppComponent
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            tableLayoutPanel1 = new TableLayoutPanel();
            menuComponent1 = new MenuComponent();
            splitContainer1 = new SplitContainer();
            SidebarComponent1 = new SidebarComponent();
            toolStripContainer1 = new ToolStripContainer();
            glControl1 = new GLControl();
            toolStrip1 = new ToolStrip();
            toolStripLabel1 = new ToolStripLabel();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            toolStripContainer1.ContentPanel.SuspendLayout();
            toolStripContainer1.RightToolStripPanel.SuspendLayout();
            toolStripContainer1.SuspendLayout();
            toolStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(menuComponent1, 0, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Margin = new Padding(3, 0, 3, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle());
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(800, 450);
            tableLayoutPanel1.TabIndex = 4;
            // 
            // menuComponent1
            // 
            menuComponent1.BackColor = SystemColors.Control;
            menuComponent1.Dock = DockStyle.Fill;
            menuComponent1.Location = new Point(6, 6);
            menuComponent1.Margin = new Padding(6);
            menuComponent1.Name = "menuComponent1";
            menuComponent1.Size = new Size(788, 26);
            menuComponent1.TabIndex = 0;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(SidebarComponent1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(toolStripContainer1);
            splitContainer1.Size = new Size(800, 450);
            splitContainer1.TabIndex = 5;
            // 
            // SidebarComponent1
            // 
            SidebarComponent1.BackColor = SystemColors.Control;
            SidebarComponent1.Dock = DockStyle.Fill;
            SidebarComponent1.Location = new Point(0, 0);
            SidebarComponent1.Name = "SidebarComponent1";
            SidebarComponent1.Size = new Size(80, 450);
            SidebarComponent1.TabIndex = 1;
            // 
            // toolStripContainer1
            // 
            // 
            // toolStripContainer1.ContentPanel
            // 
            toolStripContainer1.ContentPanel.Controls.Add(glControl1);
            toolStripContainer1.ContentPanel.Size = new Size(629, 425);
            toolStripContainer1.Dock = DockStyle.Fill;
            toolStripContainer1.Location = new Point(0, 0);
            toolStripContainer1.Margin = new Padding(2, 1, 2, 1);
            toolStripContainer1.Name = "toolStripContainer1";
            // 
            // toolStripContainer1.RightToolStripPanel
            // 
            toolStripContainer1.RightToolStripPanel.Controls.Add(toolStrip1);
            toolStripContainer1.Size = new Size(716, 450);
            toolStripContainer1.TabIndex = 2;
            toolStripContainer1.Text = "toolStripContainer1";
            // 
            // glControl1
            // 
            glControl1.API = OpenTK.Windowing.Common.ContextAPI.OpenGL;
            glControl1.APIVersion = new Version(3, 3, 0, 0);
            glControl1.Dock = DockStyle.Fill;
            glControl1.Flags = OpenTK.Windowing.Common.ContextFlags.Default;
            glControl1.IsEventDriven = true;
            glControl1.Location = new Point(0, 0);
            glControl1.Margin = new Padding(2, 1, 2, 1);
            glControl1.Name = "glControl1";
            glControl1.Profile = OpenTK.Windowing.Common.ContextProfile.Core;
            glControl1.Size = new Size(629, 425);
            glControl1.TabIndex = 0;
            glControl1.Text = "glControl1";
            // 
            // toolStrip1
            // 
            toolStrip1.Dock = DockStyle.None;
            toolStrip1.Items.AddRange(new ToolStripItem[] { toolStripLabel1 });
            toolStrip1.Location = new Point(0, 3);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(87, 29);
            toolStrip1.TabIndex = 0;
            // 
            // toolStripLabel1
            // 
            toolStripLabel1.Name = "toolStripLabel1";
            toolStripLabel1.Size = new Size(85, 15);
            toolStripLabel1.Text = "toolStripLabel1";
            // 
            // AppComponent
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(splitContainer1);
            Controls.Add(tableLayoutPanel1);
            Name = "AppComponent";
            Text = "立体有限要素法構造解析";
            WindowState = FormWindowState.Maximized;
            tableLayoutPanel1.ResumeLayout(false);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            toolStripContainer1.ContentPanel.ResumeLayout(false);
            toolStripContainer1.RightToolStripPanel.ResumeLayout(false);
            toolStripContainer1.RightToolStripPanel.PerformLayout();
            toolStripContainer1.ResumeLayout(false);
            toolStripContainer1.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private TableLayoutPanel tableLayoutPanel1;
        private MenuComponent menuComponent1;
        private SidebarComponent SidebarComponent1;
        private ToolStripContainer toolStripContainer1;
        private GLControl glControl1;
        private ToolStrip toolStrip1;
        private ToolStripLabel toolStripLabel1;
        private SplitContainer splitContainer1;
    }
}