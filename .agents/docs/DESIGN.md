# Design Document — 要件定義書 (Requirements & Macro Design)

> **Role:** Macro-level requirements and design — *what* this project builds and *why*.
> Kept current by `/init`, `/design-tracker`, and `/checkpointing`.
>
> **Document map:** Shared rules → [rules/](../rules/) ·
> Shared bootstrap → [AGENTS.md](../../AGENTS.md) · State → [STATE.md](../STATE.md) ·
> Micro work progress (latest 5 checkpoints) → [PROGRESS.md](../../PROGRESS.md)

## 背景・目的 (Background & Purpose)

FrameWeb3は、構造モデルの編集、骨組有限要素解析、結果確認、印刷・PDF出力を一つのリポジトリで提供するWeb構造解析システムである。Python解析サービス、Angularクライアント、.NETのローカル起動・印刷ホストを組み合わせ、各コンポーネントの公開契約を明示的に分離する。

ローカル開発はWindowsを主対象とし、PowerShell、各コンポーネントのmanifest、commit済みlockfileを再現可能な正規経路とする。

## スコープ (Scope)

対象範囲は製品コンポーネントとそれらの明示的な境界であり、生成物や運用secretを含めない。

### In Scope

- `FrameWeb/`のPython骨組FEM解析とFlask/functions-framework HTTP境界。
- `FrameWebforJS/`のAngularブラウザ/Electronモデル編集、計算要求、結果表示。
- `tools/FrameWeb.Startup/`のローカルセットアップ、解析・フロント起動、readiness、印刷HTTPホスト。
- `FramePrintPDF/`のローカル/Azure印刷およびPDF生成。
- `FrameGConverter/`の独立した変換機能と、`FrameWeb.sln`を中心とするVisual Studio開発経路。
- 単一・複数ケース、静的・非線形・モーダル解析の成功出力を共通化する`AnalysisResultSet`の単一契約。

### Out of Scope

- 本文書への本番認証情報、クラウドsecret、デプロイ資格情報の記載。
- versioningまたは明示的な移行なしでの公開解析・印刷契約の変更。
- 生成済み依存関係、build出力、cache、vendor資産を製品コンポーネントとして扱うこと。
- `.agents`整備に伴う製品コード、製品依存関係、API挙動の変更。

## 機能要件 (Functional Requirements)

| ID | Requirement | Priority | Notes |
|----|-------------|----------|-------|
| FR-FRAMEWEB-2 | Let the Angular client edit models, request calculations, and display calculation results and actionable errors. | High | The client validates required result fields before starting result workers. |
| FR-FRAMEWEB-3 | Start the analysis API, Angular development server, and local print host through the .NET startup project. | High | Visual Studio F5 and `dotnet run --project tools/FrameWeb.Startup` are supported local entry points. |
| FR-FRAMEWEB-4 | Produce print/PDF output through the existing .NET printing handlers without conflating their transport contract with the calculation API. | High | Azure deployment continues to use the dedicated printing project. |
| FR-FRAMEWEB-RESULT-SET-2 | Return AnalysisResultSet as the sole successful calculation response for static, nonlinear, modal, single-case, and multi-case analysis. | High | Each AnalysisResult is one case/state snapshot; nonlinear accepted steps are separate results rather than nested step_results. No compatibility or display-specific success schema is retained before release. |

## 非機能要件 (Non-Functional Requirements)

