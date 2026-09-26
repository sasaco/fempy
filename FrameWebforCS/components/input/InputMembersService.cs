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
            if (!jsonData.TryGetProperty("member", out JsonElement memberJson) ||
                memberJson.ValueKind != JsonValueKind.Object)
            {
                // throw new JsonException("member がJSONオブジェクトとして定義されていません。");
                return;
            }

            var members = new Dictionary<string, clsMember>();
            foreach (JsonProperty memberProperty in memberJson.EnumerateObject())
            {
                JsonElement coordinates = memberProperty.Value;
                if (coordinates.ValueKind != JsonValueKind.Object)
                    continue;

                var tmp = new clsMember();
                if(coordinates.TryGetProperty("ni", out JsonElement ni))
                    tmp.ni = ni.ToString();
                if (coordinates.TryGetProperty("nj", out JsonElement nj))
                    tmp.nj = nj.ToString();
                if (coordinates.TryGetProperty("e", out JsonElement e))
                    tmp.e = e.ToString();
                if (coordinates.TryGetProperty("cg", out JsonElement cg))
                    if (cg.TryGetSingle(out float cgValue))
                        tmp.cg = cgValue;

                // 何か1つでも値が入っていなければPASS
                var def = new clsMember();
                if (tmp.ni == def.ni && tmp.nj == def.nj && tmp.e == def.e && tmp.cg == def.cg)
                {
                    //throw new JsonException($"member '{memberProperty.Name}' の値が不正です。");
                    continue;
                }

                if (!members.TryAdd(memberProperty.Name, tmp))
                {
                    // throw new JsonException($"member '{memberProperty.Name}' が重複しています。");
                    continue;
                }
            }

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
                members.Add(m.Key, new { 
                    ni = m.Value.ni, 
                    nj = m.Value.nj, 
                    e = m.Value.e, 
                    cg = m.Value.cg 
                });
            }
            return members;
        }
    }
}
