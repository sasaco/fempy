# ファイル入出力・VTK

[Wikiホーム](index.md) · [モデルの入力](data-structures.md) · [Python API](python-api.md)

継続してモデルを扱う場合はJSONを使います。既存データを読み込むために `.fem` と `.fw3` の入口もありますが、対応範囲が異なります。

## 対応形式

| 形式 | 読込 | 書込 | 用途と範囲 |
|---|---|---|---|
| 編集用JSON：`node/member/element` | 対応 | 元の編集形式では保存しない | 荷重ケース・部材荷重・着目点を含む入力 |
| 保存用JSON：`nodes/elements/materials` | 対応 | 対応 | 解析モデルを保存して再読込する |
| 結果JSON | 対応 | 対応 | 数値結果の保存・別アプリとの連携 |
| V0構造形式 `.fem` | 対応範囲あり | 非対応 | 対応するシェル・ソリッドの既存データ移行 |
| 独自形式 `.fw3` | 部分対応 | 部分対応 | 簡易な旧形式。完全なモデル保存には使わない |
| ASCII VTK | 読込APIなし | 制約付き | 対応セルの変位・整形したスカラー等の可視化 |

## 保存して再び解析する

[はじめに](getting-started.md)の`beam.json`を用意して実行します。

<!-- run: model-result-roundtrip -->
```python
from math import isclose
from fem import FemModel
from fem.file_io import read_result

model = FemModel()
model.load_model("beam.json")
before = model.run()
model.save_model("saved-model.json")
model.save_results("saved-result.json")

restored = FemModel()
restored.load_model("saved-model.json")
after = restored.run()
assert isclose(after["node_displacements"][2]["dy"], before["node_displacements"][2]["dy"], rel_tol=1e-8)

saved = read_result("saved-result.json")
assert "2" in saved["node_displacements"]  # JSONのIDは文字列
print(saved["node_displacements"]["2"])
```

保存するモデルは、ケース選択・分割後の解析モデルです。元の編集用JSONにあったすべての荷重ケースや編集情報を再生成する機能ではありません。元の入力も別に保管してください。

解析設定を保持したい場合は、保存前に`model.analysis_type`と`model.analysis_params`へ設定します。`run("static")`の引数は、その呼出しに使う指定であり、保存される`model.analysis_type`を書き換える操作ではありません。

非線形材料の定義と載荷係数は保存できますが、確定済みの内部履歴状態は保存しません。再読込後は最初から解析します。

## 保存用JSONの構造

| キー | 値の形 |
|---|---|
| `nodes` | ID → `[x, y, z]` |
| `elements` | ID → `type, nodes, material_id`と要素固有パラメータ |
| `materials` | ID → `name, E, nu, density, alpha, shear_modulus`など |
| `bar_parameters` | 断面ID → `area, Iy, Iz, J, kappa_y, kappa_z`など |
| `nonlinear_materials` | ID → JR材料。正側名称は`delta_1_pos, P_1_pos`など |
| `boundary_conditions.restraints` | 節点ID → `dof, values` |
| `boundary_conditions.loads` | 節点ID → `[fx, fy, fz, mx, my, mz]` |
| `boundary_conditions.spring_supports` | 節点ID → 方向と剛性 |
| `boundary_conditions.pressures` | `element_id, face, pressure`の配列 |
| `analysis_type, analysis_params` | 解析種別・制御値 |

保存される`auxiliary_restraint_nodes`は、2D化で自動追加した支持節点を区別する情報です。通常は利用者が手で作る必要はありません。

この形式で線形梁を手書きするときは、`elements`に`section_id`を、`bar_parameters`に対応する断面を指定します。`shear_correction`も明示してください。部材荷重は要素の`line_loads`、温度は`temperature`、分布ばねは`foundation`、回転解放は`releases`として保存されます。編集用JSONの`load_member/fix_member/joint`をそのままこの形式へ移しても処理されません。

`materials`の`k`と`c`は保存する物性欄ですが、現行の公開解析に熱伝導ソルバーがあることを意味しません。直接`BoundaryCondition`へ追加した汎用分布荷重など、保存対象に含まれない状態もあります。独自の低水準モデルは保存前後の解析条件を比較してください。

