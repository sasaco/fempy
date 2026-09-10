# 線荷重・面荷重

[Wikiホーム](index.md) · [Python API](python-api.md) · [結果の読み方](results.md)

`static`で、指定した位置の線荷重・面荷重を構造節点へ配分して解析できます。
通常の節点荷重・部材荷重・シェル面圧と同じ荷重ケースに加算されます。
荷重別と総和の合力・全体原点回りモーメントを監査し、一回の静解析で結果を求めます。

## 座標・符号・単位

- パネルは全体XYに平行な平面です。パネルと経路のZ座標を一致させます。
- 正の強度は全体+Z、負の強度は全体-Z方向です。節点順を逆にしても方向は変わりません。
- 線荷重は力/長さ、面荷重は力/長さ²です。N・m系ではN/m、N/m²を使います。
  座標・剛性・通常荷重も同じ単位系にそろえます。自動換算はありません。
- 旧`line.position`のZ省略値は0です。強度や座標は入力時に丸めません。

一本の経路では、始終点強度を折れ線の累積長に沿って補間します。
二本の経路では正規化弧長で位置を対応させ、経路間にも強度を補間します。
対応する端点間の距離二乗和を比較して二本目の向きをそろえ、許容値内の同点は対応が曖昧として拒否します。
経路を逆向きに記述するときは、その経路の始終点強度も入れ替えます。

## パネルの指定

既存シェルを使う場合は`elements`で要素IDを指定します。
T3は重心座標、Q4は双一次形状関数を使います。平面内で歪んだQ4も求積の収束を確認して扱います。

シェルを持たない梁モデルでは`triangles`に既存構造節点の三角形接続を指定します。
載荷三角形は補間専用で、材料・板厚・剛性を追加しません。
梁は三角形からの節点荷重を受けます。梁要素への分布荷重と同じ精度になるとは限らないため、
荷重配分と変位・端力のメッシュ依存性を確認してください。

`elements`と`triangles`の非空指定は排他です。旧JSONで両方を省略すると、パネル節点に含まれる
既存シェルから接続を復元します。シェルのない節点集合から三角形を自動生成しません。
旧`elements`は`shell`名前空間のID、新形式は内部要素IDです。梁IDと旧シェルIDが同じでも区別します。

## 旧JSONの完全な例

長さ2の片持梁二本の先端間に、強度-3の線荷重を載せる数値確認モデルです。
合計荷重は-6、各先端への荷重は-3、曲げ剛性は1000なので先端変位は-0.008になります。

<!-- fixture: spatial-cantilevers.json -->
```json
{
  "analysis_type": "static",
  "node": {
    "1": {"x": 0, "y": 0, "z": 0}, "2": {"x": 2, "y": 0, "z": 0},
    "3": {"x": 2, "y": 2, "z": 0}, "4": {"x": 0, "y": 2, "z": 0}
  },
  "member": {"1": {"ni": 1, "nj": 2, "e": 1}, "2": {"ni": 4, "nj": 3, "e": 1}},
  "element": {"1": {"1": {"E": 1000, "A": 1, "Iy": 1, "Iz": 1, "J": 1}}},
  "fix_node": {"1": [
    {"n": 1, "tx": 1, "ty": 1, "tz": 1, "rx": 1, "ry": 1, "rz": 1},
    {"n": 4, "tx": 1, "ty": 1, "tz": 1, "rx": 1, "ry": 1, "rz": 1}
  ]},
  "inf_panel": {"7": {"nodes": [1, 2, 3, 4], "triangles": [[1, 2, 3], [1, 3, 4]]}},
  "line": {"1": {"position": [{"x": 2, "y": 0}, {"x": 2, "y": 2}]}},
  "load": {"1": {"inf_panel": 7, "load_inf": [{"L1": 1, "P11": -3, "P12": -3}]}}
}
```

