using System.Buffers.Binary;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace FrameWebforCS.Printing;

public sealed partial class TypedPdfDocumentWriter
{
    private static readonly SemaphoreSlim ExportSemaphore = new(1, 1);
    private static readonly Lazy<InstalledWindowsFontResolver> FontInitialization =
        new(InitializeFonts, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly DateTime DeterministicPdfDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly PrintEngineLimitProfile _limits;
    private readonly IPrintExportObserver? _observer;

    public TypedPdfDocumentWriter()
        : this(PrintEngineLimitProfile.Default)
    {
    }

    internal TypedPdfDocumentWriter(PrintEngineLimitProfile limits)
        : this(limits, observer: null)
    {
    }

    internal TypedPdfDocumentWriter(PrintEngineLimitProfile limits, IPrintExportObserver? observer)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _limits.Validate();
        _observer = observer;
    }

    public PdfExportConcurrencyPolicy ConcurrencyPolicy => PdfExportConcurrencyPolicy.SerializedProcessWide;

    public PrintDocumentPlan Plan(PrintJob job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrintWorkBudget budget = new(_limits);
        return PrintLayoutPlanner.Plan(job, budget, cancellationToken);
    }

    public async Task<PrintExportResult> WriteAsync(
        PrintJob job,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        PrintDocumentPlan plan = Plan(job, cancellationToken);
        return await WriteAsync(job, plan, destination, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PrintExportResult> WriteAsync(
        PrintJob job,
        PrintDocumentPlan plan,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The PDF destination must be writable.", nameof(destination));
        }

        await ExportSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool observerEntered = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _observer?.OnEntered();
            observerEntered = true;
            InstalledWindowsFontResolver resolver = FontInitialization.Value;
            resolver.EnsureLanguage(job.Language);
            PrintWorkBudget budget = new(_limits);
            PrintLayoutPlanner.ValidateExistingPlan(job, plan, budget, cancellationToken);
            using BoundedOutputStream encoded = new(_limits.MaximumEncodedPdfBytes, budget);
            WritePdf(job, plan, encoded, budget, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            encoded.Position = 0;
            await encoded.CopyToAsync(destination, 81_920, cancellationToken).ConfigureAwait(false);
            return new PrintExportResult(plan, encoded.Length);
        }
        finally
        {
            try
            {
                if (observerEntered)
                {
                    _observer?.OnExited();
                }
            }
            finally
            {
                ExportSemaphore.Release();
            }
        }
    }

    public async Task<PrintPreviewPageResult> RenderPreviewPageAsync(
        PrintJob job,
        PrintDocumentPlan plan,
        int pageNumber,
        PrintPreviewOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(plan);
        if (pageNumber <= 0 || pageNumber > plan.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        IReadOnlyList<PrintPreviewPageResult> pages = await RenderPreviewPagesAsync(
            job,
            plan,
            [pageNumber],
            options ?? new PrintPreviewOptions(),
            cancellationToken).ConfigureAwait(false);
        return pages[0];
    }

    public async Task<PrintPreviewDocumentResult> RenderPreviewAsync(
        PrintJob job,
        PrintDocumentPlan plan,
        PrintPreviewOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(plan);
        int[] pageNumbers = Enumerable.Range(1, plan.PageCount).ToArray();
        IReadOnlyList<PrintPreviewPageResult> pages = await RenderPreviewPagesAsync(
            job,
            plan,
            pageNumbers,
            options ?? new PrintPreviewOptions(),
            cancellationToken).ConfigureAwait(false);
        return new PrintPreviewDocumentResult(plan, pages);
    }

    private async Task<IReadOnlyList<PrintPreviewPageResult>> RenderPreviewPagesAsync(
        PrintJob job,
        PrintDocumentPlan plan,
        IReadOnlyList<int> pageNumbers,
        PrintPreviewOptions options,
        CancellationToken cancellationToken)
    {
        await ExportSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        bool observerEntered = false;
        PrintWorkBudget? budget = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _observer?.OnEntered();
            observerEntered = true;
            _observer?.OnPreviewSessionStarted(pageNumbers.Count);
            budget = new PrintWorkBudget(_limits);
            PrintLayoutPlanner.ValidateExistingPlan(job, plan, budget, cancellationToken);
            _observer?.OnPreviewPlanValidated(plan.Identity);

            foreach (int pageNumber in pageNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PrintPageRasterizer.ReserveContentBudget(plan.Pages[pageNumber - 1], budget);
            }

            PrintPreviewRasterSize size = options.FitToDocumentBudget
                ? PrintPageRasterizer.SelectLargestRasterSize(
                    plan,
                    options,
                    pageNumbers.Count,
                    budget)
                : PrintPageRasterizer.GetRasterSize(plan, options);
            PrintPageRasterizer.ReserveRasterBudget(size, pageNumbers.Count, budget);

            PrintPreviewPageResult[] results = await Task.Run(
                () =>
                {
                    PrintPreviewPageResult[] rendered = new PrintPreviewPageResult[pageNumbers.Count];
                    for (int index = 0; index < pageNumbers.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        PrintPagePlan page = plan.Pages[pageNumbers[index] - 1];
                        PrintPageCapture capture = PrintPageRasterizer.Render(
                            plan,
                            page,
                            size,
                            cancellationToken);
                        rendered[index] = new PrintPreviewPageResult(plan, page, capture);
                        _observer?.OnPreviewPageRendered(page.PageNumber, capture.Rgb24.Length);
                    }

                    return rendered;
                },
                cancellationToken).ConfigureAwait(false);
            _observer?.OnPreviewSessionCompleted(budget.Snapshot());
            return Array.AsReadOnly(results);
        }
        catch (Exception failure)
        {
            if (budget is not null)
            {
                try
                {
                    _observer?.OnPreviewSessionFailed(budget.Snapshot(), failure);
                }
                catch
                {
                    // A diagnostic observer must never replace the engine's original failure.
                }
            }

            throw;
        }
        finally
        {
            try
            {
                if (observerEntered)
                {
                    _observer?.OnExited();
                }
            }
            finally
            {
                ExportSemaphore.Release();
            }
        }
    }

    private static InstalledWindowsFontResolver InitializeFonts()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Installed-font PDF export requires Windows.");
        }

