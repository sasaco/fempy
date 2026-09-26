using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsFixMember
    {
        public int row;
        public string? m = null;
        public float? tx = null;
        public float? ty = null;
        public float? tz = null;
        public float? tr = null;
    }

    internal class InputFixMemberService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputFixMemberService> _instance =
            new Lazy<InputFixMemberService>(() => new InputFixMemberService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputFixMemberService Instance => _instance.Value;


        private Dictionary<string, List<clsFixMember>> _fixMember;

        // コンストラクタを private にして、外部からの new を禁止する
        private InputFixMemberService()
        {
            this.clear();
        }

        public void clear()
        {
            this._fixMember = new Dictionary<string, List<clsFixMember>>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setFixMemberJson(JsonElement jsonData)
        {
            var fixMembers = DataHelperModule.JsonToDict(
                jsonData,
                "fix_member",
                static fixMembersJson => DataHelperModule.JsonToList<clsFixMember>(fixMembersJson));

            if (fixMembers != null) this._fixMember = fixMembers;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, object> getFixMemberJson()
        {
            var fixMembers = new Dictionary<string, object>();
            foreach (KeyValuePair<string, List<clsFixMember>> fixMember in this._fixMember)
            {
                var rows = new List<Dictionary<string, object?>>();
                foreach (clsFixMember value in fixMember.Value)
                {
                    rows.Add(DataHelperModule.ClassToDictionary(value));
                }
                fixMembers.Add(fixMember.Key, rows);
            }
            return fixMembers;
        }
    }
}
