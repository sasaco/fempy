## 結論

初期診断は妥当です。原因は通信圧縮ではなく、バックエンドとFrameWebForJS間のレスポンス契約不一致です。

バックエンドは単一ケースのフラットな解析結果を返しますが、フロントエンドは次のケース別構造を期待しています。

```text
{
  caseId: {
    disg: ...,
    reac: ...,
    fsec: ...
  }
}
```

Workerは不適合なフラット結果をエラーにせず空データとして処理するため、HTTP 200・計算完了扱いなのに結果が表示されません。

## 根拠

- バックエンドはモデルを1回だけ解析し、結果をそのまま返しています：[main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:106)
- 旧形式入力は最初の荷重ケースだけに縮約されます：[file_io.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/file_io.py:70)、[legacy_beam.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_beam.py:6)
- フロントエンドはトップレベルをケースIDとして扱います：[result-data.service.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/providers/result-data.service.ts:87)
- `disg`、`reac`、`fsec`を持たないトップレベル要素は各Workerで黙って除外されます。
- Ct入力には11ケースあります。現在の単一結果を1ケースで包むだけでは、ケース2～11を復元できません。
- 既存の再現記録も「HTTP 200、フラットキー、ケース数0」を示しています。

したがって、表示タイミングやWorker初期化競合は主因ではありません。

## 最も安全な修正境界

旧形式入力専用のバックエンド互換アダプターに限定するのが最小かつ安全です。

1. HTTP/application境界で旧形式の `node`＋`load` 入力を識別する。
2. 元の荷重ケースIDを列挙する。
3. 各ケースについて `select_case(raw, case_id)` を実行する。
4. ケースごとに新しい`FemModel`を生成して解析する。
5. 結果を既存のFrameWebForJSケース別契約へ投影する。
6. 現行・非旧形式入力には、現在のフラットレスポンスを維持する。

変更対象に含めないもの：

- `FemModel.run()`の戻り値
- `_read_json_model`の全体的な既定動作
- フロントエンドWorkerや表示ロジック
- 今回変更済みの圧縮デコーダー
- 現行APIのフラットレスポンス契約

フロントエンドへのスキーマ検証追加は有用ですが、診断改善にすぎず、失われた複数ケースを復元できないため別タスクが適切です。

## 必須検証

- Ct入力でケースID 1～11がすべて返る。
- 各ケースに有効な`disg`、`reac`、`fsec`がある。
- ケース固有の荷重・拘束・結合条件が反映される。
- 現行入力のフラットレスポンスが変わらない。
- 正規形式・旧CSV形式双方の圧縮通信テストが維持される。
- FrameWebForJSで各ケースの変位・反力・断面力が表示される。
- 途中ケースの失敗は部分成功ではなく、決定的なエラーとして扱う。

未確定なのは、shell/curvature系フィールドの完全な対応関係と、11ケース連続解析の許容性能です。修正前に旧実装またはゴールデンデータとの数値・符号比較が必要です。

調査は読み取り専用で実施し、ファイル変更は行っていません。
