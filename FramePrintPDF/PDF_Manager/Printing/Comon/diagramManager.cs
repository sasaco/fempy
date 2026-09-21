using PDF_Manager.Printing.Comon;
using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PDF_Manager.Printing
{
    // 図のレイアウト
    public enum Layout
    {
        Default,            // 1ページに 1つの応力図/荷重図
        SplitHorizontal,    // 1ページに 2つの応力図/荷重図を上下に配置
        SplitVertical,      // 1ページに 2つの応力図/荷重図を左右に配置
        SplitHorizontal3,   // 1ページに 3つの応力図/荷重図を上下に配置
        SplitVertical3,     // 1ページに 3つの応力図/荷重図を左右に配置
        SplitHorizontal4,   // 1ページに 4つの応力図/荷重図を上下に配置
        SplitVertical4,     // 1ページに 4つの応力図/荷重図を左右に配置
    }

    internal class diagramManager
    {
        private double nodeSize = 2;        // 節点の円の大きさ
        private double nodePenWidth = 0.2;  // 節点の線幅

        public PdfDocument mc;

        /// <summary>
        /// デフォルトのフォントサイズ 
        /// pt ポイント　（1pt = 1/72 インチ)
        /// </summary>
        public static double FontSize = 8;

        // 図のレイアウト
        private Layout mode;        // 図のレイアウト
        private XPoint[] _Center = new[]  // 描く図の紙面における中心位置
        {
            new XPoint(0, 0),
            new XPoint(0, 0),
            new XPoint(0, 0),
            new XPoint(0, 0),
        };
        public XPoint Center(int area)
        {
            return _Center[area];
        }

        // タイトル描画開始位置
        public XPoint TitlePos { get; }

        // 縮小前の図の描画エリアのサイズ
        public XSize TrueAreaSize { get; }

        // 図の描画エリアのサイズ縮小率(横)
        private const double shrinkScaleX = 0.9;
        // 図の描画エリアのサイズ縮小率(縦)
        private const double shrinkScaleY = 0.8;

        // 図の描画エリアのサイズ
        private XSize AreaSize = new XSize(0, 0);
        public XSize areaSize
        {
            get
            {
                return this.AreaSize;
            }
        }

        public diagramManager(PdfDocument mc, Layout _mode)
        {
            // キャンパス
            this.mc = mc;

            // 線幅などの初期化
            this.mc.xpen = new XPen(XColors.Black, nodePenWidth);

            // レイアウト
            this.mode = _mode;

            // 図描画エリアの幅、高さ、および中心位置を決定する.
            var paper = this.mc.currentPageSize;    // マージンを引いたエリア
            this.AreaSize.Width = paper.Width;
            this.AreaSize.Height = paper.Height;

            var xsplit = mode switch
            {
                Layout.SplitVertical => 2,
                Layout.SplitVertical3 => 3,
                Layout.SplitVertical4 => 4,
                _ => 1,
            };
            var ysplit = mode switch
            {
                Layout.SplitHorizontal => 2,
                Layout.SplitHorizontal3 => 3,
                Layout.SplitHorizontal4 => 4,
                _ => 1,
            };

            AreaSize.Width /= xsplit;
            AreaSize.Height /= ysplit;

            var trueAreaHeight = AreaSize.Height;
            AreaSize.Height -= printManager.FontHeight; // タイトルの高さを描画エリアの高さから差し引く

            if (xsplit == 1)
            {
                for (var i = 0; i < ysplit; ++i)
                {
                    _Center[i].X = mc.Margine.Left + areaSize.Width * 0.5;
                    _Center[i].Y = mc.Margine.Top + trueAreaHeight * (i + 1) - areaSize.Height * 0.5;
                }
            }
            else if (ysplit == 1)
            {
                for (var i = 0; i < xsplit; ++i)
                {
                    _Center[i].X = mc.Margine.Left + areaSize.Width * (i + 0.5);
                    _Center[i].Y = mc.Margine.Top + trueAreaHeight - areaSize.Height * 0.5;
                }
            }
            else
            {
                throw new Exception($"unexpected values: xsplit={xsplit} and ysplit={ysplit}");
            }

            TitlePos = new XPoint(-areaSize.Width / 2, -areaSize.Height / 2);
            TrueAreaSize = areaSize;
            AreaSize = new XSize(areaSize.Width * shrinkScaleX, AreaSize.Height * shrinkScaleY - mc.currentPos.Y);     
           
            // カレント
            this.currentArea = 0;
        }

        /// <summary>
        /// これから描く図が, 紙面のどの位置なのか指定する Layout=Default の場合は関係ない
        /// </summary>
        /// <example>
        /// Layout=SplitHorizontal の場合
        ///    0
        /// ───
        ///    1
        /// Layout=SplitHorizontal3 の場合
        ///    0
        /// ───
        ///    1
        /// ───
        ///    2
        /// Layout=SplitHorizontal4 の場合
        ///    0
        /// ───
        ///    1
        /// ───
        ///    2
        /// ───
        ///    3
        /// Layout=SplitVertical の場合
        ///   │
        /// 0 │ 1
        ///   │
        /// Layout=SplitVertical3 の場合
        ///   │   │
        /// 0 │ 1 │ 2
        ///   │   │
        /// Layout=SplitVertical4 の場合
        ///   │   │   │
        /// 0 │ 1 │ 2 │ 3
        ///   │   │   │
        /// </example>
        public int currentArea { get; set; }

        /// <summary>
        /// 節点の印字
        /// </summary>
        public void printNode(double _x, double _y)
        {
            var centerPos = this._Center[this.currentArea];

            var x = centerPos.X + _x - nodeSize / 2;
            var y = centerPos.Y + _y - nodeSize / 2;

            XPoint p = new XPoint(x, y);
            XSize z = new XSize(this.nodeSize, this.nodeSize);

            Shape.Drawcircle(this.mc, p, z);
        }

        /// <summary>
        /// 節点の印字
        /// </summary>
        public void printLine(double _x1, double _y1, double _x2, double _y2)
        {
            var centerPos = this._Center[this.currentArea];

            var x1 = centerPos.X + _x1;
            var y1 = centerPos.Y + _y1;
            var x2 = centerPos.X + _x2;
            var y2 = centerPos.Y + _y2;

            XPoint p = new XPoint(x1, y1);
            XPoint q = new XPoint(x2, y2);

            Shape.DrawLine(this.mc, p, q);
        }

        public void printText(double _x1, double _y1, string str, double radian = 0, XFont font = null, XBrush brush = null, XStringFormat align = null)
        {
            var centerPos = this._Center[this.currentArea];

            var x1 = centerPos.X + _x1;
            var y1 = centerPos.Y + _y1;

            this.mc.currentPos.X = x1;
            this.mc.currentPos.Y = y1;

            var angle = radian * (180 / Math.PI);
            font ??= mc.font_mic; // 明朝
            brush ??= XBrushes.Black;
            align ??= XStringFormats.BottomLeft; // 左下起点

            this.mc.gfx.RotateAtTransform(angle, this.mc.currentPos);
            this.mc.gfx.DrawString(str, font, brush, this.mc.currentPos, align);
            this.mc.gfx.RotateAtTransform(-angle, this.mc.currentPos);

        }

        /// <summary>
        /// 円弧を描画
        /// </summary>
        /// <param name="_x1">中心のx座標</param>
        /// <param name="_y1">中心のy座標</param>
        /// <param name="rx">x軸方向の半径</param>
        /// <param name="ry">y軸方向の半径</param>
        /// <param name="startRadian">描画開始角度(radian)。3時の方向を0として反時計回りが正</param>
        /// <param name="sweepRadian">描画角度(radian)。反時計回りが正</param>
        public void printArc(double _x1, double _y1, double rx, double ry, double startRadian, double sweepRadian)
        {
            var centerPos = this._Center[this.currentArea];

            var topLeft = new XPoint(centerPos.X + _x1 - rx, centerPos.Y + _y1 - ry);
            var size = new XSize(2 * rx, 2 * ry);
            var startAngle = -startRadian * 180 / Math.PI;
            var sweepAngle = -sweepRadian * 180 / Math.PI;

            this.mc.gfx.DrawArc(this.mc.xpen, topLeft.X, topLeft.Y, size.Width, size.Height, startAngle, sweepAngle);
        }
    }
}
