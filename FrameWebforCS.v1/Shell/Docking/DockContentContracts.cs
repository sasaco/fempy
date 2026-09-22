using FrameWebforCS.Core.Shell;
using WeifenLuo.WinFormsUI.Docking;

namespace FrameWebforCS.Shell.Docking;

/// <summary>
/// Supplies the stable identity of a dock pane. Display text and CLR type names are never used
/// as persistence or reuse keys.
/// </summary>
public interface IKeyedDockContent
{
    DocumentKey ContentKey { get; }
}

/// <summary>Reports a shell factory that did not produce the content it registered.</summary>
public sealed class DockContentFactoryException : InvalidOperationException
{
    public DockContentFactoryException(DocumentKey key, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
    }

    public DocumentKey Key { get; }
}

public sealed class DockContentEventArgs : EventArgs
{
    public DockContentEventArgs(DocumentKey key, DockContent content)
    {
        key.Validate();
        Key = key;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public DocumentKey Key { get; }

    public DockContent Content { get; }
}

public enum DockLayoutCapacityKind
{
    Characters,
    Utf8Bytes,
    Contents,
}

/// <summary>Reports that persisted layout input exceeded a deterministic shell budget.</summary>
public sealed class DockLayoutCapacityException : FormatException
{
    public DockLayoutCapacityException(DockLayoutCapacityKind kind, int actual, int maximum)
        : base($"Dock layout {kind} count {actual} exceeds the maximum of {maximum}.")
    {
        Kind = kind;
        Actual = actual;
        Maximum = maximum;
    }

    public DockLayoutCapacityKind Kind { get; }

    public int Actual { get; }

    public int Maximum { get; }
}

/// <summary>Reports a DockPanelSuite state that cannot be represented by LayoutState v1.</summary>
public sealed class UnsupportedDockLayoutException : InvalidOperationException
{
    public UnsupportedDockLayoutException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Reports a layout apply failure together with a rollback failure. If rollback succeeds, the
/// original apply exception is rethrown unchanged.
/// </summary>
public sealed class DockLayoutTransactionException : InvalidOperationException
{
    public DockLayoutTransactionException(Exception applyFailure, Exception rollbackFailure)
        : base(
            "Dock layout restore failed and the previous layout could not be fully restored.",
            new AggregateException(applyFailure, rollbackFailure))
    {
        ApplyFailure = applyFailure ?? throw new ArgumentNullException(nameof(applyFailure));
        RollbackFailure = rollbackFailure ?? throw new ArgumentNullException(nameof(rollbackFailure));
    }

    public Exception ApplyFailure { get; }

    public Exception RollbackFailure { get; }
}
