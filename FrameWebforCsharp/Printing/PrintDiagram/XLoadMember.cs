using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PDF_Manager.Printing
{
    internal readonly struct XQuadrilateral
    {
        public double X1 { get; }
        public double P1 { get; }
        public double X2 { get; }
        public double P2 { get; }

        public XQuadrilateral(double x1, double p1, double x2, double p2) => (X1, P1, X2, P2) = (x1, p1, x2, p2);
    }

    internal class XLoadMember
    {
        public string mark { get; }
        public string direction { get; }
        public string m1 { get; }
        public string m2 { get; }
        public double xOffset { get; }
        public IReadOnlyCollection<XQuadrilateral> Quadrilaterals { get; }
        public bool Reverse { get; set; }

        public XLoadMember(string mark, string direction, string m1, string m2, XMemberGroup memberGroup, params XQuadrilateral[] quadrilaterals)
        {
            Debug.Assert(quadrilaterals != null);
            Debug.Assert(quadrilaterals.Length > 0);

            this.mark = mark;
            this.direction = direction;
            this.m1 = m1;
            this.m2 = m2;
            xOffset = GetXOffset(m1, memberGroup);
            Quadrilaterals = quadrilaterals;
            Reverse = false;
        }

        private double GetXOffset(string m1, XMemberGroup memberGroup)
        {
            var m1no = Convert.ToInt32(m1);
            var xOffset = memberGroup.Members.Where(m => Convert.ToInt32(m.No) < m1no).Sum(m => m.Lenngth);
            return xOffset;
        }
    }
}
