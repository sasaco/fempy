
// 描画枠の表示
//#define DRAWS_DRAWING_FRAME

using PDF_Manager.Comon;
using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;

namespace PDF_Manager.Printing
{
    class DiagramFrame
    {

        // 軸線スケール
        public double ScaleX { get; private set; }
        public double ScaleY { get; private set; }

        // 軸線スケール (JSONファイルで指定されたもの)
        public double SpecifiedScaleX { get; private set; }
        public double SpecifiedScaleY { get; private set; }

        // 位置補正
        public double posX { get; private set; }
        public double posY { get; private set; }

        // 文字サイズ
        private double fontSize;

        // 描画情報
        public Layout mode { get; private set; }

        // 言語
        public string language;


        // 軸線を作成するのに必要な情報

        // 節点情報
        public InputNode Node;
        // 要素情報
        public InputMember Member;
        // 材料情報
        public InputElement Element;
        // 支点情報
        public InputFixNode FixNode;
        // バネ情報
        public InputFixMember FixMember;

        public DiagramFrame(Dictionary<string, object> target)
        {

            // 軸線スケール (JSONファイルで指定されたもの)
            SpecifiedScaleX = dataManager.parseDouble(target, "scaleX");
            SpecifiedScaleY = dataManager.parseDouble(target, "scaleY");

            // 位置補正
            posX = dataManager.parseDouble(target, "posX");
            posY = dataManager.parseDouble(target, "posY");

            // 文字サイズ
            fontSize = dataManager.parseDouble(target, "fontSize");

            // 描画情報
            string layout = target.ContainsKey("layout") ? target["layout"].ToString() : "Default";
            switch (layout.ToLower())
            {
                case "splithorizontal":
                case "splithorizontal2":
                    mode = Layout.SplitHorizontal;
                    break;
                case "splithorizontal3":
                    mode = Layout.SplitHorizontal3;
                    break;
                case "splithorizontal4":
                    mode = Layout.SplitHorizontal4;
                    break;
                case "splitvertical":
                case "splitvertical2":
                    mode = Layout.SplitVertical;
                    break;
                case "splitvertical3":
                    mode = Layout.SplitVertical3;
                    break;
                case "splitvertical4":
                    mode = Layout.SplitVertical4;
                    break;
                default:
                    mode = Layout.Default;
                    break;
            }

        }

        // 図を描くためのモジュール
        public diagramManager canvas;
        // 骨組の中心座標
        private XPoint _CenterPos;
        public XPoint CenterPos { get { return _CenterPos; } }

        public int isOlderVer2 = 1;

        /// <summary>
        /// 骨組図の作成
        /// </summary>
        /// <param name="mc"></param>
        /// <param name="data">入力データ</param>
        public void printInit(PdfDocument mc, PrintData data)
        {
            language = data.language;

            // Ver2 より古ければ 1
            isOlderVer2 = (data.isOlderVer2) ? 1 : -1;

            // 部材長を取得できる状態にする
            Node = (InputNode)data.printDatas[InputNode.KEY];

            // 要素を取得できる状態にする
            Member = (InputMember)data.printDatas[InputMember.KEY];

            // 材料名称を取得できる状態にする
            Element = (InputElement)data.printDatas[InputElement.KEY];

            // 支点情報を取得できる状態にする
            FixNode = (InputFixNode)data.printDatas[InputFixNode.KEY];

            // バネ情報を取得できる状態にする
            FixMember = (InputFixMember)data.printDatas[InputFixMember.KEY];

            // 要素の入力情報から２次情報をあらかじめ計算しておく
            foreach (var m1 in Member.members.Values)
            {
                //要素の節点i,jの情報を取得
                Vector3 pi = Node.GetNodePos(m1.ni);   // 描画中の要素のi端座標情報
                Vector3 pj = Node.GetNodePos(m1.nj);   // 描画中の要素のj端座標情報

                // 部材長さ
                m1.L = Math.Sqrt(Math.Pow(pj.x - pi.x, 2) + Math.Pow(pj.y - pi.y, 2));

                //節点情報の座標を取得
                var xL = (pj.x - pi.x) / m1.L;
                var yL = (pj.y - pi.y) / m1.L;

                // 座標変換マトリックス
                var qq = Math.Sqrt(Math.Pow(xL, 2) + Math.Pow(yL, 2));
                m1.t = new double[3, 3] { { xL, yL, 0 }, { -yL / qq, xL / qq, 0 }, { 0, 0, qq } };

                // 角度
                m1.radian = Math.Atan2(yL, xL);
            }
        }

