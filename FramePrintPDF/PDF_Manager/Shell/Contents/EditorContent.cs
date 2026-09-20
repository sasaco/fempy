using System.Globalization;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.Shell.Contents;

public sealed class DocumentEditedEventArgs(ProjectDocument document) : EventArgs
{
    public ProjectDocument Document { get; } = document;
}

public sealed class EditorValidationEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}

public sealed class EditorContent : ShellDockContent
{
    private readonly Label _selection = new()
    {
        AutoEllipsis = true,
        Dock = DockStyle.Top,
        Height = 34,
        Name = "EditorSelection",
        Padding = new Padding(10),
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private readonly ToolStrip _toolbar = new()
    {
        Dock = DockStyle.Top,
        GripStyle = ToolStripGripStyle.Hidden,
        Name = "EditorToolbar",
    };

    private readonly ToolStripButton _undo = new() { Name = "EditorUndo" };
    private readonly ToolStripButton _redo = new() { Name = "EditorRedo" };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Name = "EditorTabs" };
    private readonly TabPage _nodesPage = new() { Name = "NodesPage" };
    private readonly TabPage _membersPage = new() { Name = "MembersPage" };
    private readonly TabPage _supportsPage = new() { Name = "SupportsPage" };
    private readonly TabPage _loadCasesPage = new() { Name = "LoadCasesPage" };
    private readonly TabPage _loadsPage = new() { Name = "LoadsPage" };
    private readonly DataGridView _nodes = CreateGrid("NodeEditorGrid");
    private readonly DataGridView _members = CreateGrid("MemberEditorGrid");
    private readonly DataGridView _supports = CreateGrid("SupportEditorGrid");
    private readonly DataGridView _loadCases = CreateGrid("LoadCaseEditorGrid");
    private readonly DataGridView _loads = CreateGrid("NodalLoadEditorGrid");
    private ProjectDocumentEditSession? _session;
    private bool _refreshing;

    public EditorContent(DocumentKey contentKey, LocalizationService localization)
        : base(contentKey, localization)
    {
        DockAreas = DockAreas.DockLeft | DockAreas.DockRight | DockAreas.Float;
        ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockRight;
        ConfigureColumns();
        _nodesPage.Controls.Add(_nodes);
        _membersPage.Controls.Add(_members);
        _supportsPage.Controls.Add(_supports);
        _loadCasesPage.Controls.Add(_loadCases);
        _loadsPage.Controls.Add(_loads);
        _tabs.TabPages.AddRange([_nodesPage, _membersPage, _supportsPage, _loadCasesPage, _loadsPage]);
        _toolbar.Items.AddRange([_undo, _redo]);
        Controls.Add(_tabs);
        Controls.Add(_toolbar);
        Controls.Add(_selection);
        _undo.Click += OnUndo;
        _redo.Click += OnRedo;
        foreach (DataGridView grid in Grids)
        {
            grid.CellEndEdit += OnCellEndEdit;
            grid.SelectionChanged += OnGridSelectionChanged;
            grid.DataError += OnDataError;
        }

        ApplyLocalization();
    }

    public event EventHandler<DocumentEditedEventArgs>? DocumentEdited;

    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<EditorValidationEventArgs>? ValidationFailed;

    public Label SelectionLabel => _selection;

    public DataGridView NodeGrid => _nodes;

    public DataGridView MemberGrid => _members;

    public DataGridView SupportGrid => _supports;

    public DataGridView LoadCaseGrid => _loadCases;

    public DataGridView NodalLoadGrid => _loads;

    public ProjectDocument? Document => _session?.Current;

    public bool CanUndo => _session?.CanUndo == true;

    public bool CanRedo => _session?.CanRedo == true;

    private IEnumerable<DataGridView> Grids => [_nodes, _members, _supports, _loadCases, _loads];

    public void SetDocument(ProjectDocument? document)
    {
        _session = document is null ? null : new ProjectDocumentEditSession(document);
        RefreshFromSession();
    }

    public bool UpsertNode(ProjectNode node) => ApplyEdit(session => session.UpsertNode(node));

    public bool UpsertMember(ProjectMember member) => ApplyEdit(session => session.UpsertMember(member));

    public bool UpsertSupport(ProjectSupport support) => ApplyEdit(session => session.UpsertSupport(support));

    public bool UpsertLoadCase(LoadCaseDefinition loadCase) => ApplyEdit(session => session.UpsertLoadCase(loadCase));

    public bool UpsertNodalLoad(NodalLoadDefinition load) => ApplyEdit(session => session.UpsertNodalLoad(load));

    public bool Undo() => ApplyEdit(session => session.Undo());

    public bool Redo() => ApplyEdit(session => session.Redo());

