using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using PDF_Manager.Core.Documents;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.ScreenComposition.Core;
using PDF_Manager.Shell.ScreenComposition.Surfaces;

namespace PDF_Manager.LiveCaptureProbe;

internal static class Program
{
    private static readonly TimeSpan RenderDelay = TimeSpan.FromMilliseconds(120);
    private static readonly CaptureConfiguration[] RequiredConfigurations =
    [
        new(1200, 800, 100),
        new(1024, 768, 100),
        new(1440, 900, 150),
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            Console.Error.WriteLine("WINFORMS_CAPTURE_FAILED apartment=non-STA");
            return 2;
        }

        try
        {
            string outputDirectory = args.Length >= 1 && !args[0].StartsWith("--", StringComparison.Ordinal)
                ? Path.GetFullPath(args[0])
                : Path.Combine(Path.GetTempPath(), "frameweb-winforms-capture");
            bool fullMatrix = args.Contains("--matrix", StringComparer.Ordinal);
            Directory.CreateDirectory(outputDirectory);

            LocalizationService localization = new(UiLanguage.Japanese);
            using MainForm form = new(new MainFormServices(localization: localization));
            ConfigureWindow(form);
            form.Show();
            form.Activate();
            form.BringToFront();
            PumpFor(RenderDelay);
            PumpUntilCompleted(form.NewProjectAsync());
            RequireCanonicalBlankDocument(form);

            List<CapturedState> captures = [];
            CaptureConfiguration[] configurations = fullMatrix
                ? RequiredConfigurations
                : [RequiredConfigurations[0]];
            foreach (CaptureConfiguration configuration in configurations)
            {
                CaptureConfigurationMatrix(form, localization, outputDirectory, configuration, fullMatrix, captures);
            }

            if (!fullMatrix)
            {
                CapturedState start = captures.Single(capture => capture.State == "overlay.start");
                CapturedState empty = captures.Single(capture => capture.State == "shell.empty");
                CapturedState elements = captures.Single(capture => capture.State == "route.input-elements");
                RequireDistinct(outputDirectory, start, empty, Scale(new Rectangle(180, 90, 840, 620), start.RenderScale), 0.01);
                RequireDistinct(outputDirectory, elements, empty, Scale(new Rectangle(760, 84, 440, 716), elements.RenderScale), 0.02);
            }

            CaptureMetadata metadata = new(
                ManifestVersion: 2,
                CapturedAtUtc: DateTimeOffset.UtcNow,
                Fixture: "PDF_Manager canonical blank document through MainForm.NewProjectAsync",
                Theme: "FrameWeb desktop light workspace",
                Language: "ja",
                ProductAssemblySha256: AssemblyHash(typeof(MainForm).Assembly),
                ProbeAssemblySha256: AssemblyHash(Assembly.GetExecutingAssembly()),
                CaptureMethod: "DrawToBitmap shell base plus independently rendered route/overlay layers; requested physical scale applied deterministically",
                StructuralGate: "exact route/overlay type, z-order, geometry, visible card/dialog colors, blank fixture, and source-derived product exclusions",
                Captures: captures);
            string metadataPath = Path.Combine(outputDirectory, "winforms-live-captures.v2.json");
            File.WriteAllText(
                metadataPath,
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));
            Console.WriteLine($"WINFORMS_CAPTURE_METADATA {metadataPath}");
            form.Hide();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("WINFORMS_CAPTURE_FAILED exception");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ConfigureWindow(MainForm form)
    {
        form.FormBorderStyle = FormBorderStyle.None;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = Point.Empty;
        form.ClientSize = new Size(1200, 800);
    }

