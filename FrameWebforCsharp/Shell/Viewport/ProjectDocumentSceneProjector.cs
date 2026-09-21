using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Rendering.Scene;

namespace PDF_Manager.Shell.Viewport;

public sealed record ViewportSceneProjectionText(string ScaleCaption, string ColorCaption);

public sealed record ViewportSceneProjection(
    ViewportSceneModel Scene,
    SceneLayerMask ChangedLayers,
    ResultCoordinate? ResultCoordinate);

/// <summary>
/// Projects the complete typed Core document/result state into the Rendering boundary.
/// This class deliberately contains no WinForms state and can be exercised without a GL context.
/// </summary>
public sealed class ProjectDocumentSceneProjector
{
    public ViewportSceneProjection Project(
        string stableId,
        ProjectDocument document,
        AnalysisResultSet? resultSet,
        AnalysisResult? result,
        int pageIndex,
        int pageCount,
        ViewportPresentationState presentation,
        ViewportSceneProjectionText text,
        ViewportSceneModel? previous = null,
        IReadOnlyList<SupportReaction>? reactionProjection = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(text);
        presentation.Validate();

        if (result is not null && resultSet is null)
        {
            throw new ArgumentException("A selected result requires its owning result set.", nameof(resultSet));
        }

        Dictionary<string, SceneNode> nodes = document.Nodes.ToDictionary(
            value => value.Id,
            value => new SceneNode(value.Id, Point(value.X, value.Y, value.Z)),
            StringComparer.Ordinal);
        Dictionary<string, SceneMember> members = document.Members.ToDictionary(
            value => value.Id,
            value => new SceneMember(value.Id, value.NodeI, value.NodeJ),
            StringComparer.Ordinal);
        AddResultTopology(resultSet, nodes, members);

        Dictionary<string, ProjectNode> documentNodes = document.Nodes.ToDictionary(
            value => value.Id,
            StringComparer.Ordinal);
        Dictionary<string, ProjectMember> documentMembers = document.Members.ToDictionary(
            value => value.Id,
            StringComparer.Ordinal);

        SceneResultContext? resultContext = result is null
            ? null
            : new SceneResultContext(
                result.CaseId,
                ResultState(result.State.Kind),
                result.State.Index,
                pageIndex,
                Math.Max(1, pageCount),
                presentation.ExtremaMode);

        IReadOnlyList<NodeDisplacement> displacementValues =
            ViewportResultExtrema.SelectDisplacements(result, presentation.ExtremaMode);
        IReadOnlyList<SupportReaction> reactionValues = reactionProjection is null
            ? ViewportResultExtrema.SelectReactions(result, presentation.ExtremaMode)
            : ViewportResultExtrema.SelectReactions(reactionProjection, presentation.ExtremaMode);
        IReadOnlyList<ViewportSectionForceValue> sectionForceValues =
            ViewportResultExtrema.SelectSectionForces(result, presentation.ExtremaMode);
        IReadOnlyList<SceneNodeDisplacement> displacements = CreateDisplacements(displacementValues);
        IReadOnlyList<SceneReaction> reactions = CreateReactions(reactionValues);
        IReadOnlyList<SceneSectionForce> sectionForces = CreateSectionForces(resultSet, sectionForceValues);

        ScenePresentationOptions scenePresentation = CreatePresentation(
            nodes.Values,
            members.Values,
            displacements,
            reactions,
            sectionForces,
            displacementValues.Select(ViewportResultExtrema.SignedValue).ToArray(),
            reactionValues.Select(ViewportResultExtrema.SignedValue).ToArray(),
            sectionForceValues.Select(ViewportResultExtrema.SignedValue).ToArray(),
            document.Dimension,
            presentation,
            text);

        string? activeCaseId = result?.CaseId;

        ViewportSceneModel scene = new(
            stableId,
            nodes.Values,
            members.Values,
            CreateSupports(document),
            document.NodalLoads
                .Where(value => activeCaseId is null || string.Equals(value.CaseId, activeCaseId, StringComparison.Ordinal))
                .Select(value => new SceneNodalLoad(
                value.Id,
                value.NodeId,
                Point(value.Fx, value.Fy, value.Fz),
                Point(value.Mx, value.My, value.Mz))),
            document.MemberLoads
                .Where(value => activeCaseId is null || string.Equals(value.CaseId, activeCaseId, StringComparison.Ordinal))
                .Select(value => CreateMemberLoad(
                    value,
                    documentMembers[value.MemberId],
                    documentNodes)),
            new SceneDisplacementLayer(
                $"{stableId}:result:displacements",
                displacements,
                presentation.DisplacementScale,
                resultContext,
                presentation.DisplayMode == ViewportDisplayMode.Displacements),
            document.RigidZones.Select(value => new SceneRigidZone(
                value.Id,
                value.MemberId,
                Float(value.ILength),
                Float(value.JLength))),
            CreateSprings(document, documentMembers),
            CreateJoints(document, documentMembers),
            document.Panels.Select(value => new ScenePanel(value.Id, value.NodeIds)),
            document.NoticePoints.Select(value => new SceneNoticePoint(
                value.Id,
                value.MemberId,
                RelativePosition(value.Distance, documentMembers[value.MemberId], documentNodes))),
            document.PrescribedDisplacements
                .Where(value => activeCaseId is null || string.Equals(value.CaseId, activeCaseId, StringComparison.Ordinal))
                .Select(value => new ScenePrescribedDisplacement(
                    value.Id,
                    value.NodeId,
                    Point(value.Dx, value.Dy, value.Dz),
                    Point(value.Rx, value.Ry, value.Rz))),
            new SceneReactionLayer(
                $"{stableId}:result:reactions",
                reactions,
                context: resultContext,
                isVisible: presentation.DisplayMode == ViewportDisplayMode.Reactions),
            new SceneSectionForceLayer(
                $"{stableId}:result:section-forces",
                sectionForces,
                context: resultContext,
                isVisible: presentation.DisplayMode == ViewportDisplayMode.SectionForces),
            scenePresentation,
            presentation.VisibleLayers);

        return new ViewportSceneProjection(scene, scene.GetChangedLayers(previous), result?.Coordinate);
    }

