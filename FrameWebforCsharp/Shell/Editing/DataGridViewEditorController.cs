using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using FrameWebforCsharp.Rendering.Scene;

namespace FrameWebforCsharp.Shell.Editing;

public sealed class DataGridViewEditorController : IDisposable
{
    public const int MaximumClipboardCharacters = 1_000_000;
    public const int MaximumClipboardRows = 1_000;
    public const int MaximumClipboardColumns = 64;
    public const int MaximumClipboardCells = 10_000;

    private readonly int _creatingThreadId = Environment.CurrentManagedThreadId;
    private readonly EditorDataGridView _grid;
    private readonly InputTableDescriptor _descriptor;
    private readonly IClipboardTextService _clipboard;
    private readonly Func<IReadOnlyList<InputGridRowUpdate>, bool> _applyRows;
    private readonly Func<int, bool> _insertRows;
    private readonly Func<IReadOnlyList<string>, bool> _deleteRows;
    private readonly Action<SceneEntityKey?> _selectionChanged;
    private readonly Action<InputGridFailure> _failed;
    private int _updateDepth;
    private bool _disposed;

    public DataGridViewEditorController(
        DataGridView grid,
        InputTableDescriptor descriptor,
        IClipboardTextService clipboard,
        Func<IReadOnlyList<InputGridRowUpdate>, bool> applyRows,
        Func<int, bool> insertRows,
        Func<IReadOnlyList<string>, bool> deleteRows,
        Action<SceneEntityKey?> selectionChanged,
        Action<InputGridFailure> failed)
    {
        _grid = grid as EditorDataGridView
            ?? throw new ArgumentException("The shared editor controller requires an EditorDataGridView.", nameof(grid));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _applyRows = applyRows ?? throw new ArgumentNullException(nameof(applyRows));
        _insertRows = insertRows ?? throw new ArgumentNullException(nameof(insertRows));
        _deleteRows = deleteRows ?? throw new ArgumentNullException(nameof(deleteRows));
        _selectionChanged = selectionChanged ?? throw new ArgumentNullException(nameof(selectionChanged));
        _failed = failed ?? throw new ArgumentNullException(nameof(failed));

        ConfigureGrid();
        _grid.CellEndEdit += OnCellEndEdit;
        _grid.SelectionChanged += OnSelectionChanged;
        _grid.DataError += OnDataError;
        _grid.CommandKeyHandler = ProcessCommandKey;
    }

    public InputTableDescriptor Descriptor => _descriptor;

    public DataGridView Grid => _grid;

    public void Reload(IReadOnlyList<InputGridRow> rows)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(rows);

        string? currentRowId = _grid.CurrentCell is null ? null : GetRowId(_grid.Rows[_grid.CurrentCell.RowIndex]);
        int? currentColumn = _grid.CurrentCell?.ColumnIndex;
        int firstDisplayedRow = _grid.FirstDisplayedScrollingRowIndex;

