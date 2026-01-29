"""
JR総研剛性低減RC型 履歴モデル

理論マニュアル「7.11 JR総研剛性低減 RC 型」に基づく実装

特徴:
- 4折線スケルトンカーブ（ひび割れ、降伏、終局）
- 剛性低減則（式7.11.1）
- 6つの履歴ルール（内部ループスタック管理含む）
- 正負非対称スケルトンカーブ対応
"""
from dataclasses import dataclass
from typing import Tuple, Optional, Dict, Any

from .base_hysteresis import HysteresisState, BaseHysteresis


@dataclass
class JRStiffnessReductionParams:
    """JRモデルのパラメータ

    正側・負側で異なるスケルトンカーブを定義可能

    Attributes:
        delta_1_pos: 正側ひび割れ変位
        delta_2_pos: 正側降伏変位
        delta_3_pos: 正側終局変位
        P_1_pos: 正側ひび割れ荷重
        P_2_pos: 正側降伏荷重
        P_3_pos: 正側終局荷重
        delta_1_neg: 負側ひび割れ変位（絶対値）
        delta_2_neg: 負側降伏変位（絶対値）
        delta_3_neg: 負側終局変位（絶対値）
        P_1_neg: 負側ひび割れ荷重（絶対値）
        P_2_neg: 負側降伏荷重（絶対値）
        P_3_neg: 負側終局荷重（絶対値）
        beta: 剛性低減係数（典型値: 0.4）
        K_min: 戻り剛性の下限値
    """
    # === 正側スケルトンカーブ ===
    delta_1_pos: float    # ひび割れ変位（正）
    delta_2_pos: float    # 降伏変位（正）
    delta_3_pos: float    # 終局変位（正）
    P_1_pos: float        # ひび割れ荷重（正）
    P_2_pos: float        # 降伏荷重（正）
    P_3_pos: float        # 終局荷重（正）

    # === 負側スケルトンカーブ ===
    delta_1_neg: float    # ひび割れ変位（負、絶対値で指定）
    delta_2_neg: float    # 降伏変位（負、絶対値）
    delta_3_neg: float    # 終局変位（負、絶対値）
    P_1_neg: float        # ひび割れ荷重（負、絶対値）
    P_2_neg: float        # 降伏荷重（負、絶対値）
    P_3_neg: float        # 終局荷重（負、絶対値）

    # === 剛性低減パラメータ ===
    beta: float           # 剛性低減係数（典型値: 0.4）
    K_min: float          # 戻り剛性の下限値

    def __post_init__(self):
        """パラメータの検証"""
        # 正側の検証
        if not (0 < self.delta_1_pos < self.delta_2_pos < self.delta_3_pos):
            raise ValueError(
                f"正側変位は 0 < delta_1 < delta_2 < delta_3 である必要があります: "
                f"delta_1={self.delta_1_pos}, delta_2={self.delta_2_pos}, delta_3={self.delta_3_pos}"
            )
        if not (0 < self.P_1_pos <= self.P_2_pos <= self.P_3_pos):
            raise ValueError(
                f"正側荷重は 0 < P_1 <= P_2 <= P_3 である必要があります: "
                f"P_1={self.P_1_pos}, P_2={self.P_2_pos}, P_3={self.P_3_pos}"
            )

        # 負側の検証
        if not (0 < self.delta_1_neg < self.delta_2_neg < self.delta_3_neg):
            raise ValueError(
                f"負側変位は 0 < delta_1 < delta_2 < delta_3 である必要があります: "
                f"delta_1={self.delta_1_neg}, delta_2={self.delta_2_neg}, delta_3={self.delta_3_neg}"
            )
        if not (0 < self.P_1_neg <= self.P_2_neg <= self.P_3_neg):
            raise ValueError(
                f"負側荷重は 0 < P_1 <= P_2 <= P_3 である必要があります: "
                f"P_1={self.P_1_neg}, P_2={self.P_2_neg}, P_3={self.P_3_neg}"
            )

        # 剛性低減パラメータの検証
        if self.beta < 0:
            raise ValueError(f"beta は非負である必要があります: beta={self.beta}")
        if self.K_min <= 0:
            raise ValueError(f"K_min は正である必要があります: K_min={self.K_min}")

    @classmethod
    def symmetric(
        cls,
        delta_1: float,
        delta_2: float,
        delta_3: float,
        P_1: float,
        P_2: float,
        P_3: float,
        beta: float,
        K_min: Optional[float] = None
    ) -> 'JRStiffnessReductionParams':
        """対称スケルトンカーブ用のコンビニエンスコンストラクタ

        Args:
            delta_1, delta_2, delta_3: 特性変位
            P_1, P_2, P_3: 特性荷重
            beta: 剛性低減係数
            K_min: 戻り剛性下限値（省略時は初期剛性の1%）

        Returns:
            対称パラメータを持つJRStiffnessReductionParams
        """
        if K_min is None:
            K_1 = P_1 / delta_1
            K_min = K_1 * 0.01

        return cls(
            delta_1_pos=delta_1, delta_2_pos=delta_2, delta_3_pos=delta_3,
            P_1_pos=P_1, P_2_pos=P_2, P_3_pos=P_3,
            delta_1_neg=delta_1, delta_2_neg=delta_2, delta_3_neg=delta_3,
            P_1_neg=P_1, P_2_neg=P_2, P_3_neg=P_3,
            beta=beta, K_min=K_min
        )

    @property
    def K_1_pos(self) -> float:
        """正側の初期剛性"""
        return self.P_1_pos / self.delta_1_pos

    @property
    def K_1_neg(self) -> float:
        """負側の初期剛性"""
        return self.P_1_neg / self.delta_1_neg

    @property
    def K_2_pos(self) -> float:
        """正側の第2剛性（ひび割れ後〜降伏）"""
        return (self.P_2_pos - self.P_1_pos) / (self.delta_2_pos - self.delta_1_pos)

    @property
    def K_2_neg(self) -> float:
        """負側の第2剛性"""
        return (self.P_2_neg - self.P_1_neg) / (self.delta_2_neg - self.delta_1_neg)

    @property
    def K_3_pos(self) -> float:
        """正側の第3剛性（降伏後〜終局）"""
        return (self.P_3_pos - self.P_2_pos) / (self.delta_3_pos - self.delta_2_pos)

    @property
    def K_3_neg(self) -> float:
        """負側の第3剛性"""
        return (self.P_3_neg - self.P_2_neg) / (self.delta_3_neg - self.delta_2_neg)


