using System.Globalization;
using FrameWebforCsharp.Core.Documents;
using FrameWebforCsharp.Core.Shell;
using FrameWebforCsharp.Rendering.Scene;
using FrameWebforCsharp.Resources;
using FrameWebforCsharp.Shell.Editing;
using WeifenLuo.WinFormsUI.Docking;

namespace FrameWebforCsharp.Shell.Contents;

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
    private const char CompositeSeparator = '\u001f';
    private readonly int _creatingThreadId = Environment.CurrentManagedThreadId;
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
    private readonly Panel _tableParking = new()
    {
        Name = "EditorTableParking",
        Size = Size.Empty,
        Visible = false,
    };
    private readonly IReadOnlyDictionary<InputTableKey, InputTableDescriptor> _descriptors;
    private readonly Dictionary<InputTableKey, DataGridViewEditorController> _controllers = [];
    private ProjectDocumentEditSession? _session;
    private bool _refreshing;
    private bool _disposed;

    public EditorContent(
        DocumentKey contentKey,
        LocalizationService localization,
        IClipboardTextService? clipboard = null)
        : base(contentKey, localization)
    {
        DockAreas = DockAreas.DockLeft | DockAreas.DockRight | DockAreas.Float;
        ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockRight;
        _descriptors = CreateDescriptors().ToDictionary(descriptor => descriptor.Key);
        IClipboardTextService clipboardService = clipboard ?? new SystemClipboardTextService();
        foreach (InputTableDescriptor descriptor in _descriptors.Values)
        {
            EditorDataGridView grid = new() { Name = descriptor.GridName };
            _tableParking.Controls.Add(grid);
            _controllers.Add(descriptor.Key, new DataGridViewEditorController(
                grid,
                descriptor,
                clipboardService,
                updates => ApplyRows(descriptor.Key, updates),
                count => InsertRows(descriptor.Key, count),
                ids => DeleteRows(descriptor.Key, ids),
                OnGridSelectionChanged,
                OnGridFailure));
        }

        _toolbar.Items.AddRange([_undo, _redo]);
        _undo.Click += OnUndo;
        _redo.Click += OnRedo;
        ApplyLocalization();
    }

    public event EventHandler<DocumentEditedEventArgs>? DocumentEdited;
    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<EditorValidationEventArgs>? ValidationFailed;

    public Label SelectionLabel => _selection;
    public IReadOnlyDictionary<InputTableKey, DataGridView> Tables =>
        _controllers.ToDictionary(pair => pair.Key, pair => pair.Value.Grid);
    public IReadOnlyDictionary<InputTableKey, InputTableDescriptor> Descriptors => _descriptors;
    public DataGridView ModelSettingsGrid => Grid(InputTableKey.ModelSettings);
    public DataGridView NodeGrid => Grid(InputTableKey.Nodes);
    public DataGridView MemberGrid => Grid(InputTableKey.Members);
    public DataGridView RigidZoneGrid => Grid(InputTableKey.RigidZones);
    public DataGridView ElementPropertySetGrid => Grid(InputTableKey.ElementPropertySets);
    public DataGridView SupportGrid => Grid(InputTableKey.Supports);
    public DataGridView SupportSetGrid => Grid(InputTableKey.SupportSets);
    public DataGridView SectionGrid => Grid(InputTableKey.Sections);
    public DataGridView PanelGrid => Grid(InputTableKey.Panels);
    public DataGridView JointGrid => Grid(InputTableKey.Joints);
    public DataGridView JointReleaseSetGrid => Grid(InputTableKey.JointReleaseSets);
    public DataGridView NoticePointGrid => Grid(InputTableKey.NoticePoints);
    public DataGridView MemberSpringGrid => Grid(InputTableKey.MemberSprings);
    public DataGridView MemberSpringSetGrid => Grid(InputTableKey.MemberSpringSets);
    public DataGridView LoadCaseGrid => Grid(InputTableKey.LoadCases);
    public DataGridView NodalLoadGrid => Grid(InputTableKey.NodalLoads);
    public DataGridView PrescribedDisplacementGrid => Grid(InputTableKey.PrescribedDisplacements);
    public DataGridView MemberLoadGrid => Grid(InputTableKey.MemberLoads);
    public DataGridView DefineGrid => Grid(InputTableKey.Define);
    public DataGridView CombineGrid => Grid(InputTableKey.Combine);
    public DataGridView PickupGrid => Grid(InputTableKey.Pickup);
    public ProjectDocument? Document => _session?.Current;
    public bool CanUndo => _session?.CanUndo == true;
    public bool CanRedo => _session?.CanRedo == true;

    public void SetDocument(ProjectDocument? document)
    {
        VerifyAccess();
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
    public bool Copy(InputTableKey table) => _controllers[table].CopySelection();
    public bool Paste(InputTableKey table) => _controllers[table].Paste();
    public bool Insert(InputTableKey table, int count = 1) => _controllers[table].InsertRows(count);
    public bool Delete(InputTableKey table) => _controllers[table].DeleteSelection();
    public bool MoveCurrentCell(InputTableKey table, Keys keyData) => _controllers[table].MoveCurrentCell(keyData);
    public bool DispatchCommand(InputTableKey table, Keys keyData) => _controllers[table].DispatchCommand(keyData);

    public void ActivateTable(InputTableKey table)
    {
        VerifyAccess();
        Grid(table).Focus();
    }

    public void AttachTable(InputTableKey table, Control parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        VerifyAccess();
        DataGridView grid = Grid(table);
        grid.Parent?.Controls.Remove(grid);
        grid.Dock = DockStyle.Fill;
        parent.Controls.Add(grid);
        grid.BringToFront();
    }

    public void ParkTable(InputTableKey table)
    {
        VerifyAccess();
        DataGridView grid = Grid(table);
        grid.Parent?.Controls.Remove(grid);
        _tableParking.Controls.Add(grid);
    }

    public void ParkAllTables()
    {
        VerifyAccess();
        foreach (InputTableKey table in _controllers.Keys)
        {
            ParkTable(table);
        }
    }

    public void SetSelection(SceneEntityKey? selection)
    {
        VerifyAccess();
        _refreshing = true;
        try
        {
            foreach (DataGridViewEditorController controller in _controllers.Values)
            {
                controller.Grid.ClearSelection();
            }

            if (selection is not { } key)
            {
                _selection.Text = Localization["EditorNoSelection"];
                return;
            }

            InputTableKey? table = key.Kind switch
            {
                SceneEntityKind.Node => InputTableKey.Nodes,
                SceneEntityKind.Member => InputTableKey.Members,
                SceneEntityKind.Support => InputTableKey.Supports,
                SceneEntityKind.NodalLoad => InputTableKey.NodalLoads,
                SceneEntityKind.MemberLoad => InputTableKey.MemberLoads,
                _ => null,
            };
            if (table is not { } tableKey)
            {
                return;
            }

            DataGridView target = Grid(tableKey);
            DataGridViewRow? row = target.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(candidate => candidate.Tag is SceneEntityKey candidateKey && candidateKey == key);
            if (row is null)
            {
                return;
            }

            row.Selected = true;
            DataGridViewCell? visibleCell = row.Cells.Cast<DataGridViewCell>()
                .FirstOrDefault(cell => cell.OwningColumn.Visible);
            if (visibleCell is null)
            {
                return;
            }

            target.CurrentCell = visibleCell;
            _selection.Text = $"{key.Kind}: {key.Id}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    public override void ApplyLocalization()
    {
        VerifyAccess();
        Text = Localization["PaneEditor"];
        TabText = Text;
        _undo.Text = Localization["EditorUndo"];
        _redo.Text = Localization["EditorRedo"];
        foreach ((InputTableKey key, InputTableDescriptor descriptor) in _descriptors)
        {
            _controllers[key].ApplyLocalization(resourceKey => Localization[resourceKey]);
        }

        if (!_selection.Text.Contains(':', StringComparison.Ordinal))
        {
            _selection.Text = Localization["EditorNoSelection"];
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            VerifyAccess();
            _disposed = true;
            _undo.Click -= OnUndo;
            _redo.Click -= OnRedo;
            foreach (DataGridViewEditorController controller in _controllers.Values)
            {
                controller.Dispose();
            }

            _tableParking.Dispose();
            _toolbar.Dispose();
            _selection.Dispose();
        }

        base.Dispose(disposing);
    }

    private static IEnumerable<InputTableDescriptor> CreateDescriptors()
    {
        yield return Table(InputTableKey.ModelSettings, "EditorModelSettings", "ModelSettingsEditorGrid",
            Choice("dimension", "EditorDimension"));
        yield return Table(InputTableKey.Nodes, "EditorNodes", "NodeEditorGrid",
            Col("id", "EditorId", true), Num("x", "EditorX"), Num("y", "EditorY"), Num("z", "EditorZ"));
        yield return Table(InputTableKey.Members, "EditorMembers", "MemberEditorGrid",
            Col("id", "EditorId", true), Col("node_i", "EditorNodeI"), Col("node_j", "EditorNodeJ"),
            Col("section", "EditorSection"), Num("rotation", "EditorRotation"), Bool("shear", "EditorShearCorrection"));
        yield return Table(InputTableKey.RigidZones, "EditorRigidZones", "RigidZoneEditorGrid",
            Col("id", "EditorId", true), Col("member", "EditorMember"), Num("i_length", "EditorILength"),
            Num("j_length", "EditorJLength"), Col("section", "EditorSection"));
        yield return Table(InputTableKey.ElementPropertySets, "EditorElementPropertySets", "ElementPropertySetEditorGrid",
            Col("id", "EditorId", true), Col("name", "EditorName"));
        yield return Table(InputTableKey.Supports, "EditorSupports", "SupportEditorGrid",
            Col("id", "EditorId", true), Col("node", "EditorNode"), Num("tx", "EditorFixX"),
            Num("ty", "EditorFixY"), Num("tz", "EditorFixZ"), Num("rx", "EditorFixRx"),
            Num("ry", "EditorFixRy"), Num("rz", "EditorFixRz"), Col("set", "EditorSet"));
        yield return Table(InputTableKey.SupportSets, "EditorSupportSets", "SupportSetEditorGrid",
            Col("id", "EditorId", true), Col("name", "EditorName"));
        yield return Table(InputTableKey.Sections, "EditorSections", "SectionEditorGrid",
            Col("id", "EditorId", true), Col("name", "EditorName"), Num("e", "EditorYoungsModulus"),
            Num("nu", "EditorPoissonRatio"), Num("g", "EditorShearModulus"), Num("area", "EditorArea"),
            Num("iy", "EditorIy"), Num("iz", "EditorIz"), Num("j", "EditorTorsion"),
            Num("xp", "EditorThermalExpansion"), Num("density", "EditorDensity"), Num("thickness", "EditorThickness"),
            Col("set", "EditorSet"));
        yield return Table(InputTableKey.Panels, "EditorPanels", "PanelEditorGrid",
            Col("id", "EditorId", true), Col("section", "EditorSection"), Col("node1", "EditorNode1"),
            Col("node2", "EditorNode2"), Col("node3", "EditorNode3"), Col("node4", "EditorNode4"));
        yield return Table(InputTableKey.Joints, "EditorJoints", "JointEditorGrid",
            Col("id", "EditorId", true), Col("member", "EditorMember"), Bool("xi", "EditorXi"),
            Bool("yi", "EditorYi"), Bool("zi", "EditorZi"), Bool("xj", "EditorXj"),
            Bool("yj", "EditorYj"), Bool("zj", "EditorZj"), Col("set", "EditorSet"));
        yield return Table(InputTableKey.JointReleaseSets, "EditorJointReleaseSets", "JointReleaseSetEditorGrid",
            Col("id", "EditorId", true), Col("name", "EditorName"));
        yield return Table(InputTableKey.NoticePoints, "EditorNoticePoints", "NoticePointEditorGrid",
            Col("id", "EditorId", true), Col("member", "EditorMember"), Num("distance", "EditorDistance"));
        yield return Table(InputTableKey.MemberSprings, "EditorMemberSprings", "MemberSpringEditorGrid",
            Col("id", "EditorId", true), Col("member", "EditorMember"), Num("tx", "EditorTx"),
            Num("ty", "EditorTy"), Num("tz", "EditorTz"), Num("tr", "EditorTr"), Col("set", "EditorSet"));
        yield return Table(InputTableKey.MemberSpringSets, "EditorMemberSpringSets", "MemberSpringSetEditorGrid",
            Col("id", "EditorId", true), Col("name", "EditorName"));
        yield return Table(InputTableKey.LoadCases, "EditorLoadCases", "LoadCaseEditorGrid",
            Col("id", "EditorId", true), Col("name", "EditorName"), Col("symbol", "EditorSymbol"),
            Col("element", "EditorElementSet"), Col("support", "EditorSupportSet"),
            Col("spring", "EditorMemberSpringSet"), Col("joint", "EditorJointSet"), Num("pitch", "EditorMovingPitch"));
        yield return Table(InputTableKey.NodalLoads, "EditorLoads", "NodalLoadEditorGrid",
            Col("id", "EditorId", true), Col("case", "EditorCase"), Col("node", "EditorNode"),
            Num("x", "EditorFx"), Num("y", "EditorFy"), Num("z", "EditorFz"), Num("rx", "EditorMx"),
            Num("ry", "EditorMy"), Num("rz", "EditorMz"));
        yield return Table(InputTableKey.PrescribedDisplacements, "EditorPrescribedDisplacements", "PrescribedDisplacementEditorGrid",
            Col("id", "EditorId", true), Col("case", "EditorCase"), Col("node", "EditorNode"),
            Num("dx", "EditorDx"), Num("dy", "EditorDy"), Num("dz", "EditorDz"), Num("rx", "EditorAx"),
            Num("ry", "EditorAy"), Num("rz", "EditorAz"));
        yield return Table(InputTableKey.MemberLoads, "EditorMemberLoads", "MemberLoadEditorGrid",
            Col("id", "EditorId", true), Col("case", "EditorCase"), Col("member", "EditorMember"),
            Choice("kind", "EditorLoadKind"), Choice("direction", "EditorDirection"), Num("l1", "EditorL1"),
            Num("l2", "EditorL2"), Num("p1", "EditorP1"), Num("p2", "EditorP2"));
        yield return DerivedTable(InputTableKey.Define, "EditorDefine", "DefineEditorGrid");
        yield return DerivedTable(InputTableKey.Combine, "EditorCombine", "CombineEditorGrid");
        yield return DerivedTable(InputTableKey.Pickup, "EditorPickup", "PickupEditorGrid");
    }

    private static InputTableDescriptor DerivedTable(InputTableKey key, string title, string name) =>
        Table(key, title, name, Col("id", "EditorId", true), Col("name", "EditorName"),
            Col("sources", "EditorSources"), Col("factors", "EditorFactors"));
    private static InputTableDescriptor Table(InputTableKey key, string title, string name, params InputColumnDescriptor[] columns) =>
        new(key, title, name, columns);
    private static InputColumnDescriptor Col(string id, string header, bool readOnly = false) =>
        new(id, header, InputColumnKind.Text, readOnly);
    private static InputColumnDescriptor Num(string id, string header) => new(id, header, InputColumnKind.Number);
    private static InputColumnDescriptor Bool(string id, string header) => new(id, header, InputColumnKind.Boolean);
    private static InputColumnDescriptor Choice(string id, string header) => new(id, header, InputColumnKind.Choice);

    private void RefreshFromSession()
    {
        VerifyAccess();
        _refreshing = true;
        try
        {
            ProjectDocument? document = _session?.Current;
            foreach ((InputTableKey key, DataGridViewEditorController controller) in _controllers)
            {
                controller.Reload(document is null ? [] : ReadRows(key, document));
            }
        }
        finally
        {
            _refreshing = false;
            UpdateCommandState();
        }
    }

    private static IReadOnlyList<InputGridRow> ReadRows(InputTableKey table, ProjectDocument document) => table switch
    {
        InputTableKey.ModelSettings => [Row("dimension", null, document.Dimension)],
        InputTableKey.Nodes => document.Nodes.Select(node => Row(node.Id,
            new SceneEntityKey(SceneEntityKind.Node, node.Id), node.Id, node.X, node.Y, node.Z)).ToArray(),
        InputTableKey.Members => document.Members.Select(member => Row(member.Id,
            new SceneEntityKey(SceneEntityKind.Member, member.Id), member.Id, member.NodeI, member.NodeJ,
            member.SectionId, member.RotationDegrees, member.ShearCorrection)).ToArray(),
        InputTableKey.RigidZones => document.RigidZones.Select(zone => Row(zone.Id, null,
            zone.Id, zone.MemberId, zone.ILength, zone.JLength, zone.SectionId)).ToArray(),
        InputTableKey.ElementPropertySets => document.ElementPropertySets.Select(set => Row(set.Id, null,
            set.Id, set.Name)).ToArray(),
        InputTableKey.Supports => ReadSupports(document),
        InputTableKey.SupportSets => document.SupportSets.Select(set => Row(set.Id, null, set.Id, set.Name)).ToArray(),
        InputTableKey.Sections => ReadSections(document),
        InputTableKey.Panels => document.Panels.Select(panel => Row(panel.Id, null,
            panel.Id, panel.SectionId, NodeAt(panel, 0), NodeAt(panel, 1), NodeAt(panel, 2), NodeAt(panel, 3))).ToArray(),
        InputTableKey.Joints => document.JointReleaseSets.SelectMany(set => set.Rows.Select(row => Row(
            Composite(set.Id, row.Id), null, row.Id, row.MemberId, row.ConnectXi, row.ConnectYi, row.ConnectZi,
            row.ConnectXj, row.ConnectYj, row.ConnectZj, set.Id))).ToArray(),
        InputTableKey.JointReleaseSets => document.JointReleaseSets.Select(set => Row(set.Id, null,
            set.Id, set.Name)).ToArray(),
        InputTableKey.NoticePoints => document.NoticePoints.Select(point => Row(point.Id, null,
            point.Id, point.MemberId, point.Distance)).ToArray(),
        InputTableKey.MemberSprings => document.MemberSpringSets.SelectMany(set => set.Rows.Select(row => Row(
            Composite(set.Id, row.Id), null, row.Id, row.MemberId, row.Tx, row.Ty, row.Tz, row.Tr, set.Id))).ToArray(),
        InputTableKey.MemberSpringSets => document.MemberSpringSets.Select(set => Row(set.Id, null,
            set.Id, set.Name)).ToArray(),
        InputTableKey.LoadCases => document.LoadCases.Select(loadCase => Row(loadCase.Id, null,
            loadCase.Id, loadCase.Name, loadCase.Symbol, loadCase.ElementSetId, loadCase.SupportSetId,
            loadCase.MemberSpringSetId, loadCase.JointSetId, loadCase.MovingLoadPitch)).ToArray(),
        InputTableKey.NodalLoads => document.NodalLoads.Select(load => Row(load.Id,
            new SceneEntityKey(SceneEntityKind.NodalLoad, load.Id), load.Id, load.CaseId, load.NodeId,
            load.Fx, load.Fy, load.Fz, load.Mx, load.My, load.Mz)).ToArray(),
        InputTableKey.PrescribedDisplacements => document.PrescribedDisplacements.Select(load => Row(load.Id, null,
            load.Id, load.CaseId, load.NodeId, load.Dx, load.Dy, load.Dz, load.Rx, load.Ry, load.Rz)).ToArray(),
        InputTableKey.MemberLoads => document.MemberLoads.Select(load => Row(load.Id,
            new SceneEntityKey(SceneEntityKind.MemberLoad, load.Id),
            load.Id, load.CaseId, load.MemberId, load.Kind, load.Direction, load.L1, load.L2, load.P1, load.P2)).ToArray(),
        InputTableKey.Define => ReadDerived(document, DerivedResultKind.Define),
        InputTableKey.Combine => ReadDerived(document, DerivedResultKind.Combine),
        InputTableKey.Pickup => ReadDerived(document, DerivedResultKind.Pickup),
        _ => throw new ArgumentOutOfRangeException(nameof(table)),
    };

    private static IReadOnlyList<InputGridRow> ReadSupports(ProjectDocument document)
    {
        List<InputGridRow> rows = document.Supports.Select(support => Row(support.Id,
            new SceneEntityKey(SceneEntityKind.Support, support.Id), support.Id, support.NodeId,
            support.FixX ? 1d : 0d, support.FixY ? 1d : 0d, support.FixZ ? 1d : 0d,
            support.FixRx ? 1d : 0d, support.FixRy ? 1d : 0d, support.FixRz ? 1d : 0d, "1")).ToList();
        rows.AddRange(document.SupportSets.SelectMany(set => set.Rows.Select(row => Row(Composite(set.Id, row.Id), null,
            row.Id, row.NodeId, row.Tx, row.Ty, row.Tz, row.Rx, row.Ry, row.Rz, set.Id))));
        return rows;
    }

    private static IReadOnlyList<InputGridRow> ReadSections(ProjectDocument document)
    {
        List<InputGridRow> rows = document.Sections.Select(section => SectionRow(section.Id, "1", section)).ToList();
        rows.AddRange(document.ElementPropertySets.SelectMany(set =>
            set.Sections.Select(section => SectionRow(Composite(set.Id, section.Id), set.Id, section))));
        return rows;
    }

    private static InputGridRow SectionRow(string stableId, string setId, FrameSectionDefinition section) => Row(
        stableId, null, section.Id, section.Name, section.YoungsModulus, section.PoissonRatio, section.ShearModulus,
        section.Area, section.MomentOfInertiaY, section.MomentOfInertiaZ, section.TorsionConstant,
        section.ThermalExpansionCoefficient, section.Density, section.PanelThickness, setId);

    private static IReadOnlyList<InputGridRow> ReadDerived(ProjectDocument document, DerivedResultKind kind) =>
        document.DerivedResults.Where(result => result.Kind == kind).Select(result => Row(result.Id, null,
            result.Id, result.Name, string.Join(';', result.Terms.Select(term => term.SourceId)),
            string.Join(';', result.Terms.Select(term => term.Factor.ToString("R", CultureInfo.InvariantCulture))))).ToArray();
    private static InputGridRow Row(string id, SceneEntityKey? sceneKey, params object?[] values) => new(id, values, sceneKey);

    private bool ApplyRows(InputTableKey table, IReadOnlyList<InputGridRowUpdate> updates)
    {
        if (_refreshing || _session is null || updates.Count == 0)
        {
            return false;
        }

        return ApplyEdit(session => session.ApplyBatch(batch => ApplyRows(batch, session.Current, table, updates)));
    }

    private static void ApplyRows(
        ProjectDocumentEditBatch batch,
        ProjectDocument document,
        InputTableKey table,
        IReadOnlyList<InputGridRowUpdate> updates)
    {
        switch (table)
        {
            case InputTableKey.ModelSettings:
                Require(updates.Count == 1 && updates[0].Id == "dimension");
                batch.SetDimension(EnumValue<ModelDimension>(updates[0], 0));
                break;
            case InputTableKey.Nodes:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertNode(new ProjectNode(update.Id, Number(update, 1), Number(update, 2), Number(update, 3)));
                break;
            case InputTableKey.Members:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertMember(new ProjectMember(update.Id, CellText(update, 1), CellText(update, 2),
                        OptionalText(update, 3), Number(update, 4), Flag(update, 5)));
                break;
            case InputTableKey.RigidZones:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertRigidZone(new RigidZoneDefinition(
                        update.Id, CellText(update, 1), Number(update, 2), Number(update, 3), CellText(update, 4)));
                break;
            case InputTableKey.ElementPropertySets:
                foreach (InputGridRowUpdate update in updates)
                {
                    ElementPropertySetDefinition set = document.ElementPropertySets.Single(value => value.Id == update.Id);
                    batch.UpsertElementPropertySet(new ElementPropertySetDefinition(set.Id, CellText(update, 1), set.Sections));
                }
                break;
            case InputTableKey.Supports:
                ApplySupportRows(batch, document, updates);
                break;
            case InputTableKey.SupportSets:
                foreach (InputGridRowUpdate update in updates)
                {
                    SupportSetDefinition set = document.SupportSets.Single(value => value.Id == update.Id);
                    batch.UpsertSupportSet(new SupportSetDefinition(set.Id, CellText(update, 1), set.Rows));
                }
                break;
            case InputTableKey.Sections:
                ApplySectionRows(batch, document, updates);
                break;
            case InputTableKey.Panels:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertPanel(new PanelDefinition(update.Id, CellText(update, 1),
                        update.Cells.Skip(2).Where(value => !string.IsNullOrWhiteSpace(value))));
                break;
            case InputTableKey.Joints:
                ApplyJointRows(batch, document, updates);
                break;
            case InputTableKey.JointReleaseSets:
                foreach (InputGridRowUpdate update in updates)
                {
                    JointReleaseSetDefinition set = document.JointReleaseSets.Single(value => value.Id == update.Id);
                    batch.UpsertJointReleaseSet(new JointReleaseSetDefinition(set.Id, CellText(update, 1), set.Rows));
                }
                break;
            case InputTableKey.NoticePoints:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertNoticePoint(new NoticePointDefinition(update.Id, CellText(update, 1), Number(update, 2)));
                break;
            case InputTableKey.MemberSprings:
                ApplySpringRows(batch, document, updates);
                break;
            case InputTableKey.MemberSpringSets:
                foreach (InputGridRowUpdate update in updates)
                {
                    MemberSpringSetDefinition set = document.MemberSpringSets.Single(value => value.Id == update.Id);
                    batch.UpsertMemberSpringSet(new MemberSpringSetDefinition(set.Id, CellText(update, 1), set.Rows));
                }
                break;
            case InputTableKey.LoadCases:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertLoadCase(new LoadCaseDefinition(update.Id, CellText(update, 1), CellText(update, 2),
                        CellText(update, 3), CellText(update, 4), CellText(update, 5), CellText(update, 6), Number(update, 7)));
                break;
            case InputTableKey.NodalLoads:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertNodalLoad(new NodalLoadDefinition(update.Id, CellText(update, 1), CellText(update, 2),
                        Number(update, 3), Number(update, 4), Number(update, 5), Number(update, 6), Number(update, 7), Number(update, 8)));
                break;
            case InputTableKey.PrescribedDisplacements:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertPrescribedDisplacement(new PrescribedDisplacementDefinition(update.Id, CellText(update, 1),
                        CellText(update, 2), Number(update, 3), Number(update, 4), Number(update, 5), Number(update, 6),
                        Number(update, 7), Number(update, 8)));
                break;
            case InputTableKey.MemberLoads:
                foreach (InputGridRowUpdate update in updates)
                    batch.UpsertMemberLoad(new MemberLoadDefinition(update.Id, CellText(update, 1), CellText(update, 2),
                        EnumValue<MemberLoadKind>(update, 3), EnumValue<MemberLoadDirection>(update, 4),
                        Number(update, 5), Number(update, 6), Number(update, 7), Number(update, 8)));
                break;
            case InputTableKey.Define:
                ApplyDerivedRows(batch, updates, DerivedResultKind.Define);
                break;
            case InputTableKey.Combine:
                ApplyDerivedRows(batch, updates, DerivedResultKind.Combine);
                break;
            case InputTableKey.Pickup:
                ApplyDerivedRows(batch, updates, DerivedResultKind.Pickup);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(table));
        }
    }

    private static void ApplySupportRows(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<InputGridRowUpdate> updates)
    {
        Dictionary<string, ProjectSupport> defaults = document.Supports.ToDictionary(row => row.Id, StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, SupportConditionDefinition>> rowsBySet = document.SupportSets.ToDictionary(
            set => set.Id,
            set => set.Rows.ToDictionary(row => row.Id, StringComparer.Ordinal),
            StringComparer.Ordinal);
        HashSet<string> changedSets = [];
        foreach (InputGridRowUpdate update in updates)
        {
            (string originSet, string id) = DecodeIdentity(update.Id);
            string targetSet = CellText(update, 8);
            Require(targetSet == "1" || rowsBySet.ContainsKey(targetSet));
            if (originSet == "1")
                defaults.Remove(id);
            else
            {
                Require(rowsBySet.TryGetValue(originSet, out Dictionary<string, SupportConditionDefinition>? origin));
                origin!.Remove(id);
                changedSets.Add(originSet);
            }

            if (targetSet == "1")
                defaults[id] = new ProjectSupport(id, CellText(update, 1), Number(update, 2) != 0,
                    Number(update, 3) != 0, Number(update, 4) != 0, Number(update, 5) != 0,
                    Number(update, 6) != 0, Number(update, 7) != 0);
            else
            {
                rowsBySet[targetSet][id] = new SupportConditionDefinition(id, CellText(update, 1), Number(update, 2),
                    Number(update, 3), Number(update, 4), Number(update, 5), Number(update, 6), Number(update, 7));
                changedSets.Add(targetSet);
            }
        }

        foreach (ProjectSupport original in document.Supports.Where(row => !defaults.ContainsKey(row.Id)))
            batch.RemoveSupport(original.Id);
        foreach (ProjectSupport row in defaults.Values)
            batch.UpsertSupport(row);
        foreach (string setId in changedSets.Order(StringComparer.Ordinal))
        {
            SupportSetDefinition set = document.SupportSets.Single(candidate => candidate.Id == setId);
            batch.UpsertSupportSet(new SupportSetDefinition(set.Id, set.Name, rowsBySet[setId].Values));
        }
    }

    private static void ApplySectionRows(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<InputGridRowUpdate> updates)
    {
        Dictionary<string, FrameSectionDefinition> defaults = document.Sections.ToDictionary(row => row.Id, StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, FrameSectionDefinition>> rowsBySet = document.ElementPropertySets.ToDictionary(
            set => set.Id,
            set => set.Sections.ToDictionary(row => row.Id, StringComparer.Ordinal),
            StringComparer.Ordinal);
        HashSet<string> changedSets = [];
        foreach (InputGridRowUpdate update in updates)
        {
            (string originSet, string id) = DecodeIdentity(update.Id);
            string targetSet = CellText(update, 12);
            Require(targetSet == "1" || rowsBySet.ContainsKey(targetSet));
            if (originSet == "1")
                defaults.Remove(id);
            else
            {
                Require(rowsBySet.TryGetValue(originSet, out Dictionary<string, FrameSectionDefinition>? origin));
                origin!.Remove(id);
                changedSets.Add(originSet);
            }

            FrameSectionDefinition section = ParseSection(update);
            if (targetSet == "1")
                defaults[id] = section;
            else
            {
                rowsBySet[targetSet][id] = section;
                changedSets.Add(targetSet);
            }
        }

        foreach (FrameSectionDefinition original in document.Sections.Where(row => !defaults.ContainsKey(row.Id)))
            batch.RemoveSection(original.Id);
        foreach (FrameSectionDefinition row in defaults.Values)
            batch.UpsertSection(row);
        foreach (string setId in changedSets.Order(StringComparer.Ordinal))
        {
            ElementPropertySetDefinition set = document.ElementPropertySets.Single(candidate => candidate.Id == setId);
            batch.UpsertElementPropertySet(new ElementPropertySetDefinition(set.Id, set.Name, rowsBySet[setId].Values));
        }
    }

    private static FrameSectionDefinition ParseSection(InputGridRowUpdate update) => new(
        CellText(update, 0), CellText(update, 1), Number(update, 2), Number(update, 3), Number(update, 4), Number(update, 5),
        Number(update, 6), Number(update, 7), Number(update, 8), Number(update, 9), Number(update, 10), Number(update, 11));

    private static void ApplyJointRows(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<InputGridRowUpdate> updates)
    {
        Dictionary<string, Dictionary<string, JointReleaseDefinition>> rowsBySet = document.JointReleaseSets.ToDictionary(
            set => set.Id,
            set => set.Rows.ToDictionary(row => row.Id, StringComparer.Ordinal),
            StringComparer.Ordinal);
        HashSet<string> changedSets = [];
        foreach (InputGridRowUpdate update in updates)
        {
            (string originSet, string id) = DecodeIdentity(update.Id, requireComposite: true);
            string targetSet = CellText(update, 8);
            Require(rowsBySet.TryGetValue(originSet, out Dictionary<string, JointReleaseDefinition>? origin));
            Require(rowsBySet.ContainsKey(targetSet));
            origin!.Remove(id);
            rowsBySet[targetSet][id] = new JointReleaseDefinition(id, CellText(update, 1), Flag(update, 2), Flag(update, 3),
                Flag(update, 4), Flag(update, 5), Flag(update, 6), Flag(update, 7));
            changedSets.Add(originSet);
            changedSets.Add(targetSet);
        }

        foreach (string setId in changedSets.Order(StringComparer.Ordinal))
        {
            JointReleaseSetDefinition set = document.JointReleaseSets.Single(candidate => candidate.Id == setId);
            batch.UpsertJointReleaseSet(new JointReleaseSetDefinition(set.Id, set.Name, rowsBySet[setId].Values));
        }
    }

    private static void ApplySpringRows(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<InputGridRowUpdate> updates)
    {
        Dictionary<string, Dictionary<string, MemberSpringDefinition>> rowsBySet = document.MemberSpringSets.ToDictionary(
            set => set.Id,
            set => set.Rows.ToDictionary(row => row.Id, StringComparer.Ordinal),
            StringComparer.Ordinal);
        HashSet<string> changedSets = [];
        foreach (InputGridRowUpdate update in updates)
        {
            (string originSet, string id) = DecodeIdentity(update.Id, requireComposite: true);
            string targetSet = CellText(update, 6);
            Require(rowsBySet.TryGetValue(originSet, out Dictionary<string, MemberSpringDefinition>? origin));
            Require(rowsBySet.ContainsKey(targetSet));
            origin!.Remove(id);
            rowsBySet[targetSet][id] = new MemberSpringDefinition(id, CellText(update, 1), Number(update, 2),
                Number(update, 3), Number(update, 4), Number(update, 5));
            changedSets.Add(originSet);
            changedSets.Add(targetSet);
        }

        foreach (string setId in changedSets.Order(StringComparer.Ordinal))
        {
            MemberSpringSetDefinition set = document.MemberSpringSets.Single(candidate => candidate.Id == setId);
            batch.UpsertMemberSpringSet(new MemberSpringSetDefinition(set.Id, set.Name, rowsBySet[setId].Values));
        }
    }

    private static void ApplyDerivedRows(ProjectDocumentEditBatch batch, IReadOnlyList<InputGridRowUpdate> updates, DerivedResultKind kind)
    {
        foreach (InputGridRowUpdate update in updates)
        {
            string[] sources = SplitList(CellText(update, 2));
            string[] factors = SplitList(CellText(update, 3));
            if (sources.Length == 0 || sources.Length != factors.Length)
                throw new FormatException("Source and factor counts must match.");
            DerivedResultTerm[] terms = sources.Select((source, index) => new DerivedResultTerm(
                source, double.Parse(factors[index], NumberStyles.Float, CultureInfo.InvariantCulture))).ToArray();
            batch.UpsertDerivedResult(new DerivedResultDefinition(update.Id, CellText(update, 1), kind, terms));
        }
    }

    private bool InsertRows(InputTableKey table, int count)
    {
        if (_session is null)
            return false;
        return ApplyEdit(
            session => session.ApplyBatch(batch => ProjectDocumentInsertPolicy.Apply(batch, session.Current, table, count)),
            "EditorInsertRejected");
    }

    private bool DeleteRows(InputTableKey table, IReadOnlyList<string> ids)
    {
        if (_session is null || ids.Count == 0)
            return false;
        return ApplyEdit(
            session => session.ApplyBatch(batch => DeleteRows(batch, session.Current, table, ids)),
            "EditorDeleteRejected");
    }

    private static void DeleteRows(ProjectDocumentEditBatch batch, ProjectDocument document, InputTableKey table, IReadOnlyList<string> ids)
    {
        switch (table)
        {
            case InputTableKey.ModelSettings: throw new InvalidOperationException("Model settings cannot be deleted.");
            case InputTableKey.Nodes: foreach (string id in ids) batch.RemoveNode(id); break;
            case InputTableKey.Members: foreach (string id in ids) batch.RemoveMember(id); break;
            case InputTableKey.RigidZones: foreach (string id in ids) batch.RemoveRigidZone(id); break;
            case InputTableKey.ElementPropertySets: foreach (string id in ids) batch.RemoveElementPropertySet(id); break;
            case InputTableKey.Supports: DeleteSupportRows(batch, document, ids); break;
            case InputTableKey.SupportSets: foreach (string id in ids) batch.RemoveSupportSet(id); break;
            case InputTableKey.Sections: DeleteSectionRows(batch, document, ids); break;
            case InputTableKey.Panels: foreach (string id in ids) batch.RemovePanel(id); break;
            case InputTableKey.Joints: DeleteJointRows(batch, document, ids); break;
            case InputTableKey.JointReleaseSets: foreach (string id in ids) batch.RemoveJointReleaseSet(id); break;
            case InputTableKey.NoticePoints: foreach (string id in ids) batch.RemoveNoticePoint(id); break;
            case InputTableKey.MemberSprings: DeleteSpringRows(batch, document, ids); break;
            case InputTableKey.MemberSpringSets: foreach (string id in ids) batch.RemoveMemberSpringSet(id); break;
            case InputTableKey.LoadCases: foreach (string id in ids) batch.RemoveLoadCase(id); break;
            case InputTableKey.NodalLoads: foreach (string id in ids) batch.RemoveNodalLoad(id); break;
            case InputTableKey.PrescribedDisplacements: foreach (string id in ids) batch.RemovePrescribedDisplacement(id); break;
            case InputTableKey.MemberLoads: foreach (string id in ids) batch.RemoveMemberLoad(id); break;
            case InputTableKey.Define:
            case InputTableKey.Combine:
            case InputTableKey.Pickup: foreach (string id in ids) batch.RemoveDerivedResult(id); break;
            default: throw new ArgumentOutOfRangeException(nameof(table));
        }
    }

    private static void DeleteSupportRows(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        foreach (string id in ids.Where(id => !id.Contains(CompositeSeparator))) batch.RemoveSupport(id);
        foreach (IGrouping<string, (string Set, string Row)> group in CompositeIds(ids).GroupBy(value => value.Set, StringComparer.Ordinal))
        {
            SupportSetDefinition set = document.SupportSets.Single(candidate => candidate.Id == group.Key);
            HashSet<string> removed = group.Select(value => value.Row).ToHashSet(StringComparer.Ordinal);
            batch.UpsertSupportSet(new SupportSetDefinition(set.Id, set.Name, set.Rows.Where(row => !removed.Contains(row.Id))));
        }
    }

    private static void DeleteSectionRows(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        foreach (string id in ids.Where(id => !id.Contains(CompositeSeparator))) batch.RemoveSection(id);
        foreach (IGrouping<string, (string Set, string Row)> group in CompositeIds(ids).GroupBy(value => value.Set, StringComparer.Ordinal))
        {
            ElementPropertySetDefinition set = document.ElementPropertySets.Single(candidate => candidate.Id == group.Key);
            HashSet<string> removed = group.Select(value => value.Row).ToHashSet(StringComparer.Ordinal);
            batch.UpsertElementPropertySet(new ElementPropertySetDefinition(set.Id, set.Name,
                set.Sections.Where(row => !removed.Contains(row.Id))));
        }
    }

    private static void DeleteJointRows(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        foreach (IGrouping<string, (string Set, string Row)> group in CompositeIds(ids).GroupBy(value => value.Set, StringComparer.Ordinal))
        {
            JointReleaseSetDefinition set = document.JointReleaseSets.Single(candidate => candidate.Id == group.Key);
            HashSet<string> removed = group.Select(value => value.Row).ToHashSet(StringComparer.Ordinal);
            batch.UpsertJointReleaseSet(new JointReleaseSetDefinition(set.Id, set.Name,
                set.Rows.Where(row => !removed.Contains(row.Id))));
        }
    }

    private static void DeleteSpringRows(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        foreach (IGrouping<string, (string Set, string Row)> group in CompositeIds(ids).GroupBy(value => value.Set, StringComparer.Ordinal))
        {
            MemberSpringSetDefinition set = document.MemberSpringSets.Single(candidate => candidate.Id == group.Key);
            HashSet<string> removed = group.Select(value => value.Row).ToHashSet(StringComparer.Ordinal);
            batch.UpsertMemberSpringSet(new MemberSpringSetDefinition(set.Id, set.Name,
                set.Rows.Where(row => !removed.Contains(row.Id))));
        }
    }

    private bool ApplyEdit(Func<ProjectDocumentEditSession, bool> edit, string failureResourceKey = "EditorInvalidValue")
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(edit);
        if (_session is null)
            return false;
        try
        {
            if (!edit(_session))
                return false;
            RefreshFromSession();
            DocumentEdited?.Invoke(this, new DocumentEditedEventArgs(_session.Current));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            RefreshFromSession();
            ValidationFailed?.Invoke(this, new EditorValidationEventArgs(Localization[failureResourceKey]));
            return false;
        }
    }

    private void OnGridSelectionChanged(SceneEntityKey? selection)
    {
        if (_refreshing || selection is not { } key)
            return;
        _selection.Text = $"{key.Kind}: {key.Id}";
        SelectionChanged?.Invoke(this, new ViewportSelectionChangedEventArgs(key, ViewportSelectionOrigin.Table));
    }

    private void OnGridFailure(InputGridFailure failure)
    {
        string resourceKey = failure switch
        {
            InputGridFailure.ClipboardUnavailable => "EditorClipboardUnavailable",
            InputGridFailure.ClipboardTooLarge => "EditorClipboardTooLarge",
            InputGridFailure.ClipboardInvalidShape => "EditorClipboardInvalidShape",
            InputGridFailure.ReadOnlyTarget => "EditorReadOnlyTarget",
            InputGridFailure.DeleteRejected => "EditorDeleteRejected",
            _ => "EditorInvalidValue",
        };
        ValidationFailed?.Invoke(this, new EditorValidationEventArgs(Localization[resourceKey]));
    }

    private void OnUndo(object? sender, EventArgs eventArgs) => Undo();
    private void OnRedo(object? sender, EventArgs eventArgs) => Redo();
    private void UpdateCommandState() { _undo.Enabled = CanUndo; _redo.Enabled = CanRedo; }
    private DataGridView Grid(InputTableKey table) => _controllers[table].Grid;
    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != _creatingThreadId)
            throw new InvalidOperationException("Editor operations must run on the creating thread.");
    }

    private static string CellText(InputGridRowUpdate update, int index) => update.Cells[index].Trim();
    private static string? OptionalText(InputGridRowUpdate update, int index) => CellText(update, index) is { Length: > 0 } value ? value : null;
    private static double Number(InputGridRowUpdate update, int index) =>
        double.Parse(CellText(update, index), NumberStyles.Float, CultureInfo.InvariantCulture);
    private static bool Flag(InputGridRowUpdate update, int index) =>
        bool.TryParse(CellText(update, index), out bool value) ? value : Number(update, index) != 0;
    private static T EnumValue<T>(InputGridRowUpdate update, int index) where T : struct, Enum =>
        Enum.Parse<T>(CellText(update, index), true);
    private static string[] SplitList(string value) => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string NodeAt(PanelDefinition panel, int index) => index < panel.NodeIds.Count ? panel.NodeIds[index] : string.Empty;
    private static string Composite(string setId, string rowId) => $"{setId}{CompositeSeparator}{rowId}";
    private static (string Set, string Row) DecodeIdentity(string id, bool requireComposite = false)
    {
        int separator = id.IndexOf(CompositeSeparator);
        if (separator < 0)
        {
            if (requireComposite)
                throw new InvalidOperationException("The set-owned row has no typed set identity.");
            return ("1", id);
        }

        return (id[..separator], id[(separator + 1)..]);
    }

    private static IEnumerable<(string Set, string Row)> CompositeIds(IEnumerable<string> ids)
    {
        foreach (string id in ids)
        {
            int separator = id.IndexOf(CompositeSeparator);
            if (separator >= 0)
                yield return (id[..separator], id[(separator + 1)..]);
        }
    }

    private static void Require(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("The current document does not contain the references required for this row.");
    }
}