    private static void AddResultTopology(
        AnalysisResultSet? resultSet,
        IDictionary<string, SceneNode> nodes,
        IDictionary<string, SceneMember> members)
    {
        if (resultSet is null)
        {
            return;
        }

        foreach (TopologyNode node in resultSet.Topology.Nodes)
        {
            nodes.TryAdd(
                node.NodeId,
                new SceneNode(node.NodeId, Point(node.Coordinates.X, node.Coordinates.Y, node.Coordinates.Z)));
        }

        foreach (TopologyMember member in resultSet.Topology.Members)
        {
            members.TryAdd(member.MemberId, new SceneMember(member.MemberId, member.NodeI, member.NodeJ));
        }
    }

    private static IEnumerable<SceneSupport> CreateSupports(ProjectDocument document)
    {
        foreach (ProjectSupport support in document.Supports)
        {
            yield return new SceneSupport(
                support.Id,
                support.NodeId,
                support.FixX,
                support.FixY,
                support.FixZ,
                support.FixRx,
                support.FixRy,
                support.FixRz);
        }

        foreach (SupportSetDefinition set in document.SupportSets)
        {
            foreach (SupportConditionDefinition support in set.Rows)
            {
                bool fixX = support.Tx != 0;
                bool fixY = support.Ty != 0;
                bool fixZ = support.Tz != 0;
                bool fixRx = support.Rx != 0;
                bool fixRy = support.Ry != 0;
                bool fixRz = support.Rz != 0;
                if (fixX || fixY || fixZ || fixRx || fixRy || fixRz)
                {
                    yield return new SceneSupport(
                        SetRowId(set.Id, support.Id),
                        support.NodeId,
                        fixX,
                        fixY,
                        fixZ,
                        fixRx,
                        fixRy,
                        fixRz);
                }
            }
        }
    }

    private static IEnumerable<SceneSpring> CreateSprings(
        ProjectDocument document,
        IReadOnlyDictionary<string, ProjectMember> members)
    {
        foreach (MemberSpringSetDefinition set in document.MemberSpringSets)
        {
            foreach (MemberSpringDefinition spring in set.Rows)
            {
                ProjectMember member = members[spring.MemberId];
                yield return new SceneSpring(SetRowId(set.Id, spring.Id), member.NodeI, member.NodeJ);
            }
        }
    }

