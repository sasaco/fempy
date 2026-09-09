# 結果の読み方

[Wikiホーム](index.md) · [モデルの入力](data-structures.md) · [材料非線形解析](nonlinear-analysis.md)

まず節点変位と支点反力を確認し、次に梁の端力やシェル・ソリッドの応力を読みます。入力と同じ単位系で返り、変位をmm、回転をミリラジアンへ自動変換する処理はありません。

## PythonとJSONの違い

| 項目 | `model.run()`のPython結果 | HTTP・保存JSON |
|---|---|---|
| 節点・要素ID | 整数の辞書キー | 文字列の辞書キー |
| 数値配列 | NumPy配列を含む | JSON配列 |
| ケース階層 | 1ケース分の辞書 | 1ケース分のオブジェクト |

Pythonでは`result["node_displacements"][2]["dy"]`、HTTPでは`result["node_displacements"]["2"]["dy"]`です。現在のHTTPは`case1.disg`や`case1.fsec`という階層を返しません。`disg/reac/fsec`は参照比較用の別ビューで使われる名称です。

## 線形静解析の主な項目

| キー | 内容 |
|---|---|
| `analysis_type` | `static` |
| `node_displacements` | 節点IDごとの変位・回転。通常の利用ではこの辞書を使う。 |
| `reaction_forces` | 支持節点・支持成分の反力。 |
| `element_stresses` | 梁では端力、シェル・ソリッドでは応力・ひずみ。 |
| `displacement` | 全自由度を並べた配列。節点数を表す配列ではない。 |
| `displacement_correction` | 静解析の微小な変位補正。詳細は後述。 |
| `shell_results` | シェルがある場合の両面・合力・エネルギー。 |
| `legacy_shell_results` | シェルの旧比較形式。通常は`shell_results`を使う。 |

数値計算の経路によって、`precise_end_forces`、`interpolated_displacements`、`constitutive_element_stresses`、`force_recovery`が追加されることがあります。固定された出力キー集合を仮定せず、必要な項目を読み取ってください。

線形結果には非線形用の`step_results`や`curvature`を追加しません。また、線形の成功結果に`converged`キーはありません。

## 節点変位と支点反力

```json
{
  "node_displacements": {
    "2": {"dx": 0.0, "dy": -0.0013333333333333333, "dz": 0.0,
          "rx": 0.0, "ry": 0.0, "rz": -0.001}
  },
  "reaction_forces": {
    "1": {"fx": 0.0, "fy": 1000.0, "fz": 0.0,
          "mx": 0.0, "my": 0.0, "mz": 2000.0}
  }
}
```

これは[最初の片持ち梁](getting-started.md)の結果の一部です。変位は全体XYZ方向、回転は全体XYZ軸まわりです。反力は構造物に作用する向きで、支持ばねの成分では`−k u`です。

反力辞書は、拘束・ばねがある成分だけを返します。たとえばX方向だけのばねなら`fx`だけの場合があります。2D化で自動追加した補助支持の節点は、反力一覧から除外されます。

全変位配列は節点IDの昇順に並び、梁・シェルを含むモデルでは1節点6成分、対応するソリッドだけなら3成分です。内部と線形の節点変位辞書は6キーを持ち、3DOFモデルの回転は0です。非線形のFemModel／HTTP互換出力では、3DOFモデルを`dx, dy, dz`の3キーに射影します。

## 梁の端力

`element_stresses`という名前ですが、梁に対して返るのは応力ではなく**断面の力・モーメント**です。

```json
{
  "element_stresses": {
    "1": {
      "i_end": [0.0, 1000.0, 0.0, 0.0, 0.0, 2000.0],
      "j_end": [0.0, -1000.0, 0.0, 0.0, 0.0, 0.0]
    }
  }
}
```

各配列の順序は`[N, Vy, Vz, T, My, Mz]`です。**要素局所座標での節点抵抗力の符号**を使います。軸引張を受ける梁では、i端のNが負、j端のNが正になります。断面力図で両端の引張を正表示するための符号変換は適用していません。

