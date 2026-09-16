# スリップ支持ばねの実装記録

2026-09-12。開始HEAD: `7fc6ef5`。

[実装計画](../plans/スリップばね実装計画.md)を、直前の
[支持条件・釣合い共通化](support-equilibrium-refactoring.md)へ合わせて修正し、TDDで実装した。
実装・対象検証・全件回帰を完了した。全3,128ケースが成功し、失敗・エラー・スキップは0件。

## 実装した挙動

- 節点と固定地盤の間に、正負対称のスリップ支持ばねを追加。
  `K1>0, delta_1>0, 0<=K2<=K1` の有限数値を受け付ける。
  並進3方向と6DOFモデルの回転3方向を、それぞれ独立に設定できる。
- 初期・除荷・再載荷はK1、最大経験変形を超える骨格はK2。
  ゼロ力位置の間は厳密に`F=0, Kt=0`。`K2=0`と`K2=K1`にも対応する。
- 旧`fix_node`の選択中ケース、正規化JSON、Pythonの`add_slip_spring_support()`を共通検証へ接続。
  数値0／1／その他の旧仕様を維持し、重複・競合、不正パラメータ、方向・節点違いを拒否する。
- 解析種別の省略時は`material_nonlinear`を自動選択。
  明示`static`・`modal`は公開APIと低水準Solverの両方で解析前に拒否する。
- 各収束ステップと最終結果に`support_response`を追加。
  通常／圧縮HTTP、結果保存、モデル保存・再読込、metadataの入力hashへ反映した。
  FW3保存は書込み前にエラーとし、定義を脱落させない。

理論マニュアル7.19のゼロ力位置を、次元の合わない旧式から
`delta_0 = delta_max - force_at_max/K1`へ訂正した。正負値を符号付きに統一した。
未降伏側の仮想境界、端点の片側接線、ゼロ増分、再反転は補完仕様として区別して記載した。

## 共通化後の構成

| 所有先 | 責務 |
|---|---|
| `nonlinear/hysteresis/slip.py` | 不変パラメータ・不変状態、確定状態からの純粋な候補評価、厳格な定義パーサ |
| `nonlinear/support_springs.py` | 共通の定義追加・検証、各解析独立の履歴、全支点の一括commit、snapshot |
| `boundary_dofs.py` | DOF正規化、支持力・接線・残差への一度だけの加算 |
| `convergence.py` | 既存の共通釣合い・収束尺度をそのまま利用 |
| `equilibrium.py` | 両制御・線探索から同じ支持応答を評価。非線形支持がある収束点では常にランク確認 |
| `solver.py`, `solver_results.py` | 収束時の履歴確定、失敗時の復元、支持診断・反力・出力 |
| `model.py` | 自動解析選択、Python API、梁端力回復の支持DOF集合 |

試行評価は状態を変更しない。線探索の不採用点や、一部の支点だけが評価できた後の例外が、
次の試行の履歴へ混入しない。rollbackは確定状態を保持する。再解析では履歴を新規作成する。
端点で変位がまだ動いていない予測時だけ、残差／制御増分の方向から片側接線を選ぶ。
確定履歴とゼロ増分時の出力は変更しない。

線形ばねの`fsum`による補償残差、Decimal経路、既存の要素内力・接線・JR履歴則は変更していない。
梁端力回復では、Ktがゼロでも非線形支持DOFを境界として保持する。

## TDDと独立期待値

証拠はリポジトリ直下からの相対パス。追加テスト95ケース、既存の支持共通化11ケースと合わせて106ケース成功。

