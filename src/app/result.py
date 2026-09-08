import math
from typing import TypedDict
from error_handling import MyCritical, logger
from components.node import Node
from components.member import Member
from components.shell import Shell
from components.support import Support
from fem.calculation import FEMCalculation, Displacement, SectionForce, ReactionForce


class Output_Disp(TypedDict):
    """出力用節点変位クラス

    Keys:
        dx (float):全体座標系X軸方向変位(mm)
        dy (float):全体座標系Y軸方向変位(mm)
        dz (float):全体座標系Z軸方向変位(mm)
        rx (float):全体座標系X軸まわり回転角(*10^-3rad)
        ry (float):全体座標系Y軸まわり回転角(*10^-3rad)
        rz (float):全体座標系Z軸まわり回転角(*10^-3rad)

    Note:
        変位は座標軸正方向への変位が正、回転角は座標軸正方向に対して右ねじ回りが正
    """
    dx: float
    dy: float
    dz: float
    rx: float
    ry: float
    rz: float


class Output_Reaction(TypedDict):
    """出力用支点反力クラス

    Keys:
        tx (float):全体座標系X軸方向支点反力(kN)
        ty (float):全体座標系Y軸方向支点反力(kN)
        tz (float):全体座標系Z軸方向支点反力(kN)
        mx (float):全体座標系X軸まわり支点反力モーメント(kNm)
        my (float):全体座標系Y軸まわり支点反力モーメント(kNm)
        mz (float):全体座標系Z軸まわり支点反力モーメント(kNm)

    Note:
        tx,ty,tzは全体座標軸の正の向きが正、mx,my,mzは全体座標軸正の向きに対して右ねじ回りが正
    """
    tx: float
    ty: float
    tz: float
    mx: float
    my: float
    mz: float


class Output_BeamForce(TypedDict):
    """出力用梁要素断面力クラス

    Keys:
        fxi (float):i端軸力(kN)
        fyi (float):i端要素座標系y軸方向せん断力(kN)
        fzi (float):i端要素座標系z軸方向せん断力(kN)
        mxi (float):i端ねじりモーメント(kNm)
        myi (float):i端要素座標系y軸まわり曲げモーメント(kNm)
        mzi (flaot):i端要素座標系z軸まわり曲げモーメント(kNm)
        fxj (float):j端軸力(kN)
        fyj (float):j端要素座標系y軸方向せん断力(kN)
        fzj (float):j端要素座標系z軸方向せん断力(kN)
        mxj (float):j端ねじりモーメント(kNm)
        myj (float):j端要素座標系y軸まわり曲げモーメント(kNm)
        mzj (flaot):j端要素座標系z軸まわり曲げモーメント(kNm)
        L (float):梁要素長さ(m)

    Note:
        fxは引張が正
        fyは始端側が要素座標系y軸正の方向に変形するようなせん断力が正
        fzは始端側が要素座標系z軸正の方向に変形するようなせん断力が正
        mxは引張の向きに対して右ねじ回りが正
        myは要素座標系y軸正の方向に凸となる曲げモーメントが正
        mzは要素座標系z軸正の方向に凸となる曲げモーメントが正
    """
    fxi: float
    fyi: float
    fzi: float
    mxi: float
    myi: float
    mzi: float
    fxj: float
    fyj: float
    fzj: float
    mxj: float
    myj: float
    mzj: float
    L: float


class Output_Result(TypedDict):
    """出力用結果クラス

    Keys:
        disg (dict[str, Output_Disp]):節点変位データ
        reac (dict[str, Output_Reaction]):支点反力データ
        fsec (dict[str, dict[str, Output_BeamForce]]):梁要素断面力データ
        shell_results (dict):シェル要素の応力・ひずみ計算結果
        size (int):分割用も含む節点数
    """
    disg: dict[str, Output_Disp]
    reac: dict[str, Output_Reaction]
    fsec: dict[str, dict[str, Output_BeamForce]]
    shell_results: dict
    size: int


def make_result(fc: FEMCalculation, members: list[Member], shells: list[Shell], rate: float, lCase: str, mode: int) -> Output_Result:
    """出力用計算結果データの作成

    Args:
        fc (FrameCalculation): フレーム計算結果
        members (list[Member]): 部材リスト
        rate (float): 割増係数
        lCase (str): 基本荷重ケース名
        mode (int): 2Dか3Dか{2 or 3}

    Returns:
        _ (Output_Result): 出力用計算結果データ
    """
    # 変位
    disg: dict[str, Output_Disp] = make_disp(fc.dNodes, fc.stiffMatrix.nodes, members, rate, lCase)
    # 支点反力
    reac: dict[str, Output_Reaction] = make_reac(fc.reactions, fc.stiffMatrix.nodes, fc.stiffMatrix.supports, rate, mode)
    # 梁要素断面力
    fsec: dict[str, dict[str, Output_BeamForce]] = make_fsec(fc.fiBeams, fc.fjBeams, members, rate)
    # シェル応力・ひずみ
    shell_results = {}
    for shell_idx, result_data in fc.shell_results.items():
        shell_results[str(shell_idx)] = result_data
    
    # 結果データの作成
    result: Output_Result = {
        "disg": disg,
        "reac": reac,
        "fsec": fsec,
        "shell_results": shell_results,
        "size": len(fc.stiffMatrix.nodes)
    }
    return result


