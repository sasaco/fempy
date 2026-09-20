using PDF_Manager.Rendering;

namespace PDF_Manager.Rendering.Tests;

public sealed class RendererLifecycleTests
{
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
