using FarPoint.Win;
using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using THREE;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FrameWebforCS.components.input
{
    internal class clsNode
    {
        public float? X = null;
        public float? Y = null;
        public float? Z = null;
    }

    internal class InputNodesService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputNodesService> _instance =
            new Lazy<InputNodesService>(() => new InputNodesService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputNodesService Instance => _instance.Value;


        private Dictionary<string, clsNode> _node;

        // コンストラクタを private にして、外部からの new を禁止する
        private InputNodesService()
        {
            this.clear();
        }

        public void clear() {
            this._node = new Dictionary<string, clsNode>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setNodeJson(JsonElement jsonData)
        {
            var nodes = DataHelperModule.JsonToDict<clsNode>(jsonData, "member");
            if (nodes != null) this._node = nodes;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getNodeJson()  {

            var nodes = new Dictionary<string, object>();
            foreach (KeyValuePair<string, clsNode> n in this._node)
            {
                nodes.Add(n.Key, DataHelperModule.ClassToDictionary<clsNode>(n.Value));
            }
            return nodes;
        }

    }
}