def make_disp(dNodes: list[Displacement], nodes: list[Node], members: list[Member], rate: float, lCase: str) -> dict[str, Output_Disp]:
    """出力用の節点変位データを作成する

    Args:
        dNodes (list[Displacement]): フレーム計算結果の節点変位リスト
        nodes (list[Node]): 節点リスト
        members (list[Member]): 部材リスト
        rate (float): 割増係数
        lCase (str): 基本荷重ケース名

    Returns:
        _ (dict[str, Output_Disp]): 出力用節点変位データ
    """
    disp: dict[str, Output_Disp] = {}
    # 節点変位の取得（分割用に追加された節点を除く）
    picked = sorted([obj for obj in nodes if obj.isNode == True], key=lambda x: x.nNode)
    for node in picked:
        iNode = nodes.index(node)  # 節点インデックス
        nNode = node.nNode  # 節点番号
        dNode = dNodes[iNode]  # 節点変位データ
        # 出力用節点変位データの作成（割増係数を乗じる）
        dispTmp: Output_Disp = {
            "dx": dNode["dx"] * rate,
            "dy": dNode["dy"] * rate,
            "dz": dNode["dz"] * rate,
            "rx": dNode["rx"] * rate,
            "ry": dNode["ry"] * rate,
            "rz": dNode["rz"] * rate
        }
        disp[str(nNode)] = dispTmp
    # 分割用に追加された節点の変位を取得
    for delimeter in ["n", "l"]:  # 着目点・剛域境界→要素荷重位置
        # 該当節点の抽出
        if delimeter == "n":  # 着目点・剛域境界
            picked = [obj for obj in nodes if (obj.isNode == False) and\
                    ((obj.isPoint == True) or (obj.isRigid == True))]
        else:  # 要素荷重位置
            picked = [obj for obj in nodes if (obj.isNode == False) and (obj.isPoint == False)\
                    and (obj.isRigid == False) and (obj.isELoad == True) and (lCase in obj.lCases)]
        # 部材ごとに出力データを作成する
        mList: list[int] = sorted(set([obj.nMem for obj in picked]))  # 重複無しの部材番号一覧（昇順）
        for nMem in mList:
            # 部材番号に該当する部材データを取得
            pickedMem = [obj for obj in members if obj.num == nMem]
            if len(pickedMem) != 1:
                errMsg = "追加節点の出力用節点変位データ作成中に予期せぬエラーが発生しました"
                logger.critical(errMsg)
                raise MyCritical(errMsg)
            member = pickedMem[0]
            # 同じ部材内の節点を部材のi端からの距離の昇順で取得
            nodeI = nodes[member.indI]  # 部材のi端節点
            picked2 = sorted([obj for obj in picked if obj.nMem == nMem],\
                             key=lambda x: math.sqrt((x.x - nodeI.x) ** 2 + (x.y - nodeI.y) ** 2 + (x.z - nodeI.z) ** 2))
            # 出力データの作成（割増係数を乗じる）
            no: int = 1  # 部材内における追加節点の通し番号
            for node in picked2:
                iNode = nodes.index(node)  # 節点インデックス
                dNode = dNodes[iNode]  # 節点変位データ
                id = str(nMem) + delimeter + str(no)  # {部材番号}{delimeter}{通し番号}　例：2n4、5l3など
                dispTmp: Output_Disp = {
                    "dx": dNode["dx"] * rate,
                    "dy": dNode["dy"] * rate,
                    "dz": dNode["dz"] * rate,
                    "rx": dNode["rx"] * rate,
                    "ry": dNode["ry"] * rate,
                    "rz": dNode["rz"] * rate
                }
                disp[id] = dispTmp
                no += 1
    return disp


