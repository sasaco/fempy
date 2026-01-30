"""
FEM解析のファイル入出力モジュール
JavaScript版のFileIO機能に対応
"""
import json
import os
from typing import Dict, Any, Optional, List
import numpy as np
from .mesh import MeshModel
from .boundary_condition import BoundaryCondition
from .material import Material, MaterialProperty, ShellParameter, BarParameter, NonlinearMaterialProperty
from .section import Section, CircleSection, RectSection, ISection, TubeSection


def read_model(file_path: str) -> Dict[str, Any]:
    """モデルファイルを読み込む
    
    Args:
        file_path: ファイルパス（.json, .fw3, .fem形式に対応）
        
    Returns:
        モデルデータの辞書
    """
    ext = os.path.splitext(file_path)[1].lower()
    
    if ext == '.json':
        """JSONフォーマットのモデルを読み込む"""
        with open(file_path, 'r', encoding='utf-8') as f:
            data = json.load(f)        
        return _read_json_model(data)
    elif ext == '.fw3':
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()        
        return _read_fw3_model(lines)
    elif ext == '.fem':
        # ✅ V0互換: .fem形式（V0テストデータ形式）をサポート
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()        
        return _read_fem_model(lines)
    else:
        raise ValueError(f"Unsupported file format: {ext}")


def _read_json_model(data: str) -> Dict[str, Any]:
       
    model_data = {
        'mesh': MeshModel(),
        'boundary': BoundaryCondition(),
        'material': Material(),
        'section': Section()
    }
    
    # 旧形式のチェック（nodeセクションがある場合）
    if 'node' in data:
        # 旧形式の読み込み処理
        return _read_legacy_json_model(data, model_data)
    
    # 新形式のノードデータの読み込み
    if 'nodes' in data:
        for node_id, coords in data['nodes'].items():
            model_data['mesh'].add_node(int(node_id), coords)
            
    # 新形式の要素データの読み込み
    if 'elements' in data:
        for elem_id, elem_data in data['elements'].items():
            model_data['mesh'].add_element(
                int(elem_id),
                elem_data['type'],
                elem_data['nodes'],
                elem_data.get('material_id', 1),
                **{k: v for k, v in elem_data.items() 
                   if k not in ['type', 'nodes', 'material_id']}
            )
            
    # 新形式の材料データの読み込み
    if 'materials' in data:
        for mat_id, mat_data in data['materials'].items():
            model_data['material'].add_material(
                int(mat_id),
                MaterialProperty(**mat_data)
            )
            
    # 新形式の境界条件の読み込み
    if 'boundary_conditions' in data:
        bc_data = data['boundary_conditions']
        
        # 拘束条件
        if 'restraints' in bc_data:
            for node_id, restraint in bc_data['restraints'].items():
                model_data['boundary'].add_restraint(
                    int(node_id),
                    restraint['dof'],
                    restraint.get('values')
                )
                
        # 荷重条件
        if 'loads' in bc_data:
            for node_id, forces in bc_data['loads'].items():
                model_data['boundary'].add_load(int(node_id), forces)
                
        # 面圧条件（新規追加）
        if 'pressures' in bc_data:
            for pressure_data in bc_data['pressures']:
                model_data['boundary'].add_pressure(
                    pressure_data['element_id'],
                    pressure_data['face'],
                    pressure_data['pressure']
                )
                
    return model_data


