using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 部材方向分布荷重の描画情報を保持する
    /// </summary>
    internal class XMemberLoad_2x : XDrawable, IXMemberLoad
    {
        /// <summary>
        /// 荷重矢印本体の長さ(単位はポイント)
        /// </summary>
        private const double ArrowLength = 60;
        /// <summary>
        /// 荷重矢印先端と荷重テキスト左端または右端の間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenArrowAndText = 2;
        /// <summary>
        /// 破線パターン(単位はポイント)
        /// </summary>
        private static readonly double[] DashPattern = new double[] { 40, 10, };

        /// <summary>
        /// 荷重テキストと荷重寸法線の重なりを軽減するための間隔(単位はポイント)
        /// </summary>
        private const double AdditionalDistanceBetweenLoadAndDimension = 10;
        /// <summary>
        /// 荷重と荷重寸法引き出し線の間隔(単位はポイント)
        /// </summary>
        private const double LeaderLineLength = 10;

        /// <summary>
        /// 部材方向分布荷重の描画情報を生成する
        /// </summary>
        /// <param name="loadMember">部材荷重情報</param>
        /// <param name="memberGroup">荷重がかかる部材の属する部材グループ</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        public XMemberLoad_2x(XLoadMember loadMember, XMemberGroup memberGroup, XFont font, Func<string, XFont, XSize> measureString)
            : base(font, measureString)
        {
            Debug.Assert(loadMember.Quadrilaterals.Count > 0);

            this.loadMember = loadMember;
            this.memberGroup = memberGroup;

            var x0 = memberGroup.NHDic[loadMember.m1].NHi.X;
            var quadrilaterals = loadMember.Quadrilaterals.Select(q => new XQuadrilateral(x0 + q.X1, q.P1, x0 + q.X2, q.P2));

            QuadrilateralList.AddRange(quadrilaterals);

            DimensionXs = memberGroup.NHDic.Values.SelectMany(n => new[] { n.NHi.X, n.NHj.X, }).Concat(QuadrilateralList.SelectMany(po => new[] { po.X1, po.X2, })).DistinctLoosely().OrderBy(x => x);

            PrintLocation = XLocationType.YM;
        }

        /// <summary>
        /// 引き出し線の引き出し対象となる部材上の点の(部材の中心を軸に回転させて部材を水平に状態での)x座標のリスト
        /// </summary>
        public IEnumerable<double> DimensionXs { get; }

        /// <summary>
        /// 荷重および荷重寸法を部材のどちら側に描画するか
        /// </summary>
        public XLocationType PrintLocation { get; }

        /// <summary>
        /// 部材荷重の描画直後は部材荷重の描画領域の左上角の座標。寸法の描画直後は寸法を含めた描画領域の左上角の座標
        /// </summary>
        public XPoint TopLeft { get; private set; }
        /// <summary>
        /// 部材荷重の描画直後は部材荷重の描画領域の右下角の座標。寸法の描画直後は寸法を含めた描画領域の右下角の座標
        /// </summary>
        public XPoint BottomRight { get; private set; }

        /// <summary>
        /// 部材荷重の描画
        /// (実際には描画データをLines, Texts, Arcsなどのリストに格納するまでの処理を行う。リストに格納されたデータは、スケール調整された後に実際に描画される)
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="yDistance">部材との間に置く隙間の高さ</param>
        /// <param name="coef">(描画領域の長辺が紙面上での200mmに相当する場合の)紙面上での1mmを図の座標系で表現するための係数</param>
        public void PrintLoad(ICanvas canvas, XDistance yDistance, double coef)
        {
            ClearDrawables();

            var center = memberGroup.Center;
            var tiltAngle = memberGroup.Angle;

            // 座標変換行列の生成
            var restoringMatrix = new XMatrix();
            restoringMatrix.RotateAtAppend(tiltAngle, center);

            var distance = yDistance.GetDistance(loadMember);

            var textHeight = 0.0;
            foreach (var pp in QuadrilateralList)
            {
                Debug.Assert(pp.P1 != 0 || pp.P2 != 0);

                var P1 = pp.P1;
                var P2 = pp.P2;

                var from = new XPoint(pp.X1, center.Y + coef * distance);
                var to = new XPoint(pp.X2, center.Y + coef * distance);

                var (ci, cj) = (LineCap.NoAnchor, LineCap.ArrowAnchor);
                if (P1 < 0)
                {
                    (ci, cj) = (LineCap.ArrowAnchor, LineCap.NoAnchor);
                }

                AddLine((restoringMatrix.Transform(from), restoringMatrix.Transform(to)), coef, ci: ci, cj: cj, dashPattern: DashPattern);

                foreach (var (p, x) in new[] { (P1, from), (P2, to), })
                {
                    var str = $"{p.ToStringF2()}kN/m";
                    var textPos = x + new XVector(0, coef * DistanceBetweenArrowAndText);
                    var an = tiltAngle;
                    var al = XStringFormats.BottomCenter;
                    if (XMath.IsAngleIn2ndOr3rdQuadrant(an))
                    {
                        an += 180;
                        al = XStringFormats.TopCenter;
                    }

                    AddText(str, restoringMatrix.Transform(textPos), coef, an, al);

                    textHeight = Math.Max(textHeight, MeasureString(str).Height);
                }
            }

            PrintDrawables(canvas);

            CalculateDiagramRect(out var topLeft, out var bottomRight);
            TopLeft = topLeft;
            BottomRight = bottomRight;

            yDistance.Update(DistanceBetweenArrowAndText + textHeight, loadMember);
        }

        /// <summary>
        /// 寸法の描画
        /// (実際には描画データをLines, Texts, Arcsなどのリストに格納するまでの処理を行う。リストに格納されたデータは、スケール調整された後に実際に描画される)
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="yDistance">部材との間に置く隙間の高さ</param>
        /// <param name="coef">(描画領域の長辺が紙面上での200mmに相当する場合の)紙面上での1mmを図の座標系で表現するための係数</param>
        /// <returns>描画される寸法の高さ</returns>
        public void PrintDimension(ICanvas canvas, XDistance yDistance, double coef)
        {
            ClearDrawables();

            var center = memberGroup.Center;
            var angle = memberGroup.Angle;

            // 座標変換行列の生成
            var restoringMatrix = new XMatrix();
            restoringMatrix.RotateAtAppend(angle, center);

            var y0 = center.Y + coef * (yDistance.GetDistanceForDimension() + AdditionalDistanceBetweenLoadAndDimension);
            var y1 = y0 + coef * LeaderLineLength;

            // 引き出し線の描画
            foreach (var x in DimensionXs)
            {
                AddLine((restoringMatrix.Transform(new XPoint(x, y0)), restoringMatrix.Transform(new XPoint(x, y1))), coef);
            }

            // 寸法線と寸法値の描画
            var textHeight = 0.0;
            foreach (var (First, Second) in DimensionXs.Take(DimensionXs.Count() - 1).Zip(DimensionXs.Skip(1)))
            {
                AddLine((restoringMatrix.Transform(new XPoint(First, y1)), restoringMatrix.Transform(new XPoint(Second, y1))), coef, ci: LineCap.ArrowAnchor, cj: LineCap.ArrowAnchor);

                var length = Second - First;
                var text = length.ToStringF2();
                var p = new XPoint((First + Second) / 2, y1);
                var textWidth2 = coef * MeasureString(text).Width / 2;
                var left = p.X - textWidth2;
                textHeight = Math.Max(textHeight, MeasureString(text).Height);
                var an = angle;
                var al = XStringFormats.BottomCenter;
                if (XMath.IsAngleIn2ndOr3rdQuadrant(an))
                {
                    an += 180;
                    al = XStringFormats.TopCenter;
                }
                AddText(text, restoringMatrix.Transform(p), coef, an, al);
            }

            PrintDrawables(canvas);

            CalculateDiagramRect(out var topLeft, out var bottomRight);
            TopLeft = topLeft;
            BottomRight = bottomRight;

            yDistance.UpdateForDimension(LeaderLineLength + textHeight); // 矢印の羽は考えなくてもOK
        }

        private readonly XLoadMember loadMember;
        private readonly XMemberGroup memberGroup;

        private readonly List<XQuadrilateral> QuadrilateralList = new List<XQuadrilateral>();
    }
}