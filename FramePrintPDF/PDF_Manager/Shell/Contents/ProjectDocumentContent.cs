using System.Globalization;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Rendering;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.Shell.Contents;

public sealed class ProjectDocumentContent : ShellDockContent
{
    private const int ModelLayerIndex = 0;
    private const int DisplacementLayerIndex = 1;
    private const int MaximumMemberForceRows = 10_000;
    private readonly OpenGlViewportLifecycle _renderer = new();
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
    private readonly ToolStripLabel _layerLabel = new() { Name = "LayerLabel" };
    private readonly ToolStripComboBox _layerSelector = new()
    {
        AutoSize = false,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = "ResultLayerSelector",
        Width = 150,
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
    private ForceAnalysisResult? _selectedResult;
    private bool _disposed;

    public ProjectDocumentContent(DocumentKey contentKey, LocalizationService localization)
        : base(contentKey, localization)
    {
        DockAreas = DockAreas.Document | DockAreas.Float;
        ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.Document;
        _toolbar.Items.AddRange([_projection, _fit, _home, new ToolStripSeparator(), _layerLabel, _layerSelector]);
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
        _layerSelector.SelectedIndexChanged += OnLayerChanged;
        _resultTableSelector.SelectedIndexChanged += OnResultTableChanged;
        _renderer.Control.HandleCreated += OnRendererHandleCreated;
        _renderer.Control.MouseClick += OnRendererMouseClick;
        _renderer.SelectionChanged += OnRendererSelectionChanged;
        _layout.SizeChanged += OnLayoutSizeChanged;
        ApplyLocalization();
    }

    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<EditorValidationEventArgs>? ViewportFailed;

    public ProjectDocument? Document => _document;

    public AnalysisResultSet? ResultSet => _resultSet;

    public Panel ViewportHost => _viewport;

    public DataGridView ResultGrid => _resultGrid;

    public ComboBox ResultTableSelector => _resultTableSelector;

    public ToolStripComboBox ResultLayerSelector => _layerSelector;

    public ViewportProjection Projection => _renderer.Projection;

    public SceneEntityKey? Selection => _renderer.Selection;

    public ViewportSceneModel? CurrentScene => _renderer.Scene;

    public bool ResultTableTruncated { get; private set; }

    public void SetDocument(ProjectDocument? document)
    {
        _document = document;
        ApplyLocalization();
        UpdateScene();
    }

    public void SetResult(AnalysisResultSet? resultSet)
    {
        _resultSet = resultSet;
        _selectedResult = resultSet?.Results.OfType<ForceAnalysisResult>().FirstOrDefault();
        RebuildResultTable();
        UpdateScene();
    }

    public void SetSelection(
        SceneEntityKey? selection,
        ViewportSelectionOrigin origin = ViewportSelectionOrigin.Table)
    {
        if (_document is null || _document.Nodes.Count == 0)
        {
            return;
        }

        _renderer.SetSelection(selection, origin);
    }

    public void ToggleProjection() => _renderer.ToggleProjection();

    public void Fit() => _renderer.Fit();

    public void Home() => _renderer.Home();

    public Bitmap CaptureViewport() => _renderer.Capture();

    public override void ApplyLocalization()
    {
        string caption = _document?.Metadata.Name ?? Localization["PaneViewport"];
        if (_document?.IsDirty == true)
        {
            caption += Localization["DirtyIndicator"];
        }

        Text = caption;
        TabText = caption;
        _summary.Text = _document is null
            ? Localization["ViewportEmpty"]
            : string.Format(
                Localization.Culture,
                Localization["ViewportSummary"],
                _document.Nodes.Count,
                _document.Members.Count);
        _renderFailure.Text = Localization["ViewportUnavailable"];
        _projection.Text = Localization["ViewportProjection"];
        _fit.Text = Localization["ViewportFit"];
        _home.Text = Localization["ViewportHome"];
        _layerLabel.Text = Localization["ViewportLayer"];
        int layer = _layerSelector.SelectedIndex;
        _layerSelector.Items.Clear();
        _layerSelector.Items.AddRange([Localization["ViewportLayerModel"], Localization["ViewportLayerDisplacement"]]);
        _layerSelector.SelectedIndex = layer is ModelLayerIndex or DisplacementLayerIndex ? layer : ModelLayerIndex;
        int table = _resultTableSelector.SelectedIndex;
        _resultTableSelector.Items.Clear();
        _resultTableSelector.Items.AddRange(
            [Localization["ResultDisplacements"], Localization["ResultReactions"], Localization["ResultMemberForces"]]);
        _resultTableSelector.SelectedIndex = table is >= 0 and <= 2 ? table : 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _projection.Click -= OnProjectionClick;
            _fit.Click -= OnFitClick;
            _home.Click -= OnHomeClick;
            _layerSelector.SelectedIndexChanged -= OnLayerChanged;
            _resultTableSelector.SelectedIndexChanged -= OnResultTableChanged;
            _renderer.Control.HandleCreated -= OnRendererHandleCreated;
            _renderer.Control.MouseClick -= OnRendererMouseClick;
            _renderer.SelectionChanged -= OnRendererSelectionChanged;
            _layout.SizeChanged -= OnLayoutSizeChanged;
            _renderer.Dispose();
            if (_viewport.Controls.Contains(_renderer.Control))
            {
                _viewport.Controls.Remove(_renderer.Control);
            }
        }

        base.Dispose(disposing);
    }

