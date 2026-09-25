using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.components.input
{
    internal class clsMember
    {
        string? ni = null;
        string? nj = null;
        string? e = null;
        double? cg = null;
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
        public void setNodeJson(Dictionary<string, object> jsonData)
        {
            if (jsonData.ContainsKey("member"))
                return;
            this._member = (Dictionary<string, clsMember>)jsonData["member"];
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        public Dictionary<string, clsMember> getNodeJson()
        {
            return this._member;
        }
    }
}
