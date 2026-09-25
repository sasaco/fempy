using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.components.input
{
    internal class clsFixNode
    {
        int row;
        string? n = null;
        float? tx = null;
        float? ty = null;
        float? tz = null;
        float? rx = null;
        float? ry = null;
        float? rz = null;
    }

    internal class InputFixNodeService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputFixNodeService> _instance =
            new Lazy<InputFixNodeService>(() => new InputFixNodeService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputFixNodeService Instance => _instance.Value;


        private Dictionary<string, List<clsFixNode>> _fix_node;

        // コンストラクタを private にして、外部からの new を禁止する
        private InputFixNodeService()
        {
            this.clear();
        }

        public void clear()
        {
            this._fix_node = new Dictionary<string, List<clsFixNode>>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setNodeJson(Dictionary<string, object> jsonData)
        {
            if (jsonData.ContainsKey("fix_node"))
                return;
            this._fix_node = (Dictionary<string, List<clsFixNode>>)jsonData["fix_node"];
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, List<clsFixNode>> getNodeJson()
        {
            return this._fix_node;
        }
    }
}
