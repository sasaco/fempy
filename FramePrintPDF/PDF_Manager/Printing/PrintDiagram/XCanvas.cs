using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// ICanvasインタフェースのメソッド定義とPDF出力メソッドの定義
    /// </summary>
    internal class XCanvas : ICanvas
    {
        /// <summary>
        /// 2点間を結ぶ直線を描画する
        /// </summary>
        /// <param name="pi">始点の座標</param>
        /// <param name="pj">終点の座標</param>
        public void LineBetween(XPoint pi, XPoint pj) => lineList.Add((pi, pj));
        /// <summary>
        /// 2点間を結ぶ波線を描画する
        /// </summary>
        /// <param name="pi">始点の座標</param>
        /// <param name="pj">終点の座標</param>
        /// <param name="dashPattern">破線パターン</param>
        public void DashedLineBetween(XPoint pi, XPoint pj, double[] dashPattern) => dashedLineList.Add((pi, pj, dashPattern));
        /// <summary>
        /// テキストを描画する
        /// </summary>
        /// <param name="text">描画対象のテキスト</param>
        /// <param name="pos">描画位置の座標</param>
        /// <param name="angle">テキストの描画角度(単位は度)</param>
        /// <param name="angle">テキストの描画角度(単位は度)。デフォルトは0。3時の方向が0で、反時計回りが正</param>
        /// <param name="align">描画位置とテキストの位置関係。デフォルトはBottomLeft</param>
        public void TextAt(string text, XPoint pos, double angle = 0, XStringFormat align = null) => textList.Add((text, pos, angle, align));
        /// <summary>
        /// 円弧を描画する
        /// </summary>
        /// <param name="center">円弧の中心座標</param>
        /// <param name="radius">円弧の半径(単位はポイント)</param>
        /// <param name="startAngle">円弧の描画開始角度(単位は度)。3時の方向が0で、反時計回りが正</param>
        /// <param name="sweepAngle">円弧の描画角度(単位は度)。反時計回りが正</param>
        public void ArcAt(XPoint center, double radius, double startAngle, double sweepAngle) => arcList.Add((center, radius, startAngle, sweepAngle));

        /// <summary>
        /// 全ての描画データをPDF出力する
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="penColor">図形の描画色</param>
        /// <param name="penWidth">図形の描画線の太さ(単位はポイント)</param>
        /// <param name="textColor">テキストの描画色</param>
        public void Print(DiagramFrame frame, XColor penColor, double penWidth, XBrush textColor)
        {
            var bkup = frame.canvas.mc.xpen;
            try
            {
                frame.canvas.mc.xpen = new XPen(penColor, penWidth);

                Print(frame.canvas, frame.ScaleX, frame.ScaleY, frame.CenterPos, frame.canvas.mc.font_got, textColor);
            }
            finally
            {
                frame.canvas.mc.xpen = bkup;
            }
        }

        private void Print(diagramManager canvas, double scaleX, double scaleY, XPoint centerPos, XFont font, XBrush textColor)
        {
            foreach (var line in lineList)
            {
                var pi = line.pi;
                var pj = line.pj;

                var xi = (pi.X - centerPos.X) * scaleX;
                var yi = -(pi.Y - centerPos.Y) * scaleY;
                var xj = (pj.X - centerPos.X) * scaleX;
                var yj = -(pj.Y - centerPos.Y) * scaleY;

                canvas.printLine(xi, yi, xj, yj);
            }

            foreach (var line in dashedLineList)
            {
                var pi = line.pi;
                var pj = line.pj;
                var dashPattern = line.dashPattern ?? Array.Empty<double>();

                var xi = (pi.X - centerPos.X) * scaleX;
                var yi = -(pi.Y - centerPos.Y) * scaleY;
                var xj = (pj.X - centerPos.X) * scaleX;
                var yj = -(pj.Y - centerPos.Y) * scaleY;

                var bkup = canvas.mc.xpen.DashPattern;
                try
                {
                    canvas.mc.xpen.DashPattern = dashPattern;

                    canvas.printLine(xi, yi, xj, yj);
                }
                finally
                {
                    canvas.mc.xpen.DashPattern = bkup;
                }
            }

            foreach (var text in textList)
            {
                var teXt = text.text;
                var pos = text.pos;
                var angle = text.angle;
                var align = text.align;

                var x = (pos.X - centerPos.X) * scaleX;
                var y = -(pos.Y - centerPos.Y) * scaleY;

                // 回転角の補正 (ここまでは反時計回りが正で単位は度、diagramManager.printText()では時計回りが正で単位はラジアン)
                angle *= -Math.PI / 180;

                align ??= XStringFormats.BottomLeft;

                canvas.printText(x, y, teXt, angle, font, textColor, align);
            }

            foreach (var arc in arcList)
            {
                var center = arc.center;
                var radius = arc.radius;
                var startAngle = arc.startAngle;
                var sweepAngle = arc.sweepAngle;

                var x = (center.X - centerPos.X) * scaleX;
                var y = -(center.Y - centerPos.Y) * scaleY;
                var rx = radius * scaleX;
                var ry = radius * scaleY;

                // 回転角の補正 (ここまでは反時計回りが正で単位は度、diagramManager.printText()では反時計回りが正で単位はラジアン)
                startAngle *= Math.PI / 180;
                sweepAngle *= Math.PI / 180;

                canvas.printArc(x, y, rx, ry, startAngle, sweepAngle);
            }
        }

        private readonly List<(XPoint pi, XPoint pj)> lineList = new List<(XPoint, XPoint)>();
        private readonly List<(XPoint pi, XPoint pj, double[] dashPattern)> dashedLineList = new List<(XPoint, XPoint, double[])>();
        private readonly List<(string text, XPoint pos, double angle, XStringFormat align)> textList = new List<(string, XPoint, double, XStringFormat)>();
        private readonly List<(XPoint center, double radius, double startAngle, double sweepAngle)> arcList = new List<(XPoint, double, double, double)>();
    }
}