    private static void CaptureConfigurationMatrix(
        MainForm form,
        LocalizationService localization,
        string outputDirectory,
        CaptureConfiguration configuration,
        bool fullMatrix,
        List<CapturedState> captures)
    {
        form.ClientSize = configuration.LogicalSize;
        form.Location = Point.Empty;
        PumpFor(RenderDelay);
        RequireWindowGeometry(form, configuration);

        ResetShell(form);
        form.RouteController.ShowOverlay(ScreenOverlayKind.Start);
        PumpFor(RenderDelay);
        captures.Add(Capture(form, localization, outputDirectory, "overlay.start", configuration));

        form.RouteController.CloseOverlay();
        PumpFor(RenderDelay);
        captures.Add(Capture(form, localization, outputDirectory, "shell.empty", configuration));

        IEnumerable<ScreenRouteDefinition> inputRoutes = fullMatrix
            ? AngularScreenManifest.InputRoutes
            : AngularScreenManifest.InputRoutes.Where(route => route.Id == ScreenRouteId.InputElements);
        foreach (ScreenRouteDefinition route in inputRoutes)
        {
            form.RouteController.SetDimension(
                route.Id == ScreenRouteId.InputPanel
                    ? ViewDimension.ThreeDimensional
                    : ViewDimension.TwoDimensional);
            form.RouteController.Navigate(route.Id);
            PumpFor(RenderDelay);
            captures.Add(Capture(form, localization, outputDirectory, $"route.{route.AngularPath}", configuration));
        }

        if (!fullMatrix) return;

        form.RouteController.SetDimension(ViewDimension.TwoDimensional);
        form.RouteController.SetResultsEnabled(true);
        foreach (ScreenRouteDefinition route in AngularScreenManifest.ResultRoutes)
        {
            form.RouteController.Navigate(route.Id);
            PumpFor(RenderDelay);
            captures.Add(Capture(form, localization, outputDirectory, $"route.{route.AngularPath}", configuration));
        }

        form.RouteController.CloseRoute();
        form.RouteController.SetResultsEnabled(false);
        foreach (ScreenOverlayKind overlay in new[]
                 {
                     ScreenOverlayKind.Preset,
                     ScreenOverlayKind.Print,
                     ScreenOverlayKind.Wait,
                     ScreenOverlayKind.Confirm,
                     ScreenOverlayKind.Alert,
                 })
        {
            form.RouteController.ShowOverlay(overlay);
            PumpFor(RenderDelay);
            captures.Add(Capture(form, localization, outputDirectory, $"overlay.{overlay.ToString().ToLowerInvariant()}", configuration));
            form.RouteController.CloseOverlay();
            PumpFor(RenderDelay);
        }
    }

    private static void ResetShell(MainForm form)
    {
        form.RouteController.CloseOverlay();
        form.RouteController.CloseRoute();
        form.RouteController.SetResultsEnabled(false);
        form.RouteController.SetDimension(ViewDimension.TwoDimensional);
        PumpFor(RenderDelay);
    }

    private static void RequireWindowGeometry(MainForm form, CaptureConfiguration configuration)
    {
        Point sourcePoint = form.ScreenShell.PointToScreen(Point.Empty);
        Console.WriteLine(
            $"WINFORMS_CAPTURE_GEOMETRY source={sourcePoint.X},{sourcePoint.Y} " +
            $"form={form.Left},{form.Top},{form.ClientSize.Width},{form.ClientSize.Height} " +
            $"shell={form.ScreenShell.ClientSize.Width},{form.ScreenShell.ClientSize.Height} " +
            $"deviceDpi={form.DeviceDpi} requestedDpi={configuration.DpiPercent}");
        if (sourcePoint != Point.Empty || form.ClientSize != configuration.LogicalSize ||
            form.ScreenShell.ClientSize != configuration.LogicalSize)
        {
            throw new InvalidDataException(
                $"Window geometry differs from requested logical client: source={sourcePoint}, " +
                $"form={form.ClientSize}, shell={form.ScreenShell.ClientSize}, expected={configuration.LogicalSize}.");
        }

        if (form.DeviceDpi != 96 || form.ScreenShell.DeviceDpi != 96)
        {
            throw new InvalidDataException(
                $"Probe requires a stable 96-DPI logical surface before deterministic physical scaling; " +
                $"form={form.DeviceDpi}, shell={form.ScreenShell.DeviceDpi}.");
        }
    }

