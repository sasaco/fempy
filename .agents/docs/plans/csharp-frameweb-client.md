## Implementation Plan: C# FrameWeb Desktop Client

### Purpose

`FramePrintPDF/PDF_Manager` を中心に、`FrameWebforJS` と同じ主要業務シナリオを提供する Windows 専用の .NET 8 / WinForms デスクトップアプリを新規構築する。Angular、RxJS、Web Worker、Electron、旧印刷JSONを逐語移植せず、型付き `ProjectDocument`、単一の `AnalysisResultSet v1`、ドッキングUI、C# THREE/OpenGL描画、型付きPDF出力へ再設計する。

既存利用者との後方互換は要件に含めない。Python FEM解析はC#へ移植せず、`FrameWeb` を数値解析サービスとして継続利用する。最初に「モデルを開く → 表示・最小編集 → 計算 → 結果表示 → PDF出力」の縦切りMVPを完成させ、その後に入力・結果・印刷のシナリオ同等性を段階的に閉じる。

### Scope

本計画は、C#デスクトップ製品の設計・実装・検証・切替までを対象とし、Python FEMの数値実装と旧クライアント互換は対象外とする。

#### Implementation Status (2026-09-20)

- Step 0 の実装成果は commit `572a1d1` に保存済みである。`PDF_Manager.Core` と41件の契約テスト、実OpenGLを使う `PDF_Manager.RendererProbe`、依存関係・来歴・再配布可否のinventory、および `.agents/docs/DESIGN.md` の設計決定が含まれる。
- package-only の開発経路は **GO** である。DockPanelSuite 3.1.1、OpenTK.GLControl 4.0.2、OpenTK 4.9.4、および所有する最小renderer/shaderで Step 1 へ進める。
- 完成アプリの再配布は **NO-GO** のままである。MS Gothic、MS Mincho、SimSun、旧THREE shader/typeface/LTC textureは製品へコピー・同梱せず、PDF font strategy、PDF golden、publish成果物のforbidden-file/SBOM検査を後続gateで解決する。
- Step 1 は作業ツリーで完了した。`PDF_Manager` は `net8.0-windows` WinExeとなり、Core/Rendering/typed Printingを参照する空WinForms shell、4つの自動test project、両solutionへの登録が実装済みである。Step 2以降は未着手である。
- Step 2 は作業ツリーで完了した。Coreへtyped `ProjectDocument v1`、厳格かつ決定的なJSON/atomic store、完全な`AnalysisResultSet v1` DTO/validator/index/commit boundary、static-only derived presentation、moving-load paging/envelope、およびtyped service boundaryを実装した。
- Step 3 は作業ツリーで完了した。localized WinForms shell、stable-key docking registry、bounded/atomic/transactional layout、serialized document transition、coalesced activation、dirty-close/cancellation/exception boundaryを実装した。
- Step 4 は作業ツリーで完了した。representative document edit、authenticated Python-only runtime、strict analysis transport、typed OpenGL scene/result presentation、live viewport capture、typed PDF vertical sliceを実装した。次はStep 5の全model input editorである。
- Step 1完了後にリポジトリ正規の全体検査 `& .agents/check.ps1 -AllowProductPath 'FramePrintPDF'` を再実行した。Agent系、scope isolation、`git diff --check`、`.NET build` はPASSしたが、全体は `overall=fail` である。Pythonは3,273件PASS・6件FAIL・4件ERROR（1:55:37）、Angular test/buildはTypeScript compilation、FontAwesome path、`environment.prod.ts` 不在でFAILした。詳細は `.agents/logs/check-20260920T035935489Z-32252.log` を参照する。いずれも既知のC#変更範囲外failureだが、リポジトリ全体をgreenとは報告しない。
- 独立レビューは品質PASS（Critical/Highなし）、テストPASS（Critical/Highなし）、セキュリティChanges requested（旧Azure/local print hostにHigh 2件）である。新desktop境界には旧印刷資産・制限font・既知脆弱packageは入っていない。一方、legacy hostの匿名endpointは脆弱なImageSharp 1.0.4へ到達でき、request/decompression/image/PDF workも無制限なので、公開・配布はNO-GOである。詳細は `.agents/docs/research/review-{quality,tests,security}-csharp-frameweb-client.md` を参照する。

