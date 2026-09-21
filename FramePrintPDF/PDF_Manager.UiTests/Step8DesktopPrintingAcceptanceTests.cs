using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Printing;
using ViewportCapture = PDF_Manager.Printing.ViewportCapture;

namespace PDF_Manager.UiTests;

public sealed class Step8DesktopPrintingAcceptanceTests
{
    [Fact]
    public void RealPreviewDialog_NavigatesPageSpecificTableAndDiagramRastersFromTheExportPlan()
    {
        StaTestRunner.Run(() =>
        {
            AnalysisResultSet results = ReadResultFixture("single-static.json");
            ResultCoordinate coordinate = results.Results[0].Coordinate;
            PrintExportRequest request = new(
                ProjectDocumentPresets.CreateRepresentativeFrame(),
                results,
                [coordinate],
                new PrintPageSettings(
                    PrintPaperSize.A4,
                    PrintPageOrientation.Portrait,
                    layout: PrintLayoutChoice.SectionPerPage),
                [PrintContentSection.DisplacementResults, PrintContentSection.ModelDiagram]);
            byte[] modelPixels = Enumerable.Range(0, 8 * 4)
                .SelectMany(index => new[] { (byte)230, (byte)(index * 5), (byte)40 })
                .ToArray();
            PrintDiagramCaptureSet captures = new(
                [new KeyValuePair<PDF_Manager.Printing.PrintDiagramKind, ViewportCapture>(
                    PDF_Manager.Printing.PrintDiagramKind.Model,
                    new ViewportCapture(8, 4, modelPixels))]);
            DesktopPdfExporter exporter = new(localization: new LocalizationService(UiLanguage.English));

            PrintPreviewResult preview = exporter.PreviewAsync(request, captures).GetAwaiter().GetResult();
            using MemoryStream pdf = new();
            PrintExportReceipt receipt = exporter.ExportWithResultAsync(request, captures, pdf).GetAwaiter().GetResult();

            Assert.Equal(2, preview.PageCount);
            Assert.Equal(preview.PlanIdentity, receipt.PlanIdentity);
            Assert.Equal(
                [PrintContentSection.DisplacementResults, PrintContentSection.ModelDiagram],
                preview.Pages.Select(page => Assert.Single(page.Sections)));
            Assert.Equal(
                [preview.PlanIdentity + ":1", preview.PlanIdentity + ":2"],
                preview.Pages.Select(page => page.PageIdentity));
            Assert.All(preview.Pages, page =>
            {
                double pageAspect = page.WidthMillimetres / page.HeightMillimetres;
                double captureAspect = (double)page.RenderedPageCapture.Width / page.RenderedPageCapture.Height;
                Assert.InRange(captureAspect, pageAspect - 0.01, pageAspect + 0.01);
            });
            string[] previewHashes = preview.Pages
                .Select(page => Convert.ToHexString(SHA256.HashData(page.RenderedPageCapture.Rgb24.Span)))
                .ToArray();
            Assert.NotEqual(previewHashes[0], previewHashes[1]);

            using PDF_Manager.Shell.Printing.PrintPreviewDialog dialog = new(
                new LocalizationService(UiLanguage.English),
                new PrintPreviewState(preview));
            dialog.Show();
            Application.DoEvents();
            Assert.Equal(previewHashes[0], BitmapRgbHash(Assert.IsType<Bitmap>(dialog.PreviewImage.Image)));
            Assert.Equal(0, dialog.CurrentState.SelectedPageIndex);

            dialog.NextButton.PerformClick();
            Application.DoEvents();

            Assert.Equal(1, dialog.CurrentState.SelectedPageIndex);
            Assert.Equal(previewHashes[1], BitmapRgbHash(Assert.IsType<Bitmap>(dialog.PreviewImage.Image)));
            Assert.Equal(preview.Pages[1].RenderedPageCapture.Width, dialog.PreviewImage.Image!.Width);
            Assert.Equal(preview.Pages[1].RenderedPageCapture.Height, dialog.PreviewImage.Image.Height);
            Assert.Contains("2", dialog.PageLabel.Text, StringComparison.Ordinal);
            Assert.True(pdf.Length > 1_000);
        }, "Step 8 real page-specific print preview dialog", TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void InFlightCancellation_PreservesExistingFileAndRemovesPartialTemporaryFile()
    {
        StaTestRunner.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), $"frameweb-step8-cancel-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, "existing.pdf");
            byte[] sentinel = Encoding.UTF8.GetBytes("existing-pdf-must-survive-cancellation");
            File.WriteAllBytes(target, sentinel);
            try
            {
                BlockingPartialExportExporter exporter = new();
                FakeShellDialogs dialogs = new() { PdfToExport = target };
                using MainForm form = new(new MainFormServices(
                    printExporter: exporter,
                    localization: new LocalizationService(UiLanguage.English),
                    dialogs: dialogs,
                    layoutStore: new NoOpLayoutStore()));
                form.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());

                Task operation = form.ExportPdfAsync();
                PumpUntil(() => exporter.Started.Task.IsCompleted, "The PDF exporter did not start.");
                form.CancelOperation();
                Pump(operation);

                Assert.True(exporter.CancellationObserved.Task.IsCompleted);
                Assert.Equal(sentinel, File.ReadAllBytes(target));
                Assert.Equal([target], Directory.GetFiles(directory));
                Assert.Empty(dialogs.Errors);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }, "Step 8 in-flight atomic PDF cancellation");
    }

    [Fact]
    public void FailedExport_PreservesExistingFileRemovesTemporaryFileAndKeepsOriginalCause()
    {
        StaTestRunner.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), $"frameweb-step8-pdf-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, "existing.pdf");
            byte[] sentinel = Encoding.UTF8.GetBytes("existing-pdf-must-survive");
            File.WriteAllBytes(target, sentinel);
            try
            {
                IOException cause = new("destination failure");
                FakeShellDialogs dialogs = new() { PdfToExport = target };
                List<Exception> diagnostics = [];
                using MainForm form = new(new MainFormServices(
                    printExporter: new FailingExportExporter(cause),
                    localization: new LocalizationService(UiLanguage.English),
                    dialogs: dialogs,
                    reportDiagnostic: diagnostics.Add,
                    layoutStore: new NoOpLayoutStore(),
                    viewportCaptureProvider: new FixedCaptureProvider()));
                form.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());

                Pump(form.ExportPdfAsync());

                Assert.Equal(sentinel, File.ReadAllBytes(target));
                Assert.Equal([target], Directory.GetFiles(directory));
                PrintExportException reported = Assert.IsType<PrintExportException>(Assert.Single(diagnostics));
                Assert.Same(cause, reported.InnerException);
                Assert.Single(dialogs.Errors);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }, "Step 8 atomic PDF failure preservation");
    }

    [Theory]
    [InlineData(UiLanguage.Japanese, PrintContentLanguage.Japanese)]
    [InlineData(UiLanguage.Chinese, PrintContentLanguage.SimplifiedChinese)]
    public void PageSetupPreviewNavigationLocalizationAndFailurePreservation_AreEndToEnd(
        UiLanguage uiLanguage,
        PrintContentLanguage expectedLanguage)
    {
        StaTestRunner.Run(() =>
        {
            PrintPageSettings settings = new(
                PrintPaperSize.A3,
                PrintPageOrientation.Landscape,
                leftMarginMillimetres: 12,
                topMarginMillimetres: 13,
                rightMarginMillimetres: 14,
                bottomMarginMillimetres: 15,
                scale: 1.25,
                layout: PrintLayoutChoice.SectionPerPage,
                showPageNumbers: true);
            PrintContentSection[] sections =
            [
                PrintContentSection.ProjectSummary,
                PrintContentSection.InputTables,
                PrintContentSection.ModelDiagram,
            ];
            FakePrintDialogs printDialogs = new()
            {
                PageSetupResult = new PrintPageSetupSelection(settings, sections),
            };
            RecordingPreviewExporter exporter = new(CreatePreview(sections));
            FakeShellDialogs shellDialogs = new();
            using MainForm form = new(new MainFormServices(
                printExporter: exporter,
                localization: new LocalizationService(uiLanguage),
                dialogs: shellDialogs,
                printDialogs: printDialogs,
                layoutStore: new NoOpLayoutStore(),
                viewportCaptureProvider: new FixedCaptureProvider()));
            form.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());

            Assert.True(form.ConfigurePrintPage());
            Assert.Equal(settings, form.PrintSelection.PageSettings);
            Assert.Equal(sections, form.PrintSelection.Sections);
            Pump(form.RefreshPrintPreviewAsync(showDialog: true));

            PrintExportRequest request = Assert.Single(exporter.Requests);
            Assert.Equal(expectedLanguage, request.Language);
            Assert.Equal(settings, request.PageSettings);
            Assert.Equal(sections, request.Sections);
            PrintPreviewState preview = Assert.IsType<PrintPreviewState>(form.CurrentPrintPreview);
            Assert.Equal(3, preview.PageCount);
            Assert.Equal(0, preview.SelectedPageIndex);
            Assert.Same(preview, Assert.Single(printDialogs.ShownPreviews));
            Assert.True(form.SelectPrintPreviewPage(2));
            PrintPreviewState selected = Assert.IsType<PrintPreviewState>(form.CurrentPrintPreview);
            Assert.Equal(2, selected.SelectedPageIndex);
            Assert.Equal(PrintContentSection.ModelDiagram, Assert.Single(selected.SelectedSections));
            Assert.False(form.SelectPrintPreviewPage(3));

            exporter.NextPreview = Task.FromException<PrintPreviewResult>(
                new PrintExportException(OperationFailureKind.Protocol, "preview failed"));
            Pump(form.RefreshPrintPreviewAsync());

            Assert.Same(selected, form.CurrentPrintPreview);
            Assert.Equal("preview failed", Assert.Single(shellDialogs.Errors).Message);
        }, $"Step 8 {uiLanguage} desktop printing acceptance");
    }

    private static PrintPreviewResult CreatePreview(IReadOnlyList<PrintContentSection> sections)
    {
        const string planIdentity = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        return new PrintPreviewResult(
            planIdentity,
            sections,
            [
                new PrintPreviewPage(
                    1,
                    420,
                    297,
                    [PrintContentSection.ProjectSummary],
                    SolidPreviewCapture(2, 3, 220, 20, 20),
                    planIdentity + ":1",
                    "Project summary page"),
                new PrintPreviewPage(
                    2,
                    420,
                    297,
                    [PrintContentSection.InputTables],
                    SolidPreviewCapture(3, 2, 20, 220, 20),
                    planIdentity + ":2",
                    "Input tables page"),
                new PrintPreviewPage(
                    3,
                    420,
                    297,
                    [PrintContentSection.ModelDiagram],
                    SolidPreviewCapture(2, 2, 20, 20, 220),
                    planIdentity + ":3",
                    "Model diagram page"),
            ]);
    }

    private static PrintPreviewCapture SolidPreviewCapture(
        int width,
        int height,
        byte red,
        byte green,
        byte blue)
    {
        byte[] rgb = new byte[checked(width * height * 3)];
        for (int index = 0; index < rgb.Length; index += 3)
        {
            rgb[index] = red;
            rgb[index + 1] = green;
            rgb[index + 2] = blue;
        }

        return new PrintPreviewCapture(width, height, rgb);
    }

    private static string BitmapRgbHash(Bitmap bitmap)
    {
        byte[] rgb = new byte[checked(bitmap.Width * bitmap.Height * 3)];
        int offset = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                rgb[offset++] = pixel.R;
                rgb[offset++] = pixel.G;
                rgb[offset++] = pixel.B;
            }
        }

        return Convert.ToHexString(SHA256.HashData(rgb));
    }

    private static AnalysisResultSet ReadResultFixture(string fileName) => AnalysisResultSetJson.Deserialize(
        File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "FrameWeb",
            "tests",
            "data",
            "contracts",
            "positive",
            fileName)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static void Pump(Task task)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(10), "The print UI operation did not complete.");
            Application.DoEvents();
            Thread.Sleep(1);
        }

        task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> condition, string message)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(10), message);
            Application.DoEvents();
            Thread.Sleep(1);
        }
    }

    private sealed class RecordingPreviewExporter(PrintPreviewResult first) : IPrintExporter
    {
        private Task<PrintPreviewResult> _nextPreview = Task.FromResult(first);

        public List<PrintExportRequest> Requests { get; } = [];

        public Task<PrintPreviewResult> NextPreview
        {
            set => _nextPreview = value;
        }

        public Task<PrintPreviewResult> PreviewAsync(
            PrintExportRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            Task<PrintPreviewResult> result = _nextPreview;
            _nextPreview = Task.FromResult(first);
            return result;
        }

        public Task ExportAsync(
            PrintExportRequest request,
            Stream destination,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FailingExportExporter(IOException failure) : IPrintExporter
    {
        public async Task ExportAsync(
            PrintExportRequest request,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync("partial"u8.ToArray(), cancellationToken);
            throw failure;
        }
    }

    private sealed class BlockingPartialExportExporter : IPrintExporter
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ExportAsync(
            PrintExportRequest request,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync("partial-pdf"u8.ToArray(), cancellationToken);
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult();
                throw;
            }
        }
    }

    private sealed class FakePrintDialogs : IPrintDialogService
    {
        public PrintPageSetupSelection? PageSetupResult { get; init; }

        public List<PrintPreviewState> ShownPreviews { get; } = [];

        public PrintPageSetupSelection? ShowPageSetup(
            IWin32Window owner,
            LocalizationService localization,
            PrintPageSetupSelection current) => PageSetupResult;

        public void ShowPreview(
            IWin32Window owner,
            LocalizationService localization,
            PrintPreviewState preview) => ShownPreviews.Add(preview);
    }

    private sealed class FixedCaptureProvider : IViewportCaptureProvider
    {
        private static readonly ViewportCapture CaptureValue = new(2, 2, new byte[12]);

        public ViewportCapture Capture(ProjectDocumentContent documentHost) => CaptureValue;
    }

    private sealed class NoOpLayoutStore : IShellLayoutStore
    {
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task SaveAsync(string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