def make_reac(reactions: list[ReactionForce], nodes: list[Node], supports: list[Support], rate: float, mode: int) -> dict[str, Output_Reaction]:
    """出力用の支点反力データを作成する

    Args:
        reactions (list[ReactionForce]): フレーム計算結果の支点反力リスト
        nodes (list[Node]): 節点リスト
        supports (list[Support]): 支点リスト
        rate (float): 割増係数
        mode (int): 2Dか3Dか{2 or 3}

    Returns:
        _ (dict[str, Output_Reaction]): 出力用支点反力データ
    """
    reac: dict[str, Output_Reaction] = {}
    for reaction in sorted(reactions, key=lambda x: nodes[x["iNode"]].nNode):  # 節点番号の昇順で出力
        iNode = reaction["iNode"]  # 節点インデックス
        # 節点に該当する支点データを取得
        pickedSup = [obj for obj in supports if obj.iNode == iNode]
        if len(pickedSup) != 1:
            errMsg = f"出力用支点反力データ作成中に予期せぬエラーが発生しました"
            logger.critical(errMsg + f": 節点番号({str(nodes[iNode].nNode)})")
            raise MyCritical(errMsg, node=nodes[iNode])
        sup = pickedSup[0]
        # 2Dモードの奥行方向拘束のために仮に追加された支点データならスキップ
        if (mode == 2) and hasattr(sup, 'for2D') and sup.for2D:
            continue  # 2D用の補助支点データならスキップ
        # 出力データの作成（割増係数を乗じる）
        nNode = nodes[iNode].nNode  # 節点番号
        reacTmp: Output_Reaction = {
            "tx": reaction["fx"] * rate,
            "ty": reaction["fy"] * rate,
            "tz": reaction["fz"] * rate if mode != 2 else 0.0,
            "mx": reaction["mx"] * rate if mode != 2 else 0.0,
            "my": reaction["my"] * rate if mode != 2 else 0.0,
            "mz": reaction["mz"] * rate
        }
        reac[str(nNode)] = reacTmp
    return reac


def make_fsec(fiBeams: list[SectionForce], fjBeams: list[SectionForce],\
              members: list[Member], rate: float) -> dict[str, dict[str, Output_BeamForce]]:
    """出力用の梁要素断面力データを作成する

    Args:
        fiBeams (list[SectionForce]): フレーム計算結果の梁要素i端断面力リスト
        fjBeams (list[SectionForce]): フレーム計算結果の梁要素j端断面力リスト
        members (list[Member]): 部材リスト
        rate (float): 割増係数

    Returns:
        _ (dict[str, dict[str, Output_BeamForce]]): 出力用梁要素断面力データ
    """
    fsec: dict[str, dict[str, Output_BeamForce]] = {}
    for member in sorted(members, key=lambda x: x.num):  # 部材番号の昇順に出力
        nMem = member.num  # 部材番号
        fsecTmp: dict[str, Output_BeamForce] = {}
        ib = 0  # 部材内における梁要素の通し番号
        flag = 0  # 0ならi端側断面力の検索中、1ならj端側断面力の検索中
        for iBeam in member.elems:
            beam = member.beams[iBeam]  # 梁要素
            # i端側断面力の取得（割増係数を乗じる）
            if flag == 0:
                fiBeam = fiBeams[iBeam]  # i端断面力
                fxi: float = -fiBeam["fx"] * rate
                fyi: float = fiBeam["fy"] * rate
                fzi: float = fiBeam["fz"] * rate
                mxi: float = -fiBeam["mx"] * rate
                myi: float = -fiBeam["my"] * rate
                mzi: float = fiBeam["mz"] * rate
                bLen: float = beam.leng  # 梁要素長さ
                flag = 1
            else:
                bLen += beam.leng
            # j端側断面力の取得
            if flag == 1:
                nj = beam.indJ  # j端節点インデックス
                nodeJ = member.nodes[nj]  # j端節点
                if (nodeJ.isNode) or (nodeJ.isPoint) or (nodeJ.isRigid):
                #if (nodeJ.isNode) or (nodeJ.isPoint) or (nodeJ.isRigid) or (lCase in nodeJ.lCases):
                    # ひとまず旧FrameWebに合わせて着目点（剛域境界含む）で分割した要素の結果しか出力しない
                    # ただし、本来は全分割要素の結果を出力し、フロントエンドでピックアップファイル作成時にフィルターすべき
                    ib += 1
                    id = "P" + str(ib)  # P{通し番号}　例：P1、P5など
                    # 割増係数を乗じる
                    fjBeam = fjBeams[iBeam]  # j端断面力
                    fxj: float = fjBeam["fx"] * rate
                    fyj: float = -fjBeam["fy"] * rate
                    fzj: float = -fjBeam["fz"] * rate
                    mxj: float = fjBeam["mx"] * rate
                    myj: float = fjBeam["my"] * rate
                    mzj: float = -fjBeam["mz"] * rate
                    # 出力データの作成
                    bForceTmp: Output_BeamForce = {
                        "fxi": fxi,
                        "fyi": fyi,
                        "fzi": fzi,
                        "mxi": mxi,
                        "myi": myi,
                        "mzi": mzi,
                        "fxj": fxj,
                        "fyj": fyj,
                        "fzj": fzj,
                        "mxj": mxj,
                        "myj": myj,
                        "mzj": mzj,
                        "L": bLen
                    }
                    fsecTmp[id] = bForceTmp
                    flag = 0
        fsec[str(nMem)] = fsecTmp
    return fsec
