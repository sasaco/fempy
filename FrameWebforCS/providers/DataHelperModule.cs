using FrameWebforCS.components.input;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.providers
{
    internal class DataHelperModule
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<DataHelperModule> _instance =
            new Lazy<DataHelperModule>(() => new DataHelperModule());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static DataHelperModule Instance => _instance.Value;


        // コンストラクタを private にして、外部からの new を禁止する
        private DataHelperModule()
        {
            // 初期化処理があればここに書く
            
        }

        // --------------------------------------------------
        // 保持したいデータやプロパティを以下に定義する
        // --------------------------------------------------
        public ToolStrip FloatingWindow { get; internal set; }

        internal void ChangeWindow(Type type)
        {
            if (type == typeof(InputElementsComponent))
            {
                //FloatingWindow.Items.Clear();
                // 1. 独自のUserControlのインスタンスを作成
                var myUserControl = new InputElementsComponent();

                // 2. ToolStripControlHostでラップする
                ToolStripControlHost hostControl = new ToolStripControlHost(myUserControl);

                // 3. ToolStripに追加する
                FloatingWindow.Items.Add(hostControl);
            }

            if (type == typeof(InputNodesComponent))
            {
                //FloatingWindow.Items.Clear();
                // 1. 独自のUserControlのインスタンスを作成
                var myUserControl = new InputNodesComponent();

                // 2. ToolStripControlHostでラップする
                ToolStripControlHost hostControl = new ToolStripControlHost(myUserControl);

                // 3. ToolStripに追加する
                FloatingWindow.Items.Add(hostControl);
            }




        }
    }
}
