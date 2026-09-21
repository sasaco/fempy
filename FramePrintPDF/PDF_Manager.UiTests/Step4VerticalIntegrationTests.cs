using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Printing;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Composition;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Lifecycle;
using PDF_Manager.Shell.Printing;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.UiTests;

public sealed class Step4VerticalIntegrationTests
{
    [Fact]
    public void RepresentativePreset_EditUndoRedoSaveReopenAnalyzeInspectAndExportPdf()
    {
        StaTestRunner.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), $"frameweb-step4-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string projectPath = Path.Combine(directory, "representative.frameweb.json");
            string pdfPath = Path.Combine(directory, "representative.pdf");
            try
            {
                AnalysisResultSet result = ReadSingleStaticResult(jEndFx: 20);
                FakeShellDialogs dialogs = new()
                {
                    ProjectToSave = projectPath,
                    ProjectToOpen = projectPath,
                    PdfToExport = pdfPath,
                };
                ExplicitHeadlessViewportCaptureProvider captureProvider = new();
                using MainForm form = new(new MainFormServices(
                    projectStore: new JsonProjectStore(),
                    analysisClient: new ResultAnalysisClient(result),
                    localization: new LocalizationService(UiLanguage.English),
                    dialogs: dialogs,
                    layoutStore: new NoOpLayoutStore(),
                    viewportCaptureProvider: captureProvider));
                Pump(form.NewProjectAsync());
                Assert.Equal(2, form.CurrentDocument!.Nodes.Count);
                Assert.Single(form.CurrentDocument.Members);
                Assert.Equal(2, form.EditorPane.NodeGrid.Rows.Count);

                EditTextCell(form.EditorPane.NodeGrid, rowIndex: 1, columnIndex: 1, "5");
                Assert.Equal(5, form.CurrentDocument!.Nodes.Single(node => node.Id == "2").X);
                Assert.True(form.CurrentDocument.IsDirty);
                Assert.True(form.EditorPane.Undo());
                Assert.Equal(4, form.CurrentDocument!.Nodes.Single(node => node.Id == "2").X);
                Assert.True(form.EditorPane.Redo());
                Assert.Equal(5, form.CurrentDocument!.Nodes.Single(node => node.Id == "2").X);

                int diagnosticsBeforeInvalidEdit = form.DiagnosticsPane.Messages.Items.Count;
                EditTextCell(form.EditorPane.NodeGrid, rowIndex: 1, columnIndex: 1, "not-a-number");
                Assert.Equal(5, form.CurrentDocument!.Nodes.Single(node => node.Id == "2").X);
                Assert.Equal("5", Convert.ToString(form.EditorPane.NodeGrid.Rows[1].Cells[1].Value));
                Assert.Equal(diagnosticsBeforeInvalidEdit + 1, form.DiagnosticsPane.Messages.Items.Count);

                Pump(form.SaveProjectAsync(saveAs: true));
                Assert.True(File.Exists(projectPath));
                Assert.False(form.CurrentDocument!.IsDirty);
                form.SetDocument(null);
                Pump(form.OpenProjectAsync());
                Assert.Equal(5, form.CurrentDocument!.Nodes.Single(node => node.Id == "2").X);

                EditTextCell(form.EditorPane.NodeGrid, rowIndex: 1, columnIndex: 1, "6");
                form.SaveMenuItem.PerformClick();
                PumpUntil(() => form.CurrentDocument?.IsDirty == false, "The ordinary Save menu command did not finish.");
                ProjectDocument saved = new JsonProjectStore().OpenAsync(projectPath).GetAwaiter().GetResult();
                Assert.Equal(6, saved.Nodes.Single(node => node.Id == "2").X);

                Pump(form.ExecuteAnalysisAsync());
                Assert.Same(result, form.CurrentResult);
                Assert.Equal(2, form.DocumentHost.ResultGrid.Rows.Count);
                form.DocumentHost.ResultTableSelector.SelectedIndex = 1;
                Assert.Single(form.DocumentHost.ResultGrid.Rows.Cast<DataGridViewRow>());
                form.DocumentHost.ResultTableSelector.SelectedIndex = 2;
                DataGridViewRow[] forceRows = form.DocumentHost.ResultGrid.Rows.Cast<DataGridViewRow>().ToArray();
                Assert.Equal(2, forceRows.Length);
                Assert.Equal(["I", "J"], forceRows.Select(row => Convert.ToString(row.Cells[2].Value)!).ToArray());
                Assert.Equal(["10", "20"], forceRows.Select(row => Convert.ToString(row.Cells[3].Value)!).ToArray());
                Assert.False(form.DocumentHost.ResultTableTruncated);

                Pump(form.ExportPdfAsync());
                ViewportCapture expectedCapture = Assert.IsType<ViewportCapture>(captureProvider.LastCapture);
                Assert.Equal(1, captureProvider.CaptureCalls);
                byte[] pdf = File.ReadAllBytes(pdfPath);
                Assert.True(pdf.Length > 1_000);
                Assert.StartsWith("%PDF-1.", Encoding.ASCII.GetString(pdf, 0, 8), StringComparison.Ordinal);
                string pdfText = Encoding.Latin1.GetString(pdf);
                using PdfDocument parsed = PdfReader.Open(
                    new MemoryStream(pdf, writable: false),
                    PdfDocumentOpenMode.Import);
                Assert.True(parsed.PageCount >= 2);
                Assert.Equal(form.CurrentDocument.Metadata.Name, parsed.Info.Title);
                Assert.All(parsed.Pages.Cast<PdfPage>(), page => Assert.NotEmpty(page.Contents.Elements));
                Assert.Contains("/Subtype/Image", pdfText.Replace(" ", string.Empty), StringComparison.Ordinal);
                Assert.Contains("/ToUnicode", pdfText, StringComparison.Ordinal);
                Assert.Contains($"/Width {expectedCapture.Width}", pdfText, StringComparison.Ordinal);
                Assert.Contains($"/Height {expectedCapture.Height}", pdfText, StringComparison.Ordinal);
                byte[] expectedRgb = expectedCapture.Rgb24.ToArray();
                byte[] embeddedRgb = Assert.Single(
                    Step8DesktopProjectionRemediationTests.ReadDecodedImageStreams(pdf),
                    candidate => candidate.AsSpan().SequenceEqual(expectedRgb));
                Assert.Equal(
                    Convert.ToHexString(SHA256.HashData(expectedRgb)),
                    Convert.ToHexString(SHA256.HashData(embeddedRgb)));
                string selectedResultText = Step8DesktopProjectionRemediationTests.ExtractMappedPdfText(pdf);
                Assert.Contains("I", selectedResultText, StringComparison.Ordinal);
                Assert.Contains("J", selectedResultText, StringComparison.Ordinal);
                Assert.Contains("10", selectedResultText, StringComparison.Ordinal);
                Assert.Contains("20", selectedResultText, StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }, "Step 4 representative vertical integration", TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task LiveViewportCaptureUsesShownRendererInBoundedStaChildProcess()
    {
        string root = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not resolve the test build configuration.");
        string probe = Path.Combine(
            root,
            "FramePrintPDF",
            "PDF_Manager.UiTests",
            "LiveCaptureProbe",
            "bin",
            configuration,
            "net8.0-windows",
            "LiveCaptureProbe.dll");
        Assert.True(File.Exists(probe), $"Live capture probe was not built: {probe}");
        ProcessStartInfo start = new("dotnet")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(probe);
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the live capture probe.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail("The process-isolated live viewport capture exceeded 15 seconds.");
        }

        string output = await outputTask;
        string error = await errorTask;
        Assert.True(process.ExitCode == 0, $"Live capture probe failed: {error}");
        Assert.Matches(@"^LIVE_CAPTURE_OK \d+ \d+ [0-9A-F]{64}\s*$", output);
    }

