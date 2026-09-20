# Security Review: C# FrameWeb Desktop (Steps 1-4)

## Integrated Verdict

**The final post-remediation review clears Step 4 with Low follow-ups; public
redistribution remains NO-GO.** Open findings across the reviewed work are
Critical: 0; High: 2; Medium: 2; Low: 6. The Step 4 delta contributes Critical:
0; High: 0; Medium: 0; Low: 3. Three earlier desktop Low findings also remain
open. The two High findings and one Medium remain inherited from the unchanged
Azure/local legacy print host. A second inherited Medium belongs to the
unchanged Angular/local-development Startup analysis host. The historical
Step 4 verdict below is preserved as review history and is superseded by the
final post-remediation section at the end of this report.

The standalone desktop graph remains isolated from `PDF_Manager.LegacyPrinting`.
That isolation permits continued local development of the desktop package; it
does not authorize publishing or redistributing the complete application or
legacy host graph.

## Step 1 Verdict

**Changes requested.** Critical: 0; High: 2; Medium: 1; Low: 2.

The standalone `PDF_Manager` desktop executable has a clean Step 1 dependency
and resource boundary: it references only Core, Rendering, and typed Printing,
its evaluated build items contain no legacy printing source or restricted font,
and NuGet's current advisory check reports no vulnerable package in that
project. The review cannot approve the combined Step 1 tree, however, because
the new active `PDF_Manager.LegacyPrinting` bridge deliberately preserves two
High-risk anonymous print-host conditions: a known-vulnerable image decoder and
unbounded request/decompression work. The bridge also embeds all three blocked
font binaries in an assembly that is copied into the active startup output.

The two High findings are preserved legacy risk rather than new renderer logic,
but the Step 1 diff makes that risk an explicit active project boundary and
keeps it in both solutions. It therefore must not be described as
security-cleared for Azure/local-host use. The existing **complete application
redistribution NO-GO** remains correct.

## Findings

### [High] The active anonymous print path decodes attacker-controlled images with a package that has multiple known vulnerabilities

