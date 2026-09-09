# 影響線・影響面を用いた線荷重／面荷重の理論

## 1. この文書の目的

前身の`FrameWeb`にある`inf_panel`、`line`、`load_inf`は、任意位置の荷重を直接FEM要素へ
載荷する機能ではない。載荷面上の節点に単位荷重を一つずつ与えて影響値を求め、指定された
線荷重または面荷重との積を積分する、線形重ね合わせの機能である。

本書では次を区別して説明する。

- 影響線・影響面による載荷の数学的な意味
- 前身`FrameWeb`が採用していた離散化方法
- 前身実装に含まれる仮定と数値上の制約
- `FrameWeb3`へ導入するときに採用すべき定式化

> 本書が扱うのは、線形静解析における**既知の配置**の点荷重・線荷重・面荷重を応答へ
> 写像する理論と、旧`FrameWeb`機能の移植方針である。道路橋示方書、Eurocode、AASHTO等の
> 設計活荷重モデル、車両移動、最不利載荷および最大・最小包絡の生成は別仕様とする。

ここでいう「面荷重」は、シェル要素に直接与える面圧`pressure`とは異なる。構造物上に定義した
載荷領域を移動荷重の候補面とみなし、その領域上の荷重によって生じる構造応答を影響値から
合成する機能を指す。

## 2. 入力項目の意味

| 項目 | 意味 |
|---|---|
| `inf_panel` | 単位荷重を順番に載荷する節点群。影響面の標本点を定義する。 |
| `line` | 荷重経路または載荷帯の境界を表す折れ線。前身では全体XY座標で定義する。 |
| `load_inf` | 使用するラインと、その始点・終点における荷重強度を定義する。 |
| `L1` | 1本目のラインID。必須。 |
| `L2` | 2本目のラインID。存在すれば2本のライン間を面荷重として扱う。 |
| `P11`, `P12` | `L1`の始点・終点における荷重強度。 |
| `P21`, `P22` | `L2`の始点・終点における荷重強度。 |

`load_inf`に`L1`だけがあれば線荷重、`L1`と`L2`があれば2本のラインに挟まれた面荷重となる。
複数の`load_inf`がある場合、前身実装はそれぞれの応答を加算する。

入力例を次に示す。

```json
{
  "inf_panel": {
    "1": {
      "nodes": ["1", "2", "3", "4"]
    }
  },
  "line": {
    "1": {
      "position": [
        {"x": 0.0, "y": 1.0},
        {"x": 10.0, "y": 1.0}
      ]
    },
    "2": {
      "position": [
        {"x": 0.0, "y": 3.0},
        {"x": 10.0, "y": 3.0}
      ]
    }
  },
  "load": {
    "1": {
      "inf_panel": 1,
      "load_inf": [
        {
          "L1": "1",
          "P11": 10.0,
          "P12": 20.0,
          "L2": "2",
          "P21": 30.0,
          "P22": 40.0
        }
      ]
    }
  }
}
```

## 3. 影響値の基本式

拘束処理後の正則な線形静解析の剛性方程式を

\[
\mathbf K_{ff}\mathbf u_f=\mathbf f_f
\]

とする。変位だけでなく反力や部材端力も含めて扱うため、求めたい一つの応答を一般の
アフィン汎関数

\[
r=\mathbf a^\mathsf T\mathbf u_f+\mathbf b^\mathsf T\mathbf f+r_c
\]

で表す。ここで\(\mathbf f\)は節点荷重だけでなく、要素分布荷重など応答回収に必要な荷重表現を
含む。第1項は変位を介する成分、第2項は荷重の直接項、\(r_c\)は対象とする荷重増分に依存しない
項である。例えば拘束自由度の反力には

\[
\mathbf R_c
=\mathbf K_{cf}\mathbf u_f+\mathbf K_{cc}\mathbf u_c-\mathbf f_c
\]

のように拘束自由度へ作用する荷重の直接項があり、分布荷重を受ける要素の端力にも
固定端力または要素等価荷重の項がある。したがって、反力・部材端力一般を
\(\mathbf c^\mathsf T\mathbf u\)だけで表してはならない。

載荷面上の位置\(\boldsymbol x\)に、所定方向の単位集中荷重ベクトル
\(\mathbf e(\boldsymbol x)\)を増分として与えたときの影響値を

