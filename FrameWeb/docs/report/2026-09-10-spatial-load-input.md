# 面荷重の実装着手: 定義・入力契約

日付: 2026-09-10

対象計画: [面荷重実装計画](../plans/面荷重実装計画.md)

## 開始状態

- HEAD: `4fed5ad1afb7635dd8daf970855a4410d7ca14fc`
- 作業ツリー: 未コミット変更なし。
- 環境: Windows、Python 3.13.11、pytest 9.0.2。既存 `.venv` / `uv.lock` を使用。
- 変更前全件テスト: 2026件を収集。90％超まで失敗表示なしだったが、実行セッションが失われたため最終終了コードは未確認。変更前全件成功とは扱わない。

## 計画に反映した判断

- シェルがない梁モデルへ荷重を配分する用途を維持し、明示載荷三角形の頂点を既存構造節点IDに固定する。
- 二本のラインの向き、正規化弧長による対応、横断補間式、交差・反転拒否を定義する。
- Q4だけでなく荷重補間も含めて求積精度を決める。affine Q4への制限だけでは二次面求積を保証できない。
- 旧シェルIDと内部要素IDを区別する。非凸・穴は後続段階、許容値は次元ごとに扱う。
- 要素節点の釣合い力と、既存シェルの応力由来出力を区別する。節点荷重を受ける梁へ固定端力を重複追加しない。

## 実装した範囲

`src/fem/spatial_loads/` に不変な定義型、基本入力検証、旧形式・新形式の入力アダプタを追加した。
入力辞書、既存節点荷重、構造メッシュを空間荷重変換で書き換えず、未展開定義を
`BoundaryCondition.spatial_loads` に保持する。`FemModel.set_spatial_loads()` は参照検証に成功した場合だけ定義を置き換える。

旧形式では選択済みの一ケースだけを取り込み、参照されない他ケース・パネル・ラインは読み込まない。
旧 `inf_panel.elements` は旧シェルID専用マップを使い、新形式の保存値は内部要素IDとする。
梁だけのパネルは `triangles` を追加して受け入れ、接続情報がない点群は無制約三角形分割しない。

JSONの保存・再読込で定義を保持する。`.fw3` は定義を失うため、書込対象を開く前に拒否する。
通常入力の保存形式には空の `spatial_loads` キーを追加しない。境界条件のクリアとモデル再読込で荷重定義も消去する。

入力受付は解析機能の完成を意味しない。荷重組立の接続まで、空間荷重を持つ `static` / `material_nonlinear` /
`modal` は解析前に `unsupported_analysis` とパネルID・荷重IDを返す。HTTPも同じ診断を返し、荷重を無視して成功しない。

## 検証

| 実行内容 | 結果・生の終了コード |
|---|---|
| `uv run --locked --extra dev python -m pytest`（変更前） | 2026件収集、終了コード未確認 |
| 定義パッケージ追加前の新規2テストファイル | `ModuleNotFoundError`、終了1（未実装検出） |
| 読込接続前の `tests/io/test_spatial_loads.py` | 17 failed / 1 passed、終了1（未実装検出） |
| `uv run --locked --extra dev python -m pytest tests/spatial_loads tests/io/test_spatial_loads.py tests/solvers/test_capabilities.py -q --tb=short` | 70 passed、終了0 |
| `uv run --locked --extra dev python -m pytest tests/io tests/solvers/test_capabilities.py tests/integration/test_input_routes.py tests/harness/test_suite_contracts.py -q --tb=short` | 156 passed、終了0（境界クリア等の追加3件より前） |
| `uv run --locked --extra dev python -m tools.validation.render_capabilities` | 生成済み文書とレジストリが一致、終了0 |
| `git diff --check` | 終了0（CRLF変換の警告のみ） |
| `.venv/Scripts/python.exe -m tools.validation.check_test_results --output-dir tmp/spatial-loads-input` | 2082 passed（新規56件を含む）、1428.58秒、生のpytest終了0。既知失敗0、基準照合一致 |

変更後全件検証のログ・JUnit・基準照合結果は `tmp/spatial-loads-input/` に保存した。
`baseline-comparison.json` の `raw_pytest_exit_code=0`、`baseline_matches=true`、`known_failures=0`、
`issues=[]` を確認した。既存保存期待値・既知失敗リスト・テストのskip設定は変更していない。
今回の変更は入力処理までなので、合力・一次モーメント・変位等の数値誤差はまだ評価していない。
不変性、参照、JSON往復の比較は定義値の完全一致で検証した。

## 残作業

- 第0段階: 独立した合力・モーメント・梁／板応答の閉形式期待値と受入試験への対応付け。
- 第2段階: 平面性、外周の連結・凸性、ライン交差、セル対応、クリッピング、求積と収束判定。
- 第3段階: 構造DOFへの荷重組立、保存性監査、荷重寄与の解析結果への受け渡し。
- 第4段階: 解析出力の数値検証、静解析での有効化、公開利用文書。

第1段階の入力処理を実装した状態であり、面荷重解析の初回リリース完了とはしない。
