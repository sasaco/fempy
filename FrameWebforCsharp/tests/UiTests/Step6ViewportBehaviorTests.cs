using System.Diagnostics;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Rendering.Scene;
using PDF_Manager.Shell.Viewport;
using Xunit.Abstractions;

namespace PDF_Manager.UiTests;

public sealed class Step6ViewportBehaviorTests(ITestOutputHelper output)
{
    [Fact]
    public void AllFourBuiltInPresets_ProjectAndCompile160FramesWithinTenSecondBudget()
    {
        const int iterations = 10;
        const int maximumVerticesPerFrame = 25_000;
        const int maximumHitTargetsPerFrame = 10_000;
        TimeSpan elapsedBudget = TimeSpan.FromSeconds(10);
        ProjectDocumentSceneProjector projector = new();
        Size viewport = new(1280, 720);
        ViewportSceneProjectionText text = new("Scale", "Color");
        ViewportPresentationState[] states =
        [
            ViewportPresentationState.Default,
            ViewportPresentationState.Default with { DisplayMode = ViewportDisplayMode.Loads },
        ];

        foreach (BuiltInProjectPresetDescriptor descriptor in ProjectDocumentPresets.Catalog)
        {
            ProjectDocument document = ProjectDocumentPresets.Create(descriptor.Id);
            ViewportSceneProjection warmup = projector.Project(
                descriptor.StableId,
                document,
                resultSet: null,
                result: null,
                pageIndex: 0,
                pageCount: 0,
                presentation: states[0],
                text: text);
            _ = ViewportSceneCompiler.Compile(
                warmup.Scene,
                ViewportSceneCompiler.Home(warmup.Scene, ViewportProjection.Orthographic),
                viewport);

            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch stopwatch = Stopwatch.StartNew();
            int frames = 0;
            int maximumVertices = 0;
            int maximumHitTargets = 0;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                foreach (ViewportPresentationState state in states)
                {
                    ViewportSceneProjection projection = projector.Project(
                        descriptor.StableId,
                        document,
                        resultSet: null,
                        result: null,
                        pageIndex: 0,
                        pageCount: 0,
                        presentation: state,
                        text: text);
                    foreach (ViewportProjection camera in Enum.GetValues<ViewportProjection>())
                    {
                        ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
                            projection.Scene,
                            ViewportSceneCompiler.Home(projection.Scene, camera),
                            viewport);
                        frames++;
                        maximumVertices = Math.Max(maximumVertices, commands.Vertices.Count);
                        maximumHitTargets = Math.Max(maximumHitTargets, commands.HitTargets.Count);
                        Assert.InRange(commands.Vertices.Count, 1, maximumVerticesPerFrame);
                        Assert.InRange(commands.HitTargets.Count, 1, maximumHitTargetsPerFrame);
                    }
                }
            }

