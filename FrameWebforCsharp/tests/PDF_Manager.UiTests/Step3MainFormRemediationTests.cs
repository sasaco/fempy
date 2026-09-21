using System.Diagnostics;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Lifecycle;

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
            MainForm form = new(CreateServices(projectStore: store, dialogs: dialogs));
            form.Show();
            ProjectDocument documentA = CreateDocument("Document A", isDirty: true);
            ProjectDocument documentB = CreateDocument("Document B", isDirty: true);
            form.SetDocument(documentA);

            Task firstNew = form.NewProjectAsync();
            PumpUntil(() => store.SaveStarted.Task.IsCompleted);
            Assert.False(HeaderItem(form, "HeaderNewItem").Enabled);
            Assert.False(HeaderItem(form, "HeaderOpenItem").Enabled);
            Assert.False(HeaderItem(form, "HeaderSaveItem").Enabled);
            Assert.False(HeaderItem(form, "HeaderSaveAsItem").Enabled);
            Assert.False(form.ScreenShell.HeaderBar.PrintButton.Enabled);

            form.SetDocument(documentB);
            Task queuedNew = form.NewProjectAsync();
            Assert.False(queuedNew.IsCompleted);
            store.CompleteSave();
            WaitFor(Task.WhenAll(firstNew, queuedNew));

            Assert.Same(documentB, form.CurrentDocument);
            Assert.Equal(["Document A", "Document B"], dialogs.ConfirmedDocumentNames);
            Assert.Equal(1, store.SaveCalls);

            form.Close();
            PumpUntil(() => dialogs.ConfirmCloseCalls == 3 && form.WhenShellTransitionIdleAsync().IsCompleted);
            Assert.True(form.Visible);
            Assert.False(form.IsDisposed);
            Assert.Same(documentB, form.CurrentDocument);

            form.SetDocument(documentB.MarkSaved());
            form.Close();
            PumpUntil(() => form.IsDisposed);
        }, "Serialized dirty-save document replacement");
    }

    [Fact]
    public void FormClosing_CancelsAndAwaitsCooperativeTerminalCleanupBeforeDisposing()
    {
        StaTestRunner.Run(() =>
        {
            CooperativeCleanupAnalysisClient analysis = new();
            MainForm form = new(CreateServices(
                analysisClient: analysis,
                operationShutdownTimeout: TimeSpan.FromSeconds(2)));
            form.Show();
            form.SetDocument(CreateDocument("Cooperative", isDirty: false));
            Task operation = form.ExecuteAnalysisAsync();
            PumpUntil(() => analysis.Started.Task.IsCompleted);

            form.Close();
            PumpUntil(() => analysis.CancellationObserved.Task.IsCompleted);

            Assert.False(form.IsDisposed);
            Assert.True(form.Visible);
            Assert.False(operation.IsCompleted);

            analysis.AllowCleanupToFinish();
            PumpUntil(() => form.IsDisposed);
            WaitFor(operation);

            Assert.True(analysis.CleanupCompleted);
        }, "Form close awaits cooperative cleanup");
    }

    [Fact]
    public void FormClosing_NonCooperativeOperationUsesBoundedTimeoutAndRejectsLateCommit()
    {
        StaTestRunner.Run(() =>
        {
            NonCooperativeAnalysisClient analysis = new();
            List<Exception> diagnostics = [];
            MainForm form = new(CreateServices(
                analysisClient: analysis,
                reportDiagnostic: diagnostics.Add,
                operationShutdownTimeout: TimeSpan.FromMilliseconds(80)));
            form.Show();
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
            analysis.CompleteAfterClose();
            WaitFor(operation);
            Assert.True(form.IsDisposed);
        }, "Form close bounded non-cooperative timeout", TimeSpan.FromSeconds(10));
    }

    private static MainFormServices CreateServices(
        IProjectStore? projectStore = null,
        IAnalysisClient? analysisClient = null,
        IShellDialogService? dialogs = null,
        Action<Exception>? reportDiagnostic = null,
        TimeSpan? operationShutdownTimeout = null) => new(
            projectStore: projectStore ?? new ImmediateProjectStore(),
            analysisClient: analysisClient,
            printExporter: new FakePrintExporter(),
            localization: new LocalizationService(UiLanguage.English),
            dialogs: dialogs ?? new SequencedShellDialogs(),
            reportDiagnostic: reportDiagnostic,
            operationShutdownTimeout: operationShutdownTimeout);

    private static ToolStripItem HeaderItem(MainForm form, string name) =>
        Assert.Single(form.ScreenShell.HeaderBar.FileMenu.DropDownItems.Cast<ToolStripItem>(), item => item.Name == name);

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

        public DirtyDocumentCloseDecision ConfirmDirtyDocument(IWin32Window owner, string title, string message)
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
                if (message.Contains(candidate, StringComparison.Ordinal)) return candidate;
            }

            return message;
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
