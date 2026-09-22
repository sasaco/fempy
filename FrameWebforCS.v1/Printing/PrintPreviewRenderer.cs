using System.Text;

namespace FrameWebforCS.Printing;

internal readonly record struct PrintPreviewRasterSize(int Width, int Height, int DecodedBytes);

internal static class PrintPageRasterizer
{
    private static readonly RgbColor Ink = new(24, 31, 42);
    private static readonly RgbColor Grid = new(65, 75, 90);
    private static readonly RgbColor HeaderFill = new(225, 229, 235);
    private static readonly RgbColor Paper = new(255, 255, 255);

    public static PrintPreviewRasterSize GetRasterSize(
        PrintDocumentPlan plan,
        PrintPreviewOptions options)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);
        double pointToPixel = Math.Min(
            options.MaximumDimension / plan.PageWidthPoints,
            options.MaximumDimension / plan.PageHeightPoints);
        int width = Math.Max(1, (int)Math.Round(plan.PageWidthPoints * pointToPixel));
        int height = Math.Max(1, (int)Math.Round(plan.PageHeightPoints * pointToPixel));
        int byteLength = PrintPageCapture.GetRequiredByteLength(width, height);
        return new PrintPreviewRasterSize(width, height, byteLength);
    }

    public static void ReserveContentBudget(
        PrintPagePlan page,
        PrintWorkBudget budget)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(budget);
        foreach (PrintPreviewTextRun run in page.TextRuns)
        {
            budget.AddText(run.Text);
        }

        budget.AddLayoutWork(page.RenderContent.RenderingWork);
    }

    public static PrintPreviewRasterSize SelectLargestRasterSize(
        PrintDocumentPlan plan,
        PrintPreviewOptions options,
        int pageCount,
        PrintWorkBudget budget)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(budget);
        if (pageCount <= 0 || pageCount > plan.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageCount));
        }

        if (budget.RemainingImages < pageCount)
        {
            // Use the checked budget path so callers receive the standard precise limit exception.
            for (int index = 0; index < pageCount; index++)
            {
                budget.AddPreviewImageBytes(0);
            }
        }

        long decodedBytesPerPage = budget.RemainingDecodedImageBytes / pageCount;
        long pixelsPerPage = budget.RemainingLayoutWork / pageCount;
        int low = 1;
        int high = options.MaximumDimension;
        PrintPreviewRasterSize minimum = GetRasterSize(plan, new PrintPreviewOptions(low));
        if (minimum.DecodedBytes > decodedBytesPerPage ||
            checked((long)minimum.Width * minimum.Height) > pixelsPerPage)
        {
            // Reserve the minimum through checked counters to report which aggregate is exhausted.
            ReserveRasterBudget(minimum, pageCount, budget);
            throw new InvalidOperationException("The minimum preview size unexpectedly fit the shared budget.");
        }

        PrintPreviewRasterSize selected = minimum;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            PrintPreviewRasterSize candidate = GetRasterSize(plan, new PrintPreviewOptions(middle));
            long candidatePixels = checked((long)candidate.Width * candidate.Height);
            if (candidate.DecodedBytes <= decodedBytesPerPage && candidatePixels <= pixelsPerPage)
            {
                selected = candidate;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return selected;
    }

    public static void ReserveRasterBudget(
        PrintPreviewRasterSize size,
        int pageCount,
        PrintWorkBudget budget)
    {
        for (int index = 0; index < pageCount; index++)
        {
            budget.AddPreviewImageBytes(size.DecodedBytes);
            budget.AddLayoutWork(checked((long)size.Width * size.Height));
        }
    }

    public static PrintPageCapture Render(
        PrintDocumentPlan plan,
        PrintPagePlan page,
        PrintPreviewRasterSize size,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(page);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] pixels = new byte[size.DecodedBytes];
        Array.Fill(pixels, Paper.Red);
        double pointToPixel = Math.Min(
            size.Width / Math.Max(1.0, plan.PageWidthPoints),
            size.Height / Math.Max(1.0, plan.PageHeightPoints));
        RasterCanvas canvas = new(pixels, size.Width, size.Height, pointToPixel);
        foreach (PrintRenderRectangle rectangle in page.RenderContent.Rectangles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rectangle.Style == PrintRenderRectangleStyle.Header)
            {
                canvas.FillRectangle(rectangle.Bounds, HeaderFill);
            }

            canvas.DrawRectangle(rectangle.Bounds, Grid);
        }

        foreach (PrintRenderImage image in page.RenderContent.Images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            canvas.DrawImage(image.Capture, image.Bounds, cancellationToken);
        }

        foreach (PrintPreviewTextRun run in page.TextRuns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            canvas.DrawText(run, cancellationToken);
        }

        return PrintPageCapture.FromOwnedRgb24(size.Width, size.Height, pixels);
    }

    private sealed class RasterCanvas
    {
        private readonly byte[] _pixels;
        private readonly int _width;
        private readonly int _height;
        private readonly double _scale;

        public RasterCanvas(byte[] pixels, int width, int height, double scale)
        {
            _pixels = pixels;
            _width = width;
            _height = height;
            _scale = scale;
        }

        private PixelClip? _activeClip;

        public void DrawText(PrintPreviewTextRun run, CancellationToken cancellationToken)
        {
            string value = run.DisplayText;
            PrintRectangle bounds = run.Bounds;
            if (string.IsNullOrEmpty(value) || bounds.WidthPoints <= 0 || bounds.HeightPoints <= 0)
            {
                return;
            }

            Rune[] glyphs = value.EnumerateRunes().ToArray();
            int glyphHeight = Math.Max(1, ToPixels(Math.Min(run.FontSizePoints, bounds.HeightPoints) * 0.72));
            int textWidth = Math.Min(ToPixels(bounds.WidthPoints), ToPixels(run.DisplayWidthPoints));
            int spacing = Math.Max(1, glyphHeight / 10);
            int advance = glyphs.Length == 0 ? 0 : Math.Max(1, textWidth / glyphs.Length);
            int glyphWidth = Math.Max(1, advance - spacing);
            int left = ToX(bounds.XPoints);
            int availableWidth = ToPixels(bounds.WidthPoints);
            int x = run.Alignment switch
            {
                PrintCellAlignment.Left => left,
                PrintCellAlignment.Center => left + Math.Max(0, (availableWidth - textWidth) / 2),
                PrintCellAlignment.Right => left + Math.Max(0, availableWidth - textWidth),
                _ => throw new ArgumentOutOfRangeException(nameof(run)),
            };
            int top = ToY(bounds.YPoints) + Math.Max(0, (ToPixels(bounds.HeightPoints) - glyphHeight) / 2);
            _activeClip = new PixelClip(
                left,
                ToY(bounds.YPoints),
                left + availableWidth,
                ToY(bounds.YPoints) + ToPixels(bounds.HeightPoints));
            try
            {
                for (int index = 0; index < glyphs.Length; index++)
                {
                    if ((index & 63) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    Rune glyph = glyphs[index];
                    if (!Rune.IsWhiteSpace(glyph))
                    {
                        DrawGlyph(x, top, glyphWidth, glyphHeight, glyph.Value, run.Bold);
                    }

                    x += advance;
                }
            }
            finally
            {
                _activeClip = null;
            }
        }

        public void DrawRectangle(PrintRectangle bounds, RgbColor color)
        {
            int left = ToX(bounds.XPoints);
            int top = ToY(bounds.YPoints);
            int right = ToX(bounds.XPoints + bounds.WidthPoints);
            int bottom = ToY(bounds.YPoints + bounds.HeightPoints);
            DrawLine(left, top, right, top, color);
            DrawLine(right, top, right, bottom, color);
            DrawLine(right, bottom, left, bottom, color);
            DrawLine(left, bottom, left, top, color);
        }

        public void FillRectangle(PrintRectangle bounds, RgbColor color)
        {
            int left = Math.Clamp(ToX(bounds.XPoints), 0, _width);
            int top = Math.Clamp(ToY(bounds.YPoints), 0, _height);
            int right = Math.Clamp(ToX(bounds.XPoints + bounds.WidthPoints), 0, _width);
            int bottom = Math.Clamp(ToY(bounds.YPoints + bounds.HeightPoints), 0, _height);
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    SetPixel(x, y, color);
                }
            }
        }

        public void DrawImage(ViewportCapture capture, PrintRectangle bounds, CancellationToken cancellationToken)
        {
            int left = ToX(bounds.XPoints);
            int top = ToY(bounds.YPoints);
            int destinationWidth = Math.Max(1, ToPixels(bounds.WidthPoints));
            int destinationHeight = Math.Max(1, ToPixels(bounds.HeightPoints));
            ReadOnlySpan<byte> source = capture.Rgb24.Span;
            for (int y = 0; y < destinationHeight; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int sourceY = Math.Min(capture.Height - 1, y * capture.Height / destinationHeight);
                for (int x = 0; x < destinationWidth; x++)
                {
                    int sourceX = Math.Min(capture.Width - 1, x * capture.Width / destinationWidth);
                    int offset = ((sourceY * capture.Width) + sourceX) * 3;
                    SetPixel(left + x, top + y, new RgbColor(source[offset], source[offset + 1], source[offset + 2]));
                }
            }
        }

        private void DrawGlyph(int left, int top, int width, int height, int codePoint, bool bold)
        {
            int right = left + width - 1;
            int bottom = top + height - 1;
            DrawLine(left, bottom, right, bottom, Ink);
            uint pattern = unchecked((uint)(codePoint * 2_654_435_761));
            for (int stroke = 0; stroke < 3; stroke++)
            {
                int x = left + 1 + (int)((pattern >> (stroke * 5)) % (uint)Math.Max(1, width - 2));
                int y1 = top + (int)((pattern >> (16 + (stroke * 3))) % (uint)Math.Max(1, height / 2));
                DrawLine(x, y1, x, bottom, Ink);
                if (bold && x + 1 <= right)
                {
                    DrawLine(x + 1, y1, x + 1, bottom, Ink);
                }
            }

            if (codePoint > 0x7f)
            {
                DrawLine(left, top, right, top, Ink);
                DrawLine(left, top, left, bottom, Ink);
                DrawLine(right, top, right, bottom, Ink);
            }
        }

        private void DrawLine(int x0, int y0, int x1, int y1, RgbColor color)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                SetPixel(x0, y0, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int doubled = error * 2;
                if (doubled >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (doubled <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private void SetPixel(int x, int y, RgbColor color)
        {
            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
            {
                return;
            }

            if (_activeClip is PixelClip clip &&
                (x < clip.Left || x >= clip.Right || y < clip.Top || y >= clip.Bottom))
            {
                return;
            }

            int offset = ((y * _width) + x) * 3;
            _pixels[offset] = color.Red;
            _pixels[offset + 1] = color.Green;
            _pixels[offset + 2] = color.Blue;
        }

        private int ToX(double points) => (int)Math.Round(points * _scale);

        private int ToY(double points) => (int)Math.Round(points * _scale);

        private int ToPixels(double points) => Math.Max(0, (int)Math.Round(points * _scale));

        private readonly record struct PixelClip(int Left, int Top, int Right, int Bottom);
    }

    private readonly record struct RgbColor(byte Red, byte Green, byte Blue);
}