    private void UpdateScene()
    {
        if (_disposed || _document is null || _document.Nodes.Count == 0)
        {
            _renderer.Control.Visible = false;
            _summary.Visible = true;
            return;
        }

        try
        {
            _renderer.SetScene(CreateScene());
            _renderer.Control.Visible = true;
            _summary.Visible = true;
            EnsureRendererInitialized();
            _renderFailure.Visible = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _renderer.Control.Visible = false;
            _renderFailure.Visible = true;
            ViewportFailed?.Invoke(this, new EditorValidationEventArgs(exception.Message));
        }
    }

    private ViewportSceneModel CreateScene()
    {
        ProjectDocument document = _document!;
        SceneDisplacementLayer? displacement = null;
        if (_layerSelector.SelectedIndex == DisplacementLayerIndex && _selectedResult is not null)
        {
            displacement = new SceneDisplacementLayer(
                $"result:{_selectedResult.Coordinate}",
                _selectedResult.NodeDisplacements.Select(row => new SceneNodeDisplacement(
                    row.NodeId,
                    new ScenePoint3(
                        checked((float)row.Components.Dx),
                        checked((float)row.Components.Dy),
                        checked((float)row.Components.Dz)))),
                scale: 20.0f);
        }

        return new ViewportSceneModel(
            $"document:{document.Metadata.Name}:{_layerSelector.SelectedIndex}",
            document.Nodes.Select(node => new SceneNode(
                node.Id,
                new ScenePoint3(checked((float)node.X), checked((float)node.Y), checked((float)node.Z)))),
            document.Members.Select(member => new SceneMember(member.Id, member.NodeI, member.NodeJ)),
            document.Supports.Select(support => new SceneSupport(
                support.Id,
                support.NodeId,
                support.FixX,
                support.FixY,
                support.FixZ,
                support.FixRx,
                support.FixRy,
                support.FixRz)),
            document.NodalLoads.Select(load => new SceneNodalLoad(
                load.Id,
                load.NodeId,
                new ScenePoint3(checked((float)load.Fx), checked((float)load.Fy), checked((float)load.Fz)),
                new ScenePoint3(checked((float)load.Mx), checked((float)load.My), checked((float)load.Mz)))),
            displacement: displacement);
    }

    private void EnsureRendererInitialized()
    {
        if (!_renderer.Control.IsHandleCreated || _renderer.IsInitialized)
        {
            return;
        }

        _renderer.Initialize();
        _renderer.Resize(_renderer.Control.ClientSize);
        _renderer.RequestRender();
    }

