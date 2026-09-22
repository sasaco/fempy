using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed partial class Step8PdfExportAcceptanceTests
{
    [Fact]
    public async Task ExportedA4Document_ParsesWithExactPagesGeometrySectionsAndRepeatedHeaders()
    {
        PrintJob job = CreateComprehensiveJob(PrintTextLanguage.English);
        TypedPdfDocumentWriter writer = new();
        using MemoryStream output = new();

        PrintExportResult result = await writer.WriteAsync(job, output);

        Assert.Equal(3, result.Plan.PageCount);
        Assert.Equal(output.Length, result.EncodedByteCount);
        Assert.Equal([44, 45, 11], result.Plan.Pages.Select(page => Assert.Single(page.Items).TableRowCount));
        Assert.Equal([false, true, true], result.Plan.Pages.Select(page => Assert.Single(page.Items).RepeatsTableHeader));
        byte[] bytes = output.ToArray();
        using PdfDocument parsed = PdfReader.Open(new MemoryStream(bytes, writable: false), PdfDocumentOpenMode.Import);
        Assert.Equal(3, parsed.PageCount);
        Assert.Equal("FrameWeb complete print", parsed.Info.Title);
        foreach (PdfPage page in parsed.Pages)
        {
            Assert.Equal(PrintUnits.MillimetresToPoints(210), page.Width.Point, precision: 3);
            Assert.Equal(PrintUnits.MillimetresToPoints(297), page.Height.Point, precision: 3);
            Assert.NotEmpty(page.Contents.Elements);
        }

        PdfRawInspection inspection = PdfRawInspection.Read(bytes);
        Assert.Equal(3, inspection.PageCount);
        Assert.True(inspection.DecodedStreams.Count >= parsed.PageCount);
        Assert.Contains("/ToUnicode", inspection.RawText, StringComparison.Ordinal);
        Assert.Contains("/FontFile2", inspection.RawText, StringComparison.Ordinal);
        Assert.Contains("/Subtype/Type0", inspection.CompactedRawText, StringComparison.Ordinal);
        string decoded = inspection.DecodedText;
        Assert.True(CountOccurrences(decoded, " Tj") + CountOccurrences(decoded, " TJ") >= 100);
        Assert.True(CountOccurrences(decoded, " re") >= 300);
    }

    [Theory]
    [InlineData(PrintTextLanguage.Japanese, "日本語", "橋梁", "荷重")]
    [InlineData(
        PrintTextLanguage.SimplifiedChinese,
        "FrameWeb 中文 / 日本語",
        "结构分析 / 構造解析",
        "打印确认 / 日本語の印刷確認")]
    public async Task InstalledFontStrategy_EmbedsCjkWithToUnicodeAndNoCopiedFontBinary(
        PrintTextLanguage language,
        string title,
        string heading,
        string body)
    {
        PrintJob job = new(
            title,
            PrintPageSettings.CreateA4(),
            language,
            [new PrintTextSection(heading, body)]);
        using MemoryStream output = new();

        await new TypedPdfDocumentWriter().WriteAsync(job, output);

        byte[] bytes = output.ToArray();
        using PdfDocument parsed = PdfReader.Open(new MemoryStream(bytes, writable: false), PdfDocumentOpenMode.Import);
        Assert.Single(parsed.Pages);
        Assert.Equal(title, parsed.Info.Title);
        PdfRawInspection inspection = PdfRawInspection.Read(bytes);
        Assert.Contains("/ToUnicode", inspection.RawText, StringComparison.Ordinal);
        Assert.Contains("/FontFile2", inspection.RawText, StringComparison.Ordinal);
        Assert.All(
            title.Concat(heading).Concat(body).Where(character => character > 0x7f).Distinct(),
            character => Assert.Contains(
                ((int)character).ToString("X4"),
                inspection.DecodedText,
                StringComparison.OrdinalIgnoreCase));

        string? visualProofPath = Environment.GetEnvironmentVariable("FRAMEWEB_CJK_VISUAL_PROOF_PATH");
        if (language == PrintTextLanguage.SimplifiedChinese && !string.IsNullOrWhiteSpace(visualProofPath))
        {
            File.WriteAllBytes(visualProofPath, bytes);
        }
    }

    [Fact]
    public async Task RepeatedAndParallelExports_AreByteDeterministicAndUseDeclaredSerializationPolicy()
    {
        TypedPdfDocumentWriter writer = new();
        PrintJob job = CreateDiagramJob();

        byte[][] sequential =
        [
            await WriteAsync(writer, job),
            await WriteAsync(writer, job),
        ];
        byte[][] parallel = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => WriteAsync(writer, job)));

        Assert.Equal(PdfExportConcurrencyPolicy.SerializedProcessWide, writer.ConcurrencyPolicy);
        PrintDocumentPlan expectedPlan = writer.Plan(job);
        Assert.All(Enumerable.Range(0, 8), _ => AssertPlansEqual(expectedPlan, writer.Plan(job)));
        PdfSemanticSnapshot expected = PdfSemanticSnapshot.Create(sequential[0]);
        Assert.All(
            sequential.Skip(1).Concat(parallel),
            bytes => AssertSemanticSnapshotEqual(expected, PdfSemanticSnapshot.Create(bytes)));
    }

    [Fact]
    public async Task CancellationAndDestinationFailure_PreserveCancellationAndOriginalExceptionProvenance()
    {
        TypedPdfDocumentWriter writer = new();
        PrintJob job = CreateDiagramJob();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        using MemoryStream untouched = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writer.WriteAsync(job, untouched, cancelled.Token));
        Assert.Empty(untouched.ToArray());

        SentinelIOException expected = new("destination failed");
        await using ThrowingWriteStream destination = new(expected);
        SentinelIOException actual = await Assert.ThrowsAsync<SentinelIOException>(() =>
            writer.WriteAsync(job, destination));
        Assert.Same(expected, actual);
    }

    [Fact]
    public void EncodedOutputBudget_AllowsExactBytesAndRejectsPlusOneBeforeMutatingTheStream()
    {
        PrintEngineLimitProfile limits = new(
            MaximumPages: 1,
            MaximumSections: 1,
            MaximumTables: 1,
            MaximumRows: 1,
            MaximumCells: 1,
            MaximumTextCharacters: 1,
            MaximumImages: 1,
            MaximumDecodedImageBytes: 3,
            MaximumEncodedPdfBytes: 4,
            MaximumLayoutWork: 1);
        using BoundedOutputStream output = new(4, new PrintWorkBudget(limits));

        output.Write([1, 2, 3, 4]);
        Assert.Equal(4, output.Length);
        PrintLimitExceededException exception = Assert.Throws<PrintLimitExceededException>(() => output.WriteByte(5));

        Assert.Equal("encoded PDF bytes", exception.ResourceName);
        Assert.Equal(4, exception.Limit);
        Assert.Equal(4, output.Length);
        Assert.Equal([1, 2, 3, 4], output.ToArray());
    }

    private static PrintJob CreateComprehensiveJob(PrintTextLanguage language)
    {
        PrintTableColumn[] columns = [new("ID"), new("X"), new("Y")];
        PrintTableRow[] rows = Enumerable.Range(1, 100)
            .Select(index => new PrintTableRow([index.ToString(), $"{index}.1", $"-{index}.2"]))
            .ToArray();
        return new PrintJob(
            "FrameWeb complete print",
            PrintPageSettings.CreateA4(),
            language,
            [new PrintTableSection(new PrintTable("Input nodes", columns, rows, repeatHeader: true))]);
    }

    private static PrintJob CreateDiagramJob()
    {
        const int width = 32;
        const int height = 24;
        byte[] pixels = new byte[ViewportCapture.GetRequiredByteLength(width, height)];
        for (int index = 0; index < pixels.Length; index += 3)
        {
            pixels[index] = (byte)(index % 251);
            pixels[index + 1] = (byte)((index / 3) % 239);
            pixels[index + 2] = 127;
        }

        return new PrintJob(
            "A3 diagram",
            PrintPageSettings.CreateA3(PrintPageOrientation.Landscape, uniformMarginMillimetres: 15),
            PrintTextLanguage.English,
            [new PrintDiagramSection(new PrintDiagram(
                "Result diagram",
                new ViewportCapture(width, height, pixels),
                PrintDiagramKind.Result,
                preferredHeightMillimetres: 160))]);
    }

    private static async Task<byte[]> WriteAsync(TypedPdfDocumentWriter writer, PrintJob job)
    {
        using MemoryStream output = new();
        await writer.WriteAsync(job, output);
        return output.ToArray();
    }

    private static int CountOccurrences(string value, string needle)
    {
        int count = 0;
        int start = 0;
        while ((start = value.IndexOf(needle, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += needle.Length;
        }

        return count;
    }

    private static void AssertPlansEqual(PrintDocumentPlan expected, PrintDocumentPlan actual)
    {
        Assert.Equal(expected.PageSettings, actual.PageSettings);
        Assert.Equal(expected.PageCount, actual.PageCount);
        Assert.Equal(expected.Pages.Select(page => page.PageNumber), actual.Pages.Select(page => page.PageNumber));
        Assert.Equal(
            expected.Pages.SelectMany(page => page.Items),
            actual.Pages.SelectMany(page => page.Items));
    }

    private static void AssertSemanticSnapshotEqual(PdfSemanticSnapshot expected, PdfSemanticSnapshot actual)
    {
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.PageGeometry, actual.PageGeometry);
        Assert.Equal(expected.NormalizedContentStreamHashes, actual.NormalizedContentStreamHashes);
        Assert.Equal(expected.ImageStreamHashes, actual.ImageStreamHashes);
    }

    private sealed class SentinelIOException(string message) : IOException(message);

    private sealed class ThrowingWriteStream(Exception failure) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw failure;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(failure);
    }
}

