## Diagnosis Report: FrameWebforJS Ct桁計算時の通信エラー

### Error Reproduction
再現済みです。Chromeで内蔵プリセット`サンプル（Ct桁）.json`を開き、`計算`→`はい`で報告どおりの通信エラーを確認しました。フロントと同じ送信形式を使う直接HTTP再現でも、`HTTP 400`、`invalid_input`、`Extra data: line 1 column 3 (char 2)`が毎回返ります。`GET /`は200で、Flaskサービス自体は稼働しています。

### Root Cause (Root Cause Analyst + Codex)
- **Defect**: `pako.gzip()`の`Uint8Array`を`btoa()`へ直接渡したため、フロントは`base64("31,139,...")`という括弧なしCSVを送っています。バックエンドは`base64("[31,139,...]")`というJSON整数配列を`json.loads()`で要求しており、転送契約が一致していません。
- **Location**: `FrameWebforJS/src/app/app.component.ts:233-245`（送信側）と`FrameWeb/main.py:153-160`（受信側）。
- **Trigger**: 現行FrameWebforJSの圧縮計算要求を、commit `29df328`以降のFrameWebへ送る全ケース。Ct桁固有ではありません。
- **Evidence**: Base64復号後の`31,139,...`に対し、`json.loads()`は最初の`31`を1値として読み、直後のカンマ（char 2 / column 3）を余分なデータとして拒否します。同一gzipバイトを`[31,139,...]`で送る対照実験は復号を通過しました。モデル読込・構造解析には到達していません。
- **Codex confidence**: 通信エラーの根因について高信頼。修正後のユーザー向け計算完了については、別の結果契約不一致があるため未完了評価です。

### Impact Assessment (Impact Investigator + Codex)
- **Blast radius**: 現行JSから現行バックエンドへの全圧縮計算が対象です。通常JSONクライアントと正規のJSON配列圧縮クライアントは非影響です。印刷APIは別C#契約で括弧なしCSVが正規形式のため、同時変更してはいけません。
- **Introducing commit**: `29df328eb29d1a17b91f706eb1b7f6dd16a0fa78`（2026-09-08）。危険な`eval()`を安全な`json.loads()`へ変更した正しいセキュリティ修正ですが、旧フロント形式との非互換を顕在化させました。
- **External context**: 外部依存不具合ではなく、リポジトリ内の送受信契約不一致です。外部調査は不要でした。
- **Regression risk**: 計算専用エンコーダー修正は低リスク。印刷との共通化は高リスク。旧配布済みクライアント互換のサーバー分岐は中リスクで、必要性が証明された場合のみです。大型プリセットはgzip約2.0 MB、Base64約9.65 MBのため、`Array.from()`の大量Box化を避ける必要があります。
- **Codex risk assessment**: `CAUTION`。通信修正自体は安全ですが、バックエンドのflat結果`node_displacements/reaction_forces/element_stresses`とフロントのcase別`disg/reac/fsec`契約が別途不一致の疑いがあります。HTTP 200やダイアログ消失だけでは完了にできません。

### Fix Plan (12 tasks) -- Codex Validated: PASS
1. 計算専用の純粋TypeScriptエンコーダーを抽出し、製品コードとテストで同じ関数を使用する。
2. Karma/ChromeHeadlessで、空・ASCII・日本語・Ct正規化・大型入力の実エンコーダー往復テストを追加する。
3. Node 18 + `ts-node/register`で実TypeScriptエンコーダーを呼び、Python `Compressor.decompress`/Flaskへ渡すJS→Python契約テストを追加する。
4. 計算要求だけを``btoa(`[${compressed.join(',')}]`)``相当へ修正し、バックエンド・ヘッダー・印刷は変更しない。
5. 印刷要求が引き続き括弧なしCSVであり、PDF生成できる回帰テストを追加する。
6. 実Angularの`InputData.getInputJson(0)`でCt桁を再実行し、元の`Extra data`と通信ダイアログが消えたことを「transport repaired」として確認する。
7. 大型ラーメン高架橋プリセットで`Array.from`/spread不使用、正確な往復、Chrome完走を必須化し、時間・ピークメモリを記録する。
8. ここで結果契約の承認ゲートを置く。計算する荷重ケース、ID/順序、変換責任、`disg/reac/fsec`対応、単位・符号・端部規約・キー形式を決定するまで全体問題は未完了とする。
9. 承認された複数荷重ケース契約について、flat結果を成功扱いしない失敗テストを追加する。
10. 承認済みのバックエンド応答変換またはフロントアダプターだけを実装する。
11. Ct桁E2Eで、期待ケース数/ID、非空かつ有限な変位・反力・断面力、結果ページ遷移、worker/consoleエラーなしを確認する。この通過だけをユーザー向け修復完了とする。
12. 配布済み旧計算クライアント対応が明示的に必要な場合だけ、厳格なサイズ・字句・範囲・gzip検証、計測、削除条件付きの一時CSV互換分岐を追加する。`eval`/`literal_eval`は禁止する。

### Alternative Approaches Considered
- **Approach A**: 計算フロントだけを正規JSON配列形式へ合わせる。旧/現行バックエンド双方と互換で、攻撃面を増やさず、推奨です。
- **Approach B**: バックエンドで正規JSON配列と旧CSVを厳格に二重受理する。配布済み旧クライアントが必要な場合の移行策ですが、入力面・gzip bomb面を広げるため恒久策にはしません。
- **Approach C**: raw gzipのBase64へプロトコル刷新する。長期的には効率的ですが、協調配備・バージョニングが必要で今回の修正には過大です。

### Next Steps
1. 製品コードはまだ変更していません。上記二段階計画で進める場合、まずMilestone T（通信形式修復）をTDDで実装します。
2. Milestone T後、複数荷重ケースの結果契約を決定・承認してからMilestone Uを実装し、Ct桁E2Eまで確認します。

---
この修正計画で進めてよいですか？
