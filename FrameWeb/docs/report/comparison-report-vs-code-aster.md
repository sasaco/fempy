# JR総研剛性低減RC型 vs code-aster 履歴アルゴリズム比較レポート

**作成日**: 2026-02-01
**目的**: 自作FEM実装とcode-asterの履歴モデルのロジック差異を特定し、結果が一致しない原因を解明する

---

## 1. エグゼクティブサマリー

### 重要な発見

**code-asterにはJR総研剛性低減RC型の直接対応モデルが存在しない。**

code-asterの非線形履歴モデルを調査した結果、JR総研剛性低減RC型（トリリニア骨格曲線 + 剛性低減履歴則）に直接対応するモデルは見当たらなかった。

| code-asterモデル | 用途 | JR総研との類似度 |
|------------------|------|-----------------|
| PINTO_MENEGOTTO | 鉄筋鋼材用（連続曲線） | 低 |
| DIS_BILI_ELAS | 離散要素バイリニア | 低 |
| VMIS_ISOT_LINE | 等方硬化 | 低 |

**結果が一致しない場合、参照実装として使用しているのはxc/OpenSeesのHystereticMaterialである可能性が高い。**

### 差異一覧（重要度順）

| 順位 | 差異 | 影響度 | 関連ファイル |
|------|------|--------|-------------|
| **1** | 再載荷経路の起点（原点 vs P=0点） | **致命的** | jr_stiffness_reduction.py:486-495 |
| **2** | P=0点の明示的記録の欠如 | **致命的** | jr_stiffness_reduction.py:457-464 |
| **3** | 剛性低減の基準剛性・基準変位 | 大 | jr_stiffness_reduction.py:290-296 |
| 4 | ピンチング機能の欠如 | 大 | - |
| 5 | ダメージモデルの欠如 | 中 | - |

---

## 2. code-aster関連モデルの分析

### 2.1 PINTO_MENEGOTTO（nm1dpm.F90）

**概要**: 鉄筋鋼材用の非線形履歴モデル

**実装位置**: `docs/code-aster/SRC/aster-14.6.0/aster-14.6.0/bibfor/nonlinear/nm1dpm.F90`

#### 主要なアルゴリズム

```fortran
! nm1dpm.F90:281-336
! サイクル時の履歴計算
if (cycl.gt.0.5d0) then
    ! 反転時に反転点を記録
    if (chgdir .lt. 0.d0) then
        epsrm=epsrp        ! 前の反転点
        epsrp=epsm         ! 新しい反転点（ひずみ）
        sigrp=sigm         ! 新しい反転点（応力）
    endif

    ! 目標点の計算
    if (depmec .ge. 0.d0) then
        eps0=-(sigrp-sy+eh*epsy-e*epsrp)/(e-eh)
        sig0=eh*(eps0-epsy)+sy
    else
        eps0=-(sigrp+sy-eh*epsy-e*epsrp)/(e-eh)
        sig0=eh*(eps0+epsy)-sy
    endif

    ! 正規化変位と剛性低減係数
    epsetp= (epsmec-epsrp)/(eps0-epsrp)
    xi = (epsrm-eps0)/(eps0-epsrp)
    r = r0-a1*xi/(a2+xi)

    ! Menegotto-Pinto曲線
    sigetp=b0*epsetp+((1-b0)/(1+(epsetp)**r)**(1/r))*epsetp
    sigp=(sig0-sigrp)* sigetp+sigrp
endif
```

**JR総研との主要な違い**:

| 項目 | PINTO_MENEGOTTO | JR総研 |
|------|-----------------|--------|
| 骨格曲線 | 連続曲線（Menegotto-Pinto式） | トリリニア（4折線） |
| 剛性低減 | R係数による曲率変化 | べき乗則（式7.11.1-7.11.3） |
| 反転点管理 | 単一の反転点（epsrp, sigrp） | スタック管理（複数反転点） |
| 目標点 | eps0（計算で求める） | 最大経験点または折れ点 |

### 2.2 DIS_BILI_ELAS（dibili.F90, dinon4.F90）

