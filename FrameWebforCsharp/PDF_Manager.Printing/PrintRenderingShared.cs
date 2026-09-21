using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PDF_Manager.Printing;

internal sealed record PrintLayoutMetrics
{
    private PrintLayoutMetrics(PrintPageSettings settings)
    {
        Scale = settings.Scale;
        PageHeaderHeightPoints = 24.0 * Scale;
        // Keep pagination stable when the footer is hidden; visibility must not
        // move rows between pages or change preview/export plan identity.
        PageFooterHeightPoints = 16.0 * Scale;
        SectionSpacingPoints = 8.0 * Scale;
        TextHeadingHeightPoints = 18.0 * Scale;
        TextLineHeightPoints = 12.0 * Scale;
        TableCaptionHeightPoints = 18.0 * Scale;
        TableHeaderHeightPoints = 18.0 * Scale;
        TableRowHeightPoints = 16.0 * Scale;
        DiagramCaptionHeightPoints = 18.0 * Scale;
        BodyFontSizePoints = 9.0 * Scale;
        HeadingFontSizePoints = 9.0 * Scale;
        TitleFontSizePoints = 13.0 * Scale;
        ApproximateCharacterWidthPoints = 6.0 * Scale;
        CellHorizontalInsetPoints = 2.0 * Scale;
    }

    public double Scale { get; }

    public double PageHeaderHeightPoints { get; }

    public double PageFooterHeightPoints { get; }

    public double SectionSpacingPoints { get; }

    public double TextHeadingHeightPoints { get; }

    public double TextLineHeightPoints { get; }

    public double TableCaptionHeightPoints { get; }

    public double TableHeaderHeightPoints { get; }

    public double TableRowHeightPoints { get; }

    public double DiagramCaptionHeightPoints { get; }

    public double BodyFontSizePoints { get; }

    public double HeadingFontSizePoints { get; }

    public double TitleFontSizePoints { get; }

    public double ApproximateCharacterWidthPoints { get; }

    public double CellHorizontalInsetPoints { get; }

    public static PrintLayoutMetrics Create(PrintPageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new PrintLayoutMetrics(settings);
    }
}

internal static class PrintTextWrapper
{
    public static IReadOnlyList<string> Wrap(string value, int charactersPerLine)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (charactersPerLine <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(charactersPerLine));
        }

        List<string> result = [];
        foreach (string paragraph in value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            for (int offset = 0; offset < paragraph.Length; offset += charactersPerLine)
            {
                result.Add(paragraph.Substring(offset, Math.Min(charactersPerLine, paragraph.Length - offset)));
            }
        }

        return result.Count == 0 ? [string.Empty] : result;
    }
}

internal static class PrintPlanIdentityFactory
{
    public static PrintPlanIdentity Create(PrintJob job, PrintPageSettings settings, IReadOnlyList<PrintPagePlan> pages)
    {
        ArgumentNullException.ThrowIfNull(job);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AddText(hash, "FrameWeb.PrintPlan.v1");
        AddJob(hash, job);
        AddPlan(hash, settings, pages);
        return new PrintPlanIdentity(Convert.ToHexString(hash.GetHashAndReset()));
    }

