using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace PDF_Manager.Tests;

internal sealed record RasterizedPdfPage(int Width, int Height, byte[] Gray8)
{
    internal string ToCompressedBase64()
    {
        using MemoryStream output = new();
        using (ZLibStream compressed = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressed.Write(Gray8);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    internal byte[] ToPortableGraymap()
    {
        byte[] header = Encoding.ASCII.GetBytes($"P5\n{Width} {Height}\n255\n");
        byte[] result = new byte[header.Length + Gray8.Length];
        header.CopyTo(result, 0);
        Gray8.CopyTo(result, header.Length);
        return result;
    }
}

/// <summary>
/// Independent fail-closed rasterizer for the deliberately small PDF subset emitted by
/// TypedPdfDocumentWriter. It follows the trailer/catalog/pages/page/resource/content references,
/// then interprets the owned graphics and text operators into a complete gray page raster.
/// </summary>
internal static class PdfSubsetPageRasterizer
{
    private const int MaximumPdfBytes = 20 * 1024 * 1024;
    private const int MaximumPageDimension = 4_096;
    private const int MaximumContentTokens = 20_000;

    internal static RasterizedPdfPage Render(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        PdfSubsetDocument document = PdfSubsetDocument.Parse(pdf);
        PdfSubsetPage page = document.ReadSinglePage();
        byte[] pixels = new byte[checked(page.Width * page.Height)];
        Array.Fill(pixels, (byte)255);
        RenderContent(document, page, pixels);
        return new RasterizedPdfPage(page.Width, page.Height, pixels);
    }

    internal static byte[] DecodeCompressedBase64(string value, int expectedLength)
    {
        byte[] compressed = Convert.FromBase64String(value.Trim());
        byte[] decompressed = Decompress(compressed);
        if (decompressed.Length != expectedLength)
        {
            throw new InvalidDataException("The approved rendered-page golden has an invalid size.");
        }

        return decompressed;
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using MemoryStream input = new(compressed, writable: false);
        using ZLibStream zlib = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static void RenderContent(PdfSubsetDocument document, PdfSubsetPage page, byte[] pixels)
    {
        PdfSubsetObject contentObject = document.ReadObject(page.ContentObjectNumber);
        if (contentObject.Stream is null || HasDictionaryKey(contentObject.Dictionary, "Filter"))
        {
            throw new InvalidDataException("The owned PDF page content must be one unfiltered stream.");
        }

        List<ContentToken> tokens = Tokenize(Encoding.Latin1.GetString(contentObject.Stream));
        List<ContentToken> operands = [];
        Stack<GraphicsState> graphicsStack = new();
        GraphicsState graphics = new(AffineMatrix.Identity, HasExplicitTransform: false);
        bool textOpen = false;
        string? fontName = null;
        double fontSize = 0;
        double? textX = null;
        double? textY = null;
        int imageDrawCount = 0;
        int textDrawCount = 0;

        foreach (ContentToken token in tokens)
        {
            if (token.Kind != ContentTokenKind.Operator)
            {
                if (operands.Count == 8)
                {
                    throw new InvalidDataException("The owned PDF content operand stack exceeded its bound.");
                }

                operands.Add(token);
                continue;
            }

            switch (token.Value)
            {
                case "BT":
                    RequireOperandCount(operands, 0, token.Value);
                    if (textOpen)
                    {
                        throw new InvalidDataException("Nested PDF text objects are outside the owned subset.");
                    }

                    textOpen = true;
                    fontName = null;
                    fontSize = 0;
                    textX = null;
                    textY = null;
                    break;

                case "Tf":
                    RequireTextOpen(textOpen, token.Value);
                    RequireOperandCount(operands, 2, token.Value);
                    fontName = RequireName(operands[0], token.Value);
                    fontSize = RequirePositiveNumber(operands[1], token.Value);
                    document.RequireOwnedFont(page, fontName);
                    break;

                case "Td":
                    RequireTextOpen(textOpen, token.Value);
                    RequireOperandCount(operands, 2, token.Value);
                    textX = RequireNumber(operands[0], token.Value);
                    textY = RequireNumber(operands[1], token.Value);
                    break;

                case "Tj":
                    RequireTextOpen(textOpen, token.Value);
                    RequireOperandCount(operands, 1, token.Value);
                    if (fontName is null || textX is null || textY is null)
                    {
                        throw new InvalidDataException("The owned PDF text operator is missing font or position state.");
                    }

                    RequireAxisAlignedTextMatrix(graphics.Matrix);
                    (double x, double y) = graphics.Matrix.Transform(textX.Value, textY.Value);
                    DrawText(
                        pixels,
                        page.Width,
                        page.Height,
                        x,
                        y,
                        fontSize * graphics.Matrix.D,
                        RequireString(operands[0], token.Value));
                    textDrawCount++;
                    break;

                case "ET":
                    RequireOperandCount(operands, 0, token.Value);
                    RequireTextOpen(textOpen, token.Value);
                    textOpen = false;
                    break;

                case "q":
                    RequireOutsideText(textOpen, token.Value);
                    RequireOperandCount(operands, 0, token.Value);
                    graphicsStack.Push(graphics);
                    break;

                case "cm":
                    RequireOutsideText(textOpen, token.Value);
                    RequireOperandCount(operands, 6, token.Value);
                    AffineMatrix specified = new(
                        RequireNumber(operands[0], token.Value),
                        RequireNumber(operands[1], token.Value),
                        RequireNumber(operands[2], token.Value),
                        RequireNumber(operands[3], token.Value),
                        RequireNumber(operands[4], token.Value),
                        RequireNumber(operands[5], token.Value));
                    graphics = new GraphicsState(
                        graphics.Matrix.Concatenate(specified),
                        HasExplicitTransform: true);
                    break;

                case "Do":
                    RequireOutsideText(textOpen, token.Value);
                    RequireOperandCount(operands, 1, token.Value);
                    if (!graphics.HasExplicitTransform)
                    {
                        throw new InvalidDataException("The owned PDF image invocation is missing its explicit cm transform.");
                    }

                    RenderImageXObject(
                        document,
                        page,
                        RequireName(operands[0], token.Value),
                        graphics.Matrix,
                        pixels);
                    imageDrawCount++;
                    break;

                case "Q":
                    RequireOutsideText(textOpen, token.Value);
                    RequireOperandCount(operands, 0, token.Value);
                    if (!graphicsStack.TryPop(out graphics))
                    {
                        throw new InvalidDataException("The owned PDF graphics stack is unbalanced.");
                    }

                    break;

                default:
                    throw new InvalidDataException($"Unsupported owned PDF content operator '{token.Value}'.");
            }

            operands.Clear();
        }

        if (operands.Count != 0 || textOpen || graphicsStack.Count != 0)
        {
            throw new InvalidDataException("The owned PDF content stream ended with unbalanced state.");
        }

        if (imageDrawCount != 1 || textDrawCount == 0)
        {
            throw new InvalidDataException("The owned PDF page must draw exactly one viewport image and at least one text run.");
        }
    }

    private static void RenderImageXObject(
        PdfSubsetDocument document,
        PdfSubsetPage page,
        string resourceName,
        AffineMatrix matrix,
        byte[] pixels)
    {
        if (!page.XObjects.TryGetValue(resourceName, out int objectNumber))
        {
            throw new InvalidDataException($"PDF XObject resource '/{resourceName}' is not mapped by the page.");
        }

        PdfSubsetObject imageObject = document.ReadObject(objectNumber);
        RequireNameEntry(imageObject.Dictionary, "Type", "XObject");
        RequireNameEntry(imageObject.Dictionary, "Subtype", "Image");
        RequireNameEntry(imageObject.Dictionary, "ColorSpace", "DeviceRGB");
        RequireNameEntry(imageObject.Dictionary, "Filter", "FlateDecode");
        if (ReadRequiredInteger(imageObject.Dictionary, "BitsPerComponent") != 8 || imageObject.Stream is null)
        {
            throw new InvalidDataException("The owned PDF viewport image must be an 8-bit RGB Flate stream.");
        }

        int imageWidth = ReadRequiredInteger(imageObject.Dictionary, "Width");
        int imageHeight = ReadRequiredInteger(imageObject.Dictionary, "Height");
        if (imageWidth is <= 0 or > MaximumPageDimension || imageHeight is <= 0 or > MaximumPageDimension)
        {
            throw new InvalidDataException("The owned PDF viewport image dimensions are outside the test bound.");
        }

        byte[] image = Decompress(imageObject.Stream);
        if (image.Length != checked(imageWidth * imageHeight * 3))
        {
            throw new InvalidDataException("The owned PDF image payload length is invalid.");
        }

        DrawImage(
            pixels,
            page.Width,
            page.Height,
            image,
            imageWidth,
            imageHeight,
            matrix);
    }

    private static List<ContentToken> Tokenize(string content)
    {
        List<ContentToken> tokens = [];
        int index = 0;
        while (index < content.Length)
        {
            SkipContentWhitespaceAndComments(content, ref index);
            if (index == content.Length)
            {
                break;
            }

            if (tokens.Count == MaximumContentTokens)
            {
                throw new InvalidDataException("The owned PDF content token count exceeded its bound.");
            }

            char current = content[index];
            if (current == '/')
            {
                tokens.Add(new ContentToken(ContentTokenKind.Name, ReadName(content, ref index)));
            }
            else if (current == '(')
            {
                tokens.Add(new ContentToken(ContentTokenKind.String, ReadLiteralString(content, ref index)));
            }
            else
            {
                string value = ReadBareToken(content, ref index);
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    tokens.Add(new ContentToken(ContentTokenKind.Number, value));
                }
                else
                {
                    tokens.Add(new ContentToken(ContentTokenKind.Operator, value));
                }
            }
        }

        return tokens;
    }

    private static void SkipContentWhitespaceAndComments(string content, ref int index)
    {
        while (index < content.Length)
        {
            if (char.IsWhiteSpace(content[index]))
            {
                index++;
                continue;
            }

            if (content[index] != '%')
            {
                return;
            }

            while (index < content.Length && content[index] != '\n')
            {
                index++;
            }
        }
    }

    private static string ReadName(string value, ref int index)
    {
        if (value[index++] != '/')
        {
            throw new InvalidDataException("A PDF name was expected.");
        }

        int start = index;
        while (index < value.Length && !IsPdfDelimiter(value[index]))
        {
            index++;
        }

        if (start == index)
        {
            throw new InvalidDataException("An empty PDF name is outside the owned subset.");
        }

        return value[start..index];
    }

    private static string ReadLiteralString(string value, ref int index)
    {
        if (value[index++] != '(')
        {
            throw new InvalidDataException("A PDF literal string was expected.");
        }

        StringBuilder result = new();
        int depth = 1;
        while (index < value.Length)
        {
            char current = value[index++];
            if (current == '\\')
            {
                if (index == value.Length)
                {
                    throw new InvalidDataException("The owned PDF literal string has an incomplete escape.");
                }

                char escaped = value[index++];
                result.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'b' => '\b',
                    'f' => '\f',
                    '(' or ')' or '\\' => escaped,
                    _ => throw new InvalidDataException("The owned PDF literal string uses an unsupported escape."),
                });
                continue;
            }

            if (current == '(')
            {
                depth++;
                result.Append(current);
            }
            else if (current == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return result.ToString();
                }

                result.Append(current);
            }
            else
            {
                result.Append(current);
            }
        }

