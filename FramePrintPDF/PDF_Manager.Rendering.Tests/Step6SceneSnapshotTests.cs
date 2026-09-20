using System.Diagnostics;
using PDF_Manager.Rendering.Scene;
using Xunit.Abstractions;

namespace PDF_Manager.Rendering.Tests;

public sealed class Step6SceneSnapshotTests(ITestOutputHelper output)
{
    [Fact]
    public void CompleteSceneCompilesEveryLayerAndDecorationIntoDeterministicSnapshots()
    {
        ViewportSceneModel scene = Step6RenderingContractTests.CreateCompleteScene();
        Size viewport = new(960, 640);
        ViewportCameraState camera = ViewportSceneCompiler.Home(scene, ViewportProjection.Orthographic);

        ViewportSceneCommandBuffer first = ViewportSceneCompiler.Compile(scene, camera, viewport);
        ViewportSceneCommandBuffer second = ViewportSceneCompiler.Compile(scene, camera, viewport);

        SceneLayerKind[] expectedRenderableLayers =
        [
            SceneLayerKind.Members,
            SceneLayerKind.Nodes,
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
        Assert.Equal(expectedRenderableLayers, first.Layers.Select(layer => layer.Kind));
        Assert.All(first.Layers, layer => Assert.NotEmpty(layer.Vertices));
        Assert.All(first.Layers, layer => Assert.NotEmpty(layer.Batches));
        Assert.Equal(first.Vertices, second.Vertices);
        Assert.Equal(first.Batches, second.Batches);
        for (int index = 0; index < first.Layers.Count; index++)
        {
            Assert.Equal(first.Layers[index].Kind, second.Layers[index].Kind);
            Assert.Equal(first.Layers[index].Vertices, second.Layers[index].Vertices);
            Assert.Equal(first.Layers[index].Batches, second.Layers[index].Batches);
            Assert.Equal(
                first.Layers[index].HitTargets.Select(target => target.Key),
                second.Layers[index].HitTargets.Select(target => target.Key));
        }

        Assert.Single(first.GridCommands);
        Assert.Equal(["X", "Y", "Z"], first.AxisCommands.Select(command => command.Axis));
        Assert.Single(first.LabelCommands);
        Assert.Single(first.ScaleCommands);
        Assert.Single(first.ColorLegendCommands);
        Assert.Equal(2, first.ColorLegendCommands[0].Entries.Count);

        SceneEntityKey[] expectedTargets =
        [
            new(SceneEntityKind.Node, "N1"),
            new(SceneEntityKind.Member, "M1"),
            new(SceneEntityKind.RigidZone, "RZ1"),
            new(SceneEntityKind.Support, "S1"),
            new(SceneEntityKind.Spring, "SP1"),
            new(SceneEntityKind.Joint, "J1"),
            new(SceneEntityKind.Panel, "P1"),
            new(SceneEntityKind.NoticePoint, "NP1"),
            new(SceneEntityKind.NodalLoad, "NL1"),
            new(SceneEntityKind.MemberLoad, "ML1"),
            new(SceneEntityKind.MemberLoad, "ML2"),
            new(SceneEntityKind.MemberLoad, "ML3"),
            new(SceneEntityKind.PrescribedDisplacement, "PD1"),
            new(SceneEntityKind.Displacement, "N2"),
            new(SceneEntityKind.Reaction, "RC1"),
            new(SceneEntityKind.SectionForce, "SF1"),
        ];
        Assert.All(expectedTargets, key => Assert.Contains(first.HitTargets, target => target.Key == key));
    }

    [Fact]
    public void Orthographic2DAndPerspective3DPoliciesProduceStableDistinctSnapshots()
    {
        ViewportSceneModel scene = Step6RenderingContractTests.CreateCompleteScene();
        Size viewport = new(960, 640);
        ViewportSceneCommandBuffer twoDimensional = ViewportSceneCompiler.Compile(
            scene,
            ViewportSceneCompiler.Home(scene, ViewportProjection.Orthographic),
            viewport);
        ViewportSceneCommandBuffer threeDimensional = ViewportSceneCompiler.Compile(
            scene,
            ViewportSceneCompiler.Home(scene, ViewportProjection.Perspective),
            viewport);

        Assert.False(twoDimensional.Vertices.SequenceEqual(threeDimensional.Vertices));
        Assert.Equal(twoDimensional.Layers.Select(layer => layer.Kind), threeDimensional.Layers.Select(layer => layer.Kind));
        Assert.Equal(twoDimensional.HitTargets.Select(target => target.Key), threeDimensional.HitTargets.Select(target => target.Key));
    }

    [Fact]
    public void HitTestingReportsStableLayerAndHonorsLayerMask()
    {
        ViewportSceneModel scene = Step6RenderingContractTests.CreateCompleteScene();
        Size viewport = new(800, 600);
        ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
            scene,
            ViewportSceneCompiler.Home(scene, ViewportProjection.Orthographic),
            viewport);
        SceneHitTarget node = commands.HitTargets.Single(target =>
            target.Key == new SceneEntityKey(SceneEntityKind.Node, "N2"));
        ScenePoint2 projected = Assert.Single(node.Points);
        Point client = ToClient(projected, viewport);
        IHitTestService hitTest = new ViewportHitTestService();

        SceneHitTestResult hit = Assert.IsType<SceneHitTestResult>(
            hitTest.HitTest(commands, client, viewport, new SceneHitTestOptions(layers: SceneLayerMask.Nodes)));
        Assert.Equal(node.Key, hit.Key);
        Assert.Equal(SceneLayerKind.Nodes, hit.Layer);
        Assert.Null(hitTest.HitTest(
            commands,
            client,
            viewport,
            new SceneHitTestOptions(tolerancePixels: 1.0f, layers: SceneLayerMask.Reactions)));
    }

