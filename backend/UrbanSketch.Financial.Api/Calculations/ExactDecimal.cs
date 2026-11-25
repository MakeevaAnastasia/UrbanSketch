using System.Numerics;

namespace UrbanSketch.Financial.Api.Calculations;

// Decimal input/output; integer coefficients prevent decimal's implicit scale reduction.
// No rounding in sums/products. Percentages have one explicit rounding boundary.
internal static class ExactDecimal
{
    private static readonly BigInteger MaxCoefficient = (BigInteger.One << 96) - 1;

    private static (BigInteger Coefficient, int Scale) Parts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32) | ((BigInteger)(uint)bits[2] << 64);
        return ((bits[3] & int.MinValue) != 0 ? -coefficient : coefficient, (bits[3] >> 16) & 255);
    }

    private static decimal Create(BigInteger coefficient, int scale)
    {
        var negative = coefficient.Sign < 0;
        coefficient = BigInteger.Abs(coefficient);
        while (scale > 0 && coefficient % 10 == 0) { coefficient /= 10; scale--; }
        if (scale > 28 || coefficient > MaxCoefficient) throw new OverflowException();
        return new decimal(unchecked((int)(uint)(coefficient & uint.MaxValue)),
            unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue)),
            unchecked((int)(uint)((coefficient >> 64) & uint.MaxValue)), negative, (byte)scale);
    }

    public static decimal Multiply(decimal left, decimal right)
    {
        var a = Parts(left); var b = Parts(right);
        return Create(a.Coefficient * b.Coefficient, a.Scale + b.Scale);
    }

    public static decimal Sum(params decimal[] values)
    {
        var parts = values.Select(Parts).ToArray();
        var scale = parts.Max(x => x.Scale);
        return Create(parts.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Coefficient * BigInteger.Pow(10, scale - x.Scale)), scale);
    }

    // Exact rational division followed by rounding to 4 places, midpoint away from zero.
    // Avoids both double rounding and overflow of (profit * 100) before division.
    public static decimal Percent(decimal profit, decimal denominator)
    {
        var a = Parts(profit); var b = Parts(denominator);
        var numerator = BigInteger.Abs(a.Coefficient) * BigInteger.Pow(10, b.Scale + 6);
        var divisor = BigInteger.Abs(b.Coefficient) * BigInteger.Pow(10, a.Scale);
        var quotient = BigInteger.DivRem(numerator, divisor, out var remainder);
        if (remainder * 2 >= divisor) quotient++;
        return Create(a.Coefficient.Sign < 0 ? -quotient : quotient, 4);
    }
}
