# Handoff — C# FrameWeb Desktop Client（Step 7以降）

## Goal

`FramePrintPDF/PDF_Manager` を .NET 8 / WinForms のデスクトップ製品へ再構築し、Python FEMを維持したまま、型付き文書、`AnalysisResultSet v1`、ドッキングUI、OpenGL描画、型付きPDF出力を段階的に実装する。Step 0～6は完了したため、次セッションは `.agents/docs/plans/csharp-frameweb-client.md` の Step 7「complete calculation and result presentation」から開始する。

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
- Step 5は完了。単一`EditorContent`に21個のtyped tableをdescriptor-drivenで収容し、model dimension、element/support/joint/member-spring set管理、非default set row、prescribed displacement、全load入力、DEFINE/COMBINE/PICKUPを編集できる。共有controllerはkeyboard、bounded clipboard、multi-row paste、insert/delete、selection、creating-thread/disposalを担い、1 batchを1 undo itemとしてatomicにcommitする。
- `ProjectDocument v1`はversion 1を維持してadditiveに拡張した。strict/deterministic JSON、16 MiB input、100,000 aggregate entity/row、cross-table reference、2D/3D、panel幾何、duplicate identity、256 cases、effective nonzero load、point load L1/L2をCore/request境界で検証する。永続IDは英数字を含むstable IDを維持し、Python用正整数IDはrequest projectionだけで検査する。
- built-in presetは`ramen-viaduct`、`concrete-t-beam-bridge`、`u-shaped-retaining-wall`、`portal-pier`の4件。legacy Angular importerは作らずtyped semantic builderとし、topology/material/support/load/selectorの固定assertionとcanonical request SHA-256を持つ。
- MemberLoadはdocumentからtyped sceneへ代表glyphを射影し、table/viewport selectionを双方向同期する。完全な分布荷重形状を含む全scene layerはStep 6で実装する。
- Step 5最終結果は両Release solution build 0 warnings/errors、両solution tests 401/401 PASS（Core 243、composition/Printing 17、Rendering 27、LocalRuntime 14、UI 100）、focused Core 53/53・persistence/preset 78/78・UI 17/17 PASS。ownershipはoverlap 0 / unowned 0 / idle 0、AgentOnlyは`overall=pass`（`.agents/logs/check-20260920T154552806Z-28216.log`）。
- Step 5 closeout reviewはsecurity/quality/testsともCritical 0 / High 0 / Medium 0 / Low 3。coverage率は未計測。Python/Angular全件は再実行せず、既知baseline 3,273 PASS・6 FAIL・4 ERRORを維持する。legacy側High 2 / Medium 2と完成アプリ再配布NO-GOも不変。
- Step 6は完了。node/member/rigid-zone/support/spring/joint/panel/notice-point/load/displacement/reaction/section-forceの12独立layerを`IViewportScene`、`ICameraController`、`IHitTestService`とstable domain IDの背後へ実装した。support/joint 6 DOF、point/distributed/thermal member load、変位・反力・断面力をtyped command bufferへ保持する。
- 2D XZ orthographic／3D perspective、grid/axes/labels、scale/color legend、hit/hover/selection、active-case load filter、case/state paging、signed min/max/absolute-max、bounded PNGを実装した。decorationsはbounded BGRA bitmapをOpenGL textureへuploadし、Paint/Capture/PNGの同一frame pathでalpha合成する。空workspaceではGL handle/contextを作らない。
- node/member dependency closureで座標・topology依存layerを明示的にinvalidateし、rapid updateをcoalesceする。scene aggregate 250,000、derived vertex/batch/hit/decor/legend、result table 10,000 rows、PNG dimension/pixel/encoded bytesをfail-fastで上限化した。10,000 nodes／9,999 membersのactual masked updateはLoadsだけを再compileし5.873 ms。
- document replacementだけを新scene install後にHomeし、通常editはcameraを保持する。extremaで除外されたselectionはscene置換前にclearし、table/viewport selectionを安定化した。viewport failureはoperation、safe message、original exception、expected/unexpectedを保持するtyped boundaryへ送る。
- 最終結果は両Release solution build 0 warnings/errors、両solution tests 463/463 PASS（Core 243、composition/Printing 17、Rendering 56、LocalRuntime 14、UI 133）。RendererProbe 100-cycleは100 contexts、1,700 frames、600 captures、PNG 200、live context/subscription/window 0。coverage率は未計測。
- Step 6 closeout reviewはsecurity Critical 0 / High 0 / Medium 0 / Low 3、quality 0 / 0 / 0 / Low 3、tests 0 / 0 / 0 / Low 4。Python/Angular full gateは完走・rebaselineせず、既知baseline 3,273 PASS・6 FAIL・4 ERRORを維持する。legacy側High 2 / Medium 2と完成アプリ再配布NO-GOも不変。

