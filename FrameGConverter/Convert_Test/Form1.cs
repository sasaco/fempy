using Convert_Test;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GConvert_Test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance); // memo: Shift-JISを扱うためのおまじない
        }

        private void ConvertFRD(string[] FileNames)
        {
            var ConvertedList = new List<resultMessage>();

            foreach(var fileName in FileNames)
            {
                // 拡張子を変更する
                string newName = Path.ChangeExtension(fileName, ".json");

                // 読み込みたいテキストを開く
                using (StreamReader st = new StreamReader(fileName))
                {
                    var conv = new ConvertManager(st.BaseStream);
                    SaveFile(newName, conv.getJsonString());

                    var rM = new resultMessage();
                    rM.filename = newName;

                    if (conv._member.message.Length > 0)
                        rM.message.Add(conv._member.message);

                    if (conv._joint.message.Length > 0)
                        rM.message.Add(conv._joint.message);

                    if (conv._gouiki.message.Length > 0)
                        rM.message.Add(conv._gouiki.message);

                    if (conv._load.message.Length > 0)
                        rM.message.Add(conv._load.message);

                    ConvertedList.Add(rM);
                }
            }

            // 終了ダイアログ
            var f2 = new Form2(ConvertedList);

            f2.ShowDialog();
            f2.Dispose();
        }

        private void SaveFile(string filename, string contents)
        {
            //'書き込むファイルが既に存在している場合は、上書きする
            using (var sw = new System.IO.StreamWriter(filename, false, System.Text.Encoding.UTF8))
            {
                // resultの内容を書き込む
                sw.Write(contents);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult dr = openFileDialog1.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                ConvertFRD(openFileDialog1.FileNames);
            }

        }

        //panel1のDragEnterイベントハンドラ
        private void panel1_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            //コントロール内にドラッグされたとき実行される
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                //ドラッグされたデータ形式を調べ、ファイルのときはコピーとする
                e.Effect = DragDropEffects.Copy;
            else
                //ファイル以外は受け付けない
                e.Effect = DragDropEffects.None;
        }

        //panel1のDragDropイベントハンドラ
        private void panel1_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            //コントロール内にドロップされたとき実行される
            //ドロップされたすべてのファイル名を取得する
            string[] fileName =
                (string[])e.Data.GetData(DataFormats.FileDrop, false);
            //ListBoxに追加する
            ConvertFRD(fileName);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