    public void SetSelection(SceneEntityKey? selection)
    {
        _refreshing = true;
        try
        {
            foreach (DataGridView grid in Grids)
            {
                grid.ClearSelection();
            }

            if (selection is not { } key)
            {
                _selection.Text = Localization["EditorNoSelection"];
                return;
            }

            DataGridView? target = key.Kind switch
            {
                SceneEntityKind.Node => _nodes,
                SceneEntityKind.Member => _members,
                SceneEntityKind.Support => _supports,
                SceneEntityKind.NodalLoad => _loads,
                _ => null,
            };
            if (target is null)
            {
                return;
            }

            DataGridViewRow? row = target.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(candidate => candidate.Tag is SceneEntityKey candidateKey && candidateKey == key);
            if (row is null)
            {
                return;
            }

            _tabs.SelectedTab = (TabPage)target.Parent!;
            row.Selected = true;
            target.CurrentCell = row.Cells[0];
            _selection.Text = $"{key.Kind}: {key.Id}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    public override void ApplyLocalization()
    {
        Text = Localization["PaneEditor"];
        TabText = Text;
        _undo.Text = Localization["EditorUndo"];
        _redo.Text = Localization["EditorRedo"];
        _nodesPage.Text = Localization["EditorNodes"];
        _membersPage.Text = Localization["EditorMembers"];
        _supportsPage.Text = Localization["EditorSupports"];
        _loadCasesPage.Text = Localization["EditorLoadCases"];
        _loadsPage.Text = Localization["EditorLoads"];
        string[] coordinateHeaders = ["EditorId", "EditorX", "EditorY", "EditorZ"];
        SetHeaders(_nodes, coordinateHeaders);
        SetHeaders(_members, ["EditorId", "EditorNodeI", "EditorNodeJ"]);
        SetHeaders(_supports,
            ["EditorId", "EditorNode", "EditorFixX", "EditorFixY", "EditorFixZ", "EditorFixRx", "EditorFixRy", "EditorFixRz"]);
        SetHeaders(_loadCases, ["EditorId", "EditorName", "EditorSymbol"]);
        SetHeaders(_loads,
            ["EditorId", "EditorCase", "EditorNode", "EditorFx", "EditorFy", "EditorFz", "EditorMx", "EditorMy", "EditorMz"]);
        if (!_selection.Text.Contains(':', StringComparison.Ordinal))
        {
            _selection.Text = Localization["EditorNoSelection"];
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _undo.Click -= OnUndo;
            _redo.Click -= OnRedo;
            foreach (DataGridView grid in Grids)
            {
                grid.CellEndEdit -= OnCellEndEdit;
                grid.SelectionChanged -= OnGridSelectionChanged;
                grid.DataError -= OnDataError;
            }
        }

        base.Dispose(disposing);
    }

    private static DataGridView CreateGrid(string name) => new()
    {
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
        MultiSelect = false,
        Name = name,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    private void ConfigureColumns()
    {
        AddTextColumns(_nodes, 4);
        AddTextColumns(_members, 3);
        AddTextColumns(_supports, 2);
        for (int index = 0; index < 6; index++)
        {
            _supports.Columns.Add(new DataGridViewCheckBoxColumn());
        }

        AddTextColumns(_loadCases, 3);
        AddTextColumns(_loads, 9);
        foreach (DataGridView grid in Grids)
        {
            grid.Columns[0].ReadOnly = true;
        }
    }

    private static void AddTextColumns(DataGridView grid, int count)
    {
        for (int index = 0; index < count; index++)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn());
        }
    }

    private void RefreshFromSession()
    {
        _refreshing = true;
        try
        {
            foreach (DataGridView grid in Grids)
            {
                grid.Rows.Clear();
            }

            ProjectDocument? document = _session?.Current;
            if (document is null)
            {
                return;
            }

            foreach (ProjectNode node in document.Nodes)
            {
                int index = _nodes.Rows.Add(node.Id, node.X, node.Y, node.Z);
                _nodes.Rows[index].Tag = new SceneEntityKey(SceneEntityKind.Node, node.Id);
            }

            foreach (ProjectMember member in document.Members)
            {
                int index = _members.Rows.Add(member.Id, member.NodeI, member.NodeJ);
                _members.Rows[index].Tag = new SceneEntityKey(SceneEntityKind.Member, member.Id);
            }

            foreach (ProjectSupport support in document.Supports)
            {
                int index = _supports.Rows.Add(
                    support.Id,
                    support.NodeId,
                    support.FixX,
                    support.FixY,
                    support.FixZ,
                    support.FixRx,
                    support.FixRy,
                    support.FixRz);
                _supports.Rows[index].Tag = new SceneEntityKey(SceneEntityKind.Support, support.Id);
            }

            foreach (LoadCaseDefinition loadCase in document.LoadCases)
            {
                _loadCases.Rows.Add(loadCase.Id, loadCase.Name, loadCase.Symbol);
            }

            foreach (NodalLoadDefinition load in document.NodalLoads)
            {
                int index = _loads.Rows.Add(
                    load.Id,
                    load.CaseId,
                    load.NodeId,
                    load.Fx,
                    load.Fy,
                    load.Fz,
                    load.Mx,
                    load.My,
                    load.Mz);
                _loads.Rows[index].Tag = new SceneEntityKey(SceneEntityKind.NodalLoad, load.Id);
            }
        }
        finally
        {
            _refreshing = false;
            UpdateCommandState();
        }
    }

    private bool ApplyEdit(Func<ProjectDocumentEditSession, bool> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (_session is null)
        {
            return false;
        }

        try
        {
            if (!edit(_session))
            {
                return false;
            }

            RefreshFromSession();
            DocumentEdited?.Invoke(this, new DocumentEditedEventArgs(_session.Current));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            RefreshFromSession();
            ValidationFailed?.Invoke(this, new EditorValidationEventArgs(exception.Message));
            return false;
        }
    }

    private void OnCellEndEdit(object? sender, DataGridViewCellEventArgs eventArgs)
    {
        if (_refreshing || _session is null || sender is not DataGridView grid || eventArgs.RowIndex < 0)
        {
            return;
        }

        DataGridViewRow row = grid.Rows[eventArgs.RowIndex];
        try
        {
            _ = grid == _nodes
                ? UpsertNode(new ProjectNode(CellText(row, 0), Number(row, 1), Number(row, 2), Number(row, 3)))
                : grid == _members
                    ? UpdateMember(row)
                    : grid == _supports
                        ? UpsertSupport(new ProjectSupport(
                            CellText(row, 0), CellText(row, 1), Flag(row, 2), Flag(row, 3), Flag(row, 4),
                            Flag(row, 5), Flag(row, 6), Flag(row, 7)))
                        : grid == _loadCases
                            ? UpsertLoadCase(new LoadCaseDefinition(
                                CellText(row, 0),
                                CellText(row, 1),
                                CellText(row, 2)))
                            : UpsertNodalLoad(new NodalLoadDefinition(
                                CellText(row, 0), CellText(row, 1), CellText(row, 2),
                                Number(row, 3), Number(row, 4), Number(row, 5),
                                Number(row, 6), Number(row, 7), Number(row, 8)));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            RefreshFromSession();
            ValidationFailed?.Invoke(this, new EditorValidationEventArgs(exception.Message));
        }
    }

    private bool UpdateMember(DataGridViewRow row)
    {
        string id = CellText(row, 0);
        ProjectMember? existing = _session?.Current.Members.FirstOrDefault(member => member.Id == id);
        return existing is not null && UpsertMember(existing with { NodeI = CellText(row, 1), NodeJ = CellText(row, 2) });
    }

    private void OnGridSelectionChanged(object? sender, EventArgs eventArgs)
    {
        if (_refreshing || sender is not DataGridView grid || grid.SelectedRows.Count == 0 ||
            grid.SelectedRows[0].Tag is not SceneEntityKey selection)
        {
            return;
        }

        _selection.Text = $"{selection.Kind}: {selection.Id}";
        SelectionChanged?.Invoke(
            this,
            new ViewportSelectionChangedEventArgs(selection, ViewportSelectionOrigin.Table));
    }

    private void OnDataError(object? sender, DataGridViewDataErrorEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        ValidationFailed?.Invoke(this, new EditorValidationEventArgs(Localization["EditorInvalidValue"]));
    }

    private void OnUndo(object? sender, EventArgs eventArgs) => Undo();

    private void OnRedo(object? sender, EventArgs eventArgs) => Redo();

    private void UpdateCommandState()
    {
        _undo.Enabled = CanUndo;
        _redo.Enabled = CanRedo;
    }

    private static string CellText(DataGridViewRow row, int index) =>
        Convert.ToString(row.Cells[index].Value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static double Number(DataGridViewRow row, int index) =>
        double.Parse(CellText(row, index), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool Flag(DataGridViewRow row, int index) =>
        Convert.ToBoolean(row.Cells[index].Value, CultureInfo.InvariantCulture);

    private void SetHeaders(DataGridView grid, IReadOnlyList<string> resourceKeys)
    {
        for (int index = 0; index < resourceKeys.Count; index++)
        {
            grid.Columns[index].HeaderText = Localization[resourceKeys[index]];
        }
    }
}
