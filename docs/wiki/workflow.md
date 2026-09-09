# 解析ワークフロー

[Wikiホーム](index.md) · [Python API](python-api.md) · [結果の読み方](results.md)

このページは、入力から結果までの処理と、要素・ソルバーを直接利用する場合の契約を説明します。通常の解析は`FemModel`を使えば、この流れをまとめて実行できます。

## 入力から結果まで

```text
JSONファイル／HTTP入力
    ↓ 形式の変換、ケース選択、参照の解決
内部モデル（mesh・material・boundary・section）
    ↓ 部材の分割、荷重・剛域・材端条件の対応付け
要素オブジェクトと自由度の配置
    ├─ static：参照弾性剛性 → 境界条件 → 直接求解
    ├─ material_nonlinear：載荷段階 → Newton反復 → 履歴確定
    └─ modal：剛性・質量行列 → 固有値問題
    ↓ 変位・反力・端力・応力の整理
Python結果辞書 → 必要に応じてJSON保存／HTTP応答
```

### 1. 入力の変換とケース選択

`load_model()`は拡張子に応じた読込を行い、変換済みモデルを`read_json_model()`へ渡します。HTTPは生のJSONを同じ変換処理へ渡します。

編集用JSONは先頭の荷重ケースを選び、そのケースが参照する材料・支持・分布ばね・材端条件を解決します。全ケースの解析を並列に実行する処理ではありません。

### 2. 部材の分割

着目点、分布荷重の境界、集中荷重位置、剛域の境界から分割位置を作ります。荷重を分割後の要素へ配分し、材端の回転解放を元部材の外端へ引き継ぎます。要素の元IDと元部材上の位置も保持します。

他の荷重ケースの部材荷重位置を共通の分割位置として参照する場合がありますが、実際に載荷するのは選んだケースです。位置が増えることと、複数荷重ケースを計算することは異なります。

### 3. 自由度と行列の組立

節点IDの昇順にコンパクトな自由度表を作ります。対応するソリッドだけなら3並進、それ以外のモデルは6成分を確保し、各要素を必要な自由度へ対応付けます。IDが飛び番でも、`(node_id−1)×6`で直接添字を計算しません。

要素の局所剛性・荷重を全体座標へ変換し、支持ばねや強制変位を反映します。静解析の拘束処理は対称消去、固有値解析では剛性・質量の両方から同じ拘束自由度を除いて縮約します。

### 4. 解析

線形静解析は1回の全荷重を参照弾性剛性で解きます。スケーリングや補償計算、必要に応じた高精度計算を使い、小さな変形や端力を保持します。非線形解析の段階を1にした計算とは、材料則も数値処理も異なります。

材料非線形解析は指定された係数列を順に処理します。各段階で基準荷重と強制変位を同じ係数で変化させ、Newton反復と残差に基づく増分縮小を行います。

- 自由DOFの釣合い残差を、`max(自由DOFの外力ノルム, 1)`で正規化します。
- 変位増分も`max(変位ノルム, 1)`で正規化します。
- 通常は相対残差と相対増分の両方が許容差未満で収束します。
- 初回に残差が十分小さい場合は、拘束後の接線行列の可解性も確認して受理します。
- 試行評価は確定履歴を基準に行い、収束した段階だけをコミットします。

失敗時は直前の確定状態へ戻して例外を送出します。失敗状態のまま次段階へ進めません。

### 5. 後処理

梁の端力、シェルの両面結果、ソリッドの積分点応力を計算します。対象の線形梁では釣合いから端力を回復し、構成則との差も検査します。後処理が失敗した場合も解析成功として返しません。

## 共通ソルバーAPI

`FemModel.run()`の静解析・材料非線形解析は共通の`Solver`を使います。固有値解析は`eigenvalue_analysis()`という別の経路です。

| 呼出し | 意味 |
|---|---|
| `solver.solve(mesh, material, boundary, elements)` | 4位置引数の既存呼出しは線形静解析 |
| `solver.solve(..., analysis_type="material_nonlinear", n_steps=10, max_iter=50, tol=1e-6, load_factors=None, callback=None)` | 材料非線形解析 |
| `solver.eigenvalue_analysis(..., n_modes=10)` | 固有値解析 |

低水準`solve()`へ`static`と`load_factors`を同時指定すると入力エラーです。`FemModel.run("static")`は非線形用係数を渡さず、基準荷重を1回で解きます。

旧`fem.nonlinear.nonlinear_solver.NonlinearSolver`、`solve_nonlinear()`は共通実装に委譲する互換入口です。`NonlinearSolver.solve()`は非線形解析を選びます。`FemModel.nonlinear_solver`は同じソルバーの変位・収束履歴を参照する互換ビューです。

## コールバックで確定段階を受け取る

低水準APIを使う場合の例です。[はじめに](getting-started.md)の`beam.json`を用意します。線形梁を非線形解析モードで段階載荷し、確定した載荷係数を記録します。要素は線形のままです。

<!-- run: solver-callback -->
```python
from math import isclose
from fem import FemModel, Solver

model = FemModel()
model.load_model("beam.json")
accepted_factors = []

def on_step(snapshot):
    accepted_factors.append(snapshot["lambda"])

solver = Solver()
result = solver.solve(
    model.mesh, model.material, model.boundary, model.elements,
    analysis_type="material_nonlinear",
    load_factors=[0, 0.5, 1], callback=on_step,
)
assert accepted_factors == [0, 0.5, 1]
assert isclose(result["node_displacements"][2]["dy"], -1/750, rel_tol=1e-8)
print(accepted_factors)
```

コールバックは段階を保存した後にスナップショットのコピーを受け取ります。コールバックが例外を送出すると、その段階は確定した状態で停止します。通常の解析未収束のロールバックとは区別してください。`FemModel.run()`やHTTPにcallback引数はありません。

低水準ソルバーの返値には、FemModelが追加するすべての後処理結果は含まれません。低水準の非線形最終返値には最終`element_stresses`を追加せず、段階結果に端力を持ちます。通常の応力・シェル結果が必要ならFemModelを利用します。

## 状態と結果の独立性

各solve呼出しで解析状態を初期化し、`reset_states()`を持つ要素は履歴も初期化します。最終返値、段階結果、コールバックの辞書・配列は独立したコピーです。内部の`solver.step_results`は線形・非線形の両方で利用できますが、線形の外部結果へは段階履歴を追加しません。

内部の節点変位と新しい非線形`Solver.solve()`の返値は6キーです。3DOFメッシュの従来`NonlinearSolver`／`solve_nonlinear()`／FemModel／HTTP／非線形callbackは`dx, dy, dz`へ射影します。`NonlinearConvergenceError`の旧importと`step, load_factor, displacement`の属性も維持しています。

## 実装への案内

- [モデルの組立・後処理](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/src/fem/model.py)
- [JSON・ファイル変換](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/src/fem/file_io.py)と[編集用部材の分割](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/src/fem/legacy_beam.py)
- [自由度の配置](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/src/fem/dof.py)
- [共通ソルバー](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/src/fem/solver.py)と[釣合い計算](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/src/fem/equilibrium.py)
- [段階結果と互換出力](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/src/fem/solver_results.py)

`src/fem/assembly.py`やモジュール直下の`solve_linear_system()`を使う旧Wikiの例は、現行構成にはありません。実装を拡張するときは上記の入口と[テストガイド](https://github.com/sasaco/fempy/blob/02e55e59d725ceab78e89fdb03200e21c44ac732/tests/README.md)を参照してください。
