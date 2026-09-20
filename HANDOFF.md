# Handoff — C# FrameWeb Desktop Client（Step 5以降）

## Goal

`FramePrintPDF/PDF_Manager` を .NET 8 / WinForms のデスクトップ製品へ再構築し、Python FEMを維持したまま、型付き文書、`AnalysisResultSet v1`、ドッキングUI、OpenGL描画、型付きPDF出力を段階的に実装する。Step 0～4は完了したため、次セッションは `.agents/docs/plans/csharp-frameweb-client.md` の Step 5「complete all model input editors」から開始する。

## Current Progress

- Branch: `sasa/csharp`
- Step 0実装commit: `572a1d1f49e1c8731bb07f04162a47f084d99b35`
- Step 0の実装項目は完了し、計画書のcheckboxと実績を2026-09-20時点へ更新した。
- `.agents/docs/DESIGN.md` には、C#はfrontendのみを置換すること、Python FEMと`AnalysisResultSet v1`を維持すること、計算と印刷を分離すること、legacy互換を要求しないことを記録済み。
- `FramePrintPDF/PDF_Manager.Core` にversioned `DocumentKey`、layout DTO/validator、whitelist factory/restore、tool hide/document dispose policyを実装済み。CoreはWinForms/OpenGL/PDF libraryへ依存しない。
- `FramePrintPDF/PDF_Manager.Core.Tests` はReleaseで41/41 PASS。
- `FramePrintPDF/PDF_Manager.RendererProbe` はDockPanelSuiteと実OpenGL 3.3 contextを使い、100サイクル・600 frames・200 capturesを完走。終了後のlive context/subscription/windowはすべて0。描画はinvalidation-onlyで`Application.Idle` loopを使わない。
- `FramePrintPDF/docs/csharp-frameweb-client-dependency-inventory.md` を作成済み。package-only経路はGO、完成アプリの再配布はNO-GO。
- Step 1は作業ツリーで完了。`PDF_Manager` は `net8.0-windows` WinExe / WinForms composition rootとなり、DockPanelSuiteの空shellを起動・終了できる。
- `PDF_Manager.Rendering` にStep 0の実OpenGL lifecycleを移し、RendererProbeはproduction libraryを検証する薄い実行ファイルになった。3/20/100-cycleがPASSし、最終100-cycleは100 contexts・600 frames・200 captures・live resource 0で完了した。
- dependency-freeなtyped `PDF_Manager.Printing` と、Core 41件・composition/Printing 9件・Rendering 10件・STA UI 1件の自動テストを追加した。両solutionで61/61 PASS。
- `FramePrintAzure` の旧`PrintInput`依存は非packableな `PDF_Manager.LegacyPrinting` に隔離した。desktop shellはlegacy bridge、旧dictionary、PdfSharpCore、restricted fontを参照しない。
- Step 0/1のproduct/test/probe/legacy bridgeは `FrameWeb.sln` と `FramePrintPDF/FramePrintPDF.sln` へ登録済み。`PDF_Test` はactive solutionからのみ外し、source/fixtureはStep 8 characterization用に保持している。
- Step 2は完了。`PDF_Manager.Core/Documents` にinput-only `ProjectDocument v1`、strict/deterministic `System.Text.Json` serializer、schema artifact、UTF-8/unknown/duplicate/non-finite/reference validation、atomic `JsonProjectStore` を追加した。selection/dirtyとruntime `AnalysisResultSet`は永続化しない。
- `PDF_Manager.Core/Analysis` に全`AnalysisResultSet v1` variantのimmutable DTO、strict parser/semantic validator、`ResultCoordinate` index、全体検証後だけ置換する`AnalysisResultState`を追加した。C#は共有positive 6件/negative 7件を直接読み、Python側contract testも14/14 PASS。
- `PDF_Manager.Core/Results` にstatic-only DEFINE/COMBINE（線形和）/PICKUP（成分別absolute selection）、明示的moving-load paging、signed min/max envelopeとsource case provenanceを追加した。base resultは変更しない。
- `IAnalysisClient`、`IPrintExporter`、`IProjectStore` とtyped error/cancellation contractを追加し、Coreは引き続きHTTP/WinForms/OpenGL/PdfSharpCore/legacy dictionaryへ依存しない。
- Core testsは75/75、両solution testは95/95 PASS。必須JSON objectの`null`はtyped format/contract errorとして拒否し、moving-load envelope単体でもcase重複・逆順を拒否する。`FrameWeb.sln` Release buildは成功し、既知の`LegacyPrinting` 28 warningsだけをclean/invalidation時に再確認した。incrementalなsolution buildの0 warningsはclean品質の根拠には使わない。
- Step 3は完了。`MainForm` にmenu/commands、left navigation、central document host、right editor、bottom diagnostics/progressを実装し、ja/en/zhの4 resource setへuser-visible stringを集約した。
- `DockContentRegistry` はstable `DocumentKey`、same-key reuse、tool hide/document dispose、creating-thread制約を持つ。`DockLayoutAdapter` はwhitelist key、live tab order、dock state、floating bounds、logical active documentをbounded JSONでatomic保存し、失敗時にsnapshot rollbackする。LayoutState v1はdocked pane比率とauto-hideを意図的に永続化しない。
- new/open/save/closeは単一transition gateとdocument revisionで直列化した。activationはlatest-winsでcoalescing/cancellationし、unexpected OCEは通常失敗としてuser-safe boundaryへ送る。終了時は既存operation、dirty save、layout saveを注入可能なtimeoutで制限し、late publishを拒否する。dirty-save timeoutはcloseを中止し、layout-save timeoutは診断後にcloseを許可する。
- UiTestsは74/74、両solution testは168/168 PASS（Core 75、composition/Printing 9、Rendering 10、UI 74）。両Release solution build、targeted format、ownership/delegation/work-log/document contract、`git diff --check`、AgentOnly gateもPASS。coverage率は未計測。
- `.agents/docs/DESIGN.md` にはLegacyPrinting隔離、Step 2 contract、Step 3 shell lifecycleに加え、Step 4のauthenticated local runtime、typed scene/live capture、dependency-free PDF境界を共有writerで記録済み。次はStep 5である。
- Step 3最終レビューのLow（timeoutしたSTA threadのprocess isolation）はStep 4 tests reviewへ継続した。Step 4レビューの残件とlegacy Azure/local print hostのHigh 2件・Medium 1件、公開・再配布NO-GOは下記のとおり不変。詳細は `.agents/docs/research/review-{quality,tests,security}-csharp-frameweb-client.md`。
- Step 4の最終AgentOnlyは `overall=pass`（`.agents/logs/check-20260920T131248150Z-46324.log`）。未変更のPython/Angular全件は約2時間かかる既知failのため再実行せず、最新full baseline `.agents/logs/check-20260920T035935489Z-32252.log`（3,273 PASS・6 FAIL・4 ERROR）を維持する。
- Step 4は完了。representative preset、new/open/save/save-as、bounded undo/redo、最低限のnode/member/support/load-case editorをtyped `ProjectDocument`へ接続した。request JSONは決定的で、`FrameWebAnalysisClient`はtransport timeout、concurrency、byte/entity/result上限とstrict `AnalysisResultSet` validationを通過した結果だけを公開する。
- `FrameWeb.LocalRuntime` はdesktop向けにPythonだけを起動する。per-launch CSPRNG secretを外部へ露出せず、ready判定と全request前にloopback listener PIDのprivate Job所属を確認し、redirect/proxy/cookie/connection reuseを無効化する。Python側は認証をbody読取前に行い、4 MiB raw/decompressed上限、single gzip member、canonical JSON media type、strict UTF-8を強制する。
- typed Z-up viewportはnode/member/support、force/moment load、selection/hit、orthographic/perspective、fit/home、displacement layer、captureを実装した。WGL teardownはcurrent判定と`MakeNoneCurrent`を明示し、live captureはUI exception boundary内で実行する。
- dependency-free typed PDFはmodel summary、result table、live viewport RGBを1 pageへ出力する。test-only subset rasterizerはxref/trailer/page/resource/content/imageを独立解析し、595x842 Gray8 goldenと壊れたresource/matrix/image mutationのfail-closedを検証する。CJK font、pagination、full print parityはStep 8へ継続する。
- 両Release solution buildは0 warning/error、両solution testは253/253 PASS（Core 112、composition/Printing 17、Rendering 27、LocalRuntime 14、UI 83）。targeted Pythonは165/165 PASS。RendererProbe 20-cycleと独立した反復/並行WGL reviewは全PASSし、live counterは0。coverage率は未計測。
- Step 4の独立reviewはsecurity/quality/testsすべてPASSで新規Critical/High/Mediumは0。残件は各reviewのLow（security 3、quality 1、tests 2）。legacy Startup匿名analysis Medium 1、legacy print High 2/Medium 1と公開・完成アプリ再配布NO-GOは不変。

