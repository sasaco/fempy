using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Results;

public sealed class ResultPresentationService
{
    public IReadOnlyList<PresentedStaticResult> BuildDerivedResults(
        AnalysisResultSet resultSet,
        IEnumerable<DerivedResultDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        ArgumentNullException.ThrowIfNull(definitions);
        AnalysisResultSetValidator.Validate(resultSet);

        Dictionary<string, SourceSnapshot> available = new(StringComparer.Ordinal);
        Dictionary<string, AnalysisCase> cases = resultSet.Cases.ToDictionary(value => value.CaseId, StringComparer.Ordinal);
        foreach (AnalysisResult result in resultSet.Results)
        {
            if (result is StaticAnalysisResult staticResult)
            {
                available.Add(result.CaseId, SourceSnapshot.From(staticResult));
            }
        }

        List<PresentedStaticResult> output = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (DerivedResultDefinition definition in definitions)
        {
            if (!ids.Add(definition.Id) || available.ContainsKey(definition.Id))
            {
                throw new ResultPresentationException($"Derived result ID '{definition.Id}' is duplicated.");
            }

            if (definition.Terms.Count == 0)
            {
                throw new ResultPresentationException($"Derived result '{definition.Id}' has no operands.");
            }

            List<WeightedSource> operands = [];
            foreach (DerivedResultTerm term in definition.Terms)
            {
                if (!double.IsFinite(term.Factor))
                {
                    throw new ResultPresentationException($"Derived result '{definition.Id}' has a non-finite factor.");
                }

                if (!available.TryGetValue(term.SourceId, out SourceSnapshot? source))
                {
                    if (cases.TryGetValue(term.SourceId, out AnalysisCase? resultCase) &&
                        resultCase.AnalysisType != AnalysisType.Static)
                    {
                        throw new ResultPresentationException(
                            $"Derived result '{definition.Id}' references non-static case '{term.SourceId}'.");
                    }

                    throw new ResultPresentationException(
                        $"Derived result '{definition.Id}' references unavailable source '{term.SourceId}'.");
                }

                operands.Add(new WeightedSource(term.SourceId, source, term.Factor));
            }

            EnsureCompatible(operands, definition.Id);
            PresentedStaticResult presented = Combine(definition, operands);
            output.Add(presented);
            available.Add(definition.Id, SourceSnapshot.From(presented));
        }

        return Array.AsReadOnly(output.ToArray());
    }

    public IReadOnlyList<ResultPresentationPage> BuildPages(
        AnalysisResultSet resultSet,
        IEnumerable<MovingLoadDefinition> movingLoads)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        ArgumentNullException.ThrowIfNull(movingLoads);
        AnalysisResultSetValidator.Validate(resultSet);