分布荷重や温度荷重を持つ線形梁の端力は、それらの荷重項を含めて計算します。単に弾性剛性と変位を掛け直した値で置き換えないでください。任意断面の材料応力を得るには、断面形状に応じた換算・後処理が別途必要です。

### 分割前の部材との対応

結果のキーは分割後の解析要素IDです。Pythonでは`model.mesh.elements`で、`original_id`、`member_start`、`member_end`、`nodes`などのメタデータを確認できます。元の部材IDと端からの位置を使って結果を整理してください。旧形式のシェル・ソリッドには元のIDを表す`shell_id`・`solid_id`が保持されます。

### 釣合いによる端力回復

線形梁で自由端から静力学的に力を決められる枝は、釣合いから端力を回復する場合があります。

- `element_stresses`：利用者向けの回復後の端力。
- `constitutive_element_stresses`：回復前の構成則による結果。
- `force_recovery`：`method: "free_branch_equilibrium"`と対象要素ID。

分布ばね、非線形要素、シェル、閉路を越えて力を決める処理ではありません。構成則との不一致が許容範囲を超えればエラーにします。非線形解析に含まれる線形梁では、段階ごとの結果にも適用されます。

## 材料非線形の段階結果

最終結果には`converged: true`、`step_results`、`convergence_history`、`curvature`が加わります。最後の載荷段階の状態が最終結果であり、途中の最大値ではありません。

| `step_results`内のキー | 意味 |
|---|---|
| `step` | 1始まりの段階番号。載荷係数0を指定した場合も1段階として数える。 |
| `lambda` | その段階の基準荷重・強制変位に対する倍率。 |
| `displacement`、`node_displacements` | その段階で収束した変位。 |
| `reaction_forces` | その段階の支点・ばね反力。 |
| `element_stresses` | 梁の確定端力。ソリッドの段階応力配列はここには追加されない。 |
| `curvature` | 非線形梁の中央断面全曲率。 |
| `converged`、`iterations` | 収束したことと、その段階の反復数。 |

シェルを含むモデルでは、`shell_results`と`legacy_shell_results`を各段階にも追加します。未収束の段階を`converged: false`として保存しながら最後まで進める仕様ではありません。未収束時は解析を停止して例外・HTTP 422を返します。

### 応答曲率

```json
{
  "curvature": {"7": {"y": 0.0, "z": 0.002}}
}
```

- `y, z`は局所軸まわりの**中央断面の全曲率**です。単位は1/L。
- 弾性分と塑性分を含みます。増分、最大経験値、塑性曲率だけの値、材端回転角ではありません。
- 対象は履歴則を設定した非線形梁です。その梁では非線形化していない曲げ軸も含め、両軸を返します。軸ひずみ・ねじり率はこの項目に含みません。
- 除荷してモーメントが0でも、残留曲率を持つ場合があります。
- 該当する非線形梁がない非線形解析では空辞書です。静解析にはこの項目がありません。
- 分割モデルでは解析要素ごとの値で、元部材の平均や材端別の値ではありません。

### 収束履歴

`convergence_history`はNewton反復ごとの配列です。各記録に`step`、`lambda`、`iteration`、`residual_norm`、`relative_residual`、`relative_du`があります。初回反復の`relative_du`はnullです。収束判定の詳細は[解析ワークフロー](workflow.md)を参照してください。

## シェルの結果

両面の曲げを含めて読む場合は`result["shell_results"][要素ID]`を使います。`element_stresses`側のシェル出力は、局所面内3成分の積分点応力・ひずみで、両面の曲げ応力を表しません。

