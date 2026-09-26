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
        public float? fxj = null;
        public float? fyj = null;
        public float? fzj = null;
        public float? mxj = null;
        public float? myj = null;
        public float? mzj = null;
        public float? L = null;
    }

    internal class ResultFsecService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<ResultFsecService> _instance =
            new Lazy<ResultFsecService>(() => new ResultFsecService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static ResultFsecService Instance => _instance.Value;


        private Dictionary<string, Dictionary<string, clsFsec>> _fsec;


        // コンストラクタを private にして、外部からの new を禁止する
        private ResultFsecService()
        {
            this.clear();
        }

        public void clear()
        {
            this._fsec = new Dictionary<string, Dictionary<string, clsFsec>>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setFsecJson(JsonElement jsonData)
        {
            var fsec = DataHelperModule.JsonToDict(
                jsonData,
                "fsec",
                static fsecJson => DataHelperModule.JsonToDict<clsFsec>(fsecJson));

            if (fsec != null) this._fsec = fsec;
        }


        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getFsecJson()
        {
            var fsecs = new Dictionary<string, object>();
            foreach (KeyValuePair<string, Dictionary<string, clsFsec>> f1 in this._fsec)
            {
                var fsec = new Dictionary<string, object>();
                foreach (KeyValuePair<string, clsFsec> f2 in f1.Value)
                {
                    clsFsec? Value = f2.Value;
                    fsec.Add(f2.Key, DataHelperModule.ClassToDictionary<clsFsec>(Value));
                }
                if (fsec.Count > 0)
                    fsecs.Add(f1.Key, fsec);
            }
            return fsecs;
        }

    }
}
