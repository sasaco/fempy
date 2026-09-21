using System.Collections.ObjectModel;

namespace FrameWebforCsharp.Printing;

public static class PrintEngineLimits
{
    public const int MaximumPages = 256;
    public const int MaximumSections = 256;
    public const int MaximumTables = 128;
    public const int MaximumColumnsPerTable = 64;
    public const int MaximumRows = 50_000;
    public const int MaximumCells = 500_000;
    public const int MaximumTextCharacters = 4_000_000;
    public const int MaximumTextLength = 16_384;
    public const int MaximumImages = 128;
    public const int MaximumImageDimension = 2_048;
    public const int DefaultPreviewMaximumDimension = 1_200;
    public const long MaximumDecodedImageBytes = 64L * 1024 * 1024;
    public const long MaximumEncodedPdfBytes = 128L * 1024 * 1024;
    public const long MaximumLayoutWork = 5_000_000;
}

public enum PrintPaperSize
{
    A4,
    A3,
}

public enum PrintTextLanguage
{
    English,
    Japanese,
    SimplifiedChinese,
}

public enum PrintCellAlignment
{
    Left,
    Center,
    Right,
}

public enum PrintPreviewTextKind
{
    Title,
    Heading,
    Body,
    TableCaption,
    TableHeader,
    TableCell,
    DiagramCaption,
    ResultContext,
    PageNumber,
}

public enum PrintDiagramKind
{
    Viewport,
    Model,
    Load,
    Result,
}

public enum PrintResultQuantity
{
    Displacement,
    Reaction,
    SectionForce,
}

public enum PrintSectionKind
{
    Text,
    Table,
    Diagram,
    Result,
}

public enum PrintLayoutMode
{
    Flow,
    SectionPerPage,
}

public enum PdfExportConcurrencyPolicy
{
    SerializedProcessWide,
}

public sealed record PrintPresentationLabels
{
    public PrintPresentationLabels(
        string pagePrefix,
        string pageSeparator,
        string pageSuffix,
        string displacement,
        string reaction,
        string sectionForce)
    {
        PagePrefix = PrintModelValidation.RequireText(pagePrefix, nameof(pagePrefix), allowEmpty: true);
        PageSeparator = PrintModelValidation.RequireText(pageSeparator, nameof(pageSeparator));
        PageSuffix = PrintModelValidation.RequireText(pageSuffix, nameof(pageSuffix), allowEmpty: true);
        Displacement = PrintModelValidation.RequireText(displacement, nameof(displacement));
        Reaction = PrintModelValidation.RequireText(reaction, nameof(reaction));
        SectionForce = PrintModelValidation.RequireText(sectionForce, nameof(sectionForce));
    }

    public string PagePrefix { get; }

    public string PageSeparator { get; }

    public string PageSuffix { get; }

    public string Displacement { get; }

    public string Reaction { get; }

    public string SectionForce { get; }

    public string FormatPageNumber(int pageNumber, int pageCount) =>
        string.Concat(
            PagePrefix,
            pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PageSeparator,
            pageCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PageSuffix);

    public string GetResultQuantity(PrintResultQuantity quantity) => quantity switch
    {
        PrintResultQuantity.Displacement => Displacement,
        PrintResultQuantity.Reaction => Reaction,
        PrintResultQuantity.SectionForce => SectionForce,
        _ => throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Unknown result quantity."),
    };

    public static PrintPresentationLabels ForLanguage(PrintTextLanguage language) => language switch
    {
        PrintTextLanguage.English => new("Page ", " / ", string.Empty, "Displacement", "Reaction", "Section force"),
        PrintTextLanguage.Japanese => new("ページ ", " / ", string.Empty, "変位", "反力", "断面力"),
        PrintTextLanguage.SimplifiedChinese => new("第 ", " / ", " 页", "位移", "反力", "截面力"),
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown print language."),
    };
}

