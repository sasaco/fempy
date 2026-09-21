using System.Security.Cryptography;
using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed class Step8PreviewAndFontAcceptanceTests
{
    [Fact]
    public async Task PagePreviewRasterAndPdfExport_UseTheExactSameImmutablePlanIdentity()
    {
        PrintJob job = CreateTableAndDiagramJob();
        TypedPdfDocumentWriter writer = new();
        PrintDocumentPlan plan = writer.Plan(job);

        PrintPreviewPageResult tablePage = await writer.RenderPreviewPageAsync(
            job,
            plan,
            pageNumber: 1,
            new PrintPreviewOptions(maximumDimension: 320));
        PrintPreviewPageResult diagramPage = await writer.RenderPreviewPageAsync(
            job,
            plan,
            pageNumber: 2,
            new PrintPreviewOptions(maximumDimension: 320));

        Assert.Same(plan, tablePage.Plan);
        Assert.Same(plan, diagramPage.Plan);
        Assert.Equal(plan.Identity, tablePage.PlanIdentity);
        Assert.Equal(plan.Identity, diagramPage.PlanIdentity);
        Assert.Same(plan.Pages[0], tablePage.Page);
        Assert.Same(plan.Pages[1], diagramPage.Page);
        Assert.Equal(226, tablePage.Capture.Width);
        Assert.Equal(320, tablePage.Capture.Height);
        Assert.Equal(
            (tablePage.Capture.Width, tablePage.Capture.Height),
            (diagramPage.Capture.Width, diagramPage.Capture.Height));
        Assert.NotEqual(
            Convert.ToHexString(SHA256.HashData(tablePage.Capture.Rgb24.Span)),
            Convert.ToHexString(SHA256.HashData(diagramPage.Capture.Rgb24.Span)));
        Assert.Contains((byte)225, tablePage.Capture.Rgb24.Span.ToArray());
        Assert.Contains((byte)230, diagramPage.Capture.Rgb24.Span.ToArray());

        using MemoryStream pdf = new();
        PrintExportResult exported = await writer.WriteAsync(job, plan, pdf);
        Assert.Same(plan, exported.Plan);
        Assert.Equal(plan.Identity, exported.PlanIdentity);
        Assert.Equal(2, exported.Plan.PageCount);

        PrintJob changedJob = new(
            "Different immutable job",
            job.PageSettings,
            job.Language,
            job.Sections,
            job.LayoutMode);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            writer.RenderPreviewPageAsync(changedJob, plan, pageNumber: 1));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            writer.WriteAsync(changedJob, plan, new MemoryStream()));
    }

    [Fact]
    public void EnglishFontInitialization_DoesNotProbeOptionalCjkFiles()
    {
        RecordingFontDiscovery discovery = new(
            new Dictionary<string, Func<Stream>>(StringComparer.OrdinalIgnoreCase)
            {
                ["arial.ttf"] = () => new MemoryStream([0, 1, 2], writable: false),
                ["arialbd.ttf"] = () => new MemoryStream([3, 4, 5], writable: false),
            });
        InstalledWindowsFontResolver resolver = new(discovery);

        resolver.EnsureLanguage(PrintTextLanguage.English);

        Assert.Equal(["arial.ttf", "arialbd.ttf"], discovery.Requests);
        Assert.DoesNotContain(discovery.Requests, name =>
            name.Contains("yumin", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("msyh", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(PrintTextLanguage.Japanese, "yumin.ttf")]
    [InlineData(PrintTextLanguage.SimplifiedChinese, "msyh.ttc")]
    public void MissingRequestedLanguageFont_ReportsExactFaceAndOriginalCause(
        PrintTextLanguage language,
        string expectedFileName)
    {
        IOException original = new($"missing {expectedFileName}");
        RecordingFontDiscovery discovery = new(
            new Dictionary<string, Func<Stream>>(StringComparer.OrdinalIgnoreCase)
            {
                [expectedFileName] = () => throw original,
            });
        InstalledWindowsFontResolver resolver = new(discovery);

        PrintFontUnavailableException exception = Assert.Throws<PrintFontUnavailableException>(() =>
            resolver.EnsureLanguage(language));

        Assert.Equal(language, exception.Language);
        Assert.False(exception.Bold);
        Assert.Equal(expectedFileName, exception.FileName);
        Assert.Same(original, exception.InnerException);
        Assert.Equal([expectedFileName], discovery.Requests);
    }

    [Fact]
    public async Task ProcessWideSerialization_AppliesAcrossWriterInstancesAndWaitCancellationIsClean()
    {
        BlockingExportObserver firstObserver = new();
        CountingExportObserver waitingObserver = new();
        TypedPdfDocumentWriter firstWriter = new(PrintEngineLimitProfile.Default, firstObserver);
        TypedPdfDocumentWriter waitingWriter = new(PrintEngineLimitProfile.Default, waitingObserver);
        PrintJob job = new(
            "Cross-instance serialization",
            PrintPageSettings.CreateA4(),
            PrintTextLanguage.English,
            [new PrintTextSection("Heading", "Body")]);

        using MemoryStream firstDestination = new();
        Task<PrintExportResult> first = Task.Run(() => firstWriter.WriteAsync(job, firstDestination));
        await firstObserver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        using CancellationTokenSource cancellation = new();
        using MemoryStream waitingDestination = new();
        Task<PrintExportResult> waiting = waitingWriter.WriteAsync(job, waitingDestination, cancellation.Token);
        await Task.Delay(50);
        Assert.Equal(0, waitingObserver.EnteredCount);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(0, waitingObserver.EnteredCount);
        Assert.Empty(waitingDestination.ToArray());

        firstObserver.Release();
        await first.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, firstObserver.ExitedCount);

        using MemoryStream finalDestination = new();
        await waitingWriter.WriteAsync(job, finalDestination).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, waitingObserver.EnteredCount);
        Assert.Equal(1, waitingObserver.ExitedCount);
        Assert.NotEmpty(finalDestination.ToArray());
    }

    private static PrintJob CreateTableAndDiagramJob()
    {
        PrintTable table = new(
            "Input nodes",
            [new PrintTableColumn("ID"), new PrintTableColumn("X")],
            Enumerable.Range(1, 12).Select(index =>
                new PrintTableRow([index.ToString(), $"{index}.25"])),
            repeatHeader: true);
        byte[] pixels = new byte[ViewportCapture.GetRequiredByteLength(8, 4)];
        for (int index = 0; index < pixels.Length; index += 3)
        {
            pixels[index] = 230;
            pixels[index + 1] = (byte)(index % 191);
            pixels[index + 2] = 40;
        }

        return new PrintJob(
            "Preview identity",
            PrintPageSettings.CreateA4(),
            PrintTextLanguage.English,
            [
                new PrintTableSection(table),
                new PrintDiagramSection(new PrintDiagram(
                    "Result diagram",
                    new ViewportCapture(8, 4, pixels),
                    PrintDiagramKind.Result,
                    preferredHeightMillimetres: 120)),
            ],
            PrintLayoutMode.SectionPerPage);
    }

    private sealed class RecordingFontDiscovery(
        IReadOnlyDictionary<string, Func<Stream>> sources) : IInstalledFontDiscovery
    {
        public List<string> Requests { get; } = [];

        public Stream OpenRead(string fileName)
        {
            Requests.Add(fileName);
            return sources.TryGetValue(fileName, out Func<Stream>? source)
                ? source()
                : throw new FileNotFoundException("Test font is unavailable.", fileName);
        }
    }

    private sealed class BlockingExportObserver : IPrintExportObserver
    {
        private readonly ManualResetEventSlim release = new(initialState: false);

        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ExitedCount { get; private set; }

        public void OnEntered()
        {
            Entered.TrySetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)), "The blocked export was not released.");
        }

        public void OnExited() => ExitedCount++;

        public void Release() => release.Set();
    }

    private sealed class CountingExportObserver : IPrintExportObserver
    {
        public int EnteredCount { get; private set; }

        public int ExitedCount { get; private set; }

        public void OnEntered() => EnteredCount++;

        public void OnExited() => ExitedCount++;
    }
}
