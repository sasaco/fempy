using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Editing;
using PDF_Manager.Shell.ScreenComposition.Core;
using PDF_Manager.Shell.ScreenComposition.Surfaces;

namespace PDF_Manager.UiTests;

public sealed class Step5EditorMatrixTests
{
    [Fact]
    public void AngularRoutesExposeExactTableGroupsInsteadOfOneTwentyOneTabPane()
    {
        (ScreenRouteId Route, InputTableKey[] Tables)[] expected =
        [
            (ScreenRouteId.InputElements, [InputTableKey.Sections, InputTableKey.ElementPropertySets, InputTableKey.ModelSettings]),
            (ScreenRouteId.InputNodes, [InputTableKey.Nodes]),
            (ScreenRouteId.InputSupports, [InputTableKey.Supports, InputTableKey.SupportSets]),
            (ScreenRouteId.InputMembers, [InputTableKey.Members]),
            (ScreenRouteId.InputRigidZone, [InputTableKey.RigidZones]),
            (ScreenRouteId.InputPanel, [InputTableKey.Panels]),
            (ScreenRouteId.InputJoints, [InputTableKey.Joints, InputTableKey.JointReleaseSets]),
            (ScreenRouteId.InputNoticePoints, [InputTableKey.NoticePoints]),
            (ScreenRouteId.InputMemberSprings, [InputTableKey.MemberSprings, InputTableKey.MemberSpringSets]),
            (ScreenRouteId.InputLoadNames, [InputTableKey.LoadCases]),
            (ScreenRouteId.InputLoads, [InputTableKey.NodalLoads, InputTableKey.MemberLoads, InputTableKey.PrescribedDisplacements]),
            (ScreenRouteId.InputDefine, [InputTableKey.Define]),
            (ScreenRouteId.InputCombine, [InputTableKey.Combine]),
            (ScreenRouteId.InputPickup, [InputTableKey.Pickup]),
        ];

        Assert.Equal(expected.Length, FrameWebSurfaceCatalog.InputRoutes.Count);
        Assert.Equal(
            expected.Select(item => item.Route),
            FrameWebSurfaceCatalog.InputRoutes.Select(item => item.Route));
        foreach ((ScreenRouteId route, InputTableKey[] tables) in expected)
        {
            Assert.Equal(tables, FrameWebSurfaceCatalog.GetInput(route).Tables);
        }

        InputTableKey[] mapped = FrameWebSurfaceCatalog.InputRoutes.SelectMany(route => route.Tables).ToArray();
        Assert.Equal(Enum.GetValues<InputTableKey>().Order(), mapped.Order());
        Assert.Equal(mapped.Length, mapped.Distinct().Count());
    }

    [Fact]
    public void RouteSurfaceAttachesOnlyItsOrderedTablesAndFields()
    {
        StaTestRunner.Run(() =>
        {
            using EditorContent editor = CreateEditor();
            editor.SetDocument(CreateFullDocument());

            foreach (InputRouteSurfaceDefinition definition in FrameWebSurfaceCatalog.InputRoutes)
            {
                using InputRouteSurfaceControl surface = new(
                    new LocalizationService(UiLanguage.English),
                    editor);
                surface.ApplyState(CreateState(definition.Route, ViewDimension.ThreeDimensional));

                Assert.Equal(definition.Route, surface.RouteKey);
                Assert.Equal(definition.Tables, surface.VisibleTableKeys);
                Assert.Equal(definition.Tables[0], surface.ActiveTableKey);
                Assert.Same(editor.Tables[definition.Tables[0]], surface.PrimaryGrid);
                Assert.NotEmpty(surface.VisibleFieldIds);
                foreach (InputTableKey table in definition.Tables)
                {
                    surface.ShowTable(table);
                    Assert.Equal(table, surface.ActiveTableKey);
                    Assert.Same(editor.Tables[table], surface.PrimaryGrid);
                    Assert.NotNull(surface.PrimaryGrid.Parent);
                }
            }
        }, "Step 5 route table matrix");
    }

