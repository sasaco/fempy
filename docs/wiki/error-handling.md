# エラーと対処

[Wikiホーム](index.md) · [HTTP API](endpoints.md) · [モデルの入力](data-structures.md)

まず「入力を読み込めない」「構造が不安定」「非線形が収束しない」「結果の読取り・保存に失敗する」のどこで止まったかを確認します。条件が同じままの再試行で解決しない問題もあります。

## 症状から調べる

| 症状 | 最初に確認すること |
|---|---|
| `No module named fem` | プロジェクトの環境から実行しているか。`uv sync --locked --extra dev`後に`uv run ...`を使う。 |
| HTTP 400 | `error`本文、JSONの階層、必須座標・参照ID・有限値。 |
| `KeyError: 'z'` | 2Dでも各節点へ`z: 0`を指定する。 |
| `Missing ... case ...` | 荷重ケースが参照する材料・支持・ばね・材端ケースを用意する。 |
| `Singular stiffness matrix` | 支持不足、切り離された節点、ゼロ剛性の自由度、材端解放の組合せ。 |
| `Conflicting restraint and spring at same DOF` | 同じ自由度に拘束と支持ばねを重ねていないか。 |
| HTTP 422／`NonlinearConvergenceError` | 載荷段階・基準荷重、骨格曲線の耐力、支持、ステップ幅。 |
| 面圧を入れても変形しない | `nodes/elements`形式か。`node`形式では明示pressuresを読み込まない。 |
| 期待した荷重倍率にならない | `rate`は現行経路で乗算されない。荷重値または非線形の`load_factors`を使う。 |
| `NotImplementedError` | 要素・解析の組合せを[対応表](elements.md)で確認する。 |
| `Non-finite analysis result` | NaN・Infinityを含む結果はHTTP・JSON保存できない。固有値のゼロモード等を確認。 |
| VTK変換中の`TypeError` | 配列を含む要素結果をそのまま渡していないか。[整形例](file-formats.md)を参照。 |
| `KeyError: 'disg'`や`'case1'` | 現行結果は`node_displacements`等を直接返す。 |

JSONの未知キーや不正な階層の一部は無視されるため、エラーが出ないことだけでは条件が正しく適用されたとは判断できません。荷重・支持を1つずつ追加し、反力と変位で確かめてください。

## 不安定なモデルを直す

1. 梁・シェルなら6つの剛体運動、平面モデルなら対応する面内の剛体運動を抑える支持があるか確認します。
2. 要素に接続されていない節点や、剛性0の自由度が残っていないか確認します。
3. 梁の回転を材端で解放した場合、接続節点の回転が他の要素にも支持にもつながらず、不要な自由度として残ることがあります。
4. 梁・シェルとソリッドを混在させた場合、ソリッドだけの節点に不要な回転自由度がないか確認します。
5. 断面値・剛域の剛性比・部材長・単位を確認します。

計算を通すためだけに無関係な節点を固定すると、解析対象そのものが変わります。実際の支持と、モデル化で不要になる自由度を区別して設定します。

## 非線形の未収束

`NonlinearConvergenceError`は次の情報を持ちます。

| 属性 | 意味 |
|---|---|
| `step` | 失敗した段階番号（1始まり） |
| `load_factor` | 失敗した載荷係数 |
| `displacement` | 最後に収束した変位のコピー。失敗段階の解ではない |

解析失敗後の`model.get_results()`はNoneです。直前までの低水準スナップショットは`model.solver.step_results`に残りますが、完了した解析結果として扱わないでください。HTTPの422応答は、これらの途中変位を返しません。

以下は耐力22 N·mのモデルへ30 N·mを与え、失敗を検出する例です。

<!-- run: nonlinear-error -->
```python
from fem import FemModel, BarParameter
from fem.nonlinear.nonlinear_solver import NonlinearConvergenceError

model = FemModel()
model.add_node(1, 0, 0, 0)
model.add_node(2, 2, 0, 0)
model.add_nonlinear_material(
    1, "Capacity example", E=10000,
    delta_1=0.001, delta_2=0.004, delta_3=0.010,
    P_1=10, P_2=16, P_3=22,
)
model.material.add_bar_parameter(1, BarParameter(1, 1, 1, 2))
model.add_nonlinear_bar_element(1, [1, 2], 1, 1, ["moment_z"], shear_correction=False)
model.add_restraint(1, True, True, True, True, True, True)
model.add_load(2, mz=30)
model.analysis_params.update(load_factors=[1], max_iterations=10)
try:
    model.run()
    raise AssertionError("耐力を超えたモデルは成功しないはずです")
except NonlinearConvergenceError as error:
    assert error.step == 1
    assert model.get_results() is None
    print("失敗段階:", error.step, "載荷係数:", error.load_factor)
```

対処の順序は次のとおりです。

1. `delta_*`が節点変位ではなく、適用成分のひずみ・曲率・ねじり率であることを確認する。
2. 単位、支持、荷重方向、骨格の耐力と勾配を確認する。
3. 解が存在する荷重範囲なら、折れ点・除荷・反転付近の`load_factors`を細かくする。
4. 収束履歴を見て、必要に応じて`max_iterations`を調整する。

耐力を超えて釣合い解がない場合や機構になった場合は、反復回数を増やしても解決しません。許容差を緩めただけの結果を妥当と判断せず、自由節点の釣合いと履歴を確認します。

## HTTPステータスの解釈

入力検証のValueErrorなどは400、非線形未収束は422、NumPyの線形代数例外は500です。同じ「不安定構造」でも、検出される箇所によってステータスが変わる場合があります。

`error_code`は`invalid_input`、`nonlinear_nonconvergence`、`analysis_failure`などで、例外によっては付かない場合もあります。共通する`error`と`converged: false`を確認します。タイムアウト・通信断はサーバーのエラーJSONを受け取れないこともあります。

圧縮要求のエラー応答は通常JSONです。成功時のBase64/gzip復号をエラー応答に適用しないでください。[HTTPの例](endpoints.md)を参照してください。

## 問題を再現できる形にする

解析種別、入力JSON、期待する値と得られた値、例外またはHTTP応答、Python・依存環境を残します。材料非線形では`load_factors`と失敗した段階も必要です。結果が大きくずれるときは、元の大規模モデルとともに、問題を保った小さなモデルを作ると原因を追いやすくなります。

製品の既存テストと保証範囲は[テストガイド](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/tests/README.md)を参照してください。
