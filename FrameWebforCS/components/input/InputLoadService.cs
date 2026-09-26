using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsLoadNode
    {
        public int row;
        public string? n = null;
        public float? tx = null;
        public float? ty = null;
        public float? tz = null;
        public float? rx = null;
        public float? ry = null;
        public float? rz = null;
    }

    internal class clsLoadMember
    {
        public int row;
        public string? m1 = null;
        public string? m2 = null;
        public string? direction = null;
        public string? mark = null;
        public string? L1 = null;
        public string? L2 = null;
        public float? P1 = null;
        public float? P2 = null;
    }

    internal class clsLoad
    {
        public int? fix_node = null;
        public int? fix_member = null;
        public int? element = null;
        public int? joint = null;
        public string? symbol = null;
        public float? LL_pitch = null;
        public string? name = null;
        public List<clsLoadNode>? load_node = null;
        public List<clsLoadMember>? load_member = null;
    }

    internal class InputLoadService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputLoadService> _instance =
            new Lazy<InputLoadService>(() => new InputLoadService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputLoadService Instance => _instance.Value;

        private Dictionary<string, clsLoad> _load;

        // コンストラクタを private にして、外部からの new を禁止する
        private InputLoadService()
        {
            this._load = new Dictionary<string, clsLoad>();
        }

        public void clear()
        {
            this._load = new Dictionary<string, clsLoad>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setLoadJson(JsonElement jsonData)
        {
            var load = DataHelperModule.JsonToDict(jsonData, "load", ReadLoad);
            if (load != null) this._load = load;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        public Dictionary<string, object> getLoadJson()
        {
            var load = new Dictionary<string, object>();
            foreach (KeyValuePair<string, clsLoad> item in this._load)
            {
                load.Add(item.Key, WriteLoad(item.Value));
            }
            return load;
        }

        private static clsLoad? ReadLoad(JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object) return null;

            clsLoad load = DataHelperModule.JsonToClass<clsLoad>(json) ?? new clsLoad();
            if (json.TryGetProperty(nameof(clsLoad.load_node), out JsonElement loadNode))
                load.load_node = DataHelperModule.JsonToList<clsLoadNode>(loadNode);
            if (json.TryGetProperty(nameof(clsLoad.load_member), out JsonElement loadMember))
                load.load_member = DataHelperModule.JsonToList<clsLoadMember>(loadMember);
            return load;
        }

        private static Dictionary<string, object?> WriteLoad(clsLoad load)
        {
            var result = DataHelperModule.ClassToDictionary(load);
            WriteRows(result, nameof(clsLoad.load_node), load.load_node);
            WriteRows(result, nameof(clsLoad.load_member), load.load_member);
            return result;
        }

        private static void WriteRows<T>(
            Dictionary<string, object?> target,
            string key,
            List<T>? rows)
        {
            if (rows == null)
            {
                target.Remove(key);
                return;
            }

            var values = new List<Dictionary<string, object?>>();
            foreach (T row in rows)
                values.Add(DataHelperModule.ClassToDictionary(row));
            target[key] = values;
        }
    }
}
