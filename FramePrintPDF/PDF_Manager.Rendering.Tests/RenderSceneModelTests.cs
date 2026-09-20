using PDF_Manager.Rendering;

namespace PDF_Manager.Rendering.Tests;

public sealed class RenderSceneModelTests
{
    private static readonly float[] TrianglePositions =
    [
        -0.5f, -0.5f,
         0.5f, -0.5f,
         0.0f,  0.5f,
    ];

    public static TheoryData<float[]> InvalidPositions => new()
    {
        Array.Empty<float>(),
        new float[5],
        new float[7],
        new[] { 0.0f, 0.0f, 1.0f, 0.0f, float.NaN, 1.0f },
        new[] { 0.0f, 0.0f, 1.0f, 0.0f, float.PositiveInfinity, 1.0f },
    };

    [Fact]
    public void Constructor_WithCompleteFiniteTriangle_CopiesInputAndReportsVertexCount()
    {
        float[] positions = TrianglePositions.ToArray();
        RenderSceneModel model = new("triangle-v1", positions);

        positions[0] = 99.0f;

        Assert.Equal("triangle-v1", model.StableId);
        Assert.Equal(3, model.VertexCount);
        Assert.Equal(TrianglePositions, model.Positions.ToArray());
    }

    [Fact]
    public void HasSameContent_RequiresMatchingStableIdAndCoordinates()
    {
        RenderSceneModel model = new("triangle-v1", TrianglePositions);
        RenderSceneModel same = new("triangle-v1", TrianglePositions.ToArray());
        RenderSceneModel differentId = new("triangle-v2", TrianglePositions);
        float[] changedPositions = TrianglePositions.ToArray();
        changedPositions[0] = -0.25f;
        RenderSceneModel differentCoordinates = new("triangle-v1", changedPositions);

        Assert.True(model.HasSameContent(same));
        Assert.False(model.HasSameContent(differentId));
        Assert.False(model.HasSameContent(differentCoordinates));
    }

    [Theory]
    [MemberData(nameof(InvalidPositions))]
    public void Constructor_WithIncompleteOrNonFinitePositions_ThrowsArgumentException(float[] positions)
    {
        Assert.Throws<ArgumentException>(() => new RenderSceneModel("invalid", positions));
    }
}