def _read_legacy_json_model(data: Dict[str, Any], model_data: Dict[str, Any]) -> Dict[str, Any]:
    """旧形式のJSONファイルを読み込む"""
    
    # nodeセクションの読み込み
    if 'node' in data:
        for node_id, coords in data['node'].items():
            coord_list = [coords['x'], coords['y'], coords['z']]
            model_data['mesh'].add_node(int(node_id), coord_list)
    
    # 非線形材料を使用するmemberを追跡するための一時リスト
    nonlinear_member_info = []

    # memberセクションの読み込み（bar要素として扱う）
    if 'member' in data:
        for member_id, member_data in data['member'].items():
            ni = member_data['ni']  # i端節点
            nj = member_data['nj']  # j端節点
            # 節点IDを整数に変換（文字列の場合）
            if isinstance(ni, str):
                ni = int(ni)
            if isinstance(nj, str):
                nj = int(nj)

            material_id = member_data.get('e', 1)  # 材料/断面ID
            # 材料IDも整数に変換
            if isinstance(material_id, str):
                material_id = int(material_id)

            # 非線形材料を使用する場合は情報を記録（後で処理）
            nonlinear_member_info.append({
                'member_id': int(member_id),
                'material_id': material_id
            })

            model_data['mesh'].add_element(
                int(member_id),
                'bar',
                [ni, nj],
                material_id,
                member_id=int(member_id)  # 部材IDを保存
            )
    
    # shellセクションの読み込み（shell要素として扱う）
    if 'shell' in data:
        for shell_id, shell_data in data['shell'].items():
            nodes = shell_data['nodes']  # 4節点のリスト
            # 節点IDリストを文字列から整数に変換
            nodes = [int(node_id) for node_id in nodes]
            
            material_id = shell_data.get('e', 1)  # 材料ID
            # 材料IDも文字列の場合があるので整数に変換
            if isinstance(material_id, str):
                material_id = int(material_id)
            
            # 材料情報からthicknessを取得
            thickness = 0.01  # デフォルト厚さ
            if 'element' in data:
                for _, elem_defs in data['element'].items():
                    for mat_id_str, elem_def in elem_defs.items():
                        if int(mat_id_str) == material_id:
                            thickness = elem_def.get('thickness', 0.01)
                            break
            
            model_data['mesh'].add_element(
                int(shell_id),
                'shell',
                nodes,
                material_id,
                thickness=thickness,
                shell_id=int(shell_id)  # shell IDを保存
            )
    
    # solidセクションの読み込み（solid要素として扱う）
    if 'solid' in data:
        for solid_id, solid_data in data['solid'].items():
            nodes = solid_data['nodes']  # 4節点（tetra）, 6節点（wedge）, 8節点（hexa）のリスト
            # 節点IDリストを文字列から整数に変換
            nodes = [int(node_id) for node_id in nodes]
            
            material_id = solid_data.get('e', 1)  # 材料ID
            # 材料IDも文字列の場合があるので整数に変換
            if isinstance(material_id, str):
                material_id = int(material_id)
            
            # typeフィールドが明示的に指定されている場合はそれを使用
            if 'type' in solid_data:
                element_type = solid_data['type']
            else:
                # 節点数により要素タイプを判定（後方互換性）
                element_type = 'tetra' if len(nodes) == 4 else 'hexa' if len(nodes) == 8 else 'wedge' if len(nodes) == 6 else 'solid'
            
            model_data['mesh'].add_element(
                int(solid_id),
                element_type,
                nodes,
                material_id,
                solid_id=int(solid_id)  # solid IDを保存
            )
    
    # 旧形式の材料情報をelementセクションから読み込む
    # 非線形材料データも同時に処理
    nonlinear_materials = {}  # {material_id: {'params': NonlinearMaterialProperty, 'hysteresis_dofs': [...]}}

    if 'element' in data:
        for _, elem_defs in data['element'].items():
            for mat_id_str, elem_def in elem_defs.items():
                material_id = int(mat_id_str)
                mp = MaterialProperty(
                    name=elem_def.get('n', f"Material{material_id}"),
                    E=elem_def['E'],
                    nu=elem_def.get('nu', 0.3),
                    density=elem_def.get('den')
                )
                model_data['material'].add_material(material_id, mp)

                # BarParameterオブジェクトも追加（梁要素の断面特性）
                bp = BarParameter(
                    area=elem_def.get('A', 1.0),     # 断面積
                    Iy=elem_def.get('Iy', 1.0),      # 断面二次モーメント(y軸周り)
                    Iz=elem_def.get('Iz', 1.0),      # 断面二次モーメント(z軸周り)
                    J=elem_def.get('J', 1.0)         # ねじり定数
                )
                model_data['material'].add_bar_parameter(material_id, bp)

                # 非線形材料データの読み込み
                if 'nonlinear' in elem_def:
                    nl_data = elem_def['nonlinear']
                    nl_type = nl_data.get('type', 'jr_stiffness_reduction')

                    if nl_type == 'jr_stiffness_reduction':
                        # 対称スケルトンカーブかどうか
                        symmetric = nl_data.get('symmetric', True)

                        # 正側パラメータ
                        delta_1 = nl_data.get('delta_1', 0.001)
                        delta_2 = nl_data.get('delta_2', 0.01)
                        delta_3 = nl_data.get('delta_3', 0.1)
                        P_1 = nl_data.get('P_1', 100.0)
                        P_2 = nl_data.get('P_2', 500.0)
                        P_3 = nl_data.get('P_3', 550.0)

                        # 負側パラメータ（非対称の場合）
                        if symmetric:
                            delta_1_neg = delta_1
                            delta_2_neg = delta_2
                            delta_3_neg = delta_3
                            P_1_neg = P_1
                            P_2_neg = P_2
                            P_3_neg = P_3
                        else:
                            delta_1_neg = nl_data.get('delta_1_neg', delta_1)
                            delta_2_neg = nl_data.get('delta_2_neg', delta_2)
                            delta_3_neg = nl_data.get('delta_3_neg', delta_3)
                            P_1_neg = nl_data.get('P_1_neg', P_1)
                            P_2_neg = nl_data.get('P_2_neg', P_2)
                            P_3_neg = nl_data.get('P_3_neg', P_3)

                        beta = nl_data.get('beta', 0.4)
                        K_min = nl_data.get('K_min', None)

                        # NonlinearMaterialPropertyを作成
                        nl_mat = NonlinearMaterialProperty(
                            name=elem_def.get('n', f"Nonlinear{material_id}"),
                            E=elem_def['E'],
                            nu=elem_def.get('nu', 0.3),
                            delta_1_pos=delta_1,
                            delta_2_pos=delta_2,
                            delta_3_pos=delta_3,
                            P_1_pos=P_1,
                            P_2_pos=P_2,
                            P_3_pos=P_3,
                            delta_1_neg=delta_1_neg,
                            delta_2_neg=delta_2_neg,
                            delta_3_neg=delta_3_neg,
                            P_1_neg=P_1_neg,
                            P_2_neg=P_2_neg,
                            P_3_neg=P_3_neg,
                            beta=beta,
                            K_min=K_min,
                            density=elem_def.get('den')
                        )

                        model_data['material'].add_nonlinear_material(material_id, nl_mat)

                        # 履歴を適用する自由度
                        hysteresis_dofs = nl_data.get('hysteresis_dofs', ['moment_z'])
                        nonlinear_materials[material_id] = {
                            'hysteresis_dofs': hysteresis_dofs
                        }

                        print(f"非線形材料を読み込みました: material_id={material_id}, "
                              f"P=({P_1}, {P_2}, {P_3}), delta=({delta_1}, {delta_2}, {delta_3}), "
                              f"hysteresis_dofs={hysteresis_dofs}")

    # 非線形材料を使用するmemberの要素タイプを'nonlinear_bar'に変更
    for member_info in nonlinear_member_info:
        member_id = member_info['member_id']
        material_id = member_info['material_id']

        if material_id in nonlinear_materials:
            # 要素タイプを'nonlinear_bar'に変更
            if member_id in model_data['mesh'].elements:
                elem_data = model_data['mesh'].elements[member_id]
                elem_data['type'] = 'nonlinear_bar'
                elem_data['section_id'] = material_id
                elem_data['hysteresis_dofs'] = nonlinear_materials[material_id]['hysteresis_dofs']
                elem_data['shear_correction'] = True
                print(f"要素{member_id}を非線形要素に変換: material_id={material_id}, "
                      f"hysteresis_dofs={elem_data['hysteresis_dofs']}")
    
    # デフォルト材料を追加（材料が一つも読み込まれなかった場合）
    if len(model_data['material'].materials) == 0:
        # デフォルト材料を設定
        default_material = MaterialProperty(
            name="Default Steel",
            E=2.05e11,  # Pa
            nu=0.3,
            density=7850.0  # kg/m³
        )
        model_data['material'].add_material(1, default_material)
    
    # 拘束条件の読み込み（fix_nodeセクション）
    if 'fix_node' in data:
        for case_id, restraints in data['fix_node'].items():
            if not isinstance(restraints, list):
                continue
                
            for restraint in restraints:
                if 'n' not in restraint:
                    continue
                    
                node_id = int(restraint['n'])
                
                # 自由度の設定（旧形式では1が拘束、0が自由、>1000がバネ定数）
                tx_val = restraint.get('tx', 0)
                ty_val = restraint.get('ty', 0)
                tz_val = restraint.get('tz', 0)
                rx_val = restraint.get('rx', 0)
                ry_val = restraint.get('ry', 0)
                rz_val = restraint.get('rz', 0)
                
                # バネ定数（>1000）、拘束（1）、微小値拘束（0.01以上1未満）の判定
                dof_restraints = [
                    tx_val == 1 or tx_val > 1000 or (0.01 <= tx_val < 1),  # X方向並進
                    ty_val == 1 or ty_val > 1000 or (0.01 <= ty_val < 1),  # Y方向並進
                    tz_val == 1 or tz_val > 1000 or (0.01 <= tz_val < 1),  # Z方向並進
                    rx_val == 1 or rx_val > 1000 or (0.01 <= rx_val < 1),  # X軸周り回転
                    ry_val == 1 or ry_val > 1000 or (0.01 <= ry_val < 1),  # Y軸周り回転
                    rz_val == 1 or rz_val > 1000 or (0.01 <= rz_val < 1)   # Z軸周り回転
                ]
                
                # バネ定数の値を設定（>1000の場合はバネ、1の場合は0）
                values = [
                    tx_val if tx_val > 1000 else 0,
                    ty_val if ty_val > 1000 else 0,
                    tz_val if tz_val > 1000 else 0,
                    rx_val if rx_val > 1000 else 0,
                    ry_val if ry_val > 1000 else 0,
                    rz_val if rz_val > 1000 else 0
                ]
                
                # バネ定数が存在する場合のみvaluesを設定
                if any(v > 1000 for v in values):
                    model_data['boundary'].add_restraint(node_id, dof_restraints, values)
                else:
                    model_data['boundary'].add_restraint(node_id, dof_restraints, None)
    
    # 2Dフレーム構造の安定性確保（V1ロジック保護のため新実装のみ）
    if 'fix_node' in data:
        # rz拘束の確認
        has_rz_restraint = False
        for case_id, restraints in data['fix_node'].items():
            if not isinstance(restraints, list):
                continue
            for restraint in restraints:
                rz_val = restraint.get('rz', 0)
                if rz_val == 1 or rz_val > 1000 or (0.01 <= rz_val < 1):
                    has_rz_restraint = True
                    break
            if has_rz_restraint:
                break
        
        # rz拘束がない場合、構造安定性のため最低限の拘束を追加
        if not has_rz_restraint and model_data['boundary'].restraints:
            # 最初の拘束節点にrz拘束を追加
            first_node_id = list(model_data['boundary'].restraints.keys())[0]
            existing_restraint = model_data['boundary'].restraints[first_node_id]
            new_dof_restraints = list(existing_restraint.dof_restraints)
            new_dof_restraints[5] = True  # rz拘束を追加
            model_data['boundary'].restraints[first_node_id].dof_restraints = new_dof_restraints
    
    # notice_pointsセクションを追加（後でFemModelで処理）
    if 'notice_points' in data:
        model_data['notice_points'] = data['notice_points']
    
    # 荷重条件の読み込み（loadセクション）
    if 'load' in data:
        # loadセクション全体をmodel_dataに追加（FemModelで要素分割に使用）
        model_data['load'] = data['load']

        # 最初の荷重ケースから荷重データを境界条件に追加
        load_cases = data['load']
        if load_cases:
            # 最初の荷重ケースから解析パラメータを抽出
            first_case_key = list(load_cases.keys())[0]
            first_case = load_cases[first_case_key]
            model_data['analysis_params'] = {
                'n_load_steps': first_case.get('n_load_steps', 10),
                'max_iterations': first_case.get('max_iterations', 50),
                'tolerance': first_case.get('tolerance', 1e-6),
                'n_modes': first_case.get('n_modes', 10),
            }
            # 最初の荷重ケースを使用（通常は基本荷重ケース）
            first_case_key = list(load_cases.keys())[0]
            case_data = load_cases[first_case_key]
            
            # 節点荷重の処理（load_node）
            if 'load_node' in case_data and len(case_data['load_node']) > 0:
                for node_load in case_data['load_node']:
                    node_id = int(node_load['n'])
                    forces = [
                        node_load.get('tx', 0.0),  # fx
                        node_load.get('ty', 0.0),  # fy
                        node_load.get('tz', 0.0),  # fz
                        node_load.get('rx', 0.0),  # mx
                        node_load.get('ry', 0.0),  # my
                        node_load.get('rz', 0.0)   # mz
                    ]
                    model_data['boundary'].add_load(node_id, forces)
            
            # 要素荷重の処理（load_member）
            if 'load_member' in case_data and len(case_data['load_member']) > 0:
                # 要素荷重を等価節点荷重に変換して境界条件に追加
                _convert_element_loads_to_node_loads(case_data['load_member'], model_data)
    
    return model_data


