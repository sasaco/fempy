using System.Collections.ObjectModel;

namespace FrameWebforCS.Rendering.Scene;

public enum SceneEntityKind
{
    Node,
    Member,
    RigidZone,
    Support,
    Spring,
    Joint,
    Panel,
    NoticePoint,
    NodalLoad,
    MemberLoad,
    PrescribedDisplacement,
    Displacement,
    Reaction,
    SectionForce,
}

public readonly record struct SceneEntityKey
{
    public SceneEntityKey(SceneEntityKind kind, string id)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Kind = kind;
        Id = id;
    }

    public SceneEntityKind Kind { get; }

    public string Id { get; }
}

public readonly record struct ScenePoint3
{
    public ScenePoint3(float x, float y, float z)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Scene coordinates must be finite.");
        }

        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }
}

public sealed record SceneNode(string Id, ScenePoint3 Position);

public sealed record SceneMember(string Id, string StartNodeId, string EndNodeId);

public sealed record SceneRigidZone(string Id, string MemberId, float StartLength, float EndLength);

public sealed record SceneSupport(
    string Id,
    string NodeId,
    bool FixX,
    bool FixY,
    bool FixZ,
    bool FixRx = false,
    bool FixRy = false,
    bool FixRz = false);

public sealed record SceneSpring(string Id, string StartNodeId, string EndNodeId);

public sealed record SceneJoint(
    string Id,
    string NodeId,
    bool ReleaseX,
    bool ReleaseY,
    bool ReleaseZ,
    bool ReleaseRx = false,
    bool ReleaseRy = false,
    bool ReleaseRz = false);

public sealed class ScenePanel : IEquatable<ScenePanel>
{
    private readonly ReadOnlyCollection<string> _nodeIds;

