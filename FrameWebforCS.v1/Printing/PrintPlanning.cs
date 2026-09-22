namespace FrameWebforCS.Printing;

public sealed class PrintLimitExceededException : InvalidOperationException
{
    public PrintLimitExceededException(string resourceName, long limit)
        : base($"The print job exceeds the {resourceName} limit of {limit}.")
    {
        ResourceName = resourceName;
        Limit = limit;
    }

    public string ResourceName { get; }

    public long Limit { get; }
}

internal sealed record PrintEngineLimitProfile(
    int MaximumPages,
    int MaximumSections,
    int MaximumTables,
    int MaximumRows,
    int MaximumCells,
    int MaximumTextCharacters,
    int MaximumImages,
    long MaximumDecodedImageBytes,
    long MaximumEncodedPdfBytes,
    long MaximumLayoutWork)
{
    public static PrintEngineLimitProfile Default { get; } = new(
        PrintEngineLimits.MaximumPages,
        PrintEngineLimits.MaximumSections,
        PrintEngineLimits.MaximumTables,
        PrintEngineLimits.MaximumRows,
        PrintEngineLimits.MaximumCells,
        PrintEngineLimits.MaximumTextCharacters,
        PrintEngineLimits.MaximumImages,
        PrintEngineLimits.MaximumDecodedImageBytes,
        PrintEngineLimits.MaximumEncodedPdfBytes,
        PrintEngineLimits.MaximumLayoutWork);

    public void Validate()
    {
        ValidateValue(MaximumPages, PrintEngineLimits.MaximumPages, nameof(MaximumPages));
        ValidateValue(MaximumSections, PrintEngineLimits.MaximumSections, nameof(MaximumSections));
        ValidateValue(MaximumTables, PrintEngineLimits.MaximumTables, nameof(MaximumTables));
        ValidateValue(MaximumRows, PrintEngineLimits.MaximumRows, nameof(MaximumRows));
        ValidateValue(MaximumCells, PrintEngineLimits.MaximumCells, nameof(MaximumCells));
        ValidateValue(MaximumTextCharacters, PrintEngineLimits.MaximumTextCharacters, nameof(MaximumTextCharacters));
        ValidateValue(MaximumImages, PrintEngineLimits.MaximumImages, nameof(MaximumImages));
        ValidateValue(
            MaximumDecodedImageBytes,
            PrintEngineLimits.MaximumDecodedImageBytes,
            nameof(MaximumDecodedImageBytes));
        ValidateValue(
            MaximumEncodedPdfBytes,
            PrintEngineLimits.MaximumEncodedPdfBytes,
            nameof(MaximumEncodedPdfBytes));
        ValidateValue(MaximumLayoutWork, PrintEngineLimits.MaximumLayoutWork, nameof(MaximumLayoutWork));
    }

    private static void ValidateValue(long value, long hardLimit, string parameterName)
    {
        if (value <= 0 || value > hardLimit)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Test limits must be positive and not exceed hard limits.");
        }
    }
}

internal sealed class PrintWorkBudget
{
    private readonly PrintEngineLimitProfile _limits;
    private long _pages;
    private long _sections;
    private long _tables;
    private long _rows;
    private long _cells;
    private long _textCharacters;
    private long _images;
    private long _decodedImageBytes;
    private long _encodedPdfBytes;
    private long _layoutWork;

    public PrintWorkBudget(PrintEngineLimitProfile limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _limits.Validate();
    }

    public void AddPage() => Add(ref _pages, 1, _limits.MaximumPages, "pages");

    public void AddSection() => Add(ref _sections, 1, _limits.MaximumSections, "sections");

    public void AddTable() => Add(ref _tables, 1, _limits.MaximumTables, "tables");

    public void AddRows(long count) => Add(ref _rows, count, _limits.MaximumRows, "rows");

    public void AddCells(long count) => Add(ref _cells, count, _limits.MaximumCells, "cells");

    public void AddText(string? value)
    {
        if (value is not null)
        {
            Add(ref _textCharacters, value.Length, _limits.MaximumTextCharacters, "text characters");
        }
    }

    public void AddImage(ViewportCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Add(ref _images, 1, _limits.MaximumImages, "images");
        Add(
            ref _decodedImageBytes,
            capture.Rgb24.Length,
            _limits.MaximumDecodedImageBytes,
            "decoded image bytes");
    }