    private static IEnumerable<SceneJoint> CreateJoints(
        ProjectDocument document,
        IReadOnlyDictionary<string, ProjectMember> members)
    {
        foreach (JointReleaseSetDefinition set in document.JointReleaseSets)
        {
            foreach (JointReleaseDefinition joint in set.Rows)
            {
                ProjectMember member = members[joint.MemberId];
                if (!joint.ConnectXi || !joint.ConnectYi || !joint.ConnectZi)
                {
                    yield return new SceneJoint(
                        $"{SetRowId(set.Id, joint.Id)}/i",
                        member.NodeI,
                        !joint.ConnectXi,
                        !joint.ConnectYi,
                        !joint.ConnectZi);
                }

                if (!joint.ConnectXj || !joint.ConnectYj || !joint.ConnectZj)
                {
                    yield return new SceneJoint(
                        $"{SetRowId(set.Id, joint.Id)}/j",
                        member.NodeJ,
                        !joint.ConnectXj,
                        !joint.ConnectYj,
                        !joint.ConnectZj);
                }
            }
        }
    }

    private static SceneMemberLoad CreateMemberLoad(
        MemberLoadDefinition load,
        ProjectMember member,
        IReadOnlyDictionary<string, ProjectNode> nodes)
    {
        double length = MemberLength(member, nodes);
        if (load.Kind == MemberLoadKind.Thermal)
        {
            return SceneMemberLoad.Thermal(load.Id, load.MemberId, Float(load.P1), Float(load.P2));
        }

        (double x, double y, double z) = MemberLoadDirectionVector(load.Direction, member, nodes, length);
        ScenePoint3 first = Point(x * load.P1, y * load.P1, z * load.P1);
        ScenePoint3 second = Point(x * load.P2, y * load.P2, z * load.P2);
        if (load.Kind == MemberLoadKind.DistributedForce)
        {
            double end = load.L2 < 0 ? load.L1 - load.L2 : length - load.L2;
            return SceneMemberLoad.Distributed(
                load.Id,
                load.MemberId,
                Float(load.L1 / length),
                Float(end / length),
                first,
                second);
        }

        SceneLoadVectorKind vectorKind = load.Kind == MemberLoadKind.PointMoment
            ? SceneLoadVectorKind.Moment
            : SceneLoadVectorKind.Force;
        return SceneMemberLoad.PointPair(
            load.Id,
            load.MemberId,
            Float(load.L1 / length),
            first,
            Float(load.L2 / length),
            second,
            vectorKind);
    }

