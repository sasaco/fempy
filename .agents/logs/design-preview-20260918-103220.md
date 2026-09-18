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
- 単一荷重ケースと複数荷重ケースの解析結果、およびFrameWebforJS向け表示投影の明示的な契約。

### Out of Scope

- 本文書への本番認証情報、クラウドsecret、デプロイ資格情報の記載。
- versioningまたは明示的な移行なしでの公開解析・印刷契約の変更。
- 生成済み依存関係、build出力、cache、vendor資産を製品コンポーネントとして扱うこと。
- `.agents`整備に伴う製品コード、製品依存関係、API挙動の変更。

## 機能要件 (Functional Requirements)

| ID | Requirement | Priority | Notes |
|----|-------------|----------|-------|
| FR-FRAMEWEB-1 | Accept supported structural-model input, run frame analysis, and return displacement, reaction, and element-result data. | High | The default modern response remains the documented single-case representation. |
| FR-FRAMEWEB-2 | Let the Angular client edit models, request calculations, and display calculation results and actionable errors. | High | The client validates required result fields before starting result workers. |
| FR-FRAMEWEB-3 | Start the analysis API, Angular development server, and local print host through the .NET startup project. | High | Visual Studio F5 and `dotnet run --project tools/FrameWeb.Startup` are supported local entry points. |
| FR-FRAMEWEB-4 | Produce print/PDF output through the existing .NET printing handlers without conflating their transport contract with the calculation API. | High | Azure deployment continues to use the dedicated printing project. |
| FR-FRAMEWEB-LEGACY-CASES-1 | FrameWebforJS can explicitly request every legacy load case as a case map containing displacement, reaction, and member-force results without changing the default FrameWeb flat response. | High | The compatibility representation is legacy-cases-v1 and the browser must reject incompatible or empty result schemas before starting result workers. |
| FR-FRAMEWEB-RESULT-SET-1 | Provide Load Case Set Analysis as a first-class operation that produces an ordered AnalysisResultSet containing one canonical AnalysisResult for every requested load case. | High | The existing single-case response remains AnalysisResult. Case identifiers and input order are preserved, and each case is solved with an isolated model instance. |
| FR-FRAMEWEB-FRAME-RESULT-SET-1 | Provide FrameResultSet as an explicit presentation projection of AnalysisResultSet for FrameWebforJS consumers. | High | FrameResultSet contains the display-oriented disg/reac/fsec/shell_fsec/size fields. It is not the canonical solver result and must not be described as the old result format. |

## 非機能要件 (Non-Functional Requirements)

| Category | Requirement | Metric / Target |
|----------|-------------|-----------------|
| Compatibility | Preserve the default AnalysisResult response and select collection or presentation representations explicitly. | Existing unversioned contract tests remain green; incompatible schemas fail visibly in the client. |
| Result-set correctness | Multi-case execution preserves input order, isolates solver state per case, applies each case rate exactly once, validates every result schema, and fails atomically without returning a partial set. | Maximum 256 cases per request; zero partial-success responses; exact case-ID/order match between request and response. |
| Reproducibility | Use the committed Python, npm, and .NET project metadata and locks from their component directories. | Canonical commands run without relying on a root-level Python or npm project. |
| Security | Decode untrusted compressed calculation input without evaluating code and keep local secrets out of tracked files. | No `eval`-style decoder; local environment files remain untracked. |
| Maintainability | Keep analysis, frontend, startup, printing, conversion, and agent-infrastructure responsibilities independently testable. | Component-specific gates report their working directory and failing command. |
| Platform | Keep the supported local workflow executable from Windows PowerShell. | Bootstrap, setup, and verification paths do not require WSL or Bash. |

## アーキテクチャ (Architecture)

FrameWeb3は次の境界を持つコンポーネント指向モノレポである。

1. `FrameWebforJS/`はブラウザ/Electron操作、モデル編集、計算要求、結果表示を所有する。
2. `FrameWeb/`は入力検証、FEMモデル構築・解析、計算HTTP応答を所有する。
3. `tools/FrameWeb.Startup/`はローカルtoolchain準備、Angular/Python子プロセス、readiness、ローカル印刷を所有する。
4. `FramePrintPDF/`は既存C#印刷/PDF handlerとAzureデプロイ境界を所有する。
5. `FrameGConverter/`はWebローカル起動ライフサイクル外の独立変換utilityである。

### Result Contracts

- `AnalysisResult`: 一つの荷重ケースに対するcanonical solver result。既定の単一ケース応答はこの契約を維持する。
- `AnalysisResultSet`: 要求順とcase IDを保持した`AnalysisResult`の順序付きcollection。`application/vnd.frameweb.analysis-result-set-v1+json`で明示的に選択する。
- `FrameResultSet`: `AnalysisResultSet`をFrameWebforJSの表示用fieldへ投影したrepresentation。`application/vnd.frameweb.frame-result-set-v1+json`で明示的に選択する。
- `application/vnd.frameweb.legacy-cases-v1+json`は移行期間だけのdeprecated compatibility aliasとし、canonical名称には使用しない。

計算と印刷は別のtransport契約である。representation adapterは明示的なservice境界に置き、frontend workerは必須field欠落を空成功へ変換しない。

