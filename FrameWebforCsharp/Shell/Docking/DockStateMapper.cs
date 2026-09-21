using CoreDockState = FrameWebforCsharp.Core.Shell.DockState;
using DockPanelState = WeifenLuo.WinFormsUI.Docking.DockState;

namespace FrameWebforCsharp.Shell.Docking;

internal static class DockStateMapper
{
    internal static DockPanelState ToDockPanelState(CoreDockState state) => state switch
    {
        CoreDockState.Document => DockPanelState.Document,
        CoreDockState.DockLeft => DockPanelState.DockLeft,
        CoreDockState.DockRight => DockPanelState.DockRight,
        CoreDockState.DockTop => DockPanelState.DockTop,
        CoreDockState.DockBottom => DockPanelState.DockBottom,
        CoreDockState.Float => DockPanelState.Float,
        CoreDockState.Hidden => DockPanelState.Hidden,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported core dock state."),
    };

    internal static CoreDockState ToCoreState(DockPanelState state) => state switch
    {
        DockPanelState.Document => CoreDockState.Document,
        DockPanelState.DockLeft => CoreDockState.DockLeft,
        DockPanelState.DockRight => CoreDockState.DockRight,
        DockPanelState.DockTop => CoreDockState.DockTop,
        DockPanelState.DockBottom => CoreDockState.DockBottom,
        DockPanelState.Float => CoreDockState.Float,
        DockPanelState.Hidden or DockPanelState.Unknown => CoreDockState.Hidden,
        DockPanelState.DockLeftAutoHide or
        DockPanelState.DockRightAutoHide or
        DockPanelState.DockTopAutoHide or
        DockPanelState.DockBottomAutoHide => throw new UnsupportedDockLayoutException(
            "LayoutState v1 does not persist DockPanelSuite auto-hide states."),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported DockPanelSuite state."),
    };
}
