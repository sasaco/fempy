using PDF_Manager.Rendering.Scene;

namespace PDF_Manager.Rendering.Tests;

public sealed class Step6RenderingContractTests
{
    [Fact]
    public void CompleteTypedSceneExposesEveryIndependentInputAndResultLayer()
    {
        ViewportSceneModel scene = CreateCompleteScene();
        SceneLayerKind[] expectedKinds =
        [
            SceneLayerKind.Nodes,
            SceneLayerKind.Members,
            SceneLayerKind.RigidZones,
            SceneLayerKind.Supports,
            SceneLayerKind.Springs,
            SceneLayerKind.Joints,
            SceneLayerKind.Panels,
            SceneLayerKind.NoticePoints,
            SceneLayerKind.Loads,
            SceneLayerKind.Displacements,
            SceneLayerKind.Reactions,
            SceneLayerKind.SectionForces,
        ];

        Assert.Equal(expectedKinds, scene.Layers.Select(layer => layer.Kind));
        Assert.All(expectedKinds, kind => Assert.NotNull(scene.GetLayer(kind)));
        Assert.All(scene.Layers, layer => Assert.False(string.IsNullOrWhiteSpace(layer.StableId)));
        Assert.Equal(5, scene.LoadLayer.Count);
        Assert.Equal(
            [SceneMemberLoadKind.Point, SceneMemberLoadKind.Distributed, SceneMemberLoadKind.Thermal],
            scene.MemberLoads.Select(load => load.Kind));
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.RigidZone, "RZ1")));
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.Spring, "SP1")));
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.Joint, "J1")));
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.Panel, "P1")));
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.NoticePoint, "NP1")));
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.PrescribedDisplacement, "PD1")));
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.Reaction, "RC1")));
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.SectionForce, "SF1")));
    }

    [Fact]
    public void SceneDiffInvalidatesOnlyChangedResultOrPresentationLayers()
    {
        ViewportSceneModel original = CreateCompleteScene();
        ViewportSceneModel changedReaction = CreateCompleteScene(
            reactions: new SceneReactionLayer(
                "result:reactions",
                [new SceneReaction("RC1", "N1", new ScenePoint3(99.0f, 0.0f, 0.0f))],
                context: CreateResultContext()));

        Assert.Equal(SceneLayerMask.Reactions, changedReaction.GetChangedLayers(original));

        ViewportSceneModel changedPresentation = CreateCompleteScene(
            presentation: new ScenePresentationOptions(
                new SceneGridDefinition(SceneGridPlane.XZ, 2.0f),
                showAxes: true,
                labels: [new SceneLabel("label:N1", "renamed", new ScenePoint3(0.0f, 0.0f, 0.0f))]));

        Assert.Equal(
            SceneLayerMask.Grid |
            SceneLayerMask.Labels |
            SceneLayerMask.ScaleLegend |
            SceneLayerMask.ColorLegend,
            changedPresentation.GetChangedLayers(original));
        Assert.Equal(SceneLayerMask.All, original.GetChangedLayers(null));
    }

    [Fact]
    public void SceneDiffAppliesExplicitCoordinateDependencyClosureWithoutExpandingVisibilityChanges()
    {
        ViewportSceneModel original = CreateCompleteScene();
        ViewportSceneModel movedNode = CreateCompleteScene(nodes:
        [
            new SceneNode("N1", new ScenePoint3(-2.0f, 1.0f, 3.0f)),
            new SceneNode("N2", new ScenePoint3(4.0f, 0.0f, 0.0f)),
            new SceneNode("N3", new ScenePoint3(4.0f, 3.0f, 1.0f)),
            new SceneNode("N4", new ScenePoint3(0.0f, 3.0f, 0.0f)),
        ]);
        SceneLayerMask nodeDependencies =
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
        Assert.Equal(nodeDependencies, movedNode.GetChangedLayers(original));

        ViewportSceneModel changedTopology = CreateCompleteScene(members:
        [
            new SceneMember("M1", "N1", "N4"),
            new SceneMember("M2", "N2", "N3"),
        ]);
        SceneLayerMask memberDependencies =
            SceneLayerMask.Members |
            SceneLayerMask.RigidZones |
            SceneLayerMask.Springs |
            SceneLayerMask.Joints |
            SceneLayerMask.NoticePoints |
            SceneLayerMask.Loads |
            SceneLayerMask.Displacements |
            SceneLayerMask.SectionForces |
            SceneLayerMask.Labels;
        Assert.Equal(memberDependencies, changedTopology.GetChangedLayers(original));

        ViewportSceneModel hiddenNodes = CreateCompleteScene(
            visibleLayers: SceneLayerMask.All & ~SceneLayerMask.Nodes);
        Assert.Equal(SceneLayerMask.Nodes, hiddenNodes.GetChangedLayers(original));
    }

    [Fact]
    public void LayerKindsMapOneToOneToCompleteIndependentMasks()
    {
        SceneLayerKind[] kinds = Enum.GetValues<SceneLayerKind>();
        SceneLayerMask aggregate = SceneLayerMask.None;

        foreach (SceneLayerKind kind in kinds)
        {
            SceneLayerMask mask = kind.ToMask();
            Assert.NotEqual(SceneLayerMask.None, mask);
            Assert.Equal(SceneLayerMask.None, aggregate & mask);
            aggregate |= mask;
        }

        Assert.Equal(SceneLayerMask.All, aggregate);
        Assert.Equal(
            SceneLayerMask.Nodes |
            SceneLayerMask.Members |
            SceneLayerMask.RigidZones |
            SceneLayerMask.Supports |
            SceneLayerMask.Springs |
            SceneLayerMask.Joints |
            SceneLayerMask.Panels |
            SceneLayerMask.NoticePoints,
            SceneLayerMask.Geometry);
        Assert.Equal(
            SceneLayerMask.Displacements | SceneLayerMask.Reactions | SceneLayerMask.SectionForces,
            SceneLayerMask.Results);
    }

    [Fact]
    public void InvalidationCoalescerMergesRapidAffectedLayerRequestsIntoOneBatch()
    {
        SceneInvalidationCoalescer coalescer = new();

        Assert.True(coalescer.Invalidate(SceneLayerMask.Nodes));
        Assert.False(coalescer.Invalidate(SceneLayerMask.Members));
        Assert.False(coalescer.Invalidate(SceneLayerMask.Nodes | SceneLayerMask.Labels));

        Assert.Equal(3, coalescer.RequestsReceived);
        Assert.Equal(SceneLayerMask.Nodes | SceneLayerMask.Members | SceneLayerMask.Labels, coalescer.Pending);
        Assert.Equal(SceneLayerMask.Nodes | SceneLayerMask.Members | SceneLayerMask.Labels, coalescer.Consume());
        Assert.Equal(SceneLayerMask.None, coalescer.Pending);
        Assert.Equal(1, coalescer.BatchesConsumed);
        Assert.Equal(SceneLayerMask.None, coalescer.Consume());
        Assert.Equal(1, coalescer.BatchesConsumed);
        Assert.False(coalescer.Invalidate(SceneLayerMask.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            coalescer.Invalidate((SceneLayerMask)(1UL << 63)));
    }

    [Theory]
    [InlineData(SceneResultStateKind.Static, 0, 0, 1, SceneExtremaMode.Values)]
    [InlineData(SceneResultStateKind.LoadStep, 4, 2, 8, SceneExtremaMode.Minimum)]
    [InlineData(SceneResultStateKind.Mode, 9, 7, 8, SceneExtremaMode.Maximum)]
    [InlineData(SceneResultStateKind.Static, 0, 1, 2, SceneExtremaMode.AbsoluteMaximum)]
    public void ResultContextRetainsCaseStatePagingAndExtrema(
        SceneResultStateKind stateKind,
        int stateIndex,
        int pageIndex,
        int pageCount,
        SceneExtremaMode extrema)
    {
        SceneResultContext context = new("case-17", stateKind, stateIndex, pageIndex, pageCount, extrema);

        Assert.Equal("case-17", context.CaseId);
        Assert.Equal(stateKind, context.StateKind);
        Assert.Equal(stateIndex, context.StateIndex);
        Assert.Equal(pageIndex, context.PageIndex);
        Assert.Equal(pageCount, context.PageCount);
        Assert.Equal(extrema, context.Extrema);
    }

    [Fact]
    public void ResultContextRejectsOutOfRangeStateAndPageCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SceneResultContext("case", SceneResultStateKind.Static, stateIndex: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SceneResultContext("case", pageIndex: 1, pageCount: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SceneResultContext("case", pageCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SceneResultContext("case", (SceneResultStateKind)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SceneResultContext("case", extrema: (SceneExtremaMode)int.MaxValue));
    }

    [Fact]
    public void PresentationOptionsKeepGridAxesLabelsAndLegendsTypedAndBounded()
    {
        ScenePresentationOptions presentation = new(
            new SceneGridDefinition(SceneGridPlane.XY, majorSpacing: 5.0f, minorDivisions: 5),
            showAxes: true,
            labels:
            [
                new SceneLabel(
                    "node-label:N1",
                    "N1",
                    new ScenePoint3(1.0f, 2.0f, 0.0f),
                    new SceneEntityKey(SceneEntityKind.Node, "N1")),
            ],
            new SceneScaleLegend("scale:displacement", "Displacement x20", 20.0f),
            new SceneColorLegend(
                "legend:force",
                "Section force",
                [
                    new SceneColorLegendEntry("minimum", -10.0f, 0.0f, 0.2f, 1.0f),
                    new SceneColorLegendEntry("maximum", 20.0f, 1.0f, 0.2f, 0.0f),
                ]));

        Assert.Equal(SceneGridPlane.XY, presentation.Grid!.Plane);
        Assert.True(presentation.ShowAxes);
        Assert.Single(presentation.Labels);
        Assert.Equal(20.0f, presentation.ScaleLegend!.Scale);
        Assert.Equal(2, presentation.ColorLegend!.Entries.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SceneGridDefinition(SceneGridPlane.XY, 0.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SceneColorLegend(
                "legend",
                "invalid",
                [new SceneColorLegendEntry("only", 0.0f, 0.0f, 0.0f, 0.0f)]));
    }

    internal static ViewportSceneModel CreateCompleteScene(
        SceneReactionLayer? reactions = null,
        ScenePresentationOptions? presentation = null,
        IEnumerable<SceneNode>? nodes = null,
        IEnumerable<SceneMember>? members = null,
        SceneLayerMask visibleLayers = SceneLayerMask.All) =>
        new(
            "complete-scene",
            nodes ??
            [
                new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                new SceneNode("N2", new ScenePoint3(4.0f, 0.0f, 0.0f)),
                new SceneNode("N3", new ScenePoint3(4.0f, 3.0f, 1.0f)),
                new SceneNode("N4", new ScenePoint3(0.0f, 3.0f, 0.0f)),
            ],
            members: members ??
            [
                new SceneMember("M1", "N1", "N2"),
                new SceneMember("M2", "N2", "N3"),
            ],
            supports: [new SceneSupport("S1", "N1", true, true, true)],
            nodalLoads: [new SceneNodalLoad("NL1", "N3", new ScenePoint3(0.0f, 0.0f, -10.0f))],
            memberLoads:
            [
                SceneMemberLoad.Point("ML1", "M1", 0.25f, new ScenePoint3(0.0f, -5.0f, 0.0f)),
                SceneMemberLoad.Distributed(
                    "ML2",
                    "M1",
                    0.0f,
                    1.0f,
                    new ScenePoint3(0.0f, -2.0f, 0.0f),
                    new ScenePoint3(0.0f, -4.0f, 0.0f)),
                SceneMemberLoad.Thermal("ML3", "M2", 15.0f, -5.0f),
            ],
            displacement: new SceneDisplacementLayer(
                "result:displacements",
                [
                    new SceneNodeDisplacement("N1", default),
                    new SceneNodeDisplacement("N2", new ScenePoint3(0.0f, 0.0f, 0.1f)),
                    new SceneNodeDisplacement("N3", new ScenePoint3(0.0f, 0.0f, 0.2f)),
                    new SceneNodeDisplacement("N4", default),
                ],
                10.0f,
                CreateResultContext()),
            rigidZones: [new SceneRigidZone("RZ1", "M1", 0.2f, 0.3f)],
            springs: [new SceneSpring("SP1", "N1", "N2")],
            joints: [new SceneJoint("J1", "N2", true, false, false, false, true)],
            panels: [new ScenePanel("P1", ["N1", "N2", "N3", "N4"])],
            noticePoints: [new SceneNoticePoint("NP1", "M1", 0.6f)],
            prescribedDisplacements:
            [
                new ScenePrescribedDisplacement(
                    "PD1",
                    "N1",
                    new ScenePoint3(0.001f, 0.0f, 0.0f)),
            ],
            reactions: reactions ?? new SceneReactionLayer(
                "result:reactions",
                [new SceneReaction("RC1", "N1", new ScenePoint3(10.0f, 0.0f, 5.0f))],
                context: CreateResultContext()),
            sectionForces: new SceneSectionForceLayer(
                "result:section-forces",
                [
                    new SceneSectionForce(
                        "SF1",
                        "M1",
                        0.5f,
                        new ScenePoint3(10.0f, 2.0f, 3.0f),
                        new ScenePoint3(4.0f, 5.0f, 6.0f)),
                ],
                context: CreateResultContext()),
            presentation: presentation ?? new ScenePresentationOptions(
                new SceneGridDefinition(SceneGridPlane.XY, 1.0f),
                showAxes: true,
                labels: [new SceneLabel("label:N1", "N1", new ScenePoint3(0.0f, 0.0f, 0.0f))],
                scaleLegend: new SceneScaleLegend("scale", "Displacement x10", 10.0f),
                colorLegend: new SceneColorLegend(
                    "color",
                    "Force",
                    [
                        new SceneColorLegendEntry("min", -10.0f, 0.0f, 0.2f, 1.0f),
                        new SceneColorLegendEntry("max", 10.0f, 1.0f, 0.2f, 0.0f),
                    ])),
            visibleLayers: visibleLayers);

    private static SceneResultContext CreateResultContext() =>
        new(
            "case-1",
            SceneResultStateKind.Static,
            pageIndex: 0,
            pageCount: 2,
            extrema: SceneExtremaMode.AbsoluteMaximum);
}
