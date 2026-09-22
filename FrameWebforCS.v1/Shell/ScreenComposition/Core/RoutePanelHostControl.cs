namespace FrameWebforCS.Shell.ScreenComposition.Core;

public sealed class RoutePanelHostControl : UserControl
{
    private bool _synchronizingSurfaceLayout;

    public RoutePanelHostControl()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.Transparent;
        Dock = DockStyle.Fill;
        Name = "RoutePanelHost";
        Visible = false;
    }

    public event EventHandler<ScreenCommandRequestedEventArgs>? CommandRequested;

    public event EventHandler? FloatingCardBoundsChanged;

    public Control? ActiveSurface { get; private set; }

    public ScreenRouteId? ActiveRoute { get; private set; }

    public int DisposedSurfaceCount { get; private set; }

    public void ApplyState(ScreenRouteState state, IFrameWebSurfaceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(factory);
        if (state.Route is not ScreenRouteId route)
        {
            Clear();
            return;
        }

        if (ActiveSurface is null || ActiveRoute != route)
        {
            ReplaceSurface(factory.CreateRouteSurface(state), route);
        }
        else
        {
            factory.ApplyState(ActiveSurface, state);
        }

        SynchronizeActiveSurfaceLayout();
        Visible = true;
        BringToFront();
        SynchronizeActiveSurfaceLayout();
    }

    public void SetContent(Control surface, ScreenRouteId route)
    {
        ArgumentNullException.ThrowIfNull(surface);
        AngularScreenManifest.GetRoute(route);
        ReplaceSurface(surface, route);
        SynchronizeActiveSurfaceLayout();
        Visible = true;
        SynchronizeActiveSurfaceLayout();
    }

    public void Clear()
    {
        DisposeActiveSurface();
        ActiveRoute = null;
        Region?.Dispose();
        Region = null;
        Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeActiveSurface();
            Region?.Dispose();
            Region = null;
        }

        base.Dispose(disposing);
    }

    protected override void OnClientSizeChanged(EventArgs eventArgs)
    {
        base.OnClientSizeChanged(eventArgs);
        SynchronizeActiveSurfaceLayout();
    }

    protected override void OnLayout(LayoutEventArgs eventArgs)
    {
        base.OnLayout(eventArgs);
        SynchronizeActiveSurfaceLayout();
    }

    protected override void OnParentChanged(EventArgs eventArgs)
    {
        base.OnParentChanged(eventArgs);
        SynchronizeActiveSurfaceLayout();
    }

    protected override void OnVisibleChanged(EventArgs eventArgs)
    {
        base.OnVisibleChanged(eventArgs);
        SynchronizeActiveSurfaceLayout();
    }

    private void ReplaceSurface(Control surface, ScreenRouteId route)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(surface), "The route surface is already disposed.");
        }

        if (surface is not IFloatingRouteSurface floating)
        {
            throw new ArgumentException(
                "Route surfaces must expose their interactive floating card.",
                nameof(surface));
        }

        DisposeActiveSurface();
        ActiveSurface = surface;
        ActiveRoute = route;
        surface.Dock = DockStyle.Fill;
        Controls.Add(surface);
        surface.Bounds = ClientRectangle;
        floating.FloatingCard.LocationChanged += OnFloatingCardBoundsChanged;
        floating.FloatingCard.SizeChanged += OnFloatingCardBoundsChanged;
        if (surface is IFrameWebSurface commandSurface)
        {
            commandSurface.CommandRequested += OnSurfaceCommandRequested;
        }

        SynchronizeActiveSurfaceLayout();
    }

    private void DisposeActiveSurface()
    {
        if (ActiveSurface is not Control surface)
        {
            return;
        }

        if (surface is IFloatingRouteSurface floating)
        {
            floating.FloatingCard.LocationChanged -= OnFloatingCardBoundsChanged;
            floating.FloatingCard.SizeChanged -= OnFloatingCardBoundsChanged;
        }

        if (surface is IFrameWebSurface commandSurface)
        {
            commandSurface.CommandRequested -= OnSurfaceCommandRequested;
        }

        Controls.Remove(surface);
        surface.Dispose();
        DisposedSurfaceCount++;
        ActiveSurface = null;
    }

    private void UpdateInteractiveRegion()
    {
        Region?.Dispose();
        Region = ActiveSurface is IFloatingRouteSurface floating
            ? new Region(floating.FloatingCard.Bounds)
            : null;
    }

    private void SynchronizeActiveSurfaceLayout()
    {
        if (_synchronizingSurfaceLayout || ActiveSurface is not Control surface || surface.IsDisposed)
        {
            return;
        }

        _synchronizingSurfaceLayout = true;
        try
        {
            if (surface.Bounds != ClientRectangle)
            {
                surface.Bounds = ClientRectangle;
            }

            surface.PerformLayout();
            UpdateInteractiveRegion();
        }
        finally
        {
            _synchronizingSurfaceLayout = false;
        }
    }

    private void OnFloatingCardBoundsChanged(object? sender, EventArgs eventArgs) =>
        NotifyFloatingCardBoundsChanged();

    private void NotifyFloatingCardBoundsChanged()
    {
        UpdateInteractiveRegion();
        FloatingCardBoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSurfaceCommandRequested(object? sender, ScreenCommandRequestedEventArgs eventArgs) =>
        CommandRequested?.Invoke(sender ?? this, eventArgs);
}
