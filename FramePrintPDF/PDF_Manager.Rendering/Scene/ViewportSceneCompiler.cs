using System.Collections.ObjectModel;
using System.Numerics;

namespace PDF_Manager.Rendering.Scene;

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

    public IReadOnlyList<ScenePoint2> Points => _points;
}

public readonly record struct ScenePoint2(float X, float Y);

public sealed class ViewportSceneCommandBuffer
{
    private readonly ReadOnlyCollection<SceneRenderVertex> _vertices;
    private readonly ReadOnlyCollection<SceneDrawBatch> _batches;
    private readonly ReadOnlyCollection<SceneHitTarget> _hitTargets;

    internal ViewportSceneCommandBuffer(
        IEnumerable<SceneRenderVertex> vertices,
        IEnumerable<SceneDrawBatch> batches,
        IEnumerable<SceneHitTarget> hitTargets)
    {
        _vertices = Array.AsReadOnly(vertices.ToArray());
        _batches = Array.AsReadOnly(batches.ToArray());
        _hitTargets = Array.AsReadOnly(hitTargets.ToArray());
    }

    public IReadOnlyList<SceneRenderVertex> Vertices => _vertices;

    public IReadOnlyList<SceneDrawBatch> Batches => _batches;

    public IReadOnlyList<SceneHitTarget> HitTargets => _hitTargets;
}

public static class ViewportSceneCompiler
{
    private static readonly Color3 NodeColor = new(0.20f, 0.82f, 0.94f);
    private static readonly Color3 MemberColor = new(0.78f, 0.82f, 0.88f);
    private static readonly Color3 SupportColor = new(0.27f, 0.78f, 0.48f);
    private static readonly Color3 LoadColor = new(1.00f, 0.58f, 0.18f);
    private static readonly Color3 DisplacementColor = new(0.95f, 0.32f, 0.72f);
    private static readonly Color3 SelectionColor = new(1.00f, 0.88f, 0.16f);

    public static ViewportSceneCommandBuffer Compile(
        ViewportSceneModel scene,
        ViewportCameraState camera,
        Size viewportSize,
        SceneEntityKey? selection = null)
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

