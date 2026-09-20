using PDF_Manager.Rendering;

namespace PDF_Manager.RendererProbe;

internal static class ProbeSceneModel
{
    private static readonly float[] KnownTrianglePositions =
    [
        -0.80f, -0.75f,
         0.80f, -0.75f,
         0.00f,  0.80f,
    ];

    public static RenderSceneModel KnownFrame { get; } = new("known-frame-v1", KnownTrianglePositions);
}