    private static void RequireCanonicalBlankDocument(MainForm form)
    {
        ProjectDocument document = form.CurrentDocument
            ?? throw new InvalidDataException("The New command did not create a document.");
        (string Name, int Count)[] collections =
        [
            (nameof(document.Nodes), document.Nodes.Count),
            (nameof(document.Sections), document.Sections.Count),
            (nameof(document.Members), document.Members.Count),
            (nameof(document.Supports), document.Supports.Count),
            (nameof(document.LoadCases), document.LoadCases.Count),
            (nameof(document.NodalLoads), document.NodalLoads.Count),
            (nameof(document.DerivedResults), document.DerivedResults.Count),
            (nameof(document.MovingLoads), document.MovingLoads.Count),
            (nameof(document.ElementPropertySets), document.ElementPropertySets.Count),
            (nameof(document.RigidZones), document.RigidZones.Count),
            (nameof(document.SupportSets), document.SupportSets.Count),
            (nameof(document.Panels), document.Panels.Count),
            (nameof(document.JointReleaseSets), document.JointReleaseSets.Count),
            (nameof(document.NoticePoints), document.NoticePoints.Count),
            (nameof(document.MemberSpringSets), document.MemberSpringSets.Count),
            (nameof(document.PrescribedDisplacements), document.PrescribedDisplacements.Count),
            (nameof(document.MemberLoads), document.MemberLoads.Count),
        ];
        string[] populated = collections.Where(item => item.Count != 0).Select(item => $"{item.Name}={item.Count}").ToArray();
        if (populated.Length != 0)
        {
            throw new InvalidDataException(
                $"The New command did not create the canonical blank fixture: {string.Join(", ", populated)}.");
        }

        if (!form.ScreenShell.HeaderBar.PrintButton.Enabled || form.RouteController.State.ResultsEnabled)
        {
            throw new InvalidDataException(
                $"Unexpected blank-document command state: print={form.ScreenShell.HeaderBar.PrintButton.Enabled}, " +
                $"results={form.RouteController.State.ResultsEnabled}.");
        }
    }

