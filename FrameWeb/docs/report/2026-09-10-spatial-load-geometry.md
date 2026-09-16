# 面荷重の第2段階: 平面幾何・補間・求積

日付: 2026-09-10

対象計画: [面荷重実装計画](../plans/面荷重実装計画.md)

## 開始状態

- HEAD: `643ca81982d25ac846eb26fbb82269bf6b32f722`
- 作業ツリー: 未コミット変更なし。第1段階の入力処理はHEADへ取り込み済み。
- 環境: Windows 11、Python 3.13.11、NumPy 2.4.1、SciPy 1.17.0、pytest 9.0.2、SymPy 1.14.0。
- 既存 `.venv` / `uv.lock` を使用し、依存関係は追加・更新していない。
- 前回の全2082件成功は[入力処理の作業記録](2026-09-10-spatial-load-input.md)を参照。

## 実装した責務

`src/fem/spatial_loads/geometry.py` に平面基底、平面性判定、凸多角形の検証・交差・クリッピング、
求積用の三角形分割、パネル接続の外周復元、線区間の所有判定を追加した。
基底計算は長い基線を選び、大きな原点オフセットに対して座標を平行移動して計算する。
辺は長さ、外積・面積は長さ許容値×代表長さ、逆写像は無次元化した座標で評価する。

パネルには連結したT3/Q4または明示載荷三角形を使う。反転混在、退化、重複座標、未使用節点、
非多様体・分岐外周、非凸外周、穴、セル内部の重なりを拒否する。全セルの一括向き反転は許容する。
共有辺に一致した線区間の所有者は最小セルキーとし、同じ区間を二重に積分しない。
クリッピングは入力境界の半平面交差であり、節点集合の凸包は生成しない。

`interpolation.py` にT3重心座標、Q4形状関数と自然座標逆写像、折れ線の投影・交差検査、
正規化累積弧長の対応と載荷帯の強度評価を追加した。Q4は平行移動・縮尺正規化したNewton法と
後退探索で逆写像し、残差と内外判定を確認する。自然座標を切り詰めて外挿しない。
幾何の入力許容値をそのまま反復打切り誤差にせず、逆写像は機械精度近くまで解く。

二本目のラインは端点距離二乗和で整列し、向きを変える場合は始終点強度も同時に反転する。
両ラインの正規化弧長の折点を統合し、各セルで横断補間する。対応が曖昧、幅ゼロ、交差、反転、
重なり、非凸の載荷帯を拒否する。構造メッシュの対角線によって荷重場を再定義しない。

`quadrature.py` に次数指定のGauss線求積、Duffy写像による三角形求積、4点／8点則と
4×4／8×8則の比較、線二分割／三角形四分割による適応求積を追加した。
積分重みは物理長・物理面積を含み、シェルのJacobianを重ねて掛けない。
複数区間・三角形を渡した場合、成分別の推定誤差を全領域で合算してから収束を判定する。
相対尺度は各成分の絶対値積分であり、正負荷重の相殺でゼロにならない。
絶対・相対許容値は成分別配列も受け取り、力とモーメントに異なる単位の絶対許容値を指定できる。
非有限な被積分関数、積分値のオーバーフロー、細分化上限での未収束は例外にする。
推定誤差は数学的な厳密上界ではないため、比較則の一致とは別に独立積分でも検証した。

`validation.py` の `prepare_panel()` / `prepare_geometry()` がこれらを接続する。
入力定義とメッシュを変更せず、不変なパネル・線区間・載荷帯セル・求積三角形を返す。
セルには構造節点IDとシェル内部要素IDを保持し、載荷三角形との区別を第3段階へ引き継ぐ。
載荷帯セルごとに、クリップ後の合計面積と元の面積も照合する。

## 受入項目と試験の対応

| 受入項目 | 主な試験 |
|---|---|
| 基底の安定性、移動・縮尺・向き | `test_plane_projection_is_stable_translated_scaled_and_rotated` |
| 不正トポロジー・非凸・穴・平面逸脱 | `test_invalid_panel_is_rejected` |
| 領域クリップの面積と一次モーメント | `test_clip_has_exact_area_and_first_moments_without_convex_hull_expansion` |
| 共有辺の区間所有と全長保存 | `test_line_intervals_cover_path_once_including_shared_edges` |
| T3の座標再現、Q4の境界・歪み・逆写像 | `test_triangle_basis_reproduces_affine_coordinates_and_boundary`, `test_distorted_q4_inverse_reproduces_coordinates` |
| 自己交差・パネル外・平面外のライン | `test_invalid_line_is_rejected_with_load_panel_and_path_ids` |
| 二本のラインの反転と強度の不変性 | `test_both_path_orientations_preserve_the_same_physical_field` |
| 正規化弧長による折点の統合 | `test_different_path_knots_are_merged_in_normalized_arc_length`, `test_polyline_correspondence_uses_length_not_vertex_index` |
| メッシュ対角線に依存しない荷重場 | `test_load_field_is_independent_of_loading_mesh_diagonal` |
| 定数・一次・高次多項式の積分 | `test_line_polynomial_degree_selects_exact_rule`, `test_triangle_monomials_match_factorial_closed_form` |
| affine Q4×一次・双一次荷重 | `test_affine_q4_area_load_matches_independent_symbolic_integral` |
| 歪んだQ4の線・面積分、T3×横断補間 | `test_distorted_q4_line_matches_independent_rational_integral`, `test_distorted_q4_and_t3_transverse_field_match_independent_native_integral` |
| 適応細分化と解析解の一致 | `test_nonpolynomial_line_refines_and_matches_exponential_integral`, `test_nonpolynomial_triangle_refines_and_matches_closed_form` |
| 領域全体の誤差配分と相殺時の尺度 | `test_global_error_budget_is_not_granted_to_each_triangle`, `test_relative_error_scale_survives_positive_negative_cancellation` |
| 未収束時のID付き拒否 | `test_nonconvergence_rejects_with_load_and_panel_ids` |
| 純粋性・繰返し一致 | `test_preparation_is_repeatable_and_does_not_mutate_inputs` |

