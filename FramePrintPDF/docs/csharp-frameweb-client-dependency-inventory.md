# C# FrameWeb Client Dependency, Provenance, and Redistribution Inventory

Status date: **2026-09-20**. All package availability, version, target-framework,
and maintenance statements below were checked on that date. Local observations
were made against this working tree and the read-only reference checkout at
`C:/Users/sasai/Documents/isasPrint`.

## Gate result

The package-only .NET 8 WinForms probe is cleared to proceed. The redistribution
gate for a shippable application is **NO-GO** until the prohibited or
unverified legacy assets are demonstrably absent from publish output and the
PDF font strategy is replaced. In particular, this inventory does **not** clear
copying the `isasPrint/THREE` tree, its shader/font/texture assets, or the three
font binaries currently embedded by `PDF_Manager`.

The safe implementation path is maintained NuGet packages plus a small owned
renderer written against OpenTK 4. `isasPrint` may be consulted for architecture
and behavior, but no source or asset should be imported from it under this gate.

## Decision vocabulary

| Decision | Meaning |
|---|---|
| `package` | Consume the named immutable NuGet version and retain its license notice. |
| `copy` | Copy the identified source/asset with its required notice. No legacy item is approved for this action in Step 0. |
| `rewrite` | Implement the required behavior as new project-owned code or data without copying legacy expression. |
| `reference architecture only` | Inspect behavior and structure, but do not copy source or assets. |
| `blocked` | Do not distribute or import until the stated provenance/license problem is resolved. |

## Confirmed facts

### Platform and selected package set