internal sealed record PdfSemanticSnapshot(
    string Title,
    IReadOnlyList<(double Width, double Height)> PageGeometry,
    IReadOnlyList<string> NormalizedContentStreamHashes,
    IReadOnlyList<string> ImageStreamHashes)
{
    public static PdfSemanticSnapshot Create(byte[] bytes)
    {
        using PdfDocument parsed = PdfReader.Open(
            new MemoryStream(bytes, writable: false),
            PdfDocumentOpenMode.Import);
        (double Width, double Height)[] geometry = parsed.Pages
            .Cast<PdfPage>()
            .Select(page => (page.Width.Point, page.Height.Point))
            .ToArray();
        PdfRawInspection inspection = PdfRawInspection.Read(bytes);
        string[] contentHashes = inspection.DecodedStreams
            .Where(stream => !stream.Dictionary.Contains("/Length1", StringComparison.Ordinal)
                && IsTextualPdfProgram(stream.Bytes))
            .Select(stream => NormalizedHash(stream.Bytes))
            .ToArray();
        string[] imageHashes = inspection.DecodedStreams
            .Where(stream => stream.Dictionary.Contains("/Subtype/Image", StringComparison.Ordinal))
            .Select(stream => Convert.ToHexString(SHA256.HashData(stream.Bytes)))
            .ToArray();
        return new PdfSemanticSnapshot(parsed.Info.Title, geometry, contentHashes, imageHashes);
    }

    private static bool IsTextualPdfProgram(byte[] stream)
    {
        if (stream.Length == 0)
        {
            return false;
        }

        int textualBytes = stream.Count(value => value is 9 or 10 or 13 || value is >= 32 and <= 126);
        if (textualBytes < Math.Ceiling(stream.Length * 0.85))
        {
            return false;
        }

        string program = Encoding.Latin1.GetString(stream);
        return program.Contains("begincmap", StringComparison.Ordinal)
            || Regex.IsMatch(
                program,
                @"(?:^|[\r\n])\s*(?:q|Q|BT|ET|[\d.+-]+\s+[\d.+-]+\s+[\d.+-]+\s+[\d.+-]+\s+re)\s*(?:[\r\n]|$)",
                RegexOptions.CultureInvariant);
    }

    private static string NormalizedHash(byte[] stream)
    {
        byte[] normalized = stream.ToArray();
        NormalizeSubsetPrefixes(normalized, stride: 1, characterOffset: 0);
        NormalizeSubsetPrefixes(normalized, stride: 2, characterOffset: 0);
        NormalizeSubsetPrefixes(normalized, stride: 2, characterOffset: 1);
        return Convert.ToHexString(SHA256.HashData(normalized));
    }

    private static void NormalizeSubsetPrefixes(byte[] bytes, int stride, int characterOffset)
    {
        ReadOnlySpan<byte> replacement = "SUBSET"u8;
        int encodedLength = stride * 7;
        for (int start = 0; start + encodedLength - stride + characterOffset < bytes.Length; start++)
        {
            if (stride == 2 && bytes[start + (1 - characterOffset)] != 0)
            {
                continue;
            }

            bool matches = true;
            for (int character = 0; character < 6; character++)
            {
                int index = start + characterOffset + (character * stride);
                if (bytes[index] is < (byte)'A' or > (byte)'Z'
                    || (stride == 2 && bytes[index + (1 - (2 * characterOffset))] != 0))
                {
                    matches = false;
                    break;
                }
            }

            int plusIndex = start + characterOffset + (6 * stride);
            if (!matches || bytes[plusIndex] != (byte)'+')
            {
                continue;
            }

            for (int character = 0; character < replacement.Length; character++)
            {
                bytes[start + characterOffset + (character * stride)] = replacement[character];
            }

            start += encodedLength - stride;
        }
    }
}