<!-- run: spatial-file-roundtrip -->
```python
from math import isclose
from fem import FemModel

model = FemModel()
model.load_model("spatial-cantilevers.json")
result = model.run()
assert isclose(result["node_displacements"][2]["dz"], -0.008, abs_tol=1e-12)
assert isclose(result["reaction_forces"][1]["fz"], 3, abs_tol=1e-12)
assert isclose(result["reaction_forces"][1]["my"], -6, abs_tol=1e-12)
assert isclose(result["spatial_load_contribution"]["resultant"][2], -6, abs_tol=1e-12)
model.save_model("spatial-saved.json")
restored = FemModel()
restored.load_model("spatial-saved.json")
assert restored.run()["node_displacements"] == result["node_displacements"]
assert model.run()["spatial_load_contribution"] == result["spatial_load_contribution"]
model.save_results("spatial-results.json")
```

`L1/P11/P12`のみで線荷重、`L2/P21/P22`も指定すると面荷重です。
一つのケース内の`load_inf`をすべて加算します。旧JSONの複数ケースは先頭一件だけを選びます。
未選択ケースの荷重は加算しません。選択ケースの`load_inf`と新形式`spatial_loads`の併記は拒否します。

## Python APIと面荷重

定義型は不変です。変更には新しい定義を`set_spatial_loads()`へ渡します。
次の例はPythonだけで同じ二本の梁を作り、パネル全体に強度-3の面荷重を載せます。

<!-- run: spatial-python-area -->
```python
from math import isclose
from fem import FemModel, BarParameter
from fem.spatial_loads import SpatialLoadDefinitions, SpatialLoadPanel, SpatialLoadPath, SpatialLoad

model = FemModel()
for node, (x, y) in enumerate(((0, 0), (2, 0), (2, 2), (0, 2)), 1):
    model.add_node(node, x, y, 0)
model.add_material(1, "Example", E=1000, nu=0.3)
model.material.add_bar_parameter(1, BarParameter(area=1, Iy=1, Iz=1, J=1))
model.add_element(1, "bar", [1, 2], 1, section_id=1, shear_correction=False)
model.add_element(2, "bar", [4, 3], 1, section_id=1, shear_correction=False)
for node in (1, 4):
    model.add_restraint(node, True, True, True, True, True, True)
model.set_spatial_loads(SpatialLoadDefinitions(
    panels=(SpatialLoadPanel(7, (1, 2, 3, 4), triangles=((1, 2, 3), (1, 3, 4))),),
    paths=(SpatialLoadPath(1, ((0, 0, 0), (2, 0, 0))),
           SpatialLoadPath(2, ((0, 2, 0), (2, 2, 0)))),
    loads=(SpatialLoad(1, 7, (1, 2), ((-3, -3), (-3, -3))),),
))
result = model.run("static")
audit = result["spatial_load_contribution"]
assert isclose(audit["resultant"][2], -12, abs_tol=1e-12)
assert isclose(audit["moment"][1], 12, abs_tol=1e-12)
assert isclose(sum(r["fz"] for r in result["reaction_forces"].values()), 12, abs_tol=1e-12)
assert model.run()["spatial_load_contribution"] == audit
```

通常荷重を残して空間荷重だけを外すには、`model.set_spatial_loads(SpatialLoadDefinitions())`を使います。

## 正規化JSONとHTTP

JSON保存時はトップレベル`spatial_loads`に、定義を欠落なく出力します。
次は上の面荷重の部分スキーマです。構造節点・材料・要素・支持はモデル本体へ指定します。

```json
{
  "spatial_loads": {
    "panels": [{"id": 7, "nodes": [1, 2, 3, 4], "triangles": [[1, 2, 3], [1, 3, 4]]}],
    "paths": [
      {"id": 1, "points": [[0, 0, 0], [2, 0, 0]]},
      {"id": 2, "points": [[0, 2, 0], [2, 2, 0]]}
    ],
    "loads": [{"id": 1, "panel_id": 7, "path_ids": [1, 2], "end_intensities": [[-3, -3], [-3, -3]]}]
  }
}
```

