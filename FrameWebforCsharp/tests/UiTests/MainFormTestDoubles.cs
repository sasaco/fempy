using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Lifecycle;

namespace PDF_Manager.UiTests;

internal sealed class FakeProjectStore : IProjectStore
{
    public int OpenCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public ProjectDocument? OpenResult { get; set; }

    public Exception? OpenFailure { get; set; }

    public Task<ProjectDocument> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        OpenCalls++;
        cancellationToken.ThrowIfCancellationRequested();
        return OpenFailure is null
            ? Task.FromResult(OpenResult ?? ShellCommandStateTests.CreateDocument(isDirty: false))
            : Task.FromException<ProjectDocument>(OpenFailure);
    }

    public Task<ProjectDocument> SaveAsync(
        ProjectDocument document,
        string path,
        CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(document.MarkSaved());
    }
}

internal sealed class FakeShellDialogs : IShellDialogService
{
    public string? ProjectToOpen { get; set; }

    public string? ProjectToSave { get; set; }

    public string? PdfToExport { get; set; }

    public DirtyDocumentCloseDecision CloseDecision { get; set; } = DirtyDocumentCloseDecision.Cancel;

    public int ConfirmCloseCalls { get; private set; }

    public List<(string Title, string Message)> Errors { get; } = [];

    public string? SelectProjectToOpen(IWin32Window owner, string title, string filter) => ProjectToOpen;

    public string? SelectProjectToSave(
        IWin32Window owner,
        string title,
        string filter,
        string? currentPath) => ProjectToSave;

    public string? SelectPdfToExport(IWin32Window owner, string title, string filter) => PdfToExport;

    public DirtyDocumentCloseDecision ConfirmDirtyDocument(
        IWin32Window owner,
        string title,
        string message)
    {
        ConfirmCloseCalls++;
        return CloseDecision;
    }

    public void ShowError(IWin32Window owner, string title, string message) => Errors.Add((title, message));
}

internal sealed class ImmediateAnalysisClient(Exception? failure = null) : IAnalysisClient
{
    public int Calls { get; private set; }

    public Task<AnalysisResultSet> AnalyzeAsync(
        ProjectDocument document,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        cancellationToken.ThrowIfCancellationRequested();
        return failure is null
            ? throw new InvalidOperationException("This test client requires a configured result or failure.")
            : Task.FromException<AnalysisResultSet>(failure);
    }
}

internal sealed class CancellableAnalysisClient : IAnalysisClient
{
    private readonly TaskCompletionSource<AnalysisResultSet> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationToken ObservedToken { get; private set; }

    public Task<AnalysisResultSet> AnalyzeAsync(
        ProjectDocument document,
        CancellationToken cancellationToken = default)
    {
        ObservedToken = cancellationToken;
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return completion.Task;
    }
}

internal sealed class FakePrintExporter : IPrintExporter
{
    public Task ExportAsync(
        PrintExportRequest request,
        Stream destination,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
