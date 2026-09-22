using System.Collections;
using System.Reflection;
using PDF_Manager.Core.Documents;
using PDF_Manager.Shell.Editing;
using System.Runtime.InteropServices;

namespace PDF_Manager.UiTests;

public sealed class Step5SharedGridBehaviorTests
{
    [Fact]
    public void MultiRowPasteCommitsOnceAndUndoRedoRestoreTheWholeBatch()
    {
        StaTestRunner.Run(() =>
        {
            Step5EditorMatrixTests.MemoryClipboard clipboard = new() { Text = "10\t11\t12\n20\t21\t22" };
            using var editor = Step5EditorMatrixTests.CreateEditor(clipboard);
            editor.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            int edits = 0;
            editor.DocumentEdited += (_, _) => edits++;
            editor.NodeGrid.CurrentCell = editor.NodeGrid.Rows[0].Cells[1];

            Assert.True(editor.Paste(InputTableKey.Nodes));
            Assert.Equal(1, edits);
            Assert.Equal((10d, 11d, 12d), Coordinates(editor.Document!, "1"));
            Assert.Equal((20d, 21d, 22d), Coordinates(editor.Document!, "2"));

            Assert.True(editor.Undo());
            Assert.Equal((0d, 0d, 0d), Coordinates(editor.Document!, "1"));
            Assert.Equal((4d, 0d, 0d), Coordinates(editor.Document!, "2"));
            Assert.False(editor.CanUndo);
            Assert.True(editor.Redo());
            Assert.Equal((10d, 11d, 12d), Coordinates(editor.Document!, "1"));
            Assert.Equal((20d, 21d, 22d), Coordinates(editor.Document!, "2"));
        }, "Step 5 atomic multi-row paste");
    }

