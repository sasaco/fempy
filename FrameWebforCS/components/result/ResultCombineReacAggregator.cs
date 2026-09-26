using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace FrameWebforCS.components.result
{
    internal readonly record struct ReacVector(double Tx, double Ty, double Tz, double Mx, double My, double Mz)
    {
        public double At(int index) => index switch
        {
            0 => Tx, 1 => Ty, 2 => Tz, 3 => Mx, 4 => My, 5 => Mz,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
        public static ReacVector operator *(double scale, ReacVector value) =>
            new(scale * value.Tx, scale * value.Ty, scale * value.Tz,
                scale * value.Mx, scale * value.My, scale * value.Mz);
        public static ReacVector operator +(ReacVector a, ReacVector b) =>
            new(a.Tx + b.Tx, a.Ty + b.Ty, a.Tz + b.Tz,
                a.Mx + b.Mx, a.My + b.My, a.Mz + b.Mz);
        public bool IsFinite => double.IsFinite(Tx) && double.IsFinite(Ty) &&
            double.IsFinite(Tz) && double.IsFinite(Mx) && double.IsFinite(My) &&
            double.IsFinite(Mz);
    }

    internal sealed record ReacNodeSnapshot(string Id, ReacVector Value);
    internal sealed record ReacCaseSnapshot(string Id, ImmutableArray<ReacNodeSnapshot> Nodes);
    internal sealed record DefineReacSnapshot(string Id, ImmutableArray<int> SignedCaseIds);
    internal sealed record CombineReacTerm(int DefineId, double Coefficient);
    internal sealed record CombineReacSnapshot(string Id, string? Name,
        ImmutableArray<CombineReacTerm> Terms);
    internal sealed record ResultCombineReacSnapshot(long Revision, int Dimension,
        ImmutableArray<ReacCaseSnapshot> Reactions,
        ImmutableArray<DefineReacSnapshot> Defines,
        ImmutableArray<CombineReacSnapshot> Combines,
        ImmutableArray<int> StaticCaseIds);

    internal sealed record CombineReacNodeResult(string Id, double Tx, double Ty,
        double Tz, double Mx, double My, double Mz, string Case);
    internal sealed record CombineReacCaseResult(string Id, string? Name,
        IReadOnlyDictionary<string, IReadOnlyList<CombineReacNodeResult>> Rows);
    internal sealed record ResultCombineReacOutput(IReadOnlyList<CombineReacCaseResult> Cases);

    internal static class ResultCombineReacAggregator
    {
        internal const int MaxDefinitions = 10_000;
        internal const int MaxCombinations = 1_000;
        internal const int MaxNodes = 100_000;
        internal const long MaxScalarOperations = 50_000_000;
        internal const long MaxOutputCells = 10_000_000;
        private static readonly string[] ThreeDimensionalModes =
        {
            "tx_max", "tx_min", "ty_max", "ty_min", "tz_max", "tz_min",
            "mx_max", "mx_min", "my_max", "my_min", "mz_max", "mz_min"
        };
        private static readonly string[] TwoDimensionalModes =
        {
            "tx_max", "tx_min", "ty_max", "ty_min", "mz_max", "mz_min"
        };

        public static ResultCombineReacOutput Calculate(
            ResultCombineReacSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Dimension is not (2 or 3))
                throw new ArgumentException("Dimension must be 2 or 3.", nameof(snapshot));
            if (snapshot.Defines.Length > MaxDefinitions ||
                snapshot.Combines.Length > MaxCombinations)
                throw new InvalidOperationException("Combination count exceeds the supported limit.");
            long totalNodes = snapshot.Reactions.Sum(item => (long)item.Nodes.Length);
            ValidateBudget(totalNodes, 0, 0);

            string[] modes = snapshot.Dimension == 3 ? ThreeDimensionalModes : TwoDimensionalModes;
            var baseCases = snapshot.Reactions.ToDictionary(
                item => item.Id,
                item => item.Nodes.ToDictionary(node => node.Id, node => node.Value));
            Dictionary<string, ReacVector>? zeroCaseTemplate =
                snapshot.Reactions.IsEmpty ? null :
                baseCases[snapshot.Reactions[0].Id];
            var definitions = snapshot.Defines;
            if (definitions.IsEmpty)
            {
                definitions = snapshot.StaticCaseIds
                    .Where(id => id > 0 && baseCases.ContainsKey(id.ToString(CultureInfo.InvariantCulture)))
                    .Select(id => new DefineReacSnapshot(
                        id.ToString(CultureInfo.InvariantCulture), ImmutableArray.Create(id)))
                    .ToImmutableArray();
            }

            long estimate = 0;
            long cells = 0;
            foreach (CombineReacSnapshot combination in snapshot.Combines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (CombineReacTerm term in combination.Terms)
                {
                    DefineReacSnapshot? definition = definitions.FirstOrDefault(
                        item => item.Id == term.DefineId.ToString(CultureInfo.InvariantCulture));
                    if (definition == null || term.Coefficient == 0) continue;
                    foreach (int signedCaseId in definition.SignedCaseIds)
                    {
                        if (signedCaseId == 0)
                        {
                            if (zeroCaseTemplate != null)
                                estimate = checked(estimate + (long)zeroCaseTemplate.Count * modes.Length * 6);
                            ValidateBudget(totalNodes, estimate, cells);
                            continue;
                        }
                        if (signedCaseId == int.MinValue)
                            throw new InvalidOperationException("DEFINE case ID is outside the supported range.");
                        if (!baseCases.TryGetValue(Math.Abs(signedCaseId).ToString(CultureInfo.InvariantCulture),
                            out var nodes)) continue;
                        estimate = checked(estimate + (long)nodes.Count * modes.Length * 6);
                        ValidateBudget(totalNodes, estimate, cells);
                    }
                }
                int maxNodeCount = snapshot.Reactions.IsEmpty ? 0 :
                    snapshot.Reactions.Max(item => item.Nodes.Length);
                cells = checked(cells + (long)maxNodeCount * modes.Length * 8);
                ValidateBudget(totalNodes, estimate, cells);
            }

            var activeDefinitionIds = snapshot.Combines
                .SelectMany(combination => combination.Terms)
                .Where(term => term.Coefficient != 0)
                .Select(term => term.DefineId.ToString(CultureInfo.InvariantCulture))
                .ToHashSet();
            var defineMap = new Dictionary<string, Dictionary<string, Dictionary<string, SelectedVector>>>();
            foreach (DefineReacSnapshot definition in definitions.Where(item =>
                activeDefinitionIds.Contains(item.Id)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var perMode = new Dictionary<string, Dictionary<string, SelectedVector>>();
                HashSet<string>? expectedNodes = null;
                foreach (int signedCaseId in definition.SignedCaseIds)
                {
                    if (signedCaseId == 0)
                    {
                        if (zeroCaseTemplate == null) continue;
                        if (expectedNodes == null) expectedNodes = zeroCaseTemplate.Keys.ToHashSet();
                        else if (!expectedNodes.SetEquals(zeroCaseTemplate.Keys))
                            throw new InvalidOperationException(
                                $"DEFINE '{definition.Id}' refers to inconsistent reaction nodes.");
                        continue;
                    }
                    if (signedCaseId == int.MinValue)
                        throw new InvalidOperationException("DEFINE case ID is outside the supported range.");
                    if (!baseCases.TryGetValue(Math.Abs(signedCaseId).ToString(CultureInfo.InvariantCulture),
                        out var nodes)) continue;
                    if (expectedNodes == null) expectedNodes = nodes.Keys.ToHashSet();
                    else if (!expectedNodes.SetEquals(nodes.Keys))
                        throw new InvalidOperationException(
                            $"DEFINE '{definition.Id}' refers to inconsistent reaction nodes.");
                }
                foreach (string mode in modes)
                {
                    int component = ComponentOf(mode);
                    bool maximum = mode.EndsWith("_max", StringComparison.Ordinal);
                    var selected = new Dictionary<string, SelectedVector>();
                    foreach (int signedCaseId in definition.SignedCaseIds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Dictionary<string, ReacVector>? nodes;
                        if (signedCaseId == 0) nodes = zeroCaseTemplate;
                        else baseCases.TryGetValue(Math.Abs(signedCaseId).ToString(CultureInfo.InvariantCulture),
                            out nodes);
                        if (nodes == null) continue;
                        double sign = Math.Sign(signedCaseId);
                        foreach (var node in nodes)
                        {
                            ReacVector value = sign * node.Value;
                            if (!selected.TryGetValue(node.Key, out SelectedVector current) ||
                                (maximum ? value.At(component) > current.Value.At(component) :
                                    value.At(component) < current.Value.At(component)))
                                selected[node.Key] = new SelectedVector(value, signedCaseId);
                        }
                    }
                    perMode.Add(mode, selected);
                }
                if (!defineMap.TryAdd(definition.Id, perMode))
                    throw new InvalidOperationException($"Duplicate DEFINE ID '{definition.Id}'.");
            }

            var output = new List<CombineReacCaseResult>();
            foreach (CombineReacSnapshot combination in snapshot.Combines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rows = new Dictionary<string, IReadOnlyList<CombineReacNodeResult>>();
                foreach (string mode in modes)
                {
                    var accumulated = new Dictionary<string, AccumulatedVector>();
                    HashSet<string>? expectedNodes = null;
                    foreach (CombineReacTerm term in combination.Terms)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (term.Coefficient == 0 ||
                            !defineMap.TryGetValue(term.DefineId.ToString(CultureInfo.InvariantCulture),
                                out var definition)) continue;
                        if (definition[mode].Count == 0) continue;
                        if (expectedNodes == null) expectedNodes = definition[mode].Keys.ToHashSet();
                        else if (!expectedNodes.SetEquals(definition[mode].Keys))
                            throw new InvalidOperationException(
                                $"COMBINE '{combination.Id}' refers to inconsistent reaction nodes.");
                        foreach (var node in definition[mode])
                        {
                            ReacVector value = term.Coefficient * node.Value.Value;
                            if (!value.IsFinite)
                                throw new InvalidOperationException("Combination produced a non-finite reaction.");
                            string label = term.Coefficient < 0 ? "-" +
                                term.DefineId.ToString(CultureInfo.InvariantCulture) :
                                node.Value.SourceCase == 0 ? string.Empty :
                                (node.Value.SourceCase < 0 ? "-" : "+") +
                                term.DefineId.ToString(CultureInfo.InvariantCulture);
                            if (accumulated.TryGetValue(node.Key, out AccumulatedVector current))
                            {
                                if (!current.Value.IsFinite)
                                    throw new InvalidOperationException("Combination produced a non-finite reaction.");
                                accumulated[node.Key] = new AccumulatedVector(current.Value + value,
                                    current.Case + label);
                            }
                            else accumulated.Add(node.Key, new AccumulatedVector(value, label));
                        }
                    }
                    var modeRows = new List<CombineReacNodeResult>(accumulated.Count);
                    foreach (var node in accumulated
                        .OrderBy(item => TryGetJavaScriptArrayIndex(item.Key, out _) ? 0 : 1)
                        .ThenBy(item => TryGetJavaScriptArrayIndex(item.Key, out uint index) ? index : 0))
                    {
                        if (!node.Value.Value.IsFinite)
                            throw new InvalidOperationException("Combination produced a non-finite reaction.");
                        ReacVector value = node.Value.Value;
                        modeRows.Add(new CombineReacNodeResult(node.Key, value.Tx, value.Ty,
                            value.Tz, value.Mx, value.My, value.Mz, node.Value.Case));
                    }
                    rows.Add(mode, modeRows);
                }
                output.Add(new CombineReacCaseResult(combination.Id, combination.Name, rows));
            }
            return new ResultCombineReacOutput(output);
        }

        internal static void ValidateBudget(long nodeCount, long scalarOperations,
            long outputCells)
        {
            if (nodeCount > MaxNodes)
                throw new InvalidOperationException("Result nodes exceed the supported limit.");
            if (scalarOperations > MaxScalarOperations)
                throw new InvalidOperationException("Combination work exceeds the supported limit.");
            if (outputCells > MaxOutputCells)
                throw new InvalidOperationException("Combination output exceeds the supported limit.");
        }

        private static int ComponentOf(string mode) => mode[..2] switch
        {
            "tx" => 0, "ty" => 1, "tz" => 2, "mx" => 3, "my" => 4, "mz" => 5,
            _ => throw new ArgumentException($"Unknown mode {mode}.")
        };
        private static bool TryGetJavaScriptArrayIndex(string key, out uint index)
        {
            if (uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out index) &&
                index != uint.MaxValue &&
                key == index.ToString(CultureInfo.InvariantCulture))
                return true;
            index = 0;
            return false;
        }
        private readonly record struct SelectedVector(ReacVector Value, int SourceCase);
        private readonly record struct AccumulatedVector(ReacVector Value, string Case);
    }
}
