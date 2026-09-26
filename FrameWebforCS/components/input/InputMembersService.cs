using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsMember
    {
        public string? ni = null;
        public string? nj = null;
        public string? e = null;
        public float? cg = null;
    }

    internal class InputMembersService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputMembersService> _instance =
            new Lazy<InputMembersService>(() => new InputMembersService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputMembersService Instance => _instance.Value;


        private Dictionary<string, clsMember> _member;

        // コンストラクタを private にして、外部からの new を禁止する
        private InputMembersService()
        {
            this.clear();
        }

        public void clear()
        {
            this._member = new Dictionary<string, clsMember>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setMemberJson(JsonElement jsonData)
        {
            var members = DataHelperModule.JsonToDict<clsMember>(jsonData, "member");
            if (members != null) this._member = members;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getMemberJson()
        {
            var members = new Dictionary<string, object>();
            foreach (KeyValuePair<string, clsMember> m in this._member)
            {
                members.Add(m.Key, DataHelperModule.ClassToDictionary<clsMember>(m.Value));
            }
            return members;
        }
    }
}