        using (BeginUpdate())
        {
            _grid.Rows.Clear();
            foreach (InputGridRow row in rows)
            {
                if (row.Values.Count != _descriptor.Columns.Count)
                {
                    throw new InvalidOperationException(
                        $"Table '{_descriptor.Key}' row '{row.Id}' has {row.Values.Count} values; " +
                        $"{_descriptor.Columns.Count} were expected.");
                }

                int rowIndex = _grid.Rows.Add(row.Values.ToArray());
                _grid.Rows[rowIndex].Tag = row.SceneKey is { } sceneKey ? sceneKey : row.Id;
            }

            if (_grid.Rows.Count > 0)
            {
                DataGridViewRow? restored = currentRowId is null
                    ? null
                    : _grid.Rows.Cast<DataGridViewRow>()
                        .FirstOrDefault(row => StringComparer.Ordinal.Equals(GetRowId(row), currentRowId));
                int rowIndex = restored?.Index ?? Math.Min(Math.Max(firstDisplayedRow, 0), _grid.Rows.Count - 1);
                DataGridViewColumn? column = currentColumn is int columnIndex &&
                    columnIndex >= 0 &&
                    columnIndex < _grid.Columns.Count &&
                    _grid.Columns[columnIndex].Visible
                        ? _grid.Columns[columnIndex]
                        : _grid.Columns.Cast<DataGridViewColumn>()
                            .Where(candidate => candidate.Visible)
                            .OrderBy(candidate => candidate.DisplayIndex)
                            .FirstOrDefault();
                if (column is not null)
                {
                    _grid.CurrentCell = _grid.Rows[rowIndex].Cells[column.Index];
                }

                if (firstDisplayedRow >= 0 && firstDisplayedRow < _grid.Rows.Count)
                {
                    _grid.FirstDisplayedScrollingRowIndex = firstDisplayedRow;
                }
            }
        }
    }

    public void ApplyLocalization(Func<string, string> getString)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(getString);
        for (int index = 0; index < _descriptor.Columns.Count; index++)
        {
            _grid.Columns[index].HeaderText = getString(_descriptor.Columns[index].HeaderResourceKey);
        }
    }

    public bool CopySelection()
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_grid.SelectedCells.Count == 0)
        {
            return false;
        }

        int minRow = _grid.SelectedCells.Cast<DataGridViewCell>().Min(cell => cell.RowIndex);
        int maxRow = _grid.SelectedCells.Cast<DataGridViewCell>().Max(cell => cell.RowIndex);
        int minColumn = _grid.SelectedCells.Cast<DataGridViewCell>().Min(cell => cell.ColumnIndex);
        int maxColumn = _grid.SelectedCells.Cast<DataGridViewCell>().Max(cell => cell.ColumnIndex);
        int cells = checked((maxRow - minRow + 1) * (maxColumn - minColumn + 1));
        if (cells > MaximumClipboardCells)
        {
            _failed(InputGridFailure.ClipboardTooLarge);
            return false;
        }

        HashSet<(int Row, int Column)> selected = _grid.SelectedCells.Cast<DataGridViewCell>()
            .Select(cell => (cell.RowIndex, cell.ColumnIndex))
            .ToHashSet();
        StringBuilder text = new();
        for (int row = minRow; row <= maxRow; row++)
        {
            if (row > minRow)
            {
                text.AppendLine();
            }

            for (int column = minColumn; column <= maxColumn; column++)
            {
                if (column > minColumn)
                {
                    text.Append('\t');
                }

                if (selected.Contains((row, column)))
                {
                    text.Append(FormatCell(_grid.Rows[row].Cells[column].Value));
                }
            }
        }

        if (text.Length > MaximumClipboardCharacters)
        {
            _failed(InputGridFailure.ClipboardTooLarge);
            return false;
        }

        try
        {
            _clipboard.SetText(text.ToString());
            return true;
        }
        catch (ExternalException)
        {
            _failed(InputGridFailure.ClipboardUnavailable);
            return false;
        }
    }

    public bool Paste()
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_grid.CurrentCell is null)
        {
            return false;
        }

        string text;
        try
        {
            text = _clipboard.GetText();
        }
        catch (ExternalException)
        {
            _failed(InputGridFailure.ClipboardUnavailable);
            return false;
        }

        if (text.Length == 0)
        {
            return false;
        }

        if (text.Length > MaximumClipboardCharacters || text.IndexOf('\0') >= 0)
        {
            _failed(InputGridFailure.ClipboardTooLarge);
            return false;
        }

        string[][] values;
        try
        {
            values = ParseClipboard(text);
        }
        catch (FormatException)
        {
            _failed(InputGridFailure.ClipboardInvalidShape);
            return false;
        }

        int startRow = _grid.CurrentCell.RowIndex;
        int startColumn = _grid.CurrentCell.ColumnIndex;
        if (startRow + values.Length > _grid.Rows.Count ||
            startColumn + values[0].Length > _grid.Columns.Count)
        {
            _failed(InputGridFailure.ClipboardInvalidShape);
            return false;
        }

        List<InputGridRowUpdate> updates = [];
        for (int rowOffset = 0; rowOffset < values.Length; rowOffset++)
        {
            DataGridViewRow row = _grid.Rows[startRow + rowOffset];
            string[] cells = row.Cells.Cast<DataGridViewCell>().Select(cell => FormatCell(cell.Value)).ToArray();
            for (int columnOffset = 0; columnOffset < values[rowOffset].Length; columnOffset++)
            {
                int columnIndex = startColumn + columnOffset;
                if (_grid.Columns[columnIndex].ReadOnly)
                {
                    _failed(InputGridFailure.ReadOnlyTarget);
                    return false;
                }

                cells[columnIndex] = values[rowOffset][columnOffset];
            }

            updates.Add(new InputGridRowUpdate(GetRequiredRowId(row), cells));
        }

        return _applyRows(updates);
    }

    public bool InsertRows(int count = 1)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count is < 1 or > MaximumClipboardRows)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        return _insertRows(count);
    }

    public bool DeleteSelection()
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        string[] ids = _grid.SelectedCells.Cast<DataGridViewCell>()
            .Select(cell => GetRowId(_grid.Rows[cell.RowIndex]))
            .Where(id => id is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return false;
        }

        return _deleteRows(ids);
    }

    public bool MoveCurrentCell(Keys keyData)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_grid.CurrentCell is null || _grid.Rows.Count == 0 || _grid.Columns.Count == 0)
        {
            return false;
        }

        bool reverse = keyData.HasFlag(Keys.Shift);
        Keys keyCode = keyData & Keys.KeyCode;
        int row = _grid.CurrentCell.RowIndex;
        int column = _grid.CurrentCell.ColumnIndex;
        if (keyCode == Keys.Enter)
        {
            row = Math.Clamp(row + (reverse ? -1 : 1), 0, _grid.Rows.Count - 1);
        }
        else if (keyCode == Keys.Tab)
        {
            int direction = reverse ? -1 : 1;
            do
            {
                column += direction;
                if (column >= _grid.Columns.Count)
                {
                    column = 0;
                    row = Math.Min(row + 1, _grid.Rows.Count - 1);
                }
                else if (column < 0)
                {
                    column = _grid.Columns.Count - 1;
                    row = Math.Max(row - 1, 0);
                }
            }
            while (_grid.Columns[column].ReadOnly &&
                   (row != _grid.CurrentCell.RowIndex || column != _grid.CurrentCell.ColumnIndex));
        }
        else
        {
            return false;
        }

        _grid.CurrentCell = _grid.Rows[row].Cells[column];
        return true;
    }

    public bool DispatchCommand(Keys keyData)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ProcessCommandKey(keyData);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        VerifyAccess();
        _disposed = true;
        _grid.CommandKeyHandler = null;
        _grid.CellEndEdit -= OnCellEndEdit;
        _grid.SelectionChanged -= OnSelectionChanged;
        _grid.DataError -= OnDataError;
    }

    private void ConfigureGrid()
    {
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _grid.BorderStyle = BorderStyle.None;
        _grid.Dock = DockStyle.Fill;
        _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        _grid.MultiSelect = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.StandardTab = false;
        _grid.Columns.Clear();
        foreach (InputColumnDescriptor column in _descriptor.Columns)
        {
            DataGridViewColumn gridColumn = column.Kind == InputColumnKind.Boolean
                ? new DataGridViewCheckBoxColumn()
                : new DataGridViewTextBoxColumn();
            gridColumn.Name = column.Id;
            gridColumn.ReadOnly = column.IsReadOnly;
            _grid.Columns.Add(gridColumn);
        }
    }

    private bool ProcessCommandKey(Keys keyData)
    {
        if (_disposed || _updateDepth > 0)
        {
            return false;
        }

        Keys code = keyData & Keys.KeyCode;
        if (keyData.HasFlag(Keys.Control) && code == Keys.C)
        {
            return CopySelection();
        }

        if (keyData.HasFlag(Keys.Control) && code == Keys.V)
        {
            return Paste();
        }

        if (code == Keys.Insert)
        {
            return InsertRows();
        }

        if (code == Keys.Delete && !_grid.IsCurrentCellInEditMode)
        {
            return DeleteSelection();
        }

        return (code is Keys.Enter or Keys.Tab) && MoveCurrentCell(keyData);
    }

    private void OnCellEndEdit(object? sender, DataGridViewCellEventArgs eventArgs)
    {
        if (_disposed || _updateDepth > 0 || eventArgs.RowIndex < 0)
        {
            return;
        }

        DataGridViewRow row = _grid.Rows[eventArgs.RowIndex];
        _applyRows([
            new InputGridRowUpdate(
                GetRequiredRowId(row),
                row.Cells.Cast<DataGridViewCell>().Select(cell => FormatCell(cell.Value)).ToArray()),
        ]);
    }

    private void OnSelectionChanged(object? sender, EventArgs eventArgs)
    {
        if (_disposed || _updateDepth > 0)
        {
            return;
        }

        DataGridViewRow? currentRow = _grid.CurrentCell is null ? null : _grid.Rows[_grid.CurrentCell.RowIndex];
        DataGridViewRow? row = currentRow?.Selected == true
            ? currentRow
            : _grid.SelectedRows.Count > 0
                ? _grid.SelectedRows[0]
                : null;
        if (row?.Tag is SceneEntityKey sceneKey)
        {
            _selectionChanged(sceneKey);
        }
    }

    private void OnDataError(object? sender, DataGridViewDataErrorEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        _failed(InputGridFailure.InvalidValue);
    }

    private IDisposable BeginUpdate()
    {
        _updateDepth++;
        return new UpdateScope(this);
    }

    private void EndUpdate() => _updateDepth--;

    private static string[][] ParseClipboard(string text)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normalized.EndsWith('\n'))
        {
            normalized = normalized[..^1];
        }

        string[] lines = normalized.Split('\n');
        if (lines.Length is 0 or > MaximumClipboardRows)
        {
            throw new FormatException("The clipboard row count is invalid.");
        }

        string[][] values = lines.Select(line => line.Split('\t')).ToArray();
        int columns = values[0].Length;
        if (columns is 0 or > MaximumClipboardColumns ||
            values.Any(row => row.Length != columns) ||
            checked(values.Length * columns) > MaximumClipboardCells)
        {
            throw new FormatException("The clipboard shape is invalid.");
        }

        return values;
    }

    private static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static string? GetRowId(DataGridViewRow row) => row.Tag switch
    {
        SceneEntityKey sceneKey => sceneKey.Id,
        string id => id,
        _ => null,
    };

    private static string GetRequiredRowId(DataGridViewRow row) => GetRowId(row)
        ?? throw new InvalidOperationException("The editor row has no stable identity.");

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != _creatingThreadId)
        {
            throw new InvalidOperationException("Grid editor operations must run on the creating thread.");
        }
    }

    private sealed class UpdateScope(DataGridViewEditorController owner) : IDisposable
    {
        private DataGridViewEditorController? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.EndUpdate();
    }
}
