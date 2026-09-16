# 面荷重の第3段階: 荷重組立と内部ソルバーへの接続

日付: 2026-09-10

対象計画: [面荷重実装計画](../plans/面荷重実装計画.md)

## 開始状態

- HEAD: `2748921595cf23872cb59c8b709bf3d45099f890`
- 作業ツリー: 未コミット変更なし。第2段階まで取り込み済み。
- 環境: Windows 11、Python 3.13.11、NumPy 2.4.1、SciPy 1.17.0、pytest 9.0.2、SymPy 1.14.0。
- 既存 `.venv` とロック済み依存関係を使用し、追加・更新なし。
- 前回の全2224件成功は[第2段階の作業記録](2026-09-10-spatial-load-geometry.md)を参照。

## 実装した責務

`src/fem/spatial_loads/assembler.py` に `compile_spatial_loads()` を追加した。
幾何準備から求積、構造節点の並進DOFへの配分までを純粋な処理として実装する。
メッシュ、入力定義、既存の節点荷重、要素荷重は変更しない。
節点IDはコンパクトな `DofLayout` に従い、非連番IDと3/6自由度の配列を扱う。
載荷三角形は既存節点への配分のみを行い、剛性や梁の固定端力を追加しない。

積分区分ごとに構造セルと荷重場の所有者を保持する `integrate_pieces()` を追加した。
全区分を一つの成分配置へ積分し、適応細分化でも所有者を引き継ぐ。
区分ごとに絶対許容誤差を与え直さず、荷重単位で推定誤差を合算する。
節点成分の絶対誤差予算は全セルの節点成分数で配分する。
T3/affine Q4の線荷重には3次、面荷重には4次まで正確な則を使い、
構造Q4または載荷帯の逆写像が非多項式になる場合は適応求積を使う。
affine判定は機械精度で行い、入力幾何許容値によって歪みを無視しない。
重みには物理長・物理面積を一度だけ含める。

節点配分とは独立に、物理的な積分点と強度から合力・原点回りモーメントを積分する。
節点荷重から再計算した値と荷重別・総和の両方で照合し、不一致ならID付きで拒否する。
`AssemblyTolerance` は力・モーメントそれぞれの絶対許容値と相対許容値を保持する。
力尺度は強度絶対値の積分、モーメント尺度はその力尺度と最大腕長から求める。
正負荷重の相殺でも尺度はゼロにならない。力・モーメントの絶対許容値はモデルの各単位に従う。

`SpatialLoadContribution` はtupleと不変dataclassでDOF荷重、荷重別監査、セル別寄与、
積分点評価数・細分化数・線長・クリップ面積・警告を保持する。
`cells` の `element_id` がシェル内部ID、`None` が載荷三角形を表す。
`panel_id`、`cell_key`、`load_id` を残すため、複数パネルから同一シェルへの加算も追跡できる。
載荷三角形の `cell_key` はパネル内の0始まり番号。警告欄は現行の拒否方針では空となる。

`Solver.assemble_load_vector()` は呼出しごとに一度コンパイルし、通常荷重へ加算する。
内部静解析は空間荷重の検証・求積・保存性監査を剛性行列組立前に完了させる。
高精度梁解析の `linear_precision.solve_precise_frame()` は外力をDecimalで再構築するため、
その経路にもコンパイル済み節点荷重を一度だけ加算する。
非線形・モード解析は、内部ソルバーを直接呼んだ場合もID付きで事前拒否する。

結果には独立した `spatial_load_contribution` 辞書を渡す。
シェルの要素節点釣合い力は別キー `element_nodal_equilibrium_forces` とし、
要素IDごとに `node_ids` と `forces`（節点ごとの全体座標 `[Fx,Fy,Fz,Mx,My,Mz]`）を保持する。
値は `K_e*u_e-f_e` で、変位の補正値と既存の直接 `pressure` 寄与も含める。
通常の節点荷重と載荷三角形荷重はこの直接シェル荷重 `f_e` に含めない。
全体反力との照合ではそれらの節点外力も別途差し引く。
既存のシェル応力・断面合力・辺の力は変更しない。

