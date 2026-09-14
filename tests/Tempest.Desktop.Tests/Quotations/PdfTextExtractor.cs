using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Tempest.Desktop.Tests.Quotations;

/// <summary>
/// A minimal text extractor for a PDF <see cref="Tempest.Desktop.Quotations.QuotationSheetRenderer"/>
/// (and <see cref="Tempest.Desktop.IssueSheets.IssueSheetRenderer"/>) produce
/// (`WP 19.5B`) — test-only, no package added. SkiaSharp's own PDF backend
/// draws text through an embedded, CID-keyed subset font: the content
/// stream's own <c>Tj</c>/<c>TJ</c> operators carry glyph ids, not literal
/// ASCII, so a raw byte search for the rendered words never matches (proven
/// empirically before writing this: it always returns nothing). What does
/// work, and is what every real PDF reader does for exactly this font
/// shape, is reading the glyph ids back through the PDF's own embedded
/// <c>ToUnicode</c> CMap (<c>beginbfchar</c>/<c>beginbfrange</c>) — small,
/// standard, and fully documented, so this needs no third-party PDF
/// library (the brief's own kill switch: no second PDF library).
/// </summary>
internal static class PdfTextExtractor
{
    /// <summary>
    /// Extracts every character SkiaSharp's own content stream(s) draw,
    /// concatenated across every <c>Tj</c>/<c>TJ</c> call found, each call
    /// separated by a space so two adjacent drawn strings never run
    /// together into one unintended word.
    /// </summary>
    public static string ExtractText(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        var cmap = new Dictionary<int, char>();
        var contentStreams = new List<string>();

        foreach (var raw in EnumerateFlateDecodedStreams(pdfBytes))
        {
            if (raw.Contains("beginbfchar", StringComparison.Ordinal) || raw.Contains("beginbfrange", StringComparison.Ordinal))
                ParseToUnicodeCMap(raw, cmap);
            else if (raw.Contains("BT", StringComparison.Ordinal) && (raw.Contains(" Tj", StringComparison.Ordinal) || raw.Contains(" TJ", StringComparison.Ordinal)))
                contentStreams.Add(raw);
        }

        var builder = new StringBuilder();
        var hexRun = new Regex("<([0-9A-Fa-f]+)>", RegexOptions.Compiled);

        foreach (var content in contentStreams)
        {
            foreach (Match match in hexRun.Matches(content))
            {
                var hex = match.Groups[1].Value;
                for (var i = 0; i + 4 <= hex.Length; i += 4)
                {
                    var code = int.Parse(hex.AsSpan(i, 4), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    builder.Append(cmap.TryGetValue(code, out var ch) ? ch : ' ');
                }

                builder.Append(' ');
            }
        }

        return builder.ToString();
    }

    /// <summary>Every <c>stream...endstream</c> block in <paramref name="pdfBytes"/> that successfully Zlib-inflates, decoded as Latin-1 (byte-for-byte — this format is never true text encoding, just a convenient string view over bytes).</summary>
    private static IEnumerable<string> EnumerateFlateDecodedStreams(byte[] pdfBytes)
    {
        var raw = Encoding.Latin1.GetString(pdfBytes);
        var index = 0;

        while (true)
        {
            var streamIdx = raw.IndexOf("stream", index, StringComparison.Ordinal);
            if (streamIdx < 0)
                yield break;

            var start = streamIdx + "stream".Length;
            if (start < raw.Length && raw[start] == '\r')
                start++;
            if (start < raw.Length && raw[start] == '\n')
                start++;

            var endIdx = raw.IndexOf("endstream", start, StringComparison.Ordinal);
            if (endIdx < 0)
                yield break;

            var slice = pdfBytes[start..Math.Min(endIdx, pdfBytes.Length)];
            index = endIdx + "endstream".Length;

            string? decoded = null;
            try
            {
                using var input = new MemoryStream(slice);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                decoded = Encoding.Latin1.GetString(output.ToArray());
            }
            catch (InvalidDataException)
            {
                // Not a Flate-encoded stream (a raw image, say) — skipped.
            }

            if (decoded is not null)
                yield return decoded;
        }
    }

    /// <summary>
    /// Parses a <c>ToUnicode</c> CMap's own <c>beginbfchar</c>/<c>beginbfrange</c>
    /// sections into <paramref name="into"/> — the standard PDF mechanism a
    /// real reader uses to map a CID-keyed font's glyph ids back to
    /// Unicode. Each section is matched only between its own
    /// <c>begin</c>/<c>end</c> markers — a <c>bfrange</c> triple's first two
    /// entries (<c>lo</c>, <c>hi</c>) would otherwise also satisfy the
    /// two-entry <c>bfchar</c> pattern, misreading a range's own upper bound
    /// as a destination character.
    /// </summary>
    private static void ParseToUnicodeCMap(string content, Dictionary<int, char> into)
    {
        foreach (Match section in Regex.Matches(content, @"beginbfchar(.*?)endbfchar", RegexOptions.Singleline | RegexOptions.Compiled))
        {
            foreach (Match m in Regex.Matches(section.Groups[1].Value, @"<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4,})>", RegexOptions.Compiled))
            {
                var code = ParseHex(m.Groups[1].Value);
                var unicode = ParseHex(m.Groups[2].Value[..4]);
                into[code] = (char)unicode;
            }
        }

        foreach (Match section in Regex.Matches(content, @"beginbfrange(.*?)endbfrange", RegexOptions.Singleline | RegexOptions.Compiled))
        {
            foreach (Match m in Regex.Matches(section.Groups[1].Value, @"<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4,})>", RegexOptions.Compiled))
            {
                var lo = ParseHex(m.Groups[1].Value);
                var hi = ParseHex(m.Groups[2].Value);
                var dstLo = ParseHex(m.Groups[3].Value[..4]);

                for (var code = lo; code <= hi; code++)
                    into[code] = (char)(dstLo + (code - lo));
            }
        }
    }

    private static int ParseHex(string value) =>
        int.Parse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
}
