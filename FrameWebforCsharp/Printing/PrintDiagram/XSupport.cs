using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 支点の描画情報を保持するクラス
    /// </summary>
    internal class XSupport : XDrawable
    {
        /// <summary>
        /// 部材節点とピン支点の頂点の間隔(単位はポイント)
        /// </summary>
        private const double PinTopGap = 0.35;
        /// <summary>
        /// ピン支点の高さ(単位はポイント)
        /// </summary>
        private const double PinHeight = 10;
        /// <summary>
        /// ピン支点の底辺の長さ(単位はポイント)
        /// </summary>
        private const double PinWidth = 14;

        /// <summary>
        /// 部材節点とローラー支点の頂点の間隔(単位はポイント)
        /// </summary>
        private const double RollerTopGap = 0.35;
        /// <summary>
        /// ローラー支点の上側三角形の高さ(単位はポイント)
        /// </summary>
        private const double RollerHeight = 10;
        /// <summary>
        /// ローラー支点の上側三角形の底辺の長さ(単位はポイント)
        /// </summary>
        private const double RollerWidth = 14;
        /// <summary>
        /// ローラー支点の上側三角形の底辺と下線の間隔(単位はポイント)
        /// </summary>
        private const double RollerUnderlineGap = 3;

        /// <summary>
        /// 固定支点を構成する4直線の始点と終点の相対座標の集合
        /// </summary>
        private static readonly double[,] FixedParams = new double[,]
        {
            { -10, 0,  10,   0 }, // 横線
            {   0, 0,  -5, -10 }, // 斜線1
            {   6, 0,   1, -10 }, // 斜線2
            {  -6, 0, -11, -10 }, // 斜線3
        };

        /// <summary>
        /// バネ支点を構成する7直線の始点と終点の相対座標の集合
        /// </summary>
        private static readonly double[,] SpringParams = new double[,]
        {
            {  0,  -0.35, -7, -10.35 }, // △
            {  0,  -0.35,  7, -10.35 }, // △
            { -7, -10.35,  7, -10.35 }, // △
            { -7, -13.35,  7, -13.35 }, // バネ
            {  7, -13.35, -7, -16.35 }, // バネ
            { -7, -16.35,  7, -16.35 }, // バネ
            {  7, -16.35, -7, -19.35 }, // バネ
            { -7, -19.35,  7, -19.35 }, // バネ
        };

        /// <summary>
        /// 支点番号
        /// </summary>
        public string NodeNo { get; }
        /// <summary>
        /// X方向の拘束条件
        /// </summary>
        public double TX { get; }
        /// <summary>
        /// Y方向の拘束条件
        /// </summary>
        public double TY { get; }
        /// <summary>
        /// 回転拘束条件
        /// </summary>
        public double MZ { get; }

        /// <summary>
        /// 支点の形状
        /// </summary>
        public enum Types
        {
            /// <summary>
            /// 未設定
            /// </summary>
            None,
            /// <summary>
            /// 固定支点
            /// </summary>
            Fixed,
            /// <summary>
            /// ピン支点
            /// </summary>
            Pin,
            /// <summary>
            /// ローラー支点
            /// </summary>
            Roller,
            /// <summary>
            /// バネ支点
            /// </summary>
            Spring,
        }
        /// <summary>
        /// 支点の形状
        /// </summary>
        public Types Type { get; } = Types.None;

        private XPoint _TopLeft;
        /// <summary>
        /// 支点の描画領域左上角の座標
        /// </summary>
        public XPoint TopLeft
        {
            get
            {
                Debug.Assert(Type != Types.None);
                return _TopLeft;
            }
            private set => _TopLeft = value;
        }
        private XPoint _BottomRight;
        /// <summary>
        /// 支点の描画領域右下角の座標
        /// </summary>
        public XPoint BottomRight
        {
            get
            {
                Debug.Assert(Type != Types.None);
                return _BottomRight;
            }
            private set => _BottomRight = value;
        }

        /// <summary>
        /// 支点情報を生成する
        /// </summary>
        /// <param name="fixNode">JSONファイルから読み込んだ支点情報</param>
        /// <param name="nodeDic">節点情報の辞書</param>
        /// <param name="frameTopLeft">描画領域左上角の座標</param>
        /// <param name="frameBottomRight">描画領域右下角の座標</param>
        public XSupport(FixNode fixNode, IReadOnlyDictionary<string, XNode> nodeDic, XPoint frameTopLeft, XPoint frameBottomRight)
            : base(null, null)
        {
            NodeNo = fixNode.n;
            TX = fixNode.tx;
            TY = fixNode.ty;
            MZ = fixNode.rz;

            var node = nodeDic[NodeNo];
            var pos = node.Pos;
            var npx = pos.X;
            var npy = pos.Y;

            // 実物の長辺を200mm四方に縮小した状態における1pt相当の長さ(単位は実物と同じ)
            var coef = Math.Max(frameBottomRight.X - frameTopLeft.X, frameTopLeft.Y - frameBottomRight.Y) / (200 * XUnit.FromMillimeter(1));

            double[,] info;
            double angle = 0;

            if (TX == 1 && TY == 1 && MZ == 0)
            {
                // ピン

                Type = Types.Pin;

                var topDistance = PinTopGap;
                var bottomDistance = PinTopGap + PinHeight;
                var baseLength2 = PinWidth / 2;

                // ピンを構成する3線分の情報
                info = new double[,]
                {
                    {            0,    -topDistance, -baseLength2, -bottomDistance, }, // /
                    {            0,    -topDistance, +baseLength2, -bottomDistance, }, //  \
                    { -baseLength2, -bottomDistance, +baseLength2, -bottomDistance, }, // __
                };

                // 上向き(△)で固定
            }
            else if ((TX == 0 && TY == 1 && MZ == 0) || (TX == 1 && TY == 0 && MZ == 0))
            {
                // ローラー

                Type = Types.Roller;

                var topDistance = RollerTopGap;
                var bottomDistance = RollerTopGap + RollerHeight;
                var baseLength2 = RollerWidth / 2;
                var underlineDistance = bottomDistance + RollerUnderlineGap;

                // ローラーを構成する4線分の情報
                info = new double[,]
                {
                    {            0,       -topDistance, -baseLength2,    -bottomDistance, }, // /
                    {            0,       -topDistance, +baseLength2,    -bottomDistance, }, //  \
                    { -baseLength2,    -bottomDistance, +baseLength2,    -bottomDistance, }, // __
                    { -baseLength2, -underlineDistance, +baseLength2, -underlineDistance, }, // __
                };

                // y方向のみの拘束の場合、描画角度は上向き(△)で固定
                // x方向のみの拘束の場合、部材の左端なら右向き(▷)、右端なら左向き(◁)に回転させる
                if (TX == 1)
                {
                    // 支点が部材の左端にあるなら270°回転、右端にあるなら90°回転
                    var another = node.Anothers.First();
                    var ap = another.Pos;
                    angle = npx < ap.X ? 270 : 90;
                }
            }
            else if (TX == 1 && TY == 1 && MZ == 1)
            {
                // 固定

                Type = Types.Fixed;

                info = FixedParams;

                // 支点の反対側の節点を探す
                var another = node.Anothers.First();
                var ap = another.Pos;
                if (npy == ap.Y)
                {
                    // 水平部材の場合、支点が部材の左端にあるなら270°回転、右端にあるなら90°回転
                    angle = npx < ap.X ? 270 : 90;
                }
                else
                {
                    // 水平部材以外の場合、支点が部材の上端にあるなら180°回転
                    if (npy > ap.Y)
                    {
                        angle = 180;
                    }
                }
            }
            else if (TX > -1 && TY != 0 && TY != 1)
            {
                // バネ

                Type = Types.Spring;

                info = SpringParams;

                // 支点の反対側の節点を探す
                var another = node.Anothers.First();
                var ap = another.Pos;
                if (npy == ap.Y)
                {
                    // 水平部材の場合、支点が部材の左端にあるなら270°回転、右端にあるなら90°回転
                    angle = npx < ap.X ? 270 : 90;
                }
                else
                {
                    // 水平部材以外の場合、支点が部材の上端にあるなら180°回転
                    if (npy > ap.Y)
                    {
                        angle = 180;
                    }
                }
            }
            else
            {
                // @TODO: 支点の種類が不明の場合はエラーとせず、何も描画しない

                return;
            }

            // 支点を構成する節点の座標
            var nps = info.Cast<double>().Chunk(2).Select(p => new XPoint(npx + coef * p.ElementAt(0), npy + coef * p.ElementAt(1))).ToArray();

            // 節点座標の回転移動
            if (angle != 0)
            {
                var mat = new XMatrix();
                mat.RotateAtAppend(angle, npx, npy);
                mat.Transform(nps);
            }

            // 支点を構成する線分情報の登録
            AddLines(nps.Chunk(2).Select(s => (s.ElementAt(0), s.ElementAt(1))));

            CalculateDiagramRect(out var topLeft, out var bottomRight);
            TopLeft = topLeft;
            BottomRight = bottomRight;
        }

        /// <summary>
        /// 支点を <paramref name="canvas"/> に描画する
        /// </summary>
        /// <param name="canvas"></param>
        public new void Print(ICanvas canvas) => base.PrintDrawables(canvas);
    }
}
