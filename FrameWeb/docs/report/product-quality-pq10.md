# PQ-10 単位換算と非線形収束判定の検証報告

実施日: 2026-09-10  
開始HEAD: `b3d33d9e`  
対象: Newton荷重制御・変位制御、JR材料骨格、支持ばね、強制変位、固有値  
実行器: [`tools/validation/unit_invariance.py`](../../tools/validation/unit_invariance.py)  
回帰試験: [`tests/validation/test_unit_invariance.py`](../../tests/validation/test_unit_invariance.py)

## 結論

同一の物理モデルをN–m、N–mm、kN–mへ一貫換算したとき、材料非線形解析の成功／失敗、
反復数、変位・回転、反力・端力、曲率、除荷・再載荷履歴が一致するようにした。密度と並進・
回転ばねを換算した固有値解析も同じ固有振動数を返した。

修正前は、許容差`0.03`の曲げ問題がN–mで4反復、N–mmで5反復だった一方、kN–mでは
初期残差の数値`0.018`を固定値`1.0`で割って1反復目を収束と判定し、未更新の変位を成功結果に
していた。修正後は3単位系すべて4反復で、物理量も一致した。許容差を緩和せず、判定量の
次元をそろえて原因を解消した。

## 一貫換算

N–mの数値から座標・変位を`a`倍、力を`b`倍する。各入力は次の倍率で換算した。

| 量 | 倍率 |
|---|---:|
| 座標・並進変位 | `a` |
| 力・軸力骨格 | `b` |
| モーメント・曲げ／ねじり骨格 | `a b` |
| `E`、`G`、応力 | `b/a²` |
| 面積 | `a²` |
| `Iy`、`Iz`、`J` | `a⁴` |
| 密度 | `b/a⁴` |
| 並進ばね | `b/a` |
| 回転ばね | `a b` |
| 軸ひずみ骨格 | 1 |
| 曲率・ねじり率骨格 | `1/a` |
| 軸の`K_min` | `b` |
| 曲げ・ねじりの`K_min` | `b a²` |

N–mmは`a=1000, b=1`、kN–mは`a=1, b=0.001`とした。基準密度7850はN–mmで
`7.85e-9`、kN–mで`7.85`となる。これはコードが自動換算した値ではなく、検証器が次元式から
独立に作った入力である。

## 収束尺度

モデル座標の各軸rangeから作るbounding-box対角長さを`L`とする。Newton反復では次を使う。

```text
force norm        = ||[Fx, Fy, Fz, Mx/L, My/L, Mz/L]||2
displacement norm = ||[dx/L, dy/L, dz/L, rx, ry, rz]||2
relative residual = residual norm /
                    max(current external, current restoring, base load-pattern norm)
relative increment = increment norm / max(current displacement norm, 1)
```

変位制御の並進拘束残差も`L`で割り、回転拘束残差はradのまま扱う。荷重倍率は無次元である。
力側の固定絶対floorは設けない。分母と残差がともに厳密な0なら相対値0、分母だけ0なら無限大と
する。除荷で荷重係数が0になる場合も、未倍率の基準荷重パターンを残すため尺度が消えない。

`convergence_history.residual_norm`は上記の等価力ノルム、`residual_scale`は分母、
`increment_norm`と`solution_norm`は無次元値である。`metadata.solver.convergence_measure`に式、
代表長さ、取得方法、残差の絶対floorなし、変位の無次元reference floor 1を保存する。
履歴を持たない直接静解析の結果メタデータも、外力－内力－ばね力で釣合い残差を作る。
並進ばねを持つ解析で残差が0へ収束することを専用回帰試験へ固定した。
通常の構造モデルは`geometry_span`を使う。
座標spanを持たない単一節点の低水準合成モデルだけは互換用の1を使い、
`point_model_fallback`として明示する。このfallbackを単位不変性の保証対象にしない。

## 実測結果

すべての値はN–mの物理単位へ戻した値である。

| 問題 | N–m | N–mm | kN–m |
|---|---:|---:|---:|
| 曲げ荷重制御の反復数 | 4 | 4 | 4 |
| 先端変位 m | 0.012 | 0.012 | 0.012 |
| 先端回転 rad | 0.012 | 0.012 | 0.012 |
| 中央曲率 1/m | 0.006 | 0.006 | 0.006 |
| 固定端反力 N·m | -18 | -18 | -18 |
| 変位制御の反復数／荷重倍率 | 4 / 2.5 | 4 / 2.5 | 4 / 2.5 |
| 強制変位 m／反力 N | 0.004 / -12 | 0.004 / -12 | 0.004 / -12 |
| 固有振動数 Hz | 5.68048350754 | 5.68048350756 | 5.68048350755 |

軸力の循環係数`[0.5, 1, 0, -0.5, -1, 0, 1]`と並進ばねを含む問題では、段階別反復数が
3単位系すべて`[3, 4, 4, 3, 4, 4, 3]`だった。各段階の変位・反力も物理単位へ戻して
相対`2e-11`、絶対`2e-12`以内で一致した。

## 再現方法

```powershell
uv run --locked --extra dev pytest tests/validation/test_unit_invariance.py -q
uv run --locked --extra dev python -m tools.validation.unit_invariance --output tmp/product-quality-pq10-measurements.json
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-pq10-current.xml
uv run --locked --extra dev python -m tools.validation.check_wiki
```

- 単位不変性の固有試験は5件成功、関連するsolver・診断を含む集中試験は104件成功した。
- 全件試験は2,023件成功、failure 0、error 0、skip 0、終了コード0だった。
- JUnit実測は1,222.748秒、pytest表示は1,222.76秒（20分22秒）だった。
- Python 3.13.11 / pytest 9.0.2 / NumPy 2.4.1 / SciPy 1.17.0を使用した。
- Wiki検査は13ページ、Python 21ブロック、JSON 17ブロック、実行例18件がすべて成功した。

## 保証しない範囲

- 単位宣言から入力値を自動換算する機能はない。利用者がモデル全体を一貫換算する。
- geometry span以外の代表長さ、複数の極端に異なる長さ尺度を持つ構造に最適な重みは未評価である。
- 座標spanがない単一節点の低水準合成モデルは単位不変性の保証対象外である。
- 弧長法、分岐・座屈、大変形、接触、速度依存材料は対象外である。
- 浮動小数点演算順の差までbit一致するとは保証しない。成功判定、反復数、物理応答を比較する。