現在のHEADはStep 3 base `090cc5a3bb3eddc219819add2219769c50eb3f97`。作業ツリーにはStep 4の未commit product/test変更、計画・DESIGN・STATE・review・本handoff変更がある。既存stage状態を含め、変更を破棄・reset・re-stageしないこと。

## What Worked

- maintained NuGet packageだけを使う経路:
  - DockPanelSuite / ThemeVS2015 3.1.1
  - OpenTK.GLControl 4.0.2
  - OpenTK Graphics / Mathematics / Windowing.Desktop 4.9.4
- 旧`isasPrint`はarchitecture/behaviorの参照だけにし、所有するOpenTK 4 rendererと最小shaderを新規実装する方針。
- GL callをUI threadへ限定し、`Initialize` / `SetModel` / `Resize` / `Render` / `Capture` / `Dispose`を冪等にした明示的lifecycle。
- stable `DocumentKey` とversioned layout DTO、CLR型名や`Activator`を使わないwhitelist-only restore。
- `Shell -> Core/Rendering/Printing` の一方向project reference。Coreは引き続きUI/OpenGL/PDF dependencyなし。
- stable keyとwhitelist factoryを使うcreating-thread-only docking。toolはhide/reuse、documentはdispose/removeし、layout restoreはtransactionalにrollbackする。
- async shell commandはdocument revisionでpublishを検査し、dirty confirmation中のreentrancyやlate completionで新しいdocumentを上書きしない。
- closeはUI threadをblockせず、active operation、dirty save、layout persistenceを個別にbounded cleanupする。非協調実装のlate completionは観測しつつUI commitを拒否する。
- 既存Azure/local print handlerを削除せず `PDF_Manager.LegacyPrinting` に隔離し、新desktopから旧untyped APIとrestricted fontを排除する境界。
- OpenGL teardownは、GLControlを親から外す前にrendererをDisposeする。`--verify` はUI-thread例外をmodal dialogにせずstderr + exit 2へ変換する。
- local runtimeはready JSONだけを信頼せず、listener PIDのJob ownershipをready時とrequest connect時に検査する。呼出側はruntimeが生成するpreconfigured `HttpClient`だけを使い、secretを扱わない。
- scene/input/result/PDF enumerablesはdimension-firstかmax+1で上限検査し、大きな列挙を無制限にmaterializeしない。PDF goldenはproduction writerから独立したsubset parser/rasterizerで検査する。
- task-scoped検証:
  - `dotnet test FramePrintPDF/PDF_Manager.Core.Tests/PDF_Manager.Core.Tests.csproj -c Release`
  - `dotnet build FrameWeb.sln -c Release`
  - `dotnet test FrameWeb.sln -c Release --no-build`
  - `dotnet build FramePrintPDF/FramePrintPDF.sln -c Release --no-restore`
  - `dotnet test FramePrintPDF/FramePrintPDF.sln -c Release --no-build`
  - `uv --directory FrameWeb run --locked --extra dev python -m pytest tests/io/test_result_contracts.py -q`
  - `& .agents/check.ps1 -AgentOnly -AllowProductPath 'FramePrintPDF'`
  - `dotnet run --project FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj -c Release --no-build -- --verify --cycles 100`

