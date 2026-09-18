# Work Log: http-docs
## Summary
FrameWebの公開HTTP契約を、正規のJSON整数配列形式とFrameWebforJS向け括弧なしCSV互換形式の両方が明確になるよう更新しました。安全境界、対象ヘッダー、要求・応答の非対称性、不許可形式も文書化しています。
## Tasks Completed
- [x] 圧縮要求の二形式を文書化: 正規形式をBase64(JSON整数配列 of gzip bytes)、互換形式をBase64(括弧なしASCII十進CSV of gzip bytes)として区別しました。
- [x] 互換パーサーの安全境界を文書化: `eval`を使わず、ASCII数字とカンマからなる0～255の値だけを厳格に解析し、Python式などを拒否することを明記しました。
- [x] 現行ルーティングを文書化: `Content-Encoding: gzip`と`Content-Encoding: gzip,base64`が圧縮経路へ入ることを明記しました。
- [x] 既存契約を維持: 通常JSON経路の推奨、圧縮要求と成功応答の非対称性、エラー応答が通常JSONであること、raw Base64(gzip JSON)と生gzipが不許可であることを維持しました。
- [x] 個別検証: `git diff --check -- FrameWeb/docs/wiki/endpoints.md`はexit 0、`rg -n "JSON整数配列|括弧なしCSV|eval|gzip,base64" FrameWeb/docs/wiki/endpoints.md`はexit 0でした。
- [x] 共有ゲート実行: `bash .agents/skills/_shared/verify.sh`はexit 1でした。WSLが`execvpe(/bin/bash) failed: No such file or directory`で停止したため、verifierの`overall`と`tools`は生成されていません。これは既知のWindows/WSL環境ベースラインであり、文書差分の検査結果ではありません。
## Files Modified
- `FrameWeb/docs/wiki/endpoints.md`: 圧縮HTTP要求の正規形式、旧互換形式、安全境界、対象ヘッダー、不許可形式を明確化しました。
## Key Decisions
- JSON整数配列を正規形式として維持し、括弧なしCSVは既存FrameWebforJSとの互換形式としてのみ説明しました。
- `eval`互換ではなく厳格な字句・バイト範囲検証であることを明記し、任意のPython式との互換性を否定しました。
- 要求・応答の非対称性と通常JSON推奨を変更せず、今回の文書範囲を輸送契約に限定しました。
## Communication with Teammates
- → `/root`: 文書更新の完了、個別検証exit 0、共有ゲートがWSL `/bin/bash` 不在でexit 1になりJSONを生成できなかったことを報告しました。
## Issues Encountered
- 共有検証スクリプトを起動できないWindows/WSLベースラインがありました。製品差分とは切り分け、利用可能な個別検証をexit code付きで実施しました。
