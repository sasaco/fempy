# Work Log: backend-tdd

## Summary

承認済み計画に沿って、圧縮入力のJSON整数配列と旧ブラウザー十進CSVを厳格に受理するdual parserを実装した。製品変更前の有効なRedから、focused 107件・指定回帰182件のGreenを確認した。

## Tasks Completed

- [x] context-loader、linksee-memory、TDDおよび指定の計画・ルールを読み、担当範囲を確認。
- [x] Node v24.13.0 / pako 2.2.0の実式 `btoa(pako.gzip(JSON.stringify(payload)))` で固定fixtureを生成し、保存後の再生成完全一致も確認。
- [x] テスト・fixtureのみを先に追加し、main.py未変更の状態で有効なRedを取得。
- [x] Base64厳格検証、JSON先行・JSONDecodeErrorのみCSV fallback、ASCII・1〜3桁・0〜255・厳密整数検証を実装。
- [x] 段階別の既知例外をInputValidationErrorへ変換し、展開後dict契約と予期しない例外の500維持を検証。
- [x] gzip / gzip,base64 × JSON配列 / CSVの4組についてHTTP 200と通常JSONとの結果完全一致を検証。
- [x] 型、字句、コード風入力、Base64、gzip破損、内側UTF-8/JSON/object契約の拒否を検証。
- [x] 指定6ファイル回帰、新規テストlint/format、禁止式検索、diff checkを実行。
- [x] 最終verify.shを実行し、既存環境に起因する起動失敗を記録。

## Validation and Files Modified

### TDD evidence

共通runnerコマンドはリポジトリ直下から `uv run --project FrameWeb --locked --extra dev python .agents/skills/_shared/run_tests.py` を使用した。rootにはpyproject.tomlがないため、既存FrameWeb環境を選ぶ `--project FrameWeb` を明示した。

| Label | Expected / observed | Runner exit | pytest result | Log |
|---|---|---|---|---|
| legacy-compressed-red | fail / failed | 0 | exit 1; 72 failed, 35 passed | `.agents/logs/legacy-compressed-red.log` |
| legacy-compressed-green | pass / passed | 0 | exit 0; 107 passed | `.agents/logs/legacy-compressed-green.log` |
| legacy-compressed-refactor | pass / passed | 0 | exit 0; 107 passed | `.agents/logs/legacy-compressed-refactor.log` |
| legacy-compressed-final | pass / passed | 0 | exit 0; 107 passed | `.agents/logs/legacy-compressed-final.log` |
| legacy-compressed-regression | pass / passed | 0 | exit 0; 182 passed | `.agents/logs/legacy-compressed-regression.log` |

Redでは旧ブラウザーfixtureと旧CSVのHTTP 2組が `Extra data: line 1 column 3` で失敗し、現行JSON配列・通常JSON・コード風入力非実行は成功した。型、厳格Base64、既知エラー分類、内側object契約の失敗も予定境界内だった。collection errorはなかった。

回帰対象: `FrameWeb/tests/io/test_compressed_transport.py`、`FrameWeb/tests/integration/test_input_routes.py`、`FrameWeb/tests/integration/test_spatial_general_plane.py`、`FrameWeb/tests/io/test_axial_force_input.py`、`FrameWeb/tests/regression/test_slip_support_history.py`、`FrameWeb/tests/io/test_http.py`。

### Static and environment gates

- `uv run --locked --extra dev ruff check/format --check ...`: ruffが既存dev環境に未収録のため起動失敗。依存ファイルは変更せず、`uv tool run ruff`を使用した。
- `uv tool run ruff check tests/io/test_compressed_transport.py`: exit 0 / All checks passed。
- `uv tool run ruff format --check tests/io/test_compressed_transport.py`: exit 0 / 1 file already formatted。
- `uv tool run ruff check main.py tests/io/test_compressed_transport.py --output-format concise`: exit 1 / main.pyの既存9件。I001 × 3、F401 × 3、BLE001 × 2、UP039 × 1。`git show HEAD:FrameWeb/main.py`を同じruffへ渡し、同じ9件を確認。新規テストの指摘なし。
- `uv tool run ruff format --check main.py tests/io/test_compressed_transport.py --output-format concise`: exit 1 / main.py would be reformatted、test already formatted。HEADのmain.pyもformat check不一致。親担当の指示によりmain.py全体format・既存import・broad catch整理は行わず、変更した新規ロジック内の折り返しのみ整えた。
- `git diff --check -- FrameWeb/main.py`: exit 0。LF/CRLF変換のgit warningのみ。
- `rg -n 'eval\(|literal_eval' FrameWeb/main.py FrameWeb/tests/io/test_compressed_transport.py`: exit 1 / 該当なし。
- 保存fixtureをNode/pakoで読み直し、同じ生成式によるbodyとの完全一致を確認: exit 0。
- 最終 `bash .agents/skills/_shared/verify.sh`: **exit 1**。WSLの `execvpe(/bin/bash) failed: No such file or directory` によりscript開始前に失敗。JSONは未生成なので **overall=null（未観測）、tools=null（未観測）**。verify.sh成功・no_gatesとは解釈しない。今回の変更に起因するテスト失敗はないが、包括ゲート完走は未確認。

### Changed files and deviations

- `FrameWeb/main.py`: strict dual parserと段階別例外変換。
- `FrameWeb/tests/io/test_compressed_transport.py`: 新規107テスト。
- `FrameWeb/tests/data/transport/legacy-browser-envelope.json`: Node/pako生成fixture。
- `.agents/logs/agent-teams/team-execute-legacy-compressed-input-compatibility/backend-tdd.md`: 本work log。

製品・テストの所有範囲逸脱なし。指定runnerの自動ログ以外の追加作業ファイルなし。既存ユーザー変更、フロント、印刷、既存テスト、STATE、計画書、docsは編集していない。Ct結果スキーマ・複数荷重ケース対応・DoS上限は対象外。

## Communication with Teammates

- ← /root: 計画、所有範囲、必須TDD/回帰、work log要件を受領。
- → /root: 製品未変更での有効Red（72 failed, 35 passed）とNode/pako fixture生成、ruff起動環境問題を報告。
- → /root: focused Greenとmain.py既存lint 9件、回帰実行状況を報告。
- ← /root: 最小差分を優先し、main.py全体formatter・既存broad catch・import整理をしない指示を受領。
- → /root: focused 107件・回帰182件PASS、静的検証結果、verify.sh環境起動失敗を報告。

## Issues Encountered

- bare `python`はPATHに存在しない: 以後すべて `uv run ... python` を使用。
- Windows PowerShellのnode -e引用が崩れた: 読み取り専用JavaScriptをhere-stringでnode stdinへ渡し、正しい実式でfixtureを生成・再検証した。
- ruff未導入とmain.py既存lint/format不一致: 依存変更や無関係な整理を避け、`uv tool run ruff`とHEAD比較でbaselineを分離した。
- verify.shのWSL /bin/bash不存在: 最終コマンドを実行してexitと未生成フィールドを上記へ記録。ホスト設定は変更していない。
