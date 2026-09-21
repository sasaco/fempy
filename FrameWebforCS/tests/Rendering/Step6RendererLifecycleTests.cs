using System.Runtime.ExceptionServices;
using System.Reflection;
using PDF_Manager.Rendering.Scene;

namespace PDF_Manager.Rendering.Tests;

public sealed class Step6RendererLifecycleTests
{
    [Fact]
    public void CameraHoverSelectionVisibilityAndInvalidationRemainIdempotentBeforeContextCreation()
    {
        RunOnStaThread(() =>
        {
            RendererDiagnosticSnapshot baseline = RendererDiagnostics.Snapshot();
            using OpenGlViewportLifecycle renderer = new();
            List<ViewportSelectionChangedEventArgs> selectionEvents = [];
            List<ViewportHoverChangedEventArgs> hoverEvents = [];
            renderer.SelectionChanged += (_, change) => selectionEvents.Add(change);
            renderer.HoverChanged += (_, change) => hoverEvents.Add(change);
            renderer.SetScene(Step6RenderingContractTests.CreateCompleteScene());
            long afterSceneInvalidations = renderer.InvalidationRequestCount;
            long afterSceneCompilations = renderer.LayerCompilationCount;

            SceneEntityKey member = new(SceneEntityKind.Member, "M1");
            renderer.SetSelection(member, ViewportSelectionOrigin.Table);
            long afterSelectionCompilations = renderer.LayerCompilationCount;
            renderer.SetSelection(member, ViewportSelectionOrigin.Viewport);
            Assert.Equal(afterSelectionCompilations, renderer.LayerCompilationCount);
            Assert.Single(selectionEvents);
            Assert.Equal(ViewportSelectionOrigin.Table, selectionEvents[0].Origin);
            Assert.Equal(member, renderer.Selection);

            SceneEntityKey node = new(SceneEntityKind.Node, "N2");
            renderer.SetHover(node);
            long afterHoverCompilations = renderer.LayerCompilationCount;
            renderer.SetHover(node);
            Assert.Equal(afterHoverCompilations, renderer.LayerCompilationCount);
            Assert.Single(hoverEvents);
            Assert.Equal(node, renderer.Hover);

            renderer.SetCameraPolicy(ViewportCameraPolicy.TwoDimensional);
            long afterTwoDimensionalCompilations = renderer.LayerCompilationCount;
            renderer.SetCameraPolicy(ViewportCameraPolicy.TwoDimensional);
            Assert.Equal(afterTwoDimensionalCompilations, renderer.LayerCompilationCount);
            Assert.Equal(ViewportCameraPolicy.TwoDimensional, renderer.CameraPolicy);
            Assert.Equal(ViewportProjection.Orthographic, renderer.Projection);

            renderer.SetCameraPolicy(ViewportCameraPolicy.ThreeDimensional);
            long afterThreeDimensionalCompilations = renderer.LayerCompilationCount;
            renderer.SetCameraPolicy(ViewportCameraPolicy.ThreeDimensional);
            Assert.Equal(afterThreeDimensionalCompilations, renderer.LayerCompilationCount);
            Assert.Equal(ViewportCameraPolicy.ThreeDimensional, renderer.CameraPolicy);
            Assert.Equal(ViewportProjection.Perspective, renderer.Projection);

            renderer.SetVisibleLayers(SceneLayerMask.Geometry);
            long afterVisibilityCompilations = renderer.LayerCompilationCount;
            renderer.SetVisibleLayers(SceneLayerMask.Geometry);
            Assert.Equal(afterVisibilityCompilations, renderer.LayerCompilationCount);
            Assert.Equal(SceneLayerMask.Geometry, renderer.VisibleLayers);
            Assert.Equal(
                SceneLayerMask.Loads | SceneLayerMask.Results | SceneLayerMask.Decorations,
                renderer.PendingInvalidation);
            Assert.Equal(afterSceneInvalidations + 5, renderer.InvalidationRequestCount);
            Assert.True(renderer.LayerCompilationCount > afterSceneCompilations);

            Assert.Equal(baseline.LiveContexts, RendererDiagnostics.Snapshot().LiveContexts);
        });
    }

