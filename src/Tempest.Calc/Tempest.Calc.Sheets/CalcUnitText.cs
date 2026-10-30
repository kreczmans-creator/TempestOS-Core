using System.Text;
using System.Text.RegularExpressions;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, row 27: a unit written as an engineer writes it (<c>°C</c>, <c>Ω</c>, <c>kN·m</c>, <c>m²</c>). The engine and the inputs accept
/// ASCII (<c>degC</c>, <c>ohm</c>, <c>kN*m</c>, <c>mm^2</c>); a sheet, a notice and a glossary show the symbols. Display only: the unit
/// string a value is stored with is never changed.
/// </summary>
public static partial class CalcUnitText
{
    [GeneratedRegex(@"\^(-?\d+)")]
    private static partial Regex Power();

    [GeneratedRegex(@"(?<![A-Za-z])([kMGmµu]?)ohm(?![A-Za-z])")]
    private static partial Regex Ohm();

    [GeneratedRegex(@"(?<![A-Za-z])deg(?![A-Za-z])")]
    private static partial Regex Degree();

    /// <summary>The unit with Unicode symbols: <c>degC</c> → <c>°C</c>, <c>deg</c> → <c>°</c>, <c>ohm</c> → <c>Ω</c>, <c>*</c> → <c>·</c>, <c>^2</c> → <c>²</c>.</summary>
    public static string Pretty(string? unit)
    {
        if (string.IsNullOrEmpty(unit))
            return string.Empty;

        var s = unit;

        if (s.Contains('^', StringComparison.Ordinal))
            s = Power().Replace(s, m => Superscript(m.Groups[1].Value));

        s = s.Replace("*", "·", StringComparison.Ordinal)
            .Replace("degC", "°C", StringComparison.Ordinal)
            .Replace("degF", "°F", StringComparison.Ordinal);

        if (s.Contains("deg", StringComparison.Ordinal))
            s = Degree().Replace(s, "°");

        if (s.Contains("ohm", StringComparison.Ordinal))
            s = Ohm().Replace(s, m => (m.Groups[1].Value == "u" ? "µ" : m.Groups[1].Value) + "Ω");

        return s;
    }

    private static string Superscript(string digits)
    {
        const string Digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var sb = new StringBuilder();

        foreach (var c in digits)
            sb.Append(c == '-' ? '⁻' : Digits[c - '0']);

        return sb.ToString();
    }
}
