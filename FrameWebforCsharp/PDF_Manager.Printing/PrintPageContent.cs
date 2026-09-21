using System.Globalization;
using System.Text;

namespace FrameWebforCsharp.Printing;

internal enum PrintRenderRectangleStyle
{
    Outline,
    Header,
}

internal readonly record struct PrintRenderRectangle(
    PrintRectangle Bounds,
    PrintRenderRectangleStyle Style);

internal sealed record PrintRenderImage(ViewportCapture Capture, PrintRectangle Bounds);

internal sealed class PrintPageRenderContent
{
    public static PrintPageRenderContent Empty { get; } = new([], [], []);

    public PrintPageRenderContent(
        IEnumerable<PrintPreviewTextRun> textRuns,
        IEnumerable<PrintRenderRectangle> rectangles,
        IEnumerable<PrintRenderImage> images)
    {
        TextRuns = Array.AsReadOnly(textRuns.ToArray());
        Rectangles = Array.AsReadOnly(rectangles.ToArray());
        Images = Array.AsReadOnly(images.ToArray());
    }

    public IReadOnlyList<PrintPreviewTextRun> TextRuns { get; }

    public IReadOnlyList<PrintRenderRectangle> Rectangles { get; }

    public IReadOnlyList<PrintRenderImage> Images { get; }

    public long RenderingWork => checked(
        TextRuns.Sum(run => (long)run.DisplayText.Length) +
        Rectangles.Count +
        Images.Count);
}

