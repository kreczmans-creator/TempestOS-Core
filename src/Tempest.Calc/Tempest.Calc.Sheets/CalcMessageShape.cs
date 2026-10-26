using System.Text.RegularExpressions;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, row 31: the one shape of a warning or block message, <c>&lt;symbol&gt;: &lt;one plain sentence&gt;.</c> The symbol is the input the
/// user changes, so the message tells them where to act. A failure that belongs to no single input (the model could not be solved) is
/// written under the symbol <see cref="CalculationSymbol"/>. This class wraps engine failures in that shape and tells a contract test
/// what is wrong with a message that is not in it.
/// </summary>
public static partial class CalcMessageShape
{
    /// <summary>The symbol of a message that belongs to the calculation as a whole and to no one input.</summary>
    public const string CalculationSymbol = "calculation";

    // One symbol, a table cell reference or an interpolation placeholder, then ": ". A symbol is letters (Latin or Greek), digits, '_', '.', ',' and
    // brackets for a table cell (segments[2].d); it holds no '/' and no space, so a message that names several symbols does not match.
    [GeneratedRegex(@"^(?<symbol>[\p{L}][\p{L}\p{N}_.,\[\]]*): ")]
    private static partial Regex SymbolPrefix();

    /// <summary>The symbol a message opens with, or <see langword="null"/> when it does not open with one.</summary>
    public static string? SymbolOf(string? message) => message is null ? null : SymbolPrefix().Match(message) is { Success: true } m ? m.Groups["symbol"].Value : null;

    /// <summary>Whether the message opens with <c>&lt;symbol&gt;: </c>.</summary>
    public static bool HasSymbolPrefix(string? message) => SymbolOf(message) is not null;

    /// <summary>
    /// An engine failure in the standard shape: a message that already opens with a symbol is kept (and ended with a full stop); any other is
    /// written under <see cref="CalculationSymbol"/>.
    /// </summary>
    public static string Engine(string? message)
    {
        var text = (message ?? string.Empty).Trim();

        if (text.Length == 0)
            text = "the calculation could not be completed";

        if (!HasSymbolPrefix(text))
            text = $"{CalculationSymbol}: {text}";

        return EndWithFullStop(text);
    }

    /// <summary>The message ended with a full stop (a message that ends in <c>.</c>, <c>!</c>, <c>?</c> or a closing quote after one is kept).</summary>
    public static string EndWithFullStop(string text)
    {
        var trimmed = text.TrimEnd();
        return trimmed.Length == 0 || trimmed[^1] is '.' or '!' or '?' or '…' ? trimmed : trimmed + ".";
    }

    /// <summary>
    /// What is wrong with <paramref name="message"/> against the standard shape, one short phrase each; empty when it conforms.
    /// An interpolation placeholder (<c>{missing}</c>) stands for a symbol, so a message the source writes with one is judged on the rest.
    /// </summary>
    public static IReadOnlyList<string> Problems(string? message)
    {
        var problems = new List<string>();
        var text = (message ?? string.Empty).Trim();

        if (text.Length == 0)
            return ["the message is empty"];

        // A placeholder opening the message is a symbol filled in at run time.
        var probe = text.StartsWith('{') && text.IndexOf("}: ", StringComparison.Ordinal) is var close and > 0 ? "x" + text[(close + 1)..] : text;

        if (!HasSymbolPrefix(probe))
        {
            var colon = probe.IndexOf(": ", StringComparison.Ordinal);
            problems.Add(colon > 0 && probe[..colon].Contains('/', StringComparison.Ordinal) ? "names several symbols before the colon" : "does not open with '<symbol>: '");
        }

        if (text[^1] is not ('.' or '!' or '?' or '…' or '}'))
            problems.Add("does not end with a full stop");

        if (text.Contains('\n', StringComparison.Ordinal))
            problems.Add("runs over more than one line");

        return problems;
    }
}
