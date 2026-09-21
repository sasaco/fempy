using FrameWebforCsharp.Core.Analysis;
using FrameWebforCsharp.Core.Documents;
using FrameWebforCsharp.Core.Results;

namespace FrameWebforCsharp.Core.Abstractions;

public enum OperationFailureKind
{
    Validation,
    Unavailable,
    Unauthorized,
    Timeout,
    Protocol,
    Internal,
}

public abstract class CoreOperationException : Exception
{
    protected CoreOperationException(
        OperationFailureKind failureKind,
        string userMessage,
        Exception? innerException = null)
        : base(userMessage, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        FailureKind = failureKind;
        UserMessage = userMessage;
    }

    public OperationFailureKind FailureKind { get; }

    public string UserMessage { get; }
}

public sealed class AnalysisClientException : CoreOperationException
{
    public AnalysisClientException(
        OperationFailureKind failureKind,
        string userMessage,
        Exception? innerException = null)
        : base(failureKind, userMessage, innerException)
    {
    }
}

public sealed class PrintExportException : CoreOperationException
{
    public PrintExportException(
        OperationFailureKind failureKind,
        string userMessage,
        Exception? innerException = null)
        : base(failureKind, userMessage, innerException)
    {
    }
}

public enum PrintPaperSize
{
    A4,
    A3,
}

public enum PrintPageOrientation
{
    Portrait,
    Landscape,
}

public enum PrintLayoutChoice
{
    Continuous,
    SectionPerPage,
}

public enum PrintContentLanguage
{
    English,
    Japanese,
    SimplifiedChinese,
}

public enum PrintContentSection
{
    ProjectSummary,
    InputTables,
    ModelDiagram,
    LoadDiagram,
    DisplacementResults,
    ReactionResults,
    MemberForceResults,
    ResultDiagram,
}

public sealed record PrintPageSettings
{
    public const double DefaultMarginMillimetres = 10;
    public const double MinimumScale = 0.25;
    public const double MaximumScale = 4;

    public PrintPageSettings(
        PrintPaperSize paperSize,
        PrintPageOrientation orientation,
        double leftMarginMillimetres = DefaultMarginMillimetres,
        double topMarginMillimetres = DefaultMarginMillimetres,
        double rightMarginMillimetres = DefaultMarginMillimetres,
        double bottomMarginMillimetres = DefaultMarginMillimetres,
        double scale = 1,
        PrintLayoutChoice layout = PrintLayoutChoice.Continuous,
        bool showPageNumbers = true)
    {
        if (!Enum.IsDefined(paperSize)) throw new ArgumentOutOfRangeException(nameof(paperSize));
        if (!Enum.IsDefined(orientation)) throw new ArgumentOutOfRangeException(nameof(orientation));
        if (!Enum.IsDefined(layout)) throw new ArgumentOutOfRangeException(nameof(layout));
        ValidateMargin(leftMarginMillimetres, nameof(leftMarginMillimetres));
        ValidateMargin(topMarginMillimetres, nameof(topMarginMillimetres));
        ValidateMargin(rightMarginMillimetres, nameof(rightMarginMillimetres));
        ValidateMargin(bottomMarginMillimetres, nameof(bottomMarginMillimetres));
        if (!double.IsFinite(scale) || scale is < MinimumScale or > MaximumScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                scale,
                $"Print scale must be between {MinimumScale} and {MaximumScale}.");
        }

        (double width, double height) = paperSize switch
        {
            PrintPaperSize.A4 => (210, 297),
            PrintPaperSize.A3 => (297, 420),
            _ => throw new ArgumentOutOfRangeException(nameof(paperSize)),
        };
        if (orientation == PrintPageOrientation.Landscape)
        {
            (width, height) = (height, width);
        }

        if (leftMarginMillimetres + rightMarginMillimetres >= width ||
            topMarginMillimetres + bottomMarginMillimetres >= height)
        {
            throw new ArgumentException("Print margins must leave a positive printable area.");
        }

