using System.Collections.ObjectModel;

namespace FrameWebforCS.Rendering.Scene;

public enum SceneLayerKind
{
    Grid,
    Axes,
    Labels,
    Nodes,
    Members,
    RigidZones,
    Supports,
    Springs,
    Joints,
    Panels,
    NoticePoints,
    Loads,
    Displacements,
    Reactions,
    SectionForces,
    ScaleLegend,
    ColorLegend,
}

[Flags]
public enum SceneLayerMask : ulong
{
    None = 0,
    Grid = 1UL << 0,
    Axes = 1UL << 1,
    Labels = 1UL << 2,
    Nodes = 1UL << 3,
    Members = 1UL << 4,
    RigidZones = 1UL << 5,
    Supports = 1UL << 6,
    Springs = 1UL << 7,
    Joints = 1UL << 8,
    Panels = 1UL << 9,
    NoticePoints = 1UL << 10,
    Loads = 1UL << 11,
    Displacements = 1UL << 12,
    Reactions = 1UL << 13,
    SectionForces = 1UL << 14,
    ScaleLegend = 1UL << 15,
    ColorLegend = 1UL << 16,
    Geometry = Nodes | Members | RigidZones | Supports | Springs | Joints | Panels | NoticePoints,
    Results = Displacements | Reactions | SectionForces,
    Decorations = Grid | Axes | Labels | ScaleLegend | ColorLegend,
    All = Geometry | Loads | Results | Decorations,
}

