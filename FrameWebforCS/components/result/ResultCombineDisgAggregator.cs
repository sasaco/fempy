using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace FrameWebforCS.components.result
{
    internal readonly record struct DisgVector(double Dx, double Dy, double Dz, double Rx, double Ry, double Rz)
    {
        public double At(int index) => index switch
        {
            0 => Dx, 1 => Dy, 2 => Dz, 3 => Rx, 4 => Ry, 5 => Rz,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
        public static DisgVector operator *(double scale, DisgVector value) =>
            new(scale * value.Dx, scale * value.Dy, scale * value.Dz,
                scale * value.Rx, scale * value.Ry, scale * value.Rz);
        public static DisgVector operator +(DisgVector a, DisgVector b) =>
            new(a.Dx + b.Dx, a.Dy + b.Dy, a.Dz + b.Dz,
                a.Rx + b.Rx, a.Ry + b.Ry, a.Rz + b.Rz);
        public bool IsFinite => double.IsFinite(Dx) && double.IsFinite(Dy) &&
            double.IsFinite(Dz) && double.IsFinite(Rx) && double.IsFinite(Ry) &&
            double.IsFinite(Rz);
    }

    internal sealed record DisgNodeSnapshot(string Id, DisgVector Value);
    internal sealed record DisgCaseSnapshot(string Id, ImmutableArray<DisgNodeSnapshot> Nodes);
    internal sealed record DefineDisgSnapshot(string Id, ImmutableArray<int> SignedCaseIds);
    internal sealed record CombineDisgTerm(int DefineId, double Coefficient);
    internal sealed record CombineDisgSnapshot(string Id, string? Name,
        ImmutableArray<CombineDisgTerm> Terms);
    internal sealed record ResultCombineDisgSnapshot(long Revision, int Dimension,
        ImmutableArray<DisgCaseSnapshot> Displacements,
        ImmutableArray<DefineDisgSnapshot> Defines,
        ImmutableArray<CombineDisgSnapshot> Combines,
        ImmutableArray<int> StaticCaseIds);

    internal sealed record CombineDisgNodeResult(string Id, double Dx, double Dy,
        double Dz, double Rx, double Ry, double Rz, string Case);
    internal sealed record CombineDisgCaseResult(string Id, string? Name,
        IReadOnlyDictionary<string, IReadOnlyList<CombineDisgNodeResult>> Rows);
    internal sealed record ResultCombineDisgOutput(IReadOnlyList<CombineDisgCaseResult> Cases);

    internal static class ResultCombineDisgAggregator
    {
        internal const int MaxDefinitions = 10_000;
        internal const int MaxCombinations = 1_000;
        internal const int MaxNodes = 100_000;
        internal const long MaxScalarOperations = 50_000_000;
        internal const long MaxOutputCells = 10_000_000;
        private static readonly string[] ThreeDimensionalModes =
        {
            "dx_max", "dx_min", "dy_max", "dy_min", "dz_max", "dz_min",
            "rx_max", "rx_min", "ry_max", "ry_min", "rz_max", "rz_min"
        };
        private static readonly string[] TwoDimensionalModes =
        {
            "dx_max", "dx_min", "dy_max", "dy_min", "rz_max", "rz_min"
        };

        public static ResultCombineDisgOutput Calculate(
            ResultCombineDisgSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Dimension is not (2 or 3))
                throw new ArgumentException("Dimension must be 2 or 3.", nameof(snapshot));
            if (snapshot.Defines.Length > MaxDefinitions ||
                snapshot.Combines.Length > MaxCombinations)
                throw new InvalidOperationException("Combination count exceeds the supported limit.");
            long totalNodes = snapshot.Displacements.Sum(item => (long)item.Nodes.Length);
            ValidateBudget(totalNodes, 0, 0);

            string[] modes = snapshot.Dimension == 3 ? ThreeDimensionalModes : TwoDimensionalModes;
            var baseCases = snapshot.Displacements.ToDictionary(
                item => item.Id,
                item => item.Nodes.ToDictionary(node => node.Id, node => node.Value));
            Dictionary<string, DisgVector>? zeroCaseTemplate =
                snapshot.Displacements.IsEmpty ? null :
                baseCases[snapshot.Displacements[0].Id];
            var definitions = snapshot.Defines;
            if (definitions.IsEmpty)
            {
                definitions = snapshot.StaticCaseIds
                    .Where(id => id > 0 && baseCases.ContainsKey(id.ToString(CultureInfo.InvariantCulture)))
                    .Select(id => new DefineDisgSnapshot(
                        id.ToString(CultureInfo.InvariantCulture), ImmutableArray.Create(id)))
                    .ToImmutableArray();
            }

            long estimate = 0;
            long cells = 0;
            foreach (CombineDisgSnapshot combination in snapshot.Combines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (CombineDisgTerm term in combination.Terms)
                {
                    DefineDisgSnapshot? definition = definitions.FirstOrDefault(
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
                int maxNodeCount = snapshot.Displacements.IsEmpty ? 0 :
                    snapshot.Displacements.Max(item => item.Nodes.Length);
                cells = checked(cells + (long)maxNodeCount * modes.Length * 8);
                ValidateBudget(totalNodes, estimate, cells);
            }

            var activeDefinitionIds = snapshot.Combines
                .SelectMany(combination => combination.Terms)
                .Where(term => term.Coefficient != 0)
                .Select(term => term.DefineId.ToString(CultureInfo.InvariantCulture))
                .ToHashSet();
            var defineMap = new Dictionary<string, Dictionary<string, Dictionary<string, SelectedVector>>>();
            foreach (DefineDisgSnapshot definition in definitions.Where(item =>
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
                                $"DEFINE '{definition.Id}' refers to inconsistent displacement nodes.");
                        continue;
                    }
                    if (signedCaseId == int.MinValue)
                        throw new InvalidOperationException("DEFINE case ID is outside the supported range.");
                    if (!baseCases.TryGetValue(Math.Abs(signedCaseId).ToString(CultureInfo.InvariantCulture),
                        out var nodes)) continue;
                    if (expectedNodes == null) expectedNodes = nodes.Keys.ToHashSet();
                    else if (!expectedNodes.SetEquals(nodes.Keys))
                        throw new InvalidOperationException(
                            $"DEFINE '{definition.Id}' refers to inconsistent displacement nodes.");
                }
                foreach (string mode in modes)
                {
                    int component = ComponentOf(mode);
                    bool maximum = mode.EndsWith("_max", StringComparison.Ordinal);
                    var selected = new Dictionary<string, SelectedVector>();
                    foreach (int signedCaseId in definition.SignedCaseIds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Dictionary<string, DisgVector>? nodes;
                        if (signedCaseId == 0) nodes = zeroCaseTemplate;
                        else baseCases.TryGetValue(Math.Abs(signedCaseId).ToString(CultureInfo.InvariantCulture),
                            out nodes);
                        if (nodes == null) continue;
                        double sign = Math.Sign(signedCaseId);
                        foreach (var node in nodes)
                        {
                            DisgVector value = sign * node.Value;
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

            var output = new List<CombineDisgCaseResult>();
            foreach (CombineDisgSnapshot combination in snapshot.Combines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rows = new Dictionary<string, IReadOnlyList<CombineDisgNodeResult>>();
                foreach (string mode in modes)
                {
                    var accumulated = new Dictionary<string, AccumulatedVector>();
                    HashSet<string>? expectedNodes = null;
                    foreach (CombineDisgTerm term in combination.Terms)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (term.Coefficient == 0 ||
                            !defineMap.TryGetValue(term.DefineId.ToString(CultureInfo.InvariantCulture),
                                out var definition)) continue;
                        if (definition[mode].Count == 0) continue;
                        if (expectedNodes == null) expectedNodes = definition[mode].Keys.ToHashSet();
                        else if (!expectedNodes.SetEquals(definition[mode].Keys))
                            throw new InvalidOperationException(
                                $"COMBINE '{combination.Id}' refers to inconsistent displacement nodes.");
                        foreach (var node in definition[mode])
                        {
                            DisgVector value = term.Coefficient * node.Value.Value;
                            if (!value.IsFinite)
                                throw new InvalidOperationException("Combination produced a non-finite displacement.");
                            string label = term.Coefficient < 0 ? "-" +
                                term.DefineId.ToString(CultureInfo.InvariantCulture) :
                                node.Value.SourceCase == 0 ? string.Empty :
                                (node.Value.SourceCase < 0 ? "-" : "+") +
                                term.DefineId.ToString(CultureInfo.InvariantCulture);
                            if (accumulated.TryGetValue(node.Key, out AccumulatedVector current))
                            {
                                if (!current.Value.IsFinite)
                                    throw new InvalidOperationException("Combination produced a non-finite displacement.");
                                accumulated[node.Key] = new AccumulatedVector(current.Value + value,
                                    current.Case + label);
                            }
                            else accumulated.Add(node.Key, new AccumulatedVector(value, label));
                        }
                    }
                    var modeRows = new List<CombineDisgNodeResult>(accumulated.Count);
                    foreach (var node in accumulated
                        .OrderBy(item => TryGetJavaScriptArrayIndex(item.Key, out _) ? 0 : 1)
                        .ThenBy(item => TryGetJavaScriptArrayIndex(item.Key, out uint index) ? index : 0))
                    {
                        if (!node.Value.Value.IsFinite)
                            throw new InvalidOperationException("Combination produced a non-finite displacement.");
                        DisgVector value = node.Value.Value;
                        modeRows.Add(new CombineDisgNodeResult(node.Key, value.Dx, value.Dy,
                            value.Dz, value.Rx, value.Ry, value.Rz, node.Value.Case));
                    }
                    rows.Add(mode, modeRows);
                }
                output.Add(new CombineDisgCaseResult(combination.Id, combination.Name, rows));
            }
            return new ResultCombineDisgOutput(output);
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
            "dx" => 0, "dy" => 1, "dz" => 2, "rx" => 3, "ry" => 4, "rz" => 5,
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
        private readonly record struct SelectedVector(DisgVector Value, int SourceCase);
        private readonly record struct AccumulatedVector(DisgVector Value, string Case);
    }
}
