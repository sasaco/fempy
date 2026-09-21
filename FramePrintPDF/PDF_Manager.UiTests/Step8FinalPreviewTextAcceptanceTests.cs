using System.Security.Cryptography;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Documents;
using PDF_Manager.Printing;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Printing;
using CoreLayoutChoice = PDF_Manager.Core.Abstractions.PrintLayoutChoice;
using CorePageOrientation = PDF_Manager.Core.Abstractions.PrintPageOrientation;
using CorePageSettings = PDF_Manager.Core.Abstractions.PrintPageSettings;
using CorePaperSize = PDF_Manager.Core.Abstractions.PrintPaperSize;

namespace PDF_Manager.UiTests;

public sealed class Step8FinalPreviewTextAcceptanceTests
{
    [Theory]
    [InlineData(0.25, false)]
    [InlineData(4.0, true)]
    public void ProductionThirteenColumnInputPreview_IsUnabridgedSelectableAndMatchesExportMetadata(
        double scale,
        bool expectsTruncation)
    {
        StaTestRunner.Run(() =>
        {
            const string FullSectionName = "SECTION_NAME_UNABRIDGED_ALPHA_123456789";
            ProjectDocumentEditSession edit = new(ProjectDocumentPresets.CreateRepresentativeFrame());
            Assert.True(edit.UpsertSection(edit.Current.Sections[0] with { Name = FullSectionName }));
            ProjectDocument document = edit.Current;
            CorePageSettings settings = new(
                CorePaperSize.A4,
                CorePageOrientation.Portrait,
                scale: scale,
                layout: CoreLayoutChoice.SectionPerPage,
                showPageNumbers: true);
            PrintExportRequest request = new(
                document,
                resultSet: null,
                pageSettings: settings,
                sections: [PrintContentSection.InputTables]);
            LocalizationService localization = new(UiLanguage.English);
            DesktopPrintJobFactory factory = new(localization);
            DesktopPrintJob projection = factory.CreateProjection(request, PrintDiagramCaptureSet.Empty);
            int sectionIndex = projection.Job.Sections
                .Select((section, index) => (section, index))
                .Single(pair => pair.section is PrintTableSection table && table.Table.Columns.Count == 13)
                .index;
            PrintTable expectedTable = Assert.IsType<PrintTableSection>(projection.Job.Sections[sectionIndex]).Table;
            Assert.Equal(13, expectedTable.Columns.Count);

            TypedPdfDocumentWriter writer = new();
            PrintDocumentPlan plan = writer.Plan(projection.Job);
            PrintPagePlan sectionPage = Assert.Single(
                plan.Pages,
                page => page.Items.Any(item => item.SectionIndex == sectionIndex));
            PrintPreviewTextRun[] sectionRuns = sectionPage.TextRuns
                .Where(run => run.SectionIndex == sectionIndex)
                .ToArray();
            PrintPreviewTextRun[] cellRuns = sectionRuns
                .Where(run => run.Kind is PrintPreviewTextKind.TableHeader or PrintPreviewTextKind.TableCell)
                .ToArray();
            Assert.Equal(26, cellRuns.Length);
            Assert.Equal(expectsTruncation, cellRuns.Any(run => run.IsTruncated));
            Assert.All(cellRuns, run =>
                Assert.InRange(run.DisplayWidthPoints, 0, run.Bounds.WidthPoints + 0.001));
            PrintPreviewTextRun sectionNameRun = Assert.Single(cellRuns, run => run.Text == FullSectionName);
            Assert.Equal(expectsTruncation, sectionNameRun.IsTruncated);

            DesktopPdfExporter exporter = new(
                writer,
                jobFactory: factory,
                localization: localization);
            PrintPreviewResult preview = exporter.PreviewAsync(request, PrintDiagramCaptureSet.Empty)
                .GetAwaiter()
                .GetResult();
            using MemoryStream pdf = new();
            PrintExportReceipt receipt = exporter.ExportWithResultAsync(
                    request,
                    PrintDiagramCaptureSet.Empty,
                    pdf)
                .GetAwaiter()
                .GetResult();

            Assert.Equal(plan.Identity.Value, preview.PlanIdentity);
            Assert.Equal(plan.Identity.Value, receipt.PlanIdentity);
            int pageIndex = sectionPage.PageNumber - 1;
            PrintPreviewPage selectedPage = preview.Pages[pageIndex];
            string expectedTextContent = string.Join(
                PrintPreviewPage.TextContentLineSeparator,
                sectionPage.TextRuns.Select(run => run.Text));
            Assert.Equal(expectedTextContent, selectedPage.TextContent);
            Assert.Contains(FullSectionName, selectedPage.TextContent, StringComparison.Ordinal);
            Assert.All(expectedTable.Columns, column =>
                Assert.Contains(column.Header, selectedPage.TextContent, StringComparison.Ordinal));
            Assert.All(expectedTable.Rows.SelectMany(row => row.Cells), value =>
                Assert.Contains(value, selectedPage.TextContent, StringComparison.Ordinal));

            Assert.True(pdf.Length > 1_000);

            int adjacentIndex = pageIndex + 1 < preview.PageCount ? pageIndex + 1 : pageIndex - 1;
            Assert.InRange(adjacentIndex, 0, preview.PageCount - 1);
            Assert.NotEqual(selectedPage.TextContent, preview.Pages[adjacentIndex].TextContent);
            Assert.NotEqual(CaptureHash(selectedPage.RenderedPageCapture), CaptureHash(preview.Pages[adjacentIndex].RenderedPageCapture));

            using PDF_Manager.Shell.Printing.PrintPreviewDialog dialog =
                new(localization, new PrintPreviewState(preview, pageIndex));
            dialog.Show();
            Application.DoEvents();

            Assert.True(dialog.ContentTextBox.Multiline);
            Assert.True(dialog.ContentTextBox.ReadOnly);
            Assert.True(dialog.ContentTextBox.TabStop);
            Assert.False(string.IsNullOrWhiteSpace(dialog.ContentTextBox.AccessibleName));
            Assert.Equal(selectedPage.TextContent, dialog.ContentTextBox.Text);
            Assert.Equal(selectedPage.TextContent, dialog.CurrentState.SelectedPageTextContent);
            dialog.ContentTextBox.SelectAll();
            Assert.Equal(dialog.ContentTextBox.Text.Length, dialog.ContentTextBox.SelectionLength);
            Assert.Equal(
                CaptureHash(selectedPage.RenderedPageCapture),
                BitmapHash(Assert.IsType<Bitmap>(dialog.PreviewImage.Image)));

            if (adjacentIndex > pageIndex)
            {
                dialog.NextButton.PerformClick();
            }
            else
            {
                dialog.PreviousButton.PerformClick();
            }

            Application.DoEvents();
            Assert.Equal(adjacentIndex, dialog.CurrentState.SelectedPageIndex);
            Assert.Equal(preview.Pages[adjacentIndex].TextContent, dialog.ContentTextBox.Text);
            Assert.NotEqual(selectedPage.TextContent, dialog.ContentTextBox.Text);
            Assert.Equal(
                CaptureHash(preview.Pages[adjacentIndex].RenderedPageCapture),
                BitmapHash(Assert.IsType<Bitmap>(dialog.PreviewImage.Image)));
        }, $"Step 8 final 13-column preview text at {scale:P0}", TimeSpan.FromSeconds(45));
    }

    private static string CaptureHash(PrintPreviewCapture capture) =>
        Convert.ToHexString(SHA256.HashData(capture.Rgb24.Span));

    private static string BitmapHash(Bitmap bitmap)
    {
        byte[] rgb = new byte[checked(bitmap.Width * bitmap.Height * 3)];
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

        return Convert.ToHexString(SHA256.HashData(rgb));
    }
}
