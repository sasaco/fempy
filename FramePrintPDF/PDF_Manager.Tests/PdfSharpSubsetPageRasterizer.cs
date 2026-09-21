using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.IO;

namespace PDF_Manager.Tests;

/// <summary>
/// Bounded independent rasterizer for the PDFsharp operator subset emitted by the Step 8 writer.
/// PDFsharp is used only to resolve a page and decode its content stream; painting is performed
/// here from the emitted operators and decoded image samples. Unsupported operators fail closed.
/// </summary>
internal static partial class PdfSharpSubsetPageRasterizer
{
    private const int MaximumOperators = 100_000;
    private const int MaximumDimension = 2_048;

    internal static RasterizedPdfPage Render(byte[] pdf, int pageIndex, bool requireImage)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        using PdfDocument document = PdfReader.Open(
            new MemoryStream(pdf, writable: false),
            PdfDocumentOpenMode.Import);
        if ((uint)pageIndex >= (uint)document.PageCount)
        {
            throw new InvalidDataException("The requested PDF page is absent.");
        }

        PdfPage page = document.Pages[pageIndex];
        int width = checked((int)Math.Round(page.Width.Point));
        int height = checked((int)Math.Round(page.Height.Point));
        if (width is <= 0 or > MaximumDimension || height is <= 0 or > MaximumDimension)
        {
            throw new InvalidDataException("The PDF page dimensions exceed the rasterizer bound.");
        }

