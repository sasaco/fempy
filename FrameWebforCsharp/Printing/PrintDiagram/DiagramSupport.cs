using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 全ての支点を描画する
    /// </summary>
    internal class DiagramSupport
    {
        /// <summary>
        /// 支点関連テキストの描画色
        /// </summary>
        private static readonly XBrush SupportTextColor = XBrushes.Black;
        /// <summary>
        /// 支点の描画色
        /// </summary>
        private static readonly XColor SupportPenColor = XColors.Black;
        /// <summary>
        /// 支点の描画線の太さ(単位はポイント)
        /// </summary>
        private const double SupportPenWidth = 0.1;

        /// <summary>
        /// 支点描画の準備
        /// </summary>
        /// <param name="fixNodes">JSONファイルから読み込んだ支点情報のコレクション</param>
        /// <param name="nodeDic">節点情報の辞書</param>
        /// <param name="topLeft">描画領域の左上角の座標(y軸の方向が逆なので実際は左下角)</param>
        /// <param name="bottomRight">描画領域の右下角の座標(y軸の方向が逆なので実際は右上角)</param>
        public DiagramSupport(IReadOnlyCollection<FixNode> fixNodes, IReadOnlyDictionary<string, XNode> nodeDic, XPoint topLeft, XPoint bottomRight)
        {
            supportList.Clear();

            foreach (var fixNode in fixNodes)
            {
                supportList.Add(new XSupport(fixNode, nodeDic, topLeft, bottomRight));
            }

            // XCanvasへの描画

            if (!supportList.Any())
            {
                return;
            }

            canvas = new XCanvas();

            foreach (var support in supportList)
            {
                support.Print(canvas);
            }
        }

        /// <summary>
        /// 支点の描画データを加味して描画領域を再設定
        /// </summary>
        /// <param name="topLeft">描画領域の左上角の座標(y軸の方向が逆なので実際は左下角)</param>
        /// <param name="bottomRight">描画領域の右下角の座標(y軸の方向が逆なので実際は右上角)</param>
        public void AdjustDiagramRect(ref XPoint topLeft, ref XPoint bottomRight)
        {
            var supports = supportList.Where(s => s.Type != XSupport.Types.None);
            if (supports.Any())
            {
                topLeft.X = Math.Min(topLeft.X, supports.Min(s => s.TopLeft.X));
                topLeft.Y = Math.Max(topLeft.Y, supports.Max(s => s.TopLeft.Y));
                bottomRight.X = Math.Max(bottomRight.X, supports.Max(s => s.BottomRight.X));
                bottomRight.Y = Math.Min(bottomRight.Y, supports.Min(s => s.BottomRight.Y));
            }
        }

        /// <summary>
        /// 支点をPDF出力する
        /// </summary>
        /// <param name="frame"></param>
        public void Print(DiagramFrame frame) => canvas?.Print(frame, SupportPenColor, SupportPenWidth, SupportTextColor);

        private readonly List<XSupport> supportList = new List<XSupport>();
        private readonly XCanvas canvas;
    }
}
