using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace FrameWebforCS.Printing;

public sealed record PdfModelNode(string Id, double X, double Y, double Z);

public sealed record PdfModelMember(string Id, string NodeI, string NodeJ);

public sealed record PdfResultRow(string Kind, string Id, double X, double Y, double Z);

public sealed record ViewportCapture
{
    public const int MaximumDimension = PrintEngineLimits.MaximumImageDimension;
    public const int MaximumBytes = 12 * 1024 * 1024;

    public ViewportCapture(int width, int height, ReadOnlyMemory<byte> rgb24)
    {
        int expectedLength = GetRequiredByteLength(width, height);
        if (rgb24.Length != expectedLength)
        {
            throw new ArgumentException("The RGB capture has an invalid payload length.", nameof(rgb24));
        }

        Width = width;
        Height = height;
        Rgb24 = rgb24.ToArray();
    }

    public int Width { get; }

    public int Height { get; }

    public ReadOnlyMemory<byte> Rgb24 { get; }

    public static int GetRequiredByteLength(int width, int height)
    {
        if (width is <= 0 or > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height is <= 0 or > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        int expectedLength = checked(width * height * 3);
        if (expectedLength > MaximumBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The RGB capture exceeds the byte limit.");
        }

        return expectedLength;
    }
}

public sealed class TypedPdfJob
{
    public const int MaximumNodes = 10_000;
    public const int MaximumMembers = 20_000;
    public const int MaximumResultRows = 18;
    public const int MaximumIdentifierLength = 128;
    public const int MaximumTextLength = 512;

    public TypedPdfJob(
        string projectName,
        int supportCount,
        int loadCaseCount,
        IEnumerable<PdfModelNode> nodes,
        IEnumerable<PdfModelMember> members,
        ViewportCapture viewport,
        IEnumerable<PdfResultRow>? resultRows = null,
        string? resultCaption = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        if (projectName.Length > MaximumTextLength)
        {
            throw new ArgumentException("The PDF project name exceeds the text limit.", nameof(projectName));
        }

        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(viewport);
        if (supportCount < 0 || loadCaseCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(supportCount));
        }

        PdfModelNode[] nodeArray = MaterializeBounded(nodes, MaximumNodes, nameof(nodes));
        PdfModelMember[] memberArray = MaterializeBounded(members, MaximumMembers, nameof(members));
        PdfResultRow[] rowArray = resultRows is null
            ? []
            : MaterializeBounded(resultRows, MaximumResultRows, nameof(resultRows));

        ValidateModel(nodeArray, memberArray);
        if (rowArray.Any(row =>
                IsInvalidIdentifier(row.Kind) || IsInvalidIdentifier(row.Id) ||
                !double.IsFinite(row.X) || !double.IsFinite(row.Y) || !double.IsFinite(row.Z)))
        {
            throw new ArgumentException("PDF result rows must have bounded IDs and finite values.", nameof(resultRows));
        }

        if (resultCaption?.Length > MaximumTextLength)
        {
            throw new ArgumentException("The PDF result caption exceeds the text limit.", nameof(resultCaption));
        }

        ProjectName = projectName;
        SupportCount = supportCount;
        LoadCaseCount = loadCaseCount;
        Nodes = Array.AsReadOnly(nodeArray);
        Members = Array.AsReadOnly(memberArray);
        Viewport = viewport;
        ResultRows = Array.AsReadOnly(rowArray);
        ResultCaption = string.IsNullOrWhiteSpace(resultCaption) ? "No result selected" : resultCaption;
    }

    public string ProjectName { get; }

    public int SupportCount { get; }

    public int LoadCaseCount { get; }

    public IReadOnlyList<PdfModelNode> Nodes { get; }

    public IReadOnlyList<PdfModelMember> Members { get; }

    public ViewportCapture Viewport { get; }

    public IReadOnlyList<PdfResultRow> ResultRows { get; }

    public string ResultCaption { get; }