public sealed record PrintPreviewOptions
{
    public PrintPreviewOptions(
        int maximumDimension = PrintEngineLimits.DefaultPreviewMaximumDimension,
        bool fitToDocumentBudget = false)
    {
        if (maximumDimension is <= 0 or > PrintEngineLimits.MaximumImageDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDimension),
                maximumDimension,
                $"Preview dimension must be between 1 and {PrintEngineLimits.MaximumImageDimension} pixels.");
        }

        MaximumDimension = maximumDimension;
        FitToDocumentBudget = fitToDocumentBudget;
    }

    public int MaximumDimension { get; }

    /// <summary>
    /// Allows the engine to lower <see cref="MaximumDimension"/> so every page fits the one
    /// document-wide preview budget. When false, the requested dimension is strict.
    /// </summary>
    public bool FitToDocumentBudget { get; }
}

public sealed record PrintMargins
{
    public PrintMargins(
        double leftMillimetres,
        double topMillimetres,
        double rightMillimetres,
        double bottomMillimetres)
    {
        Validate(leftMillimetres, nameof(leftMillimetres));
        Validate(topMillimetres, nameof(topMillimetres));
        Validate(rightMillimetres, nameof(rightMillimetres));
        Validate(bottomMillimetres, nameof(bottomMillimetres));
        LeftMillimetres = leftMillimetres;
        TopMillimetres = topMillimetres;
        RightMillimetres = rightMillimetres;
        BottomMillimetres = bottomMillimetres;
    }

    public double LeftMillimetres { get; }

    public double TopMillimetres { get; }

    public double RightMillimetres { get; }

    public double BottomMillimetres { get; }

    public static PrintMargins Uniform(double millimetres) =>
        new(millimetres, millimetres, millimetres, millimetres);

    private static void Validate(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Print margins must be finite and non-negative.");
        }
    }
}

public sealed record PrintPageSettings
{
    public const double MinimumScale = 0.25;
    public const double MaximumScale = 4.0;
    public const double A4WidthMillimetres = 210.0;
    public const double A4HeightMillimetres = 297.0;
    public const double A3WidthMillimetres = 297.0;
    public const double A3HeightMillimetres = 420.0;
    public const double DefaultMarginMillimetres = 10.0;

    public PrintPageSettings(
        PrintPaperSize paperSize,
        PrintPageOrientation orientation,
        PrintMargins margins,
        double scale = 1.0,
        bool showPageNumbers = true)
    {
        if (!Enum.IsDefined(paperSize))
        {
            throw new ArgumentOutOfRangeException(nameof(paperSize), paperSize, "Unknown paper size.");
        }

        if (!Enum.IsDefined(orientation))
        {
            throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Unknown page orientation.");
        }

        ArgumentNullException.ThrowIfNull(margins);
        if (!double.IsFinite(scale) || scale is < MinimumScale or > MaximumScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                scale,
                $"Print scale must be between {MinimumScale} and {MaximumScale}.");
        }

        (double portraitWidth, double portraitHeight) = paperSize switch
        {
            PrintPaperSize.A4 => (A4WidthMillimetres, A4HeightMillimetres),
            PrintPaperSize.A3 => (A3WidthMillimetres, A3HeightMillimetres),
            _ => throw new ArgumentOutOfRangeException(nameof(paperSize), paperSize, "Unknown paper size."),
        };
        PageWidthMillimetres = orientation == PrintPageOrientation.Portrait
            ? portraitWidth
            : portraitHeight;
        PageHeightMillimetres = orientation == PrintPageOrientation.Portrait
            ? portraitHeight
            : portraitWidth;
        if (margins.LeftMillimetres + margins.RightMillimetres >= PageWidthMillimetres)
        {
            throw new ArgumentOutOfRangeException(nameof(margins), "Horizontal margins leave no printable width.");
        }

        if (margins.TopMillimetres + margins.BottomMillimetres >= PageHeightMillimetres)
        {
            throw new ArgumentOutOfRangeException(nameof(margins), "Vertical margins leave no printable height.");
        }

