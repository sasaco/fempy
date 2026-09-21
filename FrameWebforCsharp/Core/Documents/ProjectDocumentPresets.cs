namespace FrameWebforCsharp.Core.Documents;

public enum BuiltInProjectPreset
{
    RamenViaduct,
    ConcreteTBeamBridge,
    UShapedRetainingWall,
    PortalPier,
}

public sealed record BuiltInProjectPresetDescriptor(
    BuiltInProjectPreset Id,
    string StableId,
    int SortOrder);

/// <summary>Validated built-in documents whose persisted values are sufficient for analysis.</summary>
public static class ProjectDocumentPresets
{
    public const int MaxPresetBytes = 1024 * 1024;
    public const int MaxCatalogBytes = 2 * 1024 * 1024;

    public static IReadOnlyList<BuiltInProjectPresetDescriptor> Catalog { get; } =
        Array.AsReadOnly<BuiltInProjectPresetDescriptor>(
    [
        new(BuiltInProjectPreset.RamenViaduct, "ramen-viaduct", 1),
        new(BuiltInProjectPreset.ConcreteTBeamBridge, "concrete-t-beam-bridge", 2),
        new(BuiltInProjectPreset.UShapedRetainingWall, "u-shaped-retaining-wall", 3),
        new(BuiltInProjectPreset.PortalPier, "portal-pier", 4),
    ]);

    public static ProjectDocument Create(BuiltInProjectPreset preset) => preset switch
    {
        BuiltInProjectPreset.RamenViaduct => CreateRamenViaduct(),
        BuiltInProjectPreset.ConcreteTBeamBridge => CreateConcreteTBeamBridge(),
        BuiltInProjectPreset.UShapedRetainingWall => CreateUShapedRetainingWall(),
        BuiltInProjectPreset.PortalPier => CreatePortalPier(),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown built-in project preset."),
    };

    public static ProjectDocument CreateBlank() => new(
        ProjectDocument.CurrentVersion,
        new ProjectMetadata("Untitled", string.Empty, "FrameWeb", "kN-m"),
        nodes: [],
        members: [],
        supports: [],
        loadCases: [],
        nodalLoads: [],
        derivedResults: [],
        movingLoads: [],
        selection: ProjectSelection.Empty,
        isDirty: false,
        sections: [],
        dimension: ModelDimension.TwoDimensional);

