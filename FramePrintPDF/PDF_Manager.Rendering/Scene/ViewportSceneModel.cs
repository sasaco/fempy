using System.Collections.ObjectModel;

namespace PDF_Manager.Rendering.Scene;

public enum SceneEntityKind
{
    Node,
    Member,
    Support,
    NodalLoad,
    MemberLoad,
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

public sealed record SceneSupport(
    string Id,
    string NodeId,
    bool FixX,
    bool FixY,
    bool FixZ,
    bool FixRx = false,
    bool FixRy = false,
    bool FixRz = false);

public sealed record SceneNodalLoad(
    string Id,
    string NodeId,
    ScenePoint3 Vector,
    ScenePoint3 Moment = default);

public sealed record SceneMemberLoad(string Id, string MemberId, float RelativePosition, ScenePoint3 Vector);

public sealed record SceneNodeDisplacement(string NodeId, ScenePoint3 Vector);

public sealed class SceneDisplacementLayer
{
    private readonly ReadOnlyCollection<SceneNodeDisplacement> _nodes;

    public SceneDisplacementLayer(
        string stableId,
        IEnumerable<SceneNodeDisplacement> nodes,
        float scale = 1.0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentNullException.ThrowIfNull(nodes);
        if (!float.IsFinite(scale) || scale < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Displacement scale must be finite and non-negative.");
        }

        StableId = stableId;
        Scale = scale;
        _nodes = SceneCollectionMaterializer.Materialize(nodes, nameof(nodes));
    }

    public string StableId { get; }

    public float Scale { get; }

    public IReadOnlyList<SceneNodeDisplacement> Nodes => _nodes;
}

public sealed class ViewportSceneModel
{
    private const int MaximumEntityCount = SceneCollectionMaterializer.MaximumEntityCount;
    private readonly ReadOnlyCollection<SceneNode> _nodes;
    private readonly ReadOnlyCollection<SceneMember> _members;
    private readonly ReadOnlyCollection<SceneSupport> _supports;
    private readonly ReadOnlyCollection<SceneNodalLoad> _nodalLoads;
    private readonly ReadOnlyCollection<SceneMemberLoad> _memberLoads;
    private readonly HashSet<SceneEntityKey> _entityKeys;

    public ViewportSceneModel(
        string stableId,
        IEnumerable<SceneNode> nodes,
        IEnumerable<SceneMember>? members = null,
        IEnumerable<SceneSupport>? supports = null,
        IEnumerable<SceneNodalLoad>? nodalLoads = null,
        IEnumerable<SceneMemberLoad>? memberLoads = null,
        SceneDisplacementLayer? displacement = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentNullException.ThrowIfNull(nodes);

        StableId = stableId;
        _nodes = SceneCollectionMaterializer.Materialize(nodes, nameof(nodes));
        _members = SceneCollectionMaterializer.Materialize(members ?? [], nameof(members));
        _supports = SceneCollectionMaterializer.Materialize(supports ?? [], nameof(supports));
        _nodalLoads = SceneCollectionMaterializer.Materialize(nodalLoads ?? [], nameof(nodalLoads));
        _memberLoads = SceneCollectionMaterializer.Materialize(memberLoads ?? [], nameof(memberLoads));
        Displacement = displacement;
        _entityKeys = Validate();
    }

    public string StableId { get; }

    public IReadOnlyList<SceneNode> Nodes => _nodes;

    public IReadOnlyList<SceneMember> Members => _members;

    public IReadOnlyList<SceneSupport> Supports => _supports;

    public IReadOnlyList<SceneNodalLoad> NodalLoads => _nodalLoads;

    public IReadOnlyList<SceneMemberLoad> MemberLoads => _memberLoads;

    public SceneDisplacementLayer? Displacement { get; }

    public bool Contains(SceneEntityKey key) => _entityKeys.Contains(key);

    public bool HasSameContent(ViewportSceneModel other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(StableId, other.StableId, StringComparison.Ordinal) &&
            _nodes.SequenceEqual(other._nodes) &&
            _members.SequenceEqual(other._members) &&
            _supports.SequenceEqual(other._supports) &&
            _nodalLoads.SequenceEqual(other._nodalLoads) &&
            _memberLoads.SequenceEqual(other._memberLoads) &&
            DisplacementsEqual(Displacement, other.Displacement);
    }

