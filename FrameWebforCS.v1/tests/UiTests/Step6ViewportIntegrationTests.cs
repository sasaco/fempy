using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Shell;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Viewport;

namespace PDF_Manager.UiTests;

public sealed class Step6ViewportIntegrationTests
{
    [Fact]
    public void CompleteDocumentProjectionCoalescesUpdatesAndInvalidatesOnlyAffectedLayers()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            ProjectDocument document = Step5EditorMatrixTests.CreateFullDocument();
            content.SetDocument(document);
            Assert.NotNull(content.CurrentScene);
            Assert.Equal(SceneLayerMask.All, content.LastInvalidatedLayers);
            Assert.Equal(1, content.SceneProjectionCount);

            ProjectDocumentEditSession session = new(document);
            NodalLoadDefinition load = document.NodalLoads[0];
            Assert.True(session.UpsertNodalLoad(load with { Fz = load.Fz - 1.0 }));
            IReadOnlyDictionary<SceneLayerKind, long> beforeLoad = content.LayerInvalidationCounts;
            content.SetDocument(session.Current);
            Assert.Equal(ViewportUpdateReason.Document, content.PendingUpdates);
            content.FlushPendingUpdates();

            Assert.Equal(SceneLayerMask.Loads, content.LastInvalidatedLayers);
            Assert.Equal(beforeLoad[SceneLayerKind.Loads] + 1, content.LayerInvalidationCounts[SceneLayerKind.Loads]);
            Assert.Equal(beforeLoad[SceneLayerKind.Nodes], content.LayerInvalidationCounts[SceneLayerKind.Nodes]);
            Assert.Equal(2, content.SceneProjectionCount);

            int dispatchBeforePresentation = content.UpdateDispatchCount;
            int coalescedBeforePresentation = content.CoalescedUpdateCount;
            content.SetDisplayMode(ViewportDisplayMode.Loads);
            content.SetExtremaMode(SceneExtremaMode.Maximum);
            content.SetDecorations(showGrid: false, showAxes: false, showLabels: false, showLegends: false);
            Assert.Equal(ViewportUpdateReason.Presentation, content.PendingUpdates);
            Assert.True(content.CoalescedUpdateCount >= coalescedBeforePresentation + 2);
            content.FlushPendingUpdates();

            Assert.Equal(dispatchBeforePresentation + 1, content.UpdateDispatchCount);
            Assert.Equal(
                SceneLayerMask.Loads | SceneLayerMask.Grid | SceneLayerMask.Axes | SceneLayerMask.Labels,
                content.LastInvalidatedLayers);
            Assert.Equal(3, content.SceneProjectionCount);

