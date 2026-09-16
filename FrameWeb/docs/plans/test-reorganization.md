# テストの責務整理・集約・統合・廃止計画

> ?????????????????????????????? [????](test_inventory.md)?????????? [????](../report/test-reorganization.md) ????????????????????????????????????

2026-09-09。調査基準は `c42ee68`。本書は計画であり、テスト・解析コード・参照データの移動や削除は未実施。

## 1. 方針

**機能を軸に配置し、単体・解析経路・保存サンプル・参照の信頼性を区別する。まず検証を保存したまま移動し、同等性を説明できた重複だけを統合する。**

[当初の項目表](test_items.md)の機能分類を土台にする。ただし、項目表自体にも重複と将来構想があるため、その14章をそのまま14ディレクトリにはしない。
材料非線形の完了範囲と一般FEMの残件の区別は、[完了判定](../report/material-nonlinear-closure.md)と[一般FEM別計画](general-fem-followup.md)を維持する。
今回の整理でGF-01〜06の実装・参照修復・外部較正を再開しない。

## 2. 現状の棚卸し

全38テストファイルの役割・移管先と、全264関数の一覧は[テスト台帳](test-reorganization-inventory.md)に記載する。台帳の件数は関数定義数とパラメータ展開後のケース数を分ける。

| 項目 | 調査結果 |
|---|---|
| pytest対象 | 38ファイル、264関数、1,151収集ケース |
| 開発段階を名前に持つテスト | `test_phase4*` が24ファイル。責務より修正の経緯を表している |
| Git管理中の `tests/` | 112ファイル：Python 57、JSON 45、CJS 2、VTK 4、FEM 1、NDT 2、NDU 1 |
| Pythonの内訳 | テスト38、補助・実行・修復18、conftest 1 |
| JSON回帰 | bar 23、shell 14、bend 7、snap 1＝45ファイル |
| 一般FEMの内部ケース | bar 302＋shell 14＋bend 7＝323荷重／参照ケース。pytestの44ファイルとは数え方が違う |
| beam001 | 1モデル、保存参照は0〜100の101段階。荷重ケース数と混同しない |
| pytest設定 | `pyproject.toml` にtestpaths・marker・import方式の設定なし |
| CI | 公開・Doxygenのworkflowのみ。公開workflowにpytest実行なし |
| 収集警告 | `test_base_element.py::TestElement` が補助クラスなのに収集候補となる |

今回の全件再実行は **1,116成功・35失敗・0skip・警告1件、166.46秒**。失敗対象の集合は保存済みの区切り記録と一致し、追加・解消とも0件だった。失敗は `test_run_data.py` の梁22・シェル13に限られる。

再実行の結果・ファイルごとの件数・失敗一覧は台帳に保存する。カバレッジ率は今回測定していない。下記の「未検証」は、独立した期待値による明示的な試験が見当たらないという意味であり、コードが一度も通らないという意味ではない。

### 散らかった原因と具体例

1. **責務の混在**：`test_phase4_legacy.py` に入力、荷重、静解析、保存再読込、比較器、監査、beam001が同居。`nonlinear/test_phase4_io.py`にもHTTP契約とテスト基盤自身の検証が同居。
2. **名前と保証の不一致**：`test_bar_element.py::test_boundary_conditions` は支点条件ではなく45度回転した行列の対称性。`test_stress_strain_calculation` は応力出力APIではなく `K @ u` の軸端力。`test_pressure_integration.py` の5関数は読込・保存と等価荷重で、ソルバー実行の試験ではない。
3. **テスト間import**：`test_phase4_reference → test_phase4_io`、`test_legacy_shear_input → test_phase4_legacy`、精度試験から `test_phase4_beam_precision`、シェル互換出力から `test_phase4_dkt`、ソリッド参照から `tests.elements.test_phase4_quadratic_solids`。配置変更に弱い。
4. **import名の二重化**：HTTPは `fem.*`、多くの試験は `src.fem.*`。後処理失敗の試験は両方のShellElementをmonkeypatchしている。単なる文字置換で片方の検証を落とさない。
5. **比較器と実行器の結合**：`run_sample.py` は再帰比較、旧出力変換、解析実行、beam001特例、受け入れスキーマを兼任し、監査がそこからFemModelまでimportする。
6. **データと生成物の混在**：JSONの横に手動実行のVTKや外部計算資料がある。`executeForDebug.py` は入力の隣に `*_out.json` を作り、JSON globに紛れ込む可能性がある。外部資料の多くはGit無視されており、ローカルにあることと再現可能であることは別。
7. **再計算の重複**：同じ大きなソリッドを、出典・修復・全出力・保存サンプルの各試験で解いている。今回、二次ソリッドの独立Decimal全出力比較3caseは計約35秒、同じ3モデルの修復・製品比較は計約19秒だった。ただし出典の証明と製品の正しさは別の責務で、単に一方を削除できない。

