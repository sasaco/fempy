namespace PDF_Manager.Core.Abstractions;

public interface IPrintExporter
{
    Task<PrintPreviewResult> PreviewAsync(
        PrintExportRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromException<PrintPreviewResult>(new PrintExportException(
            OperationFailureKind.Unavailable,
            "Print preview is not available."));

    Task ExportAsync(
        PrintExportRequest request,
        Stream destination,
        CancellationToken cancellationToken = default);
}
