using PdfSharpCore.Drawing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 全ての部材荷重の描画データの生成と描画
    /// </summary>
    internal class DiagramMemberLoad
    {
        /// <summary>
        /// 部材荷重関連テキストの描画色
        /// </summary>
        private static readonly XColor MemberLoadPenColor = XColors.Black;
        /// <summary>
        /// 部材荷重と部材荷重寸法線の描画色
        /// </summary>
        private static readonly XBrush MemberLoadTextColor = XBrushes.Black;
        /// <summary>
        /// 部材荷重と部材荷重寸法線の描画線の太さ(単位はポイント)
        /// </summary>
        private const double MemberLoadPenWidth = 0.1;

        /// <summary>
        /// 部材と部材荷重の間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenMemberAndMemberLoad = 15;
        /// <summary>
        /// 部材荷重と部材荷重寸法の間隔(単位はポイント)
        /// </summary>
        private const double DistanceBetweenDrawables = 10;

        /// <summary>
        /// 全ての節点荷重の描画データを生成する
        /// </summary>
        /// <param name="memberLoads">JSONファイルから読み込んだ部材荷重情報のコレクション</param>
        /// <param name="memberGroups">部材グループのコレクション</param>
        /// <param name="memberDic">部材情報の辞書</param>
        /// <param name="topLeft">描画領域左上角の座標</param>
        /// <param name="bottomRight">描画領域右下角の座標</param>
        /// <param name="isOlderVer2">JSONファイルから読み込んだデータ中のY座標を補正するための係数</param>
        /// <param name="font">テキストの描画フォント</param>
        /// <param name="measureString">テキストの描画サイズ取得メソッド</param>
        /// <exception cref="Exception"></exception>
        public DiagramMemberLoad(IReadOnlyCollection<LoadMember> memberLoads, IEnumerable<XMemberGroup> memberGroups, IReadOnlyDictionary<string, XMember> memberDic, XPoint topLeft, XPoint bottomRight, int isOlderVer2,
            XFont font, Func<string, XFont, XSize> measureString)
        {
            this.memberGroups = memberGroups;
            this.memberDic = memberDic;

            foreach (var memberGroup in memberGroups)
            {
                memberGroup.ClearMemberLoads();
            }

            if (memberLoads is null || !memberLoads.Any())
            {
                return;
            }

            // 実物の長辺を200mm四方に縮小した状態における1pt相当の長さ(単位は実物と同じ)
            coef = Math.Max(bottomRight.X - topLeft.X, topLeft.Y - bottomRight.Y) / (200 * XUnit.FromMillimeter(1));

            // 部材荷重データを利用可能な形に変換する
            var modifedMemberLoads = ModifyLoadMembers(memberLoads, isOlderVer2);

            var pMax_2y = memberLoads
                .Where(load => load.mark == "2" && load.direction == "y")
                .SelectMany(load => new[] { load.P1, load.P2, })
                .Append(0)
                .Max(p => Math.Abs(p));
            var pMax_2gx = memberLoads
                .Where(load => load.mark == "2" && load.direction == "gx")
                .SelectMany(load => new[] { load.P1, load.P2, })
                .Append(0)
                .Max(p => Math.Abs(p));
            var pMax_2gy = memberLoads
                .Where(load => load.mark == "2" && load.direction == "gy")
                .SelectMany(load => new[] { load.P1, load.P2, })
                .Append(0)
                .Max(p => Math.Abs(p));

            foreach (var memberGroup in memberGroups)
            {
                var correspondingMemberloads = modifedMemberLoads.Where(load => memberGroup.Members.Any(m => m.No == load.m1));
                if (!correspondingMemberloads.Any())
                {
                    continue;
                }

                // 部材方向分布荷重
                var memberLoads_2x = correspondingMemberloads.Where(load => load.mark == "2" && load.direction == "x");
                foreach (var memberLoad in memberLoads_2x)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_2x(memberLoad, memberGroup, font, measureString));
                }

                // 温度変化①(線膨張)
                var memberloads_9 = correspondingMemberloads.Where(load => load.mark == "9");
                foreach (var memberLoad in memberloads_9)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_9(memberLoad, memberGroup, font, measureString));
                }

                // 部材直角方向分布荷重
                var memberLoads_2y = correspondingMemberloads.Where(load => load.mark == "2" && load.direction == "y");
                foreach (var memberLoad in memberLoads_2y)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_2y(memberLoad, memberGroup, pMax_2y, font, measureString));
                }

                // 全体座標系水平方向部材分布荷重
                var memberLoads_2gx = correspondingMemberloads.Where(load => load.mark == "2" && load.direction == "gx");
                foreach (var memberLoad in memberLoads_2gx)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_2gx(memberLoad, memberGroup, pMax_2gx, font, measureString));
                }

                // 全体座標系鉛直方向部材分布荷重
                var memberLoads_2gy = correspondingMemberloads.Where(load => load.mark == "2" && load.direction == "gy");
                foreach (var memberLoad in memberLoads_2gy)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_2gy(memberLoad, memberGroup, pMax_2gy, font, measureString));
                }

                // 部材直角方向集中荷重
                var memberloads_1y = correspondingMemberloads.Where(load => load.mark == "1" && load.direction == "y");
                foreach (var memberLoad in memberloads_1y)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_1y(memberLoad, memberGroup, font, measureString));
                }

                // 全体座標系鉛直方向集中荷重
                var memberloads_1gy = correspondingMemberloads.Where(load => load.mark == "1" && load.direction == "gy");
                foreach (var memberLoad in memberloads_1gy)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_1gy(memberLoad, memberGroup, font, measureString));
                }

                // 部材方向集中荷重
                var memberloads_1x = correspondingMemberloads.Where(load => load.mark == "1" && load.direction == "x");
                foreach (var memberLoad in memberloads_1x)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_1x(memberLoad, memberGroup, font, measureString));
                }

                // 全体座標系水平方向集中荷重
                var memberloads_1gx = correspondingMemberloads.Where(load => load.mark == "1" && load.direction == "gx");
                foreach (var memberLoad in memberloads_1gx)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_1gx(memberLoad, memberGroup, font, measureString));
                }

                // モーメント荷重
                var memberloads_11z = correspondingMemberloads.Where(load => load.mark == "11" && load.direction == "z");
                foreach (var memberLoad in memberloads_11z)
                {
                    memberGroup.AddMemberLoad(new XMemberLoad_11z(memberLoad, memberGroup, font, measureString));
                }
            }

            // XCanvasへの描画

            if (!memberGroups.SelectMany(mg => mg.MemberLoads).Any())
            {
                return;
            }

            canvas = new XCanvas();

            foreach (var memberGroup in memberGroups)
            {
                // 部材集中モーメント荷重の描画
                var rDistance = new XDistance();
                foreach (var memberLoad in memberGroup.MemberLoads.Where(m => m.PrintLocation == XLocationType.C))
                {
                    memberLoad.PrintLoad(canvas, rDistance, coef); // 部材線上に描画
                }
                var distance0 = rDistance.UpperDistance;
                if (distance0 > 0)
                {
                    distance0 += DistanceBetweenDrawables;
                }

                var guDistance0 = memberGroup.Members.SelectMany(m => m.Springs.Where(m => m.LocationType == XLocationType.GU)).MaxOrDefault(s => s.Height) + DistanceBetweenMemberAndMemberLoad;
                var grDistance0 = memberGroup.Members.SelectMany(m => m.Springs.Where(m => m.LocationType == XLocationType.GR)).MaxOrDefault(s => s.Height) + DistanceBetweenMemberAndMemberLoad;
                var gdDistance0 = memberGroup.Members.SelectMany(m => m.Springs.Where(m => m.LocationType == XLocationType.GD)).MaxOrDefault(s => s.Height) + DistanceBetweenMemberAndMemberLoad;
                var glDistance0 = memberGroup.Members.SelectMany(m => m.Springs.Where(m => m.LocationType == XLocationType.GL)).MaxOrDefault(s => s.Height) + DistanceBetweenMemberAndMemberLoad;
                var ymDistance0 = memberGroup.Members.SelectMany(m => m.Springs.Where(m => m.LocationType == XLocationType.YM)).MaxOrDefault(s => s.Height) + DistanceBetweenMemberAndMemberLoad;
                var ypDistance0 = memberGroup.Members.SelectMany(m => m.Springs.Where(m => m.LocationType == XLocationType.YP)).MaxOrDefault(s => s.Height) + DistanceBetweenMemberAndMemberLoad;

                guDistance0 = Math.Max(distance0, guDistance0);
                grDistance0 = Math.Max(distance0, grDistance0);
                gdDistance0 = Math.Max(distance0, gdDistance0);
                glDistance0 = Math.Max(distance0, glDistance0);
                ymDistance0 = Math.Max(distance0, ymDistance0);
                ypDistance0 = Math.Max(distance0, ypDistance0);

                var guDistance = new XDistance(guDistance0);
                var grDistance = new XDistance(grDistance0);
                var gdDistance = new XDistance(gdDistance0);
                var glDistance = new XDistance(glDistance0);
                var ymDistance = new XDistance(ymDistance0);
                var ypDistance = new XDistance(ypDistance0);

                var dimensionOutputters = new List<IXMemberLoad>();

                // 部材集中モーメント荷重以外の描画
                foreach (var memberLoad in memberGroup.MemberLoads)
                {
                    switch (memberLoad.PrintLocation)
                    {
                        case XLocationType.GU:
                            memberLoad.PrintLoad(canvas, guDistance, coef);
                            break;
                        case XLocationType.GR:
                            memberLoad.PrintLoad(canvas, grDistance, coef);
                            break;
                        case XLocationType.GD:
                            memberLoad.PrintLoad(canvas, gdDistance, coef);
                            break;
                        case XLocationType.GL:
                            memberLoad.PrintLoad(canvas, glDistance, coef);
                            break;
                        case XLocationType.YM:
                            memberLoad.PrintLoad(canvas, ymDistance, coef);
                            break;
                        case XLocationType.YP:
                            memberLoad.PrintLoad(canvas, ypDistance, coef);
                            break;
                        case XLocationType.C:
                            // 描画済みなので何もしない
                            break;
                        default:
                            throw new Exception();
                    }

                    if (!dimensionOutputters.Any(d => d.DimensionXs.SequenceEqual(memberLoad.DimensionXs)))
                    {
                        dimensionOutputters.Add(memberLoad);
                    }
                }

                // 部材荷重の寸法線の描画
                foreach (var memberLoad in dimensionOutputters)
                {
                    switch (memberLoad.PrintLocation)
                    {
                        case XLocationType.GU:
                            memberLoad.PrintDimension(canvas, guDistance, coef);
                            break;
                        case XLocationType.GR:
                            memberLoad.PrintDimension(canvas, grDistance, coef);
                            break;
                        case XLocationType.GD:
                            memberLoad.PrintDimension(canvas, gdDistance, coef);
                            break;
                        case XLocationType.GL:
                            memberLoad.PrintDimension(canvas, glDistance, coef);
                            break;
                        case XLocationType.YM:
                            memberLoad.PrintDimension(canvas, ymDistance, coef);
                            break;
                        case XLocationType.YP:
                        case XLocationType.C: // 部材集中荷重の寸法線は部材座標系の正側に描画
                            memberLoad.PrintDimension(canvas, ypDistance, coef);
                            break;
                        default:
                            throw new Exception();
                    }
                }
            }
        }

        /// <summary>
        /// XCanvasクラスインスタンスが保持している部材荷重の描画データによって描画される領域の左上と右下の座標を計算する
        /// </summary>
        /// <param name="topLeft">全ての部材荷重を含む描画領域の左上角の座標</param>
        /// <param name="bottomRight">全ての部材荷重を含む描画領域の右下角の座標</param>
        public void AdjustDiagramRect(ref XPoint topLeft, ref XPoint bottomRight)
        {
            var memberLoads = memberGroups.SelectMany(mg => mg.MemberLoads);
            if (memberLoads.Any())
            {
                topLeft.X = Math.Min(topLeft.X, memberLoads.Min(s => s.TopLeft.X));
                topLeft.Y = Math.Max(topLeft.Y, memberLoads.Max(s => s.TopLeft.Y));
                bottomRight.X = Math.Max(bottomRight.X, memberLoads.Max(s => s.BottomRight.X));
                bottomRight.Y = Math.Min(bottomRight.Y, memberLoads.Min(s => s.BottomRight.Y));
            }
        }

        /// <summary>
        /// XCanvasクラスインスタンスが保持している部材荷重の描画データを、描画スケールや中心座標を反映させてPDF出力する
        /// </summary>
        /// <param name="frame"></param>
        public void Print(DiagramFrame frame) => canvas?.Print(frame, MemberLoadPenColor, MemberLoadPenWidth, MemberLoadTextColor);

        /// <summary>
        /// 部材荷重データを利用可能な形式に変換する
        /// </summary>
        /// <param name="memberLoads">JSONファイルから読み込んだ部材荷重情報のコレクション</param>
        /// <param name="isOlderVer2">JSONファイルから読み込んだデータ中のY座標を補正するための係数</param>
        /// <returns>利用可能な形式に変換した部材荷重データのシーケンス</returns>
        private IEnumerable<XLoadMember> ModifyLoadMembers(IReadOnlyCollection<LoadMember> memberLoads, int isOlderVer2)
        {
            var memberLoads1 = memberLoads
                // 部材荷重データの補正
                .Select(m => FixParams(m, isOlderVer2))
                // 有効な部材荷重を選別する
                .Select(m => IsMemberLoadValid(m, memberDic)).Where(m => m != null)
                // m1とm2を昇順に並び替える
                .Select(Reorder_m1m2)
                // 部材荷重を分割する(m2>0の場合)
                .SelectMany(SplitLoadMember)
                // 入力行番号順に並び替え
                .OrderBy(lm => lm.row);

            // 加算モード対応
            var memberLoads2 = SplitLoadMember2(memberLoads1);
            // 部材荷重を部材グループごとに分割する
            var memberLoads3 = SplitByMemberGroups(memberLoads2);

            return memberLoads3;
        }
        /// <summary>
        /// 部材荷重データの補正
        /// </summary>
        /// <param name="lm">部材荷重データ</param>
        /// <param name="isOlderVer2">JSONファイルから読み込んだデータ中のY座標を補正するための係数</param>
        /// <returns>補正後の部材荷重データ</returns>
        private LoadMember FixParams(LoadMember lm, int isOlderVer2)
        {
            // m1もしくはm2が欠けていたら他方を使って補う
            var m1 = lm.m1;
            var m2 = lm.m2;
            if (string.IsNullOrEmpty(m1))
            {
                m1 = m2;
            }
            if (string.IsNullOrEmpty(m2))
            {
                m2 = m1;
            }

            // L1が空文字列の場合は0.0を補う
            var L1 = lm.L1;
            if (string.IsNullOrEmpty(L1))
            {
                L1 = "0.0";
            }
            // L2が非数の場合(入力データ中のL2が空文字列だった場合に相当)は0.0に置き換える
            var L2 = lm.L2;
            if (double.IsNaN(L2))
            {
                L2 = 0.0;
            }

            // P1が非数の場合(入力データ中のP1がnullだった場合に相当)は0.0に置き換える(ただし温度荷重の場合は無効判定のために除外)
            var P1 = lm.P1;
            if (double.IsNaN(P1) && lm.mark != "9")
            {
                P1 = 0.0;
            }
            // P2が非数の場合(入力データ中のP2がnullだった場合に相当)は0.0に置き換える
            var P2 = lm.P2;
            if (double.IsNaN(P2))
            {
                P2 = 0.0;
            }

            // 荷重値の符号の補正(旧バージョンのデータではisOlderVer2=1、新バージョンのデータではisOlderVer2=-1)
            if ((lm.mark == "1" || lm.mark == "2" || lm.mark == "11") && (lm.direction == "y" || lm.direction == "gy"))
            {
                P1 *= -isOlderVer2;
                P2 *= -isOlderVer2;
            }

            var fixedLm = new LoadMember
            {
                m1 = m1,
                m2 = m2,
                mark = lm.mark,
                direction = lm.direction,
                L1 = L1,
                L2 = L2,
                P1 = P1,
                P2 = P2,
                row = lm.row,
            };
            return fixedLm;
        }
        /// <summary>
        /// 部材荷重データの有効/無効を識別する
        /// </summary>
        /// <param name="lm">部材荷重データ</param>
        /// <param name="memberDic">部材情報</param>
        /// <returns>null=無効な部材荷重データ、null以外=有効な部材荷重データ</returns>
        private LoadMember IsMemberLoadValid(LoadMember lm, IReadOnlyDictionary<string, XMember> memberDic)
        {
            // m1が部材情報に含まれていない部材なら無効
            if (!memberDic.ContainsKey(lm.m1))
            {
                return null;
            }

            // m2が部材情報に含まれていない部材なら無効
            var m2 = lm.m2;
            if (m2.StartsWith('-'))
            {
                m2 = m2[1..];
            }
            if (!memberDic.ContainsKey(m2))
            {
                return null;
            }

            // markとdirectionの組合せのチェック
            switch (lm.mark)
            {
                case "1":
                case "2":
                    switch (lm.direction)
                    {
                        case "x":
                        case "y":
                        case "gx":
                        case "gy":
                            // 有効
                            break;
                        default:
                            return null;
                    }
                    break;
                case "9":
                    // 何でもOK
                    break;
                case "11":
                    switch (lm.direction)
                    {
                        case "z":
                        case "gz":
                            // 有効
                            break;
                        default:
                            return null;
                    }
                    break;
                default:
                    return null;
            }

            // markとL1,L2,P1,P2の組合せのチェック
            switch (lm.mark)
            {
                case "1":
                case "2":
                case "11":
                    {
                        // L1が数値ではないなら無効
                        if (!double.TryParse(lm.L1, out var L1) || double.IsNaN(L1))
                        {
                            return null;
                        }
                        // P1とP2が共に0なら無効
                        if (lm.P1 == 0 && lm.P2 == 0)
                        {
                            return null;
                        }
                    }
                    break;
                case "9":
                    {
                        // L1が数値ではないなら無効
                        if ((!double.TryParse(lm.L1, out var L1) || double.IsNaN(L1)))
                        {
                            return null;
                        }
                        // P1がNaNなら無効
                        if (double.IsNaN(lm.P1))
                        {
                            return null;
                        }
                    }
                    break;
                default:
                    throw new Exception();
            }
            return lm;
        }
        /// <summary>
        /// 部材荷重データのm1とm2を昇順に並び替える(L1, L2, P1, P2の並びはそのまま)
        /// </summary>
        /// <param name="lm">部材荷重データ</param>
        /// <returns>m1とm2が昇順に並び替えられた状態の部材荷重データ</returns>
        private LoadMember Reorder_m1m2(LoadMember lm)
        {
            var m1 = Convert.ToInt32(lm.m1);
            var m2 = Convert.ToInt32(lm.m2);
            if (Math.Abs(m1) > Math.Abs(m2))
            {
                if (m2 < 0)
                {
                    (m1, m2) = (-m2, -m1);
                }
                else
                {
                    (m1, m2) = (m2, m1);
                }
            }

            return lm.CopyWithOverride(m1: m1.ToString(), m2: m2.ToString());
        }
        /// <summary>
        /// 部材荷重の分割(m2>0の場合)
        /// </summary>
        /// <param name="lm">部材荷重</param>
        /// <returns>分割後の部材荷重のシーケンス</returns>
        private IEnumerable<LoadMember> SplitLoadMember(LoadMember lm)
        {
            if (lm.m2.StartsWith('-'))
            {
                return new[] { lm, };
            }

            var m1 = Convert.ToInt32(lm.m1);
            var m2 = Convert.ToInt32(lm.m2);
            return Enumerable.Range(m1, m2 - m1 + 1).Select(i => lm.CopyWithOverride(m1: i.ToString(), m2: i.ToString()));
        }

        private struct TempLoadMember
        {
            public string m1 { get; }
            public string m2 { get; }
            public string mark { get; }
            public string direction { get; }
            public double x1 { get; }
            public double P1 { get; }
            public double x2 { get; }
            public double P2 { get; }

            public TempLoadMember(string m1, string m2, string mark, string direction, double x1, double P1, double x2, double P2)
            {
                this.m1 = m1;
                this.m2 = m2;
                this.mark = mark;
                this.direction = direction;
                this.x1 = x1;
                this.P1 = P1;
                this.x2 = x2;
                this.P2 = P2;
            }
        }

        static IEnumerable<string> Range(string m1, string m2)
        {
            var im1 = Convert.ToInt32(m1);
            var im2 = Convert.ToInt32(m2);
            Debug.Assert(im1 <= im2);
            return Enumerable.Range(im1, im2 - im1 + 1).Select(m => m.ToString());
        }

        /// <summary>
        /// 加算モード対応
        /// </summary>
        /// <param name="memberLoads">JSONファイルから読み込んだ部材荷重情報のコレクション</param>
        /// <returns>加算モード対応後の部材荷重のシーケンス</returns>
        private IEnumerable<IReadOnlyCollection<TempLoadMember>> SplitLoadMember2(IEnumerable<LoadMember> memberLoads)
        {
            var list = new List<TempLoadMember>();
            var totalList = new List<List<TempLoadMember>> { list, };
            var (current_x1, current_x2) = (0.0, 0.0);
            var prev_row = -1;
            var signature = string.Empty;
            foreach (var lm in memberLoads)
            {
                if (prev_row + 1 < lm.row)
                {
                    (current_x1, current_x2) = (0.0, 0.0);
                }
                prev_row = lm.row;

                var m1 = lm.m1;
                var m2 = lm.m2.StartsWith('-') ? lm.m2[1..] : lm.m2;

                var add_mode = false;
                if (lm.L1.StartsWith('-'))
                {
                    // L1加算モード
                    current_x1 = current_x2 + -Convert.ToDouble(lm.L1);
                    add_mode = true;
                }
                else
                {
                    current_x1 = double.TryParse(lm.L1, out var tmp_current_x1) ? tmp_current_x1 : 0.0;
                }
                if (lm.L2 < 0)
                {
                    // L2加算モード
                    current_x2 = current_x1 + -lm.L2;
                    add_mode = true;
                }
                else
                {
                    current_x2 = lm.L2;

                    // 部材m1のi端からの距離に変換
                    if (lm.mark == "2")
                    {
                        // m1～m2の部材長の合計
                        var totalLength = Range(m1, m2).Where(m => memberDic.ContainsKey(m)).Sum(m => memberDic[m].Lenngth);

                        current_x2 = totalLength - current_x2;
                    }
                }
                //if (current_x1 > current_x2)
                //{
                //    // 除外
                //    continue;
                //}
                if (list.Count > 0 && (!add_mode || signature != $"{m1},{m2},{lm.mark},{lm.direction}"))
                {
                    list = new List<TempLoadMember>();
                    totalList.Add(list);
                }

                signature = $"{m1},{m2},{lm.mark},{lm.direction}";
                list.Add(new TempLoadMember(m1, m2, lm.mark, lm.direction, current_x1, lm.P1, current_x2, lm.P2));
            }

            return totalList.AsEnumerable();
        }

        private IEnumerable<XLoadMember> SplitByMemberGroups(IEnumerable<IReadOnlyCollection<TempLoadMember>> loadmembers)
        {
            var resultList = new List<XLoadMember>();
            foreach (var lmlist in loadmembers)
            {
                if (lmlist.Count == 0)
                {
                    continue;
                }

                Debug.Assert(lmlist.Select(lm => $"{lm.m1},{lm.m2},{lm.mark},{lm.direction}").ToHashSet().Count == 1);

                var representative_lm = lmlist.ElementAt(0);

                // 各部材をグループ分けする
                var groupList = new List<(string m1, string m2, double xs, double xe)>();
                var m1_to_m2 = Range(representative_lm.m1, representative_lm.m2).Where(m => memberDic.ContainsKey(m)); // 存在しない部材は除外
                if (!m1_to_m2.Any())
                {
                    continue;
                }
                var (first_m, last_m) = (m1_to_m2.First(), m1_to_m2.First());
                var firstMember = memberDic[first_m];
                var xs = 0.0;
                var xe = firstMember.Lenngth;
                foreach (var m in m1_to_m2.Skip(1))
                {
                    if (firstMember.BelongingMemberGroup != memberDic[m].BelongingMemberGroup)
                    {
                        groupList.Add((first_m, last_m, xs, xe));

                        first_m = m;
                        firstMember = memberDic[first_m];
                        xs = xe;
                    }

                    last_m = m;
                    xe += memberDic[m].Lenngth;
                }
                groupList.Add((first_m, last_m, xs, xe));

                // 荷重データを部材グループごとに分割する
                var last_xe = 0.0;
                foreach (var group in groupList)
                {
                    Debug.Assert(group.xs <= group.xe);

                    var finalList = new List<TempLoadMember>();
                    switch (representative_lm.mark)
                    {
                        case "1":
                        case "11":
                            foreach (var lm in lmlist)
                            {
                                Debug.Assert(lm.x1 <= lm.x2 || (lm.x2 == 0 && lm.P2 == 0));

                                if (group.xs <= lm.x1)
                                {
                                    if (lm.x2 <= group.xe)
                                    {
                                        // xs <= x1 <= x2 <= xe
                                        finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, lm.direction, lm.x1 - last_xe, lm.P1, lm.x2 - last_xe, lm.P2));
                                    }
                                    else if (lm.x1 <= group.xe)
                                    {
                                        // xs <= x1 <= xe < x2
                                        finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, lm.direction, lm.x1 - last_xe, lm.P1, 0, 0));
                                    }
                                    else
                                    {
                                        // xs <= xe < x1 <= x2
                                    }
                                }
                                else if (group.xs <= lm.x2)
                                {
                                    if (lm.x2 <= group.xe)
                                    {
                                        // x1 < xs <= x2 <= xe
                                        finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, lm.direction, 0, 0, lm.x2 - last_xe, lm.P2));
                                    }
                                    else
                                    {
                                        // x1 < xs <= xe < x2 
                                    }
                                }
                                else
                                {
                                    // x1 <= x2 < xs <= xe
                                }
                            }
                            break;
                        case "2":
                            foreach (var lm in lmlist)
                            {
                                Debug.Assert(lm.x1 <= lm.x2);

                                var fn = new Func<double, double>(x => (lm.P2 - lm.P1) / (lm.x2 - lm.x1) * (x - lm.x1) + lm.P1);
                                if (group.xs <= lm.x1)
                                {
                                    if (lm.x2 <= group.xe)
                                    {
                                        // xs <= x1 <= x2 <= xe
                                        finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, lm.direction, lm.x1 - last_xe, lm.P1, lm.x2 - last_xe, lm.P2));
                                    }
                                    else if (lm.x1 <= group.xe)
                                    {
                                        // xs <= x1 <= xe <= x2
                                        if (lm.x1 < group.xe)
                                        {
                                            finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, lm.direction, lm.x1 - last_xe, lm.P1, group.xe - last_xe, fn(group.xe)));
                                        }
                                        else
                                        {
                                            finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, lm.direction, lm.x1 - last_xe, lm.P1, lm.x1 - last_xe, lm.P1));
                                        }
                                    }
                                    else
                                    {
                                        // xs <= xe < x1 <= x2
                                    }
                                }
                                else if (group.xs <= lm.x2)
                                {
                                    if (lm.x2 <= group.xe)
                                    {
                                        // x1 < xs <= x2 <= xe
                                        finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, lm.direction, group.xs - last_xe, fn(group.xs), lm.x2 - last_xe, lm.P2));
                                    }
                                    else
                                    {
                                        // x1 < xs <= xe < x2 
                                        finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, lm.direction, group.xs - last_xe, fn(group.xs), group.xe - last_xe, fn(group.xe)));
                                    }
                                }
                                else
                                {
                                    // x1 <= x2 < xs <= xe
                                }
                            }
                            break;
                        case "9":
                            foreach (var lm in lmlist)
                            {
                                Debug.Assert(lm.x1 <= lm.x2);

                                finalList.Add(new TempLoadMember(group.m1, group.m2, lm.mark, string.Empty, 0, lm.P1, 0, 0));
                            }
                            break;
                        default:
                            throw new Exception();
                    }

                    // m1, m2, mark, directionが一致する部材荷重データを一つにまとめる
                    resultList.AddRange(finalList
                        .GroupBy(g => $"{g.m1},{g.m2},{g.mark},{g.direction}")
                        .Select(g =>
                        {
                            var key = g.Key.Split(',');
                            Debug.Assert(key.Length == 4);
                            var (m1, m2, mark, direction, reverse) = (key[0], key[1], key[2], key[3], false);
                            var memberGroup = memberDic[m1].BelongingMemberGroup;
                            if (mark == "1" || mark == "2")
                            {
                                // 全体座標系で与えられた部材軸に平行な荷重を部材荷重系に置き換える
                                if (direction == "gx")
                                {
                                    if (memberGroup.Angle == 0)
                                    {
                                        direction = "x";
                                    }
                                    else if (memberGroup.Angle == 180)
                                    {
                                        (direction, reverse) = ("x", true);
                                    }
                                }
                                if (direction == "gy")
                                {
                                    if (memberGroup.Angle == -90)
                                    {
                                        direction = "x";
                                    }
                                    else if (memberGroup.Angle == 90)
                                    {
                                        (direction, reverse) = ("x", true);
                                    }
                                }
                            }
                            else if (mark == "11")
                            {
                                if (direction == "gz")
                                {
                                    direction = "z";
                                }
                            }
                            return new XLoadMember(mark, direction, m1, m2, memberGroup, g.Select(gg => new XQuadrilateral(gg.x1, gg.P1, gg.x2, gg.P2)).ToArray()) { Reverse = reverse };
                        }));

                    last_xe = group.xe;
                }
            }

            return resultList.AsEnumerable();
        }

        private readonly IEnumerable<XMemberGroup> memberGroups;
        private readonly IReadOnlyDictionary<string, XMember> memberDic;
        private readonly double coef;
        private readonly XCanvas canvas;
    }

    static class DiagramMemberLoadExt
    {
        public static LoadMember CopyWithOverride(this LoadMember lm, string m1 = null, string m2 = null, string direction = null, string mark = null, string L1 = null, double? L2 = null, double? P1 = null, double? P2 = null)
            => new LoadMember
            {
                m1 = m1 ?? lm.m1,
                m2 = m2 ?? lm.m2,
                direction = direction ?? lm.direction,
                mark = mark ?? lm.mark,
                L1 = L1 ?? lm.L1,
                L2 = L2 != null ? L2.Value : lm.L2,
                P1 = P1 != null ? P1.Value : lm.P1,
                P2 = P2 != null ? P2.Value : lm.P2,
                row = lm.row,
            };
    }
}
