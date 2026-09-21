using FrameWebforCsharp.Core.Analysis;

namespace FrameWebforCsharp.Shell.Viewport;

/// <summary>
/// Maintains deterministic paging over the ordered AnalysisResultSet coordinates.
/// The navigator never changes the immutable result set and preserves the current
/// coordinate when a semantically equivalent result set is published.
/// </summary>
public sealed class ViewportResultNavigator
{
    private IReadOnlyList<AnalysisCase> _cases = [];
    private IReadOnlyList<AnalysisResult> _results = [];
    private int _pageIndex = -1;

    public int PageIndex => _pageIndex;

    public int PageCount => _results.Count;

    public AnalysisResult? Current =>
        _pageIndex >= 0 && _pageIndex < _results.Count ? _results[_pageIndex] : null;

    public ResultCoordinate? CurrentCoordinate => Current?.Coordinate;

    public bool CanMovePrevious => _pageIndex > 0;

    public bool CanMoveNext => _pageIndex >= 0 && _pageIndex + 1 < _results.Count;

    public IReadOnlyList<AnalysisCase> Cases => _cases;

    public int CurrentCaseIndex => Current is null
        ? -1
        : FindCaseIndex(Current.CaseId);

    public int CurrentStateIndex
    {
        get
        {
            if (Current is not AnalysisResult current)
            {
                return -1;
            }

            int stateIndex = 0;
            for (int index = 0; index < _pageIndex; index++)
            {
                if (string.Equals(_results[index].CaseId, current.CaseId, StringComparison.Ordinal))
                {
                    stateIndex++;
                }
            }

            return stateIndex;
        }
    }

    public IReadOnlyList<ResultCoordinate> Coordinates =>
        _results.Select(result => result.Coordinate).ToArray();

    public bool SetResultSet(AnalysisResultSet? resultSet)
    {
        ResultCoordinate? previous = CurrentCoordinate;
        _cases = resultSet?.Cases ?? [];
        _results = resultSet?.Results ?? [];
        int nextIndex = previous is ResultCoordinate coordinate
            ? FindIndex(coordinate)
            : -1;
        if (nextIndex < 0)
        {
            nextIndex = _results.Count == 0 ? -1 : 0;
        }

        return SetPageIndexCore(nextIndex);
    }

    public bool Select(ResultCoordinate coordinate)
    {
        int index = FindIndex(coordinate);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coordinate),
                coordinate,
                "The result coordinate is not part of the current result set.");
        }

        return SetPageIndexCore(index);
    }

    public bool SelectCase(string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        int caseIndex = FindCaseIndex(caseId);
        if (caseIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(caseId),
                caseId,
                "The case is not part of the current result set.");
        }

        for (int index = 0; index < _results.Count; index++)
        {
            if (string.Equals(_results[index].CaseId, caseId, StringComparison.Ordinal))
            {
                return SetPageIndexCore(index);
            }
        }

        throw new InvalidOperationException($"The result case '{caseId}' has no result states.");
    }

    public bool SelectState(int stateIndex)
    {
        if (Current is not AnalysisResult current)
        {
            throw new InvalidOperationException("A result case must be selected before selecting a state.");
        }

        if (stateIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stateIndex));
        }

        int currentStateIndex = 0;
        foreach ((AnalysisResult result, int resultIndex) in _results.Select((value, index) => (value, index)))
        {
            if (!string.Equals(result.CaseId, current.CaseId, StringComparison.Ordinal))
            {
                continue;
            }

            if (currentStateIndex == stateIndex)
            {
                return SetPageIndexCore(resultIndex);
            }

            currentStateIndex++;
        }

        throw new ArgumentOutOfRangeException(
            nameof(stateIndex),
            stateIndex,
            $"The selected case has only {currentStateIndex} result states.");
    }

    public IReadOnlyList<AnalysisResult> GetStates(string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        if (FindCaseIndex(caseId) < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(caseId),
                caseId,
                "The case is not part of the current result set.");
        }

        return _results
            .Where(result => string.Equals(result.CaseId, caseId, StringComparison.Ordinal))
            .ToArray();
    }

    public bool MovePrevious() =>
        _results.Count > 0 && SetPageIndexCore(Math.Max(0, _pageIndex - 1));

    public bool MoveNext() =>
        _results.Count > 0 && SetPageIndexCore(Math.Min(_results.Count - 1, _pageIndex + 1));

    private int FindIndex(ResultCoordinate coordinate)
    {
        for (int index = 0; index < _results.Count; index++)
        {
            if (_results[index].Coordinate == coordinate)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindCaseIndex(string caseId)
    {
        for (int index = 0; index < _cases.Count; index++)
        {
            if (string.Equals(_cases[index].CaseId, caseId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private bool SetPageIndexCore(int pageIndex)
    {
        if (pageIndex < -1 || pageIndex >= _results.Count || (_results.Count > 0 && pageIndex < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        if (_pageIndex == pageIndex)
        {
            return false;
        }

        _pageIndex = pageIndex;
        return true;
    }
}
