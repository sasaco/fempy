namespace FrameWebforCsharp.Rendering;

public sealed class RenderSceneModel
{
    private const int CoordinatesPerVertex = 2;
    private const int VerticesPerTriangle = 3;
    private readonly float[] _positions;

    public RenderSceneModel(string stableId, ReadOnlySpan<float> positions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ValidatePositions(positions);

        StableId = stableId;
        _positions = positions.ToArray();
    }

    public string StableId { get; }

    public int VertexCount => _positions.Length / CoordinatesPerVertex;

    public ReadOnlyMemory<float> Positions => _positions;

    public bool HasSameContent(RenderSceneModel other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(StableId, other.StableId, StringComparison.Ordinal) &&
            Positions.Span.SequenceEqual(other.Positions.Span);
    }

    private static void ValidatePositions(ReadOnlySpan<float> positions)
    {
        int coordinatesPerTriangle = CoordinatesPerVertex * VerticesPerTriangle;
        if (positions.Length < coordinatesPerTriangle || positions.Length % coordinatesPerTriangle != 0)
        {
            throw new ArgumentException(
                "Positions must contain one or more complete two-dimensional triangles.",
                nameof(positions));
        }

        foreach (float coordinate in positions)
        {
            if (!float.IsFinite(coordinate))
            {
                throw new ArgumentException("Positions must contain only finite coordinates.", nameof(positions));
            }
        }
    }
}
