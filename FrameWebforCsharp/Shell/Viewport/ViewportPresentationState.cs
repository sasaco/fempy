using FrameWebforCsharp.Rendering.Scene;

namespace FrameWebforCsharp.Shell.Viewport;

public enum ViewportDisplayMode
{
    Model,
    Displacements,
    Loads,
    Reactions,
    SectionForces,
}

public sealed record ViewportPresentationState(
    ViewportDisplayMode DisplayMode,
    bool ShowGrid,
    bool ShowAxes,
    bool ShowLabels,
    bool ShowLegends,
    SceneExtremaMode ExtremaMode,
    float DisplacementScale)
{
    public static ViewportPresentationState Default { get; } = new(
        ViewportDisplayMode.Model,
        ShowGrid: true,
        ShowAxes: true,
        ShowLabels: true,
        ShowLegends: true,
        SceneExtremaMode.Values,
        DisplacementScale: 20.0f);

    public ViewportPresentationState Validate()
    {
        if (!Enum.IsDefined(DisplayMode))
        {
            throw new ArgumentOutOfRangeException(nameof(DisplayMode));
        }

        if (!Enum.IsDefined(ExtremaMode))
        {
            throw new ArgumentOutOfRangeException(nameof(ExtremaMode));
        }

        if (!float.IsFinite(DisplacementScale) || DisplacementScale <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(DisplacementScale));
        }

        return this;
    }

    public SceneLayerMask VisibleLayers => DisplayMode switch
    {
        ViewportDisplayMode.Model => SceneLayerMask.Geometry | SceneLayerMask.Decorations,
        ViewportDisplayMode.Loads => SceneLayerMask.Geometry | SceneLayerMask.Loads | SceneLayerMask.Decorations,
        ViewportDisplayMode.Displacements =>
            SceneLayerMask.Nodes | SceneLayerMask.Members | SceneLayerMask.Displacements | SceneLayerMask.Decorations,
        ViewportDisplayMode.Reactions =>
            SceneLayerMask.Nodes | SceneLayerMask.Members | SceneLayerMask.Supports |
            SceneLayerMask.Reactions | SceneLayerMask.Decorations,
        ViewportDisplayMode.SectionForces =>
            SceneLayerMask.Nodes | SceneLayerMask.Members | SceneLayerMask.SectionForces | SceneLayerMask.Decorations,
        _ => throw new ArgumentOutOfRangeException(nameof(DisplayMode)),
    };
}
