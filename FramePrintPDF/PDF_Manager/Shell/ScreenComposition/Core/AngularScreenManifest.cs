namespace PDF_Manager.Shell.ScreenComposition.Core;

public enum ScreenRouteFamily
{
    Input,
    Result,
}

public enum PrimaryNavigationId
{
    Calculate,
    Elements,
    Nodes,
    Supports,
    Members,
    Panel,
    Joints,
    NoticePoints,
    MemberSprings,
    Loads,
    Define,
    Displacements,
    Reactions,
    SectionForces,
}

public sealed record ScreenRouteDefinition(
    ScreenRouteId Id,
    string AngularPath,
    ScreenRouteFamily Family,
    PrimaryNavigationId Navigation,
    ScreenRouteContext Context);

public sealed record PrimaryNavigationDefinition(
    PrimaryNavigationId Id,
    string Text,
    ScreenRouteId? DefaultRoute,
    bool RequiresResults,
    bool ThreeDimensionalOnly = false);

public static class AngularScreenManifest
{
    private static readonly ScreenRouteDefinition[] Routes =
    [
        Input(ScreenRouteId.InputElements, "input-elements", PrimaryNavigationId.Elements),
        Input(ScreenRouteId.InputNodes, "input-nodes", PrimaryNavigationId.Nodes),
        Input(ScreenRouteId.InputSupports, "input-fix_nodes", PrimaryNavigationId.Supports),
        Input(ScreenRouteId.InputMembers, "input-members", PrimaryNavigationId.Members, ScreenRouteContext.Members),
        Input(ScreenRouteId.InputRigidZone, "input-rigid_zone", PrimaryNavigationId.Members, ScreenRouteContext.RigidZone),
        Input(ScreenRouteId.InputPanel, "input-panel", PrimaryNavigationId.Panel),
        Input(ScreenRouteId.InputJoints, "input-joints", PrimaryNavigationId.Joints),
        Input(ScreenRouteId.InputNoticePoints, "input-notice_points", PrimaryNavigationId.NoticePoints),
        Input(ScreenRouteId.InputMemberSprings, "input-fix_members", PrimaryNavigationId.MemberSprings),
        Input(ScreenRouteId.InputLoadNames, "input-load-name", PrimaryNavigationId.Loads, ScreenRouteContext.LoadNames),
        Input(ScreenRouteId.InputLoads, "input-loads", PrimaryNavigationId.Loads, ScreenRouteContext.Loads),
        Input(ScreenRouteId.InputDefine, "input-define", PrimaryNavigationId.Define, ScreenRouteContext.Define),
        Input(ScreenRouteId.InputCombine, "input-combine", PrimaryNavigationId.Define, ScreenRouteContext.Combine),
        Input(ScreenRouteId.InputPickup, "input-pickup", PrimaryNavigationId.Define, ScreenRouteContext.Pickup),
        Result(ScreenRouteId.ResultBasicDisplacements, "result-disg", PrimaryNavigationId.Displacements, ScreenRouteContext.BasicResult),
        Result(ScreenRouteId.ResultBasicReactions, "result-reac", PrimaryNavigationId.Reactions, ScreenRouteContext.BasicResult),
        Result(ScreenRouteId.ResultBasicSectionForces, "result-fsec", PrimaryNavigationId.SectionForces, ScreenRouteContext.BasicResult),
        Result(ScreenRouteId.ResultCombineDisplacements, "result-comb_disg", PrimaryNavigationId.Displacements, ScreenRouteContext.CombinedResult),
        Result(ScreenRouteId.ResultCombineReactions, "result-comb_reac", PrimaryNavigationId.Reactions, ScreenRouteContext.CombinedResult),
        Result(ScreenRouteId.ResultCombineSectionForces, "result-comb_fsec", PrimaryNavigationId.SectionForces, ScreenRouteContext.CombinedResult),
        Result(ScreenRouteId.ResultPickupDisplacements, "result-pic_disg", PrimaryNavigationId.Displacements, ScreenRouteContext.PickupResult),
        Result(ScreenRouteId.ResultPickupReactions, "result-pic_reac", PrimaryNavigationId.Reactions, ScreenRouteContext.PickupResult),
        Result(ScreenRouteId.ResultPickupSectionForces, "result-pic_fsec", PrimaryNavigationId.SectionForces, ScreenRouteContext.PickupResult),
    ];

