# テスト整理台帳（全ファイル・全関数）

> ?????????????????????????????? [????](test_inventory.md)?????????? [????](../report/test-reorganization.md) ????????????????????????????????????

2026-09-09、`c42ee68`時点。[整理計画](test-reorganization.md)の付属台帳。役割と移管先はソースのassertion・参照元・依存関係を確認して分類した。関数・展開件数はASTと今回のJUnitから照合。

## 1. 今回の実測

- `.venv/Scripts/python.exe -m pytest --collect-only -q tests`：1,151件、収集警告1件。
- `.venv/Scripts/python.exe -m pytest tests -q --tb=no --show-capture=no --durations=15 --junitxml=tmp/test-reorganization-baseline.xml`：**1,116成功・35失敗・0エラー・0skip、警告1件、166.46秒**。終了コード1。
- Python 3.13.11 / pytest 9.0.2 / NumPy 2.4.1 / SciPy 1.17.0 / SymPy 1.14.0 / Node v24.13.0、Windows。
- 補助クラス `tests/elements/test_base_element.py::TestElement` のPytestCollectionWarning。基底APIの9試験は収集・成功している。
- 全323荷重case監査・coverage計測・外部JR較正は今回実行していない。

35失敗の対象ファイル集合は、`material-nonlinear-closure-evidence.json` の `current_samples.failed_samples` と一致した。追加・解消とも0件。

ローカルの詳細出力：`tmp/test-reorganization-baseline.log` と `tmp/test-reorganization-baseline.xml`。tmpはGit無視のため、本台帳の要約・失敗一覧を永続記録とする。

| 証拠 | SHA256 |
|---|---|
| JUnit XML | `9b7fe30c9a645f90af617e6935ba1fb384483af4446b341889d0f3c59f7c5589` |
| 実行ログ | `a582891d688bb81ab9e94c9475467776932cdadcb3a42ad6b190ab55dc330458` |

## 2. 全38ファイルの役割

パスは `tests/` からの相対パス。関数数はクラスメソッドを含む定義数、case数はpytest展開後。移管先は提案で、現時点では未移動。細かい分割先は第3節を優先する。

| 現ファイル | 関数 / case | 実際の役割 | 主な移管先 |
|---|---:|---|---|
| [elements/test_bar_element.py](../../tests/elements/test_bar_element.py) | 5 / 5 | BE梁の生成、軸剛性と軸端力、質量の対角非負、回転後の対称性。応力APIや支点条件の網羅試験ではない | `elements/beam/test_elastic.py`ほか |
| [elements/test_base_element.py](../../tests/elements/test_base_element.py) | 9 / 9 | ID・接続・座標・長さ・Jacobianと基底の未実装契約。補助TestElementを改名する | `elements/test_base.py`ほか |
| [elements/test_nonlinear_bar_element.py](../../tests/elements/test_nonlinear_bar_element.py) | 15 / 63 | BE/T/非線形梁の共通運動学、弾性閉形式、断面ひずみ・曲率、接線差分、試行履歴・保存出力。全体解析部分は分離 | `elements/beam/test_nonlinear_section.py`ほか |
| [elements/test_phase4_beam_equilibrium.py](../../tests/elements/test_phase4_beam_equilibrium.py) | 6 / 13 | 自由端の静力学的端力回復、実在する微小モーメント、回復対象外と過大誤差拒否、beam001公開出力 | `postprocess/test_beam_equilibrium.py`ほか |
| [elements/test_phase4_beam_precision.py](../../tests/elements/test_phase4_beam_precision.py) | 3 / 7 | 剛な梁の剛体回転での桁落ち防止、内力・端力・整合荷重の関係、多項式の仮想仕事 | `elements/beam/test_precision.py`ほか |
| [elements/test_phase4_dkt.py](../../tests/elements/test_phase4_dkt.py) | 5 / 19 | 薄板DKTの曲率と厚さ3乗エネルギー、回転共変性、6剛体モード、辺合力と一定曲げの解析解 | `elements/shell/test_dkt.py`ほか |
| [elements/test_phase4_foundation_precision.py](../../tests/elements/test_phase4_foundation_precision.py) | 3 / 6 | 軸foundationの双曲関数解、長い梁の半無限解、線形分布荷重の特解。overflow域を含む | `elements/beam/test_foundation.py`ほか |
| [elements/test_phase4_postprocess.py](../../tests/elements/test_phase4_postprocess.py) | 2 / 7 | 一次ソリッド3実装のアフィン応力・ひずみと、Tri/Quadの回転した膜ひずみパッチ | `postprocess/test_solid_stress.py`ほか |
| [elements/test_phase4_quadratic_solids.py](../../tests/elements/test_phase4_quadratic_solids.py) | 4 / 12 | 二次Tetra/Wedge/Hexaの節点補間・多項式・導関数、質量・エネルギー・6剛体モード、反転形状拒否と公開解析 | `elements/solid/test_quadratic.py`ほか |
| [elements/test_phase4_shell_kinematics.py](../../tests/elements/test_phase4_shell_kinematics.py) | 10 / 48 | Tri/Quadの剛体モード・回転・膜エネルギー・drill、Quadのlockingと一定曲げ。面圧を別責務へ移管 | `elements/shell/test_kinematics.py`ほか |
| [elements/test_phase4_shell_legacy_view.py](../../tests/elements/test_phase4_shell_legacy_view.py) | 3 / 3 | 工学せん断・両面・エネルギー密度、点平均と面積平均の差、旧挿入順IDと現行ID | `postprocess/test_shell_results.py`ほか |
| [elements/test_phase4_shell_outputs.py](../../tests/elements/test_phase4_shell_outputs.py) | 7 / 28 | 両面の全体テンソル、膜・曲げ・せん断・剛体、積分エネルギー、辺合力・曲げ偶力、公開出力と不正変位拒否 | `postprocess/test_shell_results.py`ほか |
| [elements/test_phase4_solid_precision.py](../../tests/elements/test_phase4_solid_precision.py) | 1 / 9 | 二次ソリッドの大きな剛体変位に重なる微小ひずみと公開反力。期待反力は原V0形状のDecimal剛性 | `integration/test_solids.py`ほか |
| [elements/test_phase4_static_precision.py](../../tests/elements/test_phase4_static_precision.py) | 4 / 7 | 変位補正の微小成分、強制剛体移動下の軸力、疎積とPython3.11 fallback、短区間サンプルの構成則釣合い | `solvers/test_linear.py`ほか |
| [elements/test_shell_element.py](../../tests/elements/test_shell_element.py) | 14 / 14 | Tri/Quadの名称・DOF数、補間の和・導関数・積分点、正Jacobian、剛性対称性、質量の対角非負 | `elements/shell/test_kinematics.py`ほか |
| [elements/test_shell_pressure.py](../../tests/elements/test_shell_pressure.py) | 9 / 9 | Pressure条件の生成・文字列・管理・clearと面圧節点荷重、F1/F2・比例性・異常値。条件管理を分離 | `elements/shell/test_pressure.py`ほか |
| [elements/test_solid_element.py](../../tests/elements/test_solid_element.py) | 18 / 18 | 一次Tetra/Hexa/Wedgeの名称・補間・導関数・積分点、Tetra体積、剛性対称性・質量の対角非負 | `elements/solid/test_linear.py`ほか |
| [nonlinear/test_jr_beam_history.py](../../tests/nonlinear/test_jr_beam_history.py) | 2 / 20 | 実JRを梁断面へ組み込んだ反転履歴、commit/rollback/reset、出力の独立性、全12列接線 | `integration/test_jr_beam_history.py`ほか |
| [nonlinear/test_jr_hysteresis.py](../../tests/nonlinear/test_jr_hysteresis.py) | 26 / 152 | JRの区分骨格、正負非対称、入れ子・復帰点、剛性低減、連続性・接線、刻み不変性・状態不変性、異常値拒否 | `materials/test_jr_hysteresis.py`ほか |
| [nonlinear/test_phase4_io.py](../../tests/nonlinear/test_phase4_io.py) | 15 / 34 | Python/JSON/ファイル/HTTP同値、解析選択と400/422/500、保存再読込・旧結果消去。比較器/runner/実JR復元を分離 | `io/test_http.py`ほか |
| [nonlinear/test_phase4_reference.py](../../tests/nonlinear/test_phase4_reference.py) | 12 / 438 | 4モード・3経路・正負/β/制御/分割の独立解析解、履歴仕事、ばね、非一様曲げ収束、beam001と配置回転 | `integration/test_nonlinear_reference.py`ほか |
| [nonlinear/test_solver.py](../../tests/nonlinear/test_solver.py) | 12 / 19 | JRと独立の弾性・三次・耐力制限ばねによるNewton、残差・拘束・反力、失敗stepの復元、制御値検証。モデルライフサイクルを分離 | `solvers/test_nonlinear.py`ほか |
| [test_beam001_reference_values.py](../../tests/test_beam001_reference_values.py) | 3 / 5 | 保存101段階と独立Decimal/float式、分岐前後の手計算値、step62の変位・反力・端力改変検出 | `validation/test_stored_references.py`ほか |
| [test_legacy_shear_input.py](../../tests/test_legacy_shear_input.py) | 3 / 14 | G省略と明示shearの優先、線形/非線形、選択材料ケース・剛域。ファイル/HTTP/保存の同値 | `io/test_legacy_input.py`ほか |
| [test_phase4_input_decisions.py](../../tests/test_phase4_input_decisions.py) | 5 / 8 | 非梁で梁断面を要求しない、旧Aの厚さ解釈。Pressure/Tri1の入力同一性・保存参照と面圧支持の解析は分離 | `io/test_legacy_input.py`ほか |
| [test_phase4_legacy.py](../../tests/test_phase4_legacy.py) | 22 / 33 | 旧入力の梁解析：材料/回転、分割、荷重、支持・端部解放、分布ばね、温度。ソルバー・保存・比較器・監査・beam001が混在 | `integration/test_linear_beam.py`ほか |
| [test_phase4_quadratic_reference.py](../../tests/test_phase4_quadratic_reference.py) | 3 / 7 | Decimalの独立なアフィン仕事・剛体モード・逆行列の自己検証と、二次ソリッドの製品全出力比較 | `validation/test_v0_solid_reference.py`ほか |
| [test_phase4_shell_input.py](../../tests/test_phase4_shell_input.py) | 3 / 6 | 選択材料ケースのA→厚さ、明示厚さ/formulationの優先、厚さの欠落・不正値拒否、入力不変性 | `io/test_legacy_input.py`ほか |
| [test_phase4_source_evidence.py](../../tests/test_phase4_source_evidence.py) | 9 / 19 | 原資料と入力・単位・網羅性の同一性、旧転記不具合の再現、材料/幾何所見。製品ソリッド解析と面圧機構を分離 | `validation/test_provenance.py`ほか |
| [test_phase4_source_repairs.py](../../tests/test_phase4_source_repairs.py) | 2 / 13 | 節点/荷重/拘束/材料/種類/次元/case/既存値の改変拒否。修復冪等性と製品全変位・釣合いの混在を分離 | `harness/test_source_repairs.py`ほか |
| [test_phase4_support_repairs.py](../../tests/test_phase4_support_repairs.py) | 2 / 7 | 支持反力補完の入力・hash・残差・網羅性・未知参照拒否、原入力とdisg保持、冪等性 | `harness/test_source_repairs.py`ほか |
| [test_phase4_triangle_reference.py](../../tests/test_phase4_triangle_reference.py) | 2 / 8 | Tri1の元DKT演算子による保存全出力、外部変位・残差・240要素網羅と入力改変拒否 | `validation/test_stored_references.py`ほか |
| [test_phase4_v0_import.py](../../tests/test_phase4_v0_import.py) | 3 / 13 | 製品V0読込の材料/厚さ/拘束/荷重加算、行番号付き厳密拒否、元Hexa全レコード・3DOF | `io/test_v0_input.py`ほか |
| [test_phase4_v0_input_reference.py](../../tests/test_phase4_v0_input_reference.py) | 4 / 14 | 参照側.femの手パッチ・不正レコード拒否、Tetra1の出典/input-only/hash/残差/網羅性ガードと製品全出力 | `validation/test_v0_solid_reference.py`ほか |
| [test_phase4_v0_support_reference.py](../../tests/test_phase4_v0_support_reference.py) | 3 / 8 | 旧JS支持反力の手パッチ、不完全出力拒否、元出力変位に依存せず荷重から解く残差補正 | `validation/test_v0_solid_reference.py`ほか |
| [test_pressure_integration.py](../../tests/test_pressure_integration.py) | 5 / 5 | JSON/FEM読込、複数面圧、保存再読込。等価節点荷重の総力/モーメント検証を要素へ移す。現状ソルバー実行はない | `io/test_pressure.py`ほか |
| [test_run_data.py](../../tests/test_run_data.py) | 4 / 45（失敗35） | bar/shell/bendの全保存サンプルをファイル単位で厳密比較。snapはbeam001の全step独立解と保存101段階 | `regression/test_legacy_samples.py`ほか |
| [test_solver_boundary_conditions.py](../../tests/test_solver_boundary_conditions.py) | 6 / 6 | 6/3DOF stride、最後の節点拘束、強制変位RHSと対称性、ばね、反力、Newtonの残り強制変位 | `solvers/test_boundary_conditions.py`ほか |