現在のHEADはStep 4 commit `b82b4b52b6cb126cec305afec2107d8c077ee5f3`。作業ツリーにはStep 5～6の未commit product/test変更、計画・DESIGN・STATE・review・本handoff変更がある。commit、push、stage状態の変更は行っていないため、変更を破棄・reset・re-stageしないこと。

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
- full input authoringは21個のtable-specific descriptor/adapterと1個のshared grid controllerへ分離する。set managerとrow tableを分け、invalid multi-row editはfinal candidate validation前に公開せず、失敗時にdocument/undo/redoを完全維持する。
- topology自動挿入は辞書順かつ最大10,000候補の共通combination enumeratorで制限し、候補枯渇時はdocument/historyを変更しない。
- rendererはtyped layerごとのcommand bufferをcacheし、dependency closureを通したaffected maskだけを再compileする。screenとPNGのdecorationsを同じOpenGL overlayへ統一し、100-cycle probeでcontext/resource lifecycleを実測する。
- signed extremaは各rowの絶対値最大componentを代表値とし、Minimum／Maximum／AbsoluteMaximumをscene、legend、tableで共通選択する。同値はcontract順を保持する。
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
- Step 5初回reviewは全入力authoring不足、Nodes以外の実grid coverage不足、zero-effect load、point-load L2、退化panel、insert候補衝突、MemberLoad selection、load/displacement ID衝突などを検出した。すべて修正し、最終closeoutでCritical/High/Medium 0を確認した。
- Step 5残存Lowは、security: clipboard/JSONの上限判定前の全量取得・serialize allocationとID/nameの長さ・制御/区切り文字、quality: semantic no-op history・delimiter identity・大規模switch責務、tests: matrix各行の型別success oracle・unsupported enum string・clipboard accepted-at-limit境界である。
- Step 6初回reviewは、node/member依存cache失効、decorations未描画、extrema no-op／load case混在、結果表・derived render workの上限不足、camera replacement、glyph semantics、exception provenanceを検出した。全Critical/High/Mediumを修正し、member topology→Displacementsの最終依存枝もactual cache regressionで閉じた。
- Step 6残存Lowは、security: PNG encode後のbyte上限・非atomic overwrite・delimiter stable ID、quality: renderer/shell責務肥大・未使用scheduler interval・bounded/measured O(N) masked compile、tests: PNG実保存・pinned semantic snapshot・empty-workspace counter・malformed boundary matrixである。
- Step 6 closeout中にdefault `.agents/check.ps1`が対象外Python全件へ入り、出力なしで72分超継続したため中断した。Step 6対象の.NET／agent gatesは別途完走し、既知Python/Angular baselineをgreenとは報告しない。

## Next Steps

1. `AGENTS.md` とcontext-loaderに従い、`git status --short --branch`、本handoff、計画書、`.agents/docs/DESIGN.md`、Step 6のtyped scene/result boundariesと最終reviewを確認する。未追跡/既存stage状態を捨てない。
2. Step 7をtest-firstで開始し、ordered multi-case static result、全accepted nonlinear load step、modal modeを`AnalysisResultSet`から直接navigation・表示する。
3. displacement、support reaction、member section forceのtable/diagramを明示case/state selectorと接続し、既存active-case filter、signed extrema、stable selectionを拡張する。
4. DEFINE／COMBINE／PICKUPをstatic operandだけへ適用し、nonlinear/modal operandにはvisible domain errorを出す。canonical base resultは変更しない。
5. moving-load parent/child paging、component max/min envelope、reaction absolute maximum、member-force extrema、CSV/PICKUP export、deterministic orderを完成させる。shell/solid dataはvalidated modelに保持するが専用screenは初期parity外とする。
6. task-scoped .NET gates、ownership/delegation/work-log/document contracts、`git diff --check`、AgentOnlyを継続し、coverage未計測と既知Python/Angular full baselineを明示する。legacy Startup/print findings、CJK/full print/publish NO-GOは別枠で維持する。

package-only開発経路はStep 7へ進んでよい。ただし、`PDF_Manager.LegacyPrinting` のHigh security findings、CJK font strategy、full PDF parity、publish SBOM/forbidden-file scanが解消されるまで、legacy hostの公開と完成アプリの再配布はNO-GOのままである。
