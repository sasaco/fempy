# 実行例

[Wikiホーム](index.md) · [はじめに](getting-started.md) · [Python API](python-api.md)

各例は独立したPythonスクリプトとして実行できます。`beam.json`を使う例では、先に[はじめに](getting-started.md)のJSONを同じ作業ディレクトリへ保存してください。記載の単位はN・mです。assertは、期待する物理量や結果の形を確認するためのものです。

## 分布荷重と着目位置での分割

片持ち梁の全長へ1,000 N/mの下向き荷重をかけ、0.5 mと1 mで分割します。全長載荷の指定は`L1=0, L2=0`です。

<!-- run: distributed-beam -->
```python
import json
from pathlib import Path
from math import isclose
from fem import FemModel

source = json.loads(Path("beam.json").read_text(encoding="utf-8"))
source["notice_points"] = [{"m": 1, "Points": [0.5, 1.0]}]
source["load"]["tip"]["load_node"] = []
source["load"]["tip"]["load_member"] = [
    {"m": 1, "mark": 2, "direction": "gy", "L1": 0, "L2": 0, "P1": -1000, "P2": -1000}
]
Path("distributed.json").write_text(json.dumps(source), encoding="utf-8")
model = FemModel()
model.load_model("distributed.json")
result = model.run()

expected = -1000 * 2**4 / (8 * 200e9 * 1e-5)
assert isclose(result["node_displacements"][2]["dy"], expected, rel_tol=1e-8)
assert isclose(result["reaction_forces"][1]["fy"], 2000, rel_tol=1e-8)
assert model.get_model_info()["n_elements"] == 3
print("先端のたわみ [m]:", result["node_displacements"][2]["dy"])
print("解析要素:", list(model.mesh.elements))
```

先端のたわみは−0.001 mです。出力端力は分割後の各要素に対応します。モデル規模は`get_model_info()`で確認でき、節点数を変位配列の長さから数える必要はありません。

## 複数荷重ケースを1つずつ解析する

HTTPや`load_model()`はJSONの全ケースを一括計算しません。以下は対象ケースだけを残して、ケースごとに新しいモデルを作る例です。各ケース内の材料・支持・ばね・材端条件の参照はそのまま保持します。

<!-- run: multiple-cases -->
```python
import copy
import json
from pathlib import Path
from math import isclose
from fem import FemModel

source = json.loads(Path("beam.json").read_text(encoding="utf-8"))
source["load"]["double"] = copy.deepcopy(source["load"]["tip"])
source["load"]["double"]["load_node"][0]["ty"] = -2000
results = {}
for case_id, case in source["load"].items():
    one_case = copy.deepcopy(source)
    one_case["load"] = {case_id: copy.deepcopy(case)}
    Path("one-case.json").write_text(json.dumps(one_case), encoding="utf-8")
    model = FemModel()
    model.load_model("one-case.json")
    results[case_id] = model.run()

assert isclose(results["double"]["node_displacements"][2]["dy"],
               2 * results["tip"]["node_displacements"][2]["dy"], rel_tol=1e-8)
for case_id, result in results.items():
    print(case_id, result["node_displacements"][2]["dy"])
```

この方法は各ケースの荷重に応じて独立に分割します。比較には元の節点IDを使い、ケース間で生成節点・生成要素のIDが同じとは仮定しないでください。共通の比較位置は`notice_points`で与えます。HTTPでも同じように1ケースずつ送信します。

非線形解析の履歴は各モデルで独立です。複数ケースを足し合わせて1本の履歴として扱うことや、非線形結果を線形重ね合わせすることはできません。

## 支持ばねと強制変位

長さ2 m、軸剛性EA=1,000 Nの部材の右端に500 N/mのばねを付け、左端を0.002 m動かします。部材の軸ばねEA/Lも500 N/mなので、右端は0.001 m動きます。

