using System.Text;
using FrameWebforCS.Core.Results;

namespace FrameWebforCS.Shell.Viewport;

public sealed class ResultExportArtifact
{
    private ResultExportArtifact(
        string suggestedFileName,
        string contentType,
        int rowCount,
        ReadOnlyMemory<byte> utf8Bytes)
    {
        SuggestedFileName = suggestedFileName;
        ContentType = contentType;
        RowCount = rowCount;
        Utf8Bytes = utf8Bytes;
    }

    public string SuggestedFileName { get; }

    public string ContentType { get; }

    public int RowCount { get; }

    public int ByteCount => Utf8Bytes.Length;

    public ReadOnlyMemory<byte> Utf8Bytes { get; }

    public string Text => Encoding.UTF8.GetString(Utf8Bytes.Span);

    public void WriteTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(Utf8Bytes.Span);
    }

    public ValueTask WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.WriteAsync(Utf8Bytes, cancellationToken);
    }

    internal static ResultExportArtifact From(ResultCsvExport export)
    {
        ArgumentNullException.ThrowIfNull(export);
        return new ResultExportArtifact(
            export.SuggestedFileName,
            export.ContentType,
            export.RowCount,
            export.Utf8Bytes);
    }

    internal static ResultExportArtifact From(ResultPickupFixedWidthExport export)
    {
        ArgumentNullException.ThrowIfNull(export);
        return new ResultExportArtifact(
            export.SuggestedFileName,
            export.ContentType,
            export.RowCount,
            export.Utf8Bytes);
    }
}