\[
\eta(\boldsymbol x)
=\mathbf a^\mathsf T\mathbf K_{ff}^{-1}\mathbf e_f(\boldsymbol x)
+\mathbf b^\mathsf T\mathbf e(\boldsymbol x)
\]

と定義する。荷重の直接項がない変位応答などでは、これは従来の
\(\mathbf c^\mathsf T\mathbf K^{-1}\mathbf e\)に簡約できる。載荷位置が線上を動く場合は
影響線、面内を動く場合は影響面となる。[^1][^2][^3]

点荷重を\(P_j\)、線荷重強度を\(q(\boldsymbol x)\)、面荷重強度を
\(p(\boldsymbol x)\)とすると、既知の一つの荷重配置による応答は

\[
r=r_0
+\sum_jP_j\eta(\boldsymbol x_j)
+\int_\Gamma\eta(\boldsymbol x)q(\boldsymbol x)\,\mathrm ds
+\int_\Omega\eta(\boldsymbol x)p(\boldsymbol x)\,\mathrm dA
\]

となる。旧`load_inf`が直接表現するのは線荷重と面荷重であり、点荷重項は道路橋の車輪荷重
などを含む一般形を示すために加えている。複数の`load_inf`は対応する積分項を加算する。

これらの式は、剛性、支持条件、応答定義が荷重増分に対して変わらない線形性に基づく。
材料非線形、幾何学的非線形、接触、履歴依存などにはそのまま適用できない。

## 4. 前身FrameWebの計算手順

### 4.1 単位荷重ケースの生成

対象荷重ケースが参照する`inf_panel`の全節点について、前身実装は内部的に

```text
load_inf_norm_<元の荷重ケースID>_<節点ID>
```

という荷重ケースを生成する。各ケースでは対象節点に全体Z方向の単位荷重`tz = +1.0`だけを
与える。元ケースに含まれる通常の節点荷重・部材荷重は、この単位荷重ケースには重ねない。
支持、材料、結合条件などは元ケースと同じものを使う。

したがって、`inf_panel`に\(n\)節点があれば、原則として\(n\)回の線形解析が追加される。
各解析から次の影響値の標本を収集する。

- 全節点の変位
- 支持点反力
- 全梁要素の各評価位置における断面力

### 4.2 線上の荷重強度

一本の`line`を構成する点を\(\boldsymbol x_0,\ldots,\boldsymbol x_m\)とする。折れ線に沿った
始点からの累積距離を

\[
s_0=0,\qquad
s_j=\sum_{i=1}^{j}\lVert\boldsymbol x_i-\boldsymbol x_{i-1}\rVert
\]

とする。始点強度を\(P_1\)、終点強度を\(P_2\)とすると、前身実装は各折れ点の強度を

\[
p_j=P_1+(P_2-P_1)\frac{s_j}{s_m}
\]

としている。座標成分ごとの補間ではなく、折れ線に沿った距離に対する線形補間である。

### 4.3 パネル節点からラインへの影響値補間

単位荷重解析で得られるのは、`inf_panel`節点位置における離散的な影響値である。前身実装は
各ライン点の近傍節点を探索し、節点間の直線とライン各区間を含む鉛直面との交点に値を
線形補間した後、それらの交点値からRBF補間を作り、ライン点の影響値を評価している。

これはFEM形状関数に基づく補間ではなく、前身実装固有の幾何学的な近似である。交点が
ライン区間内または節点間にあることを厳密には確認しておらず、パネル節点の配置によっては
外挿になる。

### 4.4 線荷重の数値積分

ライン点で評価した影響値を\(\eta_j\)、荷重強度を\(p_j\)、区間長を
\(\Delta s_j=\lVert\boldsymbol x_j-\boldsymbol x_{j-1}\rVert\)とすると、前身実装は

\[
r_\Gamma\approx
\sum_{j=1}^{m}
\frac{\eta_{j-1}p_{j-1}+\eta_jp_j}{2}\Delta s_j
\]

という台形則を使う。

これは積\(\eta p\)の端点値に対する台形則である。\(\eta\)と\(p\)がそれぞれ区間内で線形なら
積は2次式になるため、一般には厳密積分ではない。どちらか一方が一定の場合には線形関数の
積分となり、台形則で厳密になる。

例えば長さ10の直線で、影響値が2から4、荷重強度が3から5へそれぞれ線形に変化するとする。
前身方式は

