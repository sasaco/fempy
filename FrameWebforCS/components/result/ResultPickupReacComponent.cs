namespace FrameWebforCS.components.result;

internal sealed class ResultPickupReacComponent : ResultPickupTableComponent<ResultCombineReacSnapshot>
{
    public ResultPickupReacComponent() : base(
        () => ResultCombineReacCoordinator.Instance.Snapshot,
        () => ResultCombineReacCoordinator.Instance.Revision,
        handler => ResultCombineReacCoordinator.Instance.Changed += handler,
        handler => ResultCombineReacCoordinator.Instance.Changed -= handler,
        ResultPickupReacAggregator.Calculate,
        ResultPickupAggregator.Reac3D, ResultPickupAggregator.Reac2D,
        ["節点 No", "X方向 支点反力(kN)", "Y方向 支点反力(kN)", "Z方向 支点反力(kN)",
            "X軸回り 回転反力(kN・m)", "Y軸回り 回転反力(kN・m)",
            "Z軸回り 回転反力(kN・m)", "組み合わせ"],
        ["節点 No", "X方向 支点反力(kN)", "Y方向 支点反力(kN)",
            "Z軸回り 回転反力(kN・m)", "組み合わせ"])
    { }
}