## 3. 当初の項目表との対応

旧番号は移行後も参照用に残す。項目ごとに「検証済み／一部検証／未検証／対象外・将来」を付け、現在の大きな✅をそのまま継承しない。

| 新しい責務 | `test_items.md`の対応 | 現在の保証と不足 |
|---|---|---|
| 基底・要素 | 1.1〜1.4 | 基底契約、BE/T/非線形梁、Mindlin/DKT、一次・二次ソリッド。旧基本試験の一部はshape・対称性・対角非負のみ。これを質量保存や解析精度の完了としない |
| 形状関数・積分 | 1.5、7.2、7.8、12 | 一次要素の和・導関数・積分重み、二次ソリッドの多項式・差分検証あり。三角形二次・三次や一般的なp/hp収束は今回の38ファイルでは検証されていない |
| 材料・断面 | 2、7.9、10.1の断面、10.4 | JRの骨格・履歴・接線・異常値は厚い。線形構成則はパッチ試験で部分保証。断面計算クラス、異方性、温度による物性変化、クリープ・緩和の独立検証はない。温度荷重の自由膨張と拘束力の検証は別に存在 |
| ソルバー・精度 | 3、7.1、7.6、7.7、7.11、10.8、11 | 境界処理、スケーリング、特異系拒否、Newtonと失敗復元、微小力の保持。独立な三次ばねと実JRの失敗試験は両方維持。Cholesky、反復前処理、弧長、分岐・後屈曲を検証済みとしない |
| メッシュ・ID | 4、10.2 | 非連続ID、材種の番号衝突、注目点・荷重・剛域による分割の試験はある。自動メッシュ生成・適応・バンド幅最適化の試験ではない |
| 入力・境界・荷重 | 5、7.3、7.4、7.10、10.2、10.3 | G省略、材料ケース、厚さ、V0厳密入力、集中・分布・温度・面圧、支持ばね・分布ばね・端部解放。半剛接合一般や任意の非線形接合まで保証しない |
| 座標・内力・後処理 | 7.5、10.1、10.5〜10.7 | 回転共変性、剛体運動、端力・反力、膜・両面テンソル・合力・エネルギー・旧出力変換。接触、座屈、拘束ねじりの試験はない |
| HTTP・保存 | 5、13のエラー処理 | Flask test clientによる実ハンドラ、400/422/500、保存再読込と結果消去。配備環境の通信E2Eではない。ログローテーション・ログ形式・トレース等は別途未検証 |
| 性能・並列 | 6、7.12、8、14、11の性能項目 | 実行時間の計測と、性能基準を判定する試験は別。性能・メモリ・並列スケーリングの専用試験はない |
| 可視化 | 7.13、9、5の可視化出力 | VTKファイル・手動出力ツールは存在するが、可視化、VTK形式、アニメーションを検証するpytestはない |

旧7章は横断的な品質観点として上記の機能へ配賦する。旧6/8/14、7.13/9、1.5/12の重複見出しを正規化する。
未実装・将来構想の項目はbacklogに分け、整理を理由に空のテストディレクトリやskipだけの試験を作らない。

## 4. 目標配置

機能を第一の分類とし、複数コンポーネントを通すものを `integration`、保存した具体例を `regression` に置く。パッチ・差分・釣合いは検証手法なので、それだけを理由に別階層へ移さない。
以下は移行後の提案であり、まだ存在しないパスを含む。
台帳上の移管先は45テストファイル。混在したファイルの分割により現38より増える一方、HTTP各応答、旧入力各種、保存参照、比較器など関連する試験はまとめる。1関数ごとにファイルを作る設計にはしない。これは初回移管の案であり、重複統合後のファイル数を固定するものではない。

