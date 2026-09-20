using System.Diagnostics;
using PDF_Manager.Rendering;

namespace PDF_Manager.RendererProbe;

internal static class RendererVerification
{
    private static readonly TimeSpan PaintTimeout = TimeSpan.FromSeconds(5);

    public static VerificationReport Run(int cycles)
    {
        RendererDiagnosticSnapshot baseline = RendererDiagnostics.Snapshot();
        AssertNoLiveObjects(baseline, ProbeWindowDiagnostics.LiveWindows, "verification start");
        string? openGlVersion = null;
        Color firstBackground = Color.Empty;
        Color firstCenter = Color.Empty;

        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            using (RendererProbeShell shell = new(verificationMode: true))
            {
                shell.Show();
                Application.DoEvents();
                RendererProbeDocument document = shell.Document;
                document.EnsureRendererReady();
                OpenGlViewportLifecycle renderer = document.Renderer;
                openGlVersion ??= renderer.OpenGlVersion;

                Assert(renderer.IsInitialized, $"Cycle {cycle}: renderer was not initialized.");
                Assert(!string.IsNullOrWhiteSpace(renderer.OpenGlVersion), $"Cycle {cycle}: no OpenGL version was reported.");
                AssertLiveObjects(cycle, expectedContexts: 1, expectedSubscriptions: 2, expectedWindows: 2);
                VerifyIdempotentInitialization(renderer, cycle);

                int frameBeforeInvalidation = renderer.RenderedFrameCount;
                renderer.RequestRender();
                PumpUntil(() => renderer.RenderedFrameCount > frameBeforeInvalidation, cycle, "initial invalidated frame");

                using Bitmap initialCapture = renderer.Capture();
                (Color background, Color center) = AssertKnownFrame(initialCapture, cycle, "initial");
                if (cycle == 1)
                {
                    firstBackground = background;
                    firstCenter = center;
                }

                VerifyIdempotentSteadyState(renderer, cycle);
                Size initialSize = initialCapture.Size;
                shell.ClientSize = new Size(720 + cycle % 2, 520 + cycle % 3);
                Application.DoEvents();
                renderer.Resize(renderer.Control.ClientSize);
                int frameBeforeResizePaint = renderer.RenderedFrameCount;
                renderer.RequestRender();
                PumpUntil(() => renderer.RenderedFrameCount > frameBeforeResizePaint, cycle, "resized invalidated frame");

                using Bitmap resizedCapture = renderer.Capture();
                AssertKnownFrame(resizedCapture, cycle, "resized");
                Assert(resizedCapture.Size != initialSize, $"Cycle {cycle}: resizing did not change capture dimensions ({initialSize}).");
                VerifyIdleDoesNotRender(renderer, cycle);

                shell.Close();
                Application.DoEvents();
            }

            AssertNoLiveObjects(RendererDiagnostics.Snapshot(), ProbeWindowDiagnostics.LiveWindows, $"cycle {cycle} teardown");
        }

        RendererDiagnosticSnapshot final = RendererDiagnostics.Snapshot();
        long contextsCreated = final.ContextsCreated - baseline.ContextsCreated;
        long framesRendered = final.FramesRendered - baseline.FramesRendered;
        long capturesCompleted = final.CapturesCompleted - baseline.CapturesCompleted;
        Assert(contextsCreated == cycles, $"Expected {cycles} contexts, created {contextsCreated}.");
        Assert(capturesCompleted == cycles * 2L, $"Expected {cycles * 2L} captures, completed {capturesCompleted}.");

