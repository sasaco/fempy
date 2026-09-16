# テスト整理の実施記録

2026-09-09。基準コミット `c42ee68aab41a081494345c15af1c3bac09860f8`。[承認済み計画](../plans/test-reorganization.md)に沿って整理した。現在の入口は [tests/README](../../tests/README.md)、保証範囲は [項目表](../plans/test_items.md)、全テストは [現行台帳](../plans/test_inventory.md)。

## 結果

| 状態 | ファイル／関数 | 展開ケース | 成功 | 失敗 | error / skip |
|---|---|---:|---:|---:|---|
| 整理前 | 38 / 264 | 1,151 | 1,116 | 35 | 0 / 0 |
| 責務別へ移動後 | 45 / 264 | 1,151 | 1,116 | 35 | 0 / 0 |
| 重複・責務統合後 | — | 1,158 | 1,123 | 35 | 0 / 0 |
| 最終 | 46 / 242 | 1,450 | 1,142 | 308 | 0 / 0 |
| 材料非線形の完了範囲 | 最終構成から選択 | 799 | 799 | 0 | 0 / 0 |

移動前後の 1,151 ケースは旧→新 ID を対応づけ、全件の成否一致を確認した。整理前の `DummyElement` 相当クラスの収集警告も解消した。

一般 FEM の 44 ファイルを内部の 323 荷重／参照ケースへ展開したため、失敗の数え方が変わった。移動前に再採取した全ケース監査は 15 一致・238 不一致・70 エラー。最終の 308 失敗はその 238+70 ケースと一致し、数値不一致件数・例外理由も一致した。新規の解析不具合や期待値変更によって数を合わせていない。

ケース増減は、重複・混在の統合で +7、保存サンプルの 44→323 展開で +279、テスト構成と CI 判定を守る管理試験で +13。合計 1,151+7+279+13=1,450。ファイル数の削減を目的に責務の違う試験を混ぜていない。

## 配置・名前・依存

- 開発段階名・内部版名を含むテスト／補助／実行スクリプト名を廃止した。原入力は `source_input`、原演算子は `source_solid` / `source_triangle`、片持ち梁履歴は `cantilever_history` のように責務を示す。
- 比較器、実測出力の section-cut 変換、解析 runner、出典検証を分離した。独立 float / Decimal / 有理数 / 原 JavaScript 演算子を維持し、製品の計算を期待値側へ持ち込んでいない。
- テスト間 import を廃止し、生成関数を `support/builders`、独立式を `support/oracles`、修復ガードを `support/repairs` へ移管した。比較器と oracle の製品への依存禁止は推移的な import 検査で確認する。
- 製品 import を `fem` / `app` に統一した。モデルと HTTP が同じクラスを使うこと、1 回の monkeypatch で両経路の後処理失敗を検出できることを確認した。pytest のパスと importlib 設定を集約した。
- 修復・監査・手動実行を `tools` へ移した。監査の既定出力は tmp。原資料との照合は相対／絶対パスでも同じ出典を保持する。Flet の手動入口も残し、生成 JSON/VTK の出力先を明示した。

原資料アーカイブ `docs/v0/`、過去 report の名前と hash、保存サンプルの識別名は出典として保持した。製品の `fem.v0_io` は今回のテスト整理で改名せず、試験側では原入力という責務で扱う。新しい試験名や CLI 名にこれらの開発履歴を引き継いでいない。

## 統合・廃止した保証の移管先

