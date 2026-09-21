using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.ScreenComposition.Core;
using PDF_Manager.Shell.ScreenComposition.Surfaces;

namespace PDF_Manager.UiTests.UiParity;

public sealed class ShellInteractionTests
{
    [Fact]
    public void InputNavigationShowsOneFloatingRouteCardAndPreservesViewport()
    {
        RunSta(() =>
        {
            using MainForm form = CreateForm();
            LayoutOffscreen(form);
            form.RouteController.CloseOverlay();
            form.RouteController.Navigate(ScreenRouteId.InputElements);
            LayoutOffscreen(form);

            Assert.Equal(ScreenRouteId.InputElements, form.RouteController.State.Route);
            Assert.Same(form.DocumentHost, form.ScreenShell.Workspace.DocumentHost);
            Assert.Same(form.ScreenShell.Workspace, form.ScreenShell.Workspace.ViewportSurface.Parent);
            Assert.Contains(
                form.ScreenShell.Workspace.ViewportSurface,
                form.ScreenShell.Workspace.Controls.Cast<Control>());
            Assert.Equal(
                1,
                form.ScreenShell.Workspace.Controls.Cast<Control>()
                    .Count(control => ReferenceEquals(control, form.ScreenShell.Workspace.ViewportSurface)));
            InputRouteSurfaceControl surface = Assert.IsType<InputRouteSurfaceControl>(
                form.ScreenShell.RoutePanelHost.ActiveSurface);
            Assert.Equal(ScreenRouteId.InputElements, surface.RouteKey);
            Control card = Assert.IsAssignableFrom<IFloatingRouteSurface>(surface).FloatingCard;
            Assert.Equal(430, card.Width);
            Assert.Equal(form.ScreenShell.RoutePanelHost.ClientSize.Width, card.Right);
            Assert.Equal(form.ScreenShell.RoutePanelHost.ClientSize.Width - 430, card.Left);
            Assert.Contains(
                card.Controls.Cast<Control>().SelectMany(DescendantsAndSelf),
                control => control.Name == "RouteCardCloseButton");
        }, "FrameWeb input route composition");
    }

    [Fact]
    public void OverlayTransitionsReplaceTheOverlayWithoutChangingTheActiveRoute()
    {
        RunSta(() =>
        {
            using MainForm form = CreateForm();
            LayoutOffscreen(form);
            form.RouteController.CloseOverlay();
            form.RouteController.Navigate(ScreenRouteId.InputNodes);
            Control originalRoute = Assert.IsType<InputRouteSurfaceControl>(form.ScreenShell.RoutePanelHost.ActiveSurface);

            form.RouteController.ShowOverlay(ScreenOverlayKind.Preset);
            Assert.IsType<PresetOverlayControl>(form.ScreenShell.OverlayHost.ActiveSurface);
            Assert.Equal(ScreenRouteId.InputNodes, form.RouteController.State.Route);
            Assert.Same(originalRoute, form.ScreenShell.RoutePanelHost.ActiveSurface);

            form.RouteController.ShowOverlay(ScreenOverlayKind.Print);
            Assert.IsType<PrintOverlayControl>(form.ScreenShell.OverlayHost.ActiveSurface);
            Assert.Equal(ScreenRouteId.InputNodes, form.RouteController.State.Route);

            form.RouteController.CloseOverlay();
            Assert.Null(form.ScreenShell.OverlayHost.ActiveSurface);
            Assert.Same(originalRoute, form.ScreenShell.RoutePanelHost.ActiveSurface);
        }, "FrameWeb route and overlay state");
    }

    [Fact]
    public void NavigationButtonsFollowResultsAndDimensionRules()
    {
        RunSta(() =>
        {
            using PrimaryNavigationControl navigation = new();
            navigation.ApplyState(ScreenRouteState.Default);

            Assert.False(navigation.Buttons[PrimaryNavigationId.Displacements].Enabled);
            Assert.False(navigation.Buttons[PrimaryNavigationId.Reactions].Enabled);
            Assert.False(navigation.Buttons[PrimaryNavigationId.SectionForces].Enabled);
            Assert.False(navigation.Buttons[PrimaryNavigationId.Panel].Visible);

            navigation.ApplyState(new ScreenRouteState(
                route: null,
                context: ScreenRouteContext.None,
                dimension: ViewDimension.TwoDimensional,
                page: ScreenPageState.Single,
                resultsEnabled: false,
                overlay: ScreenOverlayKind.Start));
            Assert.False(navigation.Buttons[PrimaryNavigationId.Panel].Visible);

            navigation.ApplyState(new ScreenRouteState(
                route: null,
                context: ScreenRouteContext.None,
                dimension: ViewDimension.ThreeDimensional,
                page: ScreenPageState.Single,
                resultsEnabled: true,
                overlay: ScreenOverlayKind.Start));
            Assert.True(navigation.Buttons[PrimaryNavigationId.Displacements].Enabled);
            Assert.True(navigation.Buttons[PrimaryNavigationId.Reactions].Enabled);
            Assert.True(navigation.Buttons[PrimaryNavigationId.SectionForces].Enabled);
        }, "FrameWeb navigation availability");
    }

    private static MainForm CreateForm()
    {
        MainForm form = new(new MainFormServices(localization: new LocalizationService(UiLanguage.English)));
        form.SetDocument(Step5EditorMatrixTests.CreateFullDocument());
        return form;
    }

    private static void LayoutOffscreen(MainForm form)
    {
        form.ClientSize = new Size(1200, 800);
        form.PerformLayout();
        form.ScreenShell.PerformLayout();
        form.ScreenShell.RoutePanelHost.PerformLayout();
        form.ScreenShell.OverlayHost.PerformLayout();
    }

    private static void RunSta(Action action, string threadName)
    {
        StaTestRunner.Run(() =>
        {
            List<Exception> uiFailures = [];
            ThreadExceptionEventHandler handler = (_, eventArgs) => uiFailures.Add(eventArgs.Exception);
            Application.ThreadException += handler;
            try
            {
                action();
                Assert.True(
                    uiFailures.Count == 0,
                    string.Join(Environment.NewLine, uiFailures.Select(failure => failure.ToString())));
            }
            finally
            {
                Application.ThreadException -= handler;
            }
        }, threadName);
    }

    private static IEnumerable<Control> DescendantsAndSelf(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (Control descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }
}