#### Current State

- `FramePrintPDF/PDF_Manager/PDF_Manager.csproj` は現在 `netcoreapp3.1` のクラスライブラリで、依存は `Newtonsoft.Json` と `PdfSharpCore` のみである。WinForms、ドッキング、3Dレンダラー、製品エントリポイントはない（`PDF_Manager.csproj:1-5,33-36`）。
- 実質的な公開入口は生JSONを `Dictionary<string, object>` に変換する `PrintInput` であり、`PrintData` は入力と旧 `disg` / `reac` / `fsec` 結果を同じ非型付きモデルへ集約している（`PrintInput.cs:11-21`、`PrintData.cs:62-143`）。この形は新アプリへ継承しない。
- `PDF_Test` と `PDF_Test_CLI` は手動ハーネスで、自動assertionを持つテストプロジェクトではない（`PDF_Test/Form2.cs:40-58`、`PDF_Test_CLI/Program.cs:18-64`）。
- `FrameWeb.Startup` は現在 Python と Angular を起動し、Angularへリダイレクトする一方、Windows Job Object による子プロセス終了管理を既に持つ（`tools/FrameWeb.Startup/LocalServices.cs:16-44,73-88`、`StartupPage.cs:18-28`、`WindowsProcessJob.cs:7-24`）。
- `FrameWebforJS` の同等性対象は、新規・開く・保存・プリセット、全入力表、2D/3D表示、計算、変位・反力・断面力、DEFINE/COMBINE/PICKUP、移動荷重表示、印刷・PDFである（`app-routing.module.ts:34-64`、`menu.component.html:17-46`）。
- 成功時の計算結果は既に `AnalysisResultSet v1` に一本化され、static / nonlinear / modalを `(case_id, state.kind, state.index)` で識別し、受信全体の検証後にだけ結果を公開する（`analysis-result-set.ts:229-245,647-705,798-833`、`result-data.service.ts:105-145`）。
- `isasPrint` は .NET Framework 4.8 / DockPanelSuite 3.1 / OpenTK 3.3.3 / vendored THREE を使う。再利用するのは描画ホストの層分離、composition root、lazy window registry、activation policyの考え方だけとし、旧framework、ComponentOne、`DockingMdi`、常時Idle描画、再Load、破棄不足、CLR型名による復元は採用しない。

#### Target Architecture

```text
PDF_Manager (net8.0-windows WinForms executable / composition root)
  ├─ Docking shell, commands, resources, document/tool windows
  ├─ PDF_Manager.Core
  │    ├─ ProjectDocument + validation + undo/redo
  │    ├─ AnalysisResultSet DTO/validator/index
  │    ├─ ResultPresentationService
  │    └─ IAnalysisClient / IPrintExporter / IProjectStore
  ├─ PDF_Manager.Rendering
  │    ├─ THREE/OpenGL host + camera + hit testing
  │    ├─ model/load/result scene layers
  │    └─ deterministic viewport capture
  ├─ PDF_Manager.Printing
  │    └─ typed PdfSharpCore document/layout/export pipeline
  └─ FrameWeb.LocalRuntime
       └─ Python process startup, readiness, cancellation, Job Object cleanup

PDF_Manager.Core --HTTP--> FrameWeb (Python FEM)
                         <-- AnalysisResultSet v1 only
```

依存方向は `Shell -> Core`、`Shell -> Rendering`、`Shell -> Printing` とし、`Core` はWinForms、OpenGL、PdfSharpCoreを参照しない。計算と印刷は別境界にし、旧印刷データを計算成功schemaとして復元しない。

#### Feature-Parity Boundary

