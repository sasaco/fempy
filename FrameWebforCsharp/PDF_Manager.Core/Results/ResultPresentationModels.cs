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
        IEnumerable<MemberSectionForces> memberSectionForces)
        : this(
            id,
            name,
            kind,
            sourceIds,
            nodeDisplacements,
            supportReactions,
            memberSectionForces,
            [],
            [],
            null)
    {
    }

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
        : this(
            id,
            name,
            kind,
            sourceIds,
            nodeDisplacements,
            supportReactions,
            memberSectionForces,
            shellResults,
            solidResults,
            null)
    {
    }

    public PresentedStaticResult(
        string id,
        string name,
        DerivedResultKind kind,
        IEnumerable<string> sourceIds,
        IEnumerable<NodeDisplacement> nodeDisplacements,
        IEnumerable<SupportReaction> supportReactions,
        IEnumerable<MemberSectionForces> memberSectionForces,
        IEnumerable<ShellResult> shellResults,
        IEnumerable<SolidResult> solidResults,
        PickupEngineeringEnvelope? pickupEnvelope)
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
        PickupEnvelope = pickupEnvelope;
        AnalysisResult = new StaticAnalysisResult(
            id,
            NodeDisplacements,
            SupportReactions,
            MemberSectionForces,
            ShellResults,
            SolidResults,
            new WarningDiagnostics([]));
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

    /// <summary>
    /// Gets the signed engineering envelope for a PICKUP definition. Generic displayed result
    /// values remain the signed greatest-absolute values and are intentionally independent of
    /// this correlated maximum/minimum projection.
    /// </summary>
    public PickupEngineeringEnvelope? PickupEnvelope { get; }

    /// <summary>
    /// Gets an immutable static-result projection suitable for the same table and viewport paths as
    /// canonical static results. The projection is derived only and is never inserted into the
    /// canonical <see cref="AnalysisResultSet"/>.
    /// </summary>
    public StaticAnalysisResult AnalysisResult { get; }

    public StaticAnalysisResult ToAnalysisResult() => AnalysisResult;
}

public sealed record ResultPresentationSourcePage(
    int CaseIndex,
    AnalysisCase ResultCase,
    AnalysisResult Result,
    bool IsParent);

public sealed class ResultPresentationPage
{
    public ResultPresentationPage(
        string pageId,
        AnalysisCase resultCase,
        AnalysisResult primaryResult,
        IEnumerable<AnalysisResult> sourceResults,
        bool isMovingLoad)
        : this(
            pageId,
            resultCase,
            primaryResult,
            sourceResults,
            isMovingLoad,
            [new ResultPresentationSourcePage(0, resultCase, primaryResult, true)])
    {
    }

    public ResultPresentationPage(
        string pageId,
        AnalysisCase resultCase,
        AnalysisResult primaryResult,
        IEnumerable<AnalysisResult> sourceResults,
        bool isMovingLoad,
        IEnumerable<ResultPresentationSourcePage> sourcePages)
    {
        PageId = pageId;
        ResultCase = resultCase;
        PrimaryResult = primaryResult;
        SourceResults = Array.AsReadOnly(sourceResults.ToArray());
        IsMovingLoad = isMovingLoad;
        SourcePages = Array.AsReadOnly(sourcePages.ToArray());
        ChildPages = Array.AsReadOnly(SourcePages.Where(page => !page.IsParent).ToArray());
    }

    public string PageId { get; }

    public AnalysisCase ResultCase { get; }

    public AnalysisResult PrimaryResult { get; }

    public IReadOnlyList<AnalysisResult> SourceResults { get; }

    public bool IsMovingLoad { get; }

    /// <summary>
    /// Gets the parent and child source pages in canonical result-set case order.
    /// Ordinary result pages contain only their primary source.
    /// </summary>
    public IReadOnlyList<ResultPresentationSourcePage> SourcePages { get; }

    /// <summary>
    /// Gets moving-load child pages in canonical result-set case order.
    /// </summary>
    public IReadOnlyList<ResultPresentationSourcePage> ChildPages { get; }
}