internal sealed record PdfDecodedStream(string Dictionary, byte[] Bytes);

internal sealed class PdfRawInspection
{
    private PdfRawInspection(byte[] bytes, string rawText, IReadOnlyList<PdfDecodedStream> decodedStreams)
    {
        Bytes = bytes;
        RawText = rawText;
        CompactedRawText = Regex.Replace(rawText, @"\s+", string.Empty);
        DecodedStreams = decodedStreams;
        DecodedText = string.Join("\n", decodedStreams.Select(stream => Encoding.Latin1.GetString(stream.Bytes)));
        PageCount = Regex.Matches(rawText, @"/Type\s*/Page(?!s)\b", RegexOptions.CultureInvariant).Count;
    }

    public byte[] Bytes { get; }
    public string RawText { get; }
    public string CompactedRawText { get; }
    public IReadOnlyList<PdfDecodedStream> DecodedStreams { get; }
    public string DecodedText { get; }
    public int PageCount { get; }

    public static PdfRawInspection Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        string raw = Encoding.Latin1.GetString(bytes);
        List<PdfDecodedStream> streams = [];
        int search = 0;
        while (true)
        {
            (int marker, int dataStart) = FindNextStream(raw, search);
            if (marker < 0)
            {
                break;
            }

            int dictionaryStart = raw.LastIndexOf("<<", marker, StringComparison.Ordinal);
            int dictionaryEnd = raw.LastIndexOf(">>", marker, StringComparison.Ordinal);
            if (dictionaryStart < 0 || dictionaryEnd < dictionaryStart)
            {
                throw new InvalidDataException("PDF stream has no immediate dictionary.");
            }

            string dictionary = raw[dictionaryStart..(dictionaryEnd + 2)];
            MatchCollection lengths = Regex.Matches(dictionary, @"/Length\s+(\d+)\b", RegexOptions.CultureInvariant);
            if (lengths.Count == 0 || !int.TryParse(lengths[^1].Groups[1].Value, out int length))
            {
                search = dataStart;
                continue;
            }

            if (dataStart + length > bytes.Length)
            {
                throw new InvalidDataException("PDF stream length exceeds the file.");
            }

            byte[] encoded = bytes.AsSpan(dataStart, length).ToArray();
            byte[] decoded = dictionary.Contains("/FlateDecode", StringComparison.Ordinal)
                ? Inflate(encoded)
                : encoded;
            streams.Add(new PdfDecodedStream(dictionary, decoded));
            search = dataStart + length;
        }