    private HashSet<SceneEntityKey> Validate()
    {
        int entityCount = checked(_nodes.Count + _members.Count + _supports.Count + _nodalLoads.Count + _memberLoads.Count);
        if (entityCount == 0 || entityCount > MaximumEntityCount)
        {
            throw new ArgumentOutOfRangeException(nameof(Nodes), $"A scene must contain 1 to {MaximumEntityCount} entities.");
        }

        Dictionary<string, SceneNode> nodes = UniqueById(_nodes, static node => node.Id, nameof(Nodes));
        Dictionary<string, SceneMember> members = UniqueById(_members, static member => member.Id, nameof(Members));
        _ = UniqueById(_supports, static support => support.Id, nameof(Supports));
        _ = UniqueById(_nodalLoads, static load => load.Id, nameof(NodalLoads));
        _ = UniqueById(_memberLoads, static load => load.Id, nameof(MemberLoads));

        foreach (SceneMember member in _members)
        {
            RequireNode(nodes, member.StartNodeId, $"Member '{member.Id}' start");
            RequireNode(nodes, member.EndNodeId, $"Member '{member.Id}' end");
            if (string.Equals(member.StartNodeId, member.EndNodeId, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Member '{member.Id}' must reference two different nodes.", nameof(Members));
            }
        }

        foreach (SceneSupport support in _supports)
        {
            RequireNode(nodes, support.NodeId, $"Support '{support.Id}'");
            if (!support.FixX && !support.FixY && !support.FixZ &&
                !support.FixRx && !support.FixRy && !support.FixRz)
            {
                throw new ArgumentException(
                    $"Support '{support.Id}' must constrain at least one translation or rotation.",
                    nameof(Supports));
            }
        }

        foreach (SceneNodalLoad load in _nodalLoads)
        {
            RequireNode(nodes, load.NodeId, $"Nodal load '{load.Id}'");
        }

        foreach (SceneMemberLoad load in _memberLoads)
        {
            if (!members.ContainsKey(load.MemberId))
            {
                throw new ArgumentException($"Member load '{load.Id}' references unknown member '{load.MemberId}'.", nameof(MemberLoads));
            }

            if (!float.IsFinite(load.RelativePosition) || load.RelativePosition < 0.0f || load.RelativePosition > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(MemberLoads), $"Member load '{load.Id}' position must be between 0 and 1.");
            }
        }

        if (Displacement is not null)
        {
            Dictionary<string, SceneNodeDisplacement> displacements =
                UniqueById(Displacement.Nodes, static displacement => displacement.NodeId, nameof(Displacement));
            foreach (string nodeId in displacements.Keys)
            {
                RequireNode(nodes, nodeId, "Displacement");
            }
        }

        HashSet<SceneEntityKey> keys = [];
        AddKeys(keys, _nodes.Select(node => new SceneEntityKey(SceneEntityKind.Node, node.Id)));
        AddKeys(keys, _members.Select(member => new SceneEntityKey(SceneEntityKind.Member, member.Id)));
        AddKeys(keys, _supports.Select(support => new SceneEntityKey(SceneEntityKind.Support, support.Id)));
        AddKeys(keys, _nodalLoads.Select(load => new SceneEntityKey(SceneEntityKind.NodalLoad, load.Id)));
        AddKeys(keys, _memberLoads.Select(load => new SceneEntityKey(SceneEntityKind.MemberLoad, load.Id)));
        return keys;
    }

    private static Dictionary<string, T> UniqueById<T>(IEnumerable<T> values, Func<T, string> id, string parameterName)
    {
        Dictionary<string, T> result = new(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string stableId = id(value);
            ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
            if (!result.TryAdd(stableId, value))
            {
                throw new ArgumentException($"Duplicate stable ID '{stableId}' in {parameterName}.", parameterName);
            }
        }

        return result;
    }

    private static void RequireNode(IReadOnlyDictionary<string, SceneNode> nodes, string nodeId, string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        if (!nodes.ContainsKey(nodeId))
        {
            throw new ArgumentException($"{owner} references unknown node '{nodeId}'.");
        }
    }

    private static void AddKeys(HashSet<SceneEntityKey> target, IEnumerable<SceneEntityKey> keys)
    {
        foreach (SceneEntityKey key in keys)
        {
            _ = target.Add(key);
        }
    }

    private static bool DisplacementsEqual(SceneDisplacementLayer? left, SceneDisplacementLayer? right) =>
        ReferenceEquals(left, right) ||
        (left is not null && right is not null &&
         string.Equals(left.StableId, right.StableId, StringComparison.Ordinal) &&
         left.Scale.Equals(right.Scale) &&
         left.Nodes.SequenceEqual(right.Nodes));
}

internal static class SceneCollectionMaterializer
{
    public const int MaximumEntityCount = 250_000;

    public static ReadOnlyCollection<T> Materialize<T>(IEnumerable<T> source, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<T> values = [];
        using IEnumerator<T> enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (values.Count == MaximumEntityCount)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"A scene collection cannot contain more than {MaximumEntityCount} entries.");
            }

            values.Add(enumerator.Current);
        }

        return values.AsReadOnly();
    }
}