def _read_fw3_model(lines: list[str]) -> Dict[str, Any]:
    """FW3フォーマットのモデルを読み込む（独自フォーマット）
    
    V0互換性を追加:
    - TriElement1（三角形Shell要素）
    - QuadElement1（四角形Shell要素）
    - その他のV0要素タイプ
    """
    model_data = {
        'mesh': MeshModel(),
        'boundary': BoundaryCondition(),
        'material': Material(),
        'section': Section()
    }
    
       
    mode = None
    for line in lines:
        line = line.strip()
        if not line or line.startswith('#'):
            continue
            
        # セクションの開始を検出
        if line.startswith('*'):
            mode = line[1:].lower()
            continue
            
        # 各セクションのデータを読み込み
        if mode == 'nodes':
            parts = line.split()
            if len(parts) >= 4:
                node_id = int(parts[0])
                coords = [float(parts[i]) for i in range(1, 4)]
                model_data['mesh'].add_node(node_id, coords)
                
        elif mode == 'elements':
            parts = line.split()
            if len(parts) >= 3:
                elem_id = int(parts[0])
                elem_type = parts[1]
                node_ids = [int(parts[i]) for i in range(2, len(parts))]
                
                # ✅ V0技術資産: 要素タイプ別のデフォルト材料設定
                if elem_type in ['TriElement1', 'QuadElement1', 'ShellElement']:
                    # Shell要素の場合、デフォルト厚さを設定
                    model_data['mesh'].add_element(elem_id, elem_type, node_ids, 1, thickness=0.01)
                    print(f"✅ V0互換: {elem_type}要素を読み込み (ID: {elem_id}, 節点: {node_ids})")
                elif elem_type in ['BarElement', 'BeamElement', 'TrussElement']:
                    # Bar要素の場合
                    model_data['mesh'].add_element(elem_id, elem_type, node_ids, 1)
                else:
                    # その他の要素
                    model_data['mesh'].add_element(elem_id, elem_type, node_ids, 1)
                
        elif mode == 'restraints':
            parts = line.split()
            if len(parts) >= 2:
                node_id = int(parts[0])
                dof_str = parts[1]
                dof_restraints = [False] * 6
                for i, char in enumerate(dof_str):
                    if i < 6 and char == '1':
                        dof_restraints[i] = True
                model_data['boundary'].add_restraint(node_id, dof_restraints)
                
        elif mode == 'loads':
            parts = line.split()
            if len(parts) >= 7:
                node_id = int(parts[0])
                forces = [float(parts[i]) for i in range(1, 7)]
                model_data['boundary'].add_load(node_id, forces)
                
    return model_data