再解析開始時と失敗時はコンパイル結果、合成外力、Solver内の空間荷重スナップショットを破棄する。
返却済み結果は独立しており、再解析や返却辞書の編集がコンパイラ状態へ戻ることはない。
空間荷重を除いた解析には新しい結果キーを追加しない。

低レベルの形状関数と自然座標微分を `src/fem/shape_functions.py` へ共通化した。
シェルのJacobian計算は共有微分を使い、空間荷重の物理求積には重ねて掛けない。
既存面圧の `F1/F2`、法線・符号・保存形式は保持する。

## 受入項目と試験

新規115件: `tests/spatial_loads/test_assembly.py` の89件と、
`tests/integration/test_spatial_loads.py` の26件。

| 保証 | 主な試験 |
|---|---|
| T3/Q4の定数・一次・双一次・ゼロ・反転 | `test_area_nodal_distribution_and_audits_match_closed_integrals` |
| 線荷重の始終点配分と共有辺の単一所有 | `test_boundary_line_linear_distribution_has_exact_endpoint_shares`, `test_shared_diagonal_is_loaded_once_without_replacing_q4_basis` |
| 比例・加法・相殺時の尺度 | `test_zero_proportionality_and_addition_include_audits`, `test_cancelled_total_force_keeps_nonzero_scale_and_pure_couple` |
| 歪んだQ4と非多項式載荷帯 | `test_nonpolynomial_area_assembly_matches_independent_native_integration`, `test_nonpolynomial_line_and_nonconvergence_rejection` |
| 平行移動・縮尺・非連番ID | `test_translation_and_length_scaling_preserve_dimensions`, `test_compact_nonconsecutive_node_ids_and_three_dof_layout` |
| T3シェルの節点順・複数パネルの加算 | `test_t3_shell_cells_keep_structural_node_order_and_direct_contributions`, `test_two_panels_on_same_shell_are_additive_without_losing_load_provenance` |
| 折れ線の累積長・異なる折点と複数セル | `test_multiple_line_segments_use_cumulative_length_intensities`, `test_merged_strip_knots_and_multiple_q4_cells_match_unpartitioned_field` |
| 区分全体の誤差予算・保存性違反拒否 | `test_piecewise_adaptive_quadrature_shares_the_absolute_error_budget`, `test_independent_audits_reject_corrupted_basis` |
| 独立節点荷重との全出力一致・拘束節点への荷重 | `test_internal_spatial_solve_matches_independent_nodal_loads_and_all_outputs` |
| 再組立・再解析・保存読込の非累積性 | `test_one_compilation_per_solve_and_no_accumulation_on_assembly_rerun_reload` |
| 幾何・剛性・求解・後処理・公開事前検証失敗の復旧 | `test_failed_analysis_discards_compilation_and_retry_is_clean` |
| 面圧の直接項・載荷三角形との区別 | `test_mixed_shell_pressure_and_spatial_loads_retain_both_direct_terms`, `test_loading_triangles_on_shell_nodes_are_not_direct_shell_pressure` |
| 面圧との応答一致・片持梁解析解 | `test_uniform_shell_spatial_area_matches_existing_pressure_displacements_and_reactions`, `test_line_on_two_cantilever_tips_matches_point_force_closed_form` |
| 高精度梁経路と結果保存 | `test_high_precision_beam_fallback_retains_spatial_external_forces`, `test_result_json_preserves_spatial_snapshot_and_equilibrium_output` |

節点配分の期待値は製品処理をimportしない `tests/support/oracles/spatial_loads.py` に追加した。
正方形を構成するT3と載荷帯の積分にはSymPyの有理数積分、歪んだQ4には既存の独立固有座標積分を使う。
誤った形状関数を注入する試験では、合力違反と合力を保存したモーメント違反の両方を拒否する。
片持梁先端の線荷重試験は、2本の梁先端に各3の節点力を与え、
`w=PL^3/(3EI)=0.008`、`ry=-PL^2/(2EI)=-0.006`、支点反力 `Fz=-3, My=6` を確認する。
面上の連続分布を梁の分布荷重へ置き換えた試験ではない。

## 検証結果

全コマンドは既存 `.venv/Scripts/python.exe` を使った。

