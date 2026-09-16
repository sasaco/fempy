using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PDF_Test
{
    public partial class Form2 : Form
    {
        const string DataFolder = "../../../TestData";
        const string OutputFolder = "../../../TestData/";

        public Form2()
        {
            InitializeComponent();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // memo: Shift-JISを扱うためのおまじない

            var files = Directory.GetFiles(DataFolder, "*.json", SearchOption.TopDirectoryOnly).Select(s => Path.GetFileName(s)).ToArray();

            comboBox1.SelectedIndex = 0;

            listBox1.Items.Clear();
            listBox1.Items.AddRange(files);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is ComboBox cb)
            {
                if (Enum.TryParse<SelectionMode>(cb.Text, out var mode))
                {
                    listBox1.SelectionMode = mode;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach (var file in listBox1.SelectedItems.Cast<string>())
            {
                var ifpath = Path.Combine(DataFolder, file);
                var ofpath = Path.Combine(OutputFolder, Path.ChangeExtension(file, ".pdf"));
                using (StreamReader st = new StreamReader(ifpath, Encoding.GetEncoding("shift-jis")))
                {
                    // テキストファイルをString型で読み込みコンソールに表示
                    string line = st.ReadToEnd();

                    // データの読み込み
                    var p = new PrintInput(line);

                    p.createPDF(ofpath);
                }
            }

            MessageBox.Show("ｵﾜﾀ＼(^o^)／");
        }
    }
}