    public void AddImage(PrintPageCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Add(ref _images, 1, _limits.MaximumImages, "images");
        Add(
            ref _decodedImageBytes,
            capture.Rgb24.Length,
            _limits.MaximumDecodedImageBytes,
            "decoded image bytes");
    }

    public void AddPreviewImageBytes(long decodedBytes)
    {
        Add(ref _images, 1, _limits.MaximumImages, "images");
        Add(ref _decodedImageBytes, decodedBytes, _limits.MaximumDecodedImageBytes, "decoded image bytes");
    }

    public void AddEncodedPdfBytes(long count) =>
        Add(ref _encodedPdfBytes, count, _limits.MaximumEncodedPdfBytes, "encoded PDF bytes");

    public void AddLayoutWork(long count = 1) =>
        Add(ref _layoutWork, count, _limits.MaximumLayoutWork, "layout work");

    public long RemainingImages => _limits.MaximumImages - _images;

    public long RemainingDecodedImageBytes => _limits.MaximumDecodedImageBytes - _decodedImageBytes;

    public long RemainingLayoutWork => _limits.MaximumLayoutWork - _layoutWork;

    public PrintWorkBudgetSnapshot Snapshot() => new(
        _pages,
        _sections,
        _tables,
        _rows,
        _cells,
        _textCharacters,
        _images,
        _decodedImageBytes,
        _encodedPdfBytes,
        _layoutWork);

    private static void Add(ref long current, long count, long limit, string resourceName)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        long next;
        try
        {
            next = checked(current + count);
        }
        catch (OverflowException)
        {
            throw new PrintLimitExceededException(resourceName, limit);
        }

        if (next > limit)
        {
            throw new PrintLimitExceededException(resourceName, limit);
        }

        current = next;
    }
}

internal sealed record PrintWorkBudgetSnapshot(
    long Pages,
    long Sections,
    long Tables,
    long Rows,
    long Cells,
    long TextCharacters,
    long Images,
    long DecodedImageBytes,
    long EncodedPdfBytes,
    long LayoutWork);

internal static class PrintLayoutPlanner
{
    public static PrintDocumentPlan Plan(PrintJob job, PrintWorkBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(budget);
        ValidateJob(job, budget, cancellationToken);

        PrintLayoutMetrics metrics = PrintLayoutMetrics.Create(job.PageSettings);
        PlannerState state = new(job.PageSettings, metrics, budget);
        for (int sectionIndex = 0; sectionIndex < job.Sections.Count; sectionIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (job.LayoutMode == PrintLayoutMode.SectionPerPage && state.HasContent)
            {
                state.NewPage();
            }

            switch (job.Sections[sectionIndex])
            {
                case PrintTextSection text:
                    AddTextSection(state, sectionIndex, text, budget);
                    break;
                case PrintTableSection table:
                    AddTableSection(state, sectionIndex, table.Table, PrintSectionKind.Table, budget);
                    break;
                case PrintDiagramSection diagram:
                    AddDiagramSection(state, sectionIndex, diagram.Diagram, budget);
                    break;
                case PrintResultSection result:
                    AddTableSection(state, sectionIndex, result.Result.Table, PrintSectionKind.Result, budget);
                    break;
                default:
                    throw new ArgumentException("Unknown print section type.", nameof(job));
            }
        }

        return state.Build(job, cancellationToken);
    }

    public static void ValidateExistingPlan(
        PrintJob job,
        PrintDocumentPlan plan,
        PrintWorkBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(budget);
        cancellationToken.ThrowIfCancellationRequested();
        PrintPlanIdentityFactory.EnsureMatches(job, plan, nameof(plan));
        ValidateJob(job, budget, cancellationToken);
        foreach (PrintPagePlan page in plan.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            budget.AddPage();
            foreach (PrintPageItemPlan item in page.Items)
            {
                budget.AddLayoutWork(Math.Max(1, item.TableRowCount + item.TextLineCount));
            }
        }
    }

