/*
 @licstart  The following is the entire license notice for the JavaScript code in this file.

 The MIT License (MIT)

 Copyright (C) 1997-2020 by Dimitri van Heesch

 Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 and associated documentation files (the "Software"), to deal in the Software without restriction,
 including without limitation the rights to use, copy, modify, merge, publish, distribute,
 sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all copies or
 substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

 @licend  The above is the entire license notice for the JavaScript code in this file
*/
var NAVTREE =
[
  [ "FrameWeb", "index.html", [
    [ "FrameWeb3 API ドキュメント", "index.html", "index" ],
    [ "データ構造", "md_docs_2wiki_2data-structures.html", [
      [ "データ構造チートシート", "md_docs_2wiki_2data-structures.html#autotoc_md1", [
        [ "節点定義", "md_docs_2wiki_2data-structures.html#autotoc_md2", null ],
        [ "材料・断面特性（element）", "md_docs_2wiki_2data-structures.html#autotoc_md3", [
          [ "A, Iy, Iz, J の要素ごとの扱い", "md_docs_2wiki_2data-structures.html#autotoc_md4", null ]
        ] ],
        [ "非線形材料定義（JR総研剛性低減RC型）", "md_docs_2wiki_2data-structures.html#autotoc_md5", [
          [ "非線形パラメータ", "md_docs_2wiki_2data-structures.html#autotoc_md6", null ],
          [ "hysteresis_dofs の選択肢", "md_docs_2wiki_2data-structures.html#autotoc_md7", null ],
          [ "非対称スケルトンカーブ", "md_docs_2wiki_2data-structures.html#autotoc_md8", null ]
        ] ],
        [ "部材定義（梁要素：bar）", "md_docs_2wiki_2data-structures.html#autotoc_md9", null ],
        [ "シェル要素定義（shell）", "md_docs_2wiki_2data-structures.html#autotoc_md10", null ],
        [ "ソリッド要素定義（tetra, wedge, hexa）", "md_docs_2wiki_2data-structures.html#autotoc_md11", null ],
        [ "支点条件", "md_docs_2wiki_2data-structures.html#autotoc_md12", null ],
        [ "荷重定義", "md_docs_2wiki_2data-structures.html#autotoc_md13", null ]
      ] ],
      [ "入力データ構造", "md_docs_2wiki_2data-structures.html#autotoc_md14", [
        [ "ルートオブジェクト", "md_docs_2wiki_2data-structures.html#autotoc_md15", null ],
        [ "節点定義", "md_docs_2wiki_2data-structures.html#autotoc_md16", null ],
        [ "部材定義", "md_docs_2wiki_2data-structures.html#autotoc_md17", null ],
        [ "シェル定義", "md_docs_2wiki_2data-structures.html#autotoc_md18", null ],
        [ "ソリッド定義", "md_docs_2wiki_2data-structures.html#autotoc_md19", null ],
        [ "要素特性", "md_docs_2wiki_2data-structures.html#autotoc_md20", null ],
        [ "厚さ定義", "md_docs_2wiki_2data-structures.html#autotoc_md21", null ],
        [ "支点条件", "md_docs_2wiki_2data-structures.html#autotoc_md22", null ],
        [ "バネ支点", "md_docs_2wiki_2data-structures.html#autotoc_md23", null ],
        [ "材端条件", "md_docs_2wiki_2data-structures.html#autotoc_md24", null ],
        [ "荷重ケース", "md_docs_2wiki_2data-structures.html#autotoc_md25", [
          [ "解析パラメータ", "md_docs_2wiki_2data-structures.html#autotoc_md26", null ],
          [ "節点荷重", "md_docs_2wiki_2data-structures.html#autotoc_md27", null ]
        ] ]
      ] ],
      [ "出力データ構造", "md_docs_2wiki_2data-structures.html#autotoc_md28", [
        [ "節点変位", "md_docs_2wiki_2data-structures.html#autotoc_md29", null ],
        [ "支点反力", "md_docs_2wiki_2data-structures.html#autotoc_md30", null ],
        [ "部材断面力（梁要素）", "md_docs_2wiki_2data-structures.html#autotoc_md31", null ],
        [ "シェル要素応力・ひずみ", "md_docs_2wiki_2data-structures.html#autotoc_md32", null ],
        [ "ソリッド要素応力・ひずみ", "md_docs_2wiki_2data-structures.html#autotoc_md33", null ]
      ] ]
    ] ],
    [ "APIエンドポイント", "md_docs_2wiki_2endpoints.html", [
      [ "ベースエンドポイント", "md_docs_2wiki_2endpoints.html#autotoc_md35", null ],
      [ "POST / - 構造解析", "md_docs_2wiki_2endpoints.html#autotoc_md36", [
        [ "リクエスト", "md_docs_2wiki_2endpoints.html#autotoc_md37", [
          [ "ヘッダー", "md_docs_2wiki_2endpoints.html#autotoc_md38", null ],
          [ "リクエストボディ", "md_docs_2wiki_2endpoints.html#autotoc_md39", null ]
        ] ],
        [ "レスポンス", "md_docs_2wiki_2endpoints.html#autotoc_md40", [
          [ "成功レスポンス (200 OK)", "md_docs_2wiki_2endpoints.html#autotoc_md41", null ],
          [ "レスポンス圧縮", "md_docs_2wiki_2endpoints.html#autotoc_md42", null ]
        ] ],
        [ "エラーレスポンス", "md_docs_2wiki_2endpoints.html#autotoc_md43", [
          [ "400 Bad Request", "md_docs_2wiki_2endpoints.html#autotoc_md44", null ],
          [ "500 Internal Server Error", "md_docs_2wiki_2endpoints.html#autotoc_md45", null ]
        ] ]
      ] ],
      [ "GET / - ヘルスチェック", "md_docs_2wiki_2endpoints.html#autotoc_md46", [
        [ "リクエスト", "md_docs_2wiki_2endpoints.html#autotoc_md47", null ],
        [ "レスポンス", "md_docs_2wiki_2endpoints.html#autotoc_md48", null ]
      ] ],
      [ "リクエスト/レスポンス例", "md_docs_2wiki_2endpoints.html#autotoc_md49", [
        [ "シンプルな2Dフレーム解析", "md_docs_2wiki_2endpoints.html#autotoc_md50", null ],
        [ "シェル要素を含む3Dフレーム", "md_docs_2wiki_2endpoints.html#autotoc_md51", null ]
      ] ],
      [ "レート制限", "md_docs_2wiki_2endpoints.html#autotoc_md52", null ],
      [ "認証", "md_docs_2wiki_2endpoints.html#autotoc_md53", null ]
    ] ],
    [ "エラーハンドリング", "md_docs_2wiki_2error-handling.html", [
      [ "概要", "md_docs_2wiki_2error-handling.html#autotoc_md55", null ],
      [ "HTTPステータスコード", "md_docs_2wiki_2error-handling.html#autotoc_md56", [
        [ "200 OK", "md_docs_2wiki_2error-handling.html#autotoc_md57", null ],
        [ "400 Bad Request", "md_docs_2wiki_2error-handling.html#autotoc_md58", null ],
        [ "500 Internal Server Error", "md_docs_2wiki_2error-handling.html#autotoc_md59", null ]
      ] ],
      [ "エラーレスポンス形式", "md_docs_2wiki_2error-handling.html#autotoc_md60", null ],
      [ "一般的なエラータイプ", "md_docs_2wiki_2error-handling.html#autotoc_md61", [
        [ "入力データエラー (400)", "md_docs_2wiki_2error-handling.html#autotoc_md62", [
          [ "必須データの欠如", "md_docs_2wiki_2error-handling.html#autotoc_md63", null ],
          [ "無効な節点参照", "md_docs_2wiki_2error-handling.html#autotoc_md64", null ],
          [ "材料特性の欠如", "md_docs_2wiki_2error-handling.html#autotoc_md65", null ],
          [ "無効な荷重データ", "md_docs_2wiki_2error-handling.html#autotoc_md66", null ]
        ] ],
        [ "解析計算エラー (500)", "md_docs_2wiki_2error-handling.html#autotoc_md67", [
          [ "特異剛性行列", "md_docs_2wiki_2error-handling.html#autotoc_md68", null ],
          [ "数値不安定性", "md_docs_2wiki_2error-handling.html#autotoc_md69", null ],
          [ "メモリ割り当てエラー", "md_docs_2wiki_2error-handling.html#autotoc_md70", null ]
        ] ]
      ] ],
      [ "エラーハンドリングのベストプラクティス", "md_docs_2wiki_2error-handling.html#autotoc_md71", [
        [ "1. 入力データの検証", "md_docs_2wiki_2error-handling.html#autotoc_md72", null ],
        [ "2. APIエラーの適切な処理", "md_docs_2wiki_2error-handling.html#autotoc_md73", null ],
        [ "3. 一時的エラーのリトライロジック", "md_docs_2wiki_2error-handling.html#autotoc_md74", null ]
      ] ],
      [ "一般的な問題のトラブルシューティング", "md_docs_2wiki_2error-handling.html#autotoc_md75", [
        [ "構造不安定性", "md_docs_2wiki_2error-handling.html#autotoc_md76", null ],
        [ "大規模モデルの性能", "md_docs_2wiki_2error-handling.html#autotoc_md77", null ],
        [ "無効な参照", "md_docs_2wiki_2error-handling.html#autotoc_md78", null ]
      ] ],
      [ "エラーコードリファレンス", "md_docs_2wiki_2error-handling.html#autotoc_md79", null ]
    ] ],
    [ "使用例", "md_docs_2wiki_2examples.html", [
      [ "🎉 新実装（FemModel）高精度解析例", "md_docs_2wiki_2examples.html#autotoc_md81", [
        [ "🚀 基本的なFemModel使用例", "md_docs_2wiki_2examples.html#autotoc_md82", null ],
        [ "🔧 要素分割機能の活用例", "md_docs_2wiki_2examples.html#autotoc_md83", [
          [ "着目点による要素分割", "md_docs_2wiki_2examples.html#autotoc_md84", null ],
          [ "分布荷重による要素分割", "md_docs_2wiki_2examples.html#autotoc_md85", null ],
          [ "集中荷重による要素分割", "md_docs_2wiki_2examples.html#autotoc_md86", null ]
        ] ],
        [ "🔍 統合テスト機能の使用例", "md_docs_2wiki_2examples.html#autotoc_md87", null ],
        [ "📈 新旧実装比較例", "md_docs_2wiki_2examples.html#autotoc_md88", null ],
        [ "🎊 プロジェクト完了記念例", "md_docs_2wiki_2examples.html#autotoc_md89", null ]
      ] ],
      [ "材料非線形解析（2026年1月追加）", "md_docs_2wiki_2examples.html#autotoc_md91", [
        [ "基本的な非線形解析", "md_docs_2wiki_2examples.html#autotoc_md92", null ],
        [ "非線形材料定義を含むJSONモデル", "md_docs_2wiki_2examples.html#autotoc_md93", null ],
        [ "プログラムから非線形材料を定義", "md_docs_2wiki_2examples.html#autotoc_md94", null ],
        [ "非対称スケルトンカーブ", "md_docs_2wiki_2examples.html#autotoc_md95", null ],
        [ "スケルトンカーブの概念図", "md_docs_2wiki_2examples.html#autotoc_md96", null ],
        [ "剛性低減則", "md_docs_2wiki_2examples.html#autotoc_md97", null ]
      ] ],
      [ "基本的な2Dフレーム解析（従来API）", "md_docs_2wiki_2examples.html#autotoc_md99", [
        [ "Python例", "md_docs_2wiki_2examples.html#autotoc_md100", null ],
        [ "期待される出力", "md_docs_2wiki_2examples.html#autotoc_md101", null ]
      ] ],
      [ "複数荷重ケースを持つ3Dフレーム", "md_docs_2wiki_2examples.html#autotoc_md102", [
        [ "モデル定義", "md_docs_2wiki_2examples.html#autotoc_md103", null ]
      ] ],
      [ "シェル要素解析", "md_docs_2wiki_2examples.html#autotoc_md104", null ],
      [ "分布荷重例", "md_docs_2wiki_2examples.html#autotoc_md105", null ],
      [ "エラーハンドリング例", "md_docs_2wiki_2examples.html#autotoc_md106", null ],
      [ "性能のヒント", "md_docs_2wiki_2examples.html#autotoc_md107", [
        [ "大規模モデルの最適化", "md_docs_2wiki_2examples.html#autotoc_md108", null ]
      ] ]
    ] ],
    [ "はじめに", "md_docs_2wiki_2getting-started.html", [
      [ "🎉 概要", "md_docs_2wiki_2getting-started.html#autotoc_md110", null ],
      [ "🏆 技術的優位性", "md_docs_2wiki_2getting-started.html#autotoc_md111", [
        [ "📈 劇的な改善実績", "md_docs_2wiki_2getting-started.html#autotoc_md112", null ],
        [ "🔧 要素分割機能（完全実装）", "md_docs_2wiki_2getting-started.html#autotoc_md113", null ],
        [ "📊 荷重データ処理（完全実装）", "md_docs_2wiki_2getting-started.html#autotoc_md114", null ]
      ] ],
      [ "🚀 主要機能", "md_docs_2wiki_2getting-started.html#autotoc_md115", [
        [ "新実装機能", "md_docs_2wiki_2getting-started.html#autotoc_md116", null ],
        [ "従来機能", "md_docs_2wiki_2getting-started.html#autotoc_md117", null ]
      ] ],
      [ "アーキテクチャ", "md_docs_2wiki_2getting-started.html#autotoc_md118", [
        [ "新実装（FemModel）のワークフロー", "md_docs_2wiki_2getting-started.html#autotoc_md119", null ],
        [ "従来API（RESTful）のワークフロー", "md_docs_2wiki_2getting-started.html#autotoc_md120", null ]
      ] ],
      [ "クイックスタート", "md_docs_2wiki_2getting-started.html#autotoc_md121", [
        [ "新実装（FemModel）の使用方法", "md_docs_2wiki_2getting-started.html#autotoc_md122", null ],
        [ "要素分割機能の活用例", "md_docs_2wiki_2getting-started.html#autotoc_md123", null ],
        [ "RESTful API の使用方法", "md_docs_2wiki_2getting-started.html#autotoc_md124", null ],
        [ "レスポンス構造", "md_docs_2wiki_2getting-started.html#autotoc_md125", null ]
      ] ],
      [ "🔍 品質保証", "md_docs_2wiki_2getting-started.html#autotoc_md126", [
        [ "統合テスト機能", "md_docs_2wiki_2getting-started.html#autotoc_md127", null ]
      ] ],
      [ "エラーハンドリング", "md_docs_2wiki_2getting-started.html#autotoc_md128", null ],
      [ "🎊 プロジェクト完了", "md_docs_2wiki_2getting-started.html#autotoc_md129", null ],
      [ "次のステップ", "md_docs_2wiki_2getting-started.html#autotoc_md130", null ]
    ] ],
    [ "クイックリファレンス", "md_docs_2wiki_2quick-reference.html", [
      [ "🎉 新実装（FemModel）高精度解析", "md_docs_2wiki_2quick-reference.html#autotoc_md155", [
        [ "🚀 基本的な使用方法（新実装）", "md_docs_2wiki_2quick-reference.html#autotoc_md156", null ],
        [ "🔧 要素分割機能チートシート", "md_docs_2wiki_2quick-reference.html#autotoc_md157", [
          [ "着目点による要素分割", "md_docs_2wiki_2quick-reference.html#autotoc_md158", null ],
          [ "分布荷重による自動分割", "md_docs_2wiki_2quick-reference.html#autotoc_md159", null ],
          [ "集中荷重による自動分割", "md_docs_2wiki_2quick-reference.html#autotoc_md160", null ]
        ] ],
        [ "📊 技術的優位性", "md_docs_2wiki_2quick-reference.html#autotoc_md161", null ],
        [ "🔍 統合テスト", "md_docs_2wiki_2quick-reference.html#autotoc_md162", null ]
      ] ],
      [ "材料非線形解析（2026年1月追加）", "md_docs_2wiki_2quick-reference.html#autotoc_md164", [
        [ "JR総研剛性低減RC型モデル", "md_docs_2wiki_2quick-reference.html#autotoc_md165", null ],
        [ "解析パラメータ（loadセクションで指定）", "md_docs_2wiki_2quick-reference.html#autotoc_md166", null ],
        [ "非線形材料定義（JSON）", "md_docs_2wiki_2quick-reference.html#autotoc_md167", null ],
        [ "4折線スケルトンカーブパラメータ", "md_docs_2wiki_2quick-reference.html#autotoc_md168", null ],
        [ "非線形適用自由度（hysteresis_dofs）", "md_docs_2wiki_2quick-reference.html#autotoc_md169", null ],
        [ "解析タイプ", "md_docs_2wiki_2quick-reference.html#autotoc_md170", null ]
      ] ],
      [ "基本的な使用方法（従来API）", "md_docs_2wiki_2quick-reference.html#autotoc_md172", [
        [ "最小限の2Dフレーム解析", "md_docs_2wiki_2quick-reference.html#autotoc_md173", null ]
      ] ],
      [ "データ構造チートシート", "md_docs_2wiki_2quick-reference.html#autotoc_md174", [
        [ "節点定義", "md_docs_2wiki_2quick-reference.html#autotoc_md175", null ],
        [ "部材定義", "md_docs_2wiki_2quick-reference.html#autotoc_md176", null ],
        [ "材料・断面特性", "md_docs_2wiki_2quick-reference.html#autotoc_md177", null ],
        [ "支点条件", "md_docs_2wiki_2quick-reference.html#autotoc_md178", null ],
        [ "🆕 荷重定義（要素分割対応）", "md_docs_2wiki_2quick-reference.html#autotoc_md179", null ],
        [ "🆕 着目点定義（要素分割）", "md_docs_2wiki_2quick-reference.html#autotoc_md180", null ]
      ] ],
      [ "一般的な支点条件", "md_docs_2wiki_2quick-reference.html#autotoc_md181", [
        [ "固定支点", "md_docs_2wiki_2quick-reference.html#autotoc_md182", null ],
        [ "ピン支点", "md_docs_2wiki_2quick-reference.html#autotoc_md183", null ],
        [ "ローラー支点（Y方向のみ拘束）", "md_docs_2wiki_2quick-reference.html#autotoc_md184", null ],
        [ "🆕 バネ支点（新実装対応）", "md_docs_2wiki_2quick-reference.html#autotoc_md185", null ]
      ] ],
      [ "荷重方向指定", "md_docs_2wiki_2quick-reference.html#autotoc_md186", [
        [ "全体座標系", "md_docs_2wiki_2quick-reference.html#autotoc_md187", null ],
        [ "要素局所座標系", "md_docs_2wiki_2quick-reference.html#autotoc_md188", null ],
        [ "🆕 荷重マーク（要素分割対応）", "md_docs_2wiki_2quick-reference.html#autotoc_md189", null ]
      ] ],
      [ "典型的な材料特性", "md_docs_2wiki_2quick-reference.html#autotoc_md190", [
        [ "鋼材（SS400）", "md_docs_2wiki_2quick-reference.html#autotoc_md191", null ],
        [ "コンクリート（Fc=24）", "md_docs_2wiki_2quick-reference.html#autotoc_md192", null ]
      ] ],
      [ "🆕 新実装エラーハンドリング", "md_docs_2wiki_2quick-reference.html#autotoc_md193", null ],
      [ "従来APIエラーハンドリング", "md_docs_2wiki_2quick-reference.html#autotoc_md194", null ],
      [ "結果データアクセス", "md_docs_2wiki_2quick-reference.html#autotoc_md195", [
        [ "🆕 新実装（FemModel）", "md_docs_2wiki_2quick-reference.html#autotoc_md196", null ],
        [ "従来API", "md_docs_2wiki_2quick-reference.html#autotoc_md197", null ]
      ] ],
      [ "単位系", "md_docs_2wiki_2quick-reference.html#autotoc_md198", null ],
      [ "🆕 新実装の特殊機能", "md_docs_2wiki_2quick-reference.html#autotoc_md199", [
        [ "L2負値処理（分布荷重）", "md_docs_2wiki_2quick-reference.html#autotoc_md200", null ],
        [ "重複節点の自動処理", "md_docs_2wiki_2quick-reference.html#autotoc_md201", null ]
      ] ],
      [ "よくある問題と解決法", "md_docs_2wiki_2quick-reference.html#autotoc_md202", [
        [ "🆕 要素分割関連", "md_docs_2wiki_2quick-reference.html#autotoc_md203", null ],
        [ "不安定構造", "md_docs_2wiki_2quick-reference.html#autotoc_md204", null ],
        [ "数値エラー", "md_docs_2wiki_2quick-reference.html#autotoc_md205", null ],
        [ "性能問題", "md_docs_2wiki_2quick-reference.html#autotoc_md206", null ]
      ] ],
      [ "🎊 プロジェクト完了", "md_docs_2wiki_2quick-reference.html#autotoc_md207", null ]
    ] ],
    [ "解析ワークフロー", "md_docs_2wiki_2workflow.html", [
      [ "🎉 概要", "md_docs_2wiki_2workflow.html#autotoc_md209", null ],
      [ "🚀 新実装（FemModel）の完全解析フロー", "md_docs_2wiki_2workflow.html#autotoc_md210", null ],
      [ "🏆 技術的優位性", "md_docs_2wiki_2workflow.html#autotoc_md211", [
        [ "📈 改善実績", "md_docs_2wiki_2workflow.html#autotoc_md212", null ]
      ] ],
      [ "材料非線形解析ワークフロー（2026年1月追加）", "md_docs_2wiki_2workflow.html#autotoc_md213", [
        [ "非線形解析フロー", "md_docs_2wiki_2workflow.html#autotoc_md214", null ],
        [ "Newton-Raphson法アルゴリズム", "md_docs_2wiki_2workflow.html#autotoc_md215", null ],
        [ "JR総研剛性低減RC型履歴ルール", "md_docs_2wiki_2workflow.html#autotoc_md216", null ],
        [ "剛性低減式", "md_docs_2wiki_2workflow.html#autotoc_md217", null ]
      ] ],
      [ "フェーズ1: 入力処理", "md_docs_2wiki_2workflow.html#autotoc_md219", [
        [ "1.1 JSON検証（新実装対応）", "md_docs_2wiki_2workflow.html#autotoc_md220", null ],
        [ "1.2 データ変換（高精度対応）", "md_docs_2wiki_2workflow.html#autotoc_md221", null ]
      ] ],
      [ "🔧 フェーズ2: 要素分割処理（新機能）", "md_docs_2wiki_2workflow.html#autotoc_md222", [
        [ "2.1 着目点による分割", "md_docs_2wiki_2workflow.html#autotoc_md223", null ],
        [ "2.2 分布荷重による分割", "md_docs_2wiki_2workflow.html#autotoc_md224", null ],
        [ "2.3 集中荷重による分割", "md_docs_2wiki_2workflow.html#autotoc_md225", null ],
        [ "2.4 分割結果の統合", "md_docs_2wiki_2workflow.html#autotoc_md226", null ]
      ] ],
      [ "フェーズ3: 行列組み立て（高精度対応）", "md_docs_2wiki_2workflow.html#autotoc_md227", [
        [ "3.1 剛性行列作成", "md_docs_2wiki_2workflow.html#autotoc_md228", null ],
        [ "3.2 境界条件適用", "md_docs_2wiki_2workflow.html#autotoc_md229", null ]
      ] ],
      [ "フェーズ4: 解析実行（高精度ソルバー）", "md_docs_2wiki_2workflow.html#autotoc_md230", [
        [ "4.1 方程式求解", "md_docs_2wiki_2workflow.html#autotoc_md231", null ],
        [ "4.2 力計算（詳細解析）", "md_docs_2wiki_2workflow.html#autotoc_md232", null ]
      ] ],
      [ "フェーズ5: 結果処理（高精度出力）", "md_docs_2wiki_2workflow.html#autotoc_md233", [
        [ "5.1 結果フォーマット", "md_docs_2wiki_2workflow.html#autotoc_md234", null ],
        [ "5.2 品質保証（統合テスト）", "md_docs_2wiki_2workflow.html#autotoc_md235", null ]
      ] ],
      [ "🎯 詳細コンポーネント相互作用", "md_docs_2wiki_2workflow.html#autotoc_md236", [
        [ "FemModelクラスワークフロー", "md_docs_2wiki_2workflow.html#autotoc_md237", null ]
      ] ],
      [ "🔍 エラーハンドリング（新実装対応）", "md_docs_2wiki_2workflow.html#autotoc_md238", null ],
      [ "📊 パフォーマンス最適化", "md_docs_2wiki_2workflow.html#autotoc_md239", [
        [ "メモリ管理（高精度対応）", "md_docs_2wiki_2workflow.html#autotoc_md240", null ],
        [ "計算効率（高精度ソルバー）", "md_docs_2wiki_2workflow.html#autotoc_md241", null ],
        [ "スケーラビリティ（拡張性）", "md_docs_2wiki_2workflow.html#autotoc_md242", null ]
      ] ],
      [ "🎊 プロジェクト完了", "md_docs_2wiki_2workflow.html#autotoc_md243", null ],
      [ "デバッグとモニタリング", "md_docs_2wiki_2workflow.html#autotoc_md244", [
        [ "ログ統合（新実装対応）", "md_docs_2wiki_2workflow.html#autotoc_md245", null ],
        [ "パフォーマンスメトリクス（高精度対応）", "md_docs_2wiki_2workflow.html#autotoc_md246", null ]
      ] ]
    ] ],
    [ "名前空間", "namespaces.html", [
      [ "名前空間一覧", "namespaces.html", "namespaces_dup" ],
      [ "名前空間メンバ", "namespacemembers.html", [
        [ "全て", "namespacemembers.html", null ],
        [ "関数", "namespacemembers_func.html", null ]
      ] ]
    ] ],
    [ "クラス", "annotated.html", [
      [ "クラス一覧", "annotated.html", "annotated_dup" ],
      [ "クラス索引", "classes.html", null ],
      [ "クラス階層", "hierarchy.html", "hierarchy" ],
      [ "クラスメンバ", "functions.html", [
        [ "全て", "functions.html", "functions_dup" ],
        [ "関数", "functions_func.html", "functions_func" ]
      ] ]
    ] ]
  ] ]
];

var NAVTREEINDEX =
[
"annotated.html",
"classfem_1_1elements_1_1nonlinear__bar__element_1_1NonlinearBarElement.html#a5b4fe58cfcb39c479ed1412d817d044e",
"classfem_1_1model_1_1FemModel.html#a91a3f6d7f13a28062f23fbdf4d66d7de",
"classfem_1_1strain__stress_1_1Strain.html#abea2184e36c495bef9ee96f6f89feb4d",
"md_docs_2wiki_2quick-reference.html#autotoc_md202"
];

var SYNCONMSG = 'クリックで同期表示が無効になります';
var SYNCOFFMSG = 'クリックで同期表示が有効になります';