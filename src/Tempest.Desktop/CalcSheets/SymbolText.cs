using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Sets a calculation's symbols as an engineer writes them (the owner's ask, 2026-10-24): what follows the first
/// underscore is a subscript (<c>D_1</c> → D₁, <c>F_M,min</c> → F with "M,min" set low), a further underscore reads as
/// a comma (<c>F_KR_req</c> → F with "KR,req"), and a Greek letter written out as a word is the letter
/// (<c>sigma_A</c> → σ with "A"). Presentation only: inputs, imports, exports and the plain-text sheet keep the
/// symbols exactly as entered.
/// </summary>
public static partial class SymbolText
{
    /// <summary>One piece of typeset text: on the line, or set low as a subscript.</summary>
    public readonly record struct Segment(string Text, bool Subscript);

    /// <summary>A subscript is set at this fraction of the text size.</summary>
    public const double SubscriptScale = 0.72;

    /// <summary>On screen a subscript is drawn this fraction of the text size below the line.</summary>
    public const double SubscriptDrop = 0.3;

    private static readonly Dictionary<string, string> Greek = new(StringComparer.Ordinal)
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε", ["eta"] = "η", ["theta"] = "θ",
        ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ", ["rho"] = "ρ", ["sigma"] = "σ", ["tau"] = "τ",
        ["phi"] = "φ", ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω",
        ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Sigma"] = "Σ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
    };

    // A symbol: a letter (Latin or Greek) not preceded by a letter, an underscore, a dot or '@' (so "mech.bolt_x" and
    // a reference are left alone), its letters and digits, then an optional subscript of letters and digits joined by
    // commas or underscores, or a decimal point before a digit (R_p0.2). ⊥ and ∥ may be a subscript too (a fillet
    // weld's F_⊥ and F_∥; the shots review found them printed with the underscore).
    // A combining mark (the bar of λ̄) belongs to the letter it sits on, so λ̄_LT is λ̄ with LT set low (the re-shoot found _LT printed as written).
    [GeneratedRegex(@"(?<![A-Za-zΑ-Ωα-ω_.@])([A-Za-zΑ-Ωα-ω]\p{Mn}*[A-Za-zΑ-Ωα-ω0-9\p{Mn}]*)(?:_([A-Za-zΑ-Ωα-ω0-9⊥∥]+(?:(?:[,_]|\.(?=[0-9]))[A-Za-zΑ-Ωα-ω0-9⊥∥]+)*))?")]
    private static partial Regex SymbolPattern();

    /// <summary>The pieces of <paramref name="text"/>, its symbols typeset; one plain piece when there is nothing to set.</summary>
    public static IReadOnlyList<Segment> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var segments = new List<Segment>();
        var line = new StringBuilder();
        var at = 0;

        foreach (Match m in SymbolPattern().Matches(text))
        {
            var word = m.Groups[1].Value;
            var subscript = m.Groups[2].Success ? m.Groups[2].Value.Replace('_', ',') : null;
            // A Greek name on its own is a symbol ("Phi"), part of a hyphenated word is not ("theta-less").
            var hyphenated = m.Index + m.Length < text.Length && text[m.Index + m.Length] == '-' || m.Index > 0 && text[m.Index - 1] == '-';
            var letter = subscript is null && hyphenated ? null : Greek.GetValueOrDefault(word);

            if (subscript is null && letter is null)
                continue;

            line.Append(text, at, m.Index - at).Append(letter ?? word);
            at = m.Index + m.Length;

            if (subscript is null)
                continue;

            segments.Add(new Segment(line.ToString(), false));
            line.Clear();
            segments.Add(new Segment(subscript, true));
        }

        line.Append(text, at, text.Length - at);

        if (line.Length > 0 || segments.Count == 0)
            segments.Add(new Segment(line.ToString(), false));

        return segments;
    }

    /// <summary>
    /// The pieces the form draws: <see cref="Parse"/>, with Unicode subscript digits (h₁, W₂) set low as ordinary digits too. The monospace working
    /// font has no glyphs for them and drew comma-like marks (the re-shoot found "h₁" reading "h,"). The PDF keeps <see cref="Parse"/>: its faces have them.
    /// </summary>
    public static IReadOnlyList<Segment> FormSegments(string? text)
    {
        var segments = new List<Segment>();

        foreach (var segment in Parse(text))
        {
            if (segment.Subscript || !segment.Text.Any(IsSubscriptDigit))
            {
                segments.Add(segment);
                continue;
            }

            var run = new StringBuilder();
            var low = false;

            foreach (var c in segment.Text)
            {
                if (IsSubscriptDigit(c) != low && run.Length > 0)
                {
                    segments.Add(new Segment(run.ToString(), low));
                    run.Clear();
                }

                low = IsSubscriptDigit(c);
                run.Append(low ? (char)('0' + (c - '\u2080')) : c);
            }

            if (run.Length > 0)
                segments.Add(new Segment(run.ToString(), low));
        }

        return segments;
    }

    private static bool IsSubscriptDigit(char c) => c is >= '\u2080' and <= '\u2089';

    /// <summary>Whether <paramref name="text"/> has anything to typeset (a subscript or a Greek name).</summary>
    public static bool HasSymbols(string? text)
    {
        var parsed = Parse(text);
        return parsed.Count > 1 || (parsed.Count == 1 && !string.Equals(parsed[0].Text, text, StringComparison.Ordinal));
    }

    /// <summary>The text as entered, kept on a block whose symbols are typeset (its <see cref="TextBlock.Text"/> is then empty).</summary>
    public static readonly AttachedProperty<string?> RawTextProperty = AvaloniaProperty.RegisterAttached<TextBlock, string?>("RawText", typeof(SymbolText));

    /// <summary>Shows <paramref name="text"/> on <paramref name="block"/> with its symbols typeset; plain text stays plain.</summary>
    public static void Set(TextBlock block, string? text)
    {
        ArgumentNullException.ThrowIfNull(block);
        block.SetValue(RawTextProperty, text);

        var segments = FormSegments(text);

        if (segments.Count == 0 || segments.Count == 1 && !segments[0].Subscript && string.Equals(segments[0].Text, text, StringComparison.Ordinal))
        {
            block.Inlines = null;
            block.Text = text;
            return;
        }

        var inlines = new InlineCollection();

        foreach (var segment in segments)
        {
            // A subscript run's own baseline alignment barely moves it at form sizes (the showcase screenshots showed
            // "FM" rather than F with M set low), so it is a small block of its own, drawn lower by a fixed share of the size.
            inlines.Add(segment.Subscript
                ? new InlineUIContainer(new TextBlock { Text = segment.Text, FontSize = block.FontSize * SubscriptScale, RenderTransform = new TranslateTransform(0, block.FontSize * SubscriptDrop) })
                : new Run(segment.Text));
        }

        block.Text = null;
        block.Inlines = inlines;
    }

    /// <summary>The text a block shows, as entered: its typeset text or its plain text.</summary>
    public static string Raw(TextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return block.GetValue(RawTextProperty) ?? block.Text ?? string.Empty;
    }
}
