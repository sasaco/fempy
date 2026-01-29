# JR総研剛性低減RC型 材料非線形モデル実装計画

## 1. 概要

`src/fem` 以下の Python 実装に対して、**JR総研剛性低減RC型**履歴モデル（理論マニュアル 7.11節）を実装します。
このモデルは鉄筋コンクリート部材の非線形挙動（ひび割れ、降伏、剛性低減）をシミュレートします。

**対象モデル:** 理論マニュアル「7.11. JR総研剛性低減RC型」

## 2. モデル仕様

### 2.1 スケルトンカーブ（4折線）

```
荷重(P)
    ^
    |           P3 ●───────── K4 (硬化/軟化)
    |          /
    |     P2 ●       K3 = (P3-P2)/(δ3-δ2)
    |       /
    |  P1 ●           K2 = (P2-P1)/(δ2-δ1)
    |   /
    | /               K1 = P1/δ1 (初期剛性)
    |/
    O────●────●────●─────> 変位(δ)
       δ1   δ2   δ3
```

**特性点:**
- (δ1, P1): ひび割れ点
- (δ2, P2): 降伏点
- (δ3, P3): 終局点

### 2.2 剛性低減則

除荷時の剛性は最大経験変位に基づいて低減される：

- **ひび割れ領域 (δ1 < δmax < δ2)**:
  ```
  Kd = K1 × |δmax/δ1|^(-β)
  ```

- **降伏領域 (δmax ≥ δ2)**:
  ```
  Kd = K2 × |δmax/δ2|^(-β)
  ```

ここで β は剛性低減係数（典型値: 0.4）

### 2.3 履歴則

1. **載荷**: スケルトンカーブに沿う
2. **除荷**: 低減剛性 Kd で除荷
3. **P=0通過後の再載荷**: 反対側の最大経験点に向かう
4. **内部ループ**: Masing則に基づく

## 3. 入力パラメータ

| パラメータ | 説明 | 単位 |
|-----------|------|------|
| δ1 | ひび割れ変位 | m |
| δ2 | 降伏変位 | m |
| δ3 | 終局変位 | m |
| P1 | ひび割れ荷重 | N |
| P2 | 降伏荷重 | N |
| P3 | 終局荷重 | N |
| β | 剛性低減係数 | - |

## 4. 新規作成ファイル

| ファイル | 内容 |
|---------|------|
| `src/fem/nonlinear/__init__.py` | 非線形パッケージ |
| `src/fem/nonlinear/hysteresis/__init__.py` | 履歴モデルパッケージ |
| `src/fem/nonlinear/hysteresis/base_hysteresis.py` | `HysteresisState`, `BaseHysteresis` 基底クラス |
| `src/fem/nonlinear/hysteresis/jr_stiffness_reduction.py` | `JRStiffnessReductionParams`, `JRStiffnessReductionModel` |
| `src/fem/nonlinear/nonlinear_solver.py` | `NonlinearSolver` (Newton-Raphson) |
| `src/fem/elements/nonlinear_bar_element.py` | `NonlinearBarElement` |

## 5. 修正ファイル

| ファイル | 修正内容 |
|---------|----------|
| `src/fem/material.py` | `NonlinearMaterialProperty` クラス追加 |
| `src/fem/model.py` | `material_nonlinear` 解析タイプ追加 |

## 6. クラス設計

### 6.1 HysteresisState（状態管理）

```python
@dataclass
class HysteresisState:
    delta_max_pos: float = 0.0    # 正方向最大変位
    delta_max_neg: float = 0.0    # 負方向最大変位
    P_max_pos: float = 0.0        # 正方向最大荷重
    P_max_neg: float = 0.0        # 負方向最大荷重
    current_delta: float = 0.0
    current_P: float = 0.0
    previous_delta: float = 0.0
    previous_P: float = 0.0
    crossed_zero: bool = False    # P=0を通過したか
```

### 6.2 JRStiffnessReductionParams（パラメータ）

```python
@dataclass
class JRStiffnessReductionParams:
    delta_1: float    # ひび割れ変位
    delta_2: float    # 降伏変位
    delta_3: float    # 終局変位
    P_1: float        # ひび割れ荷重
    P_2: float        # 降伏荷重
    P_3: float        # 終局荷重
    beta: float       # 剛性低減係数（典型値: 0.4）
```

### 6.3 JRStiffnessReductionModel（履歴モデル）

```python
class JRStiffnessReductionModel(BaseHysteresis):
    def __init__(self, params: JRStiffnessReductionParams)
    def get_force_and_stiffness(delta, state) -> Tuple[float, float]
    def update_state(delta, P, state) -> HysteresisState
    def _get_skeleton_force(delta) -> Tuple[float, float]
    def _get_reduced_stiffness(delta_max, direction) -> float
```

