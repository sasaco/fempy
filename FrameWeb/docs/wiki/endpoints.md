# HTTP API

[Wikiホーム](index.md) · [モデルの入力](data-structures.md) · [結果の読み方](results.md) · [エラーと対処](error-handling.md)

計算APIの成功応答は、解析種別やケース数にかかわらず `AnalysisResultSet v1` の一形式だけです。結果表現を選ぶ `Accept` ヘッダー、vendor media type、旧case map、flat結果はありません。

## ローカルサーバーの起動

リポジトリ直下で次を実行します。

```console
uv --directory FrameWeb run --locked --extra dev python -m flask --app main:app run --host 127.0.0.1 --port 5000
```

## エンドポイント

| メソッド・パス | 応答 | 用途 |
|---|---|---|
| `POST /` | 成功時200、`application/json; charset=utf-8` | モデルを解析する |
| `GET /` | 200、`{"results": "Hello World!"}` | HTTP経路の疎通確認 |
| `OPTIONS /` | 204、空ボディ | CORSプリフライト |

通常のPOSTは `Content-Type: application/json` にします。`Accept` は成功形式を変更しません。

## 通常JSONで解析する

```python
import json
from pathlib import Path
from urllib.request import Request, urlopen

model_data = json.loads(Path("beam.json").read_text(encoding="utf-8"))
request = Request(
    "http://localhost:5000/",
    data=json.dumps(model_data).encode("utf-8"),
    headers={"Content-Type": "application/json"},
    method="POST",
)
with urlopen(request, timeout=30) as response:
    result_set = json.load(response)

assert result_set["kind"] == "analysis_result_set"
assert result_set["schema_version"] == "1.0"
first = result_set["results"][0]
tip = next(row for row in first["node_displacements"] if row["node_id"] == "2")
print("先端変位:", tip["components"])
```

## 入力ケースの扱い

- 既存の編集用 `node/member/load` 入力では、非空の `load` mapの全entryを挿入順に解析します。上限は256ケースです。
- 各legacy caseは独立したモデルで解析します。`case_id` は元のmap keyの文字列表現です。
- `name` と `symbol` はcase内の非空文字列を使い、なければ個別に `case_id` へフォールバックします。
- top-level `analysis_type`、caseの `analysis_type`、モデル推論の順で解析種別を決めます。
- 解析parameterは既定値、case値、top-level `analysis_params` の順で上書きします。
- 既存の保存用 `nodes/elements` 入力は、従来どおり単一ケースです。case ID・名前・symbolはいずれも `"1"` です。
- case内の `rate` は結果を乗算しません。倍率を変える場合は入力荷重値、材料非線形の載荷経路には `load_factors` を使います。

ケース途中で入力・解析・結果投影が失敗した場合、途中までの結果は返しません。

### 解析作業量の上限

同期APIのCPU・メモリ使用量を制限するため、モデルを生成する前に全caseの解析設定を検査します。

| 対象 | 上限 |
|---|---:|
| 1 caseの非線形step数 | 1,000 |
| 1 stepの最大反復回数 (`max_iterations`) | 1,000 |
| request全体の結果state見積数 | 10,000 |
| request全体の非線形反復見積数 | 500,000 |

非線形step数は、`load_factors`指定時は配列長、変位制御で`targets`を指定した場合はその配列長、それ以外は`n_load_steps`です。request全体の非線形反復見積数は各caseの「step数 × `max_iterations`」の合計です。modal解析では`n_modes`を結果state数として数えます。解析種別を省略した入力はモデル生成後にstaticまたはmaterial nonlinearへ推論するため、事前検査では非線形の見積りを予約します。

上限値そのものは受理され、超過はHTTP 400 `invalid_input`です。`details`には超過を確定した`case_id`、`budget`、`requested`、`limit`が入ります。legacy入力ではcase値にtop-level `analysis_params`を上書きした実効設定を使用します。

## 成功応答

ルートは常に次の7項目です。詳細は[結果の読み方](results.md)を参照してください。

```json
{
  "kind": "analysis_result_set",
  "schema_version": "1.0",
  "units": {
    "system": "consistent_user_defined",
    "length": "unspecified",
    "force": "unspecified",
    "mass": "unspecified",
    "time": "unspecified"
  },
  "coordinate_system": {
    "name": "global_cartesian",
    "handedness": "right",
    "axes": ["x", "y", "z"]
  },
  "cases": [],
  "topology": {
    "nodes": [],
    "members": [],
    "shell_elements": [],
    "solid_elements": []
  },
  "results": []
}
```

`cases` は入力順、`results` はcase-majorかつstate index昇順です。静解析はcaseごとに1件、材料非線形は収束したload stepごとに1件、modalはmodeごとに1件です。すべての数値は有限値で、ID・参照・topology coverageを満たします。

## 圧縮転送

圧縮は結果表現ではなく転送形式だけを変更します。通常JSONと圧縮要求は、復号後に同じ `AnalysisResultSet` を返します。

| 方向 | 形式 |
|---|---|
| 要求（正規） | JSON UTF-8 → gzip → byte値のJSON整数配列 → Base64 |
| 要求（旧ブラウザー互換） | JSON UTF-8 → gzip → 括弧なし10進CSV → Base64 |
| 成功応答 | AnalysisResultSet JSON UTF-8 → gzip → Base64 |
| エラー応答 | 通常JSON。圧縮しない |

要求に `Content-Encoding: gzip` または `gzip,base64` を付けます。旧CSV受理は入力transportだけの互換機能で、旧結果schemaは復活させません。`eval` は使わず、0～255の整数byteだけを受理します。

```python
import base64
import gzip
import json
from urllib.request import Request, urlopen

compressed = gzip.compress(json.dumps(model_data).encode("utf-8"))
body = base64.b64encode(json.dumps(list(compressed)).encode("utf-8"))
request = Request(
    "http://localhost:5000/",
    data=body,
    headers={"Content-Type": "application/json", "Content-Encoding": "gzip"},
    method="POST",
)
with urlopen(request, timeout=30) as response:
    result_set = json.loads(
        gzip.decompress(base64.b64decode(response.read())).decode("utf-8")
    )
assert result_set["kind"] == "analysis_result_set"
```

## エラー応答

| HTTP | 代表的な `error_code` | 意味 |
|---:|---|---|
| 400 | `invalid_input` | JSON形式・値・参照・case数などの問題 |
| 400/422 | `unsupported_analysis` | 未知または未対応の解析組合せ |
| 422 | `structural_mechanism` | 剛体運動・特異剛性 |
| 422 | `numerical_ill_conditioning` | 数値ランク・釣合い精度の問題 |
| 422 | `nonlinear_nonconvergence` / `modal_nonconvergence` | 反復解法の未収束 |
| 500 | `analysis_failure` | 結果契約・投影を含む内部失敗 |

エラーJSONは `error`、`error_code`、`error_category`、`converged: false` を持ちます。特定できる場合は `details.case_id`、step、load factor等も付きます。失敗応答が成功schemaや部分的な `results` を含むことはありません。

## 運用上の挙動

POSTは同期処理です。ジョブID、進捗取得、キャンセルendpoint、結果streamingはありません。クライアントのtimeoutだけではサーバー側の解析停止を保証しません。
