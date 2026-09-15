using System.Globalization;

namespace Tempest.Core.Calculations;

/// <summary>
/// One way of writing a plain number wherever the product shows an
/// engineer a value: six significant figures with trailing zeros trimmed
/// ("0.496032", "1.32275", "1.5"), except that a whole number below
/// 10<sup>15</sup> is written in full ("2000000", "167552") rather than
/// in exponent form. Culture-invariant.
/// </summary>
internal static class EngineeringNumber
{
    /// <summary>Formats <paramref name="value"/> as the product shows a plain number.</summary>
    public static string Format(double value)
    {
        if (double.IsFinite(value) && Math.Abs(value) < 1e15 && value == Math.Round(value))
            return value.ToString("0", CultureInfo.InvariantCulture);

        return value.ToString("G6", CultureInfo.InvariantCulture);
    }
}
