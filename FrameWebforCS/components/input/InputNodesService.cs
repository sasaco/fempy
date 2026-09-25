using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Text;
using THREE;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FrameWebforCS.components.input
{
    public class clsNode
    {
        public string id;
        public double x;
        public double y;
        public double z;
    }
    internal class InputNodesService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputNodesService> _instance =
            new Lazy<InputNodesService>(() => new InputNodesService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputNodesService Instance => _instance.Value;


        private Dictionary<string, THREE.Vector3> _node;

        // コンストラクタを private にして、外部からの new を禁止する
        private InputNodesService()
        {
            this.clear();
        }

        public void clear() {
            this._node = new Dictionary<string, THREE.Vector3>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setNodeJson(Dictionary<string, object> jsonData)
        {
            if(jsonData.ContainsKey("node"))
              return;
            this._node = (Dictionary<string, THREE.Vector3>)jsonData["node"];
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, THREE.Vector3> getNodeJson()  {
            return this._node;
        }

    }
}
