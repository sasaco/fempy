using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Results;
using PDF_Manager.Core.Shell;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Viewport;

namespace PDF_Manager.UiTests;

public sealed class Step7ReviewRemediationTests
{
    [Fact]
    public void CtFixtureNavigatesExactFirstAndLastCasesAcrossSelectorsTablesAndScene()
    {
        StaTestRunner.Run(() =>
        {
            AnalysisResultSet results = ReadCtFixture();
            using ProjectDocumentContent content = CreateContent("ct");
            _ = content.ResultGrid.Handle;
            content.SetDocument(WithResultCases(ProjectDocumentPresets.CreateRepresentativeFrame(), results));
            content.SetResult(results);
            content.SetDisplayMode(ViewportDisplayMode.Displacements);
            content.FlushPendingUpdates();

            Assert.Equal(11, content.ResultCaseSelector.Items.Count);
            Assert.Equal(new ResultCoordinate("1", ResultStateKind.Static, 0), content.SelectedResultCoordinate);
            Assert.Contains("1", content.ResultCaseSelector.SelectedItem!.ToString(), StringComparison.Ordinal);
            Assert.Contains("固定死荷重", content.ResultCaseSelector.SelectedItem!.ToString(), StringComparison.Ordinal);
            Assert.Equal("0.001", FindResultRow(content, SceneEntityKind.Displacement, "2").Cells[1].Value);
            AssertSceneDisplacement(content, "1", "2", SceneResultStateKind.Static, 0, 0.001f, 0.0f);

            Assert.True(content.MoveToNextResult());
            Assert.Equal(new ResultCoordinate("2", ResultStateKind.Static, 0), content.SelectedResultCoordinate);

            content.ResultCaseSelector.SelectedIndex = 10;
            Application.DoEvents();
            content.FlushPendingUpdates();
            Assert.Equal(new ResultCoordinate("11", ResultStateKind.Static, 0), content.SelectedResultCoordinate);
            Assert.Contains("11", content.ResultCaseSelector.SelectedItem!.ToString(), StringComparison.Ordinal);
            Assert.Contains("風荷重", content.ResultCaseSelector.SelectedItem!.ToString(), StringComparison.Ordinal);
            Assert.Equal("0.011", FindResultRow(content, SceneEntityKind.Displacement, "2").Cells[1].Value);
            AssertSceneDisplacement(content, "11", "2", SceneResultStateKind.Static, 0, 0.011f, 0.0f);
            Assert.False(content.MoveToNextResult());
        }, "Step 7 Ct first and last result acceptance");
    }

    [Fact]
    public void ValidNonlinearModalAndStaticVariantsProjectTypedSceneGeometryAndValues()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent("scene-variants");
            _ = content.ResultGrid.Handle;
            content.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            content.SetDisplayMode(ViewportDisplayMode.Displacements);

            content.SetResult(ReadPositiveFixture("nonlinear-steps.json"));
            content.FlushPendingUpdates();
            AssertSceneDisplacement(content, "NL", "1", SceneResultStateKind.LoadStep, 0, 0.1f, 0.0f);
            content.ResultStateSelector.SelectedIndex = 1;
            Application.DoEvents();
            content.FlushPendingUpdates();
            AssertSceneDisplacement(content, "NL", "1", SceneResultStateKind.LoadStep, 1, 0.22f, 0.0f);

            content.SetResult(ReadPositiveFixture("modal.json"));
            content.FlushPendingUpdates();
            AssertSceneDisplacement(content, "1", "1", SceneResultStateKind.Mode, 0, 1.0f, 0.0f);
            content.ResultStateSelector.SelectedIndex = 1;
            Application.DoEvents();
            content.FlushPendingUpdates();
            AssertSceneDisplacement(content, "1", "1", SceneResultStateKind.Mode, 1, 0.0f, 1.0f);

