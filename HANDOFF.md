# Handoff — C# FrameWeb Desktop Client（Step 2以降）

## Goal

`FramePrintPDF/PDF_Manager` を .NET 8 / WinForms のデスクトップ製品へ再構築し、Python FEMを維持したまま、型付き文書、`AnalysisResultSet v1`、ドッキングUI、OpenGL描画、型付きPDF出力を段階的に実装する。Step 0とStep 1は完了したため、次セッションは `.agents/docs/plans/csharp-frameweb-client.md` の Step 2「型付きdocument/result foundation」から開始する。

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
- `.agents/docs/DESIGN.md` にはLegacyPrinting隔離とStep 8での廃止条件を共有writerで記録済み。Step 2以降と縦切りMVPは未着手。
- team-executeの独立レビューは、品質PASS（Critical/Highなし）、テストPASS（Critical/Highなし）、セキュリティChanges requested（legacy Azure/local print hostにHigh 2件）。新desktopの依存・資産境界は確認済みだが、旧hostの公開・配布は認められない。詳細は `.agents/docs/research/review-{quality,tests,security}-csharp-frameweb-client.md`。

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

## Next Steps

1. `AGENTS.md` とcontext-loaderに従い、`git status --short --branch`、本handoff、計画書、`.agents/docs/DESIGN.md`、dependency inventoryを確認する。作業ツリーにはStep 1実装と前回のplan/HANDOFF変更、ユーザー提供の未追跡 `.agents/skills/handoff/SKILL.md` があるため捨てない。
2. Step 2をtest-firstで開始し、新しい `ProjectDocument` aggregateとvalidation boundaryをCoreへ追加する。runtime `AnalysisResultSet` をpersisted input documentへ混在させない。
3. `System.Text.Json` によるversioned project-file schema、deterministic serializer、UTF-8、finite-number、unknown-field、ID/reference、atomic save policyを定義する。
4. 共有 `FrameWeb/tests/data/contracts/analysis-result-set-v1.schema.json` とpositive/negative fixturesを直接読むC# DTO/validator/index testを追加し、fixtureをC#側へ複製しない。
5. `ResultCoordinate(caseId, stateKind, stateIndex)` によるimmutable result indexと、失敗時にprevious resultを保持するcommit boundaryを実装する。
6. `ResultPresentationService` と `IAnalysisClient` / `IPrintExporter` / `IProjectStore` のUI非依存interfaceを定義する。HTTP、WinForms、OpenGL、PdfSharpCoreをCoreへ持ち込まない。
7. Step 2完了時に両solution build/test、共有contract fixtures、Core dependency testsを実行する。Rendererを変更した場合は短い3-cycle probeの後に100-cycleを実行し、modalなし・live resource 0を確認する。
8. 正規全体検査の既知Python/Angular failureはC# Step 2と混ぜて隠さず、repository-wide greenになるまで全体PASSとは報告しない。
9. 実GL probeの自動gate化、DockPanel構成をassertするSTA test、solution/LegacyPrinting境界の回帰test、evaluated MSBuild/publish itemsとassembly resourceを検査するrestricted-asset testを後続gateへ追加する。coverage率は未計測なので数値を推定しない。

package-only開発経路はStep 2へ進んでよい。ただし、`PDF_Manager.LegacyPrinting` のHigh security findings、CJK font strategy、PDF golden、publish SBOM/forbidden-file scanが解消されるまで、legacy hostの公開と完成アプリの再配布はNO-GOのままである。
