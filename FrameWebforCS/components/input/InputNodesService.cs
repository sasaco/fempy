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
        public void setNodeJson(JsonElement jsonData)
        {
            if (!jsonData.TryGetProperty("node", out JsonElement nodeJson) ||
                nodeJson.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("node がJSONオブジェクトとして定義されていません。");
            }

            var nodes = new Dictionary<string, THREE.Vector3>();
            foreach (JsonProperty nodeProperty in nodeJson.EnumerateObject())
            {
                JsonElement coordinates = nodeProperty.Value;
                if (coordinates.ValueKind != JsonValueKind.Object ||
                    !coordinates.TryGetProperty("x", out JsonElement x) || !x.TryGetSingle(out float xValue) ||
                    !coordinates.TryGetProperty("y", out JsonElement y) || !y.TryGetSingle(out float yValue) ||
                    !coordinates.TryGetProperty("z", out JsonElement z) || !z.TryGetSingle(out float zValue) ||
                    !float.IsFinite(xValue) || !float.IsFinite(yValue) || !float.IsFinite(zValue))
                {
                    throw new JsonException($"node '{nodeProperty.Name}' の座標が不正です。");
                }

                if (!nodes.TryAdd(nodeProperty.Name, new THREE.Vector3(xValue, yValue, zValue)))
                {
                    throw new JsonException($"node '{nodeProperty.Name}' が重複しています。");
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
            foreach (KeyValuePair<string, THREE.Vector3> node in this._node)
            {
                nodes.Add(node.Key, new { x = node.Value.X, y = node.Value.Y, z = node.Value.Z });
            }
            return nodes;
        }

    }
}
