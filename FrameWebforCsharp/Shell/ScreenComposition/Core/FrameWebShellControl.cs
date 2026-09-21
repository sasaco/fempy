using PDF_Manager.Resources;

namespace PDF_Manager.Shell.ScreenComposition.Core;

public sealed class FrameWebShellControl : UserControl
{
    private readonly IFrameWebSurfaceFactory _surfaceFactory;
    private readonly Panel _body = new()
    {
        BackColor = ScreenCompositionStyle.WorkspaceBackColor,
        Dock = DockStyle.Fill,
        Name = "FrameWebBody",
    };
    private readonly Panel _baseLayer = new()
    {
        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
        BackColor = ScreenCompositionStyle.WorkspaceBackColor,
        Dock = DockStyle.None,
        Name = "WorkspaceLayer",
    };
    private bool _disposed;

    public FrameWebShellControl(
        LocalizationService localization,
        WorkspaceControl workspace,
        IFrameWebSurfaceFactory surfaceFactory,
        ScreenRouteController? routeController = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
        RouteController = routeController ?? new ScreenRouteController();

        AutoScaleMode = AutoScaleMode.Dpi;
        Dock = DockStyle.Fill;
        Name = "FrameWebShell";
        HeaderBar = new HeaderMenuControl(localization);
        OptionalHeader = new OptionalHeaderControl(RouteController, localization);
        PrimaryNavigation = new PrimaryNavigationControl(localization);
        RoutePanelHost = new RoutePanelHostControl();
        OverlayHost = new OverlayHostControl();

        _baseLayer.Controls.Add(Workspace);
        _baseLayer.Controls.Add(PrimaryNavigation);
        _body.Controls.Add(_baseLayer);
        _body.Controls.Add(RoutePanelHost);
        Controls.Add(_body);
        Controls.Add(OptionalHeader);
        Controls.Add(HeaderBar);
        OverlayHost.Dock = DockStyle.None;
        OverlayHost.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        OverlayHost.Bounds = ClientRectangle;
        Controls.Add(OverlayHost);

        RouteController.StateChanged += OnRouteStateChanged;
        HeaderBar.CommandRequested += OnCommandRequested;
        HeaderBar.LanguageRequested += OnLanguageRequested;
        OptionalHeader.ControlPanelRequested += OnControlPanelRequested;
        PrimaryNavigation.NavigationRequested += OnNavigationRequested;
        RoutePanelHost.CommandRequested += OnCommandRequested;
        RoutePanelHost.FloatingCardBoundsChanged += OnFloatingCardBoundsChanged;
        OverlayHost.CommandRequested += OnCommandRequested;
        SizeChanged += OnShellSizeChanged;
        ApplyState(RouteController.State);
        ArrangeLayerOrder();
    }

    public event EventHandler<ScreenCommandRequestedEventArgs>? CommandRequested;

    public event EventHandler<UiLanguageRequestedEventArgs>? LanguageRequested;

    public HeaderMenuControl HeaderBar { get; }

    public OptionalHeaderControl OptionalHeader { get; }

    public PrimaryNavigationControl PrimaryNavigation { get; }

    public WorkspaceControl Workspace { get; }

    public RoutePanelHostControl RoutePanelHost { get; }

    public OverlayHostControl OverlayHost { get; }

    public ScreenRouteController RouteController { get; }

    public ScreenRouteState State => RouteController.State;

    public int ViewportOwnerCount => Workspace.VisibleViewportCount;

    public void ApplyCommandState(bool canCreate, bool canOpen, bool canSave, bool canPrint) =>
        HeaderBar.ApplyCommandState(canCreate, canOpen, canSave, canPrint);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            RouteController.StateChanged -= OnRouteStateChanged;
            HeaderBar.CommandRequested -= OnCommandRequested;
            HeaderBar.LanguageRequested -= OnLanguageRequested;
            OptionalHeader.ControlPanelRequested -= OnControlPanelRequested;
            PrimaryNavigation.NavigationRequested -= OnNavigationRequested;
            RoutePanelHost.CommandRequested -= OnCommandRequested;
            RoutePanelHost.FloatingCardBoundsChanged -= OnFloatingCardBoundsChanged;
            OverlayHost.CommandRequested -= OnCommandRequested;
            SizeChanged -= OnShellSizeChanged;
        }

        base.Dispose(disposing);
    }

    private void ApplyState(ScreenRouteState state)
    {
        PrimaryNavigation.ApplyState(state);
        OptionalHeader.ApplyState(state);
        RoutePanelHost.ApplyState(state, _surfaceFactory);
        ApplyControlPanelState(OptionalHeader.IsControlPanelOpen);
        OverlayHost.ApplyState(state, _surfaceFactory);
        UpdateWorkspaceReservation();
        ArrangeLayerOrder();
    }

    private void ArrangeLayerOrder()
    {
        _body.Controls.SetChildIndex(RoutePanelHost, 0);
        _body.Controls.SetChildIndex(_baseLayer, 1);
        Controls.SetChildIndex(OverlayHost, 0);
    }

    private void UpdateWorkspaceReservation()
    {
        int right = RoutePanelHost.ActiveSurface is IFloatingRouteSurface floating
            ? Math.Max(0, _body.ClientSize.Width - floating.FloatingCard.Left)
            : 0;
        _baseLayer.Bounds = new Rectangle(
            0,
            0,
            Math.Max(0, _body.ClientSize.Width - right),
            _body.ClientSize.Height);
    }

    private void OnFloatingCardBoundsChanged(object? sender, EventArgs eventArgs) =>
        UpdateWorkspaceReservation();

    private void OnShellSizeChanged(object? sender, EventArgs eventArgs)
    {
        OverlayHost.Bounds = ClientRectangle;
        UpdateWorkspaceReservation();
    }

    private void OnRouteStateChanged(object? sender, ScreenRouteStateChangedEventArgs eventArgs) =>
        ApplyState(eventArgs.Current);

    private void OnNavigationRequested(object? sender, NavigationRequestedEventArgs eventArgs)
    {
        PrimaryNavigationDefinition definition = AngularScreenManifest.PrimaryNavigation
            .Single(item => item.Id == eventArgs.Navigation);
        if (definition.Id == PrimaryNavigationId.Calculate)
        {
            CommandRequested?.Invoke(this, new ScreenCommandRequestedEventArgs(ScreenCommandKind.RunAnalysis));
        }
        else if (definition.DefaultRoute is ScreenRouteId route)
        {
            RouteController.Navigate(route);
        }
    }

    private void OnCommandRequested(object? sender, ScreenCommandRequestedEventArgs eventArgs) =>
        CommandRequested?.Invoke(sender ?? this, eventArgs);

    private void OnLanguageRequested(object? sender, UiLanguageRequestedEventArgs eventArgs) =>
        LanguageRequested?.Invoke(sender ?? this, eventArgs);

    private void OnControlPanelRequested(object? sender, ControlPanelRequestedEventArgs eventArgs) =>
        ApplyControlPanelState(eventArgs.IsOpen);

    private void ApplyControlPanelState(bool isOpen)
    {
        if (RoutePanelHost.ActiveSurface is IRouteControlPanelSurface controlPanelSurface)
        {
            controlPanelSurface.SetControlPanelOpen(isOpen);
        }
    }
}
