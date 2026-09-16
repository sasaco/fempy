namespace PDF_Manager.Printing
{
    /// <summary>
    /// 部材に対する荷重および荷重寸法の表示位置関係
    /// </summary>
    public enum XLocationType
    {
        /// <summary>
        /// 未設定
        /// </summary>
        None,

        /// <summary>
        /// 全体座標系水平方向右側
        /// </summary>
        GR,
        /// <summary>
        /// 全体座標系水平方向左側
        /// </summary>
        GL,
        /// <summary>
        /// 全体座標系鉛直方向上側
        /// </summary>
        GU,
        /// <summary>
        /// 全体座標系鉛直方向下側
        /// </summary>
        GD,

        /// <summary>
        /// 部材座標系Y軸負側(部材軸を+90°回転させた時のi端側)
        /// </summary>
        YM,
        /// <summary>
        /// 部材座標系Y軸正側(部材軸を+90°回転させた時のj端側)
        /// </summary>
        YP,

        /// <summary>
        /// 部材軸上(モーメント荷重用)
        /// </summary>
        C,
    }
}