**概要**: 離散要素用のバイリニア弾性モデル

**実装位置**: `docs/code-aster/SRC/aster-14.6.0/aster-14.6.0/bibfor/elements/dibili.F90`

#### 主要なアルゴリズム

```fortran
! dinon4.F90:106-131
! バイリニア履歴計算
! seuil en deplacement
useuil = abs(fpre/kdeb)
if (abs(dulel) .gt. r8min) then
    ! 時刻マイナス
    depl = abs(ulel)
    if (depl .le. useuil) then
        fmoins = kdeb*depl
    else
        fmoins = fpre + kfin*(depl-useuil)
    endif
    if (ulel .lt. zero) fmoins = -fmoins

    ! 時刻プラス
    depl = abs(utlel)
    if (depl .le. useuil) then
        fplus = kdeb*depl
    else
        fplus = fpre + kfin*(depl-useuil)
    endif
    if (utlel .lt. zero) fplus = -fplus

    ! 割線剛性
    raide(ii) = abs(fplus - fmoins)/abs(dulel)
endif
```

**JR総研との主要な違い**:

| 項目 | DIS_BILI_ELAS | JR総研 |
|------|---------------|--------|
| 骨格曲線 | バイリニア（2折線） | トリリニア（4折線） |
| 除荷時剛性 | 割線剛性（増分から計算） | 低減剛性（べき乗則） |
| 履歴則 | 単純な折返し | 6つの履歴ルール |
| 最大点管理 | なし | あり（delta_max_pos/neg） |

---

## 3. 自作実装（JR総研モデル）の詳細分析

### 3.1 実装ファイル

- **設計書**: [nonliner-bar.md](nonliner-bar.md)
- **コード**: `src/fem/nonlinear/hysteresis/jr_stiffness_reduction.py`

### 3.2 骨格曲線（get_skeleton_force）

```python
# jr_stiffness_reduction.py:191-244
def get_skeleton_force(self, delta: float, direction: int) -> Tuple[float, float]:
    """4折線スケルトンカーブ"""
    if direction > 0:
        if abs_delta <= p.delta_1_pos:
            K = p.K_1_pos
            P = K * abs_delta
        elif abs_delta <= p.delta_2_pos:
            K = p.K_2_pos
            P = p.P_1_pos + K * (abs_delta - p.delta_1_pos)
        elif abs_delta <= p.delta_3_pos:
            K = p.K_3_pos
            P = p.P_2_pos + K * (abs_delta - p.delta_2_pos)
        else:
            K = max(p.K_3_pos * 0.01, self.EPSILON)
            P = p.P_3_pos + K * (abs_delta - p.delta_3_pos)
        return P, K
```

### 3.3 剛性低減則（get_reduced_stiffness）

```python
# jr_stiffness_reduction.py:246-307
def get_reduced_stiffness(self, state: HysteresisState, direction: int) -> float:
    """理論マニュアル7.11節 式(7.11.1)〜(7.11.3)"""
    # 弾性域では剛性低減なし
    if delta_max <= delta_1:
        return K_1

    # 領域別の剛性低減式
    if delta_max <= delta_2:
        # ひび割れ域: 式(7.11.1)
        Kd = K_1 * (delta_max / delta_1) ** (-p.beta)
    else:
        # 降伏域以降: 式(7.11.2), (7.11.3)
        Kd = K_2 * (delta_max / delta_2) ** (-p.beta)

    # 下限値: (F_max - F_1)/(δmax - δ1)
    K_lower = (P_max - P_1) / (delta_max - delta_1)
    Kd = max(K_lower, min(Kd, K_1))

    return Kd
```

### 3.4 再載荷経路（**問題箇所**）

```python
# jr_stiffness_reduction.py:486-495
elif current_branch == 'reloading':
    target_delta, target_P = self.get_target_point(delta, state)

    # ⚠️ 問題: 原点から目標点への直線
    if abs(target_delta) > self.EPSILON:
        K = target_P / target_delta      # 原点基準の傾き
    else:
        K = p.K_1_pos if delta >= 0 else p.K_1_neg
    P = K * delta                        # 原点(0,0)を通る！
```