    [Fact]
    public void RepresentativeElementsNodesAndSupportsExposeSourceOrderedFields()
    {
        StaTestRunner.Run(() =>
        {
            using EditorContent editor = CreateEditor();
            editor.SetDocument(CreateFullDocument());

            AssertFields(editor, ScreenRouteId.InputElements, InputTableKey.Sections,
                "e", "g", "xp", "area", "j", "iy", "iz", "nu");
            AssertFields(editor, ScreenRouteId.InputNodes, InputTableKey.Nodes, "x", "y", "z");
            AssertFields(editor, ScreenRouteId.InputSupports, InputTableKey.Supports,
                "node", "tx", "ty", "tz", "rx", "ry", "rz");
        }, "Step 5 representative route fields");
    }

    [Fact]
    public void RouteSurfaceSwitchesBetweenTwoAndThreeDimensionalFields()
    {
        StaTestRunner.Run(() =>
        {
            using EditorContent editor = CreateEditor();
            editor.SetDocument(CreateFullDocument());
            using InputRouteSurfaceControl nodes = new(new LocalizationService(UiLanguage.English), editor);

            nodes.ApplyState(CreateState(ScreenRouteId.InputNodes, ViewDimension.ThreeDimensional));
            Assert.True(nodes.IsThreeDimensional);
            Assert.Contains("z", nodes.VisibleFieldIds);

            nodes.ApplyState(CreateState(ScreenRouteId.InputNodes, ViewDimension.TwoDimensional));
            Assert.False(nodes.IsThreeDimensional);
            Assert.DoesNotContain("z", nodes.VisibleFieldIds);
            Assert.Contains("x", nodes.VisibleFieldIds);
            Assert.Contains("y", nodes.VisibleFieldIds);

            using InputRouteSurfaceControl supports = new(new LocalizationService(UiLanguage.English), editor);
            supports.ApplyState(CreateState(ScreenRouteId.InputSupports, ViewDimension.TwoDimensional));
            Assert.DoesNotContain("tz", supports.VisibleFieldIds);
            Assert.DoesNotContain("rx", supports.VisibleFieldIds);
            Assert.DoesNotContain("ry", supports.VisibleFieldIds);
            Assert.Contains("tx", supports.VisibleFieldIds);
            Assert.Contains("ty", supports.VisibleFieldIds);
            Assert.Contains("rz", supports.VisibleFieldIds);

            using InputRouteSurfaceControl panel = new(new LocalizationService(UiLanguage.English), editor);
            Assert.Throws<ArgumentException>(() =>
                panel.ApplyState(CreateState(ScreenRouteId.InputPanel, ViewDimension.TwoDimensional)));
        }, "Step 5 2D and 3D field visibility");
    }