    private static (double X, double Y, double Z) MemberLoadDirectionVector(
        MemberLoadDirection direction,
        ProjectMember member,
        IReadOnlyDictionary<string, ProjectNode> nodes,
        double length)
    {
        if (direction == MemberLoadDirection.GlobalX) return (1.0, 0.0, 0.0);
        if (direction == MemberLoadDirection.GlobalY) return (0.0, 1.0, 0.0);
        if (direction == MemberLoadDirection.GlobalZ) return (0.0, 0.0, 1.0);

        ProjectNode start = nodes[member.NodeI];
        ProjectNode end = nodes[member.NodeJ];
        double localXx = (end.X - start.X) / length;
        double localXy = (end.Y - start.Y) / length;
        double localXz = (end.Z - start.Z) / length;
        double horizontal = Math.Sqrt((localXx * localXx) + (localXy * localXy));
        (double X, double Y, double Z) localY;
        (double X, double Y, double Z) localZ;
        if (horizontal <= 1.0e-12)
        {
            localY = (localXz, 0.0, 0.0);
            localZ = (0.0, 1.0, 0.0);
        }
        else
        {
            localY = (-localXy / horizontal, localXx / horizontal, 0.0);
            localZ = (-(localXx * localXz) / horizontal, -(localXy * localXz) / horizontal, horizontal);
        }

        double radians = member.RotationDegrees * Math.PI / 180.0;
        double cosine = Math.Cos(radians);
        double sine = Math.Sin(radians);
        (double X, double Y, double Z) rotatedY = (
            (cosine * localY.X) + (sine * localZ.X),
            (cosine * localY.Y) + (sine * localZ.Y),
            (cosine * localY.Z) + (sine * localZ.Z));
        (double X, double Y, double Z) rotatedZ = (
            (-sine * localY.X) + (cosine * localZ.X),
            (-sine * localY.Y) + (cosine * localZ.Y),
            (-sine * localY.Z) + (cosine * localZ.Z));

        return direction switch
        {
            MemberLoadDirection.LocalX => (localXx, localXy, localXz),
            MemberLoadDirection.LocalY => rotatedY,
            MemberLoadDirection.LocalZ => rotatedZ,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    private static IReadOnlyList<SceneNodeDisplacement> CreateDisplacements(
        IReadOnlyList<NodeDisplacement> values) =>
        values.Select(value => new SceneNodeDisplacement(
            value.NodeId,
            Point(value.Components.Dx, value.Components.Dy, value.Components.Dz))).ToArray();

    private static IReadOnlyList<SceneReaction> CreateReactions(
        IReadOnlyList<SupportReaction> values) =>
        values.Select(value => new SceneReaction(
                value.NodeId,
                value.NodeId,
                Point(value.Components.Fx, value.Components.Fy, value.Components.Fz),
                Point(value.Components.Mx, value.Components.My, value.Components.Mz))).ToArray();

    private static IReadOnlyList<SceneSectionForce> CreateSectionForces(
        AnalysisResultSet? resultSet,
        IReadOnlyList<ViewportSectionForceValue> values)
    {
        if (resultSet is null || values.Count == 0)
        {
            return [];
        }

        Dictionary<string, TopologyMember> topology = resultSet.Topology.Members.ToDictionary(
            value => value.MemberId,
            StringComparer.Ordinal);
        List<SceneSectionForce> samples = [];
        foreach (ViewportSectionForceValue value in values)
        {
            TopologyMember definition = topology[value.MemberId];
            Dictionary<string, double> positions = definition.Stations.ToDictionary(
                value => value.StationId,
                value => value.Position,
                StringComparer.Ordinal);
            double length = definition.Stations[^1].Position;
            samples.Add(SectionForce(
                value.Id,
                value.MemberId,
                positions[value.StationId] / length,
                value.Components));
        }

        return samples;
    }

    private static SceneSectionForce SectionForce(
        string id,
        string memberId,
        double position,
        ForceComponents value) => new(
            id,
            memberId,
            Float(position),
            Point(value.Fx, value.Fy, value.Fz),
            Point(value.Mx, value.My, value.Mz));

    private static ScenePresentationOptions CreatePresentation(
        IEnumerable<SceneNode> nodes,
        IEnumerable<SceneMember> members,
        IReadOnlyList<SceneNodeDisplacement> displacements,
        IReadOnlyList<SceneReaction> reactions,
        IReadOnlyList<SceneSectionForce> sectionForces,
        IReadOnlyList<double> displacementValues,
        IReadOnlyList<double> reactionValues,
        IReadOnlyList<double> sectionForceValues,
        ModelDimension dimension,
        ViewportPresentationState state,
        ViewportSceneProjectionText text)
    {
        SceneNode[] nodeValues = nodes.ToArray();
        Dictionary<string, SceneNode> byId = nodeValues.ToDictionary(value => value.Id, StringComparer.Ordinal);
        SceneGridPlane gridPlane = dimension == ModelDimension.TwoDimensional
            ? SceneGridPlane.XZ
            : SceneGridPlane.XY;
        SceneGridDefinition? grid = state.ShowGrid
            ? new SceneGridDefinition(gridPlane, GridSpacing(nodeValues, gridPlane), 5)
            : null;
        IEnumerable<SceneLabel> labels = state.ShowLabels
            ? CreateLabels(nodeValues, members, byId)
            : [];

        SceneScaleLegend? scaleLegend = null;
        SceneColorLegend? colorLegend = null;
        if (state.ShowLegends && state.DisplayMode is ViewportDisplayMode.Displacements or
            ViewportDisplayMode.Reactions or ViewportDisplayMode.SectionForces)
        {
            float scale = state.DisplayMode == ViewportDisplayMode.Displacements ? state.DisplacementScale : 1.0f;
            scaleLegend = new SceneScaleLegend("result-scale", text.ScaleCaption, scale);
            double[] values = state.DisplayMode switch
            {
                ViewportDisplayMode.Displacements => displacementValues.ToArray(),
                ViewportDisplayMode.Reactions => reactionValues.ToArray(),
                ViewportDisplayMode.SectionForces => sectionForceValues.ToArray(),
                _ => [],
            };
            float minimum = values.Length == 0 ? 0.0f : Float(values.Min());
            float maximum = values.Length == 0 ? 0.0f : Float(values.Max());
            float midpoint = minimum + ((maximum - minimum) * 0.5f);
            colorLegend = new SceneColorLegend(
                "result-colors",
                text.ColorCaption,
                [
                    new SceneColorLegendEntry("minimum", minimum, 0.1f, 0.3f, 0.9f),
                    new SceneColorLegendEntry("midpoint", midpoint, 0.1f, 0.8f, 0.3f),
                    new SceneColorLegendEntry("maximum", maximum, 0.9f, 0.2f, 0.1f),
                ]);
        }

        return new ScenePresentationOptions(grid, state.ShowAxes, labels, scaleLegend, colorLegend);
    }

    private static IEnumerable<SceneLabel> CreateLabels(
        IEnumerable<SceneNode> nodes,
        IEnumerable<SceneMember> members,
        IReadOnlyDictionary<string, SceneNode> byId)
    {
        foreach (SceneNode node in nodes)
        {
            yield return new SceneLabel(
                $"node/{node.Id}",
                node.Id,
                node.Position,
                new SceneEntityKey(SceneEntityKind.Node, node.Id));
        }

        foreach (SceneMember member in members)
        {
            ScenePoint3 start = byId[member.StartNodeId].Position;
            ScenePoint3 end = byId[member.EndNodeId].Position;
            yield return new SceneLabel(
                $"member/{member.Id}",
                member.Id,
                new ScenePoint3(
                    (start.X + end.X) * 0.5f,
                    (start.Y + end.Y) * 0.5f,
                    (start.Z + end.Z) * 0.5f),
                new SceneEntityKey(SceneEntityKind.Member, member.Id));
        }
    }

    private static float GridSpacing(IReadOnlyList<SceneNode> nodes, SceneGridPlane plane)
    {
        if (nodes.Count < 2)
        {
            return 1.0f;
        }

        float firstExtent = plane == SceneGridPlane.YZ
            ? nodes.Max(value => value.Position.Y) - nodes.Min(value => value.Position.Y)
            : nodes.Max(value => value.Position.X) - nodes.Min(value => value.Position.X);
        float secondExtent = plane == SceneGridPlane.XY
            ? nodes.Max(value => value.Position.Y) - nodes.Min(value => value.Position.Y)
            : nodes.Max(value => value.Position.Z) - nodes.Min(value => value.Position.Z);
        float extent = Math.Max(firstExtent, secondExtent);
        if (!float.IsFinite(extent) || extent <= 0.0f)
        {
            return 1.0f;
        }

        return MathF.Pow(10.0f, MathF.Floor(MathF.Log10(extent / 8.0f)));
    }

    private static float RelativePosition(
        double distance,
        ProjectMember member,
        IReadOnlyDictionary<string, ProjectNode> nodes) =>
        Float(distance / MemberLength(member, nodes));

    private static double MemberLength(
        ProjectMember member,
        IReadOnlyDictionary<string, ProjectNode> nodes)
    {
        ProjectNode start = nodes[member.NodeI];
        ProjectNode end = nodes[member.NodeJ];
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double dz = end.Z - start.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static string SetRowId(string setId, string rowId) => $"{setId}/{rowId}";

    private static SceneResultStateKind ResultState(ResultStateKind kind) => kind switch
    {
        ResultStateKind.Static => SceneResultStateKind.Static,
        ResultStateKind.LoadStep => SceneResultStateKind.LoadStep,
        ResultStateKind.Mode => SceneResultStateKind.Mode,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static ScenePoint3 Point(double x, double y, double z) =>
        new(Float(x), Float(y), Float(z));

    private static float Float(double value)
    {
        float result = checked((float)value);
        if (!float.IsFinite(result))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Scene values must be finite single-precision numbers.");
        }

        return result;
    }
}
