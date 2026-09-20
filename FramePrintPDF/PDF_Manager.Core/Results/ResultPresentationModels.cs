using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Results;

public sealed class PresentedStaticResult
{
    public PresentedStaticResult(
        string id,
        string name,
        DerivedResultKind kind,
        IEnumerable<string> sourceIds,
        IEnumerable<NodeDisplacement> nodeDisplacements,
        IEnumerable<SupportReaction> supportReactions,
        IEnumerable<MemberSectionForces> memberSectionForces,
        IEnumerable<ShellResult> shellResults,
        IEnumerable<SolidResult> solidResults)
    {
        Id = id;
        Name = name;
        Kind = kind;
        SourceIds = Array.AsReadOnly(sourceIds.ToArray());
        NodeDisplacements = Array.AsReadOnly(nodeDisplacements.ToArray());
        SupportReactions = Array.AsReadOnly(supportReactions.ToArray());
        MemberSectionForces = Array.AsReadOnly(memberSectionForces.ToArray());
        ShellResults = Array.AsReadOnly(shellResults.ToArray());
        SolidResults = Array.AsReadOnly(solidResults.ToArray());
    }

    public string Id { get; }

    public string Name { get; }

    public DerivedResultKind Kind { get; }

    public IReadOnlyList<string> SourceIds { get; }

    public IReadOnlyList<NodeDisplacement> NodeDisplacements { get; }

    public IReadOnlyList<SupportReaction> SupportReactions { get; }

    public IReadOnlyList<MemberSectionForces> MemberSectionForces { get; }

    public IReadOnlyList<ShellResult> ShellResults { get; }

    public IReadOnlyList<SolidResult> SolidResults { get; }
}

public sealed class ResultPresentationPage
{
    public ResultPresentationPage(
        string pageId,
        AnalysisCase resultCase,
        AnalysisResult primaryResult,
        IEnumerable<AnalysisResult> sourceResults,
        bool isMovingLoad)
    {
        PageId = pageId;
        ResultCase = resultCase;
        PrimaryResult = primaryResult;
        SourceResults = Array.AsReadOnly(sourceResults.ToArray());
        IsMovingLoad = isMovingLoad;
    }

    public string PageId { get; }

    public AnalysisCase ResultCase { get; }

    public AnalysisResult PrimaryResult { get; }

    public IReadOnlyList<AnalysisResult> SourceResults { get; }

    public bool IsMovingLoad { get; }
}

public sealed record EnvelopeExtreme(double Value, string CaseId);

public sealed record ScalarEnvelope(EnvelopeExtreme Maximum, EnvelopeExtreme Minimum);

public sealed record DisplacementEnvelopeComponents(
    ScalarEnvelope Dx,
    ScalarEnvelope Dy,
    ScalarEnvelope Dz,
    ScalarEnvelope Rx,
    ScalarEnvelope Ry,
    ScalarEnvelope Rz);

public sealed record ForceEnvelopeComponents(
    ScalarEnvelope Fx,
    ScalarEnvelope Fy,
    ScalarEnvelope Fz,
    ScalarEnvelope Mx,
    ScalarEnvelope My,
    ScalarEnvelope Mz);

public sealed record NodeDisplacementEnvelope(string NodeId, DisplacementEnvelopeComponents Components);

public sealed record SupportReactionEnvelope(string NodeId, ForceEnvelopeComponents Components);

public sealed record MemberSegmentEnvelope(
    string SegmentId,
    string StationI,
    string StationJ,
    double Length,
    ForceEnvelopeComponents IEnd,
    ForceEnvelopeComponents JEnd);

public sealed class MemberSectionForceEnvelope
{
    public MemberSectionForceEnvelope(string memberId, IEnumerable<MemberSegmentEnvelope> segments)
    {
        MemberId = memberId;
        Segments = Array.AsReadOnly(segments.ToArray());
    }

    public string MemberId { get; }

    public IReadOnlyList<MemberSegmentEnvelope> Segments { get; }
}

public sealed class MovingLoadEnvelope
{
    public MovingLoadEnvelope(
        string definitionId,
        IEnumerable<string> sourceCaseIds,
        IEnumerable<NodeDisplacementEnvelope> nodeDisplacements,
        IEnumerable<SupportReactionEnvelope> supportReactions,
        IEnumerable<MemberSectionForceEnvelope> memberSectionForces)
    {
        DefinitionId = definitionId;
        SourceCaseIds = Array.AsReadOnly(sourceCaseIds.ToArray());
        NodeDisplacements = Array.AsReadOnly(nodeDisplacements.ToArray());
        SupportReactions = Array.AsReadOnly(supportReactions.ToArray());
        MemberSectionForces = Array.AsReadOnly(memberSectionForces.ToArray());
    }

    public string DefinitionId { get; }

    public IReadOnlyList<string> SourceCaseIds { get; }

    public IReadOnlyList<NodeDisplacementEnvelope> NodeDisplacements { get; }

    public IReadOnlyList<SupportReactionEnvelope> SupportReactions { get; }

    public IReadOnlyList<MemberSectionForceEnvelope> MemberSectionForces { get; }
}

public sealed class ResultPresentationException : InvalidOperationException
{
    public ResultPresentationException(string message)
        : base(message)
    {
    }
}