| Area | C# implementation | Parity rule |
|---|---|---|
| Document | `ProjectDocument`、新規C#文書schema、open/save/save-as、4 presets | 旧保存形式migrationは行わない |
| Input | node、member、rigid zone、support、element、panel、joint、notice point、member spring、load case/value、DEFINE/COMBINE/PICKUP | Angularの画面構造ではなく入力意味とvalidationを一致させる |
| Viewport | Z-up、2D orthographic、3D perspective、grid/axis/label、selection sync | scene layer単位の意味的同等性を受入条件にする |
| Analysis | `IAnalysisClient` がPython APIを呼び、`AnalysisResultSet` を一度だけ厳格検証 | alternate success schema、旧worker schema、部分commitを禁止 |
| Results | static/nonlinear/modal、変位、反力、部材断面力、case/state paging | `ResultCoordinate` と配列順を正本にする |
| Derived results | DEFINE/COMBINE/PICKUP、移動荷重page/envelope、max/min | immutable base resultからPresentation層で導出する |
| Printing | typed print job、preview、save、viewport capture | calculation transportとprint contractを共有しない |
| Shell | docking documents/tools、layout restore、ja/en/cn resources | captionやCLR型名をidentityに使わない |
| Peripheral | authentication、help/MyPage、update、cloud integration | core MVP後のrelease gateとする |

#### Files and Projects

- New files/projects:
  - `FramePrintPDF/PDF_Manager/Program.cs`
  - `FramePrintPDF/PDF_Manager/Shell/MainForm.cs`
  - `FramePrintPDF/PDF_Manager/Shell/DocumentKey.cs`
  - `FramePrintPDF/PDF_Manager/Shell/DockContentRegistry.cs`
  - `FramePrintPDF/PDF_Manager/Shell/LayoutState.cs`
  - `FramePrintPDF/PDF_Manager/Resources/Strings*.resx`
  - `FramePrintPDF/PDF_Manager.Core/PDF_Manager.Core.csproj`
  - `FramePrintPDF/PDF_Manager.Core/Documents/ProjectDocument.cs`
  - `FramePrintPDF/PDF_Manager.Core/Documents/ProjectDocumentSerializer.cs`
  - `FramePrintPDF/PDF_Manager.Core/Analysis/AnalysisResultSet.cs`
  - `FramePrintPDF/PDF_Manager.Core/Analysis/AnalysisResultSetValidator.cs`
  - `FramePrintPDF/PDF_Manager.Core/Analysis/ResultIndex.cs`
  - `FramePrintPDF/PDF_Manager.Core/Analysis/FrameWebAnalysisClient.cs`
  - `FramePrintPDF/PDF_Manager.Core/Results/ResultPresentationService.cs`
  - `FramePrintPDF/PDF_Manager.Rendering/PDF_Manager.Rendering.csproj`
  - `FramePrintPDF/PDF_Manager.Rendering/Three/ThreeViewportHost.cs`
  - `FramePrintPDF/PDF_Manager.Rendering/Scene/*Layer.cs`
  - `FramePrintPDF/PDF_Manager.Printing/PDF_Manager.Printing.csproj`
  - `FramePrintPDF/PDF_Manager.Tests/PDF_Manager.Tests.csproj`
  - `FramePrintPDF/PDF_Manager.Rendering.Tests/PDF_Manager.Rendering.Tests.csproj`
  - `FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj`
  - `tools/FrameWeb.LocalRuntime/FrameWeb.LocalRuntime.csproj`
- Modified files/projects:
  - `FramePrintPDF/PDF_Manager/PDF_Manager.csproj`
  - useful sources under `FramePrintPDF/PDF_Manager/Printing/**` while moving them behind typed APIs
  - `FramePrintPDF/FramePrintAzure/FramePrintAzure.csproj` if Azure printing remains in the product
  - `tools/FrameWeb.Startup/**`
  - `FrameWeb.sln` and `FramePrintPDF/FramePrintPDF.sln`
  - `.agents/docs/DESIGN.md`（Step 0で設計決定を記録済み）
