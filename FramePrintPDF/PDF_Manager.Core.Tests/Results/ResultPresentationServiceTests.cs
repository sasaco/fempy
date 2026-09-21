using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Results;

namespace PDF_Manager.Core.Tests.Results;

public sealed class ResultPresentationServiceTests
{
    [Fact]
    public void DerivedResults_UseLinearDefineCombineAndAbsolutePickupWithoutMutatingSources()
    {
        AnalysisResultSet resultSet = CreateStaticSet(("A", 1, 10, 2), ("B", -3, 4, -5));
        ResultPresentationService service = new();
        DerivedResultDefinition[] definitions =
        [
            new("DEF", "Define", DerivedResultKind.Define,
                [new DerivedResultTerm("A", 2), new DerivedResultTerm("B", 0.5)]),
            new("COMB", "Combine", DerivedResultKind.Combine,
                [new DerivedResultTerm("DEF", 2)]),
            new("PICK", "Pickup", DerivedResultKind.Pickup,
                [new DerivedResultTerm("A", 1), new DerivedResultTerm("B", 1)]),
        ];

        IReadOnlyList<PresentedStaticResult> output = service.BuildDerivedResults(resultSet, definitions);

        Assert.Equal(0.5, output[0].NodeDisplacements[1].Components.Dx, 12);
        Assert.Equal(1, output[1].NodeDisplacements[1].Components.Dx, 12);
        Assert.Equal(-3, output[2].NodeDisplacements[1].Components.Dx, 12);
        Assert.Equal(-5, output[2].MemberSectionForces[0].Segments[0].IEnd.Fx, 12);
        Assert.Equal(["A", "B"], output[0].SourceIds);
        Assert.Equal(1, Assert.IsType<StaticAnalysisResult>(resultSet.Results[0])
            .NodeDisplacements[1].Components.Dx);
    }

    [Fact]
    public void DerivedResults_RejectNonStaticMissingAndOverflowingOperands()
    {
        ResultPresentationService service = new();
        AnalysisResultSet nonlinear = Analysis.AnalysisContractFixtureTests.ReadPositive("nonlinear-steps.json");
        DerivedResultDefinition nonStatic = new(
            "X", "X", DerivedResultKind.Define, [new DerivedResultTerm("NL", 1)]);

        NonStaticDerivedOperandException nonStaticError = Assert.Throws<NonStaticDerivedOperandException>(
            () => service.BuildDerivedResults(nonlinear, [nonStatic]));
        Assert.Equal("X", nonStaticError.DerivedResultId);
        Assert.Equal("NL", nonStaticError.OperandId);
        Assert.Equal(AnalysisType.MaterialNonlinear, nonStaticError.OperandAnalysisType);
        Assert.Equal("ResultDerivedStaticOnly", nonStaticError.ResourceKey);
        Assert.Contains("NL", nonStaticError.Message, StringComparison.Ordinal);

        AnalysisResultSet staticSet = CreateStaticSet(("A", 2, 1, 1));
        DerivedResultDefinition missing = new(
            "X", "X", DerivedResultKind.Define, [new DerivedResultTerm("missing", 1)]);
        Assert.Throws<ResultPresentationException>(() => service.BuildDerivedResults(staticSet, [missing]));

        DerivedResultDefinition overflow = new(
            "X", "X", DerivedResultKind.Define, [new DerivedResultTerm("A", double.MaxValue)]);
        Assert.Throws<ResultPresentationException>(() => service.BuildDerivedResults(staticSet, [overflow]));
    }

    [Fact]
    public void MovingLoadPages_GroupSourcesWithoutShiftingFollowingCase()
    {
        AnalysisResultSet resultSet = CreateStaticSet(
            ("A", 1, 10, 2),
            ("B", -3, 4, -5),
            ("C", 2, 7, 1));
        MovingLoadDefinition moving = new("MOVE", "Moving", ["A", "B"]);
        ResultPresentationService service = new();

        IReadOnlyList<ResultPresentationPage> pages = service.BuildPages(resultSet, [moving]);

        Assert.Equal(["MOVE", "C:Static:0"], pages.Select(page => page.PageId));
        Assert.True(pages[0].IsMovingLoad);
        Assert.Equal(["A", "B"], pages[0].SourceResults.Select(result => result.CaseId));
        Assert.False(pages[1].IsMovingLoad);
        Assert.Equal("C", pages[1].PrimaryResult.CaseId);
    }