## What Didn't Work

- Step 1完了後のリポジトリ正規全体検査は完走したがgreenではない。実行は `& .agents/check.ps1 -AllowProductPath 'FramePrintPDF'`、結果は `overall=fail`、ログは `.agents/logs/check-20260920T035935489Z-32252.log`。Agent系、scope isolation、`git diff --check`、`.NET build` はPASSした。
- Python: 3,273 PASS、6 FAIL、4 ERROR。validation benchmark/helperが現在の`AnalysisResultSet`ではなく旧flat keyの`node_displacements` / `metadata`を直接参照している。
- Python全件テストだけで6,937.36秒（1:55:37）かかる。正規ゲートを重複実行せず、1本を完走させること。
- Angular test: `src/polyfills.ts`と`src/test.ts`がTypeScript compilationに含まれず、`node_modules/@fortawesome/some-free/js/all.min.js`も解決できない。
- Angular build: `src/environments/environment.prod.ts`がない。
- Step 1の初回統合では `FramePrintAzure(net8.0) -> PDF_Manager(net8.0-windows)` が `NU1201` になった。desktopへlegacy APIを戻さず、`PDF_Manager.LegacyPrinting(net8.0)` を介すことで解消した。
- 主担当の最初の100-cycle probeでは、GLControlを親から外してから`MakeCurrent`していたため `WGL: Failed to make context current` のmodal dialogが出た。processを終了し、teardown順序とverification exception modeを修正後、3/20/100-cycleを再実行して全PASSした。
- `PDF_Manager.LegacyPrinting` の旧linked sourceには既存由来28 warningsがあり、cleanまたはincrementally invalidatedなsolution buildでも記録される。up-to-date buildの0 warningsをclean-build品質の根拠にしない。build自体は成功するが、bridgeは新製品品質の根拠ではなくStep 8までの移行負債である。
- セキュリティレビューでは、legacy hostの匿名HTTP triggerからHigh advisoryを持つImageSharp 1.0.4へ到達できること、request body・base64・gzip展開・image/page/PDF work・時間・同時実行数に上限がないことをHighと判定した。加えて、非packable bridge DLLに制限fontが埋め込まれStartup出力へcopy-localされる。新desktopはこれらを参照しないが、legacy hostは認証・上限・patched PDF/image基盤・font方針が揃うまで公開/配布しない。
- MS Gothic、MS Mincho、SimSunの再配布権は確認できず、旧THREE shader/typeface JSON/LTC textureも来歴が不十分。これらをコピーして検査を通す方針は不可。
- PdfSharpCoreのblind upgradeや、ComponentOne、machine-absolute DLL referenceの導入も不可。
- Step 2のteam-execute実装担当3名はruntime usage limitでコード変更前に停止した。共有ツリーに部分変更がないことを確認後、主担当が同じ所有境界でfallback実装したため、teammate self-reportは完了根拠に使用していない。
- Step 2完了後にfull `.agents/check.ps1`を開始したが、今回未変更のPython全件が前回同様の長時間実行に入ったため、AGENTS.mdのtask-scoped gate指示に従って安全に中断した。対象.NET gateとAgent-only gateは別途すべて完走済みであり、既知full baselineをgreenとは報告しない。
- Step 3初回レビューではdirty-save reentrancy、operation cleanup、layout未配線、非transactional restore、thread affinity、未キャンセルOCEの黙殺、close-phase saveの無期限待ちが見つかった。すべて修正し回帰テストを追加した。STA harnessはtimeout時にbackground threadを安全に強制終了できないため、Lowのprocess-isolation改善余地が残る。
- Step 4のWGL反復検証では、GPU resource解放後もcontextがcurrentのままGLControlをdisposeする競合で一時的な失敗が再現した。冗長な`MakeCurrent`を避け、解放後に`MakeNoneCurrent`して修正し、反復・並行probeで再発しないことを確認した。
- Step 4 Low follow-upは、runtime出力のwhole-line allocation/転送量、`Process.Start`からJob assignmentまでの短いescape window、同期Python solveへのclient cancellation非伝播、Core/user messageとtyped PDF labelのCJK fallback、STA timeout child-process isolation、runtime 503/post-ready-exit/stop-during-start race coverageである。

