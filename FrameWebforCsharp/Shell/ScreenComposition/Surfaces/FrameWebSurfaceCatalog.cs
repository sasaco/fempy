using PDF_Manager.Shell.Editing;
using PDF_Manager.Shell.ScreenComposition.Core;

namespace PDF_Manager.Shell.ScreenComposition.Surfaces;

public sealed record InputRouteSurfaceDefinition(
    ScreenRouteId Route,
    string TitleResourceKey,
    IReadOnlyList<InputTableKey> Tables,
    bool ThreeDimensionalOnly = false);

public enum ResultSurfaceCategory
{
    Displacements,
    Reactions,
    SectionForces,
}

public sealed record ResultRouteSurfaceDefinition(
    ScreenRouteId Route,
    string TitleResourceKey,
    ResultSurfaceCategory Category,
    ScreenRouteContext Context);

public static class FrameWebSurfaceCatalog
{
    private static readonly InputRouteSurfaceDefinition[] InputDefinitions =
    [
        Input(
            ScreenRouteId.InputElements,
            "SurfaceElements",
            InputTableKey.Sections,
            InputTableKey.ElementPropertySets,
            InputTableKey.ModelSettings),
        Input(ScreenRouteId.InputNodes, "EditorNodes", InputTableKey.Nodes),
        Input(
            ScreenRouteId.InputSupports,
            "EditorSupports",
            InputTableKey.Supports,
            InputTableKey.SupportSets),
        Input(ScreenRouteId.InputMembers, "EditorMembers", InputTableKey.Members),
        Input(ScreenRouteId.InputRigidZone, "EditorRigidZones", InputTableKey.RigidZones),
        Input(ScreenRouteId.InputPanel, "EditorPanels", true, InputTableKey.Panels),
        Input(
            ScreenRouteId.InputJoints,
            "EditorJoints",
            InputTableKey.Joints,
            InputTableKey.JointReleaseSets),
        Input(ScreenRouteId.InputNoticePoints, "EditorNoticePoints", InputTableKey.NoticePoints),
        Input(
            ScreenRouteId.InputMemberSprings,
            "EditorMemberSprings",
            InputTableKey.MemberSprings,
            InputTableKey.MemberSpringSets),
        Input(ScreenRouteId.InputLoadNames, "SurfaceLoadNames", InputTableKey.LoadCases),
        Input(
            ScreenRouteId.InputLoads,
            "SurfaceLoadStrength",
            InputTableKey.NodalLoads,
            InputTableKey.MemberLoads,
            InputTableKey.PrescribedDisplacements),
        Input(ScreenRouteId.InputDefine, "EditorDefine", InputTableKey.Define),
        Input(ScreenRouteId.InputCombine, "EditorCombine", InputTableKey.Combine),
        Input(ScreenRouteId.InputPickup, "EditorPickup", InputTableKey.Pickup),
    ];

    private static readonly ResultRouteSurfaceDefinition[] ResultDefinitions =
    [
        Result(ScreenRouteId.ResultBasicDisplacements, "ResultDisplacements", ResultSurfaceCategory.Displacements, ScreenRouteContext.BasicResult),
        Result(ScreenRouteId.ResultBasicReactions, "ResultReactions", ResultSurfaceCategory.Reactions, ScreenRouteContext.BasicResult),
        Result(ScreenRouteId.ResultBasicSectionForces, "ResultMemberForces", ResultSurfaceCategory.SectionForces, ScreenRouteContext.BasicResult),
        Result(ScreenRouteId.ResultCombineDisplacements, "ResultDisplacements", ResultSurfaceCategory.Displacements, ScreenRouteContext.CombinedResult),
        Result(ScreenRouteId.ResultCombineReactions, "ResultReactions", ResultSurfaceCategory.Reactions, ScreenRouteContext.CombinedResult),
        Result(ScreenRouteId.ResultCombineSectionForces, "ResultMemberForces", ResultSurfaceCategory.SectionForces, ScreenRouteContext.CombinedResult),
        Result(ScreenRouteId.ResultPickupDisplacements, "ResultDisplacements", ResultSurfaceCategory.Displacements, ScreenRouteContext.PickupResult),
        Result(ScreenRouteId.ResultPickupReactions, "ResultReactions", ResultSurfaceCategory.Reactions, ScreenRouteContext.PickupResult),
        Result(ScreenRouteId.ResultPickupSectionForces, "ResultMemberForces", ResultSurfaceCategory.SectionForces, ScreenRouteContext.PickupResult),
    ];

    private static readonly IReadOnlyDictionary<ScreenRouteId, InputRouteSurfaceDefinition> InputIndex =
        InputDefinitions.ToDictionary(definition => definition.Route);

    private static readonly IReadOnlyDictionary<ScreenRouteId, ResultRouteSurfaceDefinition> ResultIndex =
        ResultDefinitions.ToDictionary(definition => definition.Route);

    public static IReadOnlyList<InputRouteSurfaceDefinition> InputRoutes { get; } =
        Array.AsReadOnly(InputDefinitions);

    public static IReadOnlyList<ResultRouteSurfaceDefinition> ResultRoutes { get; } =
        Array.AsReadOnly(ResultDefinitions);

    public static InputRouteSurfaceDefinition GetInput(ScreenRouteId route) =>
        InputIndex.TryGetValue(route, out InputRouteSurfaceDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(route), route, "The route is not an input route.");

    public static ResultRouteSurfaceDefinition GetResult(ScreenRouteId route) =>
        ResultIndex.TryGetValue(route, out ResultRouteSurfaceDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(route), route, "The route is not a result route.");

    private static InputRouteSurfaceDefinition Input(
        ScreenRouteId route,
        string titleResourceKey,
        params InputTableKey[] tables) =>
        new(route, titleResourceKey, Array.AsReadOnly(tables));

    private static InputRouteSurfaceDefinition Input(
        ScreenRouteId route,
        string titleResourceKey,
        bool threeDimensionalOnly,
        params InputTableKey[] tables) =>
        new(route, titleResourceKey, Array.AsReadOnly(tables), threeDimensionalOnly);

    private static ResultRouteSurfaceDefinition Result(
        ScreenRouteId route,
        string titleResourceKey,
        ResultSurfaceCategory category,
        ScreenRouteContext context) =>
        new(route, titleResourceKey, category, context);
}