\[
\frac{2\times3+4\times5}{2}\times10=130
\]

を返すが、2次式を厳密に積分した値は

\[
\int_0^{10}(2+0.2s)(3+0.2s)\,\mathrm ds
=123.\overline{3}
\]

となる。この差はFEM解析誤差ではなく積分公式の離散化誤差である。

### 4.5 2本のライン間の面荷重

`L1`と`L2`が指定された場合、前身実装は次の手順を使う。

1. 各ライン上の荷重強度を始終点値から補間する。
2. 2本のラインで囲まれると判定した`inf_panel`節点を抽出する。
3. 2本のライン上の点から面内節点の荷重強度を2次元補間する。
4. ライン点と面内節点をDelaunay三角形分割する。
5. 各三角形で、3頂点の\(\eta p\)の平均に三角形面積を掛けて加算する。

三角形\(T\)の面積を\(A_T\)、頂点値を\((\eta_i,p_i)\)とすれば、使用している式は

\[
r_T\approx A_T\frac{\eta_1p_1+\eta_2p_2+\eta_3p_3}{3}
\]

である。この公式は\(\eta p\)が三角形内で1次式なら厳密だが、\(\eta\)と\(p\)が独立に
1次変化すると積は2次式になるため、一般には厳密ではない。

## 5. 単位・符号・座標系

FrameWeb3は単位を自動変換しないため、モデル全体で整合する単位系を使う。

| 量 | 次元 |
|---|---|
| ライン座標、パネル座標 | L |
| 単位集中荷重 | F。数値上は`+1.0` |
| 線荷重強度 | F/L |
| 面荷重強度 | F/L² |
| 変位の影響値 | L/F |
| 断面力の影響値 | 対象断面力の単位/F |

このとき線積分・面積分後の値は、通常の解析結果と同じ次元になる。

前身実装の座標と符号には次の固定条件がある。

- 載荷面は全体XY平面へ投影する。
- ラインは`x`、`y`だけで定義し、`z=0`として扱う。
- 単位荷重方向は全体`+Z`である。
- `P11`などの正負は、この`+Z`方向に対する倍率である。

したがって、傾斜面、鉛直面、曲面、任意方向荷重を前身方式のまま正しく扱うことはできない。

## 6. 成立条件

この方法が物理的に成立するには、少なくとも次の条件が必要である。

1. 解析が線形静解析である。
2. 全単位荷重ケースで剛性\(\mathbf K\)、支持条件、材料、結合条件が同一である。
3. 応答量が荷重に対して線形である。
4. 載荷領域と`inf_panel`の幾何学的対応が定義されている。
5. 荷重経路を覆うために十分な標本節点がある。
6. 荷重強度、長さ、解析モデルが同じ単位系を使う。

材料非線形解析では一般に

\[
r(\mathbf f_1+\mathbf f_2)\ne r(\mathbf f_1)+r(\mathbf f_2)
\]

であり、載荷順序や履歴にも依存する。このため`material_nonlinear`に対して単位荷重応答を
後から合成してはならない。固有値解析`modal`も静的な荷重応答ではないため対象外とする。

## 7. 前身実装をそのまま移植できない理由

### 7.1 補間APIの廃止

面荷重の強度補間には`scipy.interpolate.interp2d`を使っている。このAPIはSciPy 1.14で削除され、
FrameWeb3の現在の環境では`NotImplementedError`になる。別APIへ名前だけ置き換えるのではなく、
入力点が散在点か規則格子かを明示して補間方式を決める必要がある。[^6]

### 7.2 領域形状が一意に定まらない

`inf_panel`が保持するのは節点IDの集合だけであり、外周、穴、非凸境界、三角形接続を持たない。
点集合だけから面領域は一意に決まらない。前身の無制約Delaunay分割は点群の凸包を覆うため、
非凸な載荷帯では本来の領域外まで積分する可能性がある。非凸外周や穴を保持するには、制約辺と
穴を明示した制約付き三角形分割、または三角形生成後の領域クリッピングが必要である。[^7]

### 7.3 退化形状の検証不足

次の入力に対する明確な検証がない。

- 同一点の重複
- 長さゼロのライン区間
- 交差または自己交差するライン
- 2本のラインの向きや点数が一致しない場合
- 共線点だけからなる面
- パネル外へ出るライン
- 近傍節点間の直線と対象面が平行になる場合