| 旧保証 | 最終の所有先・維持した条件 |
|---|---|
| 梁の軸剛性 2 成分と `K @ u` の端軸力 | `elements/beam/test_elastic.py::TestBarElement::test_axial_stiffness_and_end_forces`。元の E/G/A/断面、長さ、2 節点変位、行列サイズと両端符号を維持。旧 `test_boundary_conditions` は実態どおり回転剛性の対称性へ改名 |
| 基底の未実装 3 API | `elements/test_base.py::test_unimplemented_geometry_contract`。体積・形状関数・導関数の 3 呼出しと NotImplementedError を保持 |
| Tri/Quad・Tetra/Hexa/Wedge の基本クラス重複 | 各 `test_kinematics.py` / `test_linear.py` のパラメータ化。元の材料、節点座標、評価点、サイズ、固有積分点／重み、四面体体積を保持。旧 unittest の 7 桁比較相当を緩和せず、行列の既定 allclose も維持 |
| 面圧の非ゼロ確認、単位正方形の等価荷重、三角形の符号・比例 | `elements/shell/test_pressure.py`。2×3 図形と元の単位 Tri/Quad、元の材料・厚さ、7/100/1000/10000 の圧力、F1/F2、回転、全節点値、総力、総モーメント、逆面符号を明示。無効種類・空配列・面未指定等の拒否を維持 |
| 旧 A を厚さとして扱う 2 試験 | `io/test_structural_input.py::test_shell_area_field_supplies_thickness`。ケース省略時の .2 と選択ケース 2 の .3、DKT、入力不変、A=0 拒否を保持 |
| Hexa1/Wedge1 の重複した製品解析 | `validation/test_solid_sources.py::test_solid_displacements_and_global_equilibrium`。5 原資料モデルの全変位と力／モーメントを一つの所有先へ。一次の厳しい許容値 1e-8/1e-7、二次の 1e-7/1e-5 を保持 |
| 修復と製品比較が混在した大規模ソリッド試験 | 製品解は `validation/test_solid_sources.py`、入力不変・既存キー・冪等性は `harness/test_source_repairs.py`。Tetra の全出力／入力保持も分離。独立 Decimal 全出力比較は別の保証として維持 |
| 無条件 glob と内部ケースをまとめた runner | 明示 manifest と `regression/test_structural_samples.py::test_saved_sample_case`。後続ケースが最初の失敗で隠れない。片持ち梁はゼロ段階と全履歴を独立に照合 |
| テスト直下の CLI・4 VTK | CLI は `tools`、生成 VTK は `docs/examples`。旧 CLI の互換ラッパーは残さず、現在の実行ガイドに入口を一本化 |

JR の材料／要素／全解析の履歴、未履歴と載荷後の接線、独立ばねと実 JR の失敗復元、片持ち梁の float/Decimal/保存 101 段階/step 62 改変/自由端回復は廃止していない。比較許容値も統一・緩和していない。

## 実行・データの管理

45 サンプルのモデル ID、ケース／段階、参照、出典 hash、単位・符号・位置、許容値、GF 残件を [manifest](../../tests/data/manifest.json) に登録した。元の JSON と FEM/NDT/NDU、製品 `src` と `main.py` はすべてバイト不変。4 VTK も内容不変で移動した。

CI は Windows / Python 3.13.11 と追跡対象にした `uv.lock` を使う。一般 FEM は pytest 終了コード 1 と JUnit を保存し、既知失敗との照合を別に判定する。CI 定義と同じ全件判定コマンドをローカルで実行して成功した。GitHub 上の workflow 実行はこの作業では行っていない。

最終全件は約 245 秒、材料非線形選択は約 46 秒。時間はこの環境での記録であり性能基準ではない。追加確認として、比較／runner 14 ケース、構成・CI 判定 13 ケース、Decimal の保存 101 段階、片持ち梁監査、手動 JSON 出力、全 CLI の `--help` を検証した。Flet 画面の対話操作は実施していない。

詳細な 112 元ファイルの旧／新 hash、264 元関数の全移管先、1,151 ケースの移動対応、実行件数、環境・入力 hash は [証拠 JSON](test-reorganization-evidence.json) に保存した。生のログ・JUnit は `tmp/test-results/` と移行用 `tmp/test-reorganization-*.xml` に残る。

一般 FEM の 308 失敗と外部 JR 較正は [GF-01〜06](../plans/general-fem-followup.md) の別作業。旧項目表の性能・並列・可視化・将来構想を整理済みという理由で検証済みにはしていない。
