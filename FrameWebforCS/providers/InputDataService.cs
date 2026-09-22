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
            dimension = 3;
        }

        // --------------------------------------------------
        // 保持したいデータやプロパティを以下に定義する
        // --------------------------------------------------
        // ３次元解析=3, ２次元解析=2
        public int dimension { get; set; }

    }
}
