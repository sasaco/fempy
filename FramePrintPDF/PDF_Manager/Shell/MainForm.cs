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
using PDF_Manager.Shell.Docking;
using PDF_Manager.Shell.Lifecycle;
using PDF_Manager.Shell.Printing;
using PDF_Manager.Shell.Viewport;
using WeifenLuo.WinFormsUI.Docking;
using CoreDockState = PDF_Manager.Core.Shell.DockState;
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

    public static DocumentKey NavigationContentKey { get; } = DocumentKey.Tool("project-navigation");
    public static DocumentKey EditorContentKey { get; } = DocumentKey.Tool("property-editor");
    public static DocumentKey DiagnosticsContentKey { get; } = DocumentKey.Tool("diagnostics-progress");
    public static DocumentKey WorkspaceDocumentKey { get; } = DocumentKey.Document("project.workspace");

    private readonly MainFormServices _services;
    private readonly int _uiThreadId;
    private readonly DockPanel _dockPanel;
    private readonly VS2015LightTheme _theme;
    private readonly DockContentRegistry _contentRegistry;
    private readonly DockLayoutAdapter _layoutAdapter;
    private readonly ActivationCoordinator _activationCoordinator;
    private readonly UserExceptionBoundary _exceptionBoundary;
    private readonly SemaphoreSlim _shellTransitionGate = new(1, 1);
    private readonly MenuStrip _menuStrip = new() { Name = "MainMenu" };
    private readonly ToolStripMenuItem _fileMenu = new() { Name = "FileMenu" };
    private readonly ToolStripMenuItem _newMenuItem = new() { Name = "NewMenuItem" };
    private readonly ToolStripMenuItem _openMenuItem = new() { Name = "OpenMenuItem" };
    private readonly ToolStripMenuItem _saveMenuItem = new() { Name = "SaveMenuItem" };
    private readonly ToolStripMenuItem _saveAsMenuItem = new() { Name = "SaveAsMenuItem" };
    private readonly ToolStripMenuItem _exitMenuItem = new() { Name = "ExitMenuItem" };
    private readonly ToolStripMenuItem _analysisMenu = new() { Name = "AnalysisMenu" };
    private readonly ToolStripMenuItem _runAnalysisMenuItem = new() { Name = "RunAnalysisMenuItem" };
    private readonly ToolStripMenuItem _cancelMenuItem = new() { Name = "CancelMenuItem" };
    private readonly ToolStripMenuItem _printMenu = new() { Name = "PrintMenu" };
    private readonly ToolStripMenuItem _pageSetupMenuItem = new() { Name = "PageSetupMenuItem" };
    private readonly ToolStripMenuItem _printPreviewMenuItem = new() { Name = "PrintPreviewMenuItem" };
    private readonly ToolStripMenuItem _exportPdfMenuItem = new() { Name = "ExportPdfMenuItem" };
    private readonly ToolStripMenuItem _viewMenu = new() { Name = "ViewMenu" };
    private readonly ToolStripMenuItem _navigationMenuItem = new() { Name = "NavigationMenuItem" };
    private readonly ToolStripMenuItem _editorMenuItem = new() { Name = "EditorMenuItem" };
    private readonly ToolStripMenuItem _diagnosticsMenuItem = new() { Name = "DiagnosticsMenuItem" };
    private readonly ToolStripMenuItem _languageMenu = new() { Name = "LanguageMenu" };
    private readonly ToolStripMenuItem _japaneseMenuItem = new() { Name = "JapaneseMenuItem" };
    private readonly ToolStripMenuItem _englishMenuItem = new() { Name = "EnglishMenuItem" };
    private readonly ToolStripMenuItem _chineseMenuItem = new() { Name = "ChineseMenuItem" };

    private AnalysisResultState _analysisState = new();
    private ProjectDocument? _currentDocument;
    private string? _documentPath;
    private Task _currentOperationTask = Task.CompletedTask;
    private Task _currentShellTransitionTask = Task.CompletedTask;
    private Task _layoutRestoreTask = Task.CompletedTask;
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
    private bool _layoutRestoreStarted;
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

        _theme = new VS2015LightTheme();
        _dockPanel = new DockPanel
        {
            Dock = DockStyle.Fill,
            DocumentStyle = DocumentStyle.DockingWindow,
            Name = "DocumentDockPanel",
            Theme = _theme,
        };
        _contentRegistry = new DockContentRegistry(_dockPanel);
        _layoutAdapter = new DockLayoutAdapter(_dockPanel, _contentRegistry);
        _exceptionBoundary = new UserExceptionBoundary(
            NotifyUser,
            _services.ReportDiagnostic,
            MapException);
        _activationCoordinator = new ActivationCoordinator(
            ApplyActiveDocumentAsync,
            reportDiagnostic: _services.ReportDiagnostic);

        BuildMenu();
        Controls.Add(_dockPanel);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;

        RegisterContents();
        NavigationPane = (NavigationContent)_contentRegistry.Open(
            NavigationContentKey,
            CoreDockState.DockLeft);
        EditorPane = (EditorContent)_contentRegistry.Open(
            EditorContentKey,
            CoreDockState.DockRight);
        DiagnosticsPane = (DiagnosticsContent)_contentRegistry.Open(
            DiagnosticsContentKey,
            CoreDockState.DockBottom);
        DocumentHost = (ProjectDocumentContent)_contentRegistry.Open(
            WorkspaceDocumentKey,
            CoreDockState.Document);
        ActiveDocumentKey = WorkspaceDocumentKey;

        SubscribeEvents();
        ApplyLocalization();
        UpdateCommandState();
    }

    public DockPanel DockSurface => _dockPanel;

    public DockContentRegistry ContentRegistry => _contentRegistry;

    public DockLayoutAdapter LayoutAdapter => _layoutAdapter;

    public NavigationContent NavigationPane { get; private set; }

    public ProjectDocumentContent DocumentHost { get; private set; }

    public ProjectDocumentContent Viewport => DocumentHost;

    public EditorContent EditorPane { get; private set; }

    public DiagnosticsContent DiagnosticsPane { get; private set; }

    public ToolStripMenuItem FileMenu => _fileMenu;

    public ToolStripMenuItem OpenMenuItem => _openMenuItem;

    public ToolStripMenuItem SaveMenuItem => _saveMenuItem;

    public ToolStripMenuItem AnalyzeMenuItem => _runAnalysisMenuItem;

    public ToolStripMenuItem CancelMenuItem => _cancelMenuItem;

    public ToolStripMenuItem ExportPdfMenuItem => _exportPdfMenuItem;

    public ToolStripMenuItem PageSetupMenuItem => _pageSetupMenuItem;

    public ToolStripMenuItem PrintPreviewMenuItem => _printPreviewMenuItem;

    public PrintPageSetupSelection PrintSelection => _printSelection;

    public PrintPreviewState? CurrentPrintPreview => _printPreviewState;

    public ProjectDocument? CurrentDocument => _currentDocument;

    public AnalysisResultSet? CurrentResult => _analysisState.Current;

    public DocumentKey? ActiveDocumentKey { get; private set; }

    public CultureInfo CurrentCulture => _services.Localization.Culture;

    public bool IsOperationRunning => _isOperationRunning;

    public Task WhenActivationIdleAsync() => _activationCoordinator.WhenIdleAsync();

    public Task WhenCurrentOperationIdleAsync() => _currentOperationTask;

    public Task WhenShellTransitionIdleAsync() => _currentShellTransitionTask;

    public Task WhenLayoutRestoredAsync() => _layoutRestoreTask;

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
            DiagnosticsPane.Report(Format("StatusProjectOpened", path));
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
            DiagnosticsPane.SetStatusResource("StatusNotConfigured");
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
            DiagnosticsPane.SetStatusResource("StatusAnalysisCompleted");
            UpdateCommandState();
        });
    }

    public bool ConfigurePrintPage()
    {
        if (_disposed || _isClosing || _isOperationRunning)
        {
            return false;
        }

        PrintPageSetupSelection? candidate = _services.PrintDialogs.ShowPageSetup(
            this,
            _services.Localization,
            _printSelection);
        if (candidate is null)
        {
            return false;
        }

        _printSelection = candidate;
        _printPreviewState = null;
        DiagnosticsPane.SetStatusResource("StatusPrintSettingsUpdated");
        return true;
    }

    public async Task RefreshPrintPreviewAsync(bool showDialog = false)
    {
        if (_isOperationRunning || _isClosing || _disposed)
        {
            return;
        }

        if (_currentDocument is null || _services.PrintExporter is null)
        {
            DiagnosticsPane.SetStatusResource("StatusNotConfigured");
            return;
        }

        DocumentSnapshot snapshot = CaptureDocument();
        PrintPreviewState? candidate = null;
        bool succeeded = await RunOperationAsync(async cancellationToken =>
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
            DiagnosticsPane.Report(Format("StatusPrintPreviewReady", preview.PageCount));
        });
        if (succeeded && showDialog && candidate is not null && ReferenceEquals(candidate, _printPreviewState))
        {
            _services.PrintDialogs.ShowPreview(this, _services.Localization, candidate);
        }
    }

    public bool SelectPrintPreviewPage(int pageIndex)
    {
        if (_printPreviewState is null || pageIndex < 0 || pageIndex >= _printPreviewState.PageCount)
        {
            return false;
        }

        _printPreviewState = _printPreviewState.SelectPage(pageIndex);
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
            DiagnosticsPane.SetStatusResource("StatusNotConfigured");
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
                DiagnosticsPane.Report(Format("StatusPdfExported", path));
            }
        });
    }

    public void CancelOperation()
    {
        if (_services.CancellationOwner.CancelCurrent())
        {
            DiagnosticsPane.SetStatusResource("StatusCanceled");
        }
    }

    public void ShowNavigationPane() => ShowContent(NavigationContentKey);

    public void ShowEditorPane() => ShowContent(EditorContentKey);

    public void ShowDiagnosticsPane() => ShowContent(DiagnosticsContentKey);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            UnsubscribeEvents();
            _activationCoordinator.Cancel();
            _activationCoordinator.Dispose();
            _services.CancellationOwner.CancelCurrent();
            _services.CancellationOwner.Dispose();
            _layoutAdapter.Dispose();
            _contentRegistry.Dispose();
            _dockPanel.Dispose();
            _theme.Dispose();
            _services.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildMenu()
    {
        _fileMenu.DropDownItems.AddRange([
            _newMenuItem,
            _openMenuItem,
            new ToolStripSeparator(),
            _saveMenuItem,
            _saveAsMenuItem,
            new ToolStripSeparator(),
            _exitMenuItem,
        ]);
        _analysisMenu.DropDownItems.AddRange([_runAnalysisMenuItem, _cancelMenuItem]);
        _printMenu.DropDownItems.AddRange([
            _pageSetupMenuItem,
            _printPreviewMenuItem,
            new ToolStripSeparator(),
            _exportPdfMenuItem,
        ]);
        _viewMenu.DropDownItems.AddRange([
            _navigationMenuItem,
            _editorMenuItem,
            _diagnosticsMenuItem,
        ]);
        _languageMenu.DropDownItems.AddRange([
            _japaneseMenuItem,
            _englishMenuItem,
            _chineseMenuItem,
        ]);
        _menuStrip.Items.AddRange([_fileMenu, _analysisMenu, _printMenu, _viewMenu, _languageMenu]);
    }

    private void RegisterContents()
    {
        LocalizationService localization = _services.Localization;
        _contentRegistry.Register(
            NavigationContentKey,
            () => new NavigationContent(NavigationContentKey, localization));
        _contentRegistry.Register(
            EditorContentKey,
            () => new EditorContent(EditorContentKey, localization));
        _contentRegistry.Register(
            DiagnosticsContentKey,
            () => new DiagnosticsContent(DiagnosticsContentKey, localization));
        _contentRegistry.Register(
            WorkspaceDocumentKey,
            () => new ProjectDocumentContent(WorkspaceDocumentKey, localization));
    }

    private void SubscribeEvents()
    {
        _newMenuItem.Click += OnNewClick;
        _openMenuItem.Click += OnOpenClick;
        _saveMenuItem.Click += OnSaveClick;
        _saveAsMenuItem.Click += OnSaveAsClick;
        _exitMenuItem.Click += OnExitClick;
        _runAnalysisMenuItem.Click += OnRunAnalysisClick;
        _cancelMenuItem.Click += OnCancelClick;
        _pageSetupMenuItem.Click += OnPageSetupClick;
        _printPreviewMenuItem.Click += OnPrintPreviewClick;
        _exportPdfMenuItem.Click += OnExportPdfClick;
        _navigationMenuItem.Click += OnNavigationClick;
        _editorMenuItem.Click += OnEditorClick;
        _diagnosticsMenuItem.Click += OnDiagnosticsClick;
        _japaneseMenuItem.Click += OnJapaneseClick;
        _englishMenuItem.Click += OnEnglishClick;
        _chineseMenuItem.Click += OnChineseClick;
        _dockPanel.ActiveDocumentChanged += OnActiveDocumentChanged;
        _contentRegistry.ContentCreated += OnContentCreated;
        _services.Localization.CultureChanged += OnCultureChanged;
        EditorPane.DocumentEdited += OnDocumentEdited;
        EditorPane.SelectionChanged += OnEditorSelectionChanged;
        EditorPane.ValidationFailed += OnEditorValidationFailed;
        DocumentHost.SelectionChanged += OnViewportSelectionChanged;
        DocumentHost.ViewportOperationFailed += OnViewportOperationFailed;
        Shown += OnShown;
        FormClosing += OnFormClosing;
    }

    private void UnsubscribeEvents()
    {
        _newMenuItem.Click -= OnNewClick;
        _openMenuItem.Click -= OnOpenClick;
        _saveMenuItem.Click -= OnSaveClick;
        _saveAsMenuItem.Click -= OnSaveAsClick;
        _exitMenuItem.Click -= OnExitClick;
        _runAnalysisMenuItem.Click -= OnRunAnalysisClick;
        _cancelMenuItem.Click -= OnCancelClick;
        _pageSetupMenuItem.Click -= OnPageSetupClick;
        _printPreviewMenuItem.Click -= OnPrintPreviewClick;
        _exportPdfMenuItem.Click -= OnExportPdfClick;
        _navigationMenuItem.Click -= OnNavigationClick;
        _editorMenuItem.Click -= OnEditorClick;
        _diagnosticsMenuItem.Click -= OnDiagnosticsClick;
        _japaneseMenuItem.Click -= OnJapaneseClick;
        _englishMenuItem.Click -= OnEnglishClick;
        _chineseMenuItem.Click -= OnChineseClick;
        _dockPanel.ActiveDocumentChanged -= OnActiveDocumentChanged;
        _contentRegistry.ContentCreated -= OnContentCreated;
        _services.Localization.CultureChanged -= OnCultureChanged;
        EditorPane.DocumentEdited -= OnDocumentEdited;
        EditorPane.SelectionChanged -= OnEditorSelectionChanged;
        EditorPane.ValidationFailed -= OnEditorValidationFailed;
        DocumentHost.SelectionChanged -= OnViewportSelectionChanged;
        DocumentHost.ViewportOperationFailed -= OnViewportOperationFailed;
        Shown -= OnShown;
        FormClosing -= OnFormClosing;
    }

    private void ApplyLocalization()
    {
        _fileMenu.Text = _services.Localization["MenuFile"];
        _newMenuItem.Text = _services.Localization["MenuNew"];
        _openMenuItem.Text = _services.Localization["MenuOpen"];
        _saveMenuItem.Text = _services.Localization["MenuSave"];
        _saveAsMenuItem.Text = _services.Localization["MenuSaveAs"];
        _exitMenuItem.Text = _services.Localization["MenuExit"];
        _analysisMenu.Text = _services.Localization["MenuAnalysis"];
        _runAnalysisMenuItem.Text = _services.Localization["MenuRunAnalysis"];
        _cancelMenuItem.Text = _services.Localization["MenuCancel"];
        _printMenu.Text = _services.Localization["MenuPrint"];
        _pageSetupMenuItem.Text = _services.Localization["MenuPageSetup"];
        _printPreviewMenuItem.Text = _services.Localization["MenuPrintPreview"];
        _exportPdfMenuItem.Text = _services.Localization["MenuExportPdf"];
        _viewMenu.Text = _services.Localization["MenuView"];
        _navigationMenuItem.Text = _services.Localization["MenuNavigation"];
        _editorMenuItem.Text = _services.Localization["MenuEditor"];
        _diagnosticsMenuItem.Text = _services.Localization["MenuDiagnostics"];
        _languageMenu.Text = _services.Localization["MenuLanguage"];
        _japaneseMenuItem.Text = _services.Localization["LanguageJapanese"];
        _englishMenuItem.Text = _services.Localization["LanguageEnglish"];
        _chineseMenuItem.Text = _services.Localization["LanguageChinese"];
        _japaneseMenuItem.Checked = _services.Localization.Language == UiLanguage.Japanese;
        _englishMenuItem.Checked = _services.Localization.Language == UiLanguage.English;
        _chineseMenuItem.Checked = _services.Localization.Language == UiLanguage.Chinese;

        foreach ((_, DockContent content) in _contentRegistry.Contents)
        {
            if (content is ILocalizedShellContent localizable)
            {
                localizable.ApplyLocalization();
            }
        }

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

        NavigationPane.SetDocument(document);
        EditorPane.SetDocument(document);
        ProjectDocumentContent viewport = EnsureDocumentHost();
        viewport.SetDocument(document, resetCamera: true);
        viewport.SetResult(_analysisState.Current);
        UpdateWindowTitle();
        UpdateCommandState();
    }

    private ProjectDocumentContent EnsureDocumentHost()
    {
        if (_contentRegistry.TryGet(WorkspaceDocumentKey, out DockContent? existing))
        {
            DocumentHost = (ProjectDocumentContent)existing!;
            return DocumentHost;
        }

        DocumentHost = (ProjectDocumentContent)_contentRegistry.Open(
            WorkspaceDocumentKey,
            CoreDockState.Document);
        return DocumentHost;
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
        _newMenuItem.Enabled = state.CanCreate;
        _openMenuItem.Enabled = state.CanOpen;
        _saveMenuItem.Enabled = state.CanSave;
        _saveAsMenuItem.Enabled = state.CanSaveAs;
        _runAnalysisMenuItem.Enabled = state.CanAnalyze && _services.AnalysisClient is not null;
        bool canPrint = _currentDocument is not null && !shellBusy && _services.PrintExporter is not null;
        _pageSetupMenuItem.Enabled = canPrint;
        _printPreviewMenuItem.Enabled = canPrint;
        _exportPdfMenuItem.Enabled = state.CanPrint && _services.PrintExporter is not null;
        _cancelMenuItem.Enabled = _isOperationRunning && !_isClosing;
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
        using OperationCancellationOwner.OperationCancellationLease lease =
            _services.CancellationOwner.Begin(cancellationToken);
        _isOperationRunning = true;
        DiagnosticsPane.SetBusy(true);
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
                DiagnosticsPane.SetStatusResource("StatusCanceled");
            }

            return succeeded;
        }
        finally
        {
            if (revision == _operationRevision && !_disposed)
            {
                _isOperationRunning = false;
                DiagnosticsPane.SetBusy(false);
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
            DiagnosticsPane.Report(Format("StatusProjectSaved", path));
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
        ProjectDocumentPresets.CreateRepresentativeFrame().MarkDirty();

    private void ShowContent(DocumentKey key) =>
        _exceptionBoundary.Execute(() => _contentRegistry.Open(key));

    private async Task RestoreLayoutAsync()
    {
        try
        {
            string? json = await _services.LayoutStore.LoadAsync();
            await SwitchToUiThread();
            if (json is null || _disposed || _isClosing)
            {
                return;
            }

            _layoutAdapter.RestoreJson(json);
            DocumentKey? restoredActiveDocument = _layoutAdapter.ActiveDocumentKey;
            if (restoredActiveDocument is DocumentKey active)
            {
                await _activationCoordinator.ActivateAsync(active);
                await SwitchToUiThread();
            }
            else
            {
                ActiveDocumentKey = null;
                _activationCoordinator.ClearActive();
            }
        }
        catch (Exception exception)
        {
            await SwitchToUiThread();
            ReportBackgroundFailure(exception);
        }
    }

    private async Task PersistLayoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string json = _layoutAdapter.CaptureJson();
            await _services.LayoutStore.SaveAsync(json, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await SwitchToUiThread();
            ReportBackgroundFailure(exception);
        }
    }

    private Task ApplyActiveDocumentAsync(DocumentKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ActiveDocumentKey = key;
        return Task.CompletedTask;
    }

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

        DiagnosticsPane.Report(information.Message);
        _services.Dialogs.ShowError(
            this,
            _services.Localization["ErrorTitle"],
            information.Message);
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
            DiagnosticsPane.Report(_services.Localization["UnexpectedError"]);
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

    private void OnContentCreated(
        object? sender,
        PDF_Manager.Shell.Docking.DockContentEventArgs eventArgs)
    {
        if (eventArgs.Key == WorkspaceDocumentKey)
        {
            DocumentHost = (ProjectDocumentContent)eventArgs.Content;
            DocumentHost.SelectionChanged += OnViewportSelectionChanged;
            DocumentHost.ViewportOperationFailed += OnViewportOperationFailed;
            DocumentHost.SetDocument(_currentDocument, resetCamera: true);
            DocumentHost.SetResult(_analysisState.Current);
        }
    }

    private async void OnActiveDocumentChanged(object? sender, EventArgs eventArgs)
    {
        if (_dockPanel.ActiveDocument is not DockContent active ||
            !_contentRegistry.TryGetKey(active, out DocumentKey key))
        {
            _layoutAdapter.ActiveDocumentKey = null;
            ActiveDocumentKey = null;
            _activationCoordinator.ClearActive();
            return;
        }

        try
        {
            await _activationCoordinator.ActivateAsync(key);
            await SwitchToUiThread();
        }
        catch (OperationCanceledException exception)
            when (exception.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await SwitchToUiThread();
            ReportException(exception);
        }
    }

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
        NavigationPane.SetDocument(_currentDocument);
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
            EditorPane.SetSelection(eventArgs.Selection);
        }
    }

    private void OnEditorValidationFailed(object? sender, EditorValidationEventArgs eventArgs)
    {
        if (!_disposed)
        {
            DiagnosticsPane.Report(eventArgs.Message);
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

        DiagnosticsPane.Report(eventArgs.Failure.SafeMessage);
    }

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();

    private void OnShown(object? sender, EventArgs eventArgs)
    {
        if (_layoutRestoreStarted || _disposed || _isClosing)
        {
            return;
        }

        _layoutRestoreStarted = true;
        _layoutRestoreTask = RunShellTransitionAsync(RestoreLayoutAsync);
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

            using CancellationTokenSource layoutSaveCancellation = new();
            Task layoutSaveTask = PersistLayoutAsync(layoutSaveCancellation.Token);
            await WaitForClosePhaseAsync(
                layoutSaveTask,
                layoutSaveCancellation,
                "layout persistence");
            await SwitchToUiThread();
            if (!CanPublish(current, allowWhileClosing: true))
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

    private async void OnNewClick(object? sender, EventArgs eventArgs) => await NewProjectAsync();

    private async void OnOpenClick(object? sender, EventArgs eventArgs) => await OpenProjectAsync();

    private async void OnSaveClick(object? sender, EventArgs eventArgs) => await SaveProjectAsync();

    private async void OnSaveAsClick(object? sender, EventArgs eventArgs) => await SaveProjectAsync(saveAs: true);

    private void OnExitClick(object? sender, EventArgs eventArgs) => Close();

    private async void OnRunAnalysisClick(object? sender, EventArgs eventArgs) => await ExecuteAnalysisAsync();

    private void OnCancelClick(object? sender, EventArgs eventArgs) => CancelOperation();

    private void OnPageSetupClick(object? sender, EventArgs eventArgs) =>
        _exceptionBoundary.Execute(() => _ = ConfigurePrintPage());

    private async void OnPrintPreviewClick(object? sender, EventArgs eventArgs) =>
        await RefreshPrintPreviewAsync(showDialog: true);

    private async void OnExportPdfClick(object? sender, EventArgs eventArgs) => await ExportPdfAsync();

    private void OnNavigationClick(object? sender, EventArgs eventArgs) => ShowNavigationPane();

    private void OnEditorClick(object? sender, EventArgs eventArgs) => ShowEditorPane();

    private void OnDiagnosticsClick(object? sender, EventArgs eventArgs) => ShowDiagnosticsPane();

    private void OnJapaneseClick(object? sender, EventArgs eventArgs) => SetLanguage(UiLanguage.Japanese);

    private void OnEnglishClick(object? sender, EventArgs eventArgs) => SetLanguage(UiLanguage.English);

    private void OnChineseClick(object? sender, EventArgs eventArgs) => SetLanguage(UiLanguage.Chinese);

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
