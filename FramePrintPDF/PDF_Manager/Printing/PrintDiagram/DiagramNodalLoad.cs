using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 全ての節点荷重の描画データの生成と描画
    /// </summary>
    internal class DiagramNodalLoad
    {
        /// <summary>
        /// 節点荷重と荷重寸法の描画色
        /// </summary>
        private static readonly XColor NodalLoadPenColor = XColors.Black;
        /// <summary>
        /// 節点荷重と荷重寸法のテキスト描画色
        /// </summary>
        private static readonly XBrush NodalLoadTextColor = XBrushes.Black;
        /// <summary>
        /// 節点荷重と荷重寸法の描画線の太さ
        /// </summary>
        private const double NodalLoadPenWidth = 0.1;

        /// <summary>
        /// 全ての節点荷重の描画データを生成する
        /// </summary>
        /// <param name="nodalLoads">JSONファイルから読み込んだ節点荷重情報のコレクション</param>
        /// <param name="nodeDic">節点情報の辞書</param>
        /// <param name="topLeft">描画領域左上角の座標</param>
        /// <param name="bottomRight">描画領域右下角の座標</param>
        /// <param name="isOlderVer2">JSONファイルから読み込んだデータ中のY座標を補正するための係数</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        public DiagramNodalLoad(IReadOnlyCollection<LoadNode> nodalLoads, IReadOnlyDictionary<string, XNode> nodeDic, XPoint topLeft, XPoint bottomRight, int isOlderVer2,
            XFont font, Func<string, XFont, XSize> measureString)
        {
            this.nodeDic = nodeDic;

            foreach (var node in nodeDic.Values)
            {
                node.ClearNodalLoads();
            }

            if (nodalLoads is null || !nodalLoads.Any())
            {
                return;
            }

            // 実物の長辺を200mm四方に縮小した状態における1pt相当の長さ(単位は実物と同じ)
            var coef = Math.Max(bottomRight.X - topLeft.X, topLeft.Y - bottomRight.Y) / (200 * XUnit.FromMillimeter(1));

            foreach (var nodalLoad in nodalLoads)
            {
                if (!nodeDic.TryGetValue(nodalLoad.n, out var node))
                {
                    continue;
                }

                var fx = nodalLoad.tx; // 水平方向にかかる力
                if (!double.IsNaN(fx) && fx != 0)
                {
                    node.AddNodalLoad(new XHorizontalNodalLoad(node, fx, coef, font, measureString));
                }

                var fy = nodalLoad.ty; // 鉛直方向にかかる力
                if (!double.IsNaN(fy) && fy != 0)
                {
                    fy *= isOlderVer2; // 符号の補正
                    node.AddNodalLoad(new XVerticalNodalLoad(node, fy, coef, font, measureString));
                }

                var m = nodalLoad.rz; // モーメント
                if (!double.IsNaN(m) && m != 0)
                {
                    node.AddNodalLoad(new XMomentNodalLoad(node, m, coef, font, measureString));
                }
            }

            // XCanvasへの描画

            var xnodalLoads = nodeDic.Values.SelectMany(m => m.NodalLoads);
            if (!xnodalLoads.Any())
            {
                return;
            }

            canvas = new XCanvas();

            foreach (var nodalLoad in xnodalLoads)
            {
                nodalLoad.Print(canvas);
            }
        }

        /// <summary>
        /// XCanvasクラスインスタンスが保持している節点荷重の描画データによって描画される領域の左上と右下の座標を計算する
        /// </summary>
        /// <param name="topLeft">全ての節点荷重を含む描画領域の左上角の座標</param>
        /// <param name="bottomRight">全ての節点荷重を含む描画領域の右下角の座標</param>
        public void AdjustDiagramRect(ref XPoint topLeft, ref XPoint bottomRight)
        {
            var nodalLoads = nodeDic.Values.SelectMany(m => m.NodalLoads);
            if (nodalLoads.Any())
            {
                topLeft.X = Math.Min(topLeft.X, nodalLoads.Min(s => s.TopLeft.X));
                topLeft.Y = Math.Max(topLeft.Y, nodalLoads.Max(s => s.TopLeft.Y));
                bottomRight.X = Math.Max(bottomRight.X, nodalLoads.Max(s => s.BottomRight.X));
                bottomRight.Y = Math.Min(bottomRight.Y, nodalLoads.Min(s => s.BottomRight.Y));
            }
        }

        /// <summary>
        /// XCanvasクラスインスタンスが保持している節点荷重の描画データを、描画スケールや中心座標を反映させてPDF出力する
        /// </summary>
        /// <param name="frame"></param>
        public void Print(DiagramFrame frame) => canvas?.Print(frame, NodalLoadPenColor, NodalLoadPenWidth, NodalLoadTextColor);

        private readonly IReadOnlyDictionary<string, XNode> nodeDic;
        private readonly XCanvas canvas;
    }
}