### 7.4 数値正解を固定する試験がない

前身の`example_inf.py`は解析が例外なく終了したことしか確認せず、期待する変位、反力、断面力を
比較していない。また、実行結果を入力用`inf.json`へ上書きする。したがって前身のサンプルを
そのまま数値オラクルとして扱うことはできない。

### 7.5 RBF補間は力学的保存性を保証しない

前身のRBF補間は節点の影響値を滑らかにつなぐ経験的近似であり、一般には次を自動的に
保証しない。

- 分割の一（\(\sum_iN_i=1\)）
- 1次場の再現性
- 非負性
- 総力と一次モーメントの保存
- 標本点の凸包外での安定した外挿

一般モードでは既存シェル要素の形状関数、または接続を明示した三角形載荷メッシュの
重心座標を優先する。旧値の再現にRBFが必要なら互換モードに限定し、保存性試験とは別に
旧値回帰試験を設ける。RBFを標本範囲外へ外挿する場合は特に注意する。[^6]

## 8. FrameWeb3で採用すべき定式化

### 8.1 影響値積分と等価節点荷重

既知の一つの荷重配置を扱う線形問題では、影響値を応答ごとに積分する方法と、荷重を先に
等価節点荷重へ変換して解析する方法は、同じ補間関数と積分則を使い、応答回収時の荷重の
直接項も同じ表現で保持すれば代数的に等価である。

載荷面上の補間関数を\(N_i(\boldsymbol x)\)とし、位置\(\boldsymbol x\)の単位荷重を

\[
\mathbf e(\boldsymbol x)=\sum_iN_i(\boldsymbol x)\mathbf e_i
\]

と近似する。線荷重の等価節点荷重係数は

\[
w_i=\int_\Gamma N_i(\boldsymbol x)q(\boldsymbol x)\,\mathrm ds
\]

面荷重では

\[
w_i=\int_\Omega N_i(\boldsymbol x)p(\boldsymbol x)\,\mathrm dA
\]

である。等価節点荷重ベクトルを

\[
\mathbf f_\mathrm{eq}=\sum_iw_i\mathbf e_i
\]

とすれば、任意の線形応答について

\[
\Delta r
=\mathbf a^\mathsf T\mathbf K_{ff}^{-1}\mathbf f_{\mathrm{eq},f}
+d_\mathrm{eq}
=\sum_iw_i\eta_i
\]

が成立する。\(d_\mathrm{eq}\)は荷重の直接項であり、等価節点荷重ベクトルだけで十分に表現できる
場合は\(\mathbf b^\mathsf T\mathbf f_\mathrm{eq}\)である。部材分布荷重の固定端力などを別に扱う
ソルバーでは、要素レベルの荷重情報も保持して同じ直接項を回収する。組み立て後の節点荷重
ベクトルだけを残した場合、変位依存応答の等価性は成立しても、任意の反力・部材端力まで
自動的に等価になるとは限らない。

この方法には次の利点がある。

- 既知配置の全応答が必要な場合、出力項目ごとに個別の影響面を構築する必要がない。
- 既知配置ごとに、パネル節点数だけ解析を繰り返さず原則1回の解析で済む。
- 直接項を含む応答回収まで整合させれば、変位、反力、梁端力、シェル合力などに同じ荷重が反映される。
- 荷重ベクトルの総力・総モーメントを解析前に検査できる。

ただし、前身のRBF補間と完全に同じ数値を必要とする互換モードでは、まず単位荷重応答方式を
再現し、基準値を確定した後で等価節点荷重方式との一致を検証する。

単一方向の平面荷重に限らない一般の整合要素荷重は、自然座標
\(\boldsymbol\xi\)上で

\[
\mathbf f_e
=\int_{\hat\Omega_e}
\mathbf N_u^\mathsf T(\boldsymbol\xi)
\boldsymbol t(\boldsymbol\xi)
J_s(\boldsymbol\xi)\,\mathrm d\hat\Omega
\]

とする。\(\mathbf N_u\)は変位場に対応するベクトルまたは行列形状関数、
\(\boldsymbol t\)は表面力ベクトル、\(J_s\)は面の幾何Jacobianである。曲線上の線荷重には
同様の線積分と\(J_l=\lVert\boldsymbol x_{,\xi}\rVert\)を用いる。整合節点力は仮想仕事から
導かれる有限要素法の標準的な定式化である。[^4]
曲面では