## 3. 全264関数の移管先

基本処置は「検証内容・parameter・許容値を保持して移管」。特記したものだけ段階R3の統合候補。`+` は関数内の責務分割を表す。行番号は調査時点。個別の英語関数名は検証する振る舞いを示し、ファイルごとの保証範囲は第2節を参照。
共通の形状関数・積分・質量の試験は最初は同じファイルへ移し、クラス共通化は後で行う。case数を減らすこと自体は目的としない。

### elements/test_bar_element.py

BE梁の生成、軸剛性と軸端力、質量の対角非負、回転後の対称性。応力APIや支点条件の網羅試験ではない。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`TestBarElement.test_invalid_node_count`](../../tests/elements/test_bar_element.py#L29) | 1 | `elements/beam/test_elastic.py` | 保持・移管 |
| [`TestBarElement.test_basic_stiffness_matrix`](../../tests/elements/test_bar_element.py#L36) | 1 | `elements/beam/test_elastic.py` | 軸剛性の2成分。閉形式試験へ統合候補 |
| [`TestBarElement.test_mass_matrix`](../../tests/elements/test_bar_element.py#L44) | 1 | `elements/beam/test_elastic.py` | 保持。現状は行列サイズ・対角非負であり質量保存の保証ではない |
| [`TestBarElement.test_boundary_conditions`](../../tests/elements/test_bar_element.py#L51) | 1 | `elements/beam/test_elastic.py` | 回転後の対称性。支点試験として扱わず改名・統合候補 |
| [`TestBarElement.test_stress_strain_calculation`](../../tests/elements/test_bar_element.py#L61) | 1 | `elements/beam/test_elastic.py` | K@uの軸端力。閉形式試験へ統合候補 |

### elements/test_base_element.py

ID・接続・座標・長さ・Jacobianと基底の未実装契約。補助TestElementを改名する。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`TestBaseElement.test_initialization`](../../tests/elements/test_base_element.py#L28) | 1 | `elements/test_base.py` | 保持・移管 |
| [`TestBaseElement.test_invalid_input`](../../tests/elements/test_base_element.py#L34) | 1 | `elements/test_base.py` | 保持・移管 |
| [`TestBaseElement.test_set_node_coordinates`](../../tests/elements/test_base_element.py#L43) | 1 | `elements/test_base.py` | 保持・移管 |
| [`TestBaseElement.test_nonexistent_node`](../../tests/elements/test_base_element.py#L53) | 1 | `elements/test_base.py` | 保持・移管 |
| [`TestBaseElement.test_get_element_length`](../../tests/elements/test_base_element.py#L61) | 1 | `elements/test_base.py` | 保持・移管 |
| [`TestBaseElement.test_get_element_volume`](../../tests/elements/test_base_element.py#L70) | 1 | `elements/test_base.py` | 基底のNotImplementedError契約。3APIをparametrizeする候補 |
| [`TestBaseElement.test_get_shape_functions`](../../tests/elements/test_base_element.py#L75) | 1 | `elements/test_base.py` | 基底のNotImplementedError契約。3APIをparametrizeする候補 |
| [`TestBaseElement.test_get_shape_derivatives`](../../tests/elements/test_base_element.py#L80) | 1 | `elements/test_base.py` | 基底のNotImplementedError契約。3APIをparametrizeする候補 |
| [`TestBaseElement.test_get_jacobian`](../../tests/elements/test_base_element.py#L85) | 1 | `elements/test_base.py` | 保持・移管 |

### elements/test_nonlinear_bar_element.py

BE/T/非線形梁の共通運動学、弾性閉形式、断面ひずみ・曲率、接線差分、試行履歴・保存出力。全体解析部分は分離。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_rigid_body_motion_has_zero_force`](../../tests/elements/test_nonlinear_bar_element.py#L64) | 12 | `elements/beam/test_kinematics.py` | 保持・移管 |
| [`test_endpoint_forces_and_moments_balance`](../../tests/elements/test_nonlinear_bar_element.py#L78) | 2 | `elements/beam/test_kinematics.py` | 保持・移管 |
| [`test_elastic_limit_matches_closed_form_matrix`](../../tests/elements/test_nonlinear_bar_element.py#L90) | 6 | `elements/beam/test_elastic.py` | 保持・移管 |
| [`test_tangent_is_finite_difference_of_force`](../../tests/elements/test_nonlinear_bar_element.py#L101) | 4 | `elements/beam/test_nonlinear_section.py` | 保持・移管 |
| [`test_section_skeleton_uses_strain_or_curvature_not_endpoint_motion`](../../tests/elements/test_nonlinear_bar_element.py#L116) | 12 | `elements/beam/test_nonlinear_section.py` | 保持・移管 |
| [`test_named_bending_law_leaves_other_plane_elastic`](../../tests/elements/test_nonlinear_bar_element.py#L129) | 2 | `elements/beam/test_nonlinear_section.py` | 保持・移管 |
| [`test_rotated_response_matches_independent_basis_rotation`](../../tests/elements/test_nonlinear_bar_element.py#L136) | 1 | `elements/beam/test_kinematics.py` | 保持・移管 |
| [`test_trial_order_cannot_change_result_or_committed_history`](../../tests/elements/test_nonlinear_bar_element.py#L158) | 1 | `elements/beam/test_nonlinear_section.py` | 保持・移管 |
| [`test_tangent_and_output_do_not_replace_trial_to_be_committed`](../../tests/elements/test_nonlinear_bar_element.py#L178) | 1 | `elements/beam/test_nonlinear_section.py` | 保持・移管 |
| [`test_output_preserves_converged_force_after_commit_and_other_trials`](../../tests/elements/test_nonlinear_bar_element.py#L191) | 1 | `elements/beam/test_nonlinear_section.py` | 保持・移管 |
| [`test_cantilever_tip_load_matches_hand_solution_and_mesh_refinement`](../../tests/elements/test_nonlinear_bar_element.py#L211) | 8 | `integration/test_beam_solutions.py` | 保持・移管 |
| [`test_fem_model_publishes_section_forces_with_nonconsecutive_nodes`](../../tests/elements/test_nonlinear_bar_element.py#L227) | 2 | `integration/test_beam_solutions.py` | 保持・移管 |
| [`test_bernoulli_euler_force_output_uses_right_handed_moments`](../../tests/elements/test_nonlinear_bar_element.py#L251) | 1 | `elements/beam/test_elastic.py` | 保持・移管 |
| [`test_nonlinear_pure_bending_solution_and_output_under_refinement`](../../tests/elements/test_nonlinear_bar_element.py#L261) | 4 | `integration/test_beam_solutions.py` | 保持・移管 |
| [`test_tangent_with_fixed_committed_history_on_smooth_branches`](../../tests/elements/test_nonlinear_bar_element.py#L294) | 6 | `elements/beam/test_nonlinear_section.py` | 保持・移管 |

### elements/test_phase4_beam_equilibrium.py

自由端の静力学的端力回復、実在する微小モーメント、回復対象外と過大誤差拒否、beam001公開出力。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_free_end_recovery_preserves_applied_moments_and_force_balance`](../../tests/elements/test_phase4_beam_equilibrium.py#L11) | 6 | `postprocess/test_beam_equilibrium.py` | 保持・移管 |
| [`test_recovery_does_not_hide_a_constitutive_or_solver_error`](../../tests/elements/test_phase4_beam_equilibrium.py#L30) | 1 | `postprocess/test_beam_equilibrium.py` | 保持・移管 |
| [`test_supported_or_foundation_member_is_not_statically_recovered`](../../tests/elements/test_phase4_beam_equilibrium.py#L39) | 2 | `postprocess/test_beam_equilibrium.py` | 保持・移管 |
| [`test_beam001_all_stored_steps_keep_strict_zero_moment_reference`](../../tests/elements/test_phase4_beam_equilibrium.py#L51) | 1 | `regression/test_beam001.py` | 保持・移管 |
| [`test_public_beam001_keeps_a_real_sub_tolerance_end_moment`](../../tests/elements/test_phase4_beam_equilibrium.py#L59) | 1 | `regression/test_beam001.py` | 保持・移管 |
| [`test_recovery_with_consistent_distributed_load_and_reversed_loading`](../../tests/elements/test_phase4_beam_equilibrium.py#L72) | 2 | `postprocess/test_beam_equilibrium.py` | 保持・移管 |

### elements/test_phase4_beam_precision.py

剛な梁の剛体回転での桁落ち防止、内力・端力・整合荷重の関係、多項式の仮想仕事。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_stiff_beam_rigid_rotation_force_does_not_cancel_large_products`](../../tests/elements/test_phase4_beam_precision.py#L19) | 3 | `elements/beam/test_precision.py` | 保持・移管 |
| [`test_loaded_beam_internal_force_and_section_force_use_same_kinematics`](../../tests/elements/test_phase4_beam_precision.py#L28) | 1 | `elements/beam/test_precision.py` | 保持・移管 |
| [`test_polynomial_beam_virtual_work_and_force_resultants`](../../tests/elements/test_phase4_beam_precision.py#L39) | 3 | `elements/beam/test_precision.py` | 保持・移管 |

### elements/test_phase4_dkt.py

薄板DKTの曲率と厚さ3乗エネルギー、回転共変性、6剛体モード、辺合力と一定曲げの解析解。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_dkt_quadratic_bending_has_exact_thickness_cubed_energy`](../../tests/elements/test_phase4_dkt.py#L20) | 12 | `elements/shell/test_dkt.py` | 保持・移管 |
| [`test_dkt_covariance_and_exactly_six_null_modes`](../../tests/elements/test_phase4_dkt.py#L36) | 1 | `elements/shell/test_dkt.py` | 保持・移管 |
| [`test_dkt_requires_a_triangle`](../../tests/elements/test_phase4_dkt.py#L47) | 1 | `elements/shell/test_dkt.py` | 保持・移管 |
| [`test_dkt_transverse_resultants_follow_moment_equilibrium`](../../tests/elements/test_phase4_dkt.py#L52) | 1 | `postprocess/test_shell_results.py` | 保持・移管 |
| [`test_dkt_cantilever_constant_moment`](../../tests/elements/test_phase4_dkt.py#L69) | 4 | `integration/test_plate_bending.py` | 保持・移管 |

### elements/test_phase4_foundation_precision.py

軸foundationの双曲関数解、長い梁の半無限解、線形分布荷重の特解。overflow域を含む。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_axial_foundation_matches_hyperbolic_dynamic_stiffness`](../../tests/elements/test_phase4_foundation_precision.py#L8) | 3 | `elements/beam/test_foundation.py` | 保持・移管 |
| [`test_long_bending_foundation_matches_independent_half_infinite_solution`](../../tests/elements/test_phase4_foundation_precision.py#L19) | 1 | `elements/beam/test_foundation.py` | 保持・移管 |
| [`test_linear_foundation_particular_solution_preserves_consistent_loads`](../../tests/elements/test_phase4_foundation_precision.py#L32) | 2 | `elements/beam/test_foundation.py` | 保持・移管 |

### elements/test_phase4_postprocess.py

一次ソリッド3実装のアフィン応力・ひずみと、Tri/Quadの回転した膜ひずみパッチ。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_solid_affine_stress_patch`](../../tests/elements/test_phase4_postprocess.py#L11) | 3 | `postprocess/test_solid_stress.py` | 保持・移管 |
| [`test_shell_rotated_membrane_patch`](../../tests/elements/test_phase4_postprocess.py#L33) | 4 | `postprocess/test_shell_results.py` | 保持・移管 |

### elements/test_phase4_quadratic_solids.py

二次Tetra/Wedge/Hexaの節点補間・多項式・導関数、質量・エネルギー・6剛体モード、反転形状拒否と公開解析。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_quadratic_partition_polynomials_derivatives`](../../tests/elements/test_phase4_quadratic_solids.py#L30) | 3 | `elements/solid/test_quadratic.py` | 保持・移管 |
| [`test_quadratic_affine_energy_mass_and_six_rigid_modes`](../../tests/elements/test_phase4_quadratic_solids.py#L49) | 3 | `elements/solid/test_quadratic.py` | 保持・移管 |
| [`test_quadratic_public_prescribed_affine_solution`](../../tests/elements/test_phase4_quadratic_solids.py#L72) | 3 | `integration/test_solids.py` | 保持・移管 |
| [`test_quadratic_inverted_geometry_is_rejected`](../../tests/elements/test_phase4_quadratic_solids.py#L83) | 3 | `elements/solid/test_quadratic.py` | 保持・移管 |

### elements/test_phase4_shell_kinematics.py

Tri/Quadの剛体モード・回転・膜エネルギー・drill、Quadのlockingと一定曲げ。面圧を別責務へ移管。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_shell_six_rigid_body_modes_have_no_internal_force`](../../tests/elements/test_phase4_shell_kinematics.py#L23) | 12 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`test_shell_stiffness_and_mass_rotate_with_the_plane`](../../tests/elements/test_phase4_shell_kinematics.py#L36) | 2 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`test_shell_affine_membrane_energy`](../../tests/elements/test_phase4_shell_kinematics.py#L49) | 2 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`test_degenerate_shell_is_rejected_without_fallback`](../../tests/elements/test_phase4_shell_kinematics.py#L63) | 2 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`test_shell_has_exactly_six_physical_rigid_zero_modes`](../../tests/elements/test_phase4_shell_kinematics.py#L72) | 2 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`test_drill_spin_constraint_has_independent_constant_field_energy`](../../tests/elements/test_phase4_shell_kinematics.py#L80) | 2 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`test_quad_constant_bending_energy_has_no_shear_locking`](../../tests/elements/test_phase4_shell_kinematics.py#L96) | 6 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`test_quad_cantilever_constant_moment_full_solver`](../../tests/elements/test_phase4_shell_kinematics.py#L111) | 8 | `integration/test_plate_bending.py` | 保持・移管 |
| [`test_surface_pressure_force_and_moment_follow_v0_faces`](../../tests/elements/test_phase4_shell_kinematics.py#L138) | 8 | `elements/shell/test_pressure.py` | 保持・移管 |
| [`test_invalid_surface_pressure_never_falls_back`](../../tests/elements/test_phase4_shell_kinematics.py#L152) | 4 | `elements/shell/test_pressure.py` | 保持・移管 |

### elements/test_phase4_shell_legacy_view.py

工学せん断・両面・エネルギー密度、点平均と面積平均の差、旧挿入順IDと現行ID。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_legacy_engineering_shear_energy_density_and_separate_surfaces`](../../tests/elements/test_phase4_shell_legacy_view.py#L10) | 1 | `postprocess/test_shell_results.py` | 保持・移管 |
| [`test_legacy_view_uses_point_mean_not_area_weighted_mean`](../../tests/elements/test_phase4_shell_legacy_view.py#L29) | 1 | `postprocess/test_shell_results.py` | 保持・移管 |
| [`test_public_legacy_ids_are_insertion_indices_and_modern_ids_are_retained`](../../tests/elements/test_phase4_shell_legacy_view.py#L42) | 1 | `integration/test_model_contracts.py` | 保持・移管 |

### elements/test_phase4_shell_outputs.py

両面の全体テンソル、膜・曲げ・せん断・剛体、積分エネルギー、辺合力・曲げ偶力、公開出力と不正変位拒否。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_surface_tensors`](../../tests/elements/test_phase4_shell_outputs.py#L23) | 16 | `postprocess/test_shell_results.py` | 保持・移管 |
| [`test_shell_integrated_energy_and_resultants`](../../tests/elements/test_phase4_shell_outputs.py#L60) | 1 | `postprocess/test_shell_results.py` | 保持・移管 |
| [`test_public_shell_output_with_noncontiguous_ids`](../../tests/elements/test_phase4_shell_outputs.py#L74) | 2 | `integration/test_model_contracts.py` | 保持・移管 |
| [`test_drilling_energy_is_separate_from_physical_energy`](../../tests/elements/test_phase4_shell_outputs.py#L91) | 1 | `postprocess/test_shell_results.py` | 保持・移管 |
| [`test_shell_output_rejects_incomplete_or_nonfinite_displacement`](../../tests/elements/test_phase4_shell_outputs.py#L100) | 3 | `postprocess/test_shell_results.py` | 保持・移管 |
| [`test_edge_tractions_from_uniform_membrane_stress`](../../tests/elements/test_phase4_shell_outputs.py#L107) | 4 | `postprocess/test_shell_results.py` | 保持・移管 |
| [`test_edge_bending_couple_has_physical_rotation_sign`](../../tests/elements/test_phase4_shell_outputs.py#L128) | 1 | `postprocess/test_shell_results.py` | 保持・移管 |

### elements/test_phase4_solid_precision.py

二次ソリッドの大きな剛体変位に重なる微小ひずみと公開反力。期待反力は原V0形状のDecimal剛性。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_quadratic_reactions_with_large_rigid_displacement`](../../tests/elements/test_phase4_solid_precision.py#L12) | 9 | `integration/test_solids.py` | 保持・移管 |

### elements/test_phase4_static_precision.py

変位補正の微小成分、強制剛体移動下の軸力、疎積とPython3.11 fallback、短区間サンプルの構成則釣合い。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_beam_keeps_displacement_below_the_rigid_translation_ulp`](../../tests/elements/test_phase4_static_precision.py#L14) | 1 | `elements/beam/test_precision.py` | 保持・移管 |
| [`test_public_axial_force_survives_prescribed_rigid_translation`](../../tests/elements/test_phase4_static_precision.py#L25) | 3 | `integration/test_beam_precision.py` | 保持・移管 |
| [`test_sparse_product_retains_real_sub_ulp_force_and_python311_fallback`](../../tests/elements/test_phase4_static_precision.py#L43) | 1 | `solvers/test_linear.py` | 保持・移管 |
| [`test_short_segment_sample_reaches_constitutive_equilibrium`](../../tests/elements/test_phase4_static_precision.py#L55) | 2 | `integration/test_beam_precision.py` | 保持・移管 |

### elements/test_shell_element.py

Tri/Quadの名称・DOF数、補間の和・導関数・積分点、正Jacobian、剛性対称性、質量の対角非負。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`TestShellElement.test_initialization`](../../tests/elements/test_shell_element.py#L24) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestShellElement.test_shape_functions`](../../tests/elements/test_shell_element.py#L29) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestShellElement.test_shape_derivatives`](../../tests/elements/test_shell_element.py#L34) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestShellElement.test_gauss_points`](../../tests/elements/test_shell_element.py#L40) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestShellElement.test_jacobian_determinant`](../../tests/elements/test_shell_element.py#L46) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestShellElement.test_stiffness_matrix`](../../tests/elements/test_shell_element.py#L50) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestShellElement.test_mass_matrix`](../../tests/elements/test_shell_element.py#L55) | 1 | `elements/shell/test_kinematics.py` | 保持。現状は行列サイズ・対角非負であり質量保存の保証ではない |
| [`TestQuadShellElement.test_initialization`](../../tests/elements/test_shell_element.py#L79) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestQuadShellElement.test_shape_functions`](../../tests/elements/test_shell_element.py#L84) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestQuadShellElement.test_shape_derivatives`](../../tests/elements/test_shell_element.py#L89) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestQuadShellElement.test_gauss_points`](../../tests/elements/test_shell_element.py#L95) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestQuadShellElement.test_jacobian_determinant`](../../tests/elements/test_shell_element.py#L101) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestQuadShellElement.test_stiffness_matrix`](../../tests/elements/test_shell_element.py#L105) | 1 | `elements/shell/test_kinematics.py` | 保持・移管 |
| [`TestQuadShellElement.test_mass_matrix`](../../tests/elements/test_shell_element.py#L110) | 1 | `elements/shell/test_kinematics.py` | 保持。現状は行列サイズ・対角非負であり質量保存の保証ではない |

### elements/test_shell_pressure.py

Pressure条件の生成・文字列・管理・clearと面圧節点荷重、F1/F2・比例性・異常値。条件管理を分離。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`TestShellPressure.test_pressure_class_creation`](../../tests/elements/test_shell_pressure.py#L36) | 1 | `io/test_pressure.py` | 保持・移管 |
| [`TestShellPressure.test_pressure_class_string_representation`](../../tests/elements/test_shell_pressure.py#L44) | 1 | `io/test_pressure.py` | 保持・移管 |
| [`TestShellPressure.test_boundary_condition_pressure_management`](../../tests/elements/test_shell_pressure.py#L50) | 1 | `io/test_pressure.py` | 保持・移管 |
| [`TestShellPressure.test_triangle_shell_pressure_equivalent_loads`](../../tests/elements/test_shell_pressure.py#L69) | 1 | `elements/shell/test_pressure.py` | 保持・移管 |
| [`TestShellPressure.test_quadrilateral_shell_pressure_equivalent_loads`](../../tests/elements/test_shell_pressure.py#L105) | 1 | `elements/shell/test_pressure.py` | サイズ・非ゼロのみ。強い節点値試験への統合候補 |
| [`TestShellPressure.test_pressure_direction_validation`](../../tests/elements/test_shell_pressure.py#L137) | 1 | `elements/shell/test_pressure.py` | 保持・移管 |
| [`TestShellPressure.test_pressure_magnitude_validation`](../../tests/elements/test_shell_pressure.py#L170) | 1 | `elements/shell/test_pressure.py` | 保持・移管 |
| [`TestShellPressure.test_invalid_pressure_parameters`](../../tests/elements/test_shell_pressure.py#L201) | 1 | `elements/shell/test_pressure.py` | 保持・移管 |
| [`TestShellPressure.test_boundary_condition_clear`](../../tests/elements/test_shell_pressure.py#L232) | 1 | `io/test_pressure.py` | 保持・移管 |

### elements/test_solid_element.py

一次Tetra/Hexa/Wedgeの名称・補間・導関数・積分点、Tetra体積、剛性対称性・質量の対角非負。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`TestTetraElement.test_initialization`](../../tests/elements/test_solid_element.py#L23) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestTetraElement.test_shape_functions`](../../tests/elements/test_solid_element.py#L26) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestTetraElement.test_shape_derivatives`](../../tests/elements/test_solid_element.py#L31) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestTetraElement.test_volume`](../../tests/elements/test_solid_element.py#L38) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestTetraElement.test_stiffness_matrix`](../../tests/elements/test_solid_element.py#L42) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestTetraElement.test_mass_matrix`](../../tests/elements/test_solid_element.py#L47) | 1 | `elements/solid/test_linear.py` | 保持。現状は行列サイズ・対角非負であり質量保存の保証ではない |
| [`TestHexaElement.test_initialization`](../../tests/elements/test_solid_element.py#L73) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestHexaElement.test_shape_functions`](../../tests/elements/test_solid_element.py#L76) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestHexaElement.test_shape_derivatives`](../../tests/elements/test_solid_element.py#L81) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestHexaElement.test_gauss_points`](../../tests/elements/test_solid_element.py#L88) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestHexaElement.test_stiffness_matrix`](../../tests/elements/test_solid_element.py#L94) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestHexaElement.test_mass_matrix`](../../tests/elements/test_solid_element.py#L99) | 1 | `elements/solid/test_linear.py` | 保持。現状は行列サイズ・対角非負であり質量保存の保証ではない |
| [`TestWedgeElement.test_initialization`](../../tests/elements/test_solid_element.py#L123) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestWedgeElement.test_shape_functions`](../../tests/elements/test_solid_element.py#L126) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestWedgeElement.test_shape_derivatives`](../../tests/elements/test_solid_element.py#L131) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestWedgeElement.test_gauss_points`](../../tests/elements/test_solid_element.py#L138) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestWedgeElement.test_stiffness_matrix`](../../tests/elements/test_solid_element.py#L144) | 1 | `elements/solid/test_linear.py` | 保持・移管 |
| [`TestWedgeElement.test_mass_matrix`](../../tests/elements/test_solid_element.py#L149) | 1 | `elements/solid/test_linear.py` | 保持。現状は行列サイズ・対角非負であり質量保存の保証ではない |

### nonlinear/test_jr_beam_history.py

実JRを梁断面へ組み込んだ反転履歴、commit/rollback/reset、出力の独立性、全12列接線。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_jr_beam_reversal_commit_rollback_and_saved_output`](../../tests/nonlinear/test_jr_beam_history.py#L33) | 8 | `integration/test_jr_beam_history.py` | 保持・移管 |
| [`test_jr_beam_history_tangent_matches_all_force_columns`](../../tests/nonlinear/test_jr_beam_history.py#L75) | 12 | `integration/test_jr_beam_history.py` | 保持・移管 |

### nonlinear/test_jr_hysteresis.py

JRの区分骨格、正負非対称、入れ子・復帰点、剛性低減、連続性・接線、刻み不変性・状態不変性、異常値拒否。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_monotonic_skeleton_all_segments_and_outgoing_tangent`](../../tests/nonlinear/test_jr_hysteresis.py#L41) | 14 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_reversal_uses_latest_committed_point_and_is_continuous`](../../tests/nonlinear/test_jr_hysteresis.py#L48) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_zero_force_crossing_at_same_sign_displacement_and_target_continuity`](../../tests/nonlinear/test_jr_hysteresis.py#L60) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_yielded_source_aims_at_opposite_second_breakpoint`](../../tests/nonlinear/test_jr_hysteresis.py#L72) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_zero_beyond_nominal_target_uses_forward_skeleton_intersection`](../../tests/nonlinear/test_jr_hysteresis.py#L80) | 1 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_asymmetric_loading_both_directions`](../../tests/nonlinear/test_jr_hysteresis.py#L89) | 1 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_nested_inner_loops_restore_return_points_and_suspended_outer_path`](../../tests/nonlinear/test_jr_hysteresis.py#L104) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_reversal_before_force_zero_retraces_unloading_line`](../../tests/nonlinear/test_jr_hysteresis.py#L121) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_region_specific_reduced_stiffness_and_limits`](../../tests/nonlinear/test_jr_hysteresis.py#L133) | 20 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_trial_reproducibility_input_immutability_and_hold`](../../tests/nonlinear/test_jr_hysteresis.py#L140) | 4 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_force_finite_difference_matches_tangent_on_smooth_branches`](../../tests/nonlinear/test_jr_hysteresis.py#L160) | 9 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_transition_continuity_and_one_sided_tangent`](../../tests/nonlinear/test_jr_hysteresis.py#L173) | 7 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_subdividing_same_path_preserves_response_and_future_history`](../../tests/nonlinear/test_jr_hysteresis.py#L187) | 9 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_nonfinite_parameters_are_explicitly_rejected`](../../tests/nonlinear/test_jr_hysteresis.py#L205) | 42 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_invalid_parameter_order_and_incompatible_stiffness_rejected`](../../tests/nonlinear/test_jr_hysteresis.py#L213) | 9 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_nonfinite_trial_deformation_is_rejected`](../../tests/nonlinear/test_jr_hysteresis.py#L219) | 3 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_zero_plateau_is_not_replaced_by_artificial_stiffness`](../../tests/nonlinear/test_jr_hysteresis.py#L227) | 1 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_material_api_validates_before_division_or_analysis`](../../tests/nonlinear/test_jr_hysteresis.py#L236) | 10 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_symmetric_constructor_rejects_zero_before_default_stiffness_division`](../../tests/nonlinear/test_jr_hysteresis.py#L244) | 1 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_reversing_at_exact_zero_retraces_and_restores_zero_crossing_path`](../../tests/nonlinear/test_jr_hysteresis.py#L250) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_internal_unloading_retrace_does_not_leave_a_stale_reversal`](../../tests/nonlinear/test_jr_hysteresis.py#L260) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_small_nonzero_increment_is_not_treated_as_hold`](../../tests/nonlinear/test_jr_hysteresis.py#L268) | 1 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_asymmetric_elastic_reversal_and_origin_one_sided_tangent`](../../tests/nonlinear/test_jr_hysteresis.py#L277) | 1 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_irregular_cyclic_path_refinement_with_asymmetry`](../../tests/nonlinear/test_jr_hysteresis.py#L288) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_maximum_deformation_api_includes_off_skeleton_experience`](../../tests/nonlinear/test_jr_hysteresis.py#L304) | 1 | `materials/test_jr_hysteresis.py` | 保持・移管 |
| [`test_reduced_stiffness_inside_loop_keeps_outer_damage_and_returns_exactly`](../../tests/nonlinear/test_jr_hysteresis.py#L312) | 2 | `materials/test_jr_hysteresis.py` | 保持・移管 |

### nonlinear/test_phase4_io.py

Python/JSON/ファイル/HTTP同値、解析選択と400/422/500、保存再読込・旧結果消去。比較器/runner/実JR復元を分離。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_python_json_file_http_equivalence`](../../tests/nonlinear/test_phase4_io.py#L80) | 1 | `integration/test_input_routes.py` | 保持・移管 |
| [`test_http_explicit_analysis_type`](../../tests/nonlinear/test_phase4_io.py#L98) | 3 | `io/test_http.py` | 保持・移管 |
| [`test_http_invalid_input_is_not_success`](../../tests/nonlinear/test_phase4_io.py#L117) | 7 | `io/test_http.py` | 保持・移管 |
| [`test_http_failed_analysis_and_reanalysis`](../../tests/nonlinear/test_phase4_io.py#L141) | 4 | `io/test_http.py` | 保持・移管 |
| [`test_comparator_rejects_missing_extra_wrong_sign_and_number`](../../tests/nonlinear/test_phase4_io.py#L161) | 1 | `harness/test_comparison.py` | 保持・移管 |
| [`test_missing_sample_reference_never_writes`](../../tests/nonlinear/test_phase4_io.py#L171) | 1 | `harness/test_sample_runner.py` | 保持・移管 |
| [`test_model_save_load_preserves_nonlinear_input_and_result`](../../tests/nonlinear/test_phase4_io.py#L181) | 1 | `io/test_model_roundtrip.py` | 保持・移管 |
| [`test_invalid_load_factors_rejected`](../../tests/nonlinear/test_phase4_io.py#L199) | 4 | `io/test_http.py` | 保持・移管 |
| [`test_real_jr_failure_restores_last_commit_and_fresh_run`](../../tests/nonlinear/test_phase4_io.py#L206) | 1 | `integration/test_jr_beam_history.py` | 保持・移管 |
| [`test_failed_postprocessing_clears_result_and_http_is_error`](../../tests/nonlinear/test_phase4_io.py#L233) | 2 | `io/test_http.py` | 保持・移管 |
| [`test_json_does_not_add_unrequested_rotational_restraint`](../../tests/nonlinear/test_phase4_io.py#L254) | 1 | `io/test_legacy_input.py` | 保持・移管 |
| [`test_sample_comparison_really_rejects_corrupted_reference`](../../tests/nonlinear/test_phase4_io.py#L264) | 5 | `harness/test_sample_runner.py` | 保持・移管 |
| [`test_explicit_static_spring_reactions_balance`](../../tests/nonlinear/test_phase4_io.py#L290) | 1 | `io/test_http.py` | 保持・移管 |
| [`test_omitted_nu_has_same_nonlinear_default_in_python_json_and_http`](../../tests/nonlinear/test_phase4_io.py#L302) | 1 | `integration/test_input_routes.py` | 保持・移管 |
| [`test_failed_file_reload_does_not_leave_previous_success`](../../tests/nonlinear/test_phase4_io.py#L321) | 1 | `io/test_model_lifecycle.py` | 保持・移管 |

### nonlinear/test_phase4_reference.py

4モード・3経路・正負/β/制御/分割の独立解析解、履歴仕事、ばね、非一様曲げ収束、beam001と配置回転。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_uniform_modes_independent_inverse_skeleton`](../../tests/nonlinear/test_phase4_reference.py#L113) | 96 | `integration/test_nonlinear_reference.py` | 保持・移管 |
| [`test_cyclic_polygon_residual_deformation_and_work`](../../tests/nonlinear/test_phase4_reference.py#L122) | 96 | `integration/test_nonlinear_reference.py` | 保持・移管 |
| [`test_public_spring_api_participates_in_equilibrium`](../../tests/nonlinear/test_phase4_reference.py#L168) | 3 | `integration/test_nonlinear_reference.py` | 保持・移管 |
| [`test_nested_reversal_and_damage_independent_polygon`](../../tests/nonlinear/test_phase4_reference.py#L185) | 192 | `integration/test_nonlinear_reference.py` | 保持・移管 |
| [`test_elastic_cantilever_force_moment_balance`](../../tests/nonlinear/test_phase4_reference.py#L232) | 6 | `integration/test_nonlinear_reference.py` | 保持・移管 |
| [`test_load_increment_and_tolerance_sensitivity`](../../tests/nonlinear/test_phase4_reference.py#L247) | 3 | `integration/test_nonlinear_convergence.py` | 保持・移管 |
| [`test_beam001_independent_flexibility_reference`](../../tests/nonlinear/test_phase4_reference.py#L254) | 2 | `regression/test_beam001.py` | 保持・移管 |
| [`test_beam001_reference_inverse_at_hand_calculated_ordinates`](../../tests/nonlinear/test_phase4_reference.py#L270) | 5 | `validation/test_stored_references.py` | 保持・移管 |
| [`test_nonuniform_bending_discrete_compliance_and_continuum_limit`](../../tests/nonlinear/test_phase4_reference.py#L280) | 5 | `integration/test_nonlinear_convergence.py` | 保持・移管 |
| [`test_explicit_G_elastic_torsion`](../../tests/nonlinear/test_phase4_reference.py#L314) | 3 | `integration/test_nonlinear_reference.py` | 保持・移管 |
| [`test_two_members_with_intermediate_load_have_independent_equilibrium`](../../tests/nonlinear/test_phase4_reference.py#L323) | 3 | `integration/test_nonlinear_reference.py` | 保持・移管 |
| [`test_rotated_member_global_displacements_and_equilibrium`](../../tests/nonlinear/test_phase4_reference.py#L339) | 24 | `integration/test_nonlinear_reference.py` | 保持・移管 |

### nonlinear/test_solver.py

JRと独立の弾性・三次・耐力制限ばねによるNewton、残差・拘束・反力、失敗stepの復元、制御値検証。モデルライフサイクルを分離。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_linear_bar_through_newton_matches_hand_calculation`](../../tests/nonlinear/test_solver.py#L35) | 1 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_prescribed_motion_without_external_force_is_applied_once_per_step`](../../tests/nonlinear/test_solver.py#L44) | 1 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_spring_force_participates_in_residual_and_reactions`](../../tests/nonlinear/test_solver.py#L52) | 1 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_unconverged_step_rolls_back_and_stops_before_callback`](../../tests/nonlinear/test_solver.py#L91) | 1 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_unrestrained_model_is_rejected_not_regularized`](../../tests/nonlinear/test_solver.py#L105) | 1 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_fixed_node_load_does_not_hide_free_dof_imbalance`](../../tests/nonlinear/test_solver.py#L112) | 1 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_cubic_spring_converges_to_known_nonlinear_root`](../../tests/nonlinear/test_solver.py#L120) | 1 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_failed_second_step_restores_first_converged_state`](../../tests/nonlinear/test_solver.py#L148) | 1 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_failed_fem_run_clears_previous_results`](../../tests/nonlinear/test_solver.py#L166) | 1 | `io/test_model_lifecycle.py` | 保持・移管 |
| [`test_invalid_controls_rejected_before_analysis`](../../tests/nonlinear/test_solver.py#L182) | 7 | `solvers/test_nonlinear.py` | 保持・移管 |
| [`test_programmatic_fem_model_has_analysis_defaults`](../../tests/nonlinear/test_solver.py#L187) | 1 | `io/test_model_lifecycle.py` | 保持・移管 |
| [`test_nonconsecutive_node_ids_preserve_reactions_and_displacements`](../../tests/nonlinear/test_solver.py#L197) | 2 | `integration/test_model_contracts.py` | 保持・移管 |

### test_beam001_reference_values.py

保存101段階と独立Decimal/float式、分岐前後の手計算値、step62の変位・反力・端力改変検出。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_stored_beam001_all_steps_against_independent_scalar_reference`](../../tests/test_beam001_reference_values.py#L11) | 1 | `validation/test_stored_references.py` | 保持・移管 |
| [`test_beam001_hand_calculated_branch_crossings_and_tip_motion`](../../tests/test_beam001_reference_values.py#L36) | 1 | `validation/test_stored_references.py` | 保持・移管 |
| [`test_late_step_reference_mutation_is_detected`](../../tests/test_beam001_reference_values.py#L51) | 3 | `harness/test_sample_runner.py` | 保持・移管 |

### test_legacy_shear_input.py

G省略と明示shearの優先、線形/非線形、選択材料ケース・剛域。ファイル/HTTP/保存の同値。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_g_omission_and_explicit_shear_flag`](../../tests/test_legacy_shear_input.py#L15) | 12 | `io/test_legacy_input.py` | 保持・移管 |
| [`test_selected_case_and_rigid_zone_use_their_own_g`](../../tests/test_legacy_shear_input.py#L36) | 1 | `io/test_legacy_input.py` | 保持・移管 |
| [`test_omitted_g_file_http_and_saved_model_agree`](../../tests/test_legacy_shear_input.py#L47) | 1 | `integration/test_input_routes.py` | 保持・移管 |

### test_phase4_input_decisions.py

非梁で梁断面を要求しない、旧Aの厚さ解釈。Pressure/Tri1の入力同一性・保存参照と面圧支持の解析は分離。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_pressure_saved_reference_is_independent_exact_polynomial_solution`](../../tests/test_phase4_input_decisions.py#L16) | 1 | `validation/test_stored_references.py` | 保持・移管 |
| [`test_tri1_conditions_match_the_original_fem`](../../tests/test_phase4_input_decisions.py#L23) | 1 | `validation/test_stored_references.py` | 保持・移管 |
| [`test_nonbeam_elements_require_no_beam_section`](../../tests/test_phase4_input_decisions.py#L37) | 4 | `io/test_legacy_input.py` | 保持・移管 |
| [`test_legacy_shell_A_is_thickness_not_area`](../../tests/test_phase4_input_decisions.py#L51) | 1 | `io/test_legacy_input.py` | A採用とA=0拒否。ケース未指定条件も移管して統合 |
| [`test_pressure_fix_is_identical_in_json_and_original_format_and_balances_load`](../../tests/test_phase4_input_decisions.py#L61) | 1 | `integration/test_pressure_solution.py` | 保持・移管 |

### test_phase4_legacy.py

旧入力の梁解析：材料/回転、分割、荷重、支持・端部解放、分布ばね、温度。ソルバー・保存・比較器・監査・beam001が混在。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_legacy_bernoulli_section_and_cg`](../../tests/test_phase4_legacy.py#L30) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_disabled_member_load_ignores_stale_values_and_does_not_split`](../../tests/test_phase4_legacy.py#L40) | 2 | `io/test_legacy_input.py` | 保持・移管 |
| [`test_generated_point_coordinates_keep_submillimetre_precision`](../../tests/test_phase4_legacy.py#L51) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_uniform_and_triangular_load_consistent_forces`](../../tests/test_phase4_legacy.py#L63) | 3 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_released_tip_propped_beam`](../../tests/test_phase4_legacy.py#L76) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_legacy_support_spring_is_not_prescribed_motion`](../../tests/test_phase4_legacy.py#L89) | 3 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_rigid_zone_uses_its_assigned_section`](../../tests/test_phase4_legacy.py#L98) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_point_load_and_negative_width_split`](../../tests/test_phase4_legacy.py#L107) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_distributed_axial_foundation_exact_solution`](../../tests/test_phase4_legacy.py#L119) | 1 | `integration/test_beam_foundation.py` | 保持・移管 |
| [`test_public_distributed_load_and_joint_apis`](../../tests/test_phase4_legacy.py#L129) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_timoshenko_uniform_load_includes_shear_and_fixed_end_forces`](../../tests/test_phase4_legacy.py#L143) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_foundation_constant_patch_and_linear_nonlinear_route`](../../tests/test_phase4_legacy.py#L153) | 1 | `integration/test_beam_foundation.py` | 保持・移管 |
| [`test_fully_released_unloaded_rotation_is_absent_not_stabilized`](../../tests/test_phase4_legacy.py#L169) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_split_roundoff_and_other_case_subdivisions_keep_loads_separate`](../../tests/test_phase4_legacy.py#L179) | 1 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_saved_legacy_member_features_retain_the_same_physical_model`](../../tests/test_phase4_legacy.py#L190) | 1 | `io/test_model_roundtrip.py` | 保持・移管 |
| [`test_member_and_shell_number_namespaces_do_not_overwrite_each_other`](../../tests/test_phase4_legacy.py#L207) | 1 | `io/test_legacy_input.py` | 保持・移管 |
| [`test_temperature_load_free_expansion_and_restrained_force`](../../tests/test_phase4_legacy.py#L218) | 2 | `integration/test_linear_beam.py` | 保持・移管 |
| [`test_audit_visits_later_case_after_first_error_and_never_writes_reference`](../../tests/test_phase4_legacy.py#L231) | 1 | `harness/test_sample_runner.py` | 保持・移管 |
| [`test_legacy_cut_signs_and_labels_have_independent_physical_values`](../../tests/test_phase4_legacy.py#L246) | 1 | `harness/test_comparison.py` | 保持・移管 |
| [`test_beam001_step_zero_reference_is_compared_in_addition_to_all_step_oracle`](../../tests/test_phase4_legacy.py#L263) | 4 | `harness/test_sample_runner.py` | 保持・移管 |
| [`test_linear_solver_scales_without_artificial_stiffness`](../../tests/test_phase4_legacy.py#L290) | 1 | `solvers/test_linear.py` | 保持・移管 |
| [`test_linear_solver_rejects_rigid_mode`](../../tests/test_phase4_legacy.py#L297) | 3 | `solvers/test_linear.py` | 保持・移管 |

### test_phase4_quadratic_reference.py

Decimalの独立なアフィン仕事・剛体モード・逆行列の自己検証と、二次ソリッドの製品全出力比較。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_original_shape_stiffness_has_exact_affine_work_and_rigid_modes`](../../tests/test_phase4_quadratic_reference.py#L17) | 3 | `validation/test_v0_solid_reference.py` | 保持・移管 |
| [`test_decimal_reference_inverse_on_skew_jacobian`](../../tests/test_phase4_quadratic_reference.py#L33) | 1 | `validation/test_v0_solid_reference.py` | 保持・移管 |
| [`test_quadratic_solid_all_outputs_against_independent_decimal_source`](../../tests/test_phase4_quadratic_reference.py#L45) | 3 | `validation/test_solid_sources.py` | 保持・移管 |

### test_phase4_shell_input.py

選択材料ケースのA→厚さ、明示厚さ/formulationの優先、厚さの欠落・不正値拒否、入力不変性。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_selected_legacy_A_defines_shell_thickness`](../../tests/test_phase4_shell_input.py#L15) | 1 | `io/test_legacy_input.py` | 保持・移管 |
| [`test_explicit_shell_thickness_and_mindlin_override_are_preserved`](../../tests/test_phase4_shell_input.py#L23) | 1 | `io/test_legacy_input.py` | 保持・移管 |
| [`test_missing_or_invalid_legacy_shell_thickness_is_rejected`](../../tests/test_phase4_shell_input.py#L32) | 4 | `io/test_legacy_input.py` | 保持・移管 |

### test_phase4_source_evidence.py

原資料と入力・単位・網羅性の同一性、旧転記不具合の再現、材料/幾何所見。製品ソリッド解析と面圧機構を分離。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_complete_external_solid_input_and_repaired_displacement`](../../tests/test_phase4_source_evidence.py#L13) | 5 | `validation/test_provenance.py` | 保持・移管 |
| [`test_missing_json_connectivity_is_proved_against_original_fem`](../../tests/test_phase4_source_evidence.py#L34) | 4 | `validation/test_provenance.py` | 保持・移管 |
| [`test_tetra_printed_reference_is_incomplete_and_not_same_model_proof`](../../tests/test_phase4_source_evidence.py#L46) | 1 | `validation/test_provenance.py` | 保持・移管 |
| [`test_source_audit_detects_input_change_instead_of_trusting_same_file_name`](../../tests/test_phase4_source_evidence.py#L54) | 1 | `validation/test_provenance.py` | 保持・移管 |
| [`test_stale_notice_record_does_not_stop_the_numerical_audit`](../../tests/test_phase4_source_evidence.py#L62) | 1 | `validation/test_provenance.py` | 保持・移管 |
| [`test_selected_material_case_and_rounded_geometry_are_reported`](../../tests/test_phase4_source_evidence.py#L68) | 1 | `validation/test_provenance.py` | 保持・移管 |
| [`test_missing_topology_has_an_input_error_before_linear_algebra`](../../tests/test_phase4_source_evidence.py#L83) | 3 | `io/test_model_lifecycle.py` | 保持・移管 |
| [`test_v0_solid_full_displacements_and_force_moment_equilibrium`](../../tests/test_phase4_source_evidence.py#L94) | 2 | `validation/test_solid_sources.py` | 全変位・釣合い・ファイル不変性を保存して大規模比較を統合 |
| [`test_releasing_pressure_fixture_rotations_creates_a_loaded_rigid_mode`](../../tests/test_phase4_source_evidence.py#L120) | 1 | `integration/test_pressure_solution.py` | 保持・移管 |

### test_phase4_source_repairs.py

節点/荷重/拘束/材料/種類/次元/case/既存値の改変拒否。修復冪等性と製品全変位・釣合いの混在を分離。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_source_repair_rejects_changed_model_or_unknown_reference`](../../tests/test_phase4_source_repairs.py#L13) | 8 | `harness/test_source_repairs.py` | 保持・移管 |
| [`test_repaired_solid_against_all_external_displacements`](../../tests/test_phase4_source_repairs.py#L28) | 5 | `validation/test_solid_sources.py + harness/test_source_repairs.py` | 製品比較と修復冪等性を分割して保持 |

### test_phase4_support_repairs.py

支持反力補完の入力・hash・残差・網羅性・未知参照拒否、原入力とdisg保持、冪等性。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_support_reference_repair_rejects_unverified_evidence`](../../tests/test_phase4_support_repairs.py#L19) | 6 | `harness/test_source_repairs.py` | 保持・移管 |
| [`test_support_reference_completion_preserves_inputs_and_is_idempotent`](../../tests/test_phase4_support_repairs.py#L31) | 1 | `harness/test_source_repairs.py` | 保持・移管 |

### test_phase4_triangle_reference.py

Tri1の元DKT演算子による保存全出力、外部変位・残差・240要素網羅と入力改変拒否。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_all_tri1_outputs_match_original_operators_without_production_imports`](../../tests/test_phase4_triangle_reference.py#L11) | 1 | `validation/test_stored_references.py` | 保持・移管 |
| [`test_tri1_reference_rejects_changed_input_conditions`](../../tests/test_phase4_triangle_reference.py#L22) | 7 | `harness/test_source_repairs.py` | 保持・移管 |

### test_phase4_v0_import.py

製品V0読込の材料/厚さ/拘束/荷重加算、行番号付き厳密拒否、元Hexa全レコード・3DOF。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_v0_shell_material_thickness_restraint_load_records`](../../tests/test_phase4_v0_import.py#L9) | 1 | `io/test_v0_input.py` | 保持・移管 |
| [`test_v0_bad_input_rejected_with_line_number`](../../tests/test_phase4_v0_import.py#L37) | 11 | `io/test_v0_input.py` | 保持・移管 |
| [`test_original_hexa_input_preserves_all_records`](../../tests/test_phase4_v0_import.py#L42) | 1 | `io/test_v0_input.py` | 保持・移管 |

### test_phase4_v0_input_reference.py

参照側.femの手パッチ・不正レコード拒否、Tetra1の出典/input-only/hash/残差/網羅性ガードと製品全出力。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_source_input_only_tetra_solves_hand_axial_patch`](../../tests/test_phase4_v0_input_reference.py#L11) | 1 | `validation/test_v0_solid_reference.py` | 保持・移管 |
| [`test_source_input_rejects_unsupported_dangling_and_duplicate_records`](../../tests/test_phase4_v0_input_reference.py#L35) | 3 | `validation/test_v0_solid_reference.py` | 保持・移管 |
| [`test_tetra_input_repair_rejects_unverified_conditions`](../../tests/test_phase4_v0_input_reference.py#L58) | 9 | `harness/test_source_repairs.py` | 保持・移管 |
| [`test_tetra_complete_reference_all_outputs_and_input_preservation`](../../tests/test_phase4_v0_input_reference.py#L73) | 1 | `validation/test_solid_sources.py + harness/test_source_repairs.py` | 製品比較と修復冪等性を分割して保持 |

### test_phase4_v0_support_reference.py

旧JS支持反力の手パッチ、不完全出力拒否、元出力変位に依存せず荷重から解く残差補正。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_original_v0_tetra_uniform_axial_stress_supports`](../../tests/test_phase4_v0_support_reference.py#L9) | 1 | `validation/test_v0_solid_reference.py` | 保持・移管 |
| [`test_original_v0_rejects_partial_output_without_echo`](../../tests/test_phase4_v0_support_reference.py#L35) | 1 | `validation/test_v0_solid_reference.py` | 保持・移管 |
| [`test_refined_source_solves_load_not_inaccurate_output_displacement`](../../tests/test_phase4_v0_support_reference.py#L44) | 6 | `validation/test_v0_solid_reference.py` | 保持・移管 |

### test_pressure_integration.py

JSON/FEM読込、複数面圧、保存再読込。等価節点荷重の総力/モーメント検証を要素へ移す。現状ソルバー実行はない。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`TestPressureIntegration.test_shell_with_pressure_load_json`](../../tests/test_pressure_integration.py#L28) | 1 | `io/test_pressure.py` | 保持・移管 |
| [`TestPressureIntegration.test_shell_with_pressure_load_fem`](../../tests/test_pressure_integration.py#L96) | 1 | `io/test_pressure.py` | 保持・移管 |
| [`TestPressureIntegration.test_pressure_vs_concentrated_load_equivalence`](../../tests/test_pressure_integration.py#L140) | 1 | `elements/shell/test_pressure.py` | 全節点値・総力・総モーメント。面圧case行列へ統合候補 |
| [`TestPressureIntegration.test_multiple_pressure_loads`](../../tests/test_pressure_integration.py#L186) | 1 | `io/test_pressure.py` | 保持・移管 |
| [`TestPressureIntegration.test_pressure_file_io_roundtrip`](../../tests/test_pressure_integration.py#L258) | 1 | `io/test_pressure.py` | 保持・移管 |

### test_run_data.py

bar/shell/bendの全保存サンプルをファイル単位で厳密比較。snapはbeam001の全step独立解と保存101段階。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_bar_elements`](../../tests/test_run_data.py#L19) | 23 | `regression/test_legacy_samples.py` | 全対象保持。case単位収集への変更は別段階 |
| [`test_shell_elements`](../../tests/test_run_data.py#L32) | 14 | `regression/test_legacy_samples.py` | 全対象保持。case単位収集への変更は別段階 |
| [`test_bend_elements`](../../tests/test_run_data.py#L45) | 7 | `regression/test_legacy_samples.py` | 全対象保持。case単位収集への変更は別段階 |
| [`test_snap_elements`](../../tests/test_run_data.py#L58) | 1 | `regression/test_beam001.py` | beam001の全段階比較を保持 |

### test_solver_boundary_conditions.py

6/3DOF stride、最後の節点拘束、強制変位RHSと対称性、ばね、反力、Newtonの残り強制変位。

| 関数（クラスを含む） | case | 移管先（tests/相対） | 処置・補足 |
|---|---:|---|---|
| [`test_only_last_node_is_fixed_in_four_node_frame`](../../tests/test_solver_boundary_conditions.py#L11) | 1 | `solvers/test_boundary_conditions.py` | 保持・移管 |
| [`test_prescribed_displacement_corrects_free_rhs_and_preserves_symmetry`](../../tests/test_solver_boundary_conditions.py#L24) | 1 | `solvers/test_boundary_conditions.py` | 保持・移管 |
| [`test_solid_boundary_uses_explicit_three_dof_stride`](../../tests/test_solver_boundary_conditions.py#L40) | 1 | `solvers/test_boundary_conditions.py` | 保持・移管 |
| [`test_spring_support_is_not_a_fixed_displacement`](../../tests/test_solver_boundary_conditions.py#L50) | 1 | `solvers/test_boundary_conditions.py` | 保持・移管 |
| [`test_reaction_uses_actual_node_stride`](../../tests/test_solver_boundary_conditions.py#L60) | 1 | `solvers/test_boundary_conditions.py` | 保持・移管 |
| [`test_newton_bc_enforces_remaining_prescribed_motion_and_spring_force`](../../tests/test_solver_boundary_conditions.py#L70) | 1 | `solvers/test_boundary_conditions.py` | 保持・移管 |

## 4. 全45JSONの状態と保管方針

実行結果はファイル単位。失敗理由は今回最初に報告されたものだけを示し、内部の全caseの診断ではない。GF-04は梁全体の上位課題、GF-01〜03はその一部。成功/失敗によってデータを削除しない。

| JSON（tests/data/相対） | 内部case/step | 今回の結果 | 残件・扱い |
|---|---:|---|---|
| [bar/2D_Sample01.json](../../tests/data/bar/2D_Sample01.json) | 24 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample02.json](../../tests/data/bar/2D_Sample02.json) | 22 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample03.json](../../tests/data/bar/2D_Sample03.json) | 32 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample04.json](../../tests/data/bar/2D_Sample04.json) | 10 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample05.json](../../tests/data/bar/2D_Sample05.json) | 19 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample06.json](../../tests/data/bar/2D_Sample06.json) | 9 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample07.json](../../tests/data/bar/2D_Sample07.json) | 18 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample08.json](../../tests/data/bar/2D_Sample08.json) | 4 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample09.json](../../tests/data/bar/2D_Sample09.json) | 9 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample10.json](../../tests/data/bar/2D_Sample10.json) | 22 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample11.json](../../tests/data/bar/2D_Sample11.json) | 3 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/2D_Sample12.json](../../tests/data/bar/2D_Sample12.json) | 1 | 成功 | 維持 |
| [bar/2D_Sample13.json](../../tests/data/bar/2D_Sample13.json) | 16 | 失敗：Singular stiffness matrix: zero diagonal | GF-02 / GF-04 |
| [bar/3D_Sample01.json](../../tests/data/bar/3D_Sample01.json) | 23 | 失敗：A loaded or supported beam mode requires positive rigidity | GF-01 / GF-04 |
| [bar/3D_Sample02.json](../../tests/data/bar/3D_Sample02.json) | 9 | 失敗：Singular stiffness matrix: numerical rank deficiency | GF-03 / GF-04 |
| [bar/3D_Sample03.json](../../tests/data/bar/3D_Sample03.json) | 33 | 失敗：Singular stiffness matrix: numerical rank deficiency | GF-03 / GF-04 |
| [bar/3D_Sample04.json](../../tests/data/bar/3D_Sample04.json) | 20 | 失敗：A loaded or supported beam mode requires positive rigidity | GF-01 / GF-04 |
| [bar/3D_Sample05.json](../../tests/data/bar/3D_Sample05.json) | 4 | 失敗：A loaded or supported beam mode requires positive rigidity | GF-01 / GF-04 |
| [bar/3D_Sample06.json](../../tests/data/bar/3D_Sample06.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/3D_Sample07.json](../../tests/data/bar/3D_Sample07.json) | 1 | 失敗：Linear frame failed constitutive equilibrium refinement | GF-03 / GF-04 |
| [bar/3D_Sample08.json](../../tests/data/bar/3D_Sample08.json) | 5 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/3D_Sample09.json](../../tests/data/bar/3D_Sample09.json) | 8 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [bar/3D_Sample10.json](../../tests/data/bar/3D_Sample10.json) | 9 | 失敗：保存参照の数値／キー不一致 | GF-04 |
| [shell/3D_Sample01.json](../../tests/data/shell/3D_Sample01.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellBeamQuad1.json](../../tests/data/shell/shellBeamQuad1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellBeamTri1.json](../../tests/data/shell/shellBeamTri1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellPressureTest1.json](../../tests/data/shell/shellPressureTest1.json) | 1 | 成功 | 維持：拘束6DOF・独立有理数参照 |
| [shell/shellQuad1.json](../../tests/data/shell/shellQuad1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellQuad1_t1.json](../../tests/data/shell/shellQuad1_t1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellQuad1_t2.json](../../tests/data/shell/shellQuad1_t2.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellRibQuad1.json](../../tests/data/shell/shellRibQuad1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellRibTri1.json](../../tests/data/shell/shellRibTri1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellTensTorQuad1.json](../../tests/data/shell/shellTensTorQuad1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellTensTorTri1.json](../../tests/data/shell/shellTensTorTri1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellThickBeamQuad1.json](../../tests/data/shell/shellThickBeamQuad1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellThickBeamTri1.json](../../tests/data/shell/shellThickBeamTri1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [shell/shellTri1.json](../../tests/data/shell/shellTri1.json) | 1 | 失敗：保存参照の数値／キー不一致 | GF-05 |
| [bend/sampleBendHexa1.json](../../tests/data/bend/sampleBendHexa1.json) | 1 | 成功 | 維持：原資料との同一性・独立参照も別に検証 |
| [bend/sampleBendHexa2.json](../../tests/data/bend/sampleBendHexa2.json) | 1 | 成功 | 維持：原資料との同一性・独立参照も別に検証 |
| [bend/sampleBendTetra1.json](../../tests/data/bend/sampleBendTetra1.json) | 1 | 成功 | 維持：原資料との同一性・独立参照も別に検証 |
| [bend/sampleBendTetra2.json](../../tests/data/bend/sampleBendTetra2.json) | 1 | 成功 | 維持：原資料との同一性・独立参照も別に検証 |
| [bend/sampleBendTri1.json](../../tests/data/bend/sampleBendTri1.json) | 1 | 成功 | 維持：原資料との同一性・独立参照も別に検証 |
| [bend/sampleBendWedge1.json](../../tests/data/bend/sampleBendWedge1.json) | 1 | 成功 | 維持：原資料との同一性・独立参照も別に検証 |
| [bend/sampleBendWedge2.json](../../tests/data/bend/sampleBendWedge2.json) | 1 | 成功 | 維持：原資料との同一性・独立参照も別に検証 |
| [snap/beam001.json](../../tests/data/snap/beam001.json) | 101 step | 成功 | 維持：独立数学参照。外部較正GF-06とは別 |

## 5. テスト以外の全ファイル

57 Pythonのうち38テスト以外の19ファイル、およびCJS 2ファイルは以下。責務別の具体的な移管・退役条件は[計画5.3](test-reorganization.md#53-補助修復スクリプト)を参照。

| 現ファイル | 役割／扱い |
|---|---|
| [conftest.py](../../tests/conftest.py) | srcとルートのsys.path設定。pytest設定へ移管後に縮小 |
| [run_sample.py](../../tests/run_sample.py) | 比較・旧表示・解析runner・非線形受け入れの複合基盤。分割 |
| [reference_solutions.py](../../tests/reference_solutions.py) | 軸棒とbeam001の独立float式・比較。oracleとassertを分離 |
| [beam001_reference_values.py](../../tests/beam001_reference_values.py) | 独立Decimal参照の算出・照合・明示書込CLI。関数とCLIを分離 |
| [pressure_reference.py](../../tests/pressure_reference.py) | SymPy有理数によるMITC4面圧参照。維持 |
| [v0_decimal_reference.py](../../tests/v0_decimal_reference.py) | 旧JS形状式を50桁評価し剛性・全体系を構成。維持 |
| [v0_refined_reference.py](../../tests/v0_refined_reference.py) | 元V0剛性から独立な釣合い解・残差補正。維持 |
| [v0_triangle_reference.py](../../tests/v0_triangle_reference.py) | 旧DKT演算子と独立な両面テンソル積分。維持 |
| [phase4_source_evidence.py](../../tests/phase4_source_evidence.py) | 原資料・入力所見・網羅性・単位の監査。provenanceへ |
| [audit_phase4_beam001.py](../../tests/audit_phase4_beam001.py) | 数学参照・収束・外部資料の診断出力。toolsへ |
| [audit_phase4_samples.py](../../tests/audit_phase4_samples.py) | 全load/reference caseを例外後も監査。toolsへ |
| [audit_phase4_support_references.py](../../tests/audit_phase4_support_references.py) | 原資料の独立反力と製品反力の監査。toolsへ |
| [restore_phase4_sources.py](../../tests/restore_phase4_sources.py) | 原資料による接続・変位の明示修復。関数と移行CLIへ |
| [restore_phase4_supports.py](../../tests/restore_phase4_supports.py) | 反力・size・空fieldの出典付き補完。関数と移行CLIへ |
| [restore_phase4_tetra1.py](../../tests/restore_phase4_tetra1.py) | 不完全.outに代えて完全.femからTetra1参照修復。関数と移行CLIへ |
| [restore_phase4_tri1.py](../../tests/restore_phase4_tri1.py) | 原.femのDKTモデル同一性・Tri1参照修復。関数と移行CLIへ |
| [restore_phase4_pressure.py](../../tests/restore_phase4_pressure.py) | 固定入力条件の有理数面圧参照修復。関数と移行CLIへ |
| [remove_retired_shell_output.py](../../tests/remove_retired_shell_output.py) | 廃止済shell_fsecの1回限りのschema移行。履歴保存後の退役候補 |
| [executeForDebug.py](../../tests/executeForDebug.py) | Flet GUI/バッチでstatic解析とJSON/VTK出力。手動toolsへ |
| [v0_support_reference.cjs](../../tests/v0_support_reference.cjs) | 元V0ソリッドのJS演算子・入力・反力/体系の出力。oracle依存として維持 |
| [v0_triangle_reference.cjs](../../tests/v0_triangle_reference.cjs) | 元V0 DKTのJS演算子・入力・体系の出力。oracle依存として維持 |

### その他のGit管理データ

| ファイル | 役割／扱い |
|---|---|
| [data/bend/sampleBendHexa1.vtk](../../tests/data/bend/sampleBendHexa1.vtk) | 手動VTK出力例。pytest読取なし。必要な例ならdocs/examplesへ、再生成可能なだけなら退役候補 |
| [data/bend/sampleBendTetra1.vtk](../../tests/data/bend/sampleBendTetra1.vtk) | 手動VTK出力例。pytest読取なし。必要な例ならdocs/examplesへ、再生成可能なだけなら退役候補 |
| [data/bend/sampleBendTri1.vtk](../../tests/data/bend/sampleBendTri1.vtk) | 手動VTK出力例。pytest読取なし。必要な例ならdocs/examplesへ、再生成可能なだけなら退役候補 |
| [data/bend/sampleBendWedge1.vtk](../../tests/data/bend/sampleBendWedge1.vtk) | 手動VTK出力例。pytest読取なし。必要な例ならdocs/examplesへ、再生成可能なだけなら退役候補 |
| [data/shell/shellPressureTest1.fem](../../tests/data/shell/shellPressureTest1.fem) | 面圧JSONと対になる製品V0入力。保持 |
| [data/snap/beam001.ndt](../../tests/data/snap/beam001.ndt) | 外部計算の原資料。保持、内部数学参照と区別 |
| [data/snap/beam002.ndt](../../tests/data/snap/beam002.ndt) | 外部計算の原資料。保持、内部数学参照と区別 |
| [data/snap/beam002.ndu](../../tests/data/snap/beam002.ndu) | 外部計算の原資料。保持、内部数学参照と区別 |

Git管理外の外部資料（OUT/FMT等）とキャッシュは上記112ファイルに含めない。外部資料の自動削除・移動は本計画の対象外。
原V0コード・`.fem/.out` は `docs/v0/` にあるテスト依存資料として保持する。歴史的なJSアプリ一式を現行pytest件数へ加算しない。

## 6. 調査で残す注意点

- 手パッチ／変換の不変性／旧実装由来の参照／保存goldの回帰は証拠の種類が異なる。同じモデル名でも自動的な重複判定をしない。
- 修復ガードを独立ファイルへ移す際、今は1関数内に同居する「製品数値」と「ガード・冪等性」の両方を移管する。
- `run_sample`が意図して比較するlegacy fieldと製品全出力は同一ではない。solid応力やmodern shell outputは専用パッチ・公開出力試験に依存している。
- 欠落キー・符号反転・数字改変、step0・後続step、初回caseエラー後の監査継続はharnessの必須契約。移動時に落とさない。
- 本台帳の旧名は移行対応の証拠。移行後は実際の新node IDと統合理由を追記し、古い264行を無言で書き換えて消さない。
