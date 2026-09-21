using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 骨組図の寸法引き出し線、寸法線および寸法値を描画する
    /// </summary>
    internal class DiagramDimension : XDrawable
    {
        /// <summary>
        /// 寸法テキストの描画色
        /// </summary>
        private static readonly XBrush DimensionTextColor = XBrushes.Black;
        /// <summary>
        /// 引き出し線と寸法線の描画色
        /// </summary>
        private static readonly XColor DimensionPenColor = XColors.Black;
        /// <summary>
        /// 引き出し線と寸法線の太さ(単位はポイント)
        /// </summary>
        private const double DimensionPenWidth = 0.1;

        /// <summary>
        /// 部材と引き出し線の間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenOuterFrameAndLeaderLine = 10;
        /// <summary>
        /// 描画領域の中心に一番近い引き出し線の長さ(単位はポイント)
        /// </summary>
        private const double DistanceBetweenOuterFrameAndInnerDimensionLine = 10;
        /// <summary>
        /// 寸法線と寸法線の間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenDimensionLines = 10;

        /// <summary>
        /// 寸法線とテキスト下端の間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenDimensionLineAndText = 0.5;

        /// <summary>
        /// 骨組図の寸法引き出し線、寸法線および寸法値の描画情報を生成する
        /// </summary>
        /// <param name="memberGroups">部材グループのコレクション</param>
        /// <param name="frameCenter">描画領域の中心座標</param>
        /// <param name="frameTopLeft">描画領域の左上角の座標</param>
        /// <param name="frameBottomRight">描画領域の右下角の座標</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        public DiagramDimension(IEnumerable<XMemberGroup> memberGroups, XPoint frameCenter, XPoint frameTopLeft, XPoint frameBottomRight,
            XFont font, Func<string, XFont, XSize> measureString) : base(font, measureString)
        {
            var xDimensionInfoList = new List<DimensionInfo>();
            var yDimensionInfoList = new List<DimensionInfo>();
            foreach (var mg in memberGroups)
            {
                xDimensionInfoList.Add(new XDimensionInfo(mg));
                yDimensionInfoList.Add(new YDimensionInfo(mg));
            }

            RemoveProperSubset(xDimensionInfoList);
            RemoveProperSubset(yDimensionInfoList);

            DetermineLocation(xDimensionInfoList, frameCenter);
            DetermineLocation(yDimensionInfoList, frameCenter);

            RemoveDuplication(xDimensionInfoList, yDimensionInfoList);

            DetermineDepth(xDimensionInfoList, yDimensionInfoList, frameCenter);

            PrepareLeaderLines(xDimensionInfoList, yDimensionInfoList, frameTopLeft, frameBottomRight);

            PrepareDimensionLines(xDimensionInfoList, yDimensionInfoList, frameTopLeft, frameBottomRight);

            // XCanvasへの描画

            canvas = new XCanvas();

            PrintDrawables(canvas);
        }

        /// <summary>
        /// 全ての寸法関連データが描画される領域の左上角と右下角の座標を計算する
        /// </summary>
        /// <param name="topLeft">左上角の座標</param>
        /// <param name="bottomRight">右下角の座標</param>
        public void AdjustDiagramRect(ref XPoint topLeft, ref XPoint bottomRight)
        {
            CalculateDiagramRect(out var _topLeft, out var _bottomRight);

            topLeft.X = Math.Min(topLeft.X, _topLeft.X);
            topLeft.Y = Math.Max(topLeft.Y, _topLeft.Y);
            bottomRight.X = Math.Max(bottomRight.X, _bottomRight.X);
            bottomRight.Y = Math.Min(bottomRight.Y, _bottomRight.Y);
        }

        /// <summary>
        /// 全ての寸法データをPDF出力する
        /// </summary>
        /// <param name="frame"></param>
        public void Print(DiagramFrame frame) => canvas.Print(frame, DimensionPenColor, DimensionPenWidth, DimensionTextColor);

        private readonly XCanvas canvas;

        private abstract class DimensionInfo
        {
            public IEnumerable<double> Dimensions { get; }
            public XPoint Center { get; }
            public XLocationType LocationType { get; private set; } = XLocationType.None;
            public int Depth { get; set; }

            protected DimensionInfo(IEnumerable<double> dimensions, XPoint center)
            {
                Dimensions = dimensions;
                Center = center;
            }

            public void DetermineLocation(XPoint frameCenter) => LocationType = DetermineLocation_Impl(frameCenter);
            protected abstract XLocationType DetermineLocation_Impl(XPoint frameCenter);
        }
        private class XDimensionInfo : DimensionInfo
        {
            public XDimensionInfo(XMemberGroup mg) : base(mg.XDimensions, mg.Center) { }

            protected override XLocationType DetermineLocation_Impl(XPoint frameCenter) => Center.Y > frameCenter.Y ? XLocationType.GU : XLocationType.GD;
        }
        private class YDimensionInfo : DimensionInfo
        {
            public YDimensionInfo(XMemberGroup mg) : base(mg.YDimensions, mg.Center) { }

            protected override XLocationType DetermineLocation_Impl(XPoint frameCenter) => Center.X < frameCenter.X ? XLocationType.GL : XLocationType.GR;
        }

        private void RemoveProperSubset(List<DimensionInfo> dimensionInfoList)
        {
            // 節点数の降順に並び替え
            dimensionInfoList.Sort((a, b) => b.Dimensions.Count() - a.Dimensions.Count());

            for (var i = 0; i < dimensionInfoList.Count - 1; i++)
            {
                var mgi = dimensionInfoList[i];

                for (var j = i + 1; j < dimensionInfoList.Count; j++)
                {
                    var mgj = dimensionInfoList[j];

                    if (mgi.Dimensions.Count() > mgj.Dimensions.Count() && !mgj.Dimensions.Except(mgi.Dimensions).Any())
                    {
                        // mgjの全座標はmgiのそれの真部分集合

                        dimensionInfoList.RemoveAt(j--);
                    }
                }
            }
        }

        private void DetermineLocation(List<DimensionInfo> dimensionInfoList, XPoint frameCenter)
        {
            foreach (var di in dimensionInfoList)
            {
                di.DetermineLocation(frameCenter);
            }
        }

        private void RemoveDuplication(List<DimensionInfo> xDimentionInfoList, List<DimensionInfo> yDimentionInfoList)
        {
            var tobeRemoved = new List<int>();

            // 水平方向の寸法の重複の削除

            // y座標の降順に並べ替え(図の上から下の順)
            xDimentionInfoList.Sort((a, b) => Math.Sign(b.Center.Y - a.Center.Y));

            // 削除対象を抽出
            tobeRemoved.Clear();
            for (var i = 0; i < xDimentionInfoList.Count - 1; i++)
            {
                if (tobeRemoved.Contains(i))
                {
                    continue;
                }

                var mgi = xDimentionInfoList[i];

                for (var j = i + 1; j < xDimentionInfoList.Count; j++)
                {
                    if (tobeRemoved.Contains(j))
                    {
                        continue;
                    }

                    var mgj = xDimentionInfoList[j];

                    var a = mgi.Dimensions;
                    var b = mgj.Dimensions;
                    if (!a.Except(b).Any() && !b.Except(a).Any())
                    {
                        tobeRemoved.Add(mgi.LocationType == XLocationType.GU ? i : j); // 下に表示されるものを残す
                    }
                }
            }

            // 削除
            foreach (var index in tobeRemoved.Distinct().OrderByDescending(s => s))
            {
                xDimentionInfoList.RemoveAt(index);
            }

            // 鉛直方向の寸法の重複の削除

            // x座標の降順に並べ替え(図の右から左の順)
            yDimentionInfoList.Sort((a, b) => Math.Sign(b.Center.X - a.Center.X));

            // 削除対象を抽出
            tobeRemoved.Clear();
            for (var i = 0; i < yDimentionInfoList.Count - 1; i++)
            {
                if (tobeRemoved.Contains(i))
                {
                    continue;
                }

                var mgi = yDimentionInfoList[i];

                for (var j = i + 1; j < yDimentionInfoList.Count; j++)
                {
                    if (tobeRemoved.Contains(j))
                    {
                        continue;
                    }

                    var mgj = yDimentionInfoList[j];

                    var a = mgi.Dimensions;
                    var b = mgj.Dimensions;
                    if (!a.Except(b).Any() && !b.Except(a).Any())
                    {
                        tobeRemoved.Add(mgi.LocationType == XLocationType.GL ? i : j); // 右に表示されるものを残す
                    }
                }
            }

            // 削除
            foreach (var index in tobeRemoved.Distinct().OrderByDescending(s => s))
            {
                yDimentionInfoList.RemoveAt(index);
            }
        }

        private void DetermineDepth(List<DimensionInfo> xDimensionInfoList, List<DimensionInfo> yDimensionInfoList, XPoint frameCenter)
        {
            var depthU = 0;
            var depthD = 0;
            foreach (var info in xDimensionInfoList.OrderByDescending(i => i.Dimensions.Count()).ThenBy(i => Math.Abs(i.Center.Y - frameCenter.Y)))
            {
                info.Depth = info.LocationType switch
                {
                    XLocationType.GU => ++depthU,
                    XLocationType.GD => ++depthD,
                    _ => throw new Exception(),
                };
            }

            var depthL = 0;
            var depthR = 0;
            foreach (var info in yDimensionInfoList.OrderByDescending(i => i.Dimensions.Count()).ThenBy(i => Math.Abs(i.Center.X - frameCenter.X)))
            {
                info.Depth = info.LocationType switch
                {
                    XLocationType.GL => ++depthL,
                    XLocationType.GR => ++depthR,
                    _ => throw new Exception(),
                };
            }
        }

        private void PrepareLeaderLines(List<DimensionInfo> xDimensionInfoList, List<DimensionInfo> yDimensionInfoList, XPoint topLeft, XPoint bottomRight)
        {
            // 実物の長辺を200mm四方に縮小した状態における1pt相当の長さ(単位は実物と同じ)
            var coef = Math.Max(bottomRight.X - topLeft.X, topLeft.Y - bottomRight.Y) / (200 * XUnit.FromMillimeter(1));

            foreach (var di in xDimensionInfoList)
            {
                var pitch = coef * (DistanceBetweenOuterFrameAndInnerDimensionLine + Math.Max(di.Depth - 1, 0) * DistanceBetweenDimensionLines);

                switch (di.LocationType)
                {
                    case XLocationType.GU:
                        {
                            var y0 = topLeft.Y + coef * DistanceBetweenOuterFrameAndLeaderLine; // 描画領域中心に近い端のy座標
                            var y1 = y0 + pitch; // 描画領域中心から遠い端のy座標
                            AddLines(di.Dimensions.Select(x => (new XPoint(x, y0), new XPoint(x, y1))));
                        }
                        break;
                    case XLocationType.GD:
                        {
                            var y0 = bottomRight.Y - coef * DistanceBetweenOuterFrameAndLeaderLine; // 描画領域中心に近い端のy座標
                            var y1 = y0 - pitch; // 描画領域中心から遠い端のy座標
                            AddLines(di.Dimensions.Select(x => (new XPoint(x, y0), new XPoint(x, y1))));
                        }
                        break;
                    default:
                        throw new Exception();
                }
            }

            foreach (var di in yDimensionInfoList)
            {
                var pitch = coef * (DistanceBetweenOuterFrameAndInnerDimensionLine + Math.Max(di.Depth - 1, 0) * DistanceBetweenDimensionLines);

                switch (di.LocationType)
                {
                    case XLocationType.GL:
                        {
                            var x0 = topLeft.X - coef * DistanceBetweenOuterFrameAndLeaderLine; // 描画領域中心に近い端のx座標
                            var x1 = x0 - pitch; // 描画領域中心から遠い端のx座標
                            AddLines(di.Dimensions.Select(y => (new XPoint(x0, y), new XPoint(x1, y))));
                        }
                        break;
                    case XLocationType.GR:
                        {
                            var x0 = bottomRight.X + coef * DistanceBetweenOuterFrameAndLeaderLine; // 描画領域中心に近い端のx座標
                            var x1 = x0 + pitch; // 描画領域中心から遠い端のx座標
                            AddLines(di.Dimensions.Select(y => (new XPoint(x0, y), new XPoint(x1, y))));
                        }
                        break;
                    default:
                        throw new Exception();
                }
            }
        }

        private void PrepareDimensionLines(List<DimensionInfo> xDimensionInfoList, List<DimensionInfo> yDimensionInfoList, XPoint topLeft, XPoint bottomRight)
        {
            // 実物の長辺を200mm四方に縮小した状態における1pt相当の長さ(単位は実物と同じ)
            var coef = Math.Max(bottomRight.X - topLeft.X, topLeft.Y - bottomRight.Y) / (200 * XUnit.FromMillimeter(1));

            foreach (var di in xDimensionInfoList)
            {
                var pitch = DistanceBetweenOuterFrameAndInnerDimensionLine + Math.Max(di.Depth - 1, 0) * DistanceBetweenDimensionLines;

                switch (di.LocationType)
                {
                    case XLocationType.GU:
                        {
                            var y = topLeft.Y + coef * (DistanceBetweenOuterFrameAndLeaderLine + pitch);
                            var lines = di.Dimensions.Take(di.Dimensions.Count() - 1).Zip(di.Dimensions.Skip(1)).Where(s => s.First != s.Second).Select(s => (new XPoint(s.First, y), new XPoint(s.Second, y)));
                            foreach (var line in lines)
                            {
                                AddLine(line, coef, LineCap.ArrowAnchor, LineCap.ArrowAnchor);
                                var text = Math.Abs(line.Item1.X - line.Item2.X).ToStringF2();
                                var pos = new XPoint((line.Item1.X + line.Item2.X) / 2, y + coef * DistanceBetweenDimensionLineAndText);
                                AddText(text, pos, coef, 0, XStringFormats.BottomCenter);
                            }
                        }
                        break;
                    case XLocationType.GD:
                        {
                            var y = bottomRight.Y - coef * (DistanceBetweenOuterFrameAndLeaderLine + pitch);
                            var lines = di.Dimensions.Take(di.Dimensions.Count() - 1).Zip(di.Dimensions.Skip(1)).Where(s => s.First != s.Second).Select(s => (new XPoint(s.First, y), new XPoint(s.Second, y)));
                            foreach (var line in lines)
                            {
                                AddLine(line, coef, LineCap.ArrowAnchor, LineCap.ArrowAnchor);
                                var text = Math.Abs(line.Item1.X - line.Item2.X).ToStringF2();
                                var pos = new XPoint((line.Item1.X + line.Item2.X) / 2, y + coef * DistanceBetweenDimensionLineAndText);
                                AddText(text, pos, coef, 0, XStringFormats.BottomCenter);
                            }
                        }
                        break;
                    default:
                        throw new Exception();
                }
            }

            foreach (var di in yDimensionInfoList)
            {
                var pitch = DistanceBetweenOuterFrameAndInnerDimensionLine + Math.Max(di.Depth - 1, 0) * DistanceBetweenDimensionLines;

                switch (di.LocationType)
                {
                    case XLocationType.GL:
                        {
                            var x = topLeft.X - coef * (DistanceBetweenOuterFrameAndLeaderLine + pitch);
                            var lines = di.Dimensions.Take(di.Dimensions.Count() - 1).Zip(di.Dimensions.Skip(1)).Where(s => s.First != s.Second).Reverse().Select(s => (new XPoint(x, s.Second), new XPoint(x, s.First)));
                            foreach (var line in lines)
                            {
                                AddLine(line, coef, LineCap.ArrowAnchor, LineCap.ArrowAnchor);
                                var text = Math.Abs(line.Item1.Y - line.Item2.Y).ToStringF2();
                                var pos = new XPoint(x - coef * DistanceBetweenDimensionLineAndText, (line.Item1.Y + line.Item2.Y) / 2);
                                AddText(text, pos, coef, 90, XStringFormats.BottomCenter);
                            }
                        }
                        break;
                    case XLocationType.GR:
                        {
                            var x = bottomRight.X + coef * (DistanceBetweenOuterFrameAndLeaderLine + pitch);
                            var lines = di.Dimensions.Take(di.Dimensions.Count() - 1).Zip(di.Dimensions.Skip(1)).Where(s => s.First != s.Second).Select(s => (new XPoint(x, s.First), new XPoint(x, s.Second)));
                            foreach (var line in lines)
                            {
                                AddLine(line, coef, LineCap.ArrowAnchor, LineCap.ArrowAnchor);
                                var text = Math.Abs(line.Item1.Y - line.Item2.Y).ToStringF2();
                                var pos = new XPoint(x + coef * DistanceBetweenDimensionLineAndText, (line.Item1.Y + line.Item2.Y) / 2);
                                AddText(text, pos, coef, -90, XStringFormats.BottomCenter);
                            }
                        }
                        break;
                    default:
                        throw new Exception();
                }
            }
        }
    }
}
