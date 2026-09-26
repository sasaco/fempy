using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FrameWebforCS.components.result
{
    internal class clsReac
    {
        public float? dx = null;
        public float? dy = null;
        public float? dz = null;
        public float? rx = null;
        public float? ry = null;
        public float? rz = null;
    }

    internal class ResultReacService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<ResultReacService> _instance =
            new Lazy<ResultReacService>(() => new ResultReacService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static ResultReacService Instance => _instance.Value;


        private Dictionary<string, clsReac> _reac;


        // コンストラクタを private にして、外部からの new を禁止する
        private ResultReacService()
        {
            this.clear();
        }

        public void clear()
        {
            this._reac = new Dictionary<string, clsReac>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setReacJson(JsonElement jsonData)
        {
            var reacs = DataHelperModule.JsonToDict<clsReac>(jsonData, "reac");
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
            foreach (KeyValuePair<string, clsReac> d in this._reac)
            {
                reacs.Add(d.Key, DataHelperModule.ClassToDictionary<clsReac>(d.Value));
            }
            return reacs;
        }

    }
}
