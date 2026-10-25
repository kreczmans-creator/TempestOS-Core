using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tempest.Calc.Import.Cad;

/// <summary>What to make of text that is not a plain number, boolean or quoted string.</summary>
internal enum CadNonLiteral
{
    /// <summary>It is a formula or a reference: keep it as read-only expression text (CAD expression files).</summary>
    Expression,

    /// <summary>It is data: keep it as a text value (a design-table cell, a CSV value).</summary>
    Text,
}

/// <summary>The classification of one right-hand side or cell.</summary>
internal sealed record CadValue(
    CadParameterKind Kind,
    double? Number = null,
    string? Unit = null,
    string? RawUnit = null,
    string? UnitProblem = null,
    string? Text = null,
    bool? Flag = null,
    string? Hint = null);

/// <summary>
/// Decides whether a CAD right-hand side is a literal or an expression, and never evaluates either. A literal is exactly
/// a number (written the way the declared number format writes numbers) optionally followed by a unit, a boolean word, or
/// a quoted string. Everything else — a reference, a function call, an operator, a sum — is an expression and is kept as
/// text.
/// </summary>
internal static class CadValueParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);
    private static readonly Regex UnitToken = new(@"^[\p{L}°µΩ%‰][\p{L}\p{N}°µΩ%‰^*·()/]*$", RegexOptions.CultureInvariant, RegexTimeout);

    public static CadValue Classify(string? rhs, NumberProfile np, string? declaredUnit = null, bool allowBoolean = true, bool quotedIsText = true, CadNonLiteral nonLiteral = CadNonLiteral.Expression)
    {
        var s = (rhs ?? string.Empty).Trim();

        if (s.Length == 0)
            return new CadValue(CadParameterKind.Text, Text: string.Empty);

        if (quotedIsText && s.Length >= 2 && s[0] == '"' && s[^1] == '"' && s.IndexOf('"', 1) == s.Length - 1)
            return new CadValue(CadParameterKind.Text, Text: s[1..^1]);

        if (allowBoolean && TryBoolean(s) is { } flag)
            return new CadValue(CadParameterKind.Boolean, Flag: flag);

        var (token, rest) = SplitNumberPrefix(s, np);

        if (token.Length > 0 && LocaleNumber.TryParse(token, np, out var number, out _, out var grouped))
        {
            var groupNote = grouped ? $"'{Clip(token)}' was read as {Engine.Values.CalcValue.FormatNumber(number, 12)}: the number format declares '{np.Grouping}' as a thousands separator. Check that is what the file means." : null;

            if (rest.Length == 0)
            {
                if (declaredUnit is { Length: > 0 })
                    return WithUnit(number, declaredUnit) with { Hint = groupNote };

                return new CadValue(CadParameterKind.Number, Number: number, Hint: groupNote);
            }

            if (CadUnits.TryResolve(rest, out var symbol, out _))
            {
                // A unit written on the value and a different one declared for it (NX [mm]len=12 in): the value's own unit is used, and the conflict is said.
                var conflict = declaredUnit is { Length: > 0 } && CadUnits.TryResolve(declaredUnit, out var declaredSymbol, out _) && !string.Equals(declaredSymbol, symbol, StringComparison.Ordinal)
                    ? $"the declared unit [{declaredUnit.Trim()}] and the unit written on the value ({rest}) differ; the unit written on the value was used."
                    : null;

                return new CadValue(CadParameterKind.Number, Number: number, Unit: symbol.Length == 0 ? null : symbol, Hint: conflict ?? groupNote);
            }

            if (rest.Length <= CadUnits.MaxChars && IsUnitToken(rest))
                return new CadValue(CadParameterKind.Number, Number: number, RawUnit: rest, UnitProblem: $"The unit '{rest}' is not one the engine knows.");
        }

        var hint = LocaleNumber.LooksLikeOtherMark(s, np)
            ? $"'{Clip(s)}' is written with a {(np.Decimal == DecimalMark.Point ? "comma" : "point")}, but this file's numbers are declared to use a decimal {(np.Decimal == DecimalMark.Point ? "point" : "comma")}. Choose the other number format if that is wrong."
            : null;

        return nonLiteral == CadNonLiteral.Text && !s.StartsWith('=') && hint is null
            ? new CadValue(CadParameterKind.Text, Text: s)
            : new CadValue(CadParameterKind.Expression, Text: s, Hint: hint);
    }

    /// <summary>The value of <paramref name="number"/> in a declared unit (NX <c>[mm]</c>), with the unit resolved or reported.</summary>
    public static CadValue WithUnit(double number, string? unitText)
    {
        if (string.IsNullOrWhiteSpace(unitText))
            return new CadValue(CadParameterKind.Number, Number: number);

        return CadUnits.TryResolve(unitText, out var symbol, out _)
            ? new CadValue(CadParameterKind.Number, Number: number, Unit: symbol.Length == 0 ? null : symbol)
            : new CadValue(CadParameterKind.Number, Number: number, RawUnit: Clip(unitText.Trim()), UnitProblem: $"The unit '{Clip(unitText.Trim())}' is not one the engine knows.");
    }

    public static bool? TryBoolean(string s) => s.ToLowerInvariant() switch
    {
        "true" or "yes" => true,
        "false" or "no" => false,
        _ => null,
    };

    public static string Clip(string s) => s.Length > 60 ? s[..60] + "…" : s;

    private static bool IsUnitToken(string s)
    {
        try
        {
            return UnitToken.IsMatch(s);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>Splits a leading number (sign, digits with the profile's marks, optional exponent) from the rest; the token is empty when the text does not start with one.</summary>
    private static (string Token, string Tail) SplitNumberPrefix(string s, NumberProfile np)
    {
        var i = 0;
        var n = s.Length;

        if (i < n && s[i] is '+' or '-')
            i++;

        var start = i;
        var mark = np.DecimalChar;

        while (i < n && (s[i] is >= '0' and <= '9' || s[i] == mark || (np.Grouping is { } g && s[i] == g && i > start)))
            i++;

        if (i == start)
            return (string.Empty, s);

        // A grouping character or a mark left dangling at the end belongs to the rest, not to the number.
        while (i > start && np.Grouping is { } trailing && s[i - 1] == trailing)
            i--;

        if (i < n && s[i] is 'e' or 'E')
        {
            var j = i + 1;

            if (j < n && s[j] is '+' or '-')
                j++;

            var k = j;

            while (k < n && s[k] is >= '0' and <= '9')
                k++;

            if (k > j)
                i = k;
        }

        return (s[..i], s[i..].Trim());
    }

    /// <summary>A name made safe to store and show: trimmed, Unicode-normalised, within the cap, with no control, invisible or private-use characters. Throws <see cref="ImportRefusedException"/> otherwise.</summary>
    public static string CleanName(string? raw, ImportLimits limits, string where)
    {
        var s = (raw ?? string.Empty).Trim();

        if (s.Length == 0)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"{where}: a parameter has no name.");

        if (s.Length > limits.MaxNameChars)
            throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"{where}: a parameter name is longer than {limits.MaxNameChars} characters.");

        string normal;

        try
        {
            normal = s.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            throw new ImportRefusedException(ImportRefusalCode.InvalidEncoding, $"{where}: a parameter name is not valid Unicode text.");
        }

        if (!IsWellFormedSurrogates(normal))
            throw new ImportRefusedException(ImportRefusalCode.InvalidEncoding, $"{where}: a parameter name is not valid Unicode text.");

        foreach (var c in normal)
        {
            if (char.IsSurrogate(c))
                continue;

            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (char.IsControl(c) || category is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
                throw new ImportRefusedException(ImportRefusalCode.ControlCharacter, $"{where}: a parameter name holds a control or invisible character (U+{(int)c:X4}).");
        }

        return normal;
    }

    private static bool IsWellFormedSurrogates(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]))
            {
                if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1]))
                    return false;

                i++;
            }
            else if (char.IsLowSurrogate(s[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Text from a file (a text value, a comment) made safe to store and show: invisible and line-break characters dropped, within the cap.</summary>
    public static string CleanText(string? raw, ImportLimits limits)
    {
        var s = raw ?? string.Empty;
        var sb = new StringBuilder(Math.Min(s.Length, limits.MaxFieldChars));

        foreach (var c in s)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (c is '\t' or '\r' or '\n')
            {
                sb.Append(' ');
                continue;
            }

            if (char.IsControl(c) || category is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                continue;

            sb.Append(c);

            if (sb.Length >= limits.MaxFieldChars)
                break;
        }

        return sb.ToString().Trim();
    }
}