- Shared contract inputs:
  - `FrameWeb/tests/data/contracts/analysis-result-set-v1.schema.json`
  - `FrameWeb/tests/data/contracts/positive/*.json`
  - `FrameWeb/tests/data/contracts/negative/*.json`
- Dependencies selected or inventoried in Step 0:
  - .NET 8 Windows Desktop SDK
  - a maintained DockPanelSuite package supporting .NET 8 WinForms
  - a maintained OpenTK/GLControl path and the reusable subset/provenance of `isasPrint/THREE`
  - PdfSharpCore or a compatible maintained PDF library selected after current PDF golden characterization
  - Microsoft.NET.Test.Sdk plus one repository-standard .NET test framework
- Explicitly excluded dependencies: ComponentOne and machine-absolute binary references from `isasPrint`.

### Implementation Steps

以下を依存順に実施し、各ステップの検証を通過してから次の広い機能面へ進む。

#### Step 0: Freeze the greenfield contract and clear feasibility blockers

- [x] Record in `.agents/docs/DESIGN.md` that the C# app replaces the frontend behavior only, retains Python FEM, consumes only `AnalysisResultSet v1`, requires no legacy compatibility, and keeps calculation/printing separate.
- [x] Produce a source/provenance inventory for the `isasPrint/THREE` subset, DockPanelSuite, OpenTK, shaders, fonts, textures, and transitive packages; decide what may be copied, rewritten, or consumed as a package.
- [x] Build a minimal .NET 8 WinForms probe that opens a DockingWindow-style document, creates a real GL context, draws one deterministic frame, resizes, captures a bitmap, closes, and reopens repeatedly.
- [x] Define the renderer lifetime contract: UI-thread-only GL calls; idempotent `Initialize`, `SetModel`, `Resize`, `Render`, `Capture`, `Dispose`; one context and subscription set per viewport; explicit disposal order; render-on-invalidation when idle.
- [x] Define versioned `DocumentKey` and layout DTOs, tool-window hide policy, document-window dispose policy, and whitelist-only restore without `Activator` or CLR type names.

**Verification result**: Core contract tests are 41/41 PASS. The real OpenGL 3.3 probe completed 100 create/resize/render/capture/close cycles, 600 frames, and 200 captures, ending with zero live contexts, subscriptions, and windows; it uses invalidation-only rendering and has no idle loop. Malformed/unknown layout data is covered by rejection tests. The package-only path is therefore cleared for Step 1. The full redistribution condition is not cleared: restricted or provenance-incomplete fonts and legacy assets must remain excluded, and the PDF font/publish verification described in the dependency inventory remains mandatory before shipping.

#### Step 1: Establish the solution and project boundaries

- [x] Retarget `FramePrintPDF/PDF_Manager/PDF_Manager.csproj` to a `net8.0-windows` WinExe with WinForms enabled and make it the composition root.
- [x] Add `PDF_Manager.Core`, `PDF_Manager.Rendering`, `PDF_Manager.Printing`, unit tests, rendering tests, and STA UI tests with the dependency direction described above.
- [x] Audit the existing `Printing/**` code before migration. No untyped source qualified for direct promotion before Step 8 characterization, so the new `PDF_Manager.Printing` contains only a typed, dependency-free page-layout contract; `PrintInput`, `PrintData`, legacy dictionaries, duplicate Function handlers, and restricted fonts remain outside the desktop boundary in the explicit non-packable `PDF_Manager.LegacyPrinting` bridge used only by the existing Azure/local print host.
- [x] Add all product/test projects to `FrameWeb.sln` and `FramePrintPDF/FramePrintPDF.sln`. `PDF_Test` is removed from the active `FramePrintPDF.sln` only; `PDF_Test` / `PDF_Test_CLI` source and fixtures remain intact for Step 8 characterization and are not treated as automated tests.
- [x] Prevent excluded files such as `KAJYU_ZU23.cs` from returning through wildcard moves.