    public ScenePanel(string id, IEnumerable<string> nodeIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        _nodeIds = SceneCollectionMaterializer.Materialize(nodeIds, nameof(nodeIds), 16);
        if (_nodeIds.Count is < 3 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeIds), "A panel requires 3 to 16 boundary nodes.");
        }

        if (_nodeIds.Any(string.IsNullOrWhiteSpace) || _nodeIds.Distinct(StringComparer.Ordinal).Count() != _nodeIds.Count)
        {
            throw new ArgumentException("Panel boundary node IDs must be non-empty and unique.", nameof(nodeIds));
        }
    }

    public string Id { get; }

    public IReadOnlyList<string> NodeIds => _nodeIds;

    public bool Equals(ScenePanel? other) =>
        other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal) && _nodeIds.SequenceEqual(other._nodeIds);

    public override bool Equals(object? obj) => Equals(obj as ScenePanel);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Id, StringComparer.Ordinal);
        foreach (string nodeId in _nodeIds)
        {
            hash.Add(nodeId, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

public sealed record SceneNoticePoint(string Id, string MemberId, float RelativePosition);

public sealed record SceneNodalLoad(string Id, string NodeId, ScenePoint3 Vector, ScenePoint3 Moment = default);

public sealed record ScenePrescribedDisplacement(string Id, string NodeId, ScenePoint3 Translation, ScenePoint3 Rotation = default);

public enum SceneMemberLoadKind
{
    Point,
    Distributed,
    Thermal,
}

public enum SceneLoadVectorKind
{
    Force,
    Moment,
}

public sealed record SceneMemberLoad
{
    public SceneMemberLoad(string id, string memberId, float relativePosition, ScenePoint3 vector)
        : this(id, memberId, SceneMemberLoadKind.Point, SceneLoadVectorKind.Force, relativePosition, relativePosition, vector, vector, 0.0f, 0.0f)
    {
    }

    private SceneMemberLoad(
        string id,
        string memberId,
        SceneMemberLoadKind kind,
        SceneLoadVectorKind vectorKind,
        float startRelativePosition,
        float endRelativePosition,
        ScenePoint3 startVector,
        ScenePoint3 endVector,
        float temperatureTop,
        float temperatureBottom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Enum.IsDefined(vectorKind))
        {
            throw new ArgumentOutOfRangeException(nameof(vectorKind));
        }

        Id = id;
        MemberId = memberId;
        Kind = kind;
        VectorKind = vectorKind;
        StartRelativePosition = startRelativePosition;
        EndRelativePosition = endRelativePosition;
        StartVector = startVector;
        EndVector = endVector;
        TemperatureTop = temperatureTop;
        TemperatureBottom = temperatureBottom;
    }

    public string Id { get; }

    public string MemberId { get; }

    public SceneMemberLoadKind Kind { get; }

    public SceneLoadVectorKind VectorKind { get; }

    public float RelativePosition => StartRelativePosition;

    public ScenePoint3 Vector => StartVector;

    public float StartRelativePosition { get; }

    public float EndRelativePosition { get; }

    public ScenePoint3 StartVector { get; }

    public ScenePoint3 EndVector { get; }

    public float TemperatureTop { get; }

    public float TemperatureBottom { get; }

    public static SceneMemberLoad Point(string id, string memberId, float relativePosition, ScenePoint3 vector) =>
        new(id, memberId, relativePosition, vector);

    public static SceneMemberLoad PointPair(
        string id,
        string memberId,
        float firstRelativePosition,
        ScenePoint3 firstVector,
        float secondRelativePosition,
        ScenePoint3 secondVector,
        SceneLoadVectorKind vectorKind = SceneLoadVectorKind.Force) =>
        new(id, memberId, SceneMemberLoadKind.Point, vectorKind, firstRelativePosition, secondRelativePosition, firstVector, secondVector, 0.0f, 0.0f);

    public static SceneMemberLoad Distributed(
        string id,
        string memberId,
        float startRelativePosition,
        float endRelativePosition,
        ScenePoint3 startVector,
        ScenePoint3 endVector,
        SceneLoadVectorKind vectorKind = SceneLoadVectorKind.Force) =>
        new(id, memberId, SceneMemberLoadKind.Distributed, vectorKind, startRelativePosition, endRelativePosition, startVector, endVector, 0.0f, 0.0f);

    public static SceneMemberLoad Thermal(string id, string memberId, float temperatureTop, float temperatureBottom) =>
        new(id, memberId, SceneMemberLoadKind.Thermal, SceneLoadVectorKind.Force, 0.0f, 1.0f, default, default, temperatureTop, temperatureBottom);
}

public sealed record SceneNodeDisplacement(string NodeId, ScenePoint3 Vector);

public sealed record SceneReaction(string Id, string NodeId, ScenePoint3 Force, ScenePoint3 Moment = default);

public sealed record SceneSectionForce(
    string Id,
    string MemberId,
    float RelativePosition,
    ScenePoint3 Force,
    ScenePoint3 Moment);

public sealed class SceneNodeLayer(string stableId, IEnumerable<SceneNode> items, bool isVisible = true)
    : ViewportSceneLayer<SceneNode>(stableId, SceneLayerKind.Nodes, items, static value => new(SceneEntityKind.Node, value.Id), isVisible);

public sealed class SceneMemberLayer(string stableId, IEnumerable<SceneMember> items, bool isVisible = true)
    : ViewportSceneLayer<SceneMember>(stableId, SceneLayerKind.Members, items, static value => new(SceneEntityKind.Member, value.Id), isVisible);

public sealed class SceneRigidZoneLayer(string stableId, IEnumerable<SceneRigidZone> items, bool isVisible = true)
    : ViewportSceneLayer<SceneRigidZone>(stableId, SceneLayerKind.RigidZones, items, static value => new(SceneEntityKind.RigidZone, value.Id), isVisible);

public sealed class SceneSupportLayer(string stableId, IEnumerable<SceneSupport> items, bool isVisible = true)
    : ViewportSceneLayer<SceneSupport>(stableId, SceneLayerKind.Supports, items, static value => new(SceneEntityKind.Support, value.Id), isVisible);

public sealed class SceneSpringLayer(string stableId, IEnumerable<SceneSpring> items, bool isVisible = true)
    : ViewportSceneLayer<SceneSpring>(stableId, SceneLayerKind.Springs, items, static value => new(SceneEntityKind.Spring, value.Id), isVisible);

public sealed class SceneJointLayer(string stableId, IEnumerable<SceneJoint> items, bool isVisible = true)
    : ViewportSceneLayer<SceneJoint>(stableId, SceneLayerKind.Joints, items, static value => new(SceneEntityKind.Joint, value.Id), isVisible);

public sealed class ScenePanelLayer(string stableId, IEnumerable<ScenePanel> items, bool isVisible = true)
    : ViewportSceneLayer<ScenePanel>(stableId, SceneLayerKind.Panels, items, static value => new(SceneEntityKind.Panel, value.Id), isVisible);

public sealed class SceneNoticePointLayer(string stableId, IEnumerable<SceneNoticePoint> items, bool isVisible = true)
    : ViewportSceneLayer<SceneNoticePoint>(stableId, SceneLayerKind.NoticePoints, items, static value => new(SceneEntityKind.NoticePoint, value.Id), isVisible);

public sealed class SceneLoadLayer : IViewportSceneLayer
{
    private readonly ReadOnlyCollection<SceneNodalLoad> _nodalLoads;
    private readonly ReadOnlyCollection<SceneMemberLoad> _memberLoads;
    private readonly ReadOnlyCollection<ScenePrescribedDisplacement> _prescribedDisplacements;
    private readonly ReadOnlyCollection<SceneEntityKey> _entityKeys;

    public SceneLoadLayer(
        string stableId,
        IEnumerable<SceneNodalLoad> nodalLoads,
        IEnumerable<SceneMemberLoad> memberLoads,
        IEnumerable<ScenePrescribedDisplacement>? prescribedDisplacements = null,
        bool isVisible = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        StableId = stableId;
        IsVisible = isVisible;
        _nodalLoads = SceneCollectionMaterializer.Materialize(nodalLoads, nameof(nodalLoads));
        _memberLoads = SceneCollectionMaterializer.Materialize(memberLoads, nameof(memberLoads));
        _prescribedDisplacements = SceneCollectionMaterializer.Materialize(prescribedDisplacements ?? [], nameof(prescribedDisplacements));
        _entityKeys = Array.AsReadOnly(
            _nodalLoads.Select(value => new SceneEntityKey(SceneEntityKind.NodalLoad, value.Id))
                .Concat(_memberLoads.Select(value => new SceneEntityKey(SceneEntityKind.MemberLoad, value.Id)))
                .Concat(_prescribedDisplacements.Select(value => new SceneEntityKey(SceneEntityKind.PrescribedDisplacement, value.Id)))
                .ToArray());
        if (_entityKeys.Distinct().Count() != _entityKeys.Count)
        {
            throw new ArgumentException("Load entity keys must be unique.");
        }
    }

    public string StableId { get; }

    public SceneLayerKind Kind => SceneLayerKind.Loads;

    public bool IsVisible { get; }

    public int Count => checked(_nodalLoads.Count + _memberLoads.Count + _prescribedDisplacements.Count);

    public IReadOnlyList<SceneEntityKey> EntityKeys => _entityKeys;

    public IReadOnlyList<SceneNodalLoad> NodalLoads => _nodalLoads;

    public IReadOnlyList<SceneMemberLoad> MemberLoads => _memberLoads;

    public IReadOnlyList<ScenePrescribedDisplacement> PrescribedDisplacements => _prescribedDisplacements;

    public bool HasSameContent(IViewportSceneLayer other) =>
        other is SceneLoadLayer typed &&
        string.Equals(StableId, typed.StableId, StringComparison.Ordinal) &&
        IsVisible == typed.IsVisible &&
        _nodalLoads.SequenceEqual(typed._nodalLoads) &&
        _memberLoads.SequenceEqual(typed._memberLoads) &&
        _prescribedDisplacements.SequenceEqual(typed._prescribedDisplacements);
}

public sealed class SceneDisplacementLayer : ViewportSceneLayer<SceneNodeDisplacement>
{
    public SceneDisplacementLayer(
        string stableId,
        IEnumerable<SceneNodeDisplacement> nodes,
        float scale = 1.0f,
        SceneResultContext? context = null,
        bool isVisible = true)
        : base(stableId, SceneLayerKind.Displacements, nodes, static value => new(SceneEntityKind.Displacement, value.NodeId), isVisible)
    {
        if (!float.IsFinite(scale) || scale < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Displacement scale must be finite and non-negative.");
        }

        Scale = scale;
        Context = context;
    }

    public float Scale { get; }

    public SceneResultContext? Context { get; }

    public IReadOnlyList<SceneNodeDisplacement> Nodes => Items;

    public override bool HasSameContent(IViewportSceneLayer other) =>
        other is SceneDisplacementLayer typed && base.HasSameContent(other) && Scale.Equals(typed.Scale) && Equals(Context, typed.Context);
}

public sealed class SceneReactionLayer : ViewportSceneLayer<SceneReaction>
{
    public SceneReactionLayer(
        string stableId,
        IEnumerable<SceneReaction> reactions,
        float scale = 1.0f,
        SceneResultContext? context = null,
        bool isVisible = true)
        : base(stableId, SceneLayerKind.Reactions, reactions, static value => new(SceneEntityKind.Reaction, value.Id), isVisible)
    {
        if (!float.IsFinite(scale) || scale < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        Scale = scale;
        Context = context;
    }

    public float Scale { get; }

    public SceneResultContext? Context { get; }

    public IReadOnlyList<SceneReaction> Reactions => Items;

    public override bool HasSameContent(IViewportSceneLayer other) =>
        other is SceneReactionLayer typed && base.HasSameContent(other) && Scale.Equals(typed.Scale) && Equals(Context, typed.Context);
}

public sealed class SceneSectionForceLayer : ViewportSceneLayer<SceneSectionForce>
{
    public SceneSectionForceLayer(
        string stableId,
        IEnumerable<SceneSectionForce> samples,
        float scale = 1.0f,
        SceneResultContext? context = null,
        bool isVisible = true)
        : base(stableId, SceneLayerKind.SectionForces, samples, static value => new(SceneEntityKind.SectionForce, value.Id), isVisible)
    {
        if (!float.IsFinite(scale) || scale < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        Scale = scale;
        Context = context;
    }

    public float Scale { get; }

    public SceneResultContext? Context { get; }

    public IReadOnlyList<SceneSectionForce> Samples => Items;

    public override bool HasSameContent(IViewportSceneLayer other) =>
        other is SceneSectionForceLayer typed && base.HasSameContent(other) && Scale.Equals(typed.Scale) && Equals(Context, typed.Context);
}

public sealed class ViewportSceneModel : IViewportScene
{
    public const int MaximumEntityCount = SceneCollectionMaterializer.MaximumEntityCount;
    private const SceneLayerMask NodeCoordinateDependencies =
        SceneLayerMask.Nodes |
        SceneLayerMask.Members |
        SceneLayerMask.RigidZones |
        SceneLayerMask.Supports |
        SceneLayerMask.Springs |
        SceneLayerMask.Joints |
        SceneLayerMask.Panels |
        SceneLayerMask.NoticePoints |
        SceneLayerMask.Loads |
        SceneLayerMask.Results |
        SceneLayerMask.Grid |
        SceneLayerMask.Labels;
    private const SceneLayerMask MemberTopologyDependencies =
        SceneLayerMask.Members |
        SceneLayerMask.RigidZones |
        SceneLayerMask.Springs |
        SceneLayerMask.Joints |
        SceneLayerMask.NoticePoints |
        SceneLayerMask.Loads |
        SceneLayerMask.Displacements |
        SceneLayerMask.SectionForces |
        SceneLayerMask.Labels;
    private readonly ReadOnlyCollection<IViewportSceneLayer> _layers;
    private readonly Dictionary<SceneLayerKind, IViewportSceneLayer> _layersByKind;
    private readonly HashSet<SceneEntityKey> _entityKeys;

    public ViewportSceneModel(
        string stableId,
        IEnumerable<SceneNode> nodes,
        IEnumerable<SceneMember>? members = null,
        IEnumerable<SceneSupport>? supports = null,
        IEnumerable<SceneNodalLoad>? nodalLoads = null,
        IEnumerable<SceneMemberLoad>? memberLoads = null,
        SceneDisplacementLayer? displacement = null,
        IEnumerable<SceneRigidZone>? rigidZones = null,
        IEnumerable<SceneSpring>? springs = null,
        IEnumerable<SceneJoint>? joints = null,
        IEnumerable<ScenePanel>? panels = null,
        IEnumerable<SceneNoticePoint>? noticePoints = null,
        IEnumerable<ScenePrescribedDisplacement>? prescribedDisplacements = null,
        SceneReactionLayer? reactions = null,
        SceneSectionForceLayer? sectionForces = null,
        ScenePresentationOptions? presentation = null,
        SceneLayerMask visibleLayers = SceneLayerMask.All)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentNullException.ThrowIfNull(nodes);
        if ((visibleLayers & ~SceneLayerMask.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleLayers));
        }

        SceneMaterializationBudget materializationBudget = new(MaximumEntityCount);
        materializationBudget.Reserve(displacement?.Count ?? 0, nameof(displacement));
        materializationBudget.Reserve(reactions?.Count ?? 0, nameof(reactions));
        materializationBudget.Reserve(sectionForces?.Count ?? 0, nameof(sectionForces));
        ReadOnlyCollection<SceneNode> materializedNodes = materializationBudget.Materialize(nodes, nameof(nodes));
        ReadOnlyCollection<SceneMember> materializedMembers = materializationBudget.Materialize(members ?? [], nameof(members));
        ReadOnlyCollection<SceneRigidZone> materializedRigidZones = materializationBudget.Materialize(rigidZones ?? [], nameof(rigidZones));
        ReadOnlyCollection<SceneSupport> materializedSupports = materializationBudget.Materialize(supports ?? [], nameof(supports));
        ReadOnlyCollection<SceneSpring> materializedSprings = materializationBudget.Materialize(springs ?? [], nameof(springs));
        ReadOnlyCollection<SceneJoint> materializedJoints = materializationBudget.Materialize(joints ?? [], nameof(joints));
        ReadOnlyCollection<ScenePanel> materializedPanels = materializationBudget.Materialize(panels ?? [], nameof(panels));
        ReadOnlyCollection<SceneNoticePoint> materializedNoticePoints = materializationBudget.Materialize(noticePoints ?? [], nameof(noticePoints));
        ReadOnlyCollection<SceneNodalLoad> materializedNodalLoads = materializationBudget.Materialize(nodalLoads ?? [], nameof(nodalLoads));
        ReadOnlyCollection<SceneMemberLoad> materializedMemberLoads = materializationBudget.Materialize(memberLoads ?? [], nameof(memberLoads));
        ReadOnlyCollection<ScenePrescribedDisplacement> materializedPrescribedDisplacements =
            materializationBudget.Materialize(prescribedDisplacements ?? [], nameof(prescribedDisplacements));

        StableId = stableId;
        VisibleLayers = visibleLayers;
        NodeLayer = new SceneNodeLayer($"{stableId}:nodes", materializedNodes, IsVisible(SceneLayerMask.Nodes));
        MemberLayer = new SceneMemberLayer($"{stableId}:members", materializedMembers, IsVisible(SceneLayerMask.Members));
        RigidZoneLayer = new SceneRigidZoneLayer($"{stableId}:rigid-zones", materializedRigidZones, IsVisible(SceneLayerMask.RigidZones));
        SupportLayer = new SceneSupportLayer($"{stableId}:supports", materializedSupports, IsVisible(SceneLayerMask.Supports));
        SpringLayer = new SceneSpringLayer($"{stableId}:springs", materializedSprings, IsVisible(SceneLayerMask.Springs));
        JointLayer = new SceneJointLayer($"{stableId}:joints", materializedJoints, IsVisible(SceneLayerMask.Joints));
        PanelLayer = new ScenePanelLayer($"{stableId}:panels", materializedPanels, IsVisible(SceneLayerMask.Panels));
        NoticePointLayer = new SceneNoticePointLayer($"{stableId}:notice-points", materializedNoticePoints, IsVisible(SceneLayerMask.NoticePoints));
        LoadLayer = new SceneLoadLayer(
            $"{stableId}:loads",
            materializedNodalLoads,
            materializedMemberLoads,
            materializedPrescribedDisplacements,
            IsVisible(SceneLayerMask.Loads));
        Displacement = displacement;
        Reactions = reactions;
        SectionForces = sectionForces;
        Presentation = presentation ?? ScenePresentationOptions.None;

        List<IViewportSceneLayer> layers = [NodeLayer, MemberLayer, RigidZoneLayer, SupportLayer, SpringLayer, JointLayer, PanelLayer, NoticePointLayer, LoadLayer];
        if (Displacement is not null) layers.Add(Displacement);
        if (Reactions is not null) layers.Add(Reactions);
        if (SectionForces is not null) layers.Add(SectionForces);
        _layers = layers.AsReadOnly();
        _layersByKind = layers.ToDictionary(layer => layer.Kind);
        _entityKeys = Validate();

        bool IsVisible(SceneLayerMask layer) => (visibleLayers & layer) != 0;
    }

    public string StableId { get; }
    public SceneNodeLayer NodeLayer { get; }
    public SceneMemberLayer MemberLayer { get; }
    public SceneRigidZoneLayer RigidZoneLayer { get; }
    public SceneSupportLayer SupportLayer { get; }
    public SceneSpringLayer SpringLayer { get; }
    public SceneJointLayer JointLayer { get; }
    public ScenePanelLayer PanelLayer { get; }
    public SceneNoticePointLayer NoticePointLayer { get; }
    public SceneLoadLayer LoadLayer { get; }
    public IReadOnlyList<SceneNode> Nodes => NodeLayer.Items;
    public IReadOnlyList<SceneMember> Members => MemberLayer.Items;
    public IReadOnlyList<SceneRigidZone> RigidZones => RigidZoneLayer.Items;
    public IReadOnlyList<SceneSupport> Supports => SupportLayer.Items;
    public IReadOnlyList<SceneSpring> Springs => SpringLayer.Items;
    public IReadOnlyList<SceneJoint> Joints => JointLayer.Items;
    public IReadOnlyList<ScenePanel> Panels => PanelLayer.Items;
    public IReadOnlyList<SceneNoticePoint> NoticePoints => NoticePointLayer.Items;
    public IReadOnlyList<SceneNodalLoad> NodalLoads => LoadLayer.NodalLoads;
    public IReadOnlyList<SceneMemberLoad> MemberLoads => LoadLayer.MemberLoads;
    public IReadOnlyList<ScenePrescribedDisplacement> PrescribedDisplacements => LoadLayer.PrescribedDisplacements;
    public SceneDisplacementLayer? Displacement { get; }
    public SceneReactionLayer? Reactions { get; }
    public SceneSectionForceLayer? SectionForces { get; }
    public ScenePresentationOptions Presentation { get; }
    public SceneLayerMask VisibleLayers { get; }
    public IReadOnlyList<IViewportSceneLayer> Layers => _layers;
    public bool Contains(SceneEntityKey key) => _entityKeys.Contains(key);
    public IViewportSceneLayer? GetLayer(SceneLayerKind kind) => _layersByKind.GetValueOrDefault(kind);

    public bool HasSameContent(ViewportSceneModel other) =>
        other is not null && GetChangedLayers(other) == SceneLayerMask.None && PresentationEquals(Presentation, other.Presentation);

    public SceneLayerMask GetChangedLayers(IViewportScene? previous)
    {
        if (previous is null || !string.Equals(StableId, previous.StableId, StringComparison.Ordinal))
        {
            return SceneLayerMask.All;
        }

        SceneLayerMask changed = SceneLayerMask.None;
        SceneLayerMask dependencySources = SceneLayerMask.None;
        if (VisibleLayers != previous.VisibleLayers)
        {
            changed |= VisibleLayers ^ previous.VisibleLayers;
        }
        foreach (SceneLayerKind kind in Enum.GetValues<SceneLayerKind>())
        {
            IViewportSceneLayer? current = GetLayer(kind);
            IViewportSceneLayer? old = previous.GetLayer(kind);
            if (current is null ? old is not null : old is null || !current.HasSameContent(old))
            {
                changed |= kind.ToMask();
                if (HasDependencyContentChanged(kind, current, old))
                {
                    dependencySources |= kind.ToMask();
                }
            }
        }

        if ((dependencySources & SceneLayerMask.Nodes) != 0)
        {
            changed |= NodeCoordinateDependencies;
        }

        if ((dependencySources & SceneLayerMask.Members) != 0)
        {
            changed |= MemberTopologyDependencies;
        }

        if (previous is ViewportSceneModel typed && !PresentationEquals(Presentation, typed.Presentation))
        {
            changed |= GetPresentationChangedLayers(Presentation, typed.Presentation);
        }

        return changed;
    }

    private static bool HasDependencyContentChanged(
        SceneLayerKind kind,
        IViewportSceneLayer? current,
        IViewportSceneLayer? previous) => kind switch
        {
            SceneLayerKind.Nodes =>
                current is not SceneNodeLayer currentNodes ||
                previous is not SceneNodeLayer previousNodes ||
                !string.Equals(currentNodes.StableId, previousNodes.StableId, StringComparison.Ordinal) ||
                !currentNodes.Items.SequenceEqual(previousNodes.Items),
            SceneLayerKind.Members =>
                current is not SceneMemberLayer currentMembers ||
                previous is not SceneMemberLayer previousMembers ||
                !string.Equals(currentMembers.StableId, previousMembers.StableId, StringComparison.Ordinal) ||
                !currentMembers.Items.SequenceEqual(previousMembers.Items),
            _ => false,
        };

    private static SceneLayerMask GetPresentationChangedLayers(
        ScenePresentationOptions current,
        ScenePresentationOptions previous)
    {
        SceneLayerMask changed = SceneLayerMask.None;
        if (!Equals(current.Grid, previous.Grid)) changed |= SceneLayerMask.Grid;
        if (current.ShowAxes != previous.ShowAxes) changed |= SceneLayerMask.Axes;
        if (!current.Labels.SequenceEqual(previous.Labels)) changed |= SceneLayerMask.Labels;
        if (!Equals(current.ScaleLegend, previous.ScaleLegend)) changed |= SceneLayerMask.ScaleLegend;
        if (!ColorLegendEquals(current.ColorLegend, previous.ColorLegend)) changed |= SceneLayerMask.ColorLegend;
        return changed;
    }

    private HashSet<SceneEntityKey> Validate()
    {
        int entityCount = _layers.Aggregate(0, static (count, layer) => checked(count + layer.Count));
        if (entityCount == 0 || entityCount > MaximumEntityCount)
        {
            throw new ArgumentOutOfRangeException(nameof(Nodes), $"A scene must contain 1 to {MaximumEntityCount} entities.");
        }

        Dictionary<string, SceneNode> nodes = UniqueById(Nodes, static value => value.Id, nameof(Nodes));
        Dictionary<string, SceneMember> members = UniqueById(Members, static value => value.Id, nameof(Members));
        _ = UniqueById(RigidZones, static value => value.Id, nameof(RigidZones));
        _ = UniqueById(Supports, static value => value.Id, nameof(Supports));
        _ = UniqueById(Springs, static value => value.Id, nameof(Springs));
        _ = UniqueById(Joints, static value => value.Id, nameof(Joints));
        _ = UniqueById(Panels, static value => value.Id, nameof(Panels));
        _ = UniqueById(NoticePoints, static value => value.Id, nameof(NoticePoints));
        _ = UniqueById(NodalLoads, static value => value.Id, nameof(NodalLoads));
        _ = UniqueById(MemberLoads, static value => value.Id, nameof(MemberLoads));
        _ = UniqueById(PrescribedDisplacements, static value => value.Id, nameof(PrescribedDisplacements));

        foreach (SceneMember value in Members)
        {
            RequireNode(nodes, value.StartNodeId, $"Member '{value.Id}' start");
            RequireNode(nodes, value.EndNodeId, $"Member '{value.Id}' end");
            if (string.Equals(value.StartNodeId, value.EndNodeId, StringComparison.Ordinal))
                throw new ArgumentException($"Member '{value.Id}' must reference two different nodes.", nameof(Members));
        }

        foreach (SceneRigidZone value in RigidZones)
        {
            RequireMember(members, value.MemberId, $"Rigid zone '{value.Id}'");
            RequireNonNegative(value.StartLength, nameof(RigidZones));
            RequireNonNegative(value.EndLength, nameof(RigidZones));
        }

        foreach (SceneSupport value in Supports)
        {
            RequireNode(nodes, value.NodeId, $"Support '{value.Id}'");
            if (!value.FixX && !value.FixY && !value.FixZ && !value.FixRx && !value.FixRy && !value.FixRz)
                throw new ArgumentException($"Support '{value.Id}' must constrain at least one translation or rotation.", nameof(Supports));
        }

        foreach (SceneSpring value in Springs)
        {
            RequireNode(nodes, value.StartNodeId, $"Spring '{value.Id}' start");
            RequireNode(nodes, value.EndNodeId, $"Spring '{value.Id}' end");
            if (string.Equals(value.StartNodeId, value.EndNodeId, StringComparison.Ordinal))
                throw new ArgumentException($"Spring '{value.Id}' must reference different nodes.", nameof(Springs));
        }

        foreach (SceneJoint value in Joints)
        {
            RequireNode(nodes, value.NodeId, $"Joint '{value.Id}'");
            if (!value.ReleaseX && !value.ReleaseY && !value.ReleaseZ && !value.ReleaseRx && !value.ReleaseRy && !value.ReleaseRz)
                throw new ArgumentException($"Joint '{value.Id}' must release at least one degree of freedom.", nameof(Joints));
        }

        foreach (ScenePanel value in Panels)
            foreach (string nodeId in value.NodeIds)
                RequireNode(nodes, nodeId, $"Panel '{value.Id}'");

        foreach (SceneNoticePoint value in NoticePoints)
        {
            RequireMember(members, value.MemberId, $"Notice point '{value.Id}'");
            RequireRelativePosition(value.RelativePosition, nameof(NoticePoints));
        }

        foreach (SceneNodalLoad value in NodalLoads) RequireNode(nodes, value.NodeId, $"Nodal load '{value.Id}'");
        foreach (ScenePrescribedDisplacement value in PrescribedDisplacements) RequireNode(nodes, value.NodeId, $"Prescribed displacement '{value.Id}'");

        foreach (SceneMemberLoad value in MemberLoads)
        {
            RequireMember(members, value.MemberId, $"Member load '{value.Id}'");
            RequireRelativePosition(value.StartRelativePosition, nameof(MemberLoads));
            RequireRelativePosition(value.EndRelativePosition, nameof(MemberLoads));
            if (value.Kind == SceneMemberLoadKind.Distributed && value.EndRelativePosition < value.StartRelativePosition)
                throw new ArgumentException($"Member load '{value.Id}' end position precedes its start.", nameof(MemberLoads));
            if (!float.IsFinite(value.TemperatureTop) || !float.IsFinite(value.TemperatureBottom))
                throw new ArgumentOutOfRangeException(nameof(MemberLoads));
        }

        if (Displacement is not null)
            foreach (SceneNodeDisplacement value in Displacement.Nodes) RequireNode(nodes, value.NodeId, "Displacement");
        if (Reactions is not null)
            foreach (SceneReaction value in Reactions.Reactions) RequireNode(nodes, value.NodeId, $"Reaction '{value.Id}'");
        if (SectionForces is not null)
            foreach (SceneSectionForce value in SectionForces.Samples)
            {
                RequireMember(members, value.MemberId, $"Section force '{value.Id}'");
                RequireRelativePosition(value.RelativePosition, nameof(SectionForces));
            }

        HashSet<SceneEntityKey> keys = [];
        foreach (SceneEntityKey key in _layers.SelectMany(layer => layer.EntityKeys))
            if (!keys.Add(key)) throw new ArgumentException($"Duplicate stable entity key '{key.Kind}:{key.Id}'.");
        return keys;
    }

    private static Dictionary<string, T> UniqueById<T>(IEnumerable<T> values, Func<T, string> id, string parameterName)
    {
        Dictionary<string, T> result = new(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string stableId = id(value);
            ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
            if (!result.TryAdd(stableId, value)) throw new ArgumentException($"Duplicate stable ID '{stableId}' in {parameterName}.", parameterName);
        }

        return result;
    }

    private static void RequireNode(IReadOnlyDictionary<string, SceneNode> nodes, string nodeId, string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (!nodes.ContainsKey(nodeId)) throw new ArgumentException($"{owner} references unknown node '{nodeId}'.");
    }

    private static void RequireMember(IReadOnlyDictionary<string, SceneMember> members, string memberId, string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        if (!members.ContainsKey(memberId)) throw new ArgumentException($"{owner} references unknown member '{memberId}'.");
    }

    private static void RequireRelativePosition(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0.0f || value > 1.0f)
            throw new ArgumentOutOfRangeException(parameterName, "Relative position must be between 0 and 1.");
    }

    private static void RequireNonNegative(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0.0f) throw new ArgumentOutOfRangeException(parameterName);
    }

    private static bool PresentationEquals(ScenePresentationOptions left, ScenePresentationOptions right) =>
        Equals(left.Grid, right.Grid) && left.ShowAxes == right.ShowAxes && left.Labels.SequenceEqual(right.Labels) &&
        Equals(left.ScaleLegend, right.ScaleLegend) && ColorLegendEquals(left.ColorLegend, right.ColorLegend);

    private static bool ColorLegendEquals(SceneColorLegend? left, SceneColorLegend? right) =>
        ReferenceEquals(left, right) || (left is not null && right is not null &&
        string.Equals(left.StableId, right.StableId, StringComparison.Ordinal) &&
        string.Equals(left.Caption, right.Caption, StringComparison.Ordinal) && left.Entries.SequenceEqual(right.Entries));
}