        PaperSize = paperSize;
        Orientation = orientation;
        Margins = margins;
        Scale = scale;
        ShowPageNumbers = showPageNumbers;
    }

    public PrintPaperSize PaperSize { get; }

    public PrintPageOrientation Orientation { get; }

    public PrintMargins Margins { get; }

    public double Scale { get; }

    public bool ShowPageNumbers { get; }

    public double PageWidthMillimetres { get; }

    public double PageHeightMillimetres { get; }

    public double PrintableWidthMillimetres =>
        PageWidthMillimetres - Margins.LeftMillimetres - Margins.RightMillimetres;

    public double PrintableHeightMillimetres =>
        PageHeightMillimetres - Margins.TopMillimetres - Margins.BottomMillimetres;

    public static PrintPageSettings CreateA4(
        PrintPageOrientation orientation = PrintPageOrientation.Portrait,
        double uniformMarginMillimetres = DefaultMarginMillimetres,
        double scale = 1.0,
        bool showPageNumbers = true) =>
        new(
            PrintPaperSize.A4,
            orientation,
            PrintMargins.Uniform(uniformMarginMillimetres),
            scale,
            showPageNumbers);

    public static PrintPageSettings CreateA3(
        PrintPageOrientation orientation = PrintPageOrientation.Landscape,
        double uniformMarginMillimetres = DefaultMarginMillimetres,
        double scale = 1.0,
        bool showPageNumbers = true) =>
        new(
            PrintPaperSize.A3,
            orientation,
            PrintMargins.Uniform(uniformMarginMillimetres),
            scale,
            showPageNumbers);
}

public abstract class PrintSection
{
    protected PrintSection(PrintSectionKind kind)
    {
        Kind = kind;
    }

    public PrintSectionKind Kind { get; }
}

public sealed class PrintTextSection : PrintSection
{
    public PrintTextSection(string heading, string body)
        : base(PrintSectionKind.Text)
    {
        Heading = PrintModelValidation.RequireText(heading, nameof(heading));
        Body = PrintModelValidation.RequireText(body, nameof(body), allowEmpty: true);
    }

    public string Heading { get; }

    public string Body { get; }
}

public sealed record PrintTableColumn
{
    public PrintTableColumn(
        string header,
        double relativeWidth = 1.0,
        PrintCellAlignment alignment = PrintCellAlignment.Left)
    {
        Header = PrintModelValidation.RequireText(header, nameof(header));
        if (!double.IsFinite(relativeWidth) || relativeWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativeWidth),
                relativeWidth,
                "Column width must be finite and positive.");
        }

        if (!Enum.IsDefined(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unknown cell alignment.");
        }

        RelativeWidth = relativeWidth;
        Alignment = alignment;
    }

    public string Header { get; }

    public double RelativeWidth { get; }

    public PrintCellAlignment Alignment { get; }
}

public sealed class PrintTableRow
{
    public PrintTableRow(IEnumerable<string> cells)
    {
        Cells = PrintModelValidation.Materialize(
            cells,
            PrintEngineLimits.MaximumColumnsPerTable,
            nameof(cells),
            value => PrintModelValidation.RequireText(value, nameof(cells), allowEmpty: true));
    }

    public IReadOnlyList<string> Cells { get; }
}

public sealed class PrintTable
{
    public PrintTable(
        string caption,
        IEnumerable<PrintTableColumn> columns,
        IEnumerable<PrintTableRow> rows,
        bool repeatHeader = true)
    {
        Caption = PrintModelValidation.RequireText(caption, nameof(caption));
        Columns = PrintModelValidation.Materialize(
            columns,
            PrintEngineLimits.MaximumColumnsPerTable,
            nameof(columns));
        if (Columns.Count == 0)
        {
            throw new ArgumentException("A print table must contain at least one column.", nameof(columns));
        }

        Rows = PrintModelValidation.Materialize(rows, PrintEngineLimits.MaximumRows, nameof(rows));
        if (Rows.Any(row => row is null || row.Cells.Count != Columns.Count))
        {
            throw new ArgumentException("Each table row must match the declared column count.", nameof(rows));
        }

        RepeatHeader = repeatHeader;
    }

    public string Caption { get; }

    public IReadOnlyList<PrintTableColumn> Columns { get; }

    public IReadOnlyList<PrintTableRow> Rows { get; }

    public bool RepeatHeader { get; }
}

public sealed class PrintTableSection : PrintSection
{
    public PrintTableSection(PrintTable table)
        : base(PrintSectionKind.Table)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
    }

    public PrintTable Table { get; }
}

