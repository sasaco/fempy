using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Results;
using PDF_Manager.Core.Shell;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Viewport;

namespace PDF_Manager.UiTests;

public sealed class Step7ResultPresentationTests
{
    [Fact]
    public void ExplicitCaseAndStateSelectorsAddressFirstLastCasesAndEveryAcceptedState()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            _ = content.ResultGrid.Handle;

            content.SetResult(ReadFixture("multiple-static.json"));
            Assert.Equal(["10 · First", "2 · Second"], ItemText(content.ResultCaseSelector));
            Assert.Equal(["Static"], ItemText(content.ResultStateSelector));
            Assert.Equal(
                new ResultCoordinate("10", ResultStateKind.Static, 0),
                content.SelectedResultCoordinate);

            content.ResultCaseSelector.SelectedIndex = 1;
            Application.DoEvents();
            Assert.Equal(
                new ResultCoordinate("2", ResultStateKind.Static, 0),
                content.SelectedResultCoordinate);

            content.SetResult(ReadFixture("multiple-nonlinear.json"));
            Assert.Equal(["A · A", "B · B"], ItemText(content.ResultCaseSelector));
            Assert.Equal(["Load step 1"], ItemText(content.ResultStateSelector));
            Assert.Equal(
                new ResultCoordinate("A", ResultStateKind.LoadStep, 0),
                content.SelectedResultCoordinate);

            content.ResultCaseSelector.SelectedIndex = 1;
            Application.DoEvents();
            Assert.Equal(["Load step 1", "Load step 2"], ItemText(content.ResultStateSelector));
            content.ResultStateSelector.SelectedIndex = 1;
            Application.DoEvents();
            Assert.Equal(
                new ResultCoordinate("B", ResultStateKind.LoadStep, 1),
                content.SelectedResultCoordinate);
            Assert.Equal(2, content.ResultPageIndex);

