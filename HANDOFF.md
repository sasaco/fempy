# Handoff — C# FrameWeb Desktop Client（Step 3以降）

## Goal

`FramePrintPDF/PDF_Manager` を .NET 8 / WinForms のデスクトップ製品へ再構築し、Python FEMを維持したまま、型付き文書、`AnalysisResultSet v1`、ドッキングUI、OpenGL描画、型付きPDF出力を段階的に実装する。Step 0～2は完了したため、次セッションは `.agents/docs/plans/csharp-frameweb-client.md` の Step 3「desktop shell and docking lifecycle」から開始する。

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
- `.agents/docs/DESIGN.md` にはLegacyPrinting隔離、Step 2のdocument/result/presentation contract、Step 8での廃止条件を共有writerで記録済み。Step 3以降と縦切りMVPは未着手。
- Step 1の独立レビューとStep 2のlead fallback addendumは、品質PASS（Critical/Highなし）、テストPASS（Critical/Highなし）、セキュリティChanges requested（legacy Azure/local print hostにHigh 2件）を維持した。Step 2には、Step 4前にJSON byte/entity/result上限を定義するLow 1件がある。詳細は `.agents/docs/research/review-{quality,tests,security}-csharp-frameweb-client.md`。
- Step 2の関連gateはすべてPASS。agent-infrastructure gateは `overall=pass`（最新確定ログ `.agents/logs/check-20260920T082712131Z-27880.log`）。未変更のPython/Angular全件は約2時間かかる既知failのため再実行せず、最新full baseline `.agents/logs/check-20260920T035935489Z-32252.log` を維持する。

現在の作業ツリーには、このhandoff作業による計画書変更と`HANDOFF.md`に加え、ユーザー提供の未追跡 `.agents/skills/handoff/SKILL.md` がある。skillファイルを変更・削除・上書きしないこと。

## What Worked

- maintained NuGet packageだけを使う経路:
  - DockPanelSuite / ThemeVS2015 3.1.1
  - OpenTK.GLControl 4.0.2
  - OpenTK Graphics / Mathematics / Windowing.Desktop 4.9.4
- 旧`isasPrint`はarchitecture/behaviorの参照だけにし、所有するOpenTK 4 rendererと最小shaderを新規実装する方針。
- GL callをUI threadへ限定し、`Initialize` / `SetModel` / `Resize` / `Render` / `Capture` / `Dispose`を冪等にした明示的lifecycle。
- stable `DocumentKey` とversioned layout DTO、CLR型名や`Activator`を使わないwhitelist-only restore。
- `Shell -> Core/Rendering/Printing` の一方向project reference。Coreは引き続きUI/OpenGL/PDF dependencyなし。
- 既存Azure/local print handlerを削除せず `PDF_Manager.LegacyPrinting` に隔離し、新desktopから旧untyped APIとrestricted fontを排除する境界。
- OpenGL teardownは、GLControlを親から外す前にrendererをDisposeする。`--verify` はUI-thread例外をmodal dialogにせずstderr + exit 2へ変換する。
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

## Next Steps

1. `AGENTS.md` とcontext-loaderに従い、`git status --short --branch`、本handoff、計画書、`.agents/docs/DESIGN.md`、Step 2のCore public contractを確認する。未追跡/既存変更を捨てない。
2. Step 3をSTA test-firstで開始し、`MainForm`へmenu/commands、left navigation、central document viewport、right editor/tools、bottom diagnostics/progressを構成する。
3. 既存Coreの`DocumentKey`と`ContentFactoryRegistry`を使う`DockContentRegistry<DocumentKey, Func<DockContent>>`をshell側へ実装し、same-key reuse/activationとentity document coexistenceを固定する。
4. versioned layout save/restoreをCore layout DTOとwhitelist registryへ接続し、unknown version/key、active document、bounds/order、hide-vs-disposeをSTA testで検証する。
5. dirty close confirmation、command state、exception boundary、cancellation、coalesced activation reducerを実装する。Step 2の`ProjectDocument.IsDirty`とtyped operation exceptionを使用し、UI層からCore contractを変更しない。
6. user-visible stringsを`Strings.resx`/`Strings.ja.resx`/`Strings.en.resx`/`Strings.zh.resx`へ移し、caption/CLR type名をidentityに使わない。
7. Step 3完了時にCore 75件をbaselineとする両solution build/test、STA repeated open/close、layout/language/confirmation testsを実行する。Renderingを変更しない限りGL probe再実行は不要。
8. 正規全体検査の既知Python/Angular failure、legacy hostのHigh findings、CJK font/PDF/publish NO-GOはStep 3と混ぜて隠さない。coverage率は未計測なので数値を推定しない。

package-only開発経路はStep 3へ進んでよい。ただし、`PDF_Manager.LegacyPrinting` のHigh security findings、CJK font strategy、PDF golden、publish SBOM/forbidden-file scanが解消されるまで、legacy hostの公開と完成アプリの再配布はNO-GOのままである。
