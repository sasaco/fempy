# Handoff — C# FrameWeb Desktop Client（Step 9開始）

## Goal

`FramePrintPDF/PDF_Manager` を .NET 8 / WinForms の完成デスクトップ製品へ移行する。Python FEM、private loopback HTTP、Windows Job管理、唯一の成功応答 `AnalysisResultSet v1` は承認済み境界として維持する。Step 8のtyped printingは完了したため、次はproduction integration、parity sign-off、supported entrypointのcutoverを行う。

## Current Progress

- Branchは `sasa/csharp`、現在のbase/HEADは `452528b`。Step 8のproduct、tests、review、plan、DESIGN、STATE、HANDOFFは未commitの作業ツリーにある。commit、push、stageは行っていない。
- 公式PDFsharp 6.2.4を `PDF_Manager.Printing` の背後に導入し、typed desktop graphからlegacy PdfSharpCore/ImageSharpと制限fontを排除した。
- immutable `PrintJob`、page、table、diagram、result、viewport-capture modelを実装し、previewとexportは同じauthoritative plan、text runs、clip bounds、layout geometryを使う。
- A3/A4、portrait/landscape、margins、scale、page numbers、table pagination、grapheme-safe clipping、full selectable preview text、atomic PDF save、例外原因保持を実装した。
- desktopは21 editor tablesとMoving Loadsの計22表、model/load/result diagrams、static/nonlinear/modal/derived/moving result selectionをUI thread上でcaptureし、失敗・取消時は既存previewを維持する。
- whole-document preview/exportは一つの共有work budgetでpreflightし、PDF/font workはprocess-wideに直列化する。installed Windows fontsは言語別にlazyかつboundedに解決し、font binaryは同梱しない。
- `FramePrintAzure` はtyped APIへ再構築せず、Step 9 cutoverでlegacy local print surface、manual harness/APIとともに廃止する方針を決定した。
- 両Release solution buildは0 warnings / 0 errors。両solution testsは567/567 PASS（Core 272、Printing 73、Rendering 56、LocalRuntime 14、UI 152）。coverage率は未計測。
- ownership reconcileはoverlap 0 / unowned 0 / idle 0。AgentOnlyは `overall=pass`（`.agents/logs/check-20260921T031543091Z-1108.log`）。`git diff --check` もPASS。
- 最終reviewはSecurity Critical 0 / High 0 / Medium 0 / Low 1、Quality 0 / 0 / 0 / Low 0、Tests 0 / 0 / 0 / Low 4。報告書は `.agents/docs/research/review-{security,quality,tests}-csharp-frameweb-client.md`。
- full Python/Angular gateは再baseline化していない。既知baseline 3,273 PASS / 6 FAIL / 4 ERRORを維持する。legacy hostの既知High 2 / Medium 2と完成アプリ再配布NO-GOも未変更。

## What Worked

- layoutを一度だけmaterializeし、preview image、selectable text、PDF exportが同じpage planを消費する構造にしたことで、表示と保存の乖離を排除できた。
- `PrintPagePlan.TextRuns` にfull textとactual display text、clip、font、width、alignment、truncationを保持し、13列表と25%〜400% scaleでもcell境界を越えないことをgoldenとacceptance testsで固定した。
- 全ページpreviewを一つのsemaphore、validation/hash、shared budgetで処理し、exact/+1境界とproduction auto-fitをテストできた。
- languageごとのfont解決をlazyにしたため、English outputはCJK fontの有無に依存しない。CJKは埋込みfontを使わず、source-size boundとTTC face検査を通す。
- final remediationで初回reviewのHigh 3 / Medium 2をすべて解消し、Qualityはfinding 0、SecurityはLow 1のみまで収束した。

## What Didn't Work

- 利用可能なcomputer-use surfaceにbrowser/appがなく、Edge/Chrome headless screenshotもblankだったため、GUI viewerによるCJK実glyph目視証跡は取得できなかった。構造、ToUnicode、抽出、font provenanceはPASSし、手動確認用 `tmp/pdfs/step8-cjk-browser-proof.pdf` は生成済み。
- test reviewのLow follow-upは、CJK実glyph表示、複数ページ本文no-loss、不正clip負例、semantic determinismを明示するテスト名の4件。coverage率も未計測。
- Security Low 1として、atomic save時のparent-directory/reparse identity swap raceが残る。通常の原子的置換は実装済みだが、敵対的な同時filesystem操作まで閉じていない。
- 両solution buildを並列実行すると共有 `FramePrintAzure/obj/.../WorkerExtensions/buildout` の一時file lockが起きるため、solution buildは直列実行する。
- legacy hostはtyped desktop境界から隔離されているだけで、安全化されていない。匿名endpoint、無制限work、脆弱dependency、制限fontを持つため公開・再配布しない。

## Next Steps

1. production authentication provider、token保管、HTTP境界へのauthorization injectionを確定する。
2. installer/update、per-user settings、crash/diagnostics、backend endpoint discovery、offline/local mode、署名付きrelease packagingを実装する。
3. `FrameWebforJS` とのscenario parity matrixを三言語、全editor、全viewport、計算、first/last case、nonlinear/modal、derived、print、cancel/error recoveryまで実行する。
4. clean-machine gate、SBOM、forbidden-file/font scan、package vulnerability scan、signed artifact検証を追加する。
5. parity sign-off後に `FrameWeb.Startup` をC# desktop supported entrypointへ切替え、Angular redirectを停止する。
6. `FramePrintAzure`、`PDF_Manager.LegacyPrinting`、legacy local print endpoint、`PDF_Test` / `PDF_Test_CLI`、obsolete manual print APIを廃止する。
7. Angular/Electronをreferenceとして残すかarchive/deleteするかを決定し、release documentationを更新する。

Step 9のproduction gatesが完了するまで、legacy hostの公開と完成アプリの再配布はNO-GOのままとする。