    private static void ValidateModel(PdfModelNode[] nodes, PdfModelMember[] members)
    {
        HashSet<string> nodeIds = new(StringComparer.Ordinal);
        foreach (PdfModelNode node in nodes)
        {
            if (IsInvalidIdentifier(node.Id) || !nodeIds.Add(node.Id) ||
                !double.IsFinite(node.X) || !double.IsFinite(node.Y) ||
                !double.IsFinite(node.Z))
            {
                throw new ArgumentException("PDF model nodes must have unique IDs and finite coordinates.", nameof(nodes));
            }
        }

        HashSet<string> memberIds = new(StringComparer.Ordinal);
        foreach (PdfModelMember member in members)
        {
            if (IsInvalidIdentifier(member.Id) || IsInvalidIdentifier(member.NodeI) ||
                IsInvalidIdentifier(member.NodeJ) || !memberIds.Add(member.Id) ||
                !nodeIds.Contains(member.NodeI) || !nodeIds.Contains(member.NodeJ) || member.NodeI == member.NodeJ)
            {
                throw new ArgumentException("PDF model members must have unique IDs and valid node references.", nameof(members));
            }
        }
    }

    private static bool IsInvalidIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentifierLength;

    internal static T[] MaterializeBounded<T>(IEnumerable<T> source, int maximumCount, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TryGetNonEnumeratedCount(out int knownCount) && knownCount > maximumCount)
        {
            throw new ArgumentException("The PDF input exceeds its item-count limit.", parameterName);
        }

        List<T> values = knownCount >= 0
            ? new List<T>(Math.Min(knownCount, maximumCount))
            : [];
        foreach (T value in source)
        {
            if (values.Count == maximumCount)
            {
                throw new ArgumentException("The PDF input exceeds its item-count limit.", parameterName);
            }

            values.Add(value);
        }

        return values.ToArray();
    }
}

public static class ModelViewportCapture
{
    public const int DefaultWidth = 480;
    public const int DefaultHeight = 240;

    public static ViewportCapture Create(
        IEnumerable<PdfModelNode> nodes,
        IEnumerable<PdfModelMember> members,
        IEnumerable<string>? supportNodeIds = null,
        IEnumerable<string>? selectedNodeIds = null,
        int width = DefaultWidth,
        int height = DefaultHeight)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(members);
        int pixelByteLength = ViewportCapture.GetRequiredByteLength(width, height);
        PdfModelNode[] nodeArray = TypedPdfJob.MaterializeBounded(
            nodes,
            TypedPdfJob.MaximumNodes,
            nameof(nodes));
        PdfModelMember[] memberArray = TypedPdfJob.MaterializeBounded(
            members,
            TypedPdfJob.MaximumMembers,
            nameof(members));
        string[] supports = supportNodeIds is null
            ? []
            : TypedPdfJob.MaterializeBounded(
                supportNodeIds,
                TypedPdfJob.MaximumNodes,
                nameof(supportNodeIds));
        string[] selectedIds = selectedNodeIds is null
            ? []
            : TypedPdfJob.MaterializeBounded(
                selectedNodeIds,
                TypedPdfJob.MaximumNodes,
                nameof(selectedNodeIds));

        byte[] pixels = new byte[pixelByteLength];
        Array.Fill(pixels, (byte)255);
        if (nodeArray.Length == 0)
        {
            return new ViewportCapture(width, height, pixels);
        }

        Dictionary<string, PdfModelNode> nodeMap = nodeArray.ToDictionary(node => node.Id, StringComparer.Ordinal);
        Dictionary<string, PixelPoint> projected = Project(nodeArray, width, height);
        foreach (PdfModelMember member in memberArray)
        {
            if (nodeMap.ContainsKey(member.NodeI) && nodeMap.ContainsKey(member.NodeJ))
            {
                DrawLine(pixels, width, height, projected[member.NodeI], projected[member.NodeJ], 45, 68, 89);
            }
        }

        HashSet<string> supportSet = supports.ToHashSet(StringComparer.Ordinal);
        HashSet<string> selected = selectedIds.ToHashSet(StringComparer.Ordinal);
        foreach (PdfModelNode node in nodeArray)
        {
            PixelPoint point = projected[node.Id];
            (byte r, byte g, byte b) = selected.Contains(node.Id)
                ? ((byte)230, (byte)75, (byte)60)
                : ((byte)25, (byte)105, (byte)180);
            DrawSquare(pixels, width, height, point, selected.Contains(node.Id) ? 4 : 2, r, g, b);
            if (supportSet.Contains(node.Id))
            {
                DrawSupport(pixels, width, height, point);
            }
        }