\[
J_s=\left\lVert
\boldsymbol x_{,\xi}\times\boldsymbol x_{,\eta}
\right\rVert
\]

を用いる。載荷専用メッシュを構造メッシュと別に持つ場合は、総力と一次モーメントを保存する
構造自由度への写像も定義する。

### 8.2 推奨する幾何学表現

理想的には`inf_panel`を単なる節点集合ではなく、次のいずれかで定義する。

- 既存シェル要素の集合
- 三角形接続を持つ載荷専用メッシュ
- 外周と穴を明示した平面領域

各三角形ではFEM形状関数または重心座標を使い、ラインを三角形境界で分割してから線積分する。
面荷重は領域内の三角形だけを積分する。これにより、非凸領域、穴、パネル境界を明示的に扱える。

旧入力互換のため節点集合だけを許す場合は、「同一平面上の凸領域として扱う」など、推論する
領域の制限を入力仕様に明記しなければならない。

### 8.3 推奨する積分則

直線区間のaffine写像の下で、線形補間された影響値と線形分布荷重の積は2次式になる。
この限定下では、各区間に2点Gauss積分を用いると厳密に積分できる。

平面の直線辺三角形でも、1次形状関数と1次荷重分布の積は2次式になる。頂点平均だけでなく、
2次多項式を積分できる三角形求積則を使う。荷重が一定の場合でも、総力と作用位置から得られる
総モーメントを別途検査する。

ただし、曲線ライン、曲面、高次アイソパラメトリック要素、RBF等の非多項式補間、曲線状の
荷重境界では、この「厳密」は成立しない。Jacobianと補間次数に応じて求積次数を選び、次数を
上げたときの収束を確認する。また、求積公式が補間多項式を厳密に積分することと、FEMで得た
影響値が連続体の真値に一致することは別である。[^5][^12]

### 8.4 座標系と載荷方向

FrameWeb3では次を明示的な仕様とするのが望ましい。

- 互換モードでは全体XY面・全体Z方向を維持する。
- 一般モードでは載荷面の局所2次元基底と法線を定義する。
- 荷重方向は全体軸、面法線、または明示ベクトルから選択できるようにする。
- 平面からの節点のずれに許容値を設け、許容値を超えた入力を黙って投影しない。

局所基底と法線の指定だけでは曲面への一般化として不十分であり、曲面の面積Jacobian、
ベクトル形状関数、荷重方向の変化、および載荷メッシュから構造自由度への写像を併せて扱う。

### 8.5 移動荷重の最不利位置と包絡

既知の配置を等価節点荷重へ変換して1回解析することと、許容される車両・車線配置から
最大・最小応答を探すことは別問題である。荷重配置パラメータを\(\boldsymbol\theta\)、
許容配置集合を\(\mathcal A\)とすると、例えば最大応答は

\[
r_{\max}
=\max_{\boldsymbol\theta\in\mathcal A}
\left[
\sum_jP_j\eta\!\left(\boldsymbol x_j(\boldsymbol\theta)\right)
+\int_{\Omega(\boldsymbol\theta)}
\eta(\boldsymbol x)p(\boldsymbol x,\boldsymbol\theta)\,\mathrm dA
\right]
\]

と表せる。最小応答も同様に定義する。車軸間隔、車線境界、複数車線同時載荷、最小離隔、
正負の影響領域などを含む\(\mathcal A\)の定義と探索は、構造解析が線形でも独立した最適化問題である。

実装方針は用途に応じて選ぶ。

- 応答数が少なく候補配置が多い場合は、応答ごとの影響面または随伴解を作って配置を高速走査する。
- 候補配置が少なく各配置の全応答が必要な場合は、配置ごとに等価節点荷重を作って直接解析する。

車輪荷重は点荷重としても、有限の接地面へ作用する面圧としても表現できる。後者を採る場合は、
接地寸法、舗装・床版内の荷重分散、動的増幅または衝撃係数を荷重モデル側で別途定義する。[^11]

