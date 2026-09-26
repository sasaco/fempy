using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;

namespace FrameWebforCS.components.result;

internal static class ResultPickupFsecAggregator
{
    internal static PickupTableOutput Calculate(ResultCombineFsecSnapshot snapshot,
        ImmutableArray<PickupSelection> pickups, CancellationToken cancellationToken = default)
    {
        ResultCombineFsecOutput combined = ResultCombineFsecAggregator.Calculate(snapshot, cancellationToken);
        return SelectCombined(combined, snapshot.Dimension, pickups, cancellationToken);
    }

    internal static PickupTableOutput SelectCombined(ResultCombineFsecOutput combined, int dimension,
        ImmutableArray<PickupSelection> pickups, CancellationToken cancellationToken = default) =>
        ResultPickupAggregator.Select(dimension, combined.Cases, pickups,
            dimension == 3 ? ResultPickupAggregator.Fsec3D : ResultPickupAggregator.Fsec2D,
            item => item.Id, item => item.Rows,
            (row, index) => index.ToString(CultureInfo.InvariantCulture),
            (row, mode) => mode[..2] switch
            {
                "fx" => row.Fx, "fy" => row.Fy, "fz" => row.Fz,
                "mx" => row.Mx, "my" => row.My, "mz" => row.Mz,
                _ => throw new ArgumentException("Unknown member-force mode.")
            },
            (row, caseId) => dimension == 3
                ? [row.MemberDisplay, row.NodeId, row.Location.ToString("F3", CultureInfo.InvariantCulture),
                    Format(row.Fx), Format(row.Fy), Format(row.Fz), Format(row.Mx),
                    Format(row.My), Format(row.Mz), caseId + ":" + row.Case]
                : [row.MemberDisplay, row.NodeId, row.Location.ToString("F3", CultureInfo.InvariantCulture),
                    Format(row.Fx), Format(row.Fy), Format(row.Mz), caseId + ":" + row.Case],
            cancellationToken);

    // JavaScript Number.toFixed(2) rounds the exact binary64 value. Decimal
    // midpoint rounding changes values such as -7.625 and 1.005.
    internal static string Format(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) >= 1e21)
            return value.ToString("G", CultureInfo.InvariantCulture);

        bool negative = value < 0;
        long bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
        int exponentBits = (int)((bits >> 52) & 0x7ff);
        long fraction = bits & ((1L << 52) - 1);
        BigInteger significand = exponentBits == 0
            ? fraction : (1L << 52) | fraction;
        int exponent = exponentBits == 0 ? -1074 : exponentBits - 1023 - 52;
        BigInteger scaled = significand * 100;
        if (exponent >= 0)
            scaled <<= exponent;
        else
        {
            BigInteger divisor = BigInteger.One << -exponent;
            scaled = BigInteger.DivRem(scaled, divisor, out BigInteger remainder);
            if (remainder * 2 >= divisor) scaled++;
        }
        BigInteger whole = BigInteger.DivRem(scaled, 100, out BigInteger cents);
        return (negative ? "-" : string.Empty) +
            whole.ToString(CultureInfo.InvariantCulture) + "." +
            cents.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
    }
}
