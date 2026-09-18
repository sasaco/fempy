結論: **NEEDS_REVISION（軽微だが実装前に修正推奨）**です。

計画の基本設計は目的に合致しています。実際の旧フロントは `btoa(compressed)` に `Uint8Array` を渡すため、暗黙の文字列化によって `31,139,...` 形式を送信します。一方、現行バックエンドは外側を JSON 配列としてのみ解析するため、`Extra data` になります。旧版の `eval(b)` がCSVを受理していたことも確認できました。

修正すべき点は次の4点です。

1. Base64を明示的に厳格化する  
   Step 2へ `base64.b64decode(data, validate=True)` を明記してください。現在の「不正Base64を400にする」だけでは、標準ライブラリの既定動作が非Base64文字を無視するため、受理境界が曖昧です。

2. 捕捉する例外を具体化する  
   「既知の輸送例外」ではなく、少なくとも以下を列挙してください。

   - `binascii.Error`: Base64不正
   - `UnicodeDecodeError`: 外側ASCII／内側UTF-8不正
   - `json.JSONDecodeError`: 外側JSON判定と内側JSON不正
   - `gzip.BadGzipFile`
   - `EOFError`: 途中切れgzip
   - `zlib.error`: gzip展開エラー

   外側の `JSONDecodeError` だけがCSVフォールバックを起動し、内側JSONの失敗はフォールバックさせず400にする必要があります。

3. 展開後JSONのトップレベル型を固定する  
   `Compressor.decompress()` は `dict` を返す契約なので、展開後が配列、文字列、数値、`null` のケースを明示的に拒否するテストを追加してください。現在の計画では「展開後の不正JSON」はありますが、「構文上正しい非object JSON」が抜けています。

4. HTTP試験を明確なマトリクスにする  
   次の4組すべてを固定すると、ヘッダーと外側形式を誤って結合する実装を防げます。

   | Content-Encoding | 外側形式 |
   |---|---|
   | `gzip` | JSON整数配列 |
   | `gzip` | 括弧なしCSV |
   | `gzip,base64` | JSON整数配列 |
   | `gzip,base64` | 括弧なしCSV |

   特に最後の組が実フロントの実形式です。単なるPythonの `",".join(...)` に加え、既存のNode再現スクリプト／取得済みリクエスト本文をfixtureとして確認するのが確実です。

そのほかは適切です。

- JSON解析成功後の型不正をCSVへフォールバックしない
- `type(value) is int` で `bool` を除外する
- 一次元・0～255を共通検証する
- `eval`／`literal_eval`を使わない
- フロントを変更しない
- 現行JSON整数配列を正規形式として維持する
- 輸送修復とCt桁の計算結果契約を分離する
- gzip展開サイズ制限を既存の独立課題として扱う

なお、「暗黙の数値変換を使用しない」は少し曖昧です。CSVトークンをASCII数字だけと字句検証した後の `int(token, 10)` は安全なので、その旨に書き換えると実装者が迷いません。

対象箇所: [計画書](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/legacy-compressed-input-compatibility.md)、[現行バックエンド](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:140)、[実フロント送信処理](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:229)、[現行HTTP文書](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/docs/wiki/endpoints.md:69)

上記4点を加えれば、この目的に対して実装可能な安全性と回帰保証を備えた計画になります。ファイル変更は行っていません。Codex補助レビューは監査ログを書けないread-only環境のため起動前に停止したので、この判定はリポジトリ実物との直接照合に基づきます。
