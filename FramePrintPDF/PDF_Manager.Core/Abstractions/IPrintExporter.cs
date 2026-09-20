namespace PDF_Manager.Core.Abstractions;

public interface IPrintExporter
{
    Task ExportAsync(
        PrintExportRequest request,
        Stream destination,
        CancellationToken cancellationToken = default);
}