    [Fact]
    public void InvalidPasteRollsBackEveryCellAndPublishesOneLocalizedFailure()
    {
        StaTestRunner.Run(() =>
        {
            Step5EditorMatrixTests.MemoryClipboard clipboard = new() { Text = "10\tbad\t12\n20\t21\t22" };
            using var editor = Step5EditorMatrixTests.CreateEditor(clipboard);
            editor.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            List<string> failures = [];
            int edits = 0;
            editor.ValidationFailed += (_, eventArgs) => failures.Add(eventArgs.Message);
            editor.DocumentEdited += (_, _) => edits++;
            editor.NodeGrid.CurrentCell = editor.NodeGrid.Rows[0].Cells[1];

            Assert.False(editor.Paste(InputTableKey.Nodes));
            Assert.Equal((0d, 0d, 0d), Coordinates(editor.Document!, "1"));
            Assert.Equal((4d, 0d, 0d), Coordinates(editor.Document!, "2"));
            Assert.Equal("The edited value is invalid.", Assert.Single(failures));
            Assert.Equal(0, edits);
            Assert.False(editor.CanUndo);
            Assert.Equal("0", Convert.ToString(editor.NodeGrid.Rows[0].Cells[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }, "Step 5 invalid paste rollback");
    }

    [Fact]
    public void ClipboardBoundsAndReadOnlyTargetsFailBeforeMutation()
    {
        StaTestRunner.Run(() =>
        {
            Step5EditorMatrixTests.MemoryClipboard clipboard = new();
            using var editor = Step5EditorMatrixTests.CreateEditor(clipboard);
            editor.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            List<string> failures = [];
            editor.ValidationFailed += (_, eventArgs) => failures.Add(eventArgs.Message);

            clipboard.Text = new string('1', DataGridViewEditorController.MaximumClipboardCharacters + 1);
            editor.NodeGrid.CurrentCell = editor.NodeGrid.Rows[0].Cells[1];
            Assert.False(editor.Paste(InputTableKey.Nodes));
            Assert.Equal("The clipboard selection exceeds the editor limit.", failures[^1]);

            clipboard.Text = "renamed";
            editor.NodeGrid.CurrentCell = editor.NodeGrid.Rows[0].Cells[0];
            Assert.False(editor.Paste(InputTableKey.Nodes));
            Assert.Equal("The paste targets a read-only column.", failures[^1]);

            clipboard.Text = "1\t2\n3";
            editor.NodeGrid.CurrentCell = editor.NodeGrid.Rows[0].Cells[1];
            Assert.False(editor.Paste(InputTableKey.Nodes));
            Assert.Equal("The clipboard rows do not fit this table.", failures[^1]);
            Assert.Equal((0d, 0d, 0d), Coordinates(editor.Document!, "1"));
        }, "Step 5 clipboard bounds");
    }

    [Fact]
    public void RangeCopyUsesInvariantTsvAndKeyboardMovementWrapsEditableCells()
    {
        StaTestRunner.Run(() =>
        {
            Step5EditorMatrixTests.MemoryClipboard clipboard = new();
            using var editor = Step5EditorMatrixTests.CreateEditor(clipboard);
            editor.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            DataGridView grid = editor.NodeGrid;
            grid.ClearSelection();
            grid.Rows[0].Selected = true;
            grid.Rows[1].Selected = true;

            Assert.True(editor.Copy(InputTableKey.Nodes));
            Assert.Equal("1\t0\t0\t0" + Environment.NewLine + "2\t4\t0\t0", clipboard.Text);

            grid.CurrentCell = grid.Rows[0].Cells[3];
            Assert.True(editor.MoveCurrentCell(InputTableKey.Nodes, Keys.Tab));
            Assert.Equal((1, 1), (grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex));
            Assert.True(editor.MoveCurrentCell(InputTableKey.Nodes, Keys.Shift | Keys.Tab));
            Assert.Equal((0, 3), (grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex));
            Assert.True(editor.MoveCurrentCell(InputTableKey.Nodes, Keys.Enter));
            Assert.Equal((1, 3), (grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex));
        }, "Step 5 copy and navigation");
    }

    [Fact]
    public void InsertAndReferencedDeleteAreAtomicAndReferenceSafe()
    {
        StaTestRunner.Run(() =>
        {
            using var editor = Step5EditorMatrixTests.CreateEditor();
            editor.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            Assert.True(editor.Insert(InputTableKey.Nodes, count: 2));
            Assert.Equal(4, editor.Document!.Nodes.Count);
            Assert.True(editor.Undo());
            Assert.Equal(2, editor.Document!.Nodes.Count);
            Assert.True(editor.Redo());
            Assert.Equal(4, editor.Document!.Nodes.Count);

            DataGridView grid = editor.NodeGrid;
            grid.ClearSelection();
            grid.Rows[0].Cells[0].Selected = true;
            List<string> failures = [];
            editor.ValidationFailed += (_, eventArgs) => failures.Add(eventArgs.Message);
            Assert.False(editor.Delete(InputTableKey.Nodes));
            Assert.Equal(4, editor.Document!.Nodes.Count);
            Assert.Equal("The selected rows cannot be deleted because they are referenced.", Assert.Single(failures));

            grid.ClearSelection();
            grid.Rows.Cast<DataGridViewRow>().Single(row => Convert.ToString(row.Cells[0].Value) == "3").Cells[0].Selected = true;
            Assert.True(editor.Delete(InputTableKey.Nodes));
            Assert.Equal(3, editor.Document!.Nodes.Count);
            Assert.True(editor.Undo());
            Assert.Equal(4, editor.Document!.Nodes.Count);
        }, "Step 5 insert delete reference safety");
    }

    [Fact]
    public void EveryTypedTableValidatesPasteRollsBackAndUsesOneUndoEntry()
    {
        StaTestRunner.Run(() =>
        {
            foreach (MutationCase test in MutationCases)
            {
                Step5EditorMatrixTests.MemoryClipboard clipboard = new();
                using var editor = Step5EditorMatrixTests.CreateEditor(clipboard);
                editor.SetDocument(Step5EditorMatrixTests.CreateFullDocument());
                DataGridView grid = editor.Tables[test.Table];
                string before = ProjectDocumentJson.SerializeToString(editor.Document!);
                grid.CurrentCell = grid.Rows[0].Cells[test.Column];
                clipboard.Text = test.ValidClipboard;

                Assert.True(editor.Paste(test.Table), test.Table.ToString());
                Assert.NotEqual(before, ProjectDocumentJson.SerializeToString(editor.Document!));
                Assert.True(editor.Undo(), test.Table.ToString());
                Assert.Equal(before, ProjectDocumentJson.SerializeToString(editor.Document!));
                Assert.False(editor.CanUndo);
                Assert.True(editor.Redo(), test.Table.ToString());
                Assert.True(editor.Undo(), test.Table.ToString());

                int failures = 0;
                editor.ValidationFailed += (_, _) => failures++;
                grid = editor.Tables[test.Table];
                grid.CurrentCell = grid.Rows[0].Cells[test.Column];
                clipboard.Text = test.InvalidClipboard;
                Assert.False(editor.Paste(test.Table));
                Assert.Equal(1, failures);
                Assert.Equal(before, ProjectDocumentJson.SerializeToString(editor.Document!));
                Assert.False(editor.CanUndo);
            }
        }, "Step 5 all-table paste and rollback matrix", TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void EveryCreatableTableInsertDeleteAndUndoAreAtomic()
    {
        StaTestRunner.Run(() =>
        {
            foreach (InputTableKey table in Enum.GetValues<InputTableKey>().Where(value => value != InputTableKey.ModelSettings))
            {
                using var editor = Step5EditorMatrixTests.CreateEditor();
                editor.SetDocument(Step5EditorMatrixTests.CreateFullDocument());
                DataGridView grid = editor.Tables[table];
                HashSet<string> beforeIds = grid.Rows.Cast<DataGridViewRow>()
                    .Select(row => Convert.ToString(row.Cells[0].Value)!)
                    .ToHashSet(StringComparer.Ordinal);
                int beforeCount = grid.Rows.Count;

                Assert.True(editor.Insert(table), table.ToString());
                Assert.Equal(beforeCount + 1, editor.Tables[table].Rows.Count);
                Assert.True(editor.Undo(), table.ToString());
                Assert.Equal(beforeCount, editor.Tables[table].Rows.Count);
                Assert.True(editor.Redo(), table.ToString());

                grid = editor.Tables[table];
                DataGridViewRow inserted = Assert.Single(grid.Rows.Cast<DataGridViewRow>(), row =>
                    !beforeIds.Contains(Convert.ToString(row.Cells[0].Value)!));
                grid.ClearSelection();
                grid.CurrentCell = inserted.Cells[0];
                inserted.Selected = true;
                Assert.True(editor.Delete(table), table.ToString());
                Assert.Equal(beforeCount, editor.Tables[table].Rows.Count);
                Assert.True(editor.Undo(), table.ToString());
                Assert.Equal(beforeCount + 1, editor.Tables[table].Rows.Count);
            }
        }, "Step 5 all-table insert delete matrix", TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void SetOwnedRowsMoveToNewTypedSetsAndUndoRestoresTheirOrigin()
    {
        StaTestRunner.Run(() =>
        {
            (InputTableKey Manager, InputTableKey Rows, int SetColumn)[] cases =
            [
                (InputTableKey.ElementPropertySets, InputTableKey.Sections, 12),
                (InputTableKey.SupportSets, InputTableKey.Supports, 8),
                (InputTableKey.JointReleaseSets, InputTableKey.Joints, 8),
                (InputTableKey.MemberSpringSets, InputTableKey.MemberSprings, 6),
            ];

            foreach ((InputTableKey manager, InputTableKey rows, int setColumn) in cases)
            {
                Step5EditorMatrixTests.MemoryClipboard clipboard = new();
                using var editor = Step5EditorMatrixTests.CreateEditor(clipboard);
                editor.SetDocument(Step5EditorMatrixTests.CreateFullDocument());
                HashSet<string> managerIds = editor.Tables[manager].Rows.Cast<DataGridViewRow>()
                    .Select(row => Convert.ToString(row.Cells[0].Value)!)
                    .ToHashSet(StringComparer.Ordinal);
                Assert.True(editor.Insert(manager));
                string targetSet = Assert.Single(editor.Tables[manager].Rows.Cast<DataGridViewRow>(), row =>
                    !managerIds.Contains(Convert.ToString(row.Cells[0].Value)!)).Cells[0].Value!.ToString()!;

                HashSet<string> rowIds = editor.Tables[rows].Rows.Cast<DataGridViewRow>()
                    .Select(row => Convert.ToString(row.Cells[0].Value)!)
                    .ToHashSet(StringComparer.Ordinal);
                Assert.True(editor.Insert(rows));
                DataGridView grid = editor.Tables[rows];
                DataGridViewRow inserted = Assert.Single(grid.Rows.Cast<DataGridViewRow>(), row =>
                    !rowIds.Contains(Convert.ToString(row.Cells[0].Value)!));
                string rowId = Convert.ToString(inserted.Cells[0].Value)!;
                grid.CurrentCell = inserted.Cells[setColumn];
                clipboard.Text = targetSet;

                Assert.True(editor.Paste(rows), rows.ToString());
                Assert.True(IsRowInSet(editor.Document!, rows, targetSet, rowId));
                Assert.False(IsRowInSet(editor.Document!, rows, "1", rowId));
                Assert.True(editor.Undo(), rows.ToString());
                Assert.True(IsRowInSet(editor.Document!, rows, "1", rowId));
                Assert.False(IsRowInSet(editor.Document!, rows, targetSet, rowId));
            }
        }, "Step 5 typed set row targeting", TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void InsertCandidatePoliciesAreDeterministicAcrossEveryBuiltInPreset()
    {
        StaTestRunner.Run(() =>
        {
            InputTableKey[] topologyTables =
            [
                InputTableKey.Members,
                InputTableKey.Supports,
                InputTableKey.RigidZones,
                InputTableKey.Panels,
                InputTableKey.Joints,
                InputTableKey.NoticePoints,
                InputTableKey.MemberSprings,
            ];
            foreach (BuiltInProjectPreset preset in Enum.GetValues<BuiltInProjectPreset>())
                foreach (InputTableKey table in topologyTables)
                {
                    using var editor = Step5EditorMatrixTests.CreateEditor();
                    ProjectDocument original = ProjectDocumentPresets.Create(preset);
                    editor.SetDocument(original);
                    List<string> failures = [];
                    editor.ValidationFailed += (_, eventArgs) => failures.Add(eventArgs.Message);

                    bool inserted = editor.Insert(table);
                    if (inserted)
                    {
                        Assert.Empty(failures);
                        Assert.True(editor.Undo(), $"{preset}/{table}");
                        Assert.Equal(
                            ProjectDocumentJson.SerializeToString(original),
                            ProjectDocumentJson.SerializeToString(editor.Document!));
                    }
                    else
                    {
                        Assert.Equal("No unused valid row can be inserted in this table.", Assert.Single(failures));
                        Assert.False(editor.CanUndo);
                    }
                }
        }, "Step 5 preset insert candidates", TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void TopologyCandidateBudgetBoundsMemberAndPanelExhaustion()
    {
        Assert.Equal(10_000, CountTopologyCandidates(itemCount: 1_000, selectionCount: 2));
        Assert.Equal(10_000, CountTopologyCandidates(itemCount: 1_000, selectionCount: 4));

        StaTestRunner.Run(() =>
        {
            ProjectDocumentEditSession session = new(ProjectDocumentPresets.CreateRepresentativeFrame());
            Assert.True(session.ApplyBatch(batch =>
            {
                for (int id = 3; id <= 24; id++)
                    batch.UpsertNode(new ProjectNode(id.ToString(), id + 10, 0, 0));
            }));
            ProjectDocument before = session.Current;
            using var editor = Step5EditorMatrixTests.CreateEditor();
            editor.SetDocument(before);
            List<string> failures = [];
            editor.ValidationFailed += (_, eventArgs) => failures.Add(eventArgs.Message);

            Assert.False(editor.Insert(InputTableKey.Panels));
            Assert.Equal("No unused valid row can be inserted in this table.", Assert.Single(failures));
            Assert.Equal(ProjectDocumentJson.SerializeToString(before), ProjectDocumentJson.SerializeToString(editor.Document!));
            Assert.False(editor.CanUndo);
        }, "Step 5 bounded topology exhaustion");
    }

    [Fact]
    public void CommandRoutingInvokesClipboardNavigationInsertAndDeleteBehaviors()
    {
        StaTestRunner.Run(() =>
        {
            Step5EditorMatrixTests.MemoryClipboard clipboard = new();
            using var editor = Step5EditorMatrixTests.CreateEditor(clipboard);
            editor.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            DataGridView grid = editor.NodeGrid;
            grid.ClearSelection();
            grid.Rows[0].Selected = true;
            Assert.True(editor.DispatchCommand(InputTableKey.Nodes, Keys.Control | Keys.C));
            Assert.Equal("1\t0\t0\t0", clipboard.Text);

            grid.CurrentCell = grid.Rows[0].Cells[1];
            clipboard.Text = "2";
            Assert.True(editor.DispatchCommand(InputTableKey.Nodes, Keys.Control | Keys.V));
            Assert.Equal(2, editor.Document!.Nodes[0].X);
            Assert.True(editor.Undo());

            grid = editor.NodeGrid;
            grid.CurrentCell = grid.Rows[0].Cells[3];
            Assert.True(editor.DispatchCommand(InputTableKey.Nodes, Keys.Tab));
            Assert.Equal((1, 1), (grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex));
            Assert.True(editor.DispatchCommand(InputTableKey.Nodes, Keys.Shift | Keys.Tab));
            Assert.Equal((0, 3), (grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex));
            Assert.True(editor.DispatchCommand(InputTableKey.Nodes, Keys.Shift | Keys.Enter));
            Assert.Equal((0, 3), (grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex));

            Assert.True(editor.DispatchCommand(InputTableKey.Nodes, Keys.Insert));
            grid = editor.NodeGrid;
            DataGridViewRow inserted = grid.Rows.Cast<DataGridViewRow>()
                .Single(row => Convert.ToString(row.Cells[0].Value) == "3");
            grid.ClearSelection();
            grid.CurrentCell = inserted.Cells[0];
            inserted.Selected = true;
            Assert.True(editor.DispatchCommand(InputTableKey.Nodes, Keys.Delete));
            Assert.DoesNotContain(editor.Document!.Nodes, node => node.Id == "3");
        }, "Step 5 command key routing");
    }

    [Fact]
    public void ClipboardShapeLimitsAndUnavailableClipboardFailWithoutMutation()
    {
        StaTestRunner.Run(() =>
        {
            Step5EditorMatrixTests.MemoryClipboard clipboard = new();
            using var editor = Step5EditorMatrixTests.CreateEditor(clipboard);
            editor.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            string before = ProjectDocumentJson.SerializeToString(editor.Document!);
            List<string> failures = [];
            editor.ValidationFailed += (_, eventArgs) => failures.Add(eventArgs.Message);
            editor.NodeGrid.CurrentCell = editor.NodeGrid.Rows[0].Cells[1];

            clipboard.Text = string.Join('\n', Enumerable.Repeat("1", DataGridViewEditorController.MaximumClipboardRows + 1));
            Assert.False(editor.Paste(InputTableKey.Nodes));
            clipboard.Text = string.Join('\t', Enumerable.Repeat("1", DataGridViewEditorController.MaximumClipboardColumns + 1));
            Assert.False(editor.Paste(InputTableKey.Nodes));
            string cells = string.Join('\t', Enumerable.Repeat("1", DataGridViewEditorController.MaximumClipboardColumns));
            clipboard.Text = string.Join('\n', Enumerable.Repeat(cells,
                (DataGridViewEditorController.MaximumClipboardCells / DataGridViewEditorController.MaximumClipboardColumns) + 1));
            Assert.False(editor.Paste(InputTableKey.Nodes));
            clipboard.Text = "1\0";
            Assert.False(editor.Paste(InputTableKey.Nodes));
            Assert.Equal(before, ProjectDocumentJson.SerializeToString(editor.Document!));
            Assert.False(editor.CanUndo);
            Assert.Equal(4, failures.Count);

            using var getFailure = new PDF_Manager.Shell.Contents.EditorContent(
                PDF_Manager.Core.Shell.DocumentKey.Tool("clipboard-get"),
                new PDF_Manager.Resources.LocalizationService(PDF_Manager.Resources.UiLanguage.English),
                new ThrowingClipboard(throwOnGet: true));
            getFailure.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            List<string> getFailures = [];
            getFailure.ValidationFailed += (_, eventArgs) => getFailures.Add(eventArgs.Message);
            getFailure.NodeGrid.CurrentCell = getFailure.NodeGrid.Rows[0].Cells[1];
            Assert.False(getFailure.Paste(InputTableKey.Nodes));
            Assert.Equal("The clipboard is unavailable.", Assert.Single(getFailures));

            using var setFailure = new PDF_Manager.Shell.Contents.EditorContent(
                PDF_Manager.Core.Shell.DocumentKey.Tool("clipboard-set"),
                new PDF_Manager.Resources.LocalizationService(PDF_Manager.Resources.UiLanguage.English),
                new ThrowingClipboard(throwOnGet: false));
            setFailure.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            List<string> setFailures = [];
            setFailure.ValidationFailed += (_, eventArgs) => setFailures.Add(eventArgs.Message);
            setFailure.NodeGrid.Rows[0].Selected = true;
            Assert.False(setFailure.Copy(InputTableKey.Nodes));
            Assert.Equal("The clipboard is unavailable.", Assert.Single(setFailures));
        }, "Step 5 clipboard exact bounds and availability", TimeSpan.FromSeconds(20));
    }

    private static (double X, double Y, double Z) Coordinates(ProjectDocument document, string id)
    {
        ProjectNode node = document.Nodes.Single(candidate => candidate.Id == id);
        return (node.X, node.Y, node.Z);
    }

    private static int CountTopologyCandidates(int itemCount, int selectionCount)
    {
        Type policy = typeof(DataGridViewEditorController).Assembly.GetType(
            "PDF_Manager.Shell.Editing.ProjectDocumentInsertPolicy",
            throwOnError: true)!;
        MethodInfo enumerate = policy.GetMethod(
            "EnumerateTopologyCandidates",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        IEnumerable candidates = Assert.IsAssignableFrom<IEnumerable>(
            enumerate.Invoke(null, [itemCount, selectionCount]));
        return candidates.Cast<object>().Count();
    }

    private static bool IsRowInSet(ProjectDocument document, InputTableKey table, string setId, string rowId) => table switch
    {
        InputTableKey.Sections when setId == "1" => document.Sections.Any(row => row.Id == rowId),
        InputTableKey.Sections => document.ElementPropertySets.Single(set => set.Id == setId).Sections.Any(row => row.Id == rowId),
        InputTableKey.Supports when setId == "1" => document.Supports.Any(row => row.Id == rowId),
        InputTableKey.Supports => document.SupportSets.Single(set => set.Id == setId).Rows.Any(row => row.Id == rowId),
        InputTableKey.Joints => document.JointReleaseSets.Single(set => set.Id == setId).Rows.Any(row => row.Id == rowId),
        InputTableKey.MemberSprings => document.MemberSpringSets.Single(set => set.Id == setId).Rows.Any(row => row.Id == rowId),
        _ => throw new ArgumentOutOfRangeException(nameof(table)),
    };

    private sealed record MutationCase(
        InputTableKey Table,
        int Column,
        string ValidClipboard,
        string InvalidClipboard);

    private static IReadOnlyList<MutationCase> MutationCases { get; } =
    [
        new(InputTableKey.ModelSettings, 0, "TwoDimensional", "invalid-dimension"),
        new(InputTableKey.Nodes, 1, "0.5", "bad"),
        new(InputTableKey.Members, 1, "5", "missing"),
        new(InputTableKey.RigidZones, 1, "2", "missing"),
        new(InputTableKey.ElementPropertySets, 1, "Renamed element set", " "),
        new(InputTableKey.Supports, 1, "3", "missing"),
        new(InputTableKey.SupportSets, 1, "Renamed support set", " "),
        new(InputTableKey.Sections, 10, "1", "bad"),
        new(InputTableKey.Panels, 2, "4\t3\t2\t1", "missing\t3\t2\t1"),
        new(InputTableKey.Joints, 1, "2", "missing"),
        new(InputTableKey.JointReleaseSets, 1, "Renamed joint set", " "),
        new(InputTableKey.NoticePoints, 2, "1", "1000"),
        new(InputTableKey.MemberSprings, 1, "2", "missing"),
        new(InputTableKey.MemberSpringSets, 1, "Renamed spring set", " "),
        new(InputTableKey.LoadCases, 4, "2", "missing"),
        new(InputTableKey.NodalLoads, 2, "3", "missing"),
        new(InputTableKey.PrescribedDisplacements, 2, "3", "missing"),
        new(InputTableKey.MemberLoads, 2, "2", "missing"),
        new(InputTableKey.Define, 3, "2", "bad"),
        new(InputTableKey.Combine, 3, "2", "bad"),
        new(InputTableKey.Pickup, 3, "2", "bad"),
    ];

    private sealed class ThrowingClipboard(bool throwOnGet) : IClipboardTextService
    {
        public string GetText()
        {
            if (throwOnGet)
                throw new ExternalException("Clipboard busy.");
            return string.Empty;
        }

        public void SetText(string text) => throw new ExternalException("Clipboard busy.");
    }
}
