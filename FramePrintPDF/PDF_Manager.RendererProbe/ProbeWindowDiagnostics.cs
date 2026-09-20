using System.Threading;

namespace PDF_Manager.RendererProbe;

internal static class ProbeWindowDiagnostics
{
    private static int _liveWindows;

    public static int LiveWindows => Volatile.Read(ref _liveWindows);

    public static void WindowOpened() => Interlocked.Increment(ref _liveWindows);

    public static void WindowClosed() => Interlocked.Decrement(ref _liveWindows);
}