        return new VerificationReport(
            "pass",
            cycles,
            contextsCreated,
            framesRendered,
            capturesCompleted,
            final.LiveContexts,
            final.LiveSubscriptions,
            ProbeWindowDiagnostics.LiveWindows,
            openGlVersion ?? string.Empty,
            ToRgb(firstBackground),
            ToRgb(firstCenter));
    }

    private static void VerifyIdempotentInitialization(OpenGlViewportLifecycle renderer, int cycle)
    {
        RendererDiagnosticSnapshot before = RendererDiagnostics.Snapshot();
        int uploadsBefore = renderer.ModelUploadCount;
        renderer.Initialize();
        renderer.SetModel(ProbeSceneModel.KnownFrame);
        renderer.Resize(renderer.Control.ClientSize);
        RendererDiagnosticSnapshot after = RendererDiagnostics.Snapshot();

        Assert(after.ContextsCreated == before.ContextsCreated, $"Cycle {cycle}: Initialize created a second context.");
        Assert(renderer.ModelUploadCount == uploadsBefore, $"Cycle {cycle}: SetModel uploaded an unchanged model.");
    }

    private static void VerifyIdempotentSteadyState(OpenGlViewportLifecycle renderer, int cycle)
    {
        int framesBefore = renderer.RenderedFrameCount;
        int resizesBefore = renderer.ResizeCount;
        renderer.Render();
        renderer.Resize(renderer.Control.ClientSize);

        Assert(renderer.RenderedFrameCount == framesBefore, $"Cycle {cycle}: clean Render produced an extra frame.");
        Assert(renderer.ResizeCount == resizesBefore, $"Cycle {cycle}: unchanged Resize changed renderer state.");
    }

    private static void VerifyIdleDoesNotRender(OpenGlViewportLifecycle renderer, int cycle)
    {
        int framesBefore = renderer.RenderedFrameCount;
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromMilliseconds(75))
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }

        Assert(renderer.RenderedFrameCount == framesBefore, $"Cycle {cycle}: idle processing rendered continuously.");
    }

    private static (Color Background, Color Center) AssertKnownFrame(Bitmap bitmap, int cycle, string phase)
    {
        Assert(bitmap.Width > 1 && bitmap.Height > 1, $"Cycle {cycle} {phase}: capture dimensions were empty.");
        Color background = bitmap.GetPixel(1, 1);
        Color center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);

        Assert(background.A == 255, $"Cycle {cycle} {phase}: background alpha was {background.A}.");
        Assert(center.A == 255, $"Cycle {cycle} {phase}: center alpha was {center.A}.");
        Assert(background.B >= 45 && background.R <= 20, $"Cycle {cycle} {phase}: unexpected background {ToRgb(background)}.");
        Assert(center.R >= 230 && center.G is >= 75 and <= 105 && center.B <= 50,
            $"Cycle {cycle} {phase}: expected triangle center, got {ToRgb(center)}.");
        Assert(background.ToArgb() != center.ToArgb(), $"Cycle {cycle} {phase}: capture did not contain distinct scene pixels.");
        return (background, center);
    }

    private static void AssertLiveObjects(int cycle, int expectedContexts, int expectedSubscriptions, int expectedWindows)
    {
        RendererDiagnosticSnapshot snapshot = RendererDiagnostics.Snapshot();
        Assert(snapshot.LiveContexts == expectedContexts,
            $"Cycle {cycle}: expected {expectedContexts} live context, found {snapshot.LiveContexts}.");
        Assert(snapshot.LiveSubscriptions == expectedSubscriptions,
            $"Cycle {cycle}: expected {expectedSubscriptions} live subscriptions, found {snapshot.LiveSubscriptions}.");
        Assert(ProbeWindowDiagnostics.LiveWindows == expectedWindows,
            $"Cycle {cycle}: expected {expectedWindows} live windows, found {ProbeWindowDiagnostics.LiveWindows}.");
    }

    private static void AssertNoLiveObjects(RendererDiagnosticSnapshot snapshot, int liveWindows, string phase)
    {
        Assert(snapshot.LiveContexts == 0, $"{phase}: {snapshot.LiveContexts} GL contexts remain live.");
        Assert(snapshot.LiveSubscriptions == 0, $"{phase}: {snapshot.LiveSubscriptions} event subscriptions remain live.");
        Assert(liveWindows == 0, $"{phase}: {liveWindows} windows remain live.");
    }

    private static void PumpUntil(Func<bool> condition, int cycle, string operation)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > PaintTimeout)
            {
                throw new InvalidOperationException($"Cycle {cycle}: timed out waiting for {operation}.");
            }

            Application.DoEvents();
            Thread.Sleep(1);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string ToRgb(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}

internal sealed record VerificationReport(
    string Status,
    int Cycles,
    long ContextsCreated,
    long FramesRendered,
    long CapturesCompleted,
    int LiveContexts,
    int LiveSubscriptions,
    int LiveWindows,
    string OpenGlVersion,
    string BackgroundRgb,
    string CenterRgb);