    [Fact]
    public void RuntimeLocalizationUpdatesEveryRouteGridHeader()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.English);
            using EditorContent editor = new(DocumentKey.Tool("step5-editor"), localization, new MemoryClipboard());
            editor.SetDocument(CreateFullDocument());
            using InputRouteSurfaceControl surface = new(localization, editor);
            surface.ApplyState(CreateState(ScreenRouteId.InputJoints, ViewDimension.ThreeDimensional));
            string[] englishHeaders = surface.PrimaryGrid.Columns.Cast<DataGridViewColumn>()
                .Select(column => column.HeaderText)
                .ToArray();

            localization.SetLanguage(UiLanguage.Japanese);
            editor.ApplyLocalization();
            string[] japaneseHeaders = surface.PrimaryGrid.Columns.Cast<DataGridViewColumn>()
                .Select(column => column.HeaderText)
                .ToArray();
            Assert.Equal(englishHeaders.Length, japaneseHeaders.Length);
            Assert.True(
                englishHeaders.Zip(japaneseHeaders).Any(pair => pair.First != pair.Second),
                "At least one route grid header must change with the runtime language.");
            Assert.All(surface.PrimaryGrid.Columns.Cast<DataGridViewColumn>(),
                column => Assert.False(string.IsNullOrWhiteSpace(column.HeaderText)));

            localization.SetLanguage(UiLanguage.Chinese);
            editor.ApplyLocalization();
            foreach (InputTableKey table in surface.VisibleTableKeys)
            {
                surface.ShowTable(table);
                Assert.All(surface.PrimaryGrid.Columns.Cast<DataGridViewColumn>(),
                    column => Assert.False(string.IsNullOrWhiteSpace(column.HeaderText)));
            }
        }, "Step 5 route localization");
    }

    [Fact]
    public void RepeatedRouteSurfaceLifecycleIsIdempotentAndCreatingThreadOnly()
    {
        StaTestRunner.Run(() =>
        {
            for (int iteration = 0; iteration < 5; iteration++)
            {
                using EditorContent editor = CreateEditor();
                editor.SetDocument(CreateFullDocument());
                InputRouteSurfaceControl surface = new(new LocalizationService(UiLanguage.English), editor);
                surface.ApplyState(CreateState(ScreenRouteId.InputElements, ViewDimension.ThreeDimensional));
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                    Task.Run(() => surface.ShowTable(InputTableKey.ModelSettings)).GetAwaiter().GetResult());
                Assert.Contains("creating thread", error.Message, StringComparison.Ordinal);
                surface.Dispose();
                surface.Dispose();
            }
        }, "Step 5 route lifecycle");
    }

    [Fact]
    public void EveryRenderableInputUsesBidirectionalStableSelectionKeysThroughRoutes()
    {
        StaTestRunner.Run(() =>
        {
            using EditorContent editor = CreateEditor();
            editor.SetDocument(CreateFullDocument());
            SceneEntityKey? tableSelection = null;
            editor.SelectionChanged += (_, eventArgs) => tableSelection = eventArgs.Selection;
            (ScreenRouteId Route, InputTableKey Table, SceneEntityKey Key)[] cases =
            [
                (ScreenRouteId.InputNodes, InputTableKey.Nodes, new SceneEntityKey(SceneEntityKind.Node, "2")),
                (ScreenRouteId.InputMembers, InputTableKey.Members, new SceneEntityKey(SceneEntityKind.Member, "1")),
                (ScreenRouteId.InputSupports, InputTableKey.Supports, new SceneEntityKey(SceneEntityKind.Support, "S1")),
                (ScreenRouteId.InputLoads, InputTableKey.NodalLoads, new SceneEntityKey(SceneEntityKind.NodalLoad, "P1")),
                (ScreenRouteId.InputLoads, InputTableKey.MemberLoads, new SceneEntityKey(SceneEntityKind.MemberLoad, "ML1")),
            ];

            foreach ((ScreenRouteId route, InputTableKey table, SceneEntityKey key) in cases)
            {
                using InputRouteSurfaceControl surface = new(new LocalizationService(UiLanguage.English), editor);
                surface.ApplyState(CreateState(route, ViewDimension.ThreeDimensional));
                surface.ShowTable(table);
                DataGridView grid = surface.PrimaryGrid;
                DataGridViewRow row = grid.Rows.Cast<DataGridViewRow>()
                    .Single(candidate => candidate.Tag is SceneEntityKey candidateKey && candidateKey == key);
                grid.ClearSelection();
                grid.CurrentCell = row.Cells.Cast<DataGridViewCell>().First(cell => cell.Visible);
                row.Selected = true;
                Application.DoEvents();
                Assert.Equal(key, tableSelection);

                grid.ClearSelection();
                editor.SetSelection(null);
                editor.SetSelection(key);
                Application.DoEvents();
                Assert.Equal(key, Assert.IsType<SceneEntityKey>(Assert.Single(grid.SelectedRows.Cast<DataGridViewRow>()).Tag));
            }
        }, "Step 5 bidirectional route selection");
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

    private static void AssertFields(
        EditorContent editor,
        ScreenRouteId route,
        InputTableKey table,
        params string[] expected)
    {
        using InputRouteSurfaceControl surface = new(new LocalizationService(UiLanguage.English), editor);
        surface.ApplyState(CreateState(route, ViewDimension.ThreeDimensional));
        surface.ShowTable(table);
        Assert.Equal(expected, surface.VisibleFieldIds);
    }

    private static ScreenRouteState CreateState(ScreenRouteId route, ViewDimension dimension) => new(
        route,
        AngularScreenManifest.GetContext(route),
        dimension,
        ScreenPageState.Single,
        resultsEnabled: false,
        ScreenOverlayKind.None);
}