| Item | Confirmed source/version and framework support | License / redistribution and transitive concerns | Decision |
|---|---|---|---|
| .NET desktop target | `Microsoft.NET.Sdk`, `net8.0-windows`, and `UseWindowsForms=true` are the supported Windows Forms project form. .NET 8 is supported through November 2026. [Desktop SDK properties](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop), [.NET lifecycle](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support) (checked 2026-09-20). | Framework redistribution is handled through the chosen framework-dependent or self-contained publish model. The short remaining .NET 8 support window is a maintenance risk, not a Step 0 license blocker. | `package` / platform |
| DockPanelSuite | Reference checkout uses 3.1.0. Selected package is [`DockPanelSuite` 3.1.1](https://www.nuget.org/packages/DockPanelSuite/3.1.1), with a `netcoreapp3.1` asset computed compatible with `net8.0-windows`, no NuGet dependencies, and MIT license (checked 2026-09-20). Upstream is [DockPanelSuite](https://github.com/dockpanelsuite/dockpanelsuite). | Retain the MIT notice. The upstream README says the project is seeking maintainers, so lifecycle behavior and future upgrades must remain behind the shell boundary. | `package` |
| DockPanelSuite VS2015 theme | Reference checkout uses 3.1.0. Selected package is [`DockPanelSuite.ThemeVS2015` 3.1.1](https://www.nuget.org/packages/DockPanelSuite.ThemeVS2015/3.1.1), MIT, `netcoreapp3.1`-compatible, dependent on DockPanelSuite >= 3.1.1 (checked 2026-09-20). | Retain the MIT notice. Include only while the selected shell actually uses this theme. | `package` |
| OpenTK WinForms host | Reference checkout uses OpenTK and OpenTK.GLControl 3.3.3. Selected host is [`OpenTK.GLControl` 4.0.2](https://www.nuget.org/packages/OpenTK.GLControl/4.0.2), MIT, `netcoreapp3.1`-compatible, dependent on OpenTK.Graphics and OpenTK.Windowing.Desktop >= 4.9.3 (checked 2026-09-20). The [official GLControl repository](https://github.com/opentk/GLControl) identifies it as the WinForms control for OpenTK 4.x. | Retain MIT notices. OpenTK 3 sample calls are not source-compatible assumptions for OpenTK 4; the real-context probe is the compatibility proof. | `package` |
| OpenTK runtime | Selected direct packages are [`OpenTK.Graphics` 4.9.4](https://www.nuget.org/packages/OpenTK.Graphics/4.9.4), [`OpenTK.Mathematics` 4.9.4](https://www.nuget.org/packages/OpenTK.Mathematics/4.9.4), and [`OpenTK.Windowing.Desktop` 4.9.4](https://www.nuget.org/packages/OpenTK.Windowing.Desktop/4.9.4), all MIT and compatible with `net8.0-windows` (checked 2026-09-20). The official [OpenTK FAQ](https://github.com/opentk/opentk.net/blob/docfx/faq.md) directs .NET Core users to OpenTK 4 and identifies the OpenTK 4 GLControl path. | Directly pinning Windowing.Desktop prevents GLControl's >= 4.9.3 lower bound from producing a mixed 4.9.3/4.9.4 managed graph. Do not use the broad `OpenTK` meta-package: it would also pull unused Audio and Compute packages. Retain the [OpenTK MIT license](https://github.com/opentk/opentk/blob/master/LICENSE.md) and native-library notices. | `package` |
| PDF library, current | `PDF_Manager` targets `netcoreapp3.1` and references `PdfSharpCore` 1.3.9. Its restore graph resolves SixLabors.Fonts 1.0.0-beta0013, SixLabors.ImageSharp 1.0.4, System.Numerics.Vectors 4.5.0, and System.Runtime.CompilerServices.Unsafe 4.7.0. The current [`PdfSharpCore` 1.3.67](https://www.nuget.org/packages/PdfSharpCore/1.3.67) remains a partial port and carries SharpZipLib, SixLabors.Fonts, and SixLabors.ImageSharp dependencies (checked 2026-09-20). | Do not make a blind 1.3.9-to-1.3.67 update. The package page calls out special Six Labors licensing terms for this distribution path. Existing PDF output and fonts must first be characterized with goldens. | `blocked` as production choice |
| PDF library, replacement candidate | [`PDFsharp` 6.2.4](https://www.nuget.org/packages/PDFsharp/6.2.4) is the maintained official package, MIT, supplies a `net8.0` asset, and depends on Microsoft.Extensions.Logging.Abstractions >= 8.0.3 and System.Security.Cryptography.Pkcs >= 8.0.1 (checked 2026-09-20). Upstream license: [PDFsharp MIT](https://github.com/empira/PDFsharp/blob/master/LICENSE). | Migrate only after PDF golden characterization; confirm Japanese/Chinese font resolution, images, pagination, and concurrency. Prefer the core `PDFsharp` package; add a GDI variant only if a characterized requirement proves it necessary. | `package`, conditional after goldens |
| .NET test host | No automated .NET test framework existed in FrameWeb3 before Step 0. Selected [`Microsoft.NET.Test.Sdk` 18.10.1](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.10.1) is MIT and compatible with `net8.0` (checked 2026-09-20). | Test-only graph resolves Microsoft.CodeCoverage, Microsoft.TestPlatform.ObjectModel, and Microsoft.TestPlatform.TestHost 18.10.1. These are not product redistribution dependencies. | `package` |
| .NET test framework | Selected [`xunit` 2.9.3](https://www.nuget.org/packages/xunit/2.9.3), metapackage over xunit.assert/core and analyzers, and [`xunit.runner.visualstudio` 3.1.5](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.5), Apache-2.0 (checked 2026-09-20). The adjacent reference checkout already uses xUnit 2.9.x. | Keep the adapter `PrivateAssets=all` with the standard restricted `IncludeAssets`; it must not flow to product consumers. xUnit runner 4 was not introduced because its new major/Microsoft Testing Platform migration is unrelated to Step 0. | `package` |

The final renderer-probe restore graph observed after alignment is:

| Role | Exact resolved packages |
|---|---|
| Direct | DockPanelSuite 3.1.1; DockPanelSuite.ThemeVS2015 3.1.1; OpenTK.GLControl 4.0.2; OpenTK.Graphics 4.9.4; OpenTK.Mathematics 4.9.4; OpenTK.Windowing.Desktop 4.9.4 |
| Transitive | OpenTK.Core 4.9.4; OpenTK.Windowing.Common 4.9.4; OpenTK.Windowing.GraphicsLibraryFramework 4.9.4; OpenTK.redist.glfw 3.4.0.44; System.Runtime.CompilerServices.Unsafe 6.0.0 |

The final test restore graph is Microsoft.NET.Test.Sdk 18.10.1,
xunit 2.9.3, and xunit.runner.visualstudio 3.1.5 directly; it resolves
Microsoft.CodeCoverage/ObjectModel/TestHost 18.10.1, xunit.abstractions 2.0.3,
xunit.analyzers 1.18.0, and the xUnit 2.9.3 core/assert/execution packages.

### Read-only `isasPrint` reference

The reference checkout has origin `https://github.com/sasaco/isasPrint.git` and
was inspected at commit `83935204002c9862c694f7165b5b154131e66f26`.
There is no repository-root license file. Its `THREE/provenance.json` records:

- upstream `https://github.com/hjoykim/THREE`;
- upstream commit `5cc300df898478b84856d8b825b38a3d5e684129`;
- MIT license;
- 289 C# source files; and
- digest `59398ce32010c0d0881a7e1b85d3ffc55258ffaa1254542ea9adc24907fb9487`.

A fresh read-only recomputation found the same 289 source files and the same
digest. The recorded upstream commit has an [MIT license](https://github.com/hjoykim/THREE/blob/5cc300df898478b84856d8b825b38a3d5e684129/LICENSE).
However, the provenance digest covers the C# source set only. It does not clear
the shader, font, image, project, or example-host material described below.

| Group | Exact observed material and provenance | Compatibility / redistribution concern | Decision |
|---|---|---|---|
| Vendored THREE C# source | 289 C# files pinned by the verified digest above; old project targets .NET Framework 4.8 and OpenTK 3.3.3. | Source provenance is strong for the pinned C# set, but importing all 289 files would preserve an obsolete OpenTK API, broad unused functionality, and weak renderer lifetime patterns. `SelectFrame/Three/Example.cs` also says it refers to `lathoub/three.cs`, an extra provenance edge not represented in `provenance.json`. | `reference architecture only` |
| Reusable behavior subset | Scene/model/result layer separation; Z-up perspective/orthographic cameras; fit/home; stable selection identities; ray hit testing; and explicit viewport capture are the useful behaviors. | These are architectural ideas, not copied source. Implement only the minimum required types behind the owned renderer contract and OpenTK 4 packages. | `rewrite` |
| Shader tree | 134 `.glsl` files. The layout overlaps upstream THREE, but the local C# provenance digest excludes them. Individual files/chunks include additional attributions and third-party algorithms. | There is no local shader manifest tying every byte to a source revision and notice set. The MVP needs only simple solid-color geometry and selection shaders. | `rewrite`; legacy copy `blocked` |
| Texture/LTC images | `ltc_1.png`, 4,125 bytes, SHA-256 `DD3395DF99A52CE34961EC1BA7000EE7DFA5CBB1F9A8B93BE266A3F3CDDECE24`; `ltc_2.png`, 2,713 bytes, SHA-256 `C263406898279B83BC3A1EEF8300B005BDAEF22D7E48D0AE05D01CA718E2E2BA`. | No standalone source/version/license record was found, and these lookup textures are unnecessary for the structural line/point MVP. | `blocked`; exclude |
| Example/host code | `ISASPrint/SelectFrame/Three/Example.cs` wires the GLRenderer/GLControl lifecycle; the application then composes Scene, cameras, controls, lights, ray casting, primitives, and selection. | Treat only as behavioral reference. Do not copy its continuous idle rendering, event wiring, disposal behavior, or OpenTK 3 calls. | `reference architecture only` |

The old `THREE` project package graph is intentionally **not** carried forward:

| Observed reference package | Exact version; official evidence (checked 2026-09-20) | Framework / license and Step 0 treatment |
|---|---|---|
| MIConvexHull | [1.1.19.1019](https://www.nuget.org/packages/MIConvexHull/1.1.19.1019) | Supplies a .NET Standard 1.0 asset, has no NuGet dependencies, and links its upstream license in package metadata. Exclude. Re-audit the license/current support only if a future geometry requirement needs it. |
| Newtonsoft.Json | [13.0.3](https://www.nuget.org/packages/Newtonsoft.Json/13.0.3) | MIT; its .NET Standard/.NET Framework assets are compatible but it is unnecessary in Rendering. Core serialization decisions belong to the contract layer. |
| OpenTK / OpenTK.GLControl | [3.3.3](https://www.nuget.org/packages/OpenTK/3.3.3) / [3.3.3](https://www.nuget.org/packages/OpenTK.GLControl/3.3.3) | MIT-era OpenTK 3 assets used by the .NET Framework reference; GLControl depends on OpenTK 3.3.3. Replace both with the pinned OpenTK 4 graph above. |
| Pfim | [0.11.1](https://www.nuget.org/packages/Pfim/0.11.1) | Package metadata links the upstream license; the legacy project used it as an image decoder. Exclude because the MVP needs no DDS loader. Re-audit before any later introduction. |
| StbImageSharp | [2.27.13](https://www.nuget.org/packages/StbImageSharp/2.27.13) | Package metadata identifies an Unlicense-or-MIT choice and compatible managed assets. Exclude because the MVP needs no texture decoder. |
| WindowsBase | [4.6.1055](https://www.nuget.org/packages/WindowsBase/4.6.1055) | Old Microsoft compatibility package used by the .NET Framework project. Exclude; .NET 8 Windows desktop supplies its own framework reference and must not inherit this package. |

These exclusions avoid transferring unused transitive license and native-runtime
obligations. Any later proposal to add one is a new dependency decision, not an
implicit benefit of the legacy source.

### Font and typeface assets

#### Current `PDF_Manager` embedded binaries

| File | Observed identity | SHA-256 | Redistribution status |
|---|---|---|---|
| `fonts/MS Gothic.ttf` | MS Gothic v2.00; Ricoh/Ryobi copyright metadata; 4,188,429 bytes | `8F9C1AC99538D03E19F298F0B1F4AE5ADCE2D16FC17E57B450ED6521091AD09D` | `blocked` |
| `fonts/MS Mincho.ttf` | MS Mincho v2.30; Ricoh/Ryobi copyright metadata; 9,099,596 bytes | `AD45BB5C7DF9BFA8C1D3F5E4CB89B279AEA0DB3619940B8DC5D8064CBB578CE2` | `blocked` |
| `fonts/simsun.ttf` | SimSun v2.10; ZhongYi copyright metadata; 10,499,104 bytes | `CA4DA082CD970F0C8ABAA79F213DDCBC475F7B5AFABCB81B385998F9EBFBB53F` | `blocked` |

Microsoft's [font redistribution FAQ](https://learn.microsoft.com/en-us/typography/fonts/font-faq)
states that most Windows-supplied fonts may not be redistributed with an
application merely because they are installed on Windows; document embedding is
a distinct permission controlled by the font's embedding bits. The official
[MS Gothic](https://learn.microsoft.com/en-us/typography/font-list/ms-gothic),
[MS Mincho](https://learn.microsoft.com/en-us/typography/font-list/ms-mincho),
and [SimSun](https://learn.microsoft.com/en-us/typography/font-list/simsun)
pages point to that redistribution guidance (checked 2026-09-20). No separate
commercial redistribution license was found in either repository. Therefore
these files must not be included in an application publish or installer.

Use installed system fonts without redistributing their binaries, or choose a
specific redistributable font from its authoritative release and include its
full license/notice. The latter choice must be tested for PDF embedding and CJK
coverage; [Noto's official documentation](https://github.com/notofonts/noto-docs/blob/main/docs/website/use.md)
identifies Noto fonts as OFL-licensed, but no font is automatically approved
until its exact artifact/version and notice are recorded.

#### `isasPrint/THREE` typeface JSON assets

| File | Size / SHA-256 | Embedded metadata | Decision |
|---|---|---|---|
| `TimesNewRomanRegular.json` | 37,306,813 / `FC022B7891190ADA503B9A0E780854BFEB715C49F177603C5B5CF03C8669EAB0` | Internally identifies FangSong/ZhongYi and says Microsoft-supplied font data has restricted use; the filename is misleading. | `blocked`; do not copy |
| `Roboto_Regular.json` | 330,717 / `A59A4496619ECBCDF40B013BB236714F41666E784443CC20C0ADFE9E9A2E7702` | Apache-2.0 text in JSON metadata. | `reference architecture only`; exact converter/source chain and notice set are not recorded |
| `optimer_regular.typeface.json` | 110,666 / `17E5E14D94D66E0E60EED3BFDE403FCFA3E677F3C914E4CBED264C0C0BA95175` | MgOpen terms in JSON metadata. | `reference architecture only`; do not copy without source/version/notice chain |
| `Noto Sans JP_Regular.json` | 21,139,323 / `271375C7E546214FF62DC8073F147C8DB2952BF965EDFB7ECAB0D0ABFBFFB225` | SIL OFL 1.1 metadata. | `reference architecture only`; use an authoritative font release instead if selected |
| `Noto Sans JP SemiBold_Regular.json` | 21,120,865 / `B91148CE97BE9C36ED8DBCC7CB31F21E7E12E06C4FB757D08A22BED89A8AA8A9` | SIL OFL 1.1 metadata. | `reference architecture only`; use an authoritative font release instead if selected |
| `helvetiker_regular.typeface.json` | 63,182 / `D5C5467690F74061179A292AF83BD85C4C551E0F106B2AF99714F11184C96981` | MgOpen terms in JSON metadata. An official [three.js counterpart](https://github.com/mrdoob/three.js/blob/dev/examples/fonts/helvetiker_regular.typeface.json) carries those terms. | `reference architecture only`; local source revision is not pinned |
| `gentilis_regular.typeface.json` | 627,529 / `7ED95F2FAA30F59DBE7CFB145B97C42A6BA1188CD2EEC01CA61485E8C83EE9DE` | SIL OFL 1.1 metadata. | `reference architecture only`; local source revision is not pinned |

The MVP/probe should render no text in OpenGL and ship none of these converted
typeface JSON files. Later labels should use a WinForms/system text overlay or a
separately approved OFL font whose exact upstream artifact and license notice
are committed together.

### Absolute and commercial references

The reference `ISASPrint.csproj` contains direct DLL `HintPath` references,
paths below `C:/Program Files (x86)/ComponentOne`, and a machine-specific
publish path. It also references ComponentOne C1Pdf, C1Report, C1Chart, and
related assemblies. None is portable, provenance-complete, or authorized for
this application. **No ComponentOne assembly, absolute binary reference,
legacy packages directory, or machine-specific publish path may be introduced.**

## Recommendations (not new facts)

1. Keep the renderer dependency boundary exactly at DockPanelSuite 3.1.1,
   ThemeVS2015 3.1.1 only when used, GLControl 4.0.2, and the three direct
   OpenTK 4.9.4 packages listed above. Commit a NuGet lock or otherwise inspect
   the resolved graph at release time.
2. Implement a minimal owned shader/renderer: solid-color lines, points and
   simple faces; model/load/result scene layers; selection ID/ray hit testing;
   explicit UI-thread context lifetime; render on invalidation; deterministic
   capture. Do not vendor THREE.
3. Keep the Step 0 test stack at Microsoft.NET.Test.Sdk 18.10.1, xunit 2.9.3,
   and xunit.runner.visualstudio 3.1.5 with the runner private.
4. Characterize PDF output before replacing PdfSharpCore 1.3.9. If goldens pass,
   migrate to official PDFsharp 6.2.4 and record its resolved graph.
5. Remove the three restricted font binaries from eventual publish input.
   Choose either installed-font lookup or one exact redistributable CJK font
   artifact, and test embedding/subsetting under its license.
6. Add a publish-artifact license/SBOM check before changing the final gate to
   GO. It must prove that legacy fonts, typeface JSON, LTC PNGs, shaders,
   ComponentOne assemblies, and absolute-path binaries are absent.
7. Plan a .NET 10 LTS rebaseline before .NET 8 reaches end of support; this is
   follow-up maintenance and does not alter the approved Step 0 target.

## Unresolved blockers

| Blocker | Resolution required |
|---|---|
| Existing `PDF_Manager` embeds three fonts without redistribution grants. | Remove them from application distribution and prove an installed-font or explicitly licensed replacement strategy with PDF goldens. |
| `isasPrint/THREE` shader files and LTC images are outside the verified C# provenance digest. | Exclude them. If any is later necessary, establish exact upstream revision, license, notice, and byte mapping before import. |
| Converted legacy typeface JSON files lack a complete source/conversion/version/notice chain; one contains explicitly restricted FangSong data. | Exclude all. Never import `TimesNewRomanRegular.json`; source any future font from an authoritative upstream release. |
| PDFsharp migration behavior has not yet been characterized. | Keep the replacement conditional until representative A3/A4, CJK, table, diagram, image, and concurrency goldens pass. |
| Final publish contents do not yet exist. | Run a publish SBOM/license and forbidden-file scan before redistribution sign-off. |

## Step 0 redistribution gate

| Distribution surface | Result | Rationale / allowed next action |
|---|---|---|
| Package-only renderer probe | **GO** | Maintained, version-pinned MIT packages; no legacy source/assets. The probe may continue. |
| Core contract tests | **GO** | Version-pinned test-only packages; runner is private and does not enter the product. |
| Copy `isasPrint/THREE` C# tree | **NO-GO** | Use architecture only; an owned OpenTK 4 renderer is smaller and avoids obsolete/lifecycle baggage. |
| Copy legacy shaders | **NO-GO** | Asset-level provenance/notice set is incomplete; write minimal owned shaders. |
| Copy legacy typeface JSON or LTC PNGs | **NO-GO** | Incomplete provenance, and one font payload carries explicit restrictions. Exclude them. |
| Ship current `PDF_Manager` embedded fonts | **NO-GO** | No app-redistribution right is established. |
| Adopt ComponentOne or machine-absolute DLL references | **NO-GO** | Explicitly excluded and non-portable. |
| Migrate printing to PDFsharp 6.2.4 | **CONDITIONAL GO** | Package/license are acceptable; behavior and replacement-font goldens remain mandatory. |
| Ship the complete desktop application | **NO-GO** | Do not change to GO until every blocker above is resolved or excluded and publish output is verified. |

This final NO-GO is intentionally conservative: package-based implementation
can proceed, but unresolved legacy source/asset provenance is not treated as a
redistribution approval.
