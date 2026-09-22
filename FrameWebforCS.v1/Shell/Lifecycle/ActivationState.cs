using FrameWebforCS.Core.Shell;

namespace FrameWebforCS.Shell.Lifecycle;

public enum ActivationPhase
{
    Idle,
    Pending,
    Active,
    Cancelled,
    Faulted,
}

/// <summary>Immutable state for a keyed shell activation.</summary>
public sealed record ActivationState(
    long Revision,
    DocumentKey? RequestedKey,
    DocumentKey? ActiveKey,
    ActivationPhase Phase,
    Exception? LastError)
{
    public static ActivationState Empty { get; } = new(0, null, null, ActivationPhase.Idle, null);
}

/// <summary>
/// Pure activation transitions. Completion for an obsolete revision is ignored so a cancelled
/// request can never overwrite the state of a newer request.
/// </summary>
public static class ActivationReducer
{
    public static ActivationState Request(ActivationState state, DocumentKey key)
    {
        ArgumentNullException.ThrowIfNull(state);
        key.Validate();
        return new ActivationState(
            checked(state.Revision + 1),
            key,
            state.ActiveKey,
            ActivationPhase.Pending,
            null);
    }

    public static ActivationState Complete(ActivationState state, long revision, DocumentKey key)
    {
        ArgumentNullException.ThrowIfNull(state);
        key.Validate();
        return IsCurrent(state, revision, key)
            ? state with
            {
                RequestedKey = null,
                ActiveKey = key,
                Phase = ActivationPhase.Active,
                LastError = null,
            }
            : state;
    }

    public static ActivationState Cancel(ActivationState state, long revision, DocumentKey key)
    {
        ArgumentNullException.ThrowIfNull(state);
        key.Validate();
        return IsCurrent(state, revision, key)
            ? state with
            {
                RequestedKey = null,
                Phase = ActivationPhase.Cancelled,
                LastError = null,
            }
            : state;
    }

    public static ActivationState Fail(ActivationState state, long revision, DocumentKey key, Exception error)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(error);
        key.Validate();
        return IsCurrent(state, revision, key)
            ? state with
            {
                RequestedKey = null,
                Phase = ActivationPhase.Faulted,
                LastError = error,
            }
            : state;
    }

    /// <summary>
    /// Invalidates all outstanding revisions and removes both the requested and active target.
    /// </summary>
    public static ActivationState Clear(ActivationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new ActivationState(
            checked(state.Revision + 1),
            null,
            null,
            ActivationPhase.Idle,
            null);
    }

    private static bool IsCurrent(ActivationState state, long revision, DocumentKey key)
        => state.Revision == revision && state.RequestedKey == key;
}
