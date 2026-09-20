namespace PDF_Manager.RendererProbe;

internal sealed class ProbeSceneModel
{
    private static readonly float[] KnownTrianglePositions =
    [
        -0.80f, -0.75f,
         0.80f, -0.75f,
         0.00f,  0.80f,
    ];

    public static ProbeSceneModel KnownFrame { get; } = new("known-frame-v1", KnownTrianglePositions);

    private readonly float[] _positions;

    public ProbeSceneModel(string stableId, ReadOnlySpan<float> positions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        if (positions.Length < 6 || positions.Length % 2 != 0)
        {
            throw new ArgumentException("Positions must contain at least three two-dimensional vertices.", nameof(positions));
        }

        StableId = stableId;
        _positions = positions.ToArray();
    }

    public string StableId { get; }

    public int VertexCount => _positions.Length / 2;

    public ReadOnlySpan<float> Positions => _positions;

    public bool HasSameContent(ProbeSceneModel other) =>
        string.Equals(StableId, other.StableId, StringComparison.Ordinal) &&
        Positions.SequenceEqual(other.Positions);
}
