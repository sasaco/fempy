using System.Diagnostics;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Docking;
using PDF_Manager.Shell.Lifecycle;
using CoreDockState = PDF_Manager.Core.Shell.DockState;

namespace PDF_Manager.UiTests;

public sealed class Step3MainFormRemediationTests
{
    [Fact]
    public void DelayedDirtySave_IsSerializedAndItsStaleContinuationCannotReplaceOrCloseNewDocument()
    {
        StaTestRunner.Run(() =>
        {
            DelayedSaveProjectStore store = new();
            SequencedShellDialogs dialogs = new(
                DirtyDocumentCloseDecision.Save,
                DirtyDocumentCloseDecision.Cancel,
                DirtyDocumentCloseDecision.Cancel)
            {
                ProjectToSave = "delayed-a.frameweb.json",
            };
            InMemoryLayoutStore layoutStore = new();
            MainForm form = new(CreateServices(
                projectStore: store,
                dialogs: dialogs,
                layoutStore: layoutStore));
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());
            ProjectDocument documentA = CreateDocument("Document A", isDirty: true);
            ProjectDocument documentB = CreateDocument("Document B", isDirty: true);
            form.SetDocument(documentA);

            Task firstNew = form.NewProjectAsync();
            PumpUntil(() => store.SaveStarted.Task.IsCompleted);
            Assert.False(GetMenuItem(form, "NewMenuItem").Enabled);
            Assert.False(form.OpenMenuItem.Enabled);
            Assert.False(form.SaveMenuItem.Enabled);
            Assert.False(GetMenuItem(form, "SaveAsMenuItem").Enabled);
            Assert.False(form.AnalyzeMenuItem.Enabled);
            Assert.False(form.ExportPdfMenuItem.Enabled);

            form.SetDocument(documentB);
            Task queuedNew = form.NewProjectAsync();
            Assert.False(queuedNew.IsCompleted);
            store.CompleteSave();
            WaitFor(Task.WhenAll(firstNew, queuedNew));

            Assert.Same(documentB, form.CurrentDocument);
            Assert.Equal(new[] { "Document A", "Document B" }, dialogs.ConfirmedDocumentNames);
            Assert.Equal(1, store.SaveCalls);

            form.Close();
            PumpUntil(() => dialogs.ConfirmCloseCalls == 3 && form.WhenShellTransitionIdleAsync().IsCompleted);

            Assert.True(form.Visible);
            Assert.False(form.IsDisposed);
            Assert.Same(documentB, form.CurrentDocument);
            Assert.Equal(new[] { "Document A", "Document B", "Document B" }, dialogs.ConfirmedDocumentNames);

            form.SetDocument(documentB.MarkSaved());
            form.Close();
            PumpUntil(() => form.IsDisposed);
        }, "Serialized dirty-save document replacement");
    }

    [Fact]
    public void FormClosing_CancelsAndAwaitsCooperativeTerminalCleanupBeforePersistingAndDisposing()
    {
        StaTestRunner.Run(() =>
        {
            CooperativeCleanupAnalysisClient analysis = new();
            InMemoryLayoutStore layoutStore = new();
            MainForm form = new(CreateServices(
                analysisClient: analysis,
                layoutStore: layoutStore,
                operationShutdownTimeout: TimeSpan.FromSeconds(2)));
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());
            form.SetDocument(CreateDocument("Cooperative", isDirty: false));
            Task operation = form.ExecuteAnalysisAsync();
            PumpUntil(() => analysis.Started.Task.IsCompleted);

            form.Close();
            PumpUntil(() => analysis.CancellationObserved.Task.IsCompleted);

            Assert.False(form.IsDisposed);
            Assert.True(form.Visible);
            Assert.False(operation.IsCompleted);
            Assert.Equal(0, layoutStore.SaveCalls);

            analysis.AllowCleanupToFinish();
            PumpUntil(() => form.IsDisposed);
            WaitFor(operation);

            Assert.True(analysis.CleanupCompleted);
            Assert.Equal(1, layoutStore.SaveCalls);
        }, "Form close awaits cooperative cleanup");
    }

    [Fact]
    public void FormClosing_NonCooperativeOperationUsesBoundedTimeoutAndRejectsLateCommit()
    {
        StaTestRunner.Run(() =>
        {
            NonCooperativeAnalysisClient analysis = new();
            InMemoryLayoutStore layoutStore = new();
            List<Exception> diagnostics = [];
            MainForm form = new(CreateServices(
                analysisClient: analysis,
                layoutStore: layoutStore,
                reportDiagnostic: diagnostics.Add,
                operationShutdownTimeout: TimeSpan.FromMilliseconds(80)));
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());
            ProjectDocument document = CreateDocument("Non-cooperative", isDirty: false);
            form.SetDocument(document);
            Task operation = form.ExecuteAnalysisAsync();
            PumpUntil(() => analysis.Started.Task.IsCompleted);
            Stopwatch elapsed = Stopwatch.StartNew();

            form.Close();
            PumpUntil(() => form.IsDisposed, TimeSpan.FromSeconds(3));
            elapsed.Stop();

            Assert.InRange(elapsed.Elapsed, TimeSpan.FromMilliseconds(40), TimeSpan.FromSeconds(3));
            Assert.Contains(diagnostics, static failure => failure is TimeoutException);
            Assert.Equal(1, layoutStore.SaveCalls);
            analysis.CompleteAfterClose();
            WaitFor(operation);
            Assert.True(form.IsDisposed);
        }, "Form close bounded non-cooperative timeout", TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void MainForm_InjectedLayoutStoreRoundTripsFinalValidatedLayoutAcrossRestart()
    {
        StaTestRunner.Run(() =>
        {
            InMemoryLayoutStore layoutStore = new();
            string expected;
            MainForm first = new(CreateServices(layoutStore: layoutStore));
            first.Show();
            WaitFor(first.WhenLayoutRestoredAsync());
            first.ContentRegistry.Open(MainForm.NavigationContentKey, CoreDockState.DockRight);
            first.ContentRegistry.Open(
                MainForm.EditorContentKey,
                CoreDockState.Float,
                new WindowBounds(140, 160, 420, 280));
            first.ContentRegistry.Open(MainForm.DiagnosticsContentKey, CoreDockState.Hidden);
            first.ContentRegistry.Activate(MainForm.WorkspaceDocumentKey);
            Application.DoEvents();
            expected = first.LayoutAdapter.CaptureJson();

            first.Close();
            PumpUntil(() => first.IsDisposed);

            Assert.Equal(1, layoutStore.LoadCalls);
            Assert.Equal(1, layoutStore.SaveCalls);
            Assert.Equal(expected, layoutStore.Json);

            MainForm second = new(CreateServices(layoutStore: layoutStore));
            second.Show();
            WaitFor(second.WhenLayoutRestoredAsync());

            Assert.Equal(2, layoutStore.LoadCalls);
            Assert.Equal(expected, second.LayoutAdapter.CaptureJson());
            Assert.Equal(CoreDockState.DockRight, GetState(second, MainForm.NavigationContentKey).DockState);
            LayoutContentState editor = GetState(second, MainForm.EditorContentKey);
            Assert.Equal(CoreDockState.Float, editor.DockState);
            Assert.NotNull(editor.Bounds);
            Assert.Equal(new WindowBounds(140, 160, 420, 280), editor.Bounds.Value);
            Assert.Equal(CoreDockState.Hidden, GetState(second, MainForm.DiagnosticsContentKey).DockState);
            Assert.Equal(MainForm.WorkspaceDocumentKey, second.LayoutAdapter.Capture().ActiveDocument);

            second.Close();
            PumpUntil(() => second.IsDisposed);
            Assert.Equal(2, layoutStore.SaveCalls);
        }, "MainForm layout-store restart");
    }

    [Fact]
    public void MainForm_AbsentInvalidAndOversizedLayoutsFallBackToDefaultPanesWithSafeDiagnostics()
    {
        StaTestRunner.Run(() =>
        {
            AssertFallback(new InMemoryLayoutStore(), expectedDiagnostics: 0);
            AssertFallback(new InMemoryLayoutStore("{not valid json"), expectedDiagnostics: 1);
            AssertFallback(
                new InMemoryLayoutStore(new string('x', DockLayoutAdapter.MaximumJsonCharacters + 1)),
                expectedDiagnostics: 1);
        }, "MainForm safe layout fallback");
    }

    [Fact]
    public void MainForm_ClosingLastDocumentClearsPublicActiveDocumentKey()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = new(CreateServices());
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());
            Assert.Equal(MainForm.WorkspaceDocumentKey, form.ActiveDocumentKey);

            form.DocumentHost.DockHandler.Close();
            Application.DoEvents();
            WaitFor(form.WhenActivationIdleAsync());

            Assert.Null(form.ActiveDocumentKey);
        }, "MainForm null active document");
    }

    private static MainFormServices CreateServices(
        IProjectStore? projectStore = null,
        IAnalysisClient? analysisClient = null,
        IShellDialogService? dialogs = null,
        IShellLayoutStore? layoutStore = null,
        Action<Exception>? reportDiagnostic = null,
        TimeSpan? operationShutdownTimeout = null) => new(
            projectStore: projectStore ?? new ImmediateProjectStore(),
            analysisClient: analysisClient,
            printExporter: new FakePrintExporter(),
            localization: new LocalizationService(UiLanguage.English),
            dialogs: dialogs ?? new SequencedShellDialogs(),
            reportDiagnostic: reportDiagnostic,
            layoutStore: layoutStore ?? new InMemoryLayoutStore(),
            operationShutdownTimeout: operationShutdownTimeout);

    private static void AssertFallback(IShellLayoutStore layoutStore, int expectedDiagnostics)
    {
        List<Exception> diagnostics = [];
        MainForm form = new(CreateServices(layoutStore: layoutStore, reportDiagnostic: diagnostics.Add));
        form.Show();
        WaitFor(form.WhenLayoutRestoredAsync());

        Assert.Equal(expectedDiagnostics, diagnostics.Count);
        Assert.Equal(4, form.ContentRegistry.Contents.Count);
        Assert.Equal(CoreDockState.DockLeft, GetState(form, MainForm.NavigationContentKey).DockState);
        Assert.Equal(CoreDockState.DockRight, GetState(form, MainForm.EditorContentKey).DockState);
        Assert.Equal(CoreDockState.DockBottom, GetState(form, MainForm.DiagnosticsContentKey).DockState);
        Assert.Equal(CoreDockState.Document, GetState(form, MainForm.WorkspaceDocumentKey).DockState);

        form.Close();
        PumpUntil(() => form.IsDisposed);
    }

    private static LayoutContentState GetState(MainForm form, DocumentKey key) =>
        Assert.Single(form.LayoutAdapter.Capture().Contents, state => state.Key == key);

    private static ToolStripMenuItem GetMenuItem(MainForm form, string name) =>
        Assert.Single(EnumerateMenuItems(form.MainMenuStrip!.Items), item => item.Name == name);

    private static IEnumerable<ToolStripMenuItem> EnumerateMenuItems(ToolStripItemCollection items)
    {
        foreach (ToolStripMenuItem item in items.OfType<ToolStripMenuItem>())
        {
            yield return item;
            foreach (ToolStripMenuItem child in EnumerateMenuItems(item.DropDownItems))
            {
                yield return child;
            }
        }
    }

    private static ProjectDocument CreateDocument(string name, bool isDirty) => new(
        ProjectDocument.CurrentVersion,
        new ProjectMetadata(name, string.Empty, string.Empty, "test_units"),
        nodes: [],
        members: [],
        supports: [],
        loadCases: [],
        nodalLoads: [],
        derivedResults: [],
        movingLoads: [],
        isDirty: isDirty);

    private static void WaitFor(Task task)
    {
        PumpUntil(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> completed, TimeSpan? timeout = null)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(5);
        while (!completed())
        {
            Assert.True(elapsed.Elapsed < limit, $"The UI operation did not complete within {limit}.");
            Application.DoEvents();
            Thread.Sleep(1);
        }

        Application.DoEvents();
    }

    private sealed class ImmediateProjectStore : IProjectStore
    {
        public Task<ProjectDocument> OpenAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDocument("Opened", isDirty: false));

        public Task<ProjectDocument> SaveAsync(
            ProjectDocument document,
            string path,
            CancellationToken cancellationToken = default) => Task.FromResult(document.MarkSaved());
    }

    private sealed class DelayedSaveProjectStore : IProjectStore
    {
        private readonly TaskCompletionSource<ProjectDocument> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ProjectDocument? pendingDocument;

        internal TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int SaveCalls { get; private set; }

        public Task<ProjectDocument> OpenAsync(string path, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectDocument> SaveAsync(
            ProjectDocument document,
            string path,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            pendingDocument = document;
            SaveStarted.TrySetResult();
            return completion.Task;
        }

        internal void CompleteSave() => completion.TrySetResult(pendingDocument!.MarkSaved());
    }

    private sealed class SequencedShellDialogs(params DirtyDocumentCloseDecision[] decisions)
        : IShellDialogService
    {
        private readonly Queue<DirtyDocumentCloseDecision> decisions = new(decisions);

        internal string? ProjectToSave { get; init; }

        internal int ConfirmCloseCalls => ConfirmedDocumentNames.Count;

        internal List<string> ConfirmedDocumentNames { get; } = [];

        public string? SelectProjectToOpen(IWin32Window owner, string title, string filter) => null;

        public string? SelectProjectToSave(
            IWin32Window owner,
            string title,
            string filter,
            string? currentPath) => ProjectToSave;

        public string? SelectPdfToExport(IWin32Window owner, string title, string filter) => null;

        public DirtyDocumentCloseDecision ConfirmDirtyDocument(
            IWin32Window owner,
            string title,
            string message)
        {
            ConfirmedDocumentNames.Add(ExtractDocumentName(message));
            return decisions.Count == 0 ? DirtyDocumentCloseDecision.Cancel : decisions.Dequeue();
        }

        public void ShowError(IWin32Window owner, string title, string message)
        {
        }

        private static string ExtractDocumentName(string message)
        {
            foreach (string candidate in new[] { "Document A", "Document B" })
            {
                if (message.Contains(candidate, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return message;
        }
    }

    private sealed class InMemoryLayoutStore(string? json = null) : IShellLayoutStore
    {
        internal string? Json { get; private set; } = json;

        internal int LoadCalls { get; private set; }

        internal int SaveCalls { get; private set; }

        public Task<string?> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Json);
        }

        public Task SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            Json = json;
            return Task.CompletedTask;
        }
    }

    private sealed class CooperativeCleanupAnalysisClient : IAnalysisClient
    {
        private readonly TaskCompletionSource allowCleanup =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool CleanupCompleted { get; private set; }

        public async Task<AnalysisResultSet> AnalyzeAsync(
            ProjectDocument document,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The cooperative operation unexpectedly completed.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult();
                await allowCleanup.Task;
                CleanupCompleted = true;
                throw;
            }
        }

        internal void AllowCleanupToFinish() => allowCleanup.TrySetResult();
    }

    private sealed class NonCooperativeAnalysisClient : IAnalysisClient
    {
        private readonly TaskCompletionSource<AnalysisResultSet> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AnalysisResultSet> AnalyzeAsync(
            ProjectDocument document,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return completion.Task;
        }

        internal void CompleteAfterClose() =>
            completion.TrySetCanceled(new CancellationToken(canceled: true));
    }
}
