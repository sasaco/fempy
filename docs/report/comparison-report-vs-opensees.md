# OpenSees vs JR総研モデル 比較検証レポート

**作成日**: 2026-02-01
**目的**: バー要素の材料非線形機能において、市販ソフト（OpenSees）と結果が合わない原因の特定

---

## 1. 概要

FrameWeb3に実装されたJR総研剛性低減RC型モデルと、OpenSeesのHystereticMaterialを比較し、ロジックの差異を分析した。

### 比較対象ファイル

| 実装 | ファイル |
|------|----------|
| FrameWeb3 (JR総研) | `src/fem/nonlinear/hysteresis/jr_stiffness_reduction.py` |
| OpenSees | `docs/OpenSees/SRC/material/uniaxial/HystereticMaterial.cpp` |

---

## 2. 主要な差異

### 2.1 剛性低減式（最重要）

これが結果の差異の**最大の原因**と考えられる。

#### OpenSees HystereticMaterial

```cpp
// HystereticMaterial.cpp:278-281
double kp = pow(CrotMax/rot1p, beta);
kp = (kp < 1.0) ? 1.0 : 1.0/kp;

// 除荷剛性の計算
Ttangent = Eup * kp;
```

ここで:
- `CrotMax`: 最大経験変位
- `rot1p`: 第1折れ点変位（ひび割れ点）
- `Eup`: スケルトンカーブの**最大勾配** (`max(E1p, E2p, E3p)`)
- `kp`: 低減係数 = `(rot1p/CrotMax)^beta` = `(CrotMax/rot1p)^(-beta)`

**特徴**:
- 常に**第1折れ点(rot1p)**を基準として低減係数を計算
- 常に**スケルトンカーブの最大勾配(Eup)**を基準剛性として使用
- 領域（ひび割れ域/降伏域）による区別なし

#### JR総研モデル（現在の実装）

```python
# jr_stiffness_reduction.py:290-296
if delta_max <= delta_2:
    # ひび割れ域: 式(7.11.1)
    Kd = K_1 * (delta_max / delta_1) ** (-beta)
else:
    # 降伏域以降: 式(7.11.2), (7.11.3)
    Kd = K_2 * (delta_max / delta_2) ** (-beta)
```

**特徴**:
- **領域によって基準剛性が変わる**（K1 → K2）
- **領域によって基準変位が変わる**（delta_1 → delta_2）
- 理論マニュアル7.11節に忠実な実装

#### 比較表

| 項目 | OpenSees | JR総研モデル |
|------|----------|-------------|
| 基準剛性 | 常に `Eup` (最大勾配) | 領域による (`K1` or `K2`) |
| 基準変位 | 常に `rot1` (第1折れ点) | 領域による (`δ1` or `δ2`) |
| 計算式 | `Eup × (rot1/rotMax)^β` | `K1 × (δmax/δ1)^(-β)` または `K2 × (δmax/δ2)^(-β)` |

---

### 2.2 ピンチング効果

#### OpenSees

ピンチング（pinchX, pinchY）パラメータにより、再載荷経路を制御:

```cpp
// HystereticMaterial.cpp:315-318
double rotmp2 = TrotMax - (1.0-pinchY)*maxmom/(Eup*kp);
double rotch = rotrel + (rotmp2-rotrel)*pinchX;
```

- `pinchX`: 変形方向のピンチング（0〜1）
- `pinchY`: 荷重方向のピンチング（0〜1）
- 再載荷時に「くびれ」を表現可能

#### JR総研モデル

ピンチング効果なし。P=0通過後は直接目標点（最大経験点または折れ点）を指向。

```python
# jr_stiffness_reduction.py:488-495
target_delta, target_P = self.get_target_point(delta, state)
K = target_P / target_delta
P = K * delta
```

---

### 2.3 除荷終点（P=0となる点）の計算方法

#### OpenSees

除荷開始時にP=0となる変位を予め計算:

```cpp
// HystereticMaterial.cpp:286
TrotNu = Cstrain - Cstress/(Eun*kn);
```

これにより、除荷経路の終点が明確に定義される。

#### JR総研モデル

反転点からの増分で計算:

```python
# jr_stiffness_reduction.py:481-484
Kd = self.get_reduced_stiffness(state, state.loading_direction)
P = reversal_P + Kd * (delta - reversal_delta)
```

P=0の通過は、前回の符号と現在の変位の符号で判定。

---

### 2.4 ダメージモデル

#### OpenSees

変形・エネルギーに基づくダメージを考慮:

```cpp
// HystereticMaterial.cpp:288-294
if (CrotMax > rot1p) {
    damfc = damfc2*energy/energyA;
    damfc += damfc1*(CrotMax-rot1p)/rot1p;
}
TrotMax = CrotMax*(1.0+damfc);  // 最大変位を修正
```

