# 非線形要素の応答曲率出力

2026-09-09。ユーザーが確認した定義に従い、中央断面の M–φ 関係上の全曲率を出力する。単位は 1/m、局所 y/z 軸、弾性分と塑性分を含む。各ステップと最終結果の `curvature[要素番号]` に保存し、Python・HTTP・結果ファイルで同じ値を公開する。[出力仕様](../wiki/data-structures.md#非線形要素の応答曲率)を参照。

## 実装

- `NonlinearBarElement.calculate_curvature` は構成則と共通の中央断面演算子で全曲率を求める。JR 履歴を再評価せず、trial/committed 状態を変更しない。
- 非線形ソルバーが各収束時の変位で値を保存する。最終結果と各ステップは独立した辞書。要素番号を維持し、線形要素を除外する。
- `disg/reac/fsec` の参照比較ビューは同じ `curvature` を保持する。既存の保存期待値・入力・許容誤差はこの変更では変更しない。

## TDD と保証

実装前に新規テストを実行し、34 件が `curvature`／`calculate_curvature` の欠落で失敗、既存ケースを含む 28 件が成功した（`tmp/curvature-red.xml`）。実装後は 62 件成功。弾性曲げ軸・非線形要素なしの境界ケースを 2 件追加し、対象 64 件が成功した（`tmp/curvature-green.xml`）。追加分は合計 37 ケース・8 関数。

| 所有先 | 検証内容 |
|---|---|
| `tests/elements/beam/test_nonlinear_section.py`（追加 6） | 0.5/2/5 m、空間回転・断面角、両曲げ軸と符号、全曲率と回転角の区別、出力による履歴非変更、rollback 後の値 |
| `tests/integration/test_curvature_output.py`（追加 31） | Python/JSON/HTTP、1/4 要素、両曲げ軸、対称・非対称の独立 JR 多角形、載荷・除荷・反転・残留曲率、ステップ保存、結果ファイル、参照比較ビュー、軸ひずみ／ねじりの除外、弾性曲げ軸、静解析・対象要素なし、beam001 全 101 段階の独立仮想仕事参照 |

`beam001` の最終 z 曲率は `-0.00097115875 [1/m]`。ゼロモーメント時の残留曲率は独立 JR 多角形で検証し、単純な骨格逆算や `M/EI` で履歴応答を置き換えていない。

## 回帰検証

- 材料非線形の 834 ケース成功（58.70 秒、`tmp/curvature-material.xml`）。その後追加した境界 2 ケースも対象 64 ケースの再実行で成功。最終の材料非線形範囲は 836 ケース。
- 最終全件実行は **1,179 成功・既知 308 失敗**（260.44 秒、全 1,487 ケース）。`python -m tools.validation.check_test_results --output-dir tmp/curvature-complete` による失敗 ID・原因の基準照合は成功。新規失敗・skip・収集不足はなく、全件成功とは区別する。
- 証跡は `tmp/curvature-complete/complete.xml`、`pytest.log` および同ディレクトリの照合結果。差分の空白チェックと変更 Python の未定義名・import 検査も成功。

現行モデルの中央断面曲率を保証する。分布塑性・材端別曲率・実験較正を追加したものではない。
