using System.Diagnostics;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Resources;
using PDF_Manager.Shell;

namespace PDF_Manager.UiTests;

public sealed class MainFormCancellationClassificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncancelledOperationCancellationFromAnalysis_IsUnexpectedAndDiagnosed(
        bool useTaskCanceledException)
    {
        StaTestRunner.Run(() =>
        {
            Exception failure = useTaskCanceledException
                ? new TaskCanceledException("analysis transport timeout")
                : new OperationCanceledException("analysis aborted without shell cancellation");
            List<Exception> diagnostics = [];
            FakeShellDialogs dialogs = new();
            MainForm form = CreateForm(
                analysisClient: new ImmediateAnalysisClient(failure),
                dialogs: dialogs,
                reportDiagnostic: diagnostics.Add);
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());
            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: false));

            WaitFor(form.ExecuteAnalysisAsync());

            Assert.Same(failure, Assert.Single(diagnostics));
            Assert.Single(dialogs.Errors);
            Assert.Equal("An unexpected error occurred.", form.DiagnosticsPane.StatusLabel.Text);
            Assert.False(form.IsOperationRunning);

            CloseAndWait(form);
        }, $"Uncancelled analysis cancellation {useTaskCanceledException}");
    }

    [Fact]
    public void UncancelledTaskCancellationFromProjectStore_IsUnexpectedAndDiagnosed()
    {
        StaTestRunner.Run(() =>
        {
            TaskCanceledException failure = new("project store timeout");
            List<Exception> diagnostics = [];
            FakeShellDialogs dialogs = new()
            {
                ProjectToOpen = "timed-out.frameweb.json",
            };
            MainForm form = CreateForm(
                projectStore: new FailingOpenProjectStore(failure),
                dialogs: dialogs,
                reportDiagnostic: diagnostics.Add);
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());

            WaitFor(form.OpenProjectAsync());

            Assert.Same(failure, Assert.Single(diagnostics));
            Assert.Single(dialogs.Errors);
            Assert.Equal("An unexpected error occurred.", form.DiagnosticsPane.StatusLabel.Text);
            Assert.Null(form.CurrentDocument);

            CloseAndWait(form);
        }, "Uncancelled project-store cancellation");
    }

    [Fact]
    public void OwnedOperationCancellation_IsExpectedAndNotDiagnosed()
    {
        StaTestRunner.Run(() =>
        {
            CancellableAnalysisClient analysis = new();
            List<Exception> diagnostics = [];
            FakeShellDialogs dialogs = new();
            MainForm form = CreateForm(
                analysisClient: analysis,
                dialogs: dialogs,
                reportDiagnostic: diagnostics.Add);
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());
            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: false));
            Task operation = form.ExecuteAnalysisAsync();
            PumpUntil(() => analysis.ObservedToken.CanBeCanceled);

            form.CancelOperation();
            WaitFor(operation);

            Assert.True(analysis.ObservedToken.IsCancellationRequested);
            Assert.Empty(diagnostics);
            Assert.Empty(dialogs.Errors);
            Assert.Equal("The operation was canceled.", form.DiagnosticsPane.StatusLabel.Text);
            Assert.False(form.IsOperationRunning);

            CloseAndWait(form);
        }, "Owned analysis cancellation");
    }

    [Fact]
    public void DirtySaveTimeout_CancelsOwnedTokenAbortsCloseAndRejectsLatePublish()
    {
        StaTestRunner.Run(() =>
        {
            NonCooperativeSaveProjectStore projectStore = new();
            ImmediateLayoutStore layoutStore = new();
            FakeShellDialogs dialogs = new()
            {
                CloseDecision = Shell.Lifecycle.DirtyDocumentCloseDecision.Save,
                ProjectToSave = "dirty-timeout.frameweb.json",
            };
            List<Exception> diagnostics = [];
            MainForm form = CreateForm(
                projectStore: projectStore,
                dialogs: dialogs,
                layoutStore: layoutStore,
                reportDiagnostic: diagnostics.Add,
                operationShutdownTimeout: TimeSpan.FromMilliseconds(80));
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());
            ProjectDocument original = ShellCommandStateTests.CreateDocument(isDirty: true);
            form.SetDocument(original);
            Stopwatch elapsed = Stopwatch.StartNew();

            form.Close();
            PumpUntil(
                () => projectStore.SaveStarted.Task.IsCompleted &&
                    form.WhenShellTransitionIdleAsync().IsCompleted,
                TimeSpan.FromSeconds(3));
            elapsed.Stop();

            Assert.InRange(elapsed.Elapsed, TimeSpan.FromMilliseconds(40), TimeSpan.FromSeconds(3));
            Assert.False(form.IsDisposed);
            Assert.True(form.Visible);
            Assert.Same(original, form.CurrentDocument);
            Assert.True(form.CurrentDocument!.IsDirty);
            Assert.True(projectStore.ObservedToken.IsCancellationRequested);
            Assert.Equal(1, projectStore.SaveCalls);
            Assert.Contains(diagnostics, static failure => failure is TimeoutException);
            Assert.Equal(0, layoutStore.SaveCalls);

            projectStore.CompleteSave();
            WaitFor(form.WhenCurrentOperationIdleAsync());
            Assert.Same(original, form.CurrentDocument);
            Assert.True(form.CurrentDocument!.IsDirty);

            form.SetDocument(original.MarkSaved());
            CloseAndWait(form);
        }, "Dirty close-save timeout", TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void LayoutSaveTimeout_CancelsOwnedTokenDiagnosesAndPermitsClose()
    {
        StaTestRunner.Run(() =>
        {
            NonCooperativeLayoutStore layoutStore = new();
            List<Exception> diagnostics = [];
            MainForm form = CreateForm(
                layoutStore: layoutStore,
                reportDiagnostic: diagnostics.Add,
                operationShutdownTimeout: TimeSpan.FromMilliseconds(80));
            form.Show();
            WaitFor(form.WhenLayoutRestoredAsync());
            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: false));
            Stopwatch elapsed = Stopwatch.StartNew();

            form.Close();
            PumpUntil(() => form.IsDisposed, TimeSpan.FromSeconds(3));
            elapsed.Stop();

            Assert.InRange(elapsed.Elapsed, TimeSpan.FromMilliseconds(40), TimeSpan.FromSeconds(3));
            Assert.Equal(1, layoutStore.SaveCalls);
            Assert.True(layoutStore.ObservedToken.IsCancellationRequested);
            Assert.Contains(diagnostics, static failure => failure is TimeoutException);

            layoutStore.CompleteSave();
            Application.DoEvents();
        }, "Layout close-save timeout", TimeSpan.FromSeconds(10));
    }

    private static MainForm CreateForm(
        IProjectStore? projectStore = null,
        IAnalysisClient? analysisClient = null,
        IShellDialogService? dialogs = null,
        IShellLayoutStore? layoutStore = null,
        Action<Exception>? reportDiagnostic = null,
        TimeSpan? operationShutdownTimeout = null) => new(new MainFormServices(
            projectStore: projectStore ?? new FakeProjectStore(),
            analysisClient: analysisClient,
            printExporter: new FakePrintExporter(),
            localization: new LocalizationService(UiLanguage.English),
            dialogs: dialogs ?? new FakeShellDialogs(),
            reportDiagnostic: reportDiagnostic,
            layoutStore: layoutStore ?? new ImmediateLayoutStore(),
            operationShutdownTimeout: operationShutdownTimeout));

    private static void CloseAndWait(MainForm form)
    {
        form.Close();
        PumpUntil(() => form.IsDisposed);
    }

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

    private sealed class FailingOpenProjectStore(Exception failure) : IProjectStore
    {
        public Task<ProjectDocument> OpenAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ProjectDocument>(failure);

        public Task<ProjectDocument> SaveAsync(
            ProjectDocument document,
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NonCooperativeSaveProjectStore : IProjectStore
    {
        private readonly TaskCompletionSource<ProjectDocument> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ProjectDocument? pendingDocument;

        internal TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CancellationToken ObservedToken { get; private set; }

        internal int SaveCalls { get; private set; }

        public Task<ProjectDocument> OpenAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProjectDocument> SaveAsync(
            ProjectDocument document,
            string path,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            ObservedToken = cancellationToken;
            pendingDocument = document;
            SaveStarted.TrySetResult();
            return completion.Task;
        }

        internal void CompleteSave() => completion.TrySetResult(pendingDocument!.MarkSaved());
    }

    private sealed class ImmediateLayoutStore : IShellLayoutStore
    {
        internal int SaveCalls { get; private set; }

        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class NonCooperativeLayoutStore : IShellLayoutStore
    {
        private readonly TaskCompletionSource completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CancellationToken ObservedToken { get; private set; }

        internal int SaveCalls { get; private set; }

        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            ObservedToken = cancellationToken;
            return completion.Task;
        }

        internal void CompleteSave() => completion.TrySetResult();
    }
}
