using FrameWebforCS.Core.Abstractions;
using FrameWebforCS.Core.Documents;
using FrameWebforCS.Core.Shell;

namespace FrameWebforCS.Shell.Lifecycle;

public enum UserSafeFailureKind
{
    Validation,
    Operation,
    Unexpected,
}

public sealed record UserSafeExceptionInfo(UserSafeFailureKind Kind, string Message);

/// <summary>
/// Converts command failures into user-safe notifications while sending the original exception to
/// an optional diagnostics hook. Cancellation is suppressed only when the boundary's expected
/// request token has actually been cancelled.
/// </summary>
public sealed class UserExceptionBoundary
{
    private const string DefaultUnexpectedMessage = "The operation could not be completed.";
    private readonly Action<UserSafeExceptionInfo> notifyUser;
    private readonly Action<Exception>? reportDiagnostic;
    private readonly Func<Exception, UserSafeExceptionInfo> mapException;

    public UserExceptionBoundary(
        Action<UserSafeExceptionInfo> notifyUser,
        Action<Exception>? reportDiagnostic = null,
        Func<Exception, UserSafeExceptionInfo>? mapException = null)
    {
        this.notifyUser = notifyUser ?? throw new ArgumentNullException(nameof(notifyUser));
        this.reportDiagnostic = reportDiagnostic;
        this.mapException = mapException ?? Map;
    }

    public bool Execute(Action action) => Execute(action, CancellationToken.None);

    public bool Execute(Action action, CancellationToken expectedCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
            return true;
        }
        catch (OperationCanceledException) when (expectedCancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            Report(exception);
            return false;
        }
    }

    public async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            Report(exception);
            return false;
        }
    }

    public static UserSafeExceptionInfo Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            CoreOperationException operation => new(UserSafeFailureKind.Operation, operation.UserMessage),
            ProjectDocumentValidationException validation => new(UserSafeFailureKind.Validation, validation.Message),
            ContractValidationException validation => new(UserSafeFailureKind.Validation, validation.Message),
            _ => new(UserSafeFailureKind.Unexpected, DefaultUnexpectedMessage),
        };
    }

    private void Report(Exception exception)
    {
        reportDiagnostic?.Invoke(exception);
        notifyUser(mapException(exception));
    }
}
