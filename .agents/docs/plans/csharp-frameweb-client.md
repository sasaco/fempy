## Implementation Plan: C# FrameWeb Desktop Client

### Purpose

`FramePrintPDF/PDF_Manager` を中心に、`FrameWebforJS` と同じ主要業務シナリオを提供する Windows 専用の .NET 8 / WinForms デスクトップアプリを新規構築する。Angular、RxJS、Web Worker、Electron、旧印刷JSONを逐語移植せず、型付き `ProjectDocument`、単一の `AnalysisResultSet v1`、ドッキングUI、C# THREE/OpenGL描画、型付きPDF出力へ再設計する。

既存利用者との後方互換は要件に含めない。Python FEM解析はC#へ移植せず、`FrameWeb` を数値解析サービスとして継続利用する。最初に「モデルを開く → 表示・最小編集 → 計算 → 結果表示 → PDF出力」の縦切りMVPを完成させ、その後に入力・結果・印刷のシナリオ同等性を段階的に閉じる。

### Scope

本計画は、C#デスクトップ製品の設計・実装・検証・切替までを対象とし、Python FEMの数値実装と旧クライアント互換は対象外とする。

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
  - `.agents/docs/DESIGN.md` after this plan is approved
- Shared contract inputs:
  - `FrameWeb/tests/data/contracts/analysis-result-set-v1.schema.json`
  - `FrameWeb/tests/data/contracts/positive/*.json`
  - `FrameWeb/tests/data/contracts/negative/*.json`
- Dependencies to resolve in Step 0:
  - .NET 8 Windows Desktop SDK
  - a maintained DockPanelSuite package supporting .NET 8 WinForms
  - a maintained OpenTK/GLControl path and the reusable subset/provenance of `isasPrint/THREE`
  - PdfSharpCore or a compatible maintained PDF library selected after current PDF golden characterization
  - Microsoft.NET.Test.Sdk plus one repository-standard .NET test framework
- Explicitly excluded dependencies: ComponentOne and machine-absolute binary references from `isasPrint`.

### Implementation Steps

以下を依存順に実施し、各ステップの検証を通過してから次の広い機能面へ進む。

#### Step 0: Freeze the greenfield contract and clear feasibility blockers

- [ ] Record in `.agents/docs/DESIGN.md` that the C# app replaces the frontend behavior only, retains Python FEM, consumes only `AnalysisResultSet v1`, requires no legacy compatibility, and keeps calculation/printing separate.
- [ ] Produce a source/provenance inventory for the `isasPrint/THREE` subset, DockPanelSuite, OpenTK, shaders, fonts, textures, and transitive packages; decide what may be copied, rewritten, or consumed as a package.
- [ ] Build a minimal .NET 8 WinForms probe that opens a DockingWindow-style document, creates a real GL context, draws one deterministic frame, resizes, captures a bitmap, closes, and reopens repeatedly.
- [ ] Define the renderer lifetime contract: UI-thread-only GL calls; idempotent `Initialize`, `SetModel`, `Resize`, `Render`, `Capture`, `Dispose`; one context and subscription set per viewport; explicit disposal order; render-on-invalidation when idle.
- [ ] Define versioned `DocumentKey` and layout DTOs, tool-window hide policy, document-window dispose policy, and whitelist-only restore without `Activator` or CLR type names.

**Verification**: dependency/license checklist has no unresolved redistribution blocker; the .NET 8 probe renders and captures a known scene; 100 close/reopen cycles keep context, subscription, and live-window counts stable; an idle viewport performs no continuous render loop; malformed/unknown layout keys are rejected safely.

#### Step 1: Establish the solution and project boundaries

- [ ] Retarget `FramePrintPDF/PDF_Manager/PDF_Manager.csproj` to a `net8.0-windows` WinExe with WinForms enabled and make it the composition root.
- [ ] Add `PDF_Manager.Core`, `PDF_Manager.Rendering`, `PDF_Manager.Printing`, unit tests, rendering tests, and STA UI tests with the dependency direction described above.
- [ ] Move the useful existing `Printing/**` code into `PDF_Manager.Printing`; do not move `PrintInput`, `PrintData`, legacy dictionaries, or duplicate Function1/Function2 handlers as public design.
- [ ] Add all product/test projects to `FrameWeb.sln` and `FramePrintPDF/FramePrintPDF.sln`; mark or remove `PDF_Test` / `PDF_Test_CLI` after their useful fixtures are represented by automated tests.
- [ ] Prevent excluded files such as `KAJYU_ZU23.cs` from returning through wildcard moves.

**Verification**: `dotnet build FrameWeb.sln` and `dotnet test FrameWeb.sln` pass with a nonzero test count; project-reference checks prove Core has no UI/render/PDF dependencies; the product opens an empty shell on Windows.

#### Step 2: Implement typed document and result foundations

- [ ] Define one new `ProjectDocument` aggregate for model inputs, load definitions, derived-result definitions, metadata, selection, dirty state, and validation; keep runtime `AnalysisResultSet` outside the persisted input document unless a later persisted-result feature is approved.
- [ ] Define the new C# project-file JSON schema and deterministic serializer using `System.Text.Json`; add open/save/save-as, atomic replace, UTF-8, finite-number, ID/reference, and unknown-field policies.
- [ ] Implement immutable C# DTOs and strict validation/indexing for the exact `AnalysisResultSet v1` contract, keyed by `ResultCoordinate(caseId, stateKind, stateIndex)`.
- [ ] Consume every shared positive and negative fixture under `FrameWeb/tests/data/contracts/` from C# tests; do not copy fixtures into a divergent C# directory.
- [ ] Implement `ResultPresentationService` for static-only DEFINE/COMBINE/PICKUP and moving-load paging/envelopes without mutating base results.
- [ ] Define `IAnalysisClient`, `IPrintExporter`, `IProjectStore`, and cancellation/error contracts without coupling Core to HTTP, WinForms, OpenGL, or PdfSharpCore.

