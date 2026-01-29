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

除荷時の剛性（戻り剛性）は最大経験変位に基づいて低減される（理論マニュアル7.11節 式7.11.1）：

```
Kd = K1 × |δmax/δ1|^(-β)    (Kmin < Kd < K1)
```

ここで：
- K1 = P1/δ1（初期剛性）
- β は剛性低減係数（典型値: 0.4）
- Kmin は戻り剛性の下限値（極端な剛性低下を防止）

**注**: 理論マニュアルでは領域（ひび割れ/降伏）によらず単一の式を使用します。

### 2.3 履歴則

1. **① 初期領域**: |δmax| < δ1 の場合、原点を通る勾配 K1 の直線上を動く
2. **② 載荷**: スケルトンカーブに沿う（δ > δ1 で第2勾配 K2 の直線上）
3. **③ 除荷**: 低減剛性 Kd で除荷。戻り点が新たな最大変形 δmax となる
4. **④ 最大点指向（P=0通過後）**: 復元力が0を超えると反対側の最大変形点を目指す
   - 反対側が弾性域の場合は、反対側の第1折れ点を目指す
   - 目標とする最大変形点は軸力変動により逐次更新
5. **⑤ 内部ループ**: 最大点指向中に戻る場合は Kd で除荷。P=0を超えると前の反転点を目指す
   - 反転点はスタックで管理（複数回の反転に対応）
   - 反転点に到達したらスタックからポップし、次の反転点または最大点を目指す
   - 最大経験点に到達したらスタックをクリアし、通常の履歴に戻る
6. **⑥ 特殊な場合（骨格曲線逸脱）**: 除荷中に骨格曲線の外側に出た場合：
   - 再度、骨格曲線上に載せる
   - そこから新たに除荷曲線を引き直す（Kd は新しい δ1 を用いて更新）

## 3. 入力パラメータ

正側・負側で異なるスケルトンカーブを定義可能：

| パラメータ | 説明 | 単位 |
|-----------|------|------|
| δ1_pos / δ1_neg | ひび割れ変位（正/負） | m |
| δ2_pos / δ2_neg | 降伏変位（正/負） | m |
| δ3_pos / δ3_neg | 終局変位（正/負） | m |
| P1_pos / P1_neg | ひび割れ荷重（正/負） | N |
| P2_pos / P2_neg | 降伏荷重（正/負） | N |
| P3_pos / P3_neg | 終局荷重（正/負） | N |
| β | 剛性低減係数 | - |
| Kmin | 戻り剛性の下限値 | N/m |

**注**: 対称なスケルトンカーブの場合は、正側パラメータのみ指定し、負側は自動的に符号反転して設定する簡易モードも提供する。

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
    # 最大経験点
    delta_max_pos: float = 0.0    # 正方向最大変位
    delta_max_neg: float = 0.0    # 負方向最大変位
    P_max_pos: float = 0.0        # 正方向最大荷重
    P_max_neg: float = 0.0        # 負方向最大荷重

    # 現在・前回の状態
    current_delta: float = 0.0
    current_P: float = 0.0
    previous_delta: float = 0.0
    previous_P: float = 0.0

    # 履歴経路の追跡
    crossed_zero: bool = False    # P=0を通過したか
    reversal_delta: float = 0.0   # 反転点変位
    reversal_P: float = 0.0       # 反転点荷重
    loading_direction: int = 0    # +1: 正載荷, -1: 負載荷, 0: 初期
    branch: str = "skeleton"      # "skeleton", "unloading", "reloading", "inner"
    current_K: float = 0.0        # 現在の剛性（履歴アルゴリズムで使用）

    # 内部ループ用の反転点スタック（理論マニュアル④⑤に対応）
    reversal_stack: List[Tuple[float, float]] = field(default_factory=list)
    # [(delta_1, P_1), (delta_2, P_2), ...] 内部ループの反転点を記憶

    # 内部ループ最大変形（理論マニュアル④に対応）
    delta_max_inner: float = 0.0
