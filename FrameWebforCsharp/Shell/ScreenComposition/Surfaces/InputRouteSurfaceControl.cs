using FrameWebforCsharp.Resources;
using FrameWebforCsharp.Shell.Contents;
using FrameWebforCsharp.Shell.Editing;
using FrameWebforCsharp.Shell.ScreenComposition.Core;

namespace FrameWebforCsharp.Shell.ScreenComposition.Surfaces;

public sealed class InputRouteSurfaceControl : FloatingCardSurface, IRouteControlPanelSurface
{
    private static readonly Color TableEmpty = Color.FromArgb(84, 88, 94);
    private static readonly Color TableGrid = Color.FromArgb(99, 103, 108);
    private static readonly Color TableRowDark = Color.FromArgb(67, 69, 71);
    private static readonly Color TableRowLight = Color.FromArgb(86, 88, 92);
    private const int ReferenceSheetFooterHeight = 64;
    private const int NodesUnitHeaderHeight = 18;
    private const int NodesColumnHeaderHeight = 30;
    private const int SupportsHeaderTierHeight = 50;

    private static readonly HashSet<string> TwoDimensionalHiddenFields =
    [
        "z", "tz", "rx", "ry", "dz", "ax", "ay", "fz", "mx", "my", "yi", "yj", "xi", "xj",
        "rotation", "g", "iy", "j", "tr",
    ];

    private static readonly IReadOnlyDictionary<InputTableKey, string[]> AngularFieldOrder =
        new Dictionary<InputTableKey, string[]>
        {
            [InputTableKey.ModelSettings] = ["dimension"],
            [InputTableKey.Nodes] = ["x", "y", "z"],
            [InputTableKey.Members] = ["node_i", "node_j", "section", "rotation"],
            [InputTableKey.RigidZones] = ["member", "section", "i_length", "j_length"],
            [InputTableKey.ElementPropertySets] = ["name"],
            [InputTableKey.Supports] = ["node", "tx", "ty", "tz", "rx", "ry", "rz"],
            [InputTableKey.SupportSets] = ["name"],
            [InputTableKey.Sections] = ["e", "g", "xp", "area", "j", "iy", "iz", "nu"],
            [InputTableKey.Panels] = ["section", "node1", "node2", "node3", "node4"],
            [InputTableKey.Joints] = ["member", "xi", "yi", "zi", "xj", "yj", "zj"],
            [InputTableKey.JointReleaseSets] = ["name"],
            [InputTableKey.NoticePoints] = ["member", "distance"],
            [InputTableKey.MemberSprings] = ["member", "tx", "ty", "tz", "tr"],
            [InputTableKey.MemberSpringSets] = ["name"],
            [InputTableKey.LoadCases] = ["symbol", "name", "support", "element", "spring", "joint", "pitch"],
            [InputTableKey.NodalLoads] = ["node", "x", "y", "z", "rx", "ry", "rz"],
            [InputTableKey.PrescribedDisplacements] = ["node", "dx", "dy", "dz", "rx", "ry", "rz"],
            [InputTableKey.MemberLoads] = ["member", "kind", "direction", "l1", "l2", "p1", "p2"],
            [InputTableKey.Define] = ["sources", "factors"],
            [InputTableKey.Combine] = ["sources", "factors", "name"],
            [InputTableKey.Pickup] = ["sources", "factors", "name"],
        };