        /// <summary>
        /// 描画領域の生成とレイアウトの設定
        /// </summary>
        /// <param name="mc"></param>
        /// <param name="layout">用紙レイアウト(未指定時はJSONファイルのlayoutの設定が参照される)</param>
        public void SetLayout(PdfDocument mc, Layout? layout = null)
        {
            canvas = new diagramManager(mc, layout ?? mode);
        }

        /// <summary>
        /// タイトルの印字
        /// </summary>
        /// <param name="title">タイトル</param>
        /// <param name="currentArea">骨組を描く位置</param>
        public void PrintTitle(string title, int currentArea = 0)
        {
            canvas.currentArea = currentArea;

            var x = canvas.TitlePos.X;
            var y = canvas.TitlePos.Y;

            canvas.printText(x, y, title);
        }

        /// <summary>
        /// 節点データから描画領域の左上角と右下角の座標を計算する(y軸の方向が逆なので実際は左下角と右上角)
        /// </summary>
        /// <param name="topLeft">描画領域の左上角の座標(y軸の方向が逆なので実際は左下角)</param>
        /// <param name="bottomRight">描画領域の右下角の座標(y軸の方向が逆なので実際は右上角)</param>
        public void CalculateDiagramRect(out XPoint topLeft, out XPoint bottomRight)
        {
            topLeft = new XPoint(double.MaxValue, double.MinValue);     // 節点の最も左上
            bottomRight = new XPoint(double.MinValue, double.MaxValue); // 節点の最も右下
            foreach (var n in Node.Nodes.Values)
            {
                if (n.x < topLeft.X)
                    topLeft.X = n.x;
                if (n.x > bottomRight.X)
                    bottomRight.X = n.x;
                if (isOlderVer2 * n.y < bottomRight.Y)
                    bottomRight.Y = isOlderVer2 * n.y;
                if (isOlderVer2 * n.y > topLeft.Y)
                    topLeft.Y = isOlderVer2 * n.y;
            }
        }

        /// <summary>
        /// 描画領域の中心の座標を求める
        /// </summary>
        /// <param name="topLeft">描画領域の左上角の座標(y軸の方向が逆なので実際は左下角)</param>
        /// <param name="bottomRight">描画領域の右下角の座標(y軸の方向が逆なので実際は右上角)</param>
        public void CalculateCenterPos(XPoint topLeft, XPoint bottomRight)
        {
            _CenterPos = new XPoint((topLeft.X + bottomRight.X) / 2, (topLeft.Y + bottomRight.Y) / 2);
        }

        /// <summary>
        /// 描画領域の鉛直方向の中心の座標を求める(中心軸がズレるのを回避するため水平方向の中心座標は更新しない)
        /// </summary>
        /// <param name="topLeft">描画領域の左上角の座標(y軸の方向が逆なので実際は左下角)</param>
        /// <param name="bottomRight">描画領域の右下角の座標(y軸の方向が逆なので実際は右上角)</param>
        public void CalculateVerticalCenterPos(XPoint topLeft, XPoint bottomRight)
        {
            _CenterPos = new XPoint(CenterPos.X, (topLeft.Y + bottomRight.Y) / 2);
        }

        /// <summary>
        /// 描画スケールを計算する
        /// </summary>
        /// <param name="topLeft">描画領域の左上角の座標(y軸の方向が逆なので実際は左下角)</param>
        /// <param name="bottomRight">描画領域の右下角の座標(y軸の方向が逆なので実際は右上角)</param>
        public void CalculateScale(XPoint topLeft, XPoint bottomRight)
        {
            if (double.IsNaN(SpecifiedScaleX) && double.IsNaN(SpecifiedScaleY))
            {
                setScaleX(topLeft, bottomRight);
                setScaleY(topLeft, bottomRight);
                // 小さい方に合わせる
                if (ScaleY <= 0)
                    ScaleY = ScaleX;
                else if (ScaleX <= 0)
                    ScaleX = ScaleY;
                else if (ScaleY < ScaleX)
                    ScaleX = ScaleY;
                else
                    ScaleY = ScaleX;
            }
            else if (double.IsNaN(SpecifiedScaleX))
            {
                setScaleX(topLeft, bottomRight);
                ScaleY = SpecifiedScaleY * XUnit.FromMillimeter(1000); // メートル(m)単位からポイント(pt)単位への変換と指定されたスケールの反映
            }
            else if (double.IsNaN(SpecifiedScaleY))
            {
                ScaleX = SpecifiedScaleX * XUnit.FromMillimeter(1000); // メートル(m)単位からポイント(pt)単位への変換と指定されたスケールの反映
                setScaleY(topLeft, bottomRight);
            }
            else
            {
                ScaleX = SpecifiedScaleX * XUnit.FromMillimeter(1000); // メートル(m)単位からポイント(pt)単位への変換と指定されたスケールの反映
                ScaleY = SpecifiedScaleY * XUnit.FromMillimeter(1000); // メートル(m)単位からポイント(pt)単位への変換と指定されたスケールの反映
            }
        }

