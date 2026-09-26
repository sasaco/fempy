using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsElement
    {
        public float? E = null;
        public float? G = null;
        public float? Xp = null;
        public float? A = null;
        public float? J = null;
        public float? Iy = null;
        public float? Iz = null;
        public string? n = null;
    }

    internal class InputElementsService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputElementsService> _instance =
            new Lazy<InputElementsService>(() => new InputElementsService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputElementsService Instance => _instance.Value;


        private Dictionary<string, Dictionary<string, clsElement>> _element;

        // コンストラクタを private にして、外部からの new を禁止する
        private InputElementsService()
        {
            this.clear();
        }

        public void clear()
        {
            this._element = new Dictionary<string, Dictionary<string, clsElement>>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setElementJson(JsonElement jsonData)
        {
            var elements = DataHelperModule.JsonToDict(
                jsonData,
                "element",
                static elementJson => DataHelperModule.JsonToDict<clsElement>(elementJson));

            if (elements != null) this._element = elements;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getElementJson()
        {
            var elements = new Dictionary<string, object>();
            foreach (KeyValuePair<string, Dictionary<string, clsElement>> e1 in this._element)
            {
                var element = new Dictionary<string, object>();
                foreach (KeyValuePair<string, clsElement> e2 in e1.Value)
                {
                    clsElement? Value = e2.Value;
                    element.Add(e2.Key, DataHelperModule.ClassToDictionary<clsElement>(Value));
                }
                if (element.Count > 0)
                    elements.Add(e1.Key, element);
            }
            return elements;
        }
    }
}
