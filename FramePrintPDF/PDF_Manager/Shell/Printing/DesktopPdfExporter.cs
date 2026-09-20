using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Printing;

namespace PDF_Manager.Shell.Printing;

public interface ILiveViewportPrintExporter
{
    Task ExportAsync(
        PrintExportRequest request,
        ViewportCapture viewportCapture,
        Stream destination,
        CancellationToken cancellationToken = default);
}

public sealed class DesktopPdfExporter : IPrintExporter, ILiveViewportPrintExporter
{
    private readonly TypedPdfDocumentWriter _writer;
    private readonly bool _allowHeadlessModelCapture;

    public DesktopPdfExporter(
        TypedPdfDocumentWriter? writer = null,
        bool allowHeadlessModelCapture = false)
    {
        _writer = writer ?? new TypedPdfDocumentWriter();
        _allowHeadlessModelCapture = allowHeadlessModelCapture;
    }

    public async Task ExportAsync(
        PrintExportRequest request,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_allowHeadlessModelCapture)
        {
            throw new PrintExportException(
                OperationFailureKind.Validation,
                "A live viewport capture is required for desktop PDF export.");
        }

        PdfModelNode[] nodes = CreateNodes(request);
        PdfModelMember[] members = CreateMembers(request);
        ViewportCapture capture = ModelViewportCapture.Create(
            nodes,
            members,
            request.Document.Supports.Select(support => support.NodeId),
            request.Document.Selection.NodeIds);
        await ExportAsync(request, capture, destination, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportAsync(
        PrintExportRequest request,
        ViewportCapture viewportCapture,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(viewportCapture);
        ArgumentNullException.ThrowIfNull(destination);
        try
        {
            PdfModelNode[] nodes = CreateNodes(request);
            PdfModelMember[] members = CreateMembers(request);
            (string caption, PdfResultRow[] rows) = CreateResultRows(request.ResultSet);
            TypedPdfJob job = new(
                request.Document.Metadata.Name,
                request.Document.Supports.Count,
                request.Document.LoadCases.Count,
                nodes,
                members,
                viewportCapture,
                rows,
                caption);
            await _writer.WriteAsync(job, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PrintExportException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException)
        {
            throw new PrintExportException(
                OperationFailureKind.Internal,
                "The PDF report could not be created.",
                exception);
        }
    }

    public static ViewportCapture CaptureBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        int byteLength = ViewportCapture.GetRequiredByteLength(bitmap.Width, bitmap.Height);
        byte[] rgb = new byte[byteLength];
        int offset = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                rgb[offset++] = pixel.R;
                rgb[offset++] = pixel.G;
                rgb[offset++] = pixel.B;
            }
        }

        return new ViewportCapture(bitmap.Width, bitmap.Height, rgb);
    }

    private static PdfModelNode[] CreateNodes(PrintExportRequest request) => request.Document.Nodes
        .Select(node => new PdfModelNode(node.Id, node.X, node.Y, node.Z))
        .ToArray();

    private static PdfModelMember[] CreateMembers(PrintExportRequest request) => request.Document.Members
        .Select(member => new PdfModelMember(member.Id, member.NodeI, member.NodeJ))
        .ToArray();

    private static (string Caption, PdfResultRow[] Rows) CreateResultRows(AnalysisResultSet? resultSet)
    {
        ForceAnalysisResult? result = resultSet?.Results.OfType<ForceAnalysisResult>().FirstOrDefault();
        if (result is null)
        {
            return ("No result selected", []);
        }

        List<PdfResultRow> rows = [];
        const int maximumNodeRows = 6;
        const int maximumReactionRows = 4;
        const int maximumMemberSegments = 4;
        rows.AddRange(result.NodeDisplacements.Take(maximumNodeRows).Select(row =>
            new PdfResultRow("D", row.NodeId, row.Components.Dx, row.Components.Dy, row.Components.Dz)));
        rows.AddRange(result.SupportReactions.Take(maximumReactionRows).Select(row =>
            new PdfResultRow("R", row.NodeId, row.Components.Fx, row.Components.Fy, row.Components.Fz)));
        int segmentCount = 0;
        foreach (MemberSectionForces member in result.MemberSectionForces)
        {
            foreach (MemberSegmentResult segment in member.Segments)
            {
                if (segmentCount == maximumMemberSegments)
                {
                    break;
                }

                rows.Add(new PdfResultRow(
                    "M-I",
                    member.MemberId,
                    segment.IEnd.Fx,
                    segment.IEnd.Fy,
                    segment.IEnd.Fz));
                rows.Add(new PdfResultRow(
                    "M-J",
                    member.MemberId,
                    segment.JEnd.Fx,
                    segment.JEnd.Fy,
                    segment.JEnd.Fz));
                segmentCount++;
            }

            if (segmentCount == maximumMemberSegments)
            {
                break;
            }
        }

        int totalSegments = result.MemberSectionForces.Sum(member => member.Segments.Count);
        bool truncated = result.NodeDisplacements.Count > maximumNodeRows ||
            result.SupportReactions.Count > maximumReactionRows ||
            totalSegments > maximumMemberSegments;
        string caption = $"Case: {result.CaseId} / {result.State.Kind}";
        if (truncated)
        {
            caption += $" / first {rows.Count} rows (truncated)";
        }

        return (caption, rows.ToArray());
    }
}
