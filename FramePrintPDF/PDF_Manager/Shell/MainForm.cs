using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Printing;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Lifecycle;
using PDF_Manager.Shell.Printing;
using PDF_Manager.Shell.ScreenComposition.Core;
using PDF_Manager.Shell.ScreenComposition.Surfaces;
using PDF_Manager.Shell.Viewport;
using CorePrintPageSettings = PDF_Manager.Core.Abstractions.PrintPageSettings;

namespace PDF_Manager.Shell;

public sealed class MainForm : Form
{
    private static readonly PrintContentSection[] DefaultPrintSections =
    [
        PrintContentSection.ProjectSummary,
        PrintContentSection.InputTables,
        PrintContentSection.ModelDiagram,
        PrintContentSection.LoadDiagram,
    ];

    private static DocumentKey EditorContentKey { get; } = DocumentKey.Tool("property-editor");
    public static DocumentKey WorkspaceDocumentKey { get; } = DocumentKey.Document("project.workspace");

    private readonly MainFormServices _services;
    private readonly int _uiThreadId;
    private readonly UserExceptionBoundary _exceptionBoundary;
    private readonly IFrameWebSurfaceFactory _surfaceFactory;
    private readonly EditorContent _editor;
    private readonly WorkspaceControl _workspace;
    private readonly FrameWebShellControl _screenShell;
    private readonly SemaphoreSlim _shellTransitionGate = new(1, 1);

    private AnalysisResultState _analysisState = new();
    private ProjectDocument? _currentDocument;
    private string? _documentPath;
    private Task _currentOperationTask = Task.CompletedTask;
    private Task _currentShellTransitionTask = Task.CompletedTask;
    private string _statusMessage = string.Empty;
    private long _documentRevision;
    private long _operationRevision;
    private PrintPageSetupSelection _printSelection = new(
        CorePrintPageSettings.Default,
        DefaultPrintSections);
    private PrintPreviewState? _printPreviewState;
    private int _pendingShellTransitions;
    private bool _isOperationRunning;
    private bool _closeCheckRunning;
    private bool _isClosing;
    private bool _allowClose;
    private bool _disposed;

    public MainForm()
        : this(new MainFormServices())
    {
    }

    public MainForm(MainFormServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _uiThreadId = Environment.CurrentManagedThreadId;
        ClientSize = new Size(1200, 800);
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterScreen;

        _exceptionBoundary = new UserExceptionBoundary(
            NotifyUser,
            _services.ReportDiagnostic,
            MapException);

        LocalizationService localization = _services.Localization;
        _editor = new EditorContent(EditorContentKey, localization);
        DocumentHost = new ProjectDocumentContent(WorkspaceDocumentKey, localization);
        _surfaceFactory = _services.ScreenSurfaceFactory?.Invoke(localization, _editor, DocumentHost)
            ?? new FrameWebSurfaceFactory(localization, _editor, DocumentHost);
        _workspace = new WorkspaceControl(DocumentHost);
        _screenShell = new FrameWebShellControl(localization, _workspace, _surfaceFactory);
        Controls.Add(_screenShell);
        SubscribeEvents();
        ApplyLocalization();
        SetStatusResource("StatusReady");
        UpdateCommandState();
    }

    public FrameWebShellControl ScreenShell => _screenShell;

    public WorkspaceControl Workspace => _workspace;

    public ScreenRouteController RouteController => _screenShell.RouteController;

    public ProjectDocumentContent DocumentHost { get; private set; }

    public PrintPageSetupSelection PrintSelection => _printSelection;

    public PrintPreviewState? CurrentPrintPreview => _printPreviewState;

    public ProjectDocument? CurrentDocument => _currentDocument;

    public AnalysisResultSet? CurrentResult => _analysisState.Current;

    public CultureInfo CurrentCulture => _services.Localization.Culture;

    public string CurrentStatusMessage => _statusMessage;

    public bool IsOperationRunning => _isOperationRunning;

    public Task WhenCurrentOperationIdleAsync() => _currentOperationTask;

    public Task WhenShellTransitionIdleAsync() => _currentShellTransitionTask;

    public void SetLanguage(UiLanguage language) => _services.Localization.SetLanguage(language);

    public void SetDocument(ProjectDocument? document, string? path = null)
    {
        if (!_disposed)
        {
            SetCurrentDocument(document, path, clearResults: true);
        }
    }

    public Task NewProjectAsync() => RunShellTransitionAsync(async () =>
    {
        if (_isClosing || _disposed)
        {
            return;
        }

        DocumentSnapshot snapshot = CaptureDocument();
        DocumentSnapshot? authorized = await AuthorizeDocumentReplacementAsync(
            snapshot,
            allowWhileClosing: false);
        await SwitchToUiThread();
        if (authorized is DocumentSnapshot current && CanPublish(current, allowWhileClosing: false))
        {
            SetCurrentDocument(CreateNewDocument(), null, clearResults: true);
        }
    });

