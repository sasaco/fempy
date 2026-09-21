namespace PDF_Manager.Printing;

public enum PrintPageOrientation
{
    Portrait,
    Landscape,
}

public sealed record PrintPageLayout
{
    public const double A4ShortSideMillimetres = 210.0;
    public const double A4LongSideMillimetres = 297.0;

    public PrintPageLayout(
        double pageWidthMillimetres,
        double pageHeightMillimetres,
        double leftMarginMillimetres,
        double topMarginMillimetres,
        double rightMarginMillimetres,
        double bottomMarginMillimetres)
    {
        ValidatePositiveFinite(pageWidthMillimetres, nameof(pageWidthMillimetres));
        ValidatePositiveFinite(pageHeightMillimetres, nameof(pageHeightMillimetres));
        ValidateNonNegativeFinite(leftMarginMillimetres, nameof(leftMarginMillimetres));
        ValidateNonNegativeFinite(topMarginMillimetres, nameof(topMarginMillimetres));
        ValidateNonNegativeFinite(rightMarginMillimetres, nameof(rightMarginMillimetres));
        ValidateNonNegativeFinite(bottomMarginMillimetres, nameof(bottomMarginMillimetres));

        if (leftMarginMillimetres + rightMarginMillimetres >= pageWidthMillimetres)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rightMarginMillimetres),
                "Horizontal margins must leave a positive printable width.");
        }

        if (topMarginMillimetres + bottomMarginMillimetres >= pageHeightMillimetres)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bottomMarginMillimetres),
                "Vertical margins must leave a positive printable height.");
        }

        PageWidthMillimetres = pageWidthMillimetres;
        PageHeightMillimetres = pageHeightMillimetres;
        LeftMarginMillimetres = leftMarginMillimetres;
        TopMarginMillimetres = topMarginMillimetres;
        RightMarginMillimetres = rightMarginMillimetres;
        BottomMarginMillimetres = bottomMarginMillimetres;
    }

    public double PageWidthMillimetres { get; }

    public double PageHeightMillimetres { get; }

    public double LeftMarginMillimetres { get; }

    public double TopMarginMillimetres { get; }

    public double RightMarginMillimetres { get; }

    public double BottomMarginMillimetres { get; }

    public double PrintableWidthMillimetres =>
        PageWidthMillimetres - LeftMarginMillimetres - RightMarginMillimetres;

    public double PrintableHeightMillimetres =>
        PageHeightMillimetres - TopMarginMillimetres - BottomMarginMillimetres;

    public PrintPageOrientation Orientation =>
        PageWidthMillimetres > PageHeightMillimetres
            ? PrintPageOrientation.Landscape
            : PrintPageOrientation.Portrait;

    public static PrintPageLayout CreateA4(
        PrintPageOrientation orientation = PrintPageOrientation.Portrait,
        double uniformMarginMillimetres = 10.0)
    {
        ValidateNonNegativeFinite(uniformMarginMillimetres, nameof(uniformMarginMillimetres));
        return orientation switch
        {
            PrintPageOrientation.Portrait => new PrintPageLayout(
                A4ShortSideMillimetres,
                A4LongSideMillimetres,
                uniformMarginMillimetres,
                uniformMarginMillimetres,
                uniformMarginMillimetres,
                uniformMarginMillimetres),
            PrintPageOrientation.Landscape => new PrintPageLayout(
                A4LongSideMillimetres,
                A4ShortSideMillimetres,
                uniformMarginMillimetres,
                uniformMarginMillimetres,
                uniformMarginMillimetres,
                uniformMarginMillimetres),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, "Unknown page orientation."),
        };
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and positive.");
        }
    }

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and non-negative.");
        }
    }
}
