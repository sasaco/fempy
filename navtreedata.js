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
    [ "要素と解析の選び方", "md_docs_2wiki_2elements.html", [
      [ "解析モード", "md_docs_2wiki_2elements.html#autotoc_md15", null ],
      [ "対応する要素", "md_docs_2wiki_2elements.html#autotoc_md16", null ],
      [ "梁：曲げとせん断変形", "md_docs_2wiki_2elements.html#autotoc_md17", null ],
      [ "シェル：薄板と厚板", "md_docs_2wiki_2elements.html#autotoc_md18", null ],
      [ "ソリッド：一次と二次", "md_docs_2wiki_2elements.html#autotoc_md19", null ],
      [ "固有値解析の設定と制約", "md_docs_2wiki_2elements.html#autotoc_md20", null ],
      [ "対応範囲を広げるとき", "md_docs_2wiki_2elements.html#autotoc_md21", null ]
    ] ],
    [ "HTTP API", "md_docs_2wiki_2endpoints.html", [
      [ "ローカルサーバーの起動", "md_docs_2wiki_2endpoints.html#autotoc_md23", null ],
      [ "エンドポイント", "md_docs_2wiki_2endpoints.html#autotoc_md24", null ],
      [ "通常JSONで解析する", "md_docs_2wiki_2endpoints.html#autotoc_md25", [
        [ "入力と解析モード", "md_docs_2wiki_2endpoints.html#autotoc_md26", null ],
        [ "成功時の結果", "md_docs_2wiki_2endpoints.html#autotoc_md27", null ]
      ] ],
      [ "互換用の圧縮転送", "md_docs_2wiki_2endpoints.html#autotoc_md28", null ],
      [ "エラー応答", "md_docs_2wiki_2endpoints.html#autotoc_md29", null ],
      [ "運用上の挙動", "md_docs_2wiki_2endpoints.html#autotoc_md30", null ]
    ] ],
    [ "エラーと対処", "md_docs_2wiki_2error-handling.html", [
      [ "症状から調べる", "md_docs_2wiki_2error-handling.html#autotoc_md32", null ],
      [ "不安定なモデルを直す", "md_docs_2wiki_2error-handling.html#autotoc_md33", null ],
      [ "非線形の未収束", "md_docs_2wiki_2error-handling.html#autotoc_md34", null ],
      [ "HTTPステータスの解釈", "md_docs_2wiki_2error-handling.html#autotoc_md35", null ],
      [ "問題を再現できる形にする", "md_docs_2wiki_2error-handling.html#autotoc_md36", null ]
    ] ],
    [ "実行例", "md_docs_2wiki_2examples.html", [
      [ "分布荷重と着目位置での分割", "md_docs_2wiki_2examples.html#autotoc_md38", null ],
      [ "複数荷重ケースを1つずつ解析する", "md_docs_2wiki_2examples.html#autotoc_md39", null ],
      [ "支持ばねと強制変位", "md_docs_2wiki_2examples.html#autotoc_md40", null ],
      [ "温度変化による梁の伸び", "md_docs_2wiki_2examples.html#autotoc_md41", null ],
      [ "シェルへの面圧", "md_docs_2wiki_2examples.html#autotoc_md42", null ],
      [ "二次四面体の応力", "md_docs_2wiki_2examples.html#autotoc_md43", null ],
      [ "荷重だけを伝達する部材", "md_docs_2wiki_2examples.html#autotoc_md44", null ],
      [ "固有値解析：ばね支持された梁", "md_docs_2wiki_2examples.html#autotoc_md45", null ],
      [ "非線形・HTTP・保存の例", "md_docs_2wiki_2examples.html#autotoc_md46", null ]
    ] ],
    [ "ファイル入出力・VTK", "md_docs_2wiki_2file-formats.html", [
      [ "対応形式", "md_docs_2wiki_2file-formats.html#autotoc_md48", null ],
      [ "保存して再び解析する", "md_docs_2wiki_2file-formats.html#autotoc_md49", null ],
      [ "保存用JSONの構造", "md_docs_2wiki_2file-formats.html#autotoc_md50", null ],
      [ "結果JSON", "md_docs_2wiki_2file-formats.html#autotoc_md51", null ],
      [ "V0の構造ファイル <tt>.fem</tt>", "md_docs_2wiki_2file-formats.html#autotoc_md52", null ],
      [ "独自形式 <tt>.fw3</tt>", "md_docs_2wiki_2file-formats.html#autotoc_md53", null ],
      [ "VTKで変位と端力を可視化する", "md_docs_2wiki_2file-formats.html#autotoc_md54", null ]
    ] ],
    [ "はじめに：片持ち梁を解析する", "md_docs_2wiki_2getting-started.html", [
      [ "1. 実行環境を用意する", "md_docs_2wiki_2getting-started.html#autotoc_md56", null ],
      [ "2. モデルをJSONで保存する", "md_docs_2wiki_2getting-started.html#autotoc_md57", null ],
      [ "3. 解析して結果を読む", "md_docs_2wiki_2getting-started.html#autotoc_md58", null ],
      [ "4. 結果を保存する", "md_docs_2wiki_2getting-started.html#autotoc_md59", null ],
      [ "次に試すこと", "md_docs_2wiki_2getting-started.html#autotoc_md60", null ]
    ] ],
    [ "材料非線形解析", "md_docs_2wiki_2nonlinear-analysis.html", [
      [ "モデル化の範囲", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md68", null ],
      [ "入力するのは断面の関係", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md69", null ],
      [ "骨格曲線の設定", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md70", [
        [ "正負非対称の骨格", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md71", null ]
      ] ],
      [ "載荷の順序を指定する", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md72", null ],
      [ "負勾配を変位制御で追跡する", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md73", null ],
      [ "実行例：曲げ・除荷・反転", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md74", null ],
      [ "収束と履歴の扱い", "md_docs_2wiki_2nonlinear-analysis.html#autotoc_md75", null ]
    ] ],
    [ "Python API", "md_docs_2wiki_2python-api.html", [
      [ "Pythonだけで片持ち梁を作る", "md_docs_2wiki_2python-api.html#autotoc_md77", null ],
      [ "モデルを作る操作", "md_docs_2wiki_2python-api.html#autotoc_md78", null ],
      [ "解析・結果の操作", "md_docs_2wiki_2python-api.html#autotoc_md79", null ],
      [ "ファイルを読んでから組み立てる場合", "md_docs_2wiki_2python-api.html#autotoc_md80", null ],
      [ "状態の管理", "md_docs_2wiki_2python-api.html#autotoc_md81", null ]
    ] ],
    [ "クイックリファレンス", "md_docs_2wiki_2quick-reference.html", [
      [ "解析を選ぶ", "md_docs_2wiki_2quick-reference.html#autotoc_md83", null ],
      [ "入力名の対応", "md_docs_2wiki_2quick-reference.html#autotoc_md84", null ],
      [ "支持・荷重", "md_docs_2wiki_2quick-reference.html#autotoc_md85", null ],
      [ "結果にアクセスする", "md_docs_2wiki_2quick-reference.html#autotoc_md86", null ],
      [ "単位・符号の要点", "md_docs_2wiki_2quick-reference.html#autotoc_md87", null ],
      [ "困ったとき", "md_docs_2wiki_2quick-reference.html#autotoc_md88", null ]
    ] ],
    [ "結果の読み方", "md_docs_2wiki_2results.html", [
      [ "PythonとJSONの違い", "md_docs_2wiki_2results.html#autotoc_md90", null ],
      [ "線形静解析の主な項目", "md_docs_2wiki_2results.html#autotoc_md91", null ],
      [ "節点変位と支点反力", "md_docs_2wiki_2results.html#autotoc_md92", null ],
      [ "梁の端力", "md_docs_2wiki_2results.html#autotoc_md93", [
        [ "分割前の部材との対応", "md_docs_2wiki_2results.html#autotoc_md94", null ],
        [ "釣合いによる端力回復", "md_docs_2wiki_2results.html#autotoc_md95", null ]
      ] ],
      [ "材料非線形の段階結果", "md_docs_2wiki_2results.html#autotoc_md96", [
        [ "応答曲率", "md_docs_2wiki_2results.html#autotoc_md97", null ],
        [ "収束履歴", "md_docs_2wiki_2results.html#autotoc_md98", null ]
      ] ],
      [ "シェルの結果", "md_docs_2wiki_2results.html#autotoc_md99", [
        [ "旧シェル比較形式との違い", "md_docs_2wiki_2results.html#autotoc_md100", null ]
      ] ],
      [ "ソリッドの応力・ひずみ", "md_docs_2wiki_2results.html#autotoc_md101", null ],
      [ "固有値解析の結果", "md_docs_2wiki_2results.html#autotoc_md102", null ],
      [ "静解析の微小補正", "md_docs_2wiki_2results.html#autotoc_md103", null ]
    ] ],
    [ "解析ワークフロー", "md_docs_2wiki_2workflow.html", [
      [ "入力から結果まで", "md_docs_2wiki_2workflow.html#autotoc_md105", [
        [ "1. 入力の変換とケース選択", "md_docs_2wiki_2workflow.html#autotoc_md106", null ],
        [ "2. 部材の分割", "md_docs_2wiki_2workflow.html#autotoc_md107", null ],
        [ "3. 自由度と行列の組立", "md_docs_2wiki_2workflow.html#autotoc_md108", null ],
        [ "4. 解析", "md_docs_2wiki_2workflow.html#autotoc_md109", null ],
        [ "5. 後処理", "md_docs_2wiki_2workflow.html#autotoc_md110", null ]
      ] ],
      [ "共通ソルバーAPI", "md_docs_2wiki_2workflow.html#autotoc_md111", null ],
      [ "コールバックで確定段階を受け取る", "md_docs_2wiki_2workflow.html#autotoc_md112", null ],
      [ "状態と結果の独立性", "md_docs_2wiki_2workflow.html#autotoc_md113", null ],
      [ "実装への案内", "md_docs_2wiki_2workflow.html#autotoc_md114", null ]
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
"classfem_1_1elements_1_1loaded__bar__element_1_1LoadedBarElement.html#ae1daaa3e3a365ea336a9eb39349a0640",
"classfem_1_1model_1_1FemModel.html#a5c60138b51d8f88d6601ad1c653bb30c",
"classfem_1_1vtk__writer_1_1VTKWriter.html"
];

var SYNCONMSG = 'クリックで同期表示が無効になります';
var SYNCOFFMSG = 'クリックで同期表示が有効になります';