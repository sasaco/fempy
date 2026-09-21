using PdfSharpCore.Drawing;
using System;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 節点荷重の描画情報を保持するクラスの共通インタフェース定義
    /// </summary>
    internal interface IXNodalLoad
    {
        /// <summary>
        /// 節点荷重の描画領域の左上角の座標
        /// </summary>
        XPoint TopLeft { get; }
        /// <summary>
        /// 節点荷重の描画領域の右下角の座標
        /// </summary>
        XPoint BottomRight { get; }

        /// <summary>
        /// 節点荷重を <paramref name="canvas"/> に描画する
        /// </summary>
        /// <param name="canvas"></param>
        void Print(ICanvas canvas);
    }

    /// <summary>
    /// 水平方向節点荷重の描画情報を保持する
    /// </summary>
    internal class XHorizontalNodalLoad : XDrawable, IXNodalLoad
    {
        /// <summary>
        /// 部材節点と荷重矢印先頭の間隔(単位はポイント)
        /// </summary>
        protected const double ArrowHeadDistance = 10;
        /// <summary>
        /// 荷重矢印本体の長さ(単位はポイント)
        /// </summary>
        protected const double ArrowBodyLength = 60;
        /// <summary>
        /// 部材矢印先端と荷重値テキスト左端(または右端)の間隔(単位はポイント)
        /// </summary>
        protected const double DistanceBetweenArrowHeadAndTextHead = 10;
        /// <summary>
        /// 部材矢印と荷重値テキスト下端の間隔(単位はポイント)
        /// </summary>
        protected const double DistanceBetweenArrowAndTextBottom = 2;
        /// <summary>
        /// 骨組線と部材荷重矢印が重なる際に部材荷重矢印を傾ける角度(単位は度)
        /// </summary>
        protected const double TiltAngle = 10;

        /// <summary>
        /// 水平方向節点荷重の描画情報を生成する
        /// </summary>
        /// <param name="node">節点情報</param>
        /// <param name="fx">水平方向の荷重</param>
        /// <param name="coef">(描画領域の長辺が紙面上での200mmに相当する場合の)紙面上での1mmを図の座標系で表現するための係数</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        public XHorizontalNodalLoad(XNode node, double fx, double coef, XFont font, Func<string, XFont, XSize> measureString)
            : base(font, measureString)
        {
            GeneratePrintData(node, fx, coef);

            CalculateDiagramRect();
        }

        /// <summary>
        /// 節点荷重の描画領域の左上角の座標
        /// </summary>
        public XPoint TopLeft { get; private set; }
        /// <summary>
        /// 節点荷重の描画領域の右下角の座標
        /// </summary>
        public XPoint BottomRight { get; private set; }

        /// <summary>
        /// 節点荷重を <paramref name="canvas"/> に描画する
        /// </summary>
        /// <param name="canvas"></param>
        public new void Print(ICanvas canvas) => base.PrintDrawables(canvas);

        protected virtual void GeneratePrintData(XNode node, double fx, double coef)
        {
            if (fx < 0)
            {
                // 左向き

                var head = new XPoint(node.Pos.X + coef * ArrowHeadDistance, node.Pos.Y);
                var tail = new XPoint(head.X + coef * ArrowBodyLength, node.Pos.Y);

                var textPos = new XPoint(head.X + coef * DistanceBetweenArrowHeadAndTextHead, head.Y + coef * DistanceBetweenArrowAndTextBottom);
                var textAngle = 0.0;
                var textAlign = XStringFormats.BottomLeft;

                var points = new[] { head, tail, textPos, };
                if (node.Anothers.Any(a => a.Pos.X > node.Pos.X && a.Pos.Y == node.Pos.Y))
                {
                    // 骨組線と荷重線が重なる場合は反時計回りに少し傾ける

                    var mat = new XMatrix();
                    mat.RotateAtAppend(TiltAngle, node.Pos);
                    mat.Transform(points);

                    textAngle += TiltAngle;
                }

                AddLine((points[1], points[0]), coef, cj: LineCap.ArrowAnchor);

                AddText($"H={(-fx).ToStringF2()}kN", points[2], coef, textAngle, textAlign);
            }
            else
            {
                // 右向き

                var head = new XPoint(node.Pos.X - coef * ArrowHeadDistance, node.Pos.Y);
                var tail = new XPoint(head.X - coef * ArrowBodyLength, node.Pos.Y);

                var textPos = new XPoint(head.X - coef * DistanceBetweenArrowHeadAndTextHead, head.Y + coef * DistanceBetweenArrowAndTextBottom);
                var textAngle = 0.0;
                var textAlign = XStringFormats.BottomRight;

                var points = new[] { head, tail, textPos, };
                if (node.Anothers.Any(a => a.Pos.X < node.Pos.X && a.Pos.Y == node.Pos.Y))
                {
                    // 骨組線と荷重線が重なる場合は時計回りに少し傾ける

                    var mat = new XMatrix();
                    mat.RotateAtAppend(-TiltAngle, node.Pos);
                    mat.Transform(points);

                    textAngle -= TiltAngle;
                }

                AddLine((points[1], points[0]), coef, cj: LineCap.ArrowAnchor);

                AddText($"H={fx.ToStringF2()}kN", points[2], coef, textAngle, textAlign);
            }
        }

        private void CalculateDiagramRect()
        {
            CalculateDiagramRect(out var _topLeft, out var _bottomRight);
            TopLeft = _topLeft;
            BottomRight = _bottomRight;
        }
    }

    /// <summary>
    /// 鉛直方向節点荷重の描画情報を保持する
    /// </summary>
    internal class XVerticalNodalLoad : XHorizontalNodalLoad
    {
        /// <summary>
        /// 鉛直方向節点荷重の描画情報を生成する
        /// </summary>
        /// <param name="node">節点情報</param>
        /// <param name="fx">水平方向の荷重</param>
        /// <param name="coef">(描画領域の長辺が紙面上での200mmに相当する場合の)紙面上での1mmを図の座標系で表現するための係数</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        public XVerticalNodalLoad(XNode node, double fy, double coef, XFont font, Func<string, XFont, XSize> measureString)
            : base(node, fy, coef, font, measureString) { }

        protected override void GeneratePrintData(XNode node, double fy, double coef)
        {
            if (fy < 0)
            {
                // 下向き

                var head = new XPoint(node.Pos.X, node.Pos.Y + coef * ArrowHeadDistance);
                var tail = new XPoint(head.X, node.Pos.Y + coef * ArrowBodyLength);

                var textPos = new XPoint(head.X + coef * DistanceBetweenArrowAndTextBottom, head.Y + coef * DistanceBetweenArrowHeadAndTextHead);
                var textAngle = 90.0;
                var textAlign = XStringFormats.TopLeft;

                var points = new[] { head, tail, textPos, };
                if (node.Anothers.Any(a => a.Pos.X == node.Pos.X && a.Pos.Y > node.Pos.Y))
                {
                    // 骨組線と荷重線が重なる場合は時計回りに少し傾ける

                    var mat = new XMatrix();
                    mat.RotateAtAppend(-TiltAngle, node.Pos);
                    mat.Transform(points);

                    textAngle -= TiltAngle;
                }

                AddLine((points[1], points[0]), coef, cj: LineCap.ArrowAnchor);

                AddText($"P={(-fy).ToStringF2()}kN", points[2], coef, textAngle, textAlign);
            }
            else
            {
                // 上向き

                var head = new XPoint(node.Pos.X, node.Pos.Y - coef * ArrowHeadDistance);
                var tail = new XPoint(head.X, node.Pos.Y - coef * ArrowBodyLength);

                var textPos = new XPoint(head.X + coef * DistanceBetweenArrowAndTextBottom, head.Y - coef * DistanceBetweenArrowHeadAndTextHead);
                var textAngle = -90.0;
                var textAlign = XStringFormats.TopLeft;

                var points = new[] { head, tail, textPos, };
                if (node.Anothers.Any(a => a.Pos.X == node.Pos.X && a.Pos.Y < node.Pos.Y))
                {
                    // 骨組線と荷重線が重なる場合は反時計回りに少し傾ける

                    var mat = new XMatrix();
                    mat.RotateAtAppend(TiltAngle, node.Pos);
                    mat.Transform(points);

                    textAngle += TiltAngle;
                }

                AddLine((points[1], points[0]), coef, cj: LineCap.ArrowAnchor);

                AddText($"P={fy.ToStringF2()}kN", points[2], coef, textAngle, textAlign);
            }
        }
    }


    /// <summary>
    /// モーメント荷重の描画情報を保持する
    /// </summary>
    internal class XMomentNodalLoad : XDrawable, IXNodalLoad
    {
        /// <summary>
        /// モーメント円弧の半径(単位は度)
        /// </summary>
        private const double ArcRadius = 20;
        /// <summary>
        /// モーメント円弧の描画開始角度(3時の方向を0として反時計回りが正)(単位は度)
        /// </summary>
        private const double ArcStartAngle = -45;
        /// <summary>
        /// モーメント円弧の描画開始から終了までの角度(反時計回りが正)(単位は度)
        /// </summary>
        private const double ArcSweepAngle = -2 * ArcStartAngle + 180;
        /// <summary>
        /// モーメント円弧と荷重値テキストの間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenArrowAndTextBottom = 2;

        /// <summary>
        /// モーメント荷重の描画情報を生成する
        /// </summary>
        /// <param name="node">節点情報</param>
        /// <param name="m">モーメント荷重</param>
        /// <param name="coef">(描画領域の長辺が紙面上での200mmに相当する場合の)紙面上での1mmを図の座標系で表現するための係数</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        public XMomentNodalLoad(XNode node, double m, double coef, XFont font, Func<string, XFont, XSize> measureString)
            : base(font, measureString)
        {
            GeneratePrintData(node, m, coef);

            CalculateDiagramRect();
        }

        /// <summary>
        /// 節点荷重の描画領域の左上角の座標
        /// </summary>
        public XPoint TopLeft { get; private set; }
        /// <summary>
        /// 節点荷重の描画領域の右下角の座標
        /// </summary>
        public XPoint BottomRight { get; private set; }

        /// <summary>
        /// 節点荷重を <paramref name="canvas"/> に描画する
        /// </summary>
        /// <param name="canvas"></param>
        public new void Print(ICanvas canvas) => base.PrintDrawables(canvas);

        private void GeneratePrintData(XNode node, double m, double coef)
        {
            if (m < 0)
            {
                // 反時計回り

                var textPos = new XPoint(
                    node.Pos.X - coef * (ArcRadius + DistanceBetweenArrowAndTextBottom) * Math.Cos(Math.PI / 4),
                    node.Pos.Y + coef * (ArcRadius + DistanceBetweenArrowAndTextBottom) * Math.Sin(Math.PI / 4));

                AddArc(node.Pos, coef * ArcRadius, ArcStartAngle, ArcSweepAngle, coef, endCap: LineCap.ArrowAnchor);

                AddText($"M={m.ToStringF2()}kN\u00B7m", textPos, coef, 0, XStringFormats.BottomRight); // 数値の符号はそのまま
            }
            else
            {
                // 時計回り

                var textPos = new XPoint(
                    node.Pos.X + coef * (ArcRadius + DistanceBetweenArrowAndTextBottom) * Math.Cos(Math.PI / 4),
                    node.Pos.Y + coef * (ArcRadius + DistanceBetweenArrowAndTextBottom) * Math.Sin(Math.PI / 4));

                AddArc(node.Pos, coef * ArcRadius, ArcStartAngle, ArcSweepAngle, coef, startCap: LineCap.ArrowAnchor);

                AddText($"M={m.ToStringF2()}kN\u00B7m", textPos, coef, 0, XStringFormats.BottomLeft);
            }
        }

        private void CalculateDiagramRect()
        {
            CalculateDiagramRect(out var _topLeft, out var _bottomRight);
            TopLeft = _topLeft;
            BottomRight = _bottomRight;
        }
    }
}
