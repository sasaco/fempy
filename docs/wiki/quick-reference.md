# クイックリファレンス

[Wikiホーム](index.md) · [モデルの入力](data-structures.md) · [Python API](python-api.md) · [結果の読み方](results.md)

日常的に使う指定とアクセス方法をまとめています。初回は[片持ち梁の例](getting-started.md)から始めてください。

## 解析を選ぶ

| 目的 | Pythonでの指定 |
|---|---|
| 線形静解析 | `model.run("static")` |
| 材料非線形解析 | `model.run("material_nonlinear")` |
| 入力指定・要素から自動選択 | `model.run()` |
| 固有値解析 | `model.analysis_params["n_modes"] = 3`の後に`model.run("modal")` |
| 任意の非線形載荷順序 | `model.analysis_params["load_factors"] = [0, 0.5, 1, 0, -1]` |

`static`は非線形材料の履歴を使いません。`n_load_steps`だけでは解析種別は変わりません。固有値解析には[対応要素・数値上の制約](elements.md)があります。

## 入力名の対応

| 内容 | 編集用JSON | Python API／保存用JSON |
|---|---|---|
| 節点 | `node: {"1": {"x": 0, "y": 0, "z": 0}}` | `add_node(1, 0, 0, 0)`／`nodes: {"1": [0, 0, 0]}` |
| 材料 | `element[ケースID][材料ID]` | `add_material(...)`／`materials[材料ID]` |
| 梁の断面積 | `A` | `BarParameter.area` |
| Gの明示値 | `G` | `shear_modulus` |
| ポアソン比 | `nu` | `nu` |
| 線膨張係数 | `Xp` | `alpha` |
| 密度 | `den` | `density` |
| 梁の回転角（度） | 部材の数値`cg` | 要素の`angle` |
| 節点荷重の力 | `tx, ty, tz` | `fx, fy, fz` |
| 節点荷重のモーメント | `rx, ry, rz` | `mx, my, mz` |
| 非線形骨格の正側 | `delta_1, P_1`など | 構築APIは同名。保存用`nonlinear_materials`は`delta_1_pos, P_1_pos`など |

## 支持・荷重

| 指定 | 意味 |
|---|---|
| `fix_node`の0 | 自由 |
| `fix_node`の1 | 0変位・0回転で拘束 |
| `fix_node`の0・1以外 | 絶対値を支持ばね剛性として使用 |
| 明示`restraints`の`dof, values` | 強制変位・回転。順序は`dx,dy,dz,rx,ry,rz` |
| `joint`の0／1 | 回転を解放／接続。軸名は`xi,yi,zi,xj,yj,zj` |
| `load_member.mark=1` | 部材途中の集中力 |
| `mark=11` | 部材途中の集中モーメント |
| `mark=2` | 分布荷重。全長なら`L1=0,L2=0` |
| `mark=9` | 梁の一様温度変化。`P1`に指定 |
| `mark=0` | 無効行 |
| `direction=x,y,z` | 梁局所座標の方向 |
| `direction=gx,gy,gz` | 全体座標の方向 |
| シェル`F1`の正圧 | 節点順序の法線と逆方向へ作用 |

分布荷重の通常のL2はj端からの距離です。負のL2は載荷幅です。集中荷重のL1・L2はどちらもi端からの距離です。支持の特殊な値・入力形式ごとの違いは[モデルの入力](data-structures.md)で確認してください。

## 結果にアクセスする

| 内容 | Pythonでの例 |
|---|---|
| 節点2のY変位 | `result["node_displacements"][2]["dy"]` |
| 節点1のY反力 | `result["reaction_forces"][1]["fy"]` |
| 梁7のj端曲げモーメントMz | `result["element_stresses"][7]["j_end"][5]` |
| 非線形梁7の中央曲率κz | `result["curvature"][7]["z"]` |
| 最後の非線形載荷係数 | `result["step_results"][-1]["lambda"]` |
| シェル20の上面平均応力 | `result["shell_results"][20]["raw_result"]["elemStress1"]` |
| ソリッド8の積分点応力 | `result["element_stresses"][8]["stress"]` |
| 節点数 | `model.get_model_info()["n_nodes"]` |
| 結果保存 | `model.save_results("result.json")` |

HTTP／結果JSONではIDを`["2"]`などの文字列にします。反力は支持成分だけを返すため、存在する成分を確認してください。

## 単位・符号の要点

- 全体で整合した単位を使います。N・m系なら変位はm、応力はN/m²。表示だけをmmにするなら1,000倍します。
- 梁端力は局所`[N,Vy,Vz,T,My,Mz]`、節点抵抗力の符号です。
- 非線形の`delta_*`は軸ひずみ・中央曲率・ねじり率です。節点変位ではありません。
- シェル`raw_result`のせん断ひずみは`γ/2`、ソリッドの`strain`は`γ`です。
- 編集用JSONでG省略の梁は、せん断変形を無視します。

## 困ったとき

HTTP 400は入力、422は非線形未収束、500は内部計算・結果処理などを示します。詳細は[エラーと対処](error-handling.md)へ進んでください。複数荷重ケース、圧縮転送、VTKは次のガイドに実行例があります。

- [複数ケースと各種解析の例](examples.md)
- [HTTPの実際の入出力・互換圧縮](endpoints.md)
- [JSON保存とVTKの対応範囲](file-formats.md)
