namespace PDF_Manager.Shell.Viewport;

[Flags]
public enum ViewportUpdateReason
{
    None = 0,
    Document = 1 << 0,
    Result = 1 << 1,
    Presentation = 1 << 2,
    Selection = 1 << 3,
    Resize = 1 << 4,
}

/// <summary>
/// Coalesces rapid viewport updates on the WinForms creating thread.
/// </summary>
public sealed class ViewportUpdateScheduler : IDisposable
{
    private readonly int _creatingThreadId = Environment.CurrentManagedThreadId;
    private readonly Action<ViewportUpdateReason> _dispatch;
    private ViewportUpdateReason _pending;
    private bool _disposed;

    public ViewportUpdateScheduler(Action<ViewportUpdateReason> dispatch, int intervalMilliseconds = 16)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (intervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));
        }

        _dispatch = dispatch;
    }

    public ViewportUpdateReason Pending => _pending;

    public int DispatchCount { get; private set; }

    public int CoalescedRequestCount { get; private set; }

    public void Schedule(ViewportUpdateReason reason)
    {
        VerifyAccess();
        ThrowIfDisposed();
        if (reason == ViewportUpdateReason.None)
        {
            return;
        }

        if (_pending != ViewportUpdateReason.None)
        {
            CoalescedRequestCount++;
        }

        _pending |= reason;
    }

    public void Flush()
    {
        VerifyAccess();
        ThrowIfDisposed();
        if (_pending == ViewportUpdateReason.None)
        {
            return;
        }

        ViewportUpdateReason pending = _pending;
        _pending = ViewportUpdateReason.None;
        DispatchCount++;
        _dispatch(pending);
    }

    public void Dispose()
    {
        VerifyAccess();
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pending = ViewportUpdateReason.None;
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != _creatingThreadId)
        {
            throw new InvalidOperationException("Viewport updates must run on the creating UI thread.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
