# HTTP API

[Wikiホーム](index.md) · [モデルの入力](data-structures.md) · [結果の読み方](results.md) · [エラーと対処](error-handling.md)

HTTP APIは、Pythonの`FemModel`と同じ解析経路を使います。1回のPOSTで1つの荷重条件を解析し、結果をJSONで返します。

## ローカルサーバーの起動

リポジトリ直下で次を実行します。[環境の準備](getting-started.md)が済んでいることを確認してください。

```console
uv run --locked --extra dev python -m flask --app main:app run --host 127.0.0.1 --port 5000
```

別のターミナルから、以降のクライアント例を実行します。例ではPython標準ライブラリを使うため、追加のHTTPクライアントライブラリは不要です。

## エンドポイント

| メソッド・パス | 応答 | 用途 |
|---|---|---|
| `POST /` | 成功時200と解析結果 | モデルを解析する |
| `GET /` | 200、`{"results": "Hello World!"}` | HTTP経路の疎通確認。解析の健全性・バージョンを返す機能ではない |
| `OPTIONS /` | 204、空ボディ | CORSのプリフライト |

通常のPOSTは`Content-Type: application/json`にします。`Content-Encoding`は省略するか`json`を指定します。

CORSは`Access-Control-Allow-Origin: *`で、プリフライトではGET・POSTと、Content-Type・Content-Encoding・Authorizationなどのヘッダーを許可します。認証・レート制限はこのアプリ内には実装されておらず、Authorizationヘッダーを送るだけで認証処理が行われるわけではありません。

## 通常JSONで解析する

[はじめに](getting-started.md)の`beam.json`をクライアントの作業ディレクトリへ保存して実行します。

<!-- run: http-json -->
```python
import json
from pathlib import Path
from math import isclose
from urllib.request import Request, urlopen

url = "http://localhost:5000/"
model_data = json.loads(Path("beam.json").read_text(encoding="utf-8"))
request = Request(url, data=json.dumps(model_data).encode("utf-8"),
                  headers={"Content-Type": "application/json"}, method="POST")
with urlopen(request, timeout=30) as response:
    result = json.load(response)

assert result["analysis_type"] == "static"
assert isclose(result["node_displacements"]["2"]["dy"], -1/750, rel_tol=1e-8)
print("先端変位 [m]:", result["node_displacements"]["2"]["dy"])
print("支点反力:", result["reaction_forces"]["1"])
```

### 入力と解析モード

POSTは編集用`node/member/element`形式と、保存用`nodes/elements/materials`形式の両方を受け付けます。ファイル名や `.fem` ファイルそのものをPOSTする仕様ではありません。

`analysis_type`を省略したときは、入力内の指定と非線形要素の有無から解析を選びます。詳しくは[解析の選び方](elements.md)を参照してください。`load_factors`は材料非線形の載荷係数です。

編集用JSONの`load`に複数ケースがある場合は、先頭のケースだけを解析します。結果に荷重ケース名の階層は付きません。全ケースの処理は[ケースごとの実行例](examples.md)と同様に、クライアント側で1ケースずつ送ります。

### 成功時の結果

線形解析では`analysis_type`、`node_displacements`、`reaction_forces`、`element_stresses`などを返します。非線形解析では`converged`、`step_results`、`curvature`、`convergence_history`が加わります。固有値解析では`frequencies`、`periods`、`modes`などになります。すべての成功結果に、製品version、入力SHA-256、解析条件、座標・単位宣言、残差、反復数、警告、高精度経路をまとめた`metadata`が付きます。

JSONのIDは文字列です。`case1.disg/reac/fsec`を返す旧説明とは異なります。単位・符号・任意項目は[結果の読み方](results.md)を参照してください。

## 互換用の圧縮転送

通常のJSON送信から始めることを推奨します。既存クライアントとの互換用に圧縮経路もありますが、**標準HTTPのgzip転送とは異なり、要求と応答の包み方も非対称**です。

