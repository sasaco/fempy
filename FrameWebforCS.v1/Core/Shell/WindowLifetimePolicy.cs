namespace FrameWebforCS.Core.Shell;

public enum WindowCloseAction
{
    Hide = 1,
    Dispose = 2,
}

public static class WindowLifetimePolicy
{
    public static WindowCloseAction GetCloseAction(DocumentKey key)
    {
        key.Validate();
        return key.Kind switch
        {
            DocumentKind.Tool => WindowCloseAction.Hide,
            DocumentKind.Document => WindowCloseAction.Dispose,
            _ => throw new ContractValidationException(
                ContractError.UnknownDocumentKind,
                $"Document kind '{key.Kind}' has no close policy."),
        };
    }
}
