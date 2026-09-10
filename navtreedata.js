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
  [ "FEMPython", "index.html", [
    [ "FEMPython ユーザーガイド", "index.html", "index" ],
    [ "モデルの入力", "md_docs_2wiki_2data-structures.html", [
      [ "単位と座標", "md_docs_2wiki_2data-structures.html#autotoc_md1", null ],
      [ "ルートの項目", "md_docs_2wiki_2data-structures.html#autotoc_md2", null ],
      [ "節点・部材・材料", "md_docs_2wiki_2data-structures.html#autotoc_md3", null ],
      [ "シェルとソリッド", "md_docs_2wiki_2data-structures.html#autotoc_md4", null ],
      [ "支持条件と強制変位", "md_docs_2wiki_2data-structures.html#autotoc_md5", [
        [ "通常の支持・支持ばね", "md_docs_2wiki_2data-structures.html#autotoc_md6", null ],
        [ "変位・ばねを明示する形式", "md_docs_2wiki_2data-structures.html#autotoc_md7", null ]
      ] ],
      [ "荷重ケース", "md_docs_2wiki_2data-structures.html#autotoc_md8", [
        [ "解析制御", "md_docs_2wiki_2data-structures.html#autotoc_md9", null ]
      ] ],
      [ "部材途中の荷重", "md_docs_2wiki_2data-structures.html#autotoc_md10", null ],
      [ "着目点・剛域・分布ばね・材端解放", "md_docs_2wiki_2data-structures.html#autotoc_md11", null ],
      [ "面圧", "md_docs_2wiki_2data-structures.html#autotoc_md12", null ],
      [ "入力の確認ポイント", "md_docs_2wiki_2data-structures.html#autotoc_md13", null ]
    ] ],
    [ "設計実務での利用手順", "md_docs_2wiki_2design-practice.html", [
      [ "適用範囲を最初に決める", "md_docs_2wiki_2design-practice.html#autotoc_md15", null ],
      [ "推奨する作業順序", "md_docs_2wiki_2design-practice.html#autotoc_md16", null ],
      [ "1. 計算条件を固定する", "md_docs_2wiki_2design-practice.html#autotoc_md17", null ],
      [ "2. 入力モデルを確認する", "md_docs_2wiki_2design-practice.html#autotoc_md18", null ],
      [ "3. 荷重ケースを分ける", "md_docs_2wiki_2design-practice.html#autotoc_md19", [
        [ "線荷重・面荷重", "md_docs_2wiki_2design-practice.html#autotoc_md20", null ]
      ] ],
      [ "4. 解析結果の成立を確認する", "md_docs_2wiki_2design-practice.html#autotoc_md21", null ],
      [ "5. メッシュとモデル化の感度を確認する", "md_docs_2wiki_2design-practice.html#autotoc_md22", null ],
      [ "6. 独立計算と照合する", "md_docs_2wiki_2design-practice.html#autotoc_md23", null ],
      [ "7. 保存する成果物", "md_docs_2wiki_2design-practice.html#autotoc_md24", null ],
      [ "使用前チェックリスト", "md_docs_2wiki_2design-practice.html#autotoc_md25", null ],
      [ "使用を止めて見直す条件", "md_docs_2wiki_2design-practice.html#autotoc_md26", null ]
    ] ],
    [ "要素と解析の選び方", "md_docs_2wiki_2elements.html", [
      [ "解析モード", "md_docs_2wiki_2elements.html#autotoc_md28", null ],
      [ "対応する要素", "md_docs_2wiki_2elements.html#autotoc_md29", null ],
      [ "梁：曲げとせん断変形", "md_docs_2wiki_2elements.html#autotoc_md30", null ],
      [ "シェル：薄板と厚板", "md_docs_2wiki_2elements.html#autotoc_md31", null ],
      [ "ソリッド：一次と二次", "md_docs_2wiki_2elements.html#autotoc_md32", null ],
      [ "固有値解析の設定と制約", "md_docs_2wiki_2elements.html#autotoc_md33", null ],
      [ "メッシュ精度と適用範囲", "md_docs_2wiki_2elements.html#autotoc_md34", null ],
      [ "対応範囲を広げるとき", "md_docs_2wiki_2elements.html#autotoc_md35", null ]
    ] ],
    [ "HTTP API", "md_docs_2wiki_2endpoints.html", [
      [ "ローカルサーバーの起動", "md_docs_2wiki_2endpoints.html#autotoc_md37", null ],
      [ "エンドポイント", "md_docs_2wiki_2endpoints.html#autotoc_md38", null ],
      [ "通常JSONで解析する", "md_docs_2wiki_2endpoints.html#autotoc_md39", [
        [ "入力と解析モード", "md_docs_2wiki_2endpoints.html#autotoc_md40", null ],
        [ "成功時の結果", "md_docs_2wiki_2endpoints.html#autotoc_md41", null ]
      ] ],
      [ "互換用の圧縮転送", "md_docs_2wiki_2endpoints.html#autotoc_md42", null ],
      [ "エラー応答", "md_docs_2wiki_2endpoints.html#autotoc_md43", null ],
      [ "運用上の挙動", "md_docs_2wiki_2endpoints.html#autotoc_md44", null ]
    ] ],
    [ "エラーと対処", "md_docs_2wiki_2error-handling.html", [
      [ "症状から調べる", "md_docs_2wiki_2error-handling.html#autotoc_md46", null ],
      [ "不安定なモデルを直す", "md_docs_2wiki_2error-handling.html#autotoc_md47", null ],
      [ "非線形の未収束", "md_docs_2wiki_2error-handling.html#autotoc_md48", null ],
      [ "Pythonの診断例外", "md_docs_2wiki_2error-handling.html#autotoc_md49", null ],
      [ "HTTPステータスの解釈", "md_docs_2wiki_2error-handling.html#autotoc_md50", null ],
      [ "解析ログを有効にする", "md_docs_2wiki_2error-handling.html#autotoc_md51", null ],
      [ "問題を再現できる形にする", "md_docs_2wiki_2error-handling.html#autotoc_md52", null ]
    ] ],
    [ "実行例", "md_docs_2wiki_2examples.html", [
      [ "分布荷重と着目位置での分割", "md_docs_2wiki_2examples.html#autotoc_md54", null ],
      [ "複数荷重ケースを1つずつ解析する", "md_docs_2wiki_2examples.html#autotoc_md55", null ],
      [ "支持ばねと強制変位", "md_docs_2wiki_2examples.html#autotoc_md56", null ],
      [ "温度変化による梁の伸び", "md_docs_2wiki_2examples.html#autotoc_md57", null ],
      [ "シェルへの面圧", "md_docs_2wiki_2examples.html#autotoc_md58", null ],
      [ "二次四面体の応力", "md_docs_2wiki_2examples.html#autotoc_md59", null ],
      [ "荷重だけを伝達する部材", "md_docs_2wiki_2examples.html#autotoc_md60", null ],
      [ "固有値解析：ばね支持された梁", "md_docs_2wiki_2examples.html#autotoc_md61", null ],
      [ "非線形・HTTP・保存の例", "md_docs_2wiki_2examples.html#autotoc_md62", null ]
    ] ],
    [ "ファイル入出力・VTK", "md_docs_2wiki_2file-formats.html", [
      [ "対応形式", "md_docs_2wiki_2file-formats.html#autotoc_md64", null ],
      [ "保存して再び解析する", "md_docs_2wiki_2file-formats.html#autotoc_md65", null ],
      [ "保存用JSONの構造", "md_docs_2wiki_2file-formats.html#autotoc_md66", null ],
      [ "結果JSON", "md_docs_2wiki_2file-formats.html#autotoc_md67", null ],
      [ "V0の構造ファイル <tt>.fem</tt>", "md_docs_2wiki_2file-formats.html#autotoc_md68", null ],
      [ "独自形式 <tt>.fw3</tt>", "md_docs_2wiki_2file-formats.html#autotoc_md69", null ],
      [ "VTKで結果を可視化する", "md_docs_2wiki_2file-formats.html#autotoc_md70", null ]
    ] ],
    [ "はじめに：片持ち梁を解析する", "md_docs_2wiki_2getting-started.html", [
      [ "1. 実行環境を用意する", "md_docs_2wiki_2getting-started.html#autotoc_md72", null ],
      [ "2. モデルをJSONで保存する", "md_docs_2wiki_2getting-started.html#autotoc_md73", null ],
      [ "3. 解析して結果を読む", "md_docs_2wiki_2getting-started.html#autotoc_md74", null ],
      [ "4. 結果を保存する", "md_docs_2wiki_2getting-started.html#autotoc_md75", null ],
      [ "次に試すこと", "md_docs_2wiki_2getting-started.html#autotoc_md76", null ]
    ] ],
    [ "材料非線形解析", "md_docs_2wiki_2nonlinear-analysis.html", [
      [ "モデル化の範囲", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md84", null ],
      [ "入力するのは断面の関係", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md85", null ],
      [ "骨格曲線の設定", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md86", [
        [ "正負非対称の骨格", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md87", null ]
      ] ],
      [ "載荷の順序を指定する", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md88", null ],
      [ "負勾配を変位制御で追跡する", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md89", null ],
      [ "実行例：曲げ・除荷・反転", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md90", null ],
      [ "収束と履歴の扱い", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md91", null ]
    ] ],
    [ "Python API", "md_docs_2wiki_2python-api.html", [
      [ "Pythonだけで片持ち梁を作る", "md_docs_2wiki_2python-api.html#autotoc_md93", null ],
      [ "モデルを作る操作", "md_docs_2wiki_2python-api.html#autotoc_md94", null ],
      [ "解析・結果の操作", "md_docs_2wiki_2python-api.html#autotoc_md95", null ],
      [ "ファイルを読んでから組み立てる場合", "md_docs_2wiki_2python-api.html#autotoc_md96", null ],
      [ "状態の管理", "md_docs_2wiki_2python-api.html#autotoc_md97", null ]
    ] ],
    [ "クイックリファレンス", "md_docs_2wiki_2quick-reference.html", [
      [ "解析を選ぶ", "md_docs_2wiki_2quick-reference.html#autotoc_md99", null ],
      [ "入力名の対応", "md_docs_2wiki_2quick-reference.html#autotoc_md100", null ],
      [ "支持・荷重", "md_docs_2wiki_2quick-reference.html#autotoc_md101", null ],
      [ "結果にアクセスする", "md_docs_2wiki_2quick-reference.html#autotoc_md102", null ],
      [ "単位・符号の要点", "md_docs_2wiki_2quick-reference.html#autotoc_md103", null ],
      [ "困ったとき", "md_docs_2wiki_2quick-reference.html#autotoc_md104", null ]
    ] ],
    [ "結果の読み方", "md_docs_2wiki_2results.html", [
      [ "PythonとJSONの違い", "md_docs_2wiki_2results.html#autotoc_md106", null ],
      [ "結果メタデータ", "md_docs_2wiki_2results.html#autotoc_md107", null ],
      [ "線形静解析の主な項目", "md_docs_2wiki_2results.html#autotoc_md108", null ],
      [ "節点変位と支点反力", "md_docs_2wiki_2results.html#autotoc_md109", null ],
      [ "梁の端力", "md_docs_2wiki_2results.html#autotoc_md110", [
        [ "分割前の部材との対応", "md_docs_2wiki_2results.html#autotoc_md111", null ],
        [ "釣合いによる端力回復", "md_docs_2wiki_2results.html#autotoc_md112", null ]
      ] ],
      [ "材料非線形の段階結果", "md_docs_2wiki_2results.html#autotoc_md113", [
        [ "応答曲率", "md_docs_2wiki_2results.html#autotoc_md114", null ],
        [ "収束履歴", "md_docs_2wiki_2results.html#autotoc_md115", null ]
      ] ],
      [ "シェルの結果", "md_docs_2wiki_2results.html#autotoc_md116", [
        [ "旧シェル比較形式との違い", "md_docs_2wiki_2results.html#autotoc_md117", null ]
      ] ],
      [ "ソリッドの応力・ひずみ", "md_docs_2wiki_2results.html#autotoc_md118", null ],
      [ "固有値解析の結果", "md_docs_2wiki_2results.html#autotoc_md119", null ],
      [ "静解析の微小補正", "md_docs_2wiki_2results.html#autotoc_md120", null ]
    ] ],
    [ "線荷重・面荷重", "md_docs_2wiki_2spatial-loads.html", [
      [ "座標・符号・単位", "md_docs_2wiki_2spatial-loads.html#autotoc_md122", null ],
      [ "パネルの指定", "md_docs_2wiki_2spatial-loads.html#autotoc_md123", null ],
      [ "旧JSONの完全な例", "md_docs_2wiki_2spatial-loads.html#autotoc_md124", null ],
      [ "Python APIと面荷重", "md_docs_2wiki_2spatial-loads.html#autotoc_md125", null ],
      [ "正規化JSONとHTTP", "md_docs_2wiki_2spatial-loads.html#autotoc_md126", null ],
      [ "出力と失敗時の扱い", "md_docs_2wiki_2spatial-loads.html#autotoc_md127", null ],
      [ "制限とエラー", "md_docs_2wiki_2spatial-loads.html#autotoc_md128", null ]
    ] ],
    [ "解析ワークフロー", "md_docs_2wiki_2workflow.html", [
      [ "入力から結果まで", "md_docs_2wiki_2workflow.html#autotoc_md130", [
        [ "1. 入力の変換とケース選択", "md_docs_2wiki_2workflow.html#autotoc_md131", null ],
        [ "2. 部材の分割", "md_docs_2wiki_2workflow.html#autotoc_md132", null ],
        [ "3. 自由度と行列の組立", "md_docs_2wiki_2workflow.html#autotoc_md133", null ],
        [ "4. 解析", "md_docs_2wiki_2workflow.html#autotoc_md134", null ],
        [ "5. 後処理", "md_docs_2wiki_2workflow.html#autotoc_md135", null ]
      ] ],
      [ "共通ソルバーAPI", "md_docs_2wiki_2workflow.html#autotoc_md136", null ],
      [ "コールバックで確定段階を受け取る", "md_docs_2wiki_2workflow.html#autotoc_md137", null ],
      [ "状態と結果の独立性", "md_docs_2wiki_2workflow.html#autotoc_md138", null ],
      [ "実装への案内", "md_docs_2wiki_2workflow.html#autotoc_md139", null ]
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
"classfem_1_1elements_1_1base__element_1_1BaseElement.html#adff74f5107c5040a1f57533206968fec",
"classfem_1_1model_1_1FemModel.html#a31c6ae6a005d19dc49c06e4126cf14b3",
"classfem_1_1solver_1_1Solver.html#ad670d42a4a9883274461a0534a36c593",
"namespacefem_1_1capabilities.html"
];

var SYNCONMSG = 'クリックで同期表示が無効になります';
var SYNCOFFMSG = 'クリックで同期表示が有効になります';