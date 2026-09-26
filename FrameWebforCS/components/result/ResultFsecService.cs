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


        private Dictionary<string, Dictionary<string, Dictionary<string, clsFsec>>> _fsec;


        // コンストラクタを private にして、外部からの new を禁止する
        private ResultFsecService()
        {
            this.clear();
        }

        public void clear()
        {
            this._fsec = new Dictionary<string, Dictionary<string, Dictionary<string, clsFsec>>>();
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
            var fsecs = DataHelperModule.JsonToDict(
                jsonData,
                "result",
                static resultJson => DataHelperModule.JsonToDict(
                    resultJson,
                    "fsec",
                    static memberJson => DataHelperModule.JsonToDict<clsFsec>(memberJson)));

            if (fsecs != null) this._fsec = fsecs;
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
