"""
非線形梁要素クラス

JR総研剛性低減RC型履歴モデルを適用可能なTimoshenko梁要素
TBarElementを継承し、材料非線形機能を追加
"""
from typing import Dict, List, Optional, Any
import numpy as np
import copy

from .bar_element import TBarElement
from ..nonlinear.hysteresis import (
    HysteresisState,
    JRStiffnessReductionParams,
    JRStiffnessReductionModel
)


class NonlinearBarElement(TBarElement):
    """非線形挙動を持つTimoshenko梁要素

    TBarElementを継承し、材料非線形機能を追加

    非線形挙動を適用可能な自由度:
    - 'axial': 軸力（N）- 要素x方向
    - 'moment_y': Y軸周り曲げモーメント（My）- z方向曲げ
    - 'moment_z': Z軸周り曲げモーメント（Mz）- y方向曲げ
    - 'torsion': ねじりモーメント（Mx）- オプション

    Attributes:
        hysteresis_models: 自由度名 -> 履歴モデルの辞書
        current_states: 自由度名 -> 端部名 -> 現在の状態
        committed_states: 自由度名 -> 端部名 -> コミット済み状態
    """

    # 自由度名とローカル座標系でのインデックスのマッピング
    # ローカル座標系: x=軸方向, y=水平, z=鉛直
    # 12自由度: [dx_i, dy_i, dz_i, rx_i, ry_i, rz_i, dx_j, dy_j, dz_j, rx_j, ry_j, rz_j]
    DOF_MAPPING = {
        'axial': (0, 6),       # 軸方向変位 (i端, j端)
        'moment_y': (5, 11),   # y軸周りモーメント (z方向曲げに対応する回転)
        'moment_z': (4, 10),   # z軸周りモーメント (y方向曲げに対応する回転)
        'torsion': (3, 9)      # ねじり (x軸周り回転)
    }

    def __init__(
        self,
        element_id: int,
        node_ids: List[int],
        material_id: int,
        section_id: int,
        angle: float = 0.0,
        shear_correction: bool = True
    ):
        """
        Args:
            element_id: 要素ID
            node_ids: 構成節点ID（2節点）
            material_id: 材料ID
            section_id: 断面ID
            angle: 要素座標軸の回転角（度）
            shear_correction: せん断変形を考慮するか
        """
        super().__init__(
            element_id, node_ids, material_id,
            section_id, angle, shear_correction
        )

        # 履歴モデル（自由度ごと）
        self.hysteresis_models: Dict[str, JRStiffnessReductionModel] = {}

        # 積分点の状態（自由度ごと、各端部）
        # {'axial': {'i_end': HysteresisState, 'j_end': HysteresisState}, ...}
        self.current_states: Dict[str, Dict[str, HysteresisState]] = {}
        self.committed_states: Dict[str, Dict[str, HysteresisState]] = {}

        # 非線形解析が有効かどうか
        self._nonlinear_enabled = False

    def get_name(self) -> str:
        """要素タイプ名を取得"""
        return "nonlinear_bar"

    def set_hysteresis_model(
        self,
        dof: str,
        params: JRStiffnessReductionParams
    ) -> None:
        """指定した自由度に履歴モデルを設定

        Args:
            dof: 対象自由度（'axial', 'moment_y', 'moment_z', 'torsion'）
            params: 履歴パラメータ

        Raises:
            ValueError: 不明な自由度が指定された場合
        """
        if dof not in self.DOF_MAPPING:
            raise ValueError(
                f"不明な自由度: {dof}。"
                f"有効な値: {list(self.DOF_MAPPING.keys())}"
            )

        model = JRStiffnessReductionModel(params)
        self.hysteresis_models[dof] = model

        # 初期状態を設定（両端）
        self.current_states[dof] = {
            'i_end': model.create_initial_state(),
            'j_end': model.create_initial_state()
        }
        self.committed_states[dof] = {
            'i_end': model.create_initial_state(),
            'j_end': model.create_initial_state()
        }

        self._nonlinear_enabled = True
        print(f"要素{self.element_id}: {dof}に非線形履歴モデルを設定")

    def get_internal_force(self, displacement: np.ndarray) -> np.ndarray:
        """内力ベクトルを計算

        非線形要素では履歴モデルを使用して内力を計算

        Args:
            displacement: 要素節点変位ベクトル（12要素、全体座標系）

        Returns:
            内力ベクトル（12要素、全体座標系）
        """
        if not self._nonlinear_enabled:
            # 線形の場合は従来通り
            K = self.get_stiffness_matrix()
            return K @ displacement

        # 変換行列を取得
        T = self.get_transformation_matrix(12)

        # 要素座標系への変換
        disp_local = T @ displacement

        # 線形剛性行列から初期内力を計算（要素座標系）
        K_local = self._get_local_linear_stiffness()
        f_local = K_local @ disp_local

        # 非線形自由度の内力を履歴モデルで計算
        for dof_name, model in self.hysteresis_models.items():
            i_idx, j_idx = self.DOF_MAPPING[dof_name]

            # i端
            delta_i = disp_local[i_idx]
            state_i = self.current_states[dof_name]['i_end']
            P_i, K_i, branch_info_i = model.get_force_and_stiffness(delta_i, state_i)

            # 履歴モデルの出力で内力を置き換え
            f_local[i_idx] = P_i

            # 状態を一時的に更新（収束後にコミット）
            if branch_info_i is not None:
                self.current_states[dof_name]['i_end'] = model.update_state(
                    delta_i, P_i, K_i, state_i, branch_info_i
                )

            # j端
            delta_j = disp_local[j_idx]
            state_j = self.current_states[dof_name]['j_end']
            P_j, K_j, branch_info_j = model.get_force_and_stiffness(delta_j, state_j)

            f_local[j_idx] = P_j

            if branch_info_j is not None:
                self.current_states[dof_name]['j_end'] = model.update_state(
                    delta_j, P_j, K_j, state_j, branch_info_j
                )

        # 全体座標系への変換
        f_global = T.T @ f_local

        return f_global

    def get_tangent_stiffness_matrix(
        self,
        displacement: np.ndarray
    ) -> np.ndarray:
        """接線剛性行列を計算

        非線形要素では履歴モデルの接線剛性を使用

        Args:
            displacement: 要素節点変位ベクトル（12要素、全体座標系）

        Returns:
            接線剛性行列（12x12、全体座標系）
        """
        if not self._nonlinear_enabled:
            return self.get_stiffness_matrix()

        # 変換行列を取得
        T = self.get_transformation_matrix(12)

        # 要素座標系への変換
        disp_local = T @ displacement

        # 線形剛性行列から開始（要素座標系）
        K_local = self._get_local_linear_stiffness()

        # 非線形自由度の剛性を更新
        for dof_name, model in self.hysteresis_models.items():
            i_idx, j_idx = self.DOF_MAPPING[dof_name]

            # i端の接線剛性
            delta_i = disp_local[i_idx]
            state_i = self.current_states[dof_name]['i_end']
            _, K_i, _ = model.get_force_and_stiffness(delta_i, state_i)
            K_local[i_idx, i_idx] = K_i

            # j端の接線剛性
            delta_j = disp_local[j_idx]
            state_j = self.current_states[dof_name]['j_end']
            _, K_j, _ = model.get_force_and_stiffness(delta_j, state_j)
            K_local[j_idx, j_idx] = K_j

        # 全体座標系への変換
        K_global = T.T @ K_local @ T

        return K_global

    def _get_local_linear_stiffness(self) -> np.ndarray:
        """要素座標系での線形剛性行列を取得

        TBarElement.get_stiffness_matrix()は全体座標系の行列を返すため、
        変換行列を使って要素座標系に戻す

        Returns:
            要素座標系での剛性行列（12x12）
        """
        T = self.get_transformation_matrix(12)
        K_global = super().get_stiffness_matrix()
        K_local = T @ K_global @ T.T
        return K_local

    def commit_state(self) -> None:
        """現在の状態をコミット（収束後に呼び出す）

        Newton-Raphson反復が収束した後に呼び出し、
        現在の状態を確定状態として保存する
        """
        for dof_name in self.hysteresis_models.keys():
            self.committed_states[dof_name] = {
                'i_end': self.current_states[dof_name]['i_end'].copy(),
                'j_end': self.current_states[dof_name]['j_end'].copy()
            }

    def rollback_state(self) -> None:
        """状態をロールバック（発散時に呼び出す）

        Newton-Raphson反復が発散した場合に呼び出し、
        前回のコミット済み状態に戻す
        """
        for dof_name in self.hysteresis_models.keys():
            self.current_states[dof_name] = {
                'i_end': self.committed_states[dof_name]['i_end'].copy(),
                'j_end': self.committed_states[dof_name]['j_end'].copy()
            }

    def get_hysteresis_state(self, dof: str) -> Optional[Dict[str, HysteresisState]]:
        """指定した自由度の履歴状態を取得

        Args:
            dof: 自由度名

        Returns:
            {'i_end': HysteresisState, 'j_end': HysteresisState} or None
        """
        return self.current_states.get(dof)

    def is_nonlinear(self) -> bool:
        """非線形要素かどうかを判定

        Returns:
            非線形挙動が有効な場合True
        """
        return self._nonlinear_enabled

    def get_nonlinear_dofs(self) -> List[str]:
        """非線形挙動が設定されている自由度のリストを取得

        Returns:
            自由度名のリスト
        """
        return list(self.hysteresis_models.keys())

    def get_max_displacement(self, dof: str) -> Dict[str, float]:
        """指定した自由度の最大経験変位を取得

        Args:
            dof: 自由度名

        Returns:
            {'i_end_pos': float, 'i_end_neg': float,
             'j_end_pos': float, 'j_end_neg': float}
        """
        if dof not in self.current_states:
            return {}

        states = self.current_states[dof]
        return {
            'i_end_pos': states['i_end'].delta_max_pos,
            'i_end_neg': states['i_end'].delta_max_neg,
            'j_end_pos': states['j_end'].delta_max_pos,
            'j_end_neg': states['j_end'].delta_max_neg
        }

    def reset_states(self) -> None:
        """すべての履歴状態をリセット

        解析をやり直す場合に使用
        """
        for dof_name, model in self.hysteresis_models.items():
            self.current_states[dof_name] = {
                'i_end': model.create_initial_state(),
                'j_end': model.create_initial_state()
            }
            self.committed_states[dof_name] = {
                'i_end': model.create_initial_state(),
                'j_end': model.create_initial_state()
            }
