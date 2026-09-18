## Diagnosis Report: FrameWebforJSで計算結果が表示されない

### Error Reproduction

再現済み。`サンプル（Ct桁）.json` と同じ旧CSV圧縮transportを現行backendへ送るとHTTP 200で計算結果を受信するが、復号後のtop-level keyは `node_displacements`、`reaction_forces`、`element_stresses` などのflat schemaである。旧frontendが要求する、各case内に `disg`、`reac`、`fsec` を持つcaseは0件だった（`.agents/logs/repro-framewebforjs-results-not-displayed.py:25-49`）。

### Root Cause (Root Cause Analyst + Codex)

- **Defect**: backendの成功応答は単一caseの現行flat resultだが、frontendは旧版と同じ「load case IDをkeyとし、各valueが `disg` / `reac` / `fsec` を持つmap」を読む。3 workerはfieldがないentryを黙ってskipし、空mapを `error: null` で返すため、計算成功後に結果だけが空になる。
- **Location**: producerは `FrameWeb/main.py:106-118`。caseを1件へ縮約する箇所は `FrameWeb/src/fem/file_io.py:70-79` と `FrameWeb/src/fem/legacy_beam.py:6-20`。consumerは `FrameWebforJS/src/app/providers/result-data.service.ts:87-95`、silent skipは `result-disg1.worker.ts:29-40`、`result-reac1.worker.ts:27-41`、`result-fsec1.worker.ts:41-70`。
- **Trigger**: FrameWebforJSから旧 `node` / `load` 形式で正常に計算できた全request。複数caseではさらに欠落が拡大し、Ct桁のload ID `1`～`11`のうち現行backendが解析するのは先頭caseだけである。
- **Evidence**: 旧版は `Controller.results` をそのまま返し、全load caseを入力順に解いて `results[id]` を生成する（`C:/Users/sasai/Documents/FrameWeb2/main.py:63-79`、`app/controller.py:90-102`）。各caseは `disg` / `reac` / `fsec` / `shell_fsec` / `size` を持つ（`app/result.py:135-180`）。現行のdirect reproductionはHTTP 200、compatible case 0件だった。
- **Codex confidence**: UNAVAILABLE。read-only相談を複数回行ったがすべてtimeoutし、正式なverdictは得られていない。結論は旧版・現行版・frontend code・direct reproductionを突合した高確度のrepository内証拠による。

### Impact Assessment (Impact Investigator + Codex)

- **Blast radius**: FrameWebforJSの変位・反力・断面力、DEFINE／COMBINE／PICKUP、結果表、3D描画、帳票用結果へ連鎖する。Ct固有ではなく、同じ旧input形式を使う全モデルが対象。複数caseほど欠落が大きい。
- **Introducing commit**: 現行flat HTTP APIは `12b8ab9f0d457ae52967d9d66296aeb34fac690d`、旧入力を先頭caseへ縮約する処理は `29b9c32b04519870445038602af5c0f9dbe786db`。Frontend取り込み時点 `be59bd48974b6fd02963ec306b327fad1293fac3` でproducer/consumer契約が不一致だった。今回の圧縮通信修正が表示不具合を作ったのではなく、その次段の既存不整合を露呈させた。
- **External context**: 外部libraryの問題ではないため外部調査は不要。repository内の独自response contract同士の不一致である。
- **Regression risk**: 既定HTTP responseを旧case-mapへ全面置換すると、現行flat schemaを利用するHTTP/Python client、43以上のbackend test file、validation tool、wiki契約を壊す。frontendだけで変換しても解析されていないcase 2～11は復元できない。
- **Codex risk assessment**: UNAVAILABLE。Codex相談はtimeout。git history、既存test、旧/current codeから、既定flat契約を維持した明示的versioned compatibility pathが最小blast radiusと判断する。

### Fix Plan (7 tasks) -- Codex Validated: UNAVAILABLE (consultations timed out)

1. 先にcontract testを追加する。既定指定なしのHTTP responseは現在のflat schemaを一切変更せず、明示した `legacy-cases-v1` だけが旧case-mapを返すことを固定する。
2. 全case orchestrationをbackend HTTP compatibility境界へ追加する。元inputをcaseごとに安全に分離し、入力順を維持してfresh modelを解析する。途中case失敗時は部分HTTP 200を返さずcase ID付きで原子的に失敗させる。
3. test helperではなくproduction projectorを作る。旧版どおり `disg`、`reac`、`fsec`、`shell_fsec`、`size` を生成し、単位・符号・`rate`・memberの `P1..Pn` 順序をgolden testで固定する。Ctにないshell互換は別oracleで仕様を確定するまで未対応範囲を明示する。
4. FrameWebforJSが明示的に `legacy-cases-v1` を要求するようにし、worker起動前にnon-empty case-mapと `disg` / `reac` / `fsec` を検証する。不適合時は `isCalculated` をtrueにせず、ユーザーへresponse schema errorを表示する。
5. Ct桁integration testを追加する。case keyが順序どおりexactに `1`～`11`、全caseの3 fieldがnon-empty、case 1の複製ではないこと、代表値が旧backend goldenと一致することを検証する。異なるcase固有definitionと `rate != 1` のfixtureも加える。
6. 実browser E2EでCt桁を読み込み、case 1と11の変位・反力・断面力ページにrow／描画が出ること、DEFINE／COMBINE／PICKUPへ全base caseが届くこと、console/worker/通信errorがないことを確認する。11 sequential solveの時間・memoryを計測し、上限とtimeoutを決める。
7. transport test、現行HTTP test、backend full suite、frontend unit test、browser E2Eを回帰gateとして実行する。HTTP 200やtransport testだけでは表示修正完了と判定しない。

### Alternative Approaches Considered

- **Approach A**: 既定backend responseを旧case-mapへ全面置換する — 旧frontendは変更が少ないが、文書化済みの現行flat contractと既存client/testを破壊するため不採用。
- **Approach B**: frontendだけでflat resultを変換する — case 1は整形できてもbackendが解いていないcase 2～11を復元できず、mesh由来のnode/member/sign規則もTypeScriptへ重複するため不採用。
- **Approach C**: 既定flatを保持し、明示的/versionedなbackend compatibility adapterを追加する — 全caseをserver側で正しく解き、既存clientを維持できるため推奨。

### Next Steps

1. 次セッションでは本診断、root-cause report、impact report、checkpointを読み、`legacy-cases-v1` の選択方法（専用endpoint、media type、version headerのいずれか）を最初に確定する。
2. 方針承認後、上記task 1のcontract testから実装し、backend compatibility adapter、frontend fail-fast、回帰reviewの順に進める。
3. shell modelまで旧契約互換を名乗る場合は、Ct修正と分離して旧backend oracleを先に用意する。

---
本セッションでは診断と引き継ぎまで行い、表示互換adapterの実装はまだ行っていない。