        /// <summary>
        /// 中心軸がズレない範囲で横の縮尺をセットする
        /// </summary>
        /// <param name="LeftTop"></param>
        /// <param name="RightBottom"></param>
        private void setScaleX(XPoint LeftTop, XPoint RightBottom)
        {
            var frameWidthL = Math.Abs(LeftTop.X - CenterPos.X);
            var frameWidthR = Math.Abs(RightBottom.X - CenterPos.X);
            var paperWidth2 = canvas.areaSize.Width / 2;
            ScaleX = paperWidth2 / Math.Max(frameWidthL, frameWidthR);
        }

        /// <summary>
        /// 縦の縮尺をセットする
        /// </summary>
        /// <param name="LeftTop"></param>
        /// <param name="RightBottom"></param>
        private void setScaleY(XPoint LeftTop, XPoint RightBottom)
        {
            var frameHeight = Math.Abs(LeftTop.Y - RightBottom.Y);
            var paperHeight = canvas.areaSize.Height;
            ScaleY = paperHeight / frameHeight;
        }

        /// <summary>
        /// 骨組みを印字する
        /// </summary>
        /// <param name="currentArea">骨組を描く位置</param>
        /// <param name="isNode">節点を描くか？</param>
        public void printFrame(int currentArea = 0, bool isNode = true)
        {
            canvas.currentArea = currentArea;

#if DRAWS_DRAWING_FRAME
            canvas.mc.xpen = new XPen(XColors.Cyan, 0.1);

            // 描画枠の描画(TrueAreaSize)
            canvas.printLine(
                -canvas.TrueAreaSize.Width / 2, -canvas.TrueAreaSize.Height / 2,
                +canvas.TrueAreaSize.Width / 2, -canvas.TrueAreaSize.Height / 2
            );
            canvas.printLine(
                +canvas.TrueAreaSize.Width / 2, -canvas.TrueAreaSize.Height / 2,
                +canvas.TrueAreaSize.Width / 2, +canvas.TrueAreaSize.Height / 2
            );
            canvas.printLine(
                +canvas.TrueAreaSize.Width / 2, +canvas.TrueAreaSize.Height / 2,
                -canvas.TrueAreaSize.Width / 2, +canvas.TrueAreaSize.Height / 2
            );
            canvas.printLine(
                -canvas.TrueAreaSize.Width / 2, +canvas.TrueAreaSize.Height / 2,
                -canvas.TrueAreaSize.Width / 2, -canvas.TrueAreaSize.Height / 2
            );

            // 描画枠の描画(areaSize)
            canvas.printLine(
                -canvas.areaSize.Width / 2, -canvas.areaSize.Height / 2,
                +canvas.areaSize.Width / 2, -canvas.areaSize.Height / 2
            );
            canvas.printLine(
                +canvas.areaSize.Width / 2, -canvas.areaSize.Height / 2,
                +canvas.areaSize.Width / 2, +canvas.areaSize.Height / 2
            );
            canvas.printLine(
                +canvas.areaSize.Width / 2, +canvas.areaSize.Height / 2,
                -canvas.areaSize.Width / 2, +canvas.areaSize.Height / 2
            );
            canvas.printLine(
                -canvas.areaSize.Width / 2, +canvas.areaSize.Height / 2,
                -canvas.areaSize.Width / 2, -canvas.areaSize.Height / 2
            );
#endif

            // 骨組の描写
            canvas.mc.xpen = new XPen(XColors.Black, 1);

            // 要素を取得できる状態にする
            foreach (var mm in Member.members.Values)
            {
                var pi = Node.GetNodePos(mm.ni);
                var pj = Node.GetNodePos(mm.nj);

                //n スケール調整
                var xi = (pi.x - _CenterPos.X) * ScaleX;
                var yi = -((isOlderVer2 * pi.y) - _CenterPos.Y) * ScaleY;
                var xj = (pj.x - _CenterPos.X) * ScaleX;
                var yj = -((isOlderVer2 * pj.y) - _CenterPos.Y) * ScaleY;

                canvas.printLine(xi, yi, xj, yj);
            }

            if (isNode == true)
            {
                // 節点データ
                foreach (var pp in Node.Nodes.Values)
                {
                    var x = (pp.x - _CenterPos.X) * ScaleX;
                    var y = -((isOlderVer2 * pp.y) - _CenterPos.Y) * ScaleY;

                    canvas.printNode(x, y);
                }
            }
        }
    }
}
