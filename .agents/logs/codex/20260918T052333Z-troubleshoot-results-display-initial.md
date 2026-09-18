## TL;DR

HTTP 200 は計算・通信の成功しか示しておらず、表示契約の成立は示しません。最有力かつほぼ確定なのは、バックエンドのフラットな結果形式と、FrameWebforJS が期待する荷重ケース別形式の不一致です。

## Analysis

### 初期仮説ランキング

1. **レスポンス・スキーマ不一致 — ほぼ確定**

   FrameWeb は `node_displacements`、`reaction_forces`、`element_stresses` をトップレベルに持つ単一の結果を返します。[main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:106)

   一方、FrameWebforJS は次の形式を期待しています。

   ```text
   {
     caseId: {
       disg: ...,
       reac: ...,
       fsec: ...
     }
   }
   ```

   各 Worker は `disg`、`reac`、`fsec` がないトップレベル項目を黙ってスキップします。

   - [result-disg1.worker.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-disg/result-disg1.worker.ts:29)
   - [result-reac1.worker.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-reac/result-reac1.worker.ts:27)
   - [result-fsec1.worker.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-fsec/result-fsec1.worker.ts:61)

   その後、空オブジェクトを `error=null` で返すため、「成功扱いだが表示データがない」という症状に完全に一致します。

2. **複数荷重ケースの実行・射影欠落 — 確定した併存欠陥**

   旧形式入力の読込みでは、荷重ケースの先頭だけが選択されます。[legacy_beam.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_beam.py:6)

   Ct 桁には11ケースあるため、現在の単一結果を単に `{caseId: ...}` で包んでも互換性は回復しません。全ケースの実行と、各ケースの `disg/reac/fsec` への変換が必要です。

3. **空結果を成功扱いする完了状態・非同期タイミング — 高確度の増幅要因**

   `loadResultData()` は非同期 Worker を開始した直後に、グローバルな `isCalculated` を真にします。[app.component.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:285)

   これにより「計算済み・画面遷移可能」に見えます。ただし、タイミングだけを直しても Worker の入力形式は不一致のままなので、主原因ではありません。

4. **項目名・符号・ID・単位の変換不足 — 修復後に顕在化する可能性が高い**

   `reaction_forces.fx/fy/fz` と UI の `reac.tx/ty/tz`、部材端応力と `fsec.P1...Pn` には非自明な変換が必要です。類似変換はテスト補助コードにありますが、本番エンドポイントでは使われていません。[section_cut_view.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/support/section_cut_view.py:63)

5. **レスポンスのBase64・gzip・JSON復号失敗 — 低い**

   復号失敗なら成功処理の `loadResultData()` には到達せず、例外ダイアログになります。今回の「HTTP 200・計算済み・空表示」とは合致しません。以前のリクエスト形式によるHTTP 400とは別問題です。

## Plan

最も判別力の高い次の確認は以下です。

1. 実際のCtレスポンスについて、全11ケースのIDと各ケースの非空 `disg/reac/fsec` を契約として検証する。
2. 現在のフラットレスポンスを3種類の Worker に渡し、`error=null` の空結果になることを固定テスト化する。
3. 旧FrameWeb2と代表ケースを比較し、変位、反力の名称・符号、部材端順序、単位を照合する。
4. 不適合レスポンスでは calculated フラグを立てず、明示的なスキーマエラーを表示する。

## Risks

バックエンドのフラット形式は既存テストで正式に固定されています。したがって、全面的に形式を置換すると他クライアントを壊す可能性があります。専用の互換アダプターまたはバージョン付きレスポンスが安全です。

なお再現スクリプトの再実行は、読み取り専用環境で `uv` キャッシュと `frame.log` の作成が拒否されたため完走できませんでした。ただし、既存のHTTP 200再現記録、現行コード、テスト、および独立した二系統の調査結果はすべて上記順位で一致しています。
