using System;
using System.Collections.Generic;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 部材に関連する情報を保持する
    /// </summary>
    internal class XMember
    {
        /// <summary>
        /// 部材番号
        /// </summary>
        public string No { get; }
        /// <summary>
        /// 部材端点
        /// </summary>
        public XNode Ni { get; }
        /// <summary>
        /// 部材端点
        /// </summary>
        public XNode Nj { get; }

        /// <summary>
        /// 部材種別(水平部材/鉛直部材/斜め部材)
        /// </summary>
        public XMemberType MemberType { get; }
        /// <summary>
        /// 部材の長さ
        /// </summary>
        public double Lenngth { get; }
        /// <summary>
        /// 部材の角度(単位は度)。3時の方向が0度で、反時計回りが正
        /// </summary>
        public double Angle { get; }

        /// <summary>
        /// 部材が属する部材グループ
        /// </summary>
        public XMemberGroup BelongingMemberGroup { get; private set; }

        /// <summary>
        /// バネ関連の描画データのコレクション
        /// </summary>
        public IEnumerable<XSpring> Springs => springList.AsEnumerable();

        /// <summary>
        /// 部材に関連する情報を生成する
        /// </summary>
        /// <param name="memberNo">部材番号</param>
        /// <param name="member">JSONファイルから読み込んだ部材情報</param>
        /// <param name="nodeDic">部材節点情報の辞書</param>
        public XMember(string memberNo, Member member, IReadOnlyDictionary<string, XNode> nodeDic)
        {
            No = memberNo;
            Ni = nodeDic[member.ni];
            Nj = nodeDic[member.nj];

            MemberType = Ni.Pos.X == Nj.Pos.X ? XMemberType.Vertical : Ni.Pos.Y == Nj.Pos.Y ? XMemberType.Horizontal : XMemberType.Diagonal;
            var xx = Nj.Pos.X - Ni.Pos.X;
            var yy = Nj.Pos.Y - Ni.Pos.Y;
            Lenngth = Math.Sqrt(xx * xx + yy * yy);
            Angle = XMath.Atan2(yy, xx);
        }

        public void SetBelongingMemberGroup(XMemberGroup memberGroup) => BelongingMemberGroup = memberGroup;

        public void ClearSprings() => springList.Clear();

        public void AddSpring(XSpring spring) => springList.Add(spring);

        private readonly List<XSpring> springList = new List<XSpring>();
    }
}
