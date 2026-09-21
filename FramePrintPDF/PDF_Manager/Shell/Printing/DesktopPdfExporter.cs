using PDF_Manager.Core.Abstractions;
using PDF_Manager.Printing;
using PDF_Manager.Resources;

namespace PDF_Manager.Shell.Printing;

public interface ILiveViewportPrintExporter
{
    Task<PrintPreviewResult> PreviewAsync(
        PrintExportRequest request,
        PrintDiagramCaptureSet diagramCaptures,
        CancellationToken cancellationToken = default);

    Task ExportAsync(
        PrintExportRequest request,
        PrintDiagramCaptureSet diagramCaptures,
        Stream destination,
        CancellationToken cancellationToken = default);

    Task<PrintExportReceipt> ExportWithResultAsync(
        PrintExportRequest request,
        PrintDiagramCaptureSet diagramCaptures,
        Stream destination,
        CancellationToken cancellationToken = default);
}

public sealed class DesktopPdfExporter : IPrintExporter, ILiveViewportPrintExporter
{
    private readonly TypedPdfDocumentWriter _writer;
    private readonly DesktopPrintJobFactory _jobFactory;
    private readonly LocalizationService _localization;
    private readonly bool _allowHeadlessModelCapture;

    public DesktopPdfExporter(
        TypedPdfDocumentWriter? writer = null,
        bool allowHeadlessModelCapture = false,
        DesktopPrintJobFactory? jobFactory = null,
        LocalizationService? localization = null)
    {
        _writer = writer ?? new TypedPdfDocumentWriter();
        _localization = localization ?? new LocalizationService();
        _jobFactory = jobFactory ?? new DesktopPrintJobFactory(_localization);
        _allowHeadlessModelCapture = allowHeadlessModelCapture;
    }

    public PdfExportConcurrencyPolicy ConcurrencyPolicy => _writer.ConcurrencyPolicy;

