using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Editing;
using PDF_Manager.Shell.Lifecycle;

namespace PDF_Manager.UiTests;

public sealed class Step5EditorMatrixTests
{
    [Fact]
    public void FullTypedMatrixIsReachableInOneEditorPane()
    {
        StaTestRunner.Run(() =>
        {
            using EditorContent editor = CreateEditor();
            editor.SetDocument(CreateFullDocument());

            InputTableKey[] expected = Enum.GetValues<InputTableKey>();
            Assert.Equal(21, expected.Length);
            Assert.Equal(expected.Order(), editor.Tables.Keys.Order());
            Assert.Equal(expected.Length, editor.Tabs.TabPages.Count);
            Assert.All(editor.Tables.Values, grid => Assert.True(grid.Rows.Count > 0, grid.Name));
            Assert.Same(editor.NodeGrid, editor.Tables[InputTableKey.Nodes]);
            Assert.Same(editor.MemberGrid, editor.Tables[InputTableKey.Members]);
            Assert.Same(editor.ModelSettingsGrid, editor.Tables[InputTableKey.ModelSettings]);
            Assert.Same(editor.ElementPropertySetGrid, editor.Tables[InputTableKey.ElementPropertySets]);
            Assert.Same(editor.SupportGrid, editor.Tables[InputTableKey.Supports]);
            Assert.Same(editor.SupportSetGrid, editor.Tables[InputTableKey.SupportSets]);
            Assert.Same(editor.JointReleaseSetGrid, editor.Tables[InputTableKey.JointReleaseSets]);
            Assert.Same(editor.MemberSpringSetGrid, editor.Tables[InputTableKey.MemberSpringSets]);
            Assert.Same(editor.LoadCaseGrid, editor.Tables[InputTableKey.LoadCases]);
            Assert.Same(editor.NodalLoadGrid, editor.Tables[InputTableKey.NodalLoads]);
            Assert.Same(editor.PrescribedDisplacementGrid, editor.Tables[InputTableKey.PrescribedDisplacements]);

            foreach (InputTableKey table in expected)
            {
                editor.ActivateTable(table);
                Assert.Same(editor.Tables[table].Parent, editor.Tabs.SelectedTab);
            }
        }, "Step 5 full editor matrix");
    }