Clarification (2026-09-18): `FrameResultSet`は`AnalysisResultSet` wire payloadの直接変換ではなく、同じephemeral per-case `CaseSolution`から生成する兄弟representationである。`CaseSolution`はcase ID、canonical `AnalysisResult`、solved model、projection metadata、compatibility rateを一case分だけ保持するnon-wire境界であり、全caseの`FemModel`をmaterializeしない。load casesは外側のcollection axis、非線形`step_results`と`convergence_history`は各caseの`AnalysisResult`内に保持し、Frame representationはtop-levelの最終受理状態だけを投影する。新しいresult-set envelopeはordered `cases` arrayを使用し、deprecated aliasだけが既存bare case mapを維持する。

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
- FrameWebforJS向け投影のために既定の`AnalysisResult`契約を暗黙変更しない。collectionとpresentation projectionは明示的かつversionedに選択する。
- 計算encoderとC#印刷APIのwire契約が同値と証明されるまで共有しない。
- `.venv`、`node_modules`、`dist`、`bin`、`obj`、cache、vendor frontend資産はsource componentではない。
- local environment/authentication fileはmachine固有値を含み得るため、bootstrap automationで上書き・commitしない。
- Load Case Set Analysisは一要求256 casesを上限とし、途中失敗時にpartial result setを返さない。

## Key Decisions

| Decision | Rationale | Alternatives Considered | Date |
|----------|-----------|------------------------|------|
| Use Codex as the main repository agent and Windows PowerShell as the canonical administration path. | This matches the active runtime and the repository's supported local development environment. | Preserve copied runtime-first and Bash-first bootstrap assumptions. | 2026-09-18 |
| Keep Python, Angular, startup, printing, and conversion as explicit component boundaries in one monorepo. | Each component has a different toolchain and public contract; explicit boundaries make setup and validation reproducible. | Treat the root as one Python project or collapse services into the startup host. | 2026-09-18 |
| Expose FrameWebforJS result compatibility through the explicit Accept media type application/vnd.frameweb.legacy-cases-v1+json while preserving the unselected flat API. | Accept is already allowed by CORS, keeps transport encoding independent from result representation, and avoids changing deployment routing. The compatibility path solves each input load case with a fresh FemModel and projects the legacy case schema atomically. | Replace the default response with the old case map; infer the response from compressed transport; adapt only in TypeScript; add a custom header or separate endpoint. General shell compatibility remains separate until an old-backend oracle is available. | 2026-09-18 |
| Replace old/new result-format terminology with AnalysisResult, AnalysisResultSet, and FrameResultSet, and name the capability Load Case Set Analysis. | The formats differ primarily by cardinality and representation, not by chronology. AnalysisResultSet is the ordered collection of canonical single-case results; FrameResultSet is a separate display projection that regroups members and maps field names and signs for FrameWebforJS. | Continue using legacy/new or flat/cases terminology; treat the display projection as merely an array of flat responses. | 2026-09-18 |
| Use application/vnd.frameweb.analysis-result-set-v1+json for the canonical multi-case representation and application/vnd.frameweb.frame-result-set-v1+json for the FrameWebforJS projection; keep application/vnd.frameweb.legacy-cases-v1+json only as a deprecated compatibility alias during migration. | Separate media types make collection semantics and presentation projection explicit while preserving the default single-case API and existing deployed clients. | Rename the existing payload in place; replace the default application/json response; keep legacy in the permanent public name. | 2026-09-18 |
| Generate AnalysisResultSet and FrameResultSet as sibling wire representations from one ephemeral per-case CaseSolution; do not implement FrameResultSet as a wire-to-wire conversion of AnalysisResultSet and do not retain a materialized collection of solved FemModel instances. | Frame projection needs solved-model and source metadata that is not contained in the canonical AnalysisResult wire payload. Processing one case at a time preserves this context while bounding memory to one solved model plus the requested response payload. Load cases remain the outer collection axis, while nonlinear step_results remain inside each case-level AnalysisResult. | Convert the AnalysisResultSet JSON directly; retain every solved FemModel in a CaseSolutionSet; flatten nonlinear steps into load cases. | 2026-09-18 |
| Keep the repository agent infrastructure Codex-focused and remove copied Claude pseudo-links, runtime-specific agents and hooks, and the inactive Antigravity workflow. | Only Codex is an active repository runtime. Removing unreachable integration surfaces prevents stale instructions and duplicate execution paths while retaining runtime-neutral skills and rules. | Maintain parallel Claude and Codex bootstrap surfaces; retain inactive integrations as examples. | 2026-09-18 |
| Default Codex to the read-only sandbox and require an explicit workspace-write opt-in for repository mutations. | Least-privilege defaults make read-only analysis safe while keeping authorized implementation work available through an explicit invocation choice. | Use workspace-write or danger-full-access as the repository default. | 2026-09-18 |

## TODO / Open Questions

- Complete end-to-end browser verification of the versioned result-set path, including the first and last requested load cases.
- Establish an old-backend oracle before claiming general shell-result compatibility.
- Keep production authentication and deployment configuration separate from the anonymous local-development calculation path.
