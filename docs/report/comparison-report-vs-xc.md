# JR総研剛性低減RC型 vs xc(OpenSees) 詳細比較レポート

**作成日**: 2026-02-01
**目的**: バー要素の材料非線形機能において、xcと結果が一致しない原因の特定

---

## 1. エグゼクティブサマリー

### 結論

**最大の原因は「再載荷経路の起点」の違い**である。

| 実装 | 再載荷経路 |
|------|------------|
| **JR総研（現在）** | **原点(0,0)を通る直線** |
| **xc/OpenSees** | **P=0となった点（TrotNu）から**最大経験点への直線 |

この根本的な差異により、再載荷時の荷重が過大評価され、履歴ループの形状とエネルギー吸収量が異なる結果となっている。

### 重要度順の差異一覧

| 順位 | 差異 | 影響度 | 修正難易度 |
|------|------|--------|------------|
| **1** | 再載荷経路が原点を通る | **致命的** | 中 |
| **2** | P=0点を明示的に記録していない | **致命的** | 低 |
| **3** | 剛性低減の基準剛性・基準変位 | 大 | 低 |
| 4 | ピンチング機能なし | 大 | 中 |
| 5 | ダメージモデルなし | 中 | 中 |

---

## 2. 根本的ロジック差異の詳細

### 2.1 再載荷経路の起点（最重要）

#### 問題の本質

JR総研実装では、再載荷時に`P = K * delta`として**原点を通る直線**を描いている。
しかし、xcでは**P=0となった点（TrotNu）から**最大経験点への直線を描いている。

#### JR総研実装のコード

```python
# jr_stiffness_reduction.py:486-495
elif current_branch == 'reloading':
    target_delta, target_P = self.get_target_point(delta, state)

    # 原点から目標点への直線
    if abs(target_delta) > self.EPSILON:
        K = target_P / target_delta      # ⚠️ 原点基準の傾き
    else:
        K = p.K_1_pos if delta >= 0 else p.K_1_neg
    P = K * delta                        # ⚠️ 原点(0,0)を通る！
```

#### xc/OpenSeesのコード

```cpp
// HystereticMaterial.cpp:218-230 (除荷開始時)
if(TloadIndicator == 2) {
    TloadIndicator = 1;
    if(converged.getStress() <= 0.0) {
        // P=0となるひずみを計算して記録
        TrotNu = converged.getStrain() - converged.getStress()/(E1n*kn);
        ...
    }
}

// HystereticMaterial.cpp:261-276 (再載荷時)
else if(trial.getStrain() >= TrotNu && trial.getStrain() < rotch) {
    if(trial.getStrain() <= rotrel) {
        trial.Stress()= 0.0;
        trial.Tangent()= E1p*1.0e-9;
    }
    else {
        // rotrel（P=0点）からの経路
        trial.Tangent()= maxmom*pinchY/(rotch-rotrel);
        tmpmo2 = (trial.getStrain()-rotrel)*trial.getTangent();  // ⭕ rotrel基準
        ...
    }
}
```

#### 図解

```
P (荷重)
^
|         ●max_pos (最大経験点)
|        /|
|       / |
|      /  |
|     /   | xc: TrotNu(P=0点)から直線
|    /    |
|   /    /
|  / JR:/
| /   ↙
|/___●TrotNu_________________> δ (変位)
O    ↑
     ここから再載荷すべき
     （JRは原点から始めている）
```

#### 影響

- **荷重の過大評価**: JR実装では同じ変位でも荷重が大きくなる
- **履歴ループ形状の違い**: ループの「くびれ」が表現できない
- **エネルギー吸収量の違い**: JR実装の方がエネルギー吸収が大きくなる傾向

---

### 2.2 P=0点の記録欠如

#### 問題

JR総研実装では、P=0を通過したことは検出しているが、**P=0となる変位を記録していない**。

#### JR総研実装

```python
# jr_stiffness_reduction.py:457-464
if current_branch == 'unloading':
    # P=0通過の検出（符号変化のみ）
    if (state.previous_P > self.EPSILON and delta < 0) or \
       (state.previous_P < -self.EPSILON and delta > 0):
        branch_info['crossed_zero'] = True    # フラグのみ
        branch_info['branch'] = 'reloading'
        # ⚠️ P=0となる変位を計算・保存していない！
```

#### xc/OpenSeesの正しい実装

```cpp
// HystereticMaterial.cpp:221
// 除荷開始時にP=0となるひずみを計算
TrotNu = converged.getStrain() - converged.getStress()/(E1n*kn);
```

この`TrotNu`は後で再載荷経路の起点として使用される。

---

### 2.3 内部ループ再載荷も同じ問題

#### JR総研実装

```python
# jr_stiffness_reduction.py:497-508
elif current_branch == 'inner_reloading':
    if len(state.reversal_stack) > 0:
        target_delta, target_P = state.reversal_stack[-1]
    else:
        target_delta, target_P = self.get_target_point(delta, state)

    if abs(target_delta) > self.EPSILON:
        K = target_P / target_delta
    else:
        K = p.K_1_pos if delta >= 0 else p.K_1_neg
    P = K * delta    # ⚠️ ここも原点を通る！
```

内部ループの再載荷でも、**P=0を通過した点から**反転点への直線であるべきところ、原点を通る直線を描いている。

---

## 3. 剛性低減式の差異

### 3.1 基準剛性と基準変位

| 項目 | xc/OpenSees | JR総研 |
|------|-------------|--------|
| **基準剛性** | 常に`E1p`（初期剛性） | 領域により`K1`→`K2`に変化 |
| **基準変位** | 常に`rot1p`（第1折れ点） | 領域により`δ1`→`δ2`に変化 |