    private static readonly PrimaryNavigationDefinition[] Navigation =
    [
        new(PrimaryNavigationId.Calculate, "Calculate", null, false),
        new(PrimaryNavigationId.Elements, "Elements", ScreenRouteId.InputElements, false),
        new(PrimaryNavigationId.Nodes, "Nodes", ScreenRouteId.InputNodes, false),
        new(PrimaryNavigationId.Supports, "Supports", ScreenRouteId.InputSupports, false),
        new(PrimaryNavigationId.Members, "Members", ScreenRouteId.InputMembers, false),
        new(PrimaryNavigationId.Panel, "Panel", ScreenRouteId.InputPanel, false, ThreeDimensionalOnly: true),
        new(PrimaryNavigationId.Joints, "Joints", ScreenRouteId.InputJoints, false),
        new(PrimaryNavigationId.NoticePoints, "Notice Points", ScreenRouteId.InputNoticePoints, false),
        new(PrimaryNavigationId.MemberSprings, "Member Springs", ScreenRouteId.InputMemberSprings, false),
        new(PrimaryNavigationId.Loads, "Loads", ScreenRouteId.InputLoadNames, false),
        new(PrimaryNavigationId.Define, "DEFINE", ScreenRouteId.InputDefine, false),
        new(PrimaryNavigationId.Displacements, "Displacements", ScreenRouteId.ResultBasicDisplacements, true),
        new(PrimaryNavigationId.Reactions, "Reactions", ScreenRouteId.ResultBasicReactions, true),
        new(PrimaryNavigationId.SectionForces, "Section Forces", ScreenRouteId.ResultBasicSectionForces, true),
    ];

    private static readonly IReadOnlyDictionary<ScreenRouteId, ScreenRouteDefinition> RouteIndex =
        Routes.ToDictionary(route => route.Id);

    public static IReadOnlyList<ScreenRouteDefinition> InputRoutes { get; } =
        Array.AsReadOnly(Routes.Where(route => route.Family == ScreenRouteFamily.Input).ToArray());

    public static IReadOnlyList<ScreenRouteDefinition> ResultRoutes { get; } =
        Array.AsReadOnly(Routes.Where(route => route.Family == ScreenRouteFamily.Result).ToArray());

    public static IReadOnlyList<PrimaryNavigationDefinition> PrimaryNavigation { get; } =
        Array.AsReadOnly(Navigation);

    public static ScreenRouteDefinition GetRoute(ScreenRouteId route) =>
        RouteIndex.TryGetValue(route, out ScreenRouteDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(route), route, "Unknown screen route.");

    public static ScreenRouteContext GetContext(ScreenRouteId route) => GetRoute(route).Context;

    public static bool IsResultRoute(ScreenRouteId route) =>
        GetRoute(route).Family == ScreenRouteFamily.Result;

    public static ScreenRouteId ResolveContextRoute(ScreenRouteId current, ScreenRouteContext context)
    {
        ScreenRouteDefinition definition = GetRoute(current);
        ScreenRouteDefinition? target = Routes.FirstOrDefault(route =>
            route.Navigation == definition.Navigation && route.Context == context);
        return target?.Id ?? throw new ArgumentException(
            $"Context '{context}' is not valid for route family '{definition.Navigation}'.",
            nameof(context));
    }

    private static ScreenRouteDefinition Input(
        ScreenRouteId id,
        string path,
        PrimaryNavigationId navigation,
        ScreenRouteContext context = ScreenRouteContext.None) =>
        new(id, path, ScreenRouteFamily.Input, navigation, context);

    private static ScreenRouteDefinition Result(
        ScreenRouteId id,
        string path,
        PrimaryNavigationId navigation,
        ScreenRouteContext context) =>
        new(id, path, ScreenRouteFamily.Result, navigation, context);
}
