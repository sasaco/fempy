using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 部材、荷重、および寸法線の間隔の制御
    /// </summary>
    internal class XDistance
    {
        /// <summary>
        /// 部材、荷重、および寸法線の間隔の制御
        /// </summary>
        /// <param name="initialDistance">間隔の初期値</param>
        public XDistance(double initialDistance = 0)
        {
            UpperDistance = LowerDistance = initialDistance;
        }

        /// <summary>
        /// 直近に描画した荷重の上側
        /// </summary>
        public double UpperDistance { get; private set; }
        /// <summary>
        /// 直近に描画した荷重の下側
        /// </summary>
        public double LowerDistance { get; private set; }

        /// <summary>
        /// 指定した荷重を描画する際に空ける間隔を計算する
        /// </summary>
        /// <param name="loadMember">これから描画する荷重データ</param>
        /// <returns>間隔値</returns>
        public double GetDistance(XLoadMember loadMember)
        {
            if (Conflicts(loadMember))
            {
                return UpperDistance;
            }
            else
            {
                return LowerDistance;
            }
        }

        /// <summary>
        /// 荷重描画後のデータ更新
        /// </summary>
        /// <param name="delta">描画した荷重の高さ</param>
        /// <param name="loadMember">描画した荷重データ</param>
        public void Update(double delta, XLoadMember loadMember)
        {
            if (Conflicts(loadMember))
            {
                LowerDistance = UpperDistance;
                UpperDistance += delta;

                xxList.Clear();
            }
            else
            {
                UpperDistance = Math.Max(UpperDistance, LowerDistance + delta);
            }

            // 干渉のチェックに使用するデータの更新
            foreach (var q in loadMember.Quadrilaterals)
            {
                var x1 = q.X1 + loadMember.xOffset;
                var x2 = q.X2 + loadMember.xOffset;
                xxList.Add(new ValueTuple<double, double>(x1, x2));
            }
        }

        /// <summary>
        /// 荷重線描画のための間隔の計算
        /// </summary>
        /// <returns>間隔値</returns>
        public double GetDistanceForDimension() => UpperDistance;

        /// <summary>
        /// 荷重線描画後のデータ更新
        /// </summary>
        /// <param name="delta">描画した荷重線の高さ</param>
        public void UpdateForDimension(double delta)
        {
            // 無条件に全体が干渉するものとして扱う

            UpperDistance += delta;
            LowerDistance = UpperDistance;

            xxList.Clear();
        }

        /// <summary>
        /// 干渉のチェックに使用するデータのリスト。このリストが空の場合は部材全体が干渉すると判断される
        /// </summary>
        private readonly List<ValueTuple<double, double>> xxList = new List<(double, double)>();

        /// <summary>
        /// 干渉のチェック
        /// </summary>
        /// <param name="loadMember">描画する/した部材データ</param>
        /// <returns>true=干渉する, false=干渉しない</returns>
        private bool Conflicts(XLoadMember loadMember)
        {
            if (xxList.Count == 0)
            {
                return true;
            }
            switch (loadMember.mark)
            {
                case "1":
                case "9":
                    return true;
                case "11":
                    return false;
                case "2":
                    foreach (var xx in xxList)
                    {
                        foreach (var q in loadMember.Quadrilaterals)
                        {
                            Debug.Assert(q.X1 <= q.X2);

                            var x1 = q.X1 + loadMember.xOffset;
                            var x2 = q.X2 + loadMember.xOffset;
                            if (xx.Item1 < x2 && x1 < xx.Item2)
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                default:
                    throw new Exception();
            }
        }
    }
}