| Category | Requirement | Metric / Target |
|----------|-------------|-----------------|
| Reproducibility | Use the committed Python, npm, and .NET project metadata and locks from their component directories. | Canonical commands run without relying on a root-level Python or npm project. |
| Security | Decode untrusted compressed calculation input without evaluating code and keep local secrets out of tracked files. | No `eval`-style decoder; local environment files remain untracked. |
| Maintainability | Keep analysis, frontend, startup, printing, conversion, and agent-infrastructure responsibilities independently testable. | Component-specific gates report their working directory and failing command. |
| Platform | Keep the supported local workflow executable from Windows PowerShell. | Bootstrap, setup, and verification paths do not require WSL or Bash. |
| Result contract | Expose exactly one validated AnalysisResultSet success schema, with no compatibility or display-specific alternatives; keep calculation input redesign outside this refactor. | Zero alternate success schemas; zero runtime result adapters; unique case/state coordinates; atomic response; deterministic case-major order. |
| Result-set correctness v1 | Preserve deterministic input-derived case order and accepted state order, isolate mutable solver state per case, preserve case-specific support sets, validate every snapshot variant, apply no post-solve display multiplier, and fail atomically. | Maximum 256 cases; zero partial responses; zero duplicate case/state coordinates; exactly one final load-step per nonlinear case; identical geometric public topology across cases. |

## アーキテクチャ (Architecture)

FrameWeb3は次の境界を持つコンポーネント指向モノレポである。

1. `FrameWebforJS/`はブラウザ/Electron操作、モデル編集、計算要求、結果表示を所有する。
2. `FrameWeb/`は入力検証、FEMモデル構築・解析、計算HTTP応答を所有する。
3. `tools/FrameWeb.Startup/`はローカルtoolchain準備、Angular/Python子プロセス、readiness、ローカル印刷を所有する。
4. `FramePrintPDF/`は既存C#印刷/PDF handlerとAzureデプロイ境界を所有する。
5. `FrameGConverter/`はWebローカル起動ライフサイクル外の独立変換utilityである。

### Result Contracts

- 計算入力は既存の検証済みschemaを継続利用し、本リファクタリングでは再設計しない。入力caseの順序を結果順序の基準とする。
- `AnalysisResultSet`: 唯一の成功response root。順序付き`cases`、一度だけ出力するcanonical `topology`、case-major順の`results`を持つ。
- `AnalysisResult`: `(case_id, state.kind, state.index)`で一意になるimmutable snapshot。staticはcaseごとに1件、material nonlinearはaccepted stepごとに1件、modalはmodeごとに1件を出力する。
- legacy入力は`load` mapの全entryを挿入順でcase化し、modern入力は現行どおり単一case `"1"`とする。支持節点はcase固有の`support_node_ids`として保持し、共有topologyはgeometryに限定する。
- legacy caseの解析種別はtop-level、case、model inferenceの順、解析parameterはdefault、case、top-level overrideの順という現行precedenceを維持する。
- `state`は`static`、`load_step`、`mode`のdiscriminated unionとする。非線形のnested `step_results`と最終状態の二重格納は行わず、caseごとに最後のload stepだけをfinalとする。
- canonical result fieldは`node_displacements`、`support_reactions`、`member_section_forces`、`shell_results`、`solid_results`、`diagnostics`とする。modal variantは`node_mode_shapes`を持ち、force-bearing fieldを持たない。
- member/shellのlocal frameとshell/solidのsampling locationをtopologyへ明示し、resultはそのID/orderを完全にcoverする。modal frequencyはHz固定ではなく宣言time unitの逆数とし、zero/degeneracy toleranceを契約で固定する。
- legacyの表示・解析後倍率`rate`は削除し、代替のrequest/result fieldは追加しない。DEFINE/COMBINE/PICKUPの係数は派生結果の概念としてbase resultを変更しない。
- 単位は既存のnormalized `model_metadata.units`をそのまま出力し、省略時は`consistent_user_defined`/`unspecified`とする。単位推定・変換は行わない。

計算成功時のschemaはこの一組だけとし、result representation negotiation、compatibility adapter、display-specific backend contractは設けない。計算と印刷は別のtransport契約であり、frontendはcanonical schemaを一度だけ検証して直接利用する。

- The Windows desktop client is composed as a `net8.0-windows` WinForms shell over UI-independent Core, Rendering, and Printing projects, plus a local Python runtime owner. Core defines typed `ProjectDocument`, `AnalysisResultSet v1` validation/indexing, result presentation, and the `IAnalysisClient`, `IPrintExporter`, and `IProjectStore` boundaries without WinForms, OpenGL, or PDF-library dependencies.

