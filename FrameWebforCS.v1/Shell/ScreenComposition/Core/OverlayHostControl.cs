namespace FrameWebforCS.Shell.ScreenComposition.Core;

public sealed class OverlayHostControl : UserControl
{
    private readonly Panel _surfaceHost = new()
    {
        BackColor = Color.White,
        Dock = DockStyle.Fill,
        Name = "OverlaySurfaceHost",
    };

    public OverlayHostControl()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(232, 238, 242);
        Dock = DockStyle.Fill;
        Name = "OverlayHost";
        Visible = false;
        Controls.Add(_surfaceHost);
    }

    public event EventHandler<ScreenCommandRequestedEventArgs>? CommandRequested;

    public Control? ActiveSurface { get; private set; }

    public ScreenOverlayKind ActiveOverlay { get; private set; }

    public int DisposedSurfaceCount { get; private set; }

    public void ApplyState(ScreenRouteState state, IFrameWebSurfaceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(factory);
        if (state.Overlay == ScreenOverlayKind.None)
        {
            Clear();
            return;
        }

        if (ActiveSurface is null || ActiveOverlay != state.Overlay)
        {
            ReplaceSurface(factory.CreateOverlaySurface(state), state.Overlay);
        }
        else
        {
            factory.ApplyState(ActiveSurface, state);
        }

        Visible = true;
        BringToFront();
    }

    public void Clear()
    {
        DisposeActiveSurface();
        ActiveOverlay = ScreenOverlayKind.None;
        Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeActiveSurface();
        }

        base.Dispose(disposing);
    }

    private void ReplaceSurface(Control surface, ScreenOverlayKind overlay)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(surface), "The overlay surface is already disposed.");
        }

        DisposeActiveSurface();
        ActiveSurface = surface;
        ActiveOverlay = overlay;
        surface.Dock = DockStyle.Fill;
        _surfaceHost.Controls.Add(surface);
        if (surface is IFrameWebSurface commandSurface)
        {
            commandSurface.CommandRequested += OnSurfaceCommandRequested;
        }
    }

    private void DisposeActiveSurface()
    {
        if (ActiveSurface is not Control surface)
        {
            return;
        }

        if (surface is IFrameWebSurface commandSurface)
        {
            commandSurface.CommandRequested -= OnSurfaceCommandRequested;
        }

        _surfaceHost.Controls.Remove(surface);
        surface.Dispose();
        DisposedSurfaceCount++;
        ActiveSurface = null;
    }

    private void OnSurfaceCommandRequested(object? sender, ScreenCommandRequestedEventArgs eventArgs) =>
        CommandRequested?.Invoke(sender ?? this, eventArgs);
}