        PaperSize = paperSize;
        Orientation = orientation;
        LeftMarginMillimetres = leftMarginMillimetres;
        TopMarginMillimetres = topMarginMillimetres;
        RightMarginMillimetres = rightMarginMillimetres;
        BottomMarginMillimetres = bottomMarginMillimetres;
        Scale = scale;
        Layout = layout;
        ShowPageNumbers = showPageNumbers;
    }

    public PrintPaperSize PaperSize { get; }

    public PrintPageOrientation Orientation { get; }

    public double LeftMarginMillimetres { get; }

    public double TopMarginMillimetres { get; }

    public double RightMarginMillimetres { get; }

    public double BottomMarginMillimetres { get; }

    public double Scale { get; }

    public PrintLayoutChoice Layout { get; }

    public bool ShowPageNumbers { get; }

    public static PrintPageSettings Default { get; } = new(
        PrintPaperSize.A4,
        PrintPageOrientation.Portrait);

    private static void ValidateMargin(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Print margins must be finite and non-negative.");
        }
    }
}

public sealed class PrintResultSelection
{
    public const int MaximumProvenanceLength = 2_048;

    public PrintResultSelection(
        ResultTableSet tables,
        ResultCoordinate? coordinate = null,
        string? provenance = null,
        bool isMovingLoad = false)
    {
        Tables = tables ?? throw new ArgumentNullException(nameof(tables));
        if (provenance?.Length > MaximumProvenanceLength)
        {
            throw new ArgumentException("Print result provenance exceeds the text limit.", nameof(provenance));
        }

        if (coordinate is ResultCoordinate requestedCoordinate && tables.Coordinate != requestedCoordinate)
        {
            throw new ArgumentException(
                "The selected print coordinate does not match the result tables.",
                nameof(coordinate));
        }

        Coordinate = coordinate ?? tables.Coordinate;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? null : provenance;
        IsMovingLoad = isMovingLoad;
    }

    public ResultTableSet Tables { get; }

    public ResultCoordinate? Coordinate { get; }

    public string? Provenance { get; }

    public bool IsMovingLoad { get; }
}

public sealed class PrintPreviewPage
{
    public const int MaximumIdentityLength = 256;
    public const int MaximumTextCharacters = 8_000_000;
    public const string TextContentLineSeparator = "\r\n";

    public PrintPreviewPage(
        int pageNumber,
        double widthMillimetres,
        double heightMillimetres,
        IEnumerable<PrintContentSection> sections,
        PrintPreviewCapture renderedPageCapture,
        string pageIdentity,
        string textContent)
    {
        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        if (!double.IsFinite(widthMillimetres) || widthMillimetres <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthMillimetres));
        }

        if (!double.IsFinite(heightMillimetres) || heightMillimetres <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightMillimetres));
        }

        PageNumber = pageNumber;
        WidthMillimetres = widthMillimetres;
        HeightMillimetres = heightMillimetres;
        Sections = Array.AsReadOnly(MaterializeSections(sections));
        RenderedPageCapture = renderedPageCapture
            ?? throw new ArgumentNullException(nameof(renderedPageCapture));
        PageIdentity = RequireIdentity(pageIdentity, nameof(pageIdentity));
        ArgumentNullException.ThrowIfNull(textContent);
        if (textContent.Length > MaximumTextCharacters)
        {
            throw new ArgumentException("Preview page text exceeds its character limit.", nameof(textContent));
        }

        TextContent = textContent;
    }

    public int PageNumber { get; }

    public double WidthMillimetres { get; }

    public double HeightMillimetres { get; }

    public IReadOnlyList<PrintContentSection> Sections { get; }

    public PrintPreviewCapture RenderedPageCapture { get; }

    public string PageIdentity { get; }

    public string TextContent { get; }

    private static PrintContentSection[] MaterializeSections(IEnumerable<PrintContentSection> sections)
    {
        PrintContentSection[] values = sections?.ToArray()
            ?? throw new ArgumentNullException(nameof(sections));
        if (values.Any(value => !Enum.IsDefined(value)) || values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException("Preview page sections must be defined and unique.", nameof(sections));
        }

        return values;
    }

    internal static string RequireIdentity(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentityLength)
        {
            throw new ArgumentException("Print preview identity is empty or exceeds its length limit.", parameterName);
        }

        return value;
    }
}