public sealed class ResultTableSet
{
    public ResultTableSet(
        string id,
        string name,
        ResultCoordinate? coordinate,
        DerivedResultKind? derivedKind,
        bool isModal,
        IEnumerable<NodeDisplacement> nodeDisplacements,
        IEnumerable<SupportReaction> supportReactions,
        IEnumerable<MemberSectionForces> memberSectionForces,
        IEnumerable<ShellResult> shellResults,
        IEnumerable<SolidResult> solidResults)
    {
        Id = id;
        Name = name;
        Coordinate = coordinate;
        DerivedKind = derivedKind;
        IsModal = isModal;
        NodeDisplacements = Array.AsReadOnly(nodeDisplacements.ToArray());
        SupportReactions = Array.AsReadOnly(supportReactions.ToArray());
        MemberSectionForces = Array.AsReadOnly(memberSectionForces.ToArray());
        ShellResults = Array.AsReadOnly(shellResults.ToArray());
        SolidResults = Array.AsReadOnly(solidResults.ToArray());
    }

    public string Id { get; }

    public string Name { get; }

    public ResultCoordinate? Coordinate { get; }

    public DerivedResultKind? DerivedKind { get; }

    public bool IsDerived => DerivedKind.HasValue;

    public bool IsModal { get; }

    public IReadOnlyList<NodeDisplacement> NodeDisplacements { get; }

    public IReadOnlyList<SupportReaction> SupportReactions { get; }

    public IReadOnlyList<MemberSectionForces> MemberSectionForces { get; }

    public IReadOnlyList<ShellResult> ShellResults { get; }

    public IReadOnlyList<SolidResult> SolidResults { get; }
}

public sealed record EnvelopeExtreme(double Value, string CaseId);

public sealed record ScalarEnvelope
{
    public ScalarEnvelope(EnvelopeExtreme maximum, EnvelopeExtreme minimum)
        : this(maximum, minimum, SelectAbsolute(maximum, minimum))
    {
    }

    public ScalarEnvelope(
        EnvelopeExtreme maximum,
        EnvelopeExtreme minimum,
        EnvelopeExtreme absoluteMaximum)
    {
        Maximum = maximum;
        Minimum = minimum;
        AbsoluteMaximum = absoluteMaximum;
    }

    public EnvelopeExtreme Maximum { get; }

    public EnvelopeExtreme Minimum { get; }

    public EnvelopeExtreme AbsoluteMaximum { get; }

    private static EnvelopeExtreme SelectAbsolute(EnvelopeExtreme maximum, EnvelopeExtreme minimum)
        => Math.Abs(minimum.Value) > Math.Abs(maximum.Value) ? minimum : maximum;
}

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

public sealed record ForceSourceCaseIds(
    string Fx,
    string Fy,
    string Fz,
    string Mx,
    string My,
    string Mz);

/// <summary>
/// Component-wise absolute support reaction selected from moving-load children when children
/// exist, or from the parent for a parent-only definition.
/// </summary>
public sealed record SupportReactionAbsoluteMaximum(
    string NodeId,
    ForceComponents Components,
    ForceSourceCaseIds SourceCaseIds);

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

public enum MemberForceEnd
{
    I,
    J,
}

public enum PickupFocusComponent
{
    Fx,
    Fy,
    Fz,
    Mx,
    My,
    Mz,
}

public sealed record PickupForceWinner(
    string SourceId,
    ForceComponents Components);

public sealed record PickupForceComponentEnvelope(
    PickupFocusComponent FocusComponent,
    PickupForceWinner Maximum,
    PickupForceWinner Minimum);

public sealed class PickupMemberEndEnvelope
{
    public PickupMemberEndEnvelope(
        string memberId,
        string segmentId,
        string stationId,
        MemberForceEnd end,
        double distance,
        double segmentLength,
        IEnumerable<PickupForceComponentEnvelope> components)
    {
        PickupForceComponentEnvelope[] componentArray = components.ToArray();
        if (!componentArray.Select(value => value.FocusComponent)
            .SequenceEqual(Enum.GetValues<PickupFocusComponent>()))
        {
            throw new ArgumentException(
                "PICKUP components must contain Fx, Fy, Fz, Mx, My, and Mz exactly once in contract order.",
                nameof(components));
        }

        if (!double.IsFinite(distance) || !double.IsFinite(segmentLength))
        {
            throw new ArgumentOutOfRangeException(
                nameof(distance),
                "PICKUP distances must be finite.");
        }

        MemberId = memberId;
        SegmentId = segmentId;
        StationId = stationId;
        End = end;
        Distance = distance;
        SegmentLength = segmentLength;
        Components = Array.AsReadOnly(componentArray);
    }

    public string MemberId { get; }

    public string SegmentId { get; }

    public string StationId { get; }

    public MemberForceEnd End { get; }

    public double Distance { get; }

    public double SegmentLength { get; }