**Verification result**: `dotnet build FrameWeb.sln -c Release` and `dotnet build FramePrintPDF/FramePrintPDF.sln -c Release --no-restore` pass. The newly added Step 1 projects build warning-free, while a clean/incrementally invalidated solution build emits 28 inherited warnings from the isolated `PDF_Manager.LegacyPrinting` source; an up-to-date incremental build can report 0 warnings and is not evidence of clean warning-free status. Both solution test runs pass 61/61: Core 41, composition/Printing 9, Rendering 10, STA UI smoke 1. Project-reference tests prove Core has no UI/render/PDF dependency and the desktop project excludes legacy printing sources and restricted fonts. The shell opens/closes on an STA thread without leaving forms. The production Rendering library passes the real OpenGL 3/20/100-cycle sequence; the final 100-cycle run created 100 contexts, rendered 600 frames, completed 200 captures, and ended with zero live contexts/subscriptions/windows. A teardown-order defect found during lead verification was fixed by disposing the renderer before WinForms removes its GLControl; verification-mode UI exceptions now exit nonzero instead of displaying a modal Continue dialog.

#### Step 2: Implement typed document and result foundations

- [x] Define one new `ProjectDocument` aggregate for model inputs, load definitions, derived-result definitions, metadata, selection, dirty state, and validation; keep runtime `AnalysisResultSet` outside the persisted input document unless a later persisted-result feature is approved.
- [x] Define the new C# project-file JSON schema and deterministic serializer using `System.Text.Json`; add open/save/save-as, atomic replace, UTF-8, finite-number, ID/reference, and unknown-field policies.
- [x] Implement immutable C# DTOs and strict validation/indexing for the exact `AnalysisResultSet v1` contract, keyed by `ResultCoordinate(caseId, stateKind, stateIndex)`.
- [x] Consume every shared positive and negative fixture under `FrameWeb/tests/data/contracts/` from C# tests; do not copy fixtures into a divergent C# directory.
- [x] Implement `ResultPresentationService` for static-only DEFINE/COMBINE/PICKUP and moving-load paging/envelopes without mutating base results.
- [x] Define `IAnalysisClient`, `IPrintExporter`, `IProjectStore`, and cancellation/error contracts without coupling Core to HTTP, WinForms, OpenGL, or PdfSharpCore.

**Verification result**: project JSON round-trips byte-stably with canonical entity ordering, strict unknown/duplicate/UTF-8/version/null checks, non-finite/reference rejection, transient selection/dirty exclusion, and same-directory atomic replace. C# reads all six shared positive fixtures and rejects all seven shared negative fixtures in place; Python contract tests pass 14/14. Core tests pass 75/75, both solution test runs pass 95/95, and both Release solution builds pass. `ResultCoordinate` ordering, semantic frame/station/segment/modal validation, failed-candidate state preservation, derived-result immutability, moving-load paging/envelope provenance and source-order rejection, cancellation-aware interfaces, and the dependency-free Core boundary are covered.

#### Step 3: Build the desktop shell and docking lifecycle

- [x] Implement `MainForm` with menu/commands, left navigation, central document viewport, right editor/tool panes, bottom diagnostics/progress pane, and optional floating panes.
- [x] Implement `DockContentRegistry<DocumentKey, Func<DockContent>>`; opening the same keyed tool reuses/activates it, while entity documents may coexist by key.
- [x] Implement versioned layout save/restore for keys, dock state, pane bounds/order, and active document using a whitelist registry.
- [x] Implement command state, dirty-document close confirmation, exception boundary, cancellation, and a pure activation reducer whose side effects are coalesced and cancellable.
- [x] Move all user-visible strings to `Strings.resx`, `Strings.ja.resx`, `Strings.en.resx`, and `Strings.zh.resx`; do not carry hardcoded Angular labels forward.