<!-- run: spring-prescribed-displacement -->
```python
from math import isclose
from fem import FemModel, BarParameter

model = FemModel()
model.add_node(1, 0, 0, 0)
model.add_node(2, 2, 0, 0)
model.add_material(1, "Illustrative elastic material", E=1000, nu=0.25)
model.material.add_bar_parameter(1, BarParameter(area=1, Iy=1, Iz=1, J=1))
model.add_element(1, "bar", [1, 2], 1, section_id=1, shear_correction=False)
model.add_restraint(1, True, True, True, True, True, True,
                    values=[0.002, 0, 0, 0, 0, 0])
model.add_spring_support(2, "x", 500)
result = model.run("static")

assert isclose(result["node_displacements"][2]["dx"], 0.001, abs_tol=1e-12)
assert isclose(result["reaction_forces"][2]["fx"], -0.5, abs_tol=1e-10)
assert isclose(result["reaction_forces"][1]["fx"], 0.5, abs_tol=1e-10)
print(result["reaction_forces"])
```

ばね反力は構造物へ作用する向きで、変位に対して`−k u`です。強制変位とばねを同じ自由度に同時に設定することはできません。

## 温度変化による梁の伸び

片持ち梁の温度を20だけ上げ、線膨張係数を12×10⁻⁶とします。軸方向の伸びが自由なので、先端変位は`alpha × 温度変化 × 長さ`です。

<!-- run: thermal-beam -->
```python
import json
from pathlib import Path
from math import isclose
from fem import FemModel

source = json.loads(Path("beam.json").read_text(encoding="utf-8"))
source["element"]["1"]["1"]["Xp"] = 12e-6
source["load"]["tip"]["load_node"] = []
source["load"]["tip"]["load_member"] = [{"m": 1, "mark": 9, "P1": 20}]
Path("thermal.json").write_text(json.dumps(source), encoding="utf-8")
model = FemModel()
model.load_model("thermal.json")
result = model.run()
assert isclose(result["node_displacements"][2]["dx"], 12e-6 * 20 * 2, rel_tol=1e-8)
assert abs(result["reaction_forces"][1]["fx"]) < 1e-7
print("自由熱伸び [m]:", result["node_displacements"][2]["dx"])
```

温度を求める熱伝導解析ではなく、与えた一様温度変化に対する構造解析です。

## シェルへの面圧

2 m×1 mの板を1枚の四角形シェルで表し、x=0の辺を固定します。節点を上から見て反時計回りに並べたため、F1の法線は+Zです。正圧1,000 N/m²は−Zへ作用します。

<!-- run: shell-pressure -->
```python
import json
from pathlib import Path
from math import isclose
from fem import FemModel

source = {
    "nodes": {"1": [0, 0, 0], "2": [2, 0, 0], "3": [2, 1, 0], "4": [0, 1, 0]},
    "materials": {"1": {"name": "Steel", "E": 200e9, "nu": 0.3}},
    "elements": {"20": {"type": "shell", "nodes": [1, 2, 3, 4],
                         "material_id": 1, "thickness": 0.1, "formulation": "mindlin"}},
    "boundary_conditions": {
        "restraints": {str(n): {"dof": [True] * 6} for n in (1, 4)},
        "pressures": [{"element_id": 20, "face": "F1", "pressure": 1000}]
    }
}
Path("plate.json").write_text(json.dumps(source), encoding="utf-8")
model = FemModel()
model.load_model("plate.json")
result = model.run("static")

assert isclose(sum(r["fz"] for r in result["reaction_forces"].values()), 2000, rel_tol=1e-8)
assert result["node_displacements"][2]["dz"] < 0
shell = result["shell_results"][20]
assert shell["strain_energy"] > 0
print("上面の面積平均応力:", shell["raw_result"]["elemStress1"])
print("局所座標の断面合力:", shell["resultants"])
```

この例のassertは面圧の合力と支持反力の釣合いを確認しています。1要素のたわみが連続した板の厳密解と一致することを保証するものではありません。たわみや局所応力を評価するときはメッシュを細かくして比較してください。

## 二次四面体の応力

10節点の四面体へ、一様なひずみが生じるように全節点の変位を与えます。頂点の後に、辺の中間節点を正しい順序で置きます。

