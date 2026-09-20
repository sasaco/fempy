using System.Threading;

namespace PDF_Manager.RendererProbe;

internal static class ProbeDiagnostics
{
    private static int _liveContexts;
    private static int _liveSubscriptions;
    private static int _liveWindows;
    private static long _contextsCreated;
    private static long _framesRendered;
    private static long _capturesCompleted;

    public static ProbeCounterSnapshot Snapshot() => new(
        Volatile.Read(ref _liveContexts),
        Volatile.Read(ref _liveSubscriptions),
        Volatile.Read(ref _liveWindows),
        Interlocked.Read(ref _contextsCreated),
        Interlocked.Read(ref _framesRendered),
        Interlocked.Read(ref _capturesCompleted));

    public static void ContextCreated()
    {
        Interlocked.Increment(ref _liveContexts);
        Interlocked.Increment(ref _contextsCreated);
    }

    public static void ContextDisposed() => Interlocked.Decrement(ref _liveContexts);

    public static void SubscriptionsAdded(int count) => Interlocked.Add(ref _liveSubscriptions, count);

    public static void SubscriptionsRemoved(int count) => Interlocked.Add(ref _liveSubscriptions, -count);

    public static void WindowOpened() => Interlocked.Increment(ref _liveWindows);

    public static void WindowClosed() => Interlocked.Decrement(ref _liveWindows);

    public static void FrameRendered() => Interlocked.Increment(ref _framesRendered);

    public static void CaptureCompleted() => Interlocked.Increment(ref _capturesCompleted);
}

internal readonly record struct ProbeCounterSnapshot(
    int LiveContexts,
    int LiveSubscriptions,
    int LiveWindows,
    long ContextsCreated,
    long FramesRendered,
    long CapturesCompleted);
