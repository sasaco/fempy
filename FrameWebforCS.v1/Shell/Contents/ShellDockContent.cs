using FrameWebforCS.Core.Shell;
using FrameWebforCS.Resources;
using FrameWebforCS.Shell.Docking;
using WeifenLuo.WinFormsUI.Docking;

namespace FrameWebforCS.Shell.Contents;

public interface ILocalizedShellContent
{
    void ApplyLocalization();
}

public abstract class ShellDockContent : DockContent, IKeyedDockContent, ILocalizedShellContent
{
    protected ShellDockContent(DocumentKey contentKey, LocalizationService localization)
    {
        contentKey.Validate();
        ContentKey = contentKey;
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        HideOnClose = WindowLifetimePolicy.GetCloseAction(contentKey) == WindowCloseAction.Hide;
    }

    public DocumentKey ContentKey { get; }

    protected LocalizationService Localization { get; }

    public abstract void ApplyLocalization();
}