    private static CapturedState Capture(
        MainForm form,
        LocalizationService localization,
        string outputDirectory,
        string state,
        CaptureConfiguration configuration)
    {
        FrameWebShellControl shell = form.ScreenShell;
        ValidateComposition(shell, localization, state, configuration);

        using Bitmap logical = new(shell.ClientSize.Width, shell.ClientSize.Height, PixelFormat.Format32bppArgb);
        shell.DrawToBitmap(logical, new Rectangle(Point.Empty, logical.Size));
        CompositeControl(shell, shell.Workspace.HomeButton, logical);
        CompositeActiveSurface(shell, logical);
        Color topLeft = logical.GetPixel(0, 0);
        Color expectedTopLeft = state.StartsWith("overlay.", StringComparison.Ordinal)
            ? Color.FromArgb(6, 7, 9)
            : Color.FromArgb(30, 37, 44);
        if (Math.Abs(topLeft.R - expectedTopLeft.R) > 8 ||
            Math.Abs(topLeft.G - expectedTopLeft.G) > 8 ||
            Math.Abs(topLeft.B - expectedTopLeft.B) > 8)
        {
            throw new InvalidDataException(
                $"Captured top-left pixel is {topLeft}; expected {expectedTopLeft} for state '{state}'.");
        }

        ValidateSurfacePixels(shell, state, logical);
        string fileName = FileName(state, configuration);
        string path = Path.Combine(outputDirectory, fileName);
        if (configuration.RenderScale == 1)
        {
            logical.Save(path, ImageFormat.Png);
        }
        else
        {
            using Bitmap physical = new(configuration.ImageSize.Width, configuration.ImageSize.Height, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(physical);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(logical, new Rectangle(Point.Empty, physical.Size));
            physical.Save(path, ImageFormat.Png);
        }

        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        Console.WriteLine(
            $"WINFORMS_CAPTURE_OK {state} {configuration.Width} {configuration.Height} " +
            $"{configuration.DpiPercent} {hash} {path}");
        return new CapturedState(
            state,
            fileName,
            configuration.Width,
            configuration.Height,
            configuration.ImageSize.Width,
            configuration.ImageSize.Height,
            configuration.DpiPercent,
            hash,
            configuration.RenderScale,
            AngularScreenManifest.InputRoutes.Concat(AngularScreenManifest.ResultRoutes)
                .FirstOrDefault(route => $"route.{route.AngularPath}" == state)?.Id.ToString(),
            shell.State.Dimension.ToString());
    }

    private static void CompositeActiveSurface(FrameWebShellControl shell, Bitmap destination)
    {
        if (shell.OverlayHost.ActiveSurface is OverlaySurfaceBase overlay)
        {
            using (Graphics graphics = Graphics.FromImage(destination))
            using (SolidBrush mask = new(Color.FromArgb(204, Color.Black)))
            {
                graphics.FillRectangle(mask, new Rectangle(Point.Empty, destination.Size));
            }

            CompositeControl(shell, overlay.DialogPanel, destination);
            return;
        }

        if (shell.RoutePanelHost.ActiveSurface is IFloatingRouteSurface route)
        {
            CompositeControl(shell, route.FloatingCard, destination);
        }
    }

    private static void CompositeControl(FrameWebShellControl shell, Control control, Bitmap destination)
    {
        if (!control.Visible || control.ClientSize.Width <= 0 || control.ClientSize.Height <= 0)
        {
            throw new InvalidDataException($"Cannot composite hidden or empty control '{control.Name}'.");
        }

        using Bitmap layerBitmap = new(control.Width, control.Height, PixelFormat.Format32bppArgb);
        control.DrawToBitmap(layerBitmap, new Rectangle(Point.Empty, layerBitmap.Size));
        Rectangle destinationBounds = BoundsRelativeToShell(shell, control);
        using Graphics graphics = Graphics.FromImage(destination);
        graphics.DrawImageUnscaled(layerBitmap, destinationBounds.Location);
    }

    private static void ValidateComposition(
        FrameWebShellControl shell,
        LocalizationService localization,
        string state,
        CaptureConfiguration configuration)
    {
        Control body = shell.Controls.Cast<Control>().Single(control => control.Name == "FrameWebBody");
        Control baseLayer = body.Controls.Cast<Control>().Single(control => control.Name == "WorkspaceLayer");
        int overlayIndex = shell.Controls.GetChildIndex(shell.OverlayHost);
        int routeIndex = body.Controls.GetChildIndex(shell.RoutePanelHost);
        int baseIndex = body.Controls.GetChildIndex(baseLayer);
        Control? homeParent = shell.Workspace.HomeButton.Parent;
        int homeIndex = homeParent?.Controls.GetChildIndex(shell.Workspace.HomeButton) ?? -1;
        Console.WriteLine(
            $"WINFORMS_CAPTURE_STRUCTURE {state} childIndex={overlayIndex},{routeIndex},{baseIndex} " +
            $"overlay={shell.OverlayHost.Visible},{shell.OverlayHost.Bounds},{shell.OverlayHost.ActiveSurface?.GetType().Name ?? "none"} " +
            $"route={shell.RoutePanelHost.Visible},{shell.RoutePanelHost.Bounds},{shell.RoutePanelHost.ActiveSurface?.GetType().Name ?? "none"} " +
            $"workspace={shell.Workspace.Bounds} home={shell.Workspace.HomeButton.Visible}," +
            $"{shell.Workspace.HomeButton.Bounds},parent={homeParent?.Name ?? "none"},index={homeIndex}");
        if ((overlayIndex, routeIndex, baseIndex) != (0, 0, 1) || shell.OverlayHost.Bounds != shell.ClientRectangle)
        {
            throw new InvalidDataException(
                $"Unexpected shell z-order/bounds: overlay={overlayIndex},{shell.OverlayHost.Bounds}, " +
                $"route={routeIndex}, base={baseIndex}, shell={shell.ClientRectangle}.");
        }

        if (!shell.Workspace.HomeButton.Visible ||
            homeParent != shell.Workspace.ViewportSurface ||
            homeIndex != 0 ||
            shell.Workspace.HomeButton.Bounds != new Rectangle(79, 20, 28, 28) ||
            BoundsRelativeToShell(shell, shell.Workspace.HomeButton) != new Rectangle(203, 104, 28, 28))
        {
            throw new InvalidDataException(
                $"Workspace home control is not in the Angular position: visible={shell.Workspace.HomeButton.Visible}, " +
                $"parent={shell.Workspace.HomeButton.Parent?.Name}, bounds={shell.Workspace.HomeButton.Bounds}, " +
                $"global={BoundsRelativeToShell(shell, shell.Workspace.HomeButton)}.");
        }

        Rectangle twoDimensionalBounds = BoundsRelativeToShell(shell, shell.OptionalHeader.TwoDimensionalButton);
        Rectangle threeDimensionalBounds = BoundsRelativeToShell(shell, shell.OptionalHeader.ThreeDimensionalButton);
        ViewDimension expectedDimension = state == "route.input-panel"
            ? ViewDimension.ThreeDimensional
            : ViewDimension.TwoDimensional;
        if (shell.State.Dimension != expectedDimension ||
            twoDimensionalBounds != new Rectangle(14, 49, 50, 30) ||
            threeDimensionalBounds != new Rectangle(66, 49, 50, 30))
        {
            throw new InvalidDataException(
                $"Dimension selector drift: state={state}, dimension={shell.State.Dimension}, " +
                $"2D={twoDimensionalBounds}, 3D={threeDimensionalBounds}.");
        }

        if (state == "shell.empty")
        {
            if (shell.OverlayHost.Visible || shell.RoutePanelHost.Visible ||
                shell.OverlayHost.ActiveSurface is not null || shell.RoutePanelHost.ActiveSurface is not null)
            {
                throw new InvalidDataException("Empty shell capture still contains a route or overlay surface.");
            }
            return;
        }

        if (state.StartsWith("route.", StringComparison.Ordinal))
        {
            ScreenRouteDefinition definition = AngularScreenManifest.InputRoutes
                .Concat(AngularScreenManifest.ResultRoutes)
                .Single(route => state == $"route.{route.AngularPath}");
            Control active = shell.RoutePanelHost.ActiveSurface
                ?? throw new InvalidDataException($"Route capture '{state}' lacks an active surface.");
            ScreenRouteId actualRoute = active switch
            {
                InputRouteSurfaceControl input => input.RouteKey,
                ResultRouteSurfaceControl result => result.RouteKey,
                _ => throw new InvalidDataException($"Unexpected route surface type '{active.GetType().Name}'."),
            };
            Control card = ((IFloatingRouteSurface)active).FloatingCard;
            using Graphics graphics = shell.RoutePanelHost.CreateGraphics();
            RectangleF regionBounds = shell.RoutePanelHost.Region?.GetBounds(graphics) ?? RectangleF.Empty;
            Rectangle expectedCardBounds = new(
                shell.RoutePanelHost.ClientSize.Width - 430,
                0,
                430,
                shell.RoutePanelHost.ClientSize.Height);
            if (actualRoute != definition.Id || !shell.RoutePanelHost.Visible || !active.Visible || !card.Visible ||
                card.Bounds != expectedCardBounds || regionBounds != expectedCardBounds)
            {
                throw new InvalidDataException(
                    $"Route '{state}' layout drift: expected={definition.Id},{expectedCardBounds}, " +
                    $"actual={actualRoute},host={shell.RoutePanelHost.Visible},surface={active.Visible}," +
                    $"card={card.Visible},{card.Bounds},region={regionBounds}.");
            }
            return;
        }

        ScreenOverlayKind expectedOverlay = Enum.Parse<ScreenOverlayKind>(state["overlay.".Length..], true);
        OverlaySurfaceBase overlay = shell.OverlayHost.ActiveSurface as OverlaySurfaceBase
            ?? throw new InvalidDataException($"Overlay capture '{state}' lacks an overlay surface.");
        if (shell.State.Overlay != expectedOverlay || !shell.OverlayHost.Visible || !overlay.Visible ||
            overlay.DialogPanel.Width <= 0 || overlay.DialogPanel.Height <= 0)
        {
            throw new InvalidDataException(
                $"Overlay '{state}' layout drift: controller={shell.State.Overlay}, host={shell.OverlayHost.Visible}, " +
                $"surface={overlay.Visible}, dialog={overlay.DialogPanel.Bounds}.");
        }

        if (overlay is OperationOverlayControl operation)
        {
            bool validOperationState = expectedOverlay switch
            {
                ScreenOverlayKind.Wait =>
                    operation.Progress.Visible &&
                    operation.Progress.Style == ProgressBarStyle.Marquee &&
                    !operation.CancelOperationButton.Visible &&
                    !operation.PrimaryButton.Visible &&
                    !operation.CloseButton.Visible &&
                    !operation.Controls.Find("OperationOverlayMessage", searchAllChildren: true).Single().Visible,
                ScreenOverlayKind.Confirm =>
                    !operation.Progress.Visible &&
                    operation.CancelOperationButton.Visible &&
                    operation.PrimaryButton.Visible &&
                    operation.CloseButton.Visible,
                ScreenOverlayKind.Alert =>
                    !operation.Progress.Visible &&
                    !operation.CancelOperationButton.Visible &&
                    operation.PrimaryButton.Visible &&
                    operation.CloseButton.Visible,
                _ => false,
            };
            Control message = operation.Controls.Find("OperationOverlayMessage", searchAllChildren: true).Single();
            bool expectedMessageVisible = expectedOverlay != ScreenOverlayKind.Wait;
            if (!validOperationState || message.Visible != expectedMessageVisible)
            {
                throw new InvalidDataException(
                    $"Angular operation-overlay controls drifted for {expectedOverlay}: " +
                    $"progress={operation.Progress.Visible}/{operation.Progress.Style}, " +
                    $"primary={operation.PrimaryButton.Visible}, cancel={operation.CancelOperationButton.Visible}, " +
                    $"close={operation.CloseButton.Visible}, message={message.Visible}.");
            }
        }

        if (expectedOverlay == ScreenOverlayKind.Print && overlay is PrintOverlayControl print)
        {
            string modelLabel = localization["PrintSectionModelDiagram"];
            if (print.SectionSelector.Items.Cast<object>().Any(item => string.Equals(item.ToString(), modelLabel, StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "Angular model-screen print option is comment-only, but WinForms exposes ModelDiagram.");
            }
        }

        Rectangle dialogGlobal = BoundsRelativeToShell(shell, overlay.DialogPanel);
        Rectangle expectedCentered = new(
            Math.Max(0, (configuration.Width - overlay.DialogPanel.Width) / 2),
            Math.Max(0, (configuration.Height - overlay.DialogPanel.Height) / 2),
            overlay.DialogPanel.Width,
            overlay.DialogPanel.Height);
        if (dialogGlobal != expectedCentered)
        {
            throw new InvalidDataException(
                $"Overlay '{state}' is not centered: actual={dialogGlobal}, expected={expectedCentered}.");
        }
    }

    private static void ValidateSurfacePixels(FrameWebShellControl shell, string state, Bitmap bitmap)
    {
        if (state.StartsWith("overlay.", StringComparison.Ordinal))
        {
            if (shell.OverlayHost.ActiveSurface is OperationOverlayControl)
            {
                RequireDarkCoverage(
                    bitmap,
                    new Rectangle(Point.Empty, bitmap.Size),
                    64,
                    0.50,
                    $"{state} modal mask");
                return;
            }

            OverlaySurfaceBase overlay = (OverlaySurfaceBase)shell.OverlayHost.ActiveSurface!;
            Rectangle dialog = BoundsRelativeToShell(shell, overlay.DialogPanel);
            RequireColorCoverage(bitmap, dialog, Color.FromArgb(30, 37, 44), 0.015, $"{state} header");
            RequireColorCoverage(bitmap, dialog, Color.FromArgb(86, 88, 92), 0.04, $"{state} body");
        }
        else if (state.StartsWith("route.", StringComparison.Ordinal))
        {
            IFloatingRouteSurface route = (IFloatingRouteSurface)shell.RoutePanelHost.ActiveSurface!;
            Rectangle bounds = BoundsRelativeToShell(shell, route.FloatingCard);
            RequireColorCoverage(bitmap, bounds, Color.FromArgb(30, 37, 44), 0.01, $"{state} header");
            RequireColorCoverage(bitmap, bounds, Color.FromArgb(86, 88, 92), 0.02, $"{state} body");
        }
    }

    private static Rectangle BoundsRelativeToShell(FrameWebShellControl shell, Control control) =>
        control.Parent is null
            ? shell.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))
            : shell.RectangleToClient(control.Parent.RectangleToScreen(control.Bounds));

