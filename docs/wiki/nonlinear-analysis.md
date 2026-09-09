# 材料非線形解析

[Wikiホーム](index.md) · [モデルの入力](data-structures.md) · [結果の読み方](results.md)

材料非線形解析は、荷重を増減させながら、部材の剛性低下と履歴を追う解析です。たとえば、載荷で曲がった部材が除荷後にどれだけ曲率を残すかを調べられます。現行の履歴モデルはJR総研剛性低減RC型です。

## モデル化の範囲

- 小変位・小回転の梁を対象に、**要素中央の1断面**へ非線形則を置きます。
- 非線形化する成分は`axial`、`moment_y`、`moment_z`、`torsion`から選びます。
- 各成分は独立した履歴です。軸力と曲げの相互作用や、断面のファイバー積分ではありません。
- せん断柔性は基準弾性から定まり、材料履歴に合わせてせん断剛性を低減するモデルではありません。
- 線形梁、シェル、ソリッドを同じ解析に含めても、それらの材料は線形のままです。

塑性分布が一様でない部材では、要素分割による結果の変化を確認してください。有限回転、集中塑性ヒンジ、任意の軟化、破断、弧長法、時刻歴の動的応答は対象外です。数学的な検証と実験・外部JR実装との較正は別で、外部との同条件較正は未実施です。[検証の適用範囲](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/docs/report/material-nonlinear-closure.md)も参照してください。

## 入力するのは断面の関係

`delta_1, delta_2, delta_3`という名前でも、節点の絶対変位や集中ヒンジの回転を入れる欄ではありません。

| `hysteresis_dofs` | `delta_*`の意味 | `P_*`の意味 | N・m系での単位 |
|---|---|---|---|
| `axial` | 軸ひずみε | 軸力N | 無次元、N |
| `moment_y` | 局所y軸まわりの中央曲率κy | 曲げモーメントMy | 1/m、N·m |
| `moment_z` | 局所z軸まわりの中央曲率κz | 曲げモーメントMz | 1/m、N·m |
| `torsion` | 局所x軸まわりの単位長さ当たりのねじり角 | ねじりモーメントT | rad/m、N·m |

初期勾配`P_1/delta_1`は断面剛性です。参照弾性と整合させる場合、軸は`EA`、曲げは対応する`EI`、ねじりは`GJ`と合わせます。同一材料の`hysteresis_dofs`を複数指定すると、同じ数値の骨格を各成分へ設定します。異なる単位・剛性の成分へ無条件に同じ曲線を流用しないでください。

## 骨格曲線の設定

編集用JSONの`element`に次のように定義します。例は曲げ用の説明値で、設計用の材料推奨値ではありません。

```json
{
  "element": {
    "1": {
      "1": {
        "E": 10000.0, "nu": 0.25, "A": 1.0, "Iy": 1.0, "Iz": 1.0, "J": 2.0,
        "nonlinear": {
          "type": "jr_stiffness_reduction",
          "delta_1": 0.001, "delta_2": 0.004, "delta_3": 0.010,
          "P_1": 10.0, "P_2": 16.0, "P_3": 22.0,
          "beta": 0.4, "symmetric": true,
          "hysteresis_dofs": ["moment_z"]
        }
      }
    }
  }
}
```

原点と3つの折れ点を結び、第3点以降は一定の力・モーメントとなる4折線です。第3点は自動破断点ではありません。各側で以下を満たす必要があります。

- `0 < delta_1 < delta_2 < delta_3`
- `0 < P_1 <= P_2 <= P_3`
- 勾配`K1 >= K2 >= K3 >= 0`。K2・K3は隣接する折れ点間の勾配。
- すべて有限値。`beta >= 0`。

`K_min`は**除荷剛性の下限**です。省略すると正負の初期剛性の小さい方の1%になります。指定する場合は`0 < K_min <= min(K1正, K1負)`です。骨格曲線の最終勾配を正にするパラメータではありません。

### 正負非対称の骨格

編集用JSONと`add_nonlinear_material()`では、正側は接尾辞のない`delta_1`・`P_1`など、負側は`delta_1_neg`・`P_1_neg`などを使います。負側も**正の大きさ**で指定します。

```json
{
  "nonlinear": {
    "type": "jr_stiffness_reduction",
    "symmetric": false,
    "delta_1": 0.001, "delta_2": 0.004, "delta_3": 0.010,
    "P_1": 10, "P_2": 16, "P_3": 22,
    "delta_1_neg": 0.002, "delta_2_neg": 0.008, "delta_3_neg": 0.020,
    "P_1_neg": 8, "P_2_neg": 14, "P_3_neg": 20,
    "beta": 0.4,
    "hysteresis_dofs": ["moment_z"]
  }
}
```

