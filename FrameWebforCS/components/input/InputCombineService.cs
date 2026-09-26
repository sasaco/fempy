using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsCombine<T>
    {
        public int row;
        public string? name = null;
        public Dictionary<int, T> Coefficients { get; } = new();
    }


    internal class InputCombineService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputCombineService> _instance =
            new Lazy<InputCombineService>(() => new InputCombineService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputCombineService Instance => _instance.Value;

        private Dictionary<string, clsCombine<float>> _combine = new();
        private Dictionary<string, clsCombine<int>> _define = new();
        private Dictionary<string, clsCombine<int>> _pickup = new();

        // コンストラクタを private にして、外部からの new を禁止する
        private InputCombineService()
        {
        }

        public void clear()
        {
            this._combine = new Dictionary<string, clsCombine<float>>();
            this._define = new Dictionary<string, clsCombine<int>>();
            this._pickup = new Dictionary<string, clsCombine<int>>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setCombineJson(JsonElement jsonData)
        {
            Dictionary<string, clsCombine<float>> combine =
                JsonToDict(jsonData, "combine", ReadSingle);
            Dictionary<string, clsCombine<int>> define =
                JsonToDict(jsonData, "define", ReadInt32);
            Dictionary<string, clsCombine<int>> pickup =
                JsonToDict(jsonData, "pickup", ReadInt32);

            // 3 種類をすべて検証してから置き換え、読込失敗時は現在値を保持する。
            this._combine = combine;
            this._define = define;
            this._pickup = pickup;
        }

        private static Dictionary<string, clsCombine<T>> JsonToDict<T>(
            JsonElement jsonData,
            string key,
            Func<JsonElement, T> readCoefficient)
        {
            if (!jsonData.TryGetProperty(key, out JsonElement combineJson))
                return new Dictionary<string, clsCombine<T>>();
            if (combineJson.ValueKind != JsonValueKind.Object)
                throw new JsonException($"{key} がJSONオブジェクトとして定義されていません。");

            var combine = new Dictionary<string, clsCombine<T>>();
            foreach (JsonProperty caseJson in combineJson.EnumerateObject())
            {
                if (caseJson.Value.ValueKind != JsonValueKind.Object)
                    throw new JsonException($"{key} '{caseJson.Name}' の値が不正です。");

                var item = new clsCombine<T>();
                bool hasRow = false;
                foreach (JsonProperty property in caseJson.Value.EnumerateObject())
                {
                    if (property.Name == "row")
                    {
                        if (property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetInt32(out item.row) || item.row <= 0)
                            throw new JsonException($"{key} '{caseJson.Name}' の row が不正です。");
                        hasRow = true;
                    }
                    else if (property.Name == "name")
                    {
                        if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                            throw new JsonException($"{key} '{caseJson.Name}' の name が不正です。");
                        item.name = property.Value.ValueKind == JsonValueKind.Null
                            ? null : property.Value.GetString();
                    }
                    else if (property.Name.StartsWith('C') &&
                             int.TryParse(property.Name.AsSpan(1), NumberStyles.None,
                                 CultureInfo.InvariantCulture, out int coefficientId) && coefficientId > 0)
                    {
                        if (property.Value.ValueKind == JsonValueKind.Null)
                            continue;

                        T coefficient;
                        try
                        {
                            coefficient = readCoefficient(property.Value);
                        }
                        catch (JsonException exception)
                        {
                            throw new JsonException(
                                $"{key} '{caseJson.Name}' の {property.Name} が不正です。",
                                exception);
                        }

                        if (!item.Coefficients.TryAdd(coefficientId, coefficient))
                            throw new JsonException($"{key} '{caseJson.Name}' の {property.Name} が重複しています。");
                    }
                    else
                    {
                        throw new JsonException($"{key} '{caseJson.Name}' に未知の項目 {property.Name} があります。");
                    }
                }

                if (!hasRow || !combine.TryAdd(caseJson.Name, item))
                    throw new JsonException($"{key} '{caseJson.Name}' の行番号またはIDが不正です。");
            }

            return combine;
        }

        private static float ReadSingle(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Number ||
                !value.TryGetSingle(out float result) ||
                !float.IsFinite(result))
            {
                throw new JsonException("有限な数値ではありません。");
            }

            return result;
        }

        private static int ReadInt32(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Number ||
                !value.TryGetInt32(out int result))
            {
                throw new JsonException("整数ではありません。");
            }

            return result;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getCombineJson()
        {
            return ClassToDictionary(this._combine);
        }
        public Dictionary<string, object> getDefineJson()
        {
            return ClassToDictionary(this._define);
        }
        public Dictionary<string, object> getPickupJson()
        {
            return ClassToDictionary(this._pickup);
        }

        private static Dictionary<string, object> ClassToDictionary<T>(
            Dictionary<string, clsCombine<T>> source)
        {
            var combine = new Dictionary<string, object>();
            foreach (KeyValuePair<string, clsCombine<T>> n in source)
            {
                var row = new Dictionary<string, object?>
                {
                    ["row"] = n.Value.row
                };

                if (n.Value.name != null)
                    row["name"] = n.Value.name;

                foreach (KeyValuePair<int, T> coefficient in n.Value.Coefficients)
                    row.Add("C" + coefficient.Key.ToString(CultureInfo.InvariantCulture), coefficient.Value);
                combine.Add(n.Key, row);
            }
            return combine;
        }
    }
}