道路橋示方書のT荷重・L荷重、Eurocodeのtandem systemとUDL、AASHTOのtruck/tandemと
lane loadなどは、点・線・面荷重を生成して不利な配置を探索する上位の設計基準レイヤーである。
`inf_panel`、`line`、`load_inf`は、任意の既知分布を応答へ写像する低レベル機能として分離する。
[^8][^9][^10]

## 9. FrameWeb3での処理境界

現行の旧形式JSON読込は先頭荷重ケースを選択して一つの`FemModel`を構築する。一方、前身の
影響線処理は内部的に多数の荷重ケースを生成・解析・集約する。このため、機能は単一モデルの
入力変換に埋め込むのではなく、次の責務に分離する。

1. 入力検証：`inf_panel`、`line`、`load_inf`、単位、座標系を検証する。
2. 幾何処理：ラインまたは面と載荷メッシュの交差・積分点を作る。
3. 荷重変換：積分点荷重を整合等価節点荷重へ変換する。
4. 線形解析：通常荷重と等価節点荷重を同一荷重ケースとして解く。
5. 出力：`node_displacements`、`reaction_forces`、`element_stresses`、シェル結果を通常形式で返す。

旧形式互換の結果を返す必要がある場合だけ、最後に`disg`、`reac`、`fsec`への射影をAPI境界で行う。

## 10. 検証すべき不変条件

実装時には、前身サンプルの無例外実行だけでなく、次の数値試験を用意する。

| 試験 | 満たすべき条件 |
|---|---|
| ゼロ荷重 | 全`P`がゼロなら通常荷重だけの結果と一致する。 |
| 比例性 | 全`P`を\(a\)倍すると影響荷重分の全応答が\(a\)倍になる。 |
| 加法性 | 二つの`load_inf`の個別結果の和が同時載荷結果と一致する。 |
| ライン反転 | 点順序を反転し、始終点強度も交換すれば結果が変わらない。 |
| 一定影響値 | \(\eta=1\)なら積分値が荷重の総力と一致する。 |
| 合力 | 等価節点荷重の合計が線荷重・面荷重の積分値と一致する。 |
| モーメント | 等価節点荷重の全体モーメントが分布荷重の作用位置と一致する。 |
| メッシュ分割 | 同じ幾何・荷重を細分化しても収束し、不要な不連続が生じない。 |
| 線形解析 | 直接載荷できる単純梁・板で、直接解と影響値積分が一致する。 |
| 非対象解析 | `material_nonlinear`と`modal`は明確な入力エラーになる。 |
| 節点集中荷重 | 車輪位置が節点上なら直接節点荷重の結果と一致する。 |
| 荷重の直接項 | 拘束自由度または着目要素へ直接荷重が入っても反力・端力が一致する。 |
| 移動荷重包絡 | 単純梁の1点荷重による最大・最小応答と位置が閉形式解に一致する。 |
| 正負領域載荷 | UDLを影響線・影響面の正または負領域へ置いた包絡が解析解に一致する。 |
| 車輪接地面 | 接地面の細分化に対して床版内力が収束する。 |
| 非凸・穴あき領域 | 境界外および穴の三角形が積分対象に入らない。 |
| 曲面Jacobian | 一定面圧の総荷重が実表面積と面圧の積に一致する。 |
| 高次要素求積 | 求積次数を上げたとき応答が収束する。 |
| RBF互換 | 一定場・1次場再現性、外挿警告、旧値回帰を保存性試験と分けて確認する。 |
| 相反性 | 対称線形系で直接法と随伴法の影響値が一致する。 |
| 包絡探索 | 配置走査の細分化または連続最適化に対して最大・最小値が収束する。 |

特に、単純支持梁の集中荷重・等分布荷重、片持梁の線形分布荷重、長方形パネルの一定面荷重は、
独立した閉形式解を作りやすい。これらを最初の数値オラクルとし、解析結果から期待値を自動生成しない。

## 11. 要約

`inf_panel`、`line`、`load_inf`の本質は、載荷候補位置の単位荷重応答から影響面を構築し、
点荷重との積および分布荷重との線積分・面積分によって線形応答を合成することである。
反力・部材端力を一般に扱うときは、変位を介する項だけでなく荷重の直接項も含める。

前身実装はこの考え方を実装しているが、全体XY面・全体Z方向への固定、経験的なRBF補間、
無制約Delaunay分割、低次の積分則、廃止API、数値オラクル不足という制約がある。