    /// <summary>Gets Fx, Fy, Fz, Mx, My, and Mz envelopes in that contract order.</summary>
    public IReadOnlyList<PickupForceComponentEnvelope> Components { get; }
}

public sealed class PickupEngineeringEnvelope
{
    public PickupEngineeringEnvelope(
        string pickupId,
        IEnumerable<PickupMemberEndEnvelope> memberEnds)
    {
        PickupId = pickupId;
        MemberEnds = Array.AsReadOnly(memberEnds.ToArray());
    }

    public string PickupId { get; }

    /// <summary>Gets rows in member, segment, and I-then-J contract order.</summary>
    public IReadOnlyList<PickupMemberEndEnvelope> MemberEnds { get; }
}

public sealed record MemberForceExtreme(
    double Value,
    string CaseId,
    string MemberId,
    string SegmentId,
    MemberForceEnd End);

public sealed record MemberForceScalarExtrema
{
    private static readonly MemberForceExtreme Unavailable = new(0, string.Empty, string.Empty, string.Empty, MemberForceEnd.I);

    public static MemberForceScalarExtrema Empty { get; } = new(Unavailable, Unavailable, Unavailable, false);

    public MemberForceScalarExtrema(
        MemberForceExtreme maximum,
        MemberForceExtreme minimum,
        MemberForceExtreme absoluteMaximum)
        : this(maximum, minimum, absoluteMaximum, true)
    {
    }

    private MemberForceScalarExtrema(
        MemberForceExtreme maximum,
        MemberForceExtreme minimum,
        MemberForceExtreme absoluteMaximum,
        bool hasValue)
    {
        Maximum = maximum;
        Minimum = minimum;
        AbsoluteMaximum = absoluteMaximum;
        HasValue = hasValue;
    }

    public MemberForceExtreme Maximum { get; }

    public MemberForceExtreme Minimum { get; }

    public MemberForceExtreme AbsoluteMaximum { get; }

    public bool HasValue { get; }
}

public sealed record MemberForceExtremaComponents(
    MemberForceScalarExtrema Fx,
    MemberForceScalarExtrema Fy,
    MemberForceScalarExtrema Fz,
    MemberForceScalarExtrema Mx,
    MemberForceScalarExtrema My,
    MemberForceScalarExtrema Mz)
{
    public static MemberForceExtremaComponents Empty { get; } = new(
        MemberForceScalarExtrema.Empty,
        MemberForceScalarExtrema.Empty,
        MemberForceScalarExtrema.Empty,
        MemberForceScalarExtrema.Empty,
        MemberForceScalarExtrema.Empty,
        MemberForceScalarExtrema.Empty);

    public bool HasValues => Fx.HasValue || Fy.HasValue || Fz.HasValue || Mx.HasValue || My.HasValue || Mz.HasValue;
}

public sealed class MovingLoadEnvelope
{
    public MovingLoadEnvelope(
        string definitionId,
        IEnumerable<string> sourceCaseIds,
        IEnumerable<NodeDisplacementEnvelope> nodeDisplacements,
        IEnumerable<SupportReactionEnvelope> supportReactions,
        IEnumerable<MemberSectionForceEnvelope> memberSectionForces)
        : this(
            definitionId,
            sourceCaseIds,
            nodeDisplacements,
            supportReactions,
            memberSectionForces,
            MemberForceExtremaComponents.Empty)
    {
    }

    public MovingLoadEnvelope(
        string definitionId,
        IEnumerable<string> sourceCaseIds,
        IEnumerable<NodeDisplacementEnvelope> nodeDisplacements,
        IEnumerable<SupportReactionEnvelope> supportReactions,
        IEnumerable<MemberSectionForceEnvelope> memberSectionForces,
        MemberForceExtremaComponents memberForceExtrema)
    {
        SupportReactionEnvelope[] reactions = supportReactions.ToArray();
        DefinitionId = definitionId;
        SourceCaseIds = Array.AsReadOnly(sourceCaseIds.ToArray());
        NodeDisplacements = Array.AsReadOnly(nodeDisplacements.ToArray());
        SupportReactions = Array.AsReadOnly(reactions);
        MemberSectionForces = Array.AsReadOnly(memberSectionForces.ToArray());
        MemberForceExtrema = memberForceExtrema;
        AbsoluteSupportReactions = Array.AsReadOnly(reactions.Select(ToAbsoluteReaction).ToArray());
        AbsoluteReactionProjection = Array.AsReadOnly(AbsoluteSupportReactions
            .Select(value => new SupportReaction(value.NodeId, value.Components))
            .ToArray());
    }