public static class SceneLayerKinds
{
    public static SceneLayerMask ToMask(this SceneLayerKind kind) => kind switch
    {
        SceneLayerKind.Grid => SceneLayerMask.Grid,
        SceneLayerKind.Axes => SceneLayerMask.Axes,
        SceneLayerKind.Labels => SceneLayerMask.Labels,
        SceneLayerKind.Nodes => SceneLayerMask.Nodes,
        SceneLayerKind.Members => SceneLayerMask.Members,
        SceneLayerKind.RigidZones => SceneLayerMask.RigidZones,
        SceneLayerKind.Supports => SceneLayerMask.Supports,
        SceneLayerKind.Springs => SceneLayerMask.Springs,
        SceneLayerKind.Joints => SceneLayerMask.Joints,
        SceneLayerKind.Panels => SceneLayerMask.Panels,
        SceneLayerKind.NoticePoints => SceneLayerMask.NoticePoints,
        SceneLayerKind.Loads => SceneLayerMask.Loads,
        SceneLayerKind.Displacements => SceneLayerMask.Displacements,
        SceneLayerKind.Reactions => SceneLayerMask.Reactions,
        SceneLayerKind.SectionForces => SceneLayerMask.SectionForces,
        SceneLayerKind.ScaleLegend => SceneLayerMask.ScaleLegend,
        SceneLayerKind.ColorLegend => SceneLayerMask.ColorLegend,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

public static class SceneEntityKinds
{
    public static SceneLayerMask ToLayerMask(this SceneEntityKind kind) => kind switch
    {
        SceneEntityKind.Node => SceneLayerMask.Nodes,
        SceneEntityKind.Member => SceneLayerMask.Members,
        SceneEntityKind.RigidZone => SceneLayerMask.RigidZones,
        SceneEntityKind.Support => SceneLayerMask.Supports,
        SceneEntityKind.Spring => SceneLayerMask.Springs,
        SceneEntityKind.Joint => SceneLayerMask.Joints,
        SceneEntityKind.Panel => SceneLayerMask.Panels,
        SceneEntityKind.NoticePoint => SceneLayerMask.NoticePoints,
        SceneEntityKind.NodalLoad or SceneEntityKind.MemberLoad or SceneEntityKind.PrescribedDisplacement => SceneLayerMask.Loads,
        SceneEntityKind.Displacement => SceneLayerMask.Displacements,
        SceneEntityKind.Reaction => SceneLayerMask.Reactions,
        SceneEntityKind.SectionForce => SceneLayerMask.SectionForces,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

public interface IViewportSceneLayer
{
    string StableId { get; }

    SceneLayerKind Kind { get; }

    bool IsVisible { get; }

    int Count { get; }

    IReadOnlyList<SceneEntityKey> EntityKeys { get; }

    bool HasSameContent(IViewportSceneLayer other);
}

public interface IViewportScene
{
    string StableId { get; }

    IReadOnlyList<IViewportSceneLayer> Layers { get; }

    SceneLayerMask VisibleLayers { get; }

    IReadOnlyList<SceneNode> Nodes { get; }

    IReadOnlyList<SceneMember> Members { get; }

    IReadOnlyList<SceneRigidZone> RigidZones { get; }

    IReadOnlyList<SceneSupport> Supports { get; }

    IReadOnlyList<SceneSpring> Springs { get; }

    IReadOnlyList<SceneJoint> Joints { get; }

    IReadOnlyList<ScenePanel> Panels { get; }

    IReadOnlyList<SceneNoticePoint> NoticePoints { get; }

    IReadOnlyList<SceneNodalLoad> NodalLoads { get; }

    IReadOnlyList<SceneMemberLoad> MemberLoads { get; }

    IReadOnlyList<ScenePrescribedDisplacement> PrescribedDisplacements { get; }

    SceneDisplacementLayer? Displacement { get; }

    SceneReactionLayer? Reactions { get; }

    SceneSectionForceLayer? SectionForces { get; }

    ScenePresentationOptions Presentation { get; }

    bool Contains(SceneEntityKey key);

    IViewportSceneLayer? GetLayer(SceneLayerKind kind);

    SceneLayerMask GetChangedLayers(IViewportScene? previous);
}

public abstract class ViewportSceneLayer<T> : IViewportSceneLayer
{
    private readonly ReadOnlyCollection<T> _items;
    private readonly ReadOnlyCollection<SceneEntityKey> _entityKeys;

    protected ViewportSceneLayer(
        string stableId,
        SceneLayerKind kind,
        IEnumerable<T> items,
        Func<T, SceneEntityKey> keySelector,
        bool isVisible = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keySelector);
        StableId = stableId;
        Kind = kind;
        IsVisible = isVisible;
        _items = SceneCollectionMaterializer.Materialize(items, nameof(items));
        _entityKeys = SceneCollectionMaterializer.Materialize(_items.Select(keySelector), nameof(items));

        HashSet<SceneEntityKey> unique = [];
        foreach (SceneEntityKey key in _entityKeys)
        {
            if (!unique.Add(key))
            {
                throw new ArgumentException($"Duplicate stable entity key '{key.Kind}:{key.Id}' in layer '{stableId}'.", nameof(items));
            }
        }
    }

    public string StableId { get; }

    public SceneLayerKind Kind { get; }

    public bool IsVisible { get; }

    public int Count => _items.Count;

    public IReadOnlyList<T> Items => _items;

    public IReadOnlyList<SceneEntityKey> EntityKeys => _entityKeys;

    public virtual bool HasSameContent(IViewportSceneLayer other) =>
        other is ViewportSceneLayer<T> typed &&
        string.Equals(StableId, typed.StableId, StringComparison.Ordinal) &&
        Kind == typed.Kind &&
        IsVisible == typed.IsVisible &&
        _items.SequenceEqual(typed._items);
}

public sealed class SceneInvalidationCoalescer
{
    private readonly object _sync = new();
    private SceneLayerMask _pending;
    private long _requestsReceived;
    private long _batchesConsumed;

    public long RequestsReceived => Interlocked.Read(ref _requestsReceived);

    public long BatchesConsumed => Interlocked.Read(ref _batchesConsumed);

    public SceneLayerMask Pending
    {
        get
        {
            lock (_sync)
            {
                return _pending;
            }
        }
    }

    public bool Invalidate(SceneLayerMask layers)
    {
        Validate(layers);
        if (layers == SceneLayerMask.None)
        {
            return false;
        }

        lock (_sync)
        {
            bool becamePending = _pending == SceneLayerMask.None;
            _pending |= layers;
            Interlocked.Increment(ref _requestsReceived);
            return becamePending;
        }
    }

    public SceneLayerMask Consume()
    {
        lock (_sync)
        {
            SceneLayerMask pending = _pending;
            _pending = SceneLayerMask.None;
            if (pending != SceneLayerMask.None)
            {
                Interlocked.Increment(ref _batchesConsumed);
            }

            return pending;
        }
    }

    private static void Validate(SceneLayerMask layers)
    {
        if ((layers & ~SceneLayerMask.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layers));
        }
    }
}

public enum SceneResultStateKind
{
    Static,
    LoadStep,
    Mode,
}

public enum SceneExtremaMode
{
    Values,
    Minimum,
    Maximum,
    AbsoluteMaximum,
}

public sealed record SceneResultContext
{
    public SceneResultContext(
        string caseId,
        SceneResultStateKind stateKind = SceneResultStateKind.Static,
        int stateIndex = 0,
        int pageIndex = 0,
        int pageCount = 1,
        SceneExtremaMode extrema = SceneExtremaMode.Values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        if (!Enum.IsDefined(stateKind))
        {
            throw new ArgumentOutOfRangeException(nameof(stateKind));
        }

        if (!Enum.IsDefined(extrema))
        {
            throw new ArgumentOutOfRangeException(nameof(extrema));
        }

        if (stateIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stateIndex));
        }

        if (pageCount <= 0 || pageIndex < 0 || pageIndex >= pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Result page must be inside the declared page count.");
        }

        CaseId = caseId;
        StateKind = stateKind;
        StateIndex = stateIndex;
        PageIndex = pageIndex;
        PageCount = pageCount;
        Extrema = extrema;
    }