        string content = Encoding.Latin1.GetString(ContentReader.ReadContent(page).ToContent());
        string[] lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is 0 or > MaximumOperators)
        {
            throw new InvalidDataException("The PDF content operator count is outside the rasterizer bound.");
        }

        List<DecodedRgbImage> images = ReadImages(pdf);
        byte[] pixels = new byte[checked(width * height)];
        Array.Fill(pixels, (byte)255);
        Stack<GraphicsState> stack = new();
        GraphicsState graphics = GraphicsState.Default;
        PendingRectangle? rectangle = null;
        PendingClipPath? clipPath = null;
        bool clipPending = false;
        bool textOpen = false;
        double textX = 0;
        double textY = 0;
        double fontSize = 0;
        int imageIndex = 0;
        int textRuns = 0;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line == "q")
            {
                RequireNoPendingClipPath(clipPath);
                RequireNoPendingClipOperator(clipPending);
                stack.Push(graphics);
                continue;
            }

            if (line == "Q")
            {
                RequireNoPendingClipPath(clipPath);
                RequireNoPendingClipOperator(clipPending);
                if (!stack.TryPop(out graphics))
                {
                    throw new InvalidDataException("The PDF graphics state is unbalanced.");
                }

                continue;
            }

            if (line == "BT")
            {
                RequireNoPendingClipPath(clipPath);
                RequireNoPendingClipOperator(clipPending);
                if (textOpen)
                {
                    throw new InvalidDataException("Nested PDF text objects are unsupported.");
                }

                textOpen = true;
                textX = 0;
                textY = 0;
                continue;
            }

            if (line == "ET")
            {
                if (!textOpen)
                {
                    throw new InvalidDataException("The PDF text state is unbalanced.");
                }

                textOpen = false;
                continue;
            }

            Match match = RectangleOperator().Match(line);
            if (match.Success)
            {
                RequireNoPendingRectangle(rectangle);
                rectangle = new PendingRectangle(
                    Parse(match, "x"),
                    Parse(match, "y"),
                    Parse(match, "width"),
                    Parse(match, "height"));
                continue;
            }

            match = PathPointOperator().Match(line);
            if (match.Success)
            {
                RequireNoPendingRectangle(rectangle);
                RequireNoPendingClipOperator(clipPending);
                double x = Parse(match, "x");
                double y = Parse(match, "y");
                string operation = match.Groups["operation"].Value;
                if (operation == "m")
                {
                    RequireNoPendingClipPath(clipPath);
                    clipPath = new PendingClipPath(x, y);
                }
                else
                {
                    (clipPath ?? throw new InvalidDataException("A PDF line has no supported move point."))
                        .AddLine(x, y);
                }

                continue;
            }

            if (line == "h")
            {
                (clipPath ?? throw new InvalidDataException("A PDF close-path operator has no path."))
                    .Close();
                continue;
            }

            if (ClipOperator().IsMatch(line))
            {
                graphics = graphics with
                {
                    Clip = (clipPath ?? throw new InvalidDataException("A PDF clip operator has no path."))
                        .ToAxisAlignedRectangle(),
                };
                clipPath = null;
                continue;
            }

            if (line is "W" or "W*")
            {
                if (clipPending || clipPath is null)
                {
                    throw new InvalidDataException("A PDF clip operator has no complete path.");
                }

                clipPending = true;
                continue;
            }

            if (line == "n")
            {
                if (!clipPending)
                {
                    throw new InvalidDataException("A PDF end-path operator has no pending clip.");
                }

                graphics = graphics with
                {
                    Clip = (clipPath ?? throw new InvalidDataException("A PDF clip operator has no path."))
                        .ToAxisAlignedRectangle(),
                };
                clipPath = null;
                clipPending = false;
                continue;
            }

            if (line is "S" or "B" or "f")
            {
                PendingRectangle path = rectangle
                    ?? throw new InvalidDataException("A paint operator has no supported rectangle path.");
                if (line is "B" or "f")
                {
                    FillRectangle(pixels, width, height, path, graphics.FillGray);
                }

                if (line is "B" or "S")
                {
                    StrokeRectangle(pixels, width, height, path);
                }

                rectangle = null;
                continue;
            }

            match = GrayOperator().Match(line);
            if (match.Success)
            {
                graphics = graphics with { FillGray = ToGray(Parse(match, "gray")) };
                continue;
            }

            match = RgbOperator().Match(line);
            if (match.Success)
            {
                graphics = graphics with
                {
                    FillGray = ToGray(
                        Parse(match, "red"),
                        Parse(match, "green"),
                        Parse(match, "blue")),
                };
                continue;
            }

            match = TextMoveOperator().Match(line);
            if (match.Success)
            {
                RequireText(textOpen);
                textX += Parse(match, "x");
                textY += Parse(match, "y");
                continue;
            }

            match = FontOperator().Match(line);
            if (match.Success)
            {
                RequireText(textOpen);
                fontSize = Parse(match, "size");
                if (fontSize <= 0)
                {
                    throw new InvalidDataException("The PDF font size must be positive.");
                }

                continue;
            }

            match = TextShowOperator().Match(line);
            if (match.Success)
            {
                RequireText(textOpen);
                if (fontSize <= 0)
                {
                    throw new InvalidDataException("The PDF text run has no font size.");
                }

                int glyphs = CountGlyphs(match.Groups["text"].Value);
                DrawTextCoverage(pixels, width, height, textX, textY, fontSize, glyphs, graphics.Clip);
                textRuns++;
                continue;
            }

            match = MatrixOperator().Match(line);
            if (match.Success)
            {
                graphics = graphics with
                {
                    Matrix = new Matrix(
                        Parse(match, "a"),
                        Parse(match, "b"),
                        Parse(match, "c"),
                        Parse(match, "d"),
                        Parse(match, "e"),
                        Parse(match, "f")),
                };
                continue;
            }

            if (ImageOperator().IsMatch(line))
            {
                if ((uint)imageIndex >= (uint)images.Count)
                {
                    throw new InvalidDataException("The PDF invokes an image with no decoded image resource.");
                }

                DrawImage(pixels, width, height, graphics.Matrix, images[imageIndex++]);
                continue;
            }

            if (SupportedStateOperator().IsMatch(line))
            {
                continue;
            }

            throw new InvalidDataException($"Unsupported PDF content operator: {line}");
        }

        if (textOpen || stack.Count != 0 || rectangle is not null || clipPath is not null || clipPending || textRuns == 0)
        {
            throw new InvalidDataException("The PDF page ended with invalid or incomplete painting state.");
        }

        if (requireImage ? imageIndex != 1 : imageIndex != 0)
        {
            throw new InvalidDataException("The PDF page has an unexpected image count.");
        }

        return new RasterizedPdfPage(width, height, pixels);
    }

    private static List<DecodedRgbImage> ReadImages(byte[] pdf)
    {
        List<DecodedRgbImage> result = [];
        foreach (PdfDecodedStream stream in PdfRawInspection.Read(pdf).DecodedStreams)
        {
            if (!stream.Dictionary.Contains("/Subtype/Image", StringComparison.Ordinal))
            {
                continue;
            }

            if (!stream.Dictionary.Contains("/DeviceRGB", StringComparison.Ordinal)
                || !stream.Dictionary.Contains("/BitsPerComponent 8", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The bounded rasterizer supports only 8-bit DeviceRGB images.");
            }

            int width = ReadDictionaryInteger(stream.Dictionary, "Width");
            int height = ReadDictionaryInteger(stream.Dictionary, "Height");
            if (stream.Bytes.Length != checked(width * height * 3))
            {
                throw new InvalidDataException("The decoded PDF image byte count is invalid.");
            }

            result.Add(new DecodedRgbImage(width, height, stream.Bytes));
        }

        return result;
    }

    private static int ReadDictionaryInteger(string dictionary, string key)
    {
        Match match = Regex.Match(
            dictionary,
            $@"/{Regex.Escape(key)}\s+(?<value>\d+)\b",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !int.TryParse(match.Groups["value"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            || value is <= 0 or > MaximumDimension)
        {
            throw new InvalidDataException($"The PDF image {key} is invalid.");
        }

        return value;
    }

    private static void DrawImage(byte[] target, int pageWidth, int pageHeight, Matrix matrix, DecodedRgbImage image)
    {
        if (matrix.B != 0 || matrix.C != 0 || matrix.A <= 0 || matrix.D <= 0)
        {
            throw new InvalidDataException("The bounded rasterizer supports only positive axis-aligned images.");
        }

        int left = (int)Math.Round(matrix.E);
        int bottom = (int)Math.Round(matrix.F);
        int drawWidth = Math.Max(1, (int)Math.Round(matrix.A));
        int drawHeight = Math.Max(1, (int)Math.Round(matrix.D));
        for (int y = 0; y < drawHeight; y++)
        {
            int targetY = pageHeight - 1 - (bottom + y);
            if ((uint)targetY >= (uint)pageHeight)
            {
                continue;
            }

            int sourceY = Math.Min(image.Height - 1, y * image.Height / drawHeight);
            for (int x = 0; x < drawWidth; x++)
            {
                int targetX = left + x;
                if ((uint)targetX >= (uint)pageWidth)
                {
                    continue;
                }

                int sourceX = Math.Min(image.Width - 1, x * image.Width / drawWidth);
                int source = ((sourceY * image.Width) + sourceX) * 3;
                target[(targetY * pageWidth) + targetX] = ToGray(
                    image.Rgb[source] / 255d,
                    image.Rgb[source + 1] / 255d,
                    image.Rgb[source + 2] / 255d);
            }
        }
    }

    private static void FillRectangle(
        byte[] pixels,
        int pageWidth,
        int pageHeight,
        PendingRectangle rectangle,
        byte gray)
    {
        int left = Math.Max(0, (int)Math.Floor(rectangle.X));
        int right = Math.Min(pageWidth, (int)Math.Ceiling(rectangle.X + rectangle.Width));
        int bottom = Math.Max(0, (int)Math.Floor(rectangle.Y));
        int top = Math.Min(pageHeight, (int)Math.Ceiling(rectangle.Y + rectangle.Height));
        for (int y = bottom; y < top; y++)
        {
            int row = pageHeight - 1 - y;
            pixels.AsSpan((row * pageWidth) + left, Math.Max(0, right - left)).Fill(gray);
        }
    }

    private static void StrokeRectangle(byte[] pixels, int width, int height, PendingRectangle rectangle)
    {
        int left = (int)Math.Round(rectangle.X);
        int right = (int)Math.Round(rectangle.X + rectangle.Width);
        int bottom = (int)Math.Round(rectangle.Y);
        int top = (int)Math.Round(rectangle.Y + rectangle.Height);
        DrawHorizontal(pixels, width, height, left, right, bottom, 0);
        DrawHorizontal(pixels, width, height, left, right, top, 0);
        DrawVertical(pixels, width, height, left, bottom, top, 0);
        DrawVertical(pixels, width, height, right, bottom, top, 0);
    }

    private static void DrawTextCoverage(
        byte[] pixels,
        int pageWidth,
        int pageHeight,
        double x,
        double y,
        double fontSize,
        int glyphs,
        PendingRectangle? clip)
    {
        int left = (int)Math.Round(x);
        int right = left + Math.Max(1, (int)Math.Round(glyphs * fontSize * 0.48));
        if (clip is PendingRectangle clipRectangle)
        {
            left = Math.Max(left, (int)Math.Ceiling(clipRectangle.X));
            right = Math.Min(right, (int)Math.Floor(clipRectangle.X + clipRectangle.Width));
        }

        int thickness = Math.Max(1, (int)Math.Round(fontSize * 0.14));
        for (int offset = 0; offset < thickness; offset++)
        {
            int baseline = (int)Math.Round(y) + offset;
            if (clip is PendingRectangle verticalClip &&
                (baseline < verticalClip.Y || baseline > verticalClip.Y + verticalClip.Height))
            {
                continue;
            }

            DrawHorizontal(
                pixels,
                pageWidth,
                pageHeight,
                left,
                right,
                baseline,
                32);
        }
    }

    private static void DrawHorizontal(byte[] pixels, int width, int height, int x0, int x1, int y, byte gray)
    {
        int row = height - 1 - y;
        if ((uint)row >= (uint)height)
        {
            return;
        }

        int left = Math.Max(0, Math.Min(x0, x1));
        int right = Math.Min(width - 1, Math.Max(x0, x1));
        for (int x = left; x <= right; x++)
        {
            pixels[(row * width) + x] = gray;
        }
    }

    private static void DrawVertical(byte[] pixels, int width, int height, int x, int y0, int y1, byte gray)
    {
        if ((uint)x >= (uint)width)
        {
            return;
        }

        int bottom = Math.Max(0, Math.Min(y0, y1));
        int top = Math.Min(height - 1, Math.Max(y0, y1));
        for (int y = bottom; y <= top; y++)
        {
            pixels[((height - 1 - y) * width) + x] = gray;
        }
    }

    private static int CountGlyphs(string encoded)
    {
        int glyphs = encoded.Count(character => character != '\0' && character != '\\');
        return Math.Max(1, glyphs);
    }

    private static void RequireText(bool textOpen)
    {
        if (!textOpen)
        {
            throw new InvalidDataException("A PDF text operator appears outside BT/ET.");
        }
    }

    private static void RequireNoPendingRectangle(PendingRectangle? rectangle)
    {
        if (rectangle is not null)
        {
            throw new InvalidDataException("The bounded rasterizer supports one rectangle per path.");
        }
    }

    private static void RequireNoPendingClipPath(PendingClipPath? path)
    {
        if (path is not null)
        {
            throw new InvalidDataException("The bounded rasterizer has an incomplete clip path.");
        }
    }

    private static void RequireNoPendingClipOperator(bool pending)
    {
        if (pending)
        {
            throw new InvalidDataException("The bounded rasterizer has an incomplete clip operator.");
        }
    }

    private static double Parse(Match match, string group) =>
        double.Parse(match.Groups[group].Value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static byte ToGray(double value) =>
        checked((byte)Math.Round(Math.Clamp(value, 0, 1) * 255));

    private static byte ToGray(double red, double green, double blue) =>
        ToGray((red * 0.299) + (green * 0.587) + (blue * 0.114));

    private const string Number = @"[-+]?(?:\d+(?:\.\d*)?|\.\d+)";

    private static Regex SupportedStateOperator() => new(
        @"^(?:" + Number + @"\s+w|" + Number + @"\s+(?:J|j)|\[.*\]" + Number + @"\s+d|/\S+\s+gs)$",
        RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^(?<x>[-+\d.]+)\s+(?<y>[-+\d.]+)\s+(?<width>[-+\d.]+)\s+(?<height>[-+\d.]+)\s+re$", RegexOptions.CultureInvariant)]
    private static partial Regex RectangleOperator();

    [GeneratedRegex(@"^(?<x>[-+\d.]+)\s+(?<y>[-+\d.]+)\s+(?<operation>[ml])$", RegexOptions.CultureInvariant)]
    private static partial Regex PathPointOperator();

    [GeneratedRegex(@"^W\*?\s+n$", RegexOptions.CultureInvariant)]
    private static partial Regex ClipOperator();

    [GeneratedRegex(@"^(?<gray>[-+\d.]+)\s+[gG]$", RegexOptions.CultureInvariant)]
    private static partial Regex GrayOperator();

    [GeneratedRegex(@"^(?<red>[-+\d.]+)\s+(?<green>[-+\d.]+)\s+(?<blue>[-+\d.]+)\s+(?:rg|RG)$", RegexOptions.CultureInvariant)]
    private static partial Regex RgbOperator();

    [GeneratedRegex(@"^(?<x>[-+\d.]+)\s+(?<y>[-+\d.]+)\s+Td$", RegexOptions.CultureInvariant)]
    private static partial Regex TextMoveOperator();

    [GeneratedRegex(@"^/\S+\s+(?<size>[-+\d.]+)\s+Tf$", RegexOptions.CultureInvariant)]
    private static partial Regex FontOperator();

    [GeneratedRegex(@"^\((?<text>.*)\)Tj$", RegexOptions.CultureInvariant)]
    private static partial Regex TextShowOperator();

    [GeneratedRegex(@"^(?<a>[-+\d.]+)\s+(?<b>[-+\d.]+)\s+(?<c>[-+\d.]+)\s+(?<d>[-+\d.]+)\s+(?<e>[-+\d.]+)\s+(?<f>[-+\d.]+)\s+cm$", RegexOptions.CultureInvariant)]
    private static partial Regex MatrixOperator();

    [GeneratedRegex(@"^/\S+\s+Do$", RegexOptions.CultureInvariant)]
    private static partial Regex ImageOperator();

    private sealed record DecodedRgbImage(int Width, int Height, byte[] Rgb);

    private readonly record struct PendingRectangle(double X, double Y, double Width, double Height);

    private sealed class PendingClipPath
    {
        private readonly List<PathPoint> _points;
        private bool _closed;

        public PendingClipPath(double x, double y)
        {
            _points = [new PathPoint(x, y)];
        }

        public void AddLine(double x, double y)
        {
            if (_closed || _points.Count >= 4)
            {
                throw new InvalidDataException("The bounded rasterizer clip path is not a rectangle.");
            }

            _points.Add(new PathPoint(x, y));
        }

        public void Close()
        {
            if (_closed || _points.Count != 4)
            {
                throw new InvalidDataException("The bounded rasterizer clip path must have four vertices.");
            }

            _closed = true;
        }

        public PendingRectangle ToAxisAlignedRectangle()
        {
            if (!_closed || _points.Count != 4)
            {
                throw new InvalidDataException("The bounded rasterizer clip path is incomplete.");
            }

            double minX = _points.Min(point => point.X);
            double maxX = _points.Max(point => point.X);
            double minY = _points.Min(point => point.Y);
            double maxY = _points.Max(point => point.Y);
            if (!(maxX > minX) || !(maxY > minY) ||
                _points.Distinct().Count() != 4 ||
                _points.Any(point =>
                    (point.X != minX && point.X != maxX) ||
                    (point.Y != minY && point.Y != maxY)))
            {
                throw new InvalidDataException("The bounded rasterizer supports only non-empty rectangular clips.");
            }

            for (int index = 0; index < _points.Count; index++)
            {
                PathPoint current = _points[index];
                PathPoint next = _points[(index + 1) % _points.Count];
                if ((current.X == next.X) == (current.Y == next.Y))
                {
                    throw new InvalidDataException("The bounded rasterizer clip edges must be axis aligned.");
                }
            }

            return new PendingRectangle(minX, minY, maxX - minX, maxY - minY);
        }
    }

    private readonly record struct PathPoint(double X, double Y);

    private readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
    {
        internal static Matrix Identity { get; } = new(1, 0, 0, 1, 0, 0);
    }

    private readonly record struct GraphicsState(Matrix Matrix, byte FillGray, PendingRectangle? Clip)
    {
        internal static GraphicsState Default { get; } = new(Matrix.Identity, 0, Clip: null);
    }
}