public sealed class PrintPreviewCapture
{
    public const int MaximumDimension = 2_048;
    public const long MaximumDecodedBytes = 64L * 1024 * 1024;
    private readonly byte[] _rgb24;

    public PrintPreviewCapture(int width, int height, IEnumerable<byte> rgb24)
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
        if (expectedLength > MaximumDecodedBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(rgb24), "Rendered preview exceeds the decoded-image limit.");
        }

        ArgumentNullException.ThrowIfNull(rgb24);
        _rgb24 = rgb24.Take(expectedLength + 1).ToArray();
        if (_rgb24.Length != expectedLength)
        {
            throw new ArgumentException("Rendered preview must contain exactly RGB24 width * height * 3 bytes.", nameof(rgb24));
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public ReadOnlyMemory<byte> Rgb24 => _rgb24;
}

public sealed class PrintPreviewResult
{
    public PrintPreviewResult(
        string planIdentity,
        IEnumerable<PrintContentSection> sections,
        IEnumerable<PrintPreviewPage> pages)
    {
        PrintContentSection[] sectionArray = MaterializeSections(sections, allowEmpty: true);
        PrintPreviewPage[] pageArray = pages?.ToArray() ?? throw new ArgumentNullException(nameof(pages));
        PlanIdentity = PrintPreviewPage.RequireIdentity(planIdentity, nameof(planIdentity));
        if (pageArray.Length == 0 || pageArray.Length > 256 ||
            pageArray.Where((page, index) => page.PageNumber != index + 1).Any() ||
            pageArray.Any(page =>
                !double.IsFinite(page.WidthMillimetres) || page.WidthMillimetres <= 0 ||
                !double.IsFinite(page.HeightMillimetres) || page.HeightMillimetres <= 0 ||
                !string.Equals(
                    page.PageIdentity,
                    $"{PlanIdentity}:{page.PageNumber}",
                    StringComparison.Ordinal) ||
                page.Sections.Except(sectionArray).Any()))
        {
            throw new ArgumentException("Print preview pages are not a valid ordered page set.", nameof(pages));
        }

        long decodedBytes = 0;
        long textCharacters = 0;
        foreach (PrintPreviewPage page in pageArray)
        {
            decodedBytes = checked(decodedBytes + page.RenderedPageCapture.Rgb24.Length);
            if (decodedBytes > PrintPreviewCapture.MaximumDecodedBytes)
            {
                throw new ArgumentException("Rendered preview pages exceed the decoded-image limit.", nameof(pages));
            }

            textCharacters = checked(textCharacters + page.TextContent.Length);
            if (textCharacters > PrintPreviewPage.MaximumTextCharacters)
            {
                throw new ArgumentException("Preview page text exceeds the aggregate character limit.", nameof(pages));
            }
        }

        Sections = Array.AsReadOnly(sectionArray);
        Pages = Array.AsReadOnly(pageArray);
    }

    public string PlanIdentity { get; }

    public int PageCount => Pages.Count;

    public IReadOnlyList<PrintContentSection> Sections { get; }

    public IReadOnlyList<PrintPreviewPage> Pages { get; }

    internal static PrintContentSection[] MaterializeSections(
        IEnumerable<PrintContentSection>? sections,
        bool allowEmpty = false)
    {
        if (sections is null)
        {
            return allowEmpty ? [] : Enum.GetValues<PrintContentSection>();
        }

        PrintContentSection[] values = sections.ToArray();
        if ((!allowEmpty && values.Length == 0) ||
            values.Any(section => !Enum.IsDefined(section)) ||
            values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException("Print sections must be non-empty, defined, and unique.", nameof(sections));
        }

        return values;
    }
}

public sealed record PrintExportReceipt
{
    public PrintExportReceipt(string planIdentity, long encodedByteCount)
    {
        PlanIdentity = PrintPreviewPage.RequireIdentity(planIdentity, nameof(planIdentity));
        if (encodedByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(encodedByteCount));
        }

