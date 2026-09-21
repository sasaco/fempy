using System.Collections.ObjectModel;

namespace FrameWebforCsharp.Core.Documents;

internal static class DocumentCollections
{
    internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values, paramName);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

public enum DerivedResultKind
{
    Define,
    Combine,
    Pickup,
}

public sealed record ProjectMetadata(
    string Name,
    string Description,
    string Author,
    string UnitSystem);

public sealed record ProjectNode(string Id, double X, double Y, double Z);

public sealed record FrameSectionDefinition(
    string Id,
    string Name,
    double YoungsModulus,
    double PoissonRatio,
    double ShearModulus,
    double Area,
    double MomentOfInertiaY,
    double MomentOfInertiaZ,
    double TorsionConstant,
    double ThermalExpansionCoefficient = 0,
    double Density = 0,
    double PanelThickness = 0);

public sealed record ProjectMember(
    string Id,
    string NodeI,
    string NodeJ,
    string? SectionId = null,
    double RotationDegrees = 0,
    bool ShearCorrection = false);

public sealed record ProjectSupport(
    string Id,
    string NodeId,
    bool FixX,
    bool FixY,
    bool FixZ,
    bool FixRx,
    bool FixRy,
    bool FixRz);

public sealed record LoadCaseDefinition(
    string Id,
    string Name,
    string Symbol,
    string ElementSetId = "1",
    string SupportSetId = "1",
    string MemberSpringSetId = "1",
    string JointSetId = "1",
    double MovingLoadPitch = 0.1);

public sealed record NodalLoadDefinition(
    string Id,
    string CaseId,
    string NodeId,
    double Fx,
    double Fy,
    double Fz,
    double Mx,
    double My,
    double Mz);

public sealed record DerivedResultTerm(string SourceId, double Factor);

public sealed class DerivedResultDefinition
{
    public DerivedResultDefinition(
        string id,
        string name,
        DerivedResultKind kind,
        IEnumerable<DerivedResultTerm> terms)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Terms = DocumentCollections.Freeze(terms, nameof(terms));
    }

    public string Id { get; }

    public string Name { get; }

    public DerivedResultKind Kind { get; }

    public IReadOnlyList<DerivedResultTerm> Terms { get; }
}

public sealed class MovingLoadDefinition
{
    public MovingLoadDefinition(string id, string name, IEnumerable<string> caseIds)
    {
        Id = id;
        Name = name;
        CaseIds = DocumentCollections.Freeze(caseIds, nameof(caseIds));
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<string> CaseIds { get; }
}

public sealed class ProjectSelection
{
    public static ProjectSelection Empty { get; } = new([], []);

    public ProjectSelection(IEnumerable<string> nodeIds, IEnumerable<string> memberIds)
    {
        NodeIds = DocumentCollections.Freeze(nodeIds, nameof(nodeIds));
        MemberIds = DocumentCollections.Freeze(memberIds, nameof(memberIds));
    }

    public IReadOnlyList<string> NodeIds { get; }

    public IReadOnlyList<string> MemberIds { get; }
}

/// <summary>
/// Typed input document for the desktop client. Runtime analysis results are deliberately not members
/// of this aggregate and therefore cannot leak into the persisted project contract.
/// </summary>
public sealed class ProjectDocument
{
    public const int CurrentVersion = 1;
    public const string ContractKind = "frameweb_project";

    public ProjectDocument(
        int version,
        ProjectMetadata metadata,
        IEnumerable<ProjectNode> nodes,
        IEnumerable<ProjectMember> members,
        IEnumerable<ProjectSupport> supports,
        IEnumerable<LoadCaseDefinition> loadCases,
        IEnumerable<NodalLoadDefinition> nodalLoads,
        IEnumerable<DerivedResultDefinition> derivedResults,
        IEnumerable<MovingLoadDefinition> movingLoads,
        ProjectSelection? selection = null,
        bool isDirty = false,
        IEnumerable<FrameSectionDefinition>? sections = null,
        ModelDimension dimension = ModelDimension.ThreeDimensional,
        IEnumerable<ElementPropertySetDefinition>? elementPropertySets = null,
        IEnumerable<RigidZoneDefinition>? rigidZones = null,
        IEnumerable<SupportSetDefinition>? supportSets = null,
        IEnumerable<PanelDefinition>? panels = null,
        IEnumerable<JointReleaseSetDefinition>? jointReleaseSets = null,
        IEnumerable<NoticePointDefinition>? noticePoints = null,
        IEnumerable<MemberSpringSetDefinition>? memberSpringSets = null,
        IEnumerable<PrescribedDisplacementDefinition>? prescribedDisplacements = null,
        IEnumerable<MemberLoadDefinition>? memberLoads = null)
    {
        Version = version;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Nodes = DocumentCollections.Freeze(nodes, nameof(nodes));
        Sections = DocumentCollections.Freeze(sections ?? [], nameof(sections));
        Members = DocumentCollections.Freeze(members, nameof(members));
        Supports = DocumentCollections.Freeze(supports, nameof(supports));
        LoadCases = DocumentCollections.Freeze(loadCases, nameof(loadCases));
        NodalLoads = DocumentCollections.Freeze(nodalLoads, nameof(nodalLoads));
        DerivedResults = DocumentCollections.Freeze(derivedResults, nameof(derivedResults));
        MovingLoads = DocumentCollections.Freeze(movingLoads, nameof(movingLoads));
        Dimension = dimension;
        ElementPropertySets = DocumentCollections.Freeze(elementPropertySets ?? [], nameof(elementPropertySets));
        RigidZones = DocumentCollections.Freeze(rigidZones ?? [], nameof(rigidZones));
        SupportSets = DocumentCollections.Freeze(supportSets ?? [], nameof(supportSets));
        Panels = DocumentCollections.Freeze(panels ?? [], nameof(panels));
        JointReleaseSets = DocumentCollections.Freeze(jointReleaseSets ?? [], nameof(jointReleaseSets));
        NoticePoints = DocumentCollections.Freeze(noticePoints ?? [], nameof(noticePoints));
        MemberSpringSets = DocumentCollections.Freeze(memberSpringSets ?? [], nameof(memberSpringSets));
        PrescribedDisplacements = DocumentCollections.Freeze(
            prescribedDisplacements ?? [],
            nameof(prescribedDisplacements));
        MemberLoads = DocumentCollections.Freeze(memberLoads ?? [], nameof(memberLoads));
        Selection = selection ?? ProjectSelection.Empty;
        IsDirty = isDirty;
        ProjectDocumentValidator.Validate(this);
    }