public sealed class PrintDiagram
{
    public PrintDiagram(
        string caption,
        ViewportCapture capture,
        PrintDiagramKind kind = PrintDiagramKind.Viewport,
        double preferredHeightMillimetres = 100.0)
    {
        Caption = PrintModelValidation.RequireText(caption, nameof(caption));
        Capture = capture ?? throw new ArgumentNullException(nameof(capture));
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown diagram kind.");
        }

        if (!double.IsFinite(preferredHeightMillimetres) || preferredHeightMillimetres <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preferredHeightMillimetres),
                preferredHeightMillimetres,
                "Diagram height must be finite and positive.");
        }

        Kind = kind;
        PreferredHeightMillimetres = preferredHeightMillimetres;
    }

    public string Caption { get; }

    public ViewportCapture Capture { get; }

    public PrintDiagramKind Kind { get; }

    public double PreferredHeightMillimetres { get; }
}

public sealed class PrintDiagramSection : PrintSection
{
    public PrintDiagramSection(PrintDiagram diagram)
        : base(PrintSectionKind.Diagram)
    {
        Diagram = diagram ?? throw new ArgumentNullException(nameof(diagram));
    }

    public PrintDiagram Diagram { get; }
}

public sealed class PrintResult
{
    public PrintResult(
        string caseId,
        PrintResultQuantity quantity,
        string? provenance,
        PrintTable table)
    {
        CaseId = PrintModelValidation.RequireText(caseId, nameof(caseId));
        if (!Enum.IsDefined(quantity))
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Unknown result quantity.");
        }

        Quantity = quantity;
        Provenance = provenance is null
            ? null
            : PrintModelValidation.RequireText(provenance, nameof(provenance), allowEmpty: true);
        Table = table ?? throw new ArgumentNullException(nameof(table));
    }

    public string CaseId { get; }

    public PrintResultQuantity Quantity { get; }

    public string? Provenance { get; }

    public PrintTable Table { get; }
}

public sealed class PrintResultSection : PrintSection
{
    public PrintResultSection(PrintResult result)
        : base(PrintSectionKind.Result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public PrintResult Result { get; }
}

public sealed class PrintJob
{
    public PrintJob(
        string title,
        PrintPageSettings pageSettings,
        PrintTextLanguage language,
        IEnumerable<PrintSection> sections,
        PrintLayoutMode layoutMode = PrintLayoutMode.Flow,
        PrintPresentationLabels? presentationLabels = null)
    {
        Title = PrintModelValidation.RequireText(title, nameof(title));
        PageSettings = pageSettings ?? throw new ArgumentNullException(nameof(pageSettings));
        if (!Enum.IsDefined(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown print language.");
        }

        Language = language;
        if (!Enum.IsDefined(layoutMode))
        {
            throw new ArgumentOutOfRangeException(nameof(layoutMode), layoutMode, "Unknown print layout mode.");
        }

        LayoutMode = layoutMode;
        PresentationLabels = presentationLabels ?? PrintPresentationLabels.ForLanguage(language);
        Sections = PrintModelValidation.Materialize(
            sections,
            PrintEngineLimits.MaximumSections,
            nameof(sections));
        if (Sections.Any(section => section is null))
        {
            throw new ArgumentException("Print sections cannot contain null values.", nameof(sections));
        }
    }

    public string Title { get; }

    public PrintPageSettings PageSettings { get; }

    public PrintTextLanguage Language { get; }

    public PrintLayoutMode LayoutMode { get; }

    public PrintPresentationLabels PresentationLabels { get; }

    public IReadOnlyList<PrintSection> Sections { get; }
}

public readonly record struct PrintRectangle(
    double XPoints,
    double YPoints,
    double WidthPoints,
    double HeightPoints);

public sealed record PrintPageItemPlan(
    int SectionIndex,
    PrintSectionKind Kind,
    PrintRectangle Bounds,
    int TableRowStart = -1,
    int TableRowCount = 0,
    bool RepeatsTableHeader = false,
    int TextLineStart = -1,
    int TextLineCount = 0);

public sealed record PrintPlanIdentity
{
    public PrintPlanIdentity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A print-plan identity must be a 64-character hexadecimal SHA-256 value.", nameof(value));
        }

