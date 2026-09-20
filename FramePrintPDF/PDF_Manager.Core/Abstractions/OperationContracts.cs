using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Abstractions;

public enum OperationFailureKind
{
    Validation,
    Unavailable,
    Unauthorized,
    Timeout,
    Protocol,
    Internal,
}

public abstract class CoreOperationException : Exception
{
    protected CoreOperationException(
        OperationFailureKind failureKind,
        string userMessage,
        Exception? innerException = null)
        : base(userMessage, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        FailureKind = failureKind;
        UserMessage = userMessage;
    }

    public OperationFailureKind FailureKind { get; }

    public string UserMessage { get; }
}

public sealed class AnalysisClientException : CoreOperationException
{
    public AnalysisClientException(
        OperationFailureKind failureKind,
        string userMessage,
        Exception? innerException = null)
        : base(failureKind, userMessage, innerException)
    {
    }
}

public sealed class PrintExportException : CoreOperationException
{
    public PrintExportException(
        OperationFailureKind failureKind,
        string userMessage,
        Exception? innerException = null)
        : base(failureKind, userMessage, innerException)
    {
    }
}

public sealed class PrintExportRequest
{
    public PrintExportRequest(
        ProjectDocument document,
        AnalysisResultSet? resultSet,
        IEnumerable<ResultCoordinate>? selectedResults = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        ProjectDocumentValidator.Validate(document);
        ResultSet = resultSet;
        if (resultSet is not null)
        {
            AnalysisResultSetValidator.Validate(resultSet);
        }

        ResultCoordinate[] selection = selectedResults?.ToArray() ?? [];
        if (resultSet is null && selection.Length > 0)
        {
            throw new ArgumentException("Selected results require an AnalysisResultSet.", nameof(selectedResults));
        }

        if (resultSet is not null)
        {
            ResultIndex index = new(resultSet);
            foreach (ResultCoordinate coordinate in selection)
            {
                if (!index.TryGet(coordinate, out _))
                {
                    throw new ArgumentException(
                        $"Selected result '{coordinate}' is not present in the result set.",
                        nameof(selectedResults));
                }
            }
        }

        SelectedResults = Array.AsReadOnly(selection);
    }

    public ProjectDocument Document { get; }

    public AnalysisResultSet? ResultSet { get; }

    public IReadOnlyList<ResultCoordinate> SelectedResults { get; }
}
