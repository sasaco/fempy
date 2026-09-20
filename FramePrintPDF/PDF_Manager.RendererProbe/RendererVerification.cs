using System.Diagnostics;
using PDF_Manager.Rendering;
using PDF_Manager.Rendering.Scene;

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

                VerifyTypedScene(shell, renderer, cycle);
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
        Assert(capturesCompleted == cycles * 3L, $"Expected {cycles * 3L} captures, completed {capturesCompleted}.");

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

    private static void VerifyTypedScene(RendererProbeShell shell, OpenGlViewportLifecycle renderer, int cycle)
    {
        ViewportSceneModel scene = ProbeSceneModel.TypedFrame;
        int uploadsBefore = renderer.ModelUploadCount;
        int framesBefore = renderer.RenderedFrameCount;

        renderer.SetScene(scene);
        Assert(ReferenceEquals(renderer.Scene, scene), $"Cycle {cycle}: typed scene was not retained.");
        Assert(renderer.ModelUploadCount == uploadsBefore + 1,
            $"Cycle {cycle}: typed scene was not uploaded exactly once.");

        renderer.Render();
        Assert(renderer.RenderedFrameCount == framesBefore + 1,
            $"Cycle {cycle}: SetScene did not produce a typed frame without a legacy model.");

        int uploadsAfterFirstSet = renderer.ModelUploadCount;
        renderer.SetScene(scene);
        Assert(renderer.ModelUploadCount == uploadsAfterFirstSet,
            $"Cycle {cycle}: setting the unchanged typed scene uploaded it again.");

        Assert(renderer.Projection == ViewportProjection.Orthographic,
            $"Cycle {cycle}: typed scene did not start in orthographic projection.");
        renderer.ToggleProjection();
        Assert(renderer.Projection == ViewportProjection.Perspective,
            $"Cycle {cycle}: projection toggle did not select perspective.");
        renderer.Home();
        renderer.Fit();

        int resizeBefore = renderer.ResizeCount;
        Size sizeBefore = renderer.Control.ClientSize;
        shell.ClientSize = new Size(shell.ClientSize.Width + 3, shell.ClientSize.Height + 2);
        Application.DoEvents();
        renderer.Resize(renderer.Control.ClientSize);
        Assert(renderer.ResizeCount > resizeBefore,
            $"Cycle {cycle}: typed scene resize did not update the viewport.");
        Assert(renderer.Control.ClientSize != sizeBefore,
            $"Cycle {cycle}: typed scene resize did not change the control dimensions.");

        List<ViewportSelectionChangedEventArgs> selectionChanges = [];
        EventHandler<ViewportSelectionChangedEventArgs> handler = (_, change) => selectionChanges.Add(change);
        renderer.SelectionChanged += handler;
        try
        {
            SceneEntityKey tableSelection = new(SceneEntityKind.Member, "M1");
            renderer.SetSelection(tableSelection, ViewportSelectionOrigin.Table);
            Assert(renderer.Selection == tableSelection,
                $"Cycle {cycle}: table selection was not applied to the viewport.");

            ViewportSceneCommandBuffer commands = ViewportSceneCompiler.Compile(
                scene,
                renderer.Camera,
                renderer.Control.ClientSize,
                renderer.Selection);
            Assert(commands.HitTargets.Any(target =>
                    target.Key == new SceneEntityKey(SceneEntityKind.Support, "S2") &&
                    target.Points.Count > 2),
                $"Cycle {cycle}: rotation-only support glyph was not compiled.");
            Assert(commands.HitTargets.Any(target =>
                    target.Key == new SceneEntityKey(SceneEntityKind.NodalLoad, "NL2") &&
                    target.Points.Count > 2),
                $"Cycle {cycle}: moment-only nodal-load glyph was not compiled.");
            SceneHitTarget node = commands.HitTargets.Single(target =>
                target.Key == new SceneEntityKey(SceneEntityKind.Node, "N3"));
            Point hitPoint = FindHitPoint(commands, node, renderer.Control.ClientSize);

            Assert(renderer.SelectAt(hitPoint), $"Cycle {cycle}: typed node hit test did not select an entity.");
            Assert(renderer.Selection == node.Key, $"Cycle {cycle}: typed node hit test selected the wrong entity.");
            Assert(selectionChanges.Count == 2,
                $"Cycle {cycle}: expected table and viewport selection notifications, found {selectionChanges.Count}.");
            Assert(selectionChanges[0].Origin == ViewportSelectionOrigin.Table,
                $"Cycle {cycle}: first typed selection did not retain table origin.");
            Assert(selectionChanges[1].Origin == ViewportSelectionOrigin.Viewport,
                $"Cycle {cycle}: hit selection did not report viewport origin.");
        }
        finally
        {
            renderer.SelectionChanged -= handler;
        }

        int frameBeforeTypedPaint = renderer.RenderedFrameCount;
        renderer.Render();
        Assert(renderer.RenderedFrameCount == frameBeforeTypedPaint + 1,
            $"Cycle {cycle}: dirty typed scene was not rendered.");

        using Bitmap typedCapture = renderer.Capture();
        AssertTypedFrame(typedCapture, cycle);
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

    private static void AssertTypedFrame(Bitmap bitmap, int cycle)
    {
        Assert(bitmap.Width > 1 && bitmap.Height > 1, $"Cycle {cycle} typed: capture dimensions were empty.");
        Color background = bitmap.GetPixel(1, 1);
        int scenePixels = 0;
        int displacementPixels = 0;
        int selectionPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.ToArgb() != background.ToArgb())
                {
                    scenePixels++;
                }

                if (pixel.R >= 220 && pixel.G <= 120 && pixel.B >= 140)
                {
                    displacementPixels++;
                }

                if (pixel.R >= 230 && pixel.G >= 180 && pixel.B <= 90)
                {
                    selectionPixels++;
                }
            }
        }

        Assert(background.B >= 45 && background.R <= 20,
            $"Cycle {cycle} typed: unexpected background {ToRgb(background)}.");
        Assert(scenePixels >= 10, $"Cycle {cycle} typed: capture did not contain typed scene pixels.");
        Assert(displacementPixels > 0, $"Cycle {cycle} typed: displacement layer was not visible.");
        Assert(selectionPixels > 0, $"Cycle {cycle} typed: selection highlight was not visible.");
    }

    private static Point FindHitPoint(
        ViewportSceneCommandBuffer commands,
        SceneHitTarget target,
        Size size)
    {
        ScenePoint2 point = target.Points.Single();
        Point center = new(
            (int)MathF.Round((point.X + 1.0f) * 0.5f * size.Width),
            (int)MathF.Round((1.0f - point.Y) * 0.5f * size.Height));
        for (int radius = 0; radius <= 8; radius++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    Point candidate = new(center.X + offsetX, center.Y + offsetY);
                    if (candidate.X < 0 || candidate.X >= size.Width ||
                        candidate.Y < 0 || candidate.Y >= size.Height)
                    {
                        continue;
                    }

                    if (ViewportSceneCompiler.HitTest(commands, candidate, size) == target.Key)
                    {
                        return candidate;
                    }
                }
            }
        }

        throw new InvalidOperationException($"No unambiguous hit point was found for {target.Key.Kind} '{target.Key.Id}'.");
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