        Dictionary<string, AnalysisCase> cases = resultSet.Cases.ToDictionary(value => value.CaseId, StringComparer.Ordinal);
        Dictionary<string, int> caseOrder = resultSet.Cases
            .Select((value, index) => (value.CaseId, index))
            .ToDictionary(value => value.CaseId, value => value.index, StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<AnalysisResult>> resultsByCase = resultSet.Cases.ToDictionary(
            value => value.CaseId,
            value => (IReadOnlyList<AnalysisResult>)Array.AsReadOnly(
                resultSet.Results.Where(result => result.CaseId == value.CaseId).ToArray()),
            StringComparer.Ordinal);
        Dictionary<string, MovingLoadDefinition> movingByFirstCase = new(StringComparer.Ordinal);
        HashSet<string> groupedCases = new(StringComparer.Ordinal);

        foreach (MovingLoadDefinition definition in movingLoads)
        {
            if (definition.CaseIds.Count == 0)
            {
                throw new ResultPresentationException($"Moving load '{definition.Id}' has no cases.");
            }

            foreach (string caseId in definition.CaseIds)
            {
                if (!cases.TryGetValue(caseId, out AnalysisCase? resultCase))
                {
                    throw new ResultPresentationException(
                        $"Moving load '{definition.Id}' references unknown case '{caseId}'.");
                }

                if (resultCase.AnalysisType != AnalysisType.Static ||
                    resultsByCase[caseId].Count != 1 ||
                    resultsByCase[caseId][0] is not StaticAnalysisResult)
                {
                    throw new ResultPresentationException(
                        $"Moving load '{definition.Id}' requires static source case '{caseId}'.");
                }

                if (!groupedCases.Add(caseId))
                {
                    throw new ResultPresentationException(
                        $"Moving-load case '{caseId}' belongs to more than one definition.");
                }
            }

            int[] positions = definition.CaseIds.Select(caseId => caseOrder[caseId]).ToArray();
            if (!positions.SequenceEqual(positions.Order()))
            {
                throw new ResultPresentationException(
                    $"Moving load '{definition.Id}' cases must follow result-set case order.");
            }

            if (!movingByFirstCase.TryAdd(definition.CaseIds[0], definition))
            {
                throw new ResultPresentationException("Moving-load definitions have the same first case.");
            }
        }

        List<ResultPresentationPage> pages = [];
        HashSet<string> consumed = new(StringComparer.Ordinal);
        foreach (AnalysisCase resultCase in resultSet.Cases)
        {
            if (consumed.Contains(resultCase.CaseId))
            {
                continue;
            }

            if (movingByFirstCase.TryGetValue(resultCase.CaseId, out MovingLoadDefinition? movingLoad))
            {
                AnalysisResult[] sourceResults = movingLoad.CaseIds
                    .Select(caseId => resultsByCase[caseId][0])
                    .ToArray();
                foreach (string caseId in movingLoad.CaseIds)
                {
                    consumed.Add(caseId);
                }

                pages.Add(new ResultPresentationPage(
                    movingLoad.Id,
                    resultCase,
                    sourceResults[0],
                    sourceResults,
                    isMovingLoad: true));
                continue;
            }

            consumed.Add(resultCase.CaseId);
            foreach (AnalysisResult result in resultsByCase[resultCase.CaseId])
            {
                pages.Add(new ResultPresentationPage(
                    $"{result.CaseId}:{result.State.Kind}:{result.State.Index}",
                    resultCase,
                    result,
                    [result],
                    isMovingLoad: false));
            }
        }

        return Array.AsReadOnly(pages.ToArray());
    }