def _read_fem_model(lines: list[str]) -> Dict[str, Any]:
    """V0の.femファイル形式を読み込む
    
    V0テストデータ互換性:
    - TriElement1 要素_ID 材料_ID パラメータ_ID 節点1 節点2 節点3
    - QuadElement1 要素_ID 材料_ID パラメータ_ID 節点1 節点2 節点3 節点4
    - BarElement 要素_ID 材料_ID パラメータ_ID 節点1 節点2
    """
    model_data = {
        'mesh': MeshModel(),
        'boundary': BoundaryCondition(),
        'material': Material(),
        'section': Section()
    }
    
    # デフォルト材料を追加
    default_material = MaterialProperty(
        name="Default Steel",
        E=2.05e11,  # Pa
        nu=0.3,
        density=7850.0  # kg/m³
    )
    model_data['material'].add_material(1, default_material)
           
    for line_num, line in enumerate(lines, 1):
        line = line.strip()
        if not line or line.startswith('#') or line.startswith('//'):
            continue
            
        parts = line.split()
        if len(parts) < 2:
            continue
            
        try:
            # ✅ V0技術資産: TriElement1の読み込み
            if parts[0].lower() == 'trielement1':
                # TriElement1 要素_ID 材料_ID パラメータ_ID 節点1 節点2 節点3
                if len(parts) >= 7:
                    elem_id = int(parts[1])
                    material_id = int(parts[2])
                    param_id = int(parts[3])  # シェルパラメータID
                    node_ids = [int(parts[4]), int(parts[5]), int(parts[6])]
                    
                    # 3節点三角形Shell要素として追加
                    model_data['mesh'].add_element(
                        elem_id, 'TriElement1', node_ids, material_id, 
                        thickness=0.01, param_id=param_id
                    )
                    print(f"✅ V0互換: TriElement1読み込み完了 (ID: {elem_id}, 節点: {node_ids})")
                    
            # ✅ V0技術資産: QuadElement1の読み込み
            elif parts[0].lower() == 'quadelement1':
                # QuadElement1 要素_ID 材料_ID パラメータ_ID 節点1 節点2 節点3 節点4
                if len(parts) >= 8:
                    elem_id = int(parts[1])
                    material_id = int(parts[2])
                    param_id = int(parts[3])
                    node_ids = [int(parts[4]), int(parts[5]), int(parts[6]), int(parts[7])]
                    
                    # 4節点四角形Shell要素として追加
                    model_data['mesh'].add_element(
                        elem_id, 'QuadElement1', node_ids, material_id,
                        thickness=0.01, param_id=param_id
                    )
                    print(f"✅ V0互換: QuadElement1読み込み完了 (ID: {elem_id}, 節点: {node_ids})")
                    
            # ✅ V0技術資産: BarElementの読み込み
            elif parts[0].lower() == 'barelement':
                # BarElement 要素_ID 材料_ID パラメータ_ID 節点1 節点2
                if len(parts) >= 6:
                    elem_id = int(parts[1])
                    material_id = int(parts[2])
                    param_id = int(parts[3])
                    node_ids = [int(parts[4]), int(parts[5])]
                    
                    model_data['mesh'].add_element(
                        elem_id, 'BarElement', node_ids, material_id,
                        param_id=param_id
                    )
                    
            # 節点の読み込み（Node 節点_ID x y z 形式）
            elif parts[0].lower() == 'node':
                if len(parts) >= 5:
                    node_id = int(parts[1])
                    coords = [float(parts[2]), float(parts[3]), float(parts[4])]
                    model_data['mesh'].add_node(node_id, coords)
                    
            # 材料の読み込み（Material 材料_ID 名前 E nu density）
            elif parts[0].lower() == 'material':
                if len(parts) >= 6:
                    material_id = int(parts[1])
                    name = parts[2]
                    E = float(parts[3])
                    nu = float(parts[4])
                    density = float(parts[5])
                    
                    material = MaterialProperty(
                        name=name,
                        E=E,
                        nu=nu,
                        density=density
                    )
                    model_data['material'].add_material(material_id, material)
                    
            # 拘束の読み込み（Restraint 節点_ID dx dy dz rx ry rz）
            elif parts[0].lower() == 'restraint':
                if len(parts) >= 8:
                    node_id = int(parts[1])
                    dof_restraints = [bool(int(parts[i])) for i in range(2, 8)]
                    model_data['boundary'].add_restraint(node_id, dof_restraints)
                    
            # 荷重の読み込み（Load 節点_ID fx fy fz mx my mz）
            elif parts[0].lower() == 'load':
                if len(parts) >= 8:
                    node_id = int(parts[1])
                    forces = [float(parts[i]) for i in range(2, 8)]
                    model_data['boundary'].add_load(node_id, forces)
                    
            # 面圧の読み込み（Pressure 要素_ID 面番号 圧力値）
            elif parts[0].lower() == 'pressure':
                if len(parts) >= 4:
                    element_id = int(parts[1])
                    face = parts[2]  # "F1", "F2"など
                    pressure = float(parts[3])
                    model_data['boundary'].add_pressure(element_id, face, pressure)
                    print(f"✅ 面圧条件読み込み完了: 要素{element_id}, 面{face}, 圧力{pressure}")
                    
        except (ValueError, IndexError) as e:
            print(f"警告: .femファイル {line_num}行目の読み込みでエラー: {e}")
            print(f"問題のある行: {line}")
            continue
            
    # 読み込み結果のサマリー
    n_nodes = len(model_data['mesh'].nodes)
    n_elements = len(model_data['mesh'].elements)
    n_tri_elements = sum(1 for elem in model_data['mesh'].elements.values() 
                        if elem['type'] == 'TriElement1')
    n_quad_elements = sum(1 for elem in model_data['mesh'].elements.values() 
                         if elem['type'] == 'QuadElement1')
    
    print(f"📊 .fem読み込み完了: 節点{n_nodes}個, 要素{n_elements}個 "
          f"(TriElement1: {n_tri_elements}, QuadElement1: {n_quad_elements})")
    
    return model_data