```text
tests/
  README.md                     # 実行方法、追加先、参照・許容値の規則
  conftest.py                   # 最小限の共通fixture。業務ロジックを置かない
  elements/
    test_base.py
    beam/                       # 弾性、非線形断面、運動学、荷重、精度
    shell/                      # 補間、Mindlin/DKT、運動学、面圧
    solid/                      # 一次/二次、補間、剛体・エネルギー
  materials/                    # JR骨格、履歴、材料入力
  solvers/                      # 線形、Newton、境界、精度
  io/                           # JSON/V0入力、HTTP、保存、入力エラー
  postprocess/                  # シェル/ソリッド応力、旧表示、端力回復
  integration/                  # 梁・板の解析解、JR梁履歴、経路全体
  regression/                   # legacy全サンプル、beam001
  validation/                   # 保存参照・出典・独立参照実装の検証
  harness/                      # 比較器・実行器・監査・修復ガードの試験
  support/
    builders/                   # 明示的な入力/要素/モデルの生成
    assertions.py               # 比較。FemModelやsrcをimportしない
    sample_runner.py            # 製品を実行して比較する
    legacy_view.py              # 実測側の旧スキーマ変換
    provenance.py               # 出典・同一性・網羅性を調べる
    oracles/                    # 製品計算を呼ばない独立参照
    repairs/                    # 純粋な入力検証・参照修復関数
    paths.py                    # リポジトリ/データ/原資料のパス
  data/                         # 初回は現配置・全バイトを維持
tools/
  validation/                   # 読取り・監査のCLI
  migrations/                   # 過去データ修復の明示的CLI
  debug/                        # 手動実行。出力はtmpへ
```

### 命名と依存の規則

- `test_<機能>_<振る舞い>.py` とし、`phase4`、`repairs`のような作業履歴は恒久的な製品テスト名から外す。修復ツールの試験に `repair` を使うのは責務を表すので可。
- `test_*.py` から別の `test_*.py` をimportしない。`beam`、`triangle`、`CASES`、`axial_json`等は用途を明示したbuilder／静的データへ移す。汎用builderに全仕様を詰め込まない。
- 期待値の算出と製品モデル生成を分ける。独立参照は製品の剛性・構成則・ソルバー・出力変換を使わない。原資料読取も製品入力parserと独立のものを維持する。
- 再帰比較だけを純粋なmoduleへ抽出し、監査・oracle側が `run_sample` 経由で製品コードに依存する構成を解消する。`source_evidence()` 内の遅延importも対象。
- 製品のimport表記は、wheelの `fem` / `app` と入口に合わせる方針を検証する。`src.fem` と同一クラスが二重ロードされないこと、HTTPの失敗注入が効くことを確認してから `--import-mode=importlib` を採用する。初回移動で同時に変更しない。
- fixtureの初期値は毎回分離する。重い独立参照を共有する場合は元入力・原コードのhash・精度モードをkeyにし、改変テストへはdeepcopyを渡す。ファイル名だけのキャッシュにしない。

## 5. 集約・統合・廃止の具体案

### 5.1 優先する集約

| 対象 | 実施内容 | 残す保証 |
|---|---|---|
| 面圧関連5ファイル | `test_shell_pressure`、`test_pressure_integration`、`shell_kinematics`末尾、`input_decisions`、`source_evidence`の面圧部分を、条件管理／入力／要素荷重／解析／参照検証へ移管 | F1/F2、法線・回転、節点荷重、総力・総モーメント、異常入力、JSON/FEM同値、拘束不足の拒否 |
| 梁関連 | `test_phase4_legacy` を入力・荷重解析・foundation・solver・harnessへ分割。beam/static/foundation precisionは機能に帰属 | 微小力・剛体移動、分布荷重の仕事、長いfoundation、自由端の実在する微小モーメント |
| シェル後処理 | `postprocess`の膜パッチ、`shell_outputs`、`shell_legacy_view`を同じ領域へ集約。単体出力とFemModel公開出力を分ける | 局所工学ひずみ、全体テンソル、表裏、点平均と面積平均、物理とdrillingエネルギーの区別 |
| 非線形 | JR材料則→materials、梁の状態連携→integration、Newton→solvers、HTTP→io、全経路の物理参照→integration | 材料・要素・ソルバー・公開経路の各層で失敗を識別。438ケースの物理参照行列は初回は全保持 |
| 参照関連 | 原資料読取、Decimal、V0 JS、SymPy、保存gold検証、修復ガードの責務を分離 | 参照の出典・入力同一性・符号・単位・位置・全必須量・元ファイル非変更 |

