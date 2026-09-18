## TL;DR

推奨は「バックエンド先行の二形式対応 → フロントエンドを正規形式へ変更」です。

- 緊急修正: FrameWeb が、正規の `[31,139,…]` と既存ブラウザの `31,139,…` の両方を安全に受理する。
- 恒久修正: FrameWebforJS は `base64(JSON.stringify(Array.from(gzipBytes)))` を送信する。
- `eval` の復活は不可。直接 `Base64(gzip(JSON))` へ変更する案は、別バージョンのプロトコルとして扱います。

## Analysis

原因は確認済みです。

- FrameWebforJS は [app.component.ts](</C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:229>) で `Uint8Array` を直接 `btoa()` に渡しています。
- JavaScript はこれを文字列 `31,139,8,…` に変換するため、実際の本文は `base64(ASCII("31,139,8,…"))` です。
- FrameWeb は [main.py](</C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:144>) でBase64復号後に `json.loads()` を実行するため、JSON配列でない本文を `Extra data` として拒否します。
- 2026-09-08のコミット `29df328` が、危険な `eval(b)` を `json.loads(b)` に置き換えました。セキュリティ修正自体は正しいものの、暗黙に許容されていた形式を狭めています。
- 既存テストは [test_input_routes.py](</C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/integration/test_input_routes.py:86>) などで、すべて `json.dumps(list(compressed))`、つまり `[31,139,…]` を生成しています。実際のAngular producerを通していません。

## Options Compared

| 案 | 互換性 | 安全性 | 評価 |
|---|---|---|---|
| `eval` に戻す | 高い | 任意コード実行につながる | 却下 |
| FrameWebforJSだけ修正 | 新クライアントは旧・新バックエンド両方で動く | 良好 | 最小だが、古い・キャッシュ済みクライアントは復旧しない |
| FrameWebだけ二形式対応 | 既存クライアントを即時復旧 | 厳密なCSVパーサーなら良好 | 緊急修正として適切 |
| バックエンド先行＋フロント修正 | 新旧組み合わせすべてを段階的にカバー | 良好 | 推奨 |
| 直接 `Base64(gzip(JSON))` に統一 | 効率と対称性は最良 | 良好 | 現行契約と非互換。別のv2移行向け |
| HTTP標準のraw gzipへ移行 | 最も標準的 | 良好 | ProxyやCloud Functionの自動展開も確認が必要。今回には広すぎる |

特にフロントエンドの正規形式は、旧バックエンドの `eval("[31,…]")` と現行バックエンドの `json.loads("[31,…]")` の両方で読めます。ただし、フロントエンドだけ直しても既存クライアントは直らない点が問題です。

## Plan

1. FrameWebを先に配備し、二形式を受理する。
2. FrameWebforJSを正規のブラケット付き形式へ変更する。
3. 旧CSV形式の利用をログまたはメトリクスで観測する。
4. 旧クライアントが残らないことを確認できた場合だけ、将来CSV互換を削除する。
5. より効率的な直接Base64形式へ移行するなら、現行修正とは分離してwire versionを付ける。

## Patch Strategy

### FrameWeb

`Compressor.decompress()` を、次の順序に分離します。

1. `base64.b64decode(..., validate=True)` で厳密にBase64復号。
2. 先頭の非空白文字が `[` ならJSON配列として解析。
3. それ以外は、正規表現または厳密な分割処理で、カンマ区切り10進整数だけを解析。
4. 両形式とも以下を検証。

   - 配列である
   - 空でない
   - 各要素が `bool` ではなく整数
   - `0 <= value <= 255`
   - ネスト、文字列、小数、指数、符号、空要素を拒否
   - gzipマジックと正常なgzipストリームを確認

単純に `json.loads(b"[" + body + b"]")` とするより、旧形式の文法を `0..255` の10進整数CSVに限定する方が契約が明確です。

入力サイズと展開後サイズにも上限が必要ですが、現行モデルの最大実績を測らずに値を決めると正常な大規模解析を壊します。回帰修正とは分離し、保守的な値を決めて導入するのが安全です。現在の `gzip.decompress()` にも展開爆弾への上限はありません。

### FrameWebforJS

現在の

```ts
const base64Encoded = btoa(compressed);
```

を概念上次へ変更します。

```ts
const byteArrayJson = JSON.stringify(Array.from(compressed));
const base64Encoded = btoa(byteArrayJson);
```

エンコード処理は純粋関数へ抽出し、コンポーネントとは独立してテストできる形が望ましいです。

`Content-Type: application/json` と `Content-Encoding: gzip,base64` はHTTPの標準的な意味とは一致していませんが、今回同時に変更すると互換性範囲が広がります。現行修正では維持し、プロトコルv2で整理すべきです。

## Validation

最低限、次を追加します。

- 正規 `[31,139,…]` が成功する。
- 実際の `btoa(pako.gzip(...))` が生成する旧 `31,139,…` が成功する。
- Ct桁プリセットをAngularとFrameWebの境界まで通す。
- 新フロントエンド形式が旧バックエンドでも成功することを互換テストで確認する。
- 不正Base64、空本文、不正UTF-8、末尾カンマ、二重カンマを拒否する。
- `-1`、`256`、小数、指数、文字列、`true`、`null`、ネスト配列を拒否する。
- gzipでないバイト列、途中切断、CRC不正、展開後がJSONでない本文を拒否する。
- `eval` が圧縮経路に再導入されていないことを静的確認する。
- 既存の圧縮HTTPテストを両形式でパラメータ化する。

## Risks

- フロントエンド先行だけでは、古いブラウザ資産や外部クライアントが復旧しません。
- 二形式対応を曖昧な汎用パーサーにすると、再び契約が拡散します。旧形式は厳密な整数CSVだけに限定すべきです。
- 旧形式の廃止時期を利用状況なしで決めると再度互換性障害になります。
- print経路にも同じ `btoa(pako.gzip(...))` が存在しますが、別サービス向けなので、今回の計算API修正を機械的に適用せず個別に監査すべきです。

今回は設計・比較のみで、製品ファイルは変更していません。Codex consultation用ラッパーは監査ログを書けないread-only環境のため起動できず、現在のコード、Git blame/show、既存診断資料、およびNodeでの実際の型変換確認を根拠にしています。