期待値は `tests/support/oracles/spatial_loads.py` に分離し、製品コードをimportしない。
三角形単項式には階乗による解析式、長方形Q4にはSymPyの有理数による積分を使う。
歪んだQ4とT3の比較では、製品側は物理三角形で逆写像し、独立側は台形の固有座標でQUADPACK積分する。
歪んだQ4の対角線荷重は、別途SymPyで積分した有理関数の閉形式値と比較する。
梁応答の閉形式関数も用意したが、ソルバーとの比較は後続段階であり、第0段階全体の完了とはしない。

## 検証結果

新規テストは142件。既存の定義型29件と合わせ、空間荷重パッケージ全体では171件。

| 実行内容 | 結果・生の終了コード |
|---|---|
| 幾何・入力検証の先行試験 `python -m pytest tests/spatial_loads -q --tb=short` | 97 passed、終了0 |
| 求積・補間追加後の同コマンド | 169 passed、23.78秒、終了0 |
| 最終版 `python -m pytest tests/spatial_loads tests/io/test_spatial_loads.py tests/solvers/test_capabilities.py tests/harness/test_suite_contracts.py -q --tb=short` | 227 passed、27.29秒、終了0 |
| 最初の全件検証 `python -m tools.validation.check_test_results --output-dir tmp/spatial-loads-geometry` | 相殺時の尺度の修正を反映するため61％超で明示中断。全件成功とは扱わない |
| 最終版の全件検証 `python -m tools.validation.check_test_results --output-dir tmp/spatial-loads-geometry-final` | 2224 passed、1338.88秒、生のpytest終了0。失敗・エラー・スキップ0、既知失敗0の基準照合一致 |
| `git diff --check`、変更ファイルの構文・UTF-8・空白・文書リンク検証 | 終了0。GitのCRLF変換警告のみ |

各Pythonコマンドは既存 `.venv/Scripts/python.exe` で実行した。

全件検証のログ・JUnit・基準照合結果は `tmp/spatial-loads-geometry-final/` に保存した。
`baseline-comparison.json` の `raw_pytest_exit_code=0`、`baseline_matches=true`、`known_failures=0`、
`issues=[]` と、JUnitの2224件・失敗/エラー/スキップ0件を確認した。
`source-hashes.json` の変更対象Python 10ファイルのハッシュも終了時に照合し、最終版検証中の変更がないことを確認した。
文書検証補助スクリプトは最初にGitの日本語パス引用をそのまま扱ってエラーとなったが、
`git ls-files -z` に修正して全12ファイルの検証を完了した。製品テストの失敗ではない。

独立積分との誤差計測は `tmp/spatial-loads-geometry-final/numerical-errors.json` に保存した。
強度・長さは試験モデルの単位系であり、下表は節点配分の積分値の成分別最大誤差。
製品ソルバーが組み立てたDOF荷重や変位・反力の誤差ではない。

| 比較 | 最大絶対誤差 | 最大相対誤差 |
|---|---:|---:|
| T3×台形載荷帯の横断補間 | `4.441e-16` | `1.519e-16` |
| 歪んだQ4×台形載荷帯の横断補間 | `1.110e-15` | `5.921e-16` |
| 歪んだQ4×対角線上の一次線荷重 | `2.220e-16` | `1.777e-16` |

既存の保存期待値、入力サンプル、既知失敗リスト、テストskip設定は変更していない。

## 現在の境界と次段階

第2段階の独立幾何・補間・求積を実装した状態であり、面荷重解析の初回リリースではない。
汎用基底の単体処理は傾斜平面でも検証したが、`prepare_panel()` は全体XYに平行なパネルのみ受け付ける。
第1段階の読込・Python APIに幾何検証を組み込んだり、解析の対応可否を変更したりしていない。
`static` / `material_nonlinear` / `modal` の空間荷重は引き続き解析前に拒否する。

次は第3段階で `PreparedLoad` から構造節点の並進DOF荷重を組み立てる。
求積の被積分関数へ節点配分・合力・原点回りモーメントを含め、荷重単位で誤差予算と保存性を監査する。
異なるセルや荷重場の区分をまとめる際にも、各区分へ同じ絶対許容値を重複して与えない。
`SpatialLoadContribution` の定義、Solver・結果へのスナップショット、直接シェル荷重の要素節点釣合い力、
再解析時の非累積性を実装・検証する。その後に第4段階の公開入力経路・解析結果検証と静解析有効化へ進む。
