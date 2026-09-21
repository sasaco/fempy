namespace FrameWebforCS.Core.Documents;

public enum ModelDimension
{
    TwoDimensional = 2,
    ThreeDimensional = 3,
}

public enum MemberLoadKind
{
    PointForce,
    PointMoment,
    DistributedForce,
    Thermal,
}

public enum MemberLoadDirection
{
    LocalX,
    LocalY,
    LocalZ,
    GlobalX,
    GlobalY,
    GlobalZ,
}

public enum ProjectEntityKind
{
    Node,
    Section,
    ElementPropertySet,
    Member,
    RigidZone,
    Support,
    SupportSet,
    Panel,
    JointReleaseSet,
    NoticePoint,
    MemberSpringSet,
    LoadCase,
    NodalLoad,
    PrescribedDisplacement,
    MemberLoad,
    DerivedResult,
    MovingLoad,
}

public readonly record struct ProjectEntityKey(ProjectEntityKind Kind, string Id);

public sealed record ProjectDocumentValidationIssue(
    string Code,
    string Message,
    string? Collection = null,
    string? EntityId = null,
    string? Field = null);

public sealed class ElementPropertySetDefinition
{
    public ElementPropertySetDefinition(
        string id,
        string name,
        IEnumerable<FrameSectionDefinition> sections)
    {
        Id = id;
        Name = name;
        Sections = DocumentCollections.Freeze(sections, nameof(sections));
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<FrameSectionDefinition> Sections { get; }
}

public sealed record RigidZoneDefinition(
    string Id,
    string MemberId,
    double ILength,
    double JLength,
    string SectionId);

public sealed record SupportConditionDefinition(
    string Id,
    string NodeId,
    double Tx,
    double Ty,
    double Tz,
    double Rx,
    double Ry,
    double Rz);

public sealed class SupportSetDefinition
{
    public SupportSetDefinition(
        string id,
        string name,
        IEnumerable<SupportConditionDefinition> rows)
    {
        Id = id;
        Name = name;
        Rows = DocumentCollections.Freeze(rows, nameof(rows));
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<SupportConditionDefinition> Rows { get; }
}

public sealed class PanelDefinition
{
    public PanelDefinition(string id, string sectionId, IEnumerable<string> nodeIds)
    {
        Id = id;
        SectionId = sectionId;
        NodeIds = DocumentCollections.Freeze(nodeIds, nameof(nodeIds));
    }

    public string Id { get; }

    public string SectionId { get; }

    public IReadOnlyList<string> NodeIds { get; }
}

public sealed record JointReleaseDefinition(
    string Id,
    string MemberId,
    bool ConnectXi,
    bool ConnectYi,
    bool ConnectZi,
    bool ConnectXj,
    bool ConnectYj,
    bool ConnectZj);

public sealed class JointReleaseSetDefinition
{
    public JointReleaseSetDefinition(
        string id,
        string name,
        IEnumerable<JointReleaseDefinition> rows)
    {
        Id = id;
        Name = name;
        Rows = DocumentCollections.Freeze(rows, nameof(rows));
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<JointReleaseDefinition> Rows { get; }
}

public sealed record NoticePointDefinition(string Id, string MemberId, double Distance);

public sealed record MemberSpringDefinition(
    string Id,
    string MemberId,
    double Tx,
    double Ty,
    double Tz,
    double Tr);

public sealed class MemberSpringSetDefinition
{
    public MemberSpringSetDefinition(
        string id,
        string name,
        IEnumerable<MemberSpringDefinition> rows)
    {
        Id = id;
        Name = name;
        Rows = DocumentCollections.Freeze(rows, nameof(rows));
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<MemberSpringDefinition> Rows { get; }
}

public sealed record PrescribedDisplacementDefinition(
    string Id,
    string CaseId,
    string NodeId,
    double Dx,
    double Dy,
    double Dz,
    double Rx,
    double Ry,
    double Rz);

public sealed record MemberLoadDefinition(
    string Id,
    string CaseId,
    string MemberId,
    MemberLoadKind Kind,
    MemberLoadDirection Direction,
    double L1,
    double L2,
    double P1,
    double P2);