    public static PrintPlanIdentity CreateStructural(PrintPageSettings settings, IReadOnlyList<PrintPagePlan> pages)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AddText(hash, "FrameWeb.StructuralPrintPlan.v1");
        AddPlan(hash, settings, pages);
        return new PrintPlanIdentity(Convert.ToHexString(hash.GetHashAndReset()));
    }

    public static void EnsureMatches(PrintJob job, PrintDocumentPlan plan, string parameterName)
    {
        if (!Equals(job.PageSettings, plan.PageSettings))
        {
            throw new ArgumentException("The print plan page settings do not match the supplied immutable print job.", parameterName);
        }

        PrintPlanIdentity expected = Create(job, plan.PageSettings, plan.Pages);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected.Value),
                Convert.FromHexString(plan.Identity.Value)))
        {
            throw new ArgumentException("The print plan does not belong to the supplied immutable print job.", parameterName);
        }
    }

    private static void AddJob(IncrementalHash hash, PrintJob job)
    {
        AddText(hash, job.Title);
        AddInt32(hash, (int)job.Language);
        AddInt32(hash, (int)job.LayoutMode);
        AddText(hash, job.PresentationLabels.PagePrefix);
        AddText(hash, job.PresentationLabels.PageSeparator);
        AddText(hash, job.PresentationLabels.PageSuffix);
        AddText(hash, job.PresentationLabels.Displacement);
        AddText(hash, job.PresentationLabels.Reaction);
        AddText(hash, job.PresentationLabels.SectionForce);
        AddInt32(hash, job.Sections.Count);
        foreach (PrintSection section in job.Sections)
        {
            AddInt32(hash, (int)section.Kind);
            switch (section)
            {
                case PrintTextSection text:
                    AddText(hash, text.Heading);
                    AddText(hash, text.Body);
                    break;
                case PrintTableSection table:
                    AddTable(hash, table.Table);
                    break;
                case PrintDiagramSection diagram:
                    AddText(hash, diagram.Diagram.Caption);
                    AddInt32(hash, (int)diagram.Diagram.Kind);
                    AddDouble(hash, diagram.Diagram.PreferredHeightMillimetres);
                    AddInt32(hash, diagram.Diagram.Capture.Width);
                    AddInt32(hash, diagram.Diagram.Capture.Height);
                    hash.AppendData(diagram.Diagram.Capture.Rgb24.Span);
                    break;
                case PrintResultSection result:
                    AddText(hash, result.Result.CaseId);
                    AddInt32(hash, (int)result.Result.Quantity);
                    AddText(hash, result.Result.Provenance ?? string.Empty);
                    AddTable(hash, result.Result.Table);
                    break;
            }
        }
    }

    private static void AddTable(IncrementalHash hash, PrintTable table)
    {
        AddText(hash, table.Caption);
        AddInt32(hash, table.RepeatHeader ? 1 : 0);
        AddInt32(hash, table.Columns.Count);
        foreach (PrintTableColumn column in table.Columns)
        {
            AddText(hash, column.Header);
            AddDouble(hash, column.RelativeWidth);
            AddInt32(hash, (int)column.Alignment);
        }

        AddInt32(hash, table.Rows.Count);
        foreach (PrintTableRow row in table.Rows)
        {
            foreach (string cell in row.Cells)
            {
                AddText(hash, cell);
            }
        }
    }

    private static void AddPlan(
        IncrementalHash hash,
        PrintPageSettings settings,
        IReadOnlyList<PrintPagePlan> pages)
    {
        AddInt32(hash, (int)settings.PaperSize);
        AddInt32(hash, (int)settings.Orientation);
        AddDouble(hash, settings.Margins.LeftMillimetres);
        AddDouble(hash, settings.Margins.TopMillimetres);
        AddDouble(hash, settings.Margins.RightMillimetres);
        AddDouble(hash, settings.Margins.BottomMillimetres);
        AddDouble(hash, settings.Scale);
        AddInt32(hash, settings.ShowPageNumbers ? 1 : 0);
        AddInt32(hash, pages.Count);
        foreach (PrintPagePlan page in pages)
        {
            AddInt32(hash, page.PageNumber);
            AddInt32(hash, page.Items.Count);
            foreach (PrintPageItemPlan item in page.Items)
            {
                AddInt32(hash, item.SectionIndex);
                AddInt32(hash, (int)item.Kind);
                AddDouble(hash, item.Bounds.XPoints);
                AddDouble(hash, item.Bounds.YPoints);
                AddDouble(hash, item.Bounds.WidthPoints);
                AddDouble(hash, item.Bounds.HeightPoints);
                AddInt32(hash, item.TableRowStart);
                AddInt32(hash, item.TableRowCount);
                AddInt32(hash, item.RepeatsTableHeader ? 1 : 0);
                AddInt32(hash, item.TextLineStart);
                AddInt32(hash, item.TextLineCount);
            }
        }
    }

    private static void AddText(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        AddInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AddInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AddDouble(IncrementalHash hash, double value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(value));
        hash.AppendData(bytes);
    }
}
