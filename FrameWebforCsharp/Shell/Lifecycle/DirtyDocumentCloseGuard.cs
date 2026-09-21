using FrameWebforCsharp.Core.Documents;

namespace FrameWebforCsharp.Shell.Lifecycle;

public enum DirtyDocumentCloseDecision
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Coordinates dirty-document confirmation without owning any WinForms dialogs.</summary>
public sealed class DirtyDocumentCloseGuard
{
    private readonly Func<ProjectDocument, DirtyDocumentCloseDecision> confirmClose;
    private readonly Func<ProjectDocument, CancellationToken, Task> saveDocument;

    public DirtyDocumentCloseGuard(
        Func<ProjectDocument, DirtyDocumentCloseDecision> confirmClose,
        Func<ProjectDocument, CancellationToken, Task> saveDocument)
    {
        this.confirmClose = confirmClose ?? throw new ArgumentNullException(nameof(confirmClose));
        this.saveDocument = saveDocument ?? throw new ArgumentNullException(nameof(saveDocument));
    }

    public async Task<bool> CanCloseAsync(
        ProjectDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.IsDirty)
        {
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DirtyDocumentCloseDecision decision = confirmClose(document);
        switch (decision)
        {
            case DirtyDocumentCloseDecision.Save:
                await saveDocument(document, cancellationToken);
                return true;
            case DirtyDocumentCloseDecision.Discard:
                return true;
            case DirtyDocumentCloseDecision.Cancel:
                return false;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown close decision.");
        }
    }
}
