using System.Collections.Immutable;
using System.Globalization;

namespace FrameWebforCS.components.result;

internal readonly record struct FsecVector(double Fx, double Fy, double Fz, double Mx, double My, double Mz)
{
    public double At(int index) => index switch
    {
        0 => Fx, 1 => Fy, 2 => Fz, 3 => Mx, 4 => My, 5 => Mz,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static FsecVector operator *(double scale, FsecVector value) =>
        new(scale * value.Fx, scale * value.Fy, scale * value.Fz,
            scale * value.Mx, scale * value.My, scale * value.Mz);

    public static FsecVector operator +(FsecVector a, FsecVector b) =>
        new(a.Fx + b.Fx, a.Fy + b.Fy, a.Fz + b.Fz,
            a.Mx + b.Mx, a.My + b.My, a.Mz + b.Mz);

    public bool IsFinite => double.IsFinite(Fx) && double.IsFinite(Fy) &&
        double.IsFinite(Fz) && double.IsFinite(Mx) && double.IsFinite(My) && double.IsFinite(Mz);
}

internal sealed record FsecRowSnapshot(string MemberId, string MemberDisplay,
    string NodeId, double Location, FsecVector Value);
internal sealed record FsecCaseSnapshot(string Id, ImmutableArray<FsecRowSnapshot> Rows);
internal sealed record ResultCombineFsecSnapshot(long Revision, int Dimension,
    ImmutableArray<FsecCaseSnapshot> Forces, ImmutableArray<DefineDisgSnapshot> Defines,
    ImmutableArray<CombineDisgSnapshot> Combines, ImmutableArray<int> StaticCaseIds);
internal sealed record CombineFsecRowResult(string MemberId, string MemberDisplay,
    string NodeId, double Location, double Fx, double Fy, double Fz,
    double Mx, double My, double Mz, string Case);
internal sealed record CombineFsecCaseResult(string Id, string? Name,
    IReadOnlyDictionary<string, IReadOnlyList<CombineFsecRowResult>> Rows);
internal sealed record ResultCombineFsecOutput(IReadOnlyList<CombineFsecCaseResult> Cases);

internal static class ResultCombineFsecAggregator
{
    internal const int MaxDefinitions = 10_000;
    internal const int MaxCombinations = 1_000;
    internal const int MaxRows = 100_000;
    internal const long MaxScalarOperations = 50_000_000;
    // At most 1.2 million materialized rows across all modes and combinations.
    internal const long MaxOutputCells = 12_000_000;

    internal static readonly string[] Modes3D =
    [
        "fx_max", "fx_min", "fy_max", "fy_min", "fz_max", "fz_min",
        "mx_max", "mx_min", "my_max", "my_min", "mz_max", "mz_min"
    ];
    internal static readonly string[] Modes2D =
        ["fx_max", "fx_min", "fy_max", "fy_min", "mz_max", "mz_min"];

    public static ResultCombineFsecOutput Calculate(ResultCombineFsecSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Dimension is not (2 or 3))
            throw new ArgumentException("Dimension must be 2 or 3.", nameof(snapshot));
        if (snapshot.Defines.Length > MaxDefinitions || snapshot.Combines.Length > MaxCombinations)
            throw new InvalidOperationException("Combination count exceeds the supported limit.");

        long totalRows = snapshot.Forces.Sum(item => (long)item.Rows.Length);
        ValidateBudget(totalRows, 0, 0);
        string[] modes = snapshot.Dimension == 3 ? Modes3D : Modes2D;
        var baseCases = snapshot.Forces.ToDictionary(item => item.Id,
            item => ToStationMap(item.Rows));
        Dictionary<string, FsecRowSnapshot>? zeroTemplate = snapshot.Forces.IsEmpty
            ? null : baseCases[snapshot.Forces[0].Id];
        ImmutableArray<DefineDisgSnapshot> definitions = snapshot.Defines;
        if (definitions.IsEmpty)
            definitions = snapshot.StaticCaseIds
                .Where(id => id > 0 && baseCases.ContainsKey(id.ToString(CultureInfo.InvariantCulture)))
                .Select(id => new DefineDisgSnapshot(id.ToString(CultureInfo.InvariantCulture),
                    ImmutableArray.Create(id))).ToImmutableArray();

        long operations = 0;
        long outputCells = 0;
        foreach (CombineDisgSnapshot combination in snapshot.Combines)
        {
            foreach (CombineDisgTerm term in combination.Terms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (term.Coefficient == 0) continue;
                DefineDisgSnapshot? definition = definitions.FirstOrDefault(item =>
                    item.Id == term.DefineId.ToString(CultureInfo.InvariantCulture));
                if (definition == null) continue;
                foreach (int signedId in definition.SignedCaseIds)
                {
                    if (signedId == int.MinValue)
                        throw new InvalidOperationException("DEFINE case ID is outside the supported range.");
                    Dictionary<string, FsecRowSnapshot>? rows = signedId == 0 ? zeroTemplate :
                        baseCases.GetValueOrDefault(Math.Abs(signedId).ToString(CultureInfo.InvariantCulture));
                    operations = checked(operations + (long)(rows?.Count ?? 0) * modes.Length * 6);
                    ValidateBudget(totalRows, operations, outputCells);
                }
            }
            int largestCase = baseCases.Count == 0 ? 0 : baseCases.Values.Max(rows => rows.Count);
            outputCells = checked(outputCells + (long)largestCase * modes.Length * 10);
            ValidateBudget(totalRows, operations, outputCells);
        }

        var usedDefinitions = snapshot.Combines.SelectMany(combination => combination.Terms)
            .Where(term => term.Coefficient != 0)
            .Select(term => term.DefineId.ToString(CultureInfo.InvariantCulture)).ToHashSet();
        var defineMap = new Dictionary<string, Dictionary<string, Dictionary<string, SelectedRow>>>();
        foreach (DefineDisgSnapshot definition in definitions.Where(item =>
            usedDefinitions.Contains(item.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var perMode = new Dictionary<string, Dictionary<string, SelectedRow>>();
            HashSet<string>? expectedStations = null;
            foreach (int signedId in definition.SignedCaseIds)
            {
                if (signedId == int.MinValue)
                    throw new InvalidOperationException("DEFINE case ID is outside the supported range.");
                Dictionary<string, FsecRowSnapshot>? rows = signedId == 0 ? zeroTemplate :
                    baseCases.GetValueOrDefault(Math.Abs(signedId).ToString(CultureInfo.InvariantCulture));
                if (rows == null) continue;
                if (expectedStations == null) expectedStations = rows.Keys.ToHashSet();
                else if (!expectedStations.SetEquals(rows.Keys))
                    throw new InvalidOperationException(
                        $"DEFINE '{definition.Id}' refers to inconsistent member stations.");
            }
            foreach (string mode in modes)
            {
                int component = ComponentOf(mode);
                bool maximum = mode.EndsWith("_max", StringComparison.Ordinal);
                var selected = new Dictionary<string, SelectedRow>();
                foreach (int signedId in definition.SignedCaseIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Dictionary<string, FsecRowSnapshot>? rows = signedId == 0 ? zeroTemplate :
                        baseCases.GetValueOrDefault(Math.Abs(signedId).ToString(CultureInfo.InvariantCulture));
                    if (rows == null) continue;
                    double sign = Math.Sign(signedId);
                    foreach (var (key, source) in rows)
                    {
                        FsecVector value = sign * source.Value;
                        if (!selected.TryGetValue(key, out SelectedRow current) ||
                            (maximum ? value.At(component) > current.Value.At(component) :
                                value.At(component) < current.Value.At(component)))
                            selected[key] = new SelectedRow(source, value, signedId);
                    }
                }
                perMode.Add(mode, selected);
            }
            if (!defineMap.TryAdd(definition.Id, perMode))
                throw new InvalidOperationException($"Duplicate DEFINE ID '{definition.Id}'.");
        }

        var output = new List<CombineFsecCaseResult>(snapshot.Combines.Length);
        foreach (CombineDisgSnapshot combination in snapshot.Combines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resultModes = new Dictionary<string, IReadOnlyList<CombineFsecRowResult>>();
            foreach (string mode in modes)
            {
                var accumulated = new Dictionary<string, AccumulatedRow>();
                HashSet<string>? expectedStations = null;
                foreach (CombineDisgTerm term in combination.Terms)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (term.Coefficient == 0 ||
                        !defineMap.TryGetValue(term.DefineId.ToString(CultureInfo.InvariantCulture),
                            out var selectedModes)) continue;
                    var selected = selectedModes[mode];
                    if (selected.Count == 0) continue;
                    if (expectedStations == null) expectedStations = selected.Keys.ToHashSet();
                    else if (!expectedStations.SetEquals(selected.Keys))
                        throw new InvalidOperationException(
                            $"COMBINE '{combination.Id}' refers to inconsistent member stations.");
                    foreach (var (key, source) in selected)
                    {
                        FsecVector value = term.Coefficient * source.Value;
                        if (!value.IsFinite)
                            throw new InvalidOperationException("Combination produced a non-finite member force.");
                        int labelCase = term.Coefficient < 0 ? -1 : source.SourceCase;
                        // Preserve worker1's signed source-case string, including its
                        // double minus for a negative DEFINE source case.
                        string label = labelCase == 0 ? string.Empty :
                            (labelCase < 0 ? "-" : "+") +
                            labelCase.ToString(CultureInfo.InvariantCulture);
                        if (accumulated.TryGetValue(key, out AccumulatedRow current))
                            accumulated[key] = current with
                            {
                                Value = current.Value + value,
                                Case = current.Case + label
                            };
                        else accumulated.Add(key, new AccumulatedRow(source.Row, value, label));
                    }
                }
                var rows = new List<CombineFsecRowResult>(accumulated.Count);
                foreach (AccumulatedRow row in accumulated.Values)
                {
                    if (!row.Value.IsFinite)
                        throw new InvalidOperationException("Combination produced a non-finite member force.");
                    rows.Add(new CombineFsecRowResult(row.Row.MemberId, row.Row.MemberDisplay,
                        row.Row.NodeId, row.Row.Location, row.Value.Fx, row.Value.Fy,
                        row.Value.Fz, row.Value.Mx, row.Value.My, row.Value.Mz, row.Case));
                }
                resultModes.Add(mode, rows);
            }
            output.Add(new CombineFsecCaseResult(combination.Id, combination.Name, resultModes));
        }
        return new ResultCombineFsecOutput(output);
    }

    internal static void ValidateBudget(long rows, long operations, long cells)
    {
        if (rows > MaxRows) throw new InvalidOperationException("Result rows exceed the supported limit.");
        if (operations > MaxScalarOperations)
            throw new InvalidOperationException("Combination work exceeds the supported limit.");
        if (cells > MaxOutputCells)
            throw new InvalidOperationException("Combination output exceeds the supported limit.");
    }

    private static Dictionary<string, FsecRowSnapshot> ToStationMap(
        ImmutableArray<FsecRowSnapshot> rows)
    {
        var result = new Dictionary<string, FsecRowSnapshot>();
        foreach (FsecRowSnapshot row in rows)
        {
            if (!row.Value.IsFinite || !double.IsFinite(row.Location))
                throw new InvalidOperationException("Result contains a non-finite member force.");
            // The JS worker keys rows by member and position rounded to three decimals.
            string key = row.MemberId + "-" +
                Math.Round(row.Location, 3, MidpointRounding.AwayFromZero)
                    .ToString("F3", CultureInfo.InvariantCulture);
            // The legacy base worker keeps the preceding j-end when the next
            // segment's i-end repeats the same station.
            result.TryAdd(key, row);
        }
        return result;
    }

    internal static int CountStationRows(ImmutableArray<FsecRowSnapshot> rows) =>
        ToStationMap(rows).Count;

    private static int ComponentOf(string mode) => mode[..2] switch
    {
        "fx" => 0, "fy" => 1, "fz" => 2, "mx" => 3, "my" => 4, "mz" => 5,
        _ => throw new ArgumentException($"Unknown mode '{mode}'.")
    };

    private readonly record struct SelectedRow(FsecRowSnapshot Row, FsecVector Value, int SourceCase);
    private readonly record struct AccumulatedRow(FsecRowSnapshot Row, FsecVector Value, string Case);
}
