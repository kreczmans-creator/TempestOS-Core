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
    private static readonly string[] LinkMarkers = ["http://", "https://", "ftp://", "ftps://", "file:", "javascript:", "vbscript:", "data:", "mailto:", "tel:", "www.", "=hyperlink(", "=image(", "=importxml(", "=webservice("];

    /// <summary>Plain text for <paramref name="raw"/>, and whether anything was removed.</summary>
    public static string Clean(string? raw, int maxChars, out bool changed)
    {
        changed = false;

        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var withoutTags = StripTags(raw, ref changed);
        var sb = new StringBuilder(Math.Min(withoutTags.Length, maxChars + 16));
        var i = 0;

        while (i < withoutTags.Length)
        {
            var marker = LinkAt(withoutTags, i);

            if (marker > 0)
            {
                // Skip the whole link word.
                var end = i + marker;

                while (end < withoutTags.Length && !char.IsWhiteSpace(withoutTags[end]))
                    end++;

                sb.Append("[link removed]");
                changed = true;
                i = end;
                continue;
            }

            var c = withoutTags[i];
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (c is '\t' or '\r' or '\n')
            {
                sb.Append(' ');
            }
            else if (char.IsControl(c) || category is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
            {
                changed = true;
            }
            else
            {
                sb.Append(c);
            }

            i++;
        }

        var text = sb.ToString().Trim();

        try
        {
            text = text.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // A lone surrogate: keep only what is well formed.
            changed = true;
            text = new string(text.Where(static ch => !char.IsSurrogate(ch)).ToArray());
        }

        // A spreadsheet-significant first character is neutralised on export (CsvSafeWriter), not changed here.
        return text;
    }

    /// <summary>The length of the link marker that starts at <paramref name="i"/> (case-insensitive), or 0.</summary>
    private static int LinkAt(string s, int i)
    {
        foreach (var marker in LinkMarkers)
        {
            if (i + marker.Length <= s.Length && string.Compare(s, i, marker, 0, marker.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                // "www." and "data:" and "file:" inside a longer word are not links.
                if (i > 0 && char.IsLetterOrDigit(s[i - 1]) && marker is "www." or "data:" or "file:" or "tel:")
                    continue;

                return marker.Length;
            }
        }

        return 0;
    }

    /// <summary>Drops markup tags: a '&lt;' followed by a letter, '/', '!' or '?' up to the next '&gt;' within 200 characters. A bare '&lt;' (as in "&lt; 0.1") stays.</summary>
    private static string StripTags(string s, ref bool changed)
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
                    changed = true;
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
