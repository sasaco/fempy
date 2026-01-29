import sys
import os

# ここでsrcをパスに追加
src_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "src")
if src_path not in sys.path:
    sys.path.insert(0, src_path)

import json
import base64
import gzip
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
from fem.model import FemModel
from fem.file_io import _read_json_model, read_model

# Flaskアプリの作成
app = Flask(__name__)

@app.route('/', methods=['OPTIONS', 'GET', 'POST'])
def post():
    return FrameWeb3(request)


# FrameWeb3の定義
@functions_framework.http
def FrameWeb3(request):
    """FrameWeb3のメイン関数
    
    @brief FrameWeb3 APIのメインエントリーポイント
    
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

         # FemModelで解析実行
        model_data = _read_json_model(inputJson)
        fem_model = FemModel()
        fem_model.read_json_model(model_data)
        result: dict  = fem_model.run("static")

         # 結果を返送する
        resultStr: str = json.dumps(result)
        if encoding == "json":
            response = resultStr
        else:  # 圧縮する場合
            response = Compressor.compress(resultStr)
        return (response, 200, headers)
    
    # 以下、エラー処理
    except MyCritical as e:  # システム起因と思われる例外
        return (json.dumps({'error': e.fixed_msg}, ensure_ascii=False), 200, headers)
    except MyError as e:  # ユーザー起因と思われる例外
        return (json.dumps({'error': e.output_msg()}, ensure_ascii=False), 200, headers)
    except Exception as e:  # その他の予期せぬエラー
        return (json.dumps({'error': "予期せぬエラーが発生しました。"}, ensure_ascii=False), 200, headers)
    # endregion

    

# データの圧縮用クラスの定義（旧FWそのまま）
class Compressor():
    """データの圧縮・解凍用クラス"""

    @staticmethod
    def decompress(data) -> dict:
        """jsonデータを解凍し辞書型にフォーマットする

        Args:
            data (Any): 圧縮されたjsonデータ

        Returns:
            _ (dict): 辞書型にフォーマットしたjsonデータ
        """
        # base64型を元に戻す
        b = base64.b64decode(data)
        # str型に変換し、カンマでばらしてint配列に変換する
        l = eval(b) #[int(n) for n in b.decode().split(',')]
        # gzipを解凍する
        fstr = gzip.decompress(bytes(l))
        # jsonを辞書型にフォーマット
        js = json.loads(fstr, object_pairs_hook=dict)
        return js
    
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

