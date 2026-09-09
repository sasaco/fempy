# はじめに：片持ち梁を解析する

[Wikiホーム](index.md) · 次へ：[モデルの入力](data-structures.md)

長さ2 mの梁を左端で固定し、右端を1,000 Nで下向きに押します。右端のたわみと、左端が受け持つ反力を計算します。この例は線形弾性・せん断変形なしで、手計算と比較できます。

## 1. 実行環境を用意する

Python 3.11以上が必要です。リポジトリを取得したディレクトリで、uvを使って依存を揃えます。

```console
uv sync --locked --extra dev
```

以降のPythonコマンドは`uv run --locked --extra dev python ...`で実行します。別の仮想環境へインストールする場合は、その環境で次を実行できます。こちらは`uv.lock`による固定ではありません。

```console
python -m pip install -e ".[dev]"
```

インポート名は`fem`です。リポジトリ内の配置を表す`src.fem`には依存しません。

## 2. モデルをJSONで保存する

以下をUTF-8の`beam.json`として保存します。これは省略のない、実行可能な入力です。

<!-- fixture: beam.json -->
```json
{
  "dimension": 2,
  "analysis_type": "static",
  "node": {
    "1": {"x": 0.0, "y": 0.0, "z": 0.0},
    "2": {"x": 2.0, "y": 0.0, "z": 0.0}
  },
  "member": {
    "1": {"ni": 1, "nj": 2, "e": 1, "cg": 0, "shear_correction": false}
  },
  "element": {
    "1": {
      "1": {"E": 200000000000.0, "nu": 0.3, "A": 0.01, "Iy": 0.00001, "Iz": 0.00001, "J": 0.00002}
    }
  },
  "fix_node": {
    "1": [{"n": 1, "tx": 1, "ty": 1, "tz": 1, "rx": 1, "ry": 1, "rz": 1}]
  },
  "load": {
    "tip": {
      "element": 1,
      "fix_node": 1,
      "load_node": [{"n": 2, "ty": -1000.0}]
    }
  }
}
```

| 入力 | この例での意味 |
|---|---|
| `node` | 左端・右端の節点座標。2Dでも`z`を省略せず、0とします。 |
| `member` | 節点1から2へ向かう梁。`e: 1`で材料・断面を参照します。 |
| `element` | 外側の`1`が材料ケース、内側の`1`が材料・断面IDです。 |
| `fix_node` | 支持ケース1。節点1の並進・回転を固定します。 |
| `load` | 荷重ケース`tip`。全体Y方向へ−1,000 Nを与えます。 |

ここでは`E`をN/m²、座標をm、断面積をm²、断面二次モーメントとねじり定数をm⁴で指定しました。`dimension: 2`の梁モデルはXY平面の解析で、面外の`dz, rx, ry`を自動拘束します。

## 3. 解析して結果を読む

以下を`analyse.py`に保存します。`beam.json`と同じディレクトリから実行してください。

<!-- run: first-beam -->
```python
from math import isclose
from fem import FemModel

model = FemModel()
model.load_model("beam.json")
result = model.run()

tip = result["node_displacements"][2]
support = result["reaction_forces"][1]
print(f"右端のたわみ: {tip['dy'] * 1000:.6f} mm")
print(f"左端の鉛直反力: {support['fy']:.1f} N")
print(f"左端の反力モーメント: {support['mz']:.1f} N m")
print(model.get_model_info())

# 片持ち梁のたわみ P L^3 / (3 E I) と釣合いを確認
expected_dy = -1000 * 2**3 / (3 * 200e9 * 1e-5)
assert isclose(tip["dy"], expected_dy, rel_tol=1e-8, abs_tol=1e-12)
assert isclose(support["fy"], 1000, rel_tol=1e-8)
assert isclose(support["mz"], 2000, rel_tol=1e-8)
```

```console
uv run --locked --extra dev python analyse.py
```

読み込み時のログに続いて、次の値を確認できます。

```text
右端のたわみ: -1.333333 mm
左端の鉛直反力: 1000.0 N
左端の反力モーメント: 2000.0 N m
```

`dy`はmで返るので、表示時に1,000倍しました。JSON／HTTPでは節点IDが文字列になるため、`[2]`の代わりに`["2"]`でアクセスします。梁の端力は`result["element_stresses"][1]`の`i_end`と`j_end`です。[結果の読み方](results.md)で成分順序と符号を説明しています。

## 4. 結果を保存する

解析済みの`model`に対して`model.save_results("result.json")`を呼ぶと、NumPy配列をJSONで保存できます。モデルの保存は`model.save_model("saved-model.json")`です。入力の編集用JSONとは形式が変わるので、詳しくは[ファイル入出力](file-formats.md)を参照してください。

## 次に試すこと

- 荷重を2倍にして、線形解析の変位・反力も2倍になるか確認する。
- [実行例](examples.md)で分布荷重、面圧、立体要素、ケースごとの解析を試す。
- [材料非線形解析](nonlinear-analysis.md)で、剛性が変化する梁と履歴を扱う。
- 他のアプリから呼び出す場合は[HTTP API](endpoints.md)へ進む。

エラーになった場合は、[エラーと対処](error-handling.md)の入力形式・支持条件から確認してください。
