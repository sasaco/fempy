namespace PDF_Manager.Shell.ScreenComposition.Core;

public enum ScreenRouteId
{
    InputElements,
    InputNodes,
    InputSupports,
    InputMembers,
    InputRigidZone,
    InputPanel,
    InputJoints,
    InputNoticePoints,
    InputMemberSprings,
    InputLoadNames,
    InputLoads,
    InputDefine,
    InputCombine,
    InputPickup,
    ResultBasicDisplacements,
    ResultBasicReactions,
    ResultBasicSectionForces,
    ResultCombineDisplacements,
    ResultCombineReactions,
    ResultCombineSectionForces,
    ResultPickupDisplacements,
    ResultPickupReactions,
    ResultPickupSectionForces,
}

public enum ScreenRouteContext
{
    None,
    Members,
    RigidZone,
    LoadNames,
    Loads,
    Define,
    Combine,
    Pickup,
    BasicResult,
    CombinedResult,
    PickupResult,
}

public enum ViewDimension
{
    TwoDimensional = 2,
    ThreeDimensional = 3,
}

public enum ScreenOverlayKind
{
    None,
    Start,
    Preset,
    Print,
    Wait,
    Confirm,
    Alert,
}

public readonly record struct ScreenPageState
{
    public static ScreenPageState Single { get; } = new(0, 1);

    public ScreenPageState(int index, int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Page count must be positive.");
        }

        if (index < 0 || index >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Page index must be within the page count.");
        }

        Index = index;
        Count = count;
    }

    public int Index { get; }

    public int Count { get; }

    public bool CanMovePrevious => Index > 0;

    public bool CanMoveNext => Index + 1 < Count;
}

public sealed record ScreenRouteState
{
    public static ScreenRouteState Default { get; } = new(
        route: null,
        context: ScreenRouteContext.None,
        dimension: ViewDimension.TwoDimensional,
        page: ScreenPageState.Single,
        resultsEnabled: false,
        overlay: ScreenOverlayKind.Start);

    public ScreenRouteState(
        ScreenRouteId? route,
        ScreenRouteContext context,
        ViewDimension dimension,
        ScreenPageState page,
        bool resultsEnabled,
        ScreenOverlayKind overlay)
    {
        ValidateDimension(dimension);
        ValidateOverlay(overlay);
        if (route is ScreenRouteId routeId)
        {
            AngularScreenManifest.GetRoute(routeId);
            ScreenRouteContext expected = AngularScreenManifest.GetContext(routeId);
            if (context != expected)
            {
                throw new ArgumentException(
                    $"Route '{routeId}' requires context '{expected}', not '{context}'.",
                    nameof(context));
            }

            if (AngularScreenManifest.IsResultRoute(routeId) && !resultsEnabled)
            {
                throw new ArgumentException(
                    "A result route cannot remain active while results are disabled.",
                    nameof(resultsEnabled));
            }
        }
        else if (context != ScreenRouteContext.None)
        {
            throw new ArgumentException("A context requires an active route.", nameof(context));
        }

        Route = route;
        Context = context;
        Dimension = dimension;
        Page = page;
        ResultsEnabled = resultsEnabled;
        Overlay = overlay;
    }

    public ScreenRouteId? Route { get; }

    public ScreenRouteContext Context { get; }

    public ViewDimension Dimension { get; }

    public ScreenPageState Page { get; }

    public bool ResultsEnabled { get; }

    public ScreenOverlayKind Overlay { get; }

    public bool HasRoute => Route.HasValue;

    public bool HasOverlay => Overlay != ScreenOverlayKind.None;

    private static void ValidateDimension(ViewDimension dimension)
    {
        if (dimension is not ViewDimension.TwoDimensional and not ViewDimension.ThreeDimensional)
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unsupported view dimension.");
        }
    }

    private static void ValidateOverlay(ScreenOverlayKind overlay)
    {
        if (!Enum.IsDefined(overlay))
        {
            throw new ArgumentOutOfRangeException(nameof(overlay), overlay, "Unsupported overlay.");
        }
    }
}
