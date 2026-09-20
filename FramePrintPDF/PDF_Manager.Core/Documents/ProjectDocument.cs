using System.Collections.ObjectModel;

namespace PDF_Manager.Core.Documents;

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
    double TorsionConstant);

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

public sealed record LoadCaseDefinition(string Id, string Name, string Symbol);

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
        IEnumerable<FrameSectionDefinition>? sections = null)
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
            Sections);
}

public sealed class ProjectDocumentValidationException : ArgumentException
{
    public ProjectDocumentValidationException(string message, string? paramName = null)
        : base(message, paramName)
    {
    }
}

public static class ProjectDocumentValidator
{
    public static void Validate(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Require(document.Version == ProjectDocument.CurrentVersion,
            $"Project document version {document.Version} is not supported.");
        ValidateId(document.Metadata.Name, "metadata.name");
        ValidateId(document.Metadata.UnitSystem, "metadata.unit_system");

        HashSet<string> nodeIds = UniqueIds(document.Nodes.Select(node => node.Id), "node");
        HashSet<string> sectionIds = UniqueIds(document.Sections.Select(section => section.Id), "section");
        HashSet<string> memberIds = UniqueIds(document.Members.Select(member => member.Id), "member");
        UniqueIds(document.Supports.Select(support => support.Id), "support");
        HashSet<string> caseIds = UniqueIds(document.LoadCases.Select(loadCase => loadCase.Id), "load case");
        UniqueIds(document.NodalLoads.Select(load => load.Id), "nodal load");
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
        }

        foreach (FrameSectionDefinition section in document.Sections)
        {
            ValidateId(section.Name, $"section '{section.Id}' name");
            RequirePositiveFinite(section.YoungsModulus, $"section '{section.Id}' youngs_modulus");
            RequireFinite(section.PoissonRatio, $"section '{section.Id}' poisson_ratio");
            Require(section.PoissonRatio > -1 && section.PoissonRatio < 0.5,
                $"Section '{section.Id}' poisson_ratio must be greater than -1 and less than 0.5.");
            RequirePositiveFinite(section.ShearModulus, $"section '{section.Id}' shear_modulus");
            RequirePositiveFinite(section.Area, $"section '{section.Id}' area");
            RequirePositiveFinite(section.MomentOfInertiaY, $"section '{section.Id}' moment_of_inertia_y");
            RequirePositiveFinite(section.MomentOfInertiaZ, $"section '{section.Id}' moment_of_inertia_z");
            RequirePositiveFinite(section.TorsionConstant, $"section '{section.Id}' torsion_constant");
        }

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
        }

        foreach (ProjectSupport support in document.Supports)
        {
            Require(nodeIds.Contains(support.NodeId), $"Support '{support.Id}' references an unknown node.");
        }

        foreach (LoadCaseDefinition loadCase in document.LoadCases)
        {
            ValidateId(loadCase.Name, $"load case '{loadCase.Id}' name");
            ValidateId(loadCase.Symbol, $"load case '{loadCase.Id}' symbol");
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

    private static void ValidateId(string value, string description)
        => Require(!string.IsNullOrWhiteSpace(value), $"{description} must be a nonblank string.");

    private static void RequireFinite(double value, string description)
        => Require(double.IsFinite(value), $"{description} must be finite.");

    private static void RequirePositiveFinite(double value, string description)
    {
        RequireFinite(value, description);
        Require(value > 0, $"{description} must be positive.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ProjectDocumentValidationException(message);
        }
    }
}
