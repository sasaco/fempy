using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 温度変化①(線膨張)の描画情報を保持する
    /// </summary>
    internal class XMemberLoad_9 : XDrawable, IXMemberLoad
    {
        /// <summary>
        /// 部材と荷重テキスト下端の間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenMemberAndTextBottom = 3;

        /// <summary>
        /// 温度変化①(線膨張)の描画情報を生成する
        /// </summary>
        /// <param name="loadMember">部材荷重情報</param>
        /// <param name="memberGroup">荷重がかかる部材の属する部材グループ</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        /// <exception cref="Exception"></exception>
        public XMemberLoad_9(XLoadMember loadMember, XMemberGroup memberGroup, XFont font, Func<string, XFont, XSize> measureString)
            : base(font, measureString)
        {
            Debug.Assert(loadMember.Quadrilaterals.Count == 1);

            this.loadMember = loadMember;

            P = loadMember.Quadrilaterals.ElementAt(0).P1;

            var itan = memberGroup.Members.First(m => m.No == loadMember.m1).Ni.Pos;
            var jtan = memberGroup.Members.First(m => m.No == loadMember.m2).Nj.Pos;
            center = new XPoint((itan.X + jtan.X) / 2, (itan.Y + jtan.Y) / 2);

            angle = memberGroup.Angle;

            DimensionXs = Enumerable.Empty<double>();

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

            // 座標変換行列の生成
            var restoringMatrix = new XMatrix();
            restoringMatrix.RotateAtAppend(angle, center);

            var text = $"{P.ToStringF1()}℃";
            var floorHeight = Math.Max(yDistance.GetDistance(loadMember), DistanceBetweenMemberAndTextBottom);
            var textPos = new XPoint(center.X, center.Y + coef * floorHeight);
            AddText(text, restoringMatrix.Transform(textPos), coef, angle, XStringFormats.BottomCenter);

            var textHeight = MeasureString(text).Height;

            PrintDrawables(canvas);

            CalculateDiagramRect(out var topLeft, out var bottomRight);
            TopLeft = topLeft;
            BottomRight = bottomRight;

            yDistance.Update(textHeight, loadMember);
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
            yDistance.UpdateForDimension(0);
        }

        private readonly XLoadMember loadMember;

        /// <summary>
        /// 荷重値
        /// </summary>
        private readonly double P;
        /// <summary>
        /// 部材軸の中点の座標
        /// </summary>
        private readonly XPoint center;
        /// <summary>
        /// 部材軸の傾き(°)
        /// </summary>
        private readonly double angle;
    }
}