    private readonly LocalizationService _localization;
    private readonly EditorContent _editor;
    private readonly Panel _gridHost = new()
    {
        BackColor = SurfaceVisuals.Card,
        Dock = DockStyle.Fill,
        Name = "InputRouteGridHost",
    };
    private readonly FlowLayoutPanel _tableControls = new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        BackColor = SurfaceVisuals.Header,
        Dock = DockStyle.Top,
        FlowDirection = FlowDirection.LeftToRight,
        Name = "InputRouteTableControls",
        Padding = new Padding(4, 3, 4, 3),
        Visible = false,
        WrapContents = true,
    };
    private readonly Dictionary<InputTableKey, Button> _tableButtons = [];
    private InputRouteSurfaceDefinition? _definition;
    private InputTableKey? _activeTable;
    private ScreenRouteState? _state;
    private bool _controlPanelOpen;
    private bool _disposed;

    public InputRouteSurfaceControl(LocalizationService localization, EditorContent editor)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Name = "InputRouteSurface";
        BodyPanel.Controls.Add(_gridHost);
        BodyPanel.Controls.Add(_tableControls);
        _tableControls.BringToFront();
        _localization.CultureChanged += OnCultureChanged;
    }

    public ScreenRouteId RouteKey =>
        _definition?.Route ?? throw new InvalidOperationException("No input route has been applied.");

    public IReadOnlyList<InputTableKey> VisibleTableKeys => _definition?.Tables ?? [];

    public InputTableKey ActiveTableKey =>
        _activeTable ?? throw new InvalidOperationException("No input table is active.");

    public DataGridView PrimaryGrid => _editor.Tables[ActiveTableKey];

    public Control TableControls => _tableControls;

    public IReadOnlyDictionary<InputTableKey, Button> TableButtons => _tableButtons;

    public bool IsTableControlPanelOpen => _controlPanelOpen && _tableButtons.Count > 1;

    public bool IsThreeDimensional => _state?.Dimension == ViewDimension.ThreeDimensional;

    public IReadOnlyList<string> VisibleFieldIds
    {
        get
        {
            if (_activeTable is not InputTableKey table)
            {
                return [];
            }

            InputTableDescriptor descriptor = _editor.Descriptors[table];
            return descriptor.Columns
                .Select((column, index) => (Column: column, Index: index))
                .Where(item => item.Index < PrimaryGrid.Columns.Count && PrimaryGrid.Columns[item.Index].Visible)
                .OrderBy(item => PrimaryGrid.Columns[item.Index].DisplayIndex)
                .Select(item => item.Column.Id)
                .ToArray();
        }
    }

    public void ApplyState(ScreenRouteState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Route is not ScreenRouteId route || AngularScreenManifest.IsResultRoute(route))
        {
            throw new ArgumentException("An input route state is required.", nameof(state));
        }

        InputRouteSurfaceDefinition definition = FrameWebSurfaceCatalog.GetInput(route);
        if (definition.ThreeDimensionalOnly && state.Dimension != ViewDimension.ThreeDimensional)
        {
            throw new ArgumentException($"Route '{route}' is available only in 3D.", nameof(state));
        }

        bool routeChanged = _definition?.Route != definition.Route;
        _definition = definition;
        _state = state;
        if (routeChanged)
        {
            RebuildTableControls();
            ShowTable(definition.Tables[0]);
        }
        else if (_activeTable is InputTableKey active)
        {
            ApplyDimension(active);
        }

        ApplyLocalization();
    }

    public void SetControlPanelOpen(bool isOpen)
    {
        _controlPanelOpen = isOpen;
        _tableControls.Visible = isOpen && _tableButtons.Count > 1;
    }

    public void ShowTable(InputTableKey table)
    {
        if (_definition is null || !_definition.Tables.Contains(table))
        {
            throw new ArgumentOutOfRangeException(nameof(table), table, "The table does not belong to the active route.");
        }

        if (_activeTable is InputTableKey previous && previous != table)
        {
            DetachSurfacePainting(_editor.Tables[previous]);
            _editor.ParkTable(previous);
        }

        _activeTable = table;
        _editor.AttachTable(table, _gridHost);
        ApplyAngularGridStyle(_editor.Tables[table]);
        ApplyDimension(table);
        ApplyTableButtonState();
    }

    private void ApplyAngularGridStyle(DataGridView grid)
    {
        grid.BackgroundColor = TableEmpty;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.BackColor = TableRowLight;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = TableRowLight;
        grid.ColumnHeadersHeight = 31;
        grid.EnableHeadersVisualStyles = false;
        grid.GridColor = TableGrid;
        grid.RowHeadersDefaultCellStyle.BackColor = TableRowDark;
        grid.RowHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.RowHeadersDefaultCellStyle.SelectionBackColor = SurfaceVisuals.Accent;
        grid.RowsDefaultCellStyle.BackColor = TableRowDark;
        grid.RowsDefaultCellStyle.ForeColor = Color.White;
        grid.RowsDefaultCellStyle.SelectionBackColor = SurfaceVisuals.Accent;
        grid.RowsDefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = TableRowLight;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = SurfaceVisuals.Accent;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
        grid.ScrollBars = ScrollBars.None;
        grid.TopLeftHeaderCell.Style.BackColor = TableEmpty;
        grid.TopLeftHeaderCell.Style.SelectionBackColor = TableEmpty;
        grid.Paint -= DrawSheetContinuation;
        grid.Paint += DrawSheetContinuation;
        grid.Paint -= DrawProjectedTableOverlay;
        grid.Paint += DrawProjectedTableOverlay;
        grid.RowPostPaint -= DrawRowNumber;
        grid.RowPostPaint += DrawRowNumber;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _localization.CultureChanged -= OnCultureChanged;
            ClearTableControls();
            if (_activeTable is InputTableKey active && !_editor.IsDisposed)
            {
                DetachSurfacePainting(_editor.Tables[active]);
                _editor.ParkTable(active);
            }
        }

        base.Dispose(disposing);
    }

    private void ApplyDimension(InputTableKey table)
    {
        bool isThreeDimensional = _state?.Dimension == ViewDimension.ThreeDimensional;
        InputTableDescriptor descriptor = _editor.Descriptors[table];
        DataGridView grid = _editor.Tables[table];
        string[] angularOrder = AngularFieldOrder.TryGetValue(table, out string[]? fields)
            ? fields
            : throw new InvalidOperationException($"No Angular field projection is defined for '{table}'.");
        HashSet<string> projectedFields = angularOrder.ToHashSet(StringComparer.Ordinal);
        bool[] visibility = descriptor.Columns
            .Select(column => projectedFields.Contains(column.Id) &&
                (isThreeDimensional || !TwoDimensionalHiddenFields.Contains(column.Id)))
            .ToArray();
        if (grid.CurrentCell is DataGridViewCell current &&
            current.ColumnIndex < visibility.Length &&
            !visibility[current.ColumnIndex])
        {
            int firstVisible = Array.FindIndex(visibility, value => value);
            if (firstVisible >= 0)
            {
                grid.CurrentCell = grid.Rows[current.RowIndex].Cells[firstVisible];
            }
        }

        for (int index = 0; index < descriptor.Columns.Count && index < grid.Columns.Count; index++)
        {
            grid.Columns[index].Visible = visibility[index];
        }

        ApplyReferenceColumnLayout(table, grid);
        grid.Invalidate();
    }

    private void ApplyLocalization()
    {
        if (_definition is null)
        {
            return;
        }

        CardTitle = string.Empty;
        CardPanel.AccessibleName = _localization[_definition.TitleResourceKey];
        if (_activeTable is InputTableKey active)
        {
            ApplyDimension(active);
        }

        foreach ((InputTableKey table, Button button) in _tableButtons)
        {
            button.Text = _localization[_editor.Descriptors[table].TitleResourceKey];
            button.AccessibleName = button.Text;
        }
    }

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();

    private void RebuildTableControls()
    {
        ClearTableControls();
        if (_definition is null)
        {
            return;
        }

        foreach (InputTableKey table in _definition.Tables)
        {
            Button button = new()
            {
                AutoSize = true,
                BackColor = SurfaceVisuals.Header,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Margin = new Padding(2),
                MinimumSize = new Size(112, 28),
                Name = $"InputRoute{table}Button",
                Padding = new Padding(8, 2, 8, 2),
                Tag = table,
                UseVisualStyleBackColor = false,
            };
            button.FlatAppearance.BorderColor = SurfaceVisuals.Border;
            button.FlatAppearance.MouseOverBackColor = SurfaceVisuals.AccentHover;
            button.Click += OnTableButtonClick;
            _tableButtons.Add(table, button);
            _tableControls.Controls.Add(button);
        }

        SetControlPanelOpen(_controlPanelOpen);
    }

    private void ClearTableControls()
    {
        foreach (Button button in _tableButtons.Values)
        {
            button.Click -= OnTableButtonClick;
            button.Dispose();
        }

        _tableButtons.Clear();
        _tableControls.Controls.Clear();
        _tableControls.Visible = false;
    }

    private void OnTableButtonClick(object? sender, EventArgs eventArgs)
    {
        if (sender is Button { Tag: InputTableKey table })
        {
            ShowTable(table);
        }
    }

    private void ApplyTableButtonState()
    {
        foreach ((InputTableKey table, Button button) in _tableButtons)
        {
            bool selected = table == _activeTable;
            button.BackColor = selected ? SurfaceVisuals.Accent : SurfaceVisuals.Header;
            button.FlatAppearance.BorderSize = selected ? 0 : 1;
        }
    }

    private void ApplyReferenceColumnLayout(InputTableKey table, DataGridView grid)
    {
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.RowTemplate.Height = 27;
        grid.RowHeadersWidth = 45;
        grid.RowHeadersVisible = table is InputTableKey.Nodes or InputTableKey.Sections;
        grid.AccessibleDescription = null;

        string[] order = AngularFieldOrder[table];
        for (int displayIndex = 0; displayIndex < order.Length; displayIndex++)
        {
            if (grid.Columns.Contains(order[displayIndex]))
            {
                grid.Columns[order[displayIndex]].DisplayIndex = displayIndex;
            }
        }

        if (table == InputTableKey.Nodes)
        {
            grid.ColumnHeadersHeight = NodesUnitHeaderHeight + NodesColumnHeaderHeight;
            SetWidths(grid, 90, "x", "y", "z");
            SetHeader(grid, "x", "X");
            SetHeader(grid, "y", "Y");
            SetHeader(grid, "z", "Z");
        }
        else if (table == InputTableKey.Sections)
        {
            grid.ColumnHeadersHeight = 85;
            SetWidth(grid, "e", 120);
            SetWidth(grid, "g", 130);
            SetWidths(grid, 100, "xp", "area", "j", "iy", "iz", "name");
            SetHeader(grid, "e", $"{_localization["EditorYoungsModulus"]}\nE(kN/m2)");
            SetHeader(grid, "g", $"{_localization["EditorShearModulus"]}\nG(kN/m2)");
            SetHeader(grid, "xp", _localization["EditorThermalExpansion"]);
            SetHeader(grid, "area", $"{_localization["EditorArea"]}\nA(m2)");
            SetHeader(grid, "j", $"{_localization["EditorTorsion"]}\nJ(m4)");
            SetHeader(grid, "iy", $"{_localization["EditorIy"]}\nIy(m4)");
            SetHeader(grid, "iz", $"{_localization["EditorIz"]}\nIz(m4)");
            SetHeader(grid, "nu", _localization["EditorPoissonRatio"]);
        }
        else if (table == InputTableKey.Supports)
        {
            grid.ColumnHeadersHeight = SupportsHeaderTierHeight * 2;
            SetWidth(grid, "node", 50);
            SetWidths(grid, 100, "tx", "ty", "tz", "rx", "ry", "rz");
            SetHeader(grid, "node", _localization["SurfaceSupportNumber"]);
            SetHeader(grid, "tx", _localization["SurfaceSupportXDirection"]);
            SetHeader(grid, "ty", _localization["SurfaceSupportYDirection"]);
            SetHeader(grid, "tz", _localization["SurfaceSupportZDirection"]);
            SetHeader(grid, "rx", _localization["SurfaceSupportXRotation"]);
            SetHeader(grid, "ry", _localization["SurfaceSupportYRotation"]);
            SetHeader(
                grid,
                "rz",
                _state?.Dimension == ViewDimension.ThreeDimensional
                    ? _localization["SurfaceSupportZRotation"]
                    : _localization["SurfaceSupportRotationalStiffnessUnit"]);
            grid.AccessibleDescription = string.Join(
                " | ",
                _localization["SurfaceSupportNodeGroup"],
                _localization["SurfaceSupportTranslationGroup"],
                _localization["SurfaceSupportRotationGroup"]);
        }
    }

    private static void SetWidths(DataGridView grid, int width, params string[] columnNames)
    {
        foreach (string columnName in columnNames)
        {
            SetWidth(grid, columnName, width);
        }
    }

    private static void SetWidth(DataGridView grid, string columnName, int width)
    {
        if (grid.Columns.Contains(columnName))
        {
            grid.Columns[columnName].Width = width;
        }
    }

    private static void SetHeader(DataGridView grid, string columnName, string text)
    {
        if (grid.Columns.Contains(columnName))
        {
            grid.Columns[columnName].HeaderText = text;
        }
    }

    private static void DrawRowNumber(object? sender, DataGridViewRowPostPaintEventArgs eventArgs)
    {
        if (sender is not DataGridView { RowHeadersVisible: true } grid)
        {
            return;
        }

        Rectangle bounds = new(0, eventArgs.RowBounds.Top, grid.RowHeadersWidth, eventArgs.RowBounds.Height);
        Color rowColor = eventArgs.RowIndex % 2 == 0 ? TableRowDark : TableRowLight;
        using (SolidBrush rowBrush = new(rowColor))
        using (Pen gridPen = new(TableGrid))
        {
            eventArgs.Graphics.FillRectangle(rowBrush, bounds);
            eventArgs.Graphics.DrawLine(gridPen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
            eventArgs.Graphics.DrawLine(gridPen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }

        TextRenderer.DrawText(
            eventArgs.Graphics,
            (eventArgs.RowIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            grid.Font,
            bounds,
            Color.White,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void DrawSheetContinuation(object? sender, PaintEventArgs eventArgs)
    {
        if (sender is not DataGridView grid || grid.ClientSize.Width <= 0 || grid.ClientSize.Height <= 0)
        {
            return;
        }

        int rowHeight = Math.Max(1, grid.RowTemplate.Height);
        int top = grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0;
        if (grid.Rows.Count > 0)
        {
            Rectangle last = grid.GetRowDisplayRectangle(grid.Rows.Count - 1, cutOverflow: true);
            top = Math.Max(top, last.Bottom);
        }

        int logicalRow = grid.Rows.Count;
        int sheetBottom = Math.Max(
            grid.ColumnHeadersHeight,
            grid.ClientSize.Height - ReferenceSheetFooterHeight);
        int maximumRows = _activeTable == InputTableKey.Sections ? 20 : int.MaxValue;
        using Pen gridPen = new(TableGrid);
        while (logicalRow < maximumRows && top < sheetBottom)
        {
            int paintedHeight = Math.Min(rowHeight, sheetBottom - top);
            Rectangle rowBounds = new(0, top, grid.ClientSize.Width, paintedHeight);
            Color rowColor = logicalRow % 2 == 0 ? TableRowDark : TableRowLight;
            using (SolidBrush rowBrush = new(rowColor))
            {
                eventArgs.Graphics.FillRectangle(rowBrush, rowBounds);
            }

            int cellsLeft = 0;
            if (grid.RowHeadersVisible)
            {
                cellsLeft = grid.RowHeadersWidth;
                using SolidBrush headerBrush = new(rowColor);
                eventArgs.Graphics.FillRectangle(headerBrush, 0, top, cellsLeft, rowHeight);
                TextRenderer.DrawText(
                    eventArgs.Graphics,
                    (logicalRow + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    grid.Font,
                    new Rectangle(0, top, cellsLeft - 5, rowHeight),
                    Color.White,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                eventArgs.Graphics.DrawLine(gridPen, cellsLeft, top, cellsLeft, top + paintedHeight);
            }

            int columnRight = cellsLeft;
            foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>()
                         .Where(column => column.Visible)
                         .OrderBy(column => column.DisplayIndex))
            {
                columnRight += column.Width;
                eventArgs.Graphics.DrawLine(
                    gridPen,
                    columnRight - 1,
                    top,
                    columnRight - 1,
                    top + paintedHeight);
            }

            eventArgs.Graphics.DrawLine(
                gridPen,
                cellsLeft,
                top + paintedHeight - 1,
                grid.ClientSize.Width,
                top + paintedHeight - 1);
            top += paintedHeight;
            logicalRow++;
        }

        if (top < grid.ClientSize.Height)
        {
            using SolidBrush emptyBrush = new(TableEmpty);
            eventArgs.Graphics.FillRectangle(emptyBrush, 0, top, grid.ClientSize.Width, grid.ClientSize.Height - top);
        }
    }

    private void DrawProjectedTableOverlay(object? sender, PaintEventArgs eventArgs)
    {
        if (sender is not DataGridView grid || grid.ClientSize.Width <= 0 || grid.ClientSize.Height <= 0)
        {
            return;
        }

        int projectedRight = GetProjectedTableRight(grid);
        using (SolidBrush emptyBrush = new(TableEmpty))
        {
            if (projectedRight < grid.ClientSize.Width)
            {
                eventArgs.Graphics.FillRectangle(
                    emptyBrush,
                    projectedRight,
                    0,
                    grid.ClientSize.Width - projectedRight,
                    grid.ClientSize.Height);
            }

            int sheetBottom = Math.Max(
                grid.ColumnHeadersHeight,
                grid.ClientSize.Height - ReferenceSheetFooterHeight);
            if (sheetBottom < grid.ClientSize.Height)
            {
                eventArgs.Graphics.FillRectangle(
                    emptyBrush,
                    0,
                    sheetBottom,
                    grid.ClientSize.Width,
                    grid.ClientSize.Height - sheetBottom);
            }
        }

        if (_activeTable == InputTableKey.Nodes)
        {
            DrawNodesHeader(grid, eventArgs.Graphics, projectedRight);
        }
        else if (_activeTable == InputTableKey.Supports)
        {
            DrawSupportsHeader(grid, eventArgs.Graphics, projectedRight);
        }
    }

    private static int GetProjectedTableRight(DataGridView grid)
    {
        int right = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
        foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>()
                     .Where(column => column.Visible)
                     .OrderBy(column => column.DisplayIndex))
        {
            right += column.Width;
        }

        return Math.Clamp(right, 0, grid.ClientSize.Width);
    }

    private static IReadOnlyDictionary<string, Rectangle> GetProjectedColumnBounds(DataGridView grid)
    {
        Dictionary<string, Rectangle> result = new(StringComparer.Ordinal);
        int left = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
        foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>()
                     .Where(column => column.Visible)
                     .OrderBy(column => column.DisplayIndex))
        {
            result[column.Name] = new Rectangle(left, 0, column.Width, grid.ColumnHeadersHeight);
            left += column.Width;
        }

        return result;
    }

    private static void DrawNodesHeader(DataGridView grid, Graphics graphics, int projectedRight)
    {
        Rectangle header = new(0, 0, projectedRight, grid.ColumnHeadersHeight);
        using SolidBrush emptyBrush = new(TableEmpty);
        using SolidBrush headerBrush = new(TableRowLight);
        using Pen gridPen = new(TableGrid);
        graphics.FillRectangle(emptyBrush, header);

        Rectangle lowerHeader = new(
            0,
            NodesUnitHeaderHeight,
            projectedRight,
            NodesColumnHeaderHeight);
        graphics.FillRectangle(headerBrush, lowerHeader);
        graphics.DrawLine(
            gridPen,
            0,
            lowerHeader.Top,
            Math.Max(0, projectedRight - 1),
            lowerHeader.Top);
        graphics.DrawLine(
            gridPen,
            0,
            lowerHeader.Bottom - 1,
            Math.Max(0, projectedRight - 1),
            lowerHeader.Bottom - 1);

        TextRenderer.DrawText(
            graphics,
            "(m)",
            grid.Font,
            new Rectangle(0, 0, Math.Max(0, grid.ClientSize.Width - 4), NodesUnitHeaderHeight),
            Color.White,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (grid.RowHeadersVisible)
        {
            graphics.DrawLine(gridPen, grid.RowHeadersWidth - 1, lowerHeader.Top, grid.RowHeadersWidth - 1, lowerHeader.Bottom);
        }

        IReadOnlyDictionary<string, Rectangle> projectedColumns = GetProjectedColumnBounds(grid);
        foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>()
                     .Where(column => column.Visible)
                     .OrderBy(column => column.DisplayIndex))
        {
            Rectangle bounds = projectedColumns[column.Name];
            Rectangle textBounds = new(bounds.X, lowerHeader.Top, bounds.Width, lowerHeader.Height);
            TextRenderer.DrawText(
                graphics,
                column.HeaderText,
                grid.Font,
                textBounds,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            graphics.DrawLine(gridPen, bounds.Right - 1, lowerHeader.Top, bounds.Right - 1, lowerHeader.Bottom);
        }
    }

    private void DrawSupportsHeader(DataGridView grid, Graphics graphics, int projectedRight)
    {
        IReadOnlyDictionary<string, Rectangle> columns = GetProjectedColumnBounds(grid);
        if (!columns.TryGetValue("node", out Rectangle node) ||
            !columns.TryGetValue("tx", out Rectangle tx) ||
            !columns.TryGetValue("ty", out Rectangle ty) ||
            !columns.TryGetValue("rz", out Rectangle rz))
        {
            return;
        }

        Rectangle translation = Rectangle.FromLTRB(tx.Left, 0, ty.Right, SupportsHeaderTierHeight);
        Rectangle rotation = _state?.Dimension == ViewDimension.ThreeDimensional &&
            columns.TryGetValue("rx", out Rectangle rx)
                ? Rectangle.FromLTRB(rx.Left, 0, rz.Right, SupportsHeaderTierHeight)
                : Rectangle.FromLTRB(rz.Left, 0, rz.Right, SupportsHeaderTierHeight);
        using SolidBrush headerBrush = new(TableRowLight);
        using Pen gridPen = new(TableGrid);
        graphics.FillRectangle(headerBrush, 0, 0, projectedRight, grid.ColumnHeadersHeight);

        DrawHeaderText(
            graphics,
            _localization["SurfaceSupportNodeGroup"],
            grid.Font,
            new Rectangle(node.Left + 7, 0, Math.Max(0, node.Width - 7), SupportsHeaderTierHeight),
            TextFormatFlags.Left);
        DrawHeaderText(
            graphics,
            _localization["SurfaceSupportTranslationGroup"],
            grid.Font,
            translation,
            TextFormatFlags.HorizontalCenter);
        DrawHeaderText(
            graphics,
            _localization["SurfaceSupportRotationGroup"],
            grid.Font,
            rotation,
            TextFormatFlags.HorizontalCenter);

        graphics.DrawLine(
            gridPen,
            0,
            SupportsHeaderTierHeight - 1,
            Math.Max(0, projectedRight - 1),
            SupportsHeaderTierHeight - 1);
        graphics.DrawLine(gridPen, node.Right - 1, 0, node.Right - 1, grid.ColumnHeadersHeight);
        graphics.DrawLine(gridPen, translation.Right - 1, 0, translation.Right - 1, grid.ColumnHeadersHeight);
        graphics.DrawLine(gridPen, rotation.Right - 1, 0, rotation.Right - 1, grid.ColumnHeadersHeight);

        foreach ((string name, Rectangle bounds) in columns.OrderBy(pair => pair.Value.Left))
        {
            Rectangle lower = new(
                bounds.Left,
                SupportsHeaderTierHeight,
                bounds.Width,
                SupportsHeaderTierHeight);
            DrawHeaderText(
                graphics,
                grid.Columns[name].HeaderText,
                grid.Font,
                lower,
                TextFormatFlags.HorizontalCenter);
            graphics.DrawLine(gridPen, bounds.Right - 1, lower.Top, bounds.Right - 1, lower.Bottom);
        }

        graphics.DrawLine(
            gridPen,
            0,
            grid.ColumnHeadersHeight - 1,
            Math.Max(0, projectedRight - 1),
            grid.ColumnHeadersHeight - 1);
    }

    private static void DrawHeaderText(
        Graphics graphics,
        string text,
        Font font,
        Rectangle bounds,
        TextFormatFlags horizontalAlignment)
    {
        TextRenderer.DrawText(
            graphics,
            text,
            font,
            bounds,
            Color.White,
            horizontalAlignment |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding);
    }

    private void DetachSurfacePainting(DataGridView grid)
    {
        grid.Paint -= DrawSheetContinuation;
        grid.Paint -= DrawProjectedTableOverlay;
    }
}