    public int Version { get; }

    public ProjectMetadata Metadata { get; }

    public IReadOnlyList<ProjectNode> Nodes { get; }

    /// <summary>
    /// Persisted physical material/section values used by member analysis. A document may omit
    /// these values for backwards-compatible editing, but it is not analysis-ready until every
    /// member references one complete section.
    /// </summary>
    public IReadOnlyList<FrameSectionDefinition> Sections { get; }

    public IReadOnlyList<ProjectMember> Members { get; }

    public IReadOnlyList<ProjectSupport> Supports { get; }

    public IReadOnlyList<LoadCaseDefinition> LoadCases { get; }

    public IReadOnlyList<NodalLoadDefinition> NodalLoads { get; }

    public IReadOnlyList<DerivedResultDefinition> DerivedResults { get; }

    public IReadOnlyList<MovingLoadDefinition> MovingLoads { get; }

    public ModelDimension Dimension { get; }

    /// <summary>Additional property tables. The existing <see cref="Sections"/> collection is table "1".</summary>
    public IReadOnlyList<ElementPropertySetDefinition> ElementPropertySets { get; }

    public IReadOnlyList<RigidZoneDefinition> RigidZones { get; }

    /// <summary>Additional support tables. The existing <see cref="Supports"/> collection is table "1".</summary>
    public IReadOnlyList<SupportSetDefinition> SupportSets { get; }

    public IReadOnlyList<PanelDefinition> Panels { get; }

    public IReadOnlyList<JointReleaseSetDefinition> JointReleaseSets { get; }

    public IReadOnlyList<NoticePointDefinition> NoticePoints { get; }

    public IReadOnlyList<MemberSpringSetDefinition> MemberSpringSets { get; }

    public IReadOnlyList<PrescribedDisplacementDefinition> PrescribedDisplacements { get; }

    public IReadOnlyList<MemberLoadDefinition> MemberLoads { get; }

    /// <summary>Runtime-only selection; not part of project-file serialization.</summary>
    public ProjectSelection Selection { get; }

    /// <summary>Runtime-only dirty flag; not part of project-file serialization.</summary>
    public bool IsDirty { get; }

    public ProjectDocument WithTransientState(ProjectSelection selection, bool isDirty)
        => Copy(selection, isDirty);

    public ProjectDocument MarkDirty() => Copy(Selection, true);

    public ProjectDocument MarkSaved() => Copy(Selection, false);

    private ProjectDocument Copy(ProjectSelection selection, bool isDirty)
        => new(
            Version,
            Metadata,
            Nodes,
            Members,
            Supports,
            LoadCases,
            NodalLoads,
            DerivedResults,
            MovingLoads,
            selection,
            isDirty,
            Sections,
            Dimension,
            ElementPropertySets,
            RigidZones,
            SupportSets,
            Panels,
            JointReleaseSets,
            NoticePoints,
            MemberSpringSets,
            PrescribedDisplacements,
            MemberLoads);
}

public sealed class ProjectDocumentValidationException : ArgumentException
{
    public ProjectDocumentValidationException(string message, string? paramName = null)
        : this(new ProjectDocumentValidationIssue("validation", message), paramName)
    {
    }

    public ProjectDocumentValidationException(ProjectDocumentValidationIssue issue, string? paramName = null)
        : base((issue ?? throw new ArgumentNullException(nameof(issue))).Message, paramName)
    {
        Issue = issue;
    }

    public ProjectDocumentValidationIssue Issue { get; }
}

public static class ProjectDocumentValidator
{
    public const int MaximumLoadCases = 256;
    private const double PanelRelativeLengthTolerance = 1e-9;
    private const double PanelAbsoluteLengthTolerance = 1e-12;
    private const double PanelMachineEpsilon = 2.2204460492503131e-16;

