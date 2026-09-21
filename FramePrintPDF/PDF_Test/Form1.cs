using PDF_Manager;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PDF_Test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance); // memo: Shift-JISを扱うためのおまじない
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 読み込みたいテキストを開く
            // using (StreamReader st = new StreamReader(@"../../../TestData/test001.json", Encoding.GetEncoding("UTF-8")))
            using (StreamReader st = new StreamReader(@"../../../TestData/test020_comb断面力.json", Encoding.GetEncoding("shift-jis")))
            {
                // テキストファイルをString型で読み込みコンソールに表示
                String line = st.ReadToEnd();

                // データの読み込み
                var p = new PrintInput(line);

                p.createPDF();

                // PDF を表示する
                var oProc = new Process();
                oProc.StartInfo.FileName = Path.GetFullPath("../../../TestData/Test.pdf");
                oProc.StartInfo.UseShellExecute = true;
                oProc.Start();
                oProc.WaitForExit();
                
                // MessageBox.Show("ｵﾜﾀ＼(^o^)／");
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            button1_Click(sender, null);
        }
    }
}
