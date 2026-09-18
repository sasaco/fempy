# State Migration Work Log

## Summary

FrameWeb3のコピー元状態を安全に除去する専用移行器とfixtureベースの回帰テストを実装した。初回は`.agents/docs/DESIGN.md`の同時変更をhash guardで検出して停止し、安定後に提示された新baselineとの完全一致を再確認してから、実STATE/DESIGNの移行を完了した。Phase 2 test review後は、未知DESIGN H2の保持、live文書のbyte idempotence、commit直前race、rollback失敗時の手動復旧を追加で強化した。`PROGRESS.md`と現行STATE/DESIGNは再書込していない。

## Tasks Completed

- `migrate_repository_state.py`へ厳格UTF-8読込、改行維持、STATE/DESIGN別SHA-256ガード、dry-run候補出力、事前contract検査を実装した。
- 適用処理はOS-backed inter-process transaction lockを全read/validate/stage/replace/rollback期間で保持し、最初のreplace直前にSTATE/DESIGNの両方を再読込して初期byte列と比較する。
- 各対象と同一ディレクトリへ候補・バックアップをstageし、2件目の置換失敗時に1件目を復元する。rollback自体が失敗した場合はexact backupを削除せず、例外とCLI JSONでrecovery pathを返す。cleanup失敗はprimary failureを隠さない。
- STATEは既知のコピー元見出しだけを除去し、未知の現行作業ブロックを保持する。DESIGNは既知のコピー元参照を除去し、未知かつforeignでないH2 chunkをheading/comment/spacing/bodyごと保持する。未知H2に既知foreign keywordがある場合は推測保持・削除せず拒否する。
- 新規生成DESIGNへmachine-readable migration markerを付け、marker導入前の現行文書はFrameWeb3 component anchorとplaceholder/foreign不在による安定した構造判定でbyte-for-byte no-opにする。
- hash不一致、片側replace失敗、再実行no-op、UTF-8/CRLF、文書欠損、非UTF-8、dry-run非破壊性に加え、未知H2保持/拒否、commit直前同時編集、rollback recovery、live byte idempotenceを含む14テストを追加した。
- `.agents/STATE.md`をCodex Main AgentとFrameWeb3 identityへ更新し、コピー元8ブロックだけを削除した。2つのFrameWebforJS実作業ブロックはEOL正規化後にHEADとbyte一致することを確認した。
- `.agents/docs/DESIGN.md`をPython FEM/Angular/.NET startup・printingの実構成で再構築し、`AnalysisResult`、`AnalysisResultSet`、`FrameResultSet`、2つの新media type、追加済み機能要件・NFR・決定を保持した。
- Phase 2修正ではlive `.agents/STATE.md` / `.agents/docs/DESIGN.md`を変更せず、no-write probeで現行result-set要件・NFR・決定を含む全byteの不変を確認した。

## Files Modified

- `.agents/skills/init/migrate_repository_state.py`（新規）
- `.agents/tests/test_migrate_repository_state.py`（新規）
- `.agents/STATE.md`
- `.agents/docs/DESIGN.md`
- `.agents/logs/agent-teams/team-execute-agents-repository-alignment/state-migration.md`（本ログ）

`PROGRESS.md`は内容・SHA-256とも変更なし。

## Key Decisions

- コピー元判定は既知のSTATE見出しと限定キーワードに閉じ、未知セクションを推測削除しない。
- 真の2ファイルfilesystem transactionは利用できないため、全候補を先に検証・stageし、順次atomic replaceと即時rollbackで観測可能なpartial commitを防ぐ。
- transaction lockは対象絶対pathから導くsystem-temp内の安定lock fileへOS advisory lockを取得し、repositoryへlock artifactを残さない。非協調writerはcommit直前の両文書byte比較で検出する。
- rollback失敗は隠蔽せずpartial状態を報告し、失敗した復元用backupだけをexact byteのまま保持してrecovery pathを公開する。
- 実repoに移行器のapply modeを実行せず、apply/rollbackは一時fixtureだけで検証する。
- ベースライン差分が1件でもあれば、内容の手動マージを推測せず実ドキュメント編集を停止する。

## Communication with Teammates

- 親エージェントから、外部作業が`.agents/docs/DESIGN.md`へ5行追加したとの連絡を受領した。
- 親へ、実ドキュメント3件を編集せず移行器とテストだけを完成させる方針を共有した。
- 親から安定後の新baselineを受領し、3件の完全一致を再確認してlive migrationを再開した。
- 適用後、STATE/DESIGN移行、PROGRESS不変、全指定gateの結果を親へ共有した。

## Verification

- `$env:PYTHONDONTWRITEBYTECODE='1'; uv run --project FrameWeb --locked --extra dev python -m pytest .agents/tests/test_migrate_repository_state.py -q` → `14 passed in 0.40s`
- live migration再開直前のhash gate:
  - `.agents/STATE.md`: `5785CABF0F4AC5E9D03BA40D731D6B5BE9328F708CF7F27EF7A12DA0B06D9D67`（baseline一致）
  - `.agents/docs/DESIGN.md`: `7DF1833DF7CD74A6D9ED5F98A9A239CB36CC6014E07807C04189ABCF1F4CA8A3`（新baseline一致）
  - `PROGRESS.md`: `BA9E3B68D5FDCCB4354F6D7B16973FB775B97060321F4B86E928742B249AA821`（baseline一致）
- state-doc contract → PASS、design-doc contract → PASS。
- `load_context.py` → `main_agent=Codex`、`design.placeholder=false`、`progress.entries=1`、`missing=[]`、`unreadable=[]`、`warnings=[]`。
- live 3文書のforeign-reference scan → 一致なし（exit 1）。
- `PROGRESS.md`再計測 → baseline hashと一致。
- `git diff --check` → PASS。
- live no-write idempotence probe（replace呼出時は即失敗するguard付き）:
  - `changed=false`、`state_changed=false`、`design_changed=false`、`artifacts=[]`、`bytes_unchanged=true`。
  - `.agents/STATE.md`: before/after `D1B68A2F568EF264CCDD2697AE0CB2375EF5AA361161EA68AE10DD1167E72BC8`。
  - `.agents/docs/DESIGN.md`: before/after `D1DA6C3AE6A22CA1AB2AC86DB72D7A8261FBA9BC40639D2DE4309EF218976D91`。
- Phase 2修正後のstate-doc/design-doc contract → PASS。
- Phase 2修正後の`load_context.py` → `main_agent=Codex`、`design.placeholder=false`、`progress.entries=1`、`missing=[]`、`unreadable=[]`、`warnings=[]`。
- 担当3ファイル対象の`git diff --check` → PASS。

## Issues Encountered

- `.agents/docs/DESIGN.md`に同時変更があり、委任時のhash guardが発火した。これは移行器の失敗ではなく、意図した同時編集保護である。
- apply-patch orchestrationでV8に`atob`が存在せず初回STATE編集準備が停止した。ファイル変更前の失敗であり、再hash後に厳格UTF-8 text読込へ切り替えた。
- Phase 2 test reviewで、旧DESIGN再構成が未知H2を落とすことと、初期hash確認後からreplaceまでの同時編集windowが再現された。前者はexact chunk保持、後者はlockとcommit直前byte比較で修正した。
- rollback failure testでは意図的にpartial状態を作り、保持backupが元STATEとbyte一致し、cleanup failureがprimary exceptionを隠さないことを確認した。

## Remaining Work

- 本担当範囲の残作業なし。親エージェントが他ユニットとの統合後にrepository-wide gateを再実行する。