    public string CaseId { get; }

    public SceneResultStateKind StateKind { get; }

    public int StateIndex { get; }

    public int PageIndex { get; }

    public int PageCount { get; }

    public SceneExtremaMode Extrema { get; }
}

public enum SceneGridPlane
{
    XY,
    XZ,
    YZ,
}

public sealed record SceneGridDefinition
{
    public SceneGridDefinition(SceneGridPlane plane, float majorSpacing, int minorDivisions = 1, bool isVisible = true)
    {
        if (!Enum.IsDefined(plane))
        {
            throw new ArgumentOutOfRangeException(nameof(plane));
        }

        if (!float.IsFinite(majorSpacing) || majorSpacing <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(majorSpacing));
        }

        if (minorDivisions is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minorDivisions));
        }

        Plane = plane;
        MajorSpacing = majorSpacing;
        MinorDivisions = minorDivisions;
        IsVisible = isVisible;
    }

    public SceneGridPlane Plane { get; }

    public float MajorSpacing { get; }

    public int MinorDivisions { get; }

    public bool IsVisible { get; }
}

public sealed record SceneLabel(string StableId, string Text, ScenePoint3 Position, SceneEntityKey? Entity = null)
{
    public string StableId { get; } = string.IsNullOrWhiteSpace(StableId)
        ? throw new ArgumentException("A label stable ID is required.", nameof(StableId))
        : StableId;

    public string Text { get; } = Text ?? throw new ArgumentNullException(nameof(Text));
}

public sealed record SceneScaleLegend(string StableId, string Caption, float Scale)
{
    public string StableId { get; } = string.IsNullOrWhiteSpace(StableId)
        ? throw new ArgumentException("A scale legend stable ID is required.", nameof(StableId))
        : StableId;

    public string Caption { get; } = Caption ?? throw new ArgumentNullException(nameof(Caption));

    public float Scale { get; } = float.IsFinite(Scale) && Scale > 0.0f
        ? Scale
        : throw new ArgumentOutOfRangeException(nameof(Scale));
}

public sealed record SceneColorLegendEntry(string Label, float Value, float Red, float Green, float Blue);

public sealed class SceneColorLegend
{
    private readonly ReadOnlyCollection<SceneColorLegendEntry> _entries;

    public SceneColorLegend(string stableId, string caption, IEnumerable<SceneColorLegendEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentNullException.ThrowIfNull(caption);
        StableId = stableId;
        Caption = caption;
        _entries = SceneCollectionMaterializer.Materialize(entries, nameof(entries), 64);
        if (_entries.Count is < 2 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(entries), "A color legend requires 2 to 64 entries.");
        }

        foreach (SceneColorLegendEntry entry in _entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Label);
            if (!float.IsFinite(entry.Value) ||
                !IsColor(entry.Red) || !IsColor(entry.Green) || !IsColor(entry.Blue))
            {
                throw new ArgumentOutOfRangeException(nameof(entries));
            }
        }
    }

    public string StableId { get; }

    public string Caption { get; }

    public IReadOnlyList<SceneColorLegendEntry> Entries => _entries;

    private static bool IsColor(float value) => float.IsFinite(value) && value is >= 0.0f and <= 1.0f;
}

public sealed class ScenePresentationOptions
{
    private readonly ReadOnlyCollection<SceneLabel> _labels;

    public ScenePresentationOptions(
        SceneGridDefinition? grid = null,
        bool showAxes = false,
        IEnumerable<SceneLabel>? labels = null,
        SceneScaleLegend? scaleLegend = null,
        SceneColorLegend? colorLegend = null)
    {
        Grid = grid;
        ShowAxes = showAxes;
        _labels = SceneCollectionMaterializer.Materialize(labels ?? [], nameof(labels));
        ScaleLegend = scaleLegend;
        ColorLegend = colorLegend;
    }

    public static ScenePresentationOptions None { get; } = new();

    public SceneGridDefinition? Grid { get; }

    public bool ShowAxes { get; }

    public IReadOnlyList<SceneLabel> Labels => _labels;

    public SceneScaleLegend? ScaleLegend { get; }

    public SceneColorLegend? ColorLegend { get; }
}
