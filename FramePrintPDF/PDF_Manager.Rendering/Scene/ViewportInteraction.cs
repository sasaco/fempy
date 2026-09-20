namespace PDF_Manager.Rendering.Scene;

public enum ViewportProjection
{
    Orthographic,
    Perspective,
}

public enum ViewportSelectionOrigin
{
    Programmatic,
    Table,
    Viewport,
}

public sealed class ViewportSelectionChangedEventArgs(
    SceneEntityKey? selection,
    ViewportSelectionOrigin origin) : EventArgs
{
    public SceneEntityKey? Selection { get; } = selection;

    public ViewportSelectionOrigin Origin { get; } = origin;
}

public readonly record struct ViewportCameraState(
    ViewportProjection Projection,
    ScenePoint3 Target,
    float Distance,
    float OrthographicHeight);
