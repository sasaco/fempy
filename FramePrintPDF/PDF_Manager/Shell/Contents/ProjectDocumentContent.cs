using System.Collections.ObjectModel;
using System.Globalization;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Rendering;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Viewport;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.Shell.Contents;

public sealed class ProjectDocumentContent : ShellDockContent
{
    public const int MaximumPngDimension = 8_192;
    public const int MaximumPngEncodedBytes = 32 * 1024 * 1024;
    public const int MaximumResultTableRows = 10_000;

    private readonly string _sceneStableId;
    private readonly OpenGlViewportLifecycle _renderer = new();
    private readonly ProjectDocumentSceneProjector _projector = new();
    private readonly ViewportResultNavigator _resultNavigator = new();
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
    private readonly ToolStripButton _previousResult = new() { Name = "PreviousResultButton" };
    private readonly ToolStripButton _nextResult = new() { Name = "NextResultButton" };
    private readonly ToolStripLabel _page = new() { Name = "ResultPageLabel" };
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

    private ProjectDocument? _document;
    private AnalysisResultSet? _resultSet;
    private ViewportPresentationState _presentation = ViewportPresentationState.Default;
    private SceneLayerMask _lastInvalidatedLayers;
    private bool _updatingControls;
    private bool _updatingResultSelection;
    private bool _flushPosted;
    private bool _resetCameraAfterProjection;
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
            _projection, _fit, _home, _png, new ToolStripSeparator(),
            _grid, _axes, _labels, _legends, new ToolStripSeparator(),
            _layerLabel, _layerSelector, new ToolStripSeparator(),
            _coordinateLabel, _coordinateSelector, _previousResult, _page, _nextResult,
            _extremaLabel, _extremaSelector,
        ]);
        _viewport.Controls.Add(_renderer.Control);
        _viewport.Controls.Add(_renderFailure);
        _viewport.Controls.Add(_summary);
        _layout.Panel1.Controls.Add(_viewport);
        _layout.Panel1.Controls.Add(_toolbar);
        _layout.Panel2.Controls.Add(_resultGrid);
        _layout.Panel2.Controls.Add(_resultTableSelector);
        Controls.Add(_layout);

        _projection.Click += OnProjectionClick;
        _fit.Click += OnFitClick;
        _home.Click += OnHomeClick;
        _png.Click += OnPngClick;
        _grid.CheckedChanged += OnDecorationChanged;
        _axes.CheckedChanged += OnDecorationChanged;
        _labels.CheckedChanged += OnDecorationChanged;
        _legends.CheckedChanged += OnDecorationChanged;
        _layerSelector.SelectedIndexChanged += OnLayerChanged;
        _coordinateSelector.SelectedIndexChanged += OnCoordinateChanged;
        _previousResult.Click += OnPreviousResultClick;
        _nextResult.Click += OnNextResultClick;
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
    public ProjectDocument? Document => _document;
    public AnalysisResultSet? ResultSet => _resultSet;
    public Panel ViewportHost => _viewport;
    public DataGridView ResultGrid => _resultGrid;
    public ComboBox ResultTableSelector => _resultTableSelector;
    public ToolStripComboBox ResultLayerSelector => _layerSelector;
    public ToolStripComboBox ResultCoordinateSelector => _coordinateSelector;
    public ToolStripComboBox ResultExtremaSelector => _extremaSelector;
    public ViewportProjection Projection => _renderer.Projection;
    public ViewportCameraState Camera => _renderer.Camera;
    public ViewportCameraPolicy CameraPolicy => _renderer.CameraPolicy;
    public SceneEntityKey? Selection => _renderer.Selection;
    public SceneEntityKey? Hover => _renderer.Hover;
    public SceneLayerMask EffectiveVisibleLayers =>
        _renderer.VisibleLayers & (_renderer.Scene?.VisibleLayers ?? SceneLayerMask.None);
    public ViewportPresentationState Presentation => _presentation;
    public ResultCoordinate? SelectedResultCoordinate => _resultNavigator.CurrentCoordinate;
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

    public void SetDocument(ProjectDocument? document) => SetDocument(document, resetCamera: false);

    public void SetDocument(ProjectDocument? document, bool resetCamera)
    {
        _document = document;
        _resetCameraAfterProjection |= resetCamera;
        ApplyLocalization();
        QueueSceneUpdate(ViewportUpdateReason.Document);
    }

    public void SetResult(AnalysisResultSet? resultSet)
    {
        _resultSet = resultSet;
        _resultNavigator.SetResultSet(resultSet);
        RefreshResultCoordinateItems();
        RebuildResultTable();
        QueueSceneUpdate(ViewportUpdateReason.Result);
    }

    public void SetResultCoordinate(ResultCoordinate coordinate)
    {
        if (_resultNavigator.Select(coordinate))
        {
            RefreshResultCoordinateItems();
            RebuildResultTable();
            QueueSceneUpdate(ViewportUpdateReason.Result);
        }
    }

    public bool MoveToPreviousResult() => MoveResult(_resultNavigator.MovePrevious);
    public bool MoveToNextResult() => MoveResult(_resultNavigator.MoveNext);

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
        _grid.Text = Localization["ViewportGrid"];
        _axes.Text = Localization["ViewportAxes"];
        _labels.Text = Localization["ViewportLabels"];
        _legends.Text = Localization["ViewportLegends"];
        _layerLabel.Text = Localization["ViewportLayer"];
        _coordinateLabel.Text = Localization["ResultCoordinate"];
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

            int table = _resultTableSelector.SelectedIndex;
            _resultTableSelector.Items.Clear();
            _resultTableSelector.Items.AddRange(
                [Localization["ResultDisplacements"], Localization["ResultReactions"], Localization["ResultMemberForces"]]);
            _resultTableSelector.SelectedIndex = table is >= 0 and <= 2 ? table : 0;
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
        RefreshResultCoordinateItems();
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
            _grid.CheckedChanged -= OnDecorationChanged;
            _axes.CheckedChanged -= OnDecorationChanged;
            _labels.CheckedChanged -= OnDecorationChanged;
            _legends.CheckedChanged -= OnDecorationChanged;
            _layerSelector.SelectedIndexChanged -= OnLayerChanged;
            _coordinateSelector.SelectedIndexChanged -= OnCoordinateChanged;
            _previousResult.Click -= OnPreviousResultClick;
            _nextResult.Click -= OnNextResultClick;
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
        }
        base.Dispose(disposing);
    }

    private bool MoveResult(Func<bool> move)
    {
        bool changed = move();
        if (changed)
        {
            RefreshResultCoordinateItems();
            RebuildResultTable();
            QueueSceneUpdate(ViewportUpdateReason.Result);
        }
        return changed;
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
                _sceneStableId, _document, _resultSet, _resultNavigator.Current,
                Math.Max(0, _resultNavigator.PageIndex), Math.Max(1, _resultNavigator.PageCount),
                _presentation,
                new ViewportSceneProjectionText(Localization["ViewportScaleLegend"], Localization["ViewportColorLegend"]),
                _renderer.Scene);
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
        }
        finally
        {
            _updatingControls = false;
        }
        _previousResult.Enabled = _resultNavigator.CanMovePrevious;
        _nextResult.Enabled = _resultNavigator.CanMoveNext;
        _page.Text = string.Format(
            Localization.Culture, Localization["ResultPage"],
            _resultNavigator.PageCount == 0 ? 0 : _resultNavigator.PageIndex + 1,
            _resultNavigator.PageCount);
    }

    private string DescribeResult(AnalysisResult result)
    {
        string state = result.State.Kind switch
        {
            ResultStateKind.Static => Localization["ResultStateStatic"],
            ResultStateKind.LoadStep => Localization["ResultStateLoadStep"],
            ResultStateKind.Mode => Localization["ResultStateMode"],
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
        return result.State.Kind == ResultStateKind.Static
            ? $"{result.CaseId} · {state}"
            : $"{result.CaseId} · {state} {result.State.Index + 1}";
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
                             _resultNavigator.Current,
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
                             _resultNavigator.Current,
                             _presentation.ExtremaMode))
                {
                    if (!TryReserveResultRows(1)) break;
                    AddTaggedRow(
                        new SceneEntityKey(SceneEntityKind.Reaction, row.NodeId), row.NodeId,
                        Format(row.Components.Fx), Format(row.Components.Fy), Format(row.Components.Fz),
                        Format(row.Components.Mx), Format(row.Components.My), Format(row.Components.Mz));
                }
            }
            else
            {
                AddColumns(
                    "EditorMember", "ResultStation", "ResultEnd", "EditorFx", "EditorFy",
                    "EditorFz", "EditorMx", "EditorMy", "EditorMz");
                IReadOnlyList<ViewportSectionForceValue> values = ViewportResultExtrema.SelectSectionForces(
                    _resultNavigator.Current,
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

    private void OnPreviousResultClick(object? sender, EventArgs eventArgs) => MoveToPreviousResult();
    private void OnNextResultClick(object? sender, EventArgs eventArgs) => MoveToNextResult();

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
                .SelectDisplacements(_resultNavigator.Current, extrema)
                .Any(value => string.Equals(value.NodeId, selection.Id, StringComparison.Ordinal)),
            SceneEntityKind.Reaction => ViewportResultExtrema
                .SelectReactions(_resultNavigator.Current, extrema)
                .Any(value => string.Equals(value.NodeId, selection.Id, StringComparison.Ordinal)),
            SceneEntityKind.SectionForce => ViewportResultExtrema
                .SelectSectionForces(_resultNavigator.Current, extrema)
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

    private void OnResultTableChanged(object? sender, EventArgs eventArgs) => RebuildResultTable();

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
}
