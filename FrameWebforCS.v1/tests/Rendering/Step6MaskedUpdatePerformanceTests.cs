using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PDF_Manager.Rendering.Scene;
using Xunit.Abstractions;

namespace PDF_Manager.Rendering.Tests;

[CollectionDefinition(Step6MaskedUpdatePerformanceTests.CollectionName, DisableParallelization = true)]
public sealed class Step6MaskedUpdatePerformanceCollection
{
}

[Collection(CollectionName)]
public sealed class Step6MaskedUpdatePerformanceTests(ITestOutputHelper output)
{
    public const string CollectionName = "Step 6 masked update performance";

    private const int NodeCount = 10_000;
    private const int MemberCount = NodeCount - 1;
    private static readonly TimeSpan UpdateBudget = TimeSpan.FromSeconds(5);

    [Fact]
    public void LoadOnlyMaskedUpdateReusesLargeSceneCacheAndCompletesWithinFiveSeconds()
    {
        RendererDiagnosticSnapshot processBaseline = RendererDiagnostics.Snapshot();

        MaskedUpdateMeasurement measurement = RunOnStaThread(() =>
        {
            SceneNode[] nodes = Enumerable.Range(0, NodeCount)
                .Select(index => new SceneNode(
                    $"N{index}",
                    new ScenePoint3(index % 100, index / 100, index % 7)))
                .ToArray();
            SceneMember[] members = Enumerable.Range(0, MemberCount)
                .Select(index => new SceneMember($"M{index}", nodes[index].Id, nodes[index + 1].Id))
                .ToArray();
            ViewportSceneModel original = CreateScene(nodes, members, loadZ: -10.0f);
            ViewportSceneModel updated = CreateScene(nodes, members, loadZ: -20.0f);
            SceneEntityKey selection = new(SceneEntityKind.Member, $"M{MemberCount / 2}");

            using OpenGlViewportLifecycle renderer = new();
            renderer.Resize(new Size(1280, 720));
            renderer.SetScene(original);
            renderer.SetSelection(selection, ViewportSelectionOrigin.Table);

            ViewportSceneCommandBuffer commandsBefore = CachedCommands(renderer);
            Dictionary<SceneLayerKind, ViewportSceneLayerCommandBuffer> layersBefore =
                commandsBefore.Layers.ToDictionary(layer => layer.Kind);
            AssertCommandCounts(commandsBefore);
            Assert.Equal(selection, renderer.Selection);

            int selectionEvents = 0;
            renderer.SelectionChanged += (_, _) => selectionEvents++;
            long compilationCountBefore = renderer.LayerCompilationCount;
            long invalidationRequestsBefore = renderer.InvalidationRequestCount;
            long invalidationBatchesBefore = renderer.InvalidationBatchCount;
            RendererDiagnosticSnapshot diagnosticsBefore = RendererDiagnostics.Snapshot();

            Stopwatch stopwatch = Stopwatch.StartNew();
            renderer.SetScene(updated, SceneLayerMask.Loads);
            renderer.SetScene(updated, SceneLayerMask.Loads);

            Assert.Equal(SceneLayerMask.Loads, renderer.PendingInvalidation);
            Assert.Equal(compilationCountBefore, renderer.LayerCompilationCount);
            Assert.Equal(invalidationRequestsBefore + 2, renderer.InvalidationRequestCount);
            Assert.Equal(invalidationBatchesBefore, renderer.InvalidationBatchCount);

            Assert.Null(renderer.HitTest(new Point(-10_000, -10_000)));
            stopwatch.Stop();

            ViewportSceneCommandBuffer commandsAfter = CachedCommands(renderer);
            Dictionary<SceneLayerKind, ViewportSceneLayerCommandBuffer> layersAfter =
                commandsAfter.Layers.ToDictionary(layer => layer.Kind);
            RendererDiagnosticSnapshot diagnosticsAfter = RendererDiagnostics.Snapshot();

            Assert.Equal(SceneLayerMask.None, renderer.PendingInvalidation);
            Assert.Equal(compilationCountBefore + 1, renderer.LayerCompilationCount);
            Assert.Equal(invalidationRequestsBefore + 2, renderer.InvalidationRequestCount);
            Assert.Equal(invalidationBatchesBefore + 1, renderer.InvalidationBatchCount);
            Assert.Equal(diagnosticsBefore.LayerCompilations + 1, diagnosticsAfter.LayerCompilations);
            Assert.Equal(diagnosticsBefore.InvalidationRequests + 2, diagnosticsAfter.InvalidationRequests);
            Assert.Equal(diagnosticsBefore.InvalidationBatches + 1, diagnosticsAfter.InvalidationBatches);

            foreach (SceneLayerKind kind in ViewportSceneCompiler.RenderableLayers)
            {
                if (kind == SceneLayerKind.Loads)
                {
                    Assert.NotSame(layersBefore[kind], layersAfter[kind]);
                }
                else
                {
                    Assert.Same(layersBefore[kind], layersAfter[kind]);
                }
            }

            AssertCommandCounts(commandsAfter);
            Assert.Equal(selection, renderer.Selection);
            Assert.Equal(0, selectionEvents);
            Assert.True(
                stopwatch.Elapsed <= UpdateBudget,
                $"Masked large-scene update exceeded {UpdateBudget.TotalSeconds:F0}s: {stopwatch.Elapsed.TotalMilliseconds:F3}ms.");

            return new MaskedUpdateMeasurement(
                stopwatch.Elapsed,
                commandsAfter.Vertices.Count,
                commandsAfter.Batches.Count,
                commandsAfter.HitTargets.Count,
                renderer.LayerCompilationCount - compilationCountBefore,
                renderer.InvalidationRequestCount - invalidationRequestsBefore,
                renderer.InvalidationBatchCount - invalidationBatchesBefore);
        });

        RendererDiagnosticSnapshot processAfter = RendererDiagnostics.Snapshot();
        Assert.Equal(processBaseline.LiveContexts, processAfter.LiveContexts);
        Assert.Equal(processBaseline.LiveSubscriptions, processAfter.LiveSubscriptions);

        output.WriteLine(
            "masked_update nodes={0}, members={1}, affected={2}, queued_updates=2, vertices={3}, batches={4}, hit_targets={5}, layer_compilations={6}, invalidation_requests={7}, invalidation_batches={8}, elapsed_ms={9:F3}, budget_ms={10:F0}",
            NodeCount,
            MemberCount,
            SceneLayerMask.Loads,
            measurement.Vertices,
            measurement.Batches,
            measurement.HitTargets,
            measurement.LayerCompilations,
            measurement.InvalidationRequests,
            measurement.InvalidationBatches,
            measurement.Elapsed.TotalMilliseconds,
            UpdateBudget.TotalMilliseconds);
    }