## 技術選定 (Tech Stack & Rationale)

| Area | Technology | Rationale | Alternatives Considered |
|------|------------|-----------|-------------------------|
| Analysis service | Python 3.11+; NumPy, SciPy, Flask, functions-framework; `uv` | FEM実装とHTTP境界に適合し、lockされたcomponent環境を提供する。 | ルート単一Python環境、host processへのsolver統合。 |
| Web client | Angular 15, TypeScript 4.9, npm/Node 18 | 既存のブラウザ/Electron UIと結果表示資産を維持する。 | `.agents`整備と同時のframework置換。 |
| Local orchestration | .NET 8 `FrameWeb.Startup` | Visual Studio/CLIの単一起点、readiness、child-process管理、ローカル印刷を提供する。 | 各serviceの常時手動起動。 |
| Printing | Existing .NET projects in `FramePrintPDF/` | 既存のC#印刷とAzureデプロイ境界を維持する。 | 計算transportの再利用、Pythonへの印刷移行。 |
| Developer shell | Windows PowerShell | setup script、Visual Studio workflow、現行環境と一致する。 | WSL/Bashを必須にする。 |

## 制約 (Constraints)

- Python製品・agent toolingは`uv run --project FrameWeb --locked --extra dev python ...`で実行し、bare `python`が`PATH`にあることを前提にしない。
- ローカルsetupはPython 3.12とNode 18/npm 9を対象とし、各componentが宣言するversion範囲を尊重する。
- 計算APIは成功時に`AnalysisResultSet v1`以外のresult schemaを提供せず、UI専用のbackend representationを追加しない。
- 計算encoderとC#印刷APIのwire契約が同値と証明されるまで共有しない。
- `.venv`、`node_modules`、`dist`、`bin`、`obj`、cache、vendor frontend資産はsource componentではない。
- local environment/authentication fileはmachine固有値を含み得るため、bootstrap automationで上書き・commitしない。
- Load Case Set Analysisは一要求256 casesを上限とし、途中失敗時にpartial result setを返さない。

- The C# desktop migration replaces frontend behavior only: Python remains the FEM implementation, successful calculations use only `AnalysisResultSet v1`, calculation and printing transports stay separate, and legacy client/print compatibility is not required.

## Key Decisions

