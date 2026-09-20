using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Documents;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Lifecycle;
using PDF_Manager.Shell.Printing;

namespace PDF_Manager.Shell;

public interface IShellDialogService
{
    string? SelectProjectToOpen(IWin32Window owner, string title, string filter);

    string? SelectProjectToSave(IWin32Window owner, string title, string filter, string? currentPath);

    string? SelectPdfToExport(IWin32Window owner, string title, string filter);

    DirtyDocumentCloseDecision ConfirmDirtyDocument(IWin32Window owner, string title, string message);

    void ShowError(IWin32Window owner, string title, string message);
}

public sealed class WinFormsShellDialogService : IShellDialogService
{
    public string? SelectProjectToOpen(IWin32Window owner, string title, string filter)
    {
        using OpenFileDialog dialog = new()
        {
            AddExtension = true,
            CheckFileExists = true,
            Filter = filter,
            Multiselect = false,
            RestoreDirectory = true,
            Title = title,
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
    }

    public string? SelectProjectToSave(
        IWin32Window owner,
        string title,
        string filter,
        string? currentPath)
    {
        using SaveFileDialog dialog = new()
        {
            AddExtension = true,
            Filter = filter,
            FileName = currentPath is null ? string.Empty : Path.GetFileName(currentPath),
            OverwritePrompt = true,
            RestoreDirectory = true,
            Title = title,
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
    }

    public string? SelectPdfToExport(IWin32Window owner, string title, string filter)
    {
        using SaveFileDialog dialog = new()
        {
            AddExtension = true,
            DefaultExt = "pdf",
            Filter = filter,
            OverwritePrompt = true,
            RestoreDirectory = true,
            Title = title,
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
    }

    public DirtyDocumentCloseDecision ConfirmDirtyDocument(
        IWin32Window owner,
        string title,
        string message)
    {
        DialogResult result = MessageBox.Show(
            owner,
            message,
            title,
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1);
        return result switch
        {
            DialogResult.Yes => DirtyDocumentCloseDecision.Save,
            DialogResult.No => DirtyDocumentCloseDecision.Discard,
            _ => DirtyDocumentCloseDecision.Cancel,
        };
    }

    public void ShowError(IWin32Window owner, string title, string message) =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
}

public sealed class MainFormServices : IDisposable
{
    private readonly IDisposable? _ownedResource;
    private bool _disposed;

    /// <summary>
    /// Bounds cooperative shutdown before the shell closes with late UI commits invalidated.
    /// </summary>
    public static TimeSpan DefaultOperationShutdownTimeout { get; } = TimeSpan.FromSeconds(2);

    public MainFormServices(
        IProjectStore? projectStore = null,
        IAnalysisClient? analysisClient = null,
        IPrintExporter? printExporter = null,
        LocalizationService? localization = null,
        IShellDialogService? dialogs = null,
        OperationCancellationOwner? cancellationOwner = null,
        Action<Exception>? reportDiagnostic = null,
        IShellLayoutStore? layoutStore = null,
        IViewportCaptureProvider? viewportCaptureProvider = null,
        IDisposable? ownedResource = null,
        TimeSpan? operationShutdownTimeout = null)
    {
        TimeSpan shutdownTimeout = operationShutdownTimeout ?? DefaultOperationShutdownTimeout;
        if (shutdownTimeout <= TimeSpan.Zero || shutdownTimeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationShutdownTimeout),
                shutdownTimeout,
                "The operation shutdown timeout must be greater than zero and at most 30 seconds.");
        }

        ProjectStore = projectStore ?? new JsonProjectStore();
        AnalysisClient = analysisClient;
        PrintExporter = printExporter ?? new DesktopPdfExporter();
        Localization = localization ?? new LocalizationService();
        Dialogs = dialogs ?? new WinFormsShellDialogService();
        CancellationOwner = cancellationOwner ?? new OperationCancellationOwner();
        ReportDiagnostic = reportDiagnostic;
        LayoutStore = layoutStore ?? new LocalShellLayoutStore();
        ViewportCaptureProvider = viewportCaptureProvider ?? new LiveViewportCaptureProvider();
        _ownedResource = ownedResource;
        OperationShutdownTimeout = shutdownTimeout;
    }

    public IProjectStore ProjectStore { get; }

    public IAnalysisClient? AnalysisClient { get; }

    public IPrintExporter? PrintExporter { get; }

    public LocalizationService Localization { get; }

    public IShellDialogService Dialogs { get; }

    public OperationCancellationOwner CancellationOwner { get; }

    public Action<Exception>? ReportDiagnostic { get; }

    public IShellLayoutStore LayoutStore { get; }

    public IViewportCaptureProvider ViewportCaptureProvider { get; }

    /// <summary>
    /// Gets the close-time budget used both for current-operation cleanup and transition ownership.
    /// </summary>
    public TimeSpan OperationShutdownTimeout { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ownedResource?.Dispose();
    }
}
