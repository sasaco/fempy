using FrameWebforCS.components.input;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FrameWebforCS.components.result
{
    internal class clsDisg
    {
        public float? dx = null;
        public float? dy = null;
        public float? dz = null;
        public float? rx= null;
        public float? ry = null;
        public float? rz = null;
    }


    internal class ResultDisgService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<ResultDisgService> _instance =
            new Lazy<ResultDisgService>(() => new ResultDisgService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static ResultDisgService Instance => _instance.Value;


        private Dictionary<string, clsDisg> _disg;


        // コンストラクタを private にして、外部からの new を禁止する
        private ResultDisgService()
        {
            this.clear();
        }

        public void clear()
        {
            this._disg = new Dictionary<string, clsDisg>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setDisgJson(JsonElement jsonData)
        {
            var disgs = DataHelperModule.JsonToDict<clsDisg>(jsonData, "disg");
            if (disgs != null) this._disg = disgs;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getDisgJson()
        {

            var disgs = new Dictionary<string, object>();
            foreach (KeyValuePair<string, clsDisg> d in this._disg)
            {
                disgs.Add(d.Key, DataHelperModule.ClassToDictionary<clsDisg>(d.Value));
            }
            return disgs;
        }

    }
}