| Decision | Rationale | Alternatives Considered | Date |
|----------|-----------|------------------------|------|
| Use Codex as the main repository agent and Windows PowerShell as the canonical administration path. | This matches the active runtime and the repository's supported local development environment. | Preserve copied runtime-first and Bash-first bootstrap assumptions. | 2026-09-18 |
| Keep Python, Angular, startup, printing, and conversion as explicit component boundaries in one monorepo. | Each component has a different toolchain and public contract; explicit boundaries make setup and validation reproducible. | Treat the root as one Python project or collapse services into the startup host. | 2026-09-18 |
| Keep the repository agent infrastructure Codex-focused and remove copied Claude pseudo-links, runtime-specific agents and hooks, and the inactive Antigravity workflow. | Only Codex is an active repository runtime. Removing unreachable integration surfaces prevents stale instructions and duplicate execution paths while retaining runtime-neutral skills and rules. | Maintain parallel Claude and Codex bootstrap surfaces; retain inactive integrations as examples. | 2026-09-18 |
| Default Codex to the read-only sandbox and require an explicit workspace-write opt-in for repository mutations. | Least-privilege defaults make read-only analysis safe while keeping authorized implementation work available through an explicit invocation choice. | Use workspace-write or danger-full-access as the repository default. | 2026-09-18 |
| Use AnalysisResultSet as the only public calculation root and remove FrameResultSet, legacy-cases-v1, the default flat AnalysisResult response, and all compatibility adapters before release. | A single ordered snapshot collection eliminates representation negotiation, duplicate result models, UI-specific backend fields, nonlinear final-state duplication, and compatibility maintenance. Each result is identified by case_id plus a discriminated state; shared topology is emitted once, and domain member-force aggregation becomes canonical postprocessing. | Keep separate AnalysisResult, AnalysisResultSet, and FrameResultSet wire contracts; retain legacy-cases-v1; nest nonlinear step_results inside a case-level final result. | 2026-09-18 |
| Limit the AnalysisResultSet refactor to the calculation success output; keep existing validated input schemas unchanged and remove the legacy rate display multiplier without introducing load_scale. | The objective is to establish one canonical result root. Redesigning the complete input contract adds unrelated migration risk and schema maintenance, while rate is post-solve display behavior that does not belong in the canonical analysis result. | Introduce AnalysisRequest v1 and rename rate to load_scale; keep post-solve rate behavior. | 2026-09-18 |
| Enumerate existing legacy load-map entries as ordered result cases, keep modern input single-case as case 1, store support_node_ids per ResultCase, and reproduce normalized existing unit metadata without inference. | This makes output construction deterministic without redesigning input, permits cases to select different support definitions while sharing geometric topology, and remains truthful when current inputs omit unit declarations. | Invent a new multi-case input; require identical supports across cases; put supports in shared topology; assume fixed engineering units. | 2026-09-18 |
| Build the Windows desktop client as a .NET 8 WinForms frontend replacement while retaining FrameWeb as the Python FEM service and accepting only AnalysisResultSet v1 as a successful calculation response. | This preserves the validated numerical-analysis boundary, keeps one canonical result contract, and lets the desktop migration focus on typed document, presentation, rendering, docking, and PDF workflows. | Port the FEM engine to C#; retain legacy disg/reac/fsec compatibility; support multiple successful result schemas. | 2026-09-20 |
| Keep calculation and printing as separate typed boundaries in the C# desktop architecture and require no legacy client or print-transport compatibility. | The calculation result contract and PDF composition have different responsibilities and lifecycles; separating them prevents the current untyped print dictionaries from becoming a new application contract. | Reuse the legacy print JSON/dictionaries as the desktop domain model or calculation success schema. | 2026-09-20 |
| Isolate the existing Azure/local print handlers in a non-packable PDF_Manager.LegacyPrinting bridge while the new desktop shell depends only on Core, Rendering, and typed Printing projects. | Retargeting PDF_Manager to a net8.0-windows WinExe otherwise makes FramePrintAzure(net8.0) depend on the desktop executable and fail NU1201. The bridge preserves the existing handlers without reintroducing PrintInput, PrintData, legacy dictionaries, or restricted fonts into the new desktop product boundary. | Remove FramePrintAzure and the local print host before parity; retarget Azure/Startup to Windows and reference the desktop executable; expose the legacy dictionary API from the new typed Printing project. | 2026-09-20 |
| Do not publish or redistribute the FramePrintAzure/local legacy print host until its anonymous endpoint, vulnerable PDF/image dependency graph, unbounded request/decompression/image/PDF work, and restricted embedded fonts are replaced or removed. | The Step 1 security review confirmed that the new desktop boundary is isolated, but the retained legacy host still exposes unauthenticated input to ImageSharp 1.0.4 High advisories, has no body/expansion/page/image/time/concurrency limits, and copies restricted fonts inside PDF_Manager.LegacyPrinting.dll into Startup output. IsPackable=false does not make that host safe to publish. | Publish the legacy host with documentation-only warnings; upgrade only ImageSharp while leaving anonymous unbounded work; remove FramePrintAzure immediately before replacement behavior is characterized. | 2026-09-20 |

## TODO / Open Questions

- Complete end-to-end browser verification of the sole `AnalysisResultSet` path, including first/last static cases and all accepted nonlinear steps.
- Keep production authentication and deployment configuration separate from the anonymous local-development calculation path.

- Replace the temporary `PDF_Manager.LegacyPrinting` bridge during Step 8 after PDF characterization, typed printing migration, CJK font licensing, golden rendering, and Azure-retention decisions are complete.

- Before any legacy print host deployment, require non-anonymous POST-only access, bounded body/decompression/page/image/time/concurrency work, cancellation, a patched PDF/image dependency graph, and removal or approved replacement of embedded CJK fonts.