- `damfc1`: 変形に基づくダメージ係数
- `damfc2`: エネルギーに基づくダメージ係数
- ダメージにより強度低下を表現

#### JR総研モデル

ダメージ考慮なし。

---

### 2.5 Eup（除荷基準剛性）の定義

#### OpenSees

```cpp
// HystereticMaterial.cpp:856-862
Eup = E1p;
if (E2p > Eup) Eup = E2p;
if (E3p > Eup) Eup = E3p;
```

スケルトンカーブの3つの勾配のうち**最大値**を使用。

#### JR総研モデル

領域に応じて `K_1` または `K_2` を使用。

---

## 3. 差異が結果に与える影響

### 3.1 剛性低減式の影響（大）

降伏後（δmax > δ2）の場合:

| 実装 | 除荷剛性 |
|------|----------|
| OpenSees | `Eup × (rot1/rotMax)^β` |
| JR総研 | `K2 × (δ2/δmax)^β` |

典型的なRCでは `Eup > K2` かつ `rot1 < δ2` なので、**OpenSeesの方が除荷剛性が大きくなる傾向**がある。

### 3.2 ピンチングの影響（大）

OpenSeesでピンチングが有効（pinchX, pinchY > 0）な場合、再載荷経路が「くびれた」形状になり、エネルギー吸収量が減少する。JR総研モデルにはこの機能がないため、エネルギー吸収量が異なる可能性がある。

### 3.3 ダメージの影響（中）

OpenSeesでダメージパラメータが有効（damfc1, damfc2 > 0）な場合、繰り返し載荷により強度が低下する。JR総研モデルでは強度低下がないため、繰り返し載荷で差が蓄積する。

---

## 4. 結論と推奨対応

### 4.1 結果が合わない主な原因

1. **剛性低減式の違い**: JR総研モデルは領域別に基準を変えるが、OpenSeesは常に第1折れ点基準
2. **ピンチング効果の有無**: OpenSeesにはあるが、JR総研モデルにはない
3. **ダメージモデルの有無**: OpenSeesにはあるが、JR総研モデルにはない

### 4.2 推奨対応

#### オプション A: OpenSees互換モードの追加

JR総研モデルにOpenSees互換の剛性低減式を追加:

```python
def get_reduced_stiffness_opensees_compatible(self, state, direction):
    """OpenSees互換の剛性低減計算"""
    if direction > 0:
        delta_max = state.delta_max_pos
        delta_1 = self.params.delta_1_pos
        Eup = max(self.params.K_1_pos, self.params.K_2_pos, self.params.K_3_pos)
    else:
        delta_max = state.delta_max_neg
        delta_1 = self.params.delta_1_neg
        Eup = max(self.params.K_1_neg, self.params.K_2_neg, self.params.K_3_neg)

    if delta_max <= delta_1:
        return Eup

    kp = (delta_1 / delta_max) ** self.params.beta
    return Eup * kp
```

#### オプション B: ピンチング機能の追加

`pinchX`, `pinchY` パラメータを追加し、再載荷経路を制御可能にする。

#### オプション C: ダメージモデルの追加

`damfc1`, `damfc2` パラメータを追加し、繰り返し載荷による強度低下を考慮。

### 4.3 検証方法

1. 同じパラメータでOpenSeesモデルを作成
2. 単調載荷・繰り返し載荷の結果を比較
3. 各オプションを適用した場合の結果を比較

---

## 5. 付録：OpenSees HystereticMaterial パラメータ

```
uniaxialMaterial Hysteretic $tag
    $mom1p $rot1p $mom2p $rot2p $mom3p $rot3p
    $mom1n $rot1n $mom2n $rot2n $mom3n $rot3n
    $pinchX $pinchY $damfc1 $damfc2 <$beta>
```

| パラメータ | 説明 | JR総研対応 |
|-----------|------|-----------|
| mom1p/rot1p | 正側第1折れ点 | P_1_pos/delta_1_pos |
| mom2p/rot2p | 正側第2折れ点 | P_2_pos/delta_2_pos |
| mom3p/rot3p | 正側第3折れ点 | P_3_pos/delta_3_pos |
| pinchX | 変形ピンチング | **なし** |
| pinchY | 荷重ピンチング | **なし** |
| damfc1 | 変形ダメージ | **なし** |
| damfc2 | エネルギーダメージ | **なし** |
| beta | 剛性低減指数 | beta |

---

## 6. 参考文献

1. OpenSees HystereticMaterial ソースコード
2. JR総研理論マニュアル 7.11節「JR総研剛性低減RC型」