    public static void Validate(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Require(document.Version == ProjectDocument.CurrentVersion,
            $"Project document version {document.Version} is not supported.");
        ValidateId(document.Metadata.Name, "metadata.name");
        ValidateId(document.Metadata.UnitSystem, "metadata.unit_system");
        Require(Enum.IsDefined(document.Dimension), "Model dimension is invalid.", "invalid_dimension");

        HashSet<string> nodeIds = UniqueIds(document.Nodes.Select(node => node.Id), "node");
        HashSet<string> sectionIds = UniqueIds(document.Sections.Select(section => section.Id), "section");
        HashSet<string> memberIds = UniqueIds(document.Members.Select(member => member.Id), "member");
        UniqueIds(document.Supports.Select(support => support.Id), "support");
        HashSet<string> caseIds = UniqueIds(document.LoadCases.Select(loadCase => loadCase.Id), "load case");
        Require(document.LoadCases.Count <= MaximumLoadCases,
            $"Project document cannot contain more than {MaximumLoadCases} load cases.",
            "case_limit");
        HashSet<string> nodalLoadIds = UniqueIds(document.NodalLoads.Select(load => load.Id), "nodal load");
        HashSet<string> prescribedDisplacementIds = UniqueIds(
            document.PrescribedDisplacements.Select(load => load.Id), "prescribed displacement");
        string? overlappingNodeLoadId = nodalLoadIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault(prescribedDisplacementIds.Contains);
        Require(overlappingNodeLoadId is null,
            $"Nodal-load and prescribed-displacement IDs must be distinct; duplicate ID '{overlappingNodeLoadId}'.",
            "duplicate_id", "nodal_loads", overlappingNodeLoadId, "id");
        UniqueIds(document.MemberLoads.Select(load => load.Id), "member load");
        UniqueIds(document.RigidZones.Select(zone => zone.Id), "rigid zone");
        UniqueIds(document.Panels.Select(panel => panel.Id), "panel");
        UniqueIds(document.NoticePoints.Select(point => point.Id), "notice point");
        HashSet<string> derivedIds = UniqueIds(document.DerivedResults.Select(result => result.Id), "derived result");
        HashSet<string> movingLoadIds = UniqueIds(document.MovingLoads.Select(load => load.Id), "moving load");

        Require(!caseIds.Overlaps(derivedIds), "Load-case and derived-result IDs must be distinct.");
        Require(!caseIds.Overlaps(movingLoadIds), "Load-case and moving-load IDs must be distinct.");
        Require(!derivedIds.Overlaps(movingLoadIds), "Derived-result and moving-load IDs must be distinct.");

        foreach (ProjectNode node in document.Nodes)
        {
            RequireFinite(node.X, $"node '{node.Id}' x");
            RequireFinite(node.Y, $"node '{node.Id}' y");
            RequireFinite(node.Z, $"node '{node.Id}' z");
            if (document.Dimension == ModelDimension.TwoDimensional)
            {
                Require(node.Z == 0, $"Two-dimensional node '{node.Id}' must have z equal to zero.",
                    "invalid_dimension", "nodes", node.Id, "z");
            }
        }

        foreach (FrameSectionDefinition section in document.Sections)
        {
            ValidateSection(section, "section");
        }

        Dictionary<string, HashSet<string>> propertySections = new(StringComparer.Ordinal)
        {
            ["1"] = sectionIds,
        };
        HashSet<string> propertySetIds = UniqueIds(
            document.ElementPropertySets.Select(set => set.Id), "element property set");
        Require(!propertySetIds.Contains("1"),
            "Element property set '1' is represented by ProjectDocument.Sections and cannot be repeated.",
            "duplicate_id");
        foreach (ElementPropertySetDefinition set in document.ElementPropertySets)
        {
            ValidateId(set.Name, $"element property set '{set.Id}' name");
            HashSet<string> ids = UniqueIds(set.Sections.Select(section => section.Id),
                $"element property set '{set.Id}' section");
            foreach (FrameSectionDefinition section in set.Sections)
            {
                ValidateSection(section, $"element property set '{set.Id}' section");
            }

            propertySections.Add(set.Id, ids);
        }

        Dictionary<string, ProjectNode> nodesById = document.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        Dictionary<string, ProjectMember> membersById = document.Members.ToDictionary(
            member => member.Id, StringComparer.Ordinal);

        foreach (ProjectMember member in document.Members)
        {
            Require(nodeIds.Contains(member.NodeI), $"Member '{member.Id}' references unknown node_i '{member.NodeI}'.");
            Require(nodeIds.Contains(member.NodeJ), $"Member '{member.Id}' references unknown node_j '{member.NodeJ}'.");
            Require(member.NodeI != member.NodeJ, $"Member '{member.Id}' cannot connect a node to itself.");
            if (member.SectionId is not null)
            {
                ValidateId(member.SectionId, $"member '{member.Id}' section_id");
                Require(sectionIds.Contains(member.SectionId),
                    $"Member '{member.Id}' references unknown section '{member.SectionId}'.");
            }

            RequireFinite(member.RotationDegrees, $"member '{member.Id}' rotation_degrees");
            MemberLength(member, nodesById);
        }

        string duplicateMemberPair = document.Members
            .GroupBy(member => string.CompareOrdinal(member.NodeI, member.NodeJ) <= 0
                ? $"{member.NodeI}\u001f{member.NodeJ}"
                : $"{member.NodeJ}\u001f{member.NodeI}", StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key ?? string.Empty;
        Require(duplicateMemberPair.Length == 0,
            "Members cannot duplicate the same node pair.", "duplicate_reference");

        foreach (ProjectSupport support in document.Supports)
        {
            Require(nodeIds.Contains(support.NodeId), $"Support '{support.Id}' references an unknown node.");
        }
        Require(document.Supports.Select(support => support.NodeId).Distinct(StringComparer.Ordinal).Count()
                == document.Supports.Count,
            "Default support table cannot contain multiple rows for the same node.", "duplicate_reference");

        HashSet<string> supportSetIds = UniqueIds(document.SupportSets.Select(set => set.Id), "support set");
        Require(!supportSetIds.Contains("1"),
            "Support set '1' is represented by ProjectDocument.Supports and cannot be repeated.",
            "duplicate_id");
        foreach (SupportSetDefinition set in document.SupportSets)
        {
            ValidateId(set.Name, $"support set '{set.Id}' name");
            UniqueIds(set.Rows.Select(row => row.Id), $"support set '{set.Id}' row");
            Require(set.Rows.Select(row => row.NodeId).Distinct(StringComparer.Ordinal).Count() == set.Rows.Count,
                $"Support set '{set.Id}' cannot contain multiple rows for the same node.",
                "duplicate_reference");
            foreach (SupportConditionDefinition row in set.Rows)
            {
                Require(nodeIds.Contains(row.NodeId),
                    $"Support condition '{row.Id}' references unknown node '{row.NodeId}'.");
                ValidateFinite(row.Tx, row.Ty, row.Tz, row.Rx, row.Ry, row.Rz,
                    $"support condition '{row.Id}'");
            }
        }

        HashSet<string> jointSetIds = UniqueIds(document.JointReleaseSets.Select(set => set.Id), "joint set");
        foreach (JointReleaseSetDefinition set in document.JointReleaseSets)
        {
            ValidateId(set.Name, $"joint set '{set.Id}' name");
            UniqueIds(set.Rows.Select(row => row.Id), $"joint set '{set.Id}' row");
            Require(set.Rows.Select(row => row.MemberId).Distinct(StringComparer.Ordinal).Count() == set.Rows.Count,
                $"Joint set '{set.Id}' cannot contain multiple rows for the same member.",
                "duplicate_reference");
            foreach (JointReleaseDefinition row in set.Rows)
            {
                Require(memberIds.Contains(row.MemberId),
                    $"Joint release '{row.Id}' references unknown member '{row.MemberId}'.");
            }
        }

        HashSet<string> springSetIds = UniqueIds(document.MemberSpringSets.Select(set => set.Id),
            "member spring set");
        foreach (MemberSpringSetDefinition set in document.MemberSpringSets)
        {
            ValidateId(set.Name, $"member spring set '{set.Id}' name");
            UniqueIds(set.Rows.Select(row => row.Id), $"member spring set '{set.Id}' row");
            Require(set.Rows.Select(row => row.MemberId).Distinct(StringComparer.Ordinal).Count() == set.Rows.Count,
                $"Member spring set '{set.Id}' cannot contain multiple rows for the same member.",
                "duplicate_reference");
            foreach (MemberSpringDefinition row in set.Rows)
            {
                Require(memberIds.Contains(row.MemberId),
                    $"Member spring '{row.Id}' references unknown member '{row.MemberId}'.");
                ValidateFinite(row.Tx, row.Ty, row.Tz, row.Tr, $"member spring '{row.Id}'");
            }
        }

        HashSet<string> availableSupportSets = new(supportSetIds, StringComparer.Ordinal) { "1" };
        HashSet<string> availableJointSets = new(jointSetIds, StringComparer.Ordinal) { "1" };
        HashSet<string> availableSpringSets = new(springSetIds, StringComparer.Ordinal) { "1" };

        foreach (LoadCaseDefinition loadCase in document.LoadCases)
        {
            ValidateId(loadCase.Name, $"load case '{loadCase.Id}' name");
            ValidateId(loadCase.Symbol, $"load case '{loadCase.Id}' symbol");
            ValidateId(loadCase.ElementSetId, $"load case '{loadCase.Id}' element_set_id");
            ValidateId(loadCase.SupportSetId, $"load case '{loadCase.Id}' support_set_id");
            ValidateId(loadCase.MemberSpringSetId, $"load case '{loadCase.Id}' member_spring_set_id");
            ValidateId(loadCase.JointSetId, $"load case '{loadCase.Id}' joint_set_id");
            Require(propertySections.ContainsKey(loadCase.ElementSetId),
                $"Load case '{loadCase.Id}' references unknown element property set '{loadCase.ElementSetId}'.");
            Require(availableSupportSets.Contains(loadCase.SupportSetId),
                $"Load case '{loadCase.Id}' references unknown support set '{loadCase.SupportSetId}'.");
            Require(availableSpringSets.Contains(loadCase.MemberSpringSetId),
                $"Load case '{loadCase.Id}' references unknown member spring set '{loadCase.MemberSpringSetId}'.");
            Require(availableJointSets.Contains(loadCase.JointSetId),
                $"Load case '{loadCase.Id}' references unknown joint set '{loadCase.JointSetId}'.");
            RequireFinite(loadCase.MovingLoadPitch, $"load case '{loadCase.Id}' moving_load_pitch");
            Require(loadCase.MovingLoadPitch >= 0,
                $"Load case '{loadCase.Id}' moving_load_pitch cannot be negative.");

            HashSet<string> selectedSections = propertySections[loadCase.ElementSetId];
            foreach (ProjectMember member in document.Members.Where(member => member.SectionId is not null))
            {
                Require(selectedSections.Contains(member.SectionId!),
                    $"Load case '{loadCase.Id}' property set does not define member '{member.Id}' section '{member.SectionId}'.");
            }
        }

        foreach (RigidZoneDefinition zone in document.RigidZones)
        {
            Require(membersById.TryGetValue(zone.MemberId, out ProjectMember? member),
                $"Rigid zone '{zone.Id}' references unknown member '{zone.MemberId}'.");
            RequireFinite(zone.ILength, $"rigid zone '{zone.Id}' i_length");
            RequireFinite(zone.JLength, $"rigid zone '{zone.Id}' j_length");
            Require(zone.ILength >= 0 && zone.JLength >= 0,
                $"Rigid zone '{zone.Id}' lengths cannot be negative.", "invalid_dimension");
            double length = MemberLength(member!, nodesById);
            Require(zone.ILength + zone.JLength <= length,
                $"Rigid zone '{zone.Id}' lengths exceed member '{zone.MemberId}' length.", "invalid_dimension");
            Require(propertySections.Values.All(ids => ids.Contains(zone.SectionId)),
                $"Rigid zone '{zone.Id}' section '{zone.SectionId}' is not defined in every property set.");
        }
        Require(document.RigidZones.Select(zone => zone.MemberId).Distinct(StringComparer.Ordinal).Count()
                == document.RigidZones.Count,
            "Rigid zones cannot contain multiple rows for the same member.", "duplicate_reference");

        foreach (PanelDefinition panel in document.Panels)
        {
            Require(panel.NodeIds.Count == 4,
                $"Panel '{panel.Id}' must contain exactly four nodes.",
                "invalid_dimension", "panels", panel.Id, "node_ids");
            UniqueIds(panel.NodeIds, $"panel '{panel.Id}' node");
            Require(panel.NodeIds.All(nodeIds.Contains),
                $"Panel '{panel.Id}' references an unknown node.");
            Require(propertySections.Values.All(ids => ids.Contains(panel.SectionId)),
                $"Panel '{panel.Id}' section '{panel.SectionId}' is not defined in every property set.");
            ValidatePanelGeometry(panel, panel.NodeIds.Select(id => nodesById[id]).ToArray());
        }

        foreach (IGrouping<string, NoticePointDefinition> group in document.NoticePoints
            .GroupBy(point => point.MemberId, StringComparer.Ordinal))
        {
            Require(memberIds.Contains(group.Key), $"Notice points reference unknown member '{group.Key}'.");
            Require(group.Count() <= 20, $"Member '{group.Key}' cannot have more than 20 notice points.",
                "dimension_limit");
            double length = MemberLength(membersById[group.Key], nodesById);
            HashSet<double> distances = [];
            foreach (NoticePointDefinition point in group)
            {
                RequireFinite(point.Distance, $"notice point '{point.Id}' distance");
                Require(point.Distance > 0 && point.Distance < length,
                    $"Notice point '{point.Id}' must be inside member '{point.MemberId}'.", "invalid_dimension");
                Require(distances.Add(point.Distance),
                    $"Member '{point.MemberId}' contains duplicate notice-point distances.", "duplicate_reference");
            }
        }

        foreach (NodalLoadDefinition load in document.NodalLoads)
        {
            Require(caseIds.Contains(load.CaseId), $"Nodal load '{load.Id}' references an unknown load case.");
            Require(nodeIds.Contains(load.NodeId), $"Nodal load '{load.Id}' references an unknown node.");
            RequireFinite(load.Fx, $"nodal load '{load.Id}' fx");
            RequireFinite(load.Fy, $"nodal load '{load.Id}' fy");
            RequireFinite(load.Fz, $"nodal load '{load.Id}' fz");
            RequireFinite(load.Mx, $"nodal load '{load.Id}' mx");
            RequireFinite(load.My, $"nodal load '{load.Id}' my");
            RequireFinite(load.Mz, $"nodal load '{load.Id}' mz");
        }

        foreach (PrescribedDisplacementDefinition load in document.PrescribedDisplacements)
        {
            Require(caseIds.Contains(load.CaseId),
                $"Prescribed displacement '{load.Id}' references an unknown load case.");
            Require(nodeIds.Contains(load.NodeId),
                $"Prescribed displacement '{load.Id}' references an unknown node.");
            ValidateFinite(load.Dx, load.Dy, load.Dz, load.Rx, load.Ry, load.Rz,
                $"prescribed displacement '{load.Id}'");
        }

        foreach (MemberLoadDefinition load in document.MemberLoads)
        {
            Require(caseIds.Contains(load.CaseId), $"Member load '{load.Id}' references an unknown load case.");
            Require(membersById.TryGetValue(load.MemberId, out ProjectMember? member),
                $"Member load '{load.Id}' references unknown member '{load.MemberId}'.");
            Require(Enum.IsDefined(load.Kind), $"Member load '{load.Id}' has an invalid kind.");
            Require(Enum.IsDefined(load.Direction), $"Member load '{load.Id}' has an invalid direction.");
            ValidateFinite(load.L1, load.L2, load.P1, load.P2, $"member load '{load.Id}'");
            double length = MemberLength(member!, nodesById);
            if (load.Kind is MemberLoadKind.PointForce or MemberLoadKind.PointMoment)
            {
                Require(load.L1 >= 0 && load.L1 <= length,
                    $"Member load '{load.Id}' L1 point position is outside member '{load.MemberId}'.",
                    "invalid_dimension", "member_loads", load.Id, "l1");
                Require(load.L2 >= 0 && load.L2 <= length,
                    $"Member load '{load.Id}' L2 point position is outside member '{load.MemberId}'.",
                    "invalid_dimension", "member_loads", load.Id, "l2");
            }
            else if (load.Kind == MemberLoadKind.DistributedForce)
            {
                double end = load.L2 < 0 ? load.L1 - load.L2 : length - load.L2;
                Require(load.L1 >= 0 && load.L1 < end && end <= length,
                    $"Member load '{load.Id}' distributed extent is invalid for member '{load.MemberId}'.",
                    "invalid_dimension");
            }
        }

        HashSet<string> availableSources = new(caseIds, StringComparer.Ordinal);
        foreach (DerivedResultDefinition derived in document.DerivedResults)
        {
            ValidateId(derived.Name, $"derived result '{derived.Id}' name");
            Require(derived.Terms.Count >= 1, $"Derived result '{derived.Id}' must contain at least one term.");
            foreach (DerivedResultTerm term in derived.Terms)
            {
                ValidateId(term.SourceId, $"derived result '{derived.Id}' source_id");
                Require(availableSources.Contains(term.SourceId),
                    $"Derived result '{derived.Id}' references unavailable source '{term.SourceId}'.");
                RequireFinite(term.Factor, $"derived result '{derived.Id}' factor");
            }

            availableSources.Add(derived.Id);
        }

        foreach (MovingLoadDefinition movingLoad in document.MovingLoads)
        {
            ValidateId(movingLoad.Name, $"moving load '{movingLoad.Id}' name");
            Require(movingLoad.CaseIds.Count >= 1, $"Moving load '{movingLoad.Id}' must contain at least one case.");
            UniqueIds(movingLoad.CaseIds, $"moving load '{movingLoad.Id}' case");
            Require(movingLoad.CaseIds.All(caseIds.Contains),
                $"Moving load '{movingLoad.Id}' references an unknown load case.");
        }

        UniqueIds(document.Selection.NodeIds, "selected node");
        UniqueIds(document.Selection.MemberIds, "selected member");
        Require(document.Selection.NodeIds.All(nodeIds.Contains), "Selection contains an unknown node.");
        Require(document.Selection.MemberIds.All(memberIds.Contains), "Selection contains an unknown member.");
    }

    private static HashSet<string> UniqueIds(IEnumerable<string> values, string description)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            ValidateId(value, $"{description} ID");
            Require(result.Add(value), $"Duplicate {description} ID '{value}'.");
        }

