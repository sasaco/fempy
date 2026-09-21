using PdfSharpCore.Drawing;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 支点、バネ、寸法、節点荷重および部材荷重の描画に使用されるメソッドのインタフェースを定義
    /// </summary>
    internal interface ICanvas
    {
        /// <summary>
        /// 2点間を結ぶ直線を描画する
        /// </summary>
        /// <param name="pi">始点の座標</param>
        /// <param name="pj">終点の座標</param>
        void LineBetween(XPoint pi, XPoint pj);
        /// <summary>
        /// 2点間を結ぶ波線を描画する
        /// </summary>
        /// <param name="pi">始点の座標</param>
        /// <param name="pj">終点の座標</param>
        /// <param name="dashPattern">破線パターン</param>
        void DashedLineBetween(XPoint pi, XPoint pj, double[] dashPattern);
        /// <summary>
        /// テキストを描画する
        /// </summary>
        /// <param name="text">描画対象のテキスト</param>
        /// <param name="pos">描画位置の座標</param>
        /// <param name="angle">テキストの描画角度(単位は度)</param>
        /// <param name="angle">テキストの描画角度(単位は度)。デフォルトは0。3時の方向が0で、反時計回りが正</param>
        /// <param name="align">描画位置とテキストの位置関係。デフォルトはBottomLeft</param>
        void TextAt(string text, XPoint pos, double angle = 0, XStringFormat align = null);
        /// <summary>
        /// 円弧を描画する
        /// </summary>
        /// <param name="center">円弧の中心座標</param>
        /// <param name="radius">円弧の半径(単位はポイント)</param>
        /// <param name="startAngle">円弧の描画開始角度(単位は度)。3時の方向が0で、反時計回りが正</param>
        /// <param name="sweepAngle">円弧の描画角度(単位は度)。反時計回りが正</param>
        void ArcAt(XPoint center, double radius, double startAngle, double sweepAngle);
    }
}
