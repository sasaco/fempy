using FrameWebforCS.components.input;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.providers
{
    public class InputDataService
    {
        // Lazy<T> を使ってスレッドセーフかつ遅延評価のシングルトンを実装
        private static readonly Lazy<InputDataService> _instance =
            new Lazy<InputDataService>(() => new InputDataService());

        // 外部からはこのプロパティを通じてのみインスタンスにアクセスできる
        public static InputDataService Instance => _instance.Value;


        // コンストラクタを private にして、外部からの new を禁止する
        private InputDataService()
        {
            // 初期化処理があればここに書く
            CurrentComponent = null;


            dimension = 3;

        }

        // --------------------------------------------------
        // 保持したいデータやプロパティを以下に定義する
        // --------------------------------------------------

        //現在編集中のコンポーネント
        public UserControl? CurrentComponent { get; set; }

        // ３次元解析=3, ２次元解析=2
        public int dimension { get; set; }



        /// <summary>
        /// Dfineケースのケース番号を
        /// </summary>
        /// <returns></returns>
        internal List<string> GetDifineCase()
        {
            var result = new List<string>();

            for(int i = 0; i < 10; i++)
            {
                result.Add("D" + (i + 1).ToString());
            }

            return result;
        }

        /// <summary>
        /// Componentに表示するデータを返す
        /// </summary>
        /// <returns></returns>
        internal Dictionary<string, object> getDisg()
        {
            var result = new Dictionary<string, object>();

            for(int i = 0; i < 20; i++)
            {
                result.Add("case " + i.ToString(), null);
            }
            return result;
        }
        internal Dictionary<string, object> getCombineDisg()
        {
            return getDisg();
        }
        internal Dictionary<string, object> getPickupDisg()
        {
            return getDisg();
        }
        internal Dictionary<string, object> getFsec()
        {
            return getDisg();
        }
        internal Dictionary<string, object> getCombineFsec()
        {
            return getFsec();
        }
        internal Dictionary<string, object> getPickupFsec()
        {
            return getFsec();
        }
        internal Dictionary<string, object> getReac()
        {
            return getDisg();
        }
        internal Dictionary<string, object> getCombineReac()
        {
            return getReac();
        }
        internal Dictionary<string, object> getPickupReac()
        {
            return getReac();
        }

        internal void JsonDataOpen(System.Text.Json.JsonElement rootElement)
        {
            InputNodesService.Instance.setNodeJson(rootElement);
            InputMembersService.Instance.setMemberJson(rootElement);
        }

        internal Dictionary<string, object>? GetSaveJson()
        {
            return new Dictionary<string, object> {
                ["node"] = InputNodesService.Instance.getNodeJson()
                ["member"] = InputMembersService.Instance.getMemberJson()
            };
        }
    }
}
