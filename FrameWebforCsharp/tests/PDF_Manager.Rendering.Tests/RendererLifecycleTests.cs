using PDF_Manager.Rendering;
using PDF_Manager.Rendering.Scene;

namespace PDF_Manager.Rendering.Tests;

public sealed class RendererLifecycleTests
{
    [Theory]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, false, false, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, false, false, true, false)]
    public void ShouldRenderFrame_AcceptsTypedOrLegacyContentOnlyWhenInitializedAndDirty(
        bool initialized,
        bool isDirty,
        bool hasLegacyModel,
        bool hasTypedScene,
        bool expected)
    {
        Assert.Equal(
            expected,
            OpenGlViewportLifecycle.ShouldRenderFrame(
                initialized,
                isDirty,
                hasLegacyModel,
                hasTypedScene));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ShouldActivateContext_SkipsRedundantCurrentContextActivation(
        bool isCurrent,
        bool expected)
    {
        Assert.Equal(expected, OpenGlViewportLifecycle.ShouldActivateContext(true, isCurrent));
    }

    [Fact]
    public void ShouldActivateContext_RejectsMissingContext()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OpenGlViewportLifecycle.ShouldActivateContext(false, false));
    }

    [Fact]
    public void DisposeBeforeInitialization_IsIdempotentAndReleasesSubscriptionsWithoutCreatingContext()
    {
        RunOnStaThread(() =>
        {
            RendererDiagnosticSnapshot baseline = RendererDiagnostics.Snapshot();
            OpenGlViewportLifecycle renderer = new();
            RendererDiagnosticSnapshot created = RendererDiagnostics.Snapshot();

            Assert.Equal(baseline.LiveContexts, created.LiveContexts);
            Assert.Equal(baseline.ContextsCreated, created.ContextsCreated);
            Assert.Equal(baseline.LiveSubscriptions + 2, created.LiveSubscriptions);

            renderer.Dispose();
            renderer.Dispose();

            RendererDiagnosticSnapshot disposed = RendererDiagnostics.Snapshot();
            Assert.True(renderer.Control.IsDisposed);
            Assert.Equal(baseline.LiveContexts, disposed.LiveContexts);
            Assert.Equal(baseline.LiveSubscriptions, disposed.LiveSubscriptions);
            Assert.Equal(baseline.ContextsCreated, disposed.ContextsCreated);
            Assert.Throws<ObjectDisposedException>(renderer.RequestRender);
        });
    }

    [Fact]
    public void TypedSceneCameraAndSelection_AreCreatingThreadOnlyAndIdempotentBeforeInitialization()
    {
        RunOnStaThread(() =>
        {
            using OpenGlViewportLifecycle renderer = new();
            ViewportSceneModel scene = SceneTestData.Create();
            List<ViewportSelectionChangedEventArgs> changes = [];
            renderer.SelectionChanged += (_, change) => changes.Add(change);

            renderer.SetScene(scene);
            renderer.SetScene(SceneTestData.Create());
            renderer.SetSelection(new SceneEntityKey(SceneEntityKind.Node, "N1"));
            renderer.SetSelection(new SceneEntityKey(SceneEntityKind.Node, "N1"));
            renderer.ToggleProjection();
            renderer.Fit();
            renderer.Home();

            Assert.Same(scene, renderer.Scene);
            Assert.Equal(new SceneEntityKey(SceneEntityKind.Node, "N1"), renderer.Selection);
            Assert.Equal(ViewportProjection.Perspective, renderer.Projection);
            Assert.Single(changes);
            Assert.Equal(ViewportSelectionOrigin.Table, changes[0].Origin);
            Assert.Throws<ArgumentException>(() => renderer.SetSelection(
                new SceneEntityKey(SceneEntityKind.Node, "missing")));

            Exception? failure = null;
            Thread worker = new(() =>
            {
                try
                {
                    renderer.ToggleProjection();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(2)));
            Assert.IsType<InvalidOperationException>(failure);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "PDF Manager renderer lifecycle test",
        };
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The STA renderer test thread did not terminate.");
        Assert.Null(failure);
    }
}

internal static class SceneTestData
{
    public static ViewportSceneModel Create(SceneDisplacementLayer? displacement = null) => new(
        "scene-v1",
        [
            new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
            new SceneNode("N2", new ScenePoint3(2.0f, 0.0f, 1.0f)),
        ],
        [new SceneMember("M1", "N1", "N2")],
        [new SceneSupport("S1", "N1", true, true, true)],
        [new SceneNodalLoad("NL1", "N2", new ScenePoint3(0.0f, 0.0f, -10.0f))],
        [new SceneMemberLoad("ML1", "M1", 0.5f, new ScenePoint3(0.0f, 0.0f, -5.0f))],
        displacement);
}