**Verification result**: STA tests cover create-once/reuse, multi-document identity, actual tool-hide/document-dispose close behavior, invalid factory fail-fast, bounded JSON/layout persistence, transactional rollback, live order/floating bounds, unknown-version/key rejection, active/null-document restore, creating-thread enforcement, language switching, serialized new/open/save/close races, expected versus unexpected cancellation, and bounded non-cooperative analysis/dirty-save/layout-save shutdown. UiTests pass 74/74; both solution test runs pass 168/168 (Core 75, composition/Printing 9, Rendering 10, UI 74); both Release solution builds and targeted `dotnet format` pass. Coverage percentage is not measured. LayoutState v1 intentionally does not persist docked pane proportions or auto-hide state.

#### Step 4: Deliver the first end-to-end vertical MVP

- [x] Implement new/open/save/save-as and at least one representative preset using the new document schema.
- [x] Implement the minimum node/member/support/load-case editors required by the representative preset, with validation and undo/redo.
- [x] Implement the first viewport slice: Z-up nodes, members, supports, loads, selection highlight, orthographic/perspective toggle, fit/home, resize, and table/viewport selection sync.
- [x] Implement `FrameWebAnalysisClient` using the current documented JSON request path and strict `AnalysisResultSet` validation; use cancellation and user-safe diagnostic mapping.
- [x] Extract or wrap the Python-only lifecycle from `FrameWeb.Startup` into `FrameWeb.LocalRuntime`, retaining readiness, timeout, stdout/stderr capture, Job Object cleanup, and parent-exit cleanup; do not start Angular for the desktop path.
- [x] Implement basic static displacement/reaction/member-force tables and one result scene layer.
- [x] Implement one typed PDF job with model summary, one result table, and a deterministic viewport capture.

**Verification result**: both Release solutions build with 0 warnings/errors and both solution test runs pass 253/253 (Core 112, composition/Printing 17, Rendering 27, LocalRuntime 14, UI 83). The real `uv --locked` Flask process is started through the production runtime, the representative preset is serialized, calculated through `FrameWebAnalysisClient`, validated, inspected, and shut down with no owned child process left. Targeted Python transport/runtime tests pass 165/165. RendererProbe passes 20 contexts, 200 frames, and 60 captures with all live counters zero; repeated and concurrent independent WGL review also passes. Typed PDF structural/text/page assertions and the independently rasterized 595x842 Gray8 golden pass, including fail-closed mutation tests. Cancellation/backend failures retain prior state. Coverage percentage is not measured; the known Python/Angular full baseline was not rerun.

#### Step 5: Complete all model input editors

- [ ] Add typed editors and validation for nodes, members, rigid zones, supports, elements/materials/sections, panels, joints, notice points, member springs, load cases, load values, DEFINE, COMBINE, and PICKUP.
- [ ] Add keyboard navigation, multi-row edit, copy/paste, insert/delete, selection synchronization, and undo/redo as shared grid behaviors rather than per-screen copies.
- [ ] Add all four built-in presets as C# resources or typed fixture builders and validate them on load.
- [ ] Enforce cross-table references, dimensional requirements, duplicate IDs, case limits, and calculation preconditions in Core before HTTP submission.

**Verification**: each input module has focused ViewModel/domain tests for create/edit/delete/paste/invalid references; every preset opens without warning, round-trips, and produces the expected request; the full editor matrix is navigable from the shell without orphaned dock panes.

#### Step 6: Complete rendering and interaction parity

- [ ] Implement independent node, member, rigid-zone, support, spring, joint, panel, notice-point, load, displacement, reaction, and section-force scene layers.
- [ ] Implement 2D orthographic and 3D perspective camera policies, grid/axis/labels, scale and color legends, hit testing, hover/selection policy, case/state paging, max/min selection, and PNG capture.
- [ ] Keep the rendering engine behind `IViewportScene`, `ICameraController`, `IHitTestService`, and stable domain IDs; do not reproduce the Angular service graph.
- [ ] Invalidate only affected layers after document/result changes and coalesce rapid edit/activation events.

**Verification**: layer-level scene snapshots or command-buffer assertions cover every input/result mode; real-context smoke tests draw at least one frame for 2D and 3D; selection is bidirectionally stable; resize/float/dock/close/reopen tests show no duplicate events or growing GPU resource counters; representative large presets remain responsive under a recorded performance baseline.

