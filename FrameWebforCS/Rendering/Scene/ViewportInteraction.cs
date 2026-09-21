namespace FrameWebforCS.Rendering.Scene;

public enum ViewportProjection
{
    Orthographic,
    Perspective,
}

public enum ViewportCameraPolicy
{
    TwoDimensional,
    ThreeDimensional,
}

public enum ViewportSelectionOrigin
{
    Programmatic,
    Table,
    Viewport,
}

public sealed class ViewportSelectionChangedEventArgs(
    SceneEntityKey? selection,
    ViewportSelectionOrigin origin) : EventArgs
{
    public SceneEntityKey? Selection { get; } = selection;

    public ViewportSelectionOrigin Origin { get; } = origin;
}

public sealed class ViewportHoverChangedEventArgs(SceneEntityKey? hover) : EventArgs
{
    public SceneEntityKey? Hover { get; } = hover;
}

public readonly record struct ViewportCameraState(
    ViewportProjection Projection,
    ScenePoint3 Target,
    float Distance,
    float OrthographicHeight);

public interface ICameraController
{
    ViewportCameraState Camera { get; }

    ViewportCameraPolicy CameraPolicy { get; }

    void SetCameraPolicy(ViewportCameraPolicy policy);

    void SetProjection(ViewportProjection projection);

    void Fit();

    void Home();
}

public sealed record SceneHitTestOptions
{
    public SceneHitTestOptions(float tolerancePixels = 9.0f, SceneLayerMask layers = SceneLayerMask.All)
    {
        if (!float.IsFinite(tolerancePixels) || tolerancePixels <= 0.0f || tolerancePixels > 256.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerancePixels));
        }

        if ((layers & ~SceneLayerMask.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layers));
        }

        TolerancePixels = tolerancePixels;
        Layers = layers;
    }

    public float TolerancePixels { get; }

    public SceneLayerMask Layers { get; }
}

public readonly record struct SceneHitTestResult(
    SceneEntityKey Key,
    SceneLayerKind Layer,
    float DistancePixels,
    int Priority);

public interface IHitTestService
{
    SceneHitTestResult? HitTest(
        ViewportSceneCommandBuffer commandBuffer,
        Point clientPoint,
        Size viewportSize,
        SceneHitTestOptions? options = null);
}

public sealed class ViewportHitTestService : IHitTestService
{
    public SceneHitTestResult? HitTest(
        ViewportSceneCommandBuffer commandBuffer,
        Point clientPoint,
        Size viewportSize,
        SceneHitTestOptions? options = null) =>
        ViewportSceneCompiler.HitTestDetailed(commandBuffer, clientPoint, viewportSize, options);
}

public sealed record ViewportPngCaptureOptions
{
    public const int MaximumDimension = 8192;
    public const int MaximumPixelCount = 16_777_216;
    public const int MaximumEncodedBytes = 64 * 1024 * 1024;

    public ViewportPngCaptureOptions(int maximumEncodedBytes = MaximumEncodedBytes)
    {
        if (maximumEncodedBytes <= 0 || maximumEncodedBytes > MaximumEncodedBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEncodedBytes));
        }

        MaximumBytes = maximumEncodedBytes;
    }

    public int MaximumBytes { get; }
}