| 実行内容 | 結果・生の終了コード |
|---|---|
| 形状関数共通化後の既存幾何・面圧・入力試験 | 222 passed、21.49秒、終了0 |
| 新規組立・内部解析の先行試験 | 104 passed、10.61秒、終了0 |
| 幾何・組立・内部解析・入力・対応可否・シェル・面圧・ハーネス試験 | 420 passed、36.77秒、終了0。高精度経路追加前 |
| 最終変更後 `python -m pytest tests/integration/test_spatial_loads.py -q --tb=short` | 26 passed、2.94秒、終了0 |
| `python -m tools.validation.render_capabilities` | 文書とレジストリの一致、終了0 |
| `git diff --check` | 終了0。GitのCRLF変換警告のみ |
| `python -m tools.validation.check_test_results --output-dir tmp/spatial-loads-assembly-final` | 2339 passed、1577.04秒、生のpytest終了0。検証コマンドも終了0 |

JUnitの全2339件について、失敗・エラー・スキップはいずれも0件。
`baseline-comparison.json` の `raw_pytest_exit_code=0`、`baseline_matches=true`、
`known_failures=0`、`issues=[]` を確認した。
検証前に記録した変更対象ソース・設定・テスト15ファイルのSHA256を終了後に再照合し、全て一致した。
全件試験中に変更したのは計画書と作業記録のみ。
変更17ファイルの構文・UTF-8・文書リンク・差分空白検査も通過した。
ログ・JUnit・基準照合・数値誤差・ハッシュ・ファイル検証結果は `tmp/spatial-loads-assembly-final/` に保存した。
既存の保存期待値、入力サンプル、既知失敗リスト、skip設定は変更していない。

開発途中の新規試験は最初に79成功・3失敗となった。
1件は大きな倍率と正負荷重によって解析上ゼロとなる成分を固定絶対誤差で比較していたため、
力の絶対値積分を尺度とする機械丸め誤差の比較に修正した。製品の求積許容値は緩和していない。
残り2件はNumPy座標の辞書を `==` で比較したテスト側の問題で、節点ごとの厳密配列比較へ修正した。
文書編集補助スクリプトはPowerShellからPythonへのパイプで日本語パスが `?` に置換され、
読込前に失敗した。ファイルは変更されておらず、`apply_patch` に切り替えた。
文書追加後のGit検証補助も既定cp932による警告の復号に失敗したため、
UTF-8を明示して再実行し、差分検査終了0を確認した。いずれも製品試験の失敗ではない。

数値誤差の計測を `tmp/spatial-loads-assembly-final/numerical-errors.json` に保存した。
次は構造DOFへ組み立てた節点力の成分別最大誤差。単位は試験モデルの整合単位系。

| 比較 | 最大絶対誤差 | 最大相対誤差 |
|---|---:|---:|
| 正方形T3×双一次面荷重 | `3.553e-15` | `3.290e-16` |
| 正方形Q4×双一次面荷重 | `5.329e-15` | `4.749e-16` |
| T3×台形帯の非多項式荷重場 | `4.441e-16` | `1.519e-16` |
| 歪んだQ4×台形帯 | `1.332e-15` | `5.921e-16` |
| 歪んだQ4×対角線線荷重 | `4.441e-16` | `1.777e-16` |

同じ計測に含む全固定モデル4種の反力は独立節点荷重と最大絶対誤差 `7.105e-15`、
最大相対誤差 `7.066e-16`。合力とモーメントの保存誤差はこの計測でともに最大 `7.105e-15`。
これらは一般モデル全体の誤差上界ではなく、記載した検証モデルの観測値。

## 公開境界と次段階

第3段階の内部荷重組立・解析接続を実装した。公開の面荷重解析リリースではない。
`FemModel.run()`、HTTPは線形静解析を含めて引き続き空間荷重を事前拒否する。
対応可否レジストリの `unsupported` は保持し、理由を「内部コンパイル済み・公開入力経路検証待ち」へ更新した。
既存の公開事前拒否試験も残している。

第4段階では公開の事前検証・座標設定・コンパイル順序を固定し、JSONファイル・辞書・HTTP・Python APIの
全出力を比較する。梁・板の解析解とメッシュ依存性の検証、Wikiと公開APIの単位・符号・例・制限の更新を行い、
全件試験成功後に `static` を有効にする。一般座標・非凸・穴・旧値互換は引き続き後続段階。