        return result;
    }

    private static void ValidateSection(FrameSectionDefinition section, string description)
    {
        ValidateId(section.Name, $"{description} '{section.Id}' name");
        RequirePositiveFinite(section.YoungsModulus, $"{description} '{section.Id}' youngs_modulus");
        RequireFinite(section.PoissonRatio, $"{description} '{section.Id}' poisson_ratio");
        Require(section.PoissonRatio > -1 && section.PoissonRatio < 0.5,
            $"{description} '{section.Id}' poisson_ratio must be greater than -1 and less than 0.5.");
        RequirePositiveFinite(section.ShearModulus, $"{description} '{section.Id}' shear_modulus");
        RequirePositiveFinite(section.Area, $"{description} '{section.Id}' area");
        RequirePositiveFinite(section.MomentOfInertiaY, $"{description} '{section.Id}' moment_of_inertia_y");
        RequirePositiveFinite(section.MomentOfInertiaZ, $"{description} '{section.Id}' moment_of_inertia_z");
        RequirePositiveFinite(section.TorsionConstant, $"{description} '{section.Id}' torsion_constant");
        RequireFinite(section.ThermalExpansionCoefficient, $"{description} '{section.Id}' thermal_expansion");
        RequireFinite(section.Density, $"{description} '{section.Id}' density");
        Require(section.Density >= 0, $"{description} '{section.Id}' density cannot be negative.");
        RequireFinite(section.PanelThickness, $"{description} '{section.Id}' panel_thickness");
        Require(section.PanelThickness >= 0,
            $"{description} '{section.Id}' panel_thickness cannot be negative.");
    }

