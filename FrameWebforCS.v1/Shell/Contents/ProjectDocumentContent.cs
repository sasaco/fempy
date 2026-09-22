using System.Collections.ObjectModel;
using System.Globalization;
using FrameWebforCS.Core.Abstractions;
using FrameWebforCS.Core.Analysis;
using FrameWebforCS.Core.Documents;
using FrameWebforCS.Core.Results;
using FrameWebforCS.Core.Shell;
using FrameWebforCS.Rendering;
using FrameWebforCS.Rendering.Scene;
using FrameWebforCS.Resources;
using FrameWebforCS.Shell.Viewport;
using WeifenLuo.WinFormsUI.Docking;

namespace FrameWebforCS.Shell.Contents;

public sealed class ResultPageChangedEventArgs(int pageIndex, int pageCount) : EventArgs
{
    public int PageIndex { get; } = pageIndex;

    public int PageCount { get; } = pageCount;
}

public sealed class ProjectDocumentContent : ShellDockContent
{
    public const int MaximumPngDimension = 8_192;
    public const int MaximumPngEncodedBytes = 32 * 1024 * 1024;
    public const int MaximumResultTableRows = 10_000;

    private readonly string _sceneStableId;
    private readonly OpenGlViewportLifecycle _renderer = new();
    private readonly ProjectDocumentSceneProjector _projector = new();
    private readonly ViewportResultNavigator _resultNavigator = new();
    private readonly ResultPresentationService _resultPresentationService = new();
    private readonly ResultCsvExporter _resultCsvExporter = new();
    private readonly ViewportUpdateScheduler _updateScheduler;
    private readonly Dictionary<SceneLayerKind, long> _layerInvalidationCounts =
        Enum.GetValues<SceneLayerKind>().ToDictionary(kind => kind, _ => 0L);
    private readonly Label _summary = new()
    {
        AutoSize = true,
        BackColor = Color.FromArgb(224, 232, 240),
        ForeColor = Color.FromArgb(38, 50, 56),
        Name = "ViewportSummary",
        Padding = new Padding(10, 6, 10, 6),
    };
    private readonly Label _renderFailure = new()
    {
        AutoSize = true,
        BackColor = Color.FromArgb(255, 235, 238),
        ForeColor = Color.FromArgb(183, 28, 28),
        Name = "ViewportFailure",
        Padding = new Padding(10, 6, 10, 6),
        Visible = false,
    };
    private readonly Panel _viewport = new()
    {
        BackColor = Color.FromArgb(245, 248, 250),
        Dock = DockStyle.Fill,
        Name = "ViewportHost",
    };
    private readonly ToolStrip _toolbar = new()
    {
        Dock = DockStyle.Top,
        GripStyle = ToolStripGripStyle.Hidden,
        Name = "ViewportToolbar",
    };
    private readonly ToolStripButton _projection = new() { Name = "ProjectionButton" };
    private readonly ToolStripButton _fit = new() { Name = "FitButton" };
    private readonly ToolStripButton _home = new() { Name = "HomeButton" };
    private readonly ToolStripButton _grid = new() { CheckOnClick = true, Name = "GridButton" };
    private readonly ToolStripButton _axes = new() { CheckOnClick = true, Name = "AxesButton" };
    private readonly ToolStripButton _labels = new() { CheckOnClick = true, Name = "LabelsButton" };
    private readonly ToolStripButton _legends = new() { CheckOnClick = true, Name = "LegendsButton" };
    private readonly ToolStripButton _png = new() { Name = "PngButton" };
    private readonly ToolStripButton _resultCsv = new() { Name = "ResultCsvExportButton" };
    private readonly ToolStripButton _pickupCsv = new() { Name = "ResultPickupExportButton" };
    private readonly ToolStripLabel _layerLabel = new() { Name = "LayerLabel" };
    private readonly ToolStripComboBox _layerSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultLayerSelector",
        Width = 132,
    };
    private readonly ToolStripLabel _coordinateLabel = new() { Name = "ResultCoordinateLabel" };
    private readonly ToolStripComboBox _coordinateSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultCoordinateSelector",
        Width = 180,
    };
    private readonly ToolStripLabel _caseLabel = new() { Name = "ResultCaseLabel" };
    private readonly ToolStripComboBox _caseSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultCaseSelector",
        Width = 132,
    };
    private readonly ToolStripLabel _stateLabel = new() { Name = "ResultStateLabel" };
    private readonly ToolStripComboBox _stateSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultStateSelector",
        Width = 132,
    };
    private readonly ToolStripButton _previousResult = new() { Name = "PreviousResultButton" };
    private readonly ToolStripButton _nextResult = new() { Name = "NextResultButton" };
    private readonly ToolStripLabel _page = new() { Name = "ResultPageLabel" };
    private readonly ToolStripLabel _derivedLabel = new() { Name = "ResultDerivedLabel" };
    private readonly ToolStripComboBox _derivedSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultDerivedSelector",
        Width = 132,
    };
    private readonly ToolStripLabel _parentLabel = new() { Name = "ResultParentLabel" };
    private readonly ToolStripComboBox _parentSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultParentSelector",
        Width = 132,
    };
    private readonly ToolStripLabel _childLabel = new() { Name = "ResultChildLabel" };
    private readonly ToolStripComboBox _childSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultChildSelector",
        Width = 132,
    };
    private readonly ToolStripLabel _extremaLabel = new() { Name = "ResultExtremaLabel" };
    private readonly ToolStripComboBox _extremaSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultExtremaSelector",
        Width = 130,
    };
    private readonly SplitContainer _layout = new()
    {
        Dock = DockStyle.Fill,
        Name = "ViewportResultSplit",
        Orientation = Orientation.Horizontal,
    };
    private readonly ComboBox _resultTableSelector = new()
    {
        Dock = DockStyle.Top,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultTableSelector",
    };
    private readonly DataGridView _resultGrid = new()
    {
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        MultiSelect = false,
        Name = "ResultGrid",
        ReadOnly = true,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };
    private readonly Label _resultPresentationError = new()
    {
        AutoSize = true,
        BackColor = Color.FromArgb(255, 235, 238),
        Dock = DockStyle.Top,
        ForeColor = Color.FromArgb(183, 28, 28),
        Name = "ResultPresentationErrorLabel",
        Padding = new Padding(8, 4, 8, 4),
        Visible = false,
    };

    private ProjectDocument? _document;
    private AnalysisResultSet? _resultSet;
    private IReadOnlyList<PresentedStaticResult> _derivedResults = [];
    private IReadOnlyList<ResultPresentationPage> _resultPages = [];
    private IReadOnlyDictionary<string, MovingLoadEnvelope> _movingLoadEnvelopes =
        new ReadOnlyDictionary<string, MovingLoadEnvelope>(new Dictionary<string, MovingLoadEnvelope>());
    private PresentedStaticResult? _selectedDerivedResult;
    private ResultPresentationPage? _selectedResultPage;
    private ResultPresentationSourcePage? _selectedSourcePage;
    private ResultTableSet? _currentResultTables;
    private ViewportPresentationState _presentation = ViewportPresentationState.Default;
    private SceneLayerMask _lastInvalidatedLayers;
    private bool _updatingControls;
    private bool _updatingResultSelection;
    private bool _flushPosted;
    private bool _resetCameraAfterProjection;
    private int _publishedResultPageIndex = int.MinValue;
    private int _publishedResultPageCount = int.MinValue;
    private bool _disposed;

    public ProjectDocumentContent(DocumentKey contentKey, LocalizationService localization)
        : base(contentKey, localization)
    {
        _sceneStableId = $"viewport:{contentKey}";
        _updateScheduler = new ViewportUpdateScheduler(FlushSceneUpdate);
        _renderer.Control.Visible = false;
        DockAreas = DockAreas.Document | DockAreas.Float;
        ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.Document;
        _toolbar.Items.AddRange(
        [
            _projection, _fit, _home, _png, _resultCsv, _pickupCsv, new ToolStripSeparator(),
            _grid, _axes, _labels, _legends, new ToolStripSeparator(),
            _layerLabel, _layerSelector, new ToolStripSeparator(),
            _caseLabel, _caseSelector, _stateLabel, _stateSelector,
            _coordinateLabel, _coordinateSelector, _previousResult, _page, _nextResult,
            _derivedLabel, _derivedSelector, _parentLabel, _parentSelector, _childLabel, _childSelector,
            _extremaLabel, _extremaSelector,
        ]);
        _coordinateLabel.Visible = false;
        _coordinateSelector.Visible = false;
        _viewport.Controls.Add(_renderer.Control);
        _viewport.Controls.Add(_renderFailure);
        _viewport.Controls.Add(_summary);
        _layout.Panel1.Controls.Add(_viewport);
        _layout.Panel1.Controls.Add(_toolbar);
        _layout.Panel2.Controls.Add(_resultGrid);
        _layout.Panel2.Controls.Add(_resultTableSelector);
        _layout.Panel2.Controls.Add(_resultPresentationError);
        Controls.Add(_layout);

        _projection.Click += OnProjectionClick;
        _fit.Click += OnFitClick;
        _home.Click += OnHomeClick;
        _png.Click += OnPngClick;
        _resultCsv.Click += OnResultCsvClick;
        _pickupCsv.Click += OnPickupCsvClick;
        _grid.CheckedChanged += OnDecorationChanged;
        _axes.CheckedChanged += OnDecorationChanged;
        _labels.CheckedChanged += OnDecorationChanged;
        _legends.CheckedChanged += OnDecorationChanged;
        _layerSelector.SelectedIndexChanged += OnLayerChanged;
        _caseSelector.SelectedIndexChanged += OnCaseChanged;
        _stateSelector.SelectedIndexChanged += OnStateChanged;
        _coordinateSelector.SelectedIndexChanged += OnCoordinateChanged;
        _previousResult.Click += OnPreviousResultClick;
        _nextResult.Click += OnNextResultClick;
        _derivedSelector.SelectedIndexChanged += OnDerivedChanged;
        _parentSelector.SelectedIndexChanged += OnParentChanged;
        _childSelector.SelectedIndexChanged += OnChildChanged;
        _extremaSelector.SelectedIndexChanged += OnExtremaChanged;
        _resultTableSelector.SelectedIndexChanged += OnResultTableChanged;
        _resultGrid.SelectionChanged += OnResultGridSelectionChanged;
        _renderer.Control.HandleCreated += OnRendererHandleCreated;
        _renderer.Control.MouseClick += OnRendererMouseClick;
        _renderer.Control.MouseMove += OnRendererMouseMove;
        _renderer.Control.MouseLeave += OnRendererMouseLeave;
        _renderer.SelectionChanged += OnRendererSelectionChanged;
        _renderer.HoverChanged += OnRendererHoverChanged;
        _layout.SizeChanged += OnLayoutSizeChanged;
        ApplyLocalization();
    }

    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<EditorValidationEventArgs>? ViewportFailed;
    public event EventHandler<ViewportOperationFailedEventArgs>? ViewportOperationFailed;
    public event EventHandler<ViewportHoverChangedEventArgs>? HoverChanged;
    public event EventHandler<ResultPageChangedEventArgs>? ResultPageChanged;
    public ProjectDocument? Document => _document;
    public AnalysisResultSet? ResultSet => _resultSet;
    public Panel ViewportHost => _viewport;
    public DataGridView ResultGrid => _resultGrid;
    public ComboBox ResultTableSelector => _resultTableSelector;
    public ToolStripComboBox ResultLayerSelector => _layerSelector;
    public ToolStripComboBox ResultCaseSelector => _caseSelector;
    public ToolStripComboBox ResultStateSelector => _stateSelector;
    public ToolStripComboBox ResultCoordinateSelector => _coordinateSelector;
    public ToolStripComboBox ResultDerivedSelector => _derivedSelector;
    public ToolStripComboBox ResultParentSelector => _parentSelector;
    public ToolStripComboBox ResultChildSelector => _childSelector;
    public ToolStripComboBox ResultExtremaSelector => _extremaSelector;
    public ToolStripButton ResultCsvExportButton => _resultCsv;
    public ToolStripButton ResultPickupExportButton => _pickupCsv;
    public Label ResultPresentationErrorLabel => _resultPresentationError;
    public ToolStrip ViewportToolbar => _toolbar;
    public ViewportProjection Projection => _renderer.Projection;
    public ViewportCameraState Camera => _renderer.Camera;
    public ViewportCameraPolicy CameraPolicy => _renderer.CameraPolicy;
    public SceneEntityKey? Selection => _renderer.Selection;
    public SceneEntityKey? Hover => _renderer.Hover;
    public SceneLayerMask EffectiveVisibleLayers =>
        _renderer.VisibleLayers & (_renderer.Scene?.VisibleLayers ?? SceneLayerMask.None);
    public ViewportPresentationState Presentation => _presentation;
    public ResultCoordinate? SelectedResultCoordinate => _resultNavigator.CurrentCoordinate;
    public string? SelectedDerivedResultId => _selectedDerivedResult?.Id;
    public ResultPresentationPage? SelectedResultParentPage => _selectedResultPage;
    public ResultPresentationSourcePage? SelectedResultSourcePage => _selectedSourcePage;
    public MovingLoadEnvelope? SelectedMovingLoadEnvelope =>
        _selectedResultPage is { IsMovingLoad: true } page &&
        _movingLoadEnvelopes.TryGetValue(page.PageId, out MovingLoadEnvelope? envelope)
            ? envelope
            : null;
    public bool CanExportSelectedResultCsv =>
        SelectedMovingLoadEnvelope is not null ||
        (_selectedDerivedResult is null && DisplayedResult is StaticAnalysisResult);
    public bool CanExportSelectedPickupCsv =>
        _document is not null &&
        _selectedDerivedResult is { Kind: DerivedResultKind.Pickup, PickupEnvelope: not null };
    public ResultPresentationLimits PresentationLimits { get; set; } = ResultPresentationLimits.Default;
    public ResultPresentationUiError? LastResultPresentationError { get; private set; }
    public int ResultPageIndex => _resultNavigator.PageIndex;
    public int ResultPageCount => _resultNavigator.PageCount;
    public ViewportUpdateReason PendingUpdates => _updateScheduler.Pending;
    public int UpdateDispatchCount => _updateScheduler.DispatchCount;
    public int CoalescedUpdateCount => _updateScheduler.CoalescedRequestCount;
    public int SceneProjectionCount { get; private set; }
    public SceneLayerMask LastInvalidatedLayers => _lastInvalidatedLayers;
    public SceneLayerMask RendererPendingInvalidation => _renderer.PendingInvalidation;
    public long RendererInvalidationRequestCount => _renderer.InvalidationRequestCount;
    public long RendererInvalidationBatchCount => _renderer.InvalidationBatchCount;
    public long RendererLayerCompilationCount => _renderer.LayerCompilationCount;
    public RendererDiagnosticSnapshot RendererDiagnosticsSnapshot => RendererDiagnostics.Snapshot();
    public bool ResultTableTruncated { get; private set; }
    public ViewportOperationException? LastViewportFailure { get; private set; }

    public ResultTableSet? CurrentResultTables => _currentResultTables;

    public PrintResultSelection? CreatePrintResultSelection()
    {
        if (_currentResultTables is null)
        {
            return null;
        }

        string? provenance = _selectedDerivedResult is not null
            ? $"{_selectedDerivedResult.Kind}: {_selectedDerivedResult.Id}; " +
                $"sources={string.Join(",", _selectedDerivedResult.SourceIds)}"
            : _selectedResultPage is { IsMovingLoad: true } page && _selectedSourcePage is not null
                ? $"moving={page.PageId}; source={_selectedSourcePage.Result.CaseId}; " +
                    $"role={(_selectedSourcePage.IsParent ? "parent" : "child")}"
                : _resultNavigator.CurrentCoordinate?.ToString();
        return new PrintResultSelection(
            _currentResultTables,
            _selectedDerivedResult is null ? _resultNavigator.CurrentCoordinate : null,
            provenance,
            _selectedResultPage?.IsMovingLoad == true);
    }

    public ViewportSceneModel? CurrentScene
    {
        get
        {
            FlushPendingUpdates();
            return _renderer.Scene;
        }
    }

    public IReadOnlyDictionary<SceneLayerKind, long> LayerInvalidationCounts =>
        new ReadOnlyDictionary<SceneLayerKind, long>(new Dictionary<SceneLayerKind, long>(_layerInvalidationCounts));

    public Control DetachViewportHost()
    {
        // The FrameWeb shell renders status and failures through its dedicated
        // overlays. The legacy in-viewport summary/error labels have no Angular
        // counterpart and must not survive when the viewport changes owner.
        _viewport.Controls.Remove(_summary);
        _viewport.Controls.Remove(_renderFailure);
        _viewport.Parent?.Controls.Remove(_viewport);
        _viewport.Dock = DockStyle.Fill;
        return _viewport;
    }

    public void AttachResultGrid(Control parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        _resultGrid.Parent?.Controls.Remove(_resultGrid);
        _resultPresentationError.Parent?.Controls.Remove(_resultPresentationError);
        _resultGrid.Dock = DockStyle.Fill;
        _resultPresentationError.Dock = DockStyle.Top;
        parent.Controls.Add(_resultGrid);
        parent.Controls.Add(_resultPresentationError);
        _resultPresentationError.BringToFront();
    }

    public void ParkResultGrid()
    {
        _resultGrid.Parent?.Controls.Remove(_resultGrid);
        _resultPresentationError.Parent?.Controls.Remove(_resultPresentationError);
        _layout.Panel2.Controls.Add(_resultGrid);
        _layout.Panel2.Controls.Add(_resultPresentationError);
        _resultPresentationError.BringToFront();
    }

    public bool SelectResultTable(int index)
    {
        if (index < 0 || index >= _resultTableSelector.Items.Count)
        {
            return false;
        }

        _resultTableSelector.SelectedIndex = index;
        return true;
    }

    public bool SelectDerivedResult(DerivedResultKind? kind)
    {
        PresentedStaticResult? selected = kind is null
            ? null
            : _derivedResults.FirstOrDefault(result => result.Kind == kind);
        if (kind is not null && selected is null)
        {
            return false;
        }

        _selectedDerivedResult = selected;
        RefreshCurrentResultTables();
        RefreshResultCoordinateItems();
        RebuildResultTable();
        QueueSceneUpdate(ViewportUpdateReason.Result);
        return true;
    }

    public void SetDocument(ProjectDocument? document) => SetDocument(document, resetCamera: false);

    public void SetDocument(ProjectDocument? document, bool resetCamera)
    {
        ResultPresentationCandidate candidate;
        try
        {
            candidate = BuildResultPresentationCandidate(_resultSet, document);
        }
        catch (ResultPresentationLimitException exception)
        {
            SetResultPresentationError(exception);
            throw;
        }

        _document = document;
        _resetCameraAfterProjection |= resetCamera;
        ApplyResultPresentationCandidate(candidate);
        SynchronizePresentationPageFromCoordinate();
        RefreshCurrentResultTables();
        ApplyLocalization();
        QueueSceneUpdate(ViewportUpdateReason.Document);
    }

    public void SetResult(AnalysisResultSet? resultSet)
    {
        ResultPresentationCandidate candidate;
        try
        {
            candidate = BuildResultPresentationCandidate(resultSet, _document);
        }
        catch (ResultPresentationLimitException exception)
        {
            SetResultPresentationError(exception);
            throw;
        }

        _resultSet = resultSet;
        _resultNavigator.SetResultSet(resultSet);
        ApplyResultPresentationCandidate(candidate);
        SynchronizePresentationPageFromCoordinate();
        RefreshCurrentResultTables();
        RefreshResultCoordinateItems();
        RebuildResultTable();
        QueueSceneUpdate(ViewportUpdateReason.Result);
    }

    public void SetResultCoordinate(ResultCoordinate coordinate)
    {
        if (_resultNavigator.Select(coordinate))
        {
            SelectBaseResult();
            SynchronizePresentationPageFromCoordinate();
            RefreshCurrentResultTables();
            RefreshResultCoordinateItems();
            RebuildResultTable();
            QueueSceneUpdate(ViewportUpdateReason.Result);
        }
    }

    public bool SelectResultPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _resultNavigator.PageCount)
        {
            return false;
        }

        ResultCoordinate coordinate = _resultNavigator.Coordinates[pageIndex];
        return MoveResult(() => _resultNavigator.Select(coordinate));
    }

    public bool MoveToPreviousResult() => MoveResult(_resultNavigator.MovePrevious);
    public bool MoveToNextResult() => MoveResult(_resultNavigator.MoveNext);

    public ResultExportArtifact ExportSelectedResultCsv(ResultCsvExportLimits? limits = null)
    {
        if (SelectedMovingLoadEnvelope is MovingLoadEnvelope movingLoad)
        {
            return ResultExportArtifact.From(_resultCsvExporter.ExportMovingLoad(movingLoad, limits));
        }

        if (_selectedDerivedResult is null && DisplayedResult is StaticAnalysisResult staticResult)
        {
            return ResultExportArtifact.From(_resultCsvExporter.ExportBaseStatic(staticResult, limits));
        }

        throw new ResultPresentationException(
            ResultPresentationErrorCode.InvalidDefinition,
            "The selected result cannot be exported as base-static or moving-load CSV.");
    }

    public ResultExportArtifact ExportSelectedPickupCsv(ResultCsvExportLimits? limits = null)
    {
        if (_document is not null &&
            _selectedDerivedResult is { Kind: DerivedResultKind.Pickup, PickupEnvelope: not null } pickup)
        {
            return _document.Dimension == ModelDimension.TwoDimensional
                ? ResultExportArtifact.From(_resultCsvExporter.ExportPickup2D(pickup, limits))
                : ResultExportArtifact.From(_resultCsvExporter.ExportPickup(pickup, limits));
        }

        throw new ResultPresentationException(
            ResultPresentationErrorCode.InvalidDefinition,
            "The selected result is not a PICKUP result.");
    }

    public void SaveSelectedResultCsv(string path, ResultCsvExportLimits? limits = null) =>
        SaveResultExport(path, ExportSelectedResultCsv(limits));

    public void SaveSelectedPickupCsv(string path, ResultCsvExportLimits? limits = null) =>
        SaveResultExport(path, ExportSelectedPickupCsv(limits));

    public void SetDisplayMode(ViewportDisplayMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        _layerSelector.SelectedIndex = (int)mode;
        if (_presentation.DisplayMode != mode)
        {
            _presentation = _presentation with { DisplayMode = mode };
            QueueSceneUpdate(ViewportUpdateReason.Presentation);
        }
    }

    public void SetExtremaMode(SceneExtremaMode extrema)
    {
        if (!Enum.IsDefined(extrema)) throw new ArgumentOutOfRangeException(nameof(extrema));
        _updatingControls = true;
        try
        {
            _extremaSelector.SelectedIndex = (int)extrema;
        }
        finally
        {
            _updatingControls = false;
        }

        if (_presentation.ExtremaMode != extrema)
        {
            _presentation = _presentation with { ExtremaMode = extrema };
            ReconcileResultSelection(extrema);
            RebuildResultTable();
            QueueSceneUpdate(ViewportUpdateReason.Presentation);
        }
    }

    public void SetDecorations(bool showGrid, bool showAxes, bool showLabels, bool showLegends)
    {
        _updatingControls = true;
        try
        {
            _grid.Checked = showGrid;
            _axes.Checked = showAxes;
            _labels.Checked = showLabels;
            _legends.Checked = showLegends;
        }
        finally
        {
            _updatingControls = false;
        }

        ViewportPresentationState next = _presentation with
        {
            ShowGrid = showGrid,
            ShowAxes = showAxes,
            ShowLabels = showLabels,
            ShowLegends = showLegends,
        };
        if (next != _presentation)
        {
            _presentation = next;
            QueueSceneUpdate(ViewportUpdateReason.Presentation);
        }
    }

    public void SetSelection(SceneEntityKey? selection, ViewportSelectionOrigin origin = ViewportSelectionOrigin.Table)
    {
        if (_document is null || _document.Nodes.Count == 0) return;
        FlushPendingUpdates();
        _renderer.SetSelection(selection, origin);
        SynchronizeResultGridSelection(selection);
    }

    public SceneHitTestResult? HitTest(Point clientPoint, SceneHitTestOptions? options = null)
    {
        FlushPendingUpdates();
        return _renderer.HitTest(clientPoint, options);
    }

    public bool HoverAt(Point clientPoint)
    {
        FlushPendingUpdates();
        return _renderer.HoverAt(clientPoint);
    }

    public void SetHover(SceneEntityKey? hover)
    {
        FlushPendingUpdates();
        _renderer.SetHover(hover);
    }

    public void ToggleProjection()
    {
        if (_document?.Dimension == ModelDimension.TwoDimensional)
            _renderer.SetProjection(ViewportProjection.Orthographic);
        else
            _renderer.ToggleProjection();
        UpdateProjectionCaption();
    }

    public void Fit() => _renderer.Fit();
    public void Home() => _renderer.Home();

    public Bitmap CaptureViewport()
    {
        FlushPendingUpdates();
        return _renderer.Capture();
    }

    public byte[] CaptureViewportPng(
        int maximumWidth = MaximumPngDimension,
        int maximumHeight = MaximumPngDimension,
        int maximumEncodedBytes = MaximumPngEncodedBytes)
    {
        if (maximumWidth is <= 0 or > MaximumPngDimension) throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        if (maximumHeight is <= 0 or > MaximumPngDimension) throw new ArgumentOutOfRangeException(nameof(maximumHeight));
        if (maximumEncodedBytes is <= 0 or > MaximumPngEncodedBytes) throw new ArgumentOutOfRangeException(nameof(maximumEncodedBytes));
        FlushPendingUpdates();
        Size viewportSize = _renderer.Control.ClientSize;
        if (viewportSize.Width > maximumWidth || viewportSize.Height > maximumHeight)
            throw new InvalidOperationException("The viewport exceeds the requested PNG dimensions.");
        return _renderer.CapturePng(new ViewportPngCaptureOptions(maximumEncodedBytes));
    }

    public void SaveViewportPng(
        string path,
        int maximumWidth = MaximumPngDimension,
        int maximumHeight = MaximumPngDimension,
        int maximumEncodedBytes = MaximumPngEncodedBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, CaptureViewportPng(maximumWidth, maximumHeight, maximumEncodedBytes));
    }

    public void FlushPendingUpdates()
    {
        if (!_disposed) _updateScheduler.Flush();
    }

    public override void ApplyLocalization()
    {
        string caption = _document?.Metadata.Name ?? Localization["PaneViewport"];
        if (_document?.IsDirty == true) caption += Localization["DirtyIndicator"];
        Text = caption;
        TabText = caption;
        _summary.Text = _document is null
            ? Localization["ViewportEmpty"]
            : string.Format(Localization.Culture, Localization["ViewportSummary"], _document.Nodes.Count, _document.Members.Count);
        _renderFailure.Text = Localization["ViewportUnavailable"];
        _fit.Text = Localization["ViewportFit"];
        _home.Text = Localization["ViewportHome"];
        _png.Text = Localization["ViewportPng"];
        _resultCsv.Text = Localization["ResultCsv"];
        _pickupCsv.Text = Localization["ResultPickupExport"];
        _grid.Text = Localization["ViewportGrid"];
        _axes.Text = Localization["ViewportAxes"];
        _labels.Text = Localization["ViewportLabels"];
        _legends.Text = Localization["ViewportLegends"];
        _layerLabel.Text = Localization["ViewportLayer"];
        _caseLabel.Text = Localization["ResultCase"];
        _stateLabel.Text = Localization["ResultState"];
        _coordinateLabel.Text = Localization["ResultCoordinate"];
        _derivedLabel.Text = Localization["ResultDerived"];
        _parentLabel.Text = Localization["ResultParent"];
        _childLabel.Text = Localization["ResultChild"];
        _previousResult.Text = Localization["ResultPrevious"];
        _nextResult.Text = Localization["ResultNext"];
        _extremaLabel.Text = Localization["ResultExtrema"];

        _updatingControls = true;
        try
        {
            int layer = _layerSelector.SelectedIndex;
            _layerSelector.Items.Clear();
            _layerSelector.Items.AddRange(
            [
                Localization["ViewportLayerModel"], Localization["ViewportLayerDisplacement"],
                Localization["ViewportLayerLoads"], Localization["ViewportLayerReaction"],
                Localization["ViewportLayerSectionForce"],
            ]);
            _layerSelector.SelectedIndex = layer is >= 0 and <= 4 ? layer : (int)_presentation.DisplayMode;

            int extrema = _extremaSelector.SelectedIndex;
            _extremaSelector.Items.Clear();
            _extremaSelector.Items.AddRange(
            [
                Localization["ResultExtremaValues"], Localization["ResultExtremaMinimum"],
                Localization["ResultExtremaMaximum"], Localization["ResultExtremaAbsoluteMaximum"],
            ]);
            _extremaSelector.SelectedIndex = extrema is >= 0 and <= 3 ? extrema : (int)_presentation.ExtremaMode;

            RefreshResultTableItems();
            _grid.Checked = _presentation.ShowGrid;
            _axes.Checked = _presentation.ShowAxes;
            _labels.Checked = _presentation.ShowLabels;
            _legends.Checked = _presentation.ShowLegends;
        }
        finally
        {
            _updatingControls = false;
        }

        UpdateProjectionCaption();
        RelocalizeResultPresentationError();
        RefreshResultCoordinateItems();
        RebuildResultTable();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _projection.Click -= OnProjectionClick;
            _fit.Click -= OnFitClick;
            _home.Click -= OnHomeClick;
            _png.Click -= OnPngClick;
            _resultCsv.Click -= OnResultCsvClick;
            _pickupCsv.Click -= OnPickupCsvClick;
            _grid.CheckedChanged -= OnDecorationChanged;
            _axes.CheckedChanged -= OnDecorationChanged;
            _labels.CheckedChanged -= OnDecorationChanged;
            _legends.CheckedChanged -= OnDecorationChanged;
            _layerSelector.SelectedIndexChanged -= OnLayerChanged;
            _caseSelector.SelectedIndexChanged -= OnCaseChanged;
            _stateSelector.SelectedIndexChanged -= OnStateChanged;
            _coordinateSelector.SelectedIndexChanged -= OnCoordinateChanged;
            _previousResult.Click -= OnPreviousResultClick;
            _nextResult.Click -= OnNextResultClick;
            _derivedSelector.SelectedIndexChanged -= OnDerivedChanged;
            _parentSelector.SelectedIndexChanged -= OnParentChanged;
            _childSelector.SelectedIndexChanged -= OnChildChanged;
            _extremaSelector.SelectedIndexChanged -= OnExtremaChanged;
            _resultTableSelector.SelectedIndexChanged -= OnResultTableChanged;
            _resultGrid.SelectionChanged -= OnResultGridSelectionChanged;
            _renderer.Control.HandleCreated -= OnRendererHandleCreated;
            _renderer.Control.MouseClick -= OnRendererMouseClick;
            _renderer.Control.MouseMove -= OnRendererMouseMove;
            _renderer.Control.MouseLeave -= OnRendererMouseLeave;
            _renderer.SelectionChanged -= OnRendererSelectionChanged;
            _renderer.HoverChanged -= OnRendererHoverChanged;
            _layout.SizeChanged -= OnLayoutSizeChanged;
            _updateScheduler.Dispose();
            _renderer.Dispose();
            if (_viewport.Controls.Contains(_renderer.Control)) _viewport.Controls.Remove(_renderer.Control);
            _summary.Dispose();
            _renderFailure.Dispose();
        }
        base.Dispose(disposing);
    }

    private bool MoveResult(Func<bool> move)
    {
        bool changed = move();
        if (changed)
        {
            SelectBaseResult();
            SynchronizePresentationPageFromCoordinate();
            RefreshCurrentResultTables();
            RefreshResultCoordinateItems();
            RebuildResultTable();
            QueueSceneUpdate(ViewportUpdateReason.Result);
        }
        return changed;
    }

    private AnalysisResult? DisplayedResult =>
        _selectedDerivedResult?.AnalysisResult ?? _selectedSourcePage?.Result ?? _resultNavigator.Current;

    private IReadOnlyList<SupportReaction>? DisplayedReactionProjection =>
        _selectedDerivedResult is null &&
        _selectedResultPage is { IsMovingLoad: true } &&
        _selectedSourcePage is { IsParent: true }
            ? SelectedMovingLoadEnvelope?.AbsoluteReactionProjection
            : null;

    private ResultPresentationCandidate BuildResultPresentationCandidate(
        AnalysisResultSet? resultSet,
        ProjectDocument? document)
    {
        if (resultSet is null)
        {
            return ResultPresentationCandidate.Empty;
        }

        AnalysisResultSetValidator.Validate(resultSet);
        ResultPresentationBudget budget = new(PresentationLimits);
        IReadOnlyList<MovingLoadDefinition> movingLoads = document?.MovingLoads ?? [];
        IReadOnlyList<DerivedResultDefinition> derivedDefinitions = document?.DerivedResults ?? [];
        ResultPresentationException? error = null;
        IReadOnlyList<ResultPresentationPage> pages;
        try
        {
            pages = _resultPresentationService.BuildPages(resultSet, movingLoads, budget);
        }
        catch (ResultPresentationLimitException)
        {
            throw;
        }
        catch (ResultPresentationException exception)
        {
            error = exception;
            pages = _resultPresentationService.BuildPages(resultSet, [], budget);
        }

        IReadOnlyList<PresentedStaticResult> derivedResults;
        try
        {
            derivedResults = _resultPresentationService.BuildDerivedResults(resultSet, derivedDefinitions, budget);
        }
        catch (ResultPresentationLimitException)
        {
            throw;
        }
        catch (NonStaticDerivedOperandException exception)
        {
            error = exception;
            derivedResults = [];
        }
        catch (ResultPresentationException exception)
        {
            error ??= exception;
            derivedResults = [];
        }

        Dictionary<string, MovingLoadEnvelope> envelopes = new(StringComparer.Ordinal);
        if (error is null || pages.Any(page => page.IsMovingLoad))
        {
            foreach (MovingLoadDefinition movingLoad in movingLoads)
            {
                if (!pages.Any(page => page.IsMovingLoad &&
                    string.Equals(page.PageId, movingLoad.Id, StringComparison.Ordinal)))
                {
                    continue;
                }

                try
                {
                    envelopes.Add(
                        movingLoad.Id,
                        _resultPresentationService.BuildMovingLoadEnvelope(resultSet, movingLoad, budget));
                }
                catch (ResultPresentationLimitException)
                {
                    throw;
                }
                catch (ResultPresentationException exception)
                {
                    error ??= exception;
                }
            }
        }

        return new ResultPresentationCandidate(
            derivedResults,
            pages,
            new ReadOnlyDictionary<string, MovingLoadEnvelope>(envelopes),
            error);
    }

    private void ApplyResultPresentationCandidate(ResultPresentationCandidate candidate)
    {
        _derivedResults = candidate.DerivedResults;
        _resultPages = candidate.Pages;
        _movingLoadEnvelopes = candidate.MovingLoadEnvelopes;
        if (_selectedDerivedResult is not null &&
            !_derivedResults.Any(result => string.Equals(result.Id, _selectedDerivedResult.Id, StringComparison.Ordinal)))
        {
            _selectedDerivedResult = null;
        }

        if (candidate.Error is null)
        {
            ClearResultPresentationError();
        }
        else
        {
            SetResultPresentationError(candidate.Error);
        }
    }

    private void SelectBaseResult() => _selectedDerivedResult = null;

    private void SynchronizePresentationPageFromCoordinate()
    {
        ResultCoordinate? coordinate = _resultNavigator.CurrentCoordinate;
        _selectedResultPage = null;
        _selectedSourcePage = null;
        if (coordinate is null)
        {
            return;
        }

        foreach (ResultPresentationPage page in _resultPages)
        {
            ResultPresentationSourcePage? source = page.SourcePages
                .FirstOrDefault(value => value.Result.Coordinate == coordinate.Value);
            if (source is not null)
            {
                _selectedResultPage = page;
                _selectedSourcePage = source;
                return;
            }
        }
    }

    private void RefreshCurrentResultTables()
    {
        _currentResultTables = _selectedDerivedResult is not null
            ? _resultPresentationService.BuildTables(_selectedDerivedResult)
            : _resultSet is not null && _resultNavigator.CurrentCoordinate is ResultCoordinate coordinate
                ? _resultPresentationService.BuildTables(_resultSet, coordinate)
                : null;
        RefreshResultTableItems();
    }

    private void SetResultPresentationError(Exception exception)
    {
        string resourceKey = exception switch
        {
            NonStaticDerivedOperandException nonStatic => nonStatic.ResourceKey,
            ResultExportLimitException limit => limit.ResourceKey,
            ResultPresentationLimitException limit => limit.ResourceKey,
            ResultExportOperationException export => export.ResourceKey,
            _ => "ResultPresentationUnavailable",
        };
        string message = exception switch
        {
            NonStaticDerivedOperandException typed => string.Format(
                Localization.Culture,
                Localization[resourceKey],
                typed.DerivedResultId,
                typed.OperandId),
            ResultExportLimitException limit => string.Format(
                Localization.Culture,
                Localization[resourceKey],
                limit.LimitKind,
                limit.Limit,
                limit.Actual),
            ResultPresentationLimitException limit => string.Format(
                Localization.Culture,
                Localization[resourceKey],
                limit.LimitKind,
                limit.Limit,
                limit.Actual),
            _ => Localization[resourceKey],
        };
        ResultPresentationErrorCode code = exception is ResultPresentationException presentation
            ? presentation.Code
            : ResultPresentationErrorCode.InvalidDefinition;
        LastResultPresentationError = new ResultPresentationUiError(
            code,
            resourceKey,
            message,
            exception);
        _resultPresentationError.Text = message;
        _resultPresentationError.Visible = true;
    }

    private void ClearResultPresentationError()
    {
        LastResultPresentationError = null;
        _resultPresentationError.Text = string.Empty;
        _resultPresentationError.Visible = false;
    }

    private void RelocalizeResultPresentationError()
    {
        if (LastResultPresentationError?.Exception is Exception exception)
        {
            SetResultPresentationError(exception);
        }
    }

    private void QueueSceneUpdate(ViewportUpdateReason reason)
    {
        if (_disposed) return;
        _updateScheduler.Schedule(reason);
        if (_renderer.Scene is null)
        {
            _updateScheduler.Flush();
            return;
        }

        if (!_flushPosted && IsHandleCreated && !Disposing && !IsDisposed)
        {
            _flushPosted = true;
            BeginInvoke((Action)FlushPostedUpdate);
        }
    }

    private void FlushPostedUpdate()
    {
        _flushPosted = false;
        if (!_disposed) FlushPendingUpdates();
    }

    private void FlushSceneUpdate(ViewportUpdateReason reason)
    {
        if (_disposed || _document is null || _document.Nodes.Count == 0)
        {
            _renderer.Control.Visible = false;
            _summary.Visible = true;
            return;
        }

        try
        {
            ViewportSceneProjection projection = _projector.Project(
                _sceneStableId, _document, _resultSet, DisplayedResult,
                Math.Max(0, _resultNavigator.PageIndex), Math.Max(1, _resultNavigator.PageCount),
                _presentation,
                new ViewportSceneProjectionText(Localization["ViewportScaleLegend"], Localization["ViewportColorLegend"]),
                _renderer.Scene,
                DisplayedReactionProjection);
            SceneProjectionCount++;
            _lastInvalidatedLayers = projection.ChangedLayers;
            CountInvalidations(projection.ChangedLayers);
            if (projection.ChangedLayers != SceneLayerMask.None)
            {
                SceneEntityKey? priorSelection = _renderer.Selection;
                _renderer.SetScene(projection.Scene, projection.ChangedLayers);
                if (priorSelection != _renderer.Selection)
                {
                    SynchronizeResultGridSelection(_renderer.Selection);
                    SelectionChanged?.Invoke(
                        this,
                        new ViewportSelectionChangedEventArgs(
                            _renderer.Selection,
                            ViewportSelectionOrigin.Programmatic));
                }
            }
            ApplyCameraPolicy();
            if (_resetCameraAfterProjection)
            {
                _renderer.Home();
            }
            _resetCameraAfterProjection = false;
            _renderer.Control.Visible = true;
            _summary.Visible = true;
            EnsureRendererInitialized();
            _renderFailure.Visible = false;
            LastViewportFailure = null;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            _renderer.Control.Visible = false;
            _renderFailure.Visible = true;
            PublishViewportFailure(
                ViewportOperation.SceneUpdate,
                exception,
                isExpected: exception is ArgumentException or OverflowException);
        }
    }

    private void CountInvalidations(SceneLayerMask layers)
    {
        foreach (SceneLayerKind kind in Enum.GetValues<SceneLayerKind>())
            if ((layers & kind.ToMask()) != 0) _layerInvalidationCounts[kind]++;
    }

    private void EnsureRendererInitialized()
    {
        if (!_renderer.Control.IsHandleCreated || _renderer.IsInitialized) return;
        _renderer.Initialize();
        _renderer.Resize(_renderer.Control.ClientSize);
        _renderer.RequestRender();
    }

    private void ApplyCameraPolicy()
    {
        if (_document is null || _document.Nodes.Count == 0) return;
        _renderer.SetCameraPolicy(_document.Dimension == ModelDimension.TwoDimensional
            ? ViewportCameraPolicy.TwoDimensional
            : ViewportCameraPolicy.ThreeDimensional);
        UpdateProjectionCaption();
    }

    private void UpdateProjectionCaption() =>
        _projection.Text = _renderer.Projection == ViewportProjection.Orthographic
            ? Localization["ViewportProjectionOrthographic"]
            : Localization["ViewportProjectionPerspective"];

    private void RefreshResultCoordinateItems()
    {
        ResultCoordinate? selected = _resultNavigator.CurrentCoordinate;
        _updatingControls = true;
        try
        {
            _caseSelector.Items.Clear();
            foreach (AnalysisCase resultCase in _resultNavigator.Cases)
            {
                _caseSelector.Items.Add(new ResultCaseItem(
                    resultCase.CaseId,
                    $"{resultCase.CaseId} · {resultCase.Name}"));
            }

            _caseSelector.SelectedIndex = _resultNavigator.CurrentCaseIndex;

            _stateSelector.Items.Clear();
            if (_resultNavigator.Current is AnalysisResult current)
            {
                foreach (AnalysisResult result in _resultNavigator.GetStates(current.CaseId))
                {
                    _stateSelector.Items.Add(new ResultStateItem(result.Coordinate, DescribeResultState(result)));
                }
            }

            _stateSelector.SelectedIndex = _resultNavigator.CurrentStateIndex;

            _coordinateSelector.Items.Clear();
            if (_resultSet is not null)
                foreach (AnalysisResult result in _resultSet.Results)
                    _coordinateSelector.Items.Add(new ResultCoordinateItem(result.Coordinate, DescribeResult(result)));
            _coordinateSelector.SelectedIndex = -1;
            for (int index = 0; index < _coordinateSelector.Items.Count; index++)
                if (_coordinateSelector.Items[index] is ResultCoordinateItem item && item.Coordinate == selected)
                {
                    _coordinateSelector.SelectedIndex = index;
                    break;
                }

            _derivedSelector.Items.Clear();
            _derivedSelector.Items.Add(new DerivedResultItem(null, Localization["ResultBase"]));
            foreach (PresentedStaticResult derived in _derivedResults)
            {
                _derivedSelector.Items.Add(new DerivedResultItem(
                    derived,
                    $"{derived.Id} · {derived.Name}"));
            }

            _derivedSelector.SelectedIndex = 0;
            if (_selectedDerivedResult is not null)
            {
                for (int index = 1; index < _derivedSelector.Items.Count; index++)
                {
                    if (_derivedSelector.Items[index] is DerivedResultItem item &&
                        string.Equals(item.Result?.Id, _selectedDerivedResult.Id, StringComparison.Ordinal))
                    {
                        _derivedSelector.SelectedIndex = index;
                        break;
                    }
                }
            }

            _parentSelector.Items.Clear();
            foreach (ResultPresentationPage page in _resultPages)
            {
                _parentSelector.Items.Add(new ResultParentItem(page, DescribeResultParent(page)));
            }

            _parentSelector.SelectedIndex = FindParentSelectorIndex(_selectedResultPage);

            _childSelector.Items.Clear();
            if (_selectedResultPage is not null)
            {
                foreach (ResultPresentationSourcePage source in _selectedResultPage.SourcePages)
                {
                    _childSelector.Items.Add(new ResultChildItem(source, DescribeResult(source.Result)));
                }
            }

            _childSelector.SelectedIndex = FindChildSelectorIndex(_selectedSourcePage);
        }
        finally
        {
            _updatingControls = false;
        }
        bool hasResults = _resultNavigator.PageCount > 0;
        _caseSelector.Enabled = hasResults;
        _stateSelector.Enabled = hasResults;
        _coordinateSelector.Enabled = hasResults;
        _derivedSelector.Enabled = _derivedResults.Count > 0;
        _parentSelector.Enabled = _resultPages.Count > 0;
        _childSelector.Enabled = _selectedResultPage?.SourcePages.Count > 1;
        _extremaSelector.Enabled = _currentResultTables is not null;
        _resultCsv.Enabled = CanExportSelectedResultCsv;
        _pickupCsv.Enabled = CanExportSelectedPickupCsv;
        _previousResult.Enabled = _resultNavigator.CanMovePrevious;
        _nextResult.Enabled = _resultNavigator.CanMoveNext;
        _page.Text = string.Format(
            Localization.Culture, Localization["ResultPage"],
            _resultNavigator.PageCount == 0 ? 0 : _resultNavigator.PageIndex + 1,
            _resultNavigator.PageCount);
        PublishResultPageChanged();
    }

    private void PublishResultPageChanged()
    {
        int pageCount = _resultNavigator.PageCount;
        int pageIndex = pageCount == 0 ? 0 : _resultNavigator.PageIndex;
        if (_publishedResultPageIndex == pageIndex && _publishedResultPageCount == pageCount)
        {
            return;
        }

        _publishedResultPageIndex = pageIndex;
        _publishedResultPageCount = pageCount;
        ResultPageChanged?.Invoke(this, new ResultPageChangedEventArgs(pageIndex, pageCount));
    }

    private string DescribeResult(AnalysisResult result)
    {
        return $"{result.CaseId} · {DescribeResultState(result)}";
    }

    private string DescribeResultParent(ResultPresentationPage page)
    {
        if (!page.IsMovingLoad)
        {
            return DescribeResult(page.PrimaryResult);
        }

        string name = _document?.MovingLoads
            .FirstOrDefault(value => string.Equals(value.Id, page.PageId, StringComparison.Ordinal))?.Name
            ?? page.PageId;
        return $"{page.PageId} · {name}";
    }

    private int FindParentSelectorIndex(ResultPresentationPage? selected)
    {
        if (selected is null)
        {
            return -1;
        }

        for (int index = 0; index < _resultPages.Count; index++)
        {
            if (string.Equals(_resultPages[index].PageId, selected.PageId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindChildSelectorIndex(ResultPresentationSourcePage? selected)
    {
        if (selected is null || _selectedResultPage is null)
        {
            return -1;
        }

        for (int index = 0; index < _selectedResultPage.SourcePages.Count; index++)
        {
            if (_selectedResultPage.SourcePages[index].Result.Coordinate == selected.Result.Coordinate)
            {
                return index;
            }
        }

        return -1;
    }

    private string DescribeResultState(AnalysisResult result)
    {
        string state = result.State.Kind switch
        {
            ResultStateKind.Static => Localization["ResultStateStatic"],
            ResultStateKind.LoadStep => Localization["ResultStateLoadStep"],
            ResultStateKind.Mode => Localization["ResultStateMode"],
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
        return result.State.Kind == ResultStateKind.Static
            ? state
            : $"{state} {result.State.Index + 1}";
    }

    private void RefreshResultTableItems()
    {
        int selected = Math.Max(0, _resultTableSelector.SelectedIndex);
        bool wasUpdating = _updatingControls;
        _updatingControls = true;
        try
        {
            _resultTableSelector.Items.Clear();
            _resultTableSelector.Items.Add(Localization["ResultDisplacements"]);
            if (_currentResultTables is { IsModal: false })
            {
                _resultTableSelector.Items.Add(Localization["ResultReactions"]);
                _resultTableSelector.Items.Add(Localization["ResultMemberForces"]);
            }

            if (SelectedMovingLoadEnvelope is not null)
            {
                _resultTableSelector.Items.Add(Localization["ResultMovingEnvelope"]);
            }

            _resultTableSelector.Enabled = _currentResultTables is not null;
            _resultTableSelector.SelectedIndex = _resultTableSelector.Items.Count == 0
                ? -1
                : Math.Min(selected, _resultTableSelector.Items.Count - 1);
        }
        finally
        {
            _updatingControls = wasUpdating;
        }
    }

    private void RebuildResultTable()
    {
        ResultTableTruncated = false;
        _updatingResultSelection = true;
        try
        {
            _resultGrid.Columns.Clear();
            _resultGrid.Rows.Clear();
            int table = Math.Max(0, _resultTableSelector.SelectedIndex);
            if (table == 0)
            {
                AddColumns("EditorNode", "EditorX", "EditorY", "EditorZ", "EditorRx", "EditorRy", "EditorRz");
                foreach (NodeDisplacement row in ViewportResultExtrema.SelectDisplacements(
                             _currentResultTables,
                             _presentation.ExtremaMode))
                {
                    if (!TryReserveResultRows(1)) break;
                    AddTaggedRow(
                        new SceneEntityKey(SceneEntityKind.Displacement, row.NodeId), row.NodeId,
                        Format(row.Components.Dx), Format(row.Components.Dy), Format(row.Components.Dz),
                        Format(row.Components.Rx), Format(row.Components.Ry), Format(row.Components.Rz));
                }
            }
            else if (table == 1)
            {
                AddColumns("EditorNode", "EditorFx", "EditorFy", "EditorFz", "EditorMx", "EditorMy", "EditorMz");
                foreach (SupportReaction row in ViewportResultExtrema.SelectReactions(
                             _currentResultTables,
                             _presentation.ExtremaMode))
                {
                    if (!TryReserveResultRows(1)) break;
                    AddTaggedRow(
                        new SceneEntityKey(SceneEntityKind.Reaction, row.NodeId), row.NodeId,
                        Format(row.Components.Fx), Format(row.Components.Fy), Format(row.Components.Fz),
                        Format(row.Components.Mx), Format(row.Components.My), Format(row.Components.Mz));
                }
            }
            else if (table == 2)
            {
                AddColumns(
                    "EditorMember", "ResultStation", "ResultEnd", "EditorFx", "EditorFy",
                    "EditorFz", "EditorMx", "EditorMy", "EditorMz");
                IReadOnlyList<ViewportSectionForceValue> values = ViewportResultExtrema.SelectSectionForces(
                    _currentResultTables,
                    _presentation.ExtremaMode);
                if (_presentation.ExtremaMode == SceneExtremaMode.Values)
                {
                    for (int index = 0; index < values.Count; index += 2)
                    {
                        int pairCount = Math.Min(2, values.Count - index);
                        if (!TryReserveResultRows(pairCount))
                        {
                            break;
                        }

                        for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
                        {
                            AddMemberForceRow(values[index + pairIndex]);
                        }
                    }
                }
                else if (values.Count == 1 && TryReserveResultRows(1))
                {
                    AddMemberForceRow(values[0]);
                }
            }
            else if (SelectedMovingLoadEnvelope is MovingLoadEnvelope envelope)
            {
                AddMovingLoadEnvelopeRows(envelope);
            }
            if (ResultTableTruncated) _resultGrid.Rows.Add(Localization["ResultTruncated"]);
        }
        finally
        {
            _updatingResultSelection = false;
        }
        SynchronizeResultGridSelection(_renderer.Selection);
    }

    private void AddMemberForceRow(ViewportSectionForceValue value) =>
        AddTaggedRow(
            new SceneEntityKey(SceneEntityKind.SectionForce, value.Id),
            value.MemberId, value.StationId, value.EndLabel,
            Format(value.Components.Fx), Format(value.Components.Fy), Format(value.Components.Fz),
            Format(value.Components.Mx), Format(value.Components.My), Format(value.Components.Mz));

    private void AddMovingLoadEnvelopeRows(MovingLoadEnvelope envelope)
    {
        AddColumns(
            "ResultCategory", "ResultEntity", "ResultLocation", "ResultComponent",
            "ResultMaximumValue", "ResultMaximumCase", "ResultMinimumValue", "ResultMinimumCase",
            "ResultAbsoluteMaximumValue", "ResultAbsoluteMaximumCase");
        foreach (NodeDisplacementEnvelope node in envelope.NodeDisplacements)
        {
            foreach ((string component, ScalarEnvelope value) in DisplacementEnvelopes(node.Components))
            {
                if (!AddEnvelopeRow(Localization["ResultDisplacements"], node.NodeId, string.Empty, component, value))
                {
                    return;
                }
            }
        }

        foreach (SupportReactionEnvelope reaction in envelope.SupportReactions)
        {
            SupportReactionAbsoluteMaximum? absolute = envelope.AbsoluteSupportReactions
                .FirstOrDefault(value => string.Equals(value.NodeId, reaction.NodeId, StringComparison.Ordinal));
            foreach ((string component, ScalarEnvelope value) in ForceEnvelopes(reaction.Components))
            {
                EnvelopeExtreme? absoluteMaximum = absolute is null
                    ? null
                    : AbsoluteReactionExtreme(absolute, component);
                if (!AddEnvelopeRow(
                    Localization["ResultReactions"],
                    reaction.NodeId,
                    string.Empty,
                    component,
                    value,
                    absoluteMaximum))
                {
                    return;
                }
            }
        }

        foreach (MemberSectionForceEnvelope member in envelope.MemberSectionForces)
        {
            foreach (MemberSegmentEnvelope segment in member.Segments)
            {
                foreach ((string component, ScalarEnvelope value) in ForceEnvelopes(segment.IEnd))
                {
                    if (!AddEnvelopeRow(
                        Localization["ResultMemberForces"], member.MemberId, $"{segment.SegmentId}/I", component, value))
                    {
                        return;
                    }
                }

                foreach ((string component, ScalarEnvelope value) in ForceEnvelopes(segment.JEnd))
                {
                    if (!AddEnvelopeRow(
                        Localization["ResultMemberForces"], member.MemberId, $"{segment.SegmentId}/J", component, value))
                    {
                        return;
                    }
                }
            }
        }

        if (envelope.MemberForceExtrema is { HasValues: true } extrema)
        {
            foreach ((string component, MemberForceScalarExtrema value) in MemberForceExtrema(extrema))
            {
                if (!value.HasValue)
                {
                    continue;
                }

                if (!TryReserveResultRows(1))
                {
                    return;
                }

                _resultGrid.Rows.Add(
                    Localization["ResultMemberForceExtrema"],
                    value.AbsoluteMaximum.MemberId,
                    $"{value.AbsoluteMaximum.SegmentId}/{value.AbsoluteMaximum.End}",
                    component,
                    Format(value.Maximum.Value), value.Maximum.CaseId,
                    Format(value.Minimum.Value), value.Minimum.CaseId,
                    Format(value.AbsoluteMaximum.Value), value.AbsoluteMaximum.CaseId);
            }
        }
    }

    private bool AddEnvelopeRow(
        string category,
        string entity,
        string location,
        string component,
        ScalarEnvelope value,
        EnvelopeExtreme? absoluteMaximum = null)
    {
        if (!TryReserveResultRows(1))
        {
            return false;
        }

        _resultGrid.Rows.Add(
            category, entity, location, component,
            Format(value.Maximum.Value), value.Maximum.CaseId,
            Format(value.Minimum.Value), value.Minimum.CaseId,
            Format((absoluteMaximum ?? value.AbsoluteMaximum).Value),
            (absoluteMaximum ?? value.AbsoluteMaximum).CaseId);
        return true;
    }

    private static EnvelopeExtreme AbsoluteReactionExtreme(
        SupportReactionAbsoluteMaximum reaction,
        string component) => component switch
        {
            "Fx" => new EnvelopeExtreme(reaction.Components.Fx, reaction.SourceCaseIds.Fx),
            "Fy" => new EnvelopeExtreme(reaction.Components.Fy, reaction.SourceCaseIds.Fy),
            "Fz" => new EnvelopeExtreme(reaction.Components.Fz, reaction.SourceCaseIds.Fz),
            "Mx" => new EnvelopeExtreme(reaction.Components.Mx, reaction.SourceCaseIds.Mx),
            "My" => new EnvelopeExtreme(reaction.Components.My, reaction.SourceCaseIds.My),
            "Mz" => new EnvelopeExtreme(reaction.Components.Mz, reaction.SourceCaseIds.Mz),
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };

    private static IEnumerable<(string Component, ScalarEnvelope Value)> DisplacementEnvelopes(
        DisplacementEnvelopeComponents values)
    {
        yield return ("Dx", values.Dx);
        yield return ("Dy", values.Dy);
        yield return ("Dz", values.Dz);
        yield return ("Rx", values.Rx);
        yield return ("Ry", values.Ry);
        yield return ("Rz", values.Rz);
    }

    private static IEnumerable<(string Component, ScalarEnvelope Value)> ForceEnvelopes(
        ForceEnvelopeComponents values)
    {
        yield return ("Fx", values.Fx);
        yield return ("Fy", values.Fy);
        yield return ("Fz", values.Fz);
        yield return ("Mx", values.Mx);
        yield return ("My", values.My);
        yield return ("Mz", values.Mz);
    }

    private static IEnumerable<(string Component, MemberForceScalarExtrema Value)> MemberForceExtrema(
        MemberForceExtremaComponents values)
    {
        yield return ("Fx", values.Fx);
        yield return ("Fy", values.Fy);
        yield return ("Fz", values.Fz);
        yield return ("Mx", values.Mx);
        yield return ("My", values.My);
        yield return ("Mz", values.Mz);
    }

    private void AddTaggedRow(SceneEntityKey key, params object?[] values)
    {
        int index = _resultGrid.Rows.Add(values);
        _resultGrid.Rows[index].Tag = key;
    }

    private bool TryReserveResultRows(int rowCount)
    {
        if (rowCount <= 0 || rowCount >= MaximumResultTableRows)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount));
        }

        if (_resultGrid.Rows.Count <= (MaximumResultTableRows - 1) - rowCount)
        {
            return true;
        }

        ResultTableTruncated = true;
        return false;
    }

    private void AddColumns(params string[] resourceKeys)
    {
        foreach (string key in resourceKeys) _resultGrid.Columns.Add(key, Localization[key]);
    }

    private void SynchronizeResultGridSelection(SceneEntityKey? selection)
    {
        if (_updatingResultSelection) return;
        _updatingResultSelection = true;
        try
        {
            _resultGrid.ClearSelection();
            if (selection is not SceneEntityKey key) return;
            DataGridViewRow? row = _resultGrid.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(candidate => candidate.Tag is SceneEntityKey candidateKey && candidateKey == key);
            if (row is not null)
            {
                row.Selected = true;
                _resultGrid.CurrentCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault();
            }
        }
        finally
        {
            _updatingResultSelection = false;
        }
    }

    private static string Format(double value) => value.ToString("G6", CultureInfo.InvariantCulture);
    private void OnProjectionClick(object? sender, EventArgs eventArgs) => ToggleProjection();
    private void OnFitClick(object? sender, EventArgs eventArgs) => Fit();
    private void OnHomeClick(object? sender, EventArgs eventArgs) => Home();

    private void OnPngClick(object? sender, EventArgs eventArgs)
    {
        using SaveFileDialog dialog = new() { AddExtension = true, DefaultExt = "png", Filter = Localization["PngFileFilter"] };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            SaveViewportPng(dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            PublishViewportFailure(
                ViewportOperation.PngCapture,
                exception,
                isExpected: exception is IOException or UnauthorizedAccessException or ArgumentException);
        }
    }

    private void OnResultCsvClick(object? sender, EventArgs eventArgs) =>
        ExportResultWithDialog(ExportSelectedResultCsv);

    private void OnPickupCsvClick(object? sender, EventArgs eventArgs) =>
        ExportResultWithDialog(ExportSelectedPickupCsv);

    private void ExportResultWithDialog(Func<ResultCsvExportLimits?, ResultExportArtifact> createExport)
    {
        try
        {
            ResultExportArtifact export = createExport(null);
            bool isPickup2D = string.Equals(
                Path.GetExtension(export.SuggestedFileName),
                ".pik",
                StringComparison.OrdinalIgnoreCase);
            using SaveFileDialog dialog = new()
            {
                AddExtension = true,
                DefaultExt = isPickup2D ? "pik" : "csv",
                FileName = export.SuggestedFileName,
                Filter = Localization[isPickup2D ? "ResultPickupPikFileFilter" : "ResultCsvFileFilter"],
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            SaveResultExport(dialog.FileName, export);
        }
        catch (ResultPresentationException exception)
        {
            SetResultPresentationError(exception);
        }
        catch (ResultExportOperationException exception)
        {
            SetResultPresentationError(exception);
        }
    }

    private static void SaveResultExport(string path, ResultExportArtifact export)
    {
        try
        {
            AtomicResultExportWriter.Write(path, export);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            throw new ResultExportOperationException(exception);
        }
    }

    private void OnDecorationChanged(object? sender, EventArgs eventArgs)
    {
        if (!_updatingControls) SetDecorations(_grid.Checked, _axes.Checked, _labels.Checked, _legends.Checked);
    }

    private void OnLayerChanged(object? sender, EventArgs eventArgs)
    {
        if (_updatingControls || _layerSelector.SelectedIndex < 0) return;
        ViewportDisplayMode mode = (ViewportDisplayMode)_layerSelector.SelectedIndex;
        if (_presentation.DisplayMode != mode)
        {
            _presentation = _presentation with { DisplayMode = mode };
            QueueSceneUpdate(ViewportUpdateReason.Presentation);
        }
    }

    private void OnCoordinateChanged(object? sender, EventArgs eventArgs)
    {
        if (!_updatingControls && _coordinateSelector.SelectedItem is ResultCoordinateItem item)
            SetResultCoordinate(item.Coordinate);
    }

    private void OnCaseChanged(object? sender, EventArgs eventArgs)
    {
        if (_updatingControls || _caseSelector.SelectedItem is not ResultCaseItem item)
        {
            return;
        }

        if (_resultNavigator.SelectCase(item.CaseId))
        {
            SelectBaseResult();
            SynchronizePresentationPageFromCoordinate();
            RefreshCurrentResultTables();
            RefreshResultCoordinateItems();
            RebuildResultTable();
            QueueSceneUpdate(ViewportUpdateReason.Result);
        }
    }

    private void OnStateChanged(object? sender, EventArgs eventArgs)
    {
        if (_updatingControls || _stateSelector.SelectedIndex < 0)
        {
            return;
        }

        if (_resultNavigator.SelectState(_stateSelector.SelectedIndex))
        {
            SelectBaseResult();
            SynchronizePresentationPageFromCoordinate();
            RefreshCurrentResultTables();
            RefreshResultCoordinateItems();
            RebuildResultTable();
            QueueSceneUpdate(ViewportUpdateReason.Result);
        }
    }

    private void OnPreviousResultClick(object? sender, EventArgs eventArgs) => MoveToPreviousResult();
    private void OnNextResultClick(object? sender, EventArgs eventArgs) => MoveToNextResult();

    private void OnDerivedChanged(object? sender, EventArgs eventArgs)
    {
        if (_updatingControls || _derivedSelector.SelectedItem is not DerivedResultItem item)
        {
            return;
        }

        _selectedDerivedResult = item.Result;
        RefreshCurrentResultTables();
        RefreshResultCoordinateItems();
        RebuildResultTable();
        QueueSceneUpdate(ViewportUpdateReason.Result);
    }

    private void OnParentChanged(object? sender, EventArgs eventArgs)
    {
        if (_updatingControls || _parentSelector.SelectedItem is not ResultParentItem item)
        {
            return;
        }

        SelectPresentationResult(
            item.Page,
            item.Page.SourcePages.FirstOrDefault(source => source.IsParent) ?? item.Page.SourcePages[0]);
    }

    private void OnChildChanged(object? sender, EventArgs eventArgs)
    {
        if (_updatingControls ||
            _selectedResultPage is null ||
            _childSelector.SelectedItem is not ResultChildItem item)
        {
            return;
        }

        SelectPresentationResult(_selectedResultPage, item.Source);
    }

    private void SelectPresentationResult(
        ResultPresentationPage page,
        ResultPresentationSourcePage source)
    {
        SelectBaseResult();
        _resultNavigator.Select(source.Result.Coordinate);
        _selectedResultPage = page;
        _selectedSourcePage = source;
        RefreshCurrentResultTables();
        RefreshResultCoordinateItems();
        RebuildResultTable();
        QueueSceneUpdate(ViewportUpdateReason.Result);
    }

    private void OnExtremaChanged(object? sender, EventArgs eventArgs)
    {
        if (_updatingControls || _extremaSelector.SelectedIndex < 0) return;
        SceneExtremaMode extrema = (SceneExtremaMode)_extremaSelector.SelectedIndex;
        if (_presentation.ExtremaMode != extrema)
        {
            _presentation = _presentation with { ExtremaMode = extrema };
            ReconcileResultSelection(extrema);
            RebuildResultTable();
            QueueSceneUpdate(ViewportUpdateReason.Presentation);
        }
    }

    private void ReconcileResultSelection(SceneExtremaMode extrema)
    {
        if (_renderer.Selection is not SceneEntityKey selection)
        {
            return;
        }

        bool retained = selection.Kind switch
        {
            SceneEntityKind.Displacement => ViewportResultExtrema
                .SelectDisplacements(_currentResultTables, extrema)
                .Any(value => string.Equals(value.NodeId, selection.Id, StringComparison.Ordinal)),
            SceneEntityKind.Reaction => ViewportResultExtrema
                .SelectReactions(_currentResultTables, extrema)
                .Any(value => string.Equals(value.NodeId, selection.Id, StringComparison.Ordinal)),
            SceneEntityKind.SectionForce => ViewportResultExtrema
                .SelectSectionForces(_currentResultTables, extrema)
                .Any(value => string.Equals(value.Id, selection.Id, StringComparison.Ordinal)),
            _ => true,
        };
        if (!retained)
        {
            _renderer.SetSelection(null, ViewportSelectionOrigin.Programmatic);
        }
    }

    private void PublishViewportFailure(
        ViewportOperation operation,
        Exception exception,
        bool isExpected)
    {
        ViewportOperationException failure = new(
            operation,
            Localization["ViewportUnavailable"],
            exception,
            isExpected);
        LastViewportFailure = failure;
        ViewportOperationFailed?.Invoke(this, new ViewportOperationFailedEventArgs(failure));
        ViewportFailed?.Invoke(this, new EditorValidationEventArgs(failure.SafeMessage));
    }

    private void OnResultTableChanged(object? sender, EventArgs eventArgs)
    {
        if (!_updatingControls)
        {
            RebuildResultTable();
        }
    }

    private void OnResultGridSelectionChanged(object? sender, EventArgs eventArgs)
    {
        if (!_updatingResultSelection && _resultGrid.CurrentRow?.Tag is SceneEntityKey key)
            SetSelection(key, ViewportSelectionOrigin.Table);
    }

    private void OnRendererHandleCreated(object? sender, EventArgs eventArgs)
    {
        QueueSceneUpdate(ViewportUpdateReason.Document);
        FlushPendingUpdates();
    }

    private void OnRendererMouseClick(object? sender, MouseEventArgs eventArgs) => _renderer.SelectAt(eventArgs.Location);

    private void OnRendererMouseMove(object? sender, MouseEventArgs eventArgs) => _renderer.HoverAt(eventArgs.Location);

    private void OnRendererMouseLeave(object? sender, EventArgs eventArgs) => _renderer.SetHover(null);

    private void OnRendererSelectionChanged(object? sender, ViewportSelectionChangedEventArgs eventArgs)
    {
        SynchronizeResultGridSelection(eventArgs.Selection);
        SelectionChanged?.Invoke(this, eventArgs);
    }

    private void OnRendererHoverChanged(object? sender, ViewportHoverChangedEventArgs eventArgs) =>
        HoverChanged?.Invoke(this, eventArgs);

    private void OnLayoutSizeChanged(object? sender, EventArgs eventArgs)
    {
        if (_layout.Height < 240) return;
        int distance = Math.Clamp((int)(_layout.Height * 0.68), 100, _layout.Height - 100);
        if (_layout.SplitterDistance != distance) _layout.SplitterDistance = distance;
    }

    private sealed class ResultCoordinateItem(ResultCoordinate coordinate, string display)
    {
        public ResultCoordinate Coordinate { get; } = coordinate;
        public override string ToString() => display;
    }

    private sealed class ResultCaseItem(string caseId, string display)
    {
        public string CaseId { get; } = caseId;
        public override string ToString() => display;
    }

    private sealed class ResultStateItem(ResultCoordinate coordinate, string display)
    {
        public ResultCoordinate Coordinate { get; } = coordinate;
        public override string ToString() => display;
    }

    private sealed class DerivedResultItem(PresentedStaticResult? result, string display)
    {
        public PresentedStaticResult? Result { get; } = result;
        public override string ToString() => display;
    }

    private sealed class ResultParentItem(ResultPresentationPage page, string display)
    {
        public ResultPresentationPage Page { get; } = page;
        public override string ToString() => display;
    }

    private sealed class ResultChildItem(ResultPresentationSourcePage source, string display)
    {
        public ResultPresentationSourcePage Source { get; } = source;
        public override string ToString() => display;
    }

    private sealed record ResultPresentationCandidate(
        IReadOnlyList<PresentedStaticResult> DerivedResults,
        IReadOnlyList<ResultPresentationPage> Pages,
        IReadOnlyDictionary<string, MovingLoadEnvelope> MovingLoadEnvelopes,
        ResultPresentationException? Error)
    {
        public static ResultPresentationCandidate Empty { get; } = new(
            [],
            [],
            new ReadOnlyDictionary<string, MovingLoadEnvelope>(
                new Dictionary<string, MovingLoadEnvelope>()),
            null);
    }
}
