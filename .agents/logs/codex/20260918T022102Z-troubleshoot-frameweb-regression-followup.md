継続調査は完了しました。製品コードはまだ変更していません。

根本原因は、計算フロントエンドの `btoa(Uint8Array)` が gzip バイト列を `31,139,...` という括弧なしCSVへ暗黙変換する一方、バックエンドが安全な `json.loads()` で `[31,139,...]` 形式を要求している契約不一致です。これにより gzip 展開前に HTTP 400 `Extra data: line 1 column 3` が発生します。

推奨修正は計算経路だけを次の形式に変更することです。

```ts
btoa(JSON.stringify(Array.from(compressed)))
```

重要な境界事項：

- `eval` は復活させない
- 印刷経路は変更しない。C# 側は括弧なしCSVを意図的に受理しており、共通化すると印刷が壊れる
- JavaScript生成物をPythonデコーダーへ渡す境界テストを追加する
- 実ブラウザで計算完了と変位・反力・断面力の表示まで確認する
- 古いキャッシュ済みフロントも支援する必要がある場合だけ、バックエンドへ厳格で期限付きのCSV互換層を先行追加する

判断が必要なのは一点です。古い配信済み・キャッシュ済みフロントも動作保証しますか？

- 不要：計算フロントのみを最小修正
- 必要：バックエンド互換層を先行し、その後フロント修正

なお、現在のワークスペースは読み取り専用のため、診断文書の追加保存とCodex再検証は実行できていません。既存資料は [bug report](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/research/troubleshoot-framewebforjs-calculation-communication-error-bug-report.md) と [context](C:/Users/sasai/Documents/FrameWeb3/.agents/docs/research/troubleshoot-framewebforjs-calculation-communication-error-context.md) にあります。
