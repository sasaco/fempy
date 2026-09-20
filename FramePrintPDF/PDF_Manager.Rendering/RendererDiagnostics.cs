using System.Threading;

namespace PDF_Manager.Rendering;

public static class RendererDiagnostics
{
    private static int _liveContexts;
    private static int _liveSubscriptions;
    private static long _contextsCreated;
    private static long _framesRendered;
    private static long _capturesCompleted;

    public static RendererDiagnosticSnapshot Snapshot() => new(
        Volatile.Read(ref _liveContexts),
        Volatile.Read(ref _liveSubscriptions),
        Interlocked.Read(ref _contextsCreated),
        Interlocked.Read(ref _framesRendered),
        Interlocked.Read(ref _capturesCompleted));

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
}

public readonly record struct RendererDiagnosticSnapshot(
    int LiveContexts,
    int LiveSubscriptions,
    long ContextsCreated,
    long FramesRendered,
    long CapturesCompleted);
