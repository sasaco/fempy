using System.Runtime.InteropServices;
using FrameWebforCS.Core.Abstractions;
using FrameWebforCS.Resources;

namespace FrameWebforCS.Shell.Printing;

public sealed class PrintPreviewState
{
    public PrintPreviewState(
        PrintPreviewResult preview,
        int selectedPageIndex = 0)
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        if (selectedPageIndex < 0 || selectedPageIndex >= preview.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedPageIndex));
        }

        SelectedPageIndex = selectedPageIndex;
    }

    public PrintPreviewResult Preview { get; }

    public PrintPreviewCapture SelectedPageCapture => SelectedPage.RenderedPageCapture;

    public PrintPreviewCapture RenderedPageCapture => SelectedPageCapture;

    public string SelectedPageTextContent => SelectedPage.TextContent;

    public int PageCount => Preview.PageCount;

    public int SelectedPageIndex { get; }

    public PrintPreviewPage SelectedPage => Preview.Pages[SelectedPageIndex];

    public IReadOnlyList<PrintContentSection> SelectedSections => SelectedPage.Sections;

    public PrintPreviewState SelectPage(int pageIndex) =>
        new(Preview, pageIndex);
}

public sealed class PrintPageSetupSelection
{
    public PrintPageSetupSelection(
        PrintPageSettings pageSettings,
        IEnumerable<PrintContentSection> sections)
    {
        PageSettings = pageSettings ?? throw new ArgumentNullException(nameof(pageSettings));
        PrintContentSection[] values = sections?.ToArray()
            ?? throw new ArgumentNullException(nameof(sections));
        if (values.Length == 0 ||
            values.Any(value => !Enum.IsDefined(value)) ||
            values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException("At least one unique print section is required.", nameof(sections));
        }

        Sections = Array.AsReadOnly(values);
    }

    public PrintPageSettings PageSettings { get; }

    public IReadOnlyList<PrintContentSection> Sections { get; }
}

public static class PrintUiResources
{
    public static string SectionResourceKey(PrintContentSection section) => section switch
    {
        PrintContentSection.ProjectSummary => "PrintSectionProjectSummary",
        PrintContentSection.InputTables => "PrintSectionInputTables",
        PrintContentSection.ModelDiagram => "PrintSectionModelDiagram",
        PrintContentSection.LoadDiagram => "PrintSectionLoadDiagram",
        PrintContentSection.DisplacementResults => "PrintSectionDisplacements",
        PrintContentSection.ReactionResults => "PrintSectionReactions",
        PrintContentSection.MemberForceResults => "PrintSectionMemberForces",
        PrintContentSection.ResultDiagram => "PrintSectionResultDiagram",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
    };
}

internal static class PrintPreviewBitmap
{
    internal static Bitmap Create(PrintPreviewCapture capture)
    {
        Bitmap bitmap = new(capture.Width, capture.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        ReadOnlySpan<byte> rgb = capture.Rgb24.Span;
        System.Drawing.Imaging.BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            byte[] bgrRow = new byte[checked(capture.Width * 3)];
            for (int y = 0; y < capture.Height; y++)
            {
                int sourceRow = checked(y * capture.Width * 3);
                for (int x = 0; x < capture.Width; x++)
                {
                    int source = sourceRow + (x * 3);
                    int destination = x * 3;
                    bgrRow[destination] = rgb[source + 2];
                    bgrRow[destination + 1] = rgb[source + 1];
                    bgrRow[destination + 2] = rgb[source];
                }

                Marshal.Copy(bgrRow, 0, data.Scan0 + (y * data.Stride), bgrRow.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}
