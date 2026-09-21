using PDF_Manager.Core.Results;

namespace PDF_Manager.Shell.Viewport;

public sealed record ResultPresentationUiError(
    ResultPresentationErrorCode Code,
    string ResourceKey,
    string Message,
    Exception Exception);

public sealed class ResultExportOperationException : Exception
{
    public const string ExportFailureResourceKey = "ResultExportFailed";

    public ResultExportOperationException(Exception innerException)
        : base("Result export failed.", innerException ?? throw new ArgumentNullException(nameof(innerException)))
    {
    }

    public string ResourceKey => ExportFailureResourceKey;
}
