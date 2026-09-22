using FrameWebforCS.Core.Documents;
using FrameWebforCS.Resources;
using FrameWebforCS.Shell.Contents;
using FrameWebforCS.Shell.ScreenComposition.Core;

namespace FrameWebforCS.Shell.ScreenComposition.Surfaces;

public sealed class ResultRouteSurfaceControl : FloatingCardSurface, IFrameWebSurface
{
    private readonly LocalizationService _localization;
    private readonly ProjectDocumentContent _documentHost;
    private readonly FlowLayoutPanel _selectors = new()
    {
        AutoSize = true,
        BackColor = SurfaceVisuals.Card,
        Dock = DockStyle.Top,
        FlowDirection = FlowDirection.LeftToRight,
        Name = "ResultRouteSelectors",
        Padding = new Padding(0, 0, 0, 6),
        WrapContents = true,
    };
    private readonly ComboBox _caseSelector = Selector("ResultRouteCaseSelector", 132);
    private readonly ComboBox _stateSelector = Selector("ResultRouteStateSelector", 128);
    private readonly ComboBox _parentSelector = Selector("ResultRouteParentSelector", 132);
    private readonly ComboBox _childSelector = Selector("ResultRouteChildSelector", 132);
    private readonly ComboBox _extremaSelector = Selector("ResultRouteExtremaSelector", 128);
    private readonly Panel _gridHost = new()
    {
        BackColor = SurfaceVisuals.Card,
        Dock = DockStyle.Fill,
        Name = "ResultRouteGridHost",
    };
    private readonly Label _provenance = new()
    {
        AutoEllipsis = true,
        BackColor = SurfaceVisuals.Card,
        Dock = DockStyle.Bottom,
        ForeColor = Color.White,
        Height = 28,
        Name = "ResultRouteProvenance",
        Padding = new Padding(6),
    };
    private ResultRouteSurfaceDefinition? _definition;
    private ScreenRouteState? _state;
    private bool _updating;
    private bool _disposed;

    public ResultRouteSurfaceControl(LocalizationService localization, ProjectDocumentContent documentHost)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _documentHost = documentHost ?? throw new ArgumentNullException(nameof(documentHost));
        Name = "ResultRouteSurface";
        _caseSelector.SelectedIndexChanged += (_, _) => SelectMirror(_caseSelector, _documentHost.ResultCaseSelector);
        _stateSelector.SelectedIndexChanged += (_, _) => SelectMirror(_stateSelector, _documentHost.ResultStateSelector);
        _parentSelector.SelectedIndexChanged += (_, _) => SelectMirror(_parentSelector, _documentHost.ResultParentSelector);
        _childSelector.SelectedIndexChanged += (_, _) => SelectMirror(_childSelector, _documentHost.ResultChildSelector);
        _extremaSelector.SelectedIndexChanged += (_, _) => SelectMirror(_extremaSelector, _documentHost.ResultExtremaSelector);

