using PDF_Manager.Rendering.Scene;

namespace PDF_Manager.Rendering.Tests;

public sealed class ViewportSceneTests
{
    [Fact]
    public void Constructor_DefensivelyCopiesAndValidatesStableReferences()
    {
        SceneNode[] nodes =
        [
            new("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
            new("N2", new ScenePoint3(1.0f, 0.0f, 0.0f)),
        ];
        ViewportSceneModel scene = new("stable", nodes, [new SceneMember("M1", "N1", "N2")]);
        nodes[0] = new SceneNode("changed", new ScenePoint3(8.0f, 8.0f, 8.0f));

        Assert.Equal("N1", scene.Nodes[0].Id);
        Assert.True(scene.Contains(new SceneEntityKey(SceneEntityKind.Member, "M1")));
        Assert.True(scene.HasSameContent(new ViewportSceneModel(
            "stable",
            [
                new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                new SceneNode("N2", new ScenePoint3(1.0f, 0.0f, 0.0f)),
            ],
            [new SceneMember("M1", "N1", "N2")])));
    }

    [Fact]
    public void Constructor_RejectsDuplicateMissingAndInvalidEntities()
    {
        Assert.Throws<ArgumentException>(() => new ViewportSceneModel(
            "duplicate",
            [
                new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                new SceneNode("N1", new ScenePoint3(1.0f, 0.0f, 0.0f)),
            ]));
        Assert.Throws<ArgumentException>(() => new ViewportSceneModel(
            "missing",
            [new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f))],
            [new SceneMember("M1", "N1", "N2")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportSceneModel(
            "load-position",
            [
                new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                new SceneNode("N2", new ScenePoint3(1.0f, 0.0f, 0.0f)),
            ],
            [new SceneMember("M1", "N1", "N2")],
            memberLoads: [new SceneMemberLoad("L1", "M1", 1.1f, new ScenePoint3(0.0f, 0.0f, -1.0f))]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScenePoint3(float.NaN, 0.0f, 0.0f));
    }

    [Fact]
    public void Constructor_BoundsEveryEntityAndDisplacementSequence()
    {
        const int oversizedCount = 250_001;
        SceneNode node = new("N1", new ScenePoint3(0.0f, 0.0f, 0.0f));
        SceneNode secondNode = new("N2", new ScenePoint3(1.0f, 0.0f, 0.0f));
        SceneMember member = new("M1", "N1", "N2");

        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportSceneModel(
            "nodes-over-limit",
            Enumerable.Repeat(node, oversizedCount)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportSceneModel(
            "members-over-limit",
            [node, secondNode],
            Enumerable.Repeat(member, oversizedCount)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportSceneModel(
            "supports-over-limit",
            [node],
            supports: Enumerable.Repeat(new SceneSupport("S1", "N1", true, false, false), oversizedCount)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportSceneModel(
            "nodal-loads-over-limit",
            [node],
            nodalLoads: Enumerable.Repeat(
                new SceneNodalLoad("L1", "N1", new ScenePoint3(1.0f, 0.0f, 0.0f)),
                oversizedCount)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportSceneModel(
            "member-loads-over-limit",
            [node, secondNode],
            [member],
            memberLoads: Enumerable.Repeat(
                new SceneMemberLoad("L1", "M1", 0.5f, new ScenePoint3(0.0f, 0.0f, -1.0f)),
                oversizedCount)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneDisplacementLayer(
            "displacements-over-limit",
            Enumerable.Repeat(
                new SceneNodeDisplacement("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                oversizedCount)));
    }

    [Fact]
    public void Constructor_StopsInfiniteEntityAndDisplacementSequencesAtTheBound()
    {
        SceneNode node = new("N1", new ScenePoint3(0.0f, 0.0f, 0.0f));
        SceneNodeDisplacement displacement = new("N1", new ScenePoint3(0.0f, 0.0f, 0.0f));

        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportSceneModel(
            "infinite-nodes",
            RepeatForever(node)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneDisplacementLayer(
            "infinite-displacements",
            RepeatForever(displacement)));
    }

    [Fact]
    public void Compile_RetainsAndRendersRotationOnlySupportAndMomentOnlyNodalLoad()
    {
        SceneSupport support = new("S1", "N1", false, false, false, true, false, true);
        SceneNodalLoad load = new(
            "L1",
            "N1",
            new ScenePoint3(0.0f, 0.0f, 0.0f),
            new ScenePoint3(0.0f, 5.0f, 0.0f));
        ViewportSceneModel scene = new(
            "rotations-and-moments",
            [new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f))],
            supports: [support],
            nodalLoads: [load]);

        ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
            scene,
            ViewportSceneCompiler.Home(scene, ViewportProjection.Orthographic),
            new Size(800, 600));

        Assert.True(scene.Supports.Single().FixRx);
        Assert.True(scene.Supports.Single().FixRz);
        Assert.Equal(5.0f, scene.NodalLoads.Single().Moment.Y);
        Assert.Contains(commands.HitTargets, target =>
            target.Key == new SceneEntityKey(SceneEntityKind.Support, "S1") && target.Points.Count > 2);
        Assert.Contains(commands.HitTargets, target =>
            target.Key == new SceneEntityKey(SceneEntityKind.NodalLoad, "L1") && target.Points.Count > 2);
        Assert.Contains(commands.Vertices, vertex => vertex.Green == 0.78f && vertex.Blue == 0.48f);
        Assert.Contains(commands.Vertices, vertex => vertex.Red == 1.0f && vertex.Green == 0.58f);
    }

    [Fact]
    public void Compile_ProducesDeterministicZUpLayersAndDisplacementOverlay()
    {
        SceneDisplacementLayer displacement = new(
            "static-1",
            [
                new SceneNodeDisplacement("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                new SceneNodeDisplacement("N2", new ScenePoint3(0.0f, 0.0f, 0.25f)),
            ],
            2.0f);
        ViewportSceneModel scene = SceneTestData.Create(displacement);
        ViewportCameraState camera = ViewportSceneCompiler.Home(scene, ViewportProjection.Orthographic);

        ViewportSceneCommandBuffer first = ViewportSceneCompiler.Compile(
            scene,
            camera,
            new Size(800, 600),
            new SceneEntityKey(SceneEntityKind.Member, "M1"));
        ViewportSceneCommandBuffer second = ViewportSceneCompiler.Compile(
            scene,
            camera,
            new Size(800, 600),
            new SceneEntityKey(SceneEntityKind.Member, "M1"));

        Assert.NotEmpty(first.Vertices);
        Assert.Equal(first.Vertices, second.Vertices);
        Assert.Equal(first.Batches, second.Batches);
        Assert.Equal(6, first.HitTargets.Count);
        Assert.Contains(first.Vertices, vertex => vertex.Red == 0.95f && vertex.Blue == 0.72f);
        Assert.Contains(first.Vertices, vertex => vertex.Red == 1.0f && vertex.Green == 0.88f);
    }

    [Theory]
    [InlineData(ViewportProjection.Orthographic)]
    [InlineData(ViewportProjection.Perspective)]
    public void CompileAndHitTest_SelectsProjectedNode(ViewportProjection projection)
    {
        ViewportSceneModel scene = SceneTestData.Create();
        ViewportCameraState camera = ViewportSceneCompiler.Home(scene, projection);
        Size size = new(800, 600);
        ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(scene, camera, size);
        SceneHitTarget node = commands.HitTargets.Single(target =>
            target.Key == new SceneEntityKey(SceneEntityKind.Node, "N2"));
        ScenePoint2 point = Assert.Single(node.Points);
        Point client = new(
            (int)MathF.Round((point.X + 1.0f) * 0.5f * size.Width),
            (int)MathF.Round((1.0f - point.Y) * 0.5f * size.Height));

        SceneEntityKey? hit = ViewportSceneCompiler.HitTest(commands, client, size);

        Assert.Equal(node.Key, hit);
    }

    private static IEnumerable<T> RepeatForever<T>(T value)
    {
        while (true)
        {
            yield return value;
        }
    }
}