            content.SetResult(ReadFixture("modal.json"));
            Assert.Equal(["1 · 1"], ItemText(content.ResultCaseSelector));
            Assert.Equal(["Mode 1", "Mode 2"], ItemText(content.ResultStateSelector));
            content.ResultStateSelector.SelectedIndex = 1;
            Application.DoEvents();
            Assert.Equal(
                new ResultCoordinate("1", ResultStateKind.Mode, 1),
                content.SelectedResultCoordinate);
        }, "Step 7 explicit case and state selectors");
    }

    [Fact]
    public void NonlinearAndModalDisplacementTablesKeepViewportSelectionStableAcrossStates()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            _ = content.ResultGrid.Handle;
            SceneEntityKey node = new(SceneEntityKind.Displacement, "1");

            content.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            content.SetResult(ReadFixture("nonlinear-steps.json"));
            SelectOnlyResultRow(content);
            Assert.Equal(node, content.Selection);
            Assert.Equal("0.1", content.ResultGrid.Rows[0].Cells[1].Value);

            content.ResultStateSelector.SelectedIndex = 1;
            Application.DoEvents();
            Assert.Equal(node, content.Selection);
            Assert.Equal("0.22", content.ResultGrid.Rows[0].Cells[1].Value);
            Assert.Equal(node, Assert.IsType<SceneEntityKey>(content.ResultGrid.SelectedRows[0].Tag));

            content.SetResult(ReadFixture("modal.json"));
            Assert.Equal(["Displacements"], content.ResultTableSelector.Items.Cast<object>()
                .Select(item => item.ToString()!));
            SelectOnlyResultRow(content);
            Assert.Equal(node, content.Selection);
            Assert.Equal("1", content.ResultGrid.Rows[0].Cells[1].Value);

            content.ResultStateSelector.SelectedIndex = 1;
            Application.DoEvents();
            Assert.Equal(node, content.Selection);
            Assert.Equal("1", content.ResultGrid.Rows[0].Cells[2].Value);
            Assert.Equal(node, Assert.IsType<SceneEntityKey>(content.ResultGrid.SelectedRows[0].Tag));

            Assert.Equal(0, content.ResultTableSelector.SelectedIndex);
        }, "Step 7 nonlinear and modal result table selection");
    }

    [Fact]
    public void InvalidPartialResultCannotEnableEmptyPresentationOrReplacePriorValidDisplay()
    {
        StaTestRunner.Run(() =>
        {
            (ProjectDocument source, AnalysisResultSet unvalidated) =
                Step6ViewportBehaviorTests.CreateSignedCaseFixture();
            AnalysisResultSet valid = NormalizeSignedResultSet(unvalidated);
            ProjectDocument document = WithPresentation(
                source,
                [new DerivedResultDefinition(
                    "PICKUP",
                    "Pickup",
                    DerivedResultKind.Pickup,
                    [new DerivedResultTerm("C1", 1), new DerivedResultTerm("C2", 1)])],
                [new MovingLoadDefinition("MOVING", "Moving", ["C1", "C2"])]);
            AnalysisResultSet partial = new(
                valid.Kind,
                valid.SchemaVersion,
                valid.Units,
                valid.CoordinateSystem,
                valid.Cases,
                valid.Topology,
                []);

            using (ProjectDocumentContent empty = CreateContent())
            {
                empty.SetDocument(document);
                Assert.Throws<AnalysisContractException>(() => empty.SetResult(partial));
                Assert.Null(empty.ResultSet);
                Assert.Null(empty.SelectedResultCoordinate);
                Assert.False(empty.ResultCaseSelector.Enabled);
                Assert.False(empty.ResultStateSelector.Enabled);
                Assert.False(empty.ResultCoordinateSelector.Enabled);
                Assert.False(empty.ResultDerivedSelector.Enabled);
                Assert.False(empty.ResultParentSelector.Enabled);
                Assert.False(empty.ResultChildSelector.Enabled);
                Assert.False(empty.ResultTableSelector.Enabled);
                Assert.False(empty.ResultExtremaSelector.Enabled);
                Assert.False(empty.ResultCsvExportButton.Enabled);
                Assert.False(empty.ResultPickupExportButton.Enabled);
                Assert.False(empty.CanExportSelectedResultCsv);
                Assert.False(empty.CanExportSelectedPickupCsv);
                Assert.Equal(0, empty.ResultPageCount);
                Assert.False(empty.MoveToPreviousResult());
                Assert.False(empty.MoveToNextResult());
                Assert.Empty(empty.ResultGrid.Rows.Cast<DataGridViewRow>());
            }

            using ProjectDocumentContent content = CreateContent();
            _ = content.ResultGrid.Handle;
            content.SetDocument(document);
            content.SetResult(valid);
            content.ResultChildSelector.SelectedIndex = 1;
            content.ResultDerivedSelector.SelectedIndex = 1;
            content.SetDisplayMode(ViewportDisplayMode.Displacements);
            Application.DoEvents();
            content.FlushPendingUpdates();
            ResultCoordinate priorCoordinate = Assert.IsType<ResultCoordinate>(content.SelectedResultCoordinate);
            string? priorDerived = content.SelectedDerivedResultId;
            ResultPresentationPage priorParent = Assert.IsType<ResultPresentationPage>(content.SelectedResultParentPage);
            ResultPresentationSourcePage priorSource = Assert.IsType<ResultPresentationSourcePage>(content.SelectedResultSourcePage);
            MovingLoadEnvelope priorMoving = Assert.IsType<MovingLoadEnvelope>(content.SelectedMovingLoadEnvelope);
            SceneDisplacementLayer priorScene = Assert.IsType<SceneDisplacementLayer>(content.CurrentScene!.Displacement);
            object?[][] priorRows = content.ResultGrid.Rows.Cast<DataGridViewRow>()
                .Select(row => row.Cells.Cast<DataGridViewCell>().Select(cell => cell.Value).ToArray())
                .ToArray();
            int priorTableIndex = content.ResultTableSelector.SelectedIndex;
            int priorExtremaIndex = content.ResultExtremaSelector.SelectedIndex;
            bool priorResultExportEnabled = content.ResultCsvExportButton.Enabled;
            bool priorCanExportResult = content.CanExportSelectedResultCsv;
            ResultExportArtifact priorPickup = content.ExportSelectedPickupCsv();

            Assert.Throws<AnalysisContractException>(() => content.SetResult(partial));

            Assert.Same(valid, content.ResultSet);
            Assert.Equal(priorCoordinate, content.SelectedResultCoordinate);
            Assert.Equal(priorDerived, content.SelectedDerivedResultId);
            Assert.Same(priorParent, content.SelectedResultParentPage);
            Assert.Same(priorSource, content.SelectedResultSourcePage);
            Assert.Same(priorMoving, content.SelectedMovingLoadEnvelope);
            Assert.Same(priorScene, content.CurrentScene!.Displacement);
            Assert.Equal(priorRows, content.ResultGrid.Rows.Cast<DataGridViewRow>()
                .Select(row => row.Cells.Cast<DataGridViewCell>().Select(cell => cell.Value).ToArray())
                .ToArray());
            Assert.Equal(priorTableIndex, content.ResultTableSelector.SelectedIndex);
            Assert.Equal(priorExtremaIndex, content.ResultExtremaSelector.SelectedIndex);
            Assert.Equal(priorResultExportEnabled, content.ResultCsvExportButton.Enabled);
            Assert.Equal(priorCanExportResult, content.CanExportSelectedResultCsv);
            Assert.True(content.ResultPickupExportButton.Enabled);
            Assert.True(content.CanExportSelectedPickupCsv);
            Assert.Equal(priorPickup.Utf8Bytes.ToArray(), content.ExportSelectedPickupCsv().Utf8Bytes.ToArray());
            Assert.True(content.ResultCaseSelector.Enabled);
            Assert.True(content.ResultStateSelector.Enabled);
            Assert.True(content.ResultCoordinateSelector.Enabled);
            Assert.True(content.ResultDerivedSelector.Enabled);
            Assert.True(content.ResultParentSelector.Enabled);
            Assert.True(content.ResultChildSelector.Enabled);
            Assert.True(content.ResultTableSelector.Enabled);
            Assert.True(content.ResultExtremaSelector.Enabled);
        }, "Step 7 invalid and partial result publication boundary");
    }

    [Fact]
    public void NonStaticDerivedOperandShowsTypedLocalizedErrorForNonlinearAndModalCases()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.English);
            using ProjectDocumentContent content = new(
                DocumentKey.Document("step7-derived-error"),
                localization);
            ProjectDocument source = ProjectDocumentPresets.CreateRepresentativeFrame();
            content.SetDocument(WithSingleLoadCase(
                source,
                "NL",
                [new DerivedResultDefinition(
                    "DERIVED",
                    "Derived",
                    DerivedResultKind.Define,
                    [new DerivedResultTerm("NL", 1)])]));

            content.SetResult(ReadFixture("nonlinear-steps.json"));

            ResultPresentationUiError nonlinearError =
                Assert.IsType<ResultPresentationUiError>(content.LastResultPresentationError);
            NonStaticDerivedOperandException nonlinearException =
                Assert.IsType<NonStaticDerivedOperandException>(nonlinearError.Exception);
            Assert.Equal(ResultPresentationErrorCode.StaticOperandsOnly, nonlinearError.Code);
            Assert.Equal("ResultDerivedStaticOnly", nonlinearError.ResourceKey);
            Assert.Equal("DERIVED", nonlinearException.DerivedResultId);
            Assert.Equal("NL", nonlinearException.OperandId);
            Assert.Equal(AnalysisType.MaterialNonlinear, nonlinearException.OperandAnalysisType);
            Assert.Equal(
                "Derived result 'DERIVED' accepts static operands only; 'NL' is not static.",
                content.ResultPresentationErrorLabel.Text);
            Assert.False(content.ResultDerivedSelector.Enabled);

            localization.SetLanguage(UiLanguage.Japanese);
            content.ApplyLocalization();
            Assert.Equal(
                "派生結果「DERIVED」に使用できるのは静的ケースのみです。「NL」は静的ケースではありません。",
                content.ResultPresentationErrorLabel.Text);
            localization.SetLanguage(UiLanguage.Chinese);
            content.ApplyLocalization();
            Assert.Equal(
                "派生结果“DERIVED”只能使用静力工况；“NL”不是静力工况。",
                content.ResultPresentationErrorLabel.Text);

            content.SetDocument(WithSingleLoadCase(
                source,
                "1",
                [new DerivedResultDefinition(
                    "MODAL-DERIVED",
                    "Modal derived",
                    DerivedResultKind.Pickup,
                    [new DerivedResultTerm("1", 1)])]));
            content.SetResult(ReadFixture("modal.json"));

            ResultPresentationUiError modalError =
                Assert.IsType<ResultPresentationUiError>(content.LastResultPresentationError);
            NonStaticDerivedOperandException modalException =
                Assert.IsType<NonStaticDerivedOperandException>(modalError.Exception);
            Assert.Equal("MODAL-DERIVED", modalException.DerivedResultId);
            Assert.Equal("1", modalException.OperandId);
            Assert.Equal(AnalysisType.Modal, modalException.OperandAnalysisType);
            Assert.Contains("MODAL-DERIVED", content.ResultPresentationErrorLabel.Text, StringComparison.Ordinal);
        }, "Step 7 typed localized derived-result rejection");
    }

    [Fact]
    public void StaticDerivedAndMovingLoadSelectorsKeepDeterministicPagesTablesAndProvenance()
    {
        StaTestRunner.Run(() =>
        {
            (ProjectDocument source, AnalysisResultSet unvalidatedResults) =
                Step6ViewportBehaviorTests.CreateSignedCaseFixture();
            AnalysisResultSet results = NormalizeSignedResultSet(unvalidatedResults);
            ProjectDocument document = WithPresentation(
                source,
                [
                    new DerivedResultDefinition(
                        "DEFINE",
                        "Define",
                        DerivedResultKind.Define,
                        [new DerivedResultTerm("C1", 2), new DerivedResultTerm("C2", 0.5)]),
                    new DerivedResultDefinition(
                        "COMBINE",
                        "Combine",
                        DerivedResultKind.Combine,
                        [new DerivedResultTerm("DEFINE", 2)]),
                    new DerivedResultDefinition(
                        "PICKUP",
                        "Pickup",
                        DerivedResultKind.Pickup,
                        [new DerivedResultTerm("C1", 1), new DerivedResultTerm("C2", 1)]),
                ],
                [new MovingLoadDefinition("MOVING", "Moving", ["C1", "C2"])]);
            using ProjectDocumentContent content = CreateContent();
            _ = content.ResultGrid.Handle;
            content.SetDocument(document);
            content.SetResult(results);

            Assert.Equal(
                ["Base results", "DEFINE · Define", "COMBINE · Combine", "PICKUP · Pickup"],
                ItemText(content.ResultDerivedSelector));
            content.ResultDerivedSelector.SelectedIndex = 1;
            Assert.Equal("DEFINE", content.SelectedDerivedResultId);
            Assert.Equal("-20", content.ResultGrid.Rows[0].Cells[1].Value);
            content.ResultDerivedSelector.SelectedIndex = 2;
            Assert.Equal("COMBINE", content.SelectedDerivedResultId);
            Assert.Equal("-40", content.ResultGrid.Rows[0].Cells[1].Value);
            content.ResultDerivedSelector.SelectedIndex = 3;
            Assert.Equal("PICKUP", content.SelectedDerivedResultId);
            Assert.Equal("-9", content.ResultGrid.Rows[0].Cells[1].Value);
            content.ResultDerivedSelector.SelectedIndex = 0;
            Assert.Null(content.SelectedDerivedResultId);
            Assert.Equal("-9", content.ResultGrid.Rows[0].Cells[1].Value);

            Assert.Equal(["MOVING · Moving"], ItemText(content.ResultParentSelector));
            Assert.Equal(["C1 · Static", "C2 · Static"], ItemText(content.ResultChildSelector));
            Assert.Equal("MOVING", content.SelectedResultParentPage?.PageId);
            Assert.True(content.SelectedResultSourcePage?.IsParent);
            Assert.Equal(["C1", "C2"], content.SelectedMovingLoadEnvelope?.SourceCaseIds);

            content.ResultChildSelector.SelectedIndex = 1;
            Application.DoEvents();
            Assert.Equal(
                new ResultCoordinate("C2", ResultStateKind.Static, 0),
                content.SelectedResultCoordinate);
            Assert.Equal("MOVING", content.SelectedResultParentPage?.PageId);
            Assert.False(content.SelectedResultSourcePage?.IsParent);
            Assert.Equal("C2", content.SelectedResultSourcePage?.Result.CaseId);

            Assert.Equal(
                ["Displacements", "Reactions", "Member forces", "Moving-load envelope"],
                content.ResultTableSelector.Items.Cast<object>().Select(item => item.ToString()!));
            content.ResultTableSelector.SelectedIndex = 3;
            DataGridViewRow reactionAbsolute = content.ResultGrid.Rows.Cast<DataGridViewRow>()
                .Single(row =>
                    Equals(row.Cells[0].Value, "Reactions") &&
                    Equals(row.Cells[1].Value, "1") &&
                    Equals(row.Cells[3].Value, "Fx"));
            Assert.Equal("-4", reactionAbsolute.Cells[8].Value);
            Assert.Equal("C2", reactionAbsolute.Cells[9].Value);
            DataGridViewRow memberAbsolute = content.ResultGrid.Rows.Cast<DataGridViewRow>()
                .Single(row =>
                    Equals(row.Cells[0].Value, "Member-force extrema") &&
                    Equals(row.Cells[3].Value, "Fx"));
            Assert.Equal("-12", memberAbsolute.Cells[8].Value);
            Assert.Equal("C1", memberAbsolute.Cells[9].Value);
        }, "Step 7 static derived and moving-load presentation");
    }

    private static void SelectOnlyResultRow(ProjectDocumentContent content)
    {
        DataGridViewRow row = Assert.Single(content.ResultGrid.Rows.Cast<DataGridViewRow>());
        SceneEntityKey key = Assert.IsType<SceneEntityKey>(row.Tag);
        content.SetSelection(key, ViewportSelectionOrigin.Table);
        Application.DoEvents();
        Assert.Equal(key, Assert.IsType<SceneEntityKey>(content.ResultGrid.SelectedRows[0].Tag));
    }

    private static string[] ItemText(ToolStripComboBox selector) =>
        selector.Items.Cast<object>().Select(item => item.ToString()!).ToArray();

    private static ProjectDocumentContent CreateContent() =>
        new(DocumentKey.Document("step7-results"), new LocalizationService(UiLanguage.English));

    private static AnalysisResultSet ReadFixture(string fileName)
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "FrameWeb",
            "tests",
            "data",
            "contracts",
            "positive",
            fileName);
        return AnalysisResultSetJson.Deserialize(File.ReadAllBytes(path));
    }

    private static ProjectDocument WithPresentation(
        ProjectDocument value,
        IEnumerable<DerivedResultDefinition> derivedResults,
        IEnumerable<MovingLoadDefinition> movingLoads) => new(
        value.Version,
        value.Metadata,
        value.Nodes,
        value.Members,
        value.Supports,
        value.LoadCases,
        value.NodalLoads,
        derivedResults,
        movingLoads,
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
        value.PrescribedDisplacements,
        value.MemberLoads);

    private static ProjectDocument WithSingleLoadCase(
        ProjectDocument value,
        string caseId,
        IEnumerable<DerivedResultDefinition> derivedResults)
    {
        LoadCaseDefinition loadCase = value.LoadCases[0] with
        {
            Id = caseId,
            Name = caseId,
            Symbol = caseId,
        };
        return new ProjectDocument(
            value.Version,
            value.Metadata,
            value.Nodes,
            value.Members,
            value.Supports,
            [loadCase],
            value.NodalLoads.Select(row => row with { CaseId = caseId }),
            derivedResults,
            [],
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
            value.PrescribedDisplacements.Select(row => row with { CaseId = caseId }),
            value.MemberLoads.Select(row => row with { CaseId = caseId }));
    }

    private static AnalysisResultSet NormalizeSignedResultSet(AnalysisResultSet value)
    {
        StaticAnalysisResult firstResult = Assert.IsType<StaticAnalysisResult>(value.Results[0]);
        double memberLength = firstResult.MemberSectionForces[0].Segments[0].Length;
        AnalysisTopology topology = new(
            value.Topology.Nodes,
            value.Topology.Members.Select(member => new TopologyMember(
                member.MemberId,
                member.NodeI,
                member.NodeJ,
                member.LocalFrame,
                [new MemberStation("S0", 0), new MemberStation("S1", memberLength)])),
            value.Topology.ShellElements,
            value.Topology.SolidElements);
        AnalysisResult[] results = value.Results.Select(result =>
        {
            StaticAnalysisResult source = Assert.IsType<StaticAnalysisResult>(result);
            return (AnalysisResult)new StaticAnalysisResult(
                source.CaseId,
                source.NodeDisplacements,
                source.SupportReactions,
                source.MemberSectionForces.Select(member => new MemberSectionForces(
                    member.MemberId,
                    member.Segments.Select(segment => new MemberSegmentResult(
                        "S0-S1",
                        "S0",
                        "S1",
                        segment.Length,
                        segment.IEnd,
                        segment.JEnd)))),
                source.ShellResults,
                source.SolidResults,
                source.Diagnostics);
        }).ToArray();
        AnalysisResultSet normalized = new(
            value.Kind,
            value.SchemaVersion,
            value.Units,
            value.CoordinateSystem,
            value.Cases,
            topology,
            results);
        AnalysisResultSetValidator.Validate(normalized);
        return normalized;
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