### 3.2 剛性低減式の比較

#### xc/OpenSees

```cpp
// HystereticMaterial.cpp:215-216
double kp = pow(CrotMax/rot1p, beta);
kp = (kp < 1.0) ? 1.0 : 1.0/kp;

// 除荷剛性 = E1p * kp = E1p * (rot1p/CrotMax)^beta
Ttangent = E1p * kp;
```

#### JR総研

```python
# jr_stiffness_reduction.py:291-296
if delta_max <= delta_2:
    # ひび割れ域
    Kd = K_1 * (delta_max / delta_1) ** (-beta)
else:
    # 降伏域以降
    Kd = K_2 * (delta_max / delta_2) ** (-beta)
```

### 3.3 数値例での比較

パラメータ: δ1=0.003, δ2=0.015, P1=100kN, P2=500kN, β=0.4

| δmax | xc除荷剛性 | JR除荷剛性 | 差異 |
|------|-----------|-----------|------|
| 0.006 (ひび割れ域) | E1p×(0.003/0.006)^0.4 = 0.758×E1p | K1×(0.006/0.003)^(-0.4) = 0.758×K1 | 同等 |
| 0.030 (降伏域) | E1p×(0.003/0.030)^0.4 = 0.398×E1p | K2×(0.030/0.015)^(-0.4) = 0.758×K2 | **大きく異なる** |

降伏域以降では、xcは常にE1pを基準とするため除荷剛性が大きく、JRはK2を基準とするため除荷剛性が小さくなる。

---

## 4. その他の差異

### 4.1 ピンチング効果

| 実装 | 状態 |
|------|------|
| xc/OpenSees | `pinchX`, `pinchY` パラメータで再載荷経路を制御 |
| JR総研 | **なし** |

### 4.2 ダメージモデル

| 実装 | 状態 |
|------|------|
| xc/OpenSees | `damfc1`（変形）, `damfc2`（エネルギー）でダメージ考慮 |
| JR総研 | **なし** |

---

## 5. 修正方針

### 5.1 最優先修正事項

#### A. P=0点の記録

```python
# HysteresisStateに追加
delta_zero_pos: float = 0.0  # 正側からの除荷でP=0となる変位
delta_zero_neg: float = 0.0  # 負側からの除荷でP=0となる変位
```

#### B. P=0点の計算（除荷開始時）

```python
# 除荷開始時にP=0となる変位を計算
Kd = self.get_reduced_stiffness(state, state.loading_direction)
delta_zero = state.reversal_delta - state.reversal_P / Kd
branch_info['delta_zero'] = delta_zero
```

#### C. 再載荷経路の修正

```python
elif current_branch == 'reloading':
    target_delta, target_P = self.get_target_point(delta, state)
    delta_zero = state.delta_zero_pos if delta >= 0 else state.delta_zero_neg

    # P=0点から目標点への直線
    if abs(target_delta - delta_zero) > self.EPSILON:
        K = target_P / (target_delta - delta_zero)
    else:
        K = p.K_1_pos if delta >= 0 else p.K_1_neg

    P = K * (delta - delta_zero)  # ⭕ delta_zeroを起点
```

### 5.2 OpenSees互換モード（オプション）

剛性低減式をxc/OpenSees互換にするオプションを追加：

```python
def get_reduced_stiffness_opensees_compatible(self, state, direction):
    """OpenSees互換の剛性低減計算"""
    if direction > 0:
        delta_max = state.delta_max_pos
        delta_1 = self.params.delta_1_pos
        E1 = self.params.K_1_pos
    else:
        delta_max = state.delta_max_neg
        delta_1 = self.params.delta_1_neg
        E1 = self.params.K_1_neg

    if delta_max <= delta_1:
        return E1

    # 常にE1（初期剛性）とdelta_1（第1折れ点）を基準
    kp = (delta_1 / delta_max) ** self.params.beta
    return E1 * kp
```

---

## 6. 検証方法

### 6.1 単純な繰返し載荷テスト

1. 正方向にδ=0.02まで載荷（降伏域）
2. 除荷してP=0を通過
3. 負方向にδ=-0.01まで載荷
4. 再除荷してP=0を通過
5. 正方向に再載荷

各ステップで、xcの結果と比較する。

### 6.2 確認ポイント

- [ ] P=0を通過する変位（δ_zero）が正しく計算されているか
- [ ] 再載荷時の傾きがδ_zeroを起点として計算されているか
- [ ] 履歴ループの形状がxcと一致するか
- [ ] エネルギー吸収量がxcと一致するか

---

## 7. 参照ファイル

| ファイル | 説明 |
|----------|------|
| `src/fem/nonlinear/hysteresis/jr_stiffness_reduction.py` | JR総研モデル実装 |
| `docs/xc/src/material/uniaxial/HystereticMaterial.cpp` | xc履歴モデル実装 |
| `docs/plans/nonliner-bar.md` | JR総研モデル設計書 |

---

## 8. 結論

結果が一致しない**根本的な原因**は、再載荷経路が**原点を通る直線**として実装されていることである。

xcでは**P=0となった点（TrotNu）を明示的に記録**し、再載荷時はその点から最大経験点への直線を描く。この差異を修正することで、xcとの結果一致が期待できる。

剛性低減式の違い（基準剛性・基準変位）も影響するが、再載荷経路の問題に比べれば影響は限定的である。ピンチング・ダメージは機能として存在しないため、xc側でこれらを無効（pinchX=0, pinchY=0, damfc1=0, damfc2=0）にして比較する必要がある。