def write_model(model_data: Dict[str, Any], file_path: str) -> None:
    """モデルデータをファイルに書き込む
    
    Args:
        model_data: モデルデータ
        file_path: 出力ファイルパス
    """
    ext = os.path.splitext(file_path)[1].lower()
    
    if ext == '.json':
        _write_json_model(model_data, file_path)
    elif ext == '.fw3':
        _write_fw3_model(model_data, file_path)
    else:
        raise ValueError(f"Unsupported file format: {ext}")


def _write_json_model(model_data: Dict[str, Any], file_path: str) -> None:
    """JSONフォーマットでモデルを書き込む"""
    output_data = {}
    
    # メッシュデータ
    mesh = model_data.get('mesh')
    if mesh:
        output_data['nodes'] = {
            str(node_id): coords.tolist() 
            for node_id, coords in mesh.nodes.items()
        }
        output_data['elements'] = {
            str(elem_id): elem_data 
            for elem_id, elem_data in mesh.elements.items()
        }
        
    # 材料データ
    material = model_data.get('material')
    if material:
        output_data['materials'] = {
            str(mat_id): {
                'name': mat.name,
                'E': mat.E,
                'nu': mat.nu,
                'density': mat.density,
                'alpha': mat.alpha,
                'k': mat.k,
                'c': mat.c
            }
            for mat_id, mat in material.materials.items()
        }
        
    # 境界条件
    boundary = model_data.get('boundary')
    if boundary:
        output_data['boundary_conditions'] = {}
        
        if boundary.restraints:
            output_data['boundary_conditions']['restraints'] = {
                str(node_id): {
                    'dof': restraint.dof_restraints,
                    'values': restraint.values
                }
                for node_id, restraint in boundary.restraints.items()
            }
            
        if boundary.loads:
            output_data['boundary_conditions']['loads'] = {
                str(node_id): load.forces.tolist()
                for node_id, load in boundary.loads.items()
            }
            
        # 面圧条件の書き込み（新規追加）
        if boundary.pressures:
            output_data['boundary_conditions']['pressures'] = [
                {
                    'element_id': pressure.element_id,
                    'face': pressure.face,
                    'pressure': pressure.pressure
                }
                for pressure in boundary.pressures
            ]
            
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(output_data, f, indent=2, ensure_ascii=False)


