using System.Globalization;
using FrameWebforCS.Core.Documents;

namespace FrameWebforCS.Shell.Editing;

internal static class ProjectDocumentInsertPolicy
{
    private const int MaximumTopologyCandidates = 10_000;

    internal static void Apply(
        ProjectDocumentEditBatch batch,
        ProjectDocument document,
        InputTableKey table,
        int count)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(document);
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count));

        string[] ids = NextIds(AllIds(table, document), count);
        switch (table)
        {
            case InputTableKey.ModelSettings:
                throw NoCandidate(table);
            case InputTableKey.ElementPropertySets:
                ids = NextIds(document.ElementPropertySets.Select(set => set.Id).Append("1"), count);
                foreach (string id in ids)
                    batch.UpsertElementPropertySet(new ElementPropertySetDefinition(id, $"Element set {id}", document.Sections));
                return;
            case InputTableKey.SupportSets:
                ids = NextIds(document.SupportSets.Select(set => set.Id).Append("1"), count);
                foreach (string id in ids)
                    batch.UpsertSupportSet(new SupportSetDefinition(id, $"Support set {id}", []));
                return;
            case InputTableKey.JointReleaseSets:
                foreach (string id in ids)
                    batch.UpsertJointReleaseSet(new JointReleaseSetDefinition(id, $"Joint set {id}", []));
                return;
            case InputTableKey.MemberSpringSets:
                foreach (string id in ids)
                    batch.UpsertMemberSpringSet(new MemberSpringSetDefinition(id, $"Spring set {id}", []));
                return;
            case InputTableKey.Members:
                InsertMembers(batch, document, ids);
                return;
            case InputTableKey.RigidZones:
                InsertRigidZones(batch, document, ids);
                return;
            case InputTableKey.Supports:
                InsertSupports(batch, document, ids);
                return;
            case InputTableKey.Panels:
                InsertPanels(batch, document, ids);
                return;
            case InputTableKey.Joints:
                InsertJoints(batch, document, ids);
                return;
            case InputTableKey.NoticePoints:
                InsertNoticePoints(batch, document, ids);
                return;
            case InputTableKey.MemberSprings:
                InsertMemberSprings(batch, document, ids);
                return;
        }

        foreach (string id in ids)
        {
            switch (table)
            {
                case InputTableKey.Nodes:
                    batch.UpsertNode(new ProjectNode(id, 0, 0, 0));
                    break;
                case InputTableKey.Sections:
                    batch.UpsertSection(new FrameSectionDefinition(
                        id, $"Section {id}", 2.05e8, 0.3, 7.88e7, 1, 1, 1, 1));
                    break;
                case InputTableKey.LoadCases:
                    batch.UpsertLoadCase(new LoadCaseDefinition(id, $"Case {id}", $"C{id}"));
                    break;
                case InputTableKey.NodalLoads:
                    Require(document.LoadCases.Count > 0 && document.Nodes.Count > 0, table);
                    batch.UpsertNodalLoad(new NodalLoadDefinition(
                        id, document.LoadCases[0].Id, document.Nodes[0].Id, 1, 0, 0, 0, 0, 0));
                    break;
                case InputTableKey.PrescribedDisplacements:
                    Require(document.LoadCases.Count > 0 && document.Nodes.Count > 0, table);
                    batch.UpsertPrescribedDisplacement(new PrescribedDisplacementDefinition(
                        id, document.LoadCases[0].Id, document.Nodes[0].Id, 0.001, 0, 0, 0, 0, 0));
                    break;
                case InputTableKey.MemberLoads:
                    Require(document.LoadCases.Count > 0 && document.Members.Count > 0, table);
                    ProjectMember member = document.Members[0];
                    batch.UpsertMemberLoad(new MemberLoadDefinition(
                        id,
                        document.LoadCases[0].Id,
                        member.Id,
                        MemberLoadKind.PointForce,
                        MemberLoadDirection.LocalY,
                        MemberLength(document, member) / 2,
                        0,
                        1,
                        0));
                    break;
                case InputTableKey.Define:
                    Require(document.LoadCases.Count > 0, table);
                    batch.UpsertDerivedResult(new DerivedResultDefinition(
                        id,
                        $"DEFINE {id}",
                        DerivedResultKind.Define,
                        [new DerivedResultTerm(document.LoadCases[0].Id, 1)]));
                    break;
                case InputTableKey.Combine:
                    InsertDerived(batch, document, id, DerivedResultKind.Combine, DerivedResultKind.Define);
                    break;
                case InputTableKey.Pickup:
                    InsertDerived(batch, document, id, DerivedResultKind.Pickup, DerivedResultKind.Combine);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(table));
            }
        }
    }

    private static void InsertMembers(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        string sectionId = CommonSectionId(document);
        HashSet<string> used = document.Members.Select(member => PairKey(member.NodeI, member.NodeJ))
            .ToHashSet(StringComparer.Ordinal);
        List<(string NodeI, string NodeJ)> candidates = [];
        foreach (int[] nodeIndices in EnumerateTopologyCandidates(document.Nodes.Count, 2))
        {
            string nodeI = document.Nodes[nodeIndices[0]].Id;
            string nodeJ = document.Nodes[nodeIndices[1]].Id;
            if (used.Add(PairKey(nodeI, nodeJ)))
                candidates.Add((nodeI, nodeJ));
            if (candidates.Count == ids.Count)
                break;
        }

        Require(candidates.Count == ids.Count, InputTableKey.Members);
        for (int index = 0; index < ids.Count; index++)
            batch.UpsertMember(new ProjectMember(ids[index], candidates[index].NodeI, candidates[index].NodeJ, sectionId));
    }

    private static void InsertRigidZones(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        string sectionId = CommonSectionId(document);
        HashSet<string> used = document.RigidZones.Select(zone => zone.MemberId).ToHashSet(StringComparer.Ordinal);
        ProjectMember[] candidates = document.Members.Where(member => used.Add(member.Id)).Take(ids.Count).ToArray();
        Require(candidates.Length == ids.Count, InputTableKey.RigidZones);
        for (int index = 0; index < ids.Count; index++)
            batch.UpsertRigidZone(new RigidZoneDefinition(ids[index], candidates[index].Id, 0, 0, sectionId));
    }

    private static void InsertSupports(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        HashSet<string> used = document.Supports.Select(row => row.NodeId).ToHashSet(StringComparer.Ordinal);
        ProjectNode[] candidates = document.Nodes.Where(node => used.Add(node.Id)).Take(ids.Count).ToArray();
        Require(candidates.Length == ids.Count, InputTableKey.Supports);
        for (int index = 0; index < ids.Count; index++)
            batch.UpsertSupport(new ProjectSupport(ids[index], candidates[index].Id, true, false, false, false, false, false));
    }

    private static void InsertPanels(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        string sectionId = CommonSectionId(document);
        HashSet<string> used = document.Panels.Select(panel => NodeSetKey(panel.NodeIds)).ToHashSet(StringComparer.Ordinal);
        List<string[]> candidates = [];
        foreach (int[] nodeIndices in EnumerateTopologyCandidates(document.Nodes.Count, 4))
        {
            ProjectNode[] nodes = nodeIndices.Select(index => document.Nodes[index]).ToArray();
            string[]? ordered = OrderPlanarPanel(nodes);
            if (ordered is not null && used.Add(NodeSetKey(ordered)))
                candidates.Add(ordered);
            if (candidates.Count == ids.Count)
                break;
        }

        Require(candidates.Count == ids.Count, InputTableKey.Panels);
        for (int index = 0; index < ids.Count; index++)
            batch.UpsertPanel(new PanelDefinition(ids[index], sectionId, candidates[index]));
    }

    private static void InsertJoints(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        JointReleaseSetDefinition? existing = document.JointReleaseSets.FirstOrDefault();
        string setId = existing?.Id ?? "1";
        string name = existing?.Name ?? "Default";
        List<JointReleaseDefinition> rows = existing?.Rows.ToList() ?? [];
        HashSet<string> used = rows.Select(row => row.MemberId).ToHashSet(StringComparer.Ordinal);
        ProjectMember[] candidates = document.Members.Where(member => used.Add(member.Id)).Take(ids.Count).ToArray();
        Require(candidates.Length == ids.Count, InputTableKey.Joints);
        for (int index = 0; index < ids.Count; index++)
            rows.Add(new JointReleaseDefinition(ids[index], candidates[index].Id, true, true, true, true, true, true));
        batch.UpsertJointReleaseSet(new JointReleaseSetDefinition(setId, name, rows));
    }

    private static void InsertNoticePoints(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        List<(string MemberId, double Distance)> candidates = [];
        foreach (ProjectMember member in document.Members)
        {
            double length = MemberLength(document, member);
            HashSet<double> used = document.NoticePoints.Where(point => point.MemberId == member.Id)
                .Select(point => point.Distance).ToHashSet();
            for (int index = 1; index <= 20 && candidates.Count < ids.Count; index++)
            {
                double distance = length * index / 21;
                if (used.Add(distance))
                    candidates.Add((member.Id, distance));
            }

            if (candidates.Count == ids.Count)
                break;
        }

        Require(candidates.Count == ids.Count, InputTableKey.NoticePoints);
        for (int index = 0; index < ids.Count; index++)
            batch.UpsertNoticePoint(new NoticePointDefinition(ids[index], candidates[index].MemberId, candidates[index].Distance));
    }

    private static void InsertMemberSprings(ProjectDocumentEditBatch batch, ProjectDocument document, IReadOnlyList<string> ids)
    {
        MemberSpringSetDefinition? existing = document.MemberSpringSets.FirstOrDefault();
        string setId = existing?.Id ?? "1";
        string name = existing?.Name ?? "Default";
        List<MemberSpringDefinition> rows = existing?.Rows.ToList() ?? [];
        HashSet<string> used = rows.Select(row => row.MemberId).ToHashSet(StringComparer.Ordinal);
        ProjectMember[] candidates = document.Members.Where(member => used.Add(member.Id)).Take(ids.Count).ToArray();
        Require(candidates.Length == ids.Count, InputTableKey.MemberSprings);
        for (int index = 0; index < ids.Count; index++)
            rows.Add(new MemberSpringDefinition(ids[index], candidates[index].Id, 1, 0, 0, 0));
        batch.UpsertMemberSpringSet(new MemberSpringSetDefinition(setId, name, rows));
    }

    private static void InsertDerived(
        ProjectDocumentEditBatch batch,
        ProjectDocument document,
        string id,
        DerivedResultKind kind,
        DerivedResultKind sourceKind)
    {
        DerivedResultDefinition source = document.DerivedResults.FirstOrDefault(result => result.Kind == sourceKind)
            ?? throw NoCandidate(kind == DerivedResultKind.Combine ? InputTableKey.Combine : InputTableKey.Pickup);
        batch.UpsertDerivedResult(new DerivedResultDefinition(
            id,
            $"{kind.ToString().ToUpperInvariant()} {id}",
            kind,
            [new DerivedResultTerm(source.Id, 1)]));
    }

    private static string CommonSectionId(ProjectDocument document)
    {
        HashSet<string> common = document.Sections.Select(section => section.Id).ToHashSet(StringComparer.Ordinal);
        foreach (ElementPropertySetDefinition set in document.ElementPropertySets)
            common.IntersectWith(set.Sections.Select(section => section.Id));
        return document.Sections.Select(section => section.Id).FirstOrDefault(common.Contains)
            ?? throw NoCandidate(InputTableKey.Sections);
    }

    private static string[]? OrderPlanarPanel(IReadOnlyList<ProjectNode> nodes)
    {
        (double X, double Y, double Z) origin = (nodes[0].X, nodes[0].Y, nodes[0].Z);
        (double X, double Y, double Z) normal = default;
        for (int first = 1; first < nodes.Count - 1 && Magnitude(normal) <= 1e-12; first++)
            for (int second = first + 1; second < nodes.Count && Magnitude(normal) <= 1e-12; second++)
                normal = Cross(Vector(origin, nodes[first]), Vector(origin, nodes[second]));
        double normalMagnitude = Magnitude(normal);
        if (normalMagnitude <= 1e-12)
            return null;

        double scale = nodes.Select(node => Magnitude(Vector(origin, node))).DefaultIfEmpty(1).Max();
        if (nodes.Any(node => Math.Abs(Dot(normal, Vector(origin, node))) > 1e-9 * normalMagnitude * Math.Max(scale, 1)))
            return null;

        int dropAxis = Math.Abs(normal.X) >= Math.Abs(normal.Y) && Math.Abs(normal.X) >= Math.Abs(normal.Z)
            ? 0
            : Math.Abs(normal.Y) >= Math.Abs(normal.Z) ? 1 : 2;
        (ProjectNode Node, double U, double V)[] projected = nodes.Select(node => dropAxis switch
        {
            0 => (node, node.Y, node.Z),
            1 => (node, node.X, node.Z),
            _ => (node, node.X, node.Y),
        }).ToArray();
        double centerU = projected.Average(point => point.U);
        double centerV = projected.Average(point => point.V);
        projected = projected.OrderBy(point => Math.Atan2(point.V - centerV, point.U - centerU)).ToArray();
        double area = 0;
        for (int index = 0; index < projected.Length; index++)
        {
            (ProjectNode Node, double U, double V) current = projected[index];
            (ProjectNode Node, double U, double V) next = projected[(index + 1) % projected.Length];
            area += (current.U * next.V) - (next.U * current.V);
        }

        return Math.Abs(area) > 1e-12 ? projected.Select(point => point.Node.Id).ToArray() : null;
    }

    private static (double X, double Y, double Z) Vector(
        (double X, double Y, double Z) origin,
        ProjectNode node) => (node.X - origin.X, node.Y - origin.Y, node.Z - origin.Z);

    private static (double X, double Y, double Z) Cross(
        (double X, double Y, double Z) first,
        (double X, double Y, double Z) second) =>
        ((first.Y * second.Z) - (first.Z * second.Y),
         (first.Z * second.X) - (first.X * second.Z),
         (first.X * second.Y) - (first.Y * second.X));

    private static double Dot(
        (double X, double Y, double Z) first,
        (double X, double Y, double Z) second) =>
        (first.X * second.X) + (first.Y * second.Y) + (first.Z * second.Z);

    private static double Magnitude((double X, double Y, double Z) value) =>
        Math.Sqrt(Dot(value, value));

    private static double MemberLength(ProjectDocument document, ProjectMember member)
    {
        ProjectNode nodeI = document.Nodes.Single(node => node.Id == member.NodeI);
        ProjectNode nodeJ = document.Nodes.Single(node => node.Id == member.NodeJ);
        double dx = nodeJ.X - nodeI.X;
        double dy = nodeJ.Y - nodeI.Y;
        double dz = nodeJ.Z - nodeI.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static string PairKey(string first, string second) => string.CompareOrdinal(first, second) <= 0
        ? $"{first}\u001f{second}"
        : $"{second}\u001f{first}";

    private static string NodeSetKey(IEnumerable<string> nodeIds) =>
        string.Join('\u001f', nodeIds.Order(StringComparer.Ordinal));

    private static IEnumerable<int[]> EnumerateTopologyCandidates(int itemCount, int selectionCount)
    {
        if (selectionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(selectionCount));
        if (itemCount < selectionCount)
            yield break;

        int[] indices = Enumerable.Range(0, selectionCount).ToArray();
        for (int inspected = 0; inspected < MaximumTopologyCandidates; inspected++)
        {
            yield return [.. indices];

            int position = selectionCount - 1;
            while (position >= 0 && indices[position] == itemCount - selectionCount + position)
                position--;
            if (position < 0)
                yield break;

            indices[position]++;
            for (int next = position + 1; next < selectionCount; next++)
                indices[next] = indices[next - 1] + 1;
        }
    }

    private static IEnumerable<string> AllIds(InputTableKey table, ProjectDocument document) => table switch
    {
        InputTableKey.Nodes => document.Nodes.Select(row => row.Id),
        InputTableKey.Members => document.Members.Select(row => row.Id),
        InputTableKey.RigidZones => document.RigidZones.Select(row => row.Id),
        InputTableKey.ElementPropertySets => document.ElementPropertySets.Select(row => row.Id),
        InputTableKey.Supports => document.Supports.Select(row => row.Id),
        InputTableKey.SupportSets => document.SupportSets.Select(row => row.Id),
        InputTableKey.Sections => document.Sections.Select(row => row.Id),
        InputTableKey.Panels => document.Panels.Select(row => row.Id),
        InputTableKey.Joints => document.JointReleaseSets.SelectMany(set => set.Rows).Select(row => row.Id),
        InputTableKey.JointReleaseSets => document.JointReleaseSets.Select(row => row.Id),
        InputTableKey.NoticePoints => document.NoticePoints.Select(row => row.Id),
        InputTableKey.MemberSprings => document.MemberSpringSets.SelectMany(set => set.Rows).Select(row => row.Id),
        InputTableKey.MemberSpringSets => document.MemberSpringSets.Select(row => row.Id),
        InputTableKey.LoadCases => document.LoadCases.Select(row => row.Id)
            .Concat(document.DerivedResults.Select(row => row.Id))
            .Concat(document.MovingLoads.Select(row => row.Id)),
        InputTableKey.NodalLoads or InputTableKey.PrescribedDisplacements => document.NodalLoads.Select(row => row.Id)
            .Concat(document.PrescribedDisplacements.Select(row => row.Id)),
        InputTableKey.MemberLoads => document.MemberLoads.Select(row => row.Id),
        InputTableKey.Define or InputTableKey.Combine or InputTableKey.Pickup => document.DerivedResults.Select(row => row.Id)
            .Concat(document.LoadCases.Select(row => row.Id))
            .Concat(document.MovingLoads.Select(row => row.Id)),
        _ => [],
    };

    private static string[] NextIds(IEnumerable<string> existing, int count)
    {
        HashSet<string> used = existing.ToHashSet(StringComparer.Ordinal);
        List<string> ids = [];
        for (int candidate = 1; ids.Count < count; candidate++)
        {
            string id = candidate.ToString(CultureInfo.InvariantCulture);
            if (used.Add(id))
                ids.Add(id);
        }

        return ids.ToArray();
    }

    private static void Require(bool condition, InputTableKey table)
    {
        if (!condition)
            throw NoCandidate(table);
    }

    private static InvalidOperationException NoCandidate(InputTableKey table) =>
        new($"Table '{table}' has no unused valid insert candidate.");
}