    private static void RequireColorCoverage(
        Bitmap bitmap,
        Rectangle region,
        Color expected,
        double minimumRatio,
        string label)
    {
        Rectangle clipped = Rectangle.Intersect(new Rectangle(Point.Empty, bitmap.Size), region);
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            throw new InvalidDataException($"{label} bounds {region} do not intersect the capture.");
        }

        int matching = 0;
        int total = checked(clipped.Width * clipped.Height);
        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                Color actual = bitmap.GetPixel(x, y);
                if (Math.Abs(actual.R - expected.R) <= 3 &&
                    Math.Abs(actual.G - expected.G) <= 3 &&
                    Math.Abs(actual.B - expected.B) <= 3)
                {
                    matching++;
                }
            }
        }

        double ratio = matching / (double)total;
        if (!double.IsFinite(ratio) || ratio < minimumRatio)
        {
            throw new InvalidDataException(
                $"{label} color coverage is {ratio:P3}; required {minimumRatio:P3} in {region}.");
        }
    }

    private static void RequireDarkCoverage(
        Bitmap bitmap,
        Rectangle region,
        byte maximumChannel,
        double minimumRatio,
        string label)
    {
        Rectangle clipped = Rectangle.Intersect(new Rectangle(Point.Empty, bitmap.Size), region);
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            throw new InvalidDataException($"{label} bounds {region} do not intersect the capture.");
        }

        int matching = 0;
        int total = checked(clipped.Width * clipped.Height);
        for (int y = clipped.Top; y < clipped.Bottom; y++)
        {
            for (int x = clipped.Left; x < clipped.Right; x++)
            {
                Color actual = bitmap.GetPixel(x, y);
                if (actual.R <= maximumChannel && actual.G <= maximumChannel && actual.B <= maximumChannel)
                {
                    matching++;
                }
            }
        }

        double ratio = matching / (double)total;
        if (!double.IsFinite(ratio) || ratio < minimumRatio)
        {
            throw new InvalidDataException(
                $"{label} dark coverage is {ratio:P3}; required {minimumRatio:P3} in {region}.");
        }
    }

    private static void RequireDistinct(
        string outputDirectory,
        CapturedState actual,
        CapturedState baseline,
        Rectangle region,
        double minimumChangedRatio)
    {
        if (string.Equals(actual.Sha256, baseline.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Capture states '{actual.State}' and '{baseline.State}' have identical SHA-256 content.");
        }

        using Bitmap actualBitmap = new(Path.Combine(outputDirectory, actual.File));
        using Bitmap baselineBitmap = new(Path.Combine(outputDirectory, baseline.File));
        int changed = 0;
        int total = checked(region.Width * region.Height);
        for (int y = region.Top; y < region.Bottom; y++)
        {
            for (int x = region.Left; x < region.Right; x++)
            {
                if (actualBitmap.GetPixel(x, y).ToArgb() != baselineBitmap.GetPixel(x, y).ToArgb()) changed++;
            }
        }

        double changedRatio = changed / (double)total;
        if (changedRatio < minimumChangedRatio)
        {
            throw new InvalidDataException(
                $"Capture states '{actual.State}' and '{baseline.State}' differ in only " +
                $"{changedRatio:P3} of their representative region; required {minimumChangedRatio:P3}.");
        }
    }

    private static string FileName(string state, CaptureConfiguration configuration)
    {
        string prefix = state switch
        {
            "shell.empty" => "shell-empty",
            "overlay.start" => "shell-start-overlay",
            _ => state.Replace('.', '-').Replace('_', '-'),
        };
        return $"{prefix}-{configuration.Width}x{configuration.Height}-dpi{configuration.DpiPercent}.png";
    }

    private static Rectangle Scale(Rectangle rectangle, double scale) => new(
        (int)Math.Round(rectangle.X * scale),
        (int)Math.Round(rectangle.Y * scale),
        (int)Math.Round(rectangle.Width * scale),
        (int)Math.Round(rectangle.Height * scale));

    private static string AssemblyHash(Assembly assembly) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)));

    private static void PumpFor(TimeSpan duration)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < duration)
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
    }

    private static void PumpUntilCompleted(Task task)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timeout.Elapsed >= TimeSpan.FromSeconds(5))
            {
                throw new TimeoutException("The New command did not complete within five seconds.");
            }

            Application.DoEvents();
            Thread.Sleep(1);
        }

        task.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    private sealed record CaptureConfiguration(int Width, int Height, int DpiPercent)
    {
        public Size LogicalSize => new(Width, Height);

        public double RenderScale => DpiPercent / 100d;

        public Size ImageSize => new(
            (int)Math.Round(Width * RenderScale),
            (int)Math.Round(Height * RenderScale));
    }

    private sealed record CapturedState(
        string State,
        string File,
        int LogicalWidth,
        int LogicalHeight,
        int ImageWidth,
        int ImageHeight,
        int DpiPercent,
        string Sha256,
        double RenderScale,
        string? RouteId,
        string Dimension);

    private sealed record CaptureMetadata(
        int ManifestVersion,
        DateTimeOffset CapturedAtUtc,
        string Fixture,
        string Theme,
        string Language,
        string ProductAssemblySha256,
        string ProbeAssemblySha256,
        string CaptureMethod,
        string StructuralGate,
        IReadOnlyList<CapturedState> Captures);
}