def _write_fw3_model(model_data: Dict[str, Any], file_path: str) -> None:
    """FW3フォーマットでモデルを書き込む"""
    with open(file_path, 'w', encoding='utf-8') as f:
        # ヘッダー
        f.write("# FrameWeb3 Model File\n")
        f.write(f"# Generated by fem.file_io module\n\n")
        
        # ノードデータ
        mesh = model_data.get('mesh')
        if mesh and mesh.nodes:
            f.write("*NODES\n")
            for node_id, coords in sorted(mesh.nodes.items()):
                f.write(f"{node_id} {coords[0]:.6f} {coords[1]:.6f} {coords[2]:.6f}\n")
            f.write("\n")
            
        # 要素データ
        if mesh and mesh.elements:
            f.write("*ELEMENTS\n")
            for elem_id, elem_data in sorted(mesh.elements.items()):
                nodes_str = ' '.join(str(n) for n in elem_data['nodes'])
                f.write(f"{elem_id} {elem_data['type']} {nodes_str}\n")
            f.write("\n")
            
        # 境界条件
        boundary = model_data.get('boundary')
        if boundary:
            # 拘束条件
            if boundary.restraints:
                f.write("*RESTRAINTS\n")
                for node_id, restraint in sorted(boundary.restraints.items()):
                    dof_str = ''.join('1' if r else '0' for r in restraint.dof_restraints)
                    f.write(f"{node_id} {dof_str}\n")
                f.write("\n")
                
            # 荷重条件
            if boundary.loads:
                f.write("*LOADS\n")
                for node_id, load in sorted(boundary.loads.items()):
                    forces_str = ' '.join(f"{f:.6f}" for f in load.forces)
                    f.write(f"{node_id} {forces_str}\n")
                    
            # 面圧条件（新規追加）
            if boundary.pressures:
                f.write("*PRESSURES\n")
                for pressure in boundary.pressures:
                    f.write(f"{pressure.element_id} {pressure.face} {pressure.pressure:.6f}\n")


