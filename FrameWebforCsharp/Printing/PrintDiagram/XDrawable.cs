
// XGraphics.MeasureString()で取得したテキストサイズを示す枠を描画
//#define DRAWS_TEXTBOXES

using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 支点、バネ、寸法、節点荷重および部材荷重の描画に使用される共通メソッドを定義した抽象クラス
    /// </summary>
    internal abstract class XDrawable
    {
        /// <summary>
        /// 矢印の羽の長さ(単位はポイント)
        /// </summary>
        private const double defaultArrowHeadLength = 10;
        /// <summary>
        /// 矢印の羽の開き角度(単位は度)
        /// </summary>
        private const double defaultArrowHeadAngle = 30;

        /// <summary>
        /// 支点、バネ、寸法、節点荷重および部材荷重の描画に使用される共通メソッドを定義した抽象クラス
        /// </summary>
        /// <param name="font">テキスト描画フォント</param>
        /// <param name="measureString">テキストの描画サイズを取得するメソッド</param>
        protected XDrawable(XFont font, Func<string, XFont, XSize> measureString)
        {
            Font = font;
            fontAscent = font != null ? font.Size * font.Metrics.Ascent / font.Metrics.UnitsPerEm : double.NaN;
            fontDescent = font != null ? font.Size * font.Metrics.Descent / font.Metrics.UnitsPerEm : double.NaN;
            MeasureString = (s) => measureString(s, font);

            ClearDrawables();
        }

        protected XFont Font { get; }
        private readonly double fontAscent;
        private readonly double fontDescent;
        protected Func<string, XSize> MeasureString { get; }

        /// <summary>
        /// 直線と円弧の始点/終点の形状
        /// </summary>
        protected enum LineCap
        {
            /// <summary>
            /// 矢印なし
            /// </summary>
            NoAnchor,
            /// <summary>
            /// 矢印あり
            /// </summary>
            ArrowAnchor,
        }

        /// <summary>
        /// 描画情報をクリアする
        /// </summary>
        protected void ClearDrawables()
        {
            lineList.Clear();
            textList.Clear();
            arcList.Clear();
        }

        /// <summary>
        /// 直線の描画情報を登録する
        /// </summary>
        /// <param name="line">始点と終点の座標</param>
        protected void AddLine((XPoint pi, XPoint pj) line) => lineList.Add(new Line(line.pi, line.pj));
        /// <summary>
        /// 直線の描画情報を登録する
        /// </summary>
        /// <param name="lines">始点と終戦の座標のコレクション</param>
        protected void AddLines(IEnumerable<(XPoint pi, XPoint pj)> lines) => lineList.AddRange(lines.Select(line => new Line(line.pi, line.pj)));

        /// <summary>
        /// 直線の描画情報を登録する
        /// </summary>
        /// <param name="line">始点と終点の座標</param>
        /// <param name="coef">ポイント値を部材座標系における長さに変換するための係数</param>
        /// <param name="ci">支点の形状。デフォルトは矢印なし</param>
        /// <param name="cj">終点の形状。デフォルトは矢印なし</param>
        /// <param name="arrowHeadLength">矢印の羽の長さ(単位はポイント)</param>
        /// <param name="arrowHeadAngle">矢印の羽の開き角度(単位は度)</param>
        /// <param name="dashPattern">破線パターン。デフォルトは破線なし</param>
        protected void AddLine((XPoint pi, XPoint pj) line, double coef,
            LineCap ci = LineCap.NoAnchor, LineCap cj = LineCap.NoAnchor, double arrowHeadLength = defaultArrowHeadLength, double arrowHeadAngle = defaultArrowHeadAngle,
            double[] dashPattern = null)
        {
            if (dashPattern is null)
            {
                lineList.Add(new Line(line.pi, line.pj));
            }
            else
            {
                dashedLineList.Add(new DashedLine(line.pi, line.pj, dashPattern));
            }

            if (ci == LineCap.ArrowAnchor)
            {
                GenerateArrowHeadFromTo(line.pj, line.pi, coef, arrowHeadLength, arrowHeadAngle);
            }
            if (cj == LineCap.ArrowAnchor)
            {
                GenerateArrowHeadFromTo(line.pi, line.pj, coef, arrowHeadLength, arrowHeadAngle);
            }
        }
        /// <summary>
        /// 直線の描画情報を登録する
        /// </summary>
        /// <param name="lines">始点と終点の座標のコレクション</param>
        /// <param name="coef">ポイント値を部材座標系における長さに変換するための係数</param>
        /// <param name="ci">支点の形状。デフォルトは矢印なし</param>
        /// <param name="cj">終点の形状。デフォルトは矢印なし</param>
        /// <param name="arrowHeadLength">矢印の羽の長さ(単位はポイント)</param>
        /// <param name="arrowHeadAngle">矢印の羽の開き角度(単位は度)</param>
        /// <param name="dashPattern">破線パターン。デフォルトは破線なし</param>
        protected void AddLines(IEnumerable<(XPoint pi, XPoint pj)> lines, double coef,
            LineCap ci = LineCap.NoAnchor, LineCap cj = LineCap.ArrowAnchor, double arrowHeadLength = defaultArrowHeadLength, double arrowHeadAngle = defaultArrowHeadAngle,
            double[] dashPattern = null)
        {
            foreach (var line in lines)
            {
                AddLine(line, coef, ci, cj, arrowHeadLength, arrowHeadAngle, dashPattern);
            }
        }
        private void GenerateArrowHeadFromTo(XPoint from, XPoint to, double coef, double arrowHeadLength, double arrowHeadAngle)
        {
            var angle = XMath.Atan2(from.Y - to.Y, from.X - to.X);

            var mat1 = new XMatrix();
            mat1.RotateAtAppend(angle + arrowHeadAngle / 2, to);
            var tail1 = mat1.Transform(new XPoint(to.X + coef * arrowHeadLength, to.Y));
            lineList.Add(new Line(to, tail1));

            var mat2 = new XMatrix();
            mat2.RotateAtAppend(angle - arrowHeadAngle / 2, to);
            var tail2 = mat2.Transform(new XPoint(to.X + coef * arrowHeadLength, to.Y));
            lineList.Add(new Line(to, tail2));
        }

        /// <summary>
        /// テキストの描画情報を登録する。条件付きコンパイルシンボルDRAWS_TEXTBOXESが定義されていると、MeasureString()で取得したテキスト描画サイズを表す四角形が描画される
        /// </summary>
        /// <param name="text">描画対象のテキスト</param>
        /// <param name="pos">描画位置の座標</param>
        /// <param name="coef">ポイント値を部材座標系における長さに変換するための係数</param>
        /// <param name="angle">テキストの描画角度(単位は度)。3時の方向が0で、反時計回りが正</param>
        /// <param name="align">描画位置とテキストの位置関係。デフォルトはBottomLeft</param>
        /// <exception cref="Exception"></exception>
        protected void AddText(string text, XPoint pos, double coef, double angle = 0, XStringFormat align = null)
        {
            align ??= XStringFormats.BottomLeft;

            var size = MeasureString(text);

            var (top, bottom) = align.LineAlignment switch
            {
                XLineAlignment.Near => (pos.Y, pos.Y - coef * Font.Height), // Top***
                XLineAlignment.Center => (pos.Y + coef * Font.Height / 2, pos.Y + coef * Font.Height / 2), // Center***
                XLineAlignment.BaseLine => (pos.Y + coef * fontAscent, pos.Y - coef * fontDescent), // Baseline***
                XLineAlignment.Far => (pos.Y + coef * Font.Height, pos.Y), // Bottom***
                _ => throw new Exception(),
            };
            var (left, right) = align.Alignment switch
            {
                XStringAlignment.Near => (pos.X, pos.X + coef * size.Width), // ***Left
                XStringAlignment.Center => (pos.X - coef * size.Width / 2, pos.X + coef * size.Width / 2), // ***Center
                XStringAlignment.Far => (pos.X - coef * size.Width, pos.X), // ***Right
                _ => throw new Exception(),
            };
            var corners = new[]
            {
                new XPoint(left, top),
                new XPoint(right, top),
                new XPoint(left, bottom),
                new XPoint(right, bottom),
            };
            var mat = new XMatrix();
            mat.RotateAtAppend(angle, pos);
            mat.Transform(corners);
            var topLeft = new XPoint(corners.Min(p => p.X), corners.Max(p => p.Y));
            var bottomRight = new XPoint(corners.Max(p => p.X), corners.Min(p => p.Y));

            textList.Add(new Text(text, pos, angle, align, topLeft, bottomRight));

#if DRAWS_TEXTBOXES
            AddTextBox(text, pos, coef, angle, align);
#endif
        }
#if DRAWS_TEXTBOXES
        private void AddTextBox(string text, XPoint pos, double coef, double textAngle, XStringFormat align, double fontScale = 1)
        {
            var size = MeasureString(text);
            size = new XSize(coef * size.Width / fontScale, coef * size.Height / fontScale);

            XPoint[] points;
            if (align.LineAlignment == XLineAlignment.Near && align.Alignment == XStringAlignment.Near) // TopLeft
            {
                points = new[]
                {
                    new XPoint(pos.X             , pos.Y              ),
                    new XPoint(pos.X + size.Width, pos.Y              ),
                    new XPoint(pos.X + size.Width, pos.Y - size.Height),
                    new XPoint(pos.X             , pos.Y - size.Height),
                };
            }
            else if (align.LineAlignment == XLineAlignment.Near && align.Alignment == XStringAlignment.Center) // TopCenter
            {
                points = new[]
                {
                    new XPoint(pos.X - size.Width / 2, pos.Y              ),
                    new XPoint(pos.X + size.Width / 2, pos.Y              ),
                    new XPoint(pos.X + size.Width / 2, pos.Y - size.Height),
                    new XPoint(pos.X - size.Width / 2, pos.Y - size.Height),
                };
            }
            else if (align.LineAlignment == XLineAlignment.Near && align.Alignment == XStringAlignment.Far) // TopRight
            {
                points = new[]
                {
                    new XPoint(pos.X - size.Width, pos.Y              ),
                    new XPoint(pos.X             , pos.Y              ),
                    new XPoint(pos.X             , pos.Y - size.Height),
                    new XPoint(pos.X - size.Width, pos.Y - size.Height),
                };
            }
            else if (align.LineAlignment == XLineAlignment.Center && align.Alignment == XStringAlignment.Near) // CenterLeft
            {
                points = new[]
                {
                    new XPoint(pos.X             , pos.Y + size.Height / 2),
                    new XPoint(pos.X + size.Width, pos.Y + size.Height / 2),
                    new XPoint(pos.X + size.Width, pos.Y - size.Height / 2),
                    new XPoint(pos.X             , pos.Y - size.Height / 2),
                };
            }
            else if (align.LineAlignment == XLineAlignment.Center && align.Alignment == XStringAlignment.Center) // Center
            {
                points = new[]
                {
                    new XPoint(pos.X - size.Width / 2, pos.Y + size.Height / 2),
                    new XPoint(pos.X + size.Width / 2, pos.Y + size.Height / 2),
                    new XPoint(pos.X + size.Width / 2, pos.Y - size.Height / 2),
                    new XPoint(pos.X - size.Width / 2, pos.Y - size.Height / 2),
                };
            }
            else if (align.LineAlignment == XLineAlignment.Center && align.Alignment == XStringAlignment.Far) // CenterRight
            {
                points = new[]
                {
                    new XPoint(pos.X - size.Width, pos.Y + size.Height / 2),
                    new XPoint(pos.X             , pos.Y + size.Height / 2),
                    new XPoint(pos.X             , pos.Y - size.Height / 2),
                    new XPoint(pos.X - size.Width, pos.Y - size.Height / 2),
                };
            }
            else if (align.LineAlignment == XLineAlignment.BaseLine && align.Alignment == XStringAlignment.Near) // BaselineLeft
            {
                points = new[]
                {
                    new XPoint(pos.X             , pos.Y + coef * fontAscent),
                    new XPoint(pos.X + size.Width, pos.Y + coef * fontAscent),
                    new XPoint(pos.X + size.Width, pos.Y - coef * fontDescent),
                    new XPoint(pos.X             , pos.Y - coef * fontDescent),
                };
            }
            else if (align.LineAlignment == XLineAlignment.BaseLine && align.Alignment == XStringAlignment.Center) // BaselineCenter
            {
                points = new[]
                {
                    new XPoint(pos.X - size.Width / 2, pos.Y + coef * fontAscent),
                    new XPoint(pos.X + size.Width / 2, pos.Y + coef * fontAscent),
                    new XPoint(pos.X + size.Width / 2, pos.Y - coef * fontDescent),
                    new XPoint(pos.X - size.Width / 2, pos.Y - coef * fontDescent),
                };
            }
            else if (align.LineAlignment == XLineAlignment.BaseLine && align.Alignment == XStringAlignment.Far) // BaselineRight
            {
                points = new[]
                {
                    new XPoint(pos.X - size.Width, pos.Y + coef * fontAscent),
                    new XPoint(pos.X             , pos.Y + coef * fontAscent),
                    new XPoint(pos.X             , pos.Y - coef * fontDescent),
                    new XPoint(pos.X - size.Width, pos.Y - coef * fontDescent),
                };
            }
            else if (align.LineAlignment == XLineAlignment.Far && align.Alignment == XStringAlignment.Near) // BottomLeft
            {
                points = new[]
                {
                    new XPoint(pos.X             , pos.Y + size.Height),
                    new XPoint(pos.X + size.Width, pos.Y + size.Height),
                    new XPoint(pos.X + size.Width, pos.Y              ),
                    new XPoint(pos.X             , pos.Y              ),
                };
            }
            else if (align.LineAlignment == XLineAlignment.Far && align.Alignment == XStringAlignment.Center) // BottomCenter
            {
                points = new[]
                {
                    new XPoint(pos.X - size.Width / 2, pos.Y + size.Height),
                    new XPoint(pos.X + size.Width / 2, pos.Y + size.Height),
                    new XPoint(pos.X + size.Width / 2, pos.Y              ),
                    new XPoint(pos.X - size.Width / 2, pos.Y              ),
                };
            }
            else if (align.LineAlignment == XLineAlignment.Far && align.Alignment == XStringAlignment.Far) // BottomRight
            {
                points = new[]
                {
                    new XPoint(pos.X - size.Width, pos.Y + size.Height),
                    new XPoint(pos.X             , pos.Y + size.Height),
                    new XPoint(pos.X             , pos.Y              ),
                    new XPoint(pos.X - size.Width, pos.Y              ),
                };
            }
            else
            {
                throw new Exception();
            }

            var mat = new XMatrix();
            mat.RotateAtAppend(textAngle, pos);
            mat.Transform(points);

            AddLines(new[] { (points[0], points[1]), (points[1], points[2]), (points[2], points[3]), (points[3], points[0]), });
        }
#endif

        /// <summary>
        /// 円弧の描画情報を登録する
        /// </summary>
        /// <param name="center">円弧の中心座標</param>
        /// <param name="radius">円弧の半径(単位はポイント)</param>
        /// <param name="startAngle">円弧の描画開始角度(単位は度)。3時の方向が0で、反時計回りが正</param>
        /// <param name="sweepAngle">円弧の描画角度(単位は度)。反時計回りが正</param>
        /// <param name="coef">ポイント値を部材座標系における長さに変換するための係数</param>
        /// <param name="startCap">円弧の描画開始点の形状。デフォルトは矢印なし</param>
        /// <param name="endCap">円弧の描画終了点の形状。デフォルトは矢印なし</param>
        /// <param name="arrowHeadLength">矢印の羽の長さ(単位はポイント)</param>
        /// <param name="arrowHeadAngle">矢印の羽の開き角度(単位は度)</param>
        protected void AddArc(XPoint center, double radius, double startAngle, double sweepAngle, double coef,
            LineCap startCap = LineCap.NoAnchor, LineCap endCap = LineCap.NoAnchor, double arrowHeadLength = defaultArrowHeadLength, double arrowHeadAngle = defaultArrowHeadAngle)
        {
            // @TODO: とりあえず真円とみなして・・・
            var topLeft = new XPoint(center.X - radius, center.Y + radius);
            var bottomRight = new XPoint(center.X + radius, center.Y - radius);

            if (startCap == LineCap.ArrowAnchor)
            {
                var mat0 = new XMatrix();
                mat0.RotateAtAppend(startAngle, center);
                var head = mat0.Transform(new XPoint(center.X + radius, center.Y));
                var mat1 = new XMatrix();
                mat1.RotateAtAppend(startAngle + 90 - arrowHeadAngle / 2, head);
                var tail1 = mat1.Transform(new XPoint(head.X + coef * arrowHeadLength, head.Y));
                var mat2 = new XMatrix();
                mat2.RotateAtAppend(startAngle + 90 + arrowHeadAngle / 2, head);
                var tail2 = mat2.Transform(new XPoint(head.X + coef * arrowHeadLength, head.Y));

                lineList.Add(new Line(head, tail1));
                lineList.Add(new Line(head, tail2));

                topLeft.X = new[] { topLeft.X, head.X, tail1.X, tail2.X, }.Min();
                topLeft.Y = new[] { topLeft.Y, head.Y, tail1.Y, tail2.Y, }.Max();
                bottomRight.X = new[] { bottomRight.X, head.X, tail1.X, tail2.X, }.Max();
                bottomRight.Y = new[] { bottomRight.Y, head.Y, tail1.Y, tail2.Y, }.Min();
            }

            if (endCap == LineCap.ArrowAnchor)
            {
                var mat0 = new XMatrix();
                mat0.RotateAtAppend(startAngle + sweepAngle, center);
                var head = mat0.Transform(new XPoint(center.X + radius, center.Y));
                var mat1 = new XMatrix();
                mat1.RotateAtAppend(startAngle + sweepAngle - 90 - arrowHeadAngle / 2, head);
                var tail1 = mat1.Transform(new XPoint(head.X + coef * arrowHeadLength, head.Y));
                var mat2 = new XMatrix();
                mat2.RotateAtAppend(startAngle + sweepAngle - 90 + arrowHeadAngle / 2, head);
                var tail2 = mat2.Transform(new XPoint(head.X + coef * arrowHeadLength, head.Y));

                lineList.Add(new Line(head, tail1));
                lineList.Add(new Line(head, tail2));

                topLeft.X = new[] { topLeft.X, head.X, tail1.X, tail2.X, }.Min();
                topLeft.Y = new[] { topLeft.Y, head.Y, tail1.Y, tail2.Y, }.Max();
                bottomRight.X = new[] { bottomRight.X, head.X, tail1.X, tail2.X, }.Max();
                bottomRight.Y = new[] { bottomRight.Y, head.Y, tail1.Y, tail2.Y, }.Min();
            }

            arcList.Add(new Arc(center, radius, startAngle, sweepAngle, topLeft, bottomRight));
        }

        /// <summary>
        /// 登録されたデータによる図形とテキストが描画される領域の左上角と右下角の座標を返す
        /// </summary>
        /// <param name="topLeft">左上角の座標</param>
        /// <param name="bottomRight">右下角の座標</param>
        protected void CalculateDiagramRect(out XPoint topLeft, out XPoint bottomRight)
        {
            topLeft = new XPoint(double.MaxValue, double.MinValue);
            bottomRight = new XPoint(double.MinValue, double.MaxValue);

            var points = lineList.SelectMany(line => new[] { line.Pi, line.Pj, }).Distinct();
            if (points.Any())
            {
                //topLeft.X = Math.Min(topLeft.X, points.Min(p => p.X));
                //topLeft.Y = Math.Max(topLeft.Y, points.Max(p => p.Y));
                //bottomRight.X = Math.Max(bottomRight.X, points.Max(p => p.X));
                //bottomRight.Y = Math.Min(bottomRight.Y, points.Min(p => p.Y));

                topLeft.X = Math.Min(topLeft.X, points.Where(p => !double.IsNaN(p.X)).Select(p => p.X).DefaultIfEmpty().Min());
                topLeft.Y = Math.Max(topLeft.Y, points.Where(p => !double.IsNaN(p.X)).Select(p => p.Y).DefaultIfEmpty().Max());
                bottomRight.X = Math.Max(bottomRight.X, points.Where(p => !double.IsNaN(p.X)).Select(p => p.X).DefaultIfEmpty().Max());
                bottomRight.Y = Math.Min(bottomRight.Y, points.Where(p => !double.IsNaN(p.X)).Select(p => p.Y).DefaultIfEmpty().Min());
            }

            if (textList.Any())
            {
                //topLeft.X = Math.Min(topLeft.X, textList.Min(s => s.TopLeft.X));
                //topLeft.Y = Math.Max(topLeft.Y, textList.Max(s => s.TopLeft.Y));
                //bottomRight.X = Math.Max(bottomRight.X, textList.Max(s => s.BottomRight.X));
                //bottomRight.Y = Math.Min(bottomRight.Y, textList.Min(s => s.BottomRight.Y));

                topLeft.X = Math.Min(topLeft.X, textList.Where(p => !double.IsNaN(p.TopLeft.X)).Select(p => p.TopLeft.X).DefaultIfEmpty().Min());
                topLeft.Y = Math.Max(topLeft.Y, textList.Where(p => !double.IsNaN(p.TopLeft.Y)).Select(p => p.TopLeft.Y).DefaultIfEmpty().Max());
                bottomRight.X = Math.Max(bottomRight.X, textList.Where(p => !double.IsNaN(p.BottomRight.X)).Select(p => p.BottomRight.X).DefaultIfEmpty().Max());
                bottomRight.Y = Math.Min(bottomRight.Y, textList.Where(p => !double.IsNaN(p.BottomRight.Y)).Select(p => p.BottomRight.Y).DefaultIfEmpty().Min());
            }

            if (arcList.Any())
            {
                //topLeft.X = Math.Min(topLeft.X, arcList.Min(s => s.TopLeft.X));
                //topLeft.Y = Math.Max(topLeft.Y, arcList.Max(s => s.TopLeft.Y));
                //bottomRight.X = Math.Max(bottomRight.X, arcList.Max(s => s.BottomRight.X));
                //bottomRight.Y = Math.Min(bottomRight.Y, arcList.Min(s => s.BottomRight.Y));

                topLeft.X = Math.Min(topLeft.X, arcList.Where(p => !double.IsNaN(p.TopLeft.X)).Select(p => p.TopLeft.X).DefaultIfEmpty().Min());
                topLeft.Y = Math.Max(topLeft.Y, arcList.Where(p => !double.IsNaN(p.TopLeft.Y)).Select(p => p.TopLeft.Y).DefaultIfEmpty().Max());
                bottomRight.X = Math.Max(bottomRight.X, arcList.Where(p => !double.IsNaN(p.BottomRight.X)).Select(p => p.BottomRight.X).DefaultIfEmpty().Max());
                bottomRight.Y = Math.Min(bottomRight.Y, arcList.Where(p => !double.IsNaN(p.BottomRight.Y)).Select(p => p.BottomRight.Y).DefaultIfEmpty().Min());
            }
        }

        /// <summary>
        /// 登録された全ての図形とテキストを <paramref name="canvas"/> に描画する
        /// </summary>
        /// <param name="canvas">図形とテキストの描画先</param>
        protected void PrintDrawables(ICanvas canvas)
        {
            foreach (var line in lineList)
            {
                canvas.LineBetween(line.Pi, line.Pj);
            }

            foreach (var line in dashedLineList)
            {
                canvas.DashedLineBetween(line.Pi, line.Pj, line.DashPattern);
            }

            foreach (var text in textList)
            {
                canvas.TextAt(text.TeXt, text.Pos, text.Angle, text.Align);
            }

            foreach (var arc in arcList)
            {
                canvas.ArcAt(arc.Center, arc.Radius, arc.StartAngle, arc.SweepAngle);
            }
        }

        private readonly struct Line
        {
            public XPoint Pi { get; }
            public XPoint Pj { get; }

            public Line(XPoint pi, XPoint pj) => (Pi, Pj) = (pi, pj);
        }
        private readonly List<Line> lineList = new List<Line>();

        private readonly struct DashedLine
        {
            public XPoint Pi { get; }
            public XPoint Pj { get; }
            public double[] DashPattern { get; }

            public DashedLine(XPoint pi, XPoint pj, double[] dashPattern) => (Pi, Pj, DashPattern) = (pi, pj, dashPattern);
        }
        private readonly List<DashedLine> dashedLineList = new List<DashedLine>();

        private readonly struct Text
        {
            public string TeXt { get; }
            public XPoint Pos { get; }
            public double Angle { get; }
            public XStringFormat Align { get; }
            public XPoint TopLeft { get; }
            public XPoint BottomRight { get; }

            public Text(string text, XPoint pos, double angle, XStringFormat align, XPoint topLeft, XPoint bottomRight)
                => (TeXt, Pos, Angle, Align, TopLeft, BottomRight) = (text, pos, angle, align, topLeft, bottomRight);
        }
        private readonly List<Text> textList = new List<Text>();

        private readonly struct Arc
        {
            public XPoint Center { get; }
            public double Radius { get; }
            public double StartAngle { get; }
            public double SweepAngle { get; }
            public XPoint TopLeft { get; }
            public XPoint BottomRight { get; }

            public Arc(XPoint center, double radius, double startAngle, double sweepAngle, XPoint topLeft, XPoint bottomRight)
                => (Center, Radius, StartAngle, SweepAngle, TopLeft, BottomRight) = (center, radius, startAngle, sweepAngle, topLeft, bottomRight);
        }
        private readonly List<Arc> arcList = new List<Arc>();
    }
}
