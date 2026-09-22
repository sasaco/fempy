using System.Text;
using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed class Step8RenderedPdfGoldenTests
{
    [Fact]
    public async Task A4TablePage_RasterMatchesOwnerApprovedGolden()
    {
        PrintTableRow[] rows = Enumerable.Range(1, 100)
            .Select(index => new PrintTableRow([index.ToString(), $"N-{index}", $"{index * 0.125:F3}"]))
            .ToArray();
        PrintJob job = new(
            "A4 table golden",
            PrintPageSettings.CreateA4(),
            PrintTextLanguage.English,
            [new PrintTableSection(new PrintTable(
                "Input nodes",
                [new PrintTableColumn("ID"), new PrintTableColumn("Node"), new PrintTableColumn("X")],
                rows))]);

        byte[] pdf = await ExportAsync(job);
        RasterizedPdfPage rendered = PdfSharpSubsetPageRasterizer.Render(pdf, pageIndex: 0, requireImage: false);

        Assert.Equal((595, 842), (rendered.Width, rendered.Height));
        Assert.Contains(rendered.Gray8, value => value < 64);
        AssertApproved("step8-a4-table-page.gray8.deflate-base64", rendered);
    }

    [Fact]
    public async Task A3DiagramPage_RasterMatchesOwnerApprovedGoldenAndCorruptionFailsClosed()
    {
        const int imageWidth = 32;
        const int imageHeight = 24;
        byte[] rgb = new byte[ViewportCapture.GetRequiredByteLength(imageWidth, imageHeight)];
        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                int offset = ((y * imageWidth) + x) * 3;
                rgb[offset] = (byte)(x * 7);
                rgb[offset + 1] = (byte)(y * 9);
                rgb[offset + 2] = (byte)((x + y) * 4);
            }
        }

        PrintJob job = new(
            "A3 diagram golden",
            PrintPageSettings.CreateA3(PrintPageOrientation.Landscape, uniformMarginMillimetres: 15),
            PrintTextLanguage.English,
            [new PrintDiagramSection(new PrintDiagram(
                "Result diagram",
                new ViewportCapture(imageWidth, imageHeight, rgb),
                PrintDiagramKind.Result,
                preferredHeightMillimetres: 160))]);
        byte[] pdf = await ExportAsync(job);
        RasterizedPdfPage rendered = PdfSharpSubsetPageRasterizer.Render(pdf, pageIndex: 0, requireImage: true);

        Assert.Equal((1_191, 842), (rendered.Width, rendered.Height));
        Assert.Contains(rendered.Gray8, value => value is > 32 and < 224);
        AssertApproved("step8-a3-diagram-page.gray8.deflate-base64", rendered);
        Assert.ThrowsAny<Exception>(() =>
            PdfSharpSubsetPageRasterizer.Render(CorruptImageStream(pdf), pageIndex: 0, requireImage: true));
    }

    private static async Task<byte[]> ExportAsync(PrintJob job)
    {
        using MemoryStream output = new();
        await new TypedPdfDocumentWriter().WriteAsync(job, output);
        return output.ToArray();
    }

    private static void AssertApproved(string fileName, RasterizedPdfPage actual)
    {
        string goldenPath = Path.Combine(AppContext.BaseDirectory, "Goldens", fileName);
        if (!File.Exists(goldenPath))
        {
            string candidateBase = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(fileName));
            string imagePath = candidateBase + ".pgm";
            string base64Path = candidateBase + ".txt";
            File.WriteAllBytes(imagePath, actual.ToPortableGraymap());
            File.WriteAllText(base64Path, actual.ToCompressedBase64());
            Assert.Fail($"Missing owner-approved golden. Inspect {imagePath}; candidate data: {base64Path}");
        }

        byte[] expected = PdfSubsetPageRasterizer.DecodeCompressedBase64(
            File.ReadAllText(goldenPath),
            actual.Gray8.Length);
        if (!expected.AsSpan().SequenceEqual(actual.Gray8))
        {
            string candidateBase = Path.Combine(
                Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(fileName)}-candidate");
            string candidatePath = candidateBase + ".pgm";
            string candidateBase64Path = candidateBase + ".txt";
            File.WriteAllBytes(candidatePath, actual.ToPortableGraymap());
            File.WriteAllText(candidateBase64Path, actual.ToCompressedBase64());
            Assert.Fail(
                $"Rendered PDF page differs from the owner-approved golden. " +
                $"{DescribeDifference(expected, actual)} Inspect: {candidatePath}; candidate data: {candidateBase64Path}");
        }
    }

    private static string DescribeDifference(byte[] expected, RasterizedPdfPage actual)
    {
        int changed = 0;
        long absoluteDifference = 0;
        int maximumDifference = 0;
        int minX = actual.Width;
        int minY = actual.Height;
        int maxX = -1;
        int maxY = -1;
        for (int index = 0; index < expected.Length; index++)
        {
            int difference = Math.Abs(expected[index] - actual.Gray8[index]);
            if (difference == 0)
            {
                continue;
            }

            changed++;
            absoluteDifference += difference;
            maximumDifference = Math.Max(maximumDifference, difference);
            int x = index % actual.Width;
            int y = index / actual.Width;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        double percent = changed * 100d / expected.Length;
        return FormattableString.Invariant(
            $"changed={changed}/{expected.Length} ({percent:F6}%), abs-sum={absoluteDifference}, max-delta={maximumDifference}, bbox=x{minX}..{maxX},y{minY}..{maxY}.");
    }

    private static byte[] CorruptImageStream(byte[] source)
    {
        byte[] result = source.ToArray();
        string text = Encoding.Latin1.GetString(result);
        int image = text.IndexOf("/Subtype/Image", StringComparison.Ordinal);
        Assert.True(image >= 0);
        int stream = text.IndexOf("stream", image, StringComparison.Ordinal);
        Assert.True(stream >= 0);
        int payload = text.IndexOf('\n', stream) + 1;
        Assert.True(payload > stream && payload + 8 < result.Length);
        result[payload + 8] ^= 0x5A;
        return result;
    }
}
