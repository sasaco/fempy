using FrameWebforCS.components.input;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FrameWebforCS.components.result
{
    internal class clsFsec
    {
        public float? fxi = null;
        public float? fyi = null;
        public float? fzi = null;
        public float? mxi = null;
        public float? myi = null;
        public float? mzi = null;
        public bool? dummyi = null;
        public float? fxj = null;
        public float? fyj = null;
        public float? fzj = null;
        public float? mxj = null;
        public float? myj = null;
        public float? mzj = null;
        public bool? dummyj = null;
        public float? L = null;
    }

    internal class ResultFsecService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<ResultFsecService> _instance =
            new Lazy<ResultFsecService>(() => new ResultFsecService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static ResultFsecService Instance => _instance.Value;

        public event EventHandler? Changed;


        private Dictionary<string, Dictionary<string, Dictionary<string, clsFsec>>> _fsec;


        // コンストラクタを private にして、外部からの new を禁止する
        private ResultFsecService()
        {
            this.clear();
        }

        public void clear()
        {
            this._fsec = new Dictionary<string, Dictionary<string, Dictionary<string, clsFsec>>>();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        public Dictionary<string, Dictionary<string, Dictionary<string, clsFsec>>> getFsec()
        {
            return this._fsec;
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setFsecJson(JsonElement jsonData)
        {
            if (!jsonData.TryGetProperty("result", out JsonElement results)) return;
            if (results.ValueKind != JsonValueKind.Object)
                throw new JsonException("result must be a JSON object.");

            var candidate = new Dictionary<string, Dictionary<string, Dictionary<string, clsFsec>>>();
            foreach (JsonProperty result in results.EnumerateObject())
            {
                if (result.Value.ValueKind != JsonValueKind.Object)
                    throw new JsonException($"result '{result.Name}' must be a JSON object.");
                if (!result.Value.TryGetProperty("fsec", out JsonElement members)) continue;
                if (members.ValueKind != JsonValueKind.Object)
                    throw new JsonException($"result '{result.Name}' fsec must be a JSON object.");
                var parsedMembers = new Dictionary<string, Dictionary<string, clsFsec>>();
                foreach (JsonProperty member in members.EnumerateObject())
                {
                    if (member.Value.ValueKind != JsonValueKind.Object)
                        throw new JsonException($"section force member '{member.Name}' must be a JSON object.");
                    var parsedPoints = new Dictionary<string, clsFsec>();
                    foreach (JsonProperty point in member.Value.EnumerateObject())
                    {
                        if (point.Value.ValueKind != JsonValueKind.Object)
                            throw new JsonException($"section force point '{point.Name}' must be a JSON object.");
                        var parsed = new clsFsec
                        {
                            fxi = ReadComponent(point.Value, "fxi"),
                            fyi = ReadComponent(point.Value, "fyi"),
                            fzi = ReadComponent(point.Value, "fzi"),
                            mxi = ReadComponent(point.Value, "mxi"),
                            myi = ReadComponent(point.Value, "myi"),
                            mzi = ReadComponent(point.Value, "mzi"),
                            fxj = ReadComponent(point.Value, "fxj"),
                            fyj = ReadComponent(point.Value, "fyj"),
                            fzj = ReadComponent(point.Value, "fzj"),
                            mxj = ReadComponent(point.Value, "mxj"),
                            myj = ReadComponent(point.Value, "myj"),
                            mzj = ReadComponent(point.Value, "mzj"),
                            L = ReadComponent(point.Value, "L"),
                            dummyi = ReadFlag(point.Value, "dummyi"),
                            dummyj = ReadFlag(point.Value, "dummyj")
                        };
                        if (!parsedPoints.TryAdd(point.Name, parsed))
                            throw new JsonException($"Duplicate section force point '{point.Name}'.");
                    }
                    if (!parsedMembers.TryAdd(member.Name, parsedPoints))
                        throw new JsonException($"Duplicate section force member '{member.Name}'.");
                }
                if (!candidate.TryAdd(result.Name, parsedMembers))
                    throw new JsonException($"Duplicate result case '{result.Name}'.");
            }
            _fsec = candidate;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static float? ReadComponent(JsonElement source, string name)
        {
            if (!source.TryGetProperty(name, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind != JsonValueKind.Number ||
                !value.TryGetSingle(out float parsed) || !float.IsFinite(parsed))
                throw new JsonException($"Section force component '{name}' must be finite or null.");
            return parsed;
        }

        private static bool? ReadFlag(JsonElement source, string name)
        {
            if (!source.TryGetProperty(name, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new JsonException($"Section force flag '{name}' must be boolean or null.");
            return value.GetBoolean();
        }


        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getFsecJson()
        {
            var fsecs = new Dictionary<string, object>();
            foreach (KeyValuePair<string, Dictionary<string, Dictionary<string, clsFsec>>> result in this._fsec)
            {
                var members = new Dictionary<string, object>();
                foreach (KeyValuePair<string, Dictionary<string, clsFsec>> member in result.Value)
                {
                    var points = new Dictionary<string, object>();
                    foreach (KeyValuePair<string, clsFsec> point in member.Value)
                    {
                        points.Add(point.Key, DataHelperModule.ClassToDictionary(point.Value));
                    }
                    members.Add(member.Key, points);
                }
                fsecs.Add(result.Key, new Dictionary<string, object> { ["fsec"] = members });
            }
            return fsecs;
        }

    }
}
