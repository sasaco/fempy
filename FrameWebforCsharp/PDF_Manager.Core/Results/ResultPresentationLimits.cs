using FrameWebforCsharp.Core.Analysis;

namespace FrameWebforCsharp.Core.Results;

public enum ResultPresentationLimitKind
{
    Pages,
    DerivedResults,
    MovingLoads,
    Operands,
    OutputEntities,
    ScalarWork,
}

public sealed class ResultPresentationLimits
{
    public const int DefaultMaxPages = 4_096;
    public const int DefaultMaxDerivedResults = 256;
    public const int DefaultMaxMovingLoads = 256;
    public const int DefaultMaxOperands = 4_096;
    public const long DefaultMaxOutputEntities = 2_000_000;
    public const long DefaultMaxScalarWork = 20_000_000;

    public const int HardMaxPages = 100_000;
    public const int HardMaxDerivedResults = 4_096;
    public const int HardMaxMovingLoads = 4_096;
    public const int HardMaxOperands = 100_000;
    public const long HardMaxOutputEntities = 4_000_000;
    public const long HardMaxScalarWork = 100_000_000;

    public static ResultPresentationLimits Default { get; } = new();

    public ResultPresentationLimits(
        int maxPages = DefaultMaxPages,
        int maxDerivedResults = DefaultMaxDerivedResults,
        int maxMovingLoads = DefaultMaxMovingLoads,
        int maxOperands = DefaultMaxOperands,
        long maxOutputEntities = DefaultMaxOutputEntities,
        long maxScalarWork = DefaultMaxScalarWork)
    {
        MaxPages = RequireRange(maxPages, HardMaxPages, nameof(maxPages));
        MaxDerivedResults = RequireRange(
            maxDerivedResults,
            HardMaxDerivedResults,
            nameof(maxDerivedResults));
        MaxMovingLoads = RequireRange(maxMovingLoads, HardMaxMovingLoads, nameof(maxMovingLoads));
        MaxOperands = RequireRange(maxOperands, HardMaxOperands, nameof(maxOperands));
        MaxOutputEntities = RequireRange(
            maxOutputEntities,
            HardMaxOutputEntities,
            nameof(maxOutputEntities));
        MaxScalarWork = RequireRange(maxScalarWork, HardMaxScalarWork, nameof(maxScalarWork));
    }

    public int MaxPages { get; }

    public int MaxDerivedResults { get; }

    public int MaxMovingLoads { get; }

    public int MaxOperands { get; }

    public long MaxOutputEntities { get; }

    public long MaxScalarWork { get; }

    private static int RequireRange(int value, int maximum, string paramName)
    {
        if (value is < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }

        return value;
    }

    private static long RequireRange(long value, long maximum, string paramName)
    {
        if (value is < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }

        return value;
    }
}

/// <summary>
/// Shared checked budget for one immutable presentation candidate. Reusing one instance across
/// pages, derived results, and moving envelopes aggregates all work and validates the result set once.
/// </summary>
public sealed class ResultPresentationBudget
{
    private readonly object _gate = new();
    private readonly HashSet<string> _movingLoadIds = new(StringComparer.Ordinal);
    private AnalysisResultSet? _validatedResultSet;

    public ResultPresentationBudget(ResultPresentationLimits? limits = null)
    {
        Limits = limits ?? ResultPresentationLimits.Default;
    }

    public ResultPresentationLimits Limits { get; }

    public long UsedPages { get; private set; }

    public long UsedDerivedResults { get; private set; }

    public long UsedMovingLoads { get; private set; }

    public long UsedOperands { get; private set; }

    public long UsedOutputEntities { get; private set; }

    public long UsedScalarWork { get; private set; }

