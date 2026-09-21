# Handoff — C# FrameWeb Desktop Client（Step 8開始）

## Goal

`FramePrintPDF/PDF_Manager` を .NET 8 / WinForms のデスクトップ製品へ再構築し、Python FEM と唯一の成功応答 `AnalysisResultSet v1` を維持したまま、型付き文書、入力、結果表示、OpenGL描画、PDF出力を段階的に完成させる。Step 0～7は完了したため、次は `.agents/docs/plans/csharp-frameweb-client.md` の Step 8「replace the legacy print path with typed printing」から開始する。

## Current Progress

- Branch: `sasa/csharp`。現在のHEADはStep 6 commit `2b3b4945887e6afd0dfcc17b1750ef57489b3c56`。
- 作業ツリーにはStep 7のproduct/test、review、plan、DESIGN、STATE、本handoffの未commit変更がある。commit、push、stageは行っていないため、破棄・reset・re-stageしない。
- Step 7はordered static/nonlinear/modal navigation、displacement/reaction/member-force table・diagram、static-only DEFINE/COMBINE/PICKUP、moving parent/child paging、signed/absolute envelope、3D CSV／2D `.pik`、atomic exportまで完成した。
- generic PICKUP表示はsigned greatest-absoluteを維持し、engineering exportはper-focus max/min sourceとcorrelated vectorを保持する独立typed envelopeを使う。moving reaction absoluteはchild-onlyで、childなしの場合だけparentへfallbackする。
- `ResultPresentationBudget` はpages、derived/moving definitions、operands、output entities、scalar workを一つのcandidate全体で制限する。CSVは非信頼textだけをspreadsheet-safeにし、負数はnumericのまま保持する。
- Python呼び出しはAPIだけが技術的選択肢ではないが、本製品ではprivate loopback HTTP、runtime-owned `HttpClient`、Job-owned listener、`AnalysisResultSet v1` の既承認境界を継続する。transport変更は別のarchitecture taskとする。
- 両Release solution buildは0 warnings / 0 errors。両solution testsは501/501 PASS（Core 272、composition/Printing 17、Rendering 56、LocalRuntime 14、UI 142）。coverage率は未計測。
- ownership reconcileはoverlap 0 / unowned 0 / idle 0。AgentOnlyは`overall=pass`。最終reviewはSecurity 0/0/0/Low 1、Quality 0/0/0/Low 2、Tests 0/0/0/Low 2。
- Python／Angular全件は再実行・再baseline化していない。既知の3,273 PASS／6 FAIL／4 ERRORを維持する。legacy host High 2／Medium 2、CJK/font・publish課題、完成アプリ再配布NO-GOも不変。

## What Worked

- UI状態を変更する前に完全なresult-presentation candidateを構築・検証し、invalid／partial resultで既存表示を壊さない境界。
- Ct 11ケースのasset-exact order、Angular moving `1, 2, 1.1, 1.2` order、全6 reaction componentのchild-only absolute provenanceをCore/UI双方で固定した。
- result table、viewport scene、navigation、extrema、exportが同じtyped presentation modelを共有し、legacy `disg`／`reac`／`fsec` adapterを導入していない。
- PICKUP 3D CSVと2D fixed-width `.pik`を実service/Shell/save経路でgolden検証し、formula-safe text、bounded bytes/work、atomic replacementを組み合わせた。
- Security／Quality／Testsの独立レビューで全Critical／High／Mediumを是正し、残件をLowだけにした。

## What Didn't Work

- solution buildを並列実行すると、両solutionが共有する `FramePrintAzure/obj/.../WorkerExtensions/buildout` で一時的なfile lockが発生した。buildは直列で再実行し両方成功したため、今後もsolution buildは直列にする。
- `verify_delegation.py` はWindowsの既定cp932で大きなUTF-8 diffを読むとdecode失敗する。`$env:PYTHONUTF8='1'` を設定して実行する。
- coverage率は未計測。full Python／Angular gateは長時間かつ既知failureを持つため、今回のC# taskでは再実行していない。
- 残存Low: export先のreparse point／parent-directory swap hardening、上限内のeager UI-thread work、`ProjectDocumentContent`の責務集中、limit constructor hard-cap行列、cleanup二次失敗のdeterministic injection。

## Next Steps

1. Step 8の前に旧 `Printing/**`、`PDF_Test`／`PDF_Test_CLI` fixtureをcharacterizeし、A3/A4、縦横、page number、table pagination、diagram layout、CJK font要求をtyped acceptance fixtureへ固定する。
2. `PrintInput`／`PrintData` dictionaryを新desktopへ戻さず、typed `PrintJob`、page section、table、diagram、result、viewport captureへ移行する。
3. legacy PdfSharpCore/ImageSharp graphを置換し、font resolver、export concurrency、exception provenance、body/decompression/image/page/work上限を明示する。
4. preview、page count、input/result table、model/load/result diagram、scale/layout、file exportをdesktop shellへ接続し、live viewport captureを再利用する。
5. `FramePrintAzure`を削除するかtyped Printingへ再構築するかを決める。認証、resource limits、patched dependencies、CJK licensing、SBOM／forbidden-file gateが揃うまでlegacy hostを公開・配布しない。
6. task-scoped .NET tests/build、PDF parse/text/page assertions、rendered golden、反復／並行export、AgentOnly、ownership、review、`git diff --check`を完走し、coverage未計測と既知Python／Angular baselineを明示する。

package-only開発経路はStep 8へ進んでよい。ただし、legacy hostのHigh security findings、CJK font strategy、full PDF parity、publish SBOM／forbidden-file scanが解消されるまで、legacy hostの公開と完成アプリの再配布はNO-GOのままである。