    public static ProjectDocument CreateRepresentativeFrame()
    {
        FrameSectionDefinition section = new(
            "1",
            "Steel rectangular section",
            YoungsModulus: 210_000_000,
            PoissonRatio: 0.3,
            ShearModulus: 80_769_230.76923077,
            Area: 0.01,
            MomentOfInertiaY: 8.333333333333334e-6,
            MomentOfInertiaZ: 8.333333333333334e-6,
            TorsionConstant: 1.6666666666666667e-5);

        return new ProjectDocument(
            ProjectDocument.CurrentVersion,
            new ProjectMetadata(
                "Representative cantilever",
                "Two-node static frame used by the vertical MVP.",
                "FrameWeb",
                "kN-m"),
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 4, 0, 0),
            ],
            [new ProjectMember("1", "1", "2", section.Id, 0, false)],
            [new ProjectSupport("S1", "1", true, true, true, true, true, true)],
            [new LoadCaseDefinition("1", "Service", "S")],
            [new NodalLoadDefinition("P1", "1", "2", 0, -10, 0, 0, 0, 0)],
            [],
            [],
            ProjectSelection.Empty,
            isDirty: false,
            sections: [section]);
    }

    private static ProjectDocument CreateRamenViaduct()
        => CreateFramePreset(
            "Ramen viaduct",
            "Typed representative of the built-in ramen viaduct input.",
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 0, 0, 6),
                new ProjectNode("3", 8, 0, 6),
                new ProjectNode("4", 8, 0, 0),
            ],
            [
                new ProjectMember("1", "1", "2", "1"),
                new ProjectMember("2", "2", "3", "1"),
                new ProjectMember("3", "3", "4", "1"),
            ],
            [
                new ProjectSupport("S1", "1", true, true, true, true, true, true),
                new ProjectSupport("S2", "4", true, true, true, true, true, true),
            ],
            new NodalLoadDefinition("P1", "1", "2", 25, 0, -120, 0, 0, 0),
            area: 1.2,
            inertia: 0.18);

    private static ProjectDocument CreateConcreteTBeamBridge()
        => CreateFramePreset(
            "Concrete T-beam bridge",
            "Typed representative of the built-in concrete T-beam bridge input.",
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 5, 0, 0),
                new ProjectNode("3", 10, 0, 0),
            ],
            [
                new ProjectMember("1", "1", "2", "1"),
                new ProjectMember("2", "2", "3", "1"),
            ],
            [
                new ProjectSupport("S1", "1", true, true, true, true, false, false),
                new ProjectSupport("S2", "3", false, true, true, false, false, false),
            ],
            new NodalLoadDefinition("P1", "1", "2", 0, 0, -80, 0, 0, 0),
            area: 0.85,
            inertia: 0.095);

    private static ProjectDocument CreateUShapedRetainingWall()
        => CreateFramePreset(
            "U-shaped retaining wall",
            "Typed representative of the built-in U-shaped retaining wall input.",
            [
                new ProjectNode("1", 0, 0, 4),
                new ProjectNode("2", 0, 0, 0),
                new ProjectNode("3", 5, 0, 0),
                new ProjectNode("4", 5, 0, 4),
            ],
            [
                new ProjectMember("1", "1", "2", "1"),
                new ProjectMember("2", "2", "3", "1"),
                new ProjectMember("3", "3", "4", "1"),
            ],
            [
                new ProjectSupport("S1", "2", true, true, true, true, true, true),
                new ProjectSupport("S2", "3", true, true, true, true, true, true),
            ],
            new NodalLoadDefinition("P1", "1", "1", 35, 0, 0, 0, 0, 0),
            area: 0.7,
            inertia: 0.065);

    private static ProjectDocument CreatePortalPier()
        => CreateFramePreset(
            "Portal pier",
            "Typed representative of the built-in portal-pier input.",
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 0, 0, 5),
                new ProjectNode("3", 7, 0, 5),
                new ProjectNode("4", 7, 0, 0),
            ],
            [
                new ProjectMember("1", "1", "2", "1"),
                new ProjectMember("2", "2", "3", "1"),
                new ProjectMember("3", "3", "4", "1"),
            ],
            [
                new ProjectSupport("S1", "1", true, true, true, true, true, true),
                new ProjectSupport("S2", "4", true, true, true, true, true, true),
            ],
            new NodalLoadDefinition("P1", "1", "3", 60, 0, -25, 0, 0, 0),
            area: 0.95,
            inertia: 0.12);

    private static ProjectDocument CreateFramePreset(
        string name,
        string description,
        IEnumerable<ProjectNode> nodes,
        IEnumerable<ProjectMember> members,
        IEnumerable<ProjectSupport> supports,
        NodalLoadDefinition load,
        double area,
        double inertia)
    {
        FrameSectionDefinition section = new(
            "1",
            $"{name} representative section",
            YoungsModulus: 30_000_000,
            PoissonRatio: 0.2,
            ShearModulus: 12_500_000,
            Area: area,
            MomentOfInertiaY: inertia,
            MomentOfInertiaZ: inertia,
            TorsionConstant: inertia * 2);

        ProjectDocument document = new(
            ProjectDocument.CurrentVersion,
            new ProjectMetadata(name, description, "FrameWeb", "kN-m"),
            nodes,
            members,
            supports,
            [new LoadCaseDefinition("1", "Service", "S")],
            [load],
            [],
            [],
            ProjectSelection.Empty,
            isDirty: false,
            sections: [section]);
        ProjectDocumentValidator.Validate(document);
        return document;
    }
}