### 6.4 NonlinearBarElement（非線形バー要素）

```python
class NonlinearBarElement(TBarElement):
    hysteresis_models: Dict[str, JRStiffnessReductionModel]
    integration_point_states: Dict[str, HysteresisState]
    committed_states: Dict[str, HysteresisState]

    def set_hysteresis_model(dof: str, params: JRStiffnessReductionParams)
    def get_internal_force(displacement: np.ndarray) -> np.ndarray
    def get_tangent_stiffness_matrix(displacement: np.ndarray) -> np.ndarray
    def update_state(displacement: np.ndarray)  # 収束後にコミット
    def rollback_state()                         # 発散時にロールバック
```

### 6.5 NonlinearSolver（Newton-Raphsonソルバー）

```python
class NonlinearSolver(Solver):
    def solve_nonlinear(mesh, material, boundary, elements,
                        n_steps=10, max_iter=50, tol=1e-6) -> Dict
```

## 7. アルゴリズム

### 7.1 Newton-Raphson法

```python
u = 0
for step in range(n_steps):
    lambda_factor = (step + 1) / n_steps
    F_ext = lambda_factor * F_total

    for iteration in range(max_iter):
        F_int = assemble_internal_forces(elements, u)
        R = F_ext - F_int

        if norm(R) / norm(F_ext) < tol:
            break  # 収束

        K_tan = assemble_tangent_stiffness(elements, u)
        du = solve(K_tan, R)
        u += du

    # Step収束後、状態変数をコミット
    for elem in elements:
        elem.update_state(u)
```

### 7.2 JRモデル履歴アルゴリズム

```python
def get_force_and_stiffness(delta, state):
    # 1. スケルトンカーブ上かチェック
    if delta > state.delta_max_pos:
        # 正方向スケルトン上
        P, K = get_skeleton_force(delta)
        return P, K

    if delta < -state.delta_max_neg:
        # 負方向スケルトン上
        P, K = get_skeleton_force(delta)
        return P, K

    # 2. 履歴ループ内
    if state.crossed_zero:
        # P=0通過後: 最大経験点に向かう
        if delta >= 0:
            K = state.P_max_pos / state.delta_max_pos
        else:
            K = state.P_max_neg / state.delta_max_neg
        P = K * delta
    else:
        # 除荷: 低減剛性で除荷
        Kd = get_reduced_stiffness(state.delta_max, state.direction)
        P = state.previous_P + Kd * (delta - state.previous_delta)
        K = Kd

    return P, K
```

## 8. 使用例

```python
from fem.model import FemModel

model = FemModel()

# ノード追加
model.add_node(1, 0.0, 0.0, 0.0)
model.add_node(2, 3.0, 0.0, 0.0)

# 非線形材料パラメータ定義
model.add_nonlinear_material(
    material_id=1,
    name="RC柱",
    E=30e9,
    delta_1=0.003, delta_2=0.015, delta_3=0.060,
    P_1=100e3, P_2=500e3, P_3=550e3,
    beta=0.4
)

# 非線形要素追加
model.add_nonlinear_bar_element(
    elem_id=1, node_ids=[1, 2],
    material_id=1, section_id=1,
    hysteresis_dofs=['moment_y']
)

# 境界条件
model.add_restraint(1, dx=True, dy=True, dz=True, rx=True, ry=True, rz=True)
model.add_load(2, fy=100e3)

# 非線形解析実行
results = model.run(
    analysis_type='material_nonlinear',
    n_load_steps=20,
    max_iterations=50,
    tolerance=1e-6
)
```

## 9. 実装手順

### Phase 1: 履歴フレームワーク
1. `src/fem/nonlinear/` パッケージ構造作成
2. `HysteresisState` データクラス実装
3. `BaseHysteresis` 抽象基底クラス実装

### Phase 2: JRモデル実装
1. `JRStiffnessReductionParams` 実装
2. スケルトンカーブロジック実装
3. 剛性低減則実装
4. 履歴経路ロジック実装

### Phase 3: 要素統合
1. `NonlinearBarElement` クラス実装
2. 内力計算 `get_internal_force()`
3. 接線剛性計算 `get_tangent_stiffness_matrix()`
4. 状態コミット/ロールバック機構

### Phase 4: 非線形ソルバー
1. Newton-Raphson反復ループ
2. 荷重増分制御
3. 収束判定

### Phase 5: システム統合
1. `MaterialProperty` 拡張
2. `FemModel.run()` に `material_nonlinear` 追加

## 10. 検証計画

1. **単体テスト**: スケルトンカーブ、剛性低減則、履歴経路
2. **統合テスト**: 単一要素の単調・繰返し載荷
3. **検証テスト**: 片持ち梁プッシュオーバー解析
4. **エネルギー検証**: 履歴ループのエネルギー散逸確認