```

### 6.2 JRStiffnessReductionParams（パラメータ）

```python
@dataclass
class JRStiffnessReductionParams:
    # 正側スケルトンカーブ
    delta_1_pos: float    # ひび割れ変位（正）
    delta_2_pos: float    # 降伏変位（正）
    delta_3_pos: float    # 終局変位（正）
    P_1_pos: float        # ひび割れ荷重（正）
    P_2_pos: float        # 降伏荷重（正）
    P_3_pos: float        # 終局荷重（正）

    # 負側スケルトンカーブ
    delta_1_neg: float    # ひび割れ変位（負、絶対値）
    delta_2_neg: float    # 降伏変位（負、絶対値）
    delta_3_neg: float    # 終局変位（負、絶対値）
    P_1_neg: float        # ひび割れ荷重（負、絶対値）
    P_2_neg: float        # 降伏荷重（負、絶対値）
    P_3_neg: float        # 終局荷重（負、絶対値）

    beta: float           # 剛性低減係数（典型値: 0.4）
    K_min: float          # 戻り剛性の下限値（式7.11.1の Kmin）

    @classmethod
    def symmetric(cls, delta_1, delta_2, delta_3, P_1, P_2, P_3, beta, K_min=None):
        """対称スケルトンカーブ用のコンビニエンスコンストラクタ"""
        # K_min のデフォルト: 初期剛性の1%
        if K_min is None:
            K_min = (P_1 / delta_1) * 0.01
        return cls(
            delta_1, delta_2, delta_3, P_1, P_2, P_3,
            delta_1, delta_2, delta_3, P_1, P_2, P_3,
            beta, K_min
        )
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
    """
    非線形挙動を適用可能な自由度:
    - 'axial': 軸力（N）
    - 'moment_y': Y軸周り曲げモーメント（My）
    - 'moment_z': Z軸周り曲げモーメント（Mz）
    - 'torsion': ねじりモーメント（Mx） - オプション
    """
    hysteresis_models: Dict[str, JRStiffnessReductionModel]
    integration_point_states: Dict[str, HysteresisState]
    committed_states: Dict[str, HysteresisState]

    def set_hysteresis_model(dof: str, params: JRStiffnessReductionParams)
        """
        Args:
            dof: 対象自由度（'axial', 'moment_y', 'moment_z', 'torsion'）
            params: 履歴パラメータ
        """
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

        # 収束判定（残差ノルム + 変位増分ノルム）
        # ゼロ除算対策: 分母に max(..., 1.0) を使用
        R_norm = norm(R) / max(norm(F_ext), 1.0)
        if iteration > 0:
            du_norm = norm(du) / max(norm(u), 1.0)
        else:
            du_norm = float('inf')

        if R_norm < tol and du_norm < tol:
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
def get_force_and_stiffness(delta, state, params):
    """
    純粋関数として実装（状態は変更しない）
    Returns: (P, K, branch_info) - 力、剛性、ブランチ情報

    理論マニュアル7.11節の履歴ルール①〜⑥に対応
    """
    # 1. 載荷方向の判定
    d_delta = delta - state.previous_delta
    if abs(d_delta) < 1e-12:
        return state.current_P, state.current_K, None

    current_direction = 1 if d_delta > 0 else -1

    # 2. 反転点の検出（載荷方向が変わった場合）
    is_reversal = (state.loading_direction != 0 and
                   current_direction != state.loading_direction)

    # 反転時の一時的な値（状態更新は update_state で行う）
    if is_reversal:
        reversal_delta = state.previous_delta
        reversal_P = state.previous_P
        branch = "unloading"
    else:
        reversal_delta = state.reversal_delta
        reversal_P = state.reversal_P
        branch = state.branch

    # 3. スケルトンカーブ上かチェック
    if delta > state.delta_max_pos:
        # 正方向スケルトン上
        P, K = get_skeleton_force(delta, params, direction=1)
        return P, K, {"branch": "skeleton", "direction": 1}

    if delta < -state.delta_max_neg:
        # 負方向スケルトン上
        P, K = get_skeleton_force(delta, params, direction=-1)
        return P, K, {"branch": "skeleton", "direction": -1}

    # 4. P=0通過の検出（前回の荷重との符号比較）
    crossed_zero = state.crossed_zero or (state.previous_P * delta < 0 and branch == "unloading")
    if crossed_zero:
        branch = "reloading"

    # 5. 内部ループの検出（最大点指向中または内部再載荷中に反転）
    inner_loop_reversal = is_reversal and branch in ("reloading", "inner_reloading")
    if inner_loop_reversal:
        branch = "inner_unloading"
        # 反転点をスタックにプッシュ
        push_to_stack = True
    else:
        push_to_stack = False

    # 6. 内部ループでのP=0通過検出
    inner_crossed_zero = (state.previous_P * delta < 0 and branch == "inner_unloading")
    if inner_crossed_zero:
        branch = "inner_reloading"

    # 7. 履歴経路の計算
    if branch == "reloading":
        # ④最大点指向: P=0通過後、最大経験点に向かう
        target_delta, target_P = get_target_point(delta, state, params)
        K = target_P / max(abs(target_delta), 1e-12)
        P = K * delta

    elif branch == "inner_reloading":
        # ⑤内部ループ再載荷: 前の反転点を目指す
        if len(state.reversal_stack) > 0:
            # スタックのトップにある反転点を目指す
            target_delta, target_P = state.reversal_stack[-1]
        else:
            # スタックが空なら最大経験点を目指す
            target_delta, target_P = get_target_point(delta, state, params)

        # 現在位置から目標点への傾き
        # （P=0を通過した点から目標点への直線）
        K = target_P / max(abs(target_delta), 1e-12)
        P = K * delta

        # 目標点に到達したかチェック
        reached_target = (delta >= 0 and delta >= target_delta) or \
                         (delta < 0 and delta <= target_delta)
        if reached_target:
            # スタックからポップして次の目標へ
            pop_from_stack = True
        else:
            pop_from_stack = False

    elif branch in ("unloading", "inner_unloading"):
        # ③除荷 / ⑤内部ループ除荷: 低減剛性で除荷
        Kd = get_reduced_stiffness(state, params, state.loading_direction)
        P = reversal_P + Kd * (delta - reversal_delta)
        K = Kd

    else:
        # その他（skeleton等）は上で処理済み
        pass

    # 8. ⑥特殊な場合: 骨格曲線の外側に出た場合の処理
    P_skeleton, K_skeleton = get_skeleton_force(delta, params,
                                                 direction=1 if delta >= 0 else -1)
    if (delta >= 0 and P > P_skeleton) or (delta < 0 and P < P_skeleton):
        # 骨格曲線の外側に出た → 骨格曲線上に載せて除荷曲線を引き直す
        P = P_skeleton
        K = K_skeleton
        branch = "skeleton_correction"

    return P, K, {
        "branch": branch,
        "crossed_zero": crossed_zero,
        "reversal_delta": reversal_delta,
        "reversal_P": reversal_P,
        "push_to_stack": push_to_stack if 'push_to_stack' in dir() else False,
        "pop_from_stack": pop_from_stack if 'pop_from_stack' in dir() else False,
    }