    [Fact]
    public void PngCaptureOptionsEnforceExplicitEncodedByteBudget()
    {
        Assert.Equal(1, new ViewportPngCaptureOptions(1).MaximumBytes);
        Assert.Equal(
            ViewportPngCaptureOptions.MaximumEncodedBytes,
            new ViewportPngCaptureOptions().MaximumBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportPngCaptureOptions(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ViewportPngCaptureOptions(ViewportPngCaptureOptions.MaximumEncodedBytes + 1));
    }

    [Fact]
    public void MemberTopologyEditRecompilesCachedDisplacementsWithUpdatedCoordinates()
    {
        RunOnStaThread(() =>
        {
            ViewportSceneModel original = Step6RenderingContractTests.CreateCompleteScene();
            ViewportSceneModel changedTopology = Step6RenderingContractTests.CreateCompleteScene(members:
            [
                new SceneMember("M1", "N1", "N4"),
                new SceneMember("M2", "N2", "N3"),
            ]);
            Assert.True(original.Displacement!.HasSameContent(changedTopology.Displacement!));

            using OpenGlViewportLifecycle renderer = new();
            renderer.Resize(new Size(800, 600));
            renderer.SetScene(original);
            ViewportSceneLayerCommandBuffer before = CachedLayer(renderer, SceneLayerKind.Displacements);
            long compilationsBefore = renderer.LayerCompilationCount;

            SceneLayerMask affected = changedTopology.GetChangedLayers(original);
            Assert.NotEqual(SceneLayerMask.None, affected & SceneLayerMask.Displacements);
            renderer.SetScene(changedTopology, affected);
            Assert.Equal(compilationsBefore, renderer.LayerCompilationCount);

            Assert.Null(renderer.HitTest(new Point(-10_000, -10_000)));

            ViewportSceneLayerCommandBuffer after = CachedLayer(renderer, SceneLayerKind.Displacements);
            Assert.Equal(compilationsBefore + 1, renderer.LayerCompilationCount);
            Assert.NotSame(before, after);
            Assert.False(before.Vertices.SequenceEqual(after.Vertices));
        });
    }

    [Fact]
    public void PreInitializationCameraPolicyMaskedSceneAndPendingInvalidationDisposeWithinTenSeconds()
    {
        RendererDiagnosticSnapshot baseline = RendererDiagnostics.Snapshot();

        RunOnStaThread(() =>
        {
            OpenGlViewportLifecycle renderer = new();
            renderer.SetCameraPolicy(ViewportCameraPolicy.TwoDimensional);
            renderer.SetScene(
                Step6RenderingContractTests.CreateCompleteScene(),
                SceneLayerMask.Nodes | SceneLayerMask.Members);

            Assert.Equal(ViewportCameraPolicy.TwoDimensional, renderer.CameraPolicy);
            Assert.Equal(ViewportProjection.Orthographic, renderer.Projection);
            Assert.Equal(SceneLayerMask.All, renderer.PendingInvalidation);
            renderer.Dispose();
            renderer.Dispose();
        });

        RendererDiagnosticSnapshot after = RendererDiagnostics.Snapshot();
        Assert.Equal(baseline.LiveContexts, after.LiveContexts);
        Assert.Equal(baseline.LiveSubscriptions, after.LiveSubscriptions);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "PDF Manager Step 6 renderer lifecycle test",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The STA renderer test thread did not terminate.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static ViewportSceneLayerCommandBuffer CachedLayer(
        OpenGlViewportLifecycle renderer,
        SceneLayerKind kind)
    {
        FieldInfo field = typeof(OpenGlViewportLifecycle).GetField(
            "_sceneCommands",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The renderer command cache field was not found.");
        ViewportSceneCommandBuffer commands =
            Assert.IsType<ViewportSceneCommandBuffer>(field.GetValue(renderer));
        return commands.Layers.Single(layer => layer.Kind == kind);
    }
}
