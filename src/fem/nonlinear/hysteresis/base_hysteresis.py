"""
履歴モデルの基底クラスと状態管理

理論マニュアル7.11節の履歴ルールに対応する状態管理を提供
"""
from dataclasses import dataclass, field
from typing import List, Tuple, Optional, Dict, Any
from abc import ABC, abstractmethod
import copy


@dataclass
class HysteresisState:
    """履歴モデルの状態変数を管理するデータクラス

    理論マニュアル7.11節の履歴ルール1〜6に対応する状態を保持

    Attributes:
        delta_max_pos: 正方向最大変位（絶対値）
        delta_max_neg: 負方向最大変位（絶対値）
        P_max_pos: 正方向最大荷重
        P_max_neg: 負方向最大荷重
        current_delta: 現在の変位
        current_P: 現在の荷重
        previous_delta: 前回の変位
        previous_P: 前回の荷重
        crossed_zero: P=0を通過したか（最大点指向開始フラグ）
        reversal_delta: 反転点変位（除荷開始点）
        reversal_P: 反転点荷重
        loading_direction: 載荷方向 (+1: 正, -1: 負, 0: 初期)
        branch: 現在のブランチ状態
        current_K: 現在の剛性
        reversal_stack: 内部ループ用の反転点スタック
        delta_max_inner: 内部ループ最大変形
    """
    # === 最大経験点（正負別々に管理） ===
    delta_max_pos: float = 0.0    # 正方向最大変位（絶対値）
    delta_max_neg: float = 0.0    # 負方向最大変位（絶対値）
    P_max_pos: float = 0.0        # 正方向最大荷重
    P_max_neg: float = 0.0        # 負方向最大荷重

    # === 現在・前回の状態 ===
    current_delta: float = 0.0
    current_P: float = 0.0
    previous_delta: float = 0.0
    previous_P: float = 0.0

    # === 履歴経路の追跡 ===
    crossed_zero: bool = False    # P=0を通過したか（最大点指向開始フラグ）
    reversal_delta: float = 0.0   # 反転点変位（除荷開始点）
    reversal_P: float = 0.0       # 反転点荷重
    loading_direction: int = 0    # +1: 正載荷, -1: 負載荷, 0: 初期

    # === ブランチ状態 ===
    # "initial": 初期領域（弾性）
    # "skeleton": スケルトンカーブ上
    # "unloading": 除荷中
    # "reloading": 最大点指向（P=0通過後）
    # "inner_unloading": 内部ループ除荷
    # "inner_reloading": 内部ループ再載荷
    branch: str = "initial"

    # === 現在の剛性 ===
    current_K: float = 0.0

    # === 内部ループ用の反転点スタック（理論マニュアル4,5に対応） ===
    # [(delta_1, P_1), (delta_2, P_2), ...] 内部ループの反転点を記憶
    reversal_stack: List[Tuple[float, float]] = field(default_factory=list)

    # === 内部ループ最大変形（理論マニュアル4に対応） ===
    delta_max_inner: float = 0.0

    def copy(self) -> 'HysteresisState':
        """状態のディープコピーを作成

        Returns:
            状態のコピー
        """
        return copy.deepcopy(self)

    def is_elastic(self, delta_1_pos: float, delta_1_neg: float) -> bool:
        """弾性域にあるかどうかを判定

        Args:
            delta_1_pos: 正側のひび割れ変位
            delta_1_neg: 負側のひび割れ変位

        Returns:
            弾性域にある場合True
        """
        return self.delta_max_pos <= delta_1_pos and self.delta_max_neg <= delta_1_neg

    def reset(self) -> None:
        """状態を初期化"""
        self.delta_max_pos = 0.0
        self.delta_max_neg = 0.0
        self.P_max_pos = 0.0
        self.P_max_neg = 0.0
        self.current_delta = 0.0
        self.current_P = 0.0
        self.previous_delta = 0.0
        self.previous_P = 0.0
        self.crossed_zero = False
        self.reversal_delta = 0.0
        self.reversal_P = 0.0
        self.loading_direction = 0
        self.branch = "initial"
        self.current_K = 0.0
        self.reversal_stack.clear()
        self.delta_max_inner = 0.0


class BaseHysteresis(ABC):
    """履歴モデルの抽象基底クラス

    すべての履歴モデルが実装すべきインターフェースを定義
    """

    @abstractmethod
    def get_force_and_stiffness(
        self,
        delta: float,
        state: HysteresisState
    ) -> Tuple[float, float, Optional[Dict[str, Any]]]:
        """変位から力と剛性を計算

        純粋関数として実装（状態は変更しない）

        Args:
            delta: 現在の変位
            state: 現在の履歴状態（変更しない）

        Returns:
            (P, K, branch_info):
                - P: 力
                - K: 接線剛性
                - branch_info: ブランチ情報（状態更新用）
        """
        pass

    @abstractmethod
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
        pass

    @abstractmethod
    def create_initial_state(self) -> HysteresisState:
        """初期状態を作成

        Returns:
            初期化された状態
        """
        pass

    def get_skeleton_force(
        self,
        delta: float,
        direction: int
    ) -> Tuple[float, float]:
        """スケルトンカーブ上の力と剛性を計算

        サブクラスでオーバーライド可能

        Args:
            delta: 変位
            direction: 方向 (+1: 正, -1: 負)

        Returns:
            (P, K): 力と剛性
        """
        raise NotImplementedError("Subclass must implement get_skeleton_force")