    [Fact]
    public void RuntimeLocalizationUpdatesEveryTabAndHeader()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.English);
            using EditorContent editor = new(DocumentKey.Tool("step5-editor"), localization, new MemoryClipboard());
            editor.SetDocument(CreateFullDocument());
            string english = editor.Tabs.TabPages.Cast<TabPage>().Single(page => page.Controls.Contains(editor.RigidZoneGrid)).Text;

            localization.SetLanguage(UiLanguage.Japanese);
            editor.ApplyLocalization();
            string japanese = editor.Tabs.TabPages.Cast<TabPage>().Single(page => page.Controls.Contains(editor.RigidZoneGrid)).Text;
            Assert.Equal("Rigid zones", english);
            Assert.Equal("剛域", japanese);
            Assert.Equal("断面", editor.MemberGrid.Columns[3].HeaderText);

            localization.SetLanguage(UiLanguage.Chinese);
            editor.ApplyLocalization();
            Assert.Equal("刚域", editor.Tabs.TabPages.Cast<TabPage>()
                .Single(page => page.Controls.Contains(editor.RigidZoneGrid)).Text);
            Assert.All(editor.Tables.Values.SelectMany(grid => grid.Columns.Cast<DataGridViewColumn>()),
                column => Assert.False(string.IsNullOrWhiteSpace(column.HeaderText)));
        }, "Step 5 editor localization");
    }

    [Fact]
    public void RepeatedLifecycleIsIdempotentAndCreatingThreadOnly()
    {
        StaTestRunner.Run(() =>
        {
            for (int iteration = 0; iteration < 5; iteration++)
            {
                EditorContent editor = CreateEditor();
                editor.SetDocument(CreateFullDocument());
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                    Task.Run(() => editor.ActivateTable(InputTableKey.Nodes)).GetAwaiter().GetResult());
                Assert.Contains("creating thread", error.Message, StringComparison.Ordinal);
                editor.Dispose();
                editor.Dispose();
            }
        }, "Step 5 repeated editor lifecycle");
    }

    [Fact]
    public void FullMatrixRemainsInOneRegisteredEditorPaneAcrossHideAndReuse()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = new(new MainFormServices(
                localization: new LocalizationService(UiLanguage.English),
                layoutStore: new NoOpLayoutStore()));
            form.SetDocument(CreateFullDocument());
            EditorContent original = form.EditorPane;

            foreach (InputTableKey table in Enum.GetValues<InputTableKey>())
            {
                form.ShowEditorPane();
                form.EditorPane.ActivateTable(table);
                Assert.Same(original, form.EditorPane);
                Assert.Single(form.ContentRegistry.Contents, pair => pair.Key == MainForm.EditorContentKey);
            }

            Assert.True(form.ContentRegistry.Close(MainForm.EditorContentKey));
            form.ShowEditorPane();
            Assert.Same(original, form.EditorPane);
            Assert.Single(form.ContentRegistry.Contents, pair => pair.Key == MainForm.EditorContentKey);
        }, "Step 5 single editor dock pane lifecycle");
    }

    [Fact]
    public void EveryRenderableInputUsesBidirectionalStableSelectionKeys()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = new(new MainFormServices(
                localization: new LocalizationService(UiLanguage.English),
                layoutStore: new NoOpLayoutStore()));
            form.SetDocument(CreateFullDocument());
            (InputTableKey Table, SceneEntityKey Key)[] cases =
            [
                (InputTableKey.Nodes, new SceneEntityKey(SceneEntityKind.Node, "2")),
                (InputTableKey.Members, new SceneEntityKey(SceneEntityKind.Member, "1")),
                (InputTableKey.Supports, new SceneEntityKey(SceneEntityKind.Support, "S1")),
                (InputTableKey.NodalLoads, new SceneEntityKey(SceneEntityKind.NodalLoad, "P1")),
                (InputTableKey.MemberLoads, new SceneEntityKey(SceneEntityKind.MemberLoad, "ML1")),
            ];

            foreach ((InputTableKey table, SceneEntityKey key) in cases)
            {
                DataGridView grid = form.EditorPane.Tables[table];
                DataGridViewRow row = grid.Rows.Cast<DataGridViewRow>()
                    .Single(candidate => candidate.Tag is SceneEntityKey candidateKey && candidateKey == key);
                grid.ClearSelection();
                grid.CurrentCell = row.Cells[0];
                row.Selected = true;
                Application.DoEvents();
                Assert.Equal(key, form.DocumentHost.Selection);

                grid.ClearSelection();
                form.DocumentHost.SetSelection(null, ViewportSelectionOrigin.Viewport);
                form.DocumentHost.SetSelection(key, ViewportSelectionOrigin.Viewport);
                Application.DoEvents();
                Assert.Equal(key, Assert.IsType<SceneEntityKey>(Assert.Single(grid.SelectedRows.Cast<DataGridViewRow>()).Tag));
            }
        }, "Step 5 bidirectional input selection");
    }

    internal static EditorContent CreateEditor(MemoryClipboard? clipboard = null) =>
        new(DocumentKey.Tool("step5-editor"), new LocalizationService(UiLanguage.English), clipboard ?? new MemoryClipboard());

    internal static ProjectDocument CreateFullDocument()
    {
        ProjectDocumentEditSession session = new(ProjectDocumentPresets.CreateRepresentativeFrame());
        Assert.True(session.ApplyBatch(batch =>
        {
            batch.UpsertNode(new ProjectNode("3", 4, 4, 0));
            batch.UpsertNode(new ProjectNode("4", 0, 4, 0));
            batch.UpsertNode(new ProjectNode("5", 0, 8, 0));
            FrameSectionDefinition section = session.Current.Sections[0] with { PanelThickness = 0.2 };
            batch.UpsertSection(section);
            batch.UpsertElementPropertySet(new ElementPropertySetDefinition(
                "2", "Alternate", [section with { Name = "Alternate section" }]));
            batch.UpsertMember(new ProjectMember("2", "2", "3", "1"));
            batch.UpsertMember(new ProjectMember("3", "3", "4", "1"));
            batch.UpsertMember(new ProjectMember("4", "4", "1", "1"));
            batch.UpsertMember(new ProjectMember("5", "4", "5", "1"));
            batch.UpsertRigidZone(new RigidZoneDefinition("R1", "1", 0.25, 0.25, "1"));
            batch.UpsertSupportSet(new SupportSetDefinition("2", "Secondary",
                [new SupportConditionDefinition("SS1", "2", 1, 0, 0, 0, 0, 0)]));
            batch.UpsertPanel(new PanelDefinition("PL1", "1", ["1", "2", "3", "4"]));
            batch.UpsertJointReleaseSet(new JointReleaseSetDefinition("1", "Default",
                [new JointReleaseDefinition("J1", "1", true, true, true, true, true, true)]));
            batch.UpsertJointReleaseSet(new JointReleaseSetDefinition("2", "Alternate", []));
            batch.UpsertNoticePoint(new NoticePointDefinition("N1", "1", 2));
            batch.UpsertMemberSpringSet(new MemberSpringSetDefinition("1", "Default",
                [new MemberSpringDefinition("MS1", "1", 1, 0, 0, 0)]));
            batch.UpsertMemberSpringSet(new MemberSpringSetDefinition("2", "Alternate", []));
            batch.UpsertPrescribedDisplacement(new PrescribedDisplacementDefinition(
                "D1", "1", "2", 0.001, 0, 0, 0, 0, 0));
            batch.UpsertMemberLoad(new MemberLoadDefinition(
                "ML1", "1", "1", MemberLoadKind.PointForce, MemberLoadDirection.LocalY, 1, 0, 5, 0));
            batch.UpsertDerivedResult(new DerivedResultDefinition(
                "DF1", "Define 1", DerivedResultKind.Define, [new DerivedResultTerm("1", 1)]));
            batch.UpsertDerivedResult(new DerivedResultDefinition(
                "CB1", "Combine 1", DerivedResultKind.Combine, [new DerivedResultTerm("DF1", 1)]));
            batch.UpsertDerivedResult(new DerivedResultDefinition(
                "PK1", "Pickup 1", DerivedResultKind.Pickup, [new DerivedResultTerm("CB1", 1)]));
        }));
        return session.Current;
    }

    internal sealed class MemoryClipboard : IClipboardTextService
    {
        public string Text { get; set; } = string.Empty;

        public string GetText() => Text;

        public void SetText(string text) => Text = text;
    }

    private sealed class NoOpLayoutStore : IShellLayoutStore
    {
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task SaveAsync(string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