    public MovingLoadEnvelope(
        string definitionId,
        IEnumerable<string> sourceCaseIds,
        IEnumerable<NodeDisplacementEnvelope> nodeDisplacements,
        IEnumerable<SupportReactionEnvelope> supportReactions,
        IEnumerable<MemberSectionForceEnvelope> memberSectionForces,
        MemberForceExtremaComponents memberForceExtrema,
        IEnumerable<SupportReactionAbsoluteMaximum> absoluteSupportReactions)
    {
        DefinitionId = definitionId;
        SourceCaseIds = Array.AsReadOnly(sourceCaseIds.ToArray());
        NodeDisplacements = Array.AsReadOnly(nodeDisplacements.ToArray());
        SupportReactions = Array.AsReadOnly(supportReactions.ToArray());
        MemberSectionForces = Array.AsReadOnly(memberSectionForces.ToArray());
        MemberForceExtrema = memberForceExtrema;
        AbsoluteSupportReactions = Array.AsReadOnly(absoluteSupportReactions.ToArray());
        AbsoluteReactionProjection = Array.AsReadOnly(AbsoluteSupportReactions
            .Select(value => new SupportReaction(value.NodeId, value.Components))
            .ToArray());
    }

    public string DefinitionId { get; }

    public IReadOnlyList<string> SourceCaseIds { get; }

    public IReadOnlyList<NodeDisplacementEnvelope> NodeDisplacements { get; }

    public IReadOnlyList<SupportReactionEnvelope> SupportReactions { get; }

    public IReadOnlyList<MemberSectionForceEnvelope> MemberSectionForces { get; }

    public MemberForceExtremaComponents MemberForceExtrema { get; }

    public IReadOnlyList<SupportReactionAbsoluteMaximum> AbsoluteSupportReactions { get; }

    /// <summary>
    /// Gets a scene-ready immutable reaction list containing the child-only component-wise
    /// absolute projection (or the parent fallback when the definition has no children).
    /// </summary>
    public IReadOnlyList<SupportReaction> AbsoluteReactionProjection { get; }

    private static SupportReactionAbsoluteMaximum ToAbsoluteReaction(
        SupportReactionEnvelope reaction)
        => new(
            reaction.NodeId,
            new ForceComponents(
                reaction.Components.Fx.AbsoluteMaximum.Value,
                reaction.Components.Fy.AbsoluteMaximum.Value,
                reaction.Components.Fz.AbsoluteMaximum.Value,
                reaction.Components.Mx.AbsoluteMaximum.Value,
                reaction.Components.My.AbsoluteMaximum.Value,
                reaction.Components.Mz.AbsoluteMaximum.Value),
            new ForceSourceCaseIds(
                reaction.Components.Fx.AbsoluteMaximum.CaseId,
                reaction.Components.Fy.AbsoluteMaximum.CaseId,
                reaction.Components.Fz.AbsoluteMaximum.CaseId,
                reaction.Components.Mx.AbsoluteMaximum.CaseId,
                reaction.Components.My.AbsoluteMaximum.CaseId,
                reaction.Components.Mz.AbsoluteMaximum.CaseId));
}

public enum ResultPresentationErrorCode
{
    InvalidDefinition,
    StaticOperandsOnly,
    IncompatibleSources,
    ArithmeticOverflow,
    ExportLimitExceeded,
    PresentationLimitExceeded,
}

public class ResultPresentationException : InvalidOperationException
{
    public ResultPresentationException(string message)
        : this(ResultPresentationErrorCode.InvalidDefinition, message)
    {
    }

    public ResultPresentationException(ResultPresentationErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public ResultPresentationErrorCode Code { get; }
}

public sealed class NonStaticDerivedOperandException : ResultPresentationException
{
    public const string StaticOnlyResourceKey = "ResultDerivedStaticOnly";

    public NonStaticDerivedOperandException(
        string derivedResultId,
        string operandId,
        AnalysisType operandAnalysisType)
        : base(
            ResultPresentationErrorCode.StaticOperandsOnly,
            $"Derived result '{derivedResultId}' references {operandAnalysisType} operand '{operandId}'.")
    {
        DerivedResultId = derivedResultId;
        OperandId = operandId;
        OperandAnalysisType = operandAnalysisType;
    }

    public string DerivedResultId { get; }

    public string OperandId { get; }

    public AnalysisType OperandAnalysisType { get; }

    public string ResourceKey => StaticOnlyResourceKey;
}
