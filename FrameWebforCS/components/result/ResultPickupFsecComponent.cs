namespace FrameWebforCS.components.result;

internal sealed class ResultPickupFsecComponent : ResultPickupTableComponent<ResultCombineFsecSnapshot>
{
    public ResultPickupFsecComponent() : base(
        () => ResultCombineFsecCoordinator.Instance.Snapshot,
        () => ResultCombineFsecCoordinator.Instance.Revision,
        handler => ResultCombineFsecCoordinator.Instance.Changed += handler,
        handler => ResultCombineFsecCoordinator.Instance.Changed -= handler,
        ResultPickupFsecAggregator.Calculate,
        ResultPickupAggregator.Fsec3D, ResultPickupAggregator.Fsec2D,
        ["部材 No", "節点 No", "着目位置 (m)", "軸方向力 (kN)", "Y方向せん断力 (kN)",
            "Z方向せん断力 (kN)", "ねじりモーメント (kN・m)",
            "Y軸回り曲げモーメント (kN・m)", "Z軸回り曲げモーメント (kN・m)", "組み合わせ"],
        ["部材 No", "節点 No", "着目位置 (m)", "軸方向力 (kN)",
            "せん断力 (kN)", "曲げモーメント (kN・m)", "組み合わせ"])
    { }
}