    public MovingLoadEnvelope BuildMovingLoadEnvelope(
        AnalysisResultSet resultSet,
        MovingLoadDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        ArgumentNullException.ThrowIfNull(definition);
        AnalysisResultSetValidator.Validate(resultSet);

        Dictionary<string, AnalysisResult[]> results = resultSet.Results
            .GroupBy(result => result.CaseId, StringComparer.Ordinal)
            .ToDictionary(
            group => group.Key,
            group => group.ToArray(),
            StringComparer.Ordinal);
        Dictionary<string, AnalysisCase> cases = resultSet.Cases.ToDictionary(
            value => value.CaseId,
            StringComparer.Ordinal);
        Dictionary<string, int> caseOrder = resultSet.Cases
            .Select((value, index) => (value.CaseId, index))
            .ToDictionary(value => value.CaseId, value => value.index, StringComparer.Ordinal);
        List<(string CaseId, StaticAnalysisResult Result)> sources = [];
        HashSet<string> seenCases = new(StringComparer.Ordinal);
        int previousCaseIndex = -1;
        foreach (string caseId in definition.CaseIds)
        {
            if (!seenCases.Add(caseId))
            {
                throw new ResultPresentationException(
                    $"Moving load '{definition.Id}' contains duplicate case '{caseId}'.");
            }

            if (!cases.TryGetValue(caseId, out AnalysisCase? resultCase))
            {
                throw new ResultPresentationException(
                    $"Moving load '{definition.Id}' references unknown case '{caseId}'.");
            }

            int currentCaseIndex = caseOrder[caseId];
            if (currentCaseIndex <= previousCaseIndex)
            {
                throw new ResultPresentationException(
                    $"Moving load '{definition.Id}' cases must follow result-set case order.");
            }

            if (!results.TryGetValue(caseId, out AnalysisResult[]? values) ||
                resultCase.AnalysisType != AnalysisType.Static ||
                values.Length != 1 ||
                values[0] is not StaticAnalysisResult staticResult)
            {
                throw new ResultPresentationException(
                    $"Moving load '{definition.Id}' requires static source case '{caseId}'.");
            }

            sources.Add((caseId, staticResult));
            previousCaseIndex = currentCaseIndex;
        }

        if (sources.Count == 0)
        {
            throw new ResultPresentationException($"Moving load '{definition.Id}' has no sources.");
        }

        EnsureCompatible(
            sources.Select(source => new WeightedSource(source.CaseId, SourceSnapshot.From(source.Result), 1)).ToList(),
            definition.Id);
        StaticAnalysisResult template = sources[0].Result;

        NodeDisplacementEnvelope[] nodes = template.NodeDisplacements.Select((node, index) =>
            new NodeDisplacementEnvelope(
                node.NodeId,
                new DisplacementEnvelopeComponents(
                    Envelope(sources, value => value.NodeDisplacements[index].Components.Dx),
                    Envelope(sources, value => value.NodeDisplacements[index].Components.Dy),
                    Envelope(sources, value => value.NodeDisplacements[index].Components.Dz),
                    Envelope(sources, value => value.NodeDisplacements[index].Components.Rx),
                    Envelope(sources, value => value.NodeDisplacements[index].Components.Ry),
                    Envelope(sources, value => value.NodeDisplacements[index].Components.Rz))))
            .ToArray();

        SupportReactionEnvelope[] reactions = template.SupportReactions.Select((reaction, index) =>
            new SupportReactionEnvelope(
                reaction.NodeId,
                EnvelopeForces(sources, value => value.SupportReactions[index].Components)))
            .ToArray();

        MemberSectionForceEnvelope[] members = template.MemberSectionForces.Select((member, memberIndex) =>
            new MemberSectionForceEnvelope(
                member.MemberId,
                member.Segments.Select((segment, segmentIndex) => new MemberSegmentEnvelope(
                    segment.SegmentId,
                    segment.StationI,
                    segment.StationJ,
                    segment.Length,
                    EnvelopeForces(sources, value =>
                        value.MemberSectionForces[memberIndex].Segments[segmentIndex].IEnd),
                    EnvelopeForces(sources, value =>
                        value.MemberSectionForces[memberIndex].Segments[segmentIndex].JEnd)))))
            .ToArray();

        return new MovingLoadEnvelope(definition.Id, definition.CaseIds, nodes, reactions, members);
    }