internal static class PrintPageContentExtractor
{
    public static PrintPageRenderContent Extract(
        PrintJob job,
        int pageNumber,
        int pageCount,
        IReadOnlyList<PrintPageItemPlan> items,
        PrintLayoutMetrics metrics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(metrics);
        if (pageNumber <= 0 || pageNumber > pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        List<PrintPreviewTextRun> textRuns = [];
        List<PrintRenderRectangle> rectangles = [];
        List<PrintRenderImage> images = [];
        double left = PrintUnits.MillimetresToPoints(job.PageSettings.Margins.LeftMillimetres);
        double top = PrintUnits.MillimetresToPoints(job.PageSettings.Margins.TopMillimetres);
        double printableWidth = PrintUnits.MillimetresToPoints(job.PageSettings.PrintableWidthMillimetres);
        AddText(
            textRuns,
            PrintPreviewTextKind.Title,
            sectionIndex: -1,
            job.Title,
            new PrintRectangle(left, top, printableWidth, metrics.PageHeaderHeightPoints),
            metrics.TitleFontSizePoints,
            PrintCellAlignment.Left,
            bold: true);

        foreach (PrintPageItemPlan item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrintSection section = job.Sections[item.SectionIndex];
            switch (section)
            {
                case PrintTextSection text:
                    AddTextSection(textRuns, item, text, metrics, cancellationToken);
                    break;
                case PrintTableSection table:
                    AddTable(
                        textRuns,
                        rectangles,
                        item,
                        table.Table,
                        metrics,
                        resultContext: null,
                        cancellationToken);
                    break;
                case PrintDiagramSection diagram:
                    AddDiagram(textRuns, images, item, diagram.Diagram, metrics);
                    break;
                case PrintResultSection result:
                    string? resultContext = string.IsNullOrEmpty(result.Result.Provenance)
                        ? null
                        : string.Concat(
                            result.Result.CaseId,
                            " / ",
                            job.PresentationLabels.GetResultQuantity(result.Result.Quantity),
                            " / ",
                            result.Result.Provenance);
                    AddTable(
                        textRuns,
                        rectangles,
                        item,
                        result.Result.Table,
                        metrics,
                        resultContext,
                        cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("The page plan contains an unknown section type.");
            }
        }

        if (job.PageSettings.ShowPageNumbers)
        {
            double footerY = PrintUnits.MillimetresToPoints(job.PageSettings.PageHeightMillimetres) -
                PrintUnits.MillimetresToPoints(job.PageSettings.Margins.BottomMillimetres) -
                metrics.PageFooterHeightPoints;
            AddText(
                textRuns,
                PrintPreviewTextKind.PageNumber,
                sectionIndex: -1,
                job.PresentationLabels.FormatPageNumber(pageNumber, pageCount),
                new PrintRectangle(left, footerY, printableWidth, metrics.PageFooterHeightPoints),
                metrics.BodyFontSizePoints,
                PrintCellAlignment.Center,
                bold: false);
        }

        return new PrintPageRenderContent(textRuns, rectangles, images);
    }

    private static void AddTextSection(
        ICollection<PrintPreviewTextRun> textRuns,
        PrintPageItemPlan item,
        PrintTextSection section,
        PrintLayoutMetrics metrics,
        CancellationToken cancellationToken)
    {
        double headingHeight = item.TextLineStart == 0 ? metrics.TextHeadingHeightPoints : 0;
        if (headingHeight > 0)
        {
            AddText(
                textRuns,
                PrintPreviewTextKind.Heading,
                item.SectionIndex,
                section.Heading,
                new PrintRectangle(
                    item.Bounds.XPoints,
                    item.Bounds.YPoints,
                    item.Bounds.WidthPoints,
                    headingHeight),
                metrics.HeadingFontSizePoints,
                PrintCellAlignment.Left,
                bold: true);
        }

        int charactersPerLine = Math.Max(
            1,
            (int)Math.Floor(item.Bounds.WidthPoints / metrics.ApproximateCharacterWidthPoints));
        IReadOnlyList<string> lines = PrintTextWrapper.Wrap(section.Body, charactersPerLine);
        int end = checked(item.TextLineStart + item.TextLineCount);
        if (item.TextLineStart < 0 || end > lines.Count)
        {
            throw new InvalidOperationException("The text page plan contains an invalid line range.");
        }

        for (int line = item.TextLineStart; line < end; line++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double lineY = item.Bounds.YPoints + headingHeight +
                ((line - item.TextLineStart) * metrics.TextLineHeightPoints);
            AddText(
                textRuns,
                PrintPreviewTextKind.Body,
                item.SectionIndex,
                lines[line],
                new PrintRectangle(
                    item.Bounds.XPoints,
                    lineY,
                    item.Bounds.WidthPoints,
                    metrics.TextLineHeightPoints),
                metrics.BodyFontSizePoints,
                PrintCellAlignment.Left,
                bold: false);
        }
    }

    private static void AddTable(
        ICollection<PrintPreviewTextRun> textRuns,
        ICollection<PrintRenderRectangle> rectangles,
        PrintPageItemPlan item,
        PrintTable table,
        PrintLayoutMetrics metrics,
        string? resultContext,
        CancellationToken cancellationToken)
    {
        double top = item.Bounds.YPoints;
        if (item.TableRowStart == 0)
        {
            double captionWidth = resultContext is null
                ? item.Bounds.WidthPoints
                : item.Bounds.WidthPoints * 0.5;
            AddText(
                textRuns,
                PrintPreviewTextKind.TableCaption,
                item.SectionIndex,
                table.Caption,
                new PrintRectangle(
                    item.Bounds.XPoints,
                    top,
                    captionWidth,
                    metrics.TableCaptionHeightPoints),
                metrics.HeadingFontSizePoints,
                PrintCellAlignment.Left,
                bold: true);
            if (resultContext is not null)
            {
                AddText(
                    textRuns,
                    PrintPreviewTextKind.ResultContext,
                    item.SectionIndex,
                    resultContext,
                    new PrintRectangle(
                        item.Bounds.XPoints + captionWidth,
                        top,
                        item.Bounds.WidthPoints - captionWidth,
                        metrics.TableCaptionHeightPoints),
                    metrics.BodyFontSizePoints,
                    PrintCellAlignment.Right,
                    bold: false);
            }

            top += metrics.TableCaptionHeightPoints;
        }

        bool drawHeader = item.TableRowStart == 0 || item.RepeatsTableHeader;
        double totalWeight = table.Columns.Sum(column => column.RelativeWidth);
        double[] widths = table.Columns
            .Select(column => item.Bounds.WidthPoints * column.RelativeWidth / totalWeight)
            .ToArray();
        double x = item.Bounds.XPoints;
        if (drawHeader)
        {
            for (int column = 0; column < table.Columns.Count; column++)
            {
                PrintRectangle cellBounds = new(x, top, widths[column], metrics.TableHeaderHeightPoints);
                rectangles.Add(new PrintRenderRectangle(cellBounds, PrintRenderRectangleStyle.Header));
                AddText(
                    textRuns,
                    PrintPreviewTextKind.TableHeader,
                    item.SectionIndex,
                    table.Columns[column].Header,
                    Inset(cellBounds, metrics.CellHorizontalInsetPoints),
                    metrics.HeadingFontSizePoints,
                    PrintCellAlignment.Center,
                    bold: true);
                x += widths[column];
            }

            top += metrics.TableHeaderHeightPoints;
        }

        for (int rowOffset = 0; rowOffset < item.TableRowCount; rowOffset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrintTableRow row = table.Rows[item.TableRowStart + rowOffset];
            x = item.Bounds.XPoints;
            for (int column = 0; column < table.Columns.Count; column++)
            {
                PrintRectangle cellBounds = new(
                    x,
                    top + (rowOffset * metrics.TableRowHeightPoints),
                    widths[column],
                    metrics.TableRowHeightPoints);
                rectangles.Add(new PrintRenderRectangle(cellBounds, PrintRenderRectangleStyle.Outline));
                AddText(
                    textRuns,
                    PrintPreviewTextKind.TableCell,
                    item.SectionIndex,
                    row.Cells[column],
                    Inset(cellBounds, metrics.CellHorizontalInsetPoints),
                    metrics.BodyFontSizePoints,
                    table.Columns[column].Alignment,
                    bold: false);
                x += widths[column];
            }
        }
    }

    private static void AddDiagram(
        ICollection<PrintPreviewTextRun> textRuns,
        ICollection<PrintRenderImage> images,
        PrintPageItemPlan item,
        PrintDiagram diagram,
        PrintLayoutMetrics metrics)
    {
        AddText(
            textRuns,
            PrintPreviewTextKind.DiagramCaption,
            item.SectionIndex,
            diagram.Caption,
            new PrintRectangle(
                item.Bounds.XPoints,
                item.Bounds.YPoints,
                item.Bounds.WidthPoints,
                metrics.DiagramCaptionHeightPoints),
            metrics.HeadingFontSizePoints,
            PrintCellAlignment.Left,
            bold: true);
        double availableHeight = Math.Max(0, item.Bounds.HeightPoints - metrics.DiagramCaptionHeightPoints);
        double scale = Math.Min(
            item.Bounds.WidthPoints / diagram.Capture.Width,
            availableHeight / diagram.Capture.Height);
        double width = Math.Max(0, diagram.Capture.Width * scale);
        double height = Math.Max(0, diagram.Capture.Height * scale);
        images.Add(new PrintRenderImage(
            diagram.Capture,
            new PrintRectangle(
                item.Bounds.XPoints + ((item.Bounds.WidthPoints - width) / 2),
                item.Bounds.YPoints + metrics.DiagramCaptionHeightPoints,
                width,
                height)));
    }

    private static void AddText(
        ICollection<PrintPreviewTextRun> textRuns,
        PrintPreviewTextKind kind,
        int sectionIndex,
        string text,
        PrintRectangle bounds,
        double fontSizePoints,
        PrintCellAlignment alignment,
        bool bold) =>
        textRuns.Add(PrintTextLayoutPolicy.Fit(
            kind,
            sectionIndex,
            text,
            bounds,
            fontSizePoints,
            alignment,
            bold));

    private static PrintRectangle Inset(PrintRectangle bounds, double horizontalInset)
    {
        double inset = Math.Min(Math.Max(0, horizontalInset), bounds.WidthPoints / 2);
        return new PrintRectangle(
            bounds.XPoints + inset,
            bounds.YPoints,
            Math.Max(0, bounds.WidthPoints - (2 * inset)),
            bounds.HeightPoints);
    }
}

internal static class PrintTextLayoutPolicy
{
    public static PrintPreviewTextRun Fit(
        PrintPreviewTextKind kind,
        int sectionIndex,
        string text,
        PrintRectangle bounds,
        double requestedFontSizePoints,
        PrintCellAlignment alignment,
        bool bold)
    {
        ArgumentNullException.ThrowIfNull(text);
        string normalized = NormalizeSingleLine(text);
        double availableWidth = Math.Max(0, bounds.WidthPoints);
        double heightLimitedSize = Math.Max(0.1, bounds.HeightPoints);
        double fontSize = Math.Min(requestedFontSizePoints, heightLimitedSize);
        double fullWidth = Measure(normalized, fontSize, bold);
        string displayText = normalized;
        bool isTruncated = !string.Equals(normalized, text, StringComparison.Ordinal);
        if (fullWidth > availableWidth && fullWidth > 0)
        {
            double minimumFontSize = Math.Min(fontSize, Math.Max(0.75, requestedFontSizePoints * 0.35));
            double fittedSize = fontSize * availableWidth / fullWidth;
            if (fittedSize >= minimumFontSize)
            {
                fontSize = fittedSize;
            }
            else
            {
                fontSize = minimumFontSize;
                displayText = Ellipsize(normalized, availableWidth, fontSize, bold);
                isTruncated = !string.Equals(displayText, text, StringComparison.Ordinal);
            }
        }

        // Bound the number of distinct PDF font instances while always rounding down so
        // quantization can never turn a fitted value back into an overflowing value.
        fontSize = Math.Max(0.1, Math.Floor((fontSize * 20) + 1e-9) / 20);

        double displayWidth = Math.Min(availableWidth, Measure(displayText, fontSize, bold));
        return new PrintPreviewTextRun(
            kind,
            sectionIndex,
            text,
            displayText,
            bounds,
            fontSize,
            displayWidth,
            alignment,
            bold,
            isTruncated);
    }

    internal static double Measure(string value, double fontSizePoints, bool bold)
    {
        ArgumentNullException.ThrowIfNull(value);
        double units = 0;
        foreach (Rune rune in value.EnumerateRunes())
        {
            units += GetAdvanceUnits(rune);
        }

        return units * fontSizePoints * (bold ? 1.035 : 1.0);
    }

    private static string Ellipsize(string value, double availableWidth, double fontSizePoints, bool bold)
    {
        const string Ellipsis = "…";
        double ellipsisWidth = Measure(Ellipsis, fontSizePoints, bold);
        if (ellipsisWidth > availableWidth)
        {
            return string.Empty;
        }

        StringBuilder result = new();
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(value);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            string candidate = string.Concat(result.ToString(), element, Ellipsis);
            if (Measure(candidate, fontSizePoints, bold) > availableWidth)
            {
                break;
            }

            result.Append(element);
        }

        return string.Concat(result.ToString(), Ellipsis);
    }

    private static string NormalizeSingleLine(string value) =>
        value.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');

    private static double GetAdvanceUnits(Rune rune)
    {
        if (Rune.IsWhiteSpace(rune))
        {
            return 0.35;
        }

        if (rune.IsAscii)
        {
            char character = (char)rune.Value;
            if (char.IsUpper(character))
            {
                return 1.05;
            }

            if (char.IsLower(character))
            {
                return 0.82;
            }

            if (char.IsDigit(character))
            {
                return 0.78;
            }

            return char.IsPunctuation(character) ? 0.65 : 0.85;
        }

        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
            ? 0
            : category is UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or UnicodeCategory.OtherPunctuation
                ? 0.65
                : 1.0;
    }
}
