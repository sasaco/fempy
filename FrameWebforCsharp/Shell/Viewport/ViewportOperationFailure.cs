namespace PDF_Manager.Shell.Viewport;

public enum ViewportOperation
{
    SceneUpdate,
    PngCapture,
}

public sealed class ViewportOperationException : Exception
{
    public ViewportOperationException(
        ViewportOperation operation,
        string safeMessage,
        Exception innerException,
        bool isExpected)
        : base($"Viewport operation '{operation}' failed.", innerException)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Operation = operation;
        SafeMessage = safeMessage;
        IsExpected = isExpected;
    }

    public ViewportOperation Operation { get; }

    public string SafeMessage { get; }

    public bool IsExpected { get; }
}

public sealed class ViewportOperationFailedEventArgs(ViewportOperationException failure) : EventArgs
{
    public ViewportOperationException Failure { get; } =
        failure ?? throw new ArgumentNullException(nameof(failure));
}