    private static PresentedStaticResult Combine(
        DerivedResultDefinition definition,
        IReadOnlyList<WeightedSource> sources)
    {
        SourceSnapshot template = sources[0].Snapshot;
        NodeDisplacement[] nodes = template.NodeDisplacements.Select((node, index) => new NodeDisplacement(
            node.NodeId,
            new DisplacementComponents(
                Aggregate(definition.Kind, sources, value => value.NodeDisplacements[index].Components.Dx),
                Aggregate(definition.Kind, sources, value => value.NodeDisplacements[index].Components.Dy),
                Aggregate(definition.Kind, sources, value => value.NodeDisplacements[index].Components.Dz),
                Aggregate(definition.Kind, sources, value => value.NodeDisplacements[index].Components.Rx),
                Aggregate(definition.Kind, sources, value => value.NodeDisplacements[index].Components.Ry),
                Aggregate(definition.Kind, sources, value => value.NodeDisplacements[index].Components.Rz))))
            .ToArray();

        SupportReaction[] reactions = template.SupportReactions.Select((reaction, index) => new SupportReaction(
            reaction.NodeId,
            CombineForces(definition.Kind, sources, value => value.SupportReactions[index].Components)))
            .ToArray();

        MemberSectionForces[] members = template.MemberSectionForces.Select((member, memberIndex) =>
            new MemberSectionForces(
                member.MemberId,
                member.Segments.Select((segment, segmentIndex) => new MemberSegmentResult(
                    segment.SegmentId,
                    segment.StationI,
                    segment.StationJ,
                    segment.Length,
                    CombineForces(definition.Kind, sources, value =>
                        value.MemberSectionForces[memberIndex].Segments[segmentIndex].IEnd),
                    CombineForces(definition.Kind, sources, value =>
                        value.MemberSectionForces[memberIndex].Segments[segmentIndex].JEnd)))))
            .ToArray();

        ShellResult[] shells = template.ShellResults.Select((shell, shellIndex) => new ShellResult(
            shell.ElementId,
            shell.Locations.Select((location, locationIndex) => new ShellResultLocation(
                location.LocationId,
                new MembraneForce(
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].MembraneForce.Nx),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].MembraneForce.Ny),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].MembraneForce.Nxy)),
                new BendingMoment(
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].BendingMoment.Mx),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].BendingMoment.My),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].BendingMoment.Mxy)),
                new TransverseShear(
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].TransverseShear.Qx),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].TransverseShear.Qy)),
                new PlaneStress(
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].TopStress.Sx),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].TopStress.Sy),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].TopStress.Txy)),
                new PlaneStress(
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].BottomStress.Sx),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].BottomStress.Sy),
                    Aggregate(definition.Kind, sources, value => value.ShellResults[shellIndex].Locations[locationIndex].BottomStress.Txy))))))
            .ToArray();

        SolidResult[] solids = template.SolidResults.Select((solid, solidIndex) => new SolidResult(
            solid.ElementId,
            solid.Locations.Select((location, locationIndex) => new SolidResultLocation(
                location.LocationId,
                new Stress3D(
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Stress.Sx),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Stress.Sy),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Stress.Sz),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Stress.Txy),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Stress.Tyz),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Stress.Tzx)),
                new Strain3D(
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Strain.Ex),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Strain.Ey),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Strain.Ez),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Strain.Gxy),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Strain.Gyz),
                    Aggregate(definition.Kind, sources, value => value.SolidResults[solidIndex].Locations[locationIndex].Strain.Gzx))))))
            .ToArray();

        return new PresentedStaticResult(
            definition.Id,
            definition.Name,
            definition.Kind,
            sources.Select(source => source.Id),
            nodes,
            reactions,
            members,
            shells,
            solids);
    }

    private static ForceComponents CombineForces(
        DerivedResultKind kind,
        IReadOnlyList<WeightedSource> sources,
        Func<SourceSnapshot, ForceComponents> selector)
        => new(
            Aggregate(kind, sources, value => selector(value).Fx),
            Aggregate(kind, sources, value => selector(value).Fy),
            Aggregate(kind, sources, value => selector(value).Fz),
            Aggregate(kind, sources, value => selector(value).Mx),
            Aggregate(kind, sources, value => selector(value).My),
            Aggregate(kind, sources, value => selector(value).Mz));

    private static double Aggregate(
        DerivedResultKind kind,
        IReadOnlyList<WeightedSource> sources,
        Func<SourceSnapshot, double> selector)
    {
        double result;
        if (kind == DerivedResultKind.Pickup)
        {
            result = sources
                .Select(source => source.Factor * selector(source.Snapshot))
                .Aggregate((selected, candidate) =>
                    Math.Abs(candidate) > Math.Abs(selected) ? candidate : selected);
        }
        else
        {
            result = sources.Sum(source => source.Factor * selector(source.Snapshot));
        }

        if (!double.IsFinite(result))
        {
            throw new ResultPresentationException("Derived result arithmetic produced a non-finite value.");
        }

        return result;
    }

    private static ForceEnvelopeComponents EnvelopeForces(
        IReadOnlyList<(string CaseId, StaticAnalysisResult Result)> sources,
        Func<StaticAnalysisResult, ForceComponents> selector)
        => new(
            Envelope(sources, value => selector(value).Fx),
            Envelope(sources, value => selector(value).Fy),
            Envelope(sources, value => selector(value).Fz),
            Envelope(sources, value => selector(value).Mx),
            Envelope(sources, value => selector(value).My),
            Envelope(sources, value => selector(value).Mz));

    private static ScalarEnvelope Envelope(
        IReadOnlyList<(string CaseId, StaticAnalysisResult Result)> sources,
        Func<StaticAnalysisResult, double> selector)
    {
        (string CaseId, double Value) maximum = (sources[0].CaseId, selector(sources[0].Result));
        (string CaseId, double Value) minimum = maximum;
        foreach ((string caseId, StaticAnalysisResult result) in sources.Skip(1))
        {
            double value = selector(result);
            if (value > maximum.Value)
            {
                maximum = (caseId, value);
            }

            if (value < minimum.Value)
            {
                minimum = (caseId, value);
            }
        }

        return new ScalarEnvelope(
            new EnvelopeExtreme(maximum.Value, maximum.CaseId),
            new EnvelopeExtreme(minimum.Value, minimum.CaseId));
    }

    private static void EnsureCompatible(IReadOnlyList<WeightedSource> sources, string ownerId)
    {
        if (sources.Count == 0)
        {
            throw new ResultPresentationException($"'{ownerId}' has no sources.");
        }

        SourceSnapshot template = sources[0].Snapshot;
        foreach (WeightedSource source in sources.Skip(1))
        {
            RequireSameIds(template.NodeDisplacements.Select(value => value.NodeId),
                source.Snapshot.NodeDisplacements.Select(value => value.NodeId), ownerId, "nodes");
            RequireSameIds(template.SupportReactions.Select(value => value.NodeId),
                source.Snapshot.SupportReactions.Select(value => value.NodeId), ownerId, "reactions");
            RequireSameIds(template.MemberSectionForces.Select(value => value.MemberId),
                source.Snapshot.MemberSectionForces.Select(value => value.MemberId), ownerId, "members");
            RequireSameIds(template.ShellResults.Select(value => value.ElementId),
                source.Snapshot.ShellResults.Select(value => value.ElementId), ownerId, "shells");
            RequireSameIds(template.SolidResults.Select(value => value.ElementId),
                source.Snapshot.SolidResults.Select(value => value.ElementId), ownerId, "solids");

            for (int memberIndex = 0; memberIndex < template.MemberSectionForces.Count; memberIndex++)
            {
                RequireSameIds(
                    template.MemberSectionForces[memberIndex].Segments.Select(value => value.SegmentId),
                    source.Snapshot.MemberSectionForces[memberIndex].Segments.Select(value => value.SegmentId),
                    ownerId,
                    "member segments");
            }

            for (int shellIndex = 0; shellIndex < template.ShellResults.Count; shellIndex++)
            {
                RequireSameIds(
                    template.ShellResults[shellIndex].Locations.Select(value => value.LocationId),
                    source.Snapshot.ShellResults[shellIndex].Locations.Select(value => value.LocationId),
                    ownerId,
                    "shell locations");
            }

            for (int solidIndex = 0; solidIndex < template.SolidResults.Count; solidIndex++)
            {
                RequireSameIds(
                    template.SolidResults[solidIndex].Locations.Select(value => value.LocationId),
                    source.Snapshot.SolidResults[solidIndex].Locations.Select(value => value.LocationId),
                    ownerId,
                    "solid locations");
            }
        }
    }

    private static void RequireSameIds(
        IEnumerable<string> expected,
        IEnumerable<string> actual,
        string ownerId,
        string description)
    {
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new ResultPresentationException(
                $"'{ownerId}' has incompatible {description} across its sources.");
        }
    }

    private sealed record WeightedSource(string Id, SourceSnapshot Snapshot, double Factor);

    private sealed class SourceSnapshot
    {
        private SourceSnapshot(
            IReadOnlyList<NodeDisplacement> nodeDisplacements,
            IReadOnlyList<SupportReaction> supportReactions,
            IReadOnlyList<MemberSectionForces> memberSectionForces,
            IReadOnlyList<ShellResult> shellResults,
            IReadOnlyList<SolidResult> solidResults)
        {
            NodeDisplacements = nodeDisplacements;
            SupportReactions = supportReactions;
            MemberSectionForces = memberSectionForces;
            ShellResults = shellResults;
            SolidResults = solidResults;
        }

        internal IReadOnlyList<NodeDisplacement> NodeDisplacements { get; }

        internal IReadOnlyList<SupportReaction> SupportReactions { get; }

        internal IReadOnlyList<MemberSectionForces> MemberSectionForces { get; }

        internal IReadOnlyList<ShellResult> ShellResults { get; }

        internal IReadOnlyList<SolidResult> SolidResults { get; }

        internal static SourceSnapshot From(StaticAnalysisResult result)
            => new(
                result.NodeDisplacements,
                result.SupportReactions,
                result.MemberSectionForces,
                result.ShellResults,
                result.SolidResults);

        internal static SourceSnapshot From(PresentedStaticResult result)
            => new(
                result.NodeDisplacements,
                result.SupportReactions,
                result.MemberSectionForces,
                result.ShellResults,
                result.SolidResults);
    }
}