負側の省略項目は正側を使います。`symmetric: true`なら負側の指定値は使用しません。`delta_1_pos`などの`_pos`付き名称を使うのは、保存用JSONの`nonlinear_materials`や低水準のパラメータクラスです。入力形式を混同しないでください。

## 載荷の順序を指定する

`n_load_steps: 10`なら基準荷重の0.1倍から1倍までの10段階です。除荷・反転を含めたい場合は`load_factors`で順序を指定します。

```json
{
  "analysis_type": "material_nonlinear",
  "analysis_params": {
    "load_factors": [0, 0.5, 1, 0.5, 0, -0.5, -1, 0],
    "max_iterations": 50,
    "tolerance": 0.000001
  }
}
```

係数はその段階の**絶対倍率**です。基準荷重と基準強制変位が同じ係数で比例的に変化します。独立した複数の荷重パターンを段階ごとに切り替えたり、未知の荷重倍率を求める変位制御・弧長制御を行ったりする機能ではありません。

0も負値も繰返し値も指定できます。通常の等間隔載荷には初期0段階を含めないため、必要なら配列の先頭へ0を入れます。`n_load_steps`と`max_iterations`は`load_factors`を使う場合も正の整数にします。

## 実行例：曲げ・除荷・反転

以下は単独で実行できます。単位はN・mです。長さ2 mの梁の先端にモーメントを与えるため、曲げモーメントが一様になり、中央断面の曲率と入力した骨格を比較できます。

<!-- run: nonlinear-bending-cycle -->
```python
from math import isclose
from fem import FemModel, BarParameter

model = FemModel()
model.add_node(1, 0, 0, 0)
model.add_node(2, 2, 0, 0)
model.add_nonlinear_material(
    1, "Illustrative RC law", E=10000, nu=0.25,
    delta_1=0.001, delta_2=0.004, delta_3=0.010,
    P_1=10, P_2=16, P_3=22, beta=0.4,
)
model.material.add_bar_parameter(1, BarParameter(area=1, Iy=1, Iz=1, J=2))
model.add_nonlinear_bar_element(7, [1, 2], 1, 1, ["moment_z"], shear_correction=False)
model.add_restraint(1, True, True, True, True, True, True)
model.add_load(2, mz=12)
model.analysis_params.update(
    load_factors=[0, 0.5, 1, 0.5, 0, -0.5, -1, 0],
    max_iterations=50, tolerance=1e-9,
)
result = model.run("material_nonlinear")

for step in result["step_results"]:
    print(step["lambda"], step["curvature"][7]["z"], step["reaction_forces"][1]["mz"])

# M=12: 第2枝の勾配は(16-10)/(.004-.001)=2000
expected_curvature = 0.001 + (12 - 10) / 2000
assert isclose(result["step_results"][2]["curvature"][7]["z"], expected_curvature, rel_tol=1e-8)
assert len(result["step_results"]) == 8
assert result["converged"] is True
assert abs(result["curvature"][7]["z"]) > 1e-8  # 除荷後の残留曲率
model.save_results("nonlinear-result.json")
```

出力の各行は「載荷係数・中央曲率・固定端反力モーメント」です。係数1の段階の曲率は0.002 1/mです。最後の係数0でも、履歴を経た曲率は通常0には戻りません。曲率の符号は局所軸に基づき、材端力のi端・j端の符号とは区別します。

## 収束と履歴の扱い

Newton反復では、直前に収束・確定した履歴から試行状態を評価します。残差が下がるように変位増分を小さくする処理を含み、収束した段階だけを確定・保存します。

未収束時は`NonlinearConvergenceError`を送出し、その段階の変位・内力・要素履歴・載荷係数を直前の確定状態へ戻します。失敗段階の結果を成功結果として返しません。HTTPでは422になります。[エラーと対処](error-handling.md)に例外の扱いを示します。

`model.run()`をもう一度呼ぶと、新しい解析として履歴を初期化します。保存した結果を読み込んで非線形状態を再開する機能はありません。続きの履歴を計算したい場合は、それまでを含む係数列を指定して最初から実行してください。

詳しい段階出力・収束履歴は[結果の読み方](results.md)、コールバックを使う低水準APIは[解析ワークフロー](workflow.md)を参照してください。
