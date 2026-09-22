namespace FrameWebforCS.Shell.ScreenComposition.Core;

public sealed class ScreenRouteStateChangedEventArgs(
    ScreenRouteState previous,
    ScreenRouteState current) : EventArgs
{
    public ScreenRouteState Previous { get; } = previous;

    public ScreenRouteState Current { get; } = current;
}

public sealed class ScreenRouteController
{
    public ScreenRouteController(ScreenRouteState? initialState = null)
    {
        State = initialState ?? ScreenRouteState.Default;
    }

    public event EventHandler<ScreenRouteStateChangedEventArgs>? StateChanged;

    public ScreenRouteState State { get; private set; }

    public void Navigate(ScreenRouteId route)
    {
        if (AngularScreenManifest.IsResultRoute(route) && !State.ResultsEnabled)
        {
            return;
        }

        SetState(new ScreenRouteState(
            route,
            AngularScreenManifest.GetContext(route),
            State.Dimension,
            ScreenPageState.Single,
            State.ResultsEnabled,
            ScreenOverlayKind.None));
    }

    public void CloseRoute()
    {
        if (State.Route is null)
        {
            return;
        }

        SetState(new ScreenRouteState(
            null,
            ScreenRouteContext.None,
            State.Dimension,
            ScreenPageState.Single,
            State.ResultsEnabled,
            State.Overlay));
    }

    public void SetResultsEnabled(bool enabled)
    {
        ScreenRouteId? route = State.Route;
        ScreenRouteContext context = State.Context;
        if (!enabled && route is ScreenRouteId current && AngularScreenManifest.IsResultRoute(current))
        {
            route = null;
            context = ScreenRouteContext.None;
        }

        SetState(new ScreenRouteState(
            route,
            context,
            State.Dimension,
            route == State.Route ? State.Page : ScreenPageState.Single,
            enabled,
            State.Overlay));
    }

    public void SetDimension(ViewDimension dimension)
    {
        ScreenRouteId? route = State.Route;
        ScreenRouteContext context = State.Context;
        ScreenPageState page = State.Page;
        if (dimension == ViewDimension.TwoDimensional && route == ScreenRouteId.InputPanel)
        {
            route = null;
            context = ScreenRouteContext.None;
            page = ScreenPageState.Single;
        }

        SetState(new ScreenRouteState(
            route,
            context,
            dimension,
            page,
            State.ResultsEnabled,
            State.Overlay));
    }

    public void SelectContext(ScreenRouteContext context)
    {
        if (State.Route is not ScreenRouteId current)
        {
            throw new InvalidOperationException("A contextual state requires an active route.");
        }

        Navigate(AngularScreenManifest.ResolveContextRoute(current, context));
    }

    public void SetPage(int index, int count) =>
        SetState(new ScreenRouteState(
            State.Route,
            State.Context,
            State.Dimension,
            new ScreenPageState(index, count),
            State.ResultsEnabled,
            State.Overlay));

    public void MovePage(int delta)
    {
        if (delta is not -1 and not 1)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "Page movement must be -1 or 1.");
        }

        int target = Math.Clamp(State.Page.Index + delta, 0, State.Page.Count - 1);
        if (target != State.Page.Index)
        {
            SetPage(target, State.Page.Count);
        }
    }

    public void ShowOverlay(ScreenOverlayKind overlay)
    {
        if (overlay == ScreenOverlayKind.None)
        {
            throw new ArgumentException("Use CloseOverlay to clear the current overlay.", nameof(overlay));
        }

        SetState(new ScreenRouteState(
            State.Route,
            State.Context,
            State.Dimension,
            State.Page,
            State.ResultsEnabled,
            overlay));
    }

    public void CloseOverlay()
    {
        if (State.Overlay == ScreenOverlayKind.None)
        {
            return;
        }

        SetState(new ScreenRouteState(
            State.Route,
            State.Context,
            State.Dimension,
            State.Page,
            State.ResultsEnabled,
            ScreenOverlayKind.None));
    }

    private void SetState(ScreenRouteState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (State == next)
        {
            return;
        }

        ScreenRouteState previous = State;
        State = next;
        StateChanged?.Invoke(this, new ScreenRouteStateChangedEventArgs(previous, next));
    }
}