    public Task OpenPresetAsync(BuiltInProjectPreset preset) => RunShellTransitionAsync(async () =>
    {
        if (_isClosing || _disposed)
        {
            return;
        }

        DocumentSnapshot snapshot = CaptureDocument();
        DocumentSnapshot? authorized = await AuthorizeDocumentReplacementAsync(
            snapshot,
            allowWhileClosing: false);
        await SwitchToUiThread();
        if (authorized is DocumentSnapshot current && CanPublish(current, allowWhileClosing: false))
        {
            SetCurrentDocument(ProjectDocumentPresets.Create(preset).MarkDirty(), null, clearResults: true);
        }
    });

    public Task OpenProjectAsync() => RunShellTransitionAsync(async () =>
    {
        if (_isClosing || _disposed)
        {
            return;
        }

        string? path = TrySelectPath(() => _services.Dialogs.SelectProjectToOpen(
            this,
            _services.Localization["OpenProjectTitle"],
            _services.Localization["ProjectFileFilter"]));
        if (path is null)
        {
            return;
        }

        DocumentSnapshot? authorized = await AuthorizeDocumentReplacementAsync(
            CaptureDocument(),
            allowWhileClosing: false);
        await SwitchToUiThread();
        if (authorized is not DocumentSnapshot current || !CanPublish(current, allowWhileClosing: false))
        {
            return;
        }

        await RunOperationAsync(async cancellationToken =>
        {
            ProjectDocument opened = await _services.ProjectStore.OpenAsync(path, cancellationToken);
            await SwitchToUiThread();
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanPublish(current, allowWhileClosing: false))
            {
                return;
            }

            SetCurrentDocument(opened, path, clearResults: true);
            ReportStatus(Format("StatusProjectOpened", path));
        });
    });

    public Task SaveProjectAsync(bool saveAs = false) => RunShellTransitionAsync(async () =>
    {
        if (_isClosing || _disposed)
        {
            return;
        }

        DocumentSnapshot snapshot = CaptureDocument();
        if (snapshot.Document is null)
        {
            return;
        }

        string? path = saveAs ? null : snapshot.Path;
        path ??= TrySelectPath(() => _services.Dialogs.SelectProjectToSave(
            this,
            _services.Localization["SaveProjectTitle"],
            _services.Localization["ProjectFileFilter"],
            snapshot.Path));
        if (path is null)
        {
            return;
        }

        await SaveDocumentAsync(snapshot, path, allowWhileClosing: false);
    });

    public async Task ExecuteAnalysisAsync()
    {
        if (_currentDocument is null || _services.AnalysisClient is null)
        {
            SetStatusResource("StatusNotConfigured");
            return;
        }

        DocumentSnapshot snapshot = CaptureDocument();
        ProjectDocument document = snapshot.Document!;
        await RunOperationAsync(async cancellationToken =>
        {
            AnalysisResultSet result = await _services.AnalysisClient.AnalyzeAsync(document, cancellationToken);
            await SwitchToUiThread();
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanPublish(snapshot, allowWhileClosing: false))
            {
                return;
            }

            _analysisState.Commit(result);
            DocumentHost.SetResult(result);
            if (_printSelection.Sections.SequenceEqual(DefaultPrintSections))
            {
                _printSelection = new PrintPageSetupSelection(
                    _printSelection.PageSettings,
                    Enum.GetValues<PrintContentSection>());
            }

            _printPreviewState = null;
            SetStatusResource("StatusAnalysisCompleted");
            UpdateCommandState();
        });
    }

    public async Task RefreshPrintPreviewAsync()
    {
        if (_isOperationRunning || _isClosing || _disposed)
        {
            return;
        }

        if (_currentDocument is null || _services.PrintExporter is null)
        {
            SetStatusResource("StatusNotConfigured");
            return;
        }

        DocumentSnapshot snapshot = CaptureDocument();
        PrintPreviewState? candidate = null;
        await RunOperationAsync(async cancellationToken =>
        {
            await SwitchToUiThread();
            cancellationToken.ThrowIfCancellationRequested();
            PrintExportRequest request = CreatePrintRequest(snapshot.Document!);
            PrintPreviewResult preview;
            if (_services.PrintExporter is ILiveViewportPrintExporter liveExporter)
            {
                PrintDiagramCaptureSet captures = _services.ViewportCaptureProvider.CaptureSet(
                    DocumentHost,
                    DesktopPrintJobFactory.RequiredDiagramKinds(request));
                cancellationToken.ThrowIfCancellationRequested();
                preview = await liveExporter.PreviewAsync(request, captures, cancellationToken);
            }
            else
            {
                preview = await _services.PrintExporter.PreviewAsync(request, cancellationToken);
            }

            candidate = new PrintPreviewState(preview);
            await SwitchToUiThread();
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanPublish(snapshot, allowWhileClosing: false))
            {
                return;
            }

            _printPreviewState = candidate;
            SyncPrintOverlay();
            ReportStatus(Format("StatusPrintPreviewReady", preview.PageCount));
        });
    }

    public bool SelectPrintPreviewPage(int pageIndex)
    {
        if (_printPreviewState is null || pageIndex < 0 || pageIndex >= _printPreviewState.PageCount)
        {
            return false;
        }

        _printPreviewState = _printPreviewState.SelectPage(pageIndex);
        SyncPrintOverlay();
        return true;
    }

    public async Task ExportPdfAsync()
    {
        if (_isOperationRunning || _isClosing || _disposed)
        {
            return;
        }

        if (_currentDocument is null || _services.PrintExporter is null)
        {
            SetStatusResource("StatusNotConfigured");
            return;
        }

        string? path = TrySelectPath(() => _services.Dialogs.SelectPdfToExport(
            this,
            _services.Localization["ExportPdfTitle"],
            _services.Localization["PdfFileFilter"]));
        if (path is null)
        {
            return;
        }

        DocumentSnapshot snapshot = CaptureDocument();
        string? expectedPlanIdentity = _printPreviewState?.Preview.PlanIdentity;
        await RunOperationAsync(async cancellationToken =>
        {
            await SwitchToUiThread();
            cancellationToken.ThrowIfCancellationRequested();
            PrintExportRequest request = CreatePrintRequest(snapshot.Document!);
            PrintDiagramCaptureSet? captures = null;
            if (_services.PrintExporter is ILiveViewportPrintExporter)
            {
                captures = _services.ViewportCaptureProvider.CaptureSet(
                    DocumentHost,
                    DesktopPrintJobFactory.RequiredDiagramKinds(request));
                cancellationToken.ThrowIfCancellationRequested();
            }

            await ExportPdfAtomicallyAsync(
                request,
                captures,
                expectedPlanIdentity,
                path,
                cancellationToken);
            await SwitchToUiThread();
            cancellationToken.ThrowIfCancellationRequested();
            if (CanPublish(snapshot, allowWhileClosing: false))
            {
                ReportStatus(Format("StatusPdfExported", path));
            }
        });
    }

    public void CancelOperation()
    {
        if (_services.CancellationOwner.CancelCurrent())
        {
            SetStatusResource("StatusCanceled");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            UnsubscribeEvents();
            _services.CancellationOwner.CancelCurrent();
            _services.CancellationOwner.Dispose();
            _screenShell.Dispose();
            if (_surfaceFactory is IDisposable disposableFactory)
            {
                disposableFactory.Dispose();
            }

            DocumentHost.Dispose();
            _editor.Dispose();
            _services.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SubscribeEvents()
    {
        _services.Localization.CultureChanged += OnCultureChanged;
        _screenShell.CommandRequested += OnScreenCommandRequested;
        _screenShell.LanguageRequested += OnScreenLanguageRequested;
        RouteController.StateChanged += OnScreenRouteStateChanged;
        _editor.DocumentEdited += OnDocumentEdited;
        _editor.SelectionChanged += OnEditorSelectionChanged;
        _editor.ValidationFailed += OnEditorValidationFailed;
        DocumentHost.SelectionChanged += OnViewportSelectionChanged;
        DocumentHost.ViewportOperationFailed += OnViewportOperationFailed;
        DocumentHost.ResultPageChanged += OnResultPageChanged;
        FormClosing += OnFormClosing;
    }

    private void UnsubscribeEvents()
    {
        _services.Localization.CultureChanged -= OnCultureChanged;
        _screenShell.CommandRequested -= OnScreenCommandRequested;
        _screenShell.LanguageRequested -= OnScreenLanguageRequested;
        RouteController.StateChanged -= OnScreenRouteStateChanged;
        _editor.DocumentEdited -= OnDocumentEdited;
        _editor.SelectionChanged -= OnEditorSelectionChanged;
        _editor.ValidationFailed -= OnEditorValidationFailed;
        DocumentHost.SelectionChanged -= OnViewportSelectionChanged;
        DocumentHost.ViewportOperationFailed -= OnViewportOperationFailed;
        DocumentHost.ResultPageChanged -= OnResultPageChanged;
        FormClosing -= OnFormClosing;
    }

    private void OnScreenRouteStateChanged(object? sender, ScreenRouteStateChangedEventArgs eventArgs)
    {
        if (_disposed ||
            eventArgs.Current.Route is not ScreenRouteId route ||
            !AngularScreenManifest.IsResultRoute(route))
        {
            return;
        }

        ScreenPageState actual = GetResultPageState();
        bool routeChanged = eventArgs.Previous.Route != eventArgs.Current.Route;
        if (!routeChanged && eventArgs.Current.Page != actual)
        {
            bool validPageRequest = DocumentHost.ResultPageCount > 0 &&
                eventArgs.Current.Page.Count == DocumentHost.ResultPageCount &&
                eventArgs.Current.Page.Index < DocumentHost.ResultPageCount;
            if (validPageRequest)
            {
                DocumentHost.SelectResultPage(eventArgs.Current.Page.Index);
            }
        }

        SynchronizeResultPage();
    }

    private void OnResultPageChanged(object? sender, ResultPageChangedEventArgs eventArgs)
    {
        if (!_disposed && RouteController.State.Route is ScreenRouteId route &&
            AngularScreenManifest.IsResultRoute(route))
        {
            SynchronizeResultPage();
        }
    }

    private ScreenPageState GetResultPageState() => DocumentHost.ResultPageCount > 0
        ? new ScreenPageState(DocumentHost.ResultPageIndex, DocumentHost.ResultPageCount)
        : ScreenPageState.Single;

    private void SynchronizeResultPage()
    {
        ScreenPageState page = GetResultPageState();
        if (RouteController.State.Page != page)
        {
            RouteController.SetPage(page.Index, page.Count);
        }
    }

    private void ApplyLocalization()
    {
        _editor.ApplyLocalization();
        DocumentHost.ApplyLocalization();

        UpdateWindowTitle();
    }

    private void SetCurrentDocument(ProjectDocument? document, string? path, bool clearResults)
    {
        _currentDocument = document;
        _documentPath = path;
        _documentRevision = checked(_documentRevision + 1);
        if (clearResults)
        {
            _analysisState = new AnalysisResultState();
        }

        _printPreviewState = null;
        if (clearResults)
        {
            RemoveUnavailableResultPrintSections();
        }

        _editor.SetDocument(document);
        DocumentHost.SetDocument(document, resetCamera: true);
        DocumentHost.SetResult(_analysisState.Current);
        _screenShell.RouteController.SetResultsEnabled(_analysisState.Current is not null);
        UpdateWindowTitle();
        UpdateCommandState();
    }

    private void UpdateWindowTitle()
    {
        string applicationTitle = _services.Localization["AppTitle"];
        if (_currentDocument is null)
        {
            Text = applicationTitle;
            return;
        }

        Text = string.Format(
            _services.Localization.Culture,
            _services.Localization["WindowTitleWithDocument"],
            applicationTitle,
            _currentDocument.Metadata.Name,
            _currentDocument.IsDirty ? _services.Localization["DirtyIndicator"] : string.Empty);
    }

    private void UpdateCommandState()
    {
        bool shellBusy = _isOperationRunning || _pendingShellTransitions > 0 || _isClosing;
        ShellCommandContext context = ShellCommandContext.Create(
            _currentDocument,
            _analysisState.Current is not null,
            shellBusy);
        ShellCommandState state = ShellCommandStateReducer.Reduce(context);
        bool canPrint = _currentDocument is not null && !shellBusy && _services.PrintExporter is not null;
        _screenShell.ApplyCommandState(
            state.CanCreate,
            state.CanOpen,
            state.CanSave,
            canPrint);
        _screenShell.RouteController.SetResultsEnabled(_analysisState.Current is not null);
    }

    private Task RunShellTransitionAsync(Func<Task> transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        Task task = RunShellTransitionCoreAsync(transition);
        _currentShellTransitionTask = task;
        return task;
    }

    private async Task RunShellTransitionCoreAsync(Func<Task> transition)
    {
        _pendingShellTransitions = checked(_pendingShellTransitions + 1);
        if (!_disposed)
        {
            UpdateCommandState();
        }

        await _shellTransitionGate.WaitAsync();
        await SwitchToUiThread();
        try
        {
            if (!_disposed)
            {
                await transition();
                await SwitchToUiThread();
            }
        }
        catch (Exception exception)
        {
            await SwitchToUiThread();
            ReportException(exception);
        }
        finally
        {
            _shellTransitionGate.Release();
            _pendingShellTransitions--;
            if (!_disposed)
            {
                UpdateCommandState();
            }
        }
    }

    private DocumentSnapshot CaptureDocument() =>
        new(_currentDocument, _documentPath, _documentRevision);

    private PrintExportRequest CreatePrintRequest(ProjectDocument document)
    {
        HashSet<PrintDiagramKind> supportedDiagramKinds =
            _services.ViewportCaptureProvider.SupportedDiagramKinds.ToHashSet();
        PrintContentSection[] supportedSections = _printSelection.Sections
            .Where(section => section switch
            {
                PrintContentSection.ModelDiagram => supportedDiagramKinds.Contains(PrintDiagramKind.Model),
                PrintContentSection.LoadDiagram => supportedDiagramKinds.Contains(PrintDiagramKind.Load),
                PrintContentSection.ResultDiagram => supportedDiagramKinds.Contains(PrintDiagramKind.Result),
                _ => true,
            })
            .ToArray();
        PrintResultSelection? selectedResult = DocumentHost.CreatePrintResultSelection();
        if (selectedResult is null && supportedSections.Any(IsResultPrintSection))
        {
            throw new PrintExportException(
                OperationFailureKind.Validation,
                _services.Localization["PrintResultUnavailable"]);
        }

        ResultCoordinate[] selectedCoordinates = selectedResult?.Coordinate is ResultCoordinate coordinate
            ? [coordinate]
            : [];
        PrintContentLanguage language = _services.Localization.Language switch
        {
            UiLanguage.Japanese => PrintContentLanguage.Japanese,
            UiLanguage.Chinese => PrintContentLanguage.SimplifiedChinese,
            _ => PrintContentLanguage.English,
        };
        return new PrintExportRequest(
            document,
            _analysisState.Current,
            selectedCoordinates,
            _printSelection.PageSettings,
            supportedSections,
            selectedResult,
            language);
    }

    private static bool IsResultPrintSection(PrintContentSection section) => section is
        PrintContentSection.DisplacementResults or
        PrintContentSection.ReactionResults or
        PrintContentSection.MemberForceResults or
        PrintContentSection.ResultDiagram;

    private void RemoveUnavailableResultPrintSections()
    {
        PrintContentSection[] available = _printSelection.Sections
            .Where(section => !IsResultPrintSection(section))
            .ToArray();
        _printSelection = new PrintPageSetupSelection(
            _printSelection.PageSettings,
            available.Length == 0 ? DefaultPrintSections : available);
    }

    private UiThreadSwitch SwitchToUiThread() => new(this);

    private bool CanPublish(DocumentSnapshot snapshot, bool allowWhileClosing) =>
        !_disposed &&
        (allowWhileClosing || !_isClosing) &&
        snapshot.Revision == _documentRevision &&
        ReferenceEquals(snapshot.Document, _currentDocument) &&
        StringComparer.Ordinal.Equals(snapshot.Path, _documentPath);

    private Task<bool> RunOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Task<bool> task = RunOperationCoreAsync(operation, cancellationToken);
        _currentOperationTask = task;
        return task;
    }

    private async Task<bool> RunOperationCoreAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        long revision = checked(++_operationRevision);
        ScreenOverlayKind previousOverlay = RouteController.State.Overlay;
        using OperationCancellationOwner.OperationCancellationLease lease =
            _services.CancellationOwner.Begin(cancellationToken);
        _isOperationRunning = true;
        RouteController.ShowOverlay(ScreenOverlayKind.Wait);
        SetStatusResource("StatusWorking");
        UpdateCommandState();
        try
        {
            bool succeeded;
            try
            {
                await operation(lease.Token);
                succeeded = true;
            }
            catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
            {
                succeeded = false;
            }
            catch (Exception exception)
            {
                await SwitchToUiThread();
                ReportException(exception);
                succeeded = false;
            }

            await SwitchToUiThread();
            if (!succeeded && lease.Token.IsCancellationRequested && !_disposed && !_isClosing)
            {
                SetStatusResource("StatusCanceled");
            }

            return succeeded;
        }
        finally
        {
            if (revision == _operationRevision && !_disposed)
            {
                _isOperationRunning = false;
                if (RouteController.State.Overlay == ScreenOverlayKind.Wait)
                {
                    if (previousOverlay == ScreenOverlayKind.None)
                    {
                        RouteController.CloseOverlay();
                    }
                    else
                    {
                        RouteController.ShowOverlay(previousOverlay);
                    }
                }

                SyncPrintOverlay();
                UpdateCommandState();
            }
        }
    }

    private async Task<DocumentSnapshot?> SaveDocumentAsync(
        DocumentSnapshot snapshot,
        string path,
        bool allowWhileClosing,
        CancellationToken cancellationToken = default)
    {
        if (snapshot.Document is null)
        {
            return null;
        }

        DocumentSnapshot? savedSnapshot = null;
        bool succeeded = await RunOperationAsync(async cancellationToken =>
        {
            ProjectDocument saved = await _services.ProjectStore.SaveAsync(
                snapshot.Document,
                path,
                cancellationToken);
            await SwitchToUiThread();
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanPublish(snapshot, allowWhileClosing))
            {
                return;
            }

            SetCurrentDocument(saved, path, clearResults: false);
            savedSnapshot = CaptureDocument();
            ReportStatus(Format("StatusProjectSaved", path));
        }, cancellationToken);
        return succeeded ? savedSnapshot : null;
    }

    private async Task<DocumentSnapshot?> AuthorizeDocumentReplacementAsync(
        DocumentSnapshot snapshot,
        bool allowWhileClosing,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanPublish(snapshot, allowWhileClosing))
        {
            return null;
        }

        if (snapshot.Document is null || !snapshot.Document.IsDirty)
        {
            return snapshot;
        }

        try
        {
            DirtyDocumentCloseDecision decision = ConfirmDirtyClose(snapshot.Document);
            switch (decision)
            {
                case DirtyDocumentCloseDecision.Save:
                    string? path = snapshot.Path ?? TrySelectPath(() =>
                        _services.Dialogs.SelectProjectToSave(
                            this,
                            _services.Localization["SaveProjectTitle"],
                            _services.Localization["ProjectFileFilter"],
                            snapshot.Path));
                    return path is null
                        ? null
                        : await SaveDocumentAsync(
                            snapshot,
                            path,
                            allowWhileClosing,
                            cancellationToken);
                case DirtyDocumentCloseDecision.Discard:
                    return CanPublish(snapshot, allowWhileClosing) ? snapshot : null;
                case DirtyDocumentCloseDecision.Cancel:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(decision),
                        decision,
                        "Unknown close decision.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            ReportException(exception);
            return null;
        }
    }

    private DirtyDocumentCloseDecision ConfirmDirtyClose(ProjectDocument document)
    {
        string message = string.Format(
            _services.Localization.Culture,
            _services.Localization["UnsavedChangesMessage"],
            document.Metadata.Name);
        return _services.Dialogs.ConfirmDirtyDocument(
            this,
            _services.Localization["UnsavedChangesTitle"],
            message);
    }

    private async Task ExportPdfAtomicallyAsync(
        PrintExportRequest request,
        PrintDiagramCaptureSet? diagramCaptures,
        string? expectedPlanIdentity,
        string path,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new IOException(_services.Localization["UnexpectedError"]);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                new FileStreamOptions
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.CreateNew,
                    Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                    Share = FileShare.None,
                }))
            {
                if (_services.PrintExporter is ILiveViewportPrintExporter liveViewportExporter)
                {
                    PrintExportReceipt receipt = await liveViewportExporter.ExportWithResultAsync(
                        request,
                        diagramCaptures ?? throw new InvalidOperationException(
                            "Live desktop PDF export requires semantic diagram captures."),
                        stream,
                        cancellationToken);
                    if (expectedPlanIdentity is not null &&
                        !string.Equals(expectedPlanIdentity, receipt.PlanIdentity, StringComparison.Ordinal))
                    {
                        throw new PrintExportException(
                            OperationFailureKind.Protocol,
                            _services.Localization["PrintPreviewOutOfDate"]);
                    }
                }
                else
                {
                    await _services.PrintExporter!.ExportAsync(request, stream, cancellationToken);
                }
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PrintExportException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            throw new PrintExportException(
                OperationFailureKind.Internal,
                _services.Localization["PrintExportFailed"],
                exception);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                try
                {
                    _services.ReportDiagnostic?.Invoke(exception);
                }
                catch (Exception diagnosticException)
                {
                    System.Diagnostics.Debug.WriteLine(diagnosticException);
                }
            }
        }
    }

    private static ProjectDocument CreateNewDocument() =>
        ProjectDocumentPresets.CreateBlank().MarkDirty();

    private UserSafeExceptionInfo MapException(Exception exception)
    {
        if (exception is ViewportOperationException viewportFailure)
        {
            return new UserSafeExceptionInfo(
                viewportFailure.IsExpected ? UserSafeFailureKind.Operation : UserSafeFailureKind.Unexpected,
                viewportFailure.SafeMessage);
        }

        UserSafeExceptionInfo mapped = UserExceptionBoundary.Map(exception);
        return mapped.Kind == UserSafeFailureKind.Unexpected
            ? mapped with { Message = _services.Localization["UnexpectedError"] }
            : mapped;
    }

    private void NotifyUser(UserSafeExceptionInfo information)
    {
        if (_disposed || _isClosing)
        {
            return;
        }

        ReportStatus(information.Message);
        RouteController.ShowOverlay(ScreenOverlayKind.Alert);
        SyncOperationOverlay();
    }

    private void ReportBackgroundFailure(Exception exception)
    {
        try
        {
            _services.ReportDiagnostic?.Invoke(exception);
        }
        catch (Exception diagnosticException)
        {
            System.Diagnostics.Debug.WriteLine(diagnosticException);
        }

        if (!_disposed)
        {
            ReportStatus(_services.Localization["UnexpectedError"]);
        }
    }

    private void ReportException(Exception exception)
    {
        _exceptionBoundary.Execute(() => ExceptionDispatchInfo.Capture(exception).Throw());
    }

    private string? TrySelectPath(Func<string?> selector)
    {
        string? result = null;
        return _exceptionBoundary.Execute(() => result = selector()) ? result : null;
    }

    private string Format(string resourceKey, object value) =>
        string.Format(_services.Localization.Culture, _services.Localization[resourceKey], value);

    private void OnDocumentEdited(object? sender, DocumentEditedEventArgs eventArgs)
    {
        if (_disposed || _isClosing)
        {
            return;
        }

        _currentDocument = eventArgs.Document;
        _documentRevision = checked(_documentRevision + 1);
        _analysisState = new AnalysisResultState();
        _printPreviewState = null;
        RemoveUnavailableResultPrintSections();
        _editor.SetDocument(_currentDocument);
        DocumentHost.SetDocument(_currentDocument, resetCamera: false);
        DocumentHost.SetResult(null);
        UpdateWindowTitle();
        UpdateCommandState();
    }

    private void OnEditorSelectionChanged(object? sender, ViewportSelectionChangedEventArgs eventArgs)
    {
        if (!_disposed)
        {
            DocumentHost.SetSelection(eventArgs.Selection, eventArgs.Origin);
        }
    }

    private void OnViewportSelectionChanged(object? sender, ViewportSelectionChangedEventArgs eventArgs)
    {
        if (!_disposed)
        {
            _editor.SetSelection(eventArgs.Selection);
        }
    }

    private void OnEditorValidationFailed(object? sender, EditorValidationEventArgs eventArgs)
    {
        if (!_disposed)
        {
            ReportStatus(eventArgs.Message);
        }
    }

    private void OnViewportOperationFailed(object? sender, ViewportOperationFailedEventArgs eventArgs)
    {
        if (_disposed)
        {
            return;
        }

        if (!eventArgs.Failure.IsExpected)
        {
            ReportException(eventArgs.Failure);
            return;
        }

        try
        {
            _services.ReportDiagnostic?.Invoke(eventArgs.Failure);
        }
        catch (Exception diagnosticException)
        {
            System.Diagnostics.Debug.WriteLine(diagnosticException);
        }

        ReportStatus(eventArgs.Failure.SafeMessage);
    }

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();

    private async void OnScreenCommandRequested(
        object? sender,
        ScreenCommandRequestedEventArgs eventArgs)
    {
        try
        {
            switch (eventArgs.Command)
            {
                case ScreenCommandKind.NewProject:
                    await NewProjectAsync();
                    if (_currentDocument is not null)
                    {
                        RouteController.CloseOverlay();
                    }

                    break;
                case ScreenCommandKind.OpenProject:
                    await OpenProjectAsync();
                    if (_currentDocument is not null)
                    {
                        RouteController.CloseOverlay();
                    }

                    break;
                case ScreenCommandKind.SaveProject:
                    await SaveProjectAsync();
                    break;
                case ScreenCommandKind.SaveProjectAs:
                    await SaveProjectAsync(saveAs: true);
                    break;
                case ScreenCommandKind.ShowPreset:
                    RouteController.ShowOverlay(ScreenOverlayKind.Preset);
                    break;
                case ScreenCommandKind.OpenPreset:
                    if (sender is IPresetOverlaySurface { SelectedPreset: BuiltInProjectPreset preset })
                    {
                        await OpenPresetAsync(preset);
                        if (_currentDocument is not null)
                        {
                            RouteController.CloseOverlay();
                        }
                    }

                    break;
                case ScreenCommandKind.RunAnalysis:
                    await ExecuteAnalysisAsync();
                    break;
                case ScreenCommandKind.CancelOperation:
                    CancelOperation();
                    break;
                case ScreenCommandKind.ShowPrint:
                    RouteController.ShowOverlay(ScreenOverlayKind.Print);
                    SyncPrintOverlay();
                    await RefreshPrintPreviewAsync();
                    break;
                case ScreenCommandKind.ConfigurePrint:
                    ApplyPrintOverlaySelection(sender);
                    await RefreshPrintPreviewAsync();
                    break;
                case ScreenCommandKind.RefreshPrintPreview:
                    await RefreshPrintPreviewAsync();
                    break;
                case ScreenCommandKind.ExportPdf:
                    await ExportPdfAsync();
                    break;
                case ScreenCommandKind.CloseOverlay:
                    RouteController.CloseOverlay();
                    break;
                case ScreenCommandKind.CloseRoute:
                    RouteController.CloseRoute();
                    break;
                case ScreenCommandKind.ShowHelp:
                    OpenExternalUri("https://help-frameweb.malme.app/");
                    break;
                case ScreenCommandKind.ShowContact:
                    ReportStatus("Contact support is available from the FrameWeb support site.");
                    RouteController.ShowOverlay(ScreenOverlayKind.Alert);
                    SyncOperationOverlay();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(eventArgs),
                        eventArgs.Command,
                        "Unknown screen command.");
            }
        }
        catch (Exception exception)
        {
            ReportException(exception);
        }
    }

    private void OnScreenLanguageRequested(object? sender, UiLanguageRequestedEventArgs eventArgs) =>
        SetLanguage(eventArgs.Language);

    private void ApplyPrintOverlaySelection(object? sender)
    {
        IPrintOverlaySurface? printSurface = sender as IPrintOverlaySurface
            ?? _screenShell.OverlayHost.ActiveSurface as IPrintOverlaySurface;
        if (printSurface is null)
        {
            throw new InvalidOperationException("The print command requires the active print overlay.");
        }

        _printSelection = printSurface.Selection;
        _printPreviewState = null;
        SyncPrintOverlay();
        UpdateCommandState();
    }

    private void SyncPrintOverlay()
    {
        if (_screenShell.OverlayHost.ActiveSurface is not IPrintOverlaySurface printSurface)
        {
            return;
        }

        printSurface.SetSelection(_printSelection);
        printSurface.SetPreview(_printPreviewState);
    }

    private void SetStatusResource(string resourceKey) =>
        ReportStatus(_services.Localization[resourceKey]);

    private void ReportStatus(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _statusMessage = message;
        SyncOperationOverlay();
    }

    private void SyncOperationOverlay()
    {
        if (_screenShell.OverlayHost.ActiveSurface is IOperationOverlaySurface operationSurface)
        {
            operationSurface.SetMessage(_statusMessage);
        }
    }

    private void OpenExternalUri(string uri)
    {
        Uri validated = new(uri, UriKind.Absolute);
        if (validated.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Only HTTPS support links may be opened.");
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = validated.AbsoluteUri,
            UseShellExecute = true,
        });
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_allowClose || _disposed)
        {
            return;
        }

        eventArgs.Cancel = true;
        TryCancelOperationForClose();
        if (_closeCheckRunning)
        {
            return;
        }

        _isClosing = true;
        _closeCheckRunning = true;
        UpdateCommandState();
        QueueCloseCompletion();
    }

    private async Task CompleteCloseAsync()
    {
        bool gateHeld = false;
        try
        {
            bool operationCompleted = await WaitForCurrentOperationAsync();
            await SwitchToUiThread();
            if (operationCompleted)
            {
                gateHeld = await _shellTransitionGate.WaitAsync(_services.OperationShutdownTimeout);
                await SwitchToUiThread();
            }

            if (!operationCompleted || !gateHeld)
            {
                ReportBackgroundFailure(new TimeoutException(
                    $"Shell shutdown exceeded the {_services.OperationShutdownTimeout.TotalMilliseconds:0}-millisecond limit; " +
                    "the closing revision will ignore late UI commits."));
            }

            if (_disposed)
            {
                return;
            }

            using CancellationTokenSource documentSaveCancellation = new();
            Task<DocumentSnapshot?> authorizationTask = AuthorizeDocumentReplacementAsync(
                CaptureDocument(),
                allowWhileClosing: true,
                documentSaveCancellation.Token);
            if (!await WaitForClosePhaseAsync(
                authorizationTask,
                documentSaveCancellation,
                "dirty-document save"))
            {
                return;
            }

            DocumentSnapshot? authorized = await authorizationTask;
            await SwitchToUiThread();
            if (authorized is not DocumentSnapshot current ||
                !CanPublish(current, allowWhileClosing: true))
            {
                return;
            }

            _allowClose = true;
            Close();
        }
        catch (Exception exception)
        {
            await SwitchToUiThread();
            ReportException(exception);
        }
        finally
        {
            if (gateHeld)
            {
                _shellTransitionGate.Release();
            }

            _closeCheckRunning = false;
            if (!_allowClose && !_disposed)
            {
                _isClosing = false;
                UpdateCommandState();
            }
        }
    }

    private void QueueCloseCompletion()
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _currentShellTransitionTask = completion.Task;
        try
        {
            BeginInvoke(new Action(async () =>
            {
                try
                {
                    await CompleteCloseAsync();
                }
                catch (Exception exception)
                {
                    ReportException(exception);
                }
                finally
                {
                    completion.TrySetResult();
                }
            }));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            completion.TrySetResult();
            _closeCheckRunning = false;
            _isClosing = false;
            ReportException(exception);
            if (!_disposed)
            {
                UpdateCommandState();
            }
        }
    }

    private async Task<bool> WaitForCurrentOperationAsync()
    {
        Task operation = _currentOperationTask;
        if (!operation.IsCompleted)
        {
            Task timeout = Task.Delay(_services.OperationShutdownTimeout);
            if (await Task.WhenAny(operation, timeout) != operation)
            {
                return false;
            }
        }

        try
        {
            await operation;
        }
        catch (Exception exception)
        {
            await SwitchToUiThread();
            ReportBackgroundFailure(exception);
        }

        return true;
    }

    private async Task<bool> WaitForClosePhaseAsync(
        Task operation,
        CancellationTokenSource cancellation,
        string phaseName)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(cancellation);
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseName);
        if (operation.IsCompleted)
        {
            await operation;
            return true;
        }

        Task timeout = Task.Delay(_services.OperationShutdownTimeout);
        if (await Task.WhenAny(operation, timeout) == operation)
        {
            await operation;
            return true;
        }

        try
        {
            cancellation.Cancel(throwOnFirstException: false);
        }
        catch (Exception exception)
        {
            await SwitchToUiThread();
            ReportBackgroundFailure(exception);
        }

        await SwitchToUiThread();
        ReportBackgroundFailure(new TimeoutException(
            $"Shell {phaseName} exceeded the " +
            $"{_services.OperationShutdownTimeout.TotalMilliseconds:0}-millisecond shutdown limit."));
        _ = ObserveLateClosePhaseAsync(operation, cancellation.Token);
        return false;
    }

    private async Task ObserveLateClosePhaseAsync(
        Task operation,
        CancellationToken expectedCancellation)
    {
        try
        {
            await operation;
        }
        catch (OperationCanceledException) when (expectedCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await SwitchToUiThread();
            ReportBackgroundFailure(exception);
        }
    }

    private void TryCancelOperationForClose()
    {
        try
        {
            _services.CancellationOwner.CancelCurrent();
        }
        catch (Exception exception)
        {
            ReportBackgroundFailure(exception);
        }
    }

    private readonly record struct DocumentSnapshot(
        ProjectDocument? Document,
        string? Path,
        long Revision);

    private readonly struct UiThreadSwitch(MainForm owner)
    {
        public UiThreadSwitchAwaiter GetAwaiter() => new(owner);
    }

    private readonly struct UiThreadSwitchAwaiter(MainForm owner) : INotifyCompletion
    {
        public bool IsCompleted => Environment.CurrentManagedThreadId == owner._uiThreadId;

        public void OnCompleted(Action continuation)
        {
            ArgumentNullException.ThrowIfNull(continuation);
            try
            {
                owner.BeginInvoke(continuation);
            }
            catch (InvalidOperationException) when (owner._disposed || !owner.IsHandleCreated)
            {
                ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), continuation);
            }
            catch (ObjectDisposedException)
            {
                ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), continuation);
            }
        }

        public void GetResult()
        {
        }
    }
}