    public Task<PrintPreviewResult> PreviewAsync(
        PrintExportRequest request,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(request, CreateHeadlessCaptureSet(request), cancellationToken);

    public async Task ExportAsync(
        PrintExportRequest request,
        Stream destination,
        CancellationToken cancellationToken = default) =>
        await ExportAsync(
            request,
            CreateHeadlessCaptureSet(request),
            destination,
            cancellationToken).ConfigureAwait(false);

    public async Task<PrintPreviewResult> PreviewAsync(
        PrintExportRequest request,
        PrintDiagramCaptureSet diagramCaptures,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(diagramCaptures);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            DesktopPrintJob projection = _jobFactory.CreateProjection(request, diagramCaptures);
            PrintDocumentPlan plan = _writer.Plan(projection.Job, cancellationToken);
            return await CreatePreviewAsync(
                request,
                projection,
                plan,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PrintExportException)
        {
            throw;
        }
        catch (PrintFontUnavailableException exception)
        {
            throw CreateFontException(exception);
        }
        catch (Exception exception) when (IsExpectedPrintFailure(exception))
        {
            throw new PrintExportException(
                OperationFailureKind.Validation,
                _localization["PrintPreviewFailed"],
                exception);
        }
    }

    public async Task ExportAsync(
        PrintExportRequest request,
        PrintDiagramCaptureSet diagramCaptures,
        Stream destination,
        CancellationToken cancellationToken = default) =>
        _ = await ExportWithResultAsync(
            request,
            diagramCaptures,
            destination,
            cancellationToken).ConfigureAwait(false);

    public async Task<PrintExportReceipt> ExportWithResultAsync(
        PrintExportRequest request,
        PrintDiagramCaptureSet diagramCaptures,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(diagramCaptures);
        ArgumentNullException.ThrowIfNull(destination);
        try
        {
            DesktopPrintJob projection = _jobFactory.CreateProjection(request, diagramCaptures);
            PrintDocumentPlan plan = _writer.Plan(projection.Job, cancellationToken);
            PrintExportResult result = await _writer.WriteAsync(
                projection.Job,
                plan,
                destination,
                cancellationToken).ConfigureAwait(false);
            return new PrintExportReceipt(result.PlanIdentity.Value, result.EncodedByteCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PrintExportException)
        {
            throw;
        }
        catch (PrintFontUnavailableException exception)
        {
            throw CreateFontException(exception);
        }
        catch (Exception exception) when (IsExpectedPrintFailure(exception))
        {
            throw new PrintExportException(
                OperationFailureKind.Internal,
                _localization["PrintExportFailed"],
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

    private PrintDiagramCaptureSet CreateHeadlessCaptureSet(PrintExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<PrintDiagramKind> kinds = DesktopPrintJobFactory.RequiredDiagramKinds(request);
        if (kinds.Count == 0)
        {
            return PrintDiagramCaptureSet.Empty;
        }

        if (!_allowHeadlessModelCapture)
        {
            throw new PrintExportException(
                OperationFailureKind.Validation,
                "A live viewport capture is required for desktop PDF export.");
        }

        if (kinds.Count != 1 || kinds[0] != PrintDiagramKind.Model)
        {
            throw new PrintExportException(
                OperationFailureKind.Validation,
                "Headless export can synthesize only the model diagram presentation.");
        }

        PdfModelNode[] nodes = request.Document.Nodes
            .Select(node => new PdfModelNode(node.Id, node.X, node.Y, node.Z))
            .ToArray();
        PdfModelMember[] members = request.Document.Members
            .Select(member => new PdfModelMember(member.Id, member.NodeI, member.NodeJ))
            .ToArray();
        ViewportCapture capture = ModelViewportCapture.Create(
            nodes,
            members,
            request.Document.Supports.Select(support => support.NodeId),
            request.Document.Selection.NodeIds);
        return new PrintDiagramCaptureSet(
            [new KeyValuePair<PrintDiagramKind, ViewportCapture>(kinds[0], capture)]);
    }

    private async Task<PrintPreviewResult> CreatePreviewAsync(
        PrintExportRequest request,
        DesktopPrintJob projection,
        PrintDocumentPlan plan,
        CancellationToken cancellationToken)
    {
        PrintPreviewOptions options = new(fitToDocumentBudget: true);
        PrintPreviewDocumentResult renderedDocument = await _writer.RenderPreviewAsync(
            projection.Job,
            plan,
            options,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
                renderedDocument.PlanIdentity.Value,
                plan.Identity.Value,
                StringComparison.Ordinal) ||
            renderedDocument.Pages.Count != plan.PageCount)
        {
            throw new InvalidOperationException("The rendered preview does not match its authoritative plan.");
        }

        PreflightTextContent(renderedDocument.Pages);
        List<PrintPreviewPage> pages = new(plan.PageCount);
        foreach (PrintPreviewPageResult rendered in renderedDocument.Pages)
        {
            PrintContentSection[] pageSections = rendered.Page.Items
                .Select(item => projection.SectionOrigins[item.SectionIndex])
                .Distinct()
                .ToArray();
            PrintPageCapture capture = rendered.Capture;
            pages.Add(new PrintPreviewPage(
                rendered.Page.PageNumber,
                plan.PageSettings.PageWidthMillimetres,
                plan.PageSettings.PageHeightMillimetres,
                Array.AsReadOnly(pageSections),
                new PrintPreviewCapture(capture.Width, capture.Height, capture.Rgb24.ToArray()),
                $"{rendered.PlanIdentity.Value}:{rendered.Page.PageNumber}",
                string.Join(
                    PrintPreviewPage.TextContentLineSeparator,
                    rendered.TextRuns.Select(run => run.Text))));
        }

        return new PrintPreviewResult(plan.Identity.Value, request.Sections, pages);
    }

    private static void PreflightTextContent(IReadOnlyList<PrintPreviewPageResult> pages)
    {
        long textCharacters = 0;
        foreach (PrintPreviewPageResult page in pages)
        {
            textCharacters = checked(
                textCharacters +
                ((long)Math.Max(0, page.TextRuns.Count - 1) *
                    PrintPreviewPage.TextContentLineSeparator.Length));
            foreach (PrintPreviewTextRun run in page.TextRuns)
            {
                textCharacters = checked(textCharacters + run.Text.Length);
                if (textCharacters > PrintPreviewPage.MaximumTextCharacters)
                {
                    throw new InvalidOperationException("Rendered preview text exceeds the aggregate character limit.");
                }
            }
        }
    }

    private PrintExportException CreateFontException(PrintFontUnavailableException exception) =>
        new(
            OperationFailureKind.Validation,
            _localization[exception.Language switch
            {
                PrintTextLanguage.English => "PrintFontUnavailableEnglish",
                PrintTextLanguage.Japanese => "PrintFontUnavailableJapanese",
                PrintTextLanguage.SimplifiedChinese => "PrintFontUnavailableChinese",
                _ => "PrintExportFailed",
            }],
            exception);

    private static bool IsExpectedPrintFailure(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or IOException or
            NotSupportedException or System.Security.SecurityException;
}