## 結果JSON

`save_results()`、`write_result()`、HTTPの結果変換はNumPy配列を配列へ、IDキーを文字列へ変換します。NaN・Infinityを含む結果は保存・送信できません。

`read_result()`や`load_results()`はJSONをそのまま読むため、文字列キーやリストを整数キー・NumPy配列へ復元しません。読込後は`model.get_results()["node_displacements"]["2"]`のようにアクセスします。数値比較が必要なら配列を`numpy.asarray()`で変換してください。

## V0の構造ファイル `.fem`

空白区切りのレコード形式です。現行リーダーは未対応行を黙って捨てず、行番号付きの入力エラーにします。

| レコード | 主な内容 |
|---|---|
| `Node` | ID、x、y、z |
| `Material` | ID、E、nu、G、密度、熱伝導率、比熱 |
| `ShellParameter` | ID、厚さ |
| `TriElement1 / QuadElement1` | ID、材料ID、厚さID、節点ID列 |
| `TetraElement1 / WedgeElement1 / HexaElement1` | ID、材料ID、一次要素の節点ID列 |
| `TetraElement2 / WedgeElement2 / HexaElement2` | ID、材料ID、二次要素の節点ID列 |
| `Restraint` | 節点ID、各自由度の「拘束フラグ・指定変位」の組。3または6成分 |
| `Load` | 節点ID、力3成分、必要ならモーメント3成分 |
| `Pressure` | シェルID、F1/F2、圧力 |

現行リーダーは梁用のV0レコード、特殊座標系、熱解析レコードなどの全機能を移植したものではありません。`docs/v0`内にサンプルがあることだけでは、現在の読込・解析対応を意味しません。詳細は[V0リーダー](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/src/fem/v0_io.py)と[入力テスト](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/tests/io/test_source_input.py)を参照してください。

## 独自形式 `.fw3`

`*NODES`、`*ELEMENTS`、`*RESTRAINTS`、`*LOADS`などのセクション形式です。現行読込は材料・断面情報を完全に復元せず、書き出した`*PRESSURES`も読込側が処理しません。これだけで解析条件を保った保存・再読込はできません。新しい作業の保存にはJSONを使ってください。

## VTKで変位と端力を可視化する

VTKライターは現行の要素・結果形式をすべて扱えるわけではありません。`shell`、`nonlinear_bar`、`tetra2/wedge2/hexa2`などはセル種別の対応がなく、セルタイプ0になります。通常の`shell`を含む解析を、そのまま完全なVTK出力として利用しないでください。

また、梁の`i_end/j_end`やソリッドの積分点応力は配列を含むため、`write_vtk()`へ結果を丸ごと渡すと変換に失敗する場合があります。次は対応する`bar`モデルに限定し、端力を要素ごとのスカラーへ明示的に変換した例です。

<!-- run: beam-vtk -->
```python
from pathlib import Path
from fem import FemModel
from fem.file_io import write_vtk

model = FemModel()
model.load_model("beam.json")
result = model.run()
vtk_result = {
    "node_displacements": result["node_displacements"],
    "element_stresses": {
        eid: {"axial_i": float(force["i_end"][0]),
              "moment_z_i": float(force["i_end"][5])}
        for eid, force in result["element_stresses"].items()
    },
}
write_vtk({"mesh": model.mesh}, vtk_result, "beam.vtk")
text = Path("beam.vtk").read_text(encoding="utf-8")
assert "CELL_TYPES 1\n3\n" in text
assert "VECTORS displacement float" in text
assert "SCALARS stress_moment_z_i float 1" in text
print("beam.vtk を保存しました")
```

このファイルには元のメッシュ座標と変位ベクトルが入ります。ParaViewなどで`displacement`を変形表示に使えます。ライターが付ける`stress_`という接頭辞にかかわらず、例の`stress_axial_i`はN、`stress_moment_z_i`はN·mの端力です。材料応力ではありません。

この制約を避けて新しい要素を可視化したい場合は、まず[結果JSON](results.md)を取り出し、要素種別・テンソル・積分点の対応を保った変換を別途用意する必要があります。
