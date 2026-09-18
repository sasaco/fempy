# Agent State

## Main Agent

Claude Code

## Repository Identity

<!-- Managed by /init. Re-run /init to refresh. -->

_Not initialized yet. Run `/init` to populate._

Macro requirements and design live in [docs/DESIGN.md](docs/DESIGN.md).

## Progress Tracker

Rolling progress summary (latest 5 checkpoints): [PROGRESS.md](../PROGRESS.md)

<!-- Working state below is maintained by workflow skills and manual notes. -->

---

## Current Feature: Daily chart history paging
<!-- orchestra:block-id: daily-chart-history-paging -->

### Context

- Goal: Show the newest 90 Daily bars with five logical bars of right padding by default, then prepend older bars when user pan or zoom reaches the left edge without interrupting replay.
- Key files: `src/tickreplay/static/daily-chart.mjs`, `src/tickreplay/static/app.js`, `src/tickreplay/static/daily-chart.test.mjs`, `tests/test_tickreplay_daily_context.py`, and `docs/tick-replay.md`.
- Dependencies: Existing Daily context endpoint and Lightweight Charts logical-range API.
- Complexity: COMPLEX

### Architecture

- Use a session-scoped, user-armed, single-flight pager with a 10-bar left-edge threshold and 200-bar strict-before pages.
- Prepare immutable pages and recompute SMA25/SMA200 before the atomic four-series commit; preserve the visible logical range by shifting both edges by the unique prepend count.
- Commit inactive Daily completions to canonical state and the saved range only, then render them when Daily becomes active again.

### Codex Validation

- Two wrapper-based Codex CLI consultations produced no usable response; a `gpt-5.6-sol` fallback independently validated scope, architecture, plan, and revalidation as PASS.
- Independent quality, security, and test reviews were run after implementation; the quality review found and drove removal of an O(n) replay-frame snapshot.

### Integration Points

- Daily tab lifecycle and chart gestures in `app.js`; canonical Daily bars, SMA series, paging state, transaction, and rollback in `daily-chart.mjs`; strict-before paging contract in the existing Daily context API.

### Decisions

- Initial viewport is exactly newest 90 bars plus five right-padding bars, matching the Minute chart policy.
- Paging requires a real user wheel, pointer, or touch gesture so programmatic range changes cannot start requests.
- Failures use per-cutoff cooldown and a three-failure stop; empty pages mark history exhausted.
- Historical SMA arrays are computed only during initial load or page commit; replay-frame partial updates remain O(1).

### Validation

- Static JavaScript: 135/135 passed. Python integration subset: 133 passed, 1 skipped. Ruff and syntax checks passed.
- Fresh Chrome CDP verification passed exact 90+5 initialization, drag- and wheel-triggered 200-bar paging with exact +N range shifts, inactive completion and Daily return, SMA continuity, Tick/Tape/board/order/position continuity, and zero runtime exceptions.

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
