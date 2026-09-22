using System.Text;
using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed class TypedPdfExporterTests
{
    [Fact]
    public async Task WriteAsync_WritesDeterministicSinglePagePdfWithCaptureAndResultTable()
    {
        (PdfModelNode[] nodes, PdfModelMember[] members) = CreatePortalFrame();
        ViewportCapture capture = ModelViewportCapture.Create(nodes, members, ["N1", "N2"], ["N3"]);
        TypedPdfJob job = new(
            "Portal frame",
            supportCount: 2,
            loadCaseCount: 1,
            nodes,
            members,
            capture,
            [new PdfResultRow("D", "N3", 0.001, 0, -0.002)],
            "Case: LC1 / Static");
        TypedPdfDocumentWriter writer = new();

        byte[] first = await WriteAsync(writer, job);
        byte[] second = await WriteAsync(writer, job);

        Assert.Equal(first, second);
        Assert.True(first.Length > 1_000);
        string pdf = Encoding.Latin1.GetString(first);
        Assert.StartsWith("%PDF-1.4", pdf, StringComparison.Ordinal);
        Assert.Contains("/Type /Catalog", pdf, StringComparison.Ordinal);
        Assert.Contains("/Type /Page ", pdf, StringComparison.Ordinal);
        Assert.Contains("/Count 1", pdf, StringComparison.Ordinal);
        Assert.Contains("/Subtype /Image", pdf, StringComparison.Ordinal);
        Assert.Contains("FrameWeb Model Report", pdf, StringComparison.Ordinal);
        Assert.Contains("Project: Portal frame", pdf, StringComparison.Ordinal);
        Assert.Contains("Nodes: 4   Members: 3", pdf, StringComparison.Ordinal);
        Assert.Contains("Case: LC1 / Static", pdf, StringComparison.Ordinal);
        Assert.Contains("D N3", pdf, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", pdf, StringComparison.Ordinal);
        AssertValidCrossReferenceTable(first, objectCount: 6);

        RasterizedPdfPage rendered = PdfSubsetPageRasterizer.Render(first);
        Assert.Equal((595, 842), (rendered.Width, rendered.Height));
        Assert.Contains(rendered.Gray8, value => value < 240);
        AssertApprovedRenderedPage(rendered);
    }

    [Fact]
    public async Task RenderedGoldenRasterizer_FollowsActualPageResourcesAndContentOperators()
    {
        (PdfModelNode[] nodes, PdfModelMember[] members) = CreatePortalFrame();
        ViewportCapture capture = ModelViewportCapture.Create(nodes, members, ["N1", "N2"], ["N3"]);
        TypedPdfJob job = new(
            "Portal frame",
            supportCount: 2,
            loadCaseCount: 1,
            nodes,
            members,
            capture,
            [new PdfResultRow("D", "N3", 0.001, 0, -0.002)],
            "Case: LC1 / Static");
        byte[] original = await WriteAsync(new TypedPdfDocumentWriter(), job);
        RasterizedPdfPage baseline = PdfSubsetPageRasterizer.Render(original);
        AssertApprovedRenderedPage(baseline);

        byte[] missingDo = ReplaceAsciiOnce(
            original,
            "/Viewport Do",
            new string(' ', "/Viewport Do".Length));
        Assert.Throws<InvalidDataException>(() => PdfSubsetPageRasterizer.Render(missingDo));

        const string imageMatrix = "500 0 0 250 48 495 cm";
        byte[] missingMatrix = ReplaceAsciiOnce(original, imageMatrix, new string(' ', imageMatrix.Length));
        Assert.Throws<InvalidDataException>(() => PdfSubsetPageRasterizer.Render(missingMatrix));

        byte[] changedMatrix = ReplaceAsciiOnce(original, imageMatrix, "400 0 0 250 48 495 cm");
        RasterizedPdfPage moved = PdfSubsetPageRasterizer.Render(changedMatrix);
        Assert.False(baseline.Gray8.AsSpan().SequenceEqual(moved.Gray8));

        byte[] brokenResource = ReplaceAsciiOnce(original, "/Viewport 6 0 R", "/Viewport 9 0 R");
        Assert.Throws<InvalidDataException>(() => PdfSubsetPageRasterizer.Render(brokenResource));

        byte[] corruptImage = CorruptImageStream(original);
        Assert.Throws<InvalidDataException>(() => PdfSubsetPageRasterizer.Render(corruptImage));

        byte[] changedPixels = capture.Rgb24.ToArray();
        Array.Fill(changedPixels, (byte)0, 0, Math.Min(changedPixels.Length, capture.Width * 3));
        TypedPdfJob changedImageJob = new(
            "Portal frame",
            supportCount: 2,
            loadCaseCount: 1,
            nodes,
            members,
            new ViewportCapture(capture.Width, capture.Height, changedPixels),
            [new PdfResultRow("D", "N3", 0.001, 0, -0.002)],
            "Case: LC1 / Static");
        RasterizedPdfPage changedImage = PdfSubsetPageRasterizer.Render(
            await WriteAsync(new TypedPdfDocumentWriter(), changedImageJob));
        Assert.False(baseline.Gray8.AsSpan().SequenceEqual(changedImage.Gray8));
    }

    [Fact]
    public void ModelViewportCapture_IsBoundedAndSelectionChangesPixels()
    {
        (PdfModelNode[] nodes, PdfModelMember[] members) = CreatePortalFrame();

        ViewportCapture plain = ModelViewportCapture.Create(nodes, members, width: 160, height: 100);
        ViewportCapture highlighted = ModelViewportCapture.Create(
            nodes,
            members,
            selectedNodeIds: ["N3"],
            width: 160,
            height: 100);

        Assert.Equal(160 * 100 * 3, plain.Rgb24.Length);
        Assert.False(plain.Rgb24.Span.SequenceEqual(highlighted.Rgb24.Span));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ViewportCapture(ViewportCapture.MaximumDimension + 1, 1, ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public async Task WriteAsync_HonorsOwnedCancellation()
    {
        (PdfModelNode[] nodes, PdfModelMember[] members) = CreatePortalFrame();
        TypedPdfJob job = new(
            "Portal frame",
            0,
            0,
            nodes,
            members,
            ModelViewportCapture.Create(nodes, members));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new TypedPdfDocumentWriter().WriteAsync(job, new MemoryStream(), cancellation.Token));
    }

    [Fact]
    public void TypedPdfJob_RejectsOversizedFreeTextAndIdentifiers()
    {
        (PdfModelNode[] nodes, PdfModelMember[] members) = CreatePortalFrame();
        ViewportCapture capture = ModelViewportCapture.Create(nodes, members);

        Assert.Throws<ArgumentException>(() => new TypedPdfJob(
            new string('P', TypedPdfJob.MaximumTextLength + 1),
            0,
            0,
            nodes,
            members,
            capture));
        Assert.Throws<ArgumentException>(() => new TypedPdfJob(
            "Portal frame",
            0,
            0,
            [new PdfModelNode(new string('N', TypedPdfJob.MaximumIdentifierLength + 1), 0, 0, 0)],
            [],
            capture));
        Assert.Throws<ArgumentException>(() => new TypedPdfJob(
            "Portal frame",
            0,
            0,
            nodes,
            members,
            capture,
            resultCaption: new string('R', TypedPdfJob.MaximumTextLength + 1)));
    }

    [Fact]
    public void TypedPdfJob_StopsEnumeratingAtTheConfiguredLimit()
    {
        int enumerationCount = 0;
        IEnumerable<PdfModelNode> nodes = CountedNodes(
            TypedPdfJob.MaximumNodes + 100,
            () => enumerationCount++);

        Assert.Throws<ArgumentException>(() => new TypedPdfJob(
            "Bounded",
            0,
            0,
            nodes,
            [],
            new ViewportCapture(1, 1, new byte[3])));
        Assert.Equal(TypedPdfJob.MaximumNodes + 1, enumerationCount);
    }

    [Fact]
    public void ModelViewportCapture_RejectsDimensionsBeforeEnumeratingOrAllocating()
    {
        bool enumerated = false;
        IEnumerable<PdfModelNode> nodes = ThrowIfEnumerated(() => enumerated = true);

        Assert.Throws<ArgumentOutOfRangeException>(() => ModelViewportCapture.Create(
            nodes,
            [],
            width: int.MaxValue,
            height: 1));
        Assert.False(enumerated);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ViewportCapture.GetRequiredByteLength(ViewportCapture.MaximumDimension, int.MaxValue));
    }

    [Fact]
    public async Task WriteAsync_UsesDocumentedQuestionMarkFallbackForCjkText()
    {
        (PdfModelNode[] nodes, PdfModelMember[] members) = CreatePortalFrame();
        TypedPdfJob job = new(
            "架構",
            0,
            0,
            nodes,
            members,
            ModelViewportCapture.Create(nodes, members));

        string pdf = Encoding.Latin1.GetString(await WriteAsync(new TypedPdfDocumentWriter(), job));

        Assert.Contains("Project: ??", pdf, StringComparison.Ordinal);
    }

    private static async Task<byte[]> WriteAsync(TypedPdfDocumentWriter writer, TypedPdfJob job)
    {
        await using MemoryStream stream = new();
        await writer.WriteAsync(job, stream);
        return stream.ToArray();
    }

    private static void AssertValidCrossReferenceTable(byte[] bytes, int objectCount)
    {
        string pdf = Encoding.Latin1.GetString(bytes);
        const string startXrefMarker = "startxref\n";
        int markerIndex = pdf.LastIndexOf(startXrefMarker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0);
        int offsetStart = markerIndex + startXrefMarker.Length;
        int offsetEnd = pdf.IndexOf('\n', offsetStart);
        int xrefOffset = int.Parse(pdf[offsetStart..offsetEnd], System.Globalization.CultureInfo.InvariantCulture);
        Assert.StartsWith($"xref\n0 {objectCount + 1}\n", pdf[xrefOffset..], StringComparison.Ordinal);

        string[] entries = pdf[(xrefOffset + $"xref\n0 {objectCount + 1}\n".Length)..]
            .Split('\n', objectCount + 2, StringSplitOptions.None);
        Assert.Equal("0000000000 65535 f ", entries[0]);
        for (int objectNumber = 1; objectNumber <= objectCount; objectNumber++)
        {
            int objectOffset = int.Parse(entries[objectNumber][..10], System.Globalization.CultureInfo.InvariantCulture);
            Assert.StartsWith($"{objectNumber} 0 obj\n", pdf[objectOffset..], StringComparison.Ordinal);
        }
    }

    private static void AssertApprovedRenderedPage(RasterizedPdfPage actual)
    {
        string goldenPath = Path.Combine(AppContext.BaseDirectory, "Goldens", "typed-pdf-page.gray8.deflate-base64");
        if (!File.Exists(goldenPath))
        {
            Assert.Fail($"Missing rendered-page golden. Candidate base64: {actual.ToCompressedBase64()}");
        }

        byte[] expected = PdfSubsetPageRasterizer.DecodeCompressedBase64(
            File.ReadAllText(goldenPath),
            actual.Gray8.Length);
        if (!expected.AsSpan().SequenceEqual(actual.Gray8))
        {
            string candidatePath = Path.Combine(Path.GetTempPath(), "frameweb-typed-pdf-page-candidate.pgm");
            File.WriteAllBytes(candidatePath, actual.ToPortableGraymap());
            Assert.Fail($"Rendered PDF page differs from the owner-approved golden. Inspect: {candidatePath}");
        }
    }

    private static byte[] ReplaceAsciiOnce(byte[] source, string expected, string replacement)
    {
        Assert.Equal(expected.Length, replacement.Length);
        byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
        int index = source.AsSpan().IndexOf(expectedBytes);
        Assert.True(index >= 0, $"Expected PDF token was not found: {expected}");
        Assert.Equal(-1, source.AsSpan(index + expectedBytes.Length).IndexOf(expectedBytes));
        byte[] result = source.ToArray();
        Encoding.ASCII.GetBytes(replacement).CopyTo(result, index);
        return result;
    }

    private static byte[] CorruptImageStream(byte[] source)
    {
        byte[] result = source.ToArray();
        string pdf = Encoding.Latin1.GetString(result);
        int image = pdf.IndexOf("/Subtype /Image", StringComparison.Ordinal);
        Assert.True(image >= 0);
        int stream = pdf.IndexOf("stream\n", image, StringComparison.Ordinal);
        Assert.True(stream >= 0);
        int payload = stream + "stream\n".Length;
        result[payload + 8] ^= 0x5A;
        return result;
    }

    private static IEnumerable<PdfModelNode> CountedNodes(int count, Action enumerated)
    {
        for (int index = 0; index < count; index++)
        {
            enumerated();
            yield return new PdfModelNode($"N{index}", index, 0, 0);
        }
    }

    private static IEnumerable<PdfModelNode> ThrowIfEnumerated(Action enumerated)
    {
        enumerated();
        yield return new PdfModelNode("N1", 0, 0, 0);
    }

    private static (PdfModelNode[] Nodes, PdfModelMember[] Members) CreatePortalFrame() =>
        (
            [
                new PdfModelNode("N1", 0, 0, 0),
                new PdfModelNode("N2", 6, 0, 0),
                new PdfModelNode("N3", 0, 0, 4),
                new PdfModelNode("N4", 6, 0, 4),
            ],
            [
                new PdfModelMember("M1", "N1", "N3"),
                new PdfModelMember("M2", "N3", "N4"),
                new PdfModelMember("M3", "N4", "N2"),
            ]);
}