        if (!float.IsFinite(camera.Distance) || camera.Distance <= 0.0f ||
            !float.IsFinite(camera.OrthographicHeight) || camera.OrthographicHeight <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(camera), "Camera extents must be finite and positive.");
        }

        int width = Math.Max(1, viewportSize.Width);
        int height = Math.Max(1, viewportSize.Height);
        CameraProjection projection = CameraProjection.Create(camera, width / (float)height);
        Builder builder = new(projection, selection);
        Dictionary<string, ScenePoint3> nodes = scene.Nodes.ToDictionary(node => node.Id, node => node.Position, StringComparer.Ordinal);

        foreach (SceneMember member in scene.Members)
        {
            builder.AddLine(
                new SceneEntityKey(SceneEntityKind.Member, member.Id),
                nodes[member.StartNodeId],
                nodes[member.EndNodeId],
                MemberColor,
                2.0f);
        }

        foreach (SceneNode node in scene.Nodes)
        {
            builder.AddPoint(new SceneEntityKey(SceneEntityKind.Node, node.Id), node.Position, NodeColor, 7.0f);
        }

        float glyphSize = CalculateGlyphSize(scene);
        foreach (SceneSupport support in scene.Supports)
        {
            ScenePoint3 origin = nodes[support.NodeId];
            SceneEntityKey key = new(SceneEntityKind.Support, support.Id);
            if (support.FixX || support.FixY || support.FixZ)
            {
                ScenePoint3 left = Add(origin, new ScenePoint3(-glyphSize, 0.0f, -glyphSize));
                ScenePoint3 right = Add(origin, new ScenePoint3(glyphSize, 0.0f, -glyphSize));
                builder.AddPolyline(key, [origin, left, right, origin], SupportColor, 2.0f);
            }

            float rotationRadius = glyphSize * 0.8f;
            if (support.FixRx)
            {
                builder.AddRotationGlyph(key, origin, Vector3.UnitX, rotationRadius, SupportColor, 2.0f);
            }

            if (support.FixRy)
            {
                builder.AddRotationGlyph(key, origin, Vector3.UnitY, rotationRadius, SupportColor, 2.0f);
            }

            if (support.FixRz)
            {
                builder.AddRotationGlyph(key, origin, Vector3.UnitZ, rotationRadius, SupportColor, 2.0f);
            }
        }

        foreach (SceneNodalLoad load in scene.NodalLoads)
        {
            builder.AddArrow(
                new SceneEntityKey(SceneEntityKind.NodalLoad, load.Id),
                nodes[load.NodeId],
                load.Vector,
                glyphSize * 2.5f,
                LoadColor);
            builder.AddRotationGlyph(
                new SceneEntityKey(SceneEntityKind.NodalLoad, load.Id),
                nodes[load.NodeId],
                new Vector3(load.Moment.X, load.Moment.Y, load.Moment.Z),
                glyphSize * 1.25f,
                LoadColor,
                2.0f);
        }

        Dictionary<string, SceneMember> members = scene.Members.ToDictionary(member => member.Id, StringComparer.Ordinal);
        foreach (SceneMemberLoad load in scene.MemberLoads)
        {
            SceneMember member = members[load.MemberId];
            ScenePoint3 position = Lerp(nodes[member.StartNodeId], nodes[member.EndNodeId], load.RelativePosition);
            builder.AddArrow(
                new SceneEntityKey(SceneEntityKind.MemberLoad, load.Id),
                position,
                load.Vector,
                glyphSize * 2.5f,
                LoadColor);
        }

        if (scene.Displacement is not null)
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
                builder.AddResultPoint(Displaced(node.Position, displacements, node.Id, scene.Displacement.Scale), DisplacementColor, 5.0f);
            }
        }

        return builder.Build();
    }

    public static SceneEntityKey? HitTest(
        ViewportSceneCommandBuffer commandBuffer,
        Point clientPoint,
        Size viewportSize,
        float tolerancePixels = 9.0f)
    {
        ArgumentNullException.ThrowIfNull(commandBuffer);
        if (!float.IsFinite(tolerancePixels) || tolerancePixels <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerancePixels));
        }

        float width = Math.Max(1, viewportSize.Width);
        float height = Math.Max(1, viewportSize.Height);
        float best = tolerancePixels;
        int bestPriority = int.MaxValue;
        SceneEntityKey? result = null;

        foreach (SceneHitTarget target in commandBuffer.HitTargets)
        {
            float distance = target.Primitive == ScenePrimitive.Points
                ? target.Points.Min(point => Distance(clientPoint, ToClient(point, width, height)))
                : SegmentDistance(target.Points, clientPoint, width, height);
            int priority = HitPriority(target.Key.Kind);
            if (distance < best - 0.01f || (MathF.Abs(distance - best) <= 0.01f && priority < bestPriority))
            {
                best = distance;
                bestPriority = priority;
                result = target.Key;
            }
        }

        return result;
    }

    private static int HitPriority(SceneEntityKind kind) => kind switch
    {
        SceneEntityKind.Node => 0,
        SceneEntityKind.NodalLoad => 1,
        SceneEntityKind.MemberLoad => 1,
        SceneEntityKind.Support => 2,
        SceneEntityKind.Member => 3,
        _ => 4,
    };

    public static ViewportCameraState Home(ViewportSceneModel scene, ViewportProjection projection)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!Enum.IsDefined(projection))
        {
            throw new ArgumentOutOfRangeException(nameof(projection));
        }

        SceneBounds bounds = SceneBounds.From(scene);
        return new ViewportCameraState(projection, bounds.Center, bounds.Radius * 3.25f, bounds.Radius * 2.4f);
    }

    public static ViewportCameraState Fit(ViewportSceneModel scene, ViewportCameraState camera)
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

    private static float CalculateGlyphSize(ViewportSceneModel scene) => Math.Max(0.05f, SceneBounds.From(scene).Radius * 0.08f);

    private static ScenePoint3 Add(ScenePoint3 left, ScenePoint3 right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

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

    private sealed class Builder(CameraProjection projection, SceneEntityKey? selection)
    {
        private readonly List<SceneRenderVertex> _vertices = [];
        private readonly List<SceneDrawBatch> _batches = [];
        private readonly List<SceneHitTarget> _hitTargets = [];

        public void AddPoint(SceneEntityKey key, ScenePoint3 point, Color3 color, float width)
        {
            ScenePoint2 projected = projection.Project(point);
            AddBatch(ScenePrimitive.Points, [projected], key.Equals(selection) ? SelectionColor : color,
                key.Equals(selection) ? width + 4.0f : width);
            _hitTargets.Add(new SceneHitTarget(key, ScenePrimitive.Points, [projected]));
        }

        public void AddLine(SceneEntityKey key, ScenePoint3 start, ScenePoint3 end, Color3 color, float width)
        {
            ScenePoint2[] points = [projection.Project(start), projection.Project(end)];
            AddBatch(ScenePrimitive.Lines, points, key.Equals(selection) ? SelectionColor : color,
                key.Equals(selection) ? width + 2.0f : width);
            _hitTargets.Add(new SceneHitTarget(key, ScenePrimitive.Lines, points));
        }

        public void AddPolyline(SceneEntityKey key, IReadOnlyList<ScenePoint3> points, Color3 color, float width)
        {
            ScenePoint2[] projected = points.Select(projection.Project).ToArray();
            List<ScenePoint2> segments = [];
            for (int index = 0; index + 1 < projected.Length; index++)
            {
                segments.Add(projected[index]);
                segments.Add(projected[index + 1]);
            }

            AddBatch(ScenePrimitive.Lines, segments, key.Equals(selection) ? SelectionColor : color,
                key.Equals(selection) ? width + 2.0f : width);
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
            if (axis.LengthSquared() <= 1.0e-12f)
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

        public void AddResultLine(ScenePoint3 start, ScenePoint3 end, Color3 color, float width) =>
            AddBatch(ScenePrimitive.Lines, [projection.Project(start), projection.Project(end)], color, width);

        public void AddResultPoint(ScenePoint3 point, Color3 color, float width) =>
            AddBatch(ScenePrimitive.Points, [projection.Project(point)], color, width);

        public ViewportSceneCommandBuffer Build() => new(_vertices, _batches, _hitTargets);

        private void AddBatch(ScenePrimitive primitive, IEnumerable<ScenePoint2> points, Color3 color, float width)
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

    private readonly record struct CameraProjection(Matrix4x4 ViewProjection)
    {
        public static CameraProjection Create(ViewportCameraState camera, float aspectRatio)
        {
            Vector3 target = new(camera.Target.X, camera.Target.Y, camera.Target.Z);
            Vector3 direction = Vector3.Normalize(new Vector3(1.0f, -1.0f, 0.75f));
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
            float divisor = Math.Abs(clip.W) < 1.0e-6f ? 1.0f : clip.W;
            return new ScenePoint2(clip.X / divisor, clip.Y / divisor);
        }
    }

    private readonly record struct SceneBounds(ScenePoint3 Center, float Radius)
    {
        public static SceneBounds From(ViewportSceneModel scene)
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