        InstalledWindowsFontResolver resolver = new();
        GlobalFontSettings.FontResolver = resolver;
        return resolver;
    }

    private static void WritePdf(
        PrintJob job,
        PrintDocumentPlan plan,
        Stream destination,
        PrintWorkBudget budget,
        CancellationToken cancellationToken)
    {
        using PdfDocument document = new();
        document.Info.Title = job.Title;
        document.Info.Creator = "FrameWeb FrameWebforCS.Printing";
        document.Info.CreationDate = DeterministicPdfDate;
        document.Info.ModificationDate = DeterministicPdfDate;
        document.Options.CompressContentStreams = true;
        document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        SetDeterministicDocumentIdentifiers(document, plan.Identity);

        Dictionary<FontCacheKey, XFont> fonts = [];
        foreach (PrintPagePlan pagePlan in plan.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PdfPage page = document.AddPage();
            page.Width = XUnit.FromMillimeter(job.PageSettings.PageWidthMillimetres);
            page.Height = XUnit.FromMillimeter(job.PageSettings.PageHeightMillimetres);
            using XGraphics graphics = XGraphics.FromPdfPage(page, XGraphicsUnit.Point);
            DrawPageContent(job.Language, pagePlan, graphics, fonts, budget, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        document.Save(destination, closeStream: false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static XFont CreateFont(PrintTextLanguage language, double size, XFontStyleEx style)
    {
        string familyName = language switch
        {
            PrintTextLanguage.English => InstalledWindowsFontResolver.EnglishFamily,
            PrintTextLanguage.Japanese => InstalledWindowsFontResolver.JapaneseFamily,
            PrintTextLanguage.SimplifiedChinese => InstalledWindowsFontResolver.ChineseFamily,
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown print language."),
        };
        XPdfFontOptions options = new(PdfFontEncoding.Unicode, PdfFontEmbedding.EmbedCompleteFontFile);
        return new XFont(familyName, size, style, options);
    }

    private static void DrawPageContent(
        PrintTextLanguage language,
        PrintPagePlan pagePlan,
        XGraphics graphics,
        IDictionary<FontCacheKey, XFont> fonts,
        PrintWorkBudget budget,
        CancellationToken cancellationToken)
    {
        foreach (PrintRenderRectangle rectangle in pagePlan.RenderContent.Rectangles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            XRect bounds = ToRect(rectangle.Bounds);
            if (rectangle.Style == PrintRenderRectangleStyle.Header)
            {
                graphics.DrawRectangle(XPens.Black, XBrushes.LightGray, bounds);
            }
            else
            {
                graphics.DrawRectangle(XPens.Black, bounds);
            }
        }

        foreach (PrintRenderImage image in pagePlan.RenderContent.Images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bitmap = RgbBitmapEncoder.Encode(image.Capture);
            using MemoryStream imageStream = new(bitmap, writable: false);
            using XImage pdfImage = XImage.FromStream(imageStream);
            graphics.DrawImage(pdfImage, ToRect(image.Bounds));
        }

        foreach (PrintPreviewTextRun run in pagePlan.TextRuns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(run.DisplayText) ||
                run.Bounds.WidthPoints <= 0 ||
                run.Bounds.HeightPoints <= 0)
            {
                continue;
            }

            FontCacheKey key = new(run.FontSizePoints, run.Bold);
            if (!fonts.TryGetValue(key, out XFont? font))
            {
                font = CreateFont(
                    language,
                    run.FontSizePoints,
                    run.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                fonts.Add(key, font);
            }

            XStringFormat format = run.Alignment switch
            {
                PrintCellAlignment.Left => IsTableText(run.Kind)
                    ? XStringFormats.CenterLeft
                    : XStringFormats.TopLeft,
                PrintCellAlignment.Center => IsTableText(run.Kind)
                    ? XStringFormats.Center
                    : XStringFormats.TopCenter,
                PrintCellAlignment.Right => IsTableText(run.Kind)
                    ? XStringFormats.CenterRight
                    : XStringFormats.TopRight,
                _ => throw new ArgumentOutOfRangeException(nameof(run)),
            };
            XRect bounds = ToRect(run.Bounds);
            if (IsTableText(run.Kind))
            {
                XGraphicsState state = graphics.Save();
                try
                {
                    graphics.IntersectClip(bounds);
                    graphics.DrawString(run.DisplayText, font, XBrushes.Black, bounds, format);
                }
                finally
                {
                    graphics.Restore(state);
                }
            }
            else if (run.Kind == PrintPreviewTextKind.Body)
            {
                graphics.DrawString(
                    run.DisplayText,
                    font,
                    XBrushes.Black,
                    new XPoint(bounds.X, bounds.Y + font.Size));
            }
            else
            {
                graphics.DrawString(run.DisplayText, font, XBrushes.Black, bounds, format);
            }
        }

        budget.AddLayoutWork(pagePlan.RenderContent.RenderingWork);
    }

    private static bool IsTableText(PrintPreviewTextKind kind) =>
        kind is PrintPreviewTextKind.TableHeader or PrintPreviewTextKind.TableCell;

    private static XRect ToRect(PrintRectangle bounds) =>
        new(bounds.XPoints, bounds.YPoints, bounds.WidthPoints, bounds.HeightPoints);

    private static void SetDeterministicDocumentIdentifiers(PdfDocument document, PrintPlanIdentity identity)
    {
        document.Internals.FirstDocumentID = identity.Value;
        document.Internals.SecondDocumentID = identity.Value;
    }

    private readonly record struct FontCacheKey(double SizePoints, bool Bold);
}

internal interface IPrintExportObserver
{
    void OnEntered();

    void OnExited();

    void OnPreviewSessionStarted(int requestedPageCount)
    {
    }

    void OnPreviewPlanValidated(PrintPlanIdentity identity)
    {
    }

    void OnPreviewPageRendered(int pageNumber, long decodedBytes)
    {
    }

    void OnPreviewSessionCompleted(PrintWorkBudgetSnapshot snapshot)
    {
    }

    void OnPreviewSessionFailed(PrintWorkBudgetSnapshot snapshot, Exception exception)
    {
    }
}

internal sealed class BoundedOutputStream : MemoryStream
{
    private readonly long _maximumBytes;
    private readonly PrintWorkBudget _budget;
    private bool _writeAlreadyCharged;

    public BoundedOutputStream(long maximumBytes, PrintWorkBudget budget)
    {
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        _maximumBytes = maximumBytes;
        _budget = budget ?? throw new ArgumentNullException(nameof(budget));
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!_writeAlreadyCharged)
        {
            ValidateWrite(count);
        }

        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ValidateWrite(buffer.Length);
        _writeAlreadyCharged = true;
        try
        {
            base.Write(buffer);
        }
        finally
        {
            _writeAlreadyCharged = false;
        }
    }

    public override void WriteByte(byte value)
    {
        ValidateWrite(1);
        base.WriteByte(value);
    }

    private void ValidateWrite(int count)
    {
        long end = checked(Position + count);
        long growth = Math.Max(0, end - Length);
        if (end > _maximumBytes)
        {
            throw new PrintLimitExceededException("encoded PDF bytes", _maximumBytes);
        }

        if (growth > 0)
        {
            _budget.AddEncodedPdfBytes(growth);
        }
    }
}

internal static class RgbBitmapEncoder
{
    public static byte[] Encode(ViewportCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        int stride = checked(((capture.Width * 3) + 3) & ~3);
        int pixelBytes = checked(stride * capture.Height);
        byte[] bitmap = new byte[checked(54 + pixelBytes)];
        bitmap[0] = (byte)'B';
        bitmap[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(2, 4), bitmap.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(10, 4), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(18, 4), capture.Width);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(22, 4), capture.Height);
        BinaryPrimitives.WriteInt16LittleEndian(bitmap.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bitmap.AsSpan(28, 2), 24);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(34, 4), pixelBytes);
        ReadOnlySpan<byte> rgb = capture.Rgb24.Span;
        for (int sourceY = 0; sourceY < capture.Height; sourceY++)
        {
            int destinationY = capture.Height - 1 - sourceY;
            int sourceOffset = sourceY * capture.Width * 3;
            int destinationOffset = 54 + (destinationY * stride);
            for (int x = 0; x < capture.Width; x++)
            {
                int sourcePixel = sourceOffset + (x * 3);
                int destinationPixel = destinationOffset + (x * 3);
                bitmap[destinationPixel] = rgb[sourcePixel + 2];
                bitmap[destinationPixel + 1] = rgb[sourcePixel + 1];
                bitmap[destinationPixel + 2] = rgb[sourcePixel];
            }
        }

        return bitmap;
    }
}