def read_result(file_path: str) -> Dict[str, Any]:
    """結果ファイルを読み込む
    
    Args:
        file_path: 結果ファイルパス
        
    Returns:
        結果データの辞書
    """
    ext = os.path.splitext(file_path)[1].lower()
    
    if ext == '.json':
        with open(file_path, 'r', encoding='utf-8') as f:
            return json.load(f)
    else:
        raise ValueError(f"Unsupported result file format: {ext}")


def write_result(result_data: Dict[str, Any], file_path: str) -> None:
    """結果データをファイルに書き込む
    
    Args:
        result_data: 結果データ
        file_path: 出力ファイルパス
    """
    # NumPy配列をリストに変換
    def convert_arrays(obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        elif isinstance(obj, dict):
            return {k: convert_arrays(v) for k, v in obj.items()}
        elif isinstance(obj, list):
            return [convert_arrays(item) for item in obj]
        else:
            return obj
            
    output_data = convert_arrays(result_data)
    
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(output_data, f, indent=2, ensure_ascii=False)


def _convert_element_loads_to_node_loads(load_members: List[Dict], model_data: Dict[str, Any]) -> None:
    """要素荷重を等価節点荷重に変換して境界条件に追加
    
    Args:
        load_members: load_memberデータのリスト
        model_data: モデルデータ辞書
    """
    mesh = model_data['mesh']
    boundary = model_data['boundary']
    
    print(f"🔍 分布荷重処理開始: {len(load_members)}個の要素荷重を処理")
    processed_count = 0
    skipped_count = 0
    total_loads_added = 0
    
    for i, load_member in enumerate(load_members):
        # デバッグ出力（最初の10個のみ詳細表示）
        debug_this = i < 10
        
        # 要素ID取得
        member_id = load_member.get('m')
        if member_id is None:
            if debug_this:
                print(f"  荷重{i+1}: member_id不明 - スキップ")
            skipped_count += 1
            continue
        member_id = int(member_id)
        
        # 荷重値とパラメータ取得
        P1 = load_member.get('P1', 0.0)
        P2 = load_member.get('P2', 0.0)
        L1 = load_member.get('L1', 0.0)
        L2 = load_member.get('L2', 0.0)
        direction = load_member.get('direction', 'y')
        mark = load_member.get('mark', 0)
        
        if debug_this:
            print(f"  荷重{i+1}: 要素{member_id}, P1={P1}, P2={P2}, L1={L1}, L2={L2}, direction={direction}, mark={mark}")
        
        # 要素データ取得
        element_data = None
        for elem_id, elem_data in mesh.elements.items():
            if elem_id == member_id or elem_data.get('member_id') == member_id:
                element_data = elem_data
                break
        
        if element_data is None:
            if debug_this:
                print(f"    → 要素{member_id}が見つからない - スキップ")
            skipped_count += 1
            continue
            
        if element_data['type'] != 'bar':
            if debug_this:
                print(f"    → 要素{member_id}はbar要素ではない({element_data['type']}) - スキップ")
            skipped_count += 1
            continue  # bar要素のみ対応
            
        # 荷重タイプ確認（mark=2は分布荷重）
        if mark != 2:
            if debug_this:
                print(f"    → mark={mark}なので分布荷重ではない - スキップ")
            skipped_count += 1
            continue  # 分布荷重のみ対応
            
        if P1 == 0.0 and P2 == 0.0:
            if debug_this:
                print(f"    → P1={P1}, P2={P2}なので荷重値ゼロ - スキップ")
            skipped_count += 1
            continue  # 荷重値が0の場合はスキップ
        
        # 要素の節点ID取得
        node_ids = element_data['nodes']
        if len(node_ids) != 2:
            if debug_this:
                print(f"    → 節点数{len(node_ids)}なので2節点要素ではない - スキップ")
            skipped_count += 1
            continue
            
        node_i_id, node_j_id = node_ids[0], node_ids[1]
        
        # 要素長計算
        node_i = mesh.nodes[node_i_id]
        node_j = mesh.nodes[node_j_id]
        dx = node_j[0] - node_i[0]
        dy = node_j[1] - node_i[1]
        dz = node_j[2] - node_i[2]
        element_length = (dx*dx + dy*dy + dz*dz)**0.5
        
        if element_length < 1e-12:
            if debug_this:
                print(f"    → 要素長{element_length}がゼロ - スキップ")
            skipped_count += 1
            continue  # ゼロ長要素はスキップ
        
        # 荷重範囲計算（全長に分布の場合: L1=0, L2=0）
        if L1 == 0.0 and L2 == 0.0:
            load_length = element_length  # 全長に分布
        else:
            load_length = element_length - L1 - abs(L2) if L2 < 0 else element_length - L1 - L2
            if load_length <= 0:
                load_length = element_length  # 全長に分布
        
        # 台形分布荷重の等価節点荷重（単位：荷重値×長さ）
        total_load = (P1 + P2) * load_length / 2.0  # 台形の面積
        
        # 各節点への配分（均等分布と仮定）
        force_i = total_load / 2.0
        force_j = total_load / 2.0
        
        if debug_this:
            print(f"    → 要素長={element_length:.4f}, 荷重長={load_length:.4f}")
            print(f"    → 全荷重={total_load:.4f}, 節点i荷重={force_i:.4f}, 節点j荷重={force_j:.4f}")
        
        # 方向別に荷重成分を設定
        force_components_i = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0]  # [fx, fy, fz, mx, my, mz]
        force_components_j = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
        
        if direction.lower() == 'x':
            force_components_i[0] = force_i
            force_components_j[0] = force_j
        elif direction.lower() == 'y':
            force_components_i[1] = force_i
            force_components_j[1] = force_j
        elif direction.lower() == 'z':
            force_components_i[2] = force_i
            force_components_j[2] = force_j
        else:
            # デフォルトはY方向
            force_components_i[1] = force_i
            force_components_j[1] = force_j
        
        if debug_this:
            print(f"    → 節点{node_i_id}に荷重{force_components_i}を追加")
            print(f"    → 節点{node_j_id}に荷重{force_components_j}を追加")
        
        # 既存の節点荷重に加算
        _add_node_load(boundary, node_i_id, force_components_i)
        _add_node_load(boundary, node_j_id, force_components_j)
        
        processed_count += 1
        total_loads_added += 2  # 2つの節点に荷重追加
    
    print(f"🔍 分布荷重処理完了: 処理済み={processed_count}個, スキップ={skipped_count}個, 追加節点荷重={total_loads_added}個")


