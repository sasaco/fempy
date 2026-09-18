# Agent State

## Main Agent

Codex

## Repository Identity

FrameWeb3 is a Windows-oriented monorepo for web-based structural frame analysis.

- `FrameWeb/`: Python FEM engine and Flask/functions-framework calculation API, managed with `uv`.
- `FrameWebforJS/`: Angular 15 browser/Electron client, managed with npm.
- `tools/FrameWeb.Startup/`: .NET 8 local launcher and print HTTP host.
- `FramePrintPDF/`: .NET printing and PDF projects.
- `FrameGConverter/`: separate conversion utility.

Windows PowerShell is the canonical development shell. Macro requirements and durable design decisions live in [docs/DESIGN.md](docs/DESIGN.md).

## Progress Tracker

Rolling progress summary (latest 5 checkpoints): [PROGRESS.md](../PROGRESS.md)

<!-- Working state below is maintained by workflow skills and manual notes. -->

---

## Current Bug Fix: framewebforjs-calculation-communication-error
<!-- orchestra:block-id: framewebforjs-calculation-communication-error -->

### Context

- Error: Ct桁プリセットの計算で汎用通信エラー。ブラウザーと直接HTTPで再現し、POSTはHTTP 400 invalid_input / Extra data: line 1 column 3を返す。
- Root cause: FrameWebforJSはgzip Uint8Arrayをbtoaへ直接渡してBase64(括弧なしCSV)を送るが、29df328以降のFrameWebは安全なjson.loadsでBase64(JSON整数配列)を要求する。失敗はモデル解析前。
- Affected files: FrameWebforJS/src/app/app.component.ts、FrameWeb/main.py。印刷側は別C#契約で括弧なしCSVが正規形式。
- Separate blocker: transport修復後もバックエンドのflat結果とフロントのcase別disg/reac/fsec契約が不一致の疑いがあり、Ctの複数荷重ケース仕様決定が必要。

### Fix Approach

- Milestone T: 計算専用の純粋エンコーダーを抽出し、btoa(`[${compressed.join(',')}]`)相当で現行JSON整数配列契約へ適合。バックエンド、ヘッダー、印刷エンコーダーは変更しない。実装結果は transport repaired とだけ報告する。
- Milestone U: 荷重ケース数・ID/順序・変位/反力/断面力の単位/符号/キー/変換所有者を先に決定し、case別disg/reac/fsecの失敗テストとアダプター/応答変換を別承認で実装する。Ct E2E通過のみをユーザー向け修復完了とする。
- 旧配布済み計算クライアント対応が明示的に必要な場合だけ、厳格制限・計測・削除条件付きCSV互換分岐を一時導入する。eval/literal_evalは禁止。

### Codex Validation

- 初回計画検証はNEEDS_REVISION: transport成功とuser成功を分離し、複数荷重ケース結果契約を実装前ゲートにするよう指摘。
- 改訂二段階計画はPASS。Milestone T PASS=transport repaired、Milestone U未承認/未通過=全体未完了、Milestone U E2E PASS=ユーザー向け修復完了。
- 追加必須: 実HTTPヘッダー検証、flat結果を成功扱いしない回帰、結果契約決定時の具体的ランナー/サービス起動コマンド。

### Regression Risks

- 計算専用正規化は低リスク。共有エンコーダーで印刷まで変更するとC# Convert.ToByte契約を壊すため高リスク。
- 大型ラーメン高架橋プリセットはgzip約2,025,295 bytes、Base64約9.65 MB。Array.fromで約202万要素をBox化せずjoinを使い、Chrome完走と性能を確認する。
- HTTP 200、ダイアログ消失、isCalculated=true、空worker結果は完了根拠にならない。
- 互換サーバー分岐は入力面とgzip bomb面を広げるため、必要性が証明された場合のみ。

### Decisions

- 29df328のeval除去は正しいセキュリティ修正であり、戻さない。
- 今回の観測エラーは全圧縮計算に共通するtransport契約不一致で、Ct固有データは直接原因ではない。
- 製品コードは調査段階では変更しない。実装は二段階計画の承認後に行う。

---

## Current Bug Fix: framewebforjs-results-not-displayed
<!-- orchestra:block-id: framewebforjs-results-not-displayed -->

### Context

- Symptom: Ct桁プリセットはHTTP 200で計算できるようになったが、FrameWebforJSの変位・反力・断面力が表示されない。
- Direct reproduction: 現行backendの復号結果はnode_displacements/reaction_forces/element_stresses等のflat schemaで、frontend互換のdisg/reac/fsec caseは0件。
- Root cause: 旧backendは全load caseを入力順で解いて{caseId: {disg,reac,fsec,shell_fsec,size}}を返す。現行backendはselect_caseで先頭caseだけを選び、1回のFemModel.run()のflat resultを返す。frontend workerはfield不一致を黙ってskipし、空mapをerror:nullで成功扱いする。
- Scope: Ct固有ではなくFrameWebforJSから送る旧node/load形式全体。Ctはcase 1～11のため、単なるshape wrapperではcase 2～11が欠落する。

### Fix Approach

- 既定の現行flat HTTP/Python contractは維持する。FrameWebforJSが明示的に要求するversioned compatibility representation（仮称legacy-cases-v1）をbackend HTTP境界へ追加する。
- compatibility pathでは全load caseを入力順にfresh modelで解析し、旧disg/reac/fsec/shell_fsec/sizeへproduction projectorで変換する。単位、符号、rate、member P1..Pn順序は旧backend goldenで固定する。
- FrameWebforJSはversionを明示し、worker起動前にnon-empty case-mapと必須3 fieldを検証する。不適合時はisCalculatedをtrueにせず、明示errorを表示する。
- test-firstで既定flat不変、Ct 11 case、case固有definition、rate、途中失敗の原子性、browser表示を検証する。

### Codex Validation

- UNAVAILABLE: lead 2回、root-cause analyst 1回、impact investigator 1回のbounded read-only相談はいずれもtimeoutまたは空responseで、正式なverdictなし。
- 診断はdirect HTTP reproduction、旧/current/frontend code、git history、既存testsを突合して確定。Codex outputを根拠として使用していない。

### Regression Risks

- 既定HTTP responseを旧case-mapへ置換すると文書化済みflat contract、現行client、広範なbackend testsを破壊するため禁止。
- frontend-only変換では解析されていないcase 2～11を復元できず、mesh由来のsign/order規則をTypeScriptへ重複するため不採用。
- Ctは11 sequential solveとなるためruntime/memory/timeoutを測定し、case間で可変FemModel stateを共有しない。
- shell_fsecはCtに含まれず現行helperともschemaが違う。旧契約互換を名乗る前に別oracleが必要。
- HTTP 200、transport test、success alert、isCalculated=trueだけでは表示修正完了と判定しない。

### Decisions

- 推奨案は、既定flatを保持した明示的/versioned backend compatibility adapterとfrontend fail-fastの組み合わせ。
- transport/input形式からresponse versionを暗黙推測しない。plain/canonical compressed/legacy compressedはいずれもtransportでありschema selectorではない。
- 本セッションは診断とhandoffまで。表示互換adapterは未実装。次セッションはlegacy-cases-v1の選択方法を確定し、contract testから開始する。
- 詳細は.agents/logs/troubleshoot-framewebforjs-results-not-displayed-diagnosis.md、root-cause/impact reportを参照。