        return new ViewportCapture(width, height, pixels);
    }

    private static Dictionary<string, PixelPoint> Project(
        IReadOnlyList<PdfModelNode> nodes,
        int width,
        int height)
    {
        (double X, double Y)[] coordinates = nodes
            .Select(node => (node.X + (0.32 * node.Y), node.Z + (0.18 * node.Y)))
            .ToArray();
        double minX = coordinates.Min(point => point.X);
        double maxX = coordinates.Max(point => point.X);
        double minY = coordinates.Min(point => point.Y);
        double maxY = coordinates.Max(point => point.Y);
        double spanX = Math.Max(maxX - minX, 1e-9);
        double spanY = Math.Max(maxY - minY, 1e-9);
        const double padding = 18;
        double scale = Math.Min((width - (padding * 2)) / spanX, (height - (padding * 2)) / spanY);
        Dictionary<string, PixelPoint> result = new(StringComparer.Ordinal);
        for (int index = 0; index < nodes.Count; index++)
        {
            int x = (int)Math.Round(padding + ((coordinates[index].X - minX) * scale), MidpointRounding.AwayFromZero);
            int y = height - 1 - (int)Math.Round(padding + ((coordinates[index].Y - minY) * scale), MidpointRounding.AwayFromZero);
            result.Add(nodes[index].Id, new PixelPoint(x, y));
        }

        return result;
    }

    private static void DrawLine(
        byte[] pixels,
        int width,
        int height,
        PixelPoint start,
        PixelPoint end,
        byte red,
        byte green,
        byte blue)
    {
        int x = start.X;
        int y = start.Y;
        int dx = Math.Abs(end.X - start.X);
        int sx = start.X < end.X ? 1 : -1;
        int dy = -Math.Abs(end.Y - start.Y);
        int sy = start.Y < end.Y ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            SetPixel(pixels, width, height, x, y, red, green, blue);
            if (x == end.X && y == end.Y)
            {
                return;
            }

            int doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    private static void DrawSquare(
        byte[] pixels,
        int width,
        int height,
        PixelPoint point,
        int radius,
        byte red,
        byte green,
        byte blue)
    {
        for (int y = point.Y - radius; y <= point.Y + radius; y++)
        {
            for (int x = point.X - radius; x <= point.X + radius; x++)
            {
                SetPixel(pixels, width, height, x, y, red, green, blue);
            }
        }
    }

    private static void DrawSupport(byte[] pixels, int width, int height, PixelPoint point)
    {
        PixelPoint left = new(point.X - 5, point.Y + 8);
        PixelPoint right = new(point.X + 5, point.Y + 8);
        DrawLine(pixels, width, height, point, left, 40, 145, 75);
        DrawLine(pixels, width, height, point, right, 40, 145, 75);
        DrawLine(pixels, width, height, left, right, 40, 145, 75);
    }

    private static void SetPixel(
        byte[] pixels,
        int width,
        int height,
        int x,
        int y,
        byte red,
        byte green,
        byte blue)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
        {
            return;
        }

        int offset = checked(((y * width) + x) * 3);
        pixels[offset] = red;
        pixels[offset + 1] = green;
        pixels[offset + 2] = blue;
    }

    private readonly record struct PixelPoint(int X, int Y);
}

public sealed partial class TypedPdfDocumentWriter
{
    private static readonly Encoding PdfEncoding = Encoding.Latin1;