internal static class SceneCollectionMaterializer
{
    public const int MaximumEntityCount = 250_000;

    public static ReadOnlyCollection<T> Materialize<T>(
        IEnumerable<T> source,
        string parameterName,
        int maximumCount = MaximumEntityCount)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maximumCount <= 0 || maximumCount > MaximumEntityCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        List<T> values = [];
        using IEnumerator<T> enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (values.Count == maximumCount)
                throw new ArgumentOutOfRangeException(parameterName, $"A scene collection cannot contain more than {maximumCount} entries.");
            values.Add(enumerator.Current);
        }

        return values.AsReadOnly();
    }
}

internal sealed class SceneMaterializationBudget
{
    private readonly int _maximumCount;
    private int _count;

    public SceneMaterializationBudget(int maximumCount)
    {
        if (maximumCount <= 0 || maximumCount > SceneCollectionMaterializer.MaximumEntityCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        _maximumCount = maximumCount;
    }

    public void Reserve(int count, string parameterName)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        long nextCount = checked((long)_count + count);
        if (nextCount > _maximumCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"A scene cannot contain more than {_maximumCount} entities across all layers.");
        }

        _count = (int)nextCount;
    }

    public ReadOnlyCollection<T> Materialize<T>(IEnumerable<T> source, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<T> values = [];
        using IEnumerator<T> enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            Reserve(1, parameterName);
            values.Add(enumerator.Current);
        }

        return values.AsReadOnly();
    }
}
