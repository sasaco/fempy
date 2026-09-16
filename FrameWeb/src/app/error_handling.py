import os
import logging
from typing import Optional
from components.node import Node
from components.member import Member
from components.shell import Shell
from components.load import CaseComb, LoadCase


# region ロガーの準備
logger = logging.getLogger("AppLogger")
logger.setLevel(logging.DEBUG)
format = "%(levelname)-9s  %(asctime)s [%(filename)s:%(lineno)d] %(message)s"

# コンソールへの出力設定
st_handler = logging.StreamHandler()
st_handler.setLevel(logging.WARNING)  # WARNING以上のログを出力
st_handler.setFormatter(logging.Formatter(format))
logger.addHandler(st_handler)

# デバッグモードで実行時のみログファイルに出力（カレントディレクトリ直下のapp.log）
if __debug__:
    if "K_SERVICE" in os.environ or "GOOGLE_CLOUD_PROJECT" in os.environ:
        # GCP環境での実行時は/tmpディレクトリにする必要がある
        current_dir = "/tmp"
    else:
        current_dir = os.path.dirname(__file__)  # このファイルがあるディレクトリ
    fl_handler = logging.FileHandler(filename=os.path.join(current_dir, "app.log"), encoding="utf-8", mode="w")
    fl_handler.setLevel(logging.INFO)  # INFO以上のログを出力
    fl_handler.setFormatter(logging.Formatter(format))
    logger.addHandler(fl_handler)
# endregion


# region 例外クラスの定義
class MyException(Exception):
    """アプリ例外クラス（基幹クラス）
    
    Properties:
        node (Optional[Node]):節点
        member (Optional[Member]):部材
        panel (Optional[Shell]):パネル
        caseComb (Optional[CaseComb]):ケース組合せ
        loadCase (Optional[LoadCase]):荷重ケース
    """
    def __init__(self, *args, node: Optional[Node] = None, member: Optional[Member] = None, \
                 panel: Optional[Shell] = None, caseComb: Optional[CaseComb] = None, \
                 loadCase: Optional[LoadCase] = None):
        super().__init__(*args)
        self.node = node
        self.member = member
        self.panel = panel
        self.caseComb = caseComb
        self.loadCase = loadCase


class MyError(MyException):
    """アプリ例外クラス（Level:error）"""
    def output_msg(self) -> str:
        """出力用のエラーメッセージを作成する

        Returns:
            _ (str): 出力用のエラーメッセージ
        """
        msg = str(self)  # メインのメッセージ
        if self.loadCase is not None:
            msg += "\n"
            msg += "・荷重ケース:　" + self.loadCase.id
        if self.caseComb is not None:
            msg += "\n"
            msg += "・材料特性ケース:　" + str(self.caseComb.nMaterialCase)
            msg += "\n"
            msg += "・支点ケース:　" + str(self.caseComb.nSupportCase)
            msg += "\n"
            msg += "・分布バネケース:　" + str(self.caseComb.nSpringCase)
            msg += "\n"
            msg += "・結合ケース:　" + str(self.caseComb.nJointCase)
        if self.panel is not None:
            msg += "\n"
            msg += "・パネル番号:　" + str(self.panel.num)
        if self.member is not None:
            msg += "\n"
            msg += "・部材番号:　" + str(self.member.num)
        if self.node is not None:
            msg += "\n"
            msg += "・節点番号:　" + str(self.node.nNode)
        return msg


class MyCritical(MyException):
    """アプリ例外クラス（Level:critical）"""
    fixed_msg: str = "予期せぬエラーが発生しました。\n担当営業にお問い合わせください。"
# endregion