    [Fact]
    public void MovingLoadEnvelope_TracksSignedExtremaAndSourceCases()
    {
        AnalysisResultSet resultSet = CreateStaticSet(("A", 1, 10, 2), ("B", -3, 4, -5));
        MovingLoadDefinition moving = new("MOVE", "Moving", ["A", "B"]);
        ResultPresentationService service = new();

        MovingLoadEnvelope envelope = service.BuildMovingLoadEnvelope(resultSet, moving);

        ScalarEnvelope dx = envelope.NodeDisplacements[1].Components.Dx;
        Assert.Equal(1, dx.Maximum.Value);
        Assert.Equal("A", dx.Maximum.CaseId);
        Assert.Equal(-3, dx.Minimum.Value);
        Assert.Equal("B", dx.Minimum.CaseId);
        Assert.Equal(-5, envelope.MemberSectionForces[0].Segments[0].IEnd.Fx.Minimum.Value);
        Assert.Equal("B", envelope.MemberSectionForces[0].Segments[0].IEnd.Fx.Minimum.CaseId);
    }

    [Fact]
    public void MovingLoadEnvelope_RejectsDuplicateAndOutOfOrderSources()
    {
        AnalysisResultSet resultSet = CreateStaticSet(("A", 1, 10, 2), ("B", -3, 4, -5));
        ResultPresentationService service = new();

        Assert.Throws<ResultPresentationException>(() => service.BuildMovingLoadEnvelope(
            resultSet,
            new MovingLoadDefinition("DUP", "Duplicate", ["A", "A"])));
        Assert.Throws<ResultPresentationException>(() => service.BuildMovingLoadEnvelope(
            resultSet,
            new MovingLoadDefinition("ORDER", "Out of order", ["B", "A"])));
    }

    private static AnalysisResultSet CreateStaticSet(params (string Id, double Dx, double Reaction, double Force)[] values)
    {
        CoordinateFrame frame = new(
            new Vector3Value(0, 0, 0),
            new Vector3Value(1, 0, 0),
            new Vector3Value(0, 1, 0),
            new Vector3Value(0, 0, 1));
        AnalysisTopology topology = new(
            [
                new TopologyNode("N1", new Vector3Value(0, 0, 0), "N1", false),
                new TopologyNode("N2", new Vector3Value(1, 0, 0), "N2", false),
            ],
            [new TopologyMember("M1", "N1", "N2", frame,
                [new MemberStation("S0", 0), new MemberStation("S1", 1)])],
            [],
            []);
        AnalysisCase[] cases = values.Select(value =>
            new AnalysisCase(value.Id, value.Id, value.Id, AnalysisType.Static, ["N1"]))
            .ToArray();
        AnalysisResult[] results = values.Select(value => (AnalysisResult)new StaticAnalysisResult(
            value.Id,
            [
                new NodeDisplacement("N1", new DisplacementComponents(0, 0, 0, 0, 0, 0)),
                new NodeDisplacement("N2", new DisplacementComponents(value.Dx, 0, 0, 0, 0, 0)),
            ],
            [new SupportReaction("N1", new ForceComponents(value.Reaction, 0, 0, 0, 0, 0))],
            [new MemberSectionForces("M1", [new MemberSegmentResult(
                "S0-S1", "S0", "S1", 1,
                new ForceComponents(value.Force, 0, 0, 0, 0, 0),
                new ForceComponents(value.Force, 0, 0, 0, 0, 0))])],
            [],
            [],
            new WarningDiagnostics([])))
            .ToArray();
        AnalysisResultSet resultSet = new(
            AnalysisResultSet.ContractKind,
            AnalysisResultSet.ContractVersion,
            new AnalysisUnits("consistent_user_defined", "m", "kN", "t", "s"),
            new CoordinateSystem("global_cartesian", "right", ["x", "y", "z"]),
            cases,
            topology,
            results);
        AnalysisResultSetValidator.Validate(resultSet);
        return resultSet;
    }
}
