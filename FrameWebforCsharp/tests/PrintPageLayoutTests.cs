using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed class PrintPageLayoutTests
{
    [Theory]
    [InlineData(PrintPaperSize.A4, PrintPageOrientation.Portrait, 210.0, 297.0)]
    [InlineData(PrintPaperSize.A4, PrintPageOrientation.Landscape, 297.0, 210.0)]
    [InlineData(PrintPaperSize.A3, PrintPageOrientation.Portrait, 297.0, 420.0)]
    [InlineData(PrintPaperSize.A3, PrintPageOrientation.Landscape, 420.0, 297.0)]
    public void PrintPageSettings_AllSupportedPaperGeometryIsExact(
        PrintPaperSize paperSize,
        PrintPageOrientation orientation,
        double expectedWidth,
        double expectedHeight)
    {
        PrintPageSettings settings = new(
            paperSize,
            orientation,
            new PrintMargins(11, 12, 13, 14),
            scale: 1.25,
            showPageNumbers: false);

        Assert.Equal(expectedWidth, settings.PageWidthMillimetres);
        Assert.Equal(expectedHeight, settings.PageHeightMillimetres);
        Assert.Equal(expectedWidth - 24, settings.PrintableWidthMillimetres);
        Assert.Equal(expectedHeight - 26, settings.PrintableHeightMillimetres);
        Assert.Equal(1.25, settings.Scale);
        Assert.False(settings.ShowPageNumbers);
    }

    [Fact]
    public void PrintPageSettings_ScaleAcceptsExactLimitsAndRejectsOutsideValues()
    {
        Assert.Equal(
            PrintPageSettings.MinimumScale,
            PrintPageSettings.CreateA4(scale: PrintPageSettings.MinimumScale).Scale);
        Assert.Equal(
            PrintPageSettings.MaximumScale,
            PrintPageSettings.CreateA3(scale: PrintPageSettings.MaximumScale).Scale);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrintPageSettings.CreateA4(scale: Math.BitDecrement(PrintPageSettings.MinimumScale)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrintPageSettings.CreateA4(scale: Math.BitIncrement(PrintPageSettings.MaximumScale)));
    }

    [Fact]
    public void CreateA4_WithLandscapeOrientation_ComputesPrintableDimensions()
    {
        PrintPageLayout layout = PrintPageLayout.CreateA4(
            PrintPageOrientation.Landscape,
            uniformMarginMillimetres: 12.0);

        Assert.Equal(PrintPageOrientation.Landscape, layout.Orientation);
        Assert.Equal(297.0, layout.PageWidthMillimetres);
        Assert.Equal(210.0, layout.PageHeightMillimetres);
        Assert.Equal(273.0, layout.PrintableWidthMillimetres);
        Assert.Equal(186.0, layout.PrintableHeightMillimetres);
    }

    [Fact]
    public void Constructor_WhenMarginsConsumePage_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrintPageLayout(
            pageWidthMillimetres: 100.0,
            pageHeightMillimetres: 100.0,
            leftMarginMillimetres: 50.0,
            topMarginMillimetres: 10.0,
            rightMarginMillimetres: 50.0,
            bottomMarginMillimetres: 10.0));
    }

    [Fact]
    public void CreateA4_WithUnknownOrientation_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrintPageLayout.CreateA4((PrintPageOrientation)int.MaxValue));
    }
}
