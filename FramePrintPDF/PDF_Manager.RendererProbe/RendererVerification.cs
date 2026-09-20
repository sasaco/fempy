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
        int orthographicCaptures = 0;
        int perspectiveCaptures = 0;
        int pngCaptures = 0;

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

                ProjectionCaptureCounts projectionCaptures = VerifyTypedScene(shell, renderer, cycle);
                orthographicCaptures += projectionCaptures.Orthographic;
                perspectiveCaptures += projectionCaptures.Perspective;
                pngCaptures += projectionCaptures.Png;
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
        Assert(capturesCompleted == cycles * 6L, $"Expected {cycles * 6L} captures, completed {capturesCompleted}.");
        Assert(orthographicCaptures == cycles,
            $"Expected {cycles} orthographic typed captures, completed {orthographicCaptures}.");
        Assert(perspectiveCaptures == cycles,
            $"Expected {cycles} perspective typed captures, completed {perspectiveCaptures}.");
        Assert(pngCaptures == cycles * 2,
            $"Expected {cycles * 2} PNG encodes, completed {pngCaptures}.");

        return new VerificationReport(
            "pass",
            cycles,
            contextsCreated,
            framesRendered,
            capturesCompleted,
            orthographicCaptures,
            perspectiveCaptures,
            pngCaptures,
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

    private static ProjectionCaptureCounts VerifyTypedScene(
        RendererProbeShell shell,
        OpenGlViewportLifecycle renderer,
        int cycle)
    {
        ViewportSceneModel scene = CreateDecoratedScene(ProbeSceneModel.TypedFrame);
        int uploadsBefore = renderer.ModelUploadCount;
        int framesBefore = renderer.RenderedFrameCount;

        renderer.SetCameraPolicy(ViewportCameraPolicy.TwoDimensional);
        renderer.SetScene(scene);
        Assert(ReferenceEquals(renderer.Scene, scene), $"Cycle {cycle}: typed scene was not retained.");
        Assert(renderer.ModelUploadCount == uploadsBefore + 1,
            $"Cycle {cycle}: typed scene was not uploaded exactly once.");

        PumpUntil(
            () => renderer.RenderedFrameCount > framesBefore,
            cycle,
            "decorated two-dimensional Paint frame");

        byte[] orthographicPng = renderer.CapturePng(new ViewportPngCaptureOptions(16 * 1024 * 1024));
        AssertPng(orthographicPng, cycle, "orthographic");
        using (MemoryStream stream = new(orthographicPng, writable: false))
        using (Bitmap orthographicCapture = new(stream))
        {
            AssertTypedFrame(orthographicCapture, cycle, "orthographic", requireSelection: false);
        }

        int uploadsAfterFirstSet = renderer.ModelUploadCount;
        renderer.SetScene(scene);
        Assert(renderer.ModelUploadCount == uploadsAfterFirstSet,
            $"Cycle {cycle}: setting the unchanged typed scene uploaded it again.");

        Assert(renderer.CameraPolicy == ViewportCameraPolicy.TwoDimensional &&
               renderer.Projection == ViewportProjection.Orthographic,
            $"Cycle {cycle}: typed scene did not start in orthographic projection.");
        int frameBeforeThreeDimensionalPaint = renderer.RenderedFrameCount;
        renderer.SetCameraPolicy(ViewportCameraPolicy.ThreeDimensional);
        Assert(renderer.CameraPolicy == ViewportCameraPolicy.ThreeDimensional &&
               renderer.Projection == ViewportProjection.Perspective,
            $"Cycle {cycle}: three-dimensional policy did not select perspective.");
        renderer.Home();
        renderer.Fit();
        PumpUntil(
            () => renderer.RenderedFrameCount > frameBeforeThreeDimensionalPaint,
            cycle,
            "decorated three-dimensional Paint frame");

        byte[] perspectivePng = renderer.CapturePng(new ViewportPngCaptureOptions(16 * 1024 * 1024));
        AssertPng(perspectivePng, cycle, "perspective");
        using (MemoryStream stream = new(perspectivePng, writable: false))
        using (Bitmap perspectiveCapture = new(stream))
        {
            AssertTypedFrame(perspectiveCapture, cycle, "perspective", requireSelection: false);
        }

        SceneLayerMask visibleLayers = renderer.VisibleLayers;
        renderer.SetVisibleLayers(visibleLayers & ~SceneLayerMask.Decorations);
        renderer.Render();
        using (Bitmap decorationsOffCapture = renderer.Capture())
        {
            AssertTypedFrame(
                decorationsOffCapture,
                cycle,
                "decorations-off",
                requireSelection: false,
                requireDecorations: false);
        }
        renderer.SetVisibleLayers(visibleLayers);
        renderer.Render();

        VerifyFloatDockLifecycle(shell, renderer, cycle);

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
        AssertTypedFrame(typedCapture, cycle, "selected", requireSelection: true);
        return new ProjectionCaptureCounts(1, 1, 2);
    }

    private static ViewportSceneModel CreateDecoratedScene(ViewportSceneModel source)
    {
        SceneNode labelNode = source.Nodes.Single(node => node.Id == "N3");
        ScenePresentationOptions presentation = new(
            grid: new SceneGridDefinition(SceneGridPlane.XZ, 0.5f, 5),
            showAxes: true,
            labels:
            [
                new SceneLabel(
                    "probe-node-label",
                    "N3",
                    labelNode.Position,
                    new SceneEntityKey(SceneEntityKind.Node, labelNode.Id)),
            ],
            scaleLegend: new SceneScaleLegend("probe-scale", "Displacement", 1.5f),
            colorLegend: new SceneColorLegend(
                "probe-color-legend",
                "Response",
                [
                    new SceneColorLegendEntry("0", 0.0f, 0.10f, 0.30f, 0.90f),
                    new SceneColorLegendEntry("50%", 0.5f, 0.10f, 0.80f, 0.30f),
                    new SceneColorLegendEntry("100%", 1.0f, 0.90f, 0.20f, 0.10f),
                ]));
        return new ViewportSceneModel(
            $"{source.StableId}:decorated",
            source.Nodes,
            source.Members,
            source.Supports,
            source.NodalLoads,
            source.MemberLoads,
            source.Displacement,
            source.RigidZones,
            source.Springs,
            source.Joints,
            source.Panels,
            source.NoticePoints,
            source.PrescribedDisplacements,
            source.Reactions,
            source.SectionForces,
            presentation,
            source.VisibleLayers);
    }

    private static void VerifyFloatDockLifecycle(
        RendererProbeShell shell,
        OpenGlViewportLifecycle renderer,
        int cycle)
    {
        RendererDiagnosticSnapshot before = RendererDiagnostics.Snapshot();

        shell.SetDocumentDockState(WeifenLuo.WinFormsUI.Docking.DockState.Float);
        Assert(shell.Document.DockState == WeifenLuo.WinFormsUI.Docking.DockState.Float,
            $"Cycle {cycle}: document did not float.");
        renderer.Resize(renderer.Control.ClientSize);

        shell.SetDocumentDockState(WeifenLuo.WinFormsUI.Docking.DockState.Document);
        Assert(shell.Document.DockState == WeifenLuo.WinFormsUI.Docking.DockState.Document,
            $"Cycle {cycle}: document did not redock.");
        renderer.Resize(renderer.Control.ClientSize);

        RendererDiagnosticSnapshot after = RendererDiagnostics.Snapshot();
        Assert(after.LiveContexts == before.LiveContexts,
            $"Cycle {cycle}: float/dock changed live context count from {before.LiveContexts} to {after.LiveContexts}.");
        Assert(after.LiveSubscriptions == before.LiveSubscriptions,
            $"Cycle {cycle}: float/dock changed live subscription count from {before.LiveSubscriptions} to {after.LiveSubscriptions}.");
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
        AssertDecorationPixels(bitmap, cycle, phase, expected: false);
        return (background, center);
    }

    private static void AssertTypedFrame(
        Bitmap bitmap,
        int cycle,
        string projection,
        bool requireSelection,
        bool requireDecorations = true)
    {
        Assert(bitmap.Width > 1 && bitmap.Height > 1, $"Cycle {cycle} typed: capture dimensions were empty.");
        Color background = Color.FromArgb(13, 25, 51);
        int backgroundPixels = 0;
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
                else
                {
                    backgroundPixels++;
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

        Assert(backgroundPixels > 0,
            $"Cycle {cycle} typed {projection}: the deterministic clear color {ToRgb(background)} was absent.");
        Assert(scenePixels >= 10, $"Cycle {cycle} typed {projection}: capture did not contain typed scene pixels.");
        Assert(displacementPixels > 0, $"Cycle {cycle} typed {projection}: displacement layer was not visible.");
        if (requireSelection)
        {
            Assert(selectionPixels > 0, $"Cycle {cycle} typed {projection}: selection highlight was not visible.");
        }

        AssertDecorationPixels(bitmap, cycle, projection, requireDecorations);
    }

    private static void AssertDecorationPixels(Bitmap bitmap, int cycle, string phase, bool expected)
    {
        Color[] signatures =
        [
            Color.FromArgb(32, 52, 79),
            Color.FromArgb(43, 73, 110),
            Color.FromArgb(237, 67, 67),
            Color.FromArgb(67, 220, 115),
            Color.FromArgb(69, 137, 240),
            Color.FromArgb(242, 242, 232),
            Color.FromArgb(109, 227, 255),
            Color.FromArgb(255, 175, 78),
        ];
        int[] counts = new int[signatures.Length];
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                for (int index = 0; index < signatures.Length; index++)
                {
                    Color signature = signatures[index];
                    if (Math.Abs(pixel.R - signature.R) <= 2 &&
                        Math.Abs(pixel.G - signature.G) <= 2 &&
                        Math.Abs(pixel.B - signature.B) <= 2)
                    {
                        counts[index]++;
                    }
                }
            }
        }

        if (expected)
        {
            Assert(counts[0] + counts[1] > 0,
                $"Cycle {cycle} {phase}: grid decoration pixels were absent.");
            Assert(counts[2] + counts[3] + counts[4] > 0,
                $"Cycle {cycle} {phase}: axis decoration pixels were absent.");
            Assert(counts[5] > 0,
                $"Cycle {cycle} {phase}: label decoration pixels were absent.");
            Assert(counts[6] > 0,
                $"Cycle {cycle} {phase}: scale decoration pixels were absent.");
            Assert(counts[7] > 0,
                $"Cycle {cycle} {phase}: color-legend decoration pixels were absent.");
            return;
        }

        Assert(counts.All(count => count == 0),
            $"Cycle {cycle} {phase}: decoration pixels remained while decorations were absent or disabled ({string.Join(',', counts)}).");
    }

    private static void AssertPng(byte[] bytes, int cycle, string projection)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];

        Assert(bytes.AsSpan().StartsWith(signature),
            $"Cycle {cycle} typed {projection}: encoded capture did not have a PNG signature.");
        Assert(bytes.Length <= 16 * 1024 * 1024,
            $"Cycle {cycle} typed {projection}: encoded capture exceeded the 16 MiB probe limit ({bytes.Length} bytes).");
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
    int OrthographicCaptures,
    int PerspectiveCaptures,
    int PngCaptures,
    int LiveContexts,
    int LiveSubscriptions,
    int LiveWindows,
    string OpenGlVersion,
    string BackgroundRgb,
    string CenterRgb);

internal readonly record struct ProjectionCaptureCounts(int Orthographic, int Perspective, int Png);