### 5.2 統合・廃止候補と条件

「廃止」は検証責務の放棄ではない。以下は置換先のassertionが同等以上であることを確認した後に旧関数を削除する候補。ファイル全体を即時削除する判断ではない。

| 候補 | 判断・移管先 | 削除してよい条件 |
|---|---|---|
| `test_bar_element::test_basic_stiffness_matrix`、`test_stress_strain_calculation` | 弾性梁の閉形式試験へ統合 | BEの軸剛性・正負の端力を残す。名称だけ「応力試験」として残さない。質量の試験は別なので削除しない |
| `test_bar_element::test_boundary_conditions` | `elements/beam/test_elastic.py` の回転・対称性試験へ改名統合 | 45度回転と対称性を明示的に保持。回転共変性試験があるだけでは置換完了としない |
| `test_shell_pressure::test_quadrilateral_shell_pressure_equivalent_loads` | `test_surface_pressure_force_and_moment_follow_v0_faces` に寄せる | 元の単位正方形・pressure=1000をcaseとして残し、単なる非ゼロより強い全節点値を比較 |
| `test_pressure_vs_concentrated_load_equivalence` | 同じ要素面圧のパラメータ試験へ統合 | 単位正方形の節点値、総力 `[0,0,-1000]`、総モーメント `[-500,500,0]` を残す。「ソルバー同値」と誤記しない |
| `test_input_decisions::test_legacy_shell_A_is_thickness_not_area` | shell_inputの厚さ試験へ統合 | loadケース未指定時のA採用・A=0拒否も残す。既存shell_inputはケース2の選択であり、そのままでは完全重複ではない |
| source_evidenceのHexa1/Wedge1全変位・釣合いとsource_repairsの同種比較 | 大規模な製品比較を `validation/test_solid_sources.py` の共通caseに統合 | 元ファイル不変性、全変位、力・モーメントの厳しい方の許容値、sourceからの読取を保持。修復の冪等性と原入力拒否はharnessに残す |
| シェル・ソリッド基本試験のクラスごとの共通処理 | 要素種類をパラメータ化 | Tri/Quad、Tetra/Hexa/Wedgeの固有積分点・名称・行列サイズ・体積を保持。二次ソリッドで一次ソリッドの保証を置き換えない |
| BaseElementの3つの未実装契約 | 1つのパラメータ試験へ統合してよい | 3APIのNotImplementedErrorは基底契約として残す。`TestElement` は `DummyElement` へ改名し警告を除去 |

以下は**廃止しない**。

- 同じJR履歴を材料単体・梁・全解析経路で確認する試験。状態の更新・確定・復元を担う層が異なる。
- `test_tangent_is_finite_difference_of_force`、履歴固定後の接線、`test_jr_beam_history_tangent_matches_all_force_columns`。未履歴／載荷後／履歴中、回転・ねじり・βの条件が違う。
- beam001の保存値Decimal検証、独立flexibility、全段階保存参照比較、step0とstep62の改変検出、自由端回復。別の欠陥を検出する。共通の実行結果を使うとしても各assertionは維持する。
- V0の手パッチ、V0の不完全出典拒否、Decimalの剛体・仕事・逆行列。製品回帰だけでは参照計算自体の誤りを検出できない。
- 旧サンプル35失敗。名前の変更やテスト数削減の目標のためにskip/xfail化しない。

### 5.3 補助・修復スクリプト

