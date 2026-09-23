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
            glControl1 = new GLControl();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(menuComponent1, 0, 0);
            tableLayoutPanel1.Controls.Add(splitContainer1, 1, 0);
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
            splitContainer1.Location = new Point(3, 41);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(SidebarComponent1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(glControl1);
            splitContainer1.Size = new Size(794, 406);
            splitContainer1.SplitterDistance = 249;
            splitContainer1.TabIndex = 5;
            splitContainer1.SplitterMoved += splitContainer1_SplitterMoved;
            // 
            // SidebarComponent1
            // 
            SidebarComponent1.AutoScroll = true;
            SidebarComponent1.BackColor = SystemColors.Control;
            SidebarComponent1.Dock = DockStyle.Left;
            SidebarComponent1.Location = new Point(0, 0);
            SidebarComponent1.Name = "SidebarComponent1";
            SidebarComponent1.Size = new Size(249, 406);
            SidebarComponent1.TabIndex = 1;
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
            glControl1.Size = new Size(541, 406);
            glControl1.TabIndex = 1;
            glControl1.Text = "glControl1";
            // 
            // AppComponent
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(tableLayoutPanel1);
            Name = "AppComponent";
            Text = "立体有限要素法構造解析";
            WindowState = FormWindowState.Maximized;
            tableLayoutPanel1.ResumeLayout(false);
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private TableLayoutPanel tableLayoutPanel1;
        private MenuComponent menuComponent1;
        private SidebarComponent SidebarComponent1;
        private SplitContainer splitContainer1;
        private GLControl glControl1;
    }
}