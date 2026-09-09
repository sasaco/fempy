# 線形・材料非線形ソルバー統合の実施記録

2026-09-09。[計画](../plans/solver-unification.md)の第1〜5段階を完了。最終全件照合は既知失敗基準と一致し、新規回帰なし。

## 開始基準と保持した変更

開始HEADは `a3a1fd1a59aa1a79151c808261f3f4fab1f72c6b`。開始時の `git status --short` とHEADに対する全差分を `tmp/solver-unification/initial-status.txt`、`initial.patch` に保存した。
曲率出力、beam001の保存参照・Decimal監査、テスト台帳等の既存変更を含む作業ツリーが基準。これらを取り消さず、材料要素の定式化、保存期待値、許容誤差、既知失敗基準は本作業で変更していない。

既存参照を調査した範囲は `Solver`、`NonlinearSolver`、`FemModel.run()`／後処理、`main.py` の例外importとHTTP出力、ソルバー公開状態を読む試験。`FemModel.nonlinear_solver.displacement` の既存参照を維持した。

## 実装した責務

| 所有先 | 変更と保持した契約 |
|---|---|
| `src/fem/solver.py` | 静解析の共通入口・入力検証・状態初期化・荷重ステップ・確定・保存・コールバック。種別から参照弾性の直接解法／材料応答のNewtonを選択。通常staticは1ステップ。荷重数から材料則を推測しない |
| `src/fem/dof.py` | 3/6DOF判定、昇順の外部ID対応、要素DOFのキャッシュ、通常行列・ベクトル加算。剛性・質量・接線・内力・分布荷重・面圧・補償付き組立・後処理が同じ対応を利用。荷重組立前にも単独利用可能 |
| `src/fem/equilibrium.py` | 直接解法とNewton反復を抽出。直接代数は対角スケーリング、補償積、4回の反復改良、数値ランク・釣合い検査。構成則による最大16回の精度補正と二次ソリッド補償も保持。Newtonは行最大値スケーリング、数値ランク、残差基準、24候補のバックトラックを保持。代数関数は解・補正／増分を返し、確定変位を更新しない |
| `src/fem/solver_results.py` | 共通の受理状態スナップショットと最終結果変換。内部変位は6キー、従来の非線形3DOF出力だけ境界で3キーへ射影。staticの外部結果へstep_resultsや曲率を追加しない |
| `src/fem/nonlinear/nonlinear_solver.py` | 継承を委譲へ変更した互換ファサード。旧import、既定値、solve_nonlinearの位置引数、例外型・step/load_factor/displacement、状態の読み書きを保持。solveも非線形へ委譲。組立・反復・整形の独自実装なし |
| `src/fem/model.py` | 両静解析をself.solverへ接続。nonlinear_solverは同じ状態の互換ビュー。端力を最後の受理スナップショットから取り、要素を再評価しない。既存の梁端力回復・シェル後処理を維持し、最終結果への辞書／配列のaliasをdeepcopyで解消 |

支持ばね力と拘束DOFへの残差射影を共有し、Newtonの収束判定と探索候補は同じ釣合いを使用する。境界消去へ渡す残差は要素内力だけを差し引いた値とし、支持ばねを二重減算しない。線形の精度補正では既存の `fsum` による二成分のばね力評価を保持した。

各解析で既存要素の `reset_states()` を使い、解析種別切替や失敗後再解析の履歴残留を防止する。未収束時は最後の変位、内力、要素履歴、要素荷重係数に戻し、後続ステップへ進まない。コールバック例外は確定済みの状態を保持して伝播する。コールバックへコピーを渡すため、その書換えは保存結果に影響しない。

`modal` は独立した一般化固有値問題のまま、共通DOF・剛性・質量組立を使用する。ペナルティ境界、既存ARPACK設定・フォールバックを変更していない。線形の空かつ無荷重の回転DOF除去を維持し、Newtonへの拡張はしていない。

## 検証コマンドと生の結果

全コマンドはリポジトリ直下、`uv run --locked --extra dev python` を使用。実行環境はPython 3.13.11、依存バージョン・Nodeバージョン・入力hashは各 `baseline-comparison.json` に保存。

