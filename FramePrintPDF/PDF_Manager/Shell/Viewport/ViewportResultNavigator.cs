using PDF_Manager.Core.Analysis;

namespace PDF_Manager.Shell.Viewport;

/// <summary>
/// Maintains deterministic paging over the ordered AnalysisResultSet coordinates.
/// The navigator never changes the immutable result set and preserves the current
/// coordinate when a semantically equivalent result set is published.
/// </summary>
public sealed class ViewportResultNavigator
{
    private IReadOnlyList<AnalysisResult> _results = [];
    private int _pageIndex = -1;

    public int PageIndex => _pageIndex;

    public int PageCount => _results.Count;

    public AnalysisResult? Current =>
        _pageIndex >= 0 && _pageIndex < _results.Count ? _results[_pageIndex] : null;

    public ResultCoordinate? CurrentCoordinate => Current?.Coordinate;

    public bool CanMovePrevious => _pageIndex > 0;

    public bool CanMoveNext => _pageIndex >= 0 && _pageIndex + 1 < _results.Count;

    public IReadOnlyList<ResultCoordinate> Coordinates =>
        _results.Select(result => result.Coordinate).ToArray();

    public bool SetResultSet(AnalysisResultSet? resultSet)
    {
        ResultCoordinate? previous = CurrentCoordinate;
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