| `shell_results`内のキー | 内容 |
|---|---|
| `node_ids` | 結果配列の節点順序 |
| `local_basis` | 局所基底。行が局所x、y、法線方向の全体座標成分 |
| `formulation` | `dkt`または`mindlin` |
| `raw_result` | 上下面の節点値・面積平均値。下記参照 |
| `resultants.membrane` | 局所座標の面内合力`[Nx, Ny, Nxy]`、F/L |
| `resultants.moment` | 局所座標の曲げ合力`[Mx, My, Mxy]`、F·L/L |
| `resultants.shear` | 局所座標の合せん断力`[Qx, Qy]`、F/L |
| `edge_resultants` | 辺ごとの節点ID・外向き法線・両端の等価な合力と偶力。成分は全体座標 |
| `strain_energy` | 要素全体に体積積分した物理的なひずみエネルギー |
| `drilling_energy` | 面内回転を変位場と結び付ける項のエネルギー。物理エネルギーとは分ける |
| `transverse_shear_recovery` | DKTは`moment_equilibrium`、Mindlinは`constitutive` |

`raw_result`の接尾辞`1`は節点順序の法線側（局所z=+t/2）、`2`は反対側（−t/2）です。

| 名前 | 意味 |
|---|---|
| `nodeStress1/2`、`nodeStrain1/2` | 各節点の表面値。配列順は`node_ids`と一致 |
| `elemStress1/2`、`elemStrain1/2` | 面積で重み付けした表面平均 |
| `nodeEnergy1/2`、`elemEnergy1/2` | 表面のエネルギー密度。要素全体のエネルギーとは異なる |

応力・ひずみテンソルは**全体座標**の`[xx, yy, zz, xy, yz, zx]`です。ここでのせん断ひずみはテンソル成分`γ/2`です。Mindlinの横せん断応力は構成則による値で、表面でせん断応力0となる分布を回復したものではありません。DKTの回復合せん断力は、表面のせん断応力・エネルギーには追加しません。

互換項目`stress`の`mx/my/mxy/qx/qy`という名前は、物理的な曲げ合力を意味しません。応力は`raw_result`、断面合力は`resultants`を使ってください。旧シェルの仮想梁断面力は廃止されています。

### 旧シェル比較形式との違い

`legacy_shell_results`は読込順の0始まりキー、工学せん断ひずみγ、積分点の単純平均を使い、`strain_energy`は上面の平均エネルギー密度です。通常形式の実要素ID、テンソルせん断γ/2、面積平均、体積積分エネルギーとは区別します。下面を上面で置き換えるなど、過去の不具合は再現しません。

## ソリッドの応力・ひずみ

`element_stresses[要素ID]`の`gauss_points`、`strain`、`stress`を使います。各行は積分点に対応し、節点への外挿や節点平均ではありません。

- 応力は全体座標の`[σxx, σyy, σzz, τxy, τyz, τzx]`。
- ひずみは全体座標の`[εxx, εyy, εzz, γxy, γyz, γzx]`。
- ソリッドのせん断ひずみは**工学せん断γ**で、シェルの`raw_result`とは異なります。

## 固有値解析の結果

| キー | 内容 |
|---|---|
| `n_modes` | 要求し、実際に返されたモード数 |
| `eigenvalues` | 固有値ω² |
| `eigenvectors` | 列ごとの固有ベクトル |
| `frequencies` | 振動数ω/(2π)。整合した秒単位の入力ならHz |
| `periods` | 正の振動数に対応する周期。ゼロモードはPythonで`inf`、JSONで`null` |
| `eigenpair_residuals` | 各固有対の相対残差 |
| `mass_orthogonality_error` | 質量行列に関する直交性の最大誤差 |
| `modes` | モードごとの節点変位辞書の配列 |

モード形は質量正規化されますが、その大きさや全体の符号は実際の載荷による変位ではありません。重根では個々のベクトルではなく部分空間を比較します。正の有限固有値を確認してから読み、[固有値解析の制約](elements.md)を考慮してください。周期以外の非有限値はHTTP送信・JSON保存でエラーになります。

## 静解析の微小補正

`displacement_correction`は`displacement`と同じ順序の微小補正です。大きな剛体移動と小さな変形が混在する場合に、微小な伸び・曲げを失わないよう主値と補正値を分けて保持します。通常の変位表示には`node_displacements`を使います。

梁端力・反力の計算では補償演算を使うため、主値と補正値を倍精度で先に足してから端力を再計算すると、重要な補正が消える場合があります。非線形の段階変位・履歴則には、この静解析用補正を追加していません。