            stopwatch.Stop();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
            output.WriteLine(
                "{0}: frames={1}, elapsed_ms={2:F3}, allocated_bytes={3}, max_vertices={4}, max_hit_targets={5}",
                descriptor.StableId,
                frames,
                stopwatch.Elapsed.TotalMilliseconds,
                allocatedBytes,
                maximumVertices,
                maximumHitTargets);
            Assert.Equal(iterations * states.Length * Enum.GetValues<ViewportProjection>().Length, frames);
            Assert.True(
                stopwatch.Elapsed <= elapsedBudget,
                $"{descriptor.StableId} exceeded the {elapsedBudget.TotalSeconds:F0}s responsiveness budget: " +
                $"{stopwatch.Elapsed.TotalMilliseconds:F3}ms for {frames} frames.");
        }
    }

    [Theory]
    [InlineData(ViewportDisplayMode.Model, SceneLayerMask.Geometry | SceneLayerMask.Decorations)]
    [InlineData(ViewportDisplayMode.Loads, SceneLayerMask.Geometry | SceneLayerMask.Loads | SceneLayerMask.Decorations)]
    [InlineData(
        ViewportDisplayMode.Displacements,
        SceneLayerMask.Nodes | SceneLayerMask.Members | SceneLayerMask.Displacements | SceneLayerMask.Decorations)]
    [InlineData(
        ViewportDisplayMode.Reactions,
        SceneLayerMask.Nodes | SceneLayerMask.Members | SceneLayerMask.Supports |
        SceneLayerMask.Reactions | SceneLayerMask.Decorations)]
    [InlineData(
        ViewportDisplayMode.SectionForces,
        SceneLayerMask.Nodes | SceneLayerMask.Members | SceneLayerMask.SectionForces | SceneLayerMask.Decorations)]
    public void PresentationModesExposeOnlyTheirIndependentLayers(
        ViewportDisplayMode mode,
        SceneLayerMask expected)
    {
        ViewportPresentationState state = ViewportPresentationState.Default with { DisplayMode = mode };

        Assert.Same(state, state.Validate());
        Assert.Equal(expected, state.VisibleLayers);
    }

    [Fact]
    public void PresentationStateRejectsInvalidModeExtremaAndScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (ViewportPresentationState.Default with { DisplayMode = (ViewportDisplayMode)int.MaxValue }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (ViewportPresentationState.Default with { ExtremaMode = (SceneExtremaMode)int.MaxValue }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (ViewportPresentationState.Default with { DisplacementScale = 0.0f }).Validate());
    }

    [Fact]
    public void ProjectorUsesCameraAlignedGridPlanesForTwoAndThreeDimensionalDocuments()
    {
        ProjectDocumentSceneProjector projector = new();
        ProjectDocument threeDimensional = ProjectDocumentPresets.Create(BuiltInProjectPreset.RamenViaduct);
        ProjectDocument twoDimensional = WithDimension(
            ProjectDocumentPresets.CreateRepresentativeFrame(),
            ModelDimension.TwoDimensional);

        ViewportSceneProjection threeDimensionalProjection = Project(projector, threeDimensional);
        ViewportSceneProjection twoDimensionalProjection = Project(projector, twoDimensional);

        Assert.Equal(SceneGridPlane.XY, threeDimensionalProjection.Scene.Presentation.Grid!.Plane);
        Assert.Equal(SceneGridPlane.XZ, twoDimensionalProjection.Scene.Presentation.Grid!.Plane);
    }

    [Theory]
    [InlineData(SceneExtremaMode.Values, 2, "1", "2", -9.0f, 9.0f)]
    [InlineData(SceneExtremaMode.Minimum, 1, "1", "1", -9.0f, -9.0f)]
    [InlineData(SceneExtremaMode.Maximum, 1, "2", "2", 9.0f, 9.0f)]
    [InlineData(SceneExtremaMode.AbsoluteMaximum, 1, "1", "1", -9.0f, -9.0f)]
    public void ProjectorAppliesSignedExtremaToSceneAndLegendWithStableTies(
        SceneExtremaMode extrema,
        int expectedCount,
        string expectedFirstNode,
        string expectedLastNode,
        float expectedLegendMinimum,
        float expectedLegendMaximum)
    {
        (ProjectDocument document, AnalysisResultSet results) = CreateSignedCaseFixture();
        ProjectDocumentSceneProjector projector = new();
        ViewportPresentationState state = ViewportPresentationState.Default with
        {
            DisplayMode = ViewportDisplayMode.Displacements,
            ExtremaMode = extrema,
        };

        ViewportSceneProjection projection = projector.Project(
            "signed-extrema",
            document,
            results,
            results.Results[0],
            0,
            results.Results.Count,
            state,
            new ViewportSceneProjectionText("Scale", "Color"));

        Assert.Equal(expectedCount, projection.Scene.Displacement!.Nodes.Count);
        Assert.Equal(expectedFirstNode, projection.Scene.Displacement.Nodes[0].NodeId);
        Assert.Equal(expectedLastNode, projection.Scene.Displacement.Nodes[^1].NodeId);
        Assert.Equal(expectedCount, projection.Scene.Reactions!.Reactions.Count);
        Assert.Equal(expectedCount, projection.Scene.SectionForces!.Samples.Count);
        if (extrema != SceneExtremaMode.Values)
        {
            Assert.Equal(
                extrema == SceneExtremaMode.Maximum ? "1/S1/j" : "1/S1/i",
                projection.Scene.SectionForces.Samples[0].Id);
        }

        SceneColorLegend legend = projection.Scene.Presentation.ColorLegend!;
        Assert.Equal(expectedLegendMinimum, legend.Entries[0].Value);
        Assert.Equal(expectedLegendMaximum, legend.Entries[^1].Value);
        Assert.Equal(extrema, projection.Scene.Displacement.Context!.Extrema);
    }

    [Fact]
    public void ProjectorNeverMixesLoadsFromDifferentActiveResultCases()
    {
        (ProjectDocument document, AnalysisResultSet results) = CreateSignedCaseFixture();
        ProjectDocumentSceneProjector projector = new();

        ViewportSceneModel first = projector.Project(
            "case-filter",
            document,
            results,
            results.Results[0],
            0,
            2,
            ViewportPresentationState.Default with { DisplayMode = ViewportDisplayMode.Loads },
            new ViewportSceneProjectionText("Scale", "Color")).Scene;
        ViewportSceneModel second = projector.Project(
            "case-filter",
            document,
            results,
            results.Results[1],
            1,
            2,
            ViewportPresentationState.Default with { DisplayMode = ViewportDisplayMode.Loads },
            new ViewportSceneProjectionText("Scale", "Color"),
            first).Scene;

        Assert.Equal(["NL-C1"], first.NodalLoads.Select(value => value.Id));
        Assert.Equal(["ML-C1"], first.MemberLoads.Select(value => value.Id));
        Assert.Equal(["PD-C1"], first.PrescribedDisplacements.Select(value => value.Id));
        Assert.Equal(["NL-C2"], second.NodalLoads.Select(value => value.Id));
        Assert.Equal(["ML-C2"], second.MemberLoads.Select(value => value.Id));
        Assert.Equal(["PD-C2"], second.PrescribedDisplacements.Select(value => value.Id));
    }

    public static IEnumerable<object[]> PointMomentDirectionCases()
    {
        yield return [MemberLoadDirection.LocalX, 1.0, 0.0, 0.0];
        yield return [MemberLoadDirection.LocalY, 0.0, 0.0, 1.0];
        yield return [MemberLoadDirection.LocalZ, 0.0, -1.0, 0.0];
        yield return [MemberLoadDirection.GlobalX, 1.0, 0.0, 0.0];
        yield return [MemberLoadDirection.GlobalY, 0.0, 1.0, 0.0];
        yield return [MemberLoadDirection.GlobalZ, 0.0, 0.0, 1.0];
    }

    [Theory]
    [MemberData(nameof(PointMomentDirectionCases))]
    public void ProjectorMapsEveryPointMomentDirectionWithExactStationsAndVectors(
        MemberLoadDirection direction,
        double expectedX,
        double expectedY,
        double expectedZ)
    {
        ProjectDocument source = ProjectDocumentPresets.Create(BuiltInProjectPreset.RamenViaduct);
        ProjectMember rotated = source.Members.Single(value => value.Id == "2") with { RotationDegrees = 90 };
        ProjectDocument document = WithMemberLoads(
            source,
            source.Members.Select(value => value.Id == rotated.Id ? rotated : value),
            [new MemberLoadDefinition(
                "MOMENT",
                "1",
                rotated.Id,
                MemberLoadKind.PointMoment,
                direction,
                2,
                6,
                -2,
                3)]);

        SceneMemberLoad load = Assert.Single(Project(new ProjectDocumentSceneProjector(), document).Scene.MemberLoads);

        Assert.Equal(SceneMemberLoadKind.Point, load.Kind);
        Assert.Equal(SceneLoadVectorKind.Moment, load.VectorKind);
        Assert.Equal(0.25f, load.StartRelativePosition, 5);
        Assert.Equal(0.75f, load.EndRelativePosition, 5);
        AssertPoint(load.StartVector, -2 * expectedX, -2 * expectedY, -2 * expectedZ);
        AssertPoint(load.EndVector, 3 * expectedX, 3 * expectedY, 3 * expectedZ);
    }

    [Fact]
    public void ProjectorPreservesPointForceDistributedAndThermalMemberLoadSemantics()
    {
        ProjectDocument source = ProjectDocumentPresets.Create(BuiltInProjectPreset.RamenViaduct);
        ProjectDocument document = WithMemberLoads(
            source,
            source.Members,
            [
                new MemberLoadDefinition(
                    "POINT", "1", "2", MemberLoadKind.PointForce, MemberLoadDirection.GlobalY,
                    2, 6, -2, 3),
                new MemberLoadDefinition(
                    "DISTRIBUTED", "1", "2", MemberLoadKind.DistributedForce, MemberLoadDirection.GlobalZ,
                    2, 1, -4, 5),
                new MemberLoadDefinition(
                    "THERMAL", "1", "2", MemberLoadKind.Thermal, MemberLoadDirection.LocalX,
                    0, 0, 18, -7),
            ]);

        IReadOnlyDictionary<string, SceneMemberLoad> loads = Project(
                new ProjectDocumentSceneProjector(),
                document)
            .Scene.MemberLoads.ToDictionary(value => value.Id, StringComparer.Ordinal);

        Assert.Equal(SceneLoadVectorKind.Force, loads["POINT"].VectorKind);
        Assert.Equal(0.25f, loads["POINT"].StartRelativePosition, 5);
        Assert.Equal(0.75f, loads["POINT"].EndRelativePosition, 5);
        AssertPoint(loads["POINT"].StartVector, 0, -2, 0);
        AssertPoint(loads["POINT"].EndVector, 0, 3, 0);

        Assert.Equal(SceneMemberLoadKind.Distributed, loads["DISTRIBUTED"].Kind);
        Assert.Equal(0.25f, loads["DISTRIBUTED"].StartRelativePosition, 5);
        Assert.Equal(0.875f, loads["DISTRIBUTED"].EndRelativePosition, 5);
        AssertPoint(loads["DISTRIBUTED"].StartVector, 0, 0, -4);
        AssertPoint(loads["DISTRIBUTED"].EndVector, 0, 0, 5);

        Assert.Equal(SceneMemberLoadKind.Thermal, loads["THERMAL"].Kind);
        Assert.Equal(18.0f, loads["THERMAL"].TemperatureTop);
        Assert.Equal(-7.0f, loads["THERMAL"].TemperatureBottom);
    }

    [Fact]
    public void RapidViewportUpdatesCoalesceIntoOneCreatingThreadDispatch()
    {
        StaTestRunner.Run(() =>
        {
            List<ViewportUpdateReason> dispatches = [];
            using ViewportUpdateScheduler scheduler = new(dispatches.Add, intervalMilliseconds: 60_000);

            scheduler.Schedule(ViewportUpdateReason.Document);
            scheduler.Schedule(ViewportUpdateReason.Result);
            scheduler.Schedule(ViewportUpdateReason.Selection | ViewportUpdateReason.Resize);

            Assert.Equal(2, scheduler.CoalescedRequestCount);
            Assert.Equal(
                ViewportUpdateReason.Document |
                ViewportUpdateReason.Result |
                ViewportUpdateReason.Selection |
                ViewportUpdateReason.Resize,
                scheduler.Pending);
            scheduler.Flush();

            Assert.Equal(1, scheduler.DispatchCount);
            Assert.Equal(
                ViewportUpdateReason.Document |
                ViewportUpdateReason.Result |
                ViewportUpdateReason.Selection |
                ViewportUpdateReason.Resize,
                Assert.Single(dispatches));
            Assert.Equal(ViewportUpdateReason.None, scheduler.Pending);
            scheduler.Flush();
            Assert.Single(dispatches);
        }, "Step 6 coalesced viewport updates");
    }

    [Fact]
    public void ViewportUpdateSchedulerIsCreatingThreadOnlyAndDisposalIsIdempotent()
    {
        StaTestRunner.Run(() =>
        {
            ViewportUpdateScheduler scheduler = new(_ => { });
            Exception? failure = null;
            Thread worker = new(() =>
            {
                try
                {
                    scheduler.Schedule(ViewportUpdateReason.Document);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(2)));
            Assert.IsType<InvalidOperationException>(failure);

            scheduler.Dispose();
            scheduler.Dispose();
            Assert.Throws<ObjectDisposedException>(() =>
                scheduler.Schedule(ViewportUpdateReason.Document));
        }, "Step 6 viewport scheduler lifecycle");
    }

    [Fact]
    public void ResultNavigatorPagesOrderedCaseStatesAndPreservesStableCoordinate()
    {
        AnalysisResultSet resultSet = ReadCombinedResultSet();
        ViewportResultNavigator navigator = new();

        Assert.True(navigator.SetResultSet(resultSet));
        Assert.Equal(resultSet.Results.Select(result => result.Coordinate), navigator.Coordinates);
        Assert.Equal(0, navigator.PageIndex);
        Assert.Equal(resultSet.Results.Count, navigator.PageCount);
        Assert.False(navigator.CanMovePrevious);

        while (navigator.CanMoveNext)
        {
            Assert.True(navigator.MoveNext());
        }

        ResultCoordinate last = resultSet.Results[^1].Coordinate;
        Assert.Equal(last, navigator.CurrentCoordinate);
        Assert.False(navigator.MoveNext());
        Assert.True(navigator.CanMovePrevious);

        AnalysisResultSet equivalent = new(
            resultSet.Kind,
            resultSet.SchemaVersion,
            resultSet.Units,
            resultSet.CoordinateSystem,
            resultSet.Cases,
            resultSet.Topology,
            resultSet.Results);
        Assert.False(navigator.SetResultSet(equivalent));
        Assert.Equal(last, navigator.CurrentCoordinate);

        Assert.True(navigator.Select(resultSet.Results[0].Coordinate));
        Assert.Equal(0, navigator.PageIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            navigator.Select(new ResultCoordinate("missing", ResultStateKind.Static, 0)));
    }

    internal static AnalysisResultSet ReadCombinedResultSet()
    {
        AnalysisResultSet staticResult = ReadFixture("single-static.json");
        AnalysisResultSet nonlinear = ReadFixture("nonlinear-steps.json");
        AnalysisResultSet modal = ReadFixture("modal.json");
        AnalysisResult[] ordered =
        [
            .. staticResult.Results,
            .. nonlinear.Results,
            .. modal.Results,
        ];

        return new AnalysisResultSet(
            staticResult.Kind,
            staticResult.SchemaVersion,
            staticResult.Units,
            staticResult.CoordinateSystem,
            staticResult.Cases,
            staticResult.Topology,
            ordered);
    }

    internal static (ProjectDocument Document, AnalysisResultSet Results) CreateSignedCaseFixture()
    {
        ProjectDocument source = ProjectDocumentPresets.CreateRepresentativeFrame();
        LoadCaseDefinition firstCase = source.LoadCases[0] with { Id = "C1", Name = "Case 1", Symbol = "C1" };
        LoadCaseDefinition secondCase = firstCase with { Id = "C2", Name = "Case 2", Symbol = "C2" };
        ProjectDocument document = new(
            source.Version,
            source.Metadata,
            source.Nodes,
            source.Members,
            source.Supports,
            [firstCase, secondCase],
            [
                new NodalLoadDefinition("NL-C1", "C1", "2", 0, 0, -3, 0, 0, 0),
                new NodalLoadDefinition("NL-C2", "C2", "2", 0, 0, -7, 0, 0, 0),
            ],
            source.DerivedResults,
            source.MovingLoads,
            source.Selection,
            source.IsDirty,
            source.Sections,
            source.Dimension,
            source.ElementPropertySets,
            source.RigidZones,
            source.SupportSets,
            source.Panels,
            source.JointReleaseSets,
            source.NoticePoints,
            source.MemberSpringSets,
            [
                new PrescribedDisplacementDefinition("PD-C1", "C1", "2", 0.001, 0, 0, 0, 0, 0),
                new PrescribedDisplacementDefinition("PD-C2", "C2", "2", 0.002, 0, 0, 0, 0, 0),
            ],
            [
                new MemberLoadDefinition(
                    "ML-C1", "C1", "1", MemberLoadKind.PointMoment, MemberLoadDirection.GlobalZ,
                    1, 2, -4, 5),
                new MemberLoadDefinition(
                    "ML-C2", "C2", "1", MemberLoadKind.PointForce, MemberLoadDirection.GlobalX,
                    1, 2, 6, -2),
            ]);

        ProjectNode nodeI = document.Nodes.Single(value => value.Id == "1");
        ProjectNode nodeJ = document.Nodes.Single(value => value.Id == "2");
        double length = Math.Sqrt(
            Math.Pow(nodeJ.X - nodeI.X, 2) +
            Math.Pow(nodeJ.Y - nodeI.Y, 2) +
            Math.Pow(nodeJ.Z - nodeI.Z, 2));
        CoordinateFrame frame = new(
            new Vector3Value(nodeI.X, nodeI.Y, nodeI.Z),
            new Vector3Value(1, 0, 0),
            new Vector3Value(0, 1, 0),
            new Vector3Value(0, 0, 1));
        AnalysisTopology topology = new(
            document.Nodes.Select(value => new TopologyNode(
                value.Id,
                new Vector3Value(value.X, value.Y, value.Z),
                value.Id,
                false)),
            [new TopologyMember(
                "1",
                "1",
                "2",
                frame,
                [new MemberStation("I", 0), new MemberStation("J", length)])],
            [],
            []);
        StaticAnalysisResult first = CreateSignedResult("C1", -9, 9, -12, 10);
        StaticAnalysisResult second = CreateSignedResult("C2", -4, 6, -8, 7);
        AnalysisResultSet results = new(
            AnalysisResultSet.ContractKind,
            AnalysisResultSet.ContractVersion,
            new AnalysisUnits("SI", "m", "N", "kg", "s"),
            new CoordinateSystem("global_cartesian", "right", ["x", "y", "z"]),
            [
                new AnalysisCase("C1", "Case 1", "C1", AnalysisType.Static, ["1", "2"]),
                new AnalysisCase("C2", "Case 2", "C2", AnalysisType.Static, ["1", "2"]),
            ],
            topology,
            [first, second]);
        return (document, results);
    }

    private static StaticAnalysisResult CreateSignedResult(
        string caseId,
        double negative,
        double positive,
        double negativeForce,
        double positiveForce) => new(
        caseId,
        [
            new NodeDisplacement("1", new DisplacementComponents(negative, 1, 0, 0, 0, 0)),
            new NodeDisplacement("2", new DisplacementComponents(positive, -1, 0, 0, 0, 0)),
        ],
        [
            new SupportReaction("1", new ForceComponents(negative, 1, 0, 0, 0, 0)),
            new SupportReaction("2", new ForceComponents(positive, -1, 0, 0, 0, 0)),
        ],
        [new MemberSectionForces(
            "1",
            [new MemberSegmentResult(
                "S1",
                "I",
                "J",
                1,
                new ForceComponents(negativeForce, 0, 0, 0, 0, 0),
                new ForceComponents(positiveForce, 0, 0, 0, 0, 0))])],
        [],
        [],
        new WarningDiagnostics([]));

    private static ViewportSceneProjection Project(
        ProjectDocumentSceneProjector projector,
        ProjectDocument document) => projector.Project(
        "grid-plane",
        document,
        resultSet: null,
        result: null,
        pageIndex: 0,
        pageCount: 0,
        ViewportPresentationState.Default,
        new ViewportSceneProjectionText("Scale", "Color"));

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

    private static ProjectDocument WithMemberLoads(
        ProjectDocument value,
        IEnumerable<ProjectMember> members,
        IEnumerable<MemberLoadDefinition> memberLoads) => new(
        value.Version,
        value.Metadata,
        value.Nodes,
        members,
        value.Supports,
        value.LoadCases,
        value.NodalLoads,
        value.DerivedResults,
        value.MovingLoads,
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
        memberLoads);

    private static void AssertPoint(ScenePoint3 actual, double x, double y, double z)
    {
        Assert.Equal((float)x, actual.X, 5);
        Assert.Equal((float)y, actual.Y, 5);
        Assert.Equal((float)z, actual.Z, 5);
    }

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
