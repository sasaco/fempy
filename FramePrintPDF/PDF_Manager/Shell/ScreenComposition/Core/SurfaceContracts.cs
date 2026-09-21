using PDF_Manager.Core.Documents;
using PDF_Manager.Shell.Printing;

namespace PDF_Manager.Shell.ScreenComposition.Core;

public enum ScreenCommandKind
{
    NewProject,
    OpenProject,
    SaveProject,
    SaveProjectAs,
    ShowPreset,
    OpenPreset,
    RunAnalysis,
    CancelOperation,
    ShowPrint,
    ConfigurePrint,
    RefreshPrintPreview,
    ExportPdf,
    CloseOverlay,
    CloseRoute,
    ShowHelp,
    ShowContact,
}

public sealed class ScreenCommandRequestedEventArgs(ScreenCommandKind command) : EventArgs
{
    public ScreenCommandKind Command { get; } = command;
}

public interface IFrameWebSurface
{
    event EventHandler<ScreenCommandRequestedEventArgs>? CommandRequested;
}

public interface IPrintOverlaySurface : IFrameWebSurface
{
    PrintPageSetupSelection Selection { get; }

    void SetSelection(PrintPageSetupSelection selection);

    void SetPreview(PrintPreviewState? preview);
}

public interface IPresetOverlaySurface : IFrameWebSurface
{
    BuiltInProjectPreset? SelectedPreset { get; }
}

public interface IFloatingRouteSurface : IFrameWebSurface
{
    Control FloatingCard { get; }
}

public interface IRouteControlPanelSurface : IFrameWebSurface
{
    void SetControlPanelOpen(bool isOpen);
}

public interface IOperationOverlaySurface : IFrameWebSurface
{
    void SetMessage(string message);
}

public interface IFrameWebSurfaceFactory
{
    Control CreateRouteSurface(ScreenRouteState state);

    Control CreateOverlaySurface(ScreenRouteState state);

    void ApplyState(Control surface, ScreenRouteState state);
}