class JRStiffnessReductionModel(BaseHysteresis):
    """JR総研剛性低減RC型履歴モデル

    理論マニュアル7.11節の履歴ルールを実装:
    1. 初期領域: |delta_max| < delta_1 で原点を通る初期剛性の直線上
    2. 載荷: スケルトンカーブに沿う
    3. 除荷: 低減剛性Kdで除荷、P=0を通過後に最大点指向
    4. 最大点指向: 反対側の最大経験点（弾性域なら第1折れ点）へ向かう
    5. 内部ループ: 最大点指向中に反転→内部ループ開始、反転点スタック管理
    6. 骨格曲線逸脱: 除荷中に骨格曲線外に出た場合の補正
    """

    # 数値許容誤差
    EPSILON = 1e-12

    def __init__(self, params: JRStiffnessReductionParams):
        """
        Args:
            params: JRモデルパラメータ
        """
        self.params = params

    def create_initial_state(self) -> HysteresisState:
        """初期状態を作成

        Returns:
            初期化された履歴状態
        """
        state = HysteresisState()
        state.current_K = self.params.K_1_pos  # 初期剛性
        state.branch = "initial"
        return state

    def get_skeleton_force(
        self,
        delta: float,
        direction: int
    ) -> Tuple[float, float]:
        """スケルトンカーブ上の力と剛性を計算

        4折線モデル（ひび割れ、降伏、終局）

        Args:
            delta: 変位（符号付き）
            direction: +1 for 正側, -1 for 負側

        Returns:
            (P, K): 力と接線剛性
        """
        p = self.params
        abs_delta = abs(delta)

        if direction > 0:
            # 正側スケルトンカーブ
            if abs_delta <= p.delta_1_pos:
                # 弾性域
                K = p.K_1_pos
                P = K * abs_delta
            elif abs_delta <= p.delta_2_pos:
                # ひび割れ後〜降伏
                K = p.K_2_pos
                P = p.P_1_pos + K * (abs_delta - p.delta_1_pos)
            elif abs_delta <= p.delta_3_pos:
                # 降伏後〜終局
                K = p.K_3_pos
                P = p.P_2_pos + K * (abs_delta - p.delta_2_pos)
            else:
                # 終局点超過（小さな正剛性で一定荷重に近い挙動）
                K = max(p.K_3_pos * 0.01, self.EPSILON)
                P = p.P_3_pos + K * (abs_delta - p.delta_3_pos)
            return P, K
        else:
            # 負側スケルトンカーブ
            if abs_delta <= p.delta_1_neg:
                K = p.K_1_neg
                P = K * abs_delta
            elif abs_delta <= p.delta_2_neg:
                K = p.K_2_neg
                P = p.P_1_neg + K * (abs_delta - p.delta_1_neg)
            elif abs_delta <= p.delta_3_neg:
                K = p.K_3_neg
                P = p.P_2_neg + K * (abs_delta - p.delta_2_neg)
            else:
                K = max(p.K_3_neg * 0.01, self.EPSILON)
                P = p.P_3_neg + K * (abs_delta - p.delta_3_neg)
            # 負側なので符号反転
            return -P, K

    def get_reduced_stiffness(
        self,
        state: HysteresisState,
        direction: int
    ) -> float:
        """低減剛性を計算（式7.11.1〜7.11.3）

        理論マニュアル7.11節に基づく領域別の剛性低減式:
        - ひび割れ域(δ1 < δmax < δ2): 式(7.11.1) Kd = K1 * |δmax/δ1|^(-β)
        - 降伏域(δ2 < δmax < δ3): 式(7.11.2) Kd = K2 * |δmax/δ2|^(-β)
        - 終局域(δmax > δ3): 式(7.11.3) Kd = K2 * |δmax/δ2|^(-β)

        下限値: (F_max - F_1)/(δmax - δ1)（第2勾配相当）

        Args:
            state: 現在の状態
            direction: 載荷方向 (+1 or -1)

        Returns:
            低減剛性 Kd
        """
        p = self.params

        if direction > 0:
            delta_max = state.delta_max_pos
            delta_1 = p.delta_1_pos
            delta_2 = p.delta_2_pos
            K_1 = p.K_1_pos
            K_2 = p.K_2_pos
            P_max = state.P_max_pos
            P_1 = p.P_1_pos
        else:
            delta_max = state.delta_max_neg
            delta_1 = p.delta_1_neg
            delta_2 = p.delta_2_neg
            K_1 = p.K_1_neg
            K_2 = p.K_2_neg
            P_max = state.P_max_neg
            P_1 = p.P_1_neg

        # 弾性域（delta_max <= delta_1）では剛性低減なし
        if delta_max <= delta_1:
            return K_1

        # 領域別の剛性低減式
        if delta_max <= delta_2:
            # ひび割れ域: 式(7.11.1) Kd = K1 * |δmax/δ1|^(-β)
            Kd = K_1 * (delta_max / delta_1) ** (-p.beta)
        else:
            # 降伏域以降: 式(7.11.2), (7.11.3) Kd = K2 * |δmax/δ2|^(-β)
            Kd = K_2 * (delta_max / delta_2) ** (-p.beta)

        # 下限値: (F_max - F_1)/(δmax - δ1)（理論マニュアル準拠）
        if delta_max > delta_1:
            K_lower = (P_max - P_1) / (delta_max - delta_1)
        else:
            K_lower = 0.0

        # 下限・上限のクリップ
        Kd = max(K_lower, min(Kd, K_1))

        return Kd

    def get_target_point(
        self,
        delta: float,
        state: HysteresisState
    ) -> Tuple[float, float]:
        """最大点指向の目標点を取得

        理論マニュアル7.11節に基づく目標点決定:
        (2) δ1 < δmax < δ2 の場合:
            - 反対側が弾性域なら第1折れ点を目指す
            - それ以外は最大経験点を目指す
        (3)(4) δmax > δ2 の場合:
            - 反対側が弾性域またはひび割れ域(δ2以下)なら第2折れ点を目指す
            - それ以外は最大経験点を目指す

        Args:
            delta: 現在の変位（符号で移動方向を判定）
            state: 現在の状態

        Returns:
            (target_delta, target_P): 目標点
        """
        p = self.params

        if delta >= 0:
            # 正方向へ移動中 → 正側の目標を設定
            if state.delta_max_pos <= p.delta_1_pos:
                # 正側が弾性域 → 第1折れ点を目指す
                return p.delta_1_pos, p.P_1_pos
            elif state.delta_max_pos <= p.delta_2_pos:
                # 正側がひび割れ域 → 最大経験点を目指す
                return state.delta_max_pos, state.P_max_pos
            else:
                # 正側が降伏域以上 → 反対側（負側）がひび割れ域以下なら第2折れ点
                if state.delta_max_neg <= p.delta_2_neg:
                    return p.delta_2_pos, p.P_2_pos
                else:
                    return state.delta_max_pos, state.P_max_pos
        else:
            # 負方向へ移動中 → 負側の目標を設定
            if state.delta_max_neg <= p.delta_1_neg:
                # 負側が弾性域 → 第1折れ点を目指す
                return -p.delta_1_neg, -p.P_1_neg
            elif state.delta_max_neg <= p.delta_2_neg:
                # 負側がひび割れ域 → 最大経験点を目指す
                return -state.delta_max_neg, -state.P_max_neg
            else:
                # 負側が降伏域以上 → 反対側（正側）がひび割れ域以下なら第2折れ点
                if state.delta_max_pos <= p.delta_2_pos:
                    return -p.delta_2_neg, -p.P_2_neg
                else:
                    return -state.delta_max_neg, -state.P_max_neg

    def get_force_and_stiffness(
        self,
        delta: float,
        state: HysteresisState
    ) -> Tuple[float, float, Optional[Dict[str, Any]]]:
        """変位から力と剛性を計算

        理論マニュアル7.11節の履歴ルール1〜6を実装

        Args:
            delta: 現在の変位
            state: 現在の履歴状態（変更しない）

        Returns:
            (P, K, branch_info):
                - P: 力
                - K: 接線剛性
                - branch_info: ブランチ情報（状態更新用）
        """
        p = self.params

        # 1. 変位増分の方向を判定
        d_delta = delta - state.previous_delta
        if abs(d_delta) < self.EPSILON:
            # 変位変化なし → 現在の状態を維持
            return state.current_P, state.current_K, None

        current_direction = 1 if d_delta > 0 else -1

        # 2. 反転点の検出（載荷方向が変わった場合）
        is_reversal = (
            state.loading_direction != 0 and
            current_direction != state.loading_direction
        )

        # ブランチ情報の初期化
        branch_info: Dict[str, Any] = {
            'branch': state.branch,
            'crossed_zero': state.crossed_zero,
            'reversal_delta': state.reversal_delta,
            'reversal_P': state.reversal_P,
            'push_to_stack': False,
            'pop_from_stack': False,
            'direction': current_direction
        }

        # 反転時の処理
        if is_reversal:
            branch_info['reversal_delta'] = state.previous_delta
            branch_info['reversal_P'] = state.previous_P

            # 現在のブランチに応じて次のブランチを決定
            if state.branch in ('skeleton', 'initial'):
                branch_info['branch'] = 'unloading'
            elif state.branch == 'reloading':
                # 最大点指向中に反転 → 内部ループ開始
                branch_info['branch'] = 'inner_unloading'
                branch_info['push_to_stack'] = True
            elif state.branch == 'inner_reloading':
                # 内部ループ再載荷中に反転
                branch_info['branch'] = 'inner_unloading'
                branch_info['push_to_stack'] = True

        # 3. スケルトンカーブ超過チェック（新たな最大点に達した場合）
        if delta > state.delta_max_pos + self.EPSILON:
            # 正方向スケルトン上に新たに載る
            P, K = self.get_skeleton_force(delta, direction=1)
            branch_info['branch'] = 'skeleton'
            branch_info['crossed_zero'] = False
            return P, K, branch_info

        if delta < -(state.delta_max_neg + self.EPSILON):
            # 負方向スケルトン上に新たに載る
            P, K = self.get_skeleton_force(delta, direction=-1)
            branch_info['branch'] = 'skeleton'
            branch_info['crossed_zero'] = False
            return P, K, branch_info

        # 4. ルール1: 初期領域のチェック
        if state.delta_max_pos <= p.delta_1_pos and state.delta_max_neg <= p.delta_1_neg:
            # まだ弾性域内
            if delta >= 0 and delta <= p.delta_1_pos:
                K = p.K_1_pos
                P = K * delta
                branch_info['branch'] = 'initial'
                return P, K, branch_info
            elif delta < 0 and abs(delta) <= p.delta_1_neg:
                K = p.K_1_neg
                P = -K * abs(delta)
                branch_info['branch'] = 'initial'
                return P, K, branch_info

        # 5. P=0通過の検出
        current_branch = branch_info['branch']

        if current_branch == 'unloading':
            # 除荷中にP=0を通過したかチェック
            # 符号が変わった、または0に到達した場合
            if (state.previous_P > self.EPSILON and delta < 0) or \
               (state.previous_P < -self.EPSILON and delta > 0):
                branch_info['crossed_zero'] = True
                branch_info['branch'] = 'reloading'
                current_branch = 'reloading'

        elif current_branch == 'inner_unloading':
            # 内部ループ除荷中にP=0を通過
            if (state.previous_P > self.EPSILON and delta < 0) or \
               (state.previous_P < -self.EPSILON and delta > 0):
                branch_info['crossed_zero'] = True
                branch_info['branch'] = 'inner_reloading'
                current_branch = 'inner_reloading'

        # 6. 各ブランチでの力・剛性計算
        P: float
        K: float

        if current_branch in ('unloading', 'inner_unloading'):
            # ルール3: 除荷 - 低減剛性で除荷
            Kd = self.get_reduced_stiffness(state, state.loading_direction)
            reversal_delta = branch_info['reversal_delta']
            reversal_P = branch_info['reversal_P']
            P = reversal_P + Kd * (delta - reversal_delta)
            K = Kd

        elif current_branch == 'reloading':
            # ルール4: 最大点指向 - P=0通過後、反対側の最大経験点へ向かう
            target_delta, target_P = self.get_target_point(delta, state)

            # 原点から目標点への直線
            if abs(target_delta) > self.EPSILON:
                K = target_P / target_delta
            else:
                K = p.K_1_pos if delta >= 0 else p.K_1_neg
            P = K * delta

        elif current_branch == 'inner_reloading':
            # ルール5: 内部ループ再載荷 - 前の反転点を目指す
            if len(state.reversal_stack) > 0:
                target_delta, target_P = state.reversal_stack[-1]
            else:
                target_delta, target_P = self.get_target_point(delta, state)

            if abs(target_delta) > self.EPSILON:
                K = target_P / target_delta
            else:
                K = p.K_1_pos if delta >= 0 else p.K_1_neg
            P = K * delta

            # 目標点到達チェック
            if delta >= 0:
                if delta >= target_delta - self.EPSILON:
                    branch_info['pop_from_stack'] = True
            else:
                if delta <= target_delta + self.EPSILON:
                    branch_info['pop_from_stack'] = True

        else:
            # skeleton または initial（既に上で処理済みのはず）
            direction = 1 if delta >= 0 else -1
            P, K = self.get_skeleton_force(delta, direction)

        # 7. ルール6: 骨格曲線逸脱チェック
        direction = 1 if delta >= 0 else -1
        P_skeleton, K_skeleton = self.get_skeleton_force(delta, direction)

        if direction > 0:
            if P > P_skeleton + self.EPSILON:
                # 骨格曲線の外側に出た → 骨格曲線上に補正
                P = P_skeleton
                K = K_skeleton
                branch_info['branch'] = 'skeleton'
        else:
            if P < P_skeleton - self.EPSILON:
                P = P_skeleton
                K = K_skeleton
                branch_info['branch'] = 'skeleton'

        return P, K, branch_info

    def update_state(
        self,
        delta: float,
        P: float,
        K: float,
        state: HysteresisState,
        branch_info: Optional[Dict[str, Any]]
    ) -> HysteresisState:
        """状態変数を更新（収束後に呼び出す）

        Args:
            delta: 収束した変位
            P: 収束した力
            K: 収束した剛性
            state: 現在の状態
            branch_info: get_force_and_stiffnessから返されたブランチ情報

        Returns:
            更新された状態（新しいインスタンス）
        """
        # 状態をコピー
        new_state = state.copy()

        # 前回の状態を保存
        new_state.previous_delta = state.current_delta
        new_state.previous_P = state.current_P

        # 現在の状態を更新
        new_state.current_delta = delta
        new_state.current_P = P
        new_state.current_K = K

        # 載荷方向を更新
        if abs(delta - state.previous_delta) > self.EPSILON:
            new_state.loading_direction = 1 if delta > state.previous_delta else -1

        # ブランチ情報から状態を更新
        if branch_info is not None:
            new_state.branch = branch_info.get('branch', state.branch)
            new_state.crossed_zero = branch_info.get('crossed_zero', state.crossed_zero)
            new_state.reversal_delta = branch_info.get('reversal_delta', state.reversal_delta)
            new_state.reversal_P = branch_info.get('reversal_P', state.reversal_P)

            # 内部ループ用スタック操作
            if branch_info.get('push_to_stack', False):
                # 内部ループ開始: 反転点をスタックにプッシュ
                new_state.reversal_stack.append(
                    (state.previous_delta, state.previous_P)
                )
                new_state.delta_max_inner = abs(state.previous_delta)

            if branch_info.get('pop_from_stack', False):
                # 目標点に到達: スタックからポップ
                if len(new_state.reversal_stack) > 0:
                    new_state.reversal_stack.pop()
                # スタックが空になったら通常の reloading に戻る
                if len(new_state.reversal_stack) == 0:
                    new_state.branch = 'reloading'

        # 最大経験点を更新
        if delta > new_state.delta_max_pos:
            new_state.delta_max_pos = delta
            new_state.P_max_pos = P
            # 最大点到達でスタッククリア（外部ループに戻る）
            new_state.reversal_stack.clear()
            new_state.crossed_zero = False

        if delta < 0 and abs(delta) > new_state.delta_max_neg:
            new_state.delta_max_neg = abs(delta)
            new_state.P_max_neg = abs(P)
            new_state.reversal_stack.clear()
            new_state.crossed_zero = False

        return new_state
