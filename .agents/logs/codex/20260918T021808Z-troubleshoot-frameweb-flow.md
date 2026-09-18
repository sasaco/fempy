## 結論

原因は、FrameWebforJS と FrameWeb の圧縮リクエスト形式の不一致です。

```text
Angular入力オブジェクト
 → JSON文字列
 → gzip Uint8Array [31,139,8,...]
 → 暗黙の文字列化 "31,139,8,..."
 → Base64
 → HTTP POST
 → Base64復号 "31,139,8,..."
 → json.loads()
 → 31をJSON数値として読了
 → 直後の「,」で Extra data (char 2 / column 3)
```

構造解析処理には到達していません。

## 正確な実行経路

1. 計算ボタン

[app.component.html](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.html:20) のクリックが `calcrate()` を呼びます。

2. 入力データの生成

[app.component.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:196) で次の処理が実行されます。

- ログインまたはローカル匿名計算を確認。
- `InputData.getInputJson(0)` を呼ぶ。
- 入力エラーを確認。
- ユーザー確認ダイアログを表示。
- 待機ダイアログを開く。
- `uid` と `production` を追加。
- 既存の計算結果を消去。
- `post_compress()` を呼ぶ。

`getInputJson(0)` は [input-data.service.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/providers/input-data.service.ts:199) で、各入力サービスの状態を一つのオブジェクトに統合します。`empty === 0` は計算用モードで、必須データ検証と2次元モデルの正規化もここで行われます。

3. フロント側のシリアライズ

[app.component.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:229) では次の変換が行われます。

```typescript
const json = JSON.stringify(jsonData);
const compressed = pako.gzip(json);
const base64Encoded = btoa(compressed);
```

状態は次のように変わります。

| 段階 | 値・型 |
|---|---|
| `jsonData` | JavaScript object |
| `json` | JSON文字列 |
| `compressed` | `Uint8Array([31, 139, 8, ...])` |
| `btoa()`への実入力 | 暗黙に文字列化された `"31,139,8,..."` |
| `base64Encoded` | 上記ASCII文字列のBase64 |

つまり、gzipバイト列そのものをBase64化しているのではありません。角括弧のない10進数CSVをBase64化しています。

4. HTTP送信

同じ箇所から `environment.calcURL`、現在のローカル構成では `http://127.0.0.1:8080/` へ次のヘッダーで送信されます。

```http
Content-Type: application/json
Content-Encoding: gzip,base64
```

5. FrameWeb のルーティング

[main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:91) は `Content-Encoding` を取得します。

値は `"gzip,base64"` で `"json"` ではないため、`request.get_json()` ではなく次へ進みます。

```python
inputJson = Compressor.decompress(request.data)
```

6. Base64復号

[main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:144) の `Compressor.decompress()` が次を実行します。

```python
b = base64.b64decode(data)
```

この時点の `b` はgzipバイト列ではなく、次のASCIIバイト列です。

```text
b'31,139,8,0,0,0,...'
```

7. `JSONDecodeError` 発生点

続いて [main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:156) が実行されます。

```python
l = json.loads(b)
```

Python JSONデコーダーは先頭の `31` を完全なJSON数値として認識します。

```text
31,139,...
^^
JSON値「31」はここで完結
  ^
  char 2、1始まりでは column 3 のカンマが余剰データ
```

そのため、正確に次が発生します。

```text
JSONDecodeError: Extra data: line 1 column 3 (char 2)
```

後続の次の処理には到達しません。

```python
gzip.decompress(bytes(l))
json.loads(fstr)
_read_json_model(inputJson)
FemModel.read_json_model(...)
FemModel.run()
```

したがって、Ct桁のモデル内容や解析ソルバーは、このエラーの直接原因ではありません。

8. HTTP 400への変換

`JSONDecodeError` は `ValueError` の派生クラスです。

[main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:130) の包括的な例外処理から [diagnostics.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/diagnostics.py:52) に渡され、`ValueError` は次へ分類されます。

```json
{
  "error": "Extra data: line 1 column 3 (char 2)",
  "error_code": "invalid_input",
  "error_category": "input",
  "converged": false
}
```

HTTPステータスは `400` です。

9. FrameWebforJSでの表示

Angular `HttpClient` は非2xx応答を成功コールバックではなくエラーコールバックへ渡します。

[app.component.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:319) が次を行います。

- `message.transmission-error` を表示。
- `HttpErrorResponse` をコンソールへ出力。
- 待機ダイアログを閉じる。

日本語メッセージは [ja.json](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/assets/i18n/ja.json:439) の以下です。

```text
通信エラーが発生しました。
担当営業にお問い合わせください。
```

## 再現結果

既存のCt桁再現スクリプトを再実行し、同一のgzip出力で確認しました。

- 元JSON: 26,630文字
- gzip: 4,080バイト
- 現行形式のBase64復号結果: `31,139,8,...`
- サーバー応答: HTTP 400
- エラー: `Extra data: line 1 column 3 (char 2)`

同じ4,080バイトをJSON配列として包んだ形式、

```text
[31,139,8,...]
```

ではJSON配列の解析、`bytes(...)`、gzip展開を通過し、元の26,630文字と完全一致しました。これにより、gzip内容ではなく外側の転送エンベロープが障害境界だと確定できます。

## 回帰が発生した変更

コミット `29df328eb29d1a17b91f706eb1b7f6dd16a0fa78` が、バックエンドの次の処理を変更しています。

```diff
- l = eval(b)
+ l = json.loads(b)
```

旧 `eval(b'31,139,8,...')` はカンマ区切り値をPythonのタプルとして偶然受理していました。`json.loads()` への変更は任意コード実行を防ぐ正しいセキュリティ強化ですが、受理可能な転送形式をJSON配列だけに狭めました。

フロントの角括弧なし形式が残ったため、ここで互換性回帰が顕在化しています。

既存のバックエンド圧縮テストも、例えば [test_input_routes.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/integration/test_input_routes.py:86) で最初から次の正しい角括弧付き形式を生成しています。

```python
json.dumps(list(gzip.compress(...)))
```

そのため、実際のAngular producerである `btoa(Uint8Array)` の形式不一致を検出できませんでした。

調査はread-onlyで実施し、ファイル変更はありません。