def get_target_point(delta, state, params):
    """
    ④最大点指向の目標点を取得

    - 反対側が弾性域の場合は第1折れ点を目指す
    - それ以外は最大経験点を目指す
    """
    if delta >= 0:
        if state.delta_max_pos <= params.delta_1_pos:
            # 反対側が弾性域 → 第1折れ点を目指す
            return params.delta_1_pos, params.P_1_pos
        else:
            return state.delta_max_pos, state.P_max_pos
    else:
        if state.delta_max_neg <= params.delta_1_neg:
            return -params.delta_1_neg, -params.P_1_neg
        else:
            return -state.delta_max_neg, -state.P_max_neg


def get_reduced_stiffness(state, params, direction):
    """
    理論マニュアル7.11節 式(7.11.1)に基づく低減剛性を計算

    Kd = K1 × |δmax/δ1|^(-β)    (Kmin < Kd < K1)

    direction: +1 for positive, -1 for negative
    """
    if direction > 0:
        delta_max = state.delta_max_pos
        delta_1 = params.delta_1_pos
        K1 = params.P_1_pos / params.delta_1_pos
    else:
        delta_max = state.delta_max_neg
        delta_1 = params.delta_1_neg
        K1 = params.P_1_neg / params.delta_1_neg

    # 弾性域（δmax < δ1）では剛性低減なし
    if delta_max <= delta_1:
        return K1

    # 式(7.11.1): Kd = K1 × |δmax/δ1|^(-β)
    Kd = K1 * (delta_max / delta_1) ** (-params.beta)

    # 下限・上限のクリップ: Kmin < Kd < K1
    Kd = max(params.K_min, min(Kd, K1))

    return Kd