def _add_node_load(boundary: BoundaryCondition, node_id: int, force_components: List[float]) -> None:
    """節点荷重を追加または既存の荷重に加算
    
    Args:
        boundary: 境界条件オブジェクト
        node_id: 節点ID
        force_components: 荷重成分 [fx, fy, fz, mx, my, mz]
    """
    # 既存の荷重をチェック
    if node_id in boundary.loads:
        # 既存の荷重に加算
        existing_forces = boundary.loads[node_id].forces
        new_forces = existing_forces + np.array(force_components)
        boundary.loads[node_id].forces = new_forces
    else:
        # 新規荷重として追加
        boundary.add_load(node_id, force_components)


def write_vtk(model_data: Dict[str, Any], result_data: Dict[str, Any], file_path: str) -> None:
    """VTK形式で解析結果を出力する"""
    from .vtk_writer import VTKWriter
    mesh = model_data.get('mesh')
    if mesh is None:
        raise ValueError("model_dataにmeshが含まれていません")
    nodes = mesh.nodes
    elements = mesh.elements
    writer = VTKWriter(file_path)
    writer.write_header()
    writer.write_points(nodes)
    writer.write_cells(elements)
    # 節点変位の出力
    node_disp = result_data.get('node_displacements', {})
    writer.write_point_data({'displacement': node_disp})
    # 要素応力の出力
    elem_stress = result_data.get('element_stresses', {})
    writer.write_cell_data({'stress': elem_stress})
    writer.write_footer() 