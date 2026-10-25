using System.Globalization;
using System.Text;

namespace Tempest.Calc.Import.Scan;

/// <summary>
/// Text from a scan table (a feature name, a stage, a serial) reduced to plain text before it is stored or shown. The importer
/// passes no hyperlink and no HTML through: markup tags are dropped, a link (a URL scheme, <c>=HYPERLINK(</c>) is replaced by
/// "[link removed]", invisible and formatting characters are dropped, the text is Unicode-normalised and held to its cap. A change is
/// reported, never silent. Done by a plain scan, not a regular expression, so hostile text cannot cost more than its length.
/// </summary>
internal static class ScanText
{
    private static readonly string[] LinkMarkers = ["http://", "https://", "ftp://", "ftps://", "file:", "javascript:", "vbscript:", "data:", "mailto:", "tel:", "www."];
    private static readonly string[] FormulaNames = ["hyperlink", "image", "importxml", "importdata", "importhtml", "importrange", "webservice", "cmd", "dde"];

    /// <summary>Plain text for <paramref name="raw"/>, held to <paramref name="maxChars"/>, and whether anything was removed or cut.</summary>
    public static string Clean(string? raw, int maxChars, out bool changed)
    {
        changed = false;

        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        // The usual name — printable ASCII with nothing that starts markup, a link or a formula — is returned as it is, without a copy.
        if (raw.Length <= maxChars && IsPlain(raw))
            return raw;

        // 1. Invisible, formatting and control characters first, so a marker cannot be hidden by one (a zero-width space inside a URL scheme, a soft hyphen inside "javascript:").
        var sb = new StringBuilder(Math.Min(raw.Length, maxChars + 16));

        foreach (var c in raw)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (c is '\t' or '\r' or '\n')
                sb.Append(' ');
            else if (char.IsControl(c) || category is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
                changed = true;
            else
                sb.Append(c);
        }

        // 2. Markup tags, repeatedly: removing one tag can expose another ("<<b>script>").
        var text = sb.ToString();

        for (var pass = 0; pass < 8; pass++)
        {
            var stripped = StripTags(text);

            if (string.Equals(stripped, text, StringComparison.Ordinal))
                break;

            changed = true;
            text = stripped;

            if (pass == 7)
                text = text.Replace("<", string.Empty, StringComparison.Ordinal).Replace(">", string.Empty, StringComparison.Ordinal);
        }

        // 3. Links and spreadsheet link formulas.
        var result = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            var marker = LinkAt(text, i);

            if (marker > 0)
            {
                var end = i + marker;

                while (end < text.Length && !char.IsWhiteSpace(text[end]))
                    end++;

                result.Append("[link removed]");
                changed = true;
                i = end;
                continue;
            }

            result.Append(text[i]);
            i++;
        }

        var clean = result.ToString().Trim();

        try
        {
            clean = clean.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            changed = true;
            clean = new string(clean.Where(static ch => !char.IsSurrogate(ch)).ToArray());
        }

        // 4. The cap holds.
        if (clean.Length > maxChars)
        {
            changed = true;
            clean = clean[..maxChars];

            if (clean.Length > 0 && char.IsHighSurrogate(clean[^1]))
                clean = clean[..^1];
        }

        return clean;
    }

    private static bool IsPlain(string s)
    {
        if (s[0] == ' ' || s[^1] == ' ')
            return false;

        foreach (var c in s)
        {
            if (c is < ' ' or > '~' or '<' or '=' or ':')
                return false;
        }

        return s.IndexOf("www.", StringComparison.OrdinalIgnoreCase) < 0;
    }

    /// <summary>
    /// The length of the link marker that starts at <paramref name="i"/> (case-insensitive, and with the full-width forms of ASCII letters, digits and
    /// punctuation read as the ASCII they stand for: a spreadsheet reads ＝ and ｈｔｔｐ：／／ as =, http://), or 0.
    /// </summary>
    private static int LinkAt(string s, int i)
    {
        if (Fold(s[i]) == '=')
        {
            var j = i + 1;

            while (j < s.Length && Fold(s[j]) == ' ')
                j++;

            foreach (var name in FormulaNames)
            {
                if (MatchesAt(s, j, name))
                {
                    var k = j + name.Length;

                    while (k < s.Length && Fold(s[k]) == ' ')
                        k++;

                    if (k < s.Length && Fold(s[k]) is '(' or '|')
                        return k - i + 1;
                }
            }

            return 0;
        }

        foreach (var marker in LinkMarkers)
        {
            if (MatchesAt(s, i, marker))
            {
                // "www." and "data:" and "file:" inside a longer word are not links.
                if (i > 0 && char.IsLetterOrDigit(s[i - 1]) && marker is "www." or "data:" or "file:" or "tel:")
                    continue;

                return marker.Length;
            }
        }

        return 0;
    }

    private static bool MatchesAt(string s, int i, string marker)
    {
        if (i + marker.Length > s.Length)
            return false;

        for (var m = 0; m < marker.Length; m++)
        {
            if (char.ToLowerInvariant(Fold(s[i + m])) != marker[m])
                return false;
        }

        return true;
    }

    /// <summary>The ASCII a full-width form (U+FF01 to U+FF5E) or the ideographic space stands for; any other character as it is.</summary>
    private static char Fold(char c) => c switch
    {
        >= '\uFF01' and <= '\uFF5E' => (char)(c - 0xFEE0),
        '\u3000' => ' ',
        _ => c,
    };


    /// <summary>Drops markup tags: a '&lt;' followed by a letter, '/', '!' or '?' up to the next '&gt;' within 200 characters. A bare '&lt;' (as in "&lt; 0.1") stays.</summary>
    private static string StripTags(string s)
    {
        if (s.IndexOf('<', StringComparison.Ordinal) < 0)
            return s;

        var sb = new StringBuilder(s.Length);
        var i = 0;

        while (i < s.Length)
        {
            var c = s[i];

            if (c == '<' && i + 1 < s.Length && (char.IsAsciiLetter(s[i + 1]) || s[i + 1] is '/' or '!' or '?'))
            {
                var close = s.IndexOf('>', i + 1, Math.Min(200, s.Length - i - 1));

                if (close > 0)
                {
                    i = close + 1;
                    continue;
                }
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }
}
