using System.Collections.ObjectModel;
using System.Numerics;

namespace FrameWebforCsharp.Rendering.Scene;

public enum ScenePrimitive
{
    Points,
    Lines,
    Triangles,
}

public readonly record struct SceneRenderVertex(float X, float Y, float Red, float Green, float Blue);

public readonly record struct SceneDrawBatch(ScenePrimitive Primitive, int FirstVertex, int VertexCount, float Width);

public sealed class SceneHitTarget
{
    private readonly ReadOnlyCollection<ScenePoint2> _points;

    internal SceneHitTarget(SceneEntityKey key, ScenePrimitive primitive, IEnumerable<ScenePoint2> points)
    {
        Key = key;
        Primitive = primitive;
        _points = Array.AsReadOnly(points.ToArray());
    }

    public SceneEntityKey Key { get; }

    public ScenePrimitive Primitive { get; }

    public SceneLayerKind Layer => LayerFor(Key.Kind);

    public IReadOnlyList<ScenePoint2> Points => _points;

    private static SceneLayerKind LayerFor(SceneEntityKind kind) => kind switch
    {
        SceneEntityKind.Node => SceneLayerKind.Nodes,
        SceneEntityKind.Member => SceneLayerKind.Members,
        SceneEntityKind.RigidZone => SceneLayerKind.RigidZones,
        SceneEntityKind.Support => SceneLayerKind.Supports,
        SceneEntityKind.Spring => SceneLayerKind.Springs,
        SceneEntityKind.Joint => SceneLayerKind.Joints,
        SceneEntityKind.Panel => SceneLayerKind.Panels,
        SceneEntityKind.NoticePoint => SceneLayerKind.NoticePoints,
        SceneEntityKind.NodalLoad or SceneEntityKind.MemberLoad or SceneEntityKind.PrescribedDisplacement => SceneLayerKind.Loads,
        SceneEntityKind.Displacement => SceneLayerKind.Displacements,
        SceneEntityKind.Reaction => SceneLayerKind.Reactions,
        SceneEntityKind.SectionForce => SceneLayerKind.SectionForces,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

public readonly record struct ScenePoint2(float X, float Y);

public readonly record struct SceneGridCommand(SceneGridPlane Plane, float MajorSpacing, int MinorDivisions);

public readonly record struct SceneAxisCommand(string Axis, ScenePoint2 Start, ScenePoint2 End);

public readonly record struct SceneLabelCommand(string StableId, string Text, ScenePoint2 Position, SceneEntityKey? Entity);

public readonly record struct SceneScaleCommand(string StableId, string Caption, float Scale);

public sealed record SceneColorLegendCommand(
    string StableId,
    string Caption,
    IReadOnlyList<SceneColorLegendEntry> Entries);

public sealed class ViewportSceneLayerCommandBuffer
{
    private readonly ReadOnlyCollection<SceneRenderVertex> _vertices;
    private readonly ReadOnlyCollection<SceneDrawBatch> _batches;
    private readonly ReadOnlyCollection<SceneHitTarget> _hitTargets;

    internal ViewportSceneLayerCommandBuffer(
        SceneLayerKind kind,
        IEnumerable<SceneRenderVertex> vertices,
        IEnumerable<SceneDrawBatch> batches,
        IEnumerable<SceneHitTarget> hitTargets)
    {
        Kind = kind;
        _vertices = Array.AsReadOnly(vertices.ToArray());
        _batches = Array.AsReadOnly(batches.ToArray());
        _hitTargets = Array.AsReadOnly(hitTargets.ToArray());
    }

    public SceneLayerKind Kind { get; }

    public IReadOnlyList<SceneRenderVertex> Vertices => _vertices;

    public IReadOnlyList<SceneDrawBatch> Batches => _batches;

    public IReadOnlyList<SceneHitTarget> HitTargets => _hitTargets;
}

public sealed class ViewportSceneCommandBuffer
{
    private readonly ReadOnlyCollection<SceneRenderVertex> _vertices;
    private readonly ReadOnlyCollection<SceneDrawBatch> _batches;
    private readonly ReadOnlyCollection<SceneHitTarget> _hitTargets;
    private readonly ReadOnlyCollection<ViewportSceneLayerCommandBuffer> _layers;
    private readonly ReadOnlyCollection<SceneGridCommand> _gridCommands;
    private readonly ReadOnlyCollection<SceneAxisCommand> _axisCommands;
    private readonly ReadOnlyCollection<SceneLabelCommand> _labelCommands;
    private readonly ReadOnlyCollection<SceneScaleCommand> _scaleCommands;
    private readonly ReadOnlyCollection<SceneColorLegendCommand> _colorLegendCommands;

    internal ViewportSceneCommandBuffer(
        IEnumerable<SceneRenderVertex> vertices,
        IEnumerable<SceneDrawBatch> batches,
        IEnumerable<SceneHitTarget> hitTargets,
        IEnumerable<ViewportSceneLayerCommandBuffer>? layers = null,
        IEnumerable<SceneGridCommand>? gridCommands = null,
        IEnumerable<SceneAxisCommand>? axisCommands = null,
        IEnumerable<SceneLabelCommand>? labelCommands = null,
        IEnumerable<SceneScaleCommand>? scaleCommands = null,
        IEnumerable<SceneColorLegendCommand>? colorLegendCommands = null)
    {
        _vertices = Array.AsReadOnly(vertices.ToArray());
        _batches = Array.AsReadOnly(batches.ToArray());
        _hitTargets = Array.AsReadOnly(hitTargets.ToArray());
        _layers = Array.AsReadOnly((layers ?? []).ToArray());
        _gridCommands = Array.AsReadOnly((gridCommands ?? []).ToArray());
        _axisCommands = Array.AsReadOnly((axisCommands ?? []).ToArray());
        _labelCommands = Array.AsReadOnly((labelCommands ?? []).ToArray());
        _scaleCommands = Array.AsReadOnly((scaleCommands ?? []).ToArray());
        _colorLegendCommands = Array.AsReadOnly((colorLegendCommands ?? []).ToArray());
    }

    public IReadOnlyList<SceneRenderVertex> Vertices => _vertices;

    public IReadOnlyList<SceneDrawBatch> Batches => _batches;

    public IReadOnlyList<SceneHitTarget> HitTargets => _hitTargets;

    public IReadOnlyList<ViewportSceneLayerCommandBuffer> Layers => _layers;

    public IReadOnlyList<SceneGridCommand> GridCommands => _gridCommands;

    public IReadOnlyList<SceneAxisCommand> AxisCommands => _axisCommands;

    public IReadOnlyList<SceneLabelCommand> LabelCommands => _labelCommands;

    public IReadOnlyList<SceneScaleCommand> ScaleCommands => _scaleCommands;

    public IReadOnlyList<SceneColorLegendCommand> ColorLegendCommands => _colorLegendCommands;
}

public static class ViewportSceneCompiler
{
    public const int MaximumVertexCount = 250_000;
    public const int MaximumBatchCount = 250_000;
    public const int MaximumHitTargetCount = 250_000;
    public const int MaximumHitTargetPointCount = 500_000;
    public const int MaximumDecorationCommandCount = 10_000;
    public const int MaximumColorLegendEntryCount = 64;

    private static readonly SceneLayerKind[] RenderableLayerOrder =
    [
        SceneLayerKind.Members,
        SceneLayerKind.Nodes,
        SceneLayerKind.RigidZones,
        SceneLayerKind.Supports,
        SceneLayerKind.Springs,
        SceneLayerKind.Joints,
        SceneLayerKind.Panels,
        SceneLayerKind.NoticePoints,
        SceneLayerKind.Loads,
        SceneLayerKind.Displacements,
        SceneLayerKind.Reactions,
        SceneLayerKind.SectionForces,
    ];
    private static readonly Color3 NodeColor = new(0.20f, 0.82f, 0.94f);
    private static readonly Color3 MemberColor = new(0.78f, 0.82f, 0.88f);
    private static readonly Color3 SupportColor = new(0.27f, 0.78f, 0.48f);
    private static readonly Color3 LoadColor = new(1.00f, 0.58f, 0.18f);
    private static readonly Color3 DisplacementColor = new(0.95f, 0.32f, 0.72f);
    private static readonly Color3 ReactionColor = new(0.92f, 0.24f, 0.28f);
    private static readonly Color3 SectionForceColor = new(0.58f, 0.42f, 0.96f);
    private static readonly Color3 RigidZoneColor = new(0.96f, 0.78f, 0.28f);
    private static readonly Color3 SpringColor = new(0.50f, 0.88f, 0.58f);
    private static readonly Color3 JointColor = new(0.96f, 0.48f, 0.64f);
    private static readonly Color3 PanelColor = new(0.32f, 0.58f, 0.86f);
    private static readonly Color3 NoticePointColor = new(0.96f, 0.96f, 0.72f);
    private static readonly Color3 PrescribedDisplacementColor = new(0.28f, 0.86f, 0.88f);
    private static readonly Color3 ThermalPositiveColor = new(1.00f, 0.34f, 0.12f);
    private static readonly Color3 ThermalNegativeColor = new(0.18f, 0.52f, 1.00f);
    private static readonly Color3 ThermalNeutralColor = new(0.72f, 0.72f, 0.72f);
    private static readonly Color3 ThermalGradientColor = new(0.86f, 0.72f, 0.28f);
    private static readonly Color3 SelectionColor = new(1.00f, 0.88f, 0.16f);
    private static readonly Color3 HoverColor = new(1.00f, 1.00f, 0.72f);

    public static ViewportSceneCommandBuffer Compile(
        IViewportScene scene,
        ViewportCameraState camera,
        Size viewportSize,
        SceneEntityKey? selection = null,
        SceneEntityKey? hover = null,
        SceneLayerMask visibleLayers = SceneLayerMask.All,
        ViewportCameraPolicy? cameraPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!Enum.IsDefined(camera.Projection))
        {
            throw new ArgumentOutOfRangeException(nameof(camera));
        }

        if (selection is { } selected && !scene.Contains(selected))
        {
            throw new ArgumentException("Selection must reference an entity in the scene.", nameof(selection));
        }

        if (hover is { } hovered && !scene.Contains(hovered))
        {
            throw new ArgumentException("Hover must reference an entity in the scene.", nameof(hover));
        }

        if ((visibleLayers & ~SceneLayerMask.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleLayers));
        }

        if (!float.IsFinite(camera.Distance) || camera.Distance <= 0.0f ||
            !float.IsFinite(camera.OrthographicHeight) || camera.OrthographicHeight <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(camera), "Camera extents must be finite and positive.");
        }

