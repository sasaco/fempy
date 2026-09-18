import sys
import os

# ここでsrcをパスに追加
src_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "src")
if src_path not in sys.path:
    sys.path.insert(0, src_path)

import json
import base64
import binascii
import gzip
import zlib
# functions_framework may import symbols not available in some local Python/site-packages
# (for example when package expects a newer Python stdlib). Import defensively and
# provide a minimal stub that implements the decorator used below so local
# `flask run` and `import main` do not fail.
try:
    import functions_framework
except Exception:
    # Minimal stub implementing @functions_framework.http decorator
    class _StubFunctionsFramework:
        def http(self, fn=None, **kwargs):
            # Support both @functions_framework.http and @functions_framework.http(...)
            if fn is None:
                return lambda f: f
            return fn

    functions_framework = _StubFunctionsFramework()
from flask import Flask, request
from app.error_handling import MyError, MyCritical
from fem.analysis_result_sets import build_analysis_result_set
from fem.diagnostics import InputValidationError, diagnostic_payload
from fem.model import FemModel  # Public import retained for embedding clients.
from fem.nonlinear.nonlinear_solver import NonlinearConvergenceError
from werkzeug.exceptions import BadRequest, UnsupportedMediaType

# Flaskアプリの作成
app = Flask(__name__)

@app.route('/', methods=['OPTIONS', 'GET', 'POST'])
def post():
    return FEMPython(request)


# FEMPythonの定義
@functions_framework.http
def FEMPython(request):
    """FEMPythonのメイン関数
    
    @brief FEMPython APIのメインエントリーポイント
    
    HTTPリクエストを受け取り、構造解析を実行して結果を返します。
    GET/POSTリクエストに対応し、JSONデータの圧縮もサポートします。
    
    @param request HTTPリクエストオブジェクト
    @return 計算結果のJSON文字列またはエラーメッセージ
    
    @note Cloud Function、Flask、FastAPIで使用可能
    @note 大規模モデル用のgzip圧縮に対応

    Args:
        request (flask.Request): HTTPリクエスト

    Returns:
        str: 計算結果のJSON文字列
    """
    # region Set CORS headers for the preflight request（旧FWそのまま）
    if request.method == 'OPTIONS':
        headers = {
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Methods': 'GET, POST',
            'Access-Control-Allow-Headers': 'Origin, X-Requested-With, Content-Type, Accept, Content-Encoding, Authorization',
            'Access-Control-Max-Age': '3600'
        }
        return ('', 204, headers)
    # endregion

    # Set CORS headers for the main request（旧FWそのまま）
    headers = {
        'Content-Type': 'application/json; charset=utf-8',
        'Access-Control-Allow-Origin': '*',
        'Access-Control-Allow-Headers': 'Origin, X-Requested-With, Content-Type, Accept, Content-Encoding, Authorization'
    }

    # region テスト用コード（旧FWそのまま）
    if request.method == 'GET':
        return (json.dumps({ 'results': 'Hello World!'}), 200, headers)
    # endregion

    # データの送受信規格を調べる
    encoding = 'json'
    if 'content-encoding' in request.headers:
        encoding = request.headers['content-encoding']

    # region メイン計算の実行部
    try:
        # 入力データの取得（圧縮されている場合は解凍）
        if encoding == "json":
            inputJson: dict = request.get_json()
        else:  # 圧縮されている場合
            inputJson: dict = Compressor.decompress(request.data)

        result = build_analysis_result_set(inputJson)

         # 結果を返送する
        resultStr: str = json.dumps(result, allow_nan=False)
        if encoding == "json":
            response = resultStr
        else:  # 圧縮する場合
            response = Compressor.compress(resultStr)
        return (response, 200, headers)
    
    # 以下、エラー処理
    except MyCritical as e:  # システム起因と思われる例外
        payload, status = diagnostic_payload(e)
        payload['error'] = e.fixed_msg
        return (json.dumps(payload, ensure_ascii=False), status, headers)
    except MyError as e:  # ユーザー起因と思われる例外
        wrapped = InputValidationError(e.output_msg())
        payload, status = diagnostic_payload(wrapped)
        return (json.dumps(payload, ensure_ascii=False), status, headers)
    except (BadRequest, UnsupportedMediaType) as e:
        payload, status = diagnostic_payload(InputValidationError(str(e)))
        return (json.dumps(payload, ensure_ascii=False), status, headers)
    except Exception as e:  # その他の予期せぬエラー
        payload, status = diagnostic_payload(e)
        return (json.dumps(payload, ensure_ascii=False), status, headers)
    # endregion


# Compatibility name used by earlier functions-framework deployments.
FrameWeb3 = FEMPython

# JSON整数配列と旧ブラウザーの十進CSVを安全に扱う圧縮互換層
class Compressor():
    """データの圧縮・解凍用クラス"""

    @staticmethod
    def decompress(data: bytes) -> dict:
        """jsonデータを解凍し辞書型にフォーマットする

        Args:
            data (bytes): 圧縮されたjsonデータ

        Returns:
            _ (dict): 辞書型にフォーマットしたjsonデータ
        """
        try:
            decoded = base64.b64decode(data, validate=True)
        except binascii.Error as exc:
            raise InputValidationError("Invalid compressed input Base64") from exc

        compressed = Compressor._parse_byte_sequence(decoded)
        try:
            content = gzip.decompress(compressed)
        except (gzip.BadGzipFile, EOFError, zlib.error) as exc:
            raise InputValidationError("Invalid compressed input gzip data") from exc

        try:
            text = content.decode("utf-8")
        except UnicodeDecodeError as exc:
            raise InputValidationError("Compressed input JSON must be UTF-8") from exc
        try:
            result = json.loads(text)
        except (json.JSONDecodeError, ValueError) as exc:
            raise InputValidationError("Invalid compressed input JSON") from exc
        if not isinstance(result, dict):
            raise InputValidationError("Compressed input JSON must be an object")
        return result

    @staticmethod
    def _parse_byte_sequence(data: bytes) -> bytes:
        """Parse canonical JSON first, falling back only to strict decimal CSV."""
        try:
            text = data.decode("ascii")
        except UnicodeDecodeError as exc:
            raise InputValidationError(
                "Compressed byte sequence must be ASCII"
            ) from exc

        try:
            values = json.loads(text)
        except json.JSONDecodeError as exc:
            tokens = text.split(",")
            if any(
                not 1 <= len(token) <= 3
                or any(char < "0" or char > "9" for char in token)
                for token in tokens
            ):
                raise InputValidationError(
                    "Invalid compressed byte sequence CSV"
                ) from exc
            values = [int(token, 10) for token in tokens]
        except ValueError as exc:
            # Python's integer-string digit limit is reported as ValueError,
            # not JSONDecodeError. It is invalid JSON input, not legacy CSV.
            raise InputValidationError(
                "Invalid compressed byte sequence JSON"
            ) from exc

        if not isinstance(values, list) or any(
            type(value) is not int or not 0 <= value <= 255 for value in values
        ):
            raise InputValidationError(
                "Compressed byte sequence must contain integers from 0 to 255"
            )
        return bytes(values)
    
    @staticmethod
    def compress(js: str) -> str:
        """jsonデータを圧縮する

        Args:
            js (str): 圧縮対象のjsonの文字列データ

        Returns:
            _ (str): 圧縮したjsonデータ
        """
        # gzip圧縮する
        l = gzip.compress(js.encode())
        # Base64エンコードする
        byteBase64 = base64.b64encode(l)
        # stringに変換
        s = byteBase64.decode()
        return s