    internal void EnsureValidated(AnalysisResultSet resultSet)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        lock (_gate)
        {
            if (_validatedResultSet is null)
            {
                AnalysisResultSetValidator.Validate(resultSet);
                _validatedResultSet = resultSet;
                return;
            }

            if (!ReferenceEquals(_validatedResultSet, resultSet))
            {
                throw new ResultPresentationException(
                    "A presentation budget cannot be reused for a different result set.");
            }
        }
    }

    internal void ConsumePages(long pages, long outputEntities, long scalarWork)
    {
        lock (_gate)
        {
            long nextPages = RequireWithin(
                ResultPresentationLimitKind.Pages,
                UsedPages,
                pages,
                Limits.MaxPages);
            long nextOutputEntities = RequireWithin(
                ResultPresentationLimitKind.OutputEntities,
                UsedOutputEntities,
                outputEntities,
                Limits.MaxOutputEntities);
            long nextScalarWork = RequireWithin(
                ResultPresentationLimitKind.ScalarWork,
                UsedScalarWork,
                scalarWork,
                Limits.MaxScalarWork);
            UsedPages = nextPages;
            UsedOutputEntities = nextOutputEntities;
            UsedScalarWork = nextScalarWork;
        }
    }

    internal void ConsumeDerived(long operands, long outputEntities, long scalarWork)
    {
        lock (_gate)
        {
            long nextDerivedResults = RequireWithin(
                ResultPresentationLimitKind.DerivedResults,
                UsedDerivedResults,
                1,
                Limits.MaxDerivedResults);
            long nextOperands = RequireWithin(
                ResultPresentationLimitKind.Operands,
                UsedOperands,
                operands,
                Limits.MaxOperands);
            long nextOutputEntities = RequireWithin(
                ResultPresentationLimitKind.OutputEntities,
                UsedOutputEntities,
                outputEntities,
                Limits.MaxOutputEntities);
            long nextScalarWork = RequireWithin(
                ResultPresentationLimitKind.ScalarWork,
                UsedScalarWork,
                scalarWork,
                Limits.MaxScalarWork);
            UsedDerivedResults = nextDerivedResults;
            UsedOperands = nextOperands;
            UsedOutputEntities = nextOutputEntities;
            UsedScalarWork = nextScalarWork;
        }
    }

    internal void ConsumeMovingLoad(
        string definitionId,
        long outputEntities,
        long scalarWork)
    {
        lock (_gate)
        {
            bool isNew = !_movingLoadIds.Contains(definitionId);
            long nextMovingLoads = isNew
                ? RequireWithin(
                    ResultPresentationLimitKind.MovingLoads,
                    UsedMovingLoads,
                    1,
                    Limits.MaxMovingLoads)
                : UsedMovingLoads;

            long nextOutputEntities = RequireWithin(
                ResultPresentationLimitKind.OutputEntities,
                UsedOutputEntities,
                outputEntities,
                Limits.MaxOutputEntities);
            long nextScalarWork = RequireWithin(
                ResultPresentationLimitKind.ScalarWork,
                UsedScalarWork,
                scalarWork,
                Limits.MaxScalarWork);
            if (isNew)
            {
                _movingLoadIds.Add(definitionId);
            }

            UsedMovingLoads = nextMovingLoads;
            UsedOutputEntities = nextOutputEntities;
            UsedScalarWork = nextScalarWork;
        }
    }

    internal void RegisterMovingLoad(string definitionId)
    {
        lock (_gate)
        {
            if (_movingLoadIds.Contains(definitionId))
            {
                return;
            }

            long nextMovingLoads = RequireWithin(
                ResultPresentationLimitKind.MovingLoads,
                UsedMovingLoads,
                1,
                Limits.MaxMovingLoads);
            _movingLoadIds.Add(definitionId);
            UsedMovingLoads = nextMovingLoads;
        }
    }

    private static long RequireWithin(
        ResultPresentationLimitKind kind,
        long current,
        long increment,
        long limit)
    {
        if (increment < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(increment));
        }

        long actual;
        try
        {
            actual = checked(current + increment);
        }
        catch (OverflowException)
        {
            throw new ResultPresentationLimitException(kind, limit, long.MaxValue);
        }

        if (actual > limit)
        {
            throw new ResultPresentationLimitException(kind, limit, actual);
        }

        return actual;
    }
}

public sealed class ResultPresentationLimitException : ResultPresentationException
{
    public const string LimitResourceKey = "ResultPresentationLimitExceeded";

    public ResultPresentationLimitException(
        ResultPresentationLimitKind limitKind,
        long limit,
        long actual)
        : base(
            ResultPresentationErrorCode.PresentationLimitExceeded,
            $"Result presentation {limitKind.ToString().ToLowerInvariant()} limit {limit} " +
            $"was exceeded by {actual}.")
    {
        LimitKind = limitKind;
        Limit = limit;
        Actual = actual;
    }

    public ResultPresentationLimitKind LimitKind { get; }

    public long Limit { get; }

    public long Actual { get; }

    public string ResourceKey => LimitResourceKey;
}