---

## 4. 根本原因の特定

### 4.1 【致命的】再載荷経路の起点

**問題**: JR総研実装では再載荷時に原点(0,0)を通る直線を描いている。

**正しい実装**: P=0となった点（TrotNu）から最大経験点への直線を描くべき。

```
P (荷重)
^
|         ●max_pos (最大経験点)
|        /|
|       / |
|      /  |  正しい経路: TrotNu → max_pos
|     /   |
|    /    |
|   /    /
|  / JR:/
| /   ↙   JR実装: 原点 → max_pos
|/___●TrotNu_________________> δ (変位)
O    ↑
     P=0点（ここを起点とすべき）
```

### 4.2 【致命的】P=0点の記録欠如

**現在の実装**:
```python
# jr_stiffness_reduction.py:457-464
if current_branch == 'unloading':
    if (state.previous_P > self.EPSILON and delta < 0) or \
       (state.previous_P < -self.EPSILON and delta > 0):
        branch_info['crossed_zero'] = True    # フラグのみ
        branch_info['branch'] = 'reloading'
        # ⚠️ P=0となる変位を計算・保存していない！
```

**必要な実装**:
```python
# 除荷開始時にP=0となる変位を計算
Kd = self.get_reduced_stiffness(state, state.loading_direction)
delta_zero = state.reversal_delta - state.reversal_P / Kd
branch_info['delta_zero'] = delta_zero
```

### 4.3 剛性低減の基準

| 項目 | OpenSees/xc | JR総研 |
|------|-------------|--------|
| 基準剛性 | 常に`Eup`（最大勾配） | 領域により`K1`→`K2`に変化 |
| 基準変位 | 常に`rot1`（第1折れ点） | 領域により`δ1`→`δ2`に変化 |

**数値例**（δ1=0.003, δ2=0.015, β=0.4）:

| δmax | OpenSees除荷剛性 | JR除荷剛性 |
|------|-----------------|-----------|
| 0.006 | Eup×0.758 | K1×0.758 |
| 0.030 | Eup×0.398 | K2×0.758 |

降伏域以降で大きな差異が発生する。

---

## 5. コード対比

### 5.1 反転点管理

#### code-aster (PINTO_MENEGOTTO)

```fortran
! nm1dpm.F90:287-291
if (chgdir .lt. 0.d0) then
    epsrm=epsrp        ! 単一の前回反転点
    epsrp=epsm         ! 単一の現在反転点
    sigrp=sigm
endif
```

#### JR総研実装

```python
# jr_stiffness_reduction.py:585-590
if branch_info.get('push_to_stack', False):
    # スタックで複数反転点を管理
    new_state.reversal_stack.append(
        (state.previous_delta, state.previous_P)
    )
```

**差異**: JR総研はスタック管理で複数の内部ループに対応。PINTO_MENEGOTTOは単一反転点のみ。

### 5.2 除荷剛性計算

#### code-aster (DIS_BILI_ELAS)

```fortran
! dinon4.F90:122
! 割線剛性（変位増分から直接計算）
raide(ii) = abs(fplus - fmoins)/abs(dulel)
```

#### JR総研実装

```python
# jr_stiffness_reduction.py:478-484
# 低減剛性（べき乗則）
if current_branch in ('unloading', 'inner_unloading'):
    Kd = self.get_reduced_stiffness(state, state.loading_direction)
    P = reversal_P + Kd * (delta - reversal_delta)
    K = Kd
```

**差異**: code-asterは割線剛性、JR総研はべき乗則による低減剛性。

---

## 6. 修正方針

### 6.1 最優先: P=0点の記録と再載荷経路の修正

#### A. HysteresisStateに追加

```python
@dataclass
class HysteresisState:
    # ... 既存フィールド ...

    # P=0点の記録（追加）
    delta_zero_pos: float = 0.0  # 正側からの除荷でP=0となる変位
    delta_zero_neg: float = 0.0  # 負側からの除荷でP=0となる変位
```

#### B. P=0点の計算（除荷処理内）