        throw new InvalidDataException("The owned PDF literal string is not terminated.");
    }

    private static string ReadBareToken(string value, ref int index)
    {
        int start = index;
        while (index < value.Length && !IsPdfDelimiter(value[index]))
        {
            index++;
        }

        if (start == index)
        {
            throw new InvalidDataException($"Unsupported PDF content delimiter '{value[index]}'.");
        }

        return value[start..index];
    }

    private static bool IsPdfDelimiter(char value) =>
        char.IsWhiteSpace(value) || value is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';

    private static void RequireOperandCount(IReadOnlyCollection<ContentToken> operands, int expected, string operation)
    {
        if (operands.Count != expected)
        {
            throw new InvalidDataException($"PDF operator '{operation}' has an invalid operand count.");
        }
    }

    private static double RequireNumber(ContentToken token, string operation)
    {
        if (token.Kind != ContentTokenKind.Number ||
            !double.TryParse(token.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !double.IsFinite(value))
        {
            throw new InvalidDataException($"PDF operator '{operation}' requires a finite number.");
        }

        return value;
    }

    private static double RequirePositiveNumber(ContentToken token, string operation)
    {
        double value = RequireNumber(token, operation);
        if (value <= 0)
        {
            throw new InvalidDataException($"PDF operator '{operation}' requires a positive number.");
        }

        return value;
    }

    private static string RequireName(ContentToken token, string operation) =>
        token.Kind == ContentTokenKind.Name
            ? token.Value
            : throw new InvalidDataException($"PDF operator '{operation}' requires a name.");

    private static string RequireString(ContentToken token, string operation) =>
        token.Kind == ContentTokenKind.String
            ? token.Value
            : throw new InvalidDataException($"PDF operator '{operation}' requires a string.");

    private static void RequireTextOpen(bool textOpen, string operation)
    {
        if (!textOpen)
        {
            throw new InvalidDataException($"PDF text operator '{operation}' is outside BT/ET.");
        }
    }

    private static void RequireOutsideText(bool textOpen, string operation)
    {
        if (textOpen)
        {
            throw new InvalidDataException($"PDF graphics operator '{operation}' is inside BT/ET.");
        }
    }

    private static void RequireAxisAlignedTextMatrix(AffineMatrix matrix)
    {
        if (matrix.B != 0 || matrix.C != 0 || matrix.A <= 0 || matrix.D <= 0)
        {
            throw new InvalidDataException("The owned PDF text matrix must remain positive and axis-aligned.");
        }
    }

    private static int RequireWholeNumber(double value, string description)
    {
        if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue || value != Math.Truncate(value))
        {
            throw new InvalidDataException($"The owned PDF {description} must be a finite integer.");
        }

        return checked((int)value);
    }

    private static void DrawImage(
        byte[] page,
        int pageWidth,
        int pageHeight,
        byte[] image,
        int imageWidth,
        int imageHeight,
        AffineMatrix matrix)
    {
        if (matrix.B != 0 || matrix.C != 0 || matrix.A <= 0 || matrix.D <= 0)
        {
            throw new InvalidDataException("The owned PDF image matrix must be a positive axis-aligned transform.");
        }

        int left = RequireWholeNumber(matrix.E, "image x");
        int width = RequireWholeNumber(matrix.A, "image width");
        int height = RequireWholeNumber(matrix.D, "image height");
        int bottom = RequireWholeNumber(matrix.F, "image y");
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("The owned PDF image matrix has an empty extent.");
        }

        int top = checked(pageHeight - checked(bottom + height));
        int firstY = Math.Max(0, top);
        int lastY = Math.Min(pageHeight, checked(top + height));
        int firstX = Math.Max(0, left);
        int lastX = Math.Min(pageWidth, checked(left + width));
        for (int y = firstY; y < lastY; y++)
        {
            int sourceY = Math.Min(imageHeight - 1, ((y - top) * imageHeight) / height);
            for (int x = firstX; x < lastX; x++)
            {
                int sourceX = Math.Min(imageWidth - 1, ((x - left) * imageWidth) / width);
                int source = ((sourceY * imageWidth) + sourceX) * 3;
                page[(y * pageWidth) + x] = ToGray(
                    image[source],
                    image[source + 1],
                    image[source + 2]);
            }
        }
    }

    private static void DrawText(
        byte[] page,
        int pageWidth,
        int pageHeight,
        double x,
        double baselineY,
        double size,
        string value)
    {
        int scale = Math.Max(1, (int)Math.Round(size / 7, MidpointRounding.AwayFromZero));
        int cursorX = (int)Math.Round(x, MidpointRounding.AwayFromZero);
        int top = pageHeight - (int)Math.Round(baselineY, MidpointRounding.AwayFromZero) - (7 * scale);
        foreach (char character in value)
        {
            byte[] rows = Glyph(character);
            for (int row = 0; row < rows.Length; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    if ((rows[row] & (1 << (4 - column))) == 0)
                    {
                        continue;
                    }

                    Fill(page, pageWidth, pageHeight, cursorX + (column * scale), top + (row * scale), scale, scale, 0);
                }
            }

            cursorX += 6 * scale;
        }
    }

    private static int ReadRequiredReference(string dictionary, string key)
    {
        int index = PositionAfterDictionaryKey(dictionary, key);
        SkipWhitespace(dictionary, ref index);
        int objectNumber = ReadUnsignedInteger(dictionary, ref index, $"/{key} object number");
        SkipWhitespace(dictionary, ref index);
        int generation = ReadUnsignedInteger(dictionary, ref index, $"/{key} generation");
        SkipWhitespace(dictionary, ref index);
        if (generation != 0 || index >= dictionary.Length || dictionary[index++] != 'R' ||
            (index < dictionary.Length && !IsPdfDelimiter(dictionary[index])))
        {
            throw new InvalidDataException($"PDF dictionary entry '/{key}' is not a generation-zero reference.");
        }

        return objectNumber;
    }

    private static int ReadRequiredInteger(string dictionary, string key)
    {
        int index = PositionAfterDictionaryKey(dictionary, key);
        SkipWhitespace(dictionary, ref index);
        int value = ReadUnsignedInteger(dictionary, ref index, $"/{key}");
        if (index < dictionary.Length && !IsPdfDelimiter(dictionary[index]))
        {
            throw new InvalidDataException($"PDF dictionary entry '/{key}' is not an integer.");
        }

        return value;
    }

    private static void RequireNameEntry(string dictionary, string key, string expected)
    {
        int index = PositionAfterDictionaryKey(dictionary, key);
        SkipWhitespace(dictionary, ref index);
        string actual = ReadName(dictionary, ref index);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"PDF dictionary entry '/{key}' must be '/{expected}'.");
        }
    }

    private static bool HasDictionaryKey(string dictionary, string key) =>
        TryFindDictionaryKey(dictionary, key, out _);

    private static string ReadNestedDictionary(string dictionary, string key)
    {
        int index = PositionAfterDictionaryKey(dictionary, key);
        SkipWhitespace(dictionary, ref index);
        if (!dictionary.AsSpan(index).StartsWith("<<", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"PDF dictionary entry '/{key}' must be an inline dictionary.");
        }

        int end = FindDictionaryEnd(dictionary, index);
        return dictionary[index..end];
    }

    private static string ReadArray(string dictionary, string key)
    {
        int index = PositionAfterDictionaryKey(dictionary, key);
        SkipWhitespace(dictionary, ref index);
        if (index >= dictionary.Length || dictionary[index] != '[')
        {
            throw new InvalidDataException($"PDF dictionary entry '/{key}' must be an array.");
        }

        int end = dictionary.IndexOf(']', index + 1);
        if (end < 0)
        {
            throw new InvalidDataException($"PDF array '/{key}' is not terminated.");
        }

        return dictionary[(index + 1)..end];
    }

    private static Dictionary<string, int> ReadResourceMap(string resources, string key)
    {
        string mapDictionary = ReadNestedDictionary(resources, key);
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        int index = 2;
        while (true)
        {
            SkipWhitespace(mapDictionary, ref index);
            if (mapDictionary.AsSpan(index).StartsWith(">>", StringComparison.Ordinal))
            {
                return result.Count > 0
                    ? result
                    : throw new InvalidDataException($"PDF resource dictionary '/{key}' is empty.");
            }

            if (index >= mapDictionary.Length || mapDictionary[index] != '/')
            {
                throw new InvalidDataException($"PDF resource dictionary '/{key}' contains an unsupported entry.");
            }

            string name = ReadName(mapDictionary, ref index);
            SkipWhitespace(mapDictionary, ref index);
            int objectNumber = ReadUnsignedInteger(mapDictionary, ref index, $"/{name} object number");
            SkipWhitespace(mapDictionary, ref index);
            int generation = ReadUnsignedInteger(mapDictionary, ref index, $"/{name} generation");
            SkipWhitespace(mapDictionary, ref index);
            if (generation != 0 || index >= mapDictionary.Length || mapDictionary[index++] != 'R' ||
                !result.TryAdd(name, objectNumber))
            {
                throw new InvalidDataException($"PDF resource '/{name}' is not one unique generation-zero reference.");
            }
        }
    }

    private static int[] ReadReferenceArray(string dictionary, string key)
    {
        string array = ReadArray(dictionary, key);
        List<int> result = [];
        int index = 0;
        while (index < array.Length)
        {
            SkipWhitespace(array, ref index);
            if (index == array.Length)
            {
                break;
            }

            int objectNumber = ReadUnsignedInteger(array, ref index, $"/{key} object number");
            SkipWhitespace(array, ref index);
            int generation = ReadUnsignedInteger(array, ref index, $"/{key} generation");
            SkipWhitespace(array, ref index);
            if (generation != 0 || index >= array.Length || array[index++] != 'R')
            {
                throw new InvalidDataException($"PDF array '/{key}' contains a non-reference entry.");
            }

            result.Add(objectNumber);
        }

        return result.ToArray();
    }

    private static double[] ReadNumberArray(string dictionary, string key)
    {
        string array = ReadArray(dictionary, key);
        List<double> result = [];
        int index = 0;
        while (index < array.Length)
        {
            SkipWhitespace(array, ref index);
            if (index == array.Length)
            {
                break;
            }

            int start = index;
            while (index < array.Length && !char.IsWhiteSpace(array[index]))
            {
                index++;
            }

            if (!double.TryParse(array[start..index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                !double.IsFinite(value))
            {
                throw new InvalidDataException($"PDF array '/{key}' contains an invalid number.");
            }

            result.Add(value);
        }

        return result.ToArray();
    }

    private static int PositionAfterDictionaryKey(string dictionary, string key) =>
        TryFindDictionaryKey(dictionary, key, out int position)
            ? position
            : throw new InvalidDataException($"Required PDF dictionary entry '/{key}' was not found.");

    private static bool TryFindDictionaryKey(string dictionary, string key, out int position)
    {
        string marker = $"/{key}";
        int search = 0;
        while (true)
        {
            int found = dictionary.IndexOf(marker, search, StringComparison.Ordinal);
            if (found < 0)
            {
                position = -1;
                return false;
            }

            int after = found + marker.Length;
            if (after == dictionary.Length || IsPdfDelimiter(dictionary[after]))
            {
                position = after;
                return true;
            }

            search = after;
        }
    }

    private static int FindDictionaryEnd(string value, int start)
    {
        if (!value.AsSpan(start).StartsWith("<<", StringComparison.Ordinal))
        {
            throw new InvalidDataException("A PDF dictionary was expected.");
        }

        int depth = 0;
        int index = start;
        while (index < value.Length - 1)
        {
            if (value[index] == '(')
            {
                _ = ReadLiteralString(value, ref index);
                continue;
            }

            if (value.AsSpan(index).StartsWith("<<", StringComparison.Ordinal))
            {
                depth++;
                index += 2;
            }
            else if (value.AsSpan(index).StartsWith(">>", StringComparison.Ordinal))
            {
                depth--;
                index += 2;
                if (depth == 0)
                {
                    return index;
                }
            }
            else
            {
                index++;
            }
        }

        throw new InvalidDataException("A PDF dictionary is not terminated.");
    }

    private static int ReadUnsignedInteger(string value, ref int index, string description)
    {
        int start = index;
        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            index++;
        }

        if (start == index || !int.TryParse(value[start..index], NumberStyles.None, CultureInfo.InvariantCulture, out int result))
        {
            throw new InvalidDataException($"PDF {description} is not a bounded unsigned integer.");
        }

        return result;
    }

    private static void SkipWhitespace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }

    private static string ReadLine(string value, ref int index)
    {
        int end = value.IndexOf('\n', index);
        if (end < 0)
        {
            throw new InvalidDataException("The owned PDF contains an unterminated line.");
        }

        string line = value[index..end];
        index = end + 1;
        return line;
    }

    private sealed class PdfSubsetDocument
    {
        private readonly byte[] bytes;
        private readonly string text;
        private readonly Dictionary<int, int> offsets;
        private readonly Dictionary<int, PdfSubsetObject> objects = [];
        private readonly int rootObjectNumber;

        private PdfSubsetDocument(byte[] bytes, string text, Dictionary<int, int> offsets, int rootObjectNumber)
        {
            this.bytes = bytes;
            this.text = text;
            this.offsets = offsets;
            this.rootObjectNumber = rootObjectNumber;
        }

        internal static PdfSubsetDocument Parse(byte[] pdf)
        {
            if (pdf.Length is < 32 or > MaximumPdfBytes)
            {
                throw new InvalidDataException("The owned PDF size is outside the test bound.");
            }

            string text = Encoding.Latin1.GetString(pdf);
            if (!text.StartsWith("%PDF-1.4\n", StringComparison.Ordinal) || !text.EndsWith("%%EOF\n", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The owned PDF header or trailer marker is invalid.");
            }

            const string startXrefMarker = "startxref\n";
            int startXref = text.LastIndexOf(startXrefMarker, StringComparison.Ordinal);
            if (startXref < 0)
            {
                throw new InvalidDataException("The owned PDF has no startxref marker.");
            }

            int offsetCursor = startXref + startXrefMarker.Length;
            if (!int.TryParse(ReadLine(text, ref offsetCursor), NumberStyles.None, CultureInfo.InvariantCulture, out int xrefOffset) ||
                xrefOffset < 0 || xrefOffset >= startXref)
            {
                throw new InvalidDataException("The owned PDF startxref offset is invalid.");
            }

            int cursor = xrefOffset;
            if (!string.Equals(ReadLine(text, ref cursor), "xref", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The owned PDF startxref does not reference an xref table.");
            }

            string[] header = ReadLine(text, ref cursor).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (header.Length != 2 || header[0] != "0" ||
                !int.TryParse(header[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count is < 2 or > 100)
            {
                throw new InvalidDataException("The owned PDF xref subsection is invalid.");
            }

            Dictionary<int, int> offsets = [];
            for (int objectNumber = 0; objectNumber < count; objectNumber++)
            {
                string[] entry = ReadLine(text, ref cursor).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (entry.Length != 3 || entry[0].Length != 10 || entry[1].Length != 5 ||
                    !int.TryParse(entry[0], NumberStyles.None, CultureInfo.InvariantCulture, out int offset) ||
                    !int.TryParse(entry[1], NumberStyles.None, CultureInfo.InvariantCulture, out int generation))
                {
                    throw new InvalidDataException("The owned PDF xref entry is invalid.");
                }

                if (objectNumber == 0)
                {
                    if (offset != 0 || generation != 65535 || entry[2] != "f")
                    {
                        throw new InvalidDataException("The owned PDF free xref entry is invalid.");
                    }
                }
                else if (generation != 0 || entry[2] != "n" || offset <= 0 || offset >= xrefOffset ||
                    !offsets.TryAdd(objectNumber, offset))
                {
                    throw new InvalidDataException("The owned PDF in-use xref entry is invalid.");
                }
            }

            if (!string.Equals(ReadLine(text, ref cursor), "trailer", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The owned PDF xref table has no trailer dictionary.");
            }

            int trailerStart = cursor;
            int trailerEnd = FindDictionaryEnd(text, trailerStart);
            string trailer = text[trailerStart..trailerEnd];
            if (ReadRequiredInteger(trailer, "Size") != count)
            {
                throw new InvalidDataException("The owned PDF trailer size does not match its xref table.");
            }

            return new PdfSubsetDocument(pdf, text, offsets, ReadRequiredReference(trailer, "Root"));
        }

        internal PdfSubsetPage ReadSinglePage()
        {
            PdfSubsetObject catalog = ReadObject(rootObjectNumber);
            RequireNameEntry(catalog.Dictionary, "Type", "Catalog");
            int pagesNumber = ReadRequiredReference(catalog.Dictionary, "Pages");
            PdfSubsetObject pages = ReadObject(pagesNumber);
            RequireNameEntry(pages.Dictionary, "Type", "Pages");
            int[] kids = ReadReferenceArray(pages.Dictionary, "Kids");
            if (ReadRequiredInteger(pages.Dictionary, "Count") != 1 || kids.Length != 1)
            {
                throw new InvalidDataException("The owned PDF must have one page in one Pages node.");
            }

            PdfSubsetObject page = ReadObject(kids[0]);
            RequireNameEntry(page.Dictionary, "Type", "Page");
            if (ReadRequiredReference(page.Dictionary, "Parent") != pagesNumber)
            {
                throw new InvalidDataException("The owned PDF page Parent does not reference its traversed Pages node.");
            }

            double[] mediaBox = ReadNumberArray(page.Dictionary, "MediaBox");
            if (mediaBox.Length != 4 || mediaBox[0] != 0 || mediaBox[1] != 0)
            {
                throw new InvalidDataException("The owned PDF MediaBox must start at the origin.");
            }

            int width = RequireWholeNumber(mediaBox[2], "page width");
            int height = RequireWholeNumber(mediaBox[3], "page height");
            if (width is <= 0 or > MaximumPageDimension || height is <= 0 or > MaximumPageDimension)
            {
                throw new InvalidDataException("The owned PDF page dimensions are outside the test bound.");
            }

            string resources = ReadNestedDictionary(page.Dictionary, "Resources");
            return new PdfSubsetPage(
                width,
                height,
                ReadRequiredReference(page.Dictionary, "Contents"),
                ReadResourceMap(resources, "Font"),
                ReadResourceMap(resources, "XObject"));
        }

        internal PdfSubsetObject ReadObject(int objectNumber)
        {
            if (objects.TryGetValue(objectNumber, out PdfSubsetObject? existing))
            {
                return existing;
            }

            if (!offsets.TryGetValue(objectNumber, out int cursor))
            {
                throw new InvalidDataException($"Referenced PDF object {objectNumber} is absent from the xref table.");
            }

            if (!string.Equals(ReadLine(text, ref cursor), $"{objectNumber} 0 obj", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"PDF object {objectNumber} does not begin at its xref offset.");
            }

            SkipWhitespace(text, ref cursor);
            int dictionaryStart = cursor;
            int dictionaryEnd = FindDictionaryEnd(text, dictionaryStart);
            string dictionary = text[dictionaryStart..dictionaryEnd];
            cursor = dictionaryEnd;
            byte[]? stream = null;
            if (text.AsSpan(cursor).StartsWith("\nstream\n", StringComparison.Ordinal))
            {
                cursor += "\nstream\n".Length;
                int length = ReadRequiredInteger(dictionary, "Length");
                int end = checked(cursor + length);
                if (end > bytes.Length || !text.AsSpan(end).StartsWith("\nendstream\nendobj\n", StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"PDF object {objectNumber} has an invalid stream boundary.");
                }

                stream = bytes.AsSpan(cursor, length).ToArray();
            }
            else if (!text.AsSpan(cursor).StartsWith("\nendobj\n", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"PDF object {objectNumber} has an invalid object boundary.");
            }

            PdfSubsetObject result = new(objectNumber, dictionary, stream);
            objects.Add(objectNumber, result);
            return result;
        }

        internal void RequireOwnedFont(PdfSubsetPage page, string resourceName)
        {
            if (!page.Fonts.TryGetValue(resourceName, out int objectNumber))
            {
                throw new InvalidDataException($"PDF font resource '/{resourceName}' is not mapped by the page.");
            }

            PdfSubsetObject font = ReadObject(objectNumber);
            RequireNameEntry(font.Dictionary, "Type", "Font");
            RequireNameEntry(font.Dictionary, "Subtype", "Type1");
            RequireNameEntry(font.Dictionary, "BaseFont", "Helvetica");
            RequireNameEntry(font.Dictionary, "Encoding", "WinAnsiEncoding");
        }
    }

    private sealed record PdfSubsetObject(int Number, string Dictionary, byte[]? Stream);

    private sealed record PdfSubsetPage(
        int Width,
        int Height,
        int ContentObjectNumber,
        IReadOnlyDictionary<string, int> Fonts,
        IReadOnlyDictionary<string, int> XObjects);

    private readonly record struct GraphicsState(AffineMatrix Matrix, bool HasExplicitTransform);

    private readonly record struct AffineMatrix(double A, double B, double C, double D, double E, double F)
    {
        internal static AffineMatrix Identity { get; } = new(1, 0, 0, 1, 0, 0);

        internal AffineMatrix Concatenate(AffineMatrix next) => new(
            (A * next.A) + (C * next.B),
            (B * next.A) + (D * next.B),
            (A * next.C) + (C * next.D),
            (B * next.C) + (D * next.D),
            (A * next.E) + (C * next.F) + E,
            (B * next.E) + (D * next.F) + F);

        internal (double X, double Y) Transform(double x, double y) =>
            ((A * x) + (C * y) + E, (B * x) + (D * y) + F);
    }

    private enum ContentTokenKind
    {
        Number,
        Name,
        String,
        Operator,
    }

    private readonly record struct ContentToken(ContentTokenKind Kind, string Value);

    private static byte[] Glyph(char character) => char.ToUpperInvariant(character) switch
    {
        'A' => [14, 17, 17, 31, 17, 17, 17],
        'B' => [30, 17, 17, 30, 17, 17, 30],
        'C' => [14, 17, 16, 16, 16, 17, 14],
        'D' => [30, 17, 17, 17, 17, 17, 30],
        'E' => [31, 16, 16, 30, 16, 16, 31],
        'F' => [31, 16, 16, 30, 16, 16, 16],
        'G' => [14, 17, 16, 23, 17, 17, 14],
        'H' => [17, 17, 17, 31, 17, 17, 17],
        'I' => [14, 4, 4, 4, 4, 4, 14],
        'J' => [7, 2, 2, 2, 18, 18, 12],
        'K' => [17, 18, 20, 24, 20, 18, 17],
        'L' => [16, 16, 16, 16, 16, 16, 31],
        'M' => [17, 27, 21, 21, 17, 17, 17],
        'N' => [17, 25, 21, 19, 17, 17, 17],
        'O' => [14, 17, 17, 17, 17, 17, 14],
        'P' => [30, 17, 17, 30, 16, 16, 16],
        'Q' => [14, 17, 17, 17, 21, 18, 13],
        'R' => [30, 17, 17, 30, 20, 18, 17],
        'S' => [15, 16, 16, 14, 1, 1, 30],
        'T' => [31, 4, 4, 4, 4, 4, 4],
        'U' => [17, 17, 17, 17, 17, 17, 14],
        'V' => [17, 17, 17, 17, 17, 10, 4],
        'W' => [17, 17, 17, 21, 21, 21, 10],
        'X' => [17, 17, 10, 4, 10, 17, 17],
        'Y' => [17, 17, 10, 4, 4, 4, 4],
        'Z' => [31, 1, 2, 4, 8, 16, 31],
        >= '0' and <= '9' => Digit(character),
        ' ' => [0, 0, 0, 0, 0, 0, 0],
        '-' => [0, 0, 0, 31, 0, 0, 0],
        '/' => [1, 2, 2, 4, 8, 8, 16],
        ':' => [0, 4, 4, 0, 4, 4, 0],
        '.' => [0, 0, 0, 0, 0, 6, 6],
        _ => [31, 17, 2, 4, 4, 0, 4],
    };

    private static byte[] Digit(char value) => value switch
    {
        '0' => [14, 17, 19, 21, 25, 17, 14],
        '1' => [4, 12, 4, 4, 4, 4, 14],
        '2' => [14, 17, 1, 2, 4, 8, 31],
        '3' => [30, 1, 1, 14, 1, 1, 30],
        '4' => [2, 6, 10, 18, 31, 2, 2],
        '5' => [31, 16, 16, 30, 1, 1, 30],
        '6' => [14, 16, 16, 30, 17, 17, 14],
        '7' => [31, 1, 2, 4, 8, 8, 8],
        '8' => [14, 17, 17, 14, 17, 17, 14],
        _ => [14, 17, 17, 15, 1, 1, 14],
    };

    private static void Fill(
        byte[] page,
        int pageWidth,
        int pageHeight,
        int x,
        int y,
        int width,
        int height,
        byte value)
    {
        for (int row = Math.Max(0, y); row < Math.Min(pageHeight, y + height); row++)
        {
            for (int column = Math.Max(0, x); column < Math.Min(pageWidth, x + width); column++)
            {
                page[(row * pageWidth) + column] = value;
            }
        }
    }

    private static byte ToGray(byte red, byte green, byte blue) =>
        (byte)((299 * red + 587 * green + 114 * blue + 500) / 1000);

}