        AddSelector("ResultCase", _caseSelector);
        AddSelector("ResultState", _stateSelector);
        AddSelector("ResultParent", _parentSelector);
        AddSelector("ResultChild", _childSelector);
        AddSelector("ResultExtrema", _extremaSelector);
        BodyPanel.Controls.Add(_gridHost);
        BodyPanel.Controls.Add(_selectors);
        BodyPanel.Controls.Add(_provenance);
        _documentHost.AttachResultGrid(_gridHost);
        ApplyAngularGridStyle(_documentHost.ResultGrid);
        _localization.CultureChanged += OnCultureChanged;
    }

    public ScreenRouteId RouteKey =>
        _definition?.Route ?? throw new InvalidOperationException("No result route has been applied.");

    public ResultSurfaceCategory Category =>
        _definition?.Category ?? throw new InvalidOperationException("No result route has been applied.");

    public ScreenRouteContext Substate =>
        _definition?.Context ?? throw new InvalidOperationException("No result route has been applied.");

    public DataGridView ResultGrid => _documentHost.ResultGrid;

    public ComboBox CaseSelector => _caseSelector;

    public ComboBox StateSelector => _stateSelector;

    public ComboBox ParentSelector => _parentSelector;

    public ComboBox ChildSelector => _childSelector;

    public ComboBox ExtremaSelector => _extremaSelector;

    public void ApplyState(ScreenRouteState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Route is not ScreenRouteId route || !AngularScreenManifest.IsResultRoute(route))
        {
            throw new ArgumentException("A result route state is required.", nameof(state));
        }

        if (!state.ResultsEnabled)
        {
            throw new ArgumentException("Result routes require calculated results.", nameof(state));
        }

        _state = state;
        _definition = FrameWebSurfaceCatalog.GetResult(route);
        _documentHost.AttachResultGrid(_gridHost);
        SelectDerivedContext(_definition.Context);
        _documentHost.SelectResultTable((int)_definition.Category);
        RefreshMirrors();
        ApplyLocalization();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _localization.CultureChanged -= OnCultureChanged;
            if (!_documentHost.IsDisposed && ReferenceEquals(_documentHost.ResultGrid.Parent, _gridHost))
            {
                _documentHost.ParkResultGrid();
            }
        }

        base.Dispose(disposing);
    }

    private static ComboBox Selector(string name, int width) => new()
    {
        BackColor = Color.FromArgb(76, 78, 82),
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        ForeColor = Color.White,
        Margin = new Padding(4, 1, 10, 1),
        Name = name,
        Width = width,
    };

    private void AddSelector(string resourceKey, ComboBox selector)
    {
        _selectors.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Margin = new Padding(4, 8, 0, 0),
            Name = $"{selector.Name}Label",
            Tag = resourceKey,
        });
        _selectors.Controls.Add(selector);
    }

    private static void ApplyAngularGridStyle(DataGridView grid)
    {
        grid.BackgroundColor = SurfaceVisuals.Card;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceVisuals.Header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceVisuals.Header;
        grid.ColumnHeadersHeight = 31;
        grid.EnableHeadersVisualStyles = false;
        grid.GridColor = SurfaceVisuals.Border;
        grid.RowHeadersDefaultCellStyle.BackColor = SurfaceVisuals.Header;
        grid.RowHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = SurfaceVisuals.Accent;
        grid.RowsDefaultCellStyle.BackColor = SurfaceVisuals.Card;
        grid.RowsDefaultCellStyle.ForeColor = Color.White;
        grid.RowsDefaultCellStyle.SelectionBackColor = SurfaceVisuals.Accent;
        grid.RowsDefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(76, 78, 82);
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = SurfaceVisuals.Accent;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
    }

    private void SelectDerivedContext(ScreenRouteContext context)
    {
        DerivedResultKind? kind = context switch
        {
            ScreenRouteContext.BasicResult => null,
            ScreenRouteContext.CombinedResult => DerivedResultKind.Combine,
            ScreenRouteContext.PickupResult => DerivedResultKind.Pickup,
            _ => throw new ArgumentOutOfRangeException(nameof(context), context, "Unknown result substate."),
        };
        _documentHost.SelectDerivedResult(kind);
    }

    private void RefreshMirrors()
    {
        _updating = true;
        try
        {
            Mirror(_documentHost.ResultCaseSelector, _caseSelector);
            Mirror(_documentHost.ResultStateSelector, _stateSelector);
            Mirror(_documentHost.ResultParentSelector, _parentSelector);
            Mirror(_documentHost.ResultChildSelector, _childSelector);
            Mirror(_documentHost.ResultExtremaSelector, _extremaSelector);
            _parentSelector.Visible = _parentSelector.Items.Count > 0;
            _childSelector.Visible = _childSelector.Items.Count > 0;
            _provenance.Text = ResolveProvenance();
        }
        finally
        {
            _updating = false;
        }
    }

    private static void Mirror(ToolStripComboBox source, ComboBox target)
    {
        target.BeginUpdate();
        try
        {
            target.Items.Clear();
            foreach (object item in source.Items)
            {
                target.Items.Add(item.ToString() ?? string.Empty);
            }

            target.SelectedIndex = source.SelectedIndex >= 0 && source.SelectedIndex < target.Items.Count
                ? source.SelectedIndex
                : -1;
            target.Enabled = source.Enabled;
        }
        finally
        {
            target.EndUpdate();
        }
    }

    private void SelectMirror(ComboBox source, ToolStripComboBox target)
    {
        if (_updating || source.SelectedIndex < 0 || source.SelectedIndex >= target.Items.Count)
        {
            return;
        }

        target.SelectedIndex = source.SelectedIndex;
        RefreshMirrors();
    }

    private string ResolveProvenance()
    {
        if (_documentHost.SelectedMovingLoadEnvelope is { } moving)
        {
            return $"{_localization["ResultMovingEnvelope"]}: {moving.DefinitionId}";
        }

        if (_documentHost.SelectedDerivedResultId is string derived)
        {
            return $"{_localization["ResultDerived"]}: {derived}";
        }

        return _documentHost.SelectedResultCoordinate?.ToString() ?? _localization["ResultPresentationUnavailable"];
    }

    private void ApplyLocalization()
    {
        if (_definition is null)
        {
            return;
        }

        string substate = _definition.Context switch
        {
            ScreenRouteContext.BasicResult => _localization["SurfaceResultBasic"],
            ScreenRouteContext.CombinedResult => _localization["SurfaceResultCombine"],
            ScreenRouteContext.PickupResult => _localization["SurfaceResultPickup"],
            _ => throw new ArgumentOutOfRangeException(nameof(_definition.Context)),
        };
        CardTitle = string.Empty;
        CardPanel.AccessibleName = $"{_localization[_definition.TitleResourceKey]} — {substate}";
        foreach (Control control in _selectors.Controls)
        {
            if (control is Label { Tag: string resourceKey } label)
            {
                label.Text = _localization[resourceKey];
            }
        }
        RefreshMirrors();
    }

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();
}