| 方向 | 実際の形式 |
|---|---|
| 要求 | JSONをUTF-8化 → gzip → バイト値の配列をJSON文字列化 → Base64 |
| 成功応答 | 結果JSONをUTF-8化 → gzip → Base64 |
| エラー応答 | 通常のJSON。圧縮しない |

要求に`Content-Encoding: gzip`を付けるとこの互換経路に入ります。`Base64(gzip(JSON))`だけの要求や、生のgzipバイト列は現行の要求形式ではありません。

<!-- run: http-compressed -->
```python
import base64
import gzip
import json
from pathlib import Path
from math import isclose
from urllib.request import Request, urlopen

url = "http://localhost:5000/"
model_data = json.loads(Path("beam.json").read_text(encoding="utf-8"))
compressed = gzip.compress(json.dumps(model_data).encode("utf-8"))
request_body = base64.b64encode(json.dumps(list(compressed)).encode("utf-8"))
request = Request(url, data=request_body, method="POST", headers={
    "Content-Type": "application/json",
    "Content-Encoding": "gzip",
})
with urlopen(request, timeout=30) as response:
    result = json.loads(gzip.decompress(base64.b64decode(response.read())).decode("utf-8"))

assert isclose(result["reaction_forces"]["1"]["fy"], 1000, rel_tol=1e-8)
print(result["node_displacements"]["2"])
```

圧縮の選択は要求の`Content-Encoding`によります。応答サイズによる自動切替や`Accept-Encoding`との交渉はありません。成功した圧縮応答には現在`Content-Encoding`が付かず、Content-TypeもJSONのままです。クライアントは自分が選んだ要求方式に合わせて復号します。

## エラー応答

| HTTP | 代表的な`error_code` | 意味 |
|---:|---|---|
| 400 | `invalid_input` | JSON形式・値・参照などの問題 |
| 400 | `unsupported_analysis` | 未知の解析種別 |
| 422 | `unsupported_analysis` | 要素・荷重と解析種別の未対応組合せ |
| 422 | `structural_mechanism` | 剛体運動・特異剛性 |
| 422 | `numerical_ill_conditioning` | 数値ランク・釣合い精度の問題 |
| 422 | `nonlinear_nonconvergence` / `modal_nonconvergence` | 反復解法の未収束 |
| 500 | `analysis_failure` | 分類できない線形代数・結果処理・内部例外 |

エラーの共通項目は`error`、`error_code`、`error_category`、`converged: false`です。
確定した節点・自由度・要素・失敗段階などがある場合だけ`details`が付きます。

```json
{
  "error": "Nonlinear analysis did not converge at step 2 (load factor 0.5)",
  "error_code": "nonlinear_nonconvergence",
  "error_category": "convergence",
  "converged": false,
  "step": 2,
  "load_factor": 0.5,
  "details": {"step": 2, "load_factor": 0.5}
}
```

Python標準の`urlopen()`は400・422・500で`HTTPError`を送出します。エラーのボディは、圧縮要求の場合でもJSONとして読みます。

<!-- run: http-invalid-input -->
```python
import json
from urllib.request import Request, urlopen
from urllib.error import HTTPError

request = Request("http://localhost:5000/", data=b"{}", method="POST",
                  headers={"Content-Type": "application/json"})
try:
    with urlopen(request, timeout=30) as response:
        raise AssertionError("空モデルは成功しないはずです")
except HTTPError as error:
    body = json.loads(error.read().decode("utf-8"))
    assert error.code == 400
    assert body["converged"] is False
    print(error.code, body["error"])
```

特異な静解析モデルは`structural_mechanism`、非線形反復で同じ状態へ到達した場合は
`nonlinear_nonconvergence`になることがあります。後者は反復中に機構の原因を確定できないためです。
[エラーと対処](error-handling.md)の手順でモデル条件を確認してください。

## 運用上の挙動

POSTは同期処理です。ジョブIDの発行、進捗取得、キャンセル用エンドポイントはありません。クライアントがタイムアウトしても、サーバー側で計算が停止したことを意味しません。環境の処理時間・メモリ制約は、ライブラリの固定上限とは別に確認してください。
