# PQ-00〜02 実装・検証記録

実施日: 2026-09-09

基準HEAD: `ac2da54fcb612833e019317164e709d60bfd0054`
対象: `PQ-00` 原資料ハッシュ、`PQ-01` 固有値解析、`PQ-02` 公開変位後処理

## 環境

- Windows 11 `10.0.26200`
- Python `3.13.11`
- NumPy `2.4.1`
- SciPy `1.17.0`
- Node.js `v24.13.0`
- 依存関係は `uv run --locked` で固定した。

開始時の作業ツリーはcleanだった。実装中に材料非線形の変位制御に関する別変更が
`solver.py`、`model.py`などへ追加されたため、その差分は保持した。本記録の固有値変更とは
メソッド領域を分け、検証結果では同時に存在する状態を明記する。

## PQ-00: 原資料ハッシュ

45サンプルのうち35件で、manifestおよび参照修復報告の期待値が、WindowsのCRLF作業ツリー、
LFへ正規化した内容、現行Git blobのいずれとも一致していなかった。修復時に作業ツリーの
生バイトを記録した後、同じ修復系列でサンプル内容が更新されたことが原因である。

原資料の識別規則を `sha256_utf8_lf` とした。これはUTF-8テキストのCRLFと単独CRをLFへ
正規化した後のSHA-256であり、現行Git blobと一致する。`.gitattributes`にも対象データ、
oracle、旧ソース、JSON報告のLFを宣言し、旧ソース内のPDF・PNG・XLSXはテキスト変換から
除外した。manifestと35件の修復記録は、構造回帰323ケースが
成功した現行Git内容および現行独立ソースから再計算した。`original_sha256`は履歴値として変更していない。

## PQ-01: 固有値解析

- `run_modal_analysis(n_modes)`を`analysis_params`へ一時反映し、呼出し後に元の値を復元する。
- モード数を正の整数として検査し、利用可能数を超える要求を無言で減らさない。
- 拘束自由度を剛性・質量行列の両方から同じ`DofLayout`で除き、支持ばねを剛性へ加える。
- 質量を持たない自由度、全拘束、質量なしを明示エラーにする。
- ARPACK再試行は`which="SA"`から`sigma=0, which="LM"`の低次選択へ移り、高次側へ切り替えない。
- 行列尺度に基づく微小負値だけをゼロとし、有意な負値は不安定性として拒否する。
- 固有ベクトルを質量正規化して全DOFへ復元し、固有対相対残差と質量直交性誤差を返す。
- ゼロモード周期はPythonで`inf`、HTTP・保存用JSONで`null`とする。

この時点の残件は、実シェル・ソリッド要素の独立参照比較と、大規模な実構造モデルに対する
ARPACK再試行の評価だった。これらは後続の[PQ-01実装・検証報告](product-quality-pq01.md)で
完了した。

## PQ-02: 公開変位後処理

`ResultProcessor.process_displacement()`を`DofLayout`へ接続した。3DOFソリッドでは並進3成分を
各節点へ割り当て、互換スキーマの回転3成分だけを0とする。入力配列そのものが不足・過剰・
非有限の場合はゼロ埋めせず拒否する。6DOF、3DOF、混在、非連続節点IDと登録順を試験した。

## 検証結果

- RED基準: 新規14件のうち13失敗、既存6DOF形状の1件だけ成功。
- 修正後集中試験: 43成功。
- Wiki検査: 13ページ、Python 18ブロック、JSON 17ブロック、実行例18件すべて成功。
- 修正前全件: `1931 passed, 5 failed, 0 skipped`、`1330.51s`。
  5失敗はすべてPQ-00ハッシュ検査。JUnitは`tmp/product-quality-pq01-pq02-junit.xml`。
- PQ-00修正後のharness検査: 19成功。
- 修正後全件: `1960 passed, 0 failed, 0 skipped`、終了コード0、`1353.89s`。
  JUnitは`tmp/product-quality-pq00-pq02-green.xml`。PQ-00の全件ゲートを完了とする。

その後、`ebd64ea`だけをcheckoutした別worktreeでは`1924 passed, 2 failed`となった。
2件はいずれも`sampleBendTetra1.fem`の固定SHA-256を生バイトで比較していたため、Windowsの
既存CRLF作業コピーとLF checkoutで値が変わる問題だった。また、`docs/v0/** text eol=lf`が
PDF・PNG・XLSXにも適用され、checkout直後から6バイナリがdirtyになることも確認した。
固定原本ハッシュをLF正規形で比較し、3種のバイナリを`-text`へ分離した。

最終確認は`aedbd26b62607e11b2e80744e449518da567db0e`にPQ-01/PQ-03/PQ-05と上記修正だけを
重ねたclean worktreeで実行した。Python 3.13.11で`1972 passed, 0 failed, 0 skipped`、
終了コード0、`1134.23s`だった。JUnitは検証worktree内の
`tmp/product-quality-current-clean.xml`へ保存した。

`ruff`は環境に実行ファイルがないため未実行。変更Pythonは`py_compile`で構文確認した。