    internal static void ValidateJob(PrintJob job, PrintWorkBudget budget, CancellationToken cancellationToken)
    {
        budget.AddText(job.Title);
        budget.AddText(job.PresentationLabels.PagePrefix);
        budget.AddText(job.PresentationLabels.PageSeparator);
        budget.AddText(job.PresentationLabels.PageSuffix);
        budget.AddText(job.PresentationLabels.Displacement);
        budget.AddText(job.PresentationLabels.Reaction);
        budget.AddText(job.PresentationLabels.SectionForce);
        foreach (PrintSection section in job.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            budget.AddSection();
            budget.AddLayoutWork();
            switch (section)
            {
                case PrintTextSection text:
                    budget.AddText(text.Heading);
                    budget.AddText(text.Body);
                    break;
                case PrintTableSection table:
                    ValidateTable(table.Table, budget);
                    break;
                case PrintDiagramSection diagram:
                    budget.AddText(diagram.Diagram.Caption);
                    budget.AddImage(diagram.Diagram.Capture);
                    break;
                case PrintResultSection result:
                    budget.AddText(result.Result.CaseId);
                    budget.AddText(result.Result.Provenance);
                    ValidateTable(result.Result.Table, budget);
                    break;
                default:
                    throw new ArgumentException("Unknown print section type.", nameof(job));
            }
        }
    }

    private static void ValidateTable(PrintTable table, PrintWorkBudget budget)
    {
        budget.AddTable();
        budget.AddText(table.Caption);
        budget.AddRows(table.Rows.Count);
        budget.AddCells(checked((long)table.Columns.Count * table.Rows.Count));
        budget.AddLayoutWork(checked((long)table.Columns.Count * (table.Rows.Count + 1L)));
        foreach (PrintTableColumn column in table.Columns)
        {
            budget.AddText(column.Header);
        }

        foreach (PrintTableRow row in table.Rows)
        {
            foreach (string cell in row.Cells)
            {
                budget.AddText(cell);
            }
        }
    }

    private static void AddTextSection(
        PlannerState state,
        int sectionIndex,
        PrintTextSection section,
        PrintWorkBudget budget)
    {
        double availableWidth = state.PrintableWidthPoints;
        int charactersPerLine = Math.Max(
            1,
            (int)Math.Floor(availableWidth / state.Metrics.ApproximateCharacterWidthPoints));
        IReadOnlyList<string> lines = PrintTextWrapper.Wrap(section.Body, charactersPerLine);
        int lineIndex = 0;
        bool firstChunk = true;
        do
        {
            double headingHeight = firstChunk ? state.Metrics.TextHeadingHeightPoints : 0;
            state.EnsureSpace(headingHeight + state.Metrics.TextLineHeightPoints);
            int linesThatFit = Math.Max(
                1,
                (int)Math.Floor(
                    (state.RemainingHeightPoints - headingHeight) /
                    state.Metrics.TextLineHeightPoints));
            int lineCount = Math.Min(linesThatFit, lines.Count - lineIndex);
            double height = headingHeight + (lineCount * state.Metrics.TextLineHeightPoints);
            state.AddItem(new PrintPageItemPlan(
                sectionIndex,
                PrintSectionKind.Text,
                state.TakeBounds(height),
                TextLineStart: lineIndex,
                TextLineCount: lineCount));
            budget.AddLayoutWork(Math.Max(1, lineCount));
            lineIndex += lineCount;
            firstChunk = false;
            if (lineIndex < lines.Count)
            {
                state.NewPage();
            }
        }
        while (lineIndex < lines.Count);

        budget.AddLayoutWork(section.Body.Length);
    }

    private static void AddTableSection(
        PlannerState state,
        int sectionIndex,
        PrintTable table,
        PrintSectionKind kind,
        PrintWorkBudget budget)
    {
        double firstFixedHeight = state.Metrics.TableCaptionHeightPoints + state.Metrics.TableHeaderHeightPoints;
        double continuedFixedHeight = table.RepeatHeader ? state.Metrics.TableHeaderHeightPoints : 0;
        double rowHeight = state.Metrics.TableRowHeightPoints;
        int rowIndex = 0;
        bool firstChunk = true;
        do
        {
            double fixedHeight = firstChunk ? firstFixedHeight : continuedFixedHeight;
            state.EnsureSpace(fixedHeight + (table.Rows.Count == 0 ? 0 : rowHeight));
            int rowsThatFit = table.Rows.Count == 0
                ? 0
                : Math.Max(1, (int)Math.Floor((state.RemainingHeightPoints - fixedHeight) / rowHeight));
            int rowCount = Math.Min(rowsThatFit, table.Rows.Count - rowIndex);
            double height = fixedHeight + (rowCount * rowHeight);
            state.AddItem(new PrintPageItemPlan(
                sectionIndex,
                kind,
                state.TakeBounds(height),
                rowIndex,
                rowCount,
                !firstChunk && table.RepeatHeader));
            budget.AddLayoutWork(Math.Max(1, rowCount));
            rowIndex += rowCount;
            firstChunk = false;
            if (rowIndex < table.Rows.Count)
            {
                state.NewPage();
            }
        }
        while (rowIndex < table.Rows.Count);
    }