FrameWeb3では、既知配置には理論的に等価な整合等価節点荷重を中心に実装し、幾何・補間・
積分・解析・出力を分離する。曲面・高次要素ではベクトル形状関数とJacobianを明示し、
求積収束と力・モーメント保存を検証する。前身互換が必要な範囲は独立した互換試験で固定する。

車両移動、設計基準ごとの荷重生成、最不利配置および包絡計算は、この低レベル荷重写像とは
別レイヤーで実装する。

## 12. 参考文献

[^1]: A. M. Memari and H. H. West, “[Computation of bridge design forces from influence surfaces](https://doi.org/10.1016/0045-7949(91)90006-8),” *Computers & Structures*, Vol. 38, Nos. 5–6, pp. 547–556, 1991.

[^2]: P. Lévêque and F. Bacchus, “[Utilisation du quadrangle « discrete shear » pour la génération des surfaces d’influence des efforts de plaque](https://doi.org/10.3166/remn.17.495-527),” *European Journal of Computational Mechanics*, Vol. 17, No. 4, pp. 495–527, 2008.

[^3]: Autodesk, “[Influence Surface Generation](https://help.autodesk.com/cloudhelp/ENU/ASBD-InProdEU/files/structure/data_entry/influence/inf_refined/ASBD_InProdEU_structure_data_entry_influence_inf_refined_Influence_Surface_Generation_html.html),” Autodesk Structural Bridge Design documentation.

[^4]: ETH Zürich, Institute of Structural Engineering, “[Method of Finite Elements I, Lecture 6](https://ethz.ch/content/dam/ethz/special-interest/baug/ibk/structural-mechanics-dam/education/femI/Lecture_6_ch.pdf),” pp. 30–32, consistent nodal force vector.

[^5]: D. A. Dunavant, “[High degree efficient symmetrical Gaussian quadrature rules for the triangle](https://doi.org/10.1002/nme.1620210612),” *International Journal for Numerical Methods in Engineering*, Vol. 21, pp. 1129–1148, 1985.

[^6]: SciPy, “[`interp2d` API reference](https://docs.scipy.org/doc/scipy/reference/generated/scipy.interpolate.interp2d.html),” “[SciPy 1.14.0 Release Notes](https://docs.scipy.org/doc/scipy/release/1.14.0-notes.html),” and “[Interpolation user guide](https://docs.scipy.org/doc/scipy/tutorial/interpolate.html).”

[^7]: J. R. Shewchuk, “[Triangle: Engineering a 2D Quality Mesh Generator and Delaunay Triangulator](https://people.eecs.berkeley.edu/~jrs/papers/triangle.pdf),” *Applied Computational Geometry*, LNCS 1148, pp. 203–222, 1996.

[^8]: European Commission Joint Research Centre, “[Bridge Design to Eurocodes — Worked Examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/Bridge_Design-Eurocodes-Worked_examples.pdf),” 2012.

[^9]: 国土交通省東北地方整備局, “[設計施工マニュアル［橋梁編］](https://www.thr.mlit.go.jp/bumon/b00097/k00910/h12-hp/R5dourokyou_R6.7ver.pdf),” 令和5年3月（令和6年7月版）; 国土交通省, “[「橋、高架の道路等の技術基準」（道路橋示方書）の改定について](https://www.mlit.go.jp/report/press/road01_hh_001980.html),” 令和7年8月22日.

[^10]: U.S. Federal Highway Administration, “[Load and Resistance Factor Design for Highway Bridge Superstructures — Reference Manual](https://www.fhwa.dot.gov/bridge/pubs/nhi15047.pdf)” and “[LRFD Steel Girder Superstructure Design Example — Live Load Effects](https://www.fhwa.dot.gov/bridge/lrfd/us_ds3.cfm).”

[^11]: 土木学会, “[道路橋床版の輪荷重直下の応力の算定について](https://www.jstage.jst.go.jp/article/jscej1969/1978/273/1978_273_15/_article/-char/ja/),” *土木学会論文報告集*, 第273号, pp. 15–23, 1978.

[^12]: A. Á. de A. Albuquerque, V. G. Haach, and R. R. Paccola, “[Dependency of modeling parameters for the construction of influence surfaces by the finite element method](https://doi.org/10.1007/s00366-017-0531-0),” *Engineering with Computers*, Vol. 34, pp. 143–154, 2018.