        Value = value.ToUpperInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed class PrintPagePlan
{
    public PrintPagePlan(int pageNumber, IEnumerable<PrintPageItemPlan> items)
        : this(pageNumber, items, PrintPageRenderContent.Empty)
    {
    }

    internal PrintPagePlan(
        int pageNumber,
        IEnumerable<PrintPageItemPlan> items,
        PrintPageRenderContent renderContent)
    {
        if (pageNumber <= 0 || pageNumber > PrintEngineLimits.MaximumPages)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        PageNumber = pageNumber;
        Items = PrintModelValidation.Materialize(
            items,
            PrintEngineLimits.MaximumSections + PrintEngineLimits.MaximumRows,
            nameof(items));
        RenderContent = renderContent ?? throw new ArgumentNullException(nameof(renderContent));
    }

    public int PageNumber { get; }

    public IReadOnlyList<PrintPageItemPlan> Items { get; }

    public IReadOnlyList<PrintPreviewTextRun> TextRuns => RenderContent.TextRuns;

    internal PrintPageRenderContent RenderContent { get; }
}

public sealed class PrintDocumentPlan
{
    public PrintDocumentPlan(PrintPageSettings pageSettings, IEnumerable<PrintPagePlan> pages)
        : this(pageSettings, pages, identity: null)
    {
    }

    internal PrintDocumentPlan(
        PrintPageSettings pageSettings,
        IEnumerable<PrintPagePlan> pages,
        PrintPlanIdentity? identity)
    {
        PageSettings = pageSettings ?? throw new ArgumentNullException(nameof(pageSettings));
        Pages = PrintModelValidation.Materialize(pages, PrintEngineLimits.MaximumPages, nameof(pages));
        if (Pages.Count == 0)
        {
            throw new ArgumentException("A print plan must contain at least one page.", nameof(pages));
        }

        if (Pages.Where((page, index) => page.PageNumber != index + 1).Any())
        {
            throw new ArgumentException("Print plan page numbers must be consecutive.", nameof(pages));
        }


        Identity = identity ?? PrintPlanIdentityFactory.CreateStructural(PageSettings, Pages);
    }

    public PrintPageSettings PageSettings { get; }

    public IReadOnlyList<PrintPagePlan> Pages { get; }

    public PrintPlanIdentity Identity { get; }

    public int PageCount => Pages.Count;

    public double PageWidthPoints => PrintUnits.MillimetresToPoints(PageSettings.PageWidthMillimetres);

    public double PageHeightPoints => PrintUnits.MillimetresToPoints(PageSettings.PageHeightMillimetres);
}

public sealed record PrintExportResult(PrintDocumentPlan Plan, long EncodedByteCount)
{
    public PrintPlanIdentity PlanIdentity => Plan.Identity;
}

public sealed record PrintPageCapture
{
    public const int MaximumDimension = PrintEngineLimits.MaximumImageDimension;
    public const int MaximumBytes = ViewportCapture.MaximumBytes;

    public PrintPageCapture(int width, int height, ReadOnlyMemory<byte> rgb24)
        : this(width, height, rgb24, copy: true)
    {
    }

    private PrintPageCapture(int width, int height, ReadOnlyMemory<byte> rgb24, bool copy)
    {
        int expectedLength = GetRequiredByteLength(width, height);
        if (rgb24.Length != expectedLength)
        {
            throw new ArgumentException("The preview RGB capture has an invalid payload length.", nameof(rgb24));
        }

        Width = width;
        Height = height;
        Rgb24 = copy ? rgb24.ToArray() : rgb24;
    }

    public int Width { get; }

    public int Height { get; }

    public ReadOnlyMemory<byte> Rgb24 { get; }

    internal static PrintPageCapture FromOwnedRgb24(int width, int height, byte[] rgb24) =>
        new(width, height, rgb24, copy: false);

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
            throw new ArgumentOutOfRangeException(nameof(width), "The preview RGB capture exceeds the byte limit.");
        }

        return expectedLength;
    }
}

public sealed record PrintPreviewTextRun
{
    internal PrintPreviewTextRun(
        PrintPreviewTextKind kind,
        int sectionIndex,
        string text,
        string displayText,
        PrintRectangle bounds,
        double fontSizePoints,
        double displayWidthPoints,
        PrintCellAlignment alignment,
        bool bold,
        bool isTruncated)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (sectionIndex < -1 || sectionIndex >= PrintEngineLimits.MaximumSections)
        {
            throw new ArgumentOutOfRangeException(nameof(sectionIndex));
        }

