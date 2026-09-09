# FEMPython

FEMPythonは、梁・シェル・ソリッドで構造物の変位、反力、断面力、応力を計算する
Python有限要素解析ライブラリです。Python API、JSONファイル、およびリポジトリ／
コンテナから起動するHTTP APIを提供します。

現行実装は、線形静解析、JR総研剛性低減RC型の材料非線形解析、固有値解析に対応します。
要素ごとの対応範囲と未実装の組合せは[要素と解析の選び方](docs/wiki/elements.md)を確認してください。

## インストール

配布wheelを利用する場合:

```bash
python -m pip install FEMPython
```

リポジトリから開発・検証する場合はPython 3.11以上、uv、固有の旧JavaScript比較を行う場合は
Node.jsを用意します。

```bash
git clone https://github.com/sasaco/fempy.git
cd fempy
uv sync --locked --extra dev
```

公開import名は`fem`と`app`です。リポジトリ内部の配置名である`src.fem`は使いません。

## 最小の解析例

次はN・m・sの一貫単位系で、長さ1 m、`E=1000 N/m²`、断面積2 m²の軸材へ
100 Nを載荷します。独立解は`u=F L/(E A)=0.05 m`です。

```python
from math import isclose
from fem import BarParameter, FemModel

model = FemModel()
model.add_node(1, 0, 0, 0)
model.add_node(2, 1, 0, 0)
model.add_material(1, "test", E=1000, nu=0.25, density=2)
model.material.add_bar_parameter(
    1, BarParameter(area=2, Iy=1, Iz=1, J=1)
)
model.add_element(
    1, "bar", [1, 2], 1, section_id=1, shear_correction=False
)
model.add_restraint(1, True, True, True, True, True, True)
model.add_restraint(2, False, True, True, True, True, True)
model.add_load(2, fx=100)

result = model.run("static")
displacement = result["node_displacements"][2]["dx"]
assert isclose(displacement, 0.05, rel_tol=1e-12)
print(displacement)
```

入力値へ単位情報は保存されません。力・長さ・時間の単位をモデル全体で統一してください。
入力形式、境界条件、結果の符号・評価位置は[ユーザーガイド](https://sasaco.github.io/fempy/)にあります。

## HTTP API

HTTP入口の`main.py`はwheelには含まれません。リポジトリまたは提供コンテナから起動します。

```bash
uv run functions-framework --target FEMPython --source main.py --port 8080
```

旧デプロイ名`FrameWeb3`は`FEMPython`の互換aliasとして残しています。通常JSONと互換圧縮形式、
成功・失敗レスポンスは[HTTP API](docs/wiki/endpoints.md)を参照してください。

## 対応範囲

- 梁、三角形／四角形シェル、一次／二次ソリッドを扱います。
- 線形静解析、材料非線形解析、固有値解析を選択できます。
- 節点荷重、部材荷重、温度荷重、支持ばね、強制変位などは要素・解析ごとに対応範囲が異なります。
- 複数の荷重ケースを同時に重ねて解く入口ではありません。編集用JSONでは選択した1ケースを解析します。
- 一次wedgeの通常`FemModel`経路には質量行列がなく、固有値解析は未対応です。
- 影響線解析、一般的な座屈解析、幾何学的非線形解析は提供していません。

「計算が終了したこと」と「対象構造に対して妥当なモデルであること」は別です。支持条件、単位、
要素選択、メッシュ依存性を確認し、重要な用途では独立解・実験・他実装との比較を行ってください。

## 検証

ロックした開発環境で全テストとWikiの実行例を検査します。

```bash
uv run --locked --extra dev pytest tests
uv run --locked --extra dev python -m tools.validation.check_wiki
```

検証範囲と最新の結果は、[テスト保証範囲](docs/plans/test_items.md)、
[テスト台帳](docs/plans/test_inventory.md)、[品質ロードマップ](docs/plans/product-quality-roadmap.md)、
および[検証報告](docs/report/)を参照してください。

## ドキュメント

- [公開ユーザーガイド](https://sasaco.github.io/fempy/)
- [はじめに](docs/wiki/getting-started.md)
- [Python API](docs/wiki/python-api.md)
- [入力データ](docs/wiki/data-structures.md)
- [実行例](docs/wiki/examples.md)
- [結果の読み方](docs/wiki/results.md)
- [エラーと対処](docs/wiki/error-handling.md)

## ライセンス

[MIT License](LICENSE)
