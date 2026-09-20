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