<!-- run: quadratic-solid -->
```python
import numpy as np
from fem import FemModel

coordinates = [
    [0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1],
    [0.5, 0, 0], [0.5, 0.5, 0], [0, 0.5, 0],
    [0, 0, 0.5], [0.5, 0, 0.5], [0, 0.5, 0.5],
]
model = FemModel()
for node_id, (x, y, z) in enumerate(coordinates, 1):
    model.add_node(node_id, x, y, z)
    model.add_restraint(node_id, True, True, True,
                        values=[0.001*x, 0.002*y, -0.001*z, 0, 0, 0])
model.add_material(1, "Illustrative solid", E=1000, nu=0.25)
model.add_element(8, "tetra2", list(range(1, 11)), 1)
result = model.run("static")
stress = result["element_stresses"][8]["stress"]

# 等方弾性: lambda=400, G=400、ひずみの和=.002
expected = [1.6, 2.4, 0, 0, 0, 0]
np.testing.assert_allclose(stress, np.tile(expected, (len(stress), 1)), atol=1e-10)
print("各積分点の応力 [N/m^2]:", stress)
```

`stress`は節点値ではなく積分点値です。`wedge2`・`hexa2`の節点順序は[要素の選び方](elements.md)を参照してください。

## 荷重だけを伝達する部材

線形梁と同じ両端に、断面値がすべて0の部材を重ねます。追加部材の分布荷重を両端へ伝達し、構造剛性は元の梁だけが持ちます。

<!-- run: load-transfer-member -->
```python
from math import isclose
from fem import FemModel, BarParameter

model = FemModel()
model.load_model("beam.json")
model.boundary.loads.clear()
model.material.add_bar_parameter(2, BarParameter(area=0, Iy=0, Iz=0, J=0))
model.add_element(2, "bar", [1, 2], 1, section_id=2, shear_correction=False)
model.add_distributed_load(2, "global_y", -1000, -1000)
result = model.run("static")

assert isclose(result["reaction_forces"][1]["fy"], 2000, rel_tol=1e-8)
assert isclose(result["node_displacements"][2]["dy"], -1000*2**3/(3*200e9*1e-5), rel_tol=1e-8)
print(result["reaction_forces"][1])
```

荷重は両端へ配分されます。元の梁自身に分布荷重を載せた場合とは変形が異なるため、荷重伝達部材の用途を区別してください。

## 固有値解析：ばね支持された梁

両端をばねで支持し、最も低いモードが梁全体の軸方向並進になるモデルです。固定支持の巨大なペナルティを使わず、ばね・質量から求めた振動数と比較します。

<!-- run: modal-spring-beam -->
```python
from math import sqrt, pi, isclose
from fem import FemModel, BarParameter

model = FemModel()
model.add_node(1, 0, 0, 0)
model.add_node(2, 2, 0, 0)
model.add_material(1, "Steel", E=200e9, nu=0.3, density=7850)
model.material.add_bar_parameter(1, BarParameter(area=0.01, Iy=1e-5, Iz=1e-5, J=2e-5))
model.add_element(1, "bar", [1, 2], 1, section_id=1, shear_correction=False)
for node in (1, 2):
    for direction, stiffness in [("x", 1e5), ("y", 1e6), ("z", 1e6),
                                 ("rx", 1e4), ("ry", 1e4), ("rz", 1e4)]:
        model.add_spring_support(node, direction, stiffness)
model.analysis_params["n_modes"] = 1
result = model.run("modal")

mass_at_each_end = 7850 * 0.01 * 2 / 2
expected_frequency = sqrt(1e5 / mass_at_each_end) / (2 * pi)
assert len(result["frequencies"]) == 1
assert isclose(result["frequencies"][0], expected_frequency, rel_tol=1e-5)
print("振動数 [Hz]:", result["frequencies"])
print("周期 [s]:", result["periods"])
```

振動数は約5.68048 Hzです。これはこの離散モデルの検証で、任意の支持条件のモード精度を保証するものではありません。固定支持のペナルティによる悪条件や未収束時の再試行など、[固有値解析の制約](elements.md)も確認してください。

## 非線形・HTTP・保存の例

- [材料非線形解析](nonlinear-analysis.md)：モーメントを載荷・除荷・反転し、残留曲率を読む。
- [HTTP API](endpoints.md)：通常JSONと、互換圧縮形式で送受信する。
- [ファイル入出力・VTK](file-formats.md)：モデル・結果を保存する。変位を可視化する。
