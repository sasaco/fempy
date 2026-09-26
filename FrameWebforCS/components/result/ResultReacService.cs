using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FrameWebforCS.components.result
{
    internal class clsReac
    {
        public float? tx = null;
        public float? ty = null;
        public float? tz = null;
        public float? mx = null;
        public float? my = null;
        public float? mz = null;
    }

    internal class ResultReacService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<ResultReacService> _instance =
            new Lazy<ResultReacService>(() => new ResultReacService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static ResultReacService Instance => _instance.Value;


        private Dictionary<string, Dictionary<string, clsReac>> _reac;


        // コンストラクタを private にして、外部からの new を禁止する
        private ResultReacService()
        {
            this.clear();
        }

        public void clear()
        {
            this._reac = new Dictionary<string, Dictionary<string, clsReac>>();
        }
        public Dictionary<string, Dictionary<string, clsReac>> getReac()
        {
            return this._reac;
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setReacJson(JsonElement jsonData)
        {
            var reacs = DataHelperModule.JsonToDict(
                jsonData,
                "result",
                static resultJson => DataHelperModule.JsonToDict<clsReac>(resultJson, "reac"));

            if (reacs != null) this._reac = reacs;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getReacJson()
        {

            var reacs = new Dictionary<string, object>();
            foreach (KeyValuePair<string, Dictionary<string, clsReac>> result in this._reac)
            {
                var nodes = new Dictionary<string, object>();
                foreach (KeyValuePair<string, clsReac> node in result.Value)
                {
                    nodes.Add(node.Key, DataHelperModule.ClassToDictionary(node.Value));
                }
                reacs.Add(result.Key, new Dictionary<string, object> { ["reac"] = nodes });
            }
            return reacs;
        }

    }
}