def update_state(delta, P, K, state, branch_info):
    """
    状態変数を更新（収束後に呼び出す）
    branch_info: get_force_and_stiffness から返されるブランチ情報
    """
    # 前回の状態を保存
    state.previous_delta = state.current_delta
    state.previous_P = state.current_P

    # 現在の状態を更新
    state.current_delta = delta
    state.current_P = P
    state.current_K = K

    # 載荷方向を更新
    if abs(delta - state.previous_delta) > 1e-12:
        state.loading_direction = 1 if delta > state.previous_delta else -1

    # ブランチ情報から状態を更新
    if branch_info is not None:
        state.branch = branch_info.get("branch", state.branch)
        if "crossed_zero" in branch_info:
            state.crossed_zero = branch_info["crossed_zero"]
        if "reversal_delta" in branch_info:
            state.reversal_delta = branch_info["reversal_delta"]
        if "reversal_P" in branch_info:
            state.reversal_P = branch_info["reversal_P"]

        # 内部ループ用スタック操作
        if branch_info.get("push_to_stack", False):
            # 内部ループ開始: 反転点をスタックにプッシュ
            state.reversal_stack.append((state.previous_delta, state.previous_P))
            # 内部ループ最大変形を記録
            state.delta_max_inner = abs(state.previous_delta)

        if branch_info.get("pop_from_stack", False):
            # 目標点に到達: スタックからポップ
            if len(state.reversal_stack) > 0:
                state.reversal_stack.pop()
            # スタックが空になったら通常の reloading に戻る
            if len(state.reversal_stack) == 0:
                state.branch = "reloading"

    # 最大経験点を更新
    if delta > state.delta_max_pos:
        state.delta_max_pos = delta
        state.P_max_pos = P
        # 最大点到達でスタックをクリア（外部ループに戻る）
        state.reversal_stack.clear()
        state.crossed_zero = False
    if delta < -state.delta_max_neg:
        state.delta_max_neg = abs(delta)
        state.P_max_neg = abs(P)
        state.reversal_stack.clear()
        state.crossed_zero = False

    return state
```

## 8. 使用例

### 8.1 対称スケルトンカーブ（簡易モード）

```python
from fem.model import FemModel

model = FemModel()

# ノード追加
model.add_node(1, 0.0, 0.0, 0.0)
model.add_node(2, 3.0, 0.0, 0.0)

# 非線形材料パラメータ定義（対称スケルトンカーブ）
# K_min: 戻り剛性の下限値（省略時は初期剛性の1%）
model.add_nonlinear_material(
    material_id=1,
    name="RC柱",
    E=30e9,
    delta_1=0.003, delta_2=0.015, delta_3=0.060,
    P_1=100e3, P_2=500e3, P_3=550e3,
    beta=0.4,
    K_min=None,       # None の場合、K1 * 0.01 が自動設定される
    symmetric=True    # 正負対称
)

# 非線形要素追加
model.add_nonlinear_bar_element(
    elem_id=1, node_ids=[1, 2],
    material_id=1, section_id=1,
    hysteresis_dofs=['moment_y']  # Y軸周り曲げに非線形を適用
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

### 8.2 非対称スケルトンカーブ

```python
# 非対称スケルトンカーブ（正側と負側で異なる特性）
model.add_nonlinear_material(
    material_id=2,
    name="非対称RC柱",
    E=30e9,
    # 正側パラメータ
    delta_1_pos=0.003, delta_2_pos=0.015, delta_3_pos=0.060,
    P_1_pos=100e3, P_2_pos=500e3, P_3_pos=550e3,
    # 負側パラメータ（例: 引張側が弱い場合）
    delta_1_neg=0.002, delta_2_neg=0.010, delta_3_neg=0.040,
    P_1_neg=80e3, P_2_neg=400e3, P_3_neg=420e3,
    beta=0.4,
    K_min=300e3       # 戻り剛性の下限値を明示的に指定
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
5. **非対称テスト**: 正負非対称スケルトンカーブの動作確認
6. **境界条件テスト**:
   - δmax = δ1 ちょうどの場合（弾性域と非弾性域の境界）
   - P がちょうど 0 になる場合
   - 反転がスケルトンカーブ上で発生する場合
   - Kd が Kmin にクリップされる場合
7. **特殊ケーステスト**:
   - 骨格曲線の外側に逸脱した場合の補正処理
   - 内部ループでの複数回反転
8. **エラーハンドリングテスト**:
   - パラメータ検証（δ1 < δ2 < δ3, P1 < P2 など）
   - 収束失敗時の処理
   - 負の剛性発生時の警告