| 現ファイル（すべてtests直下） | 移管方針 |
|---|---|
| `run_sample.py` | `support/assertions.py`、`legacy_view.py`、`sample_runner.py` に分割。用途別に `compare_legacy`、`compare_steps`、`compare_acceptance` を明示。ファイル名によるbeam001判定は将来manifestに置換 |
| `reference_solutions.py` | `support/oracles/axial.py` と `beam001_flexibility.py`。assert部分は比較側へ分離 |
| `beam001_reference_values.py` | Decimal関数は `support/oracles/beam001_decimal.py`、書込CLIは `tools/validation/beam001_reference.py`。float oracleと一本化しない |
| `pressure_reference.py` | `support/oracles/pressure.py`。SymPyの有理数計算を維持 |
| `v0_decimal_reference.py` | `support/oracles/v0_decimal.py`。原形状関数・50桁の独立性を維持 |
| `v0_refined_reference.py`、`v0_support_reference.cjs` | `support/oracles/v0_solid.py` と同階層のCJS。input-onlyと元出力反力候補を混同しない |
| `v0_triangle_reference.py`、`v0_triangle_reference.cjs` | `support/oracles/v0_triangle.py` と同階層のCJS。旧JSの誤った後処理を復活させない |
| `phase4_source_evidence.py` | 原資料parser・hash・入力所見を `support/provenance.py` に集約。比較器は純粋moduleを利用 |
| `audit_phase4_samples.py`、`audit_phase4_beam001.py`、`audit_phase4_support_references.py` | `tools/validation/audit_samples.py`、`audit_beam001.py`、`audit_supports.py`。監査関数とCLIを分け、診断出力先を引数化して既定はtmp。fixtureには書き込まない |
| `restore_phase4_sources.py`、`restore_phase4_supports.py`、`restore_phase4_tetra1.py`、`restore_phase4_tri1.py`、`restore_phase4_pressure.py` | 純粋なガード・修復関数を `support/repairs/`、CLIを `tools/migrations/` に移動。対象入力・原資料・残差・既存値の各拒否条件は統合の際も保持。再現用途があるので直ちに削除しない |
| `remove_retired_shell_output.py` | 1回限りの完了済み移行として `tools/migrations/` に保管。再実行を標準試験にしない。hash連鎖と過去の修復記録を保持できた後、Git履歴のみへ退役する候補 |
| `executeForDebug.py` | テストではない。`tools/debug/execute_model.py` に移す候補。static固定・Flet・入力横への出力を記載し、出力先をtmp等の明示ディレクトリへ変更してから運用。GUIの必要性だけで新しい自動テストを増やさない |
| `conftest.py` | パス設定をpytest設定へ移す段階で縮小。不要importを除き、ルートパスfixture等だけに限定 |

原資料参照は `parents[1]`、`__dirname/..`、`with_name`、`tests/` というhash keyに依存している。Pythonの移動だけでなくCJSのroot解決・依存hash生成・監査のパス対応も移行対象。
過去のreportに保存されたhashは歴史的証拠であり、移動後のhashで上書きしない。旧パス→新パス、旧hash→新hashを新しい移行記録に保存し、必要な新証拠だけ追加する。

## 6. データと実行単位

1. 初回は `tests/data` の45JSON、原資料、保存参照を移動・再整形しない。台帳をJSON manifestへ落とす段階で、sample ID、入力形式、期待値形式、荷重case/step、出典とhash、単位、符号、評価位置、許容値、GF課題、実行区分を記録する。
2. 現在のglobはJSONを無条件に収集し、1ファイルの最初の失敗で後続caseを見なくなる。まず現45項目をmanifestで明示選択し、次段階で `(sample_id, case_id)` にparametrizeする。対象集合はloadとreferenceの和集合とし、片方だけのcaseも不足として報告する。beam001のstep列は1解析で全段階を検証する。
3. 323case化すると件数も失敗数も変わるため、35ファイル失敗をそのまま新基準に流用しない。旧監査は15一致・238不一致・70エラーという保存記録であり、この調査では全323case監査を再実行していない。移行直前に同一環境で再採取して対応づける。
4. 四つのVTKは現在のpytestで読まれていない。生成例として必要なら `docs/examples/`、単なる再生成可能な出力ならGit管理からの退役候補とする。可視化検証の証拠とはしない。
5. NDT 2・NDU 1は外部参照資料として保持。Git無視のOUT/FMT等はローカル補助資料として列挙し、未提供なら監査結果に「資料なし」と明示する。自動試験がこれらの存在で黙って成功条件を変える構成にしない。
6. 比較の許容値は移動時にそのまま残す。共同比較器はrel=1e-8/abs=1e-10、`assert_beam001` はrel=1e-6/abs=1e-9等、現実に複数ある。単一の緩い定数へ統合せず、物理量・参照の精度・離散化誤差の理由を台帳化する。

