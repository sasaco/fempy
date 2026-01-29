from typing import Optional
import numpy as np
from helper import isInt, isFloat, convInt
from utils import get_nodeIndex, get_memIndex, get_combIndex, \
    get_secIndex, get_matIndex, get_thickIndex
from inputDataUtils import devide_byRigid, devide_byPoints, devide_byElemLoads, \
    make_supports, make_joints, make_springs, make_sectionMaterial, add_sectionMaterial, \
    make_eNodeLoads, make_eElemLoads, make_heatLoads
from error_handling import logger, MyError, MyCritical
from components.node import Node, get_coordinate, find_nodeIndex
from components.section_material import Section, Material, Thickness
from components.member import Beam, Member
from components.shell import Shell
from components.solid import Solid
from components.load import NodeLoad, CaseComb, LoadCase, NodeLoad, ForcedDisp
from components.support import Support
from components.spring import Spring
from components.joint import Joint


# >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
# ＜入力データの整理方針＞
# (A)梁要素の分割
#    材料特性ケース、支点ケース、要素分布バネケース、材端条件ケースが同じ場合に
#    剛性行列を使いまわせるよう、全ての荷重ケースの要素荷重の作用位置で部材を分割
#    してしまう。これを実現するために下記の順番で処理する。
#    (1)剛域で部材を分割
#    (2)着目点で部材を分割
#    (3)全ての荷重ケースの要素荷重の作用位置で部材を分割
#    (4)荷重ケースごとに要素荷重を分割された梁要素に割り振る
# (B)2Dモードの場合の処理
#    2Dモードの場合でも3Dモードの場合とフレーム計算の処理を同一にするため、せん断
#    弾性係数等の2Dモードで入力の無いデータには仮の値を設定するとともに、全節点に
#    対してZ方向変位、X軸回り回転、Y軸回り回転拘束の支点を設定する。
# (C)材料特性のデータの保持方法
#    材料特性は今後、材料、断面、厚さの入力が分かれる可能性を考慮し、この3種類に
#    分割した形でデータを保持する。
# <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<


