using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed class Step8WholePreviewBudgetAndTextTests
{
    [Fact]
    public async Task ProductionShapeMultiPagePreview_AutoFitsOneSharedBudgetAndKeepsPageIdentity()
    {
        PrintJob job = new(
            "Production-shaped preview",
            PrintPageSettings.CreateA4(showPageNumbers: true),
            PrintTextLanguage.English,
            Enumerable.Range(1, 22)
                .Select(page => (PrintSection)new PrintDiagramSection(new PrintDiagram(
                    $"Page {page:00} unique result diagram",
                    new ViewportCapture(
                        1,
                        1,
                        new byte[]
                        {
                            checked((byte)(page * 11)),
                            checked((byte)(page * 7)),
                            checked((byte)(page * 5)),
                        }),
                    PrintDiagramKind.Result,
                    preferredHeightMillimetres: 80)))
                .ToArray(),
            PrintLayoutMode.SectionPerPage);
        RecordingPreviewObserver observer = new();
        TypedPdfDocumentWriter writer = new(PrintEngineLimitProfile.Default, observer);
        PrintDocumentPlan plan = writer.Plan(job);

        PrintPreviewDocumentResult preview = await writer.RenderPreviewAsync(
            job,
            plan,
            new PrintPreviewOptions(
                maximumDimension: PrintEngineLimits.MaximumImageDimension,
                fitToDocumentBudget: true));

        Assert.Equal(22, plan.PageCount);
        Assert.Equal(plan.PageCount, preview.Pages.Count);
        Assert.All(preview.Pages, page => Assert.Equal(plan.Identity, page.PlanIdentity));
        Assert.All(preview.Pages, page =>
        {
            Assert.True(page.Capture.Width < PrintEngineLimits.MaximumImageDimension);
            Assert.True(page.Capture.Height < PrintEngineLimits.MaximumImageDimension);
        });
        Assert.Equal(
            plan.PageCount,
            preview.Pages
                .Select(page => Convert.ToHexString(SHA256.HashData(page.Capture.Rgb24.Span)))
                .Distinct(StringComparer.Ordinal)
                .Count());

        PrintWorkBudgetSnapshot snapshot = Assert.IsType<PrintWorkBudgetSnapshot>(observer.Completed);
        Assert.InRange(snapshot.DecodedImageBytes, 1, PrintEngineLimits.MaximumDecodedImageBytes);
        Assert.InRange(snapshot.LayoutWork, 1, PrintEngineLimits.MaximumLayoutWork);
        Assert.Equal(plan.PageCount * 2, snapshot.Images);
        Assert.Equal(1, observer.SessionStartedCount);
        Assert.Equal(1, observer.PlanValidatedCount);
        Assert.Equal(Enumerable.Range(1, plan.PageCount), observer.RenderedPages);
        Assert.Null(observer.Failed);
    }

    [Fact]
    public async Task WholeDocumentPreview_ValidatesOnceAndPreflightsOneAggregateBudget()
    {
        PrintJob job = CreateAggregatePreviewJob();
        RecordingPreviewObserver baselineObserver = new();
        TypedPdfDocumentWriter baselineWriter = new(PrintEngineLimitProfile.Default, baselineObserver);
        PrintDocumentPlan plan = baselineWriter.Plan(job);

        PrintPreviewDocumentResult baseline = await baselineWriter.RenderPreviewAsync(
            job,
            plan,
            new PrintPreviewOptions(maximumDimension: 64));

        PrintWorkBudgetSnapshot snapshot = Assert.IsType<PrintWorkBudgetSnapshot>(baselineObserver.Completed);
        Assert.Same(plan, baseline.Plan);
        Assert.Equal(plan.Identity, baseline.PlanIdentity);
        Assert.Equal(plan.PageCount, baseline.Pages.Count);
        Assert.Equal(1, baselineObserver.SessionStartedCount);
        Assert.Equal(1, baselineObserver.PlanValidatedCount);
        Assert.Equal(Enumerable.Range(1, plan.PageCount), baselineObserver.RenderedPages);
        Assert.Equal(plan.PageCount, snapshot.Images);
        Assert.True(snapshot.TextCharacters > 0);
        Assert.True(snapshot.LayoutWork > plan.PageCount);
        Assert.Null(baselineObserver.Failed);

        PrintEngineLimitProfile exactLimits = PrintEngineLimitProfile.Default with
        {
            MaximumImages = checked((int)snapshot.Images),
            MaximumTextCharacters = checked((int)snapshot.TextCharacters),
            MaximumLayoutWork = snapshot.LayoutWork,
        };
        RecordingPreviewObserver exactObserver = new();
        TypedPdfDocumentWriter exactWriter = new(exactLimits, exactObserver);

        PrintPreviewDocumentResult exact = await exactWriter.RenderPreviewAsync(
            job,
            plan,
            new PrintPreviewOptions(maximumDimension: 64));

        Assert.Equal(plan.PageCount, exact.Pages.Count);
        Assert.Equal(snapshot, exactObserver.Completed);
        Assert.Equal(1, exactObserver.PlanValidatedCount);
        Assert.Equal(plan.PageCount, exactObserver.RenderedPages.Count);

        RecordingPreviewObserver minimumObserver = new();
        TypedPdfDocumentWriter minimumWriter = new(PrintEngineLimitProfile.Default, minimumObserver);
        await minimumWriter.RenderPreviewAsync(
            job,
            plan,
            new PrintPreviewOptions(maximumDimension: 1));
        PrintWorkBudgetSnapshot minimumSnapshot = Assert.IsType<PrintWorkBudgetSnapshot>(minimumObserver.Completed);
        Assert.True(minimumSnapshot.LayoutWork < snapshot.LayoutWork);

        await AssertAggregatePlusOneFailsBeforeAnyPageIsRendered(
            job,
            plan,
            PrintEngineLimitProfile.Default with { MaximumImages = checked((int)snapshot.Images - 1) },
            "images");
        await AssertAggregatePlusOneFailsBeforeAnyPageIsRendered(
            job,
            plan,
            PrintEngineLimitProfile.Default with
            {
                MaximumTextCharacters = checked((int)snapshot.TextCharacters - 1),
            },
            "text characters");
        await AssertAggregatePlusOneFailsBeforeAnyPageIsRendered(
            job,
            plan,
            PrintEngineLimitProfile.Default with { MaximumLayoutWork = minimumSnapshot.LayoutWork - 1 },
            "layout work",
            maximumDimension: 1);
    }

    [Theory]
    [InlineData(0.25, false)]
    [InlineData(4.0, true)]
    public async Task ThirteenColumnTable_SharesDeterministicFittedClippedTextAcrossPlanPreviewAndPdf(
        double scale,
        bool expectsTruncation)
    {
        PrintJob job = CreateThirteenColumnJob(scale);
        TypedPdfDocumentWriter writer = new();
        PrintDocumentPlan plan = writer.Plan(job);
        PrintPagePlan page = Assert.Single(plan.Pages);
        PrintPreviewTextRun[] tableRuns = page.TextRuns
            .Where(run => run.Kind is PrintPreviewTextKind.TableHeader or PrintPreviewTextKind.TableCell)
            .ToArray();

        Assert.Equal(26, tableRuns.Length);
        Assert.All(tableRuns, run =>
        {
            Assert.True(run.Bounds.WidthPoints > 0);
            Assert.True(run.Bounds.HeightPoints > 0);
            Assert.InRange(run.DisplayWidthPoints, 0, run.Bounds.WidthPoints + 0.001);
            Assert.True(run.FontSizePoints > 0);
            Assert.DoesNotContain('\r', run.DisplayText);
            Assert.DoesNotContain('\n', run.DisplayText);
        });
        AssertRowsDoNotOverlap(tableRuns);
        Assert.Equal(expectsTruncation, tableRuns.Any(run => run.IsTruncated));
        if (expectsTruncation)
        {
            Assert.Contains(tableRuns, run => run.IsTruncated &&
                (run.DisplayText.EndsWith('…') || run.DisplayText.Length == 0));
        }
        else
        {
            Assert.All(tableRuns, run => Assert.Equal(run.Text, run.DisplayText));
        }

        PrintDocumentPlan repeatedPlan = writer.Plan(job);
        Assert.Equal(plan.Identity, repeatedPlan.Identity);
        Assert.Equal(page.TextRuns, Assert.Single(repeatedPlan.Pages).TextRuns);

        PrintPreviewDocumentResult preview = await writer.RenderPreviewAsync(
            job,
            plan,
            new PrintPreviewOptions(maximumDimension: 180));
        PrintPreviewPageResult previewPage = Assert.Single(preview.Pages);
        Assert.Same(plan, preview.Plan);
        Assert.Same(page, previewPage.Page);
        Assert.Same(page.TextRuns, previewPage.TextRuns);

        using MemoryStream pdf = new();
        PrintExportResult export = await writer.WriteAsync(job, plan, pdf);
        Assert.Same(plan, export.Plan);
        Assert.Same(page.TextRuns, export.Plan.Pages[0].TextRuns);

        PdfRawInspection inspection = PdfRawInspection.Read(pdf.ToArray());
        string pagePrograms = string.Join(
            "\n",
            inspection.DecodedStreams
                .Select(stream => Encoding.Latin1.GetString(stream.Bytes))
                .Where(program => program.Contains("BT", StringComparison.Ordinal)));
        int clippingOperators = Regex.Matches(
            pagePrograms,
            @"\bW\*?\s+n\b",
            RegexOptions.CultureInvariant).Count;
        Assert.True(
            clippingOperators >= tableRuns.Length,
            $"Expected at least {tableRuns.Length} table text clips but found {clippingOperators}.");
    }

    private static async Task AssertAggregatePlusOneFailsBeforeAnyPageIsRendered(
        PrintJob job,
        PrintDocumentPlan plan,
        PrintEngineLimitProfile limits,
        string expectedResource,
        int maximumDimension = 64)
    {
        RecordingPreviewObserver observer = new();
        TypedPdfDocumentWriter writer = new(limits, observer);

        PrintLimitExceededException exception = await Assert.ThrowsAsync<PrintLimitExceededException>(() =>
            writer.RenderPreviewAsync(job, plan, new PrintPreviewOptions(maximumDimension)));

        Assert.Equal(expectedResource, exception.ResourceName);
        Assert.Equal(1, observer.SessionStartedCount);
        Assert.Equal(1, observer.PlanValidatedCount);
        Assert.Empty(observer.RenderedPages);
        Assert.Null(observer.Completed);
        Assert.Same(exception, observer.Failed?.Exception);
        Assert.NotNull(observer.Failed?.Snapshot);
    }

    private static void AssertRowsDoNotOverlap(IEnumerable<PrintPreviewTextRun> runs)
    {
        foreach (IGrouping<double, PrintPreviewTextRun> row in runs.GroupBy(
                     run => Math.Round(run.Bounds.YPoints, 6)))
        {
            PrintPreviewTextRun[] ordered = row.OrderBy(run => run.Bounds.XPoints).ToArray();
            for (int index = 1; index < ordered.Length; index++)
            {
                double previousRight = ordered[index - 1].Bounds.XPoints + ordered[index - 1].Bounds.WidthPoints;
                Assert.True(
                    previousRight <= ordered[index].Bounds.XPoints + 0.001,
                    $"Text bounds overlap at y={row.Key}: {previousRight} > {ordered[index].Bounds.XPoints}.");
            }
        }
    }

    private static PrintJob CreateAggregatePreviewJob() => new(
        "Aggregate preview",
        PrintPageSettings.CreateA4(showPageNumbers: false),
        PrintTextLanguage.English,
        [
            new PrintTextSection("First heading", "First bounded body text"),
            new PrintTextSection("Second heading", "Second bounded body text"),
            new PrintTableSection(new PrintTable(
                "Aggregate table",
                [new PrintTableColumn("ID"), new PrintTableColumn("Value")],
                [new PrintTableRow(["R1", "123.45"])])),
        ],
        PrintLayoutMode.SectionPerPage);

    private static PrintJob CreateThirteenColumnJob(double scale)
    {
        PrintTableColumn[] columns = Enumerable.Range(1, 13)
            .Select(index => new PrintTableColumn($"COLUMN_{index:00}_LABEL"))
            .ToArray();
        PrintTableRow row = new(Enumerable.Range(1, 13).Select(index => $"VALUE_{index:00}_DATA"));
        return new PrintJob(
            "13-column input table",
            PrintPageSettings.CreateA4(scale: scale, showPageNumbers: false),
            PrintTextLanguage.English,
            [new PrintTableSection(new PrintTable("Section definitions", columns, [row]))],
            PrintLayoutMode.SectionPerPage);
    }

    private sealed class RecordingPreviewObserver : IPrintExportObserver
    {
        public int SessionStartedCount { get; private set; }

        public int PlanValidatedCount { get; private set; }

        public List<int> RenderedPages { get; } = [];

        public PrintWorkBudgetSnapshot? Completed { get; private set; }

        public (PrintWorkBudgetSnapshot Snapshot, Exception Exception)? Failed { get; private set; }

        public void OnEntered()
        {
        }

        public void OnExited()
        {
        }

        public void OnPreviewSessionStarted(int requestedPageCount)
        {
            SessionStartedCount++;
            Assert.True(requestedPageCount > 1);
        }

        public void OnPreviewPlanValidated(PrintPlanIdentity identity)
        {
            Assert.False(string.IsNullOrWhiteSpace(identity.Value));
            PlanValidatedCount++;
        }

        public void OnPreviewPageRendered(int pageNumber, long decodedBytes)
        {
            Assert.True(decodedBytes > 0);
            RenderedPages.Add(pageNumber);
        }

        public void OnPreviewSessionCompleted(PrintWorkBudgetSnapshot snapshot) => Completed = snapshot;

        public void OnPreviewSessionFailed(PrintWorkBudgetSnapshot snapshot, Exception exception) =>
            Failed = (snapshot, exception);
    }
}