```python
# unloading処理でP=0検出時
if crossed_zero_positive:
    Kd = self.get_reduced_stiffness(state, 1)
    delta_zero = state.reversal_delta - state.reversal_P / Kd
    branch_info['delta_zero_pos'] = delta_zero

if crossed_zero_negative:
    Kd = self.get_reduced_stiffness(state, -1)
    delta_zero = state.reversal_delta - state.reversal_P / Kd
    branch_info['delta_zero_neg'] = delta_zero
```

#### C. 再載荷経路の修正

```python
elif current_branch == 'reloading':
    target_delta, target_P = self.get_target_point(delta, state)

    # P=0点を起点とする
    if delta >= 0:
        delta_zero = state.delta_zero_pos
    else:
        delta_zero = state.delta_zero_neg

    # P=0点から目標点への直線
    if abs(target_delta - delta_zero) > self.EPSILON:
        K = target_P / (target_delta - delta_zero)
    else:
        K = p.K_1_pos if delta >= 0 else p.K_1_neg

    P = K * (delta - delta_zero)  # ⭕ delta_zeroを起点
```

### 6.2 オプション: OpenSees互換モードの追加

```python
def get_reduced_stiffness_opensees_compatible(self, state, direction):
    """OpenSees互換の剛性低減計算（オプション）"""
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

    # 常にEup（初期剛性の最大値）とdelta_1（第1折れ点）を基準
    kp = (delta_1 / delta_max) ** self.params.beta
    return Eup * kp
```

### 6.3 将来的な拡張

| 機能 | 優先度 | 難易度 |
|------|--------|--------|
| ピンチング機能 | 中 | 中 |
| ダメージモデル | 低 | 中 |
| OpenSees互換モード | 中 | 低 |

---

## 7. 検証計画

### 7.1 単体テスト

1. P=0点が正しく計算されることを確認
2. 再載荷経路がP=0点を起点としていることを確認
3. 内部ループでも同様の修正が適用されていることを確認

### 7.2 統合テスト

```python
# 繰返し載荷テスト
delta_history = [
    0.0, 0.02,   # 正方向載荷（降伏域）
    0.0,         # 除荷（P=0通過）
    -0.01,       # 負方向載荷
    0.0,         # 再除荷
    0.02         # 正方向再載荷
]

# 各ステップで以下を確認:
# - P=0を通過する変位（δ_zero）
# - 再載荷時の傾き
# - 履歴ループの形状
```

### 7.3 比較検証

修正後、xc/OpenSeesと同じパラメータで解析し、結果を比較する。

---

## 8. 参照ファイル一覧

| ファイル | 説明 |
|----------|------|
| `src/fem/nonlinear/hysteresis/jr_stiffness_reduction.py` | JR総研モデル実装 |
| `docs/plans/nonliner-bar.md` | JR総研モデル設計書 |
| `docs/code-aster/SRC/aster-14.6.0/.../nm1dpm.F90` | PINTO_MENEGOTTO実装 |
| `docs/code-aster/SRC/aster-14.6.0/.../dinon4.F90` | DIS_BILI_ELAS履歴計算 |
| `docs/plans/xc-comparison-report.md` | xc/OpenSees詳細比較 |
| `docs/plans/opensees-comparison-report.md` | OpenSees比較 |

---

## 9. 結論

### 根本原因

結果が一致しない**根本的な原因**は、再載荷経路が**原点を通る直線**として実装されていることである。

正しくは、**P=0となった点（TrotNu/delta_zero）を明示的に記録**し、再載荷時はその点から最大経験点への直線を描く必要がある。

### 補足

code-asterにはJR総研剛性低減RC型の直接対応モデルが存在しないため、結果比較の参照実装としてはxc/OpenSeesのHystereticMaterialを使用すべきである。

剛性低減式の違い（基準剛性・基準変位）も影響するが、再載荷経路の問題に比べれば影響は限定的である。

### 推奨アクション

1. **即座に修正**: P=0点の記録と再載荷経路の修正
2. **検証**: xc/OpenSeesとの結果比較
3. **オプション検討**: OpenSees互換モード、ピンチング、ダメージモデルの追加