## 7. 実行規律とCIの計画

- `testpaths = ["tests"]`、明示marker、最終的なimport方式を `pyproject.toml` に設定する。READMEから一意に実行できるようにする。
- `unit / integration / regression / oracle` は主な実行層、`slow / requires_node` は資源条件、`material_nonlinear` は完了範囲の選択に用いる。markerだけで通常実行から除外しない。
- 移行直後の `material_nonlinear` 集合は既存の799件と旧→新node ID対応で照合する。これは過去の完了集合であり、整理時に他の関連テストを加えるなら件数変更を明記する。
- 全件実行ではNodeとSymPyを必須依存として準備する。依存不足をskipで成功扱いにしない。軽量な選択実行は対象範囲を結果に表示する。
- 材料非線形の必須jobは0失敗、一般FEM回帰jobは全件を実行して既知失敗集合と比較する。後者の生のpytest終了コード1と失敗一覧は必ず保存し、無条件の `continue-on-error` で新規失敗を見落とさない。全体を「全テスト成功」と表示しない。
- 一般FEMの基準は失敗IDだけでなくfailure/error区分・原因分類も保持する。同じIDが別の理由で失敗した場合も調査する。解消した失敗は基準から外す。
- JUnit、実行時間、環境・コミット・入力hashをartifactとして保存する。重い参照の頻度変更やcase削減は実測後の別変更とし、初回移行では全組合せを維持する。

## 8. 実施順序と完了条件

| 順序 | 変更単位 | 完了条件 |
|---|---|---|
| R0：基準固定 | 本計画・台帳・全件結果。旧node ID、fixtureのhash、既知失敗、収集警告、環境を記録 | 38ファイル・264関数・1,151caseの漏れがない。既存35失敗と新規失敗を区別できる |
| R1：基盤抽出 | パス、純粋比較、builder、runner、oracleを抽出。テスト名・配置はまだ維持 | テスト間importを除去。比較器の改変検出・ファイル不変性・HTTP失敗注入・原資料hashの意味を保持。全件の成否集合不変 |
| R2：責務へ移動 | 台帳に従い改名・分割・集約。CLIをtoolsへ移管 | 全旧node IDが新IDへ対応。parameter/条件/期待量/許容値を不変にする。799件の完了集合を再現。単独ファイル実行も可能 |
| R3：重複統合 | 5.2の候補だけを小さな単位で統合 | 各削除関数について代替先・残る入力条件・assertion・改変検出を記録。大規模な同一モデル計算を共有しても検証項目を落とさない |
| R4：実行・データ管理 | manifest、case単位の収集、pytest設定、CI、tools出力先 | 323caseと101stepの扱いを明示。既知失敗の集合と理由を新単位で再採取。非追跡JSONを誤収集しない。通常実行でfixture/report履歴を改変しない |
| R5：計画更新・退役 | `test_items.md`、`test_plan.md`、`tests/README.md`、現在の再実行案内を更新 | 重複章を正規化し各要求を試験/未検証/対象外へ対応。古い段階reportは当時の記録として保存。不要な旧入口・1回限りの移行コードは再現手段を確認して退役 |

移動、重複削除、import方式変更、case展開、参照更新を一つの変更に混ぜない。
R1〜R3で失敗集合が変化したら、まずその変更だけを調べる。一般FEM実装やgold修復によって移行上の失敗を消さない。
各段階はfixture/sourceのhash不変性、全件の成否・skip集合、対応表、`git diff --check`を確認して閉じる。性能の再計測は共有化・case変更等に応じて行う。

### 今回の計画から別作業へ残すもの

- `materials` の線形材料・断面、メッシュutility、VTK・ログ等、既存機能の独立試験不足。整理後の要求台帳から優先順位を付ける。
- 弧長・適応メッシュ・クリープ等、当初の項目表にある将来機能。実装済みという前提で試験を計画しない。
- 一般FEMの35ファイル失敗と外部JR較正。GF番号で継続管理する。

テスト数やファイル数の削減率は完了基準にしない。**各保証の置き場所、参照の根拠、実行方法、既知失敗の扱いが一意に分かり、以後の追加先に迷わない状態**を完了とする。
