using PdfSharpCore.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PDF_Manager.Printing
{
    /// <summary>
    /// 一直線上に並ぶ部材に関する情報を保持する
    /// </summary>
    internal class XMemberGroup
    {
        /// <summary>
        /// 部材グループに属する部材の情報のコレクション
        /// </summary>
        public IEnumerable<XMember> Members { get; }
        /// <summary>
        /// 部材グループの端点の情報
        /// </summary>
        public XNode Ni { get; }
        /// <summary>
        /// 部材グループの端点の情報
        /// </summary>
        public XNode Nj { get; }

        /// <summary>
        /// 部材グループの中心座標
        /// </summary>
        public XPoint Center { get; }
        /// <summary>
        /// 部材グループ種別(水平部材グループ/鉛直部材グループ/斜め部材グループ)
        /// </summary>
        public XMemberType MemberType { get; } = XMemberType.None;
        /// <summary>
        /// 部材グループの角度(単位は度)。3時の方向が0度で、反時計回りが正
        /// </summary>
        public double Angle { get; }

        /// <summary>
        /// 部材グループ上の各接点のX座標のコレクション
        /// </summary>
        public IEnumerable<double> XDimensions { get; } = Enumerable.Empty<double>();
        /// <summary>
        /// 部材グループ上の各接点のY座標のコレクション
        /// </summary>
        public IEnumerable<double> YDimensions { get; } = Enumerable.Empty<double>();

        /// <summary>
        /// 部座グループを中心を軸として水平に回転移動させた時の各接点の座標のコレクション
        /// </summary>
        public Dictionary<string, (XPoint NHi, XPoint NHj)> NHDic;

        /// <summary>
        /// 部材グループに属する部材に対する部材荷重の情報のコレクション
        /// </summary>
        public IEnumerable<IXMemberLoad> MemberLoads => memberLoadList.AsEnumerable();

        /// <summary>
        /// <paramref name="memberList"/> の先頭要素をシードとして同一部材グループに属する部材の情報から部材グループを生成し、生成された部材グループに属する部材の情報をリストから削除する
        /// </summary>
        /// <param name="memberList">部材情報のリスト</param>
        /// <param name="nodes">節点情報のコレクション</param>
        public XMemberGroup(ref List<XMember> memberList, IReadOnlyCollection<XNode> nodes)
        {
            Debug.Assert(memberList.Any());

            memberList = memberList.OrderBy(m => m.No).ToList();

            var seedMember = memberList[0];
            memberList.RemoveAt(0);

            var succeedingList = new List<XMember> { seedMember, };
            var preceedingList = new List<XMember> { };
            
            for (var currentMember = seedMember; memberList.Any(); )
            {
                var nj = currentMember.Nj.No;

                var neighborIndex = memberList.FindIndex(m => m.Ni.No == nj && m.Angle.NearlyEqualTo(seedMember.Angle));
                if (neighborIndex < 0)
                {
                    break;
                }

                currentMember = memberList[neighborIndex];
                memberList.RemoveAt(neighborIndex);

                succeedingList.Add(currentMember);
            }

            for (var currentMember = seedMember; memberList.Any(); )
            {
                var ni = currentMember.Ni.No;

                var neighborIndex = memberList.FindIndex(m => m.Nj.No == ni && m.Angle.NearlyEqualTo(seedMember.Angle));
                if (neighborIndex < 0)
                {
                    break;
                }

                currentMember = memberList[neighborIndex];
                memberList.RemoveAt(neighborIndex);

                preceedingList.Insert(0, currentMember);
            }

            Members = preceedingList.Concat(succeedingList).AsEnumerable();
            Ni = Members.First().Ni;
            Nj = Members.Last().Nj;

            Center = new XPoint((Ni.Pos.X + Nj.Pos.X) / 2, (Ni.Pos.Y + Nj.Pos.Y) / 2);
            MemberType = Ni.Pos.X == Nj.Pos.X ? XMemberType.Vertical : Ni.Pos.Y == Nj.Pos.Y ? XMemberType.Horizontal : XMemberType.Diagonal;
            Angle = XMath.Atan2(Nj.Pos.Y - Ni.Pos.Y, Nj.Pos.X - Ni.Pos.X);

            var minX = nodes.Min(n => n.Pos.X);
            var maxX = nodes.Max(n => n.Pos.X);
            if (minX != maxX)
            {
                XDimensions = Members.SelectMany(m => new[] { m.Ni.Pos.X, m.Nj.Pos.X, }).Concat(new[] { minX, maxX, }).Distinct().OrderBy(x => x);
            }

            var minY = nodes.Min(n => n.Pos.Y);
            var maxY = nodes.Max(n => n.Pos.Y);
            if (minY != maxY)
            {
                YDimensions = Members.SelectMany(m => new[] { m.Ni.Pos.Y, m.Nj.Pos.Y, }).Concat(new[] { minY, maxY, }).Distinct().OrderBy(y => y);
            }

            var mat = new XMatrix();
            mat.RotateAtAppend(-Angle, Center);
            var nhs = Members.SelectMany(m => new[] { m.Ni.Pos, m.Nj.Pos, }).Distinct().ToArray();
            mat.Transform(nhs);
            NHDic = Members.Select((m, index) => (m.No, (nhs.ElementAt(index), nhs.ElementAt(index + 1)))).ToDictionary(s => s.Item1, s => s.Item2);

            // 自分が属する部材グループを各部材に登録
            foreach (var member in Members)
            {
                member.SetBelongingMemberGroup(this);
            }
        }

        public void ClearMemberLoads() => memberLoadList.Clear();

        public void AddMemberLoad(IXMemberLoad memberLoad) => memberLoadList.Add(memberLoad);

        private readonly List<IXMemberLoad> memberLoadList = new List<IXMemberLoad>();
    }
}