            AnalysisResultSet ct = ReadCtFixture();
            content.SetDocument(WithResultCases(ProjectDocumentPresets.CreateRepresentativeFrame(), ct));
            content.SetResult(ct);
            content.SetDisplayMode(ViewportDisplayMode.Reactions);
            content.FlushPendingUpdates();
            SceneReaction reaction = Assert.Single(content.CurrentScene!.Reactions!.Reactions);
            Assert.Equal(new ScenePoint3(-1, 0, 0), reaction.Force);
            Assert.Equal(new SceneResultContext("1", SceneResultStateKind.Static, 0, 0, 11),
                content.CurrentScene.Reactions.Context);

            content.SetDisplayMode(ViewportDisplayMode.SectionForces);
            content.FlushPendingUpdates();
            SceneSectionForce[] forces = content.CurrentScene!.SectionForces!.Samples.ToArray();
            Assert.Equal(2, forces.Length);
            Assert.Equal(0.0f, forces[0].RelativePosition);
            Assert.Equal(new ScenePoint3(1, 0, 0), forces[0].Force);
            Assert.Equal(1.0f, forces[1].RelativePosition);
            Assert.Equal(new ScenePoint3(-1, 0, 0), forces[1].Force);
        }, "Step 7 valid static nonlinear modal scene acceptance");
    }

    [Fact]
    public void MovingParentSceneUsesChildOnlyAggregateWhileChildSelectionUsesThatChild()
    {
        StaTestRunner.Run(() =>
        {
            AnalysisResultSet results = CreateAngularMovingSet();
            MovingLoadDefinition moving = new("MOVING", "Moving load", ["1", "1.1", "1.2"]);
            ProjectDocument document = WithResultCases(
                ProjectDocumentPresets.CreateRepresentativeFrame(),
                results,
                [moving]);
            using ProjectDocumentContent content = CreateContent("moving-scene");
            _ = content.ResultGrid.Handle;
            content.SetDocument(document);
            content.SetResult(results);
            content.SetDisplayMode(ViewportDisplayMode.Reactions);
            content.SetExtremaMode(SceneExtremaMode.AbsoluteMaximum);
            content.FlushPendingUpdates();

            Assert.Equal(["MOVING", "2"],
                content.ResultParentSelector.Items.Cast<object>().Select(value => value.ToString()!.Split(' ')[0]));
            Assert.True(content.SelectedResultSourcePage!.IsParent);
            SceneReaction aggregate = Assert.Single(content.CurrentScene!.Reactions!.Reactions);
            Assert.Equal(new ScenePoint3(10, -20, 30), aggregate.Force);
            Assert.Equal(new ScenePoint3(-40, 50, -60), aggregate.Moment);

            content.ResultChildSelector.SelectedIndex = 2;
            Application.DoEvents();
            content.FlushPendingUpdates();
            Assert.Equal("1.2", content.SelectedResultSourcePage!.Result.CaseId);
            SceneReaction child = Assert.Single(content.CurrentScene!.Reactions!.Reactions);
            Assert.Equal(new ScenePoint3(-10, 20, -30), child.Force);
            Assert.Equal(new ScenePoint3(40, -50, 60), child.Moment);
        }, "Step 7 moving aggregate versus child scene");
    }

    private static void AssertSceneDisplacement(
        ProjectDocumentContent content,
        string caseId,
        string nodeId,
        SceneResultStateKind stateKind,
        int stateIndex,
        float expectedX,
        float expectedY)
    {
        SceneDisplacementLayer layer = Assert.IsType<SceneDisplacementLayer>(content.CurrentScene!.Displacement);
        Assert.Equal(caseId, layer.Context!.CaseId);
        Assert.Equal(stateKind, layer.Context.StateKind);
        Assert.Equal(stateIndex, layer.Context.StateIndex);
        SceneNodeDisplacement node = layer.Nodes.Single(value => value.NodeId == nodeId);
        Assert.Equal(expectedX, node.Vector.X);
        Assert.Equal(expectedY, node.Vector.Y);
    }

    private static DataGridViewRow FindResultRow(
        ProjectDocumentContent content,
        SceneEntityKind kind,
        string id) => content.ResultGrid.Rows.Cast<DataGridViewRow>().Single(row =>
        row.Tag is SceneEntityKey key && key == new SceneEntityKey(kind, id));

    private static ProjectDocumentContent CreateContent(string id) =>
        new(DocumentKey.Document($"step7-review-{id}"), new LocalizationService(UiLanguage.English));

    private static AnalysisResultSet ReadPositiveFixture(string fileName) => AnalysisResultSetJson.Deserialize(
        File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "FrameWeb",
            "tests",
            "data",
            "contracts",
            "positive",
            fileName)));

    private static AnalysisResultSet ReadCtFixture() => AnalysisResultSetJson.Deserialize(
        File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "FramePrintPDF",
            "PDF_Manager.Core.Tests",
            "Results",
            "Fixtures",
            "ct-analysis-result-set-v1.fixture")));

    private static AnalysisResultSet CreateAngularMovingSet()
    {
        AnalysisResultSet source = ReadPositiveFixture("single-static.json");
        StaticAnalysisResult template = Assert.IsType<StaticAnalysisResult>(source.Results[0]);
        AnalysisResultSet resultSet = new(
            source.Kind,
            source.SchemaVersion,
            source.Units,
            source.CoordinateSystem,
            [
                new AnalysisCase("1", "Moving load", "LL", AnalysisType.Static, ["1"]),
                new AnalysisCase("2", "Following dead load", "D", AnalysisType.Static, ["1"]),
                new AnalysisCase("1.1", "Moving load position 1", "LL", AnalysisType.Static, ["1"]),
                new AnalysisCase("1.2", "Moving load position 2", "LL", AnalysisType.Static, ["1"]),
            ],
            source.Topology,
            [
                WithReaction(template, "1", new ForceComponents(100, 100, 100, 100, 100, 100)),
                WithReaction(template, "2", new ForceComponents(200, 200, 200, 200, 200, 200)),
                WithReaction(template, "1.1", new ForceComponents(10, -20, 30, -40, 50, -60)),
                WithReaction(template, "1.2", new ForceComponents(-10, 20, -30, 40, -50, 60)),
            ]);
        AnalysisResultSetValidator.Validate(resultSet);
        return resultSet;
    }

    private static StaticAnalysisResult WithReaction(
        StaticAnalysisResult template,
        string caseId,
        ForceComponents components) => new(
        caseId,
        template.NodeDisplacements,
        [new SupportReaction(template.SupportReactions[0].NodeId, components)],
        template.MemberSectionForces,
        template.ShellResults,
        template.SolidResults,
        template.Diagnostics);

    private static ProjectDocument WithResultCases(
        ProjectDocument value,
        AnalysisResultSet results,
        IEnumerable<MovingLoadDefinition>? movingLoads = null)
    {
        string firstCaseId = results.Cases[0].CaseId;
        return new ProjectDocument(
            value.Version,
            value.Metadata,
            value.Nodes,
            value.Members,
            value.Supports,
            results.Cases.Select(resultCase => new LoadCaseDefinition(
                resultCase.CaseId,
                resultCase.Name,
                resultCase.Symbol)),
            value.NodalLoads.Select(row => row with { CaseId = firstCaseId }),
            [],
            movingLoads ?? [],
            value.Selection,
            value.IsDirty,
            value.Sections,
            value.Dimension,
            value.ElementPropertySets,
            value.RigidZones,
            value.SupportSets,
            value.Panels,
            value.JointReleaseSets,
            value.NoticePoints,
            value.MemberSpringSets,
            value.PrescribedDisplacements.Select(row => row with { CaseId = firstCaseId }),
            value.MemberLoads.Select(row => row with { CaseId = firstCaseId }));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