- **Evidence**: `FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj:41-44`
  keeps `PdfSharpCore` 1.3.9. Its resolved graph contains
  `SixLabors.ImageSharp` 1.0.4. The command
  `dotnet list FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj package --vulnerable --include-transitive --no-restore`
  reports seven advisories, including three High advisories:
  [`GHSA-65x7-c272-7g7r`](https://github.com/advisories/GHSA-65x7-c272-7g7r),
  [`GHSA-63p8-c4ww-9cg7`](https://github.com/advisories/GHSA-63p8-c4ww-9cg7),
  and [`GHSA-2cmq-823j-5qj8`](https://github.com/advisories/GHSA-2cmq-823j-5qj8).
  The reviewed GitHub advisories describe crafted-image
  use-after-free/information-disclosure and out-of-bounds-write/denial-of-service
  conditions in affected ImageSharp versions.
- **Reachability**: both functions are anonymous HTTP triggers at
  `FramePrintPDF/FramePrintAzure/Function1.cs:19-20` and
  `FramePrintPDF/FramePrintAzure/Function2.cs:19-20`. The untrusted JSON reaches
  `Convert.FromBase64String` and `XImage.FromStream` at
  `FramePrintPDF/PDF_Manager/Printing/Diagram3D/PrintBase3dDiagram.cs:58-61`.
  The code removes a PNG data-URL prefix when present but does not enforce the
  declared type; raw base64 for another format can still reach decoder
  auto-detection.
- **Impact**: an unauthenticated network caller can feed crafted image bytes to
  a dependency with current High advisories. Depending on the image, this can
  crash or exhaust the print worker and may expose process memory through an
  affected decoder. Because `FrameWeb.Startup` also references
  `FramePrintAzure`, this graph is present in the local host as well as the Azure
  project.
- **Recommended fix**: do not deploy or expose the legacy bridge as an
  untrusted network service in this state. Replace the blocked PdfSharpCore
  graph with the characterized maintained PDF library, and add an explicit
  image boundary that accepts only an approved format, checks encoded byte
  length and decoded dimensions before allocation, and decodes with a patched
  dependency. Keep a `dotnet list ... --vulnerable --include-transitive` gate
  for every shippable project. If immediate replacement is impossible, disable
  external image input and keep the endpoint non-public until the migration is
  complete.

### [High] Anonymous print requests have no body, expansion, collection, or work limits

- **Evidence**: `FramePrintPDF/FramePrintAzure/Function1.cs:34-49,60-74` and
  `FramePrintPDF/FramePrintAzure/Function2.cs:34-49,60-74` read the complete body
  into a string, allocate a base64-decoded buffer, split it into an unbounded
  string array, allocate another byte array, and copy gzip output into an
  unbounded `MemoryStream`. The triggers accept anonymous GET and POST at line
  20. No code in these paths limits compressed bytes, decompressed bytes,
  JSON/object counts, embedded image bytes/dimensions, pages, cancellation, or
  elapsed work.
- **Impact**: a remote unauthenticated caller can amplify a small compressed
  request into large managed allocations and expensive PDF/image work. This is
  a straightforward memory/CPU denial-of-service path independent of the
  package vulnerabilities above. Duplicating the same path in both functions
  doubles the maintenance surface.
- **Recommended fix**: require the intended authentication/authorization mode
  (and POST only), enforce server and application body limits before
  `ReadToEndAsync`, stream base64/gzip processing through a counting limiter,
  cap decompressed bytes and parsed collection/page/image counts, propagate a
  cancellation token with a deadline, and apply rate/concurrency limits. Share
  one validated decoder implementation between the two functions and return a
  bounded client-safe error when a limit is exceeded.

### [Medium] `IsPackable=false` does not stop restricted fonts from entering active host output

- **Evidence**: `FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj:9,26-39`
  marks the bridge non-packable but embeds `MS Gothic.ttf`, `MS Mincho.ttf`, and
  `simsun.ttf` as manifest resources. `FramePrintPDF/FramePrintAzure/FramePrintAzure.csproj:17-19`
  references that assembly, and `tools/FrameWeb.Startup/FrameWeb.Startup.csproj:8-10`
  references FramePrintAzure. Inspection of the built bridge returns all three
  `PDF_Manager.fonts.*` manifest resources, and the Release startup output
  contains `PDF_Manager.LegacyPrinting.dll` (about 24 MB) plus PdfSharpCore and
  ImageSharp. In contrast, evaluated `PDF_Manager` items and its Release output
  contain none of these assets.
- **Impact**: the direct desktop shell is isolated, but any installer/publish
  assembled from the active root startup/local-host output can redistribute the
  blocked font binaries inside the DLL. `IsPackable=false` affects NuGet pack;
  it is not a publish or copy-local control. The documented NO-GO is therefore
  a policy warning, not a fail-closed technical boundary.
- **Recommended fix**: remove the font resources from every distributable
  assembly and use installed fonts or an exact approved redistributable font.
  Until then, exclude the legacy host graph from desktop packaging and add a
  publish-artifact test that inspects assemblies/resources, not only filenames,
  and fails if any blocked font digest or legacy bridge is present.

### [Low] Rendering accepts unbounded scene and capture sizes before any future untrusted-data adapter exists

- **Evidence**: `FramePrintPDF/PDF_Manager.Rendering/RenderSceneModel.cs:9-16,31-47`
  validates shape and finiteness but has no vertex/byte ceiling. The lifecycle
  copies the whole scene again and uploads it at
  `FramePrintPDF/PDF_Manager.Rendering/OpenGlViewportLifecycle.cs:322-329`.
  Capture allocates `width * height * 4` bytes at lines 196-201; arithmetic is
  checked, but there is no maximum dimension or byte budget.
- **Impact**: Step 1 currently uses only an owned fixed triangle, so there is no
  present remote exploit path. Once model data or capture dimensions are driven
  from opened documents, results, automation, or layout state, very large valid
  inputs could exhaust managed or GPU memory.
- **Recommended fix**: define explicit maximum vertices/scene bytes and maximum
  capture dimensions/bytes at the Rendering public boundary before Step 4,
  reject over-budget inputs before copying/uploading, and add boundary tests.

### [Low] The product entry point does not yet install a fail-closed UI exception boundary

- **Evidence**: `FramePrintPDF/PDF_Manager/Program.cs:7-12` initializes and runs
  WinForms without setting an unhandled-exception policy or application-level
  handler. Renderer paint callbacks invoke GL work directly at
  `FramePrintPDF/PDF_Manager.Rendering/OpenGlViewportLifecycle.cs:287-289`.
  The probe correctly opts into `UnhandledExceptionMode.ThrowException` only in
  `--verify` mode at `FramePrintPDF/PDF_Manager.RendererProbe/Program.cs:12-16`.
- **Impact**: the current empty shell does not host the renderer, so this is not
  an exploitable Step 1 failure. When rendering is wired into the product, a
  WinForms thread exception can otherwise surface a Continue dialog and allow
  execution with partially failed GPU/UI state, as the probe teardown incident
  demonstrated.
- **Recommended fix**: before the renderer enters the product shell, install a
  top-level exception policy that records bounded diagnostics, shows a
  user-safe non-sensitive error, disables/disposes the failed viewport, and
  exits when state cannot be proven consistent. Add an STA test proving a paint
  failure cannot be continued silently.

## Positive Controls Confirmed

- `FramePrintPDF/PDF_Manager/PDF_Manager.csproj:12-17` removes legacy printing
  source, restricted fonts, and related files from Compile, EmbeddedResource,
  Content, and None items. An evaluated MSBuild item query confirmed the product
  compiles only `Program.cs` and `Shell/MainForm.cs`, with no legacy resources.
- The standalone desktop references only Core, Rendering, and typed Printing at
  `FramePrintPDF/PDF_Manager/PDF_Manager.csproj:24-28`; it does not reference
  LegacyPrinting, FramePrintAzure, PdfSharpCore, or Newtonsoft.Json.
- `dotnet list FramePrintPDF/PDF_Manager/PDF_Manager.csproj package --vulnerable --include-transitive --no-restore`
  reports no package with a current advisory for the standalone desktop graph.
- The owned renderer uses constant project-owned shader text, validates all
  coordinates as finite, enforces creating-thread access for GL operations,
  uses checked capture-size arithmetic, unsubscribes events, and releases GPU
  resources before disposing the GLControl.
- The verification executable now converts UI-thread failures into a nonzero
  process result in `--verify` mode instead of presenting a modal Continue
  dialog.
- Typed `PrintPageLayout` rejects non-finite, negative, and non-printable page
  geometry before use. The new typed Printing project has no external package
  or project dependency.
- `KAJYU_ZU23.cs` is explicitly excluded from the legacy bridge. No reviewed
  change introduces credentials, filesystem writes, dynamic assembly loading,
  external shader loading, or shell-command execution.

## Verification Performed

- Reviewed `.agents/logs/review-diff-csharp-frameweb-client.patch`, the changed
  product projects/source/tests, the implementation plan, design decision, and
  dependency/provenance inventory.
- Evaluated product items with
  `dotnet msbuild FramePrintPDF/PDF_Manager/PDF_Manager.csproj -getItem:Compile,EmbeddedResource,Content,None,ProjectReference`.
- Evaluated bridge items and manifest resource names; confirmed the three
  blocked fonts are embedded in `PDF_Manager.LegacyPrinting.dll` and the bridge
  is present in the active startup Release output.
- Ran NuGet advisory checks for both the standalone desktop and the legacy
  bridge. Desktop: no vulnerable package reported. Bridge: ImageSharp 1.0.4
  with three High and four Moderate advisories.
- Confirmed the renderer's UI-thread, disposal, finite-coordinate, and checked
  allocation controls directly from source and tests.

## Scope Note

Known repository-wide Python and Angular gate failures were excluded as
requested. The pre-existing user-owned handoff skill was not reviewed. No
product code was modified by this review.

## Step 2 Addendum (2026-09-20)

### Step 2 Verdict

**PASS for the new Step 2 Core boundary** — Critical: 0; High: 0; Medium: 0;
Low: 1. The repository-level verdict remains **Changes requested** because the
two Step 1 High findings in the legacy Azure/local print host are unchanged.
The new desktop still has no dependency path to that host.

### [Low] Project and result readers do not yet enforce byte or collection budgets

- **Evidence**: `JsonProjectStore.OpenAsync` reads the complete project file,
  while `AnalysisResultSetJson.DeserializeAsync` copies a complete stream into
  memory. Both readers enforce JSON depth and strict shape/semantic validation,
  but neither currently caps encoded bytes or topology/result collection sizes.
- **Impact**: Step 2 exposes only local Core APIs, so this is presently a local
  malformed-file/resource-exhaustion concern rather than a remote path. It
  becomes externally relevant when Step 4 connects analysis responses.
- **Required follow-up**: define product-sized byte/entity/result budgets before
  the Step 4 HTTP adapter and reject over-budget streams before buffering. Keep
  the limit policy outside the exact shared `AnalysisResultSet v1` semantics.

### Step 2 Positive Controls

- Project and result JSON reject unknown and duplicate members, malformed UTF-8,
  unsupported versions/variants, non-finite values, invalid references, and
  `null` required objects through typed non-sensitive failures.
- Project saves use a same-directory temporary file, flush it to disk, atomically
  replace/move the destination, honor cancellation before commit, and clean up
  the temporary file on failure.
- The new Core code adds no package, network, process, reflection, dynamic-load,
  credential, image-decoder, font, or legacy-print dependency.
- Derived and moving-load presentation operates on validated immutable results;
  arithmetic overflow, missing/non-static sources, duplicate cases, and
  out-of-order envelope sources are rejected.

Verification: Core 75/75, both solution graphs 95/95, shared Python contract
tests 14/14, Release builds PASS, and `dotnet format --verify-no-changes` PASS.
The requested parallel reviewers were unavailable because all three worker
runtimes stopped at their usage limit; this addendum is the lead's read-only
fallback review of the frozen Step 2 diff.

## Step 3 Addendum (2026-09-20)

### Step 3 Verdict

**PASS with Low follow-up for the new Step 3 shell boundary** — Critical: 0;
High: 0; Medium: 0; Low: 2. The repository-level verdict remains **Changes
requested**, and complete-application publication or redistribution remains
**NO-GO**, because the inherited legacy-host High findings below are unchanged.

### New Step 3 Findings

#### [Low] Persisted layout JSON is deserialized without an encoded-size or entry-count budget

- **File**: `FramePrintPDF/PDF_Manager/Shell/Docking/DockLayoutAdapter.cs:61-68`
- **Evidence**: `RestoreJson` passes an arbitrary string directly to
  `LayoutStateJson.Deserialize` before the adapter can apply its complete-state
  validation. The resulting contract correctly validates versions, stable
  keys, whitelist membership, dock states, contiguous order, floating bounds,
  and active-document consistency, but it does not bound the serialized string
  or the number of `contents` objects before allocation. A very large layout
  therefore consumes parser and object-graph memory before duplicate or
  non-whitelisted keys are rejected. Current `MainForm` does not yet load a
  layout file automatically, so this is a local/future persistence path rather
  than a remote vulnerability.
- **Impact**: when Step 3 layout persistence is connected to disk or another
  startup source, a corrupted or intentionally oversized layout can delay or
  exhaust application startup before the whitelist rejection runs. It cannot
  select an arbitrary CLR type or bypass the factory whitelist.
- **Recommended fix**: define a small maximum encoded byte/character size and
  a maximum content count tied to the registered shell keys. Enforce the byte
  limit while reading the persisted file, before constructing a `string`, and
  enforce the entry limit during or immediately after streaming parse. Add
  boundary tests for exactly-at-limit and over-limit layout data.

#### [Low] Unrelated cancellation exceptions are silently treated as user cancellation

- **File**: `FramePrintPDF/PDF_Manager/Shell/Lifecycle/UserExceptionBoundary.cs:37-52,56-73`
- **Evidence**: both `Execute` and `ExecuteAsync` catch every
  `OperationCanceledException` and return `false` without invoking either the
  diagnostic sink or the user-safe error mapper. The async branch does not
  check whether its supplied `cancellationToken` was actually cancelled.
  `MainForm.RunOperationAsync` uses this boundary at
  `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:489-514`; if a future HTTP
  analysis client reports a timeout or another independent abort as
  `TaskCanceledException`/`OperationCanceledException` while the shell token is
  not cancelled, the operation ends with no user error and no protected
  diagnostic.
- **Impact**: availability, timeout, and potentially security-relevant transport
  failures can be suppressed as normal control flow. No secret is disclosed,
  but the missing audit/diagnostic signal makes a real failure indistinguishable
  from an intentional cancellation.
- **Recommended fix**: in `ExecuteAsync`, suppress cancellation only under a
  catch filter that confirms the boundary's supplied token is cancelled; map
  or diagnose every other `OperationCanceledException`. For synchronous
  execution, accept an explicit expected token (or do not special-case
  cancellation). Add tests for an unrelated cancelled token and for an
  uncancelled timeout-style `TaskCanceledException`.

### Unchanged Inherited Legacy-Host Findings

- **[High, unchanged] Vulnerable image decoder**: the anonymous Azure/local
  print route still passes attacker-controlled image data to the
  PdfSharpCore/ImageSharp 1.0.4 graph described in the first High finding.
- **[High, unchanged] Unbounded anonymous work**: request bodies, base64/gzip
  expansion, parsed collections, images, pages, elapsed work, and concurrency
  remain unbounded as described in the second High finding.
- **[Medium, unchanged] Restricted embedded fonts**: the non-packable bridge
  still embeds the three blocked CJK fonts and is copied through the active
  startup graph.

None of the Step 3 shell, docking, lifecycle, or localization files reference
that host, its dictionaries, its fonts, PdfSharpCore, ImageSharp, or
Newtonsoft.Json. This confirms isolation only. The legacy-host findings retain
their original severities and required remediation, and the complete
application must not be published or redistributed until they are resolved.

### Step 3 Positive Controls and Explicit No-Findings

- Layout identity is a versioned `DocumentKey`, not a caption, CLR type name,
  or reflection string. `MainForm.RegisterContents` registers four exact
  factories at `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:329-344`, and
  `DockContentRegistry.CreateContent` verifies that each factory returns the
  exact registered key at
  `FramePrintPDF/PDF_Manager/Shell/Docking/DockContentRegistry.cs:215-254`.
  No `Activator`, `Type.GetType`, dynamic assembly loading, or arbitrary factory
  lookup was found.
- `DockLayoutAdapter.Restore` validates the complete candidate before changing
  a pane at `FramePrintPDF/PDF_Manager/Shell/Docking/DockLayoutAdapter.cs:65-86`.
  The underlying contract rejects unknown versions/keys, invalid document/tool
  placement, invalid bounds, non-contiguous order, and inconsistent active
  documents. Malformed-version and unknown-key STA tests prove rejection before
  pane creation.
- Unexpected exceptions are mapped to a generic localized message; original
  exception objects go only to the optional injected diagnostic hook. Typed
  validation/operation messages are displayed intentionally. No stack trace,
  inner exception, credential, token, connection string, or environment value
  is written by the Step 3 implementation.
- Activation side effects are serialized, coalesced, revision-checked, and
  cancelled outside lifecycle locks. The registry enforces STA access, and the
  shell unsubscribes menu, docking, localization, and form events before
  disposing panes and the DockPanel.
- The `.resx` files contain string resources only: no file references,
  serialized objects, binary payloads, font files, or secrets. User document
  names and selected paths are inserted as formatting arguments, not parsed as
  format strings or executable content.
- The Step 3 code adds no process start, shell execution, network endpoint,
  authentication/authorization change, or secret source. PDF output uses a
  same-directory `CreateNew` temporary file, flushes before replace/move, and
  removes a leftover temporary file on failure.
- Evaluated `PDF_Manager` build items include only the new shell/localization
  sources and four string resources. Project references remain Core,
  Rendering, and typed Printing only; there is no LegacyPrinting or Azure-host
  reference. `dotnet list ... package --vulnerable --include-transitive
  --no-restore` reports no current advisory in the standalone desktop graph.
- The earlier Step 1 Low finding about a process-wide fail-closed WinForms
  exception policy remains open: the new command boundary is valuable but does
  not replace an `Application.ThreadException`/top-level policy for arbitrary
  paint or framework callbacks. The rendering-size Low and Step 2 JSON-budget
  Low also remain tracked for their planned integration steps.

### Step 3 Verification Performed

- Reviewed the Step 3 portions of
  `.agents/logs/review-diff-csharp-frameweb-client.patch`, all shell/docking/
  lifecycle/localization product files, and all Step 3 STA tests as evidence.
- Independently ran `dotnet test
  FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release
  --no-restore`: 37 passed, 0 failed, 0 skipped.
- Evaluated `PDF_Manager` compile/resource/project-reference items and confirmed
  that legacy dictionaries, printing sources, restricted fonts, and the legacy
  bridge are excluded from the desktop product graph.
- Re-ran the standalone desktop NuGet advisory check: no vulnerable package was
  reported. The supplied integration evidence also reports both solution builds
  passing, both solution test runs at 131/131, targeted formatting passing, and
  the AgentOnly gate at `overall=pass`.

### Step 3 Scope Note

The review did not modify product or test code and did not reclassify unchanged
Step 2 Core files as Step 3 changes. The Step 3 findings are limited to the
shell paths named above. Existing legacy-host findings are restated solely to
preserve the integrated security and redistribution decision.

## Final Step 3 Security Re-review (2026-09-20)

### Current Step 3 Verdict

**PASS for the new Step 3 security boundary** — Critical: 0; High: 0;
Medium: 0; Low: 0. Both original Step 3 Low findings are resolved. No new
Step 3 High or Medium finding remains after the final remediation. This does
not change the integrated repository verdict: the inherited host remains
Critical: 0; High: 2; Medium: 1, and public deployment or redistribution of
the complete application remains **NO-GO**.

### Step 3 Finding Status

#### [Resolved, Low] Persisted layout input is bounded before contract construction and factory work

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/Contents/ShellLayoutStore.cs:30-96`
  limits disk input to 256 KiB before UTF-8 string construction and uses strict
  decoding. `FramePrintPDF/PDF_Manager/Shell/Docking/DockLayoutAdapter.cs:18-20,120-129,295-342`
  additionally limits characters and UTF-8 bytes to 65,536 and counts at most
  256 `contents` entries with a bounded `JsonDocument` pass before the typed
  `LayoutState` object and whitelist-driven restore are constructed.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/Step3BoundaryRemediationTests.cs:12-55`
  proves UTF-8-byte and entry-count rejection before pane/factory mutation;
  `FramePrintPDF/PDF_Manager.UiTests/ShellLayoutStoreTests.cs:9-110` proves
  missing-file behavior, exact and
  over-limit disk data, strict malformed UTF-8 handling, atomic replacement
  cleanup, and pre-cancelled operations.
- **Disposition**: Resolved. Layout identity remains stable `DocumentKey`
  data; no CLR type-name/reflection or arbitrary-factory path was introduced.

#### [Resolved, Low] Only cancellation owned by the active request is suppressed

- **Evidence**:
  `FramePrintPDF/PDF_Manager/Shell/Lifecycle/UserExceptionBoundary.cs:38-77`
  suppresses OCE only when the explicitly supplied expected token is
  cancelled. `FramePrintPDF/PDF_Manager/Shell/MainForm.cs:555-576,611-637`
  sends an uncancelled OCE from a shell transition or operation through the
  safe exception boundary, while the operation catch filter checks the actual
  lease token. `MainForm.cs:659-745,864-879` propagates owned close-phase
  tokens through project/layout work and diagnoses unrelated failures.
- **Regression**:
  `FramePrintPDF/PDF_Manager.UiTests/MainFormCancellationClassificationTests.cs:15-101`
  proves uncancelled OCE and `TaskCanceledException` from analysis/project
  storage produce the exact diagnostic plus generic user-safe notification,
  while actual shell cancellation produces neither. The reusable-boundary
  cases remain covered at
  `FramePrintPDF/PDF_Manager.UiTests/Step3BoundaryRemediationTests.cs:144-184`.
- **Disposition**: Resolved. The final re-review initially found broad catches
  in `MainForm`; the token-filtered implementation and MainForm-level tests
  close the gap rather than relying only on unit tests of
  `UserExceptionBoundary`.

### Unchanged Inherited and Earlier Findings

#### [Residual, High] Vulnerable image decoder remains reachable from anonymous print routes

- **Evidence**:
  `FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj:41-44`
  still resolves `PdfSharpCore` 1.3.9 and transitive
  `SixLabors.ImageSharp` 1.0.4.
  `FramePrintPDF/FramePrintAzure/Function1.cs:19-20` and
  `FramePrintPDF/FramePrintAzure/Function2.cs:19-20` remain anonymous GET/POST
  triggers, and untrusted image
  data still reaches `Convert.FromBase64String`/`XImage.FromStream` at
  `FramePrintPDF/PDF_Manager/Printing/Diagram3D/PrintBase3dDiagram.cs:58-60`.
  The final `dotnet list ... --vulnerable --include-transitive --no-restore`
  run again reported three High and four Moderate ImageSharp advisories.
- **Disposition**: Residual, unchanged. Do not expose this host; replace the
  decoder/PDF graph and enforce validated image format, byte, and dimension
  limits before deployment.

#### [Residual, High] Anonymous request, expansion, collection, and work remain unbounded

- **Evidence**:
  `FramePrintPDF/FramePrintAzure/Function1.cs:20,34-49,60-74` and
  `FramePrintPDF/FramePrintAzure/Function2.cs:20,34-49,60-74` still read the
  full request, allocate repeated
  base64/CSV buffers, and decompress into an unbounded `MemoryStream` without
  authentication, input/output/page/image/time/concurrency limits, or request
  cancellation.
- **Disposition**: Residual, unchanged. Require the intended authorization and
  POST-only contract, then add server/application body, decompression,
  collection, image/page, deadline, rate, and concurrency budgets.

#### [Residual, Medium] Restricted fonts remain embedded in the active legacy-host graph

- **Evidence**:
  `FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj:9,26-39`
  still embeds MS Gothic, MS Mincho, and SimSun;
  `FramePrintPDF/FramePrintAzure/FramePrintAzure.csproj:17-19` and
  `tools/FrameWeb.Startup/FrameWeb.Startup.csproj:8-10` still copy that bridge
  through the active host graph. `IsPackable=false` is not a publish boundary.
- **Disposition**: Residual, unchanged. Remove the resources or adopt an exact
  approved redistributable font and enforce a publish-artifact digest/resource
  scan before release.

#### [Residual, Low] Rendering scene/capture allocation budgets are not defined

- **Evidence**:
  `FramePrintPDF/PDF_Manager.Rendering/RenderSceneModel.cs:9-16,31-47`
  accepts any finite complete triangle array;
  `FramePrintPDF/PDF_Manager.Rendering/OpenGlViewportLifecycle.cs:186-208,322-329`
  allocates capture bytes and recopies/uploads the full model without a product
  size ceiling.
- **Disposition**: Residual from Step 1. There is still no current untrusted
  scene adapter, but explicit vertex/scene/capture budgets remain required
  before that integration.

#### [Residual, Low] A process-wide fail-closed WinForms exception policy is not installed

- **Evidence**: `FramePrintPDF/PDF_Manager/Program.cs:7-12` still initializes
  WinForms and runs `MainForm` without an application-level thread-exception
  policy. The command boundary does not cover arbitrary framework/paint event
  failures.
- **Disposition**: Residual from Step 1. Install and test the top-level policy
  before the renderer is hosted by the product shell.

#### [Residual, Low] Project/result readers still lack byte and collection budgets

- **Evidence**:
  `FramePrintPDF/PDF_Manager.Core/Documents/JsonProjectStore.cs:24-32` still
  uses `File.ReadAllBytesAsync`;
  `FramePrintPDF/PDF_Manager.Core/Analysis/AnalysisResultSetJson.cs:70-77`
  still copies the full stream to `MemoryStream` before parsing.
- **Disposition**: Residual from Step 2. Enforce byte/entity/result limits
  before Step 4 connects HTTP analysis data. This is separate from the now
  bounded Step 3 layout format.

### Step 3 Isolation and No-finding Confirmation

- `FramePrintPDF/PDF_Manager/PDF_Manager.csproj:12-28` still removes legacy
  printing sources/fonts and references only Core, Rendering, typed Printing,
  and DockPanelSuite packages. No Step 3 shell/resource file references Azure,
  legacy dictionaries/fonts, PdfSharpCore, ImageSharp, or Newtonsoft.Json.
- `dotnet list FramePrintPDF/PDF_Manager/PDF_Manager.csproj package --vulnerable --include-transitive --no-restore`
  again reports no vulnerable package for the standalone desktop graph.
- Layout restore uses only registered `DocumentKey` factories, exact version/
  key/state/bounds/order validation, creating-thread guards, and transactional
  rollback. Unexpected diagnostics retain the original exception only in the
  optional trusted diagnostic sink; the UI receives a generic localized
  message. No new process launch, network endpoint, authentication change,
  credential/secret source, or dynamic code/type loading was found.
- Local layout storage is confined by default to the current user's local app
  data and writes a same-directory temporary file before final replacement;
  malformed, absent, invalid UTF-8, oversized, and cancelled paths fail safely.

### Final Validation

- `dotnet test FramePrintPDF/PDF_Manager.UiTests/PDF_Manager.UiTests.csproj -c Release --no-restore --verbosity minimal`:
  PASS, 74 passed, 0 failed, 0 skipped (independent final run).
- Focused final MainForm cancellation/close remediation:
  `MainFormCancellationClassificationTests`: PASS, 6/6 (implementer run,
  followed by the independent full run above).
- `dotnet list FramePrintPDF/PDF_Manager/PDF_Manager.csproj package --vulnerable --include-transitive --no-restore`:
  no vulnerable package reported.
- `dotnet list FramePrintPDF/PDF_Manager.LegacyPrinting/PDF_Manager.LegacyPrinting.csproj package --vulnerable --include-transitive --no-restore`:
  ImageSharp 1.0.4, High 3 and Moderate 4, unchanged.
- Lead final evidence is green: both solution graphs 168/168,
  prior Step 3 stress 24/24 repeated three times, targeted format and
  `git diff --check`, and AgentOnly `overall=pass` at
  `.agents/logs/check-20260920T101620531Z-29020.log`.
- Python/Angular full suites were not rerun, as required. Coverage was not
  measured. This final re-review modified only the two review reports and its
  work log, not product or test code.

## Step 4 Final Security Review (2026-09-20)

### Step 4 Verdict

**CHANGES REQUESTED for the Step 4 vertical MVP** — Critical: 0; High: 0;
Medium: 2; Low: 4. The two Medium findings require remediation before Step 4
is called complete. The four Low findings should be fixed in the same hardening
pass or explicitly accepted with owners and deadlines. This verdict is
independent of the unchanged legacy print-host findings (Critical: 0; High: 2;
Medium: 1), which continue to make public deployment and complete-application
redistribution **NO-GO**.

### New Step 4 Findings

#### [Medium] The desktop automatically exposes an unauthenticated, wildcard-CORS analysis service with unbounded direct-call work

- **Evidence**: `FramePrintPDF/PDF_Manager/Program.cs:16-21` starts
  `FrameWebDesktopRuntime` before opening the shell, and
  `tools/FrameWeb.LocalRuntime/FrameWebRuntimeCommandFactory.cs:26-31` launches
  Flask on the loopback host. The unchanged endpoint at
  `FrameWeb/main.py:41-43,69-83` accepts `OPTIONS`, `GET`, and `POST`, advertises
  `Access-Control-Allow-Origin: *`, and allows `Content-Type` and
  `Authorization`. `FrameWeb/main.py:91-112,140-169` reads complete JSON or
  base64/gzip data, expands it, parses it, and invokes the solver without an
  inbound byte, expansion, case/entity/work, concurrency, or deadline limit.
  The C# limits in `FrameWebAnalysisClient` constrain only requests made by the
  desktop client; they do not protect the Flask listener from direct callers.
- **Exploit/failure scenario**: while the desktop is open, a malicious web page
  can issue cross-origin loopback requests and repeatedly submit large or
  expensive models. This can exhaust the owned Python process and make the
  desktop analysis feature unavailable. The Python CORS behavior predates Step
  4, but automatically activating that listener is a new desktop integration
  reachability, so it is not folded into either legacy *print-host* High
  finding.
- **Concrete remediation**: generate a high-entropy per-run secret, pass it to
  the child without writing or logging it, and require it in a non-simple
  request header for both readiness and analysis. Disable CORS for the desktop
  runtime (or use an exact trusted-origin allowlist rather than `*`). Enforce
  compressed and expanded byte limits before allocation, strict JSON content
  type/UTF-8, case/entity/work/concurrency limits, and a server-side deadline.
  Add browser-origin and missing/wrong-token rejection tests.

#### [Medium] Readiness accepts any loopback 2xx response and does not prove that the launched child owns the endpoint

- **Evidence**: `tools/FrameWeb.LocalRuntime/FrameWebRuntimeContracts.cs:92-114`
  defaults to fixed port 8080 and the root URI. `FrameWebLocalRuntime.cs:227-248`
  creates a redirect-following default `HttpClient` and declares readiness for
  any 2xx response; it checks no response body, nonce, server identity, or
  post-response process state. `FrameWeb.LocalRuntime.Tests/FrameWebLocalRuntimeTests.cs:8-26`
  explicitly demonstrates that an unrelated `LocalReadyServer` can make a
  separate long-running command become `Ready`. The resulting URI is reused
  for analysis at `PDF_Manager/Shell/Composition/DesktopApplicationSession.cs:21-25,49-56`.
- **Exploit/failure scenario**: another process binds 127.0.0.1:8080 before the
  desktop child. During the bind-failure race it answers 2xx, causing the shell
  to become ready and send project model data to the unrelated service. Default
  redirects also allow the readiness probe to leave the validated loopback
  origin. This is an integrity and local data-routing failure even when no
  remote endpoint is configured.
- **Concrete remediation**: allocate/reserve an available loopback port and
  hand it to the child, use a dedicated readiness path that returns an exact
  versioned body plus the per-run secret, disable automatic redirects, and
  confirm that the child remains alive after the authenticated response. Reuse
  the authenticated origin/session for analysis and add port-squatting,
  wrong-body, wrong-token, redirect, and child-exit race tests.

#### [Low] Child-output limits are applied after whole-line allocation and do not bound Trace forwarding

- **Evidence**: `tools/FrameWeb.LocalRuntime/ManagedChildProcess.cs:27-30,69-70,136-149`
  uses `BeginOutputReadLine`/`BeginErrorReadLine`, which delivers a complete
  decoded line before the callback. `BoundedLineBuffer.cs:22-40` truncates that
  already-created string only when storing it. `FrameWebLocalRuntime.cs:370-388`
  then publishes the original untruncated line, and
  `PDF_Manager/Shell/Composition/DesktopApplicationSession.cs:43-44` writes it
  to `Trace`.
- **Exploit/failure scenario**: a failed or compromised child emits a very large
  newline-free record. The process first allocates the complete string and the
  desktop forwards it to arbitrary trace listeners even though diagnostics
  later show a bounded 256 KiB snapshot. Repeated output can cause memory/log
  pressure and expose unbounded child-controlled text in diagnostic sinks.
- **Concrete remediation**: replace line-event reading with a fixed-size
  streaming decoder that caps per-record and total characters before string
  construction, discards until the next delimiter after truncation, and
  publishes only the bounded/sanitized record. Add a multi-megabyte
  newline-free output test and a subscriber-volume assertion.

#### [Low] A child can run and spawn descendants before it is assigned to the kill-on-close Job Object

- **Evidence**: `tools/FrameWeb.LocalRuntime/ManagedChildProcess.cs:49-70`
  calls `Process.Start` at line 54 and assigns the running process to the job at
  line 61. `FrameWebRuntimeCommandFactory.cs:52-63` commonly starts `uv`, which
  then starts Python. `WindowsProcessJob.cs:27-35,43-50` configures
  `KillOnJobClose`, but that policy applies only after assignment.
- **Exploit/failure scenario**: in the start-to-assignment window, the launcher
  can create Python (or another descendant) outside the job. If the desktop
  then crashes, parent-exit disposal kills only assigned processes and can
  leave an orphan listener. The current descendant test starts its child only
  after the parent has already been assigned, so it does not cover this race.
- **Concrete remediation**: create the process suspended, assign it to the job,
  then resume it (or use a trusted launcher that self-assigns before spawning).
  Add a stress test using the actual `uv` fallback and verify no descendant
  survives abrupt parent termination.

#### [Low] PDF capture and job collection limits are checked only after potentially excessive allocation/materialization

- **Evidence**: `FramePrintPDF/PDF_Manager.Printing/TypedPdfExporter.cs:15-33`
  defines a 2048-pixel/12 MiB capture boundary, but
  `ModelViewportCapture.Create` allocates `new byte[checked(width * height * 3)]`
  at lines 162-180 before constructing `ViewportCapture`, where the dimension
  checks occur. The same file materializes `nodes`, `members`, and result rows
  with `ToArray()` before checking their maximum counts at lines 80-86 and
  170-176.
- **Exploit/failure scenario**: a direct caller can request dimensions whose
  checked product is valid but close to 2 GiB, causing a large allocation or
  `OutOfMemoryException` before the documented 2048/12 MiB rejection. An
  unbounded enumerable can likewise be exhausted into memory before its count
  is rejected. The current shell supplies fixed dimensions and owned arrays,
  which limits present reachability but does not make the public boundary safe.
- **Concrete remediation**: validate positive dimensions, each dimension, and
  the checked byte product against `ViewportCapture` limits before allocation.
  Materialize through a `maximum + 1` bounded collector (or require bounded
  read-only collections) and test huge-but-non-overflowing dimensions and
  over-limit/lazy enumerables.

#### [Low] Successful responses declaring a non-UTF-8 charset are accepted

- **Evidence**: `FramePrintPDF/PDF_Manager.Core/Analysis/FrameWebAnalysisClient.cs:243-252`
  checks only that the media type is `application/json`; it does not reject a
  present `charset` other than UTF-8. The later byte parser correctly rejects
  malformed UTF-8, so this is a strict protocol gap rather than an alternate
  success-schema path.
- **Exploit/failure scenario**: a proxy or impersonating local service can send
  ASCII-compatible JSON labelled `charset=utf-16` or another charset and still
  have it accepted. This weakens the sole-success-contract boundary and makes
  transport metadata disagree with the actual byte interpretation.
- **Concrete remediation**: accept an absent charset or an exact UTF-8 alias
  only, reject every other declared charset before reading/parsing the body,
  and add tests for UTF-16, Latin-1, quoted/case variants, and valid UTF-8.

### Earlier Desktop Findings Still Open After Step 4

- **[Low] Process-wide WinForms exception policy**:
  `FramePrintPDF/PDF_Manager/Program.cs:9-31` still does not install a
  `ThreadException`/fail-closed policy. This earlier finding is now directly
  relevant because Step 4 hosts the renderer. A paint/framework exception can
  escape the command boundary and permit continued use of partially failed UI
  or GL state. Install the policy, dispose/disable the failed viewport, and
  exit when consistency cannot be proved.
- **[Low] Project-file input budget**:
  `FramePrintPDF/PDF_Manager.Core/Documents/JsonProjectStore.cs:24-32` still
  calls `File.ReadAllBytesAsync` before any encoded-size/entity limit. The Step
  4 HTTP response is now bounded before `AnalysisResultSetJson`, so that half
  of the earlier finding is resolved; local malformed/oversized project files
  remain an allocation-denial path. Stream through a byte limiter and apply
  project collection limits before full contract construction.
- **[Low] Rendering limit placement**:
  `FramePrintPDF/PDF_Manager.Rendering/Scene/ViewportSceneModel.cs:96,104-123,154-160`
  materializes every enumerable before enforcing its 250,000-entity limit,
  while `SceneDisplacementLayer` materializes an unbounded node list at lines
  66-85. `OpenGlViewportLifecycle.cs:336-353` still allocates
  `width * height * 4` without a product byte ceiling. Use bounded collectors,
  include the displacement layer in the scene budget, and reject capture
  dimensions/bytes before allocation.

### Step 4 Positive Controls and Explicit No-Findings

- Request serialization enforces case/entity/byte limits before completing the
  body (`FrameWebAnalysisRequestJson.cs:43-69,90-108,372-393`). Response bytes
  are bounded before a depth-limited `JsonDocument`; case/result/entity counts
  are checked before typed contract construction
  (`FrameWebAnalysisClient.cs:255-284,327-470`). Actual malformed UTF-8,
  duplicate/unknown members, depth overflow, alternate success shapes, and
  partial schemas are rejected. The 64 MiB byte ceiling bounds the preflight
  DOM allocation; a streaming preflight would reduce peak memory but is not an
  additional unbounded-path finding.
- Runtime host and readiness option validation requires loopback
  (`FrameWebRuntimeContracts.cs:128-148`), process arguments use
  `ProcessStartInfo.ArgumentList` with `UseShellExecute=false`
  (`ManagedChildProcess.cs:22-47`), and no shell-string construction or
  user-controlled executable/working-directory surface is exposed by the
  desktop composition. Startup, stop, request, and shell-close paths have
  bounded timeouts/cancellation; the Job Object is kill-on-close once assigned.
- MainForm captures document revision, rejects stale/late analysis publication,
  serializes document transitions, cancels owned work, and bounds close-time
  analysis/save/layout waits. Default project/PDF writers perform same-directory
  create-new temporary writes, flush before commit, and atomically
  replace/move; no new data-loss race was found in the reviewed default path.
- The typed PDF writer bounds final image/text/result content, escapes `(`,
  `)`, and backslash while replacing non-printable/non-WinAnsi text, uses fixed
  object counts and invariant xref offsets, and keeps its bounded output far
  below the ten-digit xref limit. No path traversal, raw PDF-object injection,
  unsafe global font state, or integer overflow after accepted limits was
  found.
- The standalone desktop evaluated graph references Core, Rendering, typed
  Printing, and LocalRuntime only. It contains no legacy dictionary adapter,
  restricted font resource, `PDF_Manager.LegacyPrinting`, `FramePrintAzure`,
  PdfSharpCore, or ImageSharp path. Current no-restore advisory scans for
  `PDF_Manager`, `PDF_Manager.Printing`, and `FrameWeb.LocalRuntime` reported no
  vulnerable packages. No credential, bearer token, connection string, or
  project payload was found in user-facing messages; unexpected exceptions and
  child output remain confined to trusted diagnostic/Trace sinks, subject to
  the output-bound finding above.

### Unchanged Legacy Print-Host Findings

- **[High, unchanged]** vulnerable ImageSharp 1.0.4 remains reachable through
  anonymous legacy print routes (`PDF_Manager.LegacyPrinting.csproj:41-44`,
  `FramePrintAzure/Function1.cs:19-20`, `Function2.cs:19-20`).
- **[High, unchanged]** anonymous legacy print requests still have unbounded
  body/base64/gzip/image/PDF work (`Function1.cs:34-49,60-74` and matching
  `Function2.cs`).
- **[Medium, unchanged]** restricted MS Gothic, MS Mincho, and SimSun resources
  remain embedded and copied through the active legacy host graph
  (`PDF_Manager.LegacyPrinting.csproj:26-39`,
  `FrameWeb.Startup.csproj:8-10`).

These are the pre-existing legacy *print-host* findings. They are separate from
the Step 4 local Python analysis-service findings above and remain a complete-
application publication/redistribution blocker.

### Step 4 Independent Validation

- Reviewed the frozen 97-path patch at
  `.agents/logs/review-diff-csharp-frameweb-client.patch` plus current source,
  project graphs, Step 4 plan, DESIGN, HANDOFF, tests, and security rules.
- Independent Release/no-restore tests: Core 102/102, typed
  Printing/composition 13/13, Rendering 16/16, UI 78/78, LocalRuntime 9/9;
  total 218/218 PASS. Coverage was not measured.
- `dotnet list ... package --vulnerable --include-transitive --no-restore` for
  the standalone desktop, typed Printing, and LocalRuntime reported no
  vulnerable packages. The legacy bridge advisories remain unchanged.
- Python and Angular full suites were not rerun. No product/test source,
  staging state, commit, or user-owned work was changed by this review.

## Final Post-remediation Step 4 Security Re-review (2026-09-20)

### Final Verdict

**PASS for Step 4 security completion with tracked Low follow-ups** — Critical:
0; High: 0; Medium: 0; Low: 3. Both previously blocking Step 4 Medium findings
are resolved. Two of the four original Step 4 Low findings are resolved; two
remain, and one additional token-gated solver-lifecycle Low is recorded below.
No Critical, High, or Medium fix is required before Step 4 is called complete.
The Low items require explicit ownership and follow-up, but are not Step 4
completion blockers.

This does **not** clear public deployment or complete-application
redistribution. Integrated residual counts are Critical: 0; High: 2; Medium: 2;
Low: 6: the Step 4 Low findings below, three earlier desktop Lows, the unchanged
legacy/local-development Startup Medium, and the unchanged legacy print-host
High 2 / Medium 1 findings.

### Residual Step 4 Findings

#### [Low] Child output is bounded only after whole-line allocation, and forwarded/logged output has no lifetime budget

- **Evidence**: `tools/FrameWeb.LocalRuntime/ManagedChildProcess.cs:79-95,161-189`
  still uses `BeginOutputReadLine` / `BeginErrorReadLine`. The framework creates
  the complete decoded line before `Publish` sanitizes and truncates it.
  `FrameWebLocalRuntime.cs:567-585` stores only bounded lines but publishes every
  bounded record; `PDF_Manager/Shell/Composition/DesktopApplicationSession.cs:47-48`
  forwards every record to `Trace`. The legacy Startup likewise writes every
  forwarded record to its log at `tools/FrameWeb.Startup/ManagedProcess.cs:89-102`
  without rotation or a total-byte budget.
- **Failure scenario**: a failed or compromised child emits a very large
  newline-free record, causing a large transient string (and redaction copy)
  before truncation. Repeated bounded records can still create unlimited Trace
  or legacy log volume over the process lifetime and consume memory, I/O, or
  disk space.
- **Concrete remediation**: replace line events with a fixed-size streaming
  decoder that bounds allocation before string creation and discards through
  the delimiter after truncation. Apply a total/rate budget to subscriber
  forwarding and rotate/cap the legacy logs. Test a multi-megabyte newline-free
  record and sustained output, not only a 5,000-character line.

#### [Low] Process creation still has a pre-Job assignment escape window

- **Evidence**: `tools/FrameWeb.LocalRuntime/ManagedChildProcess.cs:79-95`
  starts the process at line 79 and assigns it to the kill-on-close Job at line
  86. `FrameWebRuntimeCommandFactory.cs:59-70` may start `uv`, which can spawn
  Python. `WindowsProcessJob.cs:27-50` applies kill-on-close only after the
  assignment succeeds.
- **Failure scenario**: a launcher scheduled in the start-to-assignment window
  can create a detached descendant outside the Job. The new listener ownership
  checks prevent that escaped listener from being trusted or receiving the
  capability token, reducing the residual impact to orphan-process and local
  availability risk, but abrupt parent termination can still leave a process.
- **Concrete remediation**: create the launcher suspended, assign it to the Job,
  and resume it, or use an equivalent trusted bootstrap that cannot spawn until
  assignment. Stress the actual `uv` fallback and verify no pre-assignment
  descendant survives abrupt parent termination.

#### [Low] Client timeout/cancellation does not terminate in-progress Python solver work

- **Evidence**: `FrameWebAnalysisClient.cs:230-259` bounds the HTTP wait and
  releases the client-side concurrency slot on timeout/cancellation, but
  `FrameWeb/main.py:223` calls `build_analysis_result_set` synchronously with no
  server-side deadline, cancellation channel, or request concurrency gate.
  Request bytes/cases/entities are finite, but the current 4 MiB, 256-case, and
  100,000-entity ceilings do not constitute a feasible dense-FEM work budget.
- **Failure scenario**: a locally opened crafted or merely extreme project can
  continue consuming CPU/memory in Python after the desktop has reported its
  30-second timeout. A retry can admit more server work after the client slot is
  released. The per-run token prevents browser and unrelated-process abuse, so
  this is a local availability concern rather than the former Medium remote
  loopback exposure.
- **Concrete remediation**: define solver-feasible topology/DOF/work limits and
  a server concurrency ceiling before solve allocation. Execute analysis in a
  cancellable/killable worker boundary with a deadline tied to the desktop
  operation, and verify that timeout leaves no continuing solve before a retry
  is admitted.

### Blocking Findings Resolved

- **Prior Medium — anonymous/unbounded desktop Flask exposure: resolved for the
  desktop path.** `FrameWebLocalAuthentication.cs:9-15` creates a 32-byte CSPRNG
  per-run capability. `FrameWebRuntimeCommandFactory.cs:26-38,73-85` injects it
  only through the child environment. `FrameWeb/main.py:82-99` authenticates
  GET/POST/OPTIONS before any body accessor; lines 102-152 and 209-221 enforce
  raw/decompressed byte, exact JSON media-type, and strict UTF-8 boundaries.
  Missing/wrong tokens cannot reach readiness or analysis. Wildcard legacy CORS
  headers therefore do not give a browser the secret required to complete a
  successful preflight/request against desktop mode.
- **Prior Medium — unrelated 2xx readiness/port squatting: resolved.** Before
  the readiness capability is transmitted,
  `FrameWebLocalRuntime.cs:307-394`, `WindowsTcpListenerOwner.cs:14-107`, and
  `WindowsProcessJob.cs:53-95` require the exact literal-loopback listener PID
  to belong to the private Job. `OwnedLoopbackHttpConnection.cs:8-47` disables
  redirects, cookies, proxying, and connection reuse, connects only to the
  literal endpoint, then rechecks Ready state and listener ownership before the
  HTTP stack can write headers or body. Readiness also requires exact HTTP 200,
  `application/json; charset=utf-8`, a <=1,024-byte body, depth <=4, and exactly
  the three versioned marker properties at `FrameWebLocalRuntime.cs:410-471`.
  Squatter, ownership-loss, redirect, wrong-marker, wrong-token, and real
  uv/Flask tests pass.

### Other Step 4 Low Remediation Confirmed

- **PDF pre-allocation/materialization: resolved.**
  `TypedPdfExporter.cs:37-55,91-105,164-185,202-226` validates capture
  dimensions/byte product before allocation and materializes enumerable inputs
  through `maximum + 1` bounded collectors. `MainForm.cs:781-844` writes a
  same-directory create-new temporary, flushes it to disk, and atomically
  replaces/moves only after successful bounded export.
- **Response charset: resolved.** `FrameWebAnalysisClient.cs:304-353` accepts
  only `application/json` with an absent or UTF-8-equivalent charset before
  reading the body. The Python desktop mode now independently rejects declared
  UTF-16, raw UTF-16, malformed UTF-8, non-JSON media types, and unsupported
  parameters at `FrameWeb/main.py:121-152`. The C# preflight remains
  depth/case/result/entity bounded at `FrameWebAnalysisClient.cs:433-570`, and
  the sole strict `AnalysisResultSet v1` parser rejects alternate/partial roots.
- **Live capture exception escape found during re-review: resolved.**
  `MainForm.cs:314-329` now performs UI-thread capture inside
  `RunOperationAsync`; `Step4VerticalIntegrationTests.cs:218-265` proves a
  throwing capture provider reaches the owned diagnostic/user-safe boundary
  and does not escape the async menu handler.

### Earlier Desktop Low Findings Still Open

- **[Low] Process-wide WinForms exception policy** remains open at
  `PDF_Manager/Program.cs:9-31`. Command/capture failures are now owned, but a
  framework or GL paint callback exception can still enter WinForms' default
  thread-exception behavior without a fail-closed application policy.
- **[Low] Project-file byte/entity input budget** remains open at
  `JsonProjectStore.cs:24-32`, which calls `File.ReadAllBytesAsync` before a file
  ceiling, while the persisted document model has no collection ceiling.
- **[Low] Rendering capture allocation boundary** remains open at
  `OpenGlViewportLifecycle.cs:343-360`, which allocates `width * height * 4`
  without the typed PDF capture ceiling. Scene and displacement enumerables are
  now individually bounded before full materialization by
  `ViewportSceneModel.cs:78-103,128-135,280-302`; that part of the earlier
  rendering finding is resolved.

### Unchanged Legacy/Compatibility Findings

- **[Medium, unchanged, outside Step 4 desktop] Legacy Startup analysis host**:
  `tools/FrameWeb.Startup/ManagedProcess.cs:30-43` deliberately does not set
  `FRAMEWEB_LOCAL_AUTH_TOKEN`; `FrameWeb/main.py:82-84` therefore retains the
  anonymous/wildcard-CORS legacy behavior, and
  `tools/FrameWeb.Startup/LocalServices.cs:74-101` still treats any success as
  readiness. This preserves the Angular/F5 development workflow, but it is not
  the authenticated desktop runtime and must remain local-development-only and
  non-shippable. If it is retained beyond migration, give it an authenticated,
  origin-scoped, bounded transport and owned-listener readiness equivalent to
  `FrameWebDesktopRuntime`.
- **[High, unchanged] Legacy print image path** still exposes ImageSharp 1.0.4
  with three current High advisories through anonymous print routes.
- **[High, unchanged] Legacy print work** still accepts anonymous unbounded
  body/base64/gzip/image/PDF work.
- **[Medium, unchanged] Legacy restricted fonts** remain embedded in the active
  Startup/print graph. These print findings retain the exact evidence and
  remediation in the original sections above.

### Final Positive Controls and Explicit No-findings

- The runtime token is fresh per launch, kept out of command arguments and
  public default headers, redacted before bounded diagnostics/events, cleared
  from runtime state during cleanup, and added only to a private cloned request
  after exact endpoint/state/ownership checks. No secret, token, project body,
  or exception detail is written to user-facing messages.
- `FrameWebAnalysisClient` rejects new work after disposal while allowing
  admitted calls to release their semaphore safely
  (`FrameWebAnalysisClient.cs:94-190`). Desktop composition owns and disposes
  both the client and its runtime-created `HttpClient`
  (`DesktopApplicationSession.cs:53-72,108-124`).
- Request entity/case/byte limits run before serialization
  (`FrameWebAnalysisRequestJson.cs:43-69,86-112`); response bytes are bounded
  before depth-limited DOM/preflight and before typed construction. No alternate
  response schema is accepted by the desktop. No unsafe URL, redirect, proxy,
  shell argument, path traversal, PDF text/object injection, xref overflow,
  global PDF/font state, legacy dictionary, restricted font, PdfSharpCore, or
  ImageSharp path enters the standalone desktop graph.
- The live-capture probe, rendered-page golden/rasterizer, and subprocess
  harnesses are test-only, use fixed repository-owned executables/arguments,
  bounded timeouts/tree kill, and bounded product output. Their redirected
  `ReadToEndAsync` streams are a test-harness robustness limitation, not a
  product attack surface; no new product vulnerability was introduced.

### Final Independent Validation

- Both Release solution builds: PASS, 0 warnings / 0 errors.
- Focused Release/no-restore tests: Core 112/112, typed
  Printing/composition 16/16, Rendering 24/24, UI 81/81, LocalRuntime 14/14;
  total 247/247 PASS. Coverage was not measured.
- Targeted authenticated local-runtime Python security tests: 30/30 PASS. An
  independent probe confirmed UTF-16=415, text/plain=415, UTF-8 JSON=200.
  Full Python and Angular suites were not rerun.
- Current advisory scans: standalone desktop, typed Printing, and LocalRuntime
  have no reported vulnerable package. The legacy bridge still resolves
  ImageSharp 1.0.4 with three High and four Moderate advisories.
- No product/test source, staging state, commit, reset, or concurrent user work
  was modified by this final review.
