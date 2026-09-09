# テストの実行と追加先

テストは開発段階ではなく、保証する機能で分類する。[項目表](../docs/plans/test_items.md)は保証範囲、[現行台帳](../docs/plans/test_inventory.md)は全テストの配置とケース数を示す。

## 実行

リポジトリ直下で実行する。基準環境は Windows・Python 3.13.11・Node.js 24。Python の依存は `uv.lock` で固定し、独立参照に使う SymPy も含める。

```powershell
uv sync --locked --extra dev
uv run --locked --extra dev python -m pytest
uv run --locked --extra dev python -m pytest -m material_nonlinear
uv run --locked --extra dev python -m pytest tests/elements/beam/test_elastic.py
uv run --locked --extra dev python -m tools.validation.check_test_results
```

現在の既知失敗は0件で、全件実行の成功条件はpytest終了コード0。最後のコマンドは全件を実行し、保存サンプルの失敗 ID・数値不一致件数・例外理由を [既知失敗](regression/known_failures.json) と照合する。新規失敗、別の失敗理由、skip、収集不足、解消した失敗を検出すると照合も失敗する。解消時は原因を確認して基準を更新する。基準に失敗が残る場合、照合成功を全テスト成功と呼ばない。

2026-09-09の全件実行は1,886件中1,863成功・23失敗。その後ユーザーが `bar/3D_Sample01` の支持を変更し、残る23件も成功した。最新の検証は対象モデルと関連118件が成功、全体の収集は1,911件。[支持変更後の検証記録](../docs/report/general-fem-support01-20260909.md)を参照。

ログ、JUnit、生の pytest 終了コード、環境、入力 hash は `tmp/test-results/` に残る。[CI](../.github/workflows/tests.yml) は材料非線形の成功と全件の基準一致を別 job で確認し、生の結果を artifact に保存する。

## 追加先

| 配置 | 所有する保証 |
|---|---|
| `elements/beam`, `shell`, `solid` | 要素単体の行列・補間・荷重・剛体運動・精度 |
| `materials` | 材料骨格・履歴・接線・状態管理・入力拒否 |
| `solvers` | 境界処理・線形精度・Newton・残差・復元 |
| `io` | 入力解釈・保存読込・HTTP・エラー・モデルの再利用 |
| `postprocess` | 物理的な応力・合力・両面テンソル・端力回復 |
| `integration` | 組立後の解析解・荷重経路・出力と ID の対応 |
| `regression` | 登録した保存サンプルの各ケース／全履歴 |
| `validation` | 期待値・原資料・独立参照計算自体の信頼性 |
| `harness` | 比較器・runner・修復ガード・監査・CI 判定の正しさ |
| `support/builders` | 新しい可変入力・製品要素・モデルを作る関数 |
| `support/oracles` | 製品コードに依存しない式・原演算子による期待値 |
| `support/assertions.py` | 再帰比較・期待値との照合。製品解析を呼ばない |
| `support/repairs` | 入力同一性を検証する純粋な修復関数 |

`unit / integration / regression / oracle` のいずれかを主 marker にする。`material_nonlinear` は検証済みの範囲指定（現在 857 ケース、応答曲率出力37ケースと共通ソルバー契約21ケースを含む）であり、単に材料非線形を扱う全テストという意味ではない。`slow` と `requires_node` は資源条件を表す。依存がない環境で自動的に skip しない。たとえば `-m "not requires_node and not slow"` は明示的な部分実行になる。

名前は `test_<機能・振る舞い>` にする。`phase4`、`v0` など時期・当時の内部呼称を新しいテスト／補助モジュール名に使わない。`test_*.py` 同士を import しない。製品 import は wheel と同じ `fem` / `app` に統一し、sys.path 操作は各テストに置かない。

## サンプルと期待値

[manifest](data/manifest.json) が唯一の収集対象一覧。45 モデルのうち一般 FEM 44 モデルは荷重／参照の和集合 323 ケースを個別に実行する。片持ち梁の 1 モデルは 100 載荷段階を 1 回の解析で確認し、別途ゼロ段階を含む保存 101 段階すべてを比較する。未登録 JSON は回帰へ自動追加せず、収集整合性の試験が登録漏れを指摘する。

保存値の再帰比較は `rel=1e-8, abs=1e-10`。片持ち梁の独立 float 式は `rel=1e-6, abs=1e-9`、保存値はさらに厳密比較する。Decimal 50 桁、有理数の面圧解、各パッチの個別閾値を一つの緩い許容値へまとめない。現在値から期待値を作らない。

`docs/v0/` は原資料の保管先として当時の名前と hash を保持する。過去 report の名前・数値・hash も当時の証拠。これらは新しいテストの名前付けとは分け、[移行記録](../docs/report/test-reorganization.md)から現在の入口をたどる。外部の未追跡 SNAP 資料は通常テストの成否条件にしない。

## 手動ツール

```powershell
uv run --locked --extra dev python -m tools.validation.audit_samples --output tmp/sample-audit.json
uv run --locked --extra dev python -m tools.validation.audit_cantilever --output tmp/cantilever-audit.json
uv run --locked --extra dev python -m tools.validation.audit_solid_supports --output tmp/support-audit.json
uv run --locked --extra dev python -m tools.validation.check_cantilever_reference --check
uv run --locked --extra dev python -m tools.debug.execute_model tests/data/snap/beam001.json --output-dir tmp/model-output
```

`tools/debug/execute_model.py --gui` は Flet 画面を開く。手動出力の既定先は `tmp/model-output`、解析種別は入力の指定に従う。VTK 生成例は [docs/examples](../docs/examples/) に保管し、pytest の検証証拠とは扱わない。

`tools/migrations/` は原資料による参照修復や完了済みスキーマ移行の再現用。通常は検証だけを行い、`--write` を明示したときだけ対象を書き換える。通常テストや CI から書込モードを呼ばない。過去の修復と新規不具合の修正は[一般 FEM の別計画](../docs/plans/general-fem-followup.md)で管理する。