    private static double MemberLength(ProjectMember member, IReadOnlyDictionary<string, ProjectNode> nodes)
    {
        ProjectNode i = nodes[member.NodeI];
        ProjectNode j = nodes[member.NodeJ];
        double dx = j.X - i.X;
        double dy = j.Y - i.Y;
        double dz = j.Z - i.Z;
        double length = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        RequirePositiveFinite(length, $"member '{member.Id}' length");
        return length;
    }

    private static void ValidatePanelGeometry(PanelDefinition panel, IReadOnlyList<ProjectNode> nodes)
    {
        double coordinateMagnitude = nodes.Max(node => Math.Max(
            Math.Abs(node.X), Math.Max(Math.Abs(node.Y), Math.Abs(node.Z))));
        double span = 0;
        for (int first = 0; first < nodes.Count - 1; first++)
        {
            for (int second = first + 1; second < nodes.Count; second++)
            {
                span = Math.Max(span, Distance(nodes[first], nodes[second]));
            }
        }

        Require(double.IsFinite(span) && span > 0,
            $"Panel '{panel.Id}' must have nonzero finite extent.",
            "invalid_geometry", "panels", panel.Id, "node_ids");

        double lengthTolerance = Math.Max(
            PanelAbsoluteLengthTolerance,
            Math.Max(
                span * PanelRelativeLengthTolerance,
                coordinateMagnitude * 64 * PanelMachineEpsilon));
        for (int first = 0; first < nodes.Count - 1; first++)
        {
            for (int second = first + 1; second < nodes.Count; second++)
            {
                Require(Distance(nodes[first], nodes[second]) > lengthTolerance,
                    $"Panel '{panel.Id}' nodes must have distinct coordinates.",
                    "invalid_geometry", "panels", panel.Id, "node_ids");
            }
        }

        ProjectNode origin = nodes[0];
        PanelVector3[] normalized = nodes
            .Select(node => new PanelVector3(
                (node.X - origin.X) / span,
                (node.Y - origin.Y) / span,
                (node.Z - origin.Z) / span))
            .ToArray();
        double normalizedTolerance = lengthTolerance / span;
        double areaTolerance = Math.Max(1e-12, normalizedTolerance);

        PanelVector3 bestNormal = default;
        double bestNormalLength = 0;
        for (int first = 0; first < normalized.Length - 2; first++)
        {
            for (int second = first + 1; second < normalized.Length - 1; second++)
            {
                for (int third = second + 1; third < normalized.Length; third++)
                {
                    PanelVector3 candidate = Cross(
                        Subtract(normalized[second], normalized[first]),
                        Subtract(normalized[third], normalized[first]));
                    double candidateLength = VectorLength(candidate);
                    if (candidateLength > bestNormalLength)
                    {
                        bestNormal = candidate;
                        bestNormalLength = candidateLength;
                    }
                }
            }
        }

        Require(double.IsFinite(bestNormalLength) && bestNormalLength > areaTolerance,
            $"Panel '{panel.Id}' must have nonzero area.",
            "invalid_geometry", "panels", panel.Id, "node_ids");
        PanelVector3 unitNormal = new(
            bestNormal.X / bestNormalLength,
            bestNormal.Y / bestNormalLength,
            bestNormal.Z / bestNormalLength);
        Require(normalized.All(point => Math.Abs(Dot(point, unitNormal)) <= normalizedTolerance),
            $"Panel '{panel.Id}' nodes must be planar.",
            "invalid_geometry", "panels", panel.Id, "node_ids");

        PanelPoint2[] projected = normalized.Select(point => Project(point, unitNormal)).ToArray();
        for (int index = 0; index < projected.Length; index++)
        {
            PanelPoint2 previous = projected[(index + projected.Length - 1) % projected.Length];
            PanelPoint2 current = projected[index];
            PanelPoint2 next = projected[(index + 1) % projected.Length];
            Require(Math.Abs(Orientation(previous, current, next)) > areaTolerance,
                $"Panel '{panel.Id}' must have four non-collinear ordered corners.",
                "invalid_geometry", "panels", panel.Id, "node_ids");
        }

        bool selfIntersects = SegmentsIntersect(
                projected[0], projected[1], projected[2], projected[3], areaTolerance, normalizedTolerance)
            || SegmentsIntersect(
                projected[1], projected[2], projected[3], projected[0], areaTolerance, normalizedTolerance);
        Require(!selfIntersects,
            $"Panel '{panel.Id}' node order creates self-intersecting edges.",
            "invalid_geometry", "panels", panel.Id, "node_ids");

        double twiceArea = 0;
        for (int index = 0; index < projected.Length; index++)
        {
            PanelPoint2 current = projected[index];
            PanelPoint2 next = projected[(index + 1) % projected.Length];
            twiceArea += (current.X * next.Y) - (current.Y * next.X);
        }

        Require(double.IsFinite(twiceArea) && Math.Abs(twiceArea) > areaTolerance,
            $"Panel '{panel.Id}' must have nonzero ordered area.",
            "invalid_geometry", "panels", panel.Id, "node_ids");
    }

