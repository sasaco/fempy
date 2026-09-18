# Work Log: Impact Investigator

## Summary

旧FrameWeb2と現行FrameWeb/FrameWebforJSのgit履歴、response consumer、test helper、Ct presetを照合し、既定flat APIを維持した明示的versioned compatibility responseが最小回帰の修正境界だと評価した。transport、backend representation、frontend fail-fast、Ct browser displayを分離した受入条件を作成した。

## Tasks Completed

- [x] git log/blameで旧case-map、現行flat API、先頭case選択、flat契約の文書/test固定、frontend importの導入commitを特定した。
- [x] 現行flat結果のbackend consumerと旧case-mapのfrontend consumer/test helperをinventoryし、backend全面置換・frontend単独変換・versioned互換responseのblast radiusを比較した。
- [x] Ct presetの荷重case ID `1..11`とcase別荷重を確認し、単純wrapperが不十分であることを確認した。
- [x] 単位、符号、member segment順、`rate`、case error、modern client保護を含む最小TDD/E2E受入testを定義した。
- [x] 低effort・bounded read-only Codex consultationを1回実施したが回答不能だったため、再試行せずrepository evidenceで完了した。

## Git History

- Introducing contract: `12b8ab9f0d457ae52967d9d66296aeb34fac690d` — 2026-01-30の現行FrameWeb初期commitで単一flat `FemModel.run()` HTTP responseを導入。
- First-case selection: `29b9c32b04519870445038602af5c0f9dbe786db` — 2026-09-08に`select_case(data)`を導入し、既定で先頭loadのみを保持。
- Modern contract lock-in: `46441c5bb6df04e70519f68a19dda0e2eaa027c3` — flat HTTP assertions、`d111a0277718f5df81fa3c6c74bfbecff4d423b6` — flat/no-case-hierarchyの文書化。
- Frontend integration: `be59bd48974b6fd02963ec306b327fad1293fac3` — 旧case-map consumerを既存状態のままmonorepoへimport。
- Old reference: FrameWeb2 `da44b42e99ad7097918b0471c278e0f36e5c03d4` — 全load caseを`disg`/`reac`/`fsec` mapへする旧契約。

## Blast Radius

- Affected code paths: FrameWebforJSの基本変位・反力・断面力worker、後段のDEFINE/COMBINE/PICKUP、結果表、3D描画、帳票用結果。
- Affected features/users: 旧入力を送るbrowser/Electron client全般。Ctは11 caseのうち現状1 caseしか解かれず、全結果表示が空になる。
- Regression-sensitive modern paths: flat keyを直接参照する43 backend test file、11 source/tool/script file、HTTP test、local smoke、公開Wiki/client。
- Reusable but unsafe-as-is path: `tests/support/section_cut_view.py`は変換規則の証拠だが、`rate`欠落、shell key差、2D synthetic reaction、production error contract欠落がある。

## External Research

None. 第三者libraryではなくrepository内の独自API契約不一致のため不要だった。

## Regression Risk

- Existing test coverage: flat solver/HTTPは広く固定され、test-only legacy viewにはsample比較がある。一方、production case-map、Ct 11 case、frontend schema rejection、worker/UI表示testはない。
- Risk areas: flat default破壊、case別定義の誤選択、mutable solver state共有、11倍の計算量、`rate`の二重/未適用、reaction/fsec符号とsegment順、shell互換、部分成功の誤表示。
- Recommended safeguard: flat defaultを保存し、明示的`legacy-cases-v1`表現だけを全case solve/projectし、frontend側もworker前にschema fail-fastする。

## Codex Risk Analysis

- Regression risk assessment: Codex unavailable。`framewebforjs-results-impact-risk` consultationは有効なresponseを返さず、response artifactは0 bytes。
- Fix safety assessment: Codex verdictではなく、git history・旧/current code・既存test・再現証拠に基づき、全面置換/ frontend-onlyはhigh risk、explicit versioned backend compatibility boundaryはmedium riskかつ推奨と評価した。

## Communication with Teammates

- → `/root/result_root_cause`: flat API/first-case selection/旧case-mapのcommit履歴、modern flat replacementのblast radiusを共有し、単位・符号・順序・rate変換仕様を依頼した。
- ← `/root/result_root_cause`: 旧rate適用、disg単位、reaction rename/sign、fsec sign vector/P順、test helperのshell/synthetic reaction差、および同じversioned boundary推奨を受領した。
- → `/root`: Codex再試行を停止し、repository evidenceでimpact report/work logを完成する方針に従った。

## Issues Encountered

- Legacy Windows PowerShellのJSON/文字コード制約: `ConvertFrom-Json -AsHashtable`非対応とUTF-8 BOMなしpresetの誤decodeを、`.NET ReadAllText(..., UTF8)`と`PSObject.Properties`で回避した。product file変更なし。
- Codex timeout/interruption: `.agents/logs/codex/20260918T053953Z-framewebforjs-results-impact-risk.md`は0 bytes。lead指示どおり再試行せず、Codex unavailableと明記した。
