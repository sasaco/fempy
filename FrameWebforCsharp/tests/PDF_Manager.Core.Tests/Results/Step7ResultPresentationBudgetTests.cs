using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Results;

namespace PDF_Manager.Core.Tests.Results;

public sealed class Step7ResultPresentationBudgetTests
{
    [Fact]
    public void SharedBudget_AcceptsExactCompositePagesDerivedPickupAndMovingCosts()
    {
        var resultSet = Step7ResultPresentationAcceptanceTests.CreateAngularMovingSet();
        ResultPresentationLimits limits = new(
            maxPages: 4,
            maxDerivedResults: 2,
            maxMovingLoads: 1,
            maxOperands: 2,
            maxOutputEntities: 34,
            maxScalarWork: 226);
        ResultPresentationBudget budget = new(limits);
        ResultPresentationService service = new();
        MovingLoadDefinition moving = new("MOVING", "Moving", ["1", "1.1", "1.2"]);

        Assert.Equal(2, service.BuildPages(resultSet, [moving], budget).Count);
        Assert.Equal(2, service.BuildDerivedResults(
            resultSet,
            [
                new DerivedResultDefinition(
                    "DEFINE", "Define", DerivedResultKind.Define, [new DerivedResultTerm("2", 1)]),
                new DerivedResultDefinition(
                    "PICKUP", "Pickup", DerivedResultKind.Pickup, [new DerivedResultTerm("2", 1)]),
            ],
            budget).Count);
        _ = service.BuildMovingLoadEnvelope(resultSet, moving, budget);

        Assert.Equal(4, budget.UsedPages);
        Assert.Equal(2, budget.UsedDerivedResults);
        Assert.Equal(1, budget.UsedMovingLoads);
        Assert.Equal(2, budget.UsedOperands);
        Assert.Equal(34, budget.UsedOutputEntities);
        Assert.Equal(226, budget.UsedScalarWork);
    }

    [Fact]
    public void EveryPresentationLimit_RejectsExactPlusOneWithTypedStableDetails()
    {
        var resultSet = Step7ResultPresentationAcceptanceTests.CreateAngularMovingSet();
        ResultPresentationService service = new();
        MovingLoadDefinition moving = new("MOVING", "Moving", ["1", "1.1", "1.2"]);

        AssertLimit(
            Assert.Throws<ResultPresentationLimitException>(() => service.BuildPages(
                resultSet,
                [moving],
                new ResultPresentationBudget(new ResultPresentationLimits(maxPages: 3)))),
            ResultPresentationLimitKind.Pages,
            3,
            4);

        AssertLimit(
            Assert.Throws<ResultPresentationLimitException>(() => service.BuildPages(
                resultSet,
                [
                    new MovingLoadDefinition("M1", "Moving one", ["1"]),
                    new MovingLoadDefinition("M2", "Moving two", ["2"]),
                ],
                new ResultPresentationBudget(new ResultPresentationLimits(maxMovingLoads: 1)))),
            ResultPresentationLimitKind.MovingLoads,
            1,
            2);

        DerivedResultDefinition define = new(
            "D1", "Define one", DerivedResultKind.Define, [new DerivedResultTerm("2", 1)]);
        DerivedResultDefinition second = new(
            "D2", "Define two", DerivedResultKind.Define, [new DerivedResultTerm("2", 1)]);
        AssertLimit(
            Assert.Throws<ResultPresentationLimitException>(() => service.BuildDerivedResults(
                resultSet,
                [define, second],
                new ResultPresentationBudget(new ResultPresentationLimits(maxDerivedResults: 1)))),
            ResultPresentationLimitKind.DerivedResults,
            1,
            2);

        DerivedResultDefinition twoOperands = new(
            "D12",
            "Define two operands",
            DerivedResultKind.Define,
            [new DerivedResultTerm("1", 1), new DerivedResultTerm("2", 1)]);
        AssertLimit(
            Assert.Throws<ResultPresentationLimitException>(() => service.BuildDerivedResults(
                resultSet,
                [twoOperands],
                new ResultPresentationBudget(new ResultPresentationLimits(maxOperands: 1)))),
            ResultPresentationLimitKind.Operands,
            1,
            2);

        Assert.Single(service.BuildDerivedResults(
            resultSet,
            [define],
            new ResultPresentationBudget(new ResultPresentationLimits(
                maxOutputEntities: 5,
                maxScalarWork: 30))));
        AssertLimit(
            Assert.Throws<ResultPresentationLimitException>(() => service.BuildDerivedResults(
                resultSet,
                [define],
                new ResultPresentationBudget(new ResultPresentationLimits(maxOutputEntities: 4)))),
            ResultPresentationLimitKind.OutputEntities,
            4,
            5);
        AssertLimit(
            Assert.Throws<ResultPresentationLimitException>(() => service.BuildDerivedResults(
                resultSet,
                [define],
                new ResultPresentationBudget(new ResultPresentationLimits(maxScalarWork: 29)))),
            ResultPresentationLimitKind.ScalarWork,
            29,
            30);
    }

    private static void AssertLimit(
        ResultPresentationLimitException exception,
        ResultPresentationLimitKind kind,
        long limit,
        long actual)
    {
        Assert.Equal(kind, exception.LimitKind);
        Assert.Equal(limit, exception.Limit);
        Assert.Equal(actual, exception.Actual);
        Assert.Equal(ResultPresentationErrorCode.PresentationLimitExceeded, exception.Code);
        Assert.Equal("ResultPresentationLimitExceeded", exception.ResourceKey);
    }
}
