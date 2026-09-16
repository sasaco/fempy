# 支持条件・釣合い評価の共通化

2026-09-12。荷重制御と変位制御に分散していた支持ばね処理を、TDDで共通化した。
開始HEADは `a3f43d481957a05b6817e3b6758823de2cf4a884`。

## 責務と数値契約

| 所有先 | 担当する処理 |
|---|---|
| `src/fem/boundary_dofs.py` | 境界入力から拘束DOF・ばね・自由DOFへの正規化。ばね剛性、ばね力、補償付き残差、拘束消去、反力ベクトル |
| `src/fem/convergence.py` | 支持ばねを含む釣合い残差、自由DOFの力・モーメントのノルム、外力・復元力・参照荷重による収束尺度 |
| `src/fem/equilibrium.py` | 直接解法、荷重制御、変位制御それぞれの解法。変位制御の初回ランク検査と反復は同じ拡大行列生成処理を使用 |
| `src/fem/solver.py` | 解析フローと既存APIの委譲。固有値解析も同じばね剛性加算を利用 |
| `src/fem/solver_results.py` | 共通の釣合い評価を使う診断と、受理済み結果の保存 |
| `src/fem/dof.py` | ソルバーと公開後処理の共通変位検証・整形・要素DOF抽出 |

境界条件は操作・ステップごとに正規化し、反復中は同じスナップショットを使う。
解析をまたぐキャッシュを設けず、入力編集・再解析時の取り残しを防ぐ。
旧形式の `abs(value)>1000` と明示的な `spring_supports` は同じばね辞書へ解決する。

要素の内力・接線には支持ばねを含めない。`residual` に渡す値は外力−要素内力であり、
そこでばね力を一度引く。`constrain` はばねを含む接線・残差を受け取り、拘束消去だけを行う。
旧 `apply_boundary_conditions` は、この順序で共通処理を呼ぶ互換ラッパーとして残した。

荷重制御のバックトラック、変位制御の荷重係数増分と制御残差、各数値ランク検査・許容値を維持した。
収束ノルムは荷重制御の全DOF上の射影と変位制御の縮約表現を保持し、回転DOFの元の添字も引き継ぐ。
線形補正の `fsum(R, -k*u, -k*low)` を共通化し、補償成分を通常のfloat加算で潰さない。
Decimalによる剛性・反力計算と梁端力回復は、それぞれの精度上の役割を保持した。
結果メタデータも従来の浮動小数点の評価規約を保持する。

## 公開後処理

`ResultProcessor.process_stress(elements, displacement, mesh=mesh)` で実際のメッシュを渡すと、
ソルバーと同じ `DofLayout` で飛び番、非接続節点、3/6DOF混在を処理する。
既存の2引数呼び出しは1始まり節点番号・各節点6自由度の旧配列規約を維持する。
メッシュを省略して圧縮されたDOF配置を推測することはしない。
不正な長さ・非有限変位は拒否し、欠損成分をゼロで埋めない。
梁の断面力APIを優先し、未実装の基底応力APIに隠れる問題も修正した。

## TDDの記録

`tests/solvers/test_support_equilibrium.py` に11ケース、
`tests/postprocess/test_result_processor.py` に6ケースを追加した。
期待値は梁の解析剛性、線形支持ばね、独立した区分線形の軟化則から定めた。
保存サンプルの期待値・許容差・既知失敗基準は変更していない。

| 段階 | 結果 | 証拠（リポジトリ直下からの相対パス） |
|---|---|---|
| 境界処理の実装前 | 新規契約4失敗、既存動作を固定する5ケース成功 | `tmp/support-refactor/red.xml` |
| 境界処理の実装後 | 9成功 | `tmp/support-refactor/boundary-green.xml` |
| 共通収束評価の実装前 | 新規契約2失敗、9成功 | `tmp/support-refactor/measure-red.xml` |
| 解法への接続後 | ソルバー・梁・単位不変性140成功 | `tmp/support-refactor/solvers-green.xml` |
| 後処理の実装前 | 新規契約5失敗、既存契約5成功 | `tmp/support-refactor/postprocess-red.xml` |
| 後処理の実装後 | 後処理・ソルバー・梁精度・単位不変性180成功 | `tmp/support-refactor/postprocess-green.xml` |
| 最終全件 | 3,033成功、失敗0・エラー0・スキップ0 | `tmp/support-refactor/full/complete.xml` |

追加した保証は以下のとおり。

- 並進・回転ばねを併用した梁で、荷重制御と変位制御の変位・荷重係数・反力を照合する。
  旧／明示ばね入力、並進／回転の制御、ゼロ荷重、除荷・逆方向・ゼロ復帰を含む。
- 大きな拘束節点荷重が自由DOFの釣合い判定を隠さない。
- 支持ばね付きの軟化要素を荷重極大点から負勾配まで変位制御で追跡する。
- 正規化時の入力所有権、ばね接線と残差微分の整合、強制変位消去時の二重減算防止、
  floatで表せない微小変位成分の残差を検証する。
- 収束尺度を全DOF表現・縮約表現で照合し、回転ばねとモーメントの長さ尺度を検証する。
- 応力後処理の飛び番・非接続節点・3/6DOF混在、旧2引数API、入力長の拒否、梁端力を検証する。

全件検証は `uv run --locked --extra dev python -m tools.validation.check_test_results --output-dir tmp/support-refactor/full` で実行した。
pytestの終了コードは0、所要時間は5,311.22秒（約88分31秒）。JUnit実測は5,311.171秒。
既知失敗0件との照合も成功し、収集漏れ・予期しない失敗・スキップはない。
全件実行中は製品コードとテストを変更していない。

生ログは `tmp/support-refactor/full/pytest.log`、環境・依存・入力hash・照合結果は
`tmp/support-refactor/full/baseline-comparison.json` に保存した。
`git diff --check` は終了0。保存入力・参照、既知失敗基準、材料・要素定式化のファイルに差分はない。
