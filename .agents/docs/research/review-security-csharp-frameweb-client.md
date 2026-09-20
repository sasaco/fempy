# Security Review: C# FrameWeb Desktop Step 1

## Verdict

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
