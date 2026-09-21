using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// モーメント荷重の描画情報を保持する
    /// </summary>
    internal class XMemberLoad_11z : XDrawable, IXMemberLoad
    {
        /// <summary>
        /// モーメント円弧の半径(単位は度)
        /// </summary>
        private const double ArcRadius = 10;
        /// <summary>
        /// モーメント円弧の描画開始角度(3時の方向を0として反時計回りが正)(単位は度)
        /// </summary>
        private const double ArcStartAngle = -45;
        /// <summary>
        /// モーメント円弧の描画開始から終了までの角度(反時計回りが正)(単位は度)
        /// </summary>
        private const double ArcSweepAngle = -2 * ArcStartAngle + 180;
        /// <summary>
        /// モーメント円弧中心と荷重値テキストの間隔(単位はポイント)
        /// </summary>
        private static readonly double DistanceBetweenCenterAndTextHead = ArcRadius * Math.Abs(Math.Sin(ArcStartAngle * Math.PI / 180));
        /// <summary>
        /// 荷重と荷重寸法引き出し線の間隔(単位はポイント)
        /// </summary>
        private const double LeaderLineLength = 10;

        /// <summary>
        /// 部材集謬モーメント荷重の描画情報を生成する
        /// </summary>
        /// <param name="loadMember">部材荷重情報</param>
        /// <param name="memberGroup">荷重がかかる部材の属する部材グループ</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        /// <exception cref="Exception"></exception>
        public XMemberLoad_11z(XLoadMember loadMember, XMemberGroup memberGroup, XFont font, Func<string, XFont, XSize> measureString)
            : base(font, measureString)
        {
            Debug.Assert(loadMember.Quadrilaterals.Count > 0);

            this.loadMember = loadMember;
            this.memberGroup = memberGroup;

            var x0 = memberGroup.NHDic[loadMember.m1].NHi.X;
            var xpList = loadMember.Quadrilaterals
                .SelectMany(q => new[] { new XPPair(x0 + q.X1, q.P1), new XPPair(x0 + q.X2, q.P2), })
                .Where(xp => xp.P != 0);
            XPPairList.AddRange(xpList);

            var x1 = memberGroup.NHDic[loadMember.m2].NHj.X;
            var xlist = new List<double> { x0, x1, };
            DimensionXs = xlist.Concat(XPPairList.Select(xp => xp.X)).DistinctLoosely().OrderBy(x => x);

            PrintLocation = XLocationType.C;
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
            var angle = memberGroup.Angle;

            // 座標変換行列の生成
            var restoringMatrix = new XMatrix();
            restoringMatrix.RotateAtAppend(angle, center);

            foreach (var xp in XPPairList)
            {
                Debug.Assert(xp.P != 0);

                if (xp.P > 0)
                {
                    // 反時計回り

                    var arcPos = restoringMatrix.Transform(new XPoint(xp.X, center.Y));
                    AddArc(arcPos, coef * ArcRadius, ArcStartAngle + angle, ArcSweepAngle, coef, startCap: LineCap.ArrowAnchor);

                    var text = $"{xp.P.ToStringF2()}kN･m";
                    var textPos = arcPos + new XVector(0, -coef * DistanceBetweenCenterAndTextHead);
                    AddText(text, textPos, coef, 0, XStringFormats.TopCenter);
                }
                else
                {
                    // 時計回り

                    var arcPos = restoringMatrix.Transform(new XPoint(xp.X, center.Y));
                    AddArc(arcPos, coef * ArcRadius, ArcStartAngle + angle, ArcSweepAngle, coef, endCap: LineCap.ArrowAnchor);

                    var text = $"{(-xp.P).ToStringF2()}kN･m"; // @TODO: 符号は大丈夫？
                    var textPos = arcPos + new XVector(0, -coef * DistanceBetweenCenterAndTextHead);
                    AddText(text, textPos, coef, 0, XStringFormats.TopCenter);
                }
            }

            PrintDrawables(canvas);

            CalculateDiagramRect(out var topLeft, out var bottomRight);
            TopLeft = topLeft;
            BottomRight = bottomRight;

            yDistance.Update(ArcRadius, loadMember);
        }

        /// <summary>
        /// 寸法の描画
        /// (実際には描画データをLines, Texts, Arcsなどのリストに格納するまでの処理を行う。リストに格納されたデータは、スケール調整された後に実際に描画される)
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="yDistance">部材との間に置く隙間の高さ</param>
        /// <param name="coef">(描画領域の長辺が紙面上での200mmに相当する場合の)紙面上での1mmを図の座標系で表現するための係数</param>
        public void PrintDimension(ICanvas canvas, XDistance yDistance, double coef)
        {
            ClearDrawables();

            var center = memberGroup.Center;
            var angle = memberGroup.Angle;

            // 座標変換行列の生成
            var restoringMatrix = new XMatrix();
            restoringMatrix.RotateAtAppend(angle, center);

            var y0 = center.Y + coef * yDistance.GetDistanceForDimension();
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
                AddText(text, restoringMatrix.Transform(p), coef, angle, XStringFormats.BaseLineCenter);

                textHeight = Math.Max(textHeight, MeasureString(text).Height);
            }

            PrintDrawables(canvas);

            CalculateDiagramRect(out var topLeft, out var bottomRight);
            TopLeft = topLeft;
            BottomRight = bottomRight;

            yDistance.UpdateForDimension(LeaderLineLength + textHeight);
        }

        private readonly XLoadMember loadMember;
        private readonly XMemberGroup memberGroup;

        private readonly struct XPPair
        {
            public double X { get; }
            public double P { get; }

            public XPPair(double x, double p) => (X, P) = (x, p);
        }
        private readonly List<XPPair> XPPairList = new List<XPPair>();
    }
}