    private static double Distance(ProjectNode first, ProjectNode second)
        => VectorLength(new PanelVector3(
            second.X - first.X,
            second.Y - first.Y,
            second.Z - first.Z));

    private static PanelVector3 Subtract(PanelVector3 left, PanelVector3 right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    private static PanelVector3 Cross(PanelVector3 left, PanelVector3 right)
        => new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));

    private static double Dot(PanelVector3 left, PanelVector3 right)
        => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private static double VectorLength(PanelVector3 value)
    {
        double scale = Math.Max(Math.Abs(value.X), Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
        if (scale == 0 || !double.IsFinite(scale))
        {
            return scale;
        }

        double x = value.X / scale;
        double y = value.Y / scale;
        double z = value.Z / scale;
        return scale * Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    private static PanelPoint2 Project(PanelVector3 point, PanelVector3 normal)
    {
        double x = Math.Abs(normal.X);
        double y = Math.Abs(normal.Y);
        double z = Math.Abs(normal.Z);
        if (x >= y && x >= z)
        {
            return new PanelPoint2(point.Y, point.Z);
        }

        return y >= z
            ? new PanelPoint2(point.X, point.Z)
            : new PanelPoint2(point.X, point.Y);
    }

    private static bool SegmentsIntersect(
        PanelPoint2 firstStart,
        PanelPoint2 firstEnd,
        PanelPoint2 secondStart,
        PanelPoint2 secondEnd,
        double areaTolerance,
        double lengthTolerance)
    {
        double firstSideStart = Orientation(firstStart, firstEnd, secondStart);
        double firstSideEnd = Orientation(firstStart, firstEnd, secondEnd);
        double secondSideStart = Orientation(secondStart, secondEnd, firstStart);
        double secondSideEnd = Orientation(secondStart, secondEnd, firstEnd);
        if (OppositeSides(firstSideStart, firstSideEnd, areaTolerance) &&
            OppositeSides(secondSideStart, secondSideEnd, areaTolerance))
        {
            return true;
        }

        return (Math.Abs(firstSideStart) <= areaTolerance &&
                OnSegment(firstStart, firstEnd, secondStart, lengthTolerance))
            || (Math.Abs(firstSideEnd) <= areaTolerance &&
                OnSegment(firstStart, firstEnd, secondEnd, lengthTolerance))
            || (Math.Abs(secondSideStart) <= areaTolerance &&
                OnSegment(secondStart, secondEnd, firstStart, lengthTolerance))
            || (Math.Abs(secondSideEnd) <= areaTolerance &&
                OnSegment(secondStart, secondEnd, firstEnd, lengthTolerance));
    }

    private static bool OppositeSides(double first, double second, double tolerance)
        => (first > tolerance && second < -tolerance) || (first < -tolerance && second > tolerance);

    private static bool OnSegment(
        PanelPoint2 start,
        PanelPoint2 end,
        PanelPoint2 point,
        double tolerance)
        => point.X >= Math.Min(start.X, end.X) - tolerance &&
           point.X <= Math.Max(start.X, end.X) + tolerance &&
           point.Y >= Math.Min(start.Y, end.Y) - tolerance &&
           point.Y <= Math.Max(start.Y, end.Y) + tolerance;

    private static double Orientation(PanelPoint2 first, PanelPoint2 second, PanelPoint2 third)
        => ((second.X - first.X) * (third.Y - first.Y))
            - ((second.Y - first.Y) * (third.X - first.X));

    private readonly record struct PanelVector3(double X, double Y, double Z);

    private readonly record struct PanelPoint2(double X, double Y);

    private static void ValidateFinite(double first, double second, double third, double fourth, string description)
    {
        RequireFinite(first, description);
        RequireFinite(second, description);
        RequireFinite(third, description);
        RequireFinite(fourth, description);
    }

    private static void ValidateFinite(
        double first,
        double second,
        double third,
        double fourth,
        double fifth,
        double sixth,
        string description)
    {
        ValidateFinite(first, second, third, fourth, description);
        RequireFinite(fifth, description);
        RequireFinite(sixth, description);
    }

    private static void ValidateId(string value, string description)
        => Require(!string.IsNullOrWhiteSpace(value), $"{description} must be a nonblank string.");

    private static void RequireFinite(double value, string description)
        => Require(double.IsFinite(value), $"{description} must be finite.");

    private static void RequirePositiveFinite(double value, string description)
    {
        RequireFinite(value, description);
        Require(value > 0, $"{description} must be positive.");
    }

    private static void Require(
        bool condition,
        string message,
        string code = "validation",
        string? collection = null,
        string? entityId = null,
        string? field = null)
    {
        if (!condition)
        {
            throw new ProjectDocumentValidationException(
                new ProjectDocumentValidationIssue(code, message, collection, entityId, field));
        }
    }
}