#### Step 7: Complete calculation and result presentation

- [ ] Support ordered multi-case static results, every accepted nonlinear load step, and modal modes directly from `AnalysisResultSet`.
- [ ] Implement displacement, support-reaction, and member-section-force tables and diagrams for basic results, with explicit case/state selectors.
- [ ] Implement DEFINE/COMBINE/PICKUP only for static operands and show a visible domain error for nonlinear/modal attempts.
- [ ] Implement moving-load parent/child paging, component max/min envelopes, reaction absolute maximum, member-force extrema, CSV/PICKUP export, and deterministic ordering.
- [ ] Retain shell/solid data in the validated result model; dedicated shell/solid screens are outside initial parity because the current Angular app has no such result routes.

**Verification**: shared and Angular-derived fixtures produce identical order, coordinates, extrema, pages, and static-only rejection; Ct first/last cases and all accepted nonlinear steps are accessible; malformed or partial responses never enable result commands; no legacy `disg` / `reac` / `fsec` adapter exists in active C# code.

#### Step 8: Replace the legacy print path with typed printing

- [ ] Characterize the useful existing PDF behavior first: A3/A4, portrait/landscape, page numbers, Japanese/Chinese fonts, table pagination, diagram layouts, and representative existing fixtures.
- [ ] Replace `PrintInput` / `PrintData` dictionaries with typed `PrintJob`, page-section, table, diagram, result, and viewport-capture models.
- [ ] Replace the vulnerable legacy PdfSharpCore/ImageSharp graph, initialize the selected font resolver once, make export concurrency explicit, preserve original exception types/causes, and enforce encoded/decompressed/image-dimension/page/work limits before allocation.
- [ ] Implement preview, page count, input tables, displacement/reaction/section-force tables, load/model/result diagrams, scale/layout options, and file export from the desktop app.
- [ ] Decide whether `FramePrintAzure` is removed or rebuilt against `PDF_Manager.Printing`; do not let the desktop executable become an Azure dependency.

**Verification**: pure layout tests cover pagination and page geometry; generated PDFs pass parse/text/page assertions; representative rendered pages match approved goldens; repeated/parallel export follows the chosen concurrency policy; oversized/compression-amplified/image-bomb inputs fail before expensive work; shippable project graphs have no known High/Critical package advisory; no old base64/comma-byte print endpoint is required by the desktop app.

#### Step 9: Production integration, parity sign-off, and cutover

- [ ] Complete production authentication after selecting one provider; keep tokens out of files/logs and inject authorization only at the HTTP boundary. Any retained Azure print endpoint must be non-anonymous, POST-only, bounded by request/decompression/page/image/time/concurrency limits, and cancellation-aware before it may be deployed.
- [ ] Choose installer/update strategy, per-user settings location, crash/diagnostic policy, backend endpoint discovery, offline/local-mode behavior, and signed release packaging.
- [ ] Execute a scenario parity matrix against `FrameWebforJS`: file workflows, every editor, every viewport mode, calculation, first/last case, nonlinear/modal navigation, derived results, print selection, three languages, cancellation, and error recovery.
- [ ] Only after the matrix passes, change `FrameWeb.Startup` and release documentation to make the C# desktop application the supported entrypoint and stop launching/redirecting to Angular.
- [ ] Remove or archive Angular/Electron production packaging and the obsolete manual print harness/API only after their accepted behavior has automated C# coverage.

**Verification**: signed clean-machine install launches without Node/Electron, manages Python lifecycle, completes the representative end-to-end matrix, produces no orphan processes, and passes `dotnet test FrameWeb.sln`, `dotnet build FrameWeb.sln`, relevant Python contract tests, and `& .agents/check.ps1`.

### Risks & Considerations