    [Fact]
    public void DescendingPointPairCoincidentSpringAndZeroResultsKeepStableHitTargets()
    {
        SceneEntityKey pointPair = new(SceneEntityKind.MemberLoad, "PP1");
        SceneEntityKey spring = new(SceneEntityKind.Spring, "SP0");
        SceneEntityKey reaction = new(SceneEntityKind.Reaction, "R0");
        SceneEntityKey sectionForce = new(SceneEntityKind.SectionForce, "SF0");
        ViewportSceneModel scene = new(
            "edge-hit-targets",
            [
                new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                new SceneNode("N2", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                new SceneNode("N3", new ScenePoint3(10.0f, 0.0f, 0.0f)),
            ],
            members: [new SceneMember("M1", "N1", "N3")],
            memberLoads:
            [
                SceneMemberLoad.PointPair(
                    pointPair.Id,
                    "M1",
                    firstRelativePosition: 0.8f,
                    new ScenePoint3(0.0f, -5.0f, 0.0f),
                    secondRelativePosition: 0.2f,
                    new ScenePoint3(0.0f, -2.0f, 0.0f)),
            ],
            springs: [new SceneSpring(spring.Id, "N1", "N2")],
            reactions: new SceneReactionLayer(
                "zero-reaction",
                [new SceneReaction(reaction.Id, "N1", default)]),
            sectionForces: new SceneSectionForceLayer(
                "zero-section-force",
                [new SceneSectionForce(sectionForce.Id, "M1", 0.5f, default, default)]));

        ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
            scene,
            ViewportSceneCompiler.Home(scene, ViewportProjection.Orthographic),
            new Size(960, 640));

        SceneHitTarget[] pointPairTargets = commands.HitTargets.Where(target => target.Key == pointPair).ToArray();
        Assert.Equal(2, pointPairTargets.Length);
        Assert.All(pointPairTargets, target => Assert.Equal(ScenePrimitive.Lines, target.Primitive));
        Assert.Equal(ScenePrimitive.Points, commands.HitTargets.Single(target => target.Key == spring).Primitive);
        Assert.Equal(ScenePrimitive.Points, commands.HitTargets.Single(target => target.Key == reaction).Primitive);
        Assert.Equal(ScenePrimitive.Points, commands.HitTargets.Single(target => target.Key == sectionForce).Primitive);
    }

    [Fact]
    public void LargeSceneWith10000NodesAnd9999Members_CompilesAndDiffsWithinTenSecondBudget()
    {
        const int nodeCount = 10_000;
        TimeSpan budget = TimeSpan.FromSeconds(10);
        SceneNode[] nodes = Enumerable.Range(0, nodeCount)
            .Select(index => new SceneNode($"N{index}", new ScenePoint3(index % 100, index / 100, index % 7)))
            .ToArray();
        SceneMember[] members = Enumerable.Range(0, nodeCount - 1)
            .Select(index => new SceneMember($"M{index}", nodes[index].Id, nodes[index + 1].Id))
            .ToArray();
        ViewportSceneModel original = new("large-scene", nodes, members);
        SceneMember[] changedMembers = members.ToArray();
        changedMembers[^1] = changedMembers[^1] with { StartNodeId = nodes[^3].Id };
        ViewportSceneModel changed = new("large-scene", nodes, changedMembers);

        _ = ViewportSceneCompiler.Compile(
            original,
            ViewportSceneCompiler.Home(original, ViewportProjection.Perspective),
            new Size(1280, 720));
        Stopwatch compileStopwatch = Stopwatch.StartNew();
        ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
            original,
            ViewportSceneCompiler.Home(original, ViewportProjection.Perspective),
            new Size(1280, 720));
        compileStopwatch.Stop();
        Stopwatch invalidationStopwatch = Stopwatch.StartNew();
        SceneLayerMask changedLayers = changed.GetChangedLayers(original);
        invalidationStopwatch.Stop();

