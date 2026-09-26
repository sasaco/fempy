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
            if (!jsonData.TryGetProperty("node", out JsonElement nodeJson) ||
                nodeJson.ValueKind != JsonValueKind.Object)
            {
                // throw new JsonException("node がJSONオブジェクトとして定義されていません。");
                return;
            }

            var nodes = new Dictionary<string, clsNode>();
            foreach (JsonProperty nodeProperty in nodeJson.EnumerateObject())
            {
                JsonElement coordinates = nodeProperty.Value;
                if (coordinates.ValueKind != JsonValueKind.Object)
                    continue;

                var tmp = new clsNode();
                if (coordinates.TryGetProperty("x", out JsonElement x))
                    if (x.TryGetSingle(out float xValue))
                        tmp.X = xValue;
                if (coordinates.TryGetProperty("y", out JsonElement y))
                    if (y.TryGetSingle(out float yValue))
                        tmp.Y = yValue;
                if (coordinates.TryGetProperty("z", out JsonElement z))
                    if (z.TryGetSingle(out float zValue))
                        tmp.Z = zValue;

                // 何か1つでも値が入っていなければPASS
                var def = new clsNode();
                if (tmp.X == def.X && tmp.Y == def.Y && tmp.Z == def.Z)
                {
                    //throw new JsonException($"node '{nodeProperty.Name}' の座標が不正です。");
                    continue;
                }

                if (!nodes.TryAdd(nodeProperty.Name, tmp))
                {
                    // throw new JsonException($"node '{nodeProperty.Name}' が重複しています。");
                    continue;
                }

            }

            this._node = nodes;
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
                nodes.Add(n.Key, new { x = n.Value.X, y = n.Value.Y, z = n.Value.Z });
            }
            return nodes;
        }

    }
}
