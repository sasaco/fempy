using System;

namespace PDF_Manager.Printing
{
    internal static class XMath
    {
        public static double Atan2(double y, double x, double threshold = 1e-5) => Math.Abs(x) < threshold ? y > 0 ? 90 : -90 : Math.Atan2(y, x) * 180 / Math.PI;

        public static bool IsAngleIn2ndOr3rdQuadrant(double angle)
        {
            angle = Math.Abs(angle % 360);
            return angle > 90 && angle < 270;
        }
    }
}
