import os
import logging
from typing import Optional


# region ロガーの準備
fLogger = logging.getLogger("FrameLogger")
fLogger.setLevel(logging.DEBUG)
format = "%(levelname)-9s  %(asctime)s [%(filename)s:%(lineno)d] %(message)s"

# コンソールへの出力設定
st_handler = logging.StreamHandler()
st_handler.setLevel(logging.WARNING)  # WARNING以上のログを出力
st_handler.setFormatter(logging.Formatter(format))
fLogger.addHandler(st_handler)

# デバッグモードで実行時のみログファイルに出力（カレントディレクトリ直下のapp.log）
if __debug__:
    if "K_SERVICE" in os.environ or "GOOGLE_CLOUD_PROJECT" in os.environ:
        # GCP環境での実行時は/tmpディレクトリにする必要がある
        current_dir = "/tmp"
    else:
        current_dir = os.path.dirname(__file__)  # このファイルがあるディレクトリ
    fl_handler = logging.FileHandler(filename=os.path.join(current_dir, "frame.log"), encoding="utf-8", mode="w")
    fl_handler.setLevel(logging.INFO)  # INFO以上のログを出力
    fl_handler.setFormatter(logging.Formatter(format))
    fLogger.addHandler(fl_handler)
# endregion


# region 例外クラスの定義
class FrameException(Exception):
    """フレーム計算例外クラス（基幹クラス）

    Properties:
        iNode (Optional[int]):節点インデックス
        iBeam (Optional[int]):梁要素インデックス
        iShell (Optional[int]):シェル要素インデックス
        direction (Optional[str]):方向等を表す文字列
    """
    def __init__(self, *args, iNode: Optional[int] = None, iBeam: Optional[int] = None, \
                 iShell: Optional[int] = None, direction: Optional[str] = None):
        super().__init__(*args)
        self.iNode = iNode
        self.iBeam = iBeam
        self.iShell = iShell
        self.direction = direction


class FrameError(FrameException):
    """フレーム計算例外クラス（Level:error）"""
    pass


class FrameCritical(FrameException):
    """フレーム計算例外クラス（Level:critical）"""
# endregion