IDは整数へ正規化し、重複ID・未知キー・欠落参照・非有限値を拒否します。
`load_inf`の荷重IDは選択ケース内の順序により1から付けます。
`.fw3`には保存できません。定義を持つモデルは既存ファイルを開く前に拒否し、JSON保存を案内します。

[HTTP API](endpoints.md)も同じ旧形式・新形式を受け付け、通常JSONと既存の圧縮形式の双方で同じ解析結果を返します。
次の例はローカルHTTPサーバー起動後に実行します。

<!-- run: spatial-http -->
```python
import json
from math import isclose
from pathlib import Path
from urllib.request import Request, urlopen

request = Request("http://localhost:5000/", method="POST",
                  data=Path("spatial-cantilevers.json").read_bytes(),
                  headers={"Content-Type": "application/json"})
with urlopen(request) as response:
    result = json.load(response)
assert isclose(result["node_displacements"]["2"]["dz"], -0.008, abs_tol=1e-12)
assert isclose(result["spatial_load_contribution"]["resultant"][2], -6, abs_tol=1e-12)
```

## 出力と失敗時の扱い

`node_displacements`、`reaction_forces`、`element_stresses`と既存のシェル結果に荷重が反映されます。
`spatial_load_contribution`には`node_loads`、`shell_element_loads`、`cells`、`loads`（荷重別監査）、
合力`resultant`、原点回りモーメント`moment`、節点側の監査値、保存誤差を出力します。
荷重別監査には積分点数・載荷面積・載荷長さ・求積推定誤差も含まれます。

`element_nodal_equilibrium_forces`はシェルの`K_e u_e - f_e`を全体座標で返します。
`node_ids`順の各行は`[Fx, Fy, Fz, Mx, My, Mz]`です。
直接シェル荷重には空間荷重と既存`pressure`の両方を含めます。
載荷三角形からの荷重は節点荷重なので、シェル直接荷重には含めません。
既存の構成則によるシェル応力・断面合力・辺の力は、引き続き別の出力です。

定義は読込時に既存荷重へ加算せず、解析ごとに組み立てます。保存・再読込・再解析で荷重は累積しません。
解析に失敗した場合は結果とコンパイル済み空間荷重を破棄し、修正後の再試行では最新の入力から作り直します。
空間荷重がないモデルには、この追加出力を付けません。

## 制限とエラー

- 対象は単純・凸・穴なしのパネルと載荷帯です。非凸領域、穴、自己交差、経路の重複点、退化要素、
  不連続なパネル、パネル外への載荷を拒否します。外挿や凸包への置換はしません。
- 傾斜平面・曲面、任意方向・面法線方向、構造節点と一致しない独立載荷節点は未対応です。
- `material_nonlinear`と`modal`は、強度がゼロでも荷重定義が一件あれば拒否します。
- 梁とT3/Q4シェルは公開経路を検証済みです。他の静解析対応要素の構造節点にも載荷三角形を設定できますが、
  それらへの個別の力学検証は未完了です。詳細は[対応表](elements.md)で区別しています。
- 移動荷重、最不利配置・包絡、設計活荷重モデル、旧RBF・台形則による旧値互換は未実装です。

パネル7の経路が外へ出ると、Pythonでは`ValueError`を継承する入力例外が発生し、
HTTPでは400・`error_code: "invalid_input"`・`converged: false`を返します。
メッセージにパネル・荷重・経路など原因のIDを含め、剛性行列組立前に停止します。
対象外解析は400・`unsupported_analysis`で、`details`に`analysis_type`、`features`、`panel_ids`、`load_ids`を返します。

<!-- run: spatial-unsupported-analysis -->
```python
from fem import FemModel

model = FemModel()
model.load_model("spatial-cantilevers.json")
try:
    model.run("modal")
except ValueError as error:
    assert error.error_code == "unsupported_analysis"
    assert error.details["panel_ids"] == [7]
    assert error.details["load_ids"] == [1]
else:
    raise AssertionError("modal must reject spatial loads")
assert model.results is None
assert model.run("static")["metadata"]["solver"]["converged"] is True
```