    private static void AddDiagramSection(
        PlannerState state,
        int sectionIndex,
        PrintDiagram diagram,
        PrintWorkBudget budget)
    {
        double requested = PrintUnits.MillimetresToPoints(diagram.PreferredHeightMillimetres) * state.Metrics.Scale;
        double height = Math.Min(
            requested + state.Metrics.DiagramCaptionHeightPoints,
            state.FullContentHeightPoints);
        state.EnsureSpace(height);
        state.AddItem(new PrintPageItemPlan(
            sectionIndex,
            PrintSectionKind.Diagram,
            state.TakeBounds(height)));
        budget.AddLayoutWork(checked((long)diagram.Capture.Width * diagram.Capture.Height));
    }

    private sealed class PlannerState
    {
        private readonly List<MutablePage> _pages = [];
        private readonly PrintWorkBudget _budget;
        private double _cursorY;

        public PlannerState(
            PrintPageSettings settings,
            PrintLayoutMetrics metrics,
            PrintWorkBudget budget)
        {
            Settings = settings;
            Metrics = metrics;
            _budget = budget;
            PrintableWidthPoints = PrintUnits.MillimetresToPoints(settings.PrintableWidthMillimetres);
            double printableHeight = PrintUnits.MillimetresToPoints(settings.PrintableHeightMillimetres);
            FullContentHeightPoints = printableHeight -
                metrics.PageHeaderHeightPoints -
                metrics.PageFooterHeightPoints;
            if (FullContentHeightPoints <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "Page settings leave no content area.");
            }

            NewPage();
        }

        public PrintPageSettings Settings { get; }

        public PrintLayoutMetrics Metrics { get; }

        public double PrintableWidthPoints { get; }

        public double FullContentHeightPoints { get; }

        public double RemainingHeightPoints => FullContentHeightPoints - _cursorY;

        public bool HasContent => _pages[^1].Items.Count > 0;

        public void EnsureSpace(double requiredHeight)
        {
            if (requiredHeight > RemainingHeightPoints && HasContent)
            {
                NewPage();
            }
        }

        public PrintRectangle TakeBounds(double height)
        {
            double left = PrintUnits.MillimetresToPoints(Settings.Margins.LeftMillimetres);
            double top = PrintUnits.MillimetresToPoints(Settings.Margins.TopMillimetres);
            PrintRectangle result = new(
                left,
                top + Metrics.PageHeaderHeightPoints + _cursorY,
                PrintableWidthPoints,
                height);
            _cursorY += height + Metrics.SectionSpacingPoints;
            return result;
        }

        public void AddItem(PrintPageItemPlan item) => _pages[^1].Items.Add(item);

        public void NewPage()
        {
            _budget.AddPage();
            _pages.Add(new MutablePage(_pages.Count + 1));
            _cursorY = 0;
        }

        public PrintDocumentPlan Build(PrintJob job, CancellationToken cancellationToken)
        {
            int pageCount = _pages.Count;
            List<PrintPagePlan> pages = new(pageCount);
            foreach (MutablePage page in _pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PrintPageRenderContent content = PrintPageContentExtractor.Extract(
                    job,
                    page.PageNumber,
                    pageCount,
                    page.Items,
                    Metrics,
                    cancellationToken);
                pages.Add(new PrintPagePlan(page.PageNumber, page.Items, content));
            }

            PrintPlanIdentity identity = PrintPlanIdentityFactory.Create(job, Settings, pages);
            return new PrintDocumentPlan(Settings, pages, identity);
        }

        private sealed record MutablePage(int PageNumber)
        {
            public List<PrintPageItemPlan> Items { get; } = [];
        }
    }
}
