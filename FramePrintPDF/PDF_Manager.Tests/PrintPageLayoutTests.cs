using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed class PrintPageLayoutTests
{
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