## Next Steps

1. `AGENTS.md` とcontext-loaderに従い、`git status --short --branch`、本handoff、計画書、`.agents/docs/DESIGN.md`、Step 4 public boundariesとreviewを確認する。未追跡/既存stage状態を捨てない。
2. Step 5をtest-firstで開始し、nodes、members、rigid zones、supports、elements/materials/sections、panels、joints、notice points、member springs、load cases/values、DEFINE/COMBINE/PICKUPのtyped editorを完成させる。
3. keyboard navigation、multi-row edit、copy/paste、insert/delete、selection sync、undo/redoをshared grid behaviorとして実装し、画面ごとの複製を避ける。
4. 残る3つを含む全4 built-in presetをtyped fixture/resourceとして追加し、warningなしのload、deterministic round-trip、expected requestを検証する。
5. cross-table reference、dimension、duplicate ID、case limit、calculation preconditionをHTTP submission前のCore validationへ集約する。
6. Step 4 Low follow-upを次の担当範囲に合わせて解消し、legacy Startup/print findingsとCJK/full print/publish NO-GOを別枠で維持する。
7. task-scoped .NET/Python gates、ownership/delegation/work-log/document contracts、`git diff --check`、AgentOnlyを継続し、coverage未計測と既知Python/Angular full baselineを明示する。

package-only開発経路はStep 5へ進んでよい。ただし、`PDF_Manager.LegacyPrinting` のHigh security findings、CJK font strategy、full PDF parity、publish SBOM/forbidden-file scanが解消されるまで、legacy hostの公開と完成アプリの再配布はNO-GOのままである。