        EncodedByteCount = encodedByteCount;
    }

    public string PlanIdentity { get; }

    public long EncodedByteCount { get; }
}

public sealed class PrintExportRequest
{
    public const int MaximumSelectedResultCount = 10_000;
    private static readonly PrintContentSection[] DefaultSections =
    [
        PrintContentSection.ProjectSummary,
        PrintContentSection.InputTables,
        PrintContentSection.ModelDiagram,
        PrintContentSection.LoadDiagram,
    ];

    public PrintExportRequest(
        ProjectDocument document,
        AnalysisResultSet? resultSet,
        IEnumerable<ResultCoordinate>? selectedResults = null,
        PrintPageSettings? pageSettings = null,
        IEnumerable<PrintContentSection>? sections = null,
        PrintResultSelection? selectedResult = null,
        PrintContentLanguage language = PrintContentLanguage.English)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        ProjectDocumentValidator.Validate(document);
        ResultSet = resultSet;
        if (resultSet is not null)
        {
            AnalysisResultSetValidator.Validate(resultSet);
        }

        ResultCoordinate[] selection = MaterializeSelection(selectedResults);
        PrintContentSection[] selectedSections = PrintPreviewResult.MaterializeSections(
            sections ?? DefaultSections);
        if (resultSet is null && selection.Length > 0)
        {
            throw new ArgumentException("Selected results require an AnalysisResultSet.", nameof(selectedResults));
        }

        if (resultSet is null && selectedResult is not null)
        {
            throw new ArgumentException("A selected result requires an AnalysisResultSet.", nameof(selectedResult));
        }

        if (!Enum.IsDefined(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language));
        }

        if (selectedResult is null && selection.Length == 0 && selectedSections.Any(section => section is
                PrintContentSection.DisplacementResults or
                PrintContentSection.ReactionResults or
                PrintContentSection.MemberForceResults or
                PrintContentSection.ResultDiagram))
        {
            throw new ArgumentException("Result print sections require an explicit current result selection.", nameof(sections));
        }

        if (resultSet is not null)
        {
            ResultIndex index = new(resultSet);
            foreach (ResultCoordinate coordinate in selection)
            {
                if (!index.TryGet(coordinate, out _))
                {
                    throw new ArgumentException(
                        $"Selected result '{coordinate}' is not present in the result set.",
                        nameof(selectedResults));
                }
            }


            if (selectedResult?.Coordinate is ResultCoordinate selectedCoordinate &&
                !index.TryGet(selectedCoordinate, out _))
            {
                throw new ArgumentException(
                    $"Selected result '{selectedCoordinate}' is not present in the result set.",
                    nameof(selectedResult));
            }
        }

        SelectedResults = Array.AsReadOnly(selection);
        PageSettings = pageSettings ?? PrintPageSettings.Default;
        Sections = Array.AsReadOnly(selectedSections);
        SelectedResult = selectedResult;
        Language = language;
    }

    public ProjectDocument Document { get; }

    public AnalysisResultSet? ResultSet { get; }

    public IReadOnlyList<ResultCoordinate> SelectedResults { get; }

    public PrintPageSettings PageSettings { get; }

    public IReadOnlyList<PrintContentSection> Sections { get; }

    public PrintResultSelection? SelectedResult { get; }

    public PrintContentLanguage Language { get; }

    private static ResultCoordinate[] MaterializeSelection(
        IEnumerable<ResultCoordinate>? selectedResults)
    {
        if (selectedResults is null)
        {
            return [];
        }

        List<ResultCoordinate> selection = [];
        foreach (ResultCoordinate coordinate in selectedResults)
        {
            if (selection.Count >= MaximumSelectedResultCount)
            {
                throw new ArgumentException(
                    $"Selected results cannot exceed {MaximumSelectedResultCount} entries.",
                    nameof(selectedResults));
            }

            selection.Add(coordinate);
        }

        return selection.ToArray();
    }
}
