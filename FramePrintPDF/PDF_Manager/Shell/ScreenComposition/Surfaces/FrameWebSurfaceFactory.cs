using PDF_Manager.Resources;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.ScreenComposition.Core;

namespace PDF_Manager.Shell.ScreenComposition.Surfaces;

public sealed class FrameWebSurfaceFactory : IFrameWebSurfaceFactory
{
    private readonly LocalizationService _localization;
    private readonly EditorContent _editor;
    private readonly ProjectDocumentContent _documentHost;

    public FrameWebSurfaceFactory(
        LocalizationService localization,
        EditorContent editor,
        ProjectDocumentContent documentHost)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _documentHost = documentHost ?? throw new ArgumentNullException(nameof(documentHost));
    }

    public Control CreateRouteSurface(ScreenRouteState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ScreenRouteId route = state.Route
            ?? throw new ArgumentException("An active route is required.", nameof(state));
        Control surface = AngularScreenManifest.IsResultRoute(route)
            ? new ResultRouteSurfaceControl(_localization, _documentHost)
            : new InputRouteSurfaceControl(_localization, _editor);
        ApplyState(surface, state);
        return surface;
    }

    public Control CreateOverlaySurface(ScreenRouteState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Control surface = state.Overlay switch
        {
            ScreenOverlayKind.Start => new StartOverlayControl(_localization),
            ScreenOverlayKind.Preset => new PresetOverlayControl(_localization),
            ScreenOverlayKind.Print => new PrintOverlayControl(_localization),
            ScreenOverlayKind.Wait or ScreenOverlayKind.Confirm or ScreenOverlayKind.Alert =>
                new OperationOverlayControl(_localization, state.Overlay),
            ScreenOverlayKind.None => throw new ArgumentException("An active overlay is required.", nameof(state)),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Overlay, "Unknown overlay kind."),
        };
        ApplyState(surface, state);
        return surface;
    }

    public void ApplyState(Control surface, ScreenRouteState state)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(state);
        switch (surface)
        {
            case InputRouteSurfaceControl input:
                input.ApplyState(state);
                break;
            case ResultRouteSurfaceControl result:
                result.ApplyState(state);
                break;
            case OperationOverlayControl operation:
                operation.ApplyKind(state.Overlay);
                break;
            case StartOverlayControl when state.Overlay == ScreenOverlayKind.Start:
            case PresetOverlayControl when state.Overlay == ScreenOverlayKind.Preset:
            case PrintOverlayControl when state.Overlay == ScreenOverlayKind.Print:
                break;
            case IFrameWebSurface:
                throw new ArgumentException("The state does not match the supplied surface.", nameof(state));
            default:
                throw new ArgumentException("The control is not a FrameWeb screen surface.", nameof(surface));
        }
    }
}