        return new PdfRawInspection(bytes, raw, streams);
    }

    private static (int Marker, int DataStart) FindNextStream(string raw, int search)
    {
        const string lineFeedMarker = "\nstream\n";
        const string carriageReturnMarker = "\r\nstream\r\n";
        int lineFeed = raw.IndexOf(lineFeedMarker, search, StringComparison.Ordinal);
        int carriageReturn = raw.IndexOf(carriageReturnMarker, search, StringComparison.Ordinal);
        if (lineFeed < 0 && carriageReturn < 0)
        {
            return (-1, -1);
        }

        if (carriageReturn >= 0 && (lineFeed < 0 || carriageReturn < lineFeed))
        {
            return (carriageReturn + 2, carriageReturn + carriageReturnMarker.Length);
        }

        return (lineFeed + 1, lineFeed + lineFeedMarker.Length);
    }

    private static byte[] Inflate(byte[] encoded)
    {
        try
        {
            return Inflate(encoded, zlibWrapped: true);
        }
        catch (InvalidDataException)
        {
            return Inflate(encoded, zlibWrapped: false);
        }
    }

    private static byte[] Inflate(byte[] encoded, bool zlibWrapped)
    {
        using MemoryStream input = new(encoded, writable: false);
        using Stream inflater = zlibWrapped
            ? new ZLibStream(input, CompressionMode.Decompress)
            : new DeflateStream(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        inflater.CopyTo(output);
        return output.ToArray();
    }
}
