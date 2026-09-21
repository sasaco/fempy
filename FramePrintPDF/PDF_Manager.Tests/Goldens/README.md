# Typed PDF rendered-page golden

`typed-pdf-page.gray8.deflate-base64` is the zlib-compressed Base64 form of the complete
595 x 842 Gray8 page raster produced by `PdfSubsetPageRasterizer`. The rasterizer independently
starts at `startxref`, validates the xref/trailer, follows Root -> Catalog -> Pages/Kids -> Page,
resolves the page's Font/XObject resources and Contents reference, and interprets the owned
`q`/`Q`, `cm`, named `Do`, and text operators. Image placement and existence therefore come from
the emitted PDF page graph and content stream, not object-number or coordinate constants. It
fails closed on missing references, unsupported operators, unbalanced state, or malformed streams.

The rasterizer uses a repository-owned 5 x 7 glyph set for deterministic layout coverage. It
therefore validates page composition and viewport pixels, but it is not a Helvetica glyph-shape
oracle. `TypedPdfDocumentWriter` deliberately remains dependency-free and replaces unsupported
non-ASCII text, including CJK, with `?`; this golden must not be cited as Unicode font coverage.

Regeneration requires explicit UI-owner approval:

1. Run `TypedPdfExporterTests.WriteAsync_WritesDeterministicSinglePagePdfWithCaptureAndResultTable`.
2. On a mismatch, inspect the emitted `%TEMP%\frameweb-typed-pdf-page-candidate.pgm` as a Gray8
   image and confirm the model capture, headings, table placement, and page bounds.
3. After approval, obtain `RasterizedPdfPage.ToCompressedBase64()` for that inspected raster and
   replace the single line in the golden file through the normal reviewed patch workflow.
4. Rerun the focused test twice and confirm both deterministic PDF bytes and raster bytes.

`RenderedGoldenRasterizer_FollowsActualPageResourcesAndContentOperators` is the independence
guard. It mutates the emitted PDF without changing xref offsets and proves that removing
`/Viewport Do` or its `cm`, breaking the XObject mapping, or corrupting the image stream fails;
changing the `cm` layout or supplying different valid image pixels produces a different raster.

The current golden was approved for the Step 4 remediation representative portal-frame report.

## Step 8 PDFsharp rendered pages

`step8-a4-table-page.gray8.deflate-base64` and
`step8-a3-diagram-page.gray8.deflate-base64` are the approved 595 x 842 A4 table page and
1191 x 842 A3 landscape diagram page. `PdfSharpSubsetPageRasterizer` resolves each emitted page
and decoded content stream, independently interprets the bounded PDFsharp operator subset, and
paints decoded RGB image samples. Unsupported operators, unbalanced state, absent images,
malformed dimensions, and corrupt image streams fail closed.

The raster intentionally uses deterministic text-coverage strokes instead of the installed font's
glyph outline. Unicode renderability is covered separately by embedded-font and ToUnicode tests;
these goldens cover page composition, repeated table rows/header geometry, diagram pixels and
placement, margins, and footer placement. On mismatch, inspect the emitted candidate PGM at its
original size. Replacing either Base64 fixture requires explicit print/UI-owner approval.

The 2026-09-21 remediation refresh was approved after original-resolution inspection. The shared
scaled layout metrics moved only the centered footer rectangle from the former hard-coded 12-point
height to `PageFooterHeightPoints` (16 points at 100% scale). Each raster changed exactly 88 pixels:
the A4 delta is confined to x=277..320, y=805..809 and the A3 delta to x=575..618, y=791..795.
Table headers/rows, diagram pixels and placement, page margins, and all non-footer pixels remained
byte-identical to the prior approved images.

The second-final 2026-09-21 text-layout remediation changed only the A4 fixture; the A3 diagram
fixture remained byte-identical and was not regenerated. Original-resolution inspection approved
the A4 candidate after the PDF and preview renderers began consuming the same fitted `TextRuns`
with explicit per-run rectangular clipping. The delta was 88 / 500,990 pixels (0.017565%),
absolute-difference sum 19,624, maximum delta 223, bounding box x=30..389 and y=99..787. The
changes are sparse one-pixel text-coverage baseline differences across the title, table rows, and
footer. Margins, the complete grid, repeated header, every row, title, and centered footer remain
intact.