**Verification**: new document round-trips byte-stably after canonical formatting; broken references and non-finite values fail before save/calculate; all shared contract positives pass and all negatives fail in both Python and C#; result order and coordinates match fixtures; failed response validation leaves the document's previous result state unchanged.

#### Step 3: Build the desktop shell and docking lifecycle

- [ ] Implement `MainForm` with menu/commands, left navigation, central document viewport, right editor/tool panes, bottom diagnostics/progress pane, and optional floating panes.
- [ ] Implement `DockContentRegistry<DocumentKey, Func<DockContent>>`; opening the same keyed tool reuses/activates it, while entity documents may coexist by key.
- [ ] Implement versioned layout save/restore for keys, dock state, pane bounds/order, and active document using a whitelist registry.
- [ ] Implement command state, dirty-document close confirmation, exception boundary, cancellation, and a pure activation reducer whose side effects are coalesced and cancellable.
- [ ] Move all user-visible strings to `Strings.resx`, `Strings.ja.resx`, `Strings.en.resx`, and `Strings.zh.resx`; do not carry hardcoded Angular labels forward.

**Verification**: STA tests cover create-once/reuse, multi-document identity, hide-vs-dispose, invalid factory fail-fast, layout round-trip, unknown-version rejection, active-document restore, language switching, close confirmation, and no duplicate event subscriptions after repeated open/close.

#### Step 4: Deliver the first end-to-end vertical MVP

- [ ] Implement new/open/save/save-as and at least one representative preset using the new document schema.
- [ ] Implement the minimum node/member/support/load-case editors required by the representative preset, with validation and undo/redo.
- [ ] Implement the first viewport slice: Z-up nodes, members, supports, loads, selection highlight, orthographic/perspective toggle, fit/home, resize, and table/viewport selection sync.
- [ ] Implement `FrameWebAnalysisClient` using the current documented JSON request path and strict `AnalysisResultSet` validation; use cancellation and user-safe diagnostic mapping.
- [ ] Extract or wrap the Python-only lifecycle from `FrameWeb.Startup` into `FrameWeb.LocalRuntime`, retaining readiness, timeout, stdout/stderr capture, Job Object cleanup, and parent-exit cleanup; do not start Angular for the desktop path.
- [ ] Implement basic static displacement/reaction/member-force tables and one result scene layer.
- [ ] Implement one typed PDF job with model summary, one result table, and a deterministic viewport capture.

**Verification**: from a clean checkout, one command starts the desktop app and Python service; a representative model can be opened, edited, saved/reopened, calculated, inspected, and exported to PDF; cancellation and backend failure leave prior results intact; application exit leaves no Python child process; the PDF passes structural/text/page assertions and an approved rendered-page golden.

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
- [ ] Initialize the PdfSharp font resolver once, make export concurrency explicit, preserve original exception types/causes, and validate image/data sizes before allocation.
- [ ] Implement preview, page count, input tables, displacement/reaction/section-force tables, load/model/result diagrams, scale/layout options, and file export from the desktop app.
- [ ] Decide whether `FramePrintAzure` is removed or rebuilt against `PDF_Manager.Printing`; do not let the desktop executable become an Azure dependency.

**Verification**: pure layout tests cover pagination and page geometry; generated PDFs pass parse/text/page assertions; representative rendered pages match approved goldens; repeated/parallel export follows the chosen concurrency policy; no old base64/comma-byte print endpoint is required by the desktop app.

#### Step 9: Production integration, parity sign-off, and cutover

- [ ] Complete production authentication after selecting one provider; keep tokens out of files/logs and inject authorization only at the HTTP boundary.
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
- **Dirty worktree**: `FrameWeb/main.py` already contains user changes. Future implementation must not overwrite it; stabilize/commit that work or assign exclusive ownership before any backend-adjacent edit.
- **Review availability**: two bounded nested Codex decomposition attempts were unusable: the first returned only already-resolved questions, and the allowed retry timed out with an unrelated plan mixed into its partial response. This document therefore relies on direct repository evidence and three completed read-only collaborator audits; it must not be reported as nested-Codex PASS.

### Open Questions

- Which production authentication provider should the C# app use: Microsoft Entra/B2C, Keycloak, another provider, or no login for the first release? This does not block Steps 0-8 but blocks production sign-off in Step 9.
- Should `FramePrintAzure` remain as a cloud printing surface, be rebuilt over the typed printing library, or be retired in favor of local desktop PDF export?
- May the `isasPrint/THREE` source and its assets be copied into this repository, or should only its architecture be referenced while the renderer is reimplemented against maintained packages? Step 0 must resolve this before source import.
- Should the final distribution use MSIX, a traditional installer, or another signed packaging/update channel?
- After parity sign-off, should `FrameWebforJS` remain as a reference implementation, be archived outside the supported solution, or be deleted?
- Are chat, Help/MyPage links, cloud document storage, and automatic update required for the first production release, or may they follow the engineering-analysis MVP?
- User approval is required before implementation. Because nested Codex validation was unavailable, approval should explicitly accept this evidence-based draft or request another validation route.
