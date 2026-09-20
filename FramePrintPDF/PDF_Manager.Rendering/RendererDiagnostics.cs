using System.Threading;

namespace PDF_Manager.Rendering;

public static class RendererDiagnostics
{
    private static int _liveContexts;
    private static int _liveSubscriptions;
    private static long _contextsCreated;
    private static long _framesRendered;
    private static long _capturesCompleted;
    private static long _layerCompilations;
    private static long _invalidationRequests;
    private static long _invalidationBatches;

    public static RendererDiagnosticSnapshot Snapshot() => new(
        Volatile.Read(ref _liveContexts),
        Volatile.Read(ref _liveSubscriptions),
        Interlocked.Read(ref _contextsCreated),
        Interlocked.Read(ref _framesRendered),
        Interlocked.Read(ref _capturesCompleted),
        Interlocked.Read(ref _layerCompilations),
        Interlocked.Read(ref _invalidationRequests),
        Interlocked.Read(ref _invalidationBatches));

    internal static void ContextCreated()
    {
        Interlocked.Increment(ref _liveContexts);
        Interlocked.Increment(ref _contextsCreated);
    }

    internal static void ContextDisposed() => Interlocked.Decrement(ref _liveContexts);

    internal static void SubscriptionsAdded(int count) => Interlocked.Add(ref _liveSubscriptions, count);

    internal static void SubscriptionsRemoved(int count) => Interlocked.Add(ref _liveSubscriptions, -count);

    internal static void FrameRendered() => Interlocked.Increment(ref _framesRendered);

    internal static void CaptureCompleted() => Interlocked.Increment(ref _capturesCompleted);

    internal static void LayerCompiled() => Interlocked.Increment(ref _layerCompilations);

    internal static void InvalidationRequested() => Interlocked.Increment(ref _invalidationRequests);

    internal static void InvalidationBatchConsumed() => Interlocked.Increment(ref _invalidationBatches);
}

public readonly record struct RendererDiagnosticSnapshot(
    int LiveContexts,
    int LiveSubscriptions,
    long ContextsCreated,
    long FramesRendered,
    long CapturesCompleted,
    long LayerCompilations,
    long InvalidationRequests,
    long InvalidationBatches);
