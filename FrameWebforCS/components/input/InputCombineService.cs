using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsCombine<T> : INotifyPropertyChanged where T : struct
    {
        private string? _name;
        public event PropertyChangedEventHandler? PropertyChanged;
        public int row;
        public string Id { get; set; } = "";
        public string? name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(name)));
            }
        }
        public Dictionary<int, T> Coefficients { get; } = new();

        public bool IsEmpty => string.IsNullOrWhiteSpace(name) && Coefficients.Count == 0;

        private T? GetCoefficient(int index) =>
            Coefficients.TryGetValue(index, out T value) ? value : null;

        private void SetCoefficient(int index, T? value, string propertyName)
        {
            if (value.HasValue)
            {
                if (typeof(T) == typeof(float) && !float.IsFinite((float)(object)value.Value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                Coefficients[index] = value.Value;
            }
            else
                Coefficients.Remove(index);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public T? C1 { get => GetCoefficient(1); set => SetCoefficient(1, value, nameof(C1)); }
        public T? C2 { get => GetCoefficient(2); set => SetCoefficient(2, value, nameof(C2)); }
        public T? C3 { get => GetCoefficient(3); set => SetCoefficient(3, value, nameof(C3)); }
        public T? C4 { get => GetCoefficient(4); set => SetCoefficient(4, value, nameof(C4)); }
        public T? C5 { get => GetCoefficient(5); set => SetCoefficient(5, value, nameof(C5)); }
        public T? C6 { get => GetCoefficient(6); set => SetCoefficient(6, value, nameof(C6)); }
        public T? C7 { get => GetCoefficient(7); set => SetCoefficient(7, value, nameof(C7)); }
        public T? C8 { get => GetCoefficient(8); set => SetCoefficient(8, value, nameof(C8)); }
        public T? C9 { get => GetCoefficient(9); set => SetCoefficient(9, value, nameof(C9)); }
        public T? C10 { get => GetCoefficient(10); set => SetCoefficient(10, value, nameof(C10)); }
        public T? C11 { get => GetCoefficient(11); set => SetCoefficient(11, value, nameof(C11)); }
        public T? C12 { get => GetCoefficient(12); set => SetCoefficient(12, value, nameof(C12)); }
        public T? C13 { get => GetCoefficient(13); set => SetCoefficient(13, value, nameof(C13)); }
        public T? C14 { get => GetCoefficient(14); set => SetCoefficient(14, value, nameof(C14)); }
        public T? C15 { get => GetCoefficient(15); set => SetCoefficient(15, value, nameof(C15)); }
        public T? C16 { get => GetCoefficient(16); set => SetCoefficient(16, value, nameof(C16)); }
        public T? C17 { get => GetCoefficient(17); set => SetCoefficient(17, value, nameof(C17)); }
        public T? C18 { get => GetCoefficient(18); set => SetCoefficient(18, value, nameof(C18)); }
        public T? C19 { get => GetCoefficient(19); set => SetCoefficient(19, value, nameof(C19)); }
        public T? C20 { get => GetCoefficient(20); set => SetCoefficient(20, value, nameof(C20)); }
        public T? C21 { get => GetCoefficient(21); set => SetCoefficient(21, value, nameof(C21)); }
        public T? C22 { get => GetCoefficient(22); set => SetCoefficient(22, value, nameof(C22)); }
        public T? C23 { get => GetCoefficient(23); set => SetCoefficient(23, value, nameof(C23)); }
        public T? C24 { get => GetCoefficient(24); set => SetCoefficient(24, value, nameof(C24)); }
        public T? C25 { get => GetCoefficient(25); set => SetCoefficient(25, value, nameof(C25)); }
        public T? C26 { get => GetCoefficient(26); set => SetCoefficient(26, value, nameof(C26)); }
        public T? C27 { get => GetCoefficient(27); set => SetCoefficient(27, value, nameof(C27)); }
        public T? C28 { get => GetCoefficient(28); set => SetCoefficient(28, value, nameof(C28)); }
        public T? C29 { get => GetCoefficient(29); set => SetCoefficient(29, value, nameof(C29)); }
        public T? C30 { get => GetCoefficient(30); set => SetCoefficient(30, value, nameof(C30)); }
        public T? C31 { get => GetCoefficient(31); set => SetCoefficient(31, value, nameof(C31)); }
        public T? C32 { get => GetCoefficient(32); set => SetCoefficient(32, value, nameof(C32)); }
        public T? C33 { get => GetCoefficient(33); set => SetCoefficient(33, value, nameof(C33)); }
        public T? C34 { get => GetCoefficient(34); set => SetCoefficient(34, value, nameof(C34)); }
        public T? C35 { get => GetCoefficient(35); set => SetCoefficient(35, value, nameof(C35)); }
        public T? C36 { get => GetCoefficient(36); set => SetCoefficient(36, value, nameof(C36)); }
        public T? C37 { get => GetCoefficient(37); set => SetCoefficient(37, value, nameof(C37)); }
        public T? C38 { get => GetCoefficient(38); set => SetCoefficient(38, value, nameof(C38)); }
        public T? C39 { get => GetCoefficient(39); set => SetCoefficient(39, value, nameof(C39)); }
        public T? C40 { get => GetCoefficient(40); set => SetCoefficient(40, value, nameof(C40)); }
        public T? C41 { get => GetCoefficient(41); set => SetCoefficient(41, value, nameof(C41)); }
        public T? C42 { get => GetCoefficient(42); set => SetCoefficient(42, value, nameof(C42)); }
        public T? C43 { get => GetCoefficient(43); set => SetCoefficient(43, value, nameof(C43)); }
        public T? C44 { get => GetCoefficient(44); set => SetCoefficient(44, value, nameof(C44)); }
        public T? C45 { get => GetCoefficient(45); set => SetCoefficient(45, value, nameof(C45)); }
        public T? C46 { get => GetCoefficient(46); set => SetCoefficient(46, value, nameof(C46)); }
        public T? C47 { get => GetCoefficient(47); set => SetCoefficient(47, value, nameof(C47)); }
        public T? C48 { get => GetCoefficient(48); set => SetCoefficient(48, value, nameof(C48)); }
        public T? C49 { get => GetCoefficient(49); set => SetCoefficient(49, value, nameof(C49)); }
        public T? C50 { get => GetCoefficient(50); set => SetCoefficient(50, value, nameof(C50)); }
    }


    internal class InputCombineService
    {
        private const int MaxNodeId = 100_000;
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputCombineService> _instance =
            new Lazy<InputCombineService>(() => new InputCombineService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputCombineService Instance => _instance.Value;

        private Dictionary<string, clsCombine<float>> _combine = new();
        private Dictionary<string, clsCombine<int>> _define = new();
        private Dictionary<string, clsCombine<int>> _pickup = new();

        public BindingList<clsCombine<int>> DefineRows { get; } = new();
        public BindingList<clsCombine<float>> CombineRows { get; } = new();
        public BindingList<clsCombine<int>> PickupRows { get; } = new();

        // コンストラクタを private にして、外部からの new を禁止する
        private InputCombineService()
        {
            InitializeRows(DefineRows, _define);
            InitializeRows(CombineRows, _combine);
            InitializeRows(PickupRows, _pickup);
            DefineRows.ListChanged += (_, e) => UpdateRow(DefineRows, _define, e);
            CombineRows.ListChanged += (_, e) => UpdateRow(CombineRows, _combine, e);
            PickupRows.ListChanged += (_, e) => UpdateRow(PickupRows, _pickup, e);
        }

        public void clear()
        {
            ReplaceRows(CombineRows, _combine, new Dictionary<string, clsCombine<float>>());
            ReplaceRows(DefineRows, _define, new Dictionary<string, clsCombine<int>>());
            ReplaceRows(PickupRows, _pickup, new Dictionary<string, clsCombine<int>>());
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
            ReplaceRows(CombineRows, _combine, combine);
            ReplaceRows(DefineRows, _define, define);
            ReplaceRows(PickupRows, _pickup, pickup);
        }

        private static Dictionary<string, clsCombine<T>> JsonToDict<T>(
            JsonElement jsonData,
            string key,
            Func<JsonElement, T> readCoefficient) where T : struct
        {
            if (!jsonData.TryGetProperty(key, out JsonElement combineJson))
                return new Dictionary<string, clsCombine<T>>();
            if (combineJson.ValueKind != JsonValueKind.Object)
                throw new JsonException($"{key} がJSONオブジェクトとして定義されていません。");

            var combine = new Dictionary<string, clsCombine<T>>();
            var occupiedRows = new HashSet<int>();
            foreach (JsonProperty caseJson in combineJson.EnumerateObject())
            {
                if (caseJson.Value.ValueKind != JsonValueKind.Object)
                    throw new JsonException($"{key} '{caseJson.Name}' の値が不正です。");

                var item = new clsCombine<T> { Id = caseJson.Name };
                bool hasRow = false;
                foreach (JsonProperty property in caseJson.Value.EnumerateObject())
                {
                    if (property.Name == "row")
                    {
                        if (property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetInt32(out item.row) ||
                            item.row <= 0 || item.row > MaxNodeId)
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

                if (!hasRow || !occupiedRows.Add(item.row) ||
                    !combine.TryAdd(caseJson.Name, item))
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

        private static void InitializeRows<T>(BindingList<clsCombine<T>> rows,
            Dictionary<string, clsCombine<T>> source) where T : struct
        {
            rows.AllowNew = false;
            rows.AllowRemove = false;
            rows.RaiseListChangedEvents = false;
            for (int index = 0; index < MaxNodeId; index++)
                rows.Add(new clsCombine<T>
                {
                    row = index + 1,
                    Id = (index + 1).ToString(CultureInfo.InvariantCulture)
                });
            rows.RaiseListChangedEvents = true;
        }

        private static void UpdateRow<T>(BindingList<clsCombine<T>> rows,
            Dictionary<string, clsCombine<T>> source, ListChangedEventArgs change)
            where T : struct
        {
            if (change.ListChangedType != ListChangedType.ItemChanged || change.NewIndex < 0)
                return;
            clsCombine<T> row = rows[change.NewIndex];
            if (row.IsEmpty)
                source.Remove(row.Id);
            else
                source[row.Id] = row;
        }

        private static void ReplaceRows<T>(BindingList<clsCombine<T>> rows,
            Dictionary<string, clsCombine<T>> destination,
            Dictionary<string, clsCombine<T>> next) where T : struct
        {
            rows.RaiseListChangedEvents = false;
            try
            {
                foreach (clsCombine<T> previous in destination.Values)
                    rows[previous.row - 1] = new clsCombine<T>
                    {
                        row = previous.row,
                        Id = previous.row.ToString(CultureInfo.InvariantCulture)
                    };
                destination.Clear();
                foreach (var (id, item) in next)
                {
                    rows[item.row - 1] = item;
                    if (!item.IsEmpty)
                        destination.Add(id, item);
                }
            }
            finally
            {
                rows.RaiseListChangedEvents = true;
                rows.ResetBindings();
            }
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
            Dictionary<string, clsCombine<T>> source) where T : struct
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