        Kind = kind;
        SectionIndex = sectionIndex;
        Text = text ?? throw new ArgumentNullException(nameof(text));
        DisplayText = displayText ?? throw new ArgumentNullException(nameof(displayText));
        Bounds = bounds;
        FontSizePoints = fontSizePoints;
        DisplayWidthPoints = displayWidthPoints;
        Alignment = alignment;
        Bold = bold;
        IsTruncated = isTruncated;
    }

    public PrintPreviewTextKind Kind { get; }

    public int SectionIndex { get; }

    /// <summary>The complete, unabridged text assigned to this planned page run.</summary>
    public string Text { get; }

    /// <summary>The deterministic fitted/ellipsized text drawn by PDF and preview renderers.</summary>
    public string DisplayText { get; }

    /// <summary>The exact clip and layout rectangle consumed by both renderers.</summary>
    public PrintRectangle Bounds { get; }

    public double FontSizePoints { get; }

    /// <summary>Width returned by the engine's deterministic text measurer.</summary>
    public double DisplayWidthPoints { get; }

    public PrintCellAlignment Alignment { get; }

    public bool Bold { get; }

    public bool IsTruncated { get; }
}

public sealed record PrintPreviewPageResult
{
    internal PrintPreviewPageResult(PrintDocumentPlan plan, PrintPagePlan page, PrintPageCapture capture)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Page = page ?? throw new ArgumentNullException(nameof(page));
        Capture = capture ?? throw new ArgumentNullException(nameof(capture));
    }

    public PrintDocumentPlan Plan { get; }

    public PrintPagePlan Page { get; }

    public PrintPageCapture Capture { get; }

    public IReadOnlyList<PrintPreviewTextRun> TextRuns => Page.TextRuns;

    public PrintPlanIdentity PlanIdentity => Plan.Identity;
}

public sealed record PrintPreviewDocumentResult
{
    internal PrintPreviewDocumentResult(
        PrintDocumentPlan plan,
        IEnumerable<PrintPreviewPageResult> pages)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Pages = PrintModelValidation.Materialize(
            pages,
            PrintEngineLimits.MaximumPages,
            nameof(pages));
        if (Pages.Count != Plan.PageCount ||
            Pages.Where((page, index) =>
                page.Page.PageNumber != index + 1 ||
                !Equals(page.PlanIdentity, Plan.Identity)).Any())
        {
            throw new ArgumentException(
                "A document preview must contain every planned page in consecutive order.",
                nameof(pages));
        }
    }

    public PrintDocumentPlan Plan { get; }

    public IReadOnlyList<PrintPreviewPageResult> Pages { get; }

    public PrintPlanIdentity PlanIdentity => Plan.Identity;
}

public static class PrintUnits
{
    public const double PointsPerInch = 72.0;
    public const double MillimetresPerInch = 25.4;

    public static double MillimetresToPoints(double millimetres) =>
        millimetres * PointsPerInch / MillimetresPerInch;
}

internal static class PrintModelValidation
{
    public static string RequireText(string? value, string parameterName, bool allowEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) || value.Length > PrintEngineLimits.MaximumTextLength)
        {
            throw new ArgumentException("Print text is empty or exceeds its length limit.", parameterName);
        }

        return value;
    }

    public static IReadOnlyList<T> Materialize<T>(
        IEnumerable<T> source,
        int maximumCount,
        string parameterName,
        Func<T, T>? selector = null)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        if (source.TryGetNonEnumeratedCount(out int knownCount) && knownCount > maximumCount)
        {
            throw new ArgumentException("The print input exceeds its item-count limit.", parameterName);
        }

        List<T> values = knownCount >= 0
            ? new List<T>(Math.Min(knownCount, maximumCount))
            : [];
        foreach (T value in source)
        {
            if (values.Count == maximumCount)
            {
                throw new ArgumentException("The print input exceeds its item-count limit.", parameterName);
            }

            values.Add(selector is null ? value : selector(value));
        }

        return new ReadOnlyCollection<T>(values);
    }
}
