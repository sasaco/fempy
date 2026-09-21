using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 部材直角方向分布荷重の描画情報を保持する
    /// </summary>
    internal class XMemberLoad_2y : XDrawable, IXMemberLoad
    {
        /// <summary>
        /// 部材直角方向分布荷重の最大値に対応する荷重矢印本体の長さ(単位はポイント)
        /// </summary>
        private const double MaxHeightOfMemberLoad = 40;
        /// <summary>
        /// 荷重矢印間隔の最小値(単位はポイント)
        /// </summary>
        private const double MinimumInterval = 20;
        /// <summary>
        /// 荷重と荷重テキスト上端または下端の間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenLoadAndText = 1;

        /// <summary>
        /// 荷重テキストと荷重寸法線の重なりを軽減するための間隔(単位はポイント)
        /// </summary>
        private const double AdditionalDistanceBetweenLoadAndDimension = 10;
        /// <summary>
        /// 荷重と荷重寸法引き出し線の間隔(単位はポイント)
        /// </summary>
        private const double LeaderLineLength = 10;
        /// <summary>
        /// 隣接する荷重テキストの重複を回避するための表示位置の底上げ量(単位はポイント)
        /// </summary>
        private const double RaisingHeight = 10;

        /// <summary>
        /// 部材方向分布荷重の描画情報を生成する
        /// </summary>
        /// <param name="loadMember">部材荷重情報</param>
        /// <param name="memberGroup">荷重がかかる部材の属する部材グループ</param>
        /// <param name="pMax">部材方向分布荷重の最大値</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        public XMemberLoad_2y(XLoadMember loadMember, XMemberGroup memberGroup, double pMax, XFont font, Func<string, XFont, XSize> measureString)
            : base(font, measureString)
        {
            Debug.Assert(loadMember.Quadrilaterals.Count > 0);

            this.loadMember = loadMember;
            this.memberGroup = memberGroup;
            this.pMax = pMax;

            var x0 = memberGroup.NHDic[loadMember.m1].NHi.X;
            var quadrilaterals = loadMember.Quadrilaterals.Select(q => new XQuadrilateral(x0 + q.X1, q.P1, x0 + q.X2, q.P2));

            QuadrilateralList.AddRange(quadrilaterals);

            DimensionXs = memberGroup.NHDic.Values.SelectMany(n => new[] { n.NHi.X, n.NHj.X, }).Concat(QuadrilateralList.SelectMany(po => new[] { po.X1, po.X2, })).DistinctLoosely().OrderBy(x => x);

            var lm0 = loadMember.Quadrilaterals.ElementAt(0);
            PrintLocation = lm0.P1 < 0 || (lm0.P1 == 0 && lm0.P2 < 0) ? XLocationType.YM : XLocationType.YP;
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
            Debug.Assert(PrintLocation == XLocationType.YM || PrintLocation == XLocationType.YP);

            ClearDrawables();

            var center = memberGroup.Center;
            var angle = memberGroup.Angle;

            // 荷重線の縮尺
            var scale = coef * MaxHeightOfMemberLoad / pMax;

            // 座標変換行列の生成
            var restoringMatrix = new XMatrix();
            restoringMatrix.RotateAtAppend(angle, center);

            // 荷重図形とテキスト間の隙間
            var spacingVector = new XVector(0, DistanceBetweenLoadAndText * coef);

            var textAngle = PrintLocation == XLocationType.YP ? angle + 90 : angle -90;

            var distance = yDistance.GetDistance(loadMember);

            if (PrintLocation == XLocationType.YM)
            {
                distance = -distance;
            }

            // 上げ底
            var max = QuadrilateralList.Select(q => Math.Max(q.P1, q.P2)).Max();
            var min = QuadrilateralList.Select(q => Math.Min(q.P1, q.P2)).Min();
            if (max * min < 0)
            {
                if (PrintLocation == XLocationType.YM)
                {
                    distance -= max * scale / coef;
                }
                else
                {
                    distance -= min * scale / coef;
                }
            }

            var textHeight = 0.0;
            foreach (var (prev, po, neXt) in QuadrilateralList.WithPreviousAndNext(firstPrevious: QuadrilateralList.First(), lastNext: QuadrilateralList.Last()))
            {
                var lowerSole1 = new XPoint(po.X1, center.Y + coef * distance);
                var upperSole1 = new XPoint(po.X1, lowerSole1.Y + po.P1 * scale);
                if (upperSole1 != lowerSole1)
                {
                    AddLine((restoringMatrix.Transform(upperSole1), restoringMatrix.Transform(lowerSole1)), coef, cj: LineCap.ArrowAnchor);

                    var textPosBase = (PrintLocation == XLocationType.YP && lowerSole1.Y < upperSole1.Y) || (PrintLocation == XLocationType.YM && lowerSole1.Y > upperSole1.Y)
                        ? upperSole1
                        : lowerSole1;
                    var textPos = restoringMatrix.Transform(textPosBase + spacingVector);

                    if (po.P1 != po.P2)
                    {
                        if (prev.P2 != po.P1)
                        {
                            var text = $"{po.P1.ToStringF2()}kN/m";
                            var an = textAngle;
                            var al = PrintLocation == XLocationType.YP ? XStringFormats.TopLeft : XStringFormats.BottomLeft;
                            if (XMath.IsAngleIn2ndOr3rdQuadrant(textAngle))
                            {
                                an += 180;
                                al = PrintLocation == XLocationType.YP ? XStringFormats.BottomRight : XStringFormats.TopRight;
                            }
                            AddText(text, textPos, coef, an, al);

                            textHeight = Math.Max(textHeight, MeasureString(text).Width);
                        }
                        else
                        {
                            var text = $"{po.P1.ToStringF2()}kN/m";
                            var an = textAngle;
                            var al = XStringFormats.CenterLeft;
                            if (XMath.IsAngleIn2ndOr3rdQuadrant(textAngle))
                            {
                                an += 180;
                                al = XStringFormats.CenterRight;
                            }
                            AddText(text, textPos, coef, an, al);

                            textHeight = Math.Max(textHeight, MeasureString(text).Width);
                        }
                    }
                }

                var lowerSole2 = new XPoint(po.X2, lowerSole1.Y);
                var upperSole2 = new XPoint(po.X2, lowerSole2.Y + po.P2 * scale);

                AddLine((restoringMatrix.Transform(lowerSole1), restoringMatrix.Transform(lowerSole2))); // 下底の描画 // @TODO: 必ず一直線なので一発描画可能
                AddLine((restoringMatrix.Transform(upperSole1), restoringMatrix.Transform(upperSole2))); // 上底の描画

                if (upperSole2 != lowerSole2)
                {
                    AddLine((restoringMatrix.Transform(upperSole2), restoringMatrix.Transform(lowerSole2)), coef, cj: LineCap.ArrowAnchor);

                    var textPosBase = (PrintLocation == XLocationType.YP && lowerSole2.Y < upperSole2.Y) || (PrintLocation == XLocationType.YM && lowerSole2.Y > upperSole2.Y)
                        ? upperSole2
                        : lowerSole2;
                    var textPos = restoringMatrix.Transform(textPosBase + spacingVector);

                    if (po.P1 != po.P2)
                    {
                        if (po.P2 != neXt.P1)
                        {
                            var text = $"{po.P2.ToStringF2()}kN/m";
                            var an = textAngle;
                            var al = PrintLocation == XLocationType.YP ? XStringFormats.BottomLeft : XStringFormats.TopLeft;
                            if (XMath.IsAngleIn2ndOr3rdQuadrant(textAngle))
                            {
                                an += 180;
                                al = PrintLocation == XLocationType.YP ? XStringFormats.TopRight : XStringFormats.BottomRight;
                            }
                            AddText(text, textPos, coef, an, al);

                            textHeight = Math.Max(textHeight, MeasureString(text).Width);
                        }
                        else
                        {
                            var text = $"{po.P2.ToStringF2()}kN/m";
                            var an = textAngle;
                            var al = XStringFormats.CenterLeft;
                            if (XMath.IsAngleIn2ndOr3rdQuadrant(textAngle))
                            {
                                an += 180;
                                al = XStringFormats.CenterRight;
                            }
                            AddText(text, textPos, coef, an, al);

                            textHeight = Math.Max(textHeight, MeasureString(text).Width);
                        }
                    }
                }

                if (po.P1 == po.P2)
                {
                    var c = new XPoint((upperSole1.X + upperSole2.X) / 2, (upperSole1.Y + upperSole2.Y) / 2);
                    var an = angle + (PrintLocation == XLocationType.YM ? 180 : 0);
                    var al = XStringFormats.BottomCenter;
                    if (XMath.IsAngleIn2ndOr3rdQuadrant(an))
                    {
                        an += 180;
                        al = XStringFormats.TopCenter;
                    }
                    var text = $"{po.P1.ToStringF2()}kN/m";
                    AddText(text, restoringMatrix.Transform(c + spacingVector), coef, an, al);

                    textHeight = Math.Max(textHeight, MeasureString(text).Height);
                }

                // P1とP2間の矢印の描画
                var n = (int)((po.X2 - po.X1) / (coef * MinimumInterval));
                if (n >= 2)
                {
                    var interval = (po.X2 - po.X1) / n;
                    for (var x = po.X1 + interval; --n > 0; x += interval)
                    {
                        var lowerSoleN = new XPoint(x, lowerSole1.Y);
                        var pN = (po.P2 - po.P1) / (po.X2 - po.X1) * (x - po.X1) + po.P1;
                        var upperSoleN = new XPoint(x, lowerSoleN.Y + pN * scale);
                        AddLine((restoringMatrix.Transform(upperSoleN), restoringMatrix.Transform(lowerSoleN)), coef, cj: LineCap.ArrowAnchor);
                    }
                }
            }

            PrintDrawables(canvas);

            CalculateDiagramRect(out var topLeft, out var bottomRight);
            TopLeft = topLeft;
            BottomRight = bottomRight;

            var delta = QuadrilateralList.SelectMany(po =>
            {
                var (P1, P2) = (Math.Abs(po.P1), Math.Abs(po.P2));
                return po.P1 * po.P2 < 0 ? new[] { P1 + P2 } : new[] { P1, P2 };
            }).Max() * MaxHeightOfMemberLoad / pMax + DistanceBetweenLoadAndText + textHeight;
            yDistance.Update(delta, loadMember);
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
            if (PrintLocation == XLocationType.YM)
            {
                // x軸を軸として鏡映変換
                restoringMatrix.ScaleAtAppend(1, -1, center.X, center.Y);
            }
            restoringMatrix.RotateAtAppend(angle, center);

            var y0 = center.Y + coef * (yDistance.GetDistanceForDimension() + AdditionalDistanceBetweenLoadAndDimension);
            var y1 = y0 + coef * LeaderLineLength;

            // 引き出し線の描画
            foreach (var x in DimensionXs)
            {
                AddLine((restoringMatrix.Transform(new XPoint(x, y0)), restoringMatrix.Transform(new XPoint(x, y1))), coef);
            }

            // 寸法線と寸法値の描画
            var prev_right = double.MinValue;
            var prev_placed_nearly = false;
            var raisingVector = new XVector(0, coef * RaisingHeight);
            var textHeight = 0.0;
            foreach (var (First, Second) in DimensionXs.Take(DimensionXs.Count() - 1).Zip(DimensionXs.Skip(1)))
            {
                AddLine((restoringMatrix.Transform(new XPoint(First, y1)), restoringMatrix.Transform(new XPoint(Second, y1))), coef, ci: LineCap.ArrowAnchor, cj: LineCap.ArrowAnchor);

                var length = Second - First;
                var text = length.ToStringF2();
                var p = new XPoint((First + Second) / 2, y1);
                var textWidth2 = coef * MeasureString(text).Width / 2;
                var left = p.X - textWidth2;
                // 連続する区間の寸法値のテキストが重なる場合は表示位置を交互に上げ下げする
                if (prev_right > left && prev_placed_nearly)
                {
                    p += raisingVector;
                    prev_placed_nearly = false;
                    textHeight = Math.Max(textHeight, MeasureString(text).Height + RaisingHeight);
                }
                else
                {
                    prev_placed_nearly = true;
                    textHeight = Math.Max(textHeight, MeasureString(text).Height);
                }
                prev_right = p.X + textWidth2;
                var an = angle + (PrintLocation == XLocationType.YM ? 180 : 0);
                if (XMath.IsAngleIn2ndOr3rdQuadrant(an))
                {
                    an += 180;
                }
                AddText(text, restoringMatrix.Transform(p), coef, an, XStringFormats.BottomCenter);
            }

            PrintDrawables(canvas);

            CalculateDiagramRect(out var topLeft, out var bottomRight);
            TopLeft = topLeft;
            BottomRight = bottomRight;

            yDistance.UpdateForDimension(LeaderLineLength + textHeight); // 矢印の羽は考えなくてもOK
        }

        private readonly XLoadMember loadMember;
        private readonly XMemberGroup memberGroup;
        private readonly double pMax;

        private readonly List<XQuadrilateral> QuadrilateralList = new List<XQuadrilateral>();
    }
}