class InputData:
    """入力データクラス
    
    @brief 構造解析用入力データの検証・変換・管理を行うクラス
    
    JSONで受け取った構造モデルデータを解析エンジンで使用可能な
    内部データ構造に変換します。以下の機能を提供：
    
    - 節点・部材・シェル・ソリッド要素の定義
    - 材料・断面・厚さ特性の管理
    - 支点・バネ・結合条件の設定
    - 荷重ケースとケース組み合わせの管理
    - データ検証とエラーハンドリング
    
    @note 2D/3D解析モードの自動判定
    @note 部材の自動分割（剛域・着目点・荷重点）

    Properties:
        mode (int):2Dか3Dか{2 or 3}
        nodes (list[Node]):節点データ
        sectionCases (dict[int, list[Section]]):断面ケースデータ
        materialCases (dict[int, list[Material]):材料ケースデータ
        thicknessCases (dict[int, list[Thickness]]):厚さケースデータ
        members (list[Member]):部材データ
        beams (list[Beam]):梁要素データ
        shells (list[Shell]):シェル要素データ
        caseCombs (list[CaseComb]):ケース組合せデータ
        loadCases (list[LoadCase]):基本荷重ケースデータ
        supportCases (dict[int, list[Support]]):支点ケースデータ
        springCases (dict[int, list[Spring]]):要素分布バネケースデータ
        jointCases (dict[int, list[Joint]]):材端条件ケースデータ
    """
    def __init__(self, inputJson: dict) -> None:
        """入力データクラス

        @brief 入力JSONから構造解析用データを構築
        
        @param inputJson 構造モデルの定義データ（JSON形式）
                        以下のキーを含む：
                        - node: 節点座標定義
                        - member: 梁部材定義
                        - shell: シェル要素定義（オプション）
                        - solid: ソリッド要素定義（オプション）
                        - element: 材料・断面特性定義
                        - thickness: 厚さ定義（オプション）
                        - fix_node: 支点条件定義（オプション）
                        - fix_member: バネ支点定義（オプション）
                        - joint: 材端条件定義（オプション）
                        - load: 荷重ケース定義
        
        @throws MyError 入力データの検証エラー時
        @throws MyCritical システムエラー時

        Args:
            inputJson (dict): json形式の入力データ
        """
        # region モード
        if ('dimension' in inputJson) and (isInt(inputJson['dimension'])):
            self.mode = convInt(inputJson['dimension'])
            if self.mode not in [2, 3]:  # 不正な値なら3次元モードと判定
                self.mode = 3
                logger.warning(f"{str(self.mode)}次元モードに設定されました")
        else:  # 不正な値なら3次元モードと判定
            self.mode = 3
            logger.warning(f"{str(self.mode)}次元モードに設定されました")
        # endregion
        
        # region 節点（入力データにあるもの）
        self.nodes: list[Node] = []
        if 'node' in inputJson:
            for nStr in inputJson['node'].keys():
                if not isInt(nStr):  # 整数化できない節点番号のデータはスキップ
                    errMsg = f"節点番号が不正な節点データを無視しました: 節点({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                nodeTmp = inputJson['node'][nStr]
                # 節点座標の取得
                xTmp: Optional[float] = get_coordinate(nodeTmp['x']) if 'x' in nodeTmp else 0.0
                yTmp: Optional[float] = get_coordinate(nodeTmp['y']) if 'y' in nodeTmp else 0.0
                zTmp: Optional[float] = get_coordinate(nodeTmp['z']) if ('z' in nodeTmp) and (self.mode == 3) else 0.0
                if (xTmp is None) or (yTmp is None) or (zTmp is None):  # 座標が非数のデータはスキップ
                    errMsg = f"座標値が不正な節点データを無視しました: 節点番号({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                # 重複節点のチェック
                iNodeCheck = find_nodeIndex(xTmp, yTmp, zTmp, self.nodes)
                if iNodeCheck is not None:
                    errMsg = "同一座標に複数の節点が設定されています"
                    logger.error(errMsg + f": 節点({str(nStr)}, {str(self.nodes[iNodeCheck].nNode)})")
                    raise MyError(errMsg, node=self.nodes[iNodeCheck])
                # 節点データの作成
                nodeNew = Node(xTmp, yTmp, zTmp)
                nodeNew.isNode = True  # 入力データに含まれる節点であることのフラグ
                nodeNew.nNode = convInt(nStr)  # 節点番号を設定
                self.nodes.append(nodeNew)
            if len(self.nodes) == 0:
                errMsg = "節点データがありません"
                logger.error(errMsg)
                raise MyError(errMsg)
        else:
            errMsg = "節点データがありません"
            logger.error(errMsg)
            raise MyError(errMsg)
        # endregion

        # region 材料特性（断面、厚さ、材料）
        self.sectionCases: dict[int, list[Section]] = {}  # 断面
        self.materialCases: dict[int, list[Material]] = {}  # 材料
        self.thicknessCases: dict[int, list[Thickness]] = {}  # 厚さ
        if 'element' in inputJson:
            for nCaseStr in inputJson['element'].keys():
                if not isInt(nCaseStr):  # 整数化できない材料特性ケース番号のデータはスキップ
                    errMsg = f"ケース番号が不正な材料特性データを無視しました: 材料特性({str(nCaseStr)})"
                    logger.warning(errMsg)
                    continue
                nCase = convInt(nCaseStr)  # 材料特性ケース番号（断面、材料、厚さで共通）
                # 断面、材料、厚さデータの作成
                if len(self.sectionCases.keys()) == 0:  # 1つ目の材料特性ケース
                    sections, materials, thicknesses = make_sectionMaterial(inputJson['element'][nCaseStr], self.mode)
                else:  # 2つ目以降の材料特性ケース
                    # 2つ目以降の材料特性ケースで入力が無い項目は1つ目の材料特性ケースの値を踏襲させる
                    sections, materials, thicknesses = add_sectionMaterial\
                        (list(self.sectionCases.values())[0], list(self.materialCases.values())[0], list(self.thicknessCases.values())[0],\
                         inputJson['element'][nCaseStr], self.mode)               
                if (len(materials) == 0): # or (len(sections) == 0)  # 有効なデータが無い材料特性ケースはスキップ（厚さデータは無くてもよい）
                    errMsg = f"有効な入力の無い材料特性データを無視しました: 材料特性番号({nCase})"
                    logger.warning(errMsg)
                    continue
                # 断面、材料、厚さの登録
                self.sectionCases[nCase] = sections
                self.materialCases[nCase] = materials
                self.thicknessCases[nCase] = thicknesses
            if (len(self.sectionCases.keys()) == 0) or (len(self.materialCases.keys()) == 0) or (len(self.thicknessCases.keys()) == 0):
                errMsg = "材料特性データがありません"
                logger.error(errMsg)
                raise MyError(errMsg)
            # ケース1が無い場合はダミーデータを設定
            if 1 not in self.sectionCases.keys():
                self.sectionCases[1] = []
            if 1 not in self.materialCases.keys():
                self.materialCases[1] = []
            if 1 not in self.thicknessCases.keys():
                self.thicknessCases[1] = []
        else:
            errMsg = "材料特性データがありません"
            logger.error(errMsg)
            raise MyError(errMsg)
        #endregion

        # region 部材（部材と梁要素）
        self.members: list[Member] = []
        self.beams: list[Beam] = []
        if 'member' in inputJson:
            for nStr in inputJson['member']:
                if not isInt(nStr):  # 整数化できない部材番号のデータはスキップ
                    errMsg = f"部材番号が不正な部材データを無視しました: 部材({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                memTmp = inputJson['member'][nStr]
                # 節点番号、材料特性番号、コードアングルの取得
                ni = convInt(memTmp['ni']) if ('ni' in memTmp) and (isInt(memTmp['ni'])) else None
                nj = convInt(memTmp['nj']) if ('nj' in memTmp) and (isInt(memTmp['nj'])) else None
                mat = convInt(memTmp['e']) if ('e' in memTmp) and (isInt(memTmp['e'])) else None
                angle = float(memTmp['cg']) if ('cg' in memTmp) and (isFloat(memTmp['cg'])) and (self.mode == 3) else 0.0
                if (ni is None) or (nj is None) or (mat is None):  # i端節点、j端節点、材料特性のいずれかが未入力ならスキップ
                    errMsg = f"入力不足の部材データを無視しました: 部材番号({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                # 節点インデックス、断面インデックス、材料インデックスの取得
                indI = get_nodeIndex(ni, self.nodes)
                indJ = get_nodeIndex(nj, self.nodes)
                iSec = get_secIndex(mat, list(self.sectionCases.values())[0])
                iMat = get_matIndex(mat, list(self.materialCases.values())[0])
                if (indI is None) or (indJ is None) or (iSec is None) or (iMat is None):
                    errMsg = "部材データ作成時に予期せぬエラーが発生しました"
                    logger.critical(errMsg + f": 部材番号{str(nStr)}")
                    raise MyCritical(errMsg)
                # 部材データの作成
                memNew = Member(convInt(nStr), indI, indJ, iSec, iMat, angle, self.nodes, self.beams)
                self.members.append(memNew)
            if len(self.members) == 0:
                errMsg = "部材データがありません"
                logger.error(errMsg)
                raise MyError(errMsg)
            # endregion
        else:
            errMsg = "部材データがありません"
            logger.info(errMsg)
            # raise MyError(errMsg)

        # region 剛域（節点追加、梁要素分割、材料特性インデックスの入れ替え）
        if 'rigid' in inputJson:
            devide_byRigid(inputJson['rigid'], self.members, \
                        list(self.sectionCases.values())[0], \
                        list(self.materialCases.values())[0])
        # endregion

        # region 着目点（節点追加、梁要素分割）
        if 'notice_points' in inputJson:
            devide_byPoints(inputJson['notice_points'], self.members)
        # endregion


        # region シェル要素
        self.shells: list[Shell] = []
        if (self.mode == 3) and ('shell' in inputJson):
            for nStr in inputJson['shell']:
                if not isInt(nStr):
                    errMsg = f"パネル番号が不正なパネルデータを無視しました: パネル({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                shellTmp = inputJson['shell'][nStr]
                if ('nodes' in shellTmp) and (len(shellTmp['nodes']) >= 3):
                    shellNodes: list = shellTmp['nodes']
                    n1 = convInt(shellNodes[0]) if isInt(shellNodes[0]) else None
                    n2 = convInt(shellNodes[1]) if isInt(shellNodes[1]) else None
                    n3 = convInt(shellNodes[2]) if isInt(shellNodes[2]) else None
                    is_triangle = len(shellTmp['nodes']) == 3
                    n4 = None if is_triangle else convInt(shellNodes[3]) if len(shellTmp['nodes']) > 3 and isInt(shellNodes[3]) else None
                else:
                    n1 = n2 = n3 = n4 = None
                    is_triangle = False
                
                mat = convInt(shellTmp['e']) if ('e' in shellTmp) and (isInt(shellTmp['e'])) else None
                
                if is_triangle:
                    if (n1 is None) or (n2 is None) or (n3 is None) or (mat is None):
                        # 有効な節点番号3つと材料特性が未入力の場合はスキップ
                        errMsg = f"入力不足のパネルデータを無視しました: パネル番号({str(nStr)})"
                        logger.warning(errMsg)
                        continue
                else:
                    if (n1 is None) or (n2 is None) or (n3 is None) or (n4 is None) or (mat is None):
                        # 有効な節点番号4つと材料特性が未入力の場合はスキップ
                        errMsg = f"入力不足のパネルデータを無視しました: パネル番号({str(nStr)})"
                        logger.warning(errMsg)
                        continue
                
                # 節点インデックス、材料インデックス、厚さインデックスの取得
                i1 = get_nodeIndex(n1, self.nodes)
                i2 = get_nodeIndex(n2, self.nodes)
                i3 = get_nodeIndex(n3, self.nodes)
                i4 = None if is_triangle else (get_nodeIndex(n4, self.nodes) if n4 is not None else None)
                # 材料インデックスと厚さインデックスの取得
                iMat = get_matIndex(mat, list(self.materialCases.values())[0])
                thick_index = get_thickIndex(mat, list(self.thicknessCases.values())[0])
                iThick = 0 if thick_index is None else thick_index
                
                if is_triangle:
                    if (i1 is None) or (i2 is None) or (i3 is None) or (iMat is None) or (iThick is None):
                        errMsg = "パネルデータ作成時に予期せぬエラーが発生しました"
                        logger.critical(errMsg + f": パネル番号({str(nStr)})")
                        raise MyCritical(errMsg)
                    self.shells.append(Shell(convInt(nStr), i1, i2, i3, i4=None, iMat=iMat, iThick=iThick, nodes=self.nodes))
                else:
                    if (i1 is None) or (i2 is None) or (i3 is None) or (i4 is None) or (iMat is None) or (iThick is None):
                        errMsg = "パネルデータ作成時に予期せぬエラーが発生しました"
                        logger.critical(errMsg + f": パネル番号({str(nStr)})")
                        raise MyCritical(errMsg)
                    self.shells.append(Shell(convInt(nStr), i1, i2, i3, i4=i4, iMat=iMat, iThick=iThick, nodes=self.nodes))
        # endregion

        self.solids: list[Solid] = []
        if (self.mode == 3) and ('solid' in inputJson):
            for nStr in inputJson['solid']:
                if not isInt(nStr):
                    errMsg = f"ソリッド番号が不正なソリッドデータを無視しました: ソリッド({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                solidTmp = inputJson['solid'][nStr]
                
                element_type = solidTmp.get('type', 'tetra')
                mat = convInt(solidTmp['e']) if ('e' in solidTmp) and (isInt(solidTmp['e'])) else None
                
                if 'nodes' not in solidTmp:
                    errMsg = f"節点データが不足しているソリッドデータを無視しました: ソリッド番号({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                
                solidNodes: list = solidTmp['nodes']
                node_indices = []
                
                required_nodes = 4 if element_type == 'tetra' else 8 if element_type == 'hexa' else 0
                
                if len(solidNodes) != required_nodes:
                    errMsg = f"節点数が不正なソリッドデータを無視しました: ソリッド番号({str(nStr)}), 要素タイプ({element_type}), 節点数({len(solidNodes)}), 必要な節点数({required_nodes})"
                    logger.warning(errMsg)
                    continue
                
                valid_nodes = True
                for i, node_str in enumerate(solidNodes):
                    if not isInt(node_str):
                        valid_nodes = False
                        break
                    node_num = convInt(node_str)
                    node_idx = get_nodeIndex(node_num, self.nodes)
                    if node_idx is None:
                        valid_nodes = False
                        break
                    node_indices.append(node_idx)
                
                if not valid_nodes or mat is None:
                    errMsg = f"入力不足のソリッドデータを無視しました: ソリッド番号({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                
                # 材料インデックスの取得
                iMat = get_matIndex(mat, list(self.materialCases.values())[0])
                if iMat is None:
                    errMsg = "ソリッドデータ作成時に予期せぬエラーが発生しました"
                    logger.critical(errMsg + f": ソリッド番号({str(nStr)})")
                    raise MyCritical(errMsg)
                
                self.solids.append(Solid(convInt(nStr), node_indices, iMat, element_type))
        # endregion

        # region 荷重ケース（各種ケース組合せデータの作成）
        if 'load' in inputJson:
            self.caseCombs: list[CaseComb] = []  # ケース組合せ
            self.loadCases: list[LoadCase] = []  # 荷重ケース
            for id in inputJson['load'].keys():
                # ケース番号の取得（入力が無い場合は1とする）
                caseTmp = inputJson['load'][id]
                # 支点ケース
                nSupCase = convInt(caseTmp['fix_node']) if ('fix_node' in caseTmp) and (isInt(caseTmp['fix_node'])) else 1
                # 分布バネケース
                nSprCase = convInt(caseTmp['fix_member']) if ('fix_member' in caseTmp) and (isInt(caseTmp['fix_member'])) else 1
                # 材料特性ケース
                nMatCase = convInt(caseTmp['element']) if ('element' in caseTmp) and (isInt(caseTmp['element'])) else 1
                # 結合ケース
                nJntCase = convInt(caseTmp['joint']) if ('joint' in caseTmp) and (isInt(caseTmp['joint'])) else 1
                # ケース組合せインデックスの取得（既に登録されている場合）
                iCaseComb = get_combIndex(nSupCase, nSprCase, nMatCase, nJntCase, self.caseCombs)
                # 重複を避けるためまだ登録されていない場合のみケース組合せの追加
                if iCaseComb is None:
                    self.caseCombs.append(CaseComb(nSupCase, nSprCase, nMatCase, nJntCase))
                    iCaseComb = len(self.caseCombs) - 1
                # 荷重ケースの追加
                rate = float(caseTmp['rate']) if ('rate' in caseTmp) and isFloat(caseTmp['rate']) else 1.0
                symbol = str(caseTmp['symbol']) if 'symbol' in caseTmp else "LoadSymbol"
                self.loadCases.append(LoadCase(str(id), iCaseComb, rate, symbol))
        else:
            errMsg = "荷重データがありません"
            logger.error(errMsg)
            raise MyError(errMsg)
        # endregion

        # region 節点荷重（節点荷重データ作成）
        if 'load' in inputJson:
            for id in inputJson['load'].keys():
                caseTmp = inputJson['load'][id]
                # 荷重ケースidの取得
                if (not 'load_node' in caseTmp) or (len(caseTmp['load_node']) == 0):  # 節点荷重データが無い場合はスキップ
                    errMsg = f"節点荷重データが無い荷重ケース: 荷重ケース({str(id)})"
                    logger.info(errMsg)
                    continue
                loadId = str(id)
                # 該当する荷重ケースの取得
                pickedLoadCase = [obj for obj in self.loadCases if obj.id == loadId]
                if len(pickedLoadCase) == 1:
                    lCaseTmp = pickedLoadCase[0]
                else:
                    errMsg = "節点荷重データ作成時に予期せぬエラーが発生しました"
                    logger.critical(errMsg + f": 荷重ケース({loadId})")
                    raise MyCritical(errMsg)
                # 節点荷重の登録
                for nodeLoadTmp in caseTmp['load_node']:
                    # 載荷点節点の取得
                    if ('n' in nodeLoadTmp) and isInt(nodeLoadTmp['n']):
                        iNode = get_nodeIndex(convInt(nodeLoadTmp['n']), self.nodes)
                    else:  # 載荷節点が不定のデータはスキップ
                        errMsg = f"載荷節点が存在しない節点荷重データを無視しました: 荷重ケース({loadId})"
                        logger.warning(errMsg)
                        continue
                    if iNode is None:  # 載荷節点が存在しないデータはスキップ
                        errMsg = f"載荷節点が存在しない節点荷重データを無視しました: 節点({str(nodeLoadTmp['n'])}) 荷重ケース({loadId})"
                        logger.warning(errMsg)
                        continue
                    # 荷重値の取得
                    fx = float(nodeLoadTmp['tx']) if ('tx' in nodeLoadTmp) and isFloat(nodeLoadTmp['tx']) else 0.0
                    fy = float(nodeLoadTmp['ty']) if ('ty' in nodeLoadTmp) and isFloat(nodeLoadTmp['ty']) else 0.0
                    fz = float(nodeLoadTmp['tz']) if ('tz' in nodeLoadTmp) and isFloat(nodeLoadTmp['tz']) and (self.mode == 3) else 0.0
                    rx = float(nodeLoadTmp['rx']) if ('rx' in nodeLoadTmp) and isFloat(nodeLoadTmp['rx']) and (self.mode == 3) else 0.0
                    ry = float(nodeLoadTmp['ry']) if ('ry' in nodeLoadTmp) and isFloat(nodeLoadTmp['ry']) and (self.mode == 3) else 0.0
                    rz = float(nodeLoadTmp['rz']) if ('rz' in nodeLoadTmp) and isFloat(nodeLoadTmp['rz']) else 0.0
                    # 節点荷重データの追加
                    if (fx != 0) or (fy != 0) or (fz != 0) or (rx != 0) or (ry != 0) or (rz != 0):
                        lCaseTmp.nodeLoads.append(NodeLoad(iNode, fx, fy, fz, rx, ry, rz, 1))
                    # 強制変位値の取得
                    dx = float(nodeLoadTmp['dx']) if ('dx' in nodeLoadTmp) and isFloat(nodeLoadTmp['dx']) else 0.0
                    dy = float(nodeLoadTmp['dy']) if ('dy' in nodeLoadTmp) and isFloat(nodeLoadTmp['dy']) else 0.0
                    dz = float(nodeLoadTmp['dz']) if ('dz' in nodeLoadTmp) and isFloat(nodeLoadTmp['dz']) and (self.mode == 3) else 0.0
                    ax = float(nodeLoadTmp['ax']) if ('ax' in nodeLoadTmp) and isFloat(nodeLoadTmp['ax']) and (self.mode == 3) else 0.0
                    ay = float(nodeLoadTmp['ay']) if ('ay' in nodeLoadTmp) and isFloat(nodeLoadTmp['ay']) and (self.mode == 3) else 0.0
                    az = float(nodeLoadTmp['az']) if ('az' in nodeLoadTmp) and isFloat(nodeLoadTmp['az']) else 0.0
                    # 強制変位データの追加
                    if (dx != 0) or (dy != 0) or (dz != 0) or (ax != 0) or (ay != 0) or (az != 0):
                        lCaseTmp.forcedDisps.append(ForcedDisp(iNode, dx, dy, dz, ax, ay, az))
                    # 荷重値、強制変位が全て0の場合は警告をログ
                    if (fx == 0) and (fy == 0) and (fz == 0) and (rx == 0) and (ry == 0) and (rz == 0) \
                        and (dx == 0) and (dy == 0) and (dz == 0) and (ax == 0) and (ay == 0) and (az == 0):
                        errMsg = f"荷重値が全て0の節点荷重データを無視しました: 節点({str(nodeLoadTmp['n'])}) 荷重ケース({loadId})"
                        logger.warning(errMsg)
        # endregion

        # region 要素荷重による節点追加、梁要素分割
        if 'load' in inputJson:
            for id in inputJson['load'].keys():
                caseTmp = inputJson['load'][id]
                if (not 'load_member' in caseTmp) or (len(caseTmp['load_member']) == 0):
                    # 要素荷重データが無い場合はスキップ
                    continue
                devide_byElemLoads(caseTmp['load_member'], self.members, str(id))
        # endregion

        # region 要素荷重（集中荷重、分布荷重、温度荷重データの作成）
        if 'load' in inputJson:
            for id in inputJson['load'].keys():
                caseTmp = inputJson['load'][id]
                if (not 'load_member' in caseTmp) or (len(caseTmp['load_member']) == 0):  # 要素荷重データが無い場合はスキップ
                    errMsg = f"要素荷重データが無い荷重ケース: 荷重ケース({str(id)})"
                    logger.info(errMsg)
                    continue
                # 該当の荷重ケースの取得
                loadId = str(id)
                pickedLoadCase = [obj for obj in self.loadCases if obj.id == loadId]
                if len(pickedLoadCase) == 1:
                    lCaseTmp = pickedLoadCase[0]
                else:
                    errMsg = "要素荷重データ作成時に予期せぬエラーが発生しました"
                    logger.critical(errMsg + f": 荷重ケース({loadId})")
                    raise MyCritical(errMsg)
                # 要素荷重データの作成
                for elemLoadTmp in caseTmp['load_member']:
                    # 載荷部材の取得
                    nMem = convInt(elemLoadTmp['m']) if ('m' in elemLoadTmp) and isInt(elemLoadTmp['m']) else 0
                    iMem = get_memIndex(nMem, self.members)
                    if iMem is None:  # 該当する部材が無い場合はスキップ
                        errMsg = f"載荷部材が存在しない要素荷重データを無視しました: 部材({str(nMem)}) 荷重ケース({loadId})"
                        logger.warning(errMsg)
                        continue
                    memTmp = self.members[iMem]
                    # 要素荷重データの登録
                    mark = convInt(elemLoadTmp['mark']) if ('mark' in elemLoadTmp) and isInt(elemLoadTmp['mark']) else 0
                    if (mark == 1) or (mark == 11):  # 集中荷重
                        lCaseTmp.nodeLoads.extend(make_eNodeLoads(elemLoadTmp, mark, memTmp))
                    elif mark == 2:  # 分布荷重
                        lCaseTmp.elemLoads.extend(make_eElemLoads(elemLoadTmp, memTmp))
                    elif mark == 9:  # 温度荷重
                        lCaseTmp.heatLoads.extend(make_heatLoads(elemLoadTmp, memTmp))
                    else:
                        errMsg = f"荷重タイプが不正な要素荷重データを無視しました: 荷重タイプ({str(mark)}) 部材({str(nMem)}) 荷重ケース({loadId})"
                        logger.warning(errMsg)
        # endregion

        # region 不要な荷重データの削除
        iNoNeeded: list[int] = []  # 不要な荷重ケースインデックス格納用
        # 荷重が1つも無い荷重ケースの探索
        for i, lCaseTmp in enumerate(self.loadCases):
            if (len(lCaseTmp.nodeLoads) == 0) and (len(lCaseTmp.elemLoads) == 0) \
                and (len(lCaseTmp.heatLoads) == 0) and (len(lCaseTmp.forcedDisps) == 0):
                iNoNeeded.append(i)  # 荷重が1つもない荷重ケースのインデックスをキープ
        # 不要な荷重ケースの削除
        if len(iNoNeeded) > 0:
            for i in sorted(iNoNeeded, reverse=True):  # インデックスの大きいほうからループで削除
                errMsg = f"荷重入力の無い荷重ケースを無視しました: 荷重ケース({self.loadCases[i].id})"
                logger.warning(errMsg)
                del self.loadCases[i]
        # 荷重ケースが無くなった場合はエラー
        if len(self.loadCases) == 0:
            errMsg = "有効な荷重ケースがありません"
            logger.error(errMsg)
            raise MyError(errMsg)
        # endregion

        # region 支点・節点バネ
        self.supportCases: dict[int, list[Support]] = {}
        if 'fix_node' in inputJson:
            for nStr in inputJson['fix_node'].keys():
                # 支点ケース番号の取得
                if not isInt(nStr):  # 整数化できない支点ケース番号のデータはスキップ
                    errMsg = f"ケース番号が不正な支点データを無視しました: 支点ケース({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                supCaseNum = convInt(nStr)
                # 支点ケースデータの作成
                supports = make_supports(inputJson['fix_node'][nStr], self.nodes, self.mode)
                self.supportCases[supCaseNum] = supports
        if 1 not in self.supportCases.keys():
            self.supportCases[1] = []  # ケース1が無い場合はダミーデータを設定
        # endregion

        # region 要素分布バネ
        self.springCases: dict[int, list[Spring]] = {}
        if 'fix_member' in inputJson:
            for nStr in inputJson['fix_member'].keys():
                # 分布バネケース番号の取得
                if not isInt(nStr):  # 整数化できない分布バネケース番号のデータはスキップ
                    errMsg = f"ケース番号が不正な分布バネデータを無視しました: 分布バネケース({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                sprCaseNum = convInt(nStr)
                # 分布バネケースデータの作成
                springs = make_springs(inputJson['fix_member'][nStr], self.members, self.mode)
                self.springCases[sprCaseNum] = springs
        if 1 not in self.springCases.keys():
            self.springCases[1] = []  # ケース1が無い場合はダミーデータを設定
        # endregion

        # region 結合（材端条件）
        self.jointCases: dict[int, list[Joint]] = {}
        if 'joint' in inputJson:
            for nStr in inputJson['joint'].keys():
                # 結合ケース番号の取得
                if not isInt(nStr):  # 整数化できない結合ケース番号のデータはスキップ
                    errMsg = f"ケース番号が不正な結合データを無視しました: 結合ケース({str(nStr)})"
                    logger.warning(errMsg)
                    continue
                jntCaseNum = convInt(nStr)
                # 結合ケースデータの作成
                joints = make_joints(inputJson['joint'][nStr], self.members, self.mode)
                self.jointCases[jntCaseNum] = joints
        if 1 not in self.jointCases.keys():
            self.jointCases[1] = []  # ケース1が無い場合はダミーデータを設定
        # endregion

        # region 荷重ケースに含まれる材料特性、支点、分布バネ、材端条件ケースの確認
        combNeeded: list[int] = []  # 基本荷重ケースに含まれる全ケース組合せインデックス
        for lCaseTmp in self.loadCases:
            if not lCaseTmp.iCaseComb in combNeeded:
                combNeeded.append(lCaseTmp.iCaseComb)
        if len(combNeeded) == 0:
            errMsg = "有効な荷重ケースがありません"
            logger.error(errMsg)
            raise MyError(errMsg)
        # 必要な各ケースのケース番号一覧の取得
        supCaseList: list[int] = []  # 支点ケース
        sprCaseList: list[int] = []  # 分布バネケース
        matCaseList: list[int] = []  # 材料特性ケース
        jntCaseList: list[int] = []  # 結合ケース
        for iComb in combNeeded:
            combTmp = self.caseCombs[iComb]
            if not combTmp.nSupportCase in supCaseList:
                supCaseList.append(combTmp.nSupportCase)
            if not combTmp.nSpringCase in sprCaseList:
                sprCaseList.append(combTmp.nSpringCase)
            if not combTmp.nMaterialCase in matCaseList:
                matCaseList.append(combTmp.nMaterialCase)
            if not combTmp.nJointCase in jntCaseList:
                jntCaseList.append(combTmp.nJointCase)
        # 支点ケース
        for nSup in supCaseList:
            if not nSup in self.supportCases.keys():
                errMsg = f"必要な支点ケースがありません: 支点ケース({str(nSup)})"
                logger.error(errMsg)
                raise MyError(errMsg)
        # 分布バネケース
        for nSpr in sprCaseList:
            if not nSpr in self.springCases.keys():
                errMsg = f"必要な分布バネケースがありません: 分布バネケース({str(nSpr)})"
                logger.error(errMsg)
                raise MyError(errMsg)
        # 材端条件ケース
        for nJnt in jntCaseList:
            if not nJnt in self.jointCases.keys():
                errMsg = f"必要な結合ケースがありません: 結合ケース({str(nJnt)})"
                logger.error(errMsg)
                raise MyError(errMsg)
        # 材料特性ケース
        for nMat in matCaseList:
            if not nMat in self.materialCases.keys():
                errMsg = f"必要な材料特性ケースがありません: 材料特性ケース({str(nMat)})"
                logger.error(errMsg)
                raise MyError(errMsg)
        # endregion

        # region 変位拘束条件有無の確認
        for lCaseTmp in self.loadCases:
            displacement = np.zeros(3, dtype=float)  # X軸、Y軸、Z軸方向変位拘束判定（拘束無しなら0）
            # 支点条件の確認
            supCase = self.supportCases[self.caseCombs[lCaseTmp.iCaseComb].nSupportCase]
            for sup in supCase:
                if (sup.dxFix == True) or (sup.dxSpr > 0):
                    displacement[0] = 1.0
                if (sup.dyFix == True) or (sup.dySpr > 0):
                    displacement[1] = 1.0
                if (sup.dzFix == True) or (sup.dzSpr > 0):
                    displacement[2] = 1.0
            # 強制変位の確認
            forcedDisp = lCaseTmp.forcedDisps
            for fDisp in forcedDisp:
                if fDisp.dx != 0:
                    displacement[0] = 1.0
                if fDisp.dy != 0:
                    displacement[1] = 1.0
                if fDisp.dz != 0:
                    displacement[2] = 1.0
            # 分布バネの確認
            sprCase = self.springCases[self.caseCombs[lCaseTmp.iCaseComb].nSpringCase]
            for spr in sprCase:
                # 要素座標系に対するバネの有無チェック
                frees = np.zeros(3, dtype=float)  # x軸、y軸、z軸方向バネ有無（バネ無しなら0）
                if spr.dxSpr > 0:
                    frees[0] = 1.0
                if spr.dySpr > 0:
                    frees[1] = 1.0
                if spr.dzSpr > 0:
                    frees[2] = 1.0
                # 全体座標系に変換してチェック
                beam = self.beams[spr.iBeam]  # 梁インデックス
                eMat = beam.get_convMatrix(3, True)  # 要素座標系→全体座標系の変換行列
                displacement += np.abs(np.matmul(eMat, frees))
            # 変位拘束有無の判定
            if np.any(displacement == 0):
                errMsg = ("X" if displacement[0] == 0 else "") + ("Y" if displacement[1] == 0 else "") + ("Z" if displacement[2] == 0 else "")
                errMsg += "方向の変位拘束条件が存在しない荷重ケースがあります"
                logger.error(errMsg + f": 荷重ケース({lCaseTmp.id})")
                raise MyError(errMsg, loadCase=lCaseTmp)
        # endregion

        # region 節点位置での不安定チェック
        for i, node in enumerate(self.nodes):
            if not node.isNode:
                continue  # 追加された節点はチェック対象外
            # 接続する梁要素の取得
            beamsI = [obj for obj in self.beams if obj.indI == i]
            beamsJ = [obj for obj in self.beams if obj.indJ == i]
            # 接続するパネルの取得
            panels = [obj for obj in self.shells if i in obj.iNodes]
            # 接続するパネルの取得
            solids = [obj for obj in self.solids if i in obj.iNodes]
            # 自由節点の判定
            if (len(beamsI) == 0) and (len(beamsJ) == 0) and (len(panels) == 0)and (len(solids) == 0):
                errMsg = "どの部材にも接続されない自由節点が存在します"
                logger.error(errMsg + f": 節点({str(node.nNode)})")
                raise MyError(errMsg, node=node)
            # パネルに接続されている場合は回転拘束OKと判定
            if len(panels) >= 1:
                continue
            # ソリッドに接続されている場合は回転拘束OKと判定
            if len(solids) >= 1:
                continue
            # 荷重ケースごとに回転不安定のチェック
            for lCase in self.loadCases:
                rotation = np.zeros(3, dtype=float)  # X軸、Y軸、Z軸まわり回転拘束判定（拘束無しなら0）
                caseComb = self.caseCombs[lCase.iCaseComb]  # 組合せケース
                # 支点条件の考慮
                pickedSup = [obj for obj in self.supportCases[caseComb.nSupportCase] if obj.iNode == i]  # 支点条件
                if len(pickedSup) > 1:
                    errMsg = "不安定構造のチェック中に予期せぬエラーが発生しました"
                    logger.critical(errMsg)
                    raise MyCritical(errMsg)
                elif len(pickedSup) == 1:
                    support = pickedSup[0]
                    if (support.rxFix) or (support.rxSpr > 0):
                        rotation[0] = 1.0
                    if (support.ryFix) or (support.rySpr > 0):
                        rotation[1] = 1.0
                    if (support.rzFix) or (support.rzSpr > 0):
                        rotation[2] = 1.0
                # 強制変位の考慮
                pickedDisp = [obj for obj in lCase.forcedDisps if obj.iNode == i]  # 強制変位
                if len(pickedDisp) > 1:
                    errMsg = "不安定構造のチェック中に予期せぬエラーが発生しました"
                    logger.critical(errMsg)
                    raise MyCritical(errMsg)
                elif len(pickedDisp) == 1:
                    forcedDisp = pickedDisp[0]
                    if forcedDisp.ax != 0:
                        rotation[0] = 1.0
                    if forcedDisp.ay != 0:
                        rotation[1] = 1.0
                    if forcedDisp.az != 0:
                        rotation[2] = 1.0
                # 材端条件の考慮
                joints = self.jointCases[caseComb.nJointCase]  # 結合条件
                for edge in ["i", "j"]:  # i端→j端
                    connectedBeams = beamsI if edge == "i" else beamsJ
                    for beam in connectedBeams:
                        # 材端条件の取得
                        iBeam = self.beams.index(beam)  # 梁要素インデックス
                        pickedJnt = [obj for obj in joints if obj.iBeam == iBeam]
                        if len(pickedJnt) > 1:
                            errMsg = "不安定構造のチェック中に予期せぬエラーが発生しました"
                            logger.critical(errMsg)
                            raise MyCritical(errMsg)
                        elif len(pickedJnt) == 0:
                            rotation[:] = 1.0
                            break  # 材端条件の設定が無い梁要素（つまり材端回転拘束）が接続されていればOK
                        joint = pickedJnt[0]
                        # 要素座標系に対する回転拘束状態の取得
                        frees = np.zeros(3, dtype=float)  # x軸、y軸、z軸まわり回転
                        if edge == "i":
                            frees[0] = 0.0 if joint.xiFree == True else 1.0
                            frees[1] = 0.0 if joint.yiFree == True else 1.0
                            frees[2] = 0.0 if joint.ziFree == True else 1.0
                        else:
                            frees[0] = 0.0 if joint.xjFree == True else 1.0
                            frees[1] = 0.0 if joint.yjFree == True else 1.0
                            frees[2] = 0.0 if joint.zjFree == True else 1.0
                        eMat = beam.get_convMatrix(3, True)  # 要素座標系→全体座標系の変換行列
                        rotation += np.abs(np.matmul(eMat, frees))
                # 不安定の判定（接続する全ての梁要素、パネルをチェックした上で回転拘束が無い方向が残っていれば不安定構造と判定）
                if np.any(rotation == 0):
                    errMsg = "節点の回転に対して不安定構造となっています"
                    logger.error(errMsg + f": 節点番号({str(node.nNode)}) 荷重ケース({lCase.id})")
                    raise MyError(errMsg, node=node, loadCase=lCase)
        # endregion

        return None
    