            content.SetDisplayMode(ViewportDisplayMode.Loads);
            content.SetExtremaMode(SceneExtremaMode.Maximum);
            content.SetDecorations(showGrid: false, showAxes: false, showLabels: false, showLegends: false);
            Assert.Equal(ViewportUpdateReason.None, content.PendingUpdates);
            content.FlushPendingUpdates();
            Assert.Equal(3, content.SceneProjectionCount);
        }, "Step 6 affected-layer invalidation and coalescing");
    }

    [Fact]
    public void TwoDimensionalAndThreeDimensionalDocumentsApplyExplicitCameraPolicies()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            ProjectDocument threeDimensional = ProjectDocumentPresets.Create(BuiltInProjectPreset.RamenViaduct);
            ProjectDocument twoDimensional = WithDimension(
                ProjectDocumentPresets.CreateRepresentativeFrame(),
                ModelDimension.TwoDimensional);

            content.SetDocument(threeDimensional);
            _ = content.CurrentScene;
            Assert.Equal(ViewportProjection.Perspective, content.Projection);

            content.SetDocument(twoDimensional);
            _ = content.CurrentScene;
            Assert.Equal(ViewportProjection.Orthographic, content.Projection);
            content.ToggleProjection();
            Assert.Equal(ViewportProjection.Orthographic, content.Projection);

            content.SetDocument(threeDimensional);
            _ = content.CurrentScene;
            Assert.Equal(ViewportProjection.Perspective, content.Projection);
        }, "Step 6 2D and 3D camera policies");
    }

    [Fact]
    public void LayerSelectorCaptionsAndIndicesMapToDisplayModesWithoutSwappingLoadsAndDisplacements()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            string[] expectedCaptions = ["Model", "Displacement", "Loads", "Reaction", "Section force"];
            Assert.Equal(expectedCaptions, content.ResultLayerSelector.Items.Cast<object>().Select(value => value.ToString()));

            foreach (ViewportDisplayMode mode in Enum.GetValues<ViewportDisplayMode>())
            {
                content.SetDisplayMode(mode);
                Assert.Equal((int)mode, content.ResultLayerSelector.SelectedIndex);
                Assert.Equal(mode, content.Presentation.DisplayMode);
            }
        }, "Step 6 display-mode selector mapping");
    }

    [Fact]
    public void ResultPagingExtremaAndTableViewportSelectionRemainStableBidirectionally()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            _ = content.ResultGrid.Handle;
            content.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            (_, AnalysisResultSet unvalidatedResults) =
                Step6ViewportBehaviorTests.CreateSignedCaseFixture();
            AnalysisResultSet results = NormalizeSignedResultSet(unvalidatedResults);
            content.SetResult(results);
            content.FlushPendingUpdates();

            Assert.Equal(results.Results.Count, content.ResultPageCount);
            Assert.Equal(results.Results[0].Coordinate, content.SelectedResultCoordinate);
            Assert.Equal(results.Results.Count, content.ResultCoordinateSelector.Items.Count);
            Assert.True(content.MoveToNextResult());
            Assert.Equal(results.Results[1].Coordinate, content.SelectedResultCoordinate);

            ResultCoordinate last = results.Results[^1].Coordinate;
            content.SetResultCoordinate(last);
            content.SetDisplayMode(ViewportDisplayMode.Displacements);
            foreach (SceneExtremaMode extrema in new[]
                     {
                         SceneExtremaMode.Minimum,
                         SceneExtremaMode.Maximum,
                         SceneExtremaMode.AbsoluteMaximum,
                     })
            {
                content.SetExtremaMode(extrema);
                content.FlushPendingUpdates();
                Assert.Equal(extrema, content.Presentation.ExtremaMode);
                Assert.Equal(extrema, content.CurrentScene!.Displacement!.Context!.Extrema);
            }
            Assert.Equal(last, content.SelectedResultCoordinate);
            Assert.Equal(SceneExtremaMode.AbsoluteMaximum, content.Presentation.ExtremaMode);
            Assert.NotEmpty(content.ResultGrid.Rows.Cast<DataGridViewRow>());

            DataGridViewRow displacementRow = content.ResultGrid.Rows.Cast<DataGridViewRow>()
                .First(row => row.Tag is SceneEntityKey);
            SceneEntityKey displacement = Assert.IsType<SceneEntityKey>(displacementRow.Tag);
            content.ResultGrid.ClearSelection();
            content.SetSelection(null, ViewportSelectionOrigin.Viewport);
            content.ResultGrid.CurrentCell = displacementRow.Cells[0];
            displacementRow.Selected = true;
            Application.DoEvents();
            Assert.Equal(displacement, content.Selection);

            content.SetResultCoordinate(results.Results[0].Coordinate);
            content.FlushPendingUpdates();
            content.ResultTableSelector.SelectedIndex = 1;
            SceneEntityKey reaction = content.ResultGrid.Rows.Cast<DataGridViewRow>()
                .Select(row => row.Tag)
                .OfType<SceneEntityKey>()
                .Single();
            Assert.Equal(SceneEntityKind.Reaction, reaction.Kind);
            content.SetSelection(reaction, ViewportSelectionOrigin.Viewport);
            DataGridViewRow selected = Assert.Single(
                content.ResultGrid.SelectedRows.Cast<DataGridViewRow>());
            Assert.Equal(reaction, Assert.IsType<SceneEntityKey>(selected.Tag));

            content.ResultTableSelector.SelectedIndex = 2;
            DataGridViewRow sectionForceRow = content.ResultGrid.Rows.Cast<DataGridViewRow>()
                .First(row => row.Tag is SceneEntityKey);
            SceneEntityKey sectionForce = Assert.IsType<SceneEntityKey>(sectionForceRow.Tag);
            Assert.Equal(SceneEntityKind.SectionForce, sectionForce.Kind);
            content.ResultGrid.ClearSelection();
            content.SetSelection(null, ViewportSelectionOrigin.Viewport);
            content.ResultGrid.CurrentCell = sectionForceRow.Cells[0];
            sectionForceRow.Selected = true;
            Application.DoEvents();
            Assert.Equal(sectionForce, content.Selection);

            SceneEntityKey currentDisplacement = new(
                SceneEntityKind.Displacement,
                Assert.Single(content.CurrentScene!.Displacement!.Nodes).NodeId);
            int selectionEvents = 0;
            content.SelectionChanged += (_, _) => selectionEvents++;
            content.SetSelection(currentDisplacement, ViewportSelectionOrigin.Viewport);
            content.SetSelection(currentDisplacement, ViewportSelectionOrigin.Viewport);
            Assert.InRange(selectionEvents, 0, 1);

            content.SetHover(new SceneEntityKey(SceneEntityKind.Node, "1"));
            content.SetHover(new SceneEntityKey(SceneEntityKind.Node, "1"));
            Assert.Equal(new SceneEntityKey(SceneEntityKind.Node, "1"), content.Hover);
        }, "Step 6 result paging extrema and bidirectional selection");
    }

    [Fact]
    public void NodeEditInvalidatesAndRecompilesEveryCoordinateDependentLayer()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            ProjectDocument document = Step5EditorMatrixTests.CreateFullDocument();
            content.SetDocument(document);
            content.SetDisplayMode(ViewportDisplayMode.Loads);
            ViewportSceneModel beforeScene = content.CurrentScene!;
            ViewportCameraState beforeCamera = content.Camera;
            ViewportSceneCommandBuffer before = ViewportSceneCompiler.Compile(
                beforeScene,
                beforeCamera,
                new Size(1000, 700));

            ProjectDocumentEditSession session = new(document);
            ProjectNode node = document.Nodes.Single(value => value.Id == "2");
            Assert.True(session.UpsertNode(node with { X = node.X + 1.5, Y = node.Y + 0.75 }));
            content.SetDocument(session.Current);
            content.FlushPendingUpdates();

            SceneLayerMask expected =
                SceneLayerMask.Nodes |
                SceneLayerMask.Members |
                SceneLayerMask.RigidZones |
                SceneLayerMask.Supports |
                SceneLayerMask.Springs |
                SceneLayerMask.Joints |
                SceneLayerMask.Panels |
                SceneLayerMask.NoticePoints |
                SceneLayerMask.Loads |
                SceneLayerMask.Results |
                SceneLayerMask.Grid |
                SceneLayerMask.Labels;
            Assert.Equal(expected, content.LastInvalidatedLayers);
            Assert.Equal(beforeCamera, content.Camera);

            ViewportSceneCommandBuffer after = ViewportSceneCompiler.Compile(
                content.CurrentScene!,
                content.Camera,
                new Size(1000, 700));
            foreach (SceneLayerKind kind in new[]
                     {
                         SceneLayerKind.Members,
                         SceneLayerKind.Supports,
                         SceneLayerKind.Springs,
                         SceneLayerKind.Panels,
                         SceneLayerKind.NoticePoints,
                         SceneLayerKind.Loads,
                     })
            {
                IReadOnlyList<SceneRenderVertex> beforeVertices = before.Layers.Single(value => value.Kind == kind).Vertices;
                IReadOnlyList<SceneRenderVertex> afterVertices = after.Layers.Single(value => value.Kind == kind).Vertices;
                Assert.NotEmpty(beforeVertices);
                Assert.False(beforeVertices.SequenceEqual(afterVertices), $"{kind} retained stale projected coordinates.");
            }
        }, "Step 6 coordinate dependency closure integration");
    }

    [Fact]
    public void ExtremaChangesSceneLegendAndEveryResultTableAndClearsExcludedSelection()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            _ = content.ResultGrid.Handle;
            (ProjectDocument document, AnalysisResultSet results) =
                Step6ViewportBehaviorTests.CreateSignedCaseFixture();
            results = NormalizeSignedResultSet(results);
            content.SetDocument(document);
            content.SetResult(results);
            content.SetDisplayMode(ViewportDisplayMode.Displacements);
            content.SetExtremaMode(SceneExtremaMode.Values);
            content.FlushPendingUpdates();

            content.ResultTableSelector.SelectedIndex = 0;
            SceneEntityKey positive = new(SceneEntityKind.Displacement, "2");
            content.SetSelection(positive, ViewportSelectionOrigin.Table);
            Assert.Equal(positive, content.Selection);

            content.SetExtremaMode(SceneExtremaMode.Minimum);
            Assert.Null(content.Selection);
            content.FlushPendingUpdates();
            Assert.Equal(["1"], content.CurrentScene!.Displacement!.Nodes.Select(value => value.NodeId));
            Assert.Equal(-9.0f, content.CurrentScene.Presentation.ColorLegend!.Entries[0].Value);
            Assert.Equal(-9.0f, content.CurrentScene.Presentation.ColorLegend.Entries[^1].Value);
            AssertResultKeys(content, 0, new SceneEntityKey(SceneEntityKind.Displacement, "1"));
            AssertResultKeys(content, 1, new SceneEntityKey(SceneEntityKind.Reaction, "1"));
            AssertResultKeys(content, 2, new SceneEntityKey(SceneEntityKind.SectionForce, "1/S0-S1/i"));

            content.SetExtremaMode(SceneExtremaMode.Maximum);
            content.FlushPendingUpdates();
            AssertResultKeys(content, 0, new SceneEntityKey(SceneEntityKind.Displacement, "2"));
            AssertResultKeys(content, 1, new SceneEntityKey(SceneEntityKind.Reaction, "2"));
            AssertResultKeys(content, 2, new SceneEntityKey(SceneEntityKind.SectionForce, "1/S0-S1/j"));

            content.SetExtremaMode(SceneExtremaMode.AbsoluteMaximum);
            content.FlushPendingUpdates();
            AssertResultKeys(content, 0, new SceneEntityKey(SceneEntityKind.Displacement, "1"));
            AssertResultKeys(content, 1, new SceneEntityKey(SceneEntityKind.Reaction, "1"));
            AssertResultKeys(content, 2, new SceneEntityKey(SceneEntityKind.SectionForce, "1/S0-S1/i"));
            Assert.Equal(new ResultCoordinate("C1", ResultStateKind.Static, 0), content.SelectedResultCoordinate);
        }, "Step 6 signed extrema integration");
    }

    [Fact]
    public void DocumentReplacementHomesNewSceneWhileOrdinaryEditPreservesCamera()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            ProjectDocument first = ProjectDocumentPresets.CreateRepresentativeFrame();
            ProjectDocument second = ProjectDocumentPresets.Create(BuiltInProjectPreset.RamenViaduct);

            content.SetDocument(first, resetCamera: true);
            _ = content.CurrentScene;
            ViewportCameraState firstCamera = content.Camera;

            content.SetDocument(second, resetCamera: true);
            ViewportSceneModel secondScene = content.CurrentScene!;
            ViewportCameraState expectedSecond = ViewportSceneCompiler.Home(
                secondScene,
                ViewportProjection.Perspective);
            Assert.Equal(expectedSecond, content.Camera);
            Assert.NotEqual(firstCamera, content.Camera);

            ViewportCameraState beforeEdit = content.Camera;
            ProjectDocumentEditSession edit = new(second);
            ProjectNode node = second.Nodes[0];
            Assert.True(edit.UpsertNode(node with { X = node.X + 3 }));
            content.SetDocument(edit.Current, resetCamera: false);
            _ = content.CurrentScene;
            Assert.Equal(beforeEdit, content.Camera);

            ProjectDocument twoDimensional = WithDimension(first, ModelDimension.TwoDimensional);
            content.SetDocument(twoDimensional, resetCamera: true);
            ViewportSceneModel twoDimensionalScene = content.CurrentScene!;
            Assert.Equal(ViewportCameraPolicy.TwoDimensional, content.CameraPolicy);
            Assert.Equal(
                ViewportSceneCompiler.Home(twoDimensionalScene, ViewportProjection.Orthographic),
                content.Camera);
        }, "Step 6 replacement camera reset policy");
    }

    [Fact]
    public void PngEntryPointRejectsInvalidLimitsBeforeCreatingAGlContext()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            Assert.Throws<ArgumentOutOfRangeException>(() => content.CaptureViewportPng(maximumWidth: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => content.CaptureViewportPng(maximumHeight: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => content.CaptureViewportPng(maximumEncodedBytes: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                content.CaptureViewportPng(maximumWidth: ProjectDocumentContent.MaximumPngDimension + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                content.CaptureViewportPng(maximumEncodedBytes: ProjectDocumentContent.MaximumPngEncodedBytes + 1));
        }, "Step 6 bounded PNG entry point");
    }

    [Fact]
    public void TypedViewportFailurePreservesOperationOriginalExceptionAndLocalizedSafeMessage()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            (ProjectDocument document, AnalysisResultSet source) =
                Step6ViewportBehaviorTests.CreateSignedCaseFixture();
            source = NormalizeSignedResultSet(source);
            StaticAnalysisResult valid = Assert.IsType<StaticAnalysisResult>(source.Results[0]);
            StaticAnalysisResult invalid = new(
                "C1",
                valid.NodeDisplacements.Select(row => row.NodeId == "1"
                    ? new NodeDisplacement(
                        row.NodeId,
                        new DisplacementComponents(double.MaxValue, 0, 0, 0, 0, 0))
                    : row),
                valid.SupportReactions,
                valid.MemberSectionForces,
                valid.ShellResults,
                valid.SolidResults,
                valid.Diagnostics);
            AnalysisResultSet invalidSet = new(
                source.Kind,
                source.SchemaVersion,
                source.Units,
                source.CoordinateSystem,
                source.Cases,
                source.Topology,
                [invalid, .. source.Results.Skip(1)]);
            List<ViewportOperationException> typed = [];
            List<string> legacy = [];
            content.ViewportOperationFailed += (_, eventArgs) => typed.Add(eventArgs.Failure);
            content.ViewportFailed += (_, eventArgs) => legacy.Add(eventArgs.Message);

            content.SetDocument(document);
            content.SetResult(invalidSet);
            content.FlushPendingUpdates();

            ViewportOperationException failure = Assert.Single(typed);
            Assert.Equal(ViewportOperation.SceneUpdate, failure.Operation);
            Assert.True(failure.IsExpected);
            Assert.IsType<ArgumentOutOfRangeException>(failure.InnerException);
            Assert.Equal("The OpenGL viewport is unavailable.", failure.SafeMessage);
            Assert.Equal(failure.SafeMessage, Assert.Single(legacy));
            Assert.Same(failure, content.LastViewportFailure);
        }, "Step 6 typed viewport failure contract");
    }

    [Fact]
    public void AllResultTablesUseOneDeterministicRowLimitAndExposeTruncation()
    {
        StaTestRunner.Run(() =>
        {
            using ProjectDocumentContent content = CreateContent();
            _ = content.ResultGrid.Handle;
            content.SetResult(CreateOversizedResultSet());

            AssertResultTable(
                content,
                tableIndex: 0,
                expectedRowCount: ProjectDocumentContent.MaximumResultTableRows,
                expectedLastDataKey: new SceneEntityKey(SceneEntityKind.Displacement, "N9998"));
            AssertResultTable(
                content,
                tableIndex: 1,
                expectedRowCount: ProjectDocumentContent.MaximumResultTableRows,
                expectedLastDataKey: new SceneEntityKey(SceneEntityKind.Reaction, "N9998"));
            AssertResultTable(
                content,
                tableIndex: 2,
                expectedRowCount: ProjectDocumentContent.MaximumResultTableRows - 1,
                expectedLastDataKey: new SceneEntityKey(SceneEntityKind.SectionForce, "M1/S4998-S4999/j"));
        }, "Step 6 bounded result tables");
    }

    private static void AssertResultTable(
        ProjectDocumentContent content,
        int tableIndex,
        int expectedRowCount,
        SceneEntityKey expectedLastDataKey)
    {
        content.ResultTableSelector.SelectedIndex = tableIndex;

        Assert.True(content.ResultTableTruncated);
        Assert.Equal(expectedRowCount, content.ResultGrid.Rows.Count);
        Assert.Equal(
            expectedLastDataKey,
            Assert.IsType<SceneEntityKey>(content.ResultGrid.Rows[^2].Tag));
        Assert.Null(content.ResultGrid.Rows[^1].Tag);
        Assert.Equal("Additional rows were omitted by the display limit.", content.ResultGrid.Rows[^1].Cells[0].Value);
    }

    private static void AssertResultKeys(
        ProjectDocumentContent content,
        int tableIndex,
        params SceneEntityKey[] expected)
    {
        content.ResultTableSelector.SelectedIndex = tableIndex;
        Assert.Equal(
            expected,
            content.ResultGrid.Rows.Cast<DataGridViewRow>()
                .Select(value => value.Tag)
                .OfType<SceneEntityKey>());
    }

    private static AnalysisResultSet CreateOversizedResultSet()
    {
        NodeDisplacement[] displacements = Enumerable.Range(0, ProjectDocumentContent.MaximumResultTableRows + 1)
            .Select(index => new NodeDisplacement($"N{index}", new DisplacementComponents(index, 0, 0, 0, 0, 0)))
            .ToArray();
        SupportReaction[] reactions = Enumerable.Range(0, ProjectDocumentContent.MaximumResultTableRows + 1)
            .Select(index => new SupportReaction($"N{index}", new ForceComponents(index, 0, 0, 0, 0, 0)))
            .ToArray();
        MemberSegmentResult[] segments = Enumerable.Range(0, (ProjectDocumentContent.MaximumResultTableRows / 2) + 1)
            .Select(index => new MemberSegmentResult(
                $"S{index}-S{index + 1}",
                $"S{index}",
                $"S{index + 1}",
                1.0,
                new ForceComponents(index, 0, 0, 0, 0, 0),
                new ForceComponents(index, 0, 0, 0, 0, 0)))
            .ToArray();
        TopologyNode[] nodes = Enumerable.Range(0, ProjectDocumentContent.MaximumResultTableRows + 1)
            .Select(index => new TopologyNode(
                $"N{index}",
                new Vector3Value(index, 0, 0),
                $"N{index}",
                false))
            .ToArray();
        MemberStation[] stations = Enumerable.Range(0, segments.Length + 1)
            .Select(index => new MemberStation($"S{index}", index))
            .ToArray();
        AnalysisTopology topology = new(
            nodes,
            [new TopologyMember(
                "M1",
                "N0",
                $"N{segments.Length}",
                new CoordinateFrame(
                    new Vector3Value(0, 0, 0),
                    new Vector3Value(1, 0, 0),
                    new Vector3Value(0, 1, 0),
                    new Vector3Value(0, 0, 1)),
                stations)],
            [],
            []);
        StaticAnalysisResult result = new(
            "C1",
            displacements,
            reactions,
            [new MemberSectionForces("M1", segments)],
            [],
            [],
            new WarningDiagnostics([]));
        return new AnalysisResultSet(
            AnalysisResultSet.ContractKind,
            AnalysisResultSet.ContractVersion,
            new AnalysisUnits("SI", "m", "N", "kg", "s"),
            new CoordinateSystem("global_cartesian", "right", ["x", "y", "z"]),
            [new AnalysisCase("C1", "Case", "C1", AnalysisType.Static, nodes.Select(node => node.NodeId))],
            topology,
            [result]);
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

    private static ProjectDocumentContent CreateContent() =>
        new(DocumentKey.Document("step6-viewport"), new LocalizationService(UiLanguage.English));

    private static ProjectDocument WithDimension(ProjectDocument value, ModelDimension dimension) =>
        new(
            value.Version,
            value.Metadata,
            value.Nodes.Select(node => node with { Z = 0 }),
            value.Members,
            value.Supports,
            value.LoadCases,
            value.NodalLoads,
            value.DerivedResults,
            value.MovingLoads,
            value.Selection,
            value.IsDirty,
            value.Sections,
            dimension,
            value.ElementPropertySets,
            value.RigidZones,
            value.SupportSets,
            value.Panels,
            value.JointReleaseSets,
            value.NoticePoints,
            value.MemberSpringSets,
            value.PrescribedDisplacements,
            value.MemberLoads);
}