        int width = Math.Max(1, viewportSize.Width);
        int height = Math.Max(1, viewportSize.Height);
        CameraProjection projection = CameraProjection.Create(camera, width / (float)height, cameraPolicy);
        Builder builder = new(projection, selection, hover);
        SceneLayerMask effectiveLayers = visibleLayers & scene.VisibleLayers;
        foreach (IViewportSceneLayer layer in scene.Layers)
        {
            if (!layer.IsVisible)
            {
                effectiveLayers &= ~layer.Kind.ToMask();
            }
        }

        Dictionary<string, ScenePoint3> nodes = scene.Nodes.ToDictionary(node => node.Id, node => node.Position, StringComparer.Ordinal);

        builder.BeginLayer(SceneLayerKind.Members);
        if ((effectiveLayers & SceneLayerMask.Members) != 0)
        {
            foreach (SceneMember member in scene.Members)
            {
                builder.AddLine(
                    new SceneEntityKey(SceneEntityKind.Member, member.Id),
                    nodes[member.StartNodeId],
                    nodes[member.EndNodeId],
                    MemberColor,
                    2.0f);
            }
        }
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.Nodes);
        if ((effectiveLayers & SceneLayerMask.Nodes) != 0)
        {
            foreach (SceneNode node in scene.Nodes)
            {
                builder.AddPoint(new SceneEntityKey(SceneEntityKind.Node, node.Id), node.Position, NodeColor, 7.0f);
            }
        }
        builder.EndLayer();

        float glyphSize = CalculateGlyphSize(scene);
        builder.BeginLayer(SceneLayerKind.RigidZones);
        if ((effectiveLayers & SceneLayerMask.RigidZones) != 0)
        {
            AddRigidZones(builder, scene, nodes, glyphSize);
        }
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.Supports);
        if ((effectiveLayers & SceneLayerMask.Supports) != 0)
        {
            foreach (SceneSupport support in scene.Supports)
            {
                ScenePoint3 origin = nodes[support.NodeId];
                SceneEntityKey key = new(SceneEntityKind.Support, support.Id);
                List<Vector3> translationAxes = [];
                if (support.FixX) translationAxes.Add(Vector3.UnitX);
                if (support.FixY) translationAxes.Add(Vector3.UnitY);
                if (support.FixZ) translationAxes.Add(Vector3.UnitZ);
                AddTranslationGlyphs(builder, key, origin, translationAxes, -glyphSize, SupportColor);

                float rotationRadius = glyphSize * 0.8f;
                if (support.FixRx) builder.AddRotationGlyph(key, origin, Vector3.UnitX, rotationRadius, SupportColor, 2.0f);
                if (support.FixRy) builder.AddRotationGlyph(key, origin, Vector3.UnitY, rotationRadius, SupportColor, 2.0f);
                if (support.FixRz) builder.AddRotationGlyph(key, origin, Vector3.UnitZ, rotationRadius, SupportColor, 2.0f);
            }
        }
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.Springs);
        if ((effectiveLayers & SceneLayerMask.Springs) != 0) AddSprings(builder, scene, nodes, glyphSize);
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.Joints);
        if ((effectiveLayers & SceneLayerMask.Joints) != 0) AddJoints(builder, scene, nodes, glyphSize);
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.Panels);
        if ((effectiveLayers & SceneLayerMask.Panels) != 0) AddPanels(builder, scene, nodes);
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.NoticePoints);
        if ((effectiveLayers & SceneLayerMask.NoticePoints) != 0) AddNoticePoints(builder, scene, nodes, glyphSize);
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.Loads);
        if ((effectiveLayers & SceneLayerMask.Loads) != 0)
        {
            foreach (SceneNodalLoad load in scene.NodalLoads)
            {
                builder.AddArrow(new SceneEntityKey(SceneEntityKind.NodalLoad, load.Id), nodes[load.NodeId], load.Vector, glyphSize * 2.5f, LoadColor);
                builder.AddRotationGlyph(new SceneEntityKey(SceneEntityKind.NodalLoad, load.Id), nodes[load.NodeId], new Vector3(load.Moment.X, load.Moment.Y, load.Moment.Z), glyphSize * 1.25f, LoadColor, 2.0f);
            }

            AddMemberLoads(builder, scene, nodes, glyphSize);
            foreach (ScenePrescribedDisplacement value in scene.PrescribedDisplacements)
            {
                SceneEntityKey key = new(SceneEntityKind.PrescribedDisplacement, value.Id);
                builder.AddArrow(key, nodes[value.NodeId], value.Translation, glyphSize * 2.0f, PrescribedDisplacementColor);
                builder.AddRotationGlyph(key, nodes[value.NodeId], new Vector3(value.Rotation.X, value.Rotation.Y, value.Rotation.Z), glyphSize, PrescribedDisplacementColor, 2.0f);
            }
        }
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.Displacements);
        if ((effectiveLayers & SceneLayerMask.Displacements) != 0 && scene.Displacement is not null)
        {
            Dictionary<string, ScenePoint3> displacements = scene.Displacement.Nodes.ToDictionary(
                displacement => displacement.NodeId,
                displacement => displacement.Vector,
                StringComparer.Ordinal);
            foreach (SceneMember member in scene.Members)
            {
                ScenePoint3 start = Displaced(nodes[member.StartNodeId], displacements, member.StartNodeId, scene.Displacement.Scale);
                ScenePoint3 end = Displaced(nodes[member.EndNodeId], displacements, member.EndNodeId, scene.Displacement.Scale);
                builder.AddResultLine(start, end, DisplacementColor, 2.5f);
            }

            foreach (SceneNode node in scene.Nodes)
            {
                ScenePoint3 position = Displaced(node.Position, displacements, node.Id, scene.Displacement.Scale);
                if (scene.Displacement.Context is not null && displacements.ContainsKey(node.Id))
                    builder.AddResultPoint(new SceneEntityKey(SceneEntityKind.Displacement, node.Id), position, DisplacementColor, 5.0f);
                else
                    builder.AddResultPoint(position, DisplacementColor, 5.0f);
            }
        }
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.Reactions);
        if ((effectiveLayers & SceneLayerMask.Reactions) != 0) AddReactions(builder, scene, nodes, glyphSize);
        builder.EndLayer();

        builder.BeginLayer(SceneLayerKind.SectionForces);
        if ((effectiveLayers & SceneLayerMask.SectionForces) != 0) AddSectionForces(builder, scene, nodes, glyphSize);
        builder.EndLayer();

        AddPresentationCommands(builder, scene, projection, effectiveLayers);

        return builder.Build();
    }

    public static IReadOnlyList<SceneLayerKind> RenderableLayers { get; } = Array.AsReadOnly(RenderableLayerOrder);

    public static ViewportSceneLayerCommandBuffer CompileLayer(
        IViewportScene scene,
        SceneLayerKind layer,
        ViewportCameraState camera,
        Size viewportSize,
        SceneEntityKey? selection = null,
        SceneEntityKey? hover = null,
        ViewportCameraPolicy? cameraPolicy = null)
    {
        if (!RenderableLayerOrder.Contains(layer))
        {
            throw new ArgumentOutOfRangeException(nameof(layer), "The requested layer does not contain OpenGL geometry.");
        }

        ViewportSceneCommandBuffer commands = Compile(
            scene,
            camera,
            viewportSize,
            selection,
            hover,
            layer.ToMask(),
            cameraPolicy);
        return commands.Layers.Single(value => value.Kind == layer);
    }

    public static ViewportSceneCommandBuffer Compose(
        IEnumerable<ViewportSceneLayerCommandBuffer> layers,
        IEnumerable<SceneGridCommand>? gridCommands = null,
        IEnumerable<SceneAxisCommand>? axisCommands = null,
        IEnumerable<SceneLabelCommand>? labelCommands = null,
        IEnumerable<SceneScaleCommand>? scaleCommands = null,
        IEnumerable<SceneColorLegendCommand>? colorLegendCommands = null)
    {
        ArgumentNullException.ThrowIfNull(layers);
        Dictionary<SceneLayerKind, ViewportSceneLayerCommandBuffer> byKind = layers.ToDictionary(value => value.Kind);
        List<SceneRenderVertex> vertices = [];
        List<SceneDrawBatch> batches = [];
        List<SceneHitTarget> hitTargets = [];
        List<ViewportSceneLayerCommandBuffer> ordered = [];
        SceneCompileBudget compileBudget = new();
        List<SceneGridCommand> boundedGridCommands = MaterializeDecorationCommands(
            gridCommands,
            compileBudget,
            static _ => 0);
        List<SceneAxisCommand> boundedAxisCommands = MaterializeDecorationCommands(
            axisCommands,
            compileBudget,
            static _ => 0);
        List<SceneLabelCommand> boundedLabelCommands = MaterializeDecorationCommands(
            labelCommands,
            compileBudget,
            static _ => 0);
        List<SceneScaleCommand> boundedScaleCommands = MaterializeDecorationCommands(
            scaleCommands,
            compileBudget,
            static _ => 0);
        List<SceneColorLegendCommand> boundedColorLegendCommands = MaterializeDecorationCommands(
            colorLegendCommands,
            compileBudget,
            static command => command.Entries.Count);
        foreach (SceneLayerKind kind in RenderableLayerOrder)
        {
            if (!byKind.TryGetValue(kind, out ViewportSceneLayerCommandBuffer? layer))
            {
                continue;
            }

            long layerHitTargetPointCount = 0;
            foreach (SceneHitTarget hitTarget in layer.HitTargets)
            {
                layerHitTargetPointCount = checked(layerHitTargetPointCount + hitTarget.Points.Count);
            }
            compileBudget.Reserve(
                layer.Vertices.Count,
                layer.Batches.Count,
                layer.HitTargets.Count,
                layerHitTargetPointCount);

            int vertexOffset = vertices.Count;
            vertices.AddRange(layer.Vertices);
            foreach (SceneDrawBatch batch in layer.Batches)
            {
                batches.Add(batch with { FirstVertex = checked(batch.FirstVertex + vertexOffset) });
            }
            hitTargets.AddRange(layer.HitTargets);
            ordered.Add(layer);
        }

        return new ViewportSceneCommandBuffer(
            vertices,
            batches,
            hitTargets,
            ordered,
            boundedGridCommands,
            boundedAxisCommands,
            boundedLabelCommands,
            boundedScaleCommands,
            boundedColorLegendCommands);
    }

    private static List<T> MaterializeDecorationCommands<T>(
        IEnumerable<T>? source,
        SceneCompileBudget compileBudget,
        Func<T, int> colorLegendEntryCount)
    {
        List<T> result = [];
        foreach (T command in source ?? [])
        {
            compileBudget.ReserveDecoration(
                commandCount: 1,
                colorLegendEntryCount: colorLegendEntryCount(command));
            result.Add(command);
        }

        return result;
    }

    private static void AddRigidZones(
        Builder builder,
        IViewportScene scene,
        IReadOnlyDictionary<string, ScenePoint3> nodes,
        float glyphSize)
    {
        Dictionary<string, SceneMember> members = scene.Members.ToDictionary(value => value.Id, StringComparer.Ordinal);
        foreach (SceneRigidZone zone in scene.RigidZones)
        {
            SceneMember member = members[zone.MemberId];
            ScenePoint3 start = nodes[member.StartNodeId];
            ScenePoint3 end = nodes[member.EndNodeId];
            float length = Distance3(start, end);
            if (length <= 1.0e-6f)
            {
                continue;
            }

            SceneEntityKey key = new(SceneEntityKind.RigidZone, zone.Id);
            float startRatio = Math.Clamp(zone.StartLength / length, 0.0f, 1.0f);
            float endRatio = Math.Clamp(zone.EndLength / length, 0.0f, 1.0f);
            if (startRatio > 0.0f) builder.AddLine(key, start, Lerp(start, end, startRatio), RigidZoneColor, Math.Max(3.0f, glyphSize * 5.0f));
            if (endRatio > 0.0f) builder.AddLine(key, Lerp(start, end, 1.0f - endRatio), end, RigidZoneColor, Math.Max(3.0f, glyphSize * 5.0f));
            if (startRatio == 0.0f && endRatio == 0.0f) builder.AddPoint(key, Lerp(start, end, 0.5f), RigidZoneColor, 7.0f);
        }
    }

    private static void AddSprings(
        Builder builder,
        IViewportScene scene,
        IReadOnlyDictionary<string, ScenePoint3> nodes,
        float glyphSize)
    {
        foreach (SceneSpring spring in scene.Springs)
        {
            ScenePoint3 start = nodes[spring.StartNodeId];
            ScenePoint3 end = nodes[spring.EndNodeId];
            Vector3 direction = ToVector(end) - ToVector(start);
            if (direction.LengthSquared() <= 1.0e-12f)
            {
                builder.AddPoint(new SceneEntityKey(SceneEntityKind.Spring, spring.Id), start, SpringColor, 8.0f);
                continue;
            }

            Vector3 normal = Vector3.Cross(Vector3.Normalize(direction), Vector3.UnitZ);
            if (normal.LengthSquared() <= 1.0e-8f) normal = Vector3.UnitX;
            normal = Vector3.Normalize(normal) * glyphSize * 0.35f;
            List<ScenePoint3> points = [start];
            for (int index = 1; index < 8; index++)
            {
                Vector3 point = Vector3.Lerp(ToVector(start), ToVector(end), index / 8.0f) + (normal * (index % 2 == 0 ? -1.0f : 1.0f));
                points.Add(ToPoint(point));
            }

            points.Add(end);
            builder.AddPolyline(new SceneEntityKey(SceneEntityKind.Spring, spring.Id), points, SpringColor, 2.0f);
        }
    }

    private static void AddJoints(
        Builder builder,
        IViewportScene scene,
        IReadOnlyDictionary<string, ScenePoint3> nodes,
        float glyphSize)
    {
        foreach (SceneJoint joint in scene.Joints)
        {
            SceneEntityKey key = new(SceneEntityKind.Joint, joint.Id);
            ScenePoint3 origin = nodes[joint.NodeId];
            builder.AddPoint(key, origin, JointColor, 10.0f);
            float translationLength = glyphSize * 0.85f;
            List<Vector3> translationAxes = [];
            if (joint.ReleaseX) translationAxes.Add(Vector3.UnitX);
            if (joint.ReleaseY) translationAxes.Add(Vector3.UnitY);
            if (joint.ReleaseZ) translationAxes.Add(Vector3.UnitZ);
            AddTranslationGlyphs(builder, key, origin, translationAxes, translationLength, JointColor);

            float rotationRadius = glyphSize * 0.65f;
            if (joint.ReleaseRx) builder.AddRotationGlyph(key, origin, Vector3.UnitX, rotationRadius, JointColor, 1.5f);
            if (joint.ReleaseRy) builder.AddRotationGlyph(key, origin, Vector3.UnitY, rotationRadius, JointColor, 1.5f);
            if (joint.ReleaseRz) builder.AddRotationGlyph(key, origin, Vector3.UnitZ, rotationRadius, JointColor, 1.5f);
        }
    }

    private static void AddTranslationGlyphs(
        Builder builder,
        SceneEntityKey key,
        ScenePoint3 origin,
        IReadOnlyList<Vector3> axes,
        float length,
        Color3 color)
    {
        if (axes.Count == 0)
        {
            return;
        }

        if (axes.Count == 1)
        {
            Vector3 offset = Vector3.Normalize(axes[0]) * length;
            builder.AddLine(key, origin, Add(origin, ToPoint(offset)), color, 2.0f);
            return;
        }

        List<ScenePoint3> points = [Add(origin, ToPoint(Vector3.Normalize(axes[0]) * length))];
        for (int index = 1; index < axes.Count; index++)
        {
            points.Add(origin);
            points.Add(Add(origin, ToPoint(Vector3.Normalize(axes[index]) * length)));
        }
        builder.AddPolyline(key, points, color, 2.0f);
    }

    private static void AddPanels(
        Builder builder,
        IViewportScene scene,
        IReadOnlyDictionary<string, ScenePoint3> nodes)
    {
        foreach (ScenePanel panel in scene.Panels)
        {
            List<ScenePoint3> boundary = panel.NodeIds.Select(nodeId => nodes[nodeId]).ToList();
            boundary.Add(boundary[0]);
            builder.AddPolyline(new SceneEntityKey(SceneEntityKind.Panel, panel.Id), boundary, PanelColor, 1.5f);
        }
    }

    private static void AddNoticePoints(
        Builder builder,
        IViewportScene scene,
        IReadOnlyDictionary<string, ScenePoint3> nodes,
        float glyphSize)
    {
        Dictionary<string, SceneMember> members = scene.Members.ToDictionary(value => value.Id, StringComparer.Ordinal);
        foreach (SceneNoticePoint point in scene.NoticePoints)
        {
            SceneMember member = members[point.MemberId];
            ScenePoint3 position = Lerp(nodes[member.StartNodeId], nodes[member.EndNodeId], point.RelativePosition);
            SceneEntityKey key = new(SceneEntityKind.NoticePoint, point.Id);
            builder.AddPoint(key, position, NoticePointColor, 8.0f);
            builder.AddLine(key, Add(position, new ScenePoint3(-glyphSize * 0.4f, 0.0f, 0.0f)), Add(position, new ScenePoint3(glyphSize * 0.4f, 0.0f, 0.0f)), NoticePointColor, 1.5f);
        }
    }

    private static void AddMemberLoads(
        Builder builder,
        IViewportScene scene,
        IReadOnlyDictionary<string, ScenePoint3> nodes,
        float glyphSize)
    {
        Dictionary<string, SceneMember> members = scene.Members.ToDictionary(value => value.Id, StringComparer.Ordinal);
        foreach (SceneMemberLoad load in scene.MemberLoads)
        {
            SceneMember member = members[load.MemberId];
            ScenePoint3 memberStart = nodes[member.StartNodeId];
            ScenePoint3 memberEnd = nodes[member.EndNodeId];
            SceneEntityKey key = new(SceneEntityKind.MemberLoad, load.Id);
            if (load.Kind == SceneMemberLoadKind.Thermal)
            {
                AddThermalMemberLoad(builder, key, memberStart, memberEnd, load, glyphSize);
                continue;
            }

            int sampleCount = load.Kind == SceneMemberLoadKind.Distributed ? 7 : 2;
            float maximumMagnitude = MathF.Max(ToVector(load.StartVector).Length(), ToVector(load.EndVector).Length());
            List<ScenePoint3> tips = [];
            for (int index = 0; index < sampleCount; index++)
            {
                float amount = sampleCount == 1 ? 0.0f : index / (float)(sampleCount - 1);
                float relative = load.StartRelativePosition + ((load.EndRelativePosition - load.StartRelativePosition) * amount);
                ScenePoint3 position = Lerp(memberStart, memberEnd, relative);
                ScenePoint3 vector = Lerp(load.StartVector, load.EndVector, amount);
                float relativeMagnitude = maximumMagnitude <= 1.0e-12f
                    ? 0.0f
                    : ToVector(vector).Length() / maximumMagnitude;
                if (load.VectorKind == SceneLoadVectorKind.Moment)
                {
                    builder.AddRotationGlyph(key, position, ToVector(vector), glyphSize * 1.25f * relativeMagnitude, LoadColor, 2.0f);
                }
                else
                {
                    float arrowLength = glyphSize * 2.5f * relativeMagnitude;
                    builder.AddArrow(key, position, vector, arrowLength, LoadColor);
                    Vector3 direction = ToVector(vector);
                    if (direction.LengthSquared() > 1.0e-12f)
                    {
                        tips.Add(Add(position, ToPoint(Vector3.Normalize(direction) * arrowLength)));
                    }
                }

                if (load.Kind == SceneMemberLoadKind.Point && load.StartRelativePosition == load.EndRelativePosition)
                {
                    break;
                }
            }

            if (load.Kind == SceneMemberLoadKind.Distributed && tips.Count > 1)
            {
                builder.AddPolyline(key, tips, LoadColor, 1.5f);
            }
        }
    }

    private static void AddThermalMemberLoad(
        Builder builder,
        SceneEntityKey key,
        ScenePoint3 memberStart,
        ScenePoint3 memberEnd,
        SceneMemberLoad load,
        float glyphSize)
    {
        Vector3 memberAxis = ToVector(memberEnd) - ToVector(memberStart);
        if (memberAxis.LengthSquared() <= 1.0e-12f)
        {
            memberAxis = Vector3.UnitX;
        }
        else
        {
            memberAxis = Vector3.Normalize(memberAxis);
        }

        Vector3 reference = MathF.Abs(Vector3.Dot(memberAxis, Vector3.UnitZ)) < 0.9f
            ? Vector3.UnitZ
            : Vector3.UnitY;
        Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(memberAxis, reference)) * glyphSize;
        float maximumMagnitude = MathF.Max(MathF.Abs(load.TemperatureTop), MathF.Abs(load.TemperatureBottom));
        float topRatio = maximumMagnitude <= 1.0e-12f ? 0.0f : load.TemperatureTop / maximumMagnitude;
        float bottomRatio = maximumMagnitude <= 1.0e-12f ? 0.0f : load.TemperatureBottom / maximumMagnitude;
        Vector3 topOffset = faceNormal * (0.60f + (0.35f * topRatio));
        Vector3 bottomOffset = faceNormal * (-0.60f + (0.35f * bottomRatio));
        ScenePoint3 topStart = Add(memberStart, ToPoint(topOffset));
        ScenePoint3 topEnd = Add(memberEnd, ToPoint(topOffset));
        ScenePoint3 bottomStart = Add(memberStart, ToPoint(bottomOffset));
        ScenePoint3 bottomEnd = Add(memberEnd, ToPoint(bottomOffset));

        builder.AddLine(key, topStart, topEnd, ThermalColor(load.TemperatureTop), 2.0f);
        builder.AddLine(key, bottomStart, bottomEnd, ThermalColor(load.TemperatureBottom), 2.0f);
        builder.AddLine(
            key,
            Lerp(topStart, topEnd, 0.5f),
            Lerp(bottomStart, bottomEnd, 0.5f),
            ThermalGradientColor,
            1.5f);
    }

    private static Color3 ThermalColor(float value) => value switch
    {
        > 0.0f => ThermalPositiveColor,
        < 0.0f => ThermalNegativeColor,
        _ => ThermalNeutralColor,
    };

    private static void AddReactions(
        Builder builder,
        IViewportScene scene,
        IReadOnlyDictionary<string, ScenePoint3> nodes,
        float glyphSize)
    {
        if (scene.Reactions is null) return;
        foreach (SceneReaction reaction in scene.Reactions.Reactions)
        {
            SceneEntityKey key = new(SceneEntityKind.Reaction, reaction.Id);
            ScenePoint3 origin = nodes[reaction.NodeId];
            if (ToVector(reaction.Force).LengthSquared() <= 1.0e-12f && ToVector(reaction.Moment).LengthSquared() <= 1.0e-12f)
                builder.AddPoint(key, origin, ReactionColor, 6.0f);
            builder.AddArrow(key, origin, reaction.Force, glyphSize * 2.5f * scene.Reactions.Scale, ReactionColor);
            builder.AddRotationGlyph(key, origin, ToVector(reaction.Moment), glyphSize * 1.25f * scene.Reactions.Scale, ReactionColor, 2.0f);
        }
    }

    private static void AddSectionForces(
        Builder builder,
        IViewportScene scene,
        IReadOnlyDictionary<string, ScenePoint3> nodes,
        float glyphSize)
    {
        if (scene.SectionForces is null) return;
        Dictionary<string, SceneMember> members = scene.Members.ToDictionary(value => value.Id, StringComparer.Ordinal);
        foreach (SceneSectionForce sample in scene.SectionForces.Samples)
        {
            SceneMember member = members[sample.MemberId];
            ScenePoint3 position = Lerp(nodes[member.StartNodeId], nodes[member.EndNodeId], sample.RelativePosition);
            SceneEntityKey key = new(SceneEntityKind.SectionForce, sample.Id);
            if (ToVector(sample.Force).LengthSquared() <= 1.0e-12f && ToVector(sample.Moment).LengthSquared() <= 1.0e-12f)
                builder.AddPoint(key, position, SectionForceColor, 6.0f);
            builder.AddArrow(key, position, sample.Force, glyphSize * scene.SectionForces.Scale, SectionForceColor);
            builder.AddRotationGlyph(key, position, ToVector(sample.Moment), glyphSize * 0.8f * scene.SectionForces.Scale, SectionForceColor, 2.0f);
        }
    }

    private static void AddPresentationCommands(
        Builder builder,
        IViewportScene scene,
        CameraProjection projection,
        SceneLayerMask visibleLayers)
    {
        if ((visibleLayers & SceneLayerMask.Grid) != 0 && scene.Presentation.Grid is { IsVisible: true } grid)
            builder.AddGridCommand(new SceneGridCommand(grid.Plane, grid.MajorSpacing, grid.MinorDivisions));
        if ((visibleLayers & SceneLayerMask.Axes) != 0 && scene.Presentation.ShowAxes)
        {
            builder.AddAxisCommand(new SceneAxisCommand("X", projection.Project(default), projection.Project(new ScenePoint3(1.0f, 0.0f, 0.0f))));
            builder.AddAxisCommand(new SceneAxisCommand("Y", projection.Project(default), projection.Project(new ScenePoint3(0.0f, 1.0f, 0.0f))));
            builder.AddAxisCommand(new SceneAxisCommand("Z", projection.Project(default), projection.Project(new ScenePoint3(0.0f, 0.0f, 1.0f))));
        }
        if ((visibleLayers & SceneLayerMask.Labels) != 0)
            foreach (SceneLabel label in scene.Presentation.Labels)
                builder.AddLabelCommand(new SceneLabelCommand(label.StableId, label.Text, projection.Project(label.Position), label.Entity));
        if ((visibleLayers & SceneLayerMask.ScaleLegend) != 0 && scene.Presentation.ScaleLegend is { } scale)
            builder.AddScaleCommand(new SceneScaleCommand(scale.StableId, scale.Caption, scale.Scale));
        if ((visibleLayers & SceneLayerMask.ColorLegend) != 0 && scene.Presentation.ColorLegend is { } legend)
            builder.AddColorLegendCommand(new SceneColorLegendCommand(legend.StableId, legend.Caption, legend.Entries));
    }

    public static SceneEntityKey? HitTest(
        ViewportSceneCommandBuffer commandBuffer,
        Point clientPoint,
        Size viewportSize,
        float tolerancePixels = 9.0f)
        => HitTestDetailed(
            commandBuffer,
            clientPoint,
            viewportSize,
            new SceneHitTestOptions(tolerancePixels))?.Key;

    public static SceneHitTestResult? HitTestDetailed(
        ViewportSceneCommandBuffer commandBuffer,
        Point clientPoint,
        Size viewportSize,
        SceneHitTestOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(commandBuffer);
        options ??= new SceneHitTestOptions();

        float width = Math.Max(1, viewportSize.Width);
        float height = Math.Max(1, viewportSize.Height);
        float best = options.TolerancePixels;
        int bestPriority = int.MaxValue;
        SceneHitTestResult? result = null;

        foreach (SceneHitTarget target in commandBuffer.HitTargets)
        {
            if ((target.Layer.ToMask() & options.Layers) == 0)
            {
                continue;
            }

            float distance = target.Primitive == ScenePrimitive.Points
                ? target.Points.Min(point => Distance(clientPoint, ToClient(point, width, height)))
                : SegmentDistance(target.Points, clientPoint, width, height);
            int priority = HitPriority(target.Key.Kind);
            if (distance < best - 0.01f || (MathF.Abs(distance - best) <= 0.01f && priority < bestPriority))
            {
                best = distance;
                bestPriority = priority;
                result = new SceneHitTestResult(target.Key, target.Layer, distance, priority);
            }
        }

        return result;
    }

    private static int HitPriority(SceneEntityKind kind) => kind switch
    {
        SceneEntityKind.Node => 0,
        SceneEntityKind.NodalLoad or SceneEntityKind.MemberLoad or SceneEntityKind.PrescribedDisplacement or
            SceneEntityKind.NoticePoint or SceneEntityKind.Joint or SceneEntityKind.Displacement or
            SceneEntityKind.Reaction or SceneEntityKind.SectionForce => 1,
        SceneEntityKind.Support or SceneEntityKind.Spring => 2,
        SceneEntityKind.Member or SceneEntityKind.RigidZone or SceneEntityKind.Panel => 3,
        _ => 4,
    };

    public static ViewportCameraState Home(IViewportScene scene, ViewportProjection projection)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!Enum.IsDefined(projection))
        {
            throw new ArgumentOutOfRangeException(nameof(projection));
        }

        SceneBounds bounds = SceneBounds.From(scene);
        return new ViewportCameraState(projection, bounds.Center, bounds.Radius * 3.25f, bounds.Radius * 2.4f);
    }

    public static ViewportCameraState Home(IViewportScene scene, ViewportCameraPolicy policy)
    {
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }

        return Home(
            scene,
            policy == ViewportCameraPolicy.TwoDimensional
                ? ViewportProjection.Orthographic
                : ViewportProjection.Perspective);
    }

    public static ViewportCameraState Fit(IViewportScene scene, ViewportCameraState camera)
    {
        ArgumentNullException.ThrowIfNull(scene);
        SceneBounds bounds = SceneBounds.From(scene);
        return camera with
        {
            Target = bounds.Center,
            Distance = bounds.Radius * 3.25f,
            OrthographicHeight = bounds.Radius * 2.4f,
        };
    }

    private static ScenePoint3 Displaced(
        ScenePoint3 position,
        IReadOnlyDictionary<string, ScenePoint3> displacements,
        string nodeId,
        float scale) =>
        displacements.TryGetValue(nodeId, out ScenePoint3 displacement)
            ? Add(position, Multiply(displacement, scale))
            : position;

    private static float CalculateGlyphSize(IViewportScene scene) => Math.Max(0.05f, SceneBounds.From(scene).Radius * 0.08f);

    private static ScenePoint3 Add(ScenePoint3 left, ScenePoint3 right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    private static float Distance3(ScenePoint3 left, ScenePoint3 right) => Vector3.Distance(ToVector(left), ToVector(right));

    private static Vector3 ToVector(ScenePoint3 value) => new(value.X, value.Y, value.Z);

    private static ScenePoint3 ToPoint(Vector3 value) => new(value.X, value.Y, value.Z);

    private static ScenePoint3 Multiply(ScenePoint3 value, float scale) =>
        new(value.X * scale, value.Y * scale, value.Z * scale);

    private static ScenePoint3 Lerp(ScenePoint3 start, ScenePoint3 end, float amount) =>
        new(
            start.X + ((end.X - start.X) * amount),
            start.Y + ((end.Y - start.Y) * amount),
            start.Z + ((end.Z - start.Z) * amount));

    private static PointF ToClient(ScenePoint2 point, float width, float height) =>
        new((point.X + 1.0f) * 0.5f * width, (1.0f - point.Y) * 0.5f * height);

    private static float Distance(Point point, PointF other)
    {
        float dx = point.X - other.X;
        float dy = point.Y - other.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float SegmentDistance(IReadOnlyList<ScenePoint2> points, Point point, float width, float height)
    {
        float result = float.PositiveInfinity;
        for (int index = 0; index + 1 < points.Count; index++)
        {
            PointF start = ToClient(points[index], width, height);
            PointF end = ToClient(points[index + 1], width, height);
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float lengthSquared = (dx * dx) + (dy * dy);
            float amount = lengthSquared == 0.0f
                ? 0.0f
                : Math.Clamp((((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared, 0.0f, 1.0f);
            float closestX = start.X + (amount * dx);
            float closestY = start.Y + (amount * dy);
            float distanceX = point.X - closestX;
            float distanceY = point.Y - closestY;
            result = Math.Min(result, MathF.Sqrt((distanceX * distanceX) + (distanceY * distanceY)));
        }

        return result;
    }

    private readonly record struct Color3(float Red, float Green, float Blue);

    private sealed class Builder(CameraProjection projection, SceneEntityKey? selection, SceneEntityKey? hover)
    {
        private readonly SceneCompileBudget _compileBudget = new();
        private readonly List<SceneRenderVertex> _vertices = [];
        private readonly List<SceneDrawBatch> _batches = [];
        private readonly List<SceneHitTarget> _hitTargets = [];
        private readonly List<ViewportSceneLayerCommandBuffer> _layers = [];
        private readonly List<SceneGridCommand> _gridCommands = [];
        private readonly List<SceneAxisCommand> _axisCommands = [];
        private readonly List<SceneLabelCommand> _labelCommands = [];
        private readonly List<SceneScaleCommand> _scaleCommands = [];
        private readonly List<SceneColorLegendCommand> _colorLegendCommands = [];
        private SceneLayerKind? _currentLayer;
        private int _layerVertexStart;
        private int _layerBatchStart;
        private int _layerHitStart;

        public void BeginLayer(SceneLayerKind kind)
        {
            if (_currentLayer is not null)
            {
                throw new InvalidOperationException("The preceding scene layer was not completed.");
            }

            _currentLayer = kind;
            _layerVertexStart = _vertices.Count;
            _layerBatchStart = _batches.Count;
            _layerHitStart = _hitTargets.Count;
        }

        public void EndLayer()
        {
            SceneLayerKind kind = _currentLayer ?? throw new InvalidOperationException("No scene layer is active.");
            SceneRenderVertex[] vertices = _vertices.Skip(_layerVertexStart).ToArray();
            SceneDrawBatch[] batches = _batches.Skip(_layerBatchStart)
                .Select(batch => batch with { FirstVertex = batch.FirstVertex - _layerVertexStart })
                .ToArray();
            SceneHitTarget[] hitTargets = _hitTargets.Skip(_layerHitStart).ToArray();
            _layers.Add(new ViewportSceneLayerCommandBuffer(kind, vertices, batches, hitTargets));
            _currentLayer = null;
        }

        public void AddPoint(SceneEntityKey key, ScenePoint3 point, Color3 color, float width)
        {
            _compileBudget.Reserve(vertexCount: 1, batchCount: 1, hitTargetCount: 1, hitTargetPointCount: 1);
            ScenePoint2 projected = projection.Project(point);
            (Color3 resolvedColor, float resolvedWidth) = ResolveStyle(key, color, width, 4.0f);
            AddBatch(ScenePrimitive.Points, [projected], resolvedColor, resolvedWidth);
            _hitTargets.Add(new SceneHitTarget(key, ScenePrimitive.Points, [projected]));
        }

        public void AddLine(SceneEntityKey key, ScenePoint3 start, ScenePoint3 end, Color3 color, float width)
        {
            _compileBudget.Reserve(vertexCount: 2, batchCount: 1, hitTargetCount: 1, hitTargetPointCount: 2);
            ScenePoint2[] points = [projection.Project(start), projection.Project(end)];
            (Color3 resolvedColor, float resolvedWidth) = ResolveStyle(key, color, width, 2.0f);
            AddBatch(ScenePrimitive.Lines, points, resolvedColor, resolvedWidth);
            _hitTargets.Add(new SceneHitTarget(key, ScenePrimitive.Lines, points));
        }

        public void AddPolyline(SceneEntityKey key, IReadOnlyList<ScenePoint3> points, Color3 color, float width)
        {
            int segmentVertexCount = checked(Math.Max(0, points.Count - 1) * 2);
            _compileBudget.Reserve(
                segmentVertexCount,
                segmentVertexCount == 0 ? 0 : 1,
                hitTargetCount: 1,
                hitTargetPointCount: points.Count);
            ScenePoint2[] projected = points.Select(projection.Project).ToArray();
            List<ScenePoint2> segments = [];
            for (int index = 0; index + 1 < projected.Length; index++)
            {
                segments.Add(projected[index]);
                segments.Add(projected[index + 1]);
            }

            (Color3 resolvedColor, float resolvedWidth) = ResolveStyle(key, color, width, 2.0f);
            AddBatch(ScenePrimitive.Lines, segments, resolvedColor, resolvedWidth);
            _hitTargets.Add(new SceneHitTarget(key, ScenePrimitive.Lines, projected));
        }

        public void AddArrow(SceneEntityKey key, ScenePoint3 origin, ScenePoint3 vector, float length, Color3 color)
        {
            Vector3 direction = new(vector.X, vector.Y, vector.Z);
            if (direction.LengthSquared() <= 1.0e-12f)
            {
                return;
            }

            direction = Vector3.Normalize(direction) * length;
            ScenePoint3 end = new(origin.X + direction.X, origin.Y + direction.Y, origin.Z + direction.Z);
            AddLine(key, origin, end, color, 2.0f);
        }

        public void AddRotationGlyph(
            SceneEntityKey key,
            ScenePoint3 origin,
            Vector3 axis,
            float radius,
            Color3 color,
            float width)
        {
            if (!float.IsFinite(radius) || radius < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if (axis.LengthSquared() <= 1.0e-12f || radius <= 1.0e-8f)
            {
                return;
            }

            axis = Vector3.Normalize(axis);
            Vector3 reference = MathF.Abs(Vector3.Dot(axis, Vector3.UnitZ)) < 0.9f
                ? Vector3.UnitZ
                : Vector3.UnitX;
            Vector3 first = Vector3.Normalize(Vector3.Cross(axis, reference)) * radius;
            Vector3 second = Vector3.Normalize(Vector3.Cross(axis, first)) * radius;
            const int segmentCount = 12;
            List<ScenePoint3> points = new(segmentCount + 1);
            for (int index = 0; index <= segmentCount; index++)
            {
                float angle = index / (float)segmentCount * MathF.PI * 1.75f;
                Vector3 offset = (first * MathF.Cos(angle)) + (second * MathF.Sin(angle));
                points.Add(new ScenePoint3(
                    origin.X + offset.X,
                    origin.Y + offset.Y,
                    origin.Z + offset.Z));
            }

            AddPolyline(key, points, color, width);
        }

        public void AddResultLine(ScenePoint3 start, ScenePoint3 end, Color3 color, float width)
        {
            _compileBudget.Reserve(vertexCount: 2, batchCount: 1, hitTargetCount: 0, hitTargetPointCount: 0);
            AddBatch(ScenePrimitive.Lines, [projection.Project(start), projection.Project(end)], color, width);
        }

        public void AddResultPoint(ScenePoint3 point, Color3 color, float width)
        {
            _compileBudget.Reserve(vertexCount: 1, batchCount: 1, hitTargetCount: 0, hitTargetPointCount: 0);
            AddBatch(ScenePrimitive.Points, [projection.Project(point)], color, width);
        }

        public void AddResultPoint(SceneEntityKey key, ScenePoint3 point, Color3 color, float width) =>
            AddPoint(key, point, color, width);

        public void AddGridCommand(SceneGridCommand command)
        {
            _compileBudget.ReserveDecoration(commandCount: 1, colorLegendEntryCount: 0);
            _gridCommands.Add(command);
        }

        public void AddAxisCommand(SceneAxisCommand command)
        {
            _compileBudget.ReserveDecoration(commandCount: 1, colorLegendEntryCount: 0);
            _axisCommands.Add(command);
        }

        public void AddLabelCommand(SceneLabelCommand command)
        {
            _compileBudget.ReserveDecoration(commandCount: 1, colorLegendEntryCount: 0);
            _labelCommands.Add(command);
        }

        public void AddScaleCommand(SceneScaleCommand command)
        {
            _compileBudget.ReserveDecoration(commandCount: 1, colorLegendEntryCount: 0);
            _scaleCommands.Add(command);
        }

        public void AddColorLegendCommand(SceneColorLegendCommand command)
        {
            _compileBudget.ReserveDecoration(commandCount: 1, colorLegendEntryCount: command.Entries.Count);
            _colorLegendCommands.Add(command);
        }

        public ViewportSceneCommandBuffer Build()
        {
            if (_currentLayer is not null)
            {
                throw new InvalidOperationException("A scene layer is still active.");
            }

            return new(
                _vertices,
                _batches,
                _hitTargets,
                _layers,
                _gridCommands,
                _axisCommands,
                _labelCommands,
                _scaleCommands,
                _colorLegendCommands);
        }

        private (Color3 Color, float Width) ResolveStyle(SceneEntityKey key, Color3 color, float width, float emphasis)
        {
            if (key.Equals(selection)) return (SelectionColor, width + emphasis);
            if (key.Equals(hover)) return (HoverColor, width + (emphasis * 0.5f));
            return (color, width);
        }

        private void AddBatch(ScenePrimitive primitive, IReadOnlyList<ScenePoint2> points, Color3 color, float width)
        {
            int first = _vertices.Count;
            foreach (ScenePoint2 point in points)
            {
                _vertices.Add(new SceneRenderVertex(point.X, point.Y, color.Red, color.Green, color.Blue));
            }

            int count = _vertices.Count - first;
            if (count > 0)
            {
                _batches.Add(new SceneDrawBatch(primitive, first, count, width));
            }
        }
    }

    private sealed class SceneCompileBudget
    {
        private int _vertexCount;
        private int _batchCount;
        private int _hitTargetCount;
        private int _hitTargetPointCount;
        private int _decorationCommandCount;
        private int _colorLegendEntryCount;

        public void Reserve(
            int vertexCount,
            int batchCount,
            int hitTargetCount,
            long hitTargetPointCount)
        {
            if (vertexCount < 0 || batchCount < 0 || hitTargetCount < 0 || hitTargetPointCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexCount));
            }

            long nextVertexCount = checked((long)_vertexCount + vertexCount);
            long nextBatchCount = checked((long)_batchCount + batchCount);
            long nextHitTargetCount = checked((long)_hitTargetCount + hitTargetCount);
            long nextHitTargetPointCount = checked(_hitTargetPointCount + hitTargetPointCount);
            if (nextVertexCount > MaximumVertexCount)
            {
                throw new InvalidOperationException(
                    $"Compiled scene vertex budget of {MaximumVertexCount} would be exceeded.");
            }

            if (nextBatchCount > MaximumBatchCount)
            {
                throw new InvalidOperationException(
                    $"Compiled scene batch budget of {MaximumBatchCount} would be exceeded.");
            }

            if (nextHitTargetCount > MaximumHitTargetCount)
            {
                throw new InvalidOperationException(
                    $"Compiled scene hit-target budget of {MaximumHitTargetCount} would be exceeded.");
            }

            if (nextHitTargetPointCount > MaximumHitTargetPointCount)
            {
                throw new InvalidOperationException(
                    $"Compiled scene hit-target point budget of {MaximumHitTargetPointCount} would be exceeded.");
            }

            _vertexCount = (int)nextVertexCount;
            _batchCount = (int)nextBatchCount;
            _hitTargetCount = (int)nextHitTargetCount;
            _hitTargetPointCount = (int)nextHitTargetPointCount;
        }

        public void ReserveDecoration(int commandCount, int colorLegendEntryCount)
        {
            if (commandCount < 0 || colorLegendEntryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(commandCount));
            }

            long nextCommandCount = checked((long)_decorationCommandCount + commandCount);
            long nextColorLegendEntryCount = checked((long)_colorLegendEntryCount + colorLegendEntryCount);
            if (nextCommandCount > MaximumDecorationCommandCount)
            {
                throw new InvalidOperationException(
                    $"Compiled scene decoration command budget of {MaximumDecorationCommandCount} would be exceeded.");
            }

            if (nextColorLegendEntryCount > MaximumColorLegendEntryCount)
            {
                throw new InvalidOperationException(
                    $"Compiled scene color-legend entry budget of {MaximumColorLegendEntryCount} would be exceeded.");
            }

            _decorationCommandCount = (int)nextCommandCount;
            _colorLegendEntryCount = (int)nextColorLegendEntryCount;
        }
    }

    private readonly record struct CameraProjection(Matrix4x4 ViewProjection)
    {
        public static CameraProjection Create(
            ViewportCameraState camera,
            float aspectRatio,
            ViewportCameraPolicy? cameraPolicy = null)
        {
            Vector3 target = new(camera.Target.X, camera.Target.Y, camera.Target.Z);
            Vector3 direction = cameraPolicy == ViewportCameraPolicy.TwoDimensional
                ? -Vector3.UnitY
                : Vector3.Normalize(new Vector3(1.0f, -1.0f, 0.75f));
            Vector3 eye = target + (direction * Math.Max(0.01f, camera.Distance));
            Matrix4x4 view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitZ);
            Matrix4x4 projection = camera.Projection == ViewportProjection.Orthographic
                ? Matrix4x4.CreateOrthographic(
                    Math.Max(0.01f, camera.OrthographicHeight) * Math.Max(0.01f, aspectRatio),
                    Math.Max(0.01f, camera.OrthographicHeight),
                    0.01f,
                    Math.Max(10.0f, camera.Distance * 10.0f))
                : Matrix4x4.CreatePerspectiveFieldOfView(
                    MathF.PI / 4.0f,
                    Math.Max(0.01f, aspectRatio),
                    0.01f,
                    Math.Max(10.0f, camera.Distance * 10.0f));
            return new CameraProjection(view * projection);
        }

        public ScenePoint2 Project(ScenePoint3 point)
        {
            Vector4 clip = Vector4.Transform(new Vector4(point.X, point.Y, point.Z, 1.0f), ViewProjection);
            if (!float.IsFinite(clip.X) || !float.IsFinite(clip.Y) || !float.IsFinite(clip.W))
            {
                throw new InvalidOperationException("Scene projection produced a non-finite coordinate.");
            }

            float divisor = Math.Abs(clip.W) < 1.0e-6f ? 1.0f : clip.W;
            return new ScenePoint2(clip.X / divisor, clip.Y / divisor);
        }
    }

    private readonly record struct SceneBounds(ScenePoint3 Center, float Radius)
    {
        public static SceneBounds From(IViewportScene scene)
        {
            float minX = scene.Nodes.Min(node => node.Position.X);
            float minY = scene.Nodes.Min(node => node.Position.Y);
            float minZ = scene.Nodes.Min(node => node.Position.Z);
            float maxX = scene.Nodes.Max(node => node.Position.X);
            float maxY = scene.Nodes.Max(node => node.Position.Y);
            float maxZ = scene.Nodes.Max(node => node.Position.Z);
            ScenePoint3 center = new((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            float radius = scene.Nodes.Max(node =>
            {
                float dx = node.Position.X - center.X;
                float dy = node.Position.Y - center.Y;
                float dz = node.Position.Z - center.Z;
                return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            });
            return new SceneBounds(center, Math.Max(0.5f, radius));
        }
    }
}