    private static ViewportSceneModel CreateScene(
        IReadOnlyList<SceneNode> nodes,
        IReadOnlyList<SceneMember> members,
        float loadZ) =>
        new(
            "masked-update-large-scene",
            nodes,
            members,
            nodalLoads:
            [
                new SceneNodalLoad(
                    "NL1",
                    nodes[NodeCount / 2].Id,
                    new ScenePoint3(0.0f, 0.0f, loadZ)),
            ]);

    private static void AssertCommandCounts(ViewportSceneCommandBuffer commands)
    {
        Assert.Equal(30_000, commands.Vertices.Count);
        Assert.Equal(20_000, commands.Batches.Count);
        Assert.Equal(20_000, commands.HitTargets.Count);
        Assert.Equal(NodeCount, commands.Layers.Single(layer => layer.Kind == SceneLayerKind.Nodes).HitTargets.Count);
        Assert.Equal(MemberCount, commands.Layers.Single(layer => layer.Kind == SceneLayerKind.Members).HitTargets.Count);
        Assert.Single(commands.Layers.Single(layer => layer.Kind == SceneLayerKind.Loads).HitTargets);
    }

    private static ViewportSceneCommandBuffer CachedCommands(OpenGlViewportLifecycle renderer)
    {
        FieldInfo field = typeof(OpenGlViewportLifecycle).GetField(
            "_sceneCommands",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The renderer command cache field was not found.");
        return Assert.IsType<ViewportSceneCommandBuffer>(field.GetValue(renderer));
    }

    private static T RunOnStaThread<T>(Func<T> action)
    {
        T? result = default;
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "PDF Manager Step 6 masked update performance test",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "The STA performance test thread did not terminate.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result!;
    }

    private readonly record struct MaskedUpdateMeasurement(
        TimeSpan Elapsed,
        int Vertices,
        int Batches,
        int HitTargets,
        long LayerCompilations,
        long InvalidationRequests,
        long InvalidationBatches);
}
