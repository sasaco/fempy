## TL;DR

- 判定: **NEEDS_REVISION**
- 基本設計は目的に合致しています。JSON配列を優先し、構文エラー時だけ厳格な十進CSVへフォールバックする方針は妥当です。
- ただし「安全に」の完了条件、実フロント形式の自動回帰試験、再現可能な検証コマンドに不足があります。

## Analysis

### 1. 高: 無制限gzip展開を別課題へ送ると「安全に」を満たす根拠が弱い

計画はgzip展開サイズ制限を別課題へ送っています（[legacy-compressed-input-compatibility.md:64](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/legacy-compressed-input-compatibility.md:64)）。しかし対象は未信頼HTTP入力であり、`gzip.decompress()` は展開量を制限しません。

次のどちらかを計画に明記すべきです。

- 今回、圧縮本文と展開後JSONに上限を設け、超過をHTTP 400または413にする。
- 今回の「安全」を「コード評価経路を作らないこと」に限定すると目的を明確化し、DoS耐性を受入条件外と明記する。

現状の目的文では、後者を暗黙に仮定するには曖昧です。

### 2. 中: 実フロント形式のテストが必須ではなく選択式

計画は「Node再現スクリプト**または**そこから採取した要求本文」としています（[同計画:21](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/legacy-compressed-input-compatibility.md:21)）。これでは実装者がPythonで生成したCSVだけを試し、実際の次のJavaScript挙動を固定しない可能性があります。

[app.component.ts:236](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:236) では `pako.gzip()` の `Uint8Array` を直接 `btoa()` へ渡しています。この暗黙文字列化が括弧なしCSVを生む核心です。

少なくとも一つ、Nodeで生成した実 envelope を自動テスト用fixtureに固定し、JSON配列と同じモデルへ復元されることを必須にすべきです。可能なら小型モデルを使い、巨大なCtプリセットは手動E2Eに分離します。

### 3. 中: CSV数値変換の既知エラーが例外契約から漏れている

[同計画:32](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/legacy-compressed-input-compatibility.md:32) は字句検証後に `int(token, 10)` を呼びますが、極端に長い数字列はPythonの整数文字列桁数制限により `ValueError` になり得ます。一方、変換対象として列挙された例外には `ValueError` がありません（[同計画:34](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/legacy-compressed-input-compatibility.md:34)）。

HTTP層では現在、一般の`ValueError`も偶然400へ分類されますが、`Compressor.decompress()` 自体の契約は不統一になります。各トークンを最大3桁に制限してから変換するのが最も明快です。総トークン数または外側復号サイズの境界試験も必要です。

### 4. 中: E2E検証手順がそのまま実行できない

再現スクリプトはポート8080へ接続しますが、文書の起動例は5000です。Step 5には具体的な起動コマンド、ポート、終了方法、期待終了コードがありません。

またStep 4の「既存ドキュメント検証」も実際のコマンドが指定されていません（[同計画:49](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/legacy-compressed-input-compatibility.md:49)）。存在が確認できないゲートを書くより、実在するコマンドを特定するか、Markdownリンク・サンプル検証を個別のpytestとして追加すべきです。

### 5. 低: REDフェーズの判定が曖昧

「旧CSV受理と不足している型検証だけがRED」（[同計画:27](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/plans/legacy-compressed-input-compatibility.md:27)）では、どのテストIDが失敗し、既存JSON配列テストは何件成功すべきか判定できません。

既存JSON配列の保持を重要な目的としているため、RED時点でも以下はPASSすると明記すると安全です。

- 現行JSON整数配列の直接復元
- 現行JSON整数配列のHTTP経路
- 通常JSON経路
- コード風入力の非実行

## Plan

計画へ次を追加・修正すれば、目的に対して十分な内容になります。

1. 「安全」の範囲を明文化し、展開サイズ制限を今回実装するか明示的に除外する。
2. Node生成の実FrameWeb2 envelope fixtureを自動回帰試験の必須入力にする。
3. CSVトークンを変換前に1～3桁へ制限し、巨大数字列の拒否試験を追加する。
4. サーバー起動、ポート、Nodeスクリプト実行、終了条件を具体的なコマンドにする。
5. Step 4のドキュメント検証を実在するコマンドへ置き換える。
6. REDフェーズの期待PASS/FAILテストをテストID単位で記載する。

## Patch Strategy

今回はレビューのみで、ファイルは変更していません。

修正対象は計画書だけで足ります。実装対象として挙げた `FrameWeb/main.py`、新規テスト、`endpoints.md` のスコープは適切です。フロントエンドを変更しない方針も目的と一致しています。

## Validation

計画書の形式ゲートは成功しました。

```text
plan-doc: PASS
sections_missing: []
warnings: []
```

設計面では以下を確認しました。

- 現行バックエンドは非`json`の全Content-Encodingを圧縮経路へ送る。
- 現行形式はBase64内のJSON整数配列。
- 実フロントは `pako.gzip(json)` の返り値を直接 `btoa()` へ渡す。
- `gzip` と `gzip,base64` は現行ルーティング上、同じ圧縮処理になる。
- 通常JSON、圧縮JSON配列、成功応答の再圧縮には既存回帰試験がある。

## Risks

提案された二形式パーサー自体は、`eval`や`literal_eval`を使わず、JSON成功後の型不正をCSVへフォールバックさせないため、安全な方向です。残る主要リスクはコード実行ではなく、無制限入力・展開による資源消費と、「実フロント形式」を模しただけのテストが実装されることです。
