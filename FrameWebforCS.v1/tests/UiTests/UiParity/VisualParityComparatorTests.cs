namespace PDF_Manager.UiTests.UiParity;

public sealed class VisualParityComparatorTests
{
    private static readonly Rectangle FullImage = new(0, 0, 32, 32);

    [Fact]
    public void IdenticalSolidImages_PassWithFiniteNonEmptyMetrics()
    {
        using Bitmap expected = Solid(Color.FromArgb(255, 30, 37, 44));
        using Bitmap actual = Solid(Color.FromArgb(255, 30, 37, 44));

        VisualParityDifference result = VisualParityComparator.Compare(expected, actual, [FullImage], 8, 1);
        VisualParityComparator.RequireWithinThreshold(result, 0.005);

        Assert.Equal(0, result.OverThresholdPixelRatio);
        Assert.Equal(32 * 32, result.ComparedPixels);
        Assert.True(double.IsFinite(result.MeanAbsoluteChannelError));
    }

    [Fact]
    public void CandidateCheckerboardNoise_CannotCreateAnEmptyPassingComparison()
    {
        using Bitmap expected = Solid(Color.FromArgb(255, 30, 37, 44));
        using Bitmap actual = Solid(Color.FromArgb(255, 30, 37, 44));
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                actual.SetPixel(x, y, (x + y) % 2 == 0 ? Color.Black : Color.White);
            }
        }

        Assert.Throws<InvalidDataException>(() =>
            VisualParityComparator.Compare(expected, actual, [FullImage], 8, 1));
    }

    [Fact]
    public void ReferenceWithNoUnmaskedPixels_FailsClosed()
    {
        using Bitmap expected = Solid(Color.Black);
        using Bitmap actual = Solid(Color.Black);
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Color value = (x + y) % 2 == 0 ? Color.Black : Color.White;
                expected.SetPixel(x, y, value);
                actual.SetPixel(x, y, value);
            }
        }

        Assert.Throws<InvalidDataException>(() =>
            VisualParityComparator.Compare(expected, actual, [FullImage], 8, 1));
    }

    [Fact]
    public void EmptyOrTooSmallComparisonRegions_FailClosed()
    {
        using Bitmap expected = Solid(Color.Black);
        using Bitmap actual = Solid(Color.Black);

        Assert.Throws<InvalidDataException>(() =>
            VisualParityComparator.Compare(expected, actual, [], 8, 1));
        Assert.Throws<InvalidDataException>(() =>
            VisualParityComparator.Compare(expected, actual, [Rectangle.Empty], 8, 1));
        Assert.Throws<InvalidDataException>(() =>
            VisualParityComparator.Compare(expected, actual, [new Rectangle(31, 31, 2, 2)], 8, 1));
    }

    [Fact]
    public void NonFinitePolicyValues_FailClosed()
    {
        using Bitmap expected = Solid(Color.Black);
        using Bitmap actual = Solid(Color.Black);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VisualParityComparator.Compare(expected, actual, [FullImage], 8, 1, double.NaN));
        VisualParityDifference result = VisualParityComparator.Compare(expected, actual, [FullImage], 8, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VisualParityComparator.RequireWithinThreshold(result, double.PositiveInfinity));
        Assert.Throws<InvalidDataException>(() =>
            VisualParityComparator.RequireWithinThreshold(
                result with { OverThresholdPixelRatio = double.NaN },
                0.005));
    }

    [Fact]
    public void LargeGeometryShift_FailsThreshold()
    {
        using Bitmap expected = Solid(Color.White);
        using Bitmap actual = Solid(Color.White);
        using (Graphics expectedGraphics = Graphics.FromImage(expected))
        using (Graphics actualGraphics = Graphics.FromImage(actual))
        {
            expectedGraphics.FillRectangle(Brushes.Black, new Rectangle(3, 3, 10, 20));
            actualGraphics.FillRectangle(Brushes.Black, new Rectangle(18, 3, 10, 20));
        }

        VisualParityDifference result = VisualParityComparator.Compare(expected, actual, [FullImage], 8, 1);
        Assert.Throws<InvalidDataException>(() => VisualParityComparator.RequireWithinThreshold(result, 0.005));
    }

    [Fact]
    public void SolidReplacement_FailsThreshold()
    {
        using Bitmap expected = Solid(Color.FromArgb(255, 30, 37, 44));
        using Bitmap actual = Solid(Color.FromArgb(255, 86, 88, 92));

        VisualParityDifference result = VisualParityComparator.Compare(expected, actual, [FullImage], 8, 1);
        Assert.Throws<InvalidDataException>(() => VisualParityComparator.RequireWithinThreshold(result, 0.005));
    }

    [Fact]
    public void OneUnitOverPerChannelThreshold_FailsAtApprovedRatio()
    {
        using Bitmap expected = Solid(Color.FromArgb(255, 30, 37, 44), 10, 10);
        using Bitmap actual = Solid(Color.FromArgb(255, 39, 37, 44), 10, 10);

        VisualParityDifference result = VisualParityComparator.Compare(
            expected,
            actual,
            [new Rectangle(0, 0, 10, 10)],
            8,
            1);

        Assert.Equal(1, result.OverThresholdPixelRatio);
        Assert.Throws<InvalidDataException>(() => VisualParityComparator.RequireWithinThreshold(result, 0.005));
    }

    private static Bitmap Solid(Color color, int width = 32, int height = 32)
    {
        Bitmap bitmap = new(width, height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }
}
