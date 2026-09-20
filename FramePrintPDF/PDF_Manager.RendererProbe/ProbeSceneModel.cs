using PDF_Manager.Rendering;
using PDF_Manager.Rendering.Scene;

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

    public static ViewportSceneModel TypedFrame { get; } = new(
        "typed-probe-frame-v1",
        [
            new SceneNode("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
            new SceneNode("N2", new ScenePoint3(2.0f, 0.0f, 1.0f)),
            new SceneNode("N3", new ScenePoint3(2.0f, 1.5f, 2.0f)),
        ],
        [
            new SceneMember("M1", "N1", "N2"),
            new SceneMember("M2", "N2", "N3"),
        ],
        [
            new SceneSupport("S1", "N1", true, true, true),
            new SceneSupport("S2", "N2", false, false, false, true, false, true),
        ],
        [
            new SceneNodalLoad("NL1", "N3", new ScenePoint3(0.0f, 0.0f, -10.0f)),
            new SceneNodalLoad(
                "NL2",
                "N2",
                new ScenePoint3(0.0f, 0.0f, 0.0f),
                new ScenePoint3(0.0f, 4.0f, 0.0f)),
        ],
        [new SceneMemberLoad("ML1", "M1", 0.5f, new ScenePoint3(0.0f, 0.0f, -5.0f))],
        new SceneDisplacementLayer(
            "typed-probe-static-displacement-v1",
            [
                new SceneNodeDisplacement("N1", new ScenePoint3(0.0f, 0.0f, 0.0f)),
                new SceneNodeDisplacement("N2", new ScenePoint3(0.0f, 0.0f, 0.15f)),
                new SceneNodeDisplacement("N3", new ScenePoint3(0.0f, 0.0f, 0.25f)),
            ],
            1.5f));
}
