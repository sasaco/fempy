using FrameWebforCsharp.Core.Documents;

namespace FrameWebforCsharp.Shell.Lifecycle;

public sealed record ShellCommandContext(
    bool HasDocument,
    bool IsDocumentDirty,
    bool HasAnalysisResults,
    bool IsOperationRunning)
{
    public static ShellCommandContext Create(
        ProjectDocument? document,
        bool hasAnalysisResults,
        bool isOperationRunning)
        => new(document is not null, document?.IsDirty == true, hasAnalysisResults, isOperationRunning);
}

public sealed record ShellCommandState(
    bool CanCreate,
    bool CanOpen,
    bool CanSave,
    bool CanSaveAs,
    bool CanAnalyze,
    bool CanPrint,
    bool CanCloseDocument,
    bool CanCancel);

public static class ShellCommandStateReducer
{
    public static ShellCommandState Reduce(ShellCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        bool idle = !context.IsOperationRunning;
        return new ShellCommandState(
            CanCreate: idle,
            CanOpen: idle,
            CanSave: idle && context.HasDocument && context.IsDocumentDirty,
            CanSaveAs: idle && context.HasDocument,
            CanAnalyze: idle && context.HasDocument,
            CanPrint: idle && context.HasDocument && context.HasAnalysisResults,
            CanCloseDocument: context.HasDocument,
            CanCancel: context.IsOperationRunning);
    }
}