    public async Task WriteAsync(
        TypedPdfJob job,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The PDF destination must be writable.", nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        byte[] pdf = BuildPdf(job, cancellationToken);
        await destination.WriteAsync(pdf, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildPdf(TypedPdfJob job, CancellationToken cancellationToken)
    {
        byte[] image = Compress(job.Viewport.Rgb24.Span);
        byte[] content = PdfEncoding.GetBytes(BuildPageContent(job, cancellationToken));
        List<byte[]> objects =
        [
            PdfEncoding.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            PdfEncoding.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            PdfEncoding.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                "/Resources << /Font << /F1 4 0 R >> /XObject << /Viewport 6 0 R >> >> " +
                "/Contents 5 0 R >>"),
            PdfEncoding.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            StreamObject(content, ""),
            StreamObject(
                image,
                $"/Type /XObject /Subtype /Image /Width {job.Viewport.Width.ToString(CultureInfo.InvariantCulture)} " +
                $"/Height {job.Viewport.Height.ToString(CultureInfo.InvariantCulture)} /ColorSpace /DeviceRGB " +
                "/BitsPerComponent 8 /Filter /FlateDecode "),
        ];
        using MemoryStream output = new();
        Write(output, "%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        long[] offsets = new long[objects.Count + 1];
        for (int index = 0; index < objects.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            offsets[index + 1] = output.Position;
            Write(output, $"{index + 1} 0 obj\n");
            output.Write(objects[index]);
            Write(output, "\nendobj\n");
        }

        long xref = output.Position;
        Write(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (int index = 1; index < offsets.Length; index++)
        {
            Write(output, $"{offsets[index].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        Write(output,
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n" +
            $"{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        return output.ToArray();
    }

    private static string BuildPageContent(TypedPdfJob job, CancellationToken cancellationToken)
    {
        StringBuilder builder = new();
        AddText(builder, 50, 806, 16, "FrameWeb Model Report");
        AddText(builder, 50, 784, 11, $"Project: {job.ProjectName}");
        AddText(builder, 50, 768, 10,
            $"Nodes: {job.Nodes.Count}   Members: {job.Members.Count}   " +
            $"Supports: {job.SupportCount}   Load cases: {job.LoadCaseCount}");
        builder.Append("q\n500 0 0 250 48 495 cm\n/Viewport Do\nQ\n");
        AddText(builder, 50, 478, 12, "Static result table");
        AddText(builder, 50, 461, 9, job.ResultCaption);
        AddText(builder, 50, 445, 9, "Kind / ID                                  X/Fx/Nx        Y/Fy/Vy        Z/Fz/Vz");
        double y = 429;
        if (job.ResultRows.Count == 0)
        {
            AddText(builder, 50, y, 9, "No static force result is available.");
            return builder.ToString();
        }

        foreach (PdfResultRow row in job.ResultRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddText(builder, 50, y, 8, FormatRow($"{row.Kind} {row.Id}", row.X, row.Y, row.Z));
            y -= 12;
        }

        return builder.ToString();
    }

    private static string FormatRow(string id, double x, double y, double z) =>
        string.Create(CultureInfo.InvariantCulture, $"{id,-38}{x,14:G6}{y,14:G6}{z,14:G6}");

    private static void AddText(StringBuilder builder, double x, double y, double size, string value)
    {
        builder.Append("BT /F1 ")
            .Append(size.ToString("0.###", CultureInfo.InvariantCulture))
            .Append(" Tf ")
            .Append(x.ToString("0.###", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(y.ToString("0.###", CultureInfo.InvariantCulture))
            .Append(" Td (")
            .Append(EscapePdfText(value))
            .Append(") Tj ET\n");
    }

    private static string EscapePdfText(string value)
    {
        StringBuilder result = new(value.Length);
        foreach (char character in value.Normalize(NormalizationForm.FormKC))
        {
            char safe = character is >= ' ' and <= '~' ? character : '?';
            if (safe is '(' or ')' or '\\')
            {
                result.Append('\\');
            }

            result.Append(safe);
        }

        return result.ToString();
    }

    private static byte[] Compress(ReadOnlySpan<byte> bytes)
    {
        using MemoryStream output = new();
        using (ZLibStream compressed = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressed.Write(bytes);
        }

        return output.ToArray();
    }

    private static byte[] StreamObject(byte[] bytes, string dictionaryEntries)
    {
        using MemoryStream stream = new();
        Write(stream,
            $"<< {dictionaryEntries}/Length {bytes.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
        stream.Write(bytes);
        Write(stream, "\nendstream");
        return stream.ToArray();
    }

    private static void Write(Stream destination, string value) =>
        destination.Write(PdfEncoding.GetBytes(value));
}
