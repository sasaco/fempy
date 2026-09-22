using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.ScreenComposition.Core;
using PDF_Manager.Shell.ScreenComposition.Surfaces;

namespace PDF_Manager.UiTests.UiParity;

public sealed class ScreenRouteStateTests
{
    [Fact]
    public void CatalogsExposeAllRoutesAndSourceNavigationOrder()
    {
        Assert.Equal(14, AngularScreenManifest.InputRoutes.Count);
        Assert.Equal(9, AngularScreenManifest.ResultRoutes.Count);
        Assert.Equal(14, AngularScreenManifest.PrimaryNavigation.Count);
        Assert.Equal(
            [
                PrimaryNavigationId.Calculate,
                PrimaryNavigationId.Elements,
                PrimaryNavigationId.Nodes,
                PrimaryNavigationId.Supports,
                PrimaryNavigationId.Members,
                PrimaryNavigationId.Panel,
                PrimaryNavigationId.Joints,
                PrimaryNavigationId.NoticePoints,
                PrimaryNavigationId.MemberSprings,
                PrimaryNavigationId.Loads,
                PrimaryNavigationId.Define,
                PrimaryNavigationId.Displacements,
                PrimaryNavigationId.Reactions,
                PrimaryNavigationId.SectionForces,
            ],
            AngularScreenManifest.PrimaryNavigation.Select(item => item.Id));
        Assert.Equal(
            AngularScreenManifest.InputRoutes.Select(item => item.Id),
            FrameWebSurfaceCatalog.InputRoutes.Select(item => item.Route));
        Assert.Equal(
            AngularScreenManifest.ResultRoutes.Select(item => item.Id),
            FrameWebSurfaceCatalog.ResultRoutes.Select(item => item.Route));
    }

    [Fact]
    public void DefaultStateShowsStartAndRejectsResultNavigationUntilCalculation()
    {
        ScreenRouteController controller = new();

        Assert.Null(controller.State.Route);
        Assert.Equal(ScreenOverlayKind.Start, controller.State.Overlay);
        Assert.Equal(ViewDimension.TwoDimensional, controller.State.Dimension);
        Assert.False(controller.State.ResultsEnabled);

        controller.Navigate(ScreenRouteId.ResultBasicDisplacements);
        Assert.Null(controller.State.Route);
        Assert.Equal(ScreenOverlayKind.Start, controller.State.Overlay);

        controller.SetResultsEnabled(true);
        controller.Navigate(ScreenRouteId.ResultBasicDisplacements);
        Assert.Equal(ScreenRouteId.ResultBasicDisplacements, controller.State.Route);
        Assert.Equal(ScreenRouteContext.BasicResult, controller.State.Context);
        Assert.Equal(ScreenOverlayKind.None, controller.State.Overlay);

        controller.SetResultsEnabled(false);
        Assert.Null(controller.State.Route);
        Assert.Equal(ScreenRouteContext.None, controller.State.Context);
    }

    [Fact]
    public void ContextDimensionPageAndOverlayTransitionsAreExplicit()
    {
        ScreenRouteController controller = new();
        List<ScreenRouteState> states = [];
        controller.StateChanged += (_, eventArgs) => states.Add(eventArgs.Current);

        controller.CloseOverlay();
        controller.Navigate(ScreenRouteId.InputMembers);
        controller.SelectContext(ScreenRouteContext.RigidZone);
        controller.SetPage(index: 1, count: 3);
        controller.MovePage(1);
        controller.ShowOverlay(ScreenOverlayKind.Preset);
        controller.ShowOverlay(ScreenOverlayKind.Print);
        controller.CloseOverlay();

        Assert.Equal(ScreenRouteId.InputRigidZone, controller.State.Route);
        Assert.Equal(ScreenRouteContext.RigidZone, controller.State.Context);
        Assert.Equal(new ScreenPageState(2, 3), controller.State.Page);
        Assert.Equal(ScreenOverlayKind.None, controller.State.Overlay);
        Assert.Contains(states, state => state.Overlay == ScreenOverlayKind.Preset);
        Assert.Contains(states, state => state.Overlay == ScreenOverlayKind.Print);

        controller.Navigate(ScreenRouteId.InputPanel);
        controller.SetDimension(ViewDimension.TwoDimensional);
        Assert.Null(controller.State.Route);
        Assert.Equal(ViewDimension.TwoDimensional, controller.State.Dimension);
    }

    [Fact]
    public void EveryOverlayKindHasAFactorySurfaceAndNoneFailsClosed()
    {
        StaTestRunner.Run(() =>
        {
            using var editor = Step5EditorMatrixTests.CreateEditor();
            using var document = new PDF_Manager.Shell.Contents.ProjectDocumentContent(
                MainForm.WorkspaceDocumentKey,
                new PDF_Manager.Resources.LocalizationService(PDF_Manager.Resources.UiLanguage.English));
            LocalizationService localization = new(UiLanguage.English);
            FrameWebSurfaceFactory factory = new(localization, editor, document);

            foreach (ScreenOverlayKind kind in Enum.GetValues<ScreenOverlayKind>().Where(kind => kind != ScreenOverlayKind.None))
            {
                ScreenRouteState state = new(
                    route: null,
                    ScreenRouteContext.None,
                    ViewDimension.ThreeDimensional,
                    ScreenPageState.Single,
                    resultsEnabled: false,
                    kind);
                using Control surface = factory.CreateOverlaySurface(state);
                Assert.IsAssignableFrom<IFrameWebSurface>(surface);
            }

            Assert.Throws<ArgumentException>(() => factory.CreateOverlaySurface(new ScreenRouteState(
                route: null,
                ScreenRouteContext.None,
                ViewDimension.ThreeDimensional,
                ScreenPageState.Single,
                resultsEnabled: false,
                ScreenOverlayKind.None)));
        }, "FrameWeb overlay factory exhaustiveness");
    }
}