| 段階 | 結果 | JUnit |
|---|---|---|
| 履歴則の実装前 | 未実装モジュールによる収集エラー1 | `tmp/slip-spring/material-red.xml` |
| 履歴則の実装後 | 28成功 | `tmp/slip-spring/material-green.xml` |
| 入力の実装前 | 29失敗・旧数値互換4成功 | `tmp/slip-spring/input-red.xml` |
| 入力の実装後 | 入力33＋履歴28成功 | `tmp/slip-spring/input-green.xml` |
| Solver接続前 | 13失敗・既存機構検出1成功 | `tmp/slip-spring/solver-red.xml` |
| Solver接続後 | 14成功 | `tmp/slip-spring/solver-green.xml` |
| 履歴寿命の追加検証 | 旧支点行の指定順・局所例外の診断不足の2失敗を確認後修正 | `tmp/slip-spring/lifecycle-red.xml` |
| 公開経路・例 | 13成功、未作成の公開例1失敗を確認後追加 | `tmp/slip-spring/public-red.xml` |
| metadata・対応範囲 | 未実装1失敗を確認後、対応範囲既存試験も含め34成功 | `tmp/slip-spring/metadata-red.xml`, `metadata-green.xml` |
| 既存の材料・ソルバー・入出力を先行確認 | 852成功 | `tmp/slip-spring/targeted.xml` |
| 新規受入＋支持共通化の最終確認 | 106成功 | `tmp/slip-spring/acceptance-final.xml` |
| 最終全件回帰 | 3,128成功、失敗・エラー・スキップ0 | `tmp/slip-spring/full/complete.xml` |

期待値を製品の評価関数から生成していない。

- 計画書の14点の手計算値、初回履歴の仕事0.396、最大経験点内の閉経路の仕事0。
  正負反転、未経験側、端点と枝内部の接線、単位換算、分割不変性、K2の両端値を確認した。
- 線形梁とスリップ支持を並列に持つモデルの`P=kb*u+Fslip`を、6方向・両制御で照合。
  `kb`はEA/L、12EI/L³、GJ/L、4EI/Lの解析式を使った。
- 一次四面体の`V*Dxxxx*(dN/dx)^2`、平面応力三角形の`t*A*Dxxxx`を用い、
  3DOF・6DOF、単独要素・梁との混在を照合。JR軸方向則と支持則の独立した力も照合した。
- ゼロ剛性区間の変位制御、他要素で安定する荷重制御、荷重制御の機構、
  制御外自由度の機構、同一変位の繰返し、一定力骨格の機構を分けて試験した。
- 候補評価順序、複数支点の一部だけが失敗する試行、ステップ失敗、再run、clear、
  snapshot変更、局所評価例外の節点・方向・段階付き診断を検証した。
- 旧／正規化JSON、Python、モデル／結果ファイル、通常／圧縮HTTPを通して照合した。
  ケース選択、旧拘束の明示置換、FW3の既存ファイル保護、入力hashの変化も確認した。

## 全件回帰

`uv run --locked --extra dev python -m tools.validation.check_test_results --output-dir tmp/slip-spring/full`
で実行した。pytest終了コード0、3,128成功、失敗・エラー・スキップ0。
所要時間は1,252.11秒（約20分52秒）、JUnit実測1,252.094秒。
既知失敗0件との照合も成功し、保存サンプルの収集漏れ・予期しない失敗はない。

生ログは`tmp/slip-spring/full/pytest.log`、Python・依存・入力hash・基準照合結果は
`tmp/slip-spring/full/baseline-comparison.json`へ保存した。
Python 3.13.11、NumPy 2.4.1、SciPy 1.17.0、pytest 9.0.2、SymPy 1.14.0で実行した。
全件実行中に製品コードとテストを変更していない。
`tmp/slip-spring/source-hashes.json`の263ファイルのhashが実行終了後も一致した。

`git diff --check`は終了0。保存入力・参照値・許容差・既知失敗基準、要素定式化、
既存の支持共通化実施記録に変更はない。初回の全件回帰で成功した。

## 検証範囲

梁・JR軸方向梁・線形シェル・一次四面体と支持則の組合せを独立期待値で検証した。
その他の要素との組合せは既存の材料非線形要素レジストリに従うが、スリップ支持との個別力学検証は追加していない。
実験・外部ソフトウェアとの較正はこの実装の検証には含めていない。

支持地盤の移動、初期ギャップ独立指定、非対称骨格、局所方向、二節点間ばね、連成支持、
動的時刻歴、公開APIでばねだけのモデル、途中再開checkpointは対象外。
反転を含む経路は履歴点として指定する。ゼロ剛性で全体に機構が生じるケースには人工剛性を追加しない。

理論マニュアル、Wikiの入力・解析・API・保存・結果、対応範囲レジストリと生成表を更新した。
実行できる例は[slip-support-history.json](../examples/slip-support-history.json)。
