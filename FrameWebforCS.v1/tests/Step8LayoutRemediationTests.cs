using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.IO;
using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed partial class Step8LayoutRemediationTests
{
    [Theory]
    [InlineData(PrintPaperSize.A3, PrintPageOrientation.Portrait, 297d, 420d)]
    [InlineData(PrintPaperSize.A4, PrintPageOrientation.Landscape, 297d, 210d)]
    public async Task GeneratedPdf_UsesExactRemainingPaperOrientations(
        PrintPaperSize paper,
        PrintPageOrientation orientation,
        double widthMillimetres,
        double heightMillimetres)
    {
        PrintPageSettings settings = new(
            paper,
            orientation,
            new PrintMargins(11, 12, 13, 14));
        PrintJob job = new(
            "Generated geometry",
            settings,
            PrintTextLanguage.English,
            [new PrintTextSection("Geometry", "Body")]);

        byte[] bytes = await ExportAsync(job);

        using PdfDocument parsed = PdfReader.Open(new MemoryStream(bytes, writable: false), PdfDocumentOpenMode.Import);
        PdfPage page = Assert.Single(parsed.Pages.Cast<PdfPage>());
        Assert.Equal(PrintUnits.MillimetresToPoints(widthMillimetres), page.Width.Point, precision: 3);
        Assert.Equal(PrintUnits.MillimetresToPoints(heightMillimetres), page.Height.Point, precision: 3);
        Assert.NotEmpty(ContentProgram(page));
    }

    [Fact]
    public async Task PageNumberOption_AddsExactlyOneFooterTextRunPerGeneratedPage()
    {
        PrintTable table = CreateTable(rowCount: 100, repeatHeader: true);
        PrintJob enabled = CreateTableJob(table, showPageNumbers: true);
        PrintJob disabled = CreateTableJob(table, showPageNumbers: false);

        (PrintExportResult enabledResult, byte[] enabledBytes) = await ExportWithResultAsync(enabled);
        (PrintExportResult disabledResult, byte[] disabledBytes) = await ExportWithResultAsync(disabled);

        Assert.Equal(enabledResult.Plan.PageCount, disabledResult.Plan.PageCount);
        string[] enabledPrograms = ContentPrograms(enabledBytes);
        string[] disabledPrograms = ContentPrograms(disabledBytes);
        Assert.Equal(enabledPrograms.Length, disabledPrograms.Length);
        for (int page = 0; page < enabledPrograms.Length; page++)
        {
            Assert.Equal(
                CountTextRuns(disabledPrograms[page]) + 1,
                CountTextRuns(enabledPrograms[page]));
        }
    }

    [Fact]
    public async Task RepeatHeaderFalse_OmitsHeaderGeometryAndTextOnEveryContinuedPage()
    {
        PrintTable table = CreateTable(rowCount: 100, repeatHeader: false);
        PrintJob job = CreateTableJob(table, showPageNumbers: true);

        (PrintExportResult result, byte[] bytes) = await ExportWithResultAsync(job);

        Assert.True(result.Plan.PageCount > 1);
        Assert.All(
            result.Plan.Pages.Skip(1),
            page => Assert.False(Assert.Single(page.Items).RepeatsTableHeader));
        string[] programs = ContentPrograms(bytes);
        for (int pageIndex = 1; pageIndex < programs.Length; pageIndex++)
        {
            PrintPageItemPlan item = Assert.Single(result.Plan.Pages[pageIndex].Items);
            int expectedTextRuns = 1 + (item.TableRowCount * table.Columns.Count) + 1;
            int expectedRectangles = item.TableRowCount * table.Columns.Count;
            Assert.Equal(expectedTextRuns, CountTextRuns(programs[pageIndex]));
            Assert.Equal(expectedRectangles, RectangleOperator().Matches(programs[pageIndex]).Count);
        }
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(4.0)]
    public async Task ScaleBoundary_UsesOneMetricForPlanTextDiagramAndRenderedOperators(double scale)
    {
        PrintPageSettings settings = PrintPageSettings.CreateA3(
            PrintPageOrientation.Landscape,
            uniformMarginMillimetres: 10,
            scale: scale,
            showPageNumbers: false);
        ViewportCapture capture = CreateGradientCapture(20, 10);
        PrintJob job = new(
            "Scaled layout",
            settings,
            PrintTextLanguage.English,
            [
                new PrintTextSection("Heading", "Body line"),
                new PrintDiagramSection(new PrintDiagram(
                    "Diagram",
                    capture,
                    PrintDiagramKind.Result,
                    preferredHeightMillimetres: 20)),
            ],
            PrintLayoutMode.SectionPerPage);

        (PrintExportResult result, byte[] bytes) = await ExportWithResultAsync(job);
        string[] programs = ContentPrograms(bytes);

        Assert.Equal(2, result.Plan.PageCount);
        PrintPageItemPlan textItem = Assert.Single(result.Plan.Pages[0].Items);
        (double X, double Y)[] positions = TextPositions(programs[0]);
        Assert.True(positions.Length >= 3);
        double bodyBaselineFromTop = result.Plan.PageHeightPoints - positions.Sum(value => value.Y);
        double expectedBodyBaselineFromTop = textItem.Bounds.YPoints + (18 * scale) + (9 * scale);
        Assert.True(
            bodyBaselineFromTop >= expectedBodyBaselineFromTop - 1 &&
            bodyBaselineFromTop <= expectedBodyBaselineFromTop + 1,
            $"Expected body baseline {expectedBodyBaselineFromTop:G17}; actual {bodyBaselineFromTop:G17}; " +
            $"positions={string.Join(';', positions.Select(value => $"{value.X:G17},{value.Y:G17}"))}");

        PrintPageItemPlan diagramItem = Assert.Single(result.Plan.Pages[1].Items);
        Match image = ImageMatrixOperator().Matches(programs[1]).Last();
        double imageHeight = Parse(image.Groups["height"].Value);
        double expectedAvailableHeight = diagramItem.Bounds.HeightPoints - (18 * scale);
        Assert.True(imageHeight <= expectedAvailableHeight + 0.1);
        Assert.True(imageHeight > 0);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(4.0)]
    public async Task ScaledMultipageTable_RendersExactlyThePlannedRowsAndHeaders(double scale)
    {
        PrintTable table = CreateTable(rowCount: 120, repeatHeader: true);
        PrintJob job = new(
            "Scaled table",
            PrintPageSettings.CreateA4(scale: scale, showPageNumbers: false),
            PrintTextLanguage.English,
            [new PrintTableSection(table)]);

        (PrintExportResult result, byte[] bytes) = await ExportWithResultAsync(job);
        string[] programs = ContentPrograms(bytes);

        Assert.Equal(result.Plan.PageCount, programs.Length);
        Assert.True(result.Plan.PageCount >= 1);
        for (int pageIndex = 0; pageIndex < programs.Length; pageIndex++)
        {
            PrintPageItemPlan item = Assert.Single(result.Plan.Pages[pageIndex].Items);
            int headerRows = pageIndex == 0 || item.RepeatsTableHeader ? 1 : 0;
            int expectedRectangles = (item.TableRowCount + headerRows) * table.Columns.Count;
            Assert.Equal(expectedRectangles, RectangleOperator().Matches(programs[pageIndex]).Count);
        }

        Assert.Equal(120, result.Plan.Pages.Sum(page => Assert.Single(page.Items).TableRowCount));
    }

    private static PrintJob CreateTableJob(PrintTable table, bool showPageNumbers) => new(
        "Table report",
        PrintPageSettings.CreateA4(showPageNumbers: showPageNumbers),
        PrintTextLanguage.English,
        [new PrintTableSection(table)]);

    private static PrintTable CreateTable(int rowCount, bool repeatHeader)
    {
        PrintTableColumn[] columns = [new("ID"), new("X"), new("Y")];
        PrintTableRow[] rows = Enumerable.Range(1, rowCount)
            .Select(index => new PrintTableRow([index.ToString(), $"{index}.1", $"-{index}.2"]))
            .ToArray();
        return new PrintTable("Rows", columns, rows, repeatHeader);
    }

    private static ViewportCapture CreateGradientCapture(int width, int height)
    {
        byte[] rgb = new byte[ViewportCapture.GetRequiredByteLength(width, height)];
        for (int index = 0; index < rgb.Length; index++)
        {
            rgb[index] = (byte)(index % 251);
        }

        return new ViewportCapture(width, height, rgb);
    }

    private static async Task<byte[]> ExportAsync(PrintJob job)
    {
        (_, byte[] bytes) = await ExportWithResultAsync(job);
        return bytes;
    }

    private static async Task<(PrintExportResult Result, byte[] Bytes)> ExportWithResultAsync(PrintJob job)
    {
        using MemoryStream output = new();
        PrintExportResult result = await new TypedPdfDocumentWriter().WriteAsync(job, output);
        return (result, output.ToArray());
    }

    private static string[] ContentPrograms(byte[] bytes)
    {
        using PdfDocument parsed = PdfReader.Open(new MemoryStream(bytes, writable: false), PdfDocumentOpenMode.Import);
        return parsed.Pages.Cast<PdfPage>().Select(ContentProgram).ToArray();
    }

    private static string ContentProgram(PdfPage page) =>
        Encoding.Latin1.GetString(ContentReader.ReadContent(page).ToContent());

    private static int CountTextRuns(string program) => TextShowOperator().Matches(program).Count;

    private static (double X, double Y)[] TextPositions(string program) =>
        TextPositionOperator().Matches(program)
            .Select(match => (Parse(match.Groups["x"].Value), Parse(match.Groups["y"].Value)))
            .ToArray();

    private static double Parse(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(?m)^\([^\r\n]*\)Tj$")]
    private static partial Regex TextShowOperator();

    [GeneratedRegex(@"(?m)^[-+\d.]+\s+[-+\d.]+\s+[-+\d.]+\s+[-+\d.]+\s+re$")]
    private static partial Regex RectangleOperator();

    [GeneratedRegex(@"(?m)^(?<x>[-+\d.]+)\s+(?<y>[-+\d.]+)\s+Td$")]
    private static partial Regex TextPositionOperator();

    [GeneratedRegex(@"(?m)^(?<width>[-+\d.]+)\s+0\s+0\s+(?<height>[-+\d.]+)\s+[-+\d.]+\s+[-+\d.]+\s+cm\r?$", RegexOptions.CultureInvariant)]
    private static partial Regex ImageMatrixOperator();
}