    [Fact]
    public void TableAndViewportSelectionUseTheSameStableEntityKey()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = new(new MainFormServices(
                localization: new LocalizationService(UiLanguage.English),
                layoutStore: new NoOpLayoutStore()));
            form.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());

            form.EditorPane.NodeGrid.ClearSelection();
            form.EditorPane.NodeGrid.Rows[1].Selected = true;
            Application.DoEvents();
            SceneEntityKey expected = new(SceneEntityKind.Node, "2");
            Assert.Equal(expected, form.DocumentHost.Selection);

            SceneEntityKey member = new(SceneEntityKind.Member, "1");
            form.DocumentHost.SetSelection(member, ViewportSelectionOrigin.Viewport);
            Application.DoEvents();
            DataGridViewRow selected = Assert.Single(
                form.EditorPane.MemberGrid.SelectedRows.Cast<DataGridViewRow>());
            Assert.Equal(member, Assert.IsType<SceneEntityKey>(selected.Tag));
        }, "Step 4 selection synchronization");
    }

    [Fact]
    public void SceneMappingPreservesRotationOnlySupportAndMomentOnlyNodalLoad()
    {
        StaTestRunner.Run(() =>
        {
            ProjectDocumentEditSession edit = new(ProjectDocumentPresets.CreateRepresentativeFrame());
            Assert.True(edit.UpsertSupport(new ProjectSupport(
                "S-ROT",
                "2",
                false,
                false,
                false,
                true,
                false,
                true)));
            Assert.True(edit.UpsertNodalLoad(new NodalLoadDefinition(
                "M-ONLY",
                "1",
                "2",
                0,
                0,
                0,
                4,
                -5,
                6)));

            using MainForm form = new(new MainFormServices(
                localization: new LocalizationService(UiLanguage.English),
                layoutStore: new NoOpLayoutStore()));
            form.SetDocument(edit.Current);

            ViewportSceneModel scene = Assert.IsType<ViewportSceneModel>(form.DocumentHost.CurrentScene);
            Assert.Equal(edit.Current.Supports.Count, scene.Supports.Count);
            SceneSupport support = Assert.Single(scene.Supports, candidate => candidate.Id == "S-ROT");
            Assert.False(support.FixX);
            Assert.False(support.FixY);
            Assert.False(support.FixZ);
            Assert.True(support.FixRx);
            Assert.False(support.FixRy);
            Assert.True(support.FixRz);

            SceneNodalLoad load = Assert.Single(scene.NodalLoads, candidate => candidate.Id == "M-ONLY");
            Assert.Equal(default, load.Vector);
            Assert.Equal(new ScenePoint3(4, -5, 6), load.Moment);
            Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.Support, support.Id)));
            Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.NodalLoad, load.Id)));
        }, "Step 4 rotational support and moment load scene mapping");
    }

    [Fact]
    public void DisplacementLayerSelectorMapsAnalysisResultIntoCurrentScene()
    {
        StaTestRunner.Run(() =>
        {
            AnalysisResultSet result = ReadSingleStaticResult();
            ForceAnalysisResult expectedResult = result.Results.OfType<ForceAnalysisResult>().First();
            List<string> viewportFailures = [];
            using MainForm form = new(new MainFormServices(
                analysisClient: new ResultAnalysisClient(result),
                localization: new LocalizationService(UiLanguage.English),
                layoutStore: new NoOpLayoutStore()));
            form.DocumentHost.ViewportFailed += (_, eventArgs) => viewportFailures.Add(eventArgs.Message);
            form.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            Pump(form.ExecuteAnalysisAsync());

            form.DocumentHost.ResultLayerSelector.SelectedIndex = 1;
            Application.DoEvents();

            ViewportSceneModel scene = Assert.IsType<ViewportSceneModel>(form.DocumentHost.CurrentScene);
            SceneDisplacementLayer displacement = Assert.IsType<SceneDisplacementLayer>(scene.Displacement);
            Assert.Equal(20, displacement.Scale);
            Assert.Equal(
                expectedResult.NodeDisplacements.Select(row => row.NodeId),
                displacement.Nodes.Select(row => row.NodeId));
            foreach (NodeDisplacement expected in expectedResult.NodeDisplacements)
            {
                SceneNodeDisplacement actual = Assert.Single(
                    displacement.Nodes,
                    candidate => candidate.NodeId == expected.NodeId);
                Assert.Equal(
                    new ScenePoint3(
                        checked((float)expected.Components.Dx),
                        checked((float)expected.Components.Dy),
                        checked((float)expected.Components.Dz)),
                    actual.Vector);
            }

            SceneEntityKey selected = new(SceneEntityKind.Node, expectedResult.NodeDisplacements[0].NodeId);
            form.DocumentHost.SetSelection(selected);
            Assert.Equal(selected, form.DocumentHost.Selection);
            Assert.Empty(viewportFailures);
        }, "Step 4 displacement result scene mapping");
    }

    [Fact]
    public void CancellationAndBackendFailurePreserveThePriorValidatedResult()
    {
        StaTestRunner.Run(() =>
        {
            AnalysisResultSet result = ReadSingleStaticResult();
            SequenceAnalysisClient analysis = new(result);
            FakeShellDialogs dialogs = new();
            using MainForm form = new(new MainFormServices(
                analysisClient: analysis,
                localization: new LocalizationService(UiLanguage.English),
                dialogs: dialogs,
                layoutStore: new NoOpLayoutStore()));
            form.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());

            Pump(form.ExecuteAnalysisAsync());
            Assert.Same(result, form.CurrentResult);

            analysis.Next = Task.FromException<AnalysisResultSet>(
                new AnalysisClientException(OperationFailureKind.Unavailable, "Backend unavailable."));
            Pump(form.ExecuteAnalysisAsync());
            Assert.Same(result, form.CurrentResult);
            Assert.Equal("Backend unavailable.", Assert.Single(dialogs.Errors).Message);

            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            analysis.Next = Task.FromCanceled<AnalysisResultSet>(cancellation.Token);
            Pump(form.ExecuteAnalysisAsync());
            Assert.Same(result, form.CurrentResult);
        }, "Step 4 prior result preservation");
    }

    [Fact]
    public void ExportPdfMenuCatchesUiThreadViewportCaptureFailure()
    {
        StaTestRunner.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), $"frameweb-capture-failure-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                AnalysisResultSet result = ReadSingleStaticResult();
                FakeShellDialogs dialogs = new()
                {
                    PdfToExport = Path.Combine(directory, "must-not-exist.pdf"),
                };
                ThrowingViewportCaptureProvider captureProvider = new();
                List<Exception> diagnostics = [];
                using MainForm form = new(new MainFormServices(
                    analysisClient: new ResultAnalysisClient(result),
                    localization: new LocalizationService(UiLanguage.English),
                    dialogs: dialogs,
                    layoutStore: new NoOpLayoutStore(),
                    viewportCaptureProvider: captureProvider,
                    reportDiagnostic: diagnostics.Add));
                form.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
                Pump(form.ExecuteAnalysisAsync());
                Assert.True(form.ExportPdfMenuItem.Enabled);

                int uiThreadId = Environment.CurrentManagedThreadId;
                Exception? escaped = null;
                ThreadExceptionEventHandler handler = (_, eventArgs) => escaped = eventArgs.Exception;
                Application.ThreadException += handler;
                try
                {
                    form.ExportPdfMenuItem.PerformClick();
                    PumpUntil(() => dialogs.Errors.Count == 1, "The capture failure was not reported.");
                    Pump(form.WhenCurrentOperationIdleAsync());
                    Application.DoEvents();
                }
                finally
                {
                    Application.ThreadException -= handler;
                }

                Assert.Null(escaped);
                Assert.Equal(uiThreadId, captureProvider.CaptureThreadId);
                Assert.Same(captureProvider.Failure, Assert.Single(diagnostics));
                Assert.Equal("An unexpected error occurred.", Assert.Single(dialogs.Errors).Message);
                Assert.False(form.IsOperationRunning);
                Assert.False(File.Exists(dialogs.PdfToExport));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }, "Step 4 viewport capture exception boundary");
    }

    [Fact]
    public void DesktopSessionKeepsUiOnOriginalStaWhenRuntimeStartupCompletesAsynchronously()
    {
        StaTestRunner.Run(() =>
        {
            FakeDesktopRuntime runtime = new();
            LocalizationService localization = new(UiLanguage.English);
            int originalThreadId = Environment.CurrentManagedThreadId;
            int formThreadId = -1;
            int runThreadId = -1;

            DesktopApplicationSession.Run(
                runtime,
                _ =>
                {
                    formThreadId = Environment.CurrentManagedThreadId;
                    Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
                    MainFormServices services = DesktopApplicationSession.CreateServices(runtime, localization);
                    return new MainForm(services);
                },
                form =>
                {
                    runThreadId = Environment.CurrentManagedThreadId;
                    Assert.True(runtime.Started);
                    Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
                });

            Assert.Equal(1, runtime.StartCalls);
            Assert.Equal(1, runtime.StopCalls);
            Assert.Equal(1, runtime.DisposeCalls);
            Assert.Equal(1, runtime.CreateHttpClientCalls);
            Assert.Equal(originalThreadId, formThreadId);
            Assert.Equal(originalThreadId, runThreadId);
            Assert.Equal(originalThreadId, runtime.CreateHttpClientThreadId);
            Assert.NotEqual(originalThreadId, runtime.StartCompletionThreadId);
            Assert.Equal(runtime.Endpoint, runtime.CreatedClient!.BaseAddress);
            Assert.ThrowsAsync<ObjectDisposedException>(() => runtime.CreatedClient.GetAsync("/"))
                .GetAwaiter()
                .GetResult();
        }, "Step 4 injected runtime disposal");
    }

    [Fact]
    public void LocalizedResourcesHaveExactKeyParity()
    {
        string resources = Path.Combine(FindRepositoryRoot(), "FramePrintPDF", "PDF_Manager", "Resources");
        string[] files = ["Strings.resx", "Strings.en.resx", "Strings.ja.resx", "Strings.zh.resx"];
        string[] expected = ReadResourceKeys(Path.Combine(resources, files[0]));

        foreach (string file in files.Skip(1))
        {
            Assert.Equal(expected, ReadResourceKeys(Path.Combine(resources, file)));
        }
    }

    private static AnalysisResultSet ReadSingleStaticResult(double? jEndFx = null)
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "FrameWeb", "tests", "data", "contracts", "positive", "single-static.json");
        string json = File.ReadAllText(path);
        if (jEndFx is not null)
        {
            json = json.Replace(
                "\"j_end\": {\"fx\": 10",
                $"\"j_end\": {{\"fx\": {jEndFx.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                StringComparison.Ordinal);
        }

        return AnalysisResultSetJson.Deserialize(Encoding.UTF8.GetBytes(json));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static void Pump(Task task)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(10), "The UI operation did not complete.");
            Application.DoEvents();
            Thread.Sleep(1);
        }

        task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> predicate, string message)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!predicate())
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(10), message);
            Application.DoEvents();
            Thread.Sleep(1);
        }
    }

    private static void EditTextCell(DataGridView grid, int rowIndex, int columnIndex, string value)
    {
        grid.CurrentCell = grid.Rows[rowIndex].Cells[columnIndex];
        Assert.True(grid.BeginEdit(selectAll: true));
        TextBox editingControl = Assert.IsType<DataGridViewTextBoxEditingControl>(grid.EditingControl);
        editingControl.Text = value;
        Assert.True(grid.EndEdit());
        Application.DoEvents();
    }

    private static byte[] ExtractViewportRgb(byte[] pdf)
    {
        string text = Encoding.Latin1.GetString(pdf);
        int objectStart = text.IndexOf("6 0 obj\n", StringComparison.Ordinal);
        int streamMarker = text.IndexOf("stream\n", objectStart, StringComparison.Ordinal);
        Match length = Regex.Match(text[objectStart..streamMarker], @"/Length (?<length>\d+)");
        Assert.True(length.Success);
        int byteLength = int.Parse(length.Groups["length"].Value, System.Globalization.CultureInfo.InvariantCulture);
        int dataStart = streamMarker + "stream\n".Length;
        using MemoryStream input = new(pdf, dataStart, byteLength, writable: false);
        using ZLibStream compressed = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        compressed.CopyTo(output);
        return output.ToArray();
    }

    private static string[] ReadResourceKeys(string path)
    {
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(path);
        return document.Root!.Elements("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => name is not null)
            .Cast<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class ResultAnalysisClient(AnalysisResultSet result) : IAnalysisClient
    {
        public Task<AnalysisResultSet> AnalyzeAsync(
            ProjectDocument document,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class SequenceAnalysisClient(AnalysisResultSet first) : IAnalysisClient
    {
        private Task<AnalysisResultSet> _next = Task.FromResult(first);

        public Task<AnalysisResultSet> Next
        {
            set => _next = value;
        }

        public Task<AnalysisResultSet> AnalyzeAsync(
            ProjectDocument document,
            CancellationToken cancellationToken = default)
        {
            Task<AnalysisResultSet> next = _next;
            _next = Task.FromResult(first);
            return next;
        }
    }

    private sealed class NoOpLayoutStore : IShellLayoutStore
    {
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task SaveAsync(string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDesktopRuntime : IDesktopRuntime
    {
        public Uri Endpoint { get; } = new("http://127.0.0.1:8080/");

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public int StartCompletionThreadId { get; private set; }

        public bool Started => StartCalls == 1 && StopCalls == 0;

        public int CreateHttpClientCalls { get; private set; }

        public int CreateHttpClientThreadId { get; private set; }

        public HttpClient? CreatedClient { get; private set; }

        public HttpClient CreateHttpClient()
        {
            if (!Started)
            {
                throw new InvalidOperationException("The fake runtime must be started first.");
            }

            CreateHttpClientCalls++;
            CreateHttpClientThreadId = Environment.CurrentManagedThreadId;
            CreatedClient = new HttpClient { BaseAddress = Endpoint };
            return CreatedClient;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            StartCompletionThreadId = Environment.CurrentManagedThreadId;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ExplicitHeadlessViewportCaptureProvider : IViewportCaptureProvider
    {
        public int CaptureCalls { get; private set; }

        public ViewportCapture? LastCapture { get; private set; }

        public ViewportCapture Capture(PDF_Manager.Shell.Contents.ProjectDocumentContent documentHost)
        {
            ProjectDocument document = documentHost.Document
                ?? throw new InvalidOperationException("A document is required for the explicit headless test capture.");
            PdfModelNode[] nodes = document.Nodes
                .Select(node => new PdfModelNode(node.Id, node.X, node.Y, node.Z))
                .ToArray();
            PdfModelMember[] members = document.Members
                .Select(member => new PdfModelMember(member.Id, member.NodeI, member.NodeJ))
                .ToArray();
            CaptureCalls++;
            LastCapture = ModelViewportCapture.Create(
                nodes,
                members,
                document.Supports.Select(support => support.NodeId),
                document.Selection.NodeIds);
            return LastCapture;
        }
    }

    private sealed class ThrowingViewportCaptureProvider : IViewportCaptureProvider
    {
        public InvalidOperationException Failure { get; } = new("Viewport capture failed.");

        public int CaptureThreadId { get; private set; }

        public ViewportCapture Capture(PDF_Manager.Shell.Contents.ProjectDocumentContent documentHost)
        {
            CaptureThreadId = Environment.CurrentManagedThreadId;
            throw Failure;
        }
    }
}