| 検証 | コマンド末尾 | 生の結果 |
|---|---|---|
| 変更前の全件 | `-m tools.validation.check_test_results --output-dir tmp/solver-unification/before` | pytest **1,179成功・308失敗**、終了1。照合終了0、既知失敗と一致 |
| 新規入口の実装前 | `-m pytest tests/solvers/test_unification.py -q --tb=short --junitxml=tmp/solver-unification/red.xml` | **9失敗・2成功**。未実装入口・旧solve・独立組立・共通状態でRED |
| 初期抽出の関連範囲 | `-m pytest tests/solvers tests/io/test_model_lifecycle.py tests/integration/test_curvature_output.py -q --tb=short` | **74成功** |
| 初期材料非線形 | `-m pytest -m material_nonlinear -q --tb=short --junitxml=tmp/solver-unification/material.xml` | **847成功・651対象外**、終了0 |
| 初期全件照合 | `-m tools.validation.check_test_results --output-dir tmp/solver-unification/after` | pytest **1,190成功・308失敗**、終了1。照合終了0、新規失敗なし |
| 直接代数の状態分離後 | `-m pytest tests/solvers tests/integration/test_beam_precision.py tests/elements/solid tests/integration/test_solids.py -q --tb=short` | **89成功** |
| 追加契約の最終単独実行 | `-m pytest tests/solvers/test_unification.py -q --tb=short` | **21成功** |
| 最終材料非線形 | `-m pytest -m material_nonlinear -q --tb=short --junitxml=tmp/solver-unification/material-final.xml` | **857成功・651対象外**、終了0、55.90秒 |
| 最終全件照合 | `-m tools.validation.check_test_results --output-dir tmp/solver-unification/final` | pytest **1,200成功・308失敗**、終了1、259.56秒。照合終了0、既知失敗と一致 |

最終 `summary.json` では、変更前1,487ケースの成否がすべて不変、追加21ケースが成功、全入力hash不変、beam001の監査JSON全体が同一であることを照合した。`git diff --check` は終了0。生ログ・JUnit・照合JSONは `tmp/solver-unification/` に保存した。

新規21ケースは `tests/solvers/test_unification.py`（16関数）。直接解法とNewton各々の軸力手計算、非線形1ステップの三次方程式の根、Newtonを通らないstatic、3/6混在・飛び番・入力順・再利用、強制変位と旧／明示ばねの釣合い、例外・荷重係数・履歴復元、旧API、スナップショット独立性、コールバック、読み取り回数、一般化固有値の既知解を追加した。

既存の材料・梁・シェル・ソリッド・圧力・HTTP・保存読込・全保存段階の試験は同じ期待値と閾値で実行。skip追加や保存期待値の更新による成功扱いは行っていない。既知失敗照合成功と全テスト成功は異なる。

## beam001の変更前後と独立参照

開始HEADのsrcだけを `git archive` から `tmp/solver-unification/baseline-source` に展開し、開始時patchのsrc変更を適用して変更前のソースを復元した。そのsrcを優先して同じ監査ツール・同じ入力で再計算した。作業ツリーの製品コードや保存参照を差し替えていない。

変更前は `tmp/solver-unification/cantilever-before.json`、変更後は次のコマンドで `cantilever-after.json` を保存。

```powershell
uv run --locked --extra dev python -m tools.validation.audit_cantilever --output tmp/solver-unification/cantilever-after.json
```

| 100載荷段階の独立参照に対する最大絶対誤差 | 変更前 | 変更後 |
|---|---:|---:|
| 節点変位 | 6.505213034913027e-19 | 6.505213034913027e-19 |
| 要素端力 | 3.510436386022775e-7 | 3.510436386022775e-7 |
| 支点反力 | 2.056594894384034e-10 | 2.056594894384034e-10 |

最終先端dxは両方 `0.0009626976156053463`、rzは `9.804068632075476e-5`。これは現行の小変位・中央断面モデルの数学的参照との照合であり、外部JRソフトとの一般的な一致を意味しない。別途既存回帰はゼロ段階を含む101保存段階とDecimal参照を検証する。

## 完了判断と互換境界

静解析入口・荷重ステップ・結果保存は一つになり、旧非線形クラスに組立・DOF判定・反復・整形ループは残っていない。数値方針や材料定式化の統一は行っていない。互換ラッパーの削除、線形の公開複数ステップ、幾何学的非線形、固有値アルゴリズム、一般FEMの既知308件の修復は対象外。

最終全件照合まで確認し、計画の実施済み項目を完了とした。一般FEMには従来どおり308件の既知失敗が残る。