        output.WriteLine(
            "large_scene nodes={0}, members={1}, vertices={2}, hit_targets={3}, compile_ms={4:F3}, invalidation_ms={5:F3}",
            nodes.Length,
            members.Length,
            commands.Vertices.Count,
            commands.HitTargets.Count,
            compileStopwatch.Elapsed.TotalMilliseconds,
            invalidationStopwatch.Elapsed.TotalMilliseconds);
        Assert.Equal(
            SceneLayerMask.Labels |
            SceneLayerMask.Members |
            SceneLayerMask.RigidZones |
            SceneLayerMask.Springs |
            SceneLayerMask.Joints |
            SceneLayerMask.NoticePoints |
            SceneLayerMask.Loads |
            SceneLayerMask.Displacements |
            SceneLayerMask.SectionForces,
            changedLayers);
        Assert.Equal(nodeCount, commands.Layers.Single(layer => layer.Kind == SceneLayerKind.Nodes).HitTargets.Count);
        Assert.Equal(nodeCount - 1, commands.Layers.Single(layer => layer.Kind == SceneLayerKind.Members).HitTargets.Count);
        Assert.True(
            compileStopwatch.Elapsed <= budget,
            $"Large-scene compilation exceeded {budget.TotalSeconds:F0}s: {compileStopwatch.Elapsed.TotalMilliseconds:F3}ms.");
        Assert.True(
            invalidationStopwatch.Elapsed <= budget,
            $"Large-scene invalidation diff exceeded {budget.TotalSeconds:F0}s: {invalidationStopwatch.Elapsed.TotalMilliseconds:F3}ms.");
    }

    [Fact]
    public void AggregateEntityBudgetRejectsTheFirstItemFromTheNextLayerBeforeLayerCopies()
    {
        int memberItemsEnumerated = 0;
        IEnumerable<SceneMember> members = EnumerateMembers();
        IEnumerable<SceneNode> nodes = Enumerable.Range(0, ViewportSceneModel.MaximumEntityCount)
            .Select(index => new SceneNode($"N{index}", new ScenePoint3(index, 0.0f, 0.0f)));

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ViewportSceneModel("aggregate-limit", nodes, members));

        Assert.Equal("members", exception.ParamName);
        Assert.Equal(1, memberItemsEnumerated);

        IEnumerable<SceneMember> EnumerateMembers()
        {
            memberItemsEnumerated++;
            yield return new SceneMember("M0", "N0", "N1");
        }
    }

    [Fact]
    public void DerivedGeometryBudgetRejectsGlyphExpansionBeforeTheVertexLimitIsExceeded()
    {
        const int verticesPerSupport = 80;
        int supportCount = (ViewportSceneCompiler.MaximumVertexCount / verticesPerSupport) + 1;
        SceneNode[] nodes = Enumerable.Range(0, supportCount)
            .Select(index => new SceneNode($"N{index}", new ScenePoint3(index, 0.0f, 0.0f)))
            .ToArray();
        SceneSupport[] supports = Enumerable.Range(0, supportCount)
            .Select(index => new SceneSupport($"S{index}", nodes[index].Id, true, true, true, true, true, true))
            .ToArray();
        ViewportSceneModel scene = new("derived-limit", nodes, supports: supports);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ViewportSceneCompiler.Compile(
                scene,
                ViewportSceneCompiler.Home(scene, ViewportProjection.Orthographic),
                new Size(1280, 720)));

        Assert.Contains("vertex", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecorationCommandBudgetAcceptsTheExactLimitAndRejectsTheNextCommand()
    {
        SceneLabelCommand[] exact = Enumerable.Range(0, ViewportSceneCompiler.MaximumDecorationCommandCount)
            .Select(index => new SceneLabelCommand($"L{index}", index.ToString(), default, null))
            .ToArray();

        ViewportSceneCommandBuffer accepted = ViewportSceneCompiler.Compose([], labelCommands: exact);

        Assert.Equal(ViewportSceneCompiler.MaximumDecorationCommandCount, accepted.LabelCommands.Count);
        int enumerated = 0;
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ViewportSceneCompiler.Compose([], labelCommands: EnumerateOverLimit()));
        Assert.Equal(ViewportSceneCompiler.MaximumDecorationCommandCount + 1, enumerated);
        Assert.Contains("decoration", exception.Message, StringComparison.OrdinalIgnoreCase);

        IEnumerable<SceneLabelCommand> EnumerateOverLimit()
        {
            for (int index = 0; index <= ViewportSceneCompiler.MaximumDecorationCommandCount; index++)
            {
                enumerated++;
                yield return new SceneLabelCommand($"L{index}", index.ToString(), default, null);
            }
        }
    }

    [Fact]
    public void SceneCompilationAppliesTheDecorationBudgetBeforeAppendingAnExtraLabel()
    {
        SceneLabel[] exact = Enumerable.Range(0, ViewportSceneCompiler.MaximumDecorationCommandCount)
            .Select(index => new SceneLabel($"L{index}", index.ToString(), default))
            .ToArray();
        ViewportSceneModel acceptedScene = new(
            "labels-exact",
            [new SceneNode("N1", default)],
            presentation: new ScenePresentationOptions(labels: exact));

        ViewportSceneCommandBuffer accepted = ViewportSceneCompiler.Compile(
            acceptedScene,
            ViewportSceneCompiler.Home(acceptedScene, ViewportProjection.Orthographic),
            new Size(960, 640),
            visibleLayers: SceneLayerMask.Labels);

        Assert.Equal(ViewportSceneCompiler.MaximumDecorationCommandCount, accepted.LabelCommands.Count);
        ViewportSceneModel rejectedScene = new(
            "labels-over",
            [new SceneNode("N1", default)],
            presentation: new ScenePresentationOptions(
                labels: exact.Append(new SceneLabel("extra", "extra", default))));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ViewportSceneCompiler.Compile(
                rejectedScene,
                ViewportSceneCompiler.Home(rejectedScene, ViewportProjection.Orthographic),
                new Size(960, 640),
                visibleLayers: SceneLayerMask.Labels));
        Assert.Contains("decoration", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ColorLegendEntryBudgetAcceptsTheExactLimitAndRejectsOneExtraEntry()
    {
        SceneColorLegendEntry[] exact = CreateLegendEntries(ViewportSceneCompiler.MaximumColorLegendEntryCount);
        ViewportSceneCommandBuffer accepted = ViewportSceneCompiler.Compose(
            [],
            colorLegendCommands: [new SceneColorLegendCommand("legend", "Legend", exact)]);

        Assert.Equal(
            ViewportSceneCompiler.MaximumColorLegendEntryCount,
            Assert.Single(accepted.ColorLegendCommands).Entries.Count);
        SceneColorLegendEntry[] overLimit = CreateLegendEntries(
            ViewportSceneCompiler.MaximumColorLegendEntryCount + 1);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ViewportSceneCompiler.Compose(
                [],
                colorLegendCommands: [new SceneColorLegendCommand("legend", "Legend", overLimit)]));
        Assert.Contains("legend", exception.Message, StringComparison.OrdinalIgnoreCase);

        static SceneColorLegendEntry[] CreateLegendEntries(int count) => Enumerable.Range(0, count)
            .Select(index => new SceneColorLegendEntry($"E{index}", index, 0.0f, 0.0f, 0.0f))
            .ToArray();
    }

    [Fact]
    public void SupportGlyphsPreserveEachTranslationalAndRotationalAxis()
    {
        SceneSupport[] variants =
        [
            new SceneSupport("Sx", "N1", true, false, false),
            new SceneSupport("Sy", "N1", false, true, false),
            new SceneSupport("Sz", "N1", false, false, true),
            new SceneSupport("Srx", "N1", false, false, false, true),
            new SceneSupport("Sry", "N1", false, false, false, false, true),
            new SceneSupport("Srz", "N1", false, false, false, false, false, true),
        ];
        List<SceneRenderVertex[]> signatures = [];

        for (int index = 0; index < variants.Length; index++)
        {
            ViewportSceneModel scene = new(
                $"support-{index}",
                [new SceneNode("N1", default)],
                supports: [variants[index]]);
            ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
                scene,
                ViewportSceneCompiler.Home(scene, ViewportProjection.Perspective),
                new Size(960, 640),
                visibleLayers: SceneLayerMask.Supports);
            ViewportSceneLayerCommandBuffer layer = commands.Layers.Single(value => value.Kind == SceneLayerKind.Supports);

            SceneDrawBatch batch = Assert.Single(layer.Batches);
            Assert.Single(layer.HitTargets);
            Assert.Equal(index < 3 ? 2 : 24, batch.VertexCount);
            SceneRenderVertex[] signature = layer.Vertices
                .Skip(batch.FirstVertex)
                .Take(batch.VertexCount)
                .ToArray();
            Assert.DoesNotContain(signatures, existing => existing.SequenceEqual(signature));
            signatures.Add(signature);
        }
    }

    [Fact]
    public void JointGlyphsMapEachReleaseFlagToItsOwnTranslationOrRotationAxis()
    {
        SceneJoint[] variants =
        [
            new SceneJoint("Jx", "N1", true, false, false),
            new SceneJoint("Jy", "N1", false, true, false),
            new SceneJoint("Jz", "N1", false, false, true),
            new SceneJoint("Jrx", "N1", false, false, false, true),
            new SceneJoint("Jry", "N1", false, false, false, false, true),
            new SceneJoint("Jrz", "N1", false, false, false, false, false, true),
        ];
        List<SceneRenderVertex[]> signatures = [];

        for (int index = 0; index < variants.Length; index++)
        {
            ViewportSceneModel scene = new(
                $"joint-{index}",
                [new SceneNode("N1", default)],
                joints: [variants[index]]);
            ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
                scene,
                ViewportSceneCompiler.Home(scene, ViewportProjection.Perspective),
                new Size(960, 640),
                visibleLayers: SceneLayerMask.Joints);
            ViewportSceneLayerCommandBuffer layer = commands.Layers.Single(value => value.Kind == SceneLayerKind.Joints);

            Assert.Equal(2, layer.Batches.Count);
            Assert.Equal(2, layer.HitTargets.Count);
            SceneDrawBatch releaseBatch = layer.Batches[1];
            Assert.Equal(index < 3 ? 2 : 24, releaseBatch.VertexCount);
            SceneRenderVertex[] signature = layer.Vertices
                .Skip(releaseBatch.FirstVertex)
                .Take(releaseBatch.VertexCount)
                .ToArray();
            Assert.DoesNotContain(signatures, existing => existing.SequenceEqual(signature));
            signatures.Add(signature);
        }
    }

    [Fact]
    public void ThermalMemberLoadGlyphsPreserveEveryTopBottomSignCombinationAndGradient()
    {
        (float Top, float Bottom)[] combinations =
        [
            (10.0f, 5.0f),
            (10.0f, -5.0f),
            (-10.0f, 5.0f),
            (-10.0f, -5.0f),
        ];
        List<SceneRenderVertex[]> signatures = [];

        foreach ((float top, float bottom) in combinations)
        {
            ViewportSceneModel scene = new(
                $"thermal-{top}-{bottom}",
                [
                    new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                    new SceneNode("N2", new ScenePoint3(4.0f, 0.0f, 0.0f)),
                ],
                members: [new SceneMember("M1", "N1", "N2")],
                memberLoads: [SceneMemberLoad.Thermal("T1", "M1", top, bottom)]);
            ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
                scene,
                ViewportSceneCompiler.Home(scene, ViewportProjection.Perspective),
                new Size(960, 640),
                visibleLayers: SceneLayerMask.Loads);
            ViewportSceneLayerCommandBuffer layer = commands.Layers.Single(value => value.Kind == SceneLayerKind.Loads);

            Assert.Equal(3, layer.Batches.Count);
            Assert.Equal(3, layer.HitTargets.Count);
            Assert.All(layer.Batches, batch =>
            {
                Assert.Equal(ScenePrimitive.Lines, batch.Primitive);
                Assert.Equal(2, batch.VertexCount);
            });
            AssertThermalSign(layer.Vertices[layer.Batches[0].FirstVertex], top);
            AssertThermalSign(layer.Vertices[layer.Batches[1].FirstVertex], bottom);
            SceneDrawBatch gradient = layer.Batches[2];
            Assert.NotEqual(
                layer.Vertices[gradient.FirstVertex],
                layer.Vertices[gradient.FirstVertex + 1]);
            SceneRenderVertex[] signature = layer.Vertices.ToArray();
            Assert.DoesNotContain(signatures, existing => existing.SequenceEqual(signature));
            signatures.Add(signature);
        }

        static void AssertThermalSign(SceneRenderVertex vertex, float value)
        {
            if (value > 0.0f)
            {
                Assert.True(vertex.Red > vertex.Blue);
            }
            else
            {
                Assert.True(vertex.Blue > vertex.Red);
            }
        }
    }

    private static Point ToClient(ScenePoint2 point, Size viewport) => new(
        (int)MathF.Round((point.X + 1.0f) * 0.5f * viewport.Width),
        (int)MathF.Round((1.0f - point.Y) * 0.5f * viewport.Height));
}