- **THREE provenance and modernization**: `isasPrint/THREE` records MIT provenance, but shader/font/texture/transitive licenses need separate verification. Gate source import on Step 0; prefer a minimal owned adapter over copying all 289 vendored files.
- **OpenTK generation gap**: sample code targets OpenTK 3.3.3/.NET Framework 4.8. A modern OpenTK port may change GLControl, context, input, shader, and disposal APIs. Prove it with a real-context spike before architecture lock-in.
- **Lifecycle leaks**: the sample's `Application.Idle` loop, repeated Load subscription, non-disposable controls, and incomplete teardown can cause duplicate work and GPU leaks. Render on invalidation and test explicit disposal order.
- **Docking performance and identity**: do not inherit `DockingMdi`, maximized MDI children, menu merge, caption dispatch, or CLR type persistence. Use DockingWindow-style panes and stable `DocumentKey` identities.
- **Scope size**: full FrameWebforJS parity spans many editors, result modes, and integrations. The Step 4 vertical MVP is a mandatory checkpoint; later phases may proceed only with the parity matrix kept current.
- **Contract drift**: C# must consume shared `AnalysisResultSet` schema/fixtures directly. Do not fork DTO semantics or create a UI-specific backend response.
- **Input contract**: this plan creates a new persisted C# document but keeps the Python calculation input meaning. Request serialization needs golden tests against Python before UI breadth expands.
- **Printing model**: current PDF code mixes input, results, screenshots, and presentation in untyped dictionaries. Characterize useful layout behavior before deletion, but do not preserve the old public API.
- **Global PDF state**: current font resolver changes process-global state. Initialize once and define whether export is serialized or proven thread-safe.
- **Startup ownership**: desktop, startup host, Azure print, and Python service currently overlap. Give the desktop path exactly one owner for process lifetime and leave cloud hosting behind explicit interfaces.
- **Canonical gate debt**: the post-Step-1 full repository check in `.agents/logs/check-20260920T035935489Z-32252.log` completed with Python and Angular failures outside the C# diff. Keep those failures separate from task-scoped validation, but do not claim repository-wide green until they are fixed or explicitly baselined.
- **Solution coverage**: Step 0/1のdesktop product、Core、Rendering、typed Printing、LegacyPrinting、probe、4 test projectsは両solutionへ登録済みである。以後はsolution-level build/testを受入証拠にする。
- **Temporary legacy print bridge**: `FramePrintAzure` とlocal print hostの現行挙動を維持するため、旧untyped printing sourceと制限fontは非packableな `PDF_Manager.LegacyPrinting` に隔離した。desktop shellはこのprojectを参照しない。ただし `IsPackable=false` はpublish/copy-localを防がず、Startup出力のbridge DLLには制限fontが埋め込まれる。さらに匿名endpointからHigh advisoryを持つImageSharp 1.0.4と無制限のbase64/gzip/image/PDF処理へ到達できる。Step 8/9でtyped migration、patched dependency、resource limits、authentication、font licensing、golden、Azure継続判断を完了するまで、このhostを公開・配布しない。
- **Review availability**: two bounded nested Codex decomposition attempts were unusable: the first returned only already-resolved questions, and the allowed retry timed out with an unrelated plan mixed into its partial response. This document therefore relies on direct repository evidence and three completed read-only collaborator audits; it must not be reported as nested-Codex PASS.

### Open Questions

- Which production authentication provider should the C# app use: Microsoft Entra/B2C, Keycloak, another provider, or no login for the first release? This does not block Steps 0-8 but blocks production sign-off in Step 9.
- Should `FramePrintAzure` remain as a cloud printing surface, be rebuilt over the typed printing library, or be retired in favor of local desktop PDF export?
- Should CJK PDF output use installed system fonts or one exact redistributable upstream font artifact? The answer must be proven by PDF goldens and a publish-content scan before distribution; the three current embedded font binaries may not ship.
- Should the final distribution use MSIX, a traditional installer, or another signed packaging/update channel?
- After parity sign-off, should `FrameWebforJS` remain as a reference implementation, be archived outside the supported solution, or be deleted?
- Are chat, Help/MyPage links, cloud document storage, and automatic update required for the first production release, or may they follow the engineering-analysis MVP?