    private void RebuildResultTable()
    {
        ResultTableTruncated = false;
        _resultGrid.Columns.Clear();
        _resultGrid.Rows.Clear();
        int table = Math.Max(0, _resultTableSelector.SelectedIndex);
        if (table == 0)
        {
            AddColumns("EditorNode", "EditorX", "EditorY", "EditorZ", "EditorRx", "EditorRy", "EditorRz");
            if (_selectedResult is not null)
            {
                foreach (NodeDisplacement row in _selectedResult.NodeDisplacements)
                {
                    _resultGrid.Rows.Add(
                        row.NodeId,
                        Format(row.Components.Dx),
                        Format(row.Components.Dy),
                        Format(row.Components.Dz),
                        Format(row.Components.Rx),
                        Format(row.Components.Ry),
                        Format(row.Components.Rz));
                }
            }
        }
        else if (table == 1)
        {
            AddColumns("EditorNode", "EditorFx", "EditorFy", "EditorFz", "EditorMx", "EditorMy", "EditorMz");
            if (_selectedResult is not null)
            {
                foreach (SupportReaction row in _selectedResult.SupportReactions)
                {
                    _resultGrid.Rows.Add(
                        row.NodeId,
                        Format(row.Components.Fx),
                        Format(row.Components.Fy),
                        Format(row.Components.Fz),
                        Format(row.Components.Mx),
                        Format(row.Components.My),
                        Format(row.Components.Mz));
                }
            }
        }
        else
        {
            AddColumns(
                "EditorMember",
                "ResultStation",
                "ResultEnd",
                "EditorFx",
                "EditorFy",
                "EditorFz",
                "EditorMx",
                "EditorMy",
                "EditorMz");
            if (_selectedResult is not null)
            {
                bool limitReached = false;
                foreach (MemberSectionForces member in _selectedResult.MemberSectionForces)
                {
                    foreach (MemberSegmentResult segment in member.Segments)
                    {
                        if (_resultGrid.Rows.Count + 2 > MaximumMemberForceRows - 1)
                        {
                            limitReached = true;
                            break;
                        }

                        AddMemberForceRow(member.MemberId, segment.StationI, "I", segment.IEnd);
                        AddMemberForceRow(member.MemberId, segment.StationJ, "J", segment.JEnd);
                    }

                    if (limitReached)
                    {
                        break;
                    }
                }

                ResultTableTruncated = limitReached;
                if (limitReached)
                {
                    _resultGrid.Rows.Add(Localization["ResultTruncated"]);
                }
            }
        }
    }

    private void AddMemberForceRow(
        string memberId,
        string station,
        string end,
        ForceComponents force)
    {
        _resultGrid.Rows.Add(
            memberId,
            station,
            end,
            Format(force.Fx),
            Format(force.Fy),
            Format(force.Fz),
            Format(force.Mx),
            Format(force.My),
            Format(force.Mz));
    }

    private void AddColumns(params string[] resourceKeys)
    {
        foreach (string resourceKey in resourceKeys)
        {
            _resultGrid.Columns.Add(resourceKey, Localization[resourceKey]);
        }
    }

    private static string Format(double value) => value.ToString("G6", CultureInfo.InvariantCulture);

    private void OnProjectionClick(object? sender, EventArgs eventArgs) => ToggleProjection();

    private void OnFitClick(object? sender, EventArgs eventArgs) => Fit();

    private void OnHomeClick(object? sender, EventArgs eventArgs) => Home();

    private void OnLayerChanged(object? sender, EventArgs eventArgs) => UpdateScene();

    private void OnResultTableChanged(object? sender, EventArgs eventArgs) => RebuildResultTable();

    private void OnRendererHandleCreated(object? sender, EventArgs eventArgs) => UpdateScene();

    private void OnRendererMouseClick(object? sender, MouseEventArgs eventArgs) => _renderer.SelectAt(eventArgs.Location);

    private void OnRendererSelectionChanged(object? sender, ViewportSelectionChangedEventArgs eventArgs) =>
        SelectionChanged?.Invoke(this, eventArgs);

    private void OnLayoutSizeChanged(object? sender, EventArgs eventArgs)
    {
        if (_layout.Height < 240)
        {
            return;
        }

        int distance = Math.Clamp((int)(_layout.Height * 0.68), 100, _layout.Height - 100);
        if (_layout.SplitterDistance != distance)
        {
            _layout.SplitterDistance = distance;
        }
    }
}
