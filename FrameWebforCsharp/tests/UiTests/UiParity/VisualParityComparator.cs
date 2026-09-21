using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PDF_Manager.UiTests.UiParity;

internal static class VisualParityComparator
{
    public const double MinimumUnmaskedPixelRatio = 0.10;

    public static VisualParityDifference Compare(
        string expectedPath,
        string actualPath,
        IReadOnlyList<Rectangle> regions,
        int maxChannelDelta,
        int edgeMaskRadius,
        double minimumUnmaskedPixelRatio = MinimumUnmaskedPixelRatio)
    {
        using Bitmap expected = new(expectedPath);
        using Bitmap actual = new(actualPath);
        return Compare(expected, actual, regions, maxChannelDelta, edgeMaskRadius, minimumUnmaskedPixelRatio);
    }

    public static VisualParityDifference Compare(
        Bitmap expected,
        Bitmap actual,
        IReadOnlyList<Rectangle> regions,
        int maxChannelDelta,
        int edgeMaskRadius,
        double minimumUnmaskedPixelRatio = MinimumUnmaskedPixelRatio)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(regions);
        if (expected.Size != actual.Size)
        {
            throw new InvalidDataException($"Capture sizes differ: expected={expected.Size}, actual={actual.Size}.");
        }

        if (maxChannelDelta < 0 || maxChannelDelta > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChannelDelta));
        }

        if (edgeMaskRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeMaskRadius));
        }

        if (!double.IsFinite(minimumUnmaskedPixelRatio) ||
            minimumUnmaskedPixelRatio <= 0 ||
            minimumUnmaskedPixelRatio > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumUnmaskedPixelRatio));
        }

        Rectangle imageBounds = new(Point.Empty, expected.Size);
        if (regions.Count == 0)
        {
            throw new InvalidDataException("At least one non-empty comparison region is required.");
        }

        for (int index = 0; index < regions.Count; index++)
        {
            Rectangle region = regions[index];
            if (region.Width <= 0 || region.Height <= 0 || Rectangle.Intersect(region, imageBounds) != region)
            {
                throw new InvalidDataException($"Comparison region {index} is empty or outside the image: {region}.");
            }

            for (int previous = 0; previous < index; previous++)
            {
                Rectangle intersection = Rectangle.Intersect(region, regions[previous]);
                if (intersection.Width > 0 && intersection.Height > 0)
                {
                    throw new InvalidDataException(
                        $"Comparison regions {previous} and {index} overlap; pixels may not be double-counted.");
                }
            }
        }

        byte[] expectedBytes = CopyPixels(expected);
        byte[] actualBytes = CopyPixels(actual);
        // Both renderers contribute antialiasing edges. The minimum-unmasked invariant below prevents
        // arbitrary candidate noise from turning the entire comparison into an empty/NaN pass.
        bool[] edgeMask = CreateEdgeMask(
            expectedBytes,
            actualBytes,
            expected.Width,
            expected.Height,
            maxChannelDelta,
            edgeMaskRadius);

        long absoluteChannelError = 0;
        int overThresholdPixels = 0;
        int comparedPixels = 0;
        int edgeMaskedPixels = 0;
        int totalRegionPixels = 0;
        foreach (Rectangle region in regions)
        {
            int regionPixels = checked(region.Width * region.Height);
            int regionComparedPixels = 0;
            totalRegionPixels = checked(totalRegionPixels + regionPixels);
            for (int y = region.Top; y < region.Bottom; y++)
            {
                for (int x = region.Left; x < region.Right; x++)
                {
                    int pixel = checked((y * expected.Width) + x);
                    if (edgeMask[pixel])
                    {
                        edgeMaskedPixels++;
                        continue;
                    }

                    int offset = checked(pixel * 4);
                    bool overThreshold = false;
                    for (int channel = 0; channel < 4; channel++)
                    {
                        int difference = Math.Abs(expectedBytes[offset + channel] - actualBytes[offset + channel]);
                        absoluteChannelError += difference;
                        overThreshold |= difference > maxChannelDelta;
                    }

                    if (overThreshold) overThresholdPixels++;
                    comparedPixels++;
                    regionComparedPixels++;
                }
            }

            RequireSufficientUnmaskedPixels(
                regionComparedPixels,
                regionPixels,
                minimumUnmaskedPixelRatio,
                $"region {region}");
        }

        RequireSufficientUnmaskedPixels(
            comparedPixels,
            totalRegionPixels,
            minimumUnmaskedPixelRatio,
            "all regions");

        double meanAbsoluteChannelError = absoluteChannelError / (comparedPixels * 4d);
        double overThresholdPixelRatio = overThresholdPixels / (double)comparedPixels;
        if (!double.IsFinite(meanAbsoluteChannelError) || !double.IsFinite(overThresholdPixelRatio))
        {
            throw new InvalidDataException("Visual parity metrics must be finite.");
        }

        return new VisualParityDifference(
            meanAbsoluteChannelError,
            overThresholdPixelRatio,
            edgeMaskedPixels,
            comparedPixels,
            totalRegionPixels);
    }

    public static void RequireWithinThreshold(VisualParityDifference difference, double maximumOverThresholdPixelRatio)
    {
        if (!double.IsFinite(maximumOverThresholdPixelRatio) ||
            maximumOverThresholdPixelRatio < 0 ||
            maximumOverThresholdPixelRatio > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOverThresholdPixelRatio));
        }

        if (!double.IsFinite(difference.MeanAbsoluteChannelError) ||
            !double.IsFinite(difference.OverThresholdPixelRatio) ||
            difference.ComparedPixels <= 0 ||
            difference.TotalRegionPixels <= 0)
        {
            throw new InvalidDataException("Visual parity result is empty or non-finite.");
        }

        if (difference.OverThresholdPixelRatio > maximumOverThresholdPixelRatio)
        {
            throw new InvalidDataException(
                $"Over-threshold pixel ratio {difference.OverThresholdPixelRatio:F6} exceeds " +
                $"{maximumOverThresholdPixelRatio:F6}.");
        }
    }

    private static void RequireSufficientUnmaskedPixels(
        int comparedPixels,
        int totalPixels,
        double minimumRatio,
        string scope)
    {
        if (totalPixels <= 0 || comparedPixels <= 0)
        {
            throw new InvalidDataException($"Visual comparison has no unmasked pixels in {scope}.");
        }

        double ratio = comparedPixels / (double)totalPixels;
        if (!double.IsFinite(ratio) || ratio < minimumRatio)
        {
            throw new InvalidDataException(
                $"Visual comparison retains only {ratio:P3} unmasked pixels in {scope}; " +
                $"at least {minimumRatio:P3} is required.");
        }
    }

    private static bool[] CreateEdgeMask(
        byte[] expected,
        byte[] actual,
        int width,
        int height,
        int threshold,
        int radius)
    {
        bool[] referenceEdges = new bool[checked(width * height)];
        bool[] candidateEdges = new bool[referenceEdges.Length];
        MarkEdges(expected, referenceEdges, width, height, threshold);
        MarkEdges(actual, candidateEdges, width, height, threshold);
        bool[] edges = (bool[])referenceEdges.Clone();
        for (int pixel = 0; pixel < edges.Length; pixel++)
        {
            edges[pixel] |= candidateEdges[pixel];
        }

        bool[] dilated = (bool[])edges.Clone();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!edges[(y * width) + x]) continue;
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    int targetY = y + offsetY;
                    if (targetY < 0 || targetY >= height) continue;
                    for (int offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        int targetX = x + offsetX;
                        if (targetX >= 0 && targetX < width)
                        {
                            dilated[(targetY * width) + targetX] = true;
                        }
                    }
                }
            }
        }

        return dilated;
    }

    private static void MarkEdges(byte[] pixels, bool[] edges, int width, int height, int threshold)
    {
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int pixel = (y * width) + x;
                int right = pixel + 1;
                int below = pixel + width;
                if (ChannelsDiffer(pixels, pixel, right, threshold) ||
                    ChannelsDiffer(pixels, pixel, below, threshold))
                {
                    edges[pixel] = true;
                    edges[right] = true;
                    edges[below] = true;
                }
            }
        }
    }

    private static bool ChannelsDiffer(byte[] pixels, int leftPixel, int rightPixel, int threshold)
    {
        int left = checked(leftPixel * 4);
        int right = checked(rightPixel * 4);
        for (int channel = 0; channel < 4; channel++)
        {
            if (Math.Abs(pixels[left + channel] - pixels[right + channel]) > threshold) return true;
        }

        return false;
    }

    private static byte[] CopyPixels(Bitmap source)
    {
        using Bitmap normalized = source.Clone(
            new Rectangle(Point.Empty, source.Size),
            PixelFormat.Format32bppArgb);
        BitmapData data = normalized.LockBits(
            new Rectangle(Point.Empty, normalized.Size),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = checked(normalized.Width * 4);
            byte[] contiguous = new byte[checked(rowBytes * normalized.Height)];
            for (int y = 0; y < normalized.Height; y++)
            {
                IntPtr row = data.Stride >= 0
                    ? IntPtr.Add(data.Scan0, checked(y * data.Stride))
                    : IntPtr.Add(data.Scan0, checked((normalized.Height - 1 - y) * -data.Stride));
                Marshal.Copy(row, contiguous, checked(y * rowBytes), rowBytes);
            }

            return contiguous;
        }
        finally
        {
            normalized.UnlockBits(data);
        }
    }
}

internal sealed record VisualParityDifference(
    double MeanAbsoluteChannelError,
    double OverThresholdPixelRatio,
    int EdgeMaskedPixels,
    int ComparedPixels,
    int TotalRegionPixels